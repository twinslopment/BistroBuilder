using System;
using UnityEngine;

public sealed class Waiter : MonoBehaviour
{
    [Header("Identificación")]
    [SerializeField, Min(1)]
    private int waiterId = 1;

    [Header("Estado actual")]
    [SerializeField]
    private WaiterState currentState = WaiterState.Idle;

    [Header("Asignación actual")]
    [SerializeField]
    private RestaurantTable assignedTable;

    private RestaurantOrder assignedOrder;

    private string assignedOrderLineId = string.Empty;

    public event Action<Waiter, WaiterState> StateChanged;

    public int WaiterId => waiterId;
    public WaiterState CurrentState => currentState;
    public RestaurantTable AssignedTable => assignedTable;
    public RestaurantOrder AssignedOrder => assignedOrder;
    public string AssignedOrderLineId => assignedOrderLineId ?? string.Empty;
    public bool HasAssignedOrderLine =>
        assignedOrder != null &&
        BistroBuilderOrderIdUtility.IsValid(AssignedOrderLineId);

    public bool IsAvailable =>
        currentState == WaiterState.Idle &&
        assignedTable == null &&
        assignedOrder == null &&
        string.IsNullOrEmpty(AssignedOrderLineId);

    public bool AssignTable(RestaurantTable table)
    {
        if (!IsAvailable)
            return false;

        if (table == null)
            return false;

        if (table.CurrentState != TableState.WaitingForWaiter)
            return false;

        assignedTable = table;

        Debug.Log(
            $"Camarero {waiterId} asignado a mesa {table.TableId}.",
            this
        );

        SetState(WaiterState.WalkingToTable);
        return true;
    }

    public bool AssignOrderForPickup(RestaurantOrder order)
    {
        if (!IsAvailable || order == null)
        {
            return false;
        }

        if (order.CurrentState != OrderState.ReadyForPickup)
        {
            return false;
        }

        assignedOrder = order;
        assignedOrderLineId = string.Empty;
        assignedTable = order.Table;

        Debug.Log(
            "Camarero " + waiterId + " asignado para recoger " +
            "la comanda " + order.OrderId + ".",
            this
        );

        SetState(WaiterState.WalkingToKitchen);
        return true;
    }

    /// <summary>
    /// Asigna un plato físico concreto. La autoridad canónica debe reservar la
    /// línea antes de llamar a este método.
    /// </summary>
    public bool AssignOrderLineForPickup(
        RestaurantOrder order,
        string orderLineId
    )
    {
        if (!IsAvailable || order == null || order.Table == null)
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

        assignedOrder = order;
        assignedOrderLineId = normalizedLineId;
        assignedTable = order.Table;

        Debug.Log(
            "Camarero " + waiterId + " asignado para recoger la línea " +
            normalizedLineId + " de la comanda " + order.OrderId + ".",
            this
        );

        SetState(WaiterState.WalkingToKitchen);
        return true;
    }

    public bool AssignTableForBill(RestaurantTable table)
    {
        if (!IsAvailable)
            return false;

        if (table == null)
            return false;

        if (table.CurrentState != TableState.WaitingForBill)
            return false;

        if (table.AssignedCustomerGroup == null)
            return false;

        assignedTable = table;

        Debug.Log(
            $"Camarero {waiterId} asignado para llevar la cuenta " +
            $"a la mesa {table.TableId}.",
            this
        );

        SetState(WaiterState.WalkingToBill);
        return true;
    }

    public bool AssignTableForCleaning(RestaurantTable table)
    {
        if (!IsAvailable)
            return false;

        if (table == null)
            return false;

        if (table.CurrentState != TableState.Dirty)
            return false;

        if (table.AssignedCustomerGroup != null)
            return false;

        assignedTable = table;

        Debug.Log(
            $"Camarero {waiterId} asignado para limpiar " +
            $"la mesa {table.TableId}.",
            this
        );

        SetState(WaiterState.WalkingToCleanTable);
        return true;
    }

    public void SetState(WaiterState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        Debug.Log(
            $"Camarero {waiterId}: estado cambiado a {currentState}.",
            this
        );

        StateChanged?.Invoke(this, currentState);
    }

    public void ClearAssignment()
    {
        assignedTable = null;
        assignedOrder = null;
        assignedOrderLineId = string.Empty;

        SetState(WaiterState.Idle);
    }
}