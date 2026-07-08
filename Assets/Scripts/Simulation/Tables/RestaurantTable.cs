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

    [Header("Estado actual")]
    [SerializeField]
    private TableState currentState = TableState.Free;

    public event Action<RestaurantTable, TableState> StateChanged;

    public int TableId => tableId;
    public int Capacity => capacity;
    public Transform CustomerApproachPoint => customerApproachPoint;
    public TableState CurrentState => currentState;

    public bool IsAvailable => currentState == TableState.Free;

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
}