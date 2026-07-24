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

    private readonly List<string> orderableDishIds =
        new List<string>(32);

    private bool initialized;

    public event Action<BistroBuilderCanonicalOrderChangedEvent>
        OrdersChanged;

    public BistroBuilderRestaurantMenuService MenuService => menuService;
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

        MenuDishResolver resolver = new MenuDishResolver(menuService);

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
    /// Política provisional y determinista para probar el dominio: asigna los
    /// platos disponibles por orden de carta y rota entre ellos.
    ///
    /// Las preferencias de cliente sustituirán esta selección, no el modelo
    /// de comanda.
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
                tableReferenceId = tableReferenceId,
                customerGroupReferenceId = customerGroupReferenceId,
                mealService = mealService
            };

        HashSet<string> uniqueCustomers =
            new HashSet<string>(StringComparer.Ordinal);

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

            if (!uniqueCustomers.Add(customerId))
            {
                return Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .DuplicateReferenceId,
                    "El mismo cliente aparece más de una vez."
                );
            }

            string dishId =
                orderableDishIds[index % orderableDishIds.Count];

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

        if (!EnsureInitialized(out _) ||
            !byOrderId.TryGetValue(
                BistroBuilderOrderIdUtility.Normalize(orderId),
                out BistroBuilderCanonicalOrder order
            ))
        {
            return false;
        }

        snapshot = order.Clone();
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
        menuBuffer.Clear();

        if (!EnsureInitialized(out error) ||
            !menuService.TryGetSnapshot(menuBuffer, out error))
        {
            return false;
        }

        menuBuffer.Sort(CompareMenuItems);

        for (int index = 0; index < menuBuffer.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = menuBuffer[index];

            if (item != null &&
                menuService.IsDishOrderable(
                    item.DishId,
                    mealService,
                    out _
                ))
            {
                orderableDishIds.Add(item.DishId);
            }
        }

        if (orderableDishIds.Count == 0)
        {
            error = "No existe ningún plato pedible para el servicio " +
                    mealService + ".";
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

        public MenuDishResolver(
            BistroBuilderRestaurantMenuService menu
        )
        {
            this.menu = menu;
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

            if (!menu.IsDishOrderable(
                    dishId,
                    mealService,
                    out rejectionReason
                ))
            {
                return false;
            }

            if (!menu.TryGetItemSnapshot(
                    dishId,
                    out BistroBuilderMenuItemRuntimeState item
                ))
            {
                rejectionReason =
                    "No se pudo obtener el estado runtime del plato.";
                return false;
            }

            dish = new BistroBuilderResolvedOrderDish(
                item.DishId,
                item.CurrentPriceCents,
                item.DisplayOrder
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
