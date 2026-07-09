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

    public event Action<Waiter, WaiterState> StateChanged;

    public int WaiterId => waiterId;
    public WaiterState CurrentState => currentState;
    public RestaurantTable AssignedTable => assignedTable;
    public RestaurantOrder AssignedOrder => assignedOrder;

    public bool IsAvailable =>
        currentState == WaiterState.Idle &&
        assignedTable == null &&
        assignedOrder == null;

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
        if (!IsAvailable)
            return false;

        if (order == null)
            return false;

        if (order.CurrentState != OrderState.ReadyForPickup)
            return false;

        assignedOrder = order;
        assignedTable = order.Table;

        Debug.Log(
            $"Camarero {waiterId} asignado para recoger " +
            $"la comanda {order.OrderId}.",
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

        SetState(WaiterState.Idle);
    }
}