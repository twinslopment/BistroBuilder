using System;
using UnityEngine;

/// <summary>
/// Estado operativo y asignación actual de un camarero.
///
/// Desde 367H una asignación alimentaria puede tener como destino una mesa
/// o una plaza real de barra. 4D añade únicamente una elegibilidad de sesión:
/// Waiter sigue siendo el agente operativo y Employee continúa fuera de este
/// componente.
/// </summary>
public sealed class Waiter : MonoBehaviour
{
    public const string RuntimeRevision = "367H";

    [Header("Identificación")]
    [SerializeField, Min(1)]
    private int waiterId = 1;

    [Header("Capacidad de reparto")]
    [SerializeField, Min(1)]
    private int foodDeliveryCapacity = 3;

    [Header("Estado actual")]
    [SerializeField]
    private WaiterState currentState = WaiterState.Idle;

    [Header("Asignación actual")]
    [SerializeField]
    private RestaurantTable assignedTable;

    [SerializeField]
    private BistroBuilderBarServiceSpot assignedBarSpot;

    private RestaurantOrder assignedOrder;
    private string assignedOrderLineId = string.Empty;
    private BistroBuilderDeliveryRun assignedDeliveryRun;

    // 4D: filtro puramente runtime. No se serializa en escena ni Save; el
    // binding de Personal lo reconstruye para cada sesión. Su valor por
    // defecto true conserva exactamente el comportamiento de instalaciones
    // anteriores que todavía no tengan Personal.
    private bool staffServiceEligible = true;

    public event Action<Waiter, WaiterState> StateChanged;

    public int WaiterId => waiterId;
    public int FoodDeliveryCapacity => Mathf.Max(1, foodDeliveryCapacity);
    public WaiterState CurrentState => currentState;
    public RestaurantTable AssignedTable => assignedTable;
    public BistroBuilderBarServiceSpot AssignedBarSpot => assignedBarSpot;
    public RestaurantOrder AssignedOrder => assignedOrder;
    public string AssignedOrderLineId => assignedOrderLineId ?? string.Empty;
    public BistroBuilderDeliveryRun AssignedDeliveryRun => assignedDeliveryRun;
    public bool IsStaffServiceEligible => staffServiceEligible;

    public BistroBuilderServiceDestinationKind AssignedDestinationKind =>
        assignedTable != null
            ? BistroBuilderServiceDestinationKind.Table
            : assignedBarSpot != null
                ? BistroBuilderServiceDestinationKind.BarSpot
                : BistroBuilderServiceDestinationKind.None;

    public string AssignedDestinationReferenceId =>
        BistroBuilderServiceModeUtility.BuildDestinationReference(
            assignedTable,
            assignedBarSpot);

    public Transform AssignedWaiterServicePoint =>
        BistroBuilderServiceModeUtility.GetWaiterServicePoint(
            assignedTable,
            assignedBarSpot);

    public bool HasAssignedDeliveryRun =>
        assignedDeliveryRun != null && !assignedDeliveryRun.IsTerminal;

    public bool HasAssignedOrderLine =>
        assignedOrder != null &&
        BistroBuilderOrderIdUtility.IsValid(AssignedOrderLineId);

    /// <summary>
    /// Disponibilidad operativa existente más la elegibilidad de sesión 4D.
    /// Todos los asignadores legacy y modernos ya consultan esta propiedad o
    /// los métodos Assign*, por lo que un agente sin EmployeeId no puede
    /// ejecutar trabajo sin crear un segundo sistema de camareros.
    /// </summary>
    public bool IsAvailable =>
        staffServiceEligible && HasNoOperationalAssignment;

    /// <summary>
    /// 4D activa un agente solo cuando existe un binding EmployeeId ↔ WaiterId.
    /// Desactivar un agente ocupado se rechaza para no romper una tarea real.
    /// </summary>
    public bool TrySetStaffServiceEligibility(bool eligible)
    {
        if (staffServiceEligible == eligible)
        {
            return true;
        }

        if (!eligible && !HasNoOperationalAssignment)
        {
            return false;
        }

        staffServiceEligible = eligible;
        return true;
    }

    public bool AssignTable(RestaurantTable table)
    {
        if (!IsAvailable || table == null ||
            table.CurrentState != TableState.WaitingForWaiter)
        {
            return false;
        }

        assignedTable = table;
        Debug.Log(
            $"Camarero {waiterId} asignado a mesa {table.TableId}.",
            this);
        SetState(WaiterState.WalkingToTable);
        return true;
    }

    public bool AssignBarSpot(
        BistroBuilderBarServiceSpot barSpot,
        RestaurantOrder order,
        WaiterState walkingState)
    {
        if (!IsAvailable || barSpot == null ||
            (walkingState != WaiterState.WalkingToBar &&
             walkingState != WaiterState.WalkingToBarBill))
        {
            return false;
        }

        assignedBarSpot = barSpot;
        assignedOrder = order;
        assignedOrderLineId = string.Empty;
        SetState(walkingState);
        return true;
    }

    public bool AssignOrderForPickup(RestaurantOrder order)
    {
        if (!IsAvailable || order == null ||
            order.CurrentState != OrderState.ReadyForPickup ||
            !order.HasValidDestination)
        {
            return false;
        }

        SetFoodDestination(order);
        assignedOrder = order;
        assignedOrderLineId = string.Empty;
        Debug.Log(
            "Camarero " + waiterId + " asignado para recoger " +
            "la comanda " + order.OrderId + ".",
            this);
        SetState(WaiterState.WalkingToKitchen);
        return true;
    }

    public bool AssignOrderLineForPickup(
        RestaurantOrder order,
        string orderLineId)
    {
        if (!IsAvailable || order == null || !order.HasValidDestination)
        {
            return false;
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);
        if (!order.HasCanonicalOrder ||
            !BistroBuilderOrderIdUtility.IsValid(normalizedLineId))
        {
            return false;
        }

        SetFoodDestination(order);
        assignedOrder = order;
        assignedOrderLineId = normalizedLineId;
        Debug.Log(
            "Camarero " + waiterId + " asignado para recoger la línea " +
            normalizedLineId + " de la comanda " + order.OrderId + ".",
            this);
        SetState(WaiterState.WalkingToKitchen);
        return true;
    }

    public bool AssignDeliveryRun(BistroBuilderDeliveryRun deliveryRun)
    {
        if (!IsAvailable || deliveryRun == null ||
            deliveryRun.Items.Count == 0 ||
            deliveryRun.Items.Count > FoodDeliveryCapacity ||
            !deliveryRun.TryAssignWaiter(this))
        {
            return false;
        }

        assignedDeliveryRun = deliveryRun;
        BistroBuilderDeliveryRunItem firstItem = deliveryRun.Items[0];
        SetDestination(firstItem.Table, firstItem.BarSpot);
        assignedOrder = firstItem.Order;
        assignedOrderLineId = firstItem.OrderLineId;
        Debug.Log(
            "Camarero " + waiterId + " acepta la ronda " +
            deliveryRun.RunId + " con " + deliveryRun.Items.Count +
            " plato(s) y " + deliveryRun.Stops.Count + " parada(s).",
            this);
        SetState(WaiterState.WalkingToKitchen);
        return true;
    }

    public bool HasDeliveryLine(RestaurantOrder order, string orderLineId)
    {
        return assignedDeliveryRun != null &&
               assignedDeliveryRun.ContainsLine(order, orderLineId);
    }

    public bool TryBeginDeliveryRunStops()
    {
        if (assignedDeliveryRun == null ||
            !assignedDeliveryRun.TryBeginDelivery())
        {
            return false;
        }
        return SynchronizeCurrentDeliveryStop();
    }

    public bool TryAdvanceDeliveryRunStop()
    {
        if (assignedDeliveryRun == null ||
            !assignedDeliveryRun.TryAdvanceStop())
        {
            return false;
        }
        return SynchronizeCurrentDeliveryStop();
    }

    public bool TrySelectDeliveryLine(
        RestaurantOrder order,
        string orderLineId)
    {
        if (assignedDeliveryRun == null ||
            assignedDeliveryRun.CurrentStop == null ||
            !assignedDeliveryRun.TryGetItem(
                order,
                orderLineId,
                out BistroBuilderDeliveryRunItem item) ||
            !assignedDeliveryRun.CurrentStop.ContainsDestinationOf(item))
        {
            return false;
        }

        SetDestination(item.Table, item.BarSpot);
        assignedOrder = item.Order;
        assignedOrderLineId = item.OrderLineId;
        return true;
    }

    public bool AssignTableForBill(RestaurantTable table)
    {
        if (!IsAvailable || table == null ||
            table.CurrentState != TableState.WaitingForBill ||
            table.AssignedCustomerGroup == null)
        {
            return false;
        }

        assignedTable = table;
        Debug.Log(
            $"Camarero {waiterId} asignado para llevar la cuenta " +
            $"a la mesa {table.TableId}.",
            this);
        SetState(WaiterState.WalkingToBill);
        return true;
    }

    public bool AssignTableForCleaning(RestaurantTable table)
    {
        if (!IsAvailable || table == null ||
            table.CurrentState != TableState.Dirty ||
            table.AssignedCustomerGroup != null)
        {
            return false;
        }

        assignedTable = table;
        Debug.Log(
            $"Camarero {waiterId} asignado para limpiar " +
            $"la mesa {table.TableId}.",
            this);
        SetState(WaiterState.WalkingToCleanTable);
        return true;
    }

    public void SetState(WaiterState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        currentState = newState;
        Debug.Log(
            $"Camarero {waiterId}: estado cambiado a {currentState}.",
            this);
        StateChanged?.Invoke(this, currentState);
    }

    public void ClearAssignment()
    {
        assignedTable = null;
        assignedBarSpot = null;
        assignedOrder = null;
        assignedOrderLineId = string.Empty;
        assignedDeliveryRun = null;
        SetState(WaiterState.Idle);
    }

    private bool HasNoOperationalAssignment =>
        currentState == WaiterState.Idle &&
        assignedTable == null &&
        assignedBarSpot == null &&
        assignedOrder == null &&
        string.IsNullOrEmpty(AssignedOrderLineId) &&
        assignedDeliveryRun == null;

    private bool SynchronizeCurrentDeliveryStop()
    {
        BistroBuilderDeliveryStop stop = assignedDeliveryRun?.CurrentStop;
        if (stop == null || stop.Items.Count == 0)
        {
            return false;
        }

        BistroBuilderDeliveryRunItem selectedItem = null;
        for (int index = 0; index < stop.Items.Count; index++)
        {
            if (!stop.Items[index].IsFinished)
            {
                selectedItem = stop.Items[index];
                break;
            }
        }
        if (selectedItem == null)
        {
            selectedItem = stop.Items[0];
        }

        SetDestination(stop.Table, stop.BarSpot);
        assignedOrder = selectedItem.Order;
        assignedOrderLineId = selectedItem.OrderLineId;
        return true;
    }

    private void SetFoodDestination(RestaurantOrder order)
    {
        SetDestination(order.Table, order.BarSpot);
    }

    private void SetDestination(
        RestaurantTable table,
        BistroBuilderBarServiceSpot barSpot)
    {
        assignedTable = table;
        assignedBarSpot = barSpot;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        waiterId = Mathf.Max(1, waiterId);
        foodDeliveryCapacity = Mathf.Max(1, foodDeliveryCapacity);
    }
#endif
}
