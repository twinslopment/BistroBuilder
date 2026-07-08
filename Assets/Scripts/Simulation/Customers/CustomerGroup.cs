using System;
using UnityEngine;

public sealed class CustomerGroup : MonoBehaviour
{
    [Header("Identificación")]
    [SerializeField, Min(1)]
    private int groupId = 1;

    [Header("Configuración")]
    [SerializeField, Min(1)]
    private int groupSize = 2;

    [Header("Estado actual")]
    [SerializeField]
    private CustomerGroupState currentState =
        CustomerGroupState.Entering;

    [Header("Información durante el servicio")]
    [SerializeField, Min(0)]
    private int waitingMinutes;

    [SerializeField]
    private RestaurantTable assignedTable;

    public event Action<CustomerGroup, CustomerGroupState> StateChanged;

    public int GroupId => groupId;
    public int GroupSize => groupSize;
    public CustomerGroupState CurrentState => currentState;
    public int WaitingMinutes => waitingMinutes;
    public RestaurantTable AssignedTable => assignedTable;

    public bool HasAssignedTable => assignedTable != null;

    public void SetState(CustomerGroupState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        Debug.Log(
            $"Grupo {groupId}: estado cambiado a {currentState}.",
            this
        );

        StateChanged?.Invoke(this, currentState);
    }

    public bool CanUseTable(RestaurantTable table)
    {
        return table != null &&
               table.CanSeatGroup(groupSize);
    }

    public bool AssignTable(RestaurantTable table)
    {
        if (!CanUseTable(table))
            return false;

        assignedTable = table;

        Debug.Log(
            $"Grupo {groupId} asignado a mesa {table.TableId}.",
            this
        );

        return true;
    }

    public void ClearAssignedTable()
    {
        assignedTable = null;
    }

    public void AddWaitingMinutes(int minutes)
    {
        if (minutes <= 0)
            return;

        if (currentState != CustomerGroupState.WaitingForTable)
            return;

        waitingMinutes += minutes;
    }

    public void ResetWaitingTime()
    {
        waitingMinutes = 0;
    }
}