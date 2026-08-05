using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime de las comandas canónicas.
///
/// - Mantiene índices O(1) por OrderId y OrderLineId.
/// - Crea agregados de forma atómica contra la carta 367A.
/// - Publica eventos después de cada mutación válida.
/// - Expone snapshots profundos para el futuro service.runtime.
/// - No utiliza Update, Find ni referencias persistentes a objetos de escena.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Orders/Canonical Order Service")]
public sealed class BistroBuilderCanonicalOrderService : MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderMenuOfferService offerService;

    [SerializeField]
    private BistroBuilderMenuSelectionService selectionService;

    [Header("Estado runtime")]

    [SerializeField]
    private long nextSequenceNumber = 1;

    [SerializeField]
    private List<BistroBuilderCanonicalOrder> orders =
        new List<BistroBuilderCanonicalOrder>();

    [Header("Depuración")]

    [SerializeField]
    private bool logChanges = true;

    private readonly Dictionary<string, BistroBuilderCanonicalOrder>
        byOrderId =
            new Dictionary<string, BistroBuilderCanonicalOrder>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, BistroBuilderCanonicalOrder>
        orderByLineId =
            new Dictionary<string, BistroBuilderCanonicalOrder>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderMenuItemRuntimeState> menuBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private readonly List<BistroBuilderMenuOfferItemSnapshot> offerBuffer =
        new List<BistroBuilderMenuOfferItemSnapshot>(32);

    private readonly List<string> orderableDishIds =
        new List<string>(32);

    private readonly List<string> normalizedCustomerBuffer =
        new List<string>(16);

    private readonly HashSet<string> uniqueCustomerBuffer =
        new HashSet<string>(StringComparer.Ordinal);

    private bool initialized;

    public event Action<BistroBuilderCanonicalOrderChangedEvent>
        OrdersChanged;

    public BistroBuilderRestaurantMenuService MenuService => menuService;
    public BistroBuilderMenuOfferService OfferService => offerService;
    public BistroBuilderMenuSelectionService SelectionService =>
        selectionService;
    public int OrderCount => orders != null ? orders.Count : 0;
    public int Revision { get; private set; }
    public long NextSequenceNumber => nextSequenceNumber;

    private void Awake()
    {
        if (!RebuildRuntimeIndex(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService.";
            return false;
        }

        if (!menuService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (offerService != null &&
            !ReferenceEquals(offerService.MenuService, menuService))
        {
            error = "Comandas no comparte la oferta canónica 2.1C.";
            return false;
        }

        if (selectionService != null)
        {
            if (!selectionService.ValidateConfiguration(out error))
            {
                return false;
            }

            if (offerService == null ||
                !ReferenceEquals(
                    selectionService.OfferService,
                    offerService
                ))
            {
                error = "Comandas no comparte la selección canónica 2.1D.";
                return false;
            }
        }

        if (orders == null)
        {
            error = "La colección runtime de comandas es nula.";
            return false;
        }

        if (nextSequenceNumber < 1)
        {
            error = "La siguiente secuencia de comanda es inválida.";
            return false;
        }

        return ValidateOrderCollection(
            orders,
            true,
            out error
        );
    }

    public bool RebuildRuntimeIndex(out string error)
    {
        CacheDependenciesIfNeeded();

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService.";
            initialized = false;
            return false;
        }

        if (!menuService.ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        if (orders == null)
        {
            orders = new List<BistroBuilderCanonicalOrder>();
        }

        if (!TryBuildIndexes(
                orders,
                byOrderId,
                orderByLineId,
                out error
            ))
        {
            initialized = false;
            return false;
        }

        long highestSequence = 0;

        for (int index = 0; index < orders.Count; index++)
        {
            highestSequence = Math.Max(
                highestSequence,
                orders[index].SequenceNumber
            );
        }

        if (nextSequenceNumber <= highestSequence)
        {
            nextSequenceNumber = highestSequence + 1;
        }

        initialized = true;
        error = string.Empty;
        return true;
    }

    public BistroBuilderCanonicalOrderOperationResult TryCreateOrder(
        BistroBuilderCanonicalOrderCreationRequest request,
        out BistroBuilderCanonicalOrder createdSnapshot
    )
    {
        createdSnapshot = null;

        if (!EnsureInitialized(out string error))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason
                    .InvalidConfiguration,
                error
            );
        }

        MenuDishResolver resolver = new MenuDishResolver(
            menuService,
            offerService,
            request != null
                ? request.serviceMode
                : BistroBuilderServiceMode.TableService
        );

        if (!BistroBuilderCanonicalOrderFactory.TryCreate(
                request,
                resolver,
                nextSequenceNumber,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderOperationResult result
            ))
        {
            return result;
        }

        if (byOrderId.ContainsKey(order.OrderId))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.DuplicateOrderId,
                "Se generó una identidad de comanda duplicada."
            );
        }

        orders.Add(order);
        IndexOrder(order);
        nextSequenceNumber++;
        Revision++;
        createdSnapshot = order.Clone();

        PublishChange(
            BistroBuilderCanonicalOrderChangeType.OrderCreated,
            order.OrderId,
            string.Empty,
            "Comanda canónica creada."
        );

        return BistroBuilderCanonicalOrderOperationResult.Success(
            "Comanda canónica creada correctamente.",
            order.OrderId,
            string.Empty
        );
    }

    /// <summary>
    /// Crea una línea individual por cliente. Desde 2.1D delega la elección
    /// en la autoridad ponderada de carta; la rotación histórica permanece
    /// únicamente como compatibilidad para escenas anteriores al instalador.
    /// </summary>
    public BistroBuilderCanonicalOrderOperationResult
        TryCreateIndividualOrder(
            string tableReferenceId,
            string customerGroupReferenceId,
            IList<string> customerIds,
            BistroBuilderMealServiceAvailability mealService,
            int courseIndex,
            out BistroBuilderCanonicalOrder createdSnapshot
        )
    {
        return TryCreateIndividualOrder(
            string.Empty,
            tableReferenceId,
            customerGroupReferenceId,
            customerIds,
            mealService,
            courseIndex,
            out createdSnapshot
        );
    }

    /// <summary>
    /// Crea una comanda individual conservando una referencia externa
    /// estable. 367C la utiliza para enlazar el OrderId legacy sin convertirlo
    /// en la identidad canónica de la comanda.
    /// </summary>
    public BistroBuilderCanonicalOrderOperationResult
        TryCreateIndividualOrder(
            string externalReferenceId,
            string tableReferenceId,
            string customerGroupReferenceId,
            IList<string> customerIds,
            BistroBuilderMealServiceAvailability mealService,
            int courseIndex,
            out BistroBuilderCanonicalOrder createdSnapshot
        )
    {
        createdSnapshot = null;

        if (customerIds == null || customerIds.Count == 0)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "Debe indicarse al menos un cliente."
            );
        }

        if (!TryBuildOrderableDishList(
                mealService,
                out string error
            ))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.NoOrderableDishes,
                error
            );
        }

        BistroBuilderCanonicalOrderCreationRequest request =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                externalReferenceId = externalReferenceId,
                tableReferenceId = tableReferenceId,
                customerGroupReferenceId = customerGroupReferenceId,
                mealService = mealService
            };

        normalizedCustomerBuffer.Clear();
        uniqueCustomerBuffer.Clear();

        // Valida el conjunto completo antes de publicar la primera decisión
        // 2.1D. Así una referencia inválida o duplicada nunca deja una
        // selección parcial observable en telemetría.
        for (int index = 0; index < customerIds.Count; index++)
        {
            string customerId = BistroBuilderOrderIdUtility.Normalize(
                customerIds[index]
            );

            if (!BistroBuilderOrderIdUtility.IsValid(customerId))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .InvalidReferenceId,
                    "Existe un CustomerId inválido."
                );
            }

            if (!uniqueCustomerBuffer.Add(customerId))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .DuplicateReferenceId,
                    "El mismo cliente aparece más de una vez."
                );
            }

            normalizedCustomerBuffer.Add(customerId);
        }

        for (int index = 0;
             index < normalizedCustomerBuffer.Count;
             index++)
        {
            string customerId = normalizedCustomerBuffer[index];
            string dishId;

            if (selectionService != null)
            {
                BistroBuilderMenuSelectionContext selectionContext =
                    new BistroBuilderMenuSelectionContext(
                        mealService,
                        BistroBuilderServiceMode.TableService,
                        customerId,
                        courseIndex,
                        index,
                        index
                    );

                if (!selectionService.TrySelectFromCandidates(
                        selectionContext,
                        offerBuffer,
                        null,
                        out BistroBuilderMenuSelectionResult selection,
                        out string selectionError
                    ))
                {
                    return Failure(
                        BistroBuilderCanonicalOrderFailureReason
                            .NoOrderableDishes,
                        selectionError
                    );
                }

                dishId = selection.DishId;
            }
            else
            {
                dishId = orderableDishIds[
                    index % orderableDishIds.Count
                ];
            }

            request.lines.Add(
                new BistroBuilderCanonicalOrderLineRequest(
                    dishId,
                    customerId,
                    new[] { customerId },
                    courseIndex
                )
            );
        }

        return TryCreateOrder(request, out createdSnapshot);
    }

    public BistroBuilderCanonicalOrderOperationResult TryTransitionLine(
        string lineId,
        BistroBuilderCanonicalOrderLineState target,
        string actorReferenceId
    )
    {
        if (!EnsureInitialized(out string error))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason
                    .InvalidConfiguration,
                error
            );
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(lineId);

        if (!orderByLineId.TryGetValue(
                normalizedLineId,
                out BistroBuilderCanonicalOrder order
            ))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.LineNotFound,
                "No existe una línea con esa identidad."
            );
        }

        if (!order.TryTransitionLine(
                normalizedLineId,
                target,
                actorReferenceId,
                out error
            ))
        {
            return Failure(
                order.IsTerminal
                    ? BistroBuilderCanonicalOrderFailureReason
                        .OrderAlreadyTerminal
                    : BistroBuilderCanonicalOrderFailureReason
                        .InvalidTransition,
                error,
                order.OrderId,
                normalizedLineId
            );
        }

        Revision++;

        PublishChange(
            BistroBuilderCanonicalOrderChangeType.LineStateChanged,
            order.OrderId,
            normalizedLineId,
            "Estado de línea actualizado a " + target + "."
        );

        return BistroBuilderCanonicalOrderOperationResult.Success(
            "Estado de línea actualizado.",
            order.OrderId,
            normalizedLineId
        );
    }

    /// <summary>
    /// Consume de forma atómica un conjunto concreto de líneas servidas.
    ///
    /// 367E utiliza esta operación cuando un cliente termina un pase. Las
    /// líneas compartidas solo se incluyen cuando todos sus consumidores han
    /// registrado el consumo en BistroBuilderCustomerDiningService.
    ///
    /// La comanda original se sustituye únicamente después de validar una
    /// copia profunda completa, evitando consumos parciales si una línea es
    /// inválida o cambia de estado durante la operación.
    /// </summary>
    public BistroBuilderCanonicalOrderOperationResult TryConsumeServedLines(
        string orderId,
        IList<string> lineIds,
        string actorReferenceId
    )
    {
        if (!TryResolveOrder(
                orderId,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderOperationResult failure
            ))
        {
            return failure;
        }

        if (lineIds == null || lineIds.Count == 0)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "Debe indicarse al menos una línea servida.",
                order.OrderId,
                string.Empty
            );
        }

        if (order.State == BistroBuilderCanonicalOrderState.Cancelled ||
            order.State == BistroBuilderCanonicalOrderState.Failed)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderAlreadyTerminal,
                "La comanda está cancelada o fallida.",
                order.OrderId,
                string.Empty
            );
        }

        HashSet<string> uniqueLineIds =
            new HashSet<string>(StringComparer.Ordinal);
        BistroBuilderCanonicalOrder candidate = order.Clone();
        bool changed = false;
        string firstChangedLineId = string.Empty;

        for (int index = 0; index < lineIds.Count; index++)
        {
            string normalizedLineId =
                BistroBuilderOrderIdUtility.Normalize(lineIds[index]);

            if (!BistroBuilderOrderIdUtility.IsValid(normalizedLineId))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason.InvalidLineId,
                    "La operación contiene un LineId inválido.",
                    order.OrderId,
                    normalizedLineId
                );
            }

            if (!uniqueLineIds.Add(normalizedLineId))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .DuplicateLineId,
                    "La operación contiene un LineId duplicado.",
                    order.OrderId,
                    normalizedLineId
                );
            }

            if (!candidate.TryGetLine(
                    normalizedLineId,
                    out BistroBuilderCanonicalOrderLine line
                ) ||
                line == null)
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason.LineNotFound,
                    "La línea no pertenece a la comanda indicada.",
                    order.OrderId,
                    normalizedLineId
                );
            }

            if (line.State == BistroBuilderCanonicalOrderLineState.Consumed)
            {
                continue;
            }

            if (line.State != BistroBuilderCanonicalOrderLineState.Served)
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .InvalidTransition,
                    "La línea " + normalizedLineId +
                    " no está servida; su estado es " + line.State + ".",
                    order.OrderId,
                    normalizedLineId
                );
            }

            if (!candidate.TryTransitionLine(
                    normalizedLineId,
                    BistroBuilderCanonicalOrderLineState.Consumed,
                    actorReferenceId,
                    out string transitionError
                ))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .InvalidTransition,
                    transitionError,
                    order.OrderId,
                    normalizedLineId
                );
            }

            if (string.IsNullOrEmpty(firstChangedLineId))
            {
                firstChangedLineId = normalizedLineId;
            }

            changed = true;
        }

        if (!changed)
        {
            return BistroBuilderCanonicalOrderOperationResult.Success(
                "Las líneas indicadas ya estaban consumidas.",
                order.OrderId,
                string.Empty
            );
        }

        if (!candidate.TryValidate(out string validationError))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidSnapshot,
                validationError,
                order.OrderId,
                firstChangedLineId
            );
        }

        int orderIndex = orders.IndexOf(order);

        if (orderIndex < 0)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderNotFound,
                "La comanda no figura en la colección runtime.",
                order.OrderId,
                firstChangedLineId
            );
        }

        UnindexOrder(order);
        orders[orderIndex] = candidate;
        IndexOrder(candidate);
        Revision++;

        PublishChange(
            BistroBuilderCanonicalOrderChangeType.LineStateChanged,
            candidate.OrderId,
            uniqueLineIds.Count == 1 ? firstChangedLineId : string.Empty,
            uniqueLineIds.Count == 1
                ? "Línea consumida individualmente."
                : "Líneas consumidas atómicamente por un cliente."
        );

        return BistroBuilderCanonicalOrderOperationResult.Success(
            "Consumo de líneas aplicado atómicamente.",
            candidate.OrderId,
            uniqueLineIds.Count == 1 ? firstChangedLineId : string.Empty
        );
    }

    /// <summary>
    /// Avanza todas las líneas no terminales de una comanda hasta un mismo
    /// estado de la ruta normal.
    ///
    /// La operación se realiza sobre una copia profunda y sustituye el
    /// agregado original únicamente después de validarlo. Por tanto, un fallo
    /// en una sola línea no deja la comanda parcialmente actualizada.
    ///
    /// Este método existe para la migración 367C del flujo coarse. Los
    /// sistemas definitivos de cocina y entrega continuarán utilizando
    /// TryTransitionLine para procesar cada plato individualmente.
    /// </summary>
    public BistroBuilderCanonicalOrderOperationResult
        TryAdvanceAllLinesToState(
            string orderId,
            BistroBuilderCanonicalOrderLineState target,
            string actorReferenceId
        )
    {
        if (!TryResolveOrder(
                orderId,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderOperationResult failure
            ))
        {
            return failure;
        }

        int targetValue = (int)target;

        if (targetValue <
                (int)BistroBuilderCanonicalOrderLineState.Draft ||
            targetValue >
                (int)BistroBuilderCanonicalOrderLineState.Consumed)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidTransition,
                "El destino no pertenece a la ruta normal de una línea.",
                order.OrderId,
                string.Empty
            );
        }

        if (order.IsTerminal)
        {
            bool alreadyCompleted =
                target == BistroBuilderCanonicalOrderLineState.Consumed &&
                order.State == BistroBuilderCanonicalOrderState.Completed;

            if (alreadyCompleted)
            {
                return BistroBuilderCanonicalOrderOperationResult.Success(
                    "La comanda ya se encuentra completada.",
                    order.OrderId,
                    string.Empty
                );
            }

            return Failure(
                BistroBuilderCanonicalOrderFailureReason
                    .OrderAlreadyTerminal,
                "La comanda ya está en un estado terminal.",
                order.OrderId,
                string.Empty
            );
        }

        BistroBuilderCanonicalOrder candidate = order.Clone();
        bool changed = false;

        for (int lineIndex = 0;
             lineIndex < candidate.Lines.Count;
             lineIndex++)
        {
            BistroBuilderCanonicalOrderLine line =
                candidate.Lines[lineIndex];

            if (line == null)
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .InvalidSnapshot,
                    "La comanda contiene una línea nula.",
                    order.OrderId,
                    string.Empty
                );
            }

            if (line.State == target)
            {
                continue;
            }

            if (line.IsTerminal ||
                (int)line.State > targetValue)
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .InvalidTransition,
                    "La línea " + line.LineId +
                    " no puede retroceder de " + line.State +
                    " a " + target + ".",
                    order.OrderId,
                    line.LineId
                );
            }

            while (line.State != target)
            {
                if (!BistroBuilderCanonicalOrderTransitionPolicy
                        .TryGetNormalNextState(
                            line.State,
                            out BistroBuilderCanonicalOrderLineState next
                        ) ||
                    (int)next > targetValue)
                {
                    return Failure(
                        BistroBuilderCanonicalOrderFailureReason
                            .InvalidTransition,
                        "No existe una ruta normal desde " +
                        line.State + " hasta " + target + ".",
                        order.OrderId,
                        line.LineId
                    );
                }

                if (!candidate.TryTransitionLine(
                        line.LineId,
                        next,
                        actorReferenceId,
                        out string transitionError
                    ))
                {
                    return Failure(
                        BistroBuilderCanonicalOrderFailureReason
                            .InvalidTransition,
                        transitionError,
                        order.OrderId,
                        line.LineId
                    );
                }

                changed = true;
            }
        }

        if (!changed)
        {
            return BistroBuilderCanonicalOrderOperationResult.Success(
                "Todas las líneas ya se encuentran en el estado solicitado.",
                order.OrderId,
                string.Empty
            );
        }

        if (!candidate.TryValidate(out string validationError))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidSnapshot,
                validationError,
                order.OrderId,
                string.Empty
            );
        }

        BistroBuilderCanonicalOrderState expectedAggregate =
            GetExpectedAggregateForUniformLineState(target);

        if (candidate.State != expectedAggregate)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidSnapshot,
                "El estado agregado " + candidate.State +
                " no coincide con el destino uniforme " + target + ".",
                order.OrderId,
                string.Empty
            );
        }

        int orderIndex = orders.IndexOf(order);

        if (orderIndex < 0)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderNotFound,
                "La comanda no figura en la colección runtime.",
                order.OrderId,
                string.Empty
            );
        }

        UnindexOrder(order);
        orders[orderIndex] = candidate;
        IndexOrder(candidate);
        Revision++;

        PublishChange(
            BistroBuilderCanonicalOrderChangeType.LineStateChanged,
            candidate.OrderId,
            string.Empty,
            "Todas las líneas se avanzaron atómicamente a " +
            target + "."
        );

        return BistroBuilderCanonicalOrderOperationResult.Success(
            "Comanda avanzada atómicamente.",
            candidate.OrderId,
            string.Empty
        );
    }

    /// <summary>
    /// Somete todas las líneas Draft de una comanda y libera a Queued
    /// únicamente las líneas del pase indicado.
    ///
    /// La operación se ejecuta sobre una copia profunda. Los pases futuros
    /// permanecen Submitted y no pueden entrar en cocina hasta una liberación
    /// posterior de 367F.
    /// </summary>
    public BistroBuilderCanonicalOrderOperationResult
        TrySubmitOrderAndReleaseCourse(
            string orderId,
            int courseIndex,
            string actorReferenceId
        )
    {
        if (!BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(
                courseIndex
            ))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "El pase inicial indicado no es válido."
            );
        }

        if (!TryResolveOrder(
                orderId,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderOperationResult failure
            ))
        {
            return failure;
        }

        if (order.IsTerminal)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderAlreadyTerminal,
                "La comanda ya está en un estado terminal.",
                order.OrderId
            );
        }

        BistroBuilderCanonicalOrder candidate = order.Clone();
        bool foundCourse = false;
        bool changed = false;
        string firstReleasedLineId = string.Empty;
        int releasedCount = 0;

        for (int index = 0; index < candidate.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = candidate.Lines[index];

            if (line == null)
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason.InvalidSnapshot,
                    "La comanda contiene una línea nula.",
                    order.OrderId
                );
            }

            if (line.State == BistroBuilderCanonicalOrderLineState.Draft)
            {
                if (!candidate.TryTransitionLine(
                        line.LineId,
                        BistroBuilderCanonicalOrderLineState.Submitted,
                        actorReferenceId,
                        out string submitError
                    ))
                {
                    return Failure(
                        BistroBuilderCanonicalOrderFailureReason
                            .InvalidTransition,
                        submitError,
                        order.OrderId,
                        line.LineId
                    );
                }

                changed = true;
            }

            if (line.CourseIndex != courseIndex || line.IsTerminal)
            {
                continue;
            }

            foundCourse = true;

            if (line.State == BistroBuilderCanonicalOrderLineState.Submitted)
            {
                if (!candidate.TryTransitionLine(
                        line.LineId,
                        BistroBuilderCanonicalOrderLineState.Queued,
                        actorReferenceId,
                        out string releaseError
                    ))
                {
                    return Failure(
                        BistroBuilderCanonicalOrderFailureReason
                            .InvalidTransition,
                        releaseError,
                        order.OrderId,
                        line.LineId
                    );
                }

                if (releasedCount == 0)
                {
                    firstReleasedLineId = line.LineId;
                }

                releasedCount++;
                changed = true;
            }
        }

        if (!foundCourse)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "La comanda no contiene líneas activas en el pase indicado.",
                order.OrderId
            );
        }

        if (!candidate.TryValidate(out string validationError))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidSnapshot,
                validationError,
                order.OrderId
            );
        }

        if (!changed)
        {
            return BistroBuilderCanonicalOrderOperationResult.Success(
                "La comanda ya estaba sometida y el pase ya estaba liberado.",
                order.OrderId,
                string.Empty
            );
        }

        int orderIndex = orders.IndexOf(order);

        if (orderIndex < 0)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderNotFound,
                "La comanda no figura en la colección runtime.",
                order.OrderId
            );
        }

        UnindexOrder(order);
        orders[orderIndex] = candidate;
        IndexOrder(candidate);
        Revision++;

        PublishChange(
            BistroBuilderCanonicalOrderChangeType.LineStateChanged,
            candidate.OrderId,
            releasedCount == 1 ? firstReleasedLineId : string.Empty,
            "Comanda sometida y pase " + courseIndex +
            " liberado atómicamente."
        );

        return BistroBuilderCanonicalOrderOperationResult.Success(
            "Comanda sometida y pase inicial liberado.",
            candidate.OrderId,
            releasedCount == 1 ? firstReleasedLineId : string.Empty
        );
    }

    /// <summary>
    /// Libera atómicamente un conjunto explícito de líneas Submitted.
    ///
    /// Un LineId repetido o ajeno rechaza la operación completa. Las líneas
    /// que ya superaron Queued se aceptan de forma idempotente, pero una línea
    /// Draft indica que la comanda todavía no fue sometida.
    /// </summary>
    public BistroBuilderCanonicalOrderOperationResult
        TryReleaseSubmittedLines(
            string orderId,
            IList<string> lineIds,
            string actorReferenceId
        )
    {
        if (!TryResolveOrder(
                orderId,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderOperationResult failure
            ))
        {
            return failure;
        }

        if (lineIds == null || lineIds.Count == 0)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "Debe indicarse al menos un LineId para liberar.",
                order.OrderId
            );
        }

        if (order.IsTerminal)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderAlreadyTerminal,
                "La comanda ya está en un estado terminal.",
                order.OrderId
            );
        }

        HashSet<string> uniqueLineIds =
            new HashSet<string>(StringComparer.Ordinal);
        List<string> normalizedLineIds = new List<string>(lineIds.Count);

        for (int index = 0; index < lineIds.Count; index++)
        {
            string lineId = BistroBuilderOrderIdUtility.Normalize(
                lineIds[index]
            );

            if (!BistroBuilderOrderIdUtility.IsValid(lineId))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason.InvalidLineId,
                    "La liberación contiene un LineId inválido.",
                    order.OrderId,
                    lineId
                );
            }

            if (!uniqueLineIds.Add(lineId))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .DuplicateLineId,
                    "La liberación contiene un LineId duplicado.",
                    order.OrderId,
                    lineId
                );
            }

            normalizedLineIds.Add(lineId);
        }

        BistroBuilderCanonicalOrder candidate = order.Clone();
        bool changed = false;
        string firstChangedLineId = string.Empty;
        int changedCount = 0;

        for (int index = 0; index < normalizedLineIds.Count; index++)
        {
            string lineId = normalizedLineIds[index];

            if (!candidate.TryGetLine(
                    lineId,
                    out BistroBuilderCanonicalOrderLine line
                ) ||
                line == null)
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason.LineNotFound,
                    "Una línea de liberación no pertenece a la comanda.",
                    order.OrderId,
                    lineId
                );
            }

            if (line.State == BistroBuilderCanonicalOrderLineState.Draft)
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason.InvalidTransition,
                    "La línea " + lineId +
                    " continúa en Draft y no puede liberarse.",
                    order.OrderId,
                    lineId
                );
            }

            if (line.State != BistroBuilderCanonicalOrderLineState.Submitted)
            {
                // Idempotencia: una línea ya liberada o resuelta no se muta.
                continue;
            }

            if (!candidate.TryTransitionLine(
                    lineId,
                    BistroBuilderCanonicalOrderLineState.Queued,
                    actorReferenceId,
                    out string transitionError
                ))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason.InvalidTransition,
                    transitionError,
                    order.OrderId,
                    lineId
                );
            }

            if (changedCount == 0)
            {
                firstChangedLineId = lineId;
            }

            changedCount++;
            changed = true;
        }

        if (!candidate.TryValidate(out string validationError))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidSnapshot,
                validationError,
                order.OrderId
            );
        }

        if (!changed)
        {
            return BistroBuilderCanonicalOrderOperationResult.Success(
                "Todas las líneas indicadas ya estaban liberadas.",
                order.OrderId,
                normalizedLineIds.Count == 1
                    ? normalizedLineIds[0]
                    : string.Empty
            );
        }

        int orderIndex = orders.IndexOf(order);

        if (orderIndex < 0)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderNotFound,
                "La comanda no figura en la colección runtime.",
                order.OrderId
            );
        }

        UnindexOrder(order);
        orders[orderIndex] = candidate;
        IndexOrder(candidate);
        Revision++;

        PublishChange(
            BistroBuilderCanonicalOrderChangeType.LineStateChanged,
            candidate.OrderId,
            changedCount == 1 ? firstChangedLineId : string.Empty,
            "Líneas Submitted liberadas atómicamente hacia cocina."
        );

        return BistroBuilderCanonicalOrderOperationResult.Success(
            "Líneas liberadas correctamente.",
            candidate.OrderId,
            changedCount == 1 ? firstChangedLineId : string.Empty
        );
    }

    /// <summary>
    /// Consume atómicamente todas las líneas servidas de una comanda.
    ///
    /// Las líneas canceladas o ya consumidas se conservan. Cualquier línea
    /// todavía en cocina, reparto o pase rechaza la operación completa.
    /// </summary>
    public BistroBuilderCanonicalOrderOperationResult TryCompleteServedOrder(
        string orderId,
        string actorReferenceId
    )
    {
        if (!TryResolveOrder(
                orderId,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderOperationResult failure
            ))
        {
            return failure;
        }

        if (order.State == BistroBuilderCanonicalOrderState.Completed)
        {
            return BistroBuilderCanonicalOrderOperationResult.Success(
                "La comanda ya se encuentra completada.",
                order.OrderId,
                string.Empty
            );
        }

        if (order.IsTerminal)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason
                    .OrderAlreadyTerminal,
                "La comanda ya está en un estado terminal.",
                order.OrderId,
                string.Empty
            );
        }

        BistroBuilderCanonicalOrder candidate = order.Clone();
        bool consumedAny = false;

        for (int index = 0; index < candidate.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = candidate.Lines[index];

            if (line == null)
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason.InvalidSnapshot,
                    "La comanda contiene una línea nula.",
                    order.OrderId,
                    string.Empty
                );
            }

            if (line.State == BistroBuilderCanonicalOrderLineState.Consumed ||
                line.State == BistroBuilderCanonicalOrderLineState.Cancelled)
            {
                continue;
            }

            if (line.State != BistroBuilderCanonicalOrderLineState.Served)
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .InvalidTransition,
                    "La línea " + line.LineId +
                    " todavía no está servida.",
                    order.OrderId,
                    line.LineId
                );
            }

            if (!candidate.TryTransitionLine(
                    line.LineId,
                    BistroBuilderCanonicalOrderLineState.Consumed,
                    actorReferenceId,
                    out string transitionError
                ))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .InvalidTransition,
                    transitionError,
                    order.OrderId,
                    line.LineId
                );
            }

            consumedAny = true;
        }

        if (!consumedAny)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.NoChange,
                "La comanda no contiene líneas servidas pendientes de consumo.",
                order.OrderId,
                string.Empty
            );
        }

        if (!candidate.TryValidate(out string validationError))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidSnapshot,
                validationError,
                order.OrderId,
                string.Empty
            );
        }

        if (candidate.State != BistroBuilderCanonicalOrderState.Completed)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidSnapshot,
                "La comanda no alcanzó el estado Completed.",
                order.OrderId,
                string.Empty
            );
        }

        int orderIndex = orders.IndexOf(order);

        if (orderIndex < 0)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderNotFound,
                "La comanda no figura en la colección runtime.",
                order.OrderId,
                string.Empty
            );
        }

        UnindexOrder(order);
        orders[orderIndex] = candidate;
        IndexOrder(candidate);
        Revision++;

        PublishChange(
            BistroBuilderCanonicalOrderChangeType.LineStateChanged,
            candidate.OrderId,
            string.Empty,
            "Todas las líneas servidas se consumieron atómicamente."
        );

        return BistroBuilderCanonicalOrderOperationResult.Success(
            "Comanda completada atómicamente.",
            candidate.OrderId,
            string.Empty
        );
    }

    public BistroBuilderCanonicalOrderOperationResult TryCancelOrder(
        string orderId,
        string actorReferenceId
    )
    {
        if (!TryResolveOrder(
                orderId,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderOperationResult failure
            ))
        {
            return failure;
        }

        if (!order.TryCancel(actorReferenceId, out string error))
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason
                    .OrderAlreadyTerminal,
                error,
                order.OrderId,
                string.Empty
            );
        }

        Revision++;

        PublishChange(
            BistroBuilderCanonicalOrderChangeType.OrderCancelled,
            order.OrderId,
            string.Empty,
            "Comanda cancelada."
        );

        return BistroBuilderCanonicalOrderOperationResult.Success(
            "Comanda cancelada.",
            order.OrderId,
            string.Empty
        );
    }

    public BistroBuilderCanonicalOrderOperationResult TryRemoveTerminalOrder(
        string orderId
    )
    {
        if (!TryResolveOrder(
                orderId,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderOperationResult failure
            ))
        {
            return failure;
        }

        if (!order.IsTerminal)
        {
            return Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderNotTerminal,
                "Solo pueden retirarse comandas terminales.",
                order.OrderId,
                string.Empty
            );
        }

        UnindexOrder(order);
        orders.Remove(order);
        Revision++;

        PublishChange(
            BistroBuilderCanonicalOrderChangeType.OrderRemoved,
            order.OrderId,
            string.Empty,
            "Comanda terminal retirada del runtime."
        );

        return BistroBuilderCanonicalOrderOperationResult.Success(
            "Comanda terminal retirada.",
            order.OrderId,
            string.Empty
        );
    }

    public bool TryGetOrderSnapshot(
        string orderId,
        out BistroBuilderCanonicalOrder snapshot
    )
    {
        snapshot = null;

        if (!EnsureInitialized(out _))
        {
            return false;
        }

        string normalizedOrderId =
            BistroBuilderOrderIdUtility.Normalize(orderId);

        if (!byOrderId.TryGetValue(
                normalizedOrderId,
                out BistroBuilderCanonicalOrder order
            ))
        {
            return false;
        }

        snapshot = order.Clone();
        return true;
    }

    public bool TryGetOrderAndLineSnapshot(
        string orderId,
        string lineId,
        out BistroBuilderCanonicalOrder orderSnapshot,
        out BistroBuilderCanonicalOrderLine lineSnapshot
    )
    {
        orderSnapshot = null;
        lineSnapshot = null;

        if (!TryGetOrderSnapshot(orderId, out orderSnapshot))
        {
            return false;
        }

        if (orderSnapshot == null)
        {
            return false;
        }

        if (!orderSnapshot.TryGetLine(lineId, out lineSnapshot))
        {
            orderSnapshot = null;
            return false;
        }

        if (lineSnapshot == null)
        {
            orderSnapshot = null;
            return false;
        }

        return true;
    }

    public int CopyOrderSnapshotsTo(
        List<BistroBuilderCanonicalOrder> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();

        if (!EnsureInitialized(out _))
        {
            return 0;
        }

        for (int index = 0; index < orders.Count; index++)
        {
            destination.Add(orders[index].Clone());
        }

        destination.Sort(CompareOrders);
        return destination.Count;
    }

    public bool TryCaptureRuntimeSnapshot(
        out BistroBuilderCanonicalOrderRuntimeSnapshot snapshot,
        out string error
    )
    {
        snapshot = null;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        snapshot = new BistroBuilderCanonicalOrderRuntimeSnapshot(
            nextSequenceNumber,
            orders
        );

        if (!snapshot.TryValidate(out error))
        {
            snapshot = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Restauración atómica preparada para service.runtime.
    ///
    /// Valida una copia completa y las referencias DishId antes de sustituir
    /// el estado actual. No exige que el plato siga activo en la carta: una
    /// comanda ya aceptada debe poder recuperarse aunque la carta haya cambiado.
    /// </summary>
    public bool TryReplaceFromRuntimeSnapshot(
        BistroBuilderCanonicalOrderRuntimeSnapshot snapshot,
        bool notify,
        out string error
    )
    {
        if (snapshot == null)
        {
            error = "El snapshot de comandas es nulo.";
            return false;
        }

        if (!snapshot.TryValidate(out error))
        {
            return false;
        }

        CacheDependenciesIfNeeded();

        if (menuService == null || menuService.CatalogService == null)
        {
            error = "El catálogo de platos no está disponible.";
            return false;
        }

        List<BistroBuilderCanonicalOrder> candidates =
            new List<BistroBuilderCanonicalOrder>(snapshot.Orders.Count);

        for (int orderIndex = 0;
             orderIndex < snapshot.Orders.Count;
             orderIndex++)
        {
            BistroBuilderCanonicalOrder candidate =
                snapshot.Orders[orderIndex].Clone();

            for (int lineIndex = 0;
                 lineIndex < candidate.Lines.Count;
                 lineIndex++)
            {
                if (!menuService.CatalogService.TryGetDefinition(
                        candidate.Lines[lineIndex].DishId,
                        out _
                    ))
                {
                    error = "No existe la definición canónica del plato " +
                            candidate.Lines[lineIndex].DishId + ".";
                    return false;
                }
            }

            candidates.Add(candidate);
        }

        Dictionary<string, BistroBuilderCanonicalOrder> candidateByOrder =
            new Dictionary<string, BistroBuilderCanonicalOrder>(
                StringComparer.Ordinal
            );
        Dictionary<string, BistroBuilderCanonicalOrder> candidateByLine =
            new Dictionary<string, BistroBuilderCanonicalOrder>(
                StringComparer.Ordinal
            );

        if (!TryBuildIndexes(
                candidates,
                candidateByOrder,
                candidateByLine,
                out error
            ))
        {
            return false;
        }

        orders.Clear();
        orders.AddRange(candidates);

        byOrderId.Clear();
        orderByLineId.Clear();

        foreach (KeyValuePair<string, BistroBuilderCanonicalOrder> pair
                 in candidateByOrder)
        {
            byOrderId.Add(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, BistroBuilderCanonicalOrder> pair
                 in candidateByLine)
        {
            orderByLineId.Add(pair.Key, pair.Value);
        }

        nextSequenceNumber = snapshot.NextSequenceNumber;
        initialized = true;
        Revision++;

        if (notify)
        {
            PublishChange(
                BistroBuilderCanonicalOrderChangeType.StateRestored,
                string.Empty,
                string.Empty,
                "Estado runtime de comandas restaurado atómicamente."
            );
        }

        error = string.Empty;
        return true;
    }

    public void ClearAllOrders(bool notify)
    {
        if (orders == null)
        {
            orders = new List<BistroBuilderCanonicalOrder>();
        }

        orders.Clear();
        byOrderId.Clear();
        orderByLineId.Clear();
        nextSequenceNumber = 1;
        initialized = true;
        Revision++;

        if (notify)
        {
            PublishChange(
                BistroBuilderCanonicalOrderChangeType.AllOrdersCleared,
                string.Empty,
                string.Empty,
                "Todas las comandas runtime se han eliminado."
            );
        }
    }

    private bool TryBuildOrderableDishList(
        BistroBuilderMealServiceAvailability mealService,
        out string error
    )
    {
        orderableDishIds.Clear();

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (offerService != null)
        {
            offerBuffer.Clear();

            if (!offerService.TryGetOffer(
                    mealService,
                    BistroBuilderServiceMode.TableService,
                    false,
                    offerBuffer,
                    out error
                ))
            {
                return false;
            }

            for (int index = 0; index < offerBuffer.Count; index++)
            {
                BistroBuilderMenuOfferItemSnapshot item = offerBuffer[index];

                if (item.IsOrderable)
                {
                    orderableDishIds.Add(item.DishId);
                }
            }
        }
        else
        {
            // Compatibilidad defensiva para autotests antiguos y escenas
            // todavía no reparadas por 2.1C. Solo se evalúa el estado
            // persistente de carta; la modalidad se valida después contra la
            // definición y no se exige inventario runtime inicializado.
            menuBuffer.Clear();

            if (!menuService.TryGetSnapshot(menuBuffer, out error))
            {
                return false;
            }

            menuBuffer.Sort(CompareMenuItems);

            for (int index = 0; index < menuBuffer.Count; index++)
            {
                BistroBuilderMenuItemRuntimeState item = menuBuffer[index];

                if (item != null &&
                    menuService.IsDishEligibleByMenuState(
                        item.DishId,
                        mealService,
                        out _
                    ) &&
                    menuService.CatalogService.TryGetDefinition(
                        item.DishId,
                        out BistroBuilderDishDefinition definition
                    ) &&
                    definition.IsAvailableForServiceMode(
                        BistroBuilderServiceMode.TableService
                    ))
                {
                    orderableDishIds.Add(item.DishId);
                }
            }
        }

        if (orderableDishIds.Count == 0)
        {
            error = "No existe ningún plato pedible para el servicio " +
                    mealService + " en modalidad de mesa.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool EnsureInitialized(out string error)
    {
        if (initialized)
        {
            error = string.Empty;
            return true;
        }

        return RebuildRuntimeIndex(out error);
    }

    private bool TryResolveOrder(
        string orderId,
        out BistroBuilderCanonicalOrder order,
        out BistroBuilderCanonicalOrderOperationResult failure
    )
    {
        order = null;
        failure = default(BistroBuilderCanonicalOrderOperationResult);

        if (!EnsureInitialized(out string error))
        {
            failure = Failure(
                BistroBuilderCanonicalOrderFailureReason
                    .InvalidConfiguration,
                error
            );
            return false;
        }

        string normalized = BistroBuilderOrderIdUtility.Normalize(orderId);

        if (!byOrderId.TryGetValue(normalized, out order))
        {
            failure = Failure(
                BistroBuilderCanonicalOrderFailureReason.OrderNotFound,
                "No existe una comanda con esa identidad."
            );
            return false;
        }

        return true;
    }

    private bool ValidateOrderCollection(
        IList<BistroBuilderCanonicalOrder> source,
        bool validateDishDefinitions,
        out string error
    )
    {
        Dictionary<string, BistroBuilderCanonicalOrder> temporaryOrders =
            new Dictionary<string, BistroBuilderCanonicalOrder>(
                StringComparer.Ordinal
            );
        Dictionary<string, BistroBuilderCanonicalOrder> temporaryLines =
            new Dictionary<string, BistroBuilderCanonicalOrder>(
                StringComparer.Ordinal
            );

        if (!TryBuildIndexes(
                source,
                temporaryOrders,
                temporaryLines,
                out error
            ))
        {
            return false;
        }

        if (validateDishDefinitions)
        {
            for (int orderIndex = 0;
                 orderIndex < source.Count;
                 orderIndex++)
            {
                for (int lineIndex = 0;
                     lineIndex < source[orderIndex].Lines.Count;
                     lineIndex++)
                {
                    string dishId =
                        source[orderIndex].Lines[lineIndex].DishId;

                    if (menuService.CatalogService == null ||
                        !menuService.CatalogService.TryGetDefinition(
                            dishId,
                            out _
                        ))
                    {
                        error = "No existe la definición del plato " +
                                dishId + ".";
                        return false;
                    }
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryBuildIndexes(
        IList<BistroBuilderCanonicalOrder> source,
        Dictionary<string, BistroBuilderCanonicalOrder> destinationOrders,
        Dictionary<string, BistroBuilderCanonicalOrder> destinationLines,
        out string error
    )
    {
        destinationOrders.Clear();
        destinationLines.Clear();

        if (source == null)
        {
            error = "La colección de comandas es nula.";
            return false;
        }

        for (int orderIndex = 0;
             orderIndex < source.Count;
             orderIndex++)
        {
            BistroBuilderCanonicalOrder order = source[orderIndex];

            if (order == null)
            {
                error = "La colección contiene una comanda nula.";
                return false;
            }

            if (!order.TryValidate(out error))
            {
                return false;
            }

            if (destinationOrders.ContainsKey(order.OrderId))
            {
                error = "Existe un OrderId duplicado: " + order.OrderId + ".";
                return false;
            }

            destinationOrders.Add(order.OrderId, order);

            for (int lineIndex = 0;
                 lineIndex < order.Lines.Count;
                 lineIndex++)
            {
                string lineId = order.Lines[lineIndex].LineId;

                if (destinationLines.ContainsKey(lineId))
                {
                    error = "Existe un LineId duplicado: " + lineId + ".";
                    return false;
                }

                destinationLines.Add(lineId, order);
            }
        }

        error = string.Empty;
        return true;
    }

    private void IndexOrder(BistroBuilderCanonicalOrder order)
    {
        byOrderId.Add(order.OrderId, order);

        for (int index = 0; index < order.Lines.Count; index++)
        {
            orderByLineId.Add(order.Lines[index].LineId, order);
        }
    }

    private void UnindexOrder(BistroBuilderCanonicalOrder order)
    {
        byOrderId.Remove(order.OrderId);

        for (int index = 0; index < order.Lines.Count; index++)
        {
            orderByLineId.Remove(order.Lines[index].LineId);
        }
    }

    private void PublishChange(
        BistroBuilderCanonicalOrderChangeType changeType,
        string orderId,
        string lineId,
        string description
    )
    {
        BistroBuilderCanonicalOrderChangedEvent change =
            new BistroBuilderCanonicalOrderChangedEvent(
                changeType,
                orderId,
                lineId,
                Revision,
                description
            );

        OrdersChanged?.Invoke(change);

        if (logChanges)
        {
            Debug.Log(
                "Comandas canónicas: " + changeType +
                ". OrderId: " +
                (string.IsNullOrEmpty(orderId) ? "-" : orderId) +
                ". LineId: " +
                (string.IsNullOrEmpty(lineId) ? "-" : lineId) +
                ". Revisión: " + Revision + ".",
                this
            );
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (menuService == null)
        {
            TryGetComponent(out menuService);
        }

        if (offerService == null)
        {
            TryGetComponent(out offerService);
        }

        if (selectionService == null)
        {
            TryGetComponent(out selectionService);
        }
    }

    private static BistroBuilderCanonicalOrderState
        GetExpectedAggregateForUniformLineState(
            BistroBuilderCanonicalOrderLineState lineState
        )
    {
        switch (lineState)
        {
            case BistroBuilderCanonicalOrderLineState.Draft:
                return BistroBuilderCanonicalOrderState.Draft;

            case BistroBuilderCanonicalOrderLineState.Submitted:
                return BistroBuilderCanonicalOrderState.Submitted;

            case BistroBuilderCanonicalOrderLineState.Queued:
            case BistroBuilderCanonicalOrderLineState.Preparing:
                return BistroBuilderCanonicalOrderState.InProgress;

            case BistroBuilderCanonicalOrderLineState.ReadyForPickup:
                return BistroBuilderCanonicalOrderState.ReadyForPickup;

            case BistroBuilderCanonicalOrderLineState
                .AssignedForDelivery:
            case BistroBuilderCanonicalOrderLineState.InTransit:
                return BistroBuilderCanonicalOrderState.InDelivery;

            case BistroBuilderCanonicalOrderLineState.Served:
                return BistroBuilderCanonicalOrderState.Served;

            case BistroBuilderCanonicalOrderLineState.Consumed:
                return BistroBuilderCanonicalOrderState.Completed;

            default:
                return BistroBuilderCanonicalOrderState.Failed;
        }
    }

    private static int CompareOrders(
        BistroBuilderCanonicalOrder first,
        BistroBuilderCanonicalOrder second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int sequenceComparison =
            first.SequenceNumber.CompareTo(second.SequenceNumber);

        return sequenceComparison != 0
            ? sequenceComparison
            : string.Compare(
                first.OrderId,
                second.OrderId,
                StringComparison.Ordinal
            );
    }

    private static int CompareMenuItems(
        BistroBuilderMenuItemRuntimeState first,
        BistroBuilderMenuItemRuntimeState second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int orderComparison =
            first.DisplayOrder.CompareTo(second.DisplayOrder);

        return orderComparison != 0
            ? orderComparison
            : string.Compare(
                first.DishId,
                second.DishId,
                StringComparison.Ordinal
            );
    }

    private static BistroBuilderCanonicalOrderOperationResult Failure(
        BistroBuilderCanonicalOrderFailureReason reason,
        string message,
        string orderId = "",
        string lineId = ""
    )
    {
        return BistroBuilderCanonicalOrderOperationResult.Failure(
            reason,
            message,
            orderId,
            lineId
        );
    }

    private sealed class MenuDishResolver :
        IBistroBuilderOrderDishResolver
    {
        private readonly BistroBuilderRestaurantMenuService menu;
        private readonly BistroBuilderMenuOfferService offer;
        private readonly BistroBuilderServiceMode serviceMode;

        public MenuDishResolver(
            BistroBuilderRestaurantMenuService menu,
            BistroBuilderMenuOfferService offer,
            BistroBuilderServiceMode serviceMode
        )
        {
            this.menu = menu;
            this.offer = offer;
            this.serviceMode = serviceMode;
        }

        public bool TryResolveOrderableDish(
            string dishId,
            BistroBuilderMealServiceAvailability mealService,
            out BistroBuilderResolvedOrderDish dish,
            out string rejectionReason
        )
        {
            dish = default(BistroBuilderResolvedOrderDish);

            if (menu == null)
            {
                rejectionReason = "La carta runtime no está disponible.";
                return false;
            }

            if (offer != null)
            {
                if (!offer.TryEvaluateDish(
                        dishId,
                        mealService,
                        serviceMode,
                        out BistroBuilderMenuOfferItemSnapshot item,
                        out rejectionReason
                    ))
                {
                    return false;
                }

                if (!item.IsOrderable)
                {
                    rejectionReason = string.IsNullOrWhiteSpace(
                        item.RejectionMessage
                    )
                        ? "El plato no está disponible."
                        : item.RejectionMessage;
                    return false;
                }

                dish = new BistroBuilderResolvedOrderDish(
                    item.DishId,
                    item.PriceCents,
                    item.DisplayOrder,
                    item.SignatureDish,
                    item.RestaurantId,
                    item.OfferRevision
                );
                rejectionReason = string.Empty;
                return true;
            }

            // Compatibilidad defensiva: sin la fachada 2.1C se comprueba el
            // estado persistente de carta y después la modalidad. No se exige
            // inventario runtime, porque esta ruta existe para escenas antiguas
            // y autotests aislados; el juego instalado usa siempre la oferta.
            if (!menu.IsDishEligibleByMenuState(
                    dishId,
                    mealService,
                    out rejectionReason
                ))
            {
                return false;
            }

            if (!menu.TryGetItemSnapshot(
                    dishId,
                    out BistroBuilderMenuItemRuntimeState legacyItem
                ) ||
                menu.CatalogService == null ||
                !menu.CatalogService.TryGetDefinition(
                    dishId,
                    out BistroBuilderDishDefinition definition
                ))
            {
                rejectionReason =
                    "No se pudo resolver el plato en la carta canónica.";
                return false;
            }

            if (!definition.IsAvailableForServiceMode(serviceMode))
            {
                rejectionReason =
                    "El plato no está disponible en esta modalidad de servicio.";
                return false;
            }

            dish = new BistroBuilderResolvedOrderDish(
                legacyItem.DishId,
                legacyItem.CurrentPriceCents,
                legacyItem.DisplayOrder,
                legacyItem.SignatureDish,
                string.Empty,
                menu.Revision
            );
            rejectionReason = string.Empty;
            return true;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
