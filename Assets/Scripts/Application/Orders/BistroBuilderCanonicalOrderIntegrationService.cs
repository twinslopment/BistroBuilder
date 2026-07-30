using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puente transitorio entre el servicio legacy y las comandas canónicas.
///
/// Responsabilidades:
/// - Crear una comanda canónica antes de crear RestaurantOrder.
/// - Componer líneas individuales o compartidas según un perfil de datos.
/// - Conservar un enlace único legacy OrderId -> Canonical OrderId.
/// - Aprobar cada transición legacy solo después de aplicarla de forma
///   atómica en la autoridad canónica.
/// - Impedir divergencias si falla la carta, una referencia o una transición.
///
/// No ejecuta cocina, movimiento ni entrega. Esas responsabilidades continúan
/// en sus sistemas actuales hasta los siguientes bloques de integración.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Orders/Canonical Order Integration Service"
)]
public sealed class BistroBuilderCanonicalOrderIntegrationService :
    MonoBehaviour,
    IRestaurantOrderTransitionGate
{
    public event Action<BistroBuilderMealServiceAvailability>
        CurrentMealServiceChanged;

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderCanonicalOrderService canonicalOrderService;

    [SerializeField]
    private BistroBuilderOrderCompositionService orderCompositionService;

    [SerializeField]
    private BistroBuilderCourseAndSharingService courseAndSharingService;

    [Header("Servicio provisional")]

    [Tooltip(
        "Servicio utilizado hasta que el sistema definitivo de horarios " +
        "publique la franja activa."
    )]
    [SerializeField]
    private BistroBuilderMealServiceAvailability currentMealService =
        BistroBuilderMealServiceAvailability.Lunch;

    [SerializeField, Range(0, 20)]
    private int defaultCourseIndex = 1;

    [Tooltip(
        "Cuando está activo, cocina y reparto mutan cada línea de forma " +
        "individual. La fachada legacy solo se valida y sincroniza."
    )]
    [SerializeField]
    private bool individualLineExecutionEnabled;

    [Tooltip(
        "Activa composición por pases, platos compartidos y liberación " +
        "coordinada 367F."
    )]
    [SerializeField]
    private bool courseAndSharingExecutionEnabled;

    [Header("Depuración")]

    [SerializeField]
    private bool logSynchronization = true;

    private readonly List<string> customerIds =
        new List<string>(16);

    private readonly Dictionary<int, string> canonicalByLegacyOrderId =
        new Dictionary<int, string>();

    private readonly Dictionary<string, int> legacyByCanonicalOrderId =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private bool initialized;

    public BistroBuilderCanonicalOrderService CanonicalOrderService =>
        canonicalOrderService;

    public BistroBuilderMealServiceAvailability CurrentMealService =>
        currentMealService;

    public int DefaultCourseIndex => defaultCourseIndex;

    public bool IndividualLineExecutionEnabled =>
        individualLineExecutionEnabled;

    public bool CourseAndSharingExecutionEnabled =>
        courseAndSharingExecutionEnabled;

    public BistroBuilderOrderCompositionService OrderCompositionService =>
        orderCompositionService;

    public BistroBuilderCourseAndSharingService CourseAndSharingService =>
        courseAndSharingService;

    public int ActiveLinkCount => canonicalByLegacyOrderId.Count;

    private void Awake()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (canonicalOrderService == null)
        {
            initialized = false;
            error = "Falta BistroBuilderCanonicalOrderService.";
            return false;
        }

        if (!canonicalOrderService.ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        if (!IsConcreteMealService(currentMealService))
        {
            initialized = false;
            error =
                "La integración necesita un servicio concreto válido.";
            return false;
        }

        if (defaultCourseIndex < 0 ||
            defaultCourseIndex > 20)
        {
            initialized = false;
            error = "El pase predeterminado queda fuera de rango.";
            return false;
        }

        if (courseAndSharingExecutionEnabled)
        {
            if (!individualLineExecutionEnabled)
            {
                initialized = false;
                error = "367F requiere la ejecución individual 367D activa.";
                return false;
            }

            if (orderCompositionService == null ||
                !orderCompositionService.ValidateConfiguration(out error))
            {
                initialized = false;
                return false;
            }

            if (courseAndSharingService == null ||
                !courseAndSharingService.ValidateConfiguration(out error))
            {
                initialized = false;
                return false;
            }
        }

        initialized = true;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Permite que el futuro sistema de horarios cambie la franja activa sin
    /// modificar OrderSystem ni el dominio de comandas.
    /// </summary>
    public bool TrySetCurrentMealService(
        BistroBuilderMealServiceAvailability mealService,
        out string error
    )
    {
        if (!IsConcreteMealService(mealService))
        {
            error = "Debe indicarse un único servicio válido.";
            return false;
        }

        if (currentMealService == mealService)
        {
            error = string.Empty;
            return true;
        }

        currentMealService = mealService;
        initialized = false;
        CurrentMealServiceChanged?.Invoke(currentMealService);
        error = string.Empty;
        return true;
    }

    public bool TryCreateCanonicalOrder(
        RestaurantTable table,
        CustomerGroup customerGroup,
        Waiter waiter,
        int legacyOrderId,
        out string canonicalOrderId,
        out string error
    )
    {
        canonicalOrderId = string.Empty;

        if (!EnsureReady(out error))
        {
            return false;
        }

        if (table == null)
        {
            error = "No se puede crear una comanda sin mesa.";
            return false;
        }

        if (customerGroup == null)
        {
            error = "No se puede crear una comanda sin grupo.";
            return false;
        }

        if (waiter == null)
        {
            error = "No se puede crear una comanda sin camarero.";
            return false;
        }

        if (legacyOrderId < 1)
        {
            error = "La identidad legacy de comanda no es válida.";
            return false;
        }

        if (!ReferenceEquals(
                table.AssignedCustomerGroup,
                customerGroup
            ))
        {
            error =
                "La mesa no está ocupada por el grupo indicado.";
            return false;
        }

        if (!BistroBuilderServiceOrderIdentityUtility
                .TryBuildCustomerReferences(
                    customerGroup.GroupId,
                    customerGroup.GroupSize,
                    customerIds,
                    out error
                ))
        {
            return false;
        }

        string externalReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildLegacyOrderReference(legacyOrderId);
        string tableReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildTableReference(table.TableId);
        string groupReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildGroupReference(customerGroup.GroupId);

        BistroBuilderCanonicalOrderOperationResult result;
        BistroBuilderCanonicalOrder snapshot;

        if (courseAndSharingExecutionEnabled)
        {
            if (!orderCompositionService.TryBuildCreationRequest(
                    externalReferenceId,
                    tableReferenceId,
                    groupReferenceId,
                    customerIds,
                    currentMealService,
                    out BistroBuilderCanonicalOrderCreationRequest request,
                    out error
                ))
            {
                return false;
            }

            request.serviceMode = BistroBuilderServiceMode.TableService;

            result = canonicalOrderService.TryCreateOrder(
                request,
                out snapshot
            );
        }
        else
        {
            result = canonicalOrderService.TryCreateIndividualOrder(
                externalReferenceId,
                tableReferenceId,
                groupReferenceId,
                customerIds,
                currentMealService,
                defaultCourseIndex,
                out snapshot
            );
        }

        if (!result.Succeeded ||
            snapshot == null)
        {
            error = string.IsNullOrWhiteSpace(result.Message)
                ? "La autoridad canónica rechazó la comanda."
                : result.Message;
            return false;
        }

        if (!string.Equals(
                snapshot.ExternalReferenceId,
                externalReferenceId,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                snapshot.TableReferenceId,
                tableReferenceId,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                snapshot.CustomerGroupReferenceId,
                groupReferenceId,
                StringComparison.Ordinal
            ) ||
            snapshot.ServiceMode !=
                BistroBuilderServiceMode.TableService ||
            snapshot.Lines.Count == 0 ||
            !DoesSnapshotCoverCustomers(snapshot, customerIds))
        {
            TryRollbackUnregisteredCanonicalOrder(
                snapshot.OrderId,
                out _
            );

            error =
                "La comanda canónica creada no conserva todas sus " +
                "referencias o líneas.";
            return false;
        }

        canonicalOrderId = snapshot.OrderId;
        error = string.Empty;
        return true;
    }


    public bool TryCreateCanonicalBarOrder(
        BistroBuilderBarServiceSpot barSpot,
        CustomerGroup customerGroup,
        Waiter waiter,
        int legacyOrderId,
        BistroBuilderServiceMode serviceMode,
        IList<string> dishIds,
        out string canonicalOrderId,
        out string error
    )
    {
        canonicalOrderId = string.Empty;

        if (!EnsureReady(out error))
        {
            return false;
        }

        if (barSpot == null || customerGroup == null || waiter == null)
        {
            error = "La comanda de barra necesita plaza, grupo y camarero.";
            return false;
        }

        if (!BistroBuilderServiceModeUtility.IsBarMode(serviceMode))
        {
            error = "La modalidad indicada no pertenece al servicio de barra.";
            return false;
        }

        if (legacyOrderId < 1 || dishIds == null || dishIds.Count == 0)
        {
            error = "La identidad o los artículos de la comanda no son válidos.";
            return false;
        }

        if (!ReferenceEquals(barSpot.AssignedCustomerGroup, customerGroup) ||
            !ReferenceEquals(customerGroup.AssignedBarSpot, barSpot))
        {
            error = "La plaza de barra no está ocupada por el grupo indicado.";
            return false;
        }

        BistroBuilderRestaurantMenuService menu =
            orderCompositionService != null
                ? orderCompositionService.MenuService
                : null;

        if (menu == null || menu.CatalogService == null)
        {
            error = "No está disponible la carta para crear la comanda de barra.";
            return false;
        }

        if (!BistroBuilderServiceOrderIdentityUtility
                .TryBuildCustomerReferences(
                    customerGroup.GroupId,
                    customerGroup.GroupSize,
                    customerIds,
                    out error
                ))
        {
            return false;
        }

        string externalReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildLegacyOrderReference(legacyOrderId);
        string destinationReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildBarSpotReference(barSpot.BarSpotId);
        string groupReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildGroupReference(customerGroup.GroupId);

        BistroBuilderCanonicalOrderCreationRequest request =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                externalReferenceId = externalReferenceId,
                tableReferenceId = destinationReferenceId,
                customerGroupReferenceId = groupReferenceId,
                mealService = currentMealService,
                serviceMode = serviceMode
            };

        for (int index = 0; index < dishIds.Count; index++)
        {
            string dishId = BistroBuilderMenuIdUtility.NormalizeStableId(
                dishIds[index]
            );

            if (!menu.IsDishOrderable(
                    dishId,
                    currentMealService,
                    out string rejection
                ) ||
                !menu.CatalogService.TryGetDefinition(
                    dishId,
                    out BistroBuilderDishDefinition definition
                ) ||
                !definition.IsAvailableForServiceMode(serviceMode))
            {
                error = string.IsNullOrWhiteSpace(rejection)
                    ? "El artículo " + dishId +
                      " no está disponible en esta modalidad de barra."
                    : rejection;
                return false;
            }

            string customerId = customerIds[index % customerIds.Count];
            request.lines.Add(
                new BistroBuilderCanonicalOrderLineRequest(
                    dishId,
                    customerId,
                    new[] { customerId },
                    1
                )
            );
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryCreateOrder(request, out var snapshot);

        if (!result.Succeeded || snapshot == null)
        {
            error = string.IsNullOrWhiteSpace(result.Message)
                ? "La autoridad canónica rechazó la comanda de barra."
                : result.Message;
            return false;
        }

        bool valid =
            snapshot.ServiceMode == serviceMode &&
            string.Equals(
                snapshot.ServiceDestinationReferenceId,
                destinationReferenceId,
                StringComparison.Ordinal
            ) &&
            string.Equals(
                snapshot.CustomerGroupReferenceId,
                groupReferenceId,
                StringComparison.Ordinal
            ) &&
            snapshot.Lines.Count == dishIds.Count &&
            DoesSnapshotCoverCustomers(snapshot, customerIds);

        if (!valid)
        {
            TryRollbackUnregisteredCanonicalOrder(snapshot.OrderId, out _);
            error = "La comanda de barra creada no conserva su contexto.";
            return false;
        }

        canonicalOrderId = snapshot.OrderId;
        error = string.Empty;
        return true;
    }

    public bool TryRegisterLegacyOrder(
        RestaurantOrder order,
        out string error
    )
    {
        if (order == null)
        {
            error = "La comanda legacy es nula.";
            return false;
        }

        if (!order.HasCanonicalOrder)
        {
            error =
                "La comanda legacy no contiene un CanonicalOrderId válido.";
            return false;
        }

        if (canonicalByLegacyOrderId.ContainsKey(order.OrderId))
        {
            error =
                "El OrderId legacy ya está registrado en la integración.";
            return false;
        }

        if (legacyByCanonicalOrderId.ContainsKey(order.CanonicalOrderId))
        {
            error =
                "El CanonicalOrderId ya está enlazado a otra comanda.";
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder snapshot
            ))
        {
            error =
                "La comanda canónica enlazada no existe en el runtime.";
            return false;
        }

        string expectedExternal =
            BistroBuilderServiceOrderIdentityUtility
                .BuildLegacyOrderReference(order.OrderId);
        string expectedDestination = order.ServiceDestinationReferenceId;
        string expectedGroup =
            BistroBuilderServiceOrderIdentityUtility
                .BuildGroupReference(order.CustomerGroup.GroupId);

        if (!string.Equals(
                snapshot.ExternalReferenceId,
                expectedExternal,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                snapshot.ServiceDestinationReferenceId,
                expectedDestination,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                snapshot.CustomerGroupReferenceId,
                expectedGroup,
                StringComparison.Ordinal
            ) ||
            snapshot.ServiceMode != order.ServiceMode ||
            snapshot.Lines.Count == 0 ||
            !TryBuildExpectedCustomerReferences(
                order.CustomerGroup,
                out _
            ) ||
            !DoesSnapshotCoverCustomers(snapshot, customerIds))
        {
            error =
                "Las referencias del enlace legacy-canónico no coinciden.";
            return false;
        }

        canonicalByLegacyOrderId.Add(
            order.OrderId,
            order.CanonicalOrderId
        );
        legacyByCanonicalOrderId.Add(
            order.CanonicalOrderId,
            order.OrderId
        );

        error = string.Empty;
        return true;
    }

    public bool TryApproveTransition(
        RestaurantOrder order,
        OrderState currentState,
        OrderState targetState,
        out string error
    )
    {
        error = string.Empty;

        if (!EnsureReady(out error))
        {
            return false;
        }

        if (order == null)
        {
            error = "La comanda legacy es nula.";
            return false;
        }

        if (!order.HasCanonicalOrder)
        {
            error =
                "La comanda legacy no está enlazada a una comanda canónica.";
            return false;
        }

        if (!canonicalByLegacyOrderId.TryGetValue(
                order.OrderId,
                out string registeredCanonicalId
            ) ||
            !string.Equals(
                registeredCanonicalId,
                order.CanonicalOrderId,
                StringComparison.Ordinal
            ))
        {
            error =
                "El enlace legacy-canónico no está registrado o ha cambiado.";
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult result;

        if (!individualLineExecutionEnabled)
        {
            if (!BistroBuilderLegacyCanonicalOrderStateMap.TryGetLineTarget(
                    targetState,
                    out BistroBuilderCanonicalOrderLineState targetLineState,
                    out bool cancelOrder
                ))
            {
                error =
                    "No existe traducción canónica para el estado " +
                    targetState + ".";
                return false;
            }

            result = cancelOrder
                ? canonicalOrderService.TryCancelOrder(
                    order.CanonicalOrderId,
                    BistroBuilderServiceOrderIdentityUtility
                        .BuildWaiterReference(order.AssignedWaiter.WaiterId)
                )
                : canonicalOrderService.TryAdvanceAllLinesToState(
                    order.CanonicalOrderId,
                    targetLineState,
                    BistroBuilderServiceOrderIdentityUtility
                        .BuildWaiterReference(order.AssignedWaiter.WaiterId)
                );

            if (!result.Succeeded)
            {
                error =
                    "La transición canónica fue rechazada. " +
                    result.Message;
                return false;
            }
        }
        else
        {
            if (!TryApproveIndividualLineExecutionTransition(
                    order,
                    targetState,
                    out result,
                    out error
                ))
            {
                return false;
            }
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder snapshot
            ))
        {
            error =
                "La comanda canónica no pudo verificarse tras la transición.";
            return false;
        }

        if (snapshot == null)
        {
            error =
                "La fotografía canónica resultante es nula.";
            return false;
        }

        bool compatible = individualLineExecutionEnabled &&
                          (targetState == OrderState.Preparing ||
                           targetState == OrderState.ReadyForPickup ||
                           targetState == OrderState.Served)
            ? BistroBuilderLegacyCanonicalOrderStateMap
                .TryValidateIndividualLineCompatibility(
                    targetState,
                    snapshot.Lines,
                    out error
                )
            : BistroBuilderLegacyCanonicalOrderStateMap
                .IsAggregateCompatible(
                    targetState,
                    snapshot.State
                );

        if (!compatible)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error =
                    "El estado canónico resultante " + snapshot.State +
                    " no es compatible con el estado legacy " +
                    targetState + ".";
            }

            return false;
        }

        if (logSynchronization)
        {
            Debug.Log(
                "367C sincroniza comanda legacy " +
                order.OrderId + " (" + currentState + " -> " +
                targetState + ") con " +
                order.CanonicalOrderId + " (" +
                snapshot.State + ").",
                this
            );
        }

        error = string.Empty;
        return true;
    }

    private bool TryApproveIndividualLineExecutionTransition(
        RestaurantOrder order,
        OrderState targetState,
        out BistroBuilderCanonicalOrderOperationResult result,
        out string error
    )
    {
        result = default;
        error = string.Empty;

        string actorReference = order.AssignedWaiter != null
            ? BistroBuilderServiceOrderIdentityUtility
                .BuildWaiterReference(order.AssignedWaiter.WaiterId)
            : "legacy_order_transition";

        switch (targetState)
        {
            case OrderState.SentToKitchen:
                if (courseAndSharingExecutionEnabled)
                {
                    if (!courseAndSharingService
                            .TrySubmitAndReleaseInitialCourse(
                                order,
                                actorReference,
                                out result,
                                out error
                            ))
                    {
                        return false;
                    }
                }
                else
                {
                    result = canonicalOrderService.TryAdvanceAllLinesToState(
                        order.CanonicalOrderId,
                        BistroBuilderCanonicalOrderLineState.Queued,
                        actorReference
                    );
                }
                break;

            case OrderState.Preparing:
            case OrderState.ReadyForPickup:
            case OrderState.Served:
                if (!canonicalOrderService.TryGetOrderSnapshot(
                        order.CanonicalOrderId,
                        out BistroBuilderCanonicalOrder snapshot
                    ))
                {
                    result = default;
                    error = "No se encontró la comanda canónica enlazada.";
                    return false;
                }

                if (snapshot == null)
                {
                    result = default;
                    error = "La fotografía canónica enlazada es nula.";
                    return false;
                }

                if (!BistroBuilderLegacyCanonicalOrderStateMap
                        .TryValidateIndividualLineCompatibility(
                            targetState,
                            snapshot.Lines,
                            out error
                        ))
                {
                    result = default;
                    return false;
                }

                result = BistroBuilderCanonicalOrderOperationResult.Success(
                    "La fachada legacy es compatible con las líneas.",
                    order.CanonicalOrderId,
                    string.Empty
                );
                break;

            case OrderState.Completed:
                result = canonicalOrderService.TryCompleteServedOrder(
                    order.CanonicalOrderId,
                    actorReference
                );
                break;

            case OrderState.Cancelled:
                result = canonicalOrderService.TryCancelOrder(
                    order.CanonicalOrderId,
                    actorReference
                );
                break;

            default:
                result = default;
                error =
                    "367F no reconoce la transición legacy a " +
                    targetState + ".";
                return false;
        }

        if (!result.Succeeded)
        {
            error =
                "La transición canónica fue rechazada. " +
                result.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void NotifyLegacyOrderRemoved(RestaurantOrder order)
    {
        if (order == null)
        {
            return;
        }

        if (canonicalByLegacyOrderId.TryGetValue(
                order.OrderId,
                out string canonicalOrderId
            ))
        {
            canonicalByLegacyOrderId.Remove(order.OrderId);
            legacyByCanonicalOrderId.Remove(canonicalOrderId);
        }
    }

    /// <summary>
    /// Revierte una comanda canónica recién creada cuando todavía no existe
    /// RestaurantOrder. Se usa únicamente durante la creación atómica.
    /// </summary>
    public bool TryRollbackUnregisteredCanonicalOrder(
        string canonicalOrderId,
        out string error
    )
    {
        error = string.Empty;

        if (canonicalOrderService == null)
        {
            error = "La autoridad canónica no está disponible.";
            return false;
        }

        string normalized =
            BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);

        if (!canonicalOrderService.TryGetOrderSnapshot(
                normalized,
                out BistroBuilderCanonicalOrder snapshot
            ))
        {
            return true;
        }

        if (!snapshot.IsTerminal)
        {
            BistroBuilderCanonicalOrderOperationResult cancelResult =
                canonicalOrderService.TryCancelOrder(
                    normalized,
                    "integration_rollback"
                );

            if (!cancelResult.Succeeded)
            {
                error = cancelResult.Message;
                return false;
            }
        }

        BistroBuilderCanonicalOrderOperationResult removeResult =
            canonicalOrderService.TryRemoveTerminalOrder(normalized);

        if (!removeResult.Succeeded)
        {
            error = removeResult.Message;
            return false;
        }

        return true;
    }


    public void ClearRuntimeLinksForLoad()
    {
        canonicalByLegacyOrderId.Clear();
        legacyByCanonicalOrderId.Clear();
    }

    public bool TryGetLinkedCanonicalOrderId(
        int legacyOrderId,
        out string canonicalOrderId
    )
    {
        return canonicalByLegacyOrderId.TryGetValue(
            legacyOrderId,
            out canonicalOrderId
        );
    }

    private bool TryBuildExpectedCustomerReferences(
        CustomerGroup customerGroup,
        out string error
    )
    {
        if (customerGroup == null)
        {
            error = "No existe el grupo legacy del enlace canónico.";
            return false;
        }

        return BistroBuilderServiceOrderIdentityUtility
            .TryBuildCustomerReferences(
                customerGroup.GroupId,
                customerGroup.GroupSize,
                customerIds,
                out error
            );
    }

    private static bool DoesSnapshotCoverCustomers(
        BistroBuilderCanonicalOrder snapshot,
        IList<string> expectedCustomerIds
    )
    {
        if (snapshot == null || expectedCustomerIds == null)
        {
            return false;
        }

        HashSet<string> expected =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> actual =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < expectedCustomerIds.Count; index++)
        {
            expected.Add(BistroBuilderOrderIdUtility.Normalize(
                expectedCustomerIds[index]
            ));
        }

        for (int lineIndex = 0;
             lineIndex < snapshot.Lines.Count;
             lineIndex++)
        {
            BistroBuilderCanonicalOrderLine line = snapshot.Lines[lineIndex];

            if (line == null || line.ConsumerCustomerIds == null)
            {
                return false;
            }

            for (int consumerIndex = 0;
                 consumerIndex < line.ConsumerCustomerIds.Count;
                 consumerIndex++)
            {
                actual.Add(BistroBuilderOrderIdUtility.Normalize(
                    line.ConsumerCustomerIds[consumerIndex]
                ));
            }
        }

        return expected.SetEquals(actual);
    }

    private bool EnsureReady(out string error)
    {
        if (initialized &&
            canonicalOrderService != null)
        {
            error = string.Empty;
            return true;
        }

        return ValidateConfiguration(out error);
    }

    private static bool IsConcreteMealService(
        BistroBuilderMealServiceAvailability mealService
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                mealService,
                false
            ))
        {
            return false;
        }

        int value = (int)mealService;

        // Un único bit activo representa Breakfast, Lunch o Dinner.
        return value > 0 &&
               (value & (value - 1)) == 0;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (canonicalOrderService == null)
        {
            TryGetComponent(out canonicalOrderService);
        }

        if (orderCompositionService == null)
        {
            TryGetComponent(out orderCompositionService);
        }

        if (courseAndSharingService == null)
        {
            TryGetComponent(out courseAndSharingService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        defaultCourseIndex = Mathf.Clamp(
            defaultCourseIndex,
            0,
            20
        );
    }
#endif
}
