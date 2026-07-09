using System;
using UnityEngine;

public sealed class RestaurantTable : MonoBehaviour
{
    [Header("Identificación")]
    [SerializeField, Min(1)]
    private int tableId = 1;

    [Header("Configuración")]
    [SerializeField, Min(1)]
    private int capacity = 2;

    [Header("Puntos de interacción")]
    [SerializeField]
    private Transform customerApproachPoint;

    [SerializeField]
    private Transform waiterServicePoint;

    [Header("Ocupación actual")]
    [SerializeField]
    private CustomerGroup assignedCustomerGroup;

    [Header("Estado actual")]
    [SerializeField]
    private TableState currentState = TableState.Free;

    public event Action<RestaurantTable, TableState> StateChanged;

    public int TableId => tableId;
    public int Capacity => capacity;

    public Transform CustomerApproachPoint => customerApproachPoint;
    public Transform WaiterServicePoint => waiterServicePoint;

    public CustomerGroup AssignedCustomerGroup =>
        assignedCustomerGroup;

    public TableState CurrentState => currentState;

    public bool IsAvailable =>
        currentState == TableState.Free &&
        assignedCustomerGroup == null;

    public void SetState(TableState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        Debug.Log(
            $"Mesa {tableId}: estado cambiado a {currentState}.",
            this
        );

        StateChanged?.Invoke(this, currentState);
    }

    public bool CanSeatGroup(int groupSize)
    {
        return IsAvailable &&
               groupSize > 0 &&
               groupSize <= capacity;
    }

    public bool TryAssignCustomerGroup(CustomerGroup customerGroup)
    {
        if (customerGroup == null)
            return false;

        if (!CanSeatGroup(customerGroup.GroupSize))
            return false;

        assignedCustomerGroup = customerGroup;

        Debug.Log(
            $"Mesa {tableId}: grupo {customerGroup.GroupId} registrado.",
            this
        );

        return true;
    }

    public void ReleaseCustomerGroup(CustomerGroup customerGroup)
    {
        if (assignedCustomerGroup != customerGroup)
            return;

        assignedCustomerGroup = null;

        Debug.Log(
            $"Mesa {tableId}: grupo liberado.",
            this
        );
    }
}