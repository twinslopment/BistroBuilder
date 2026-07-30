using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad operativa del servicio de barra 367H.
///
/// Mantiene sesiones independientes de mesa, ocupa plazas reales, crea una
/// comanda canónica propia, coordina toma de pedido, consumo, pago o
/// transferencia de cargos y libera la barra antes de sentar al grupo.
/// No utiliza mesas proxy ni consultas continuas en Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Service/Bar Service System")]
public sealed class BistroBuilderBarServiceSystem : MonoBehaviour
{
    public const string RuntimeRevision = "367H";

    private enum SessionState
    {
        Allocated = 0,
        CustomerWalking = 1,
        WaitingForOrderWaiter = 2,
        TakingOrder = 3,
        WaitingForItems = 4,
        Consuming = 5,
        WaitingForPaymentWaiter = 6,
        Paying = 7,
        ClosingForTable = 8,
        WaitingForTableAfterConsumption = 9,
        Completed = 10,
        Cancelled = 11
    }

    [Serializable]
    private sealed class Session
    {
        public CustomerGroup Group;
        public BistroBuilderBarServiceSpot Spot;
        public BistroBuilderServiceMode Mode;
        public RestaurantOrder Order;
        public Waiter ResponsibleWaiter;
        public SessionState State;
        public int ChargeCents;
        public bool TableRequested;
        public readonly HashSet<string> ServedLineIds =
            new HashSet<string>(StringComparer.Ordinal);
        public Coroutine Routine;
    }

    [Serializable]
    private sealed class TransferredCharge
    {
        public int GroupId;
        public string SourceOrderId = string.Empty;
        public int AmountCents;
        public bool Settled;
    }

    [Header("Dependencias")]
    [SerializeField]
    private BistroBuilderBarServiceRegistry barRegistry;

    [SerializeField]
    private OrderSystem orderSystem;

    [SerializeField]
    private BistroBuilderDishCatalogService catalogService;

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;

    [SerializeField]
    private CustomerWaitingAreaSystem waitingAreaSystem;

    [Header("Oferta automática provisional")]
    [SerializeField, Min(1)]
    private int maximumItemsPerBarOrder = 4;

    [Tooltip(
        "Tiempo para que el camarero tome el pedido una vez en la barra."
    )]
    [SerializeField, Min(0.1f)]
    private float orderTakingDuration = 1.2f;

    [SerializeField, Min(0.1f)]
    private float consumptionDuration = 4f;

    [SerializeField, Min(0.1f)]
    private float billDeliveryDuration = 1f;

    [SerializeField, Min(0.1f)]
    private float paymentDuration = 1.5f;

    [Header("Comportamiento")]
    [SerializeField]
    private bool automaticallyOfferBarToWaitingGroups = true;

    [SerializeField]
    private bool logChanges = true;

    private readonly Dictionary<CustomerGroup, Session> sessionsByGroup =
        new Dictionary<CustomerGroup, Session>();

    private readonly Dictionary<RestaurantOrder, Session> sessionsByOrder =
        new Dictionary<RestaurantOrder, Session>();

    private readonly Dictionary<Waiter, Session> waiterAssignments =
        new Dictionary<Waiter, Session>();

    private readonly List<TransferredCharge> transferredCharges =
        new List<TransferredCharge>();

    private readonly List<BistroBuilderMenuItemRuntimeState> menuBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private readonly List<string> dishBuffer = new List<string>(8);
    private readonly List<string> lineBuffer = new List<string>(8);
    private readonly HashSet<CustomerGroup> registeredGroups =
        new HashSet<CustomerGroup>();
    private readonly HashSet<Waiter> registeredWaiters =
        new HashSet<Waiter>();
    private readonly Dictionary<WaiterMovementView, Waiter>
        waiterByMovementView =
            new Dictionary<WaiterMovementView, Waiter>();
    private readonly Dictionary<CustomerMovementView, CustomerGroup>
        groupByMovementView =
            new Dictionary<CustomerMovementView, CustomerGroup>();

    private readonly List<BistroBuilderBarServiceSpot> occupiedSpotBuffer =
        new List<BistroBuilderBarServiceSpot>(8);

    public event Action<BistroBuilderBarServiceCompletedEvent>
        SessionCompleted;

    public int ActiveSessionCount => sessionsByGroup.Count;
    public int CompletedBarServiceCount { get; private set; }
    public int CompletedWaitingAtBarCount { get; private set; }
    public BistroBuilderBarServiceCompletedEvent LastCompletedSession
    {
        get;
        private set;
    }

    public int TransferredChargeCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < transferredCharges.Count; index++)
            {
                if (!transferredCharges[index].Settled)
                {
                    count++;
                }
            }
            return count;
        }
    }

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        DiscoverWaiters();
    }

    private void Start()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        foreach (CustomerGroup group in registeredGroups)
        {
            if (group != null)
            {
                group.StateChanged -= HandleGroupStateChanged;
            }
        }

        foreach (KeyValuePair<CustomerMovementView, CustomerGroup> pair
                 in groupByMovementView)
        {
            if (pair.Key != null)
            {
                pair.Key.DestinationReached -= HandleCustomerDestinationReached;
            }
        }

        foreach (Waiter waiter in registeredWaiters)
        {
            if (waiter != null)
            {
                waiter.StateChanged -= HandleWaiterStateChanged;
            }
        }

        foreach (KeyValuePair<WaiterMovementView, Waiter> pair
                 in waiterByMovementView)
        {
            if (pair.Key != null)
            {
                pair.Key.DestinationReached -= HandleWaiterDestinationReached;
            }
        }

        registeredGroups.Clear();
        registeredWaiters.Clear();
        groupByMovementView.Clear();
        waiterByMovementView.Clear();
    }

    public bool RegisterCustomerGroup(CustomerGroup group)
    {
        if (group == null || !registeredGroups.Add(group))
        {
            return false;
        }

        group.StateChanged += HandleGroupStateChanged;

        CustomerMovementView movement =
            group.GetComponent<CustomerMovementView>();

        if (movement != null)
        {
            movement.DestinationReached -= HandleCustomerDestinationReached;
            movement.DestinationReached += HandleCustomerDestinationReached;
            groupByMovementView[movement] = group;
        }

        if (group.CurrentState == CustomerGroupState.WaitingForTable)
        {
            ScheduleBarAllocation(group);
        }

        return true;
    }

    public bool UnregisterCustomerGroup(CustomerGroup group)
    {
        if (group == null || !registeredGroups.Remove(group))
        {
            return false;
        }

        group.StateChanged -= HandleGroupStateChanged;

        CustomerMovementView movement =
            group.GetComponent<CustomerMovementView>();

        if (movement != null)
        {
            movement.DestinationReached -= HandleCustomerDestinationReached;
            groupByMovementView.Remove(movement);
        }

        if (sessionsByGroup.TryGetValue(group, out Session session))
        {
            CancelSession(session, "El grupo fue retirado del servicio.");
        }

        return true;
    }

    /// <summary>
    /// TableAssignmentSystem consulta esta operación antes de sentar a un
    /// grupo que está consumiendo en barra. Devuelve true solo cuando la
    /// sesión ya está cerrada y la plaza ha sido liberada.
    /// </summary>
    public bool TryPrepareGroupForTable(
        CustomerGroup group,
        out string reason
    )
    {
        reason = string.Empty;

        if (group == null)
        {
            reason = "El grupo es nulo.";
            return false;
        }

        if (!sessionsByGroup.TryGetValue(group, out Session session))
        {
            if (group.HasAssignedBarSpot && barRegistry != null)
            {
                barRegistry.ReleaseGroup(group);
            }

            return !group.HasAssignedBarSpot;
        }

        if (session.Mode == BistroBuilderServiceMode.BarService)
        {
            reason = "El grupo eligió servicio exclusivo de barra.";
            return false;
        }

        if (session.State == SessionState.Completed ||
            session.State == SessionState.Cancelled)
        {
            CloseAndRemoveSession(session, false);
            return !group.HasAssignedBarSpot;
        }

        session.TableRequested = true;

        // Si todavía no existe una comanda, la mesa tiene prioridad y la
        // visita temporal a barra puede cerrarse sin generar cargos.
        if (session.Order == null)
        {
            StopSessionRoutine(session);
            ReleaseSessionWaiter(session);
            session.State = SessionState.Completed;
            CloseAndRemoveSession(session, false);
            reason = "La plaza de barra se liberó antes de tomar el pedido.";
            return true;
        }

        // Una comanda ya consumida puede cerrarse sin repetir el tiempo de
        // consumo. La transferencia y el cierre son atómicos para el grupo.
        if (AreAllLinesConsumed(session.Order))
        {
            if (!TryCloseWaitingSessionForTable(session, out reason))
            {
                return false;
            }

            return true;
        }

        session.State = SessionState.ClosingForTable;

        if (session.Routine == null && AreAllLinesServed(session.Order))
        {
            session.Routine = StartCoroutine(ConsumeAndCloseRoutine(session));
        }

        reason =
            "La comanda de barra terminará antes de sentar al grupo.";
        return false;
    }

    public bool TryNotifyLineServed(
        RestaurantOrder order,
        string orderLineId,
        out string error
    )
    {
        error = string.Empty;

        if (order == null || !order.HasBarDestination ||
            !sessionsByOrder.TryGetValue(order, out Session session))
        {
            error = "La línea no pertenece a una sesión activa de barra.";
            return false;
        }

        string normalized =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        if (!BistroBuilderOrderIdUtility.IsValid(normalized))
        {
            error = "El LineId servido no es válido.";
            return false;
        }

        session.ServedLineIds.Add(normalized);

        if (!TryGetCanonicalOrder(
                order,
                out BistroBuilderCanonicalOrder snapshot,
                out error
            ))
        {
            return false;
        }

        for (int index = 0; index < snapshot.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = snapshot.Lines[index];

            if (line.State != BistroBuilderCanonicalOrderLineState.Served &&
                line.State != BistroBuilderCanonicalOrderLineState.Consumed)
            {
                return true;
            }
        }

        if (session.Routine == null &&
            (session.State == SessionState.WaitingForItems ||
             session.State == SessionState.ClosingForTable))
        {
            session.Routine = StartCoroutine(ConsumeAndCloseRoutine(session));
        }

        return true;
    }

    public int GetPendingTransferredChargeCents(CustomerGroup group)
    {
        if (group == null)
        {
            return 0;
        }

        int total = 0;

        for (int index = 0; index < transferredCharges.Count; index++)
        {
            TransferredCharge charge = transferredCharges[index];

            if (!charge.Settled && charge.GroupId == group.GroupId)
            {
                total += charge.AmountCents;
            }
        }

        return total;
    }

    public bool TrySettleTransferredCharges(
        CustomerGroup group,
        out int amountCents
    )
    {
        amountCents = 0;

        if (group == null)
        {
            return false;
        }

        for (int index = 0; index < transferredCharges.Count; index++)
        {
            TransferredCharge charge = transferredCharges[index];

            if (charge.Settled || charge.GroupId != group.GroupId)
            {
                continue;
            }

            charge.Settled = true;
            amountCents += charge.AmountCents;
        }

        return amountCents > 0;
    }

    public bool ValidateConfiguration(out string error)
    {
        // Inicialización explícita: evita rutas de cortocircuito en las que
        // un proveedor nulo dejaba el parámetro out sin asignar.
        error = string.Empty;
        ResolveDependencies();

        if (barRegistry == null)
        {
            error = "Falta BistroBuilderBarServiceRegistry.";
            return false;
        }

        if (!barRegistry.ValidateConfiguration(out error))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "BistroBuilderBarServiceRegistry no es válido.";
            }
            return false;
        }

        if (orderSystem == null)
        {
            error = "Falta OrderSystem.";
            return false;
        }

        if (!orderSystem.ValidateConfiguration(out error))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "OrderSystem no es válido.";
            }
            return false;
        }

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "BistroBuilderDishCatalogService no es válido.";
            }
            return false;
        }

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService.";
            return false;
        }

        if (!menuService.ValidateConfiguration(out error))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "BistroBuilderRestaurantMenuService no es válido.";
            }
            return false;
        }

        if (tableAssignmentSystem == null)
        {
            error = "Falta TableAssignmentSystem.";
            return false;
        }

        if (maximumItemsPerBarOrder < 1 ||
            !IsFinitePositive(orderTakingDuration) ||
            !IsFinitePositive(consumptionDuration) ||
            !IsFinitePositive(billDeliveryDuration) ||
            !IsFinitePositive(paymentDuration))
        {
            error = "La configuración temporal de barra es inválida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleGroupStateChanged(
        CustomerGroup group,
        CustomerGroupState state
    )
    {
        if (group == null)
        {
            return;
        }

        if (state == CustomerGroupState.WaitingForTable)
        {
            ScheduleBarAllocation(group);
            return;
        }

        if (state == CustomerGroupState.Finished)
        {
            UnregisterCustomerGroup(group);
        }
    }

    private void ScheduleBarAllocation(CustomerGroup group)
    {
        if (group == null ||
            group.HasAssignedTable ||
            sessionsByGroup.ContainsKey(group))
        {
            return;
        }

        bool shouldUseBar =
            group.RequestedServiceMode == BistroBuilderServiceMode.BarService ||
            (automaticallyOfferBarToWaitingGroups &&
             group.RequestedServiceMode ==
                 BistroBuilderServiceMode.WaitingAtBar);

        if (!shouldUseBar)
        {
            return;
        }

        StartCoroutine(AllocateBarNextFrame(group));
    }

    private IEnumerator AllocateBarNextFrame(CustomerGroup group)
    {
        // Permite que una mesa libre tenga prioridad inmediata para
        // WaitingAtBar sin alterar el orden de la cola existente.
        yield return null;

        if (group == null || group.HasAssignedTable ||
            group.CurrentState != CustomerGroupState.WaitingForTable ||
            sessionsByGroup.ContainsKey(group))
        {
            yield break;
        }

        BistroBuilderServiceMode mode = group.RequestedServiceMode;

        if (!barRegistry.TryAllocateSpot(group, mode, out var spot))
        {
            yield break;
        }

        Session session = new Session
        {
            Group = group,
            Spot = spot,
            Mode = mode,
            State = SessionState.Allocated
        };

        sessionsByGroup.Add(group, session);
        waitingAreaSystem?.RefreshWaitingQueue();

        CustomerMovementView movement =
            group.GetComponent<CustomerMovementView>();

        if (movement == null || !movement.MoveToBarPoint(spot))
        {
            CancelSession(session, "No se pudo iniciar el movimiento a barra.");
            yield break;
        }

        session.State = SessionState.CustomerWalking;

        if (mode == BistroBuilderServiceMode.BarService)
        {
            group.SetState(CustomerGroupState.WalkingToBar);
        }

        Log(
            "Grupo " + group.GroupId + " se dirige a " + spot.BarSpotId +
            " con modalidad " + mode + "."
        );
    }

    private void HandleCustomerDestinationReached(
        CustomerMovementView movement
    )
    {
        if (movement == null ||
            !groupByMovementView.TryGetValue(movement, out CustomerGroup group) ||
            group == null ||
            !sessionsByGroup.TryGetValue(group, out Session session) ||
            session.State != SessionState.CustomerWalking)
        {
            return;
        }

        session.State = SessionState.WaitingForOrderWaiter;

        if (session.Mode == BistroBuilderServiceMode.BarService)
        {
            group.SetState(CustomerGroupState.WaitingForBarOrder);
        }

        TryAssignWaiterForSession(session, false);
    }

    private void DiscoverWaiters()
    {
        Waiter[] waiters = FindObjectsByType<Waiter>(
            FindObjectsSortMode.InstanceID
        );

        for (int index = 0; index < waiters.Length; index++)
        {
            RegisterWaiter(waiters[index]);
        }
    }

    public bool RegisterWaiter(Waiter waiter)
    {
        if (waiter == null || !registeredWaiters.Add(waiter))
        {
            return false;
        }

        waiter.StateChanged += HandleWaiterStateChanged;

        WaiterMovementView movement =
            waiter.GetComponent<WaiterMovementView>();

        if (movement != null)
        {
            movement.DestinationReached -= HandleWaiterDestinationReached;
            movement.DestinationReached += HandleWaiterDestinationReached;
            waiterByMovementView[movement] = waiter;
        }

        return true;
    }

    private void HandleWaiterStateChanged(Waiter waiter, WaiterState state)
    {
        if (state != WaiterState.Idle)
        {
            return;
        }

        foreach (Session session in sessionsByGroup.Values)
        {
            if (session == null)
            {
                continue;
            }

            if (session.State == SessionState.WaitingForOrderWaiter)
            {
                TryAssignWaiterForSession(session, false);
                return;
            }

            if (session.State == SessionState.WaitingForPaymentWaiter)
            {
                TryAssignWaiterForSession(session, true);
                return;
            }
        }
    }

    private bool TryAssignWaiterForSession(Session session, bool forPayment)
    {
        if (session == null || session.Spot == null)
        {
            return false;
        }

        Waiter selected = null;

        foreach (Waiter waiter in registeredWaiters)
        {
            if (waiter == null || !waiter.IsAvailable)
            {
                continue;
            }

            if (selected == null || waiter.WaiterId < selected.WaiterId)
            {
                selected = waiter;
            }
        }

        if (selected == null)
        {
            return false;
        }

        WaiterState walkingState = forPayment
            ? WaiterState.WalkingToBarBill
            : WaiterState.WalkingToBar;

        if (!selected.AssignBarSpot(session.Spot, session.Order, walkingState))
        {
            return false;
        }

        session.ResponsibleWaiter = selected;
        waiterAssignments[selected] = session;
        return true;
    }

    private void HandleWaiterDestinationReached(WaiterMovementView movement)
    {
        if (movement == null ||
            !waiterByMovementView.TryGetValue(movement, out Waiter waiter) ||
            waiter == null ||
            !waiterAssignments.TryGetValue(waiter, out Session session) ||
            session == null)
        {
            return;
        }

        if (waiter.CurrentState == WaiterState.WalkingToBar &&
            session.State == SessionState.WaitingForOrderWaiter)
        {
            session.Routine = StartCoroutine(TakeOrderRoutine(session, waiter));
            return;
        }

        if (waiter.CurrentState == WaiterState.WalkingToBarBill &&
            session.State == SessionState.WaitingForPaymentWaiter)
        {
            session.Routine = StartCoroutine(PayAtBarRoutine(session, waiter));
        }
    }

    private IEnumerator TakeOrderRoutine(Session session, Waiter waiter)
    {
        session.State = SessionState.TakingOrder;
        waiter.SetState(WaiterState.TakingBarOrder);

        if (session.Mode == BistroBuilderServiceMode.BarService)
        {
            session.Group.SetState(CustomerGroupState.OrderingAtBar);
        }

        yield return new WaitForSeconds(orderTakingDuration);

        if (!BuildOrderDishIds(session, dishBuffer, out string error))
        {
            Debug.LogError(error, this);
            waiterAssignments.Remove(waiter);
            waiter.ClearAssignment();
            session.Routine = null;
            CancelSession(session, error);
            yield break;
        }

        RestaurantOrder order = orderSystem.CreateBarOrder(
            session.Spot,
            session.Group,
            waiter,
            session.Mode,
            dishBuffer
        );

        if (order == null || !order.TrySetState(OrderState.SentToKitchen))
        {
            error = order == null
                ? "No se pudo crear la comanda de barra."
                : order.LastTransitionError;
            Debug.LogError(error, this);
            waiterAssignments.Remove(waiter);
            waiter.ClearAssignment();
            session.Routine = null;
            CancelSession(session, error);
            yield break;
        }

        session.Order = order;
        session.ResponsibleWaiter = waiter;
        session.ChargeCents = CalculateOrderTotalCents(order);
        session.State = session.TableRequested
            ? SessionState.ClosingForTable
            : SessionState.WaitingForItems;
        sessionsByOrder[order] = session;

        if (session.Mode == BistroBuilderServiceMode.BarService)
        {
            session.Group.SetState(CustomerGroupState.WaitingForBarItems);
        }

        Log(
            "Comanda de barra " + order.OrderId + " enviada a producción " +
            "desde " + session.Spot.BarSpotId + "."
        );

        waiterAssignments.Remove(waiter);
        waiter.ClearAssignment();
        session.Routine = null;
    }

    private IEnumerator ConsumeAndCloseRoutine(Session session)
    {
        if (session == null || session.Order == null)
        {
            yield break;
        }

        session.State = SessionState.Consuming;

        if (session.Mode == BistroBuilderServiceMode.BarService)
        {
            session.Group.SetState(CustomerGroupState.ConsumingAtBar);
        }

        yield return new WaitForSeconds(consumptionDuration);

        if (!TryConsumeAllServedLines(session.Order, out string error))
        {
            Debug.LogError(error, this);
            session.Routine = null;
            CancelSession(session, error);
            yield break;
        }

        if (session.Mode == BistroBuilderServiceMode.WaitingAtBar)
        {
            session.Routine = null;

            if (session.TableRequested)
            {
                if (!TryCloseWaitingSessionForTable(session, out error))
                {
                    Debug.LogError(error, this);
                }

                yield break;
            }

            // El grupo ya terminó su consumo, pero conserva la plaza y su
            // posición en la cola hasta que una mesa quede reservada para él.
            session.State = SessionState.WaitingForTableAfterConsumption;
            tableAssignmentSystem.RequestReevaluation();
            Log(
                "Grupo " + session.Group.GroupId +
                " terminó su consumo y continúa esperando mesa desde barra."
            );
            yield break;
        }

        session.State = SessionState.WaitingForPaymentWaiter;
        session.Routine = null;
        TryAssignWaiterForSession(session, true);
    }

    private IEnumerator PayAtBarRoutine(Session session, Waiter waiter)
    {
        session.State = SessionState.Paying;
        waiter.SetState(WaiterState.DeliveringBarBill);
        session.Group.SetState(CustomerGroupState.PayingAtBar);

        yield return new WaitForSeconds(billDeliveryDuration);
        yield return new WaitForSeconds(paymentDuration);

        if (!orderSystem.CompleteOrder(session.Order))
        {
            Debug.LogError(
                "No se pudo completar el pago de la comanda de barra.",
                this
            );
            waiterAssignments.Remove(waiter);
            waiter.ClearAssignment();
            session.Routine = null;
            yield break;
        }

        Log(
            "Grupo " + session.Group.GroupId + " pagó " +
            FormatCents(session.ChargeCents) + " en barra."
        );

        waiterAssignments.Remove(waiter);
        waiter.ClearAssignment();
        session.State = SessionState.Completed;
        session.Routine = null;
        PublishSessionCompleted(session, false);
        CloseAndRemoveSession(session, false);
        session.Group.SetState(CustomerGroupState.Leaving);
    }

    private bool BuildOrderDishIds(
        Session session,
        List<string> destination,
        out string error
    )
    {
        destination.Clear();

        if (!menuService.TryGetSnapshot(menuBuffer, out error))
        {
            return false;
        }

        int targetCount = Mathf.Clamp(
            session.Group.GroupSize,
            1,
            Mathf.Max(1, maximumItemsPerBarOrder)
        );

        for (int index = 0;
             index < menuBuffer.Count && destination.Count < targetCount;
             index++)
        {
            BistroBuilderMenuItemRuntimeState item = menuBuffer[index];

            if (item == null || !item.Enabled || !item.Unlocked ||
                item.ManuallySoldOut ||
                !catalogService.TryGetDefinition(
                    item.DishId,
                    out BistroBuilderDishDefinition definition
                ) ||
                !definition.IsAvailableForServiceMode(session.Mode))
            {
                continue;
            }

            bool isQuickWaitingItem =
                definition.RequiredStation ==
                    BistroBuilderKitchenStationType.Bar ||
                definition.RequiredStation ==
                    BistroBuilderKitchenStationType.ColdPreparation;

            if (session.Mode == BistroBuilderServiceMode.WaitingAtBar &&
                !isQuickWaitingItem)
            {
                continue;
            }

            destination.Add(definition.DishId);
        }

        if (destination.Count < session.Group.GroupSize)
        {
            error =
                "No hay suficientes artículos de barra activos para cubrir " +
                "a todos los clientes del grupo " + session.Group.GroupId +
                ".";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryConsumeAllServedLines(
        RestaurantOrder order,
        out string error
    )
    {
        lineBuffer.Clear();

        if (!TryGetCanonicalOrder(order, out var snapshot, out error))
        {
            return false;
        }

        for (int index = 0; index < snapshot.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = snapshot.Lines[index];

            if (line.State == BistroBuilderCanonicalOrderLineState.Consumed)
            {
                continue;
            }

            if (line.State != BistroBuilderCanonicalOrderLineState.Served)
            {
                error = "La línea " + line.LineId +
                    " aún no está servida.";
                return false;
            }

            lineBuffer.Add(line.LineId);
        }

        if (lineBuffer.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        var result = orderSystem.CanonicalIntegrationService
            .CanonicalOrderService.TryConsumeServedLines(
                order.CanonicalOrderId,
                lineBuffer,
                "bar_consumption"
            );

        error = result.Succeeded ? string.Empty : result.Message;
        return result.Succeeded;
    }

    private int CalculateOrderTotalCents(RestaurantOrder order)
    {
        return TryGetCanonicalOrder(order, out var snapshot, out _)
            ? snapshot.CalculateTotalPriceCents()
            : 0;
    }

    private bool TryGetCanonicalOrder(
        RestaurantOrder order,
        out BistroBuilderCanonicalOrder snapshot,
        out string error
    )
    {
        snapshot = null;

        if (order == null || !order.HasCanonicalOrder ||
            orderSystem == null ||
            orderSystem.CanonicalIntegrationService == null ||
            orderSystem.CanonicalIntegrationService.CanonicalOrderService == null)
        {
            error = "La comanda no tiene una autoridad canónica disponible.";
            return false;
        }

        if (!orderSystem.CanonicalIntegrationService.CanonicalOrderService
                .TryGetOrderSnapshot(order.CanonicalOrderId, out snapshot))
        {
            error = "No se encontró la comanda canónica de barra.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryCloseWaitingSessionForTable(
        Session session,
        out string error
    )
    {
        if (session == null ||
            session.Group == null ||
            session.Order == null ||
            session.Mode != BistroBuilderServiceMode.WaitingAtBar)
        {
            error = "La sesión WaitingAtBar no es válida para cerrarse.";
            return false;
        }

        if (!orderSystem.CompleteOrder(session.Order))
        {
            error = "No se pudo cerrar la comanda de espera en barra.";
            return false;
        }

        // El cargo se registra solo después de confirmar el cierre de la
        // comanda. Así nunca queda un importe huérfano si falla la transición.
        TransferChargeOnce(session);
        session.State = SessionState.Completed;
        session.Routine = null;
        PublishSessionCompleted(session, true);
        CloseAndRemoveSession(session, true);
        tableAssignmentSystem.RequestReevaluation();
        error = string.Empty;
        return true;
    }

    private void TransferChargeOnce(Session session)
    {
        string sourceOrderId = session.Order != null
            ? session.Order.CanonicalOrderId
            : string.Empty;

        for (int index = 0; index < transferredCharges.Count; index++)
        {
            if (string.Equals(
                    transferredCharges[index].SourceOrderId,
                    sourceOrderId,
                    StringComparison.Ordinal
                ))
            {
                return;
            }
        }

        transferredCharges.Add(
            new TransferredCharge
            {
                GroupId = session.Group.GroupId,
                SourceOrderId = sourceOrderId,
                AmountCents = Mathf.Max(0, session.ChargeCents),
                Settled = false
            }
        );

        Log(
            "Cargo de barra " + FormatCents(session.ChargeCents) +
            " transferido una sola vez a la futura cuenta del grupo " +
            session.Group.GroupId + "."
        );
    }

    private void CloseAndRemoveSession(Session session, bool keepWaiting)
    {
        if (session == null)
        {
            return;
        }

        if (session.Order != null)
        {
            sessionsByOrder.Remove(session.Order);
        }

        sessionsByGroup.Remove(session.Group);

        if (session.Group != null && session.Group.HasAssignedBarSpot)
        {
            if (barRegistry != null)
            {
                barRegistry.ReleaseGroup(session.Group);
            }
            else
            {
                session.Group.TryReleaseBarSpot();
            }
        }

        waitingAreaSystem?.RefreshWaitingQueue();

        if (keepWaiting && session.Group != null &&
            session.Group.CurrentState != CustomerGroupState.WaitingForTable)
        {
            session.Group.SetState(CustomerGroupState.WaitingForTable);
        }
    }

    public bool TryGetSessionSnapshot(
        CustomerGroup group,
        out BistroBuilderBarSessionSnapshot snapshot
    )
    {
        snapshot = default;

        if (group == null ||
            !sessionsByGroup.TryGetValue(group, out Session session) ||
            session == null)
        {
            return false;
        }

        int totalLines = 0;

        if (session.Order != null &&
            TryGetCanonicalOrder(session.Order, out var orderSnapshot, out _))
        {
            totalLines = orderSnapshot.Lines.Count;
        }

        int reservedSpotCount = barRegistry != null
            ? barRegistry.GetOccupiedSpots(group, occupiedSpotBuffer)
            : group.HasAssignedBarSpot ? 1 : 0;
        int reservedCapacity = barRegistry != null
            ? barRegistry.GetReservedCapacity(group)
            : reservedSpotCount;

        snapshot = new BistroBuilderBarSessionSnapshot(
            group.GroupId,
            session.Spot != null ? session.Spot.BarSpotId : string.Empty,
            reservedSpotCount,
            reservedCapacity,
            session.Mode,
            session.Order != null ? session.Order.CanonicalOrderId : string.Empty,
            ToPublicPhase(session.State),
            session.ChargeCents,
            session.ServedLineIds.Count,
            totalLines
        );

        return true;
    }


    public void UnregisterCustomerGroupForRuntimeLoad(CustomerGroup group)
    {
        if (group == null)
        {
            return;
        }

        if (sessionsByGroup.TryGetValue(group, out Session session))
        {
            StopSessionRoutine(session);
            ReleaseSessionWaiter(session);
            sessionsByGroup.Remove(group);
            if (session.Order != null)
            {
                sessionsByOrder.Remove(session.Order);
            }
        }

        registeredGroups.Remove(group);
        group.StateChanged -= HandleGroupStateChanged;

        CustomerMovementView movement = group.GetComponent<CustomerMovementView>();
        if (movement != null)
        {
            movement.DestinationReached -= HandleCustomerDestinationReached;
            groupByMovementView.Remove(movement);
        }
    }

    public void ClearRuntimeForLoad()
    {
        var sessions = new List<Session>(sessionsByGroup.Values);
        for (int index = 0; index < sessions.Count; index++)
        {
            StopSessionRoutine(sessions[index]);
            ReleaseSessionWaiter(sessions[index]);
        }

        sessionsByGroup.Clear();
        sessionsByOrder.Clear();
        waiterAssignments.Clear();
        transferredCharges.Clear();
    }

    public bool TryCaptureRuntimeSaveRecords(
        List<BistroBuilderBarSessionSaveRecord> sessionDestination,
        List<BistroBuilderTransferredBarChargeSaveRecord> chargeDestination,
        out string error
    )
    {
        error = string.Empty;

        if (sessionDestination == null || chargeDestination == null)
        {
            error = "Los destinos de snapshot de barra son nulos.";
            return false;
        }

        sessionDestination.Clear();
        chargeDestination.Clear();

        foreach (KeyValuePair<CustomerGroup, Session> pair in sessionsByGroup)
        {
            Session session = pair.Value;
            if (session == null || session.Group == null || session.Spot == null)
            {
                error = "Existe una sesión de barra incompleta.";
                return false;
            }

            var occupied = new List<BistroBuilderBarServiceSpot>();
            barRegistry.GetOccupiedSpots(session.Group, occupied);
            var record = new BistroBuilderBarSessionSaveRecord
            {
                groupId = session.Group.GroupId,
                anchorBarSpotId = session.Spot.BarSpotId,
                serviceMode = (int)session.Mode,
                canonicalOrderId = session.Order != null
                    ? session.Order.CanonicalOrderId
                    : string.Empty,
                phase = (int)ToPublicPhase(session.State),
                chargeCents = session.ChargeCents,
                tableRequested = session.TableRequested
            };

            for (int index = 0; index < occupied.Count; index++)
            {
                record.occupiedBarSpotIds.Add(occupied[index].BarSpotId);
            }
            foreach (string lineId in session.ServedLineIds)
            {
                record.servedLineIds.Add(lineId);
            }
            sessionDestination.Add(record);
        }

        for (int index = 0; index < transferredCharges.Count; index++)
        {
            TransferredCharge charge = transferredCharges[index];
            chargeDestination.Add(new BistroBuilderTransferredBarChargeSaveRecord
            {
                groupId = charge.GroupId,
                sourceOrderId = charge.SourceOrderId,
                amountCents = charge.AmountCents,
                settled = charge.Settled
            });
        }

        return true;
    }

    public bool TryRestoreRuntimeSaveRecords(
        IList<BistroBuilderBarSessionSaveRecord> sessionRecords,
        IList<BistroBuilderTransferredBarChargeSaveRecord> chargeRecords,
        IReadOnlyDictionary<int, CustomerGroup> groupsById,
        IReadOnlyDictionary<string, RestaurantOrder> ordersByCanonicalId,
        out string error
    )
    {
        error = string.Empty;
        ResolveDependencies();
        ClearRuntimeForLoad();

        if (sessionRecords == null || chargeRecords == null ||
            groupsById == null || ordersByCanonicalId == null ||
            barRegistry == null)
        {
            error = "Faltan datos para restaurar el servicio de barra.";
            return false;
        }

        for (int index = 0; index < chargeRecords.Count; index++)
        {
            BistroBuilderTransferredBarChargeSaveRecord record =
                chargeRecords[index];
            if (record == null || !record.TryValidate(out error))
            {
                return false;
            }
            transferredCharges.Add(new TransferredCharge
            {
                GroupId = record.groupId,
                SourceOrderId = record.sourceOrderId,
                AmountCents = record.amountCents,
                Settled = record.settled
            });
        }

        for (int index = 0; index < sessionRecords.Count; index++)
        {
            BistroBuilderBarSessionSaveRecord record = sessionRecords[index];
            if (record == null || !record.TryValidate(out error) ||
                !groupsById.TryGetValue(record.groupId, out CustomerGroup group) ||
                group == null || group.AssignedBarSpot == null)
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "No se pudo resolver una sesión de barra.";
                }
                return false;
            }

            RestaurantOrder order = null;
            if (!string.IsNullOrWhiteSpace(record.canonicalOrderId) &&
                !ordersByCanonicalId.TryGetValue(record.canonicalOrderId, out order))
            {
                error = "No se pudo resolver la comanda de una sesión de barra.";
                return false;
            }

            var session = new Session
            {
                Group = group,
                Spot = group.AssignedBarSpot,
                Mode = (BistroBuilderServiceMode)record.serviceMode,
                Order = order,
                State = ResolveRestoredSessionState(record, order),
                ChargeCents = record.chargeCents,
                TableRequested = record.tableRequested
            };

            for (int lineIndex = 0; lineIndex < record.servedLineIds.Count; lineIndex++)
            {
                session.ServedLineIds.Add(record.servedLineIds[lineIndex]);
            }

            sessionsByGroup.Add(group, session);
            if (order != null)
            {
                sessionsByOrder.Add(order, session);
            }
        }

        return true;
    }

    public void ResumeRestoredRuntime()
    {
        foreach (Session session in sessionsByGroup.Values)
        {
            if (session == null || session.Group == null)
            {
                continue;
            }

            switch (session.State)
            {
                case SessionState.CustomerWalking:
                    session.Group.NotifyRestoredRuntimeState();
                    break;
                case SessionState.WaitingForOrderWaiter:
                    TryAssignWaiterForSession(session, false);
                    break;
                case SessionState.WaitingForItems:
                    break;
                case SessionState.Consuming:
                    session.Routine = StartCoroutine(ConsumeAndCloseRoutine(session));
                    break;
                case SessionState.WaitingForPaymentWaiter:
                    TryAssignWaiterForSession(session, true);
                    break;
                case SessionState.ClosingForTable:
                case SessionState.WaitingForTableAfterConsumption:
                    if (!TryCloseWaitingSessionForTable(session, out _))
                    {
                        session.State = SessionState.WaitingForTableAfterConsumption;
                    }
                    break;
            }
        }
    }

    private SessionState ResolveRestoredSessionState(
        BistroBuilderBarSessionSaveRecord record,
        RestaurantOrder order
    )
    {
        BistroBuilderBarSessionPhase phase =
            (BistroBuilderBarSessionPhase)record.phase;

        if (phase == BistroBuilderBarSessionPhase.ClosingForTable)
        {
            return SessionState.ClosingForTable;
        }

        if (phase ==
            BistroBuilderBarSessionPhase.WaitingForTableAfterConsumption)
        {
            return SessionState.WaitingForTableAfterConsumption;
        }

        if (phase == BistroBuilderBarSessionPhase.WalkingToBar ||
            phase == BistroBuilderBarSessionPhase.Allocated)
        {
            return SessionState.CustomerWalking;
        }

        if (order == null)
        {
            return SessionState.WaitingForOrderWaiter;
        }

        if (order.CurrentState == OrderState.Served)
        {
            return SessionState.Consuming;
        }

        if (order.CurrentState == OrderState.Completed)
        {
            return record.serviceMode == (int)BistroBuilderServiceMode.WaitingAtBar
                ? SessionState.ClosingForTable
                : SessionState.WaitingForPaymentWaiter;
        }

        return SessionState.WaitingForItems;
    }

    private bool AreAllLinesServed(RestaurantOrder order)
    {
        if (!TryGetCanonicalOrder(order, out var snapshot, out _))
        {
            return false;
        }

        for (int index = 0; index < snapshot.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLineState state =
                snapshot.Lines[index].State;

            if (state != BistroBuilderCanonicalOrderLineState.Served &&
                state != BistroBuilderCanonicalOrderLineState.Consumed)
            {
                return false;
            }
        }

        return snapshot.Lines.Count > 0;
    }

    private bool AreAllLinesConsumed(RestaurantOrder order)
    {
        if (!TryGetCanonicalOrder(order, out var snapshot, out _))
        {
            return false;
        }

        if (snapshot.Lines.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < snapshot.Lines.Count; index++)
        {
            if (snapshot.Lines[index].State !=
                BistroBuilderCanonicalOrderLineState.Consumed)
            {
                return false;
            }
        }

        return true;
    }

    private void PublishSessionCompleted(Session session, bool transferred)
    {
        if (session == null || session.Group == null)
        {
            return;
        }

        if (session.Mode == BistroBuilderServiceMode.BarService)
        {
            CompletedBarServiceCount++;
        }
        else if (session.Mode == BistroBuilderServiceMode.WaitingAtBar)
        {
            CompletedWaitingAtBarCount++;
        }

        LastCompletedSession = new BistroBuilderBarServiceCompletedEvent(
            session.Group.GroupId,
            session.Order != null ? session.Order.CanonicalOrderId : string.Empty,
            session.Mode,
            session.ChargeCents,
            transferred
        );

        SessionCompleted?.Invoke(LastCompletedSession);
    }

    private void StopSessionRoutine(Session session)
    {
        if (session != null && session.Routine != null)
        {
            StopCoroutine(session.Routine);
            session.Routine = null;
        }
    }

    private void ReleaseSessionWaiter(Session session)
    {
        if (session == null || session.ResponsibleWaiter == null)
        {
            return;
        }

        Waiter responsible = session.ResponsibleWaiter;
        waiterAssignments.Remove(responsible);

        if (ReferenceEquals(responsible.AssignedBarSpot, session.Spot))
        {
            responsible.ClearAssignment();
        }

        session.ResponsibleWaiter = null;
    }

    private static BistroBuilderBarSessionPhase ToPublicPhase(
        SessionState state
    )
    {
        return state switch
        {
            SessionState.Allocated => BistroBuilderBarSessionPhase.Allocated,
            SessionState.CustomerWalking =>
                BistroBuilderBarSessionPhase.WalkingToBar,
            SessionState.WaitingForOrderWaiter =>
                BistroBuilderBarSessionPhase.WaitingForOrder,
            SessionState.TakingOrder =>
                BistroBuilderBarSessionPhase.TakingOrder,
            SessionState.WaitingForItems =>
                BistroBuilderBarSessionPhase.WaitingForItems,
            SessionState.Consuming => BistroBuilderBarSessionPhase.Consuming,
            SessionState.WaitingForPaymentWaiter =>
                BistroBuilderBarSessionPhase.WaitingForPayment,
            SessionState.Paying => BistroBuilderBarSessionPhase.Paying,
            SessionState.ClosingForTable =>
                BistroBuilderBarSessionPhase.ClosingForTable,
            SessionState.WaitingForTableAfterConsumption =>
                BistroBuilderBarSessionPhase.WaitingForTableAfterConsumption,
            SessionState.Completed => BistroBuilderBarSessionPhase.Completed,
            SessionState.Cancelled => BistroBuilderBarSessionPhase.Cancelled,
            _ => BistroBuilderBarSessionPhase.Cancelled
        };
    }

    private void CancelSession(Session session, string reason)
    {
        if (session == null ||
            session.State == SessionState.Completed ||
            session.State == SessionState.Cancelled)
        {
            return;
        }

        StopSessionRoutine(session);
        ReleaseSessionWaiter(session);

        if (session.Order != null && !session.Order.IsFinished)
        {
            orderSystem.CancelOrder(session.Order);
        }

        session.State = SessionState.Cancelled;
        CloseAndRemoveSession(session, false);

        Debug.LogWarning(
            "Sesión de barra cancelada. " + reason,
            this
        );
    }

    private void ResolveDependencies()
    {
        if (barRegistry == null)
        {
            barRegistry = FindFirstObjectByType<
                BistroBuilderBarServiceRegistry
            >();
        }

        if (orderSystem == null)
        {
            orderSystem = FindFirstObjectByType<OrderSystem>();
        }

        if (catalogService == null)
        {
            catalogService = FindFirstObjectByType<
                BistroBuilderDishCatalogService
            >();
        }

        if (menuService == null)
        {
            menuService = FindFirstObjectByType<
                BistroBuilderRestaurantMenuService
            >();
        }

        if (tableAssignmentSystem == null)
        {
            tableAssignmentSystem = FindFirstObjectByType<
                TableAssignmentSystem
            >();
        }

        if (waitingAreaSystem == null)
        {
            waitingAreaSystem = FindFirstObjectByType<
                CustomerWaitingAreaSystem
            >();
        }
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value > 0f;
    }

    private static string FormatCents(int cents)
    {
        return (Mathf.Max(0, cents) / 100f).ToString("0.00") + " €";
    }

    private void Log(string message)
    {
        if (logChanges)
        {
            Debug.Log("367H barra: " + message, this);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maximumItemsPerBarOrder = Mathf.Max(1, maximumItemsPerBarOrder);
        orderTakingDuration = Mathf.Max(0.1f, orderTakingDuration);
        consumptionDuration = Mathf.Max(0.1f, consumptionDuration);
        billDeliveryDuration = Mathf.Max(0.1f, billDeliveryDuration);
        paymentDuration = Mathf.Max(0.1f, paymentDuration);
    }
#endif
}
