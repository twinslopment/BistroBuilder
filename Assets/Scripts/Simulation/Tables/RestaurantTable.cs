using System;
using UnityEngine;

/// <summary>
/// Componente operativo de una mesa del restaurante.
///
/// La identidad de artículo colocable pertenece a
/// RestaurantPlaceableObject.
///
/// TableId es exclusivamente la identidad funcional de la mesa
/// dentro de los sistemas de sala, comandas, cuentas y camareros.
/// RestaurantTableRegistry garantiza que no existan duplicados.
/// </summary>
public sealed class RestaurantTable :
    MonoBehaviour
{
    [Header("Identificación")]

    [SerializeField]
    [Min(1)]
    private int tableId = 1;

    [Header("Configuración")]

    [SerializeField]
    [Min(1)]
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
    private TableState currentState =
        TableState.Free;

    public event Action<
        RestaurantTable,
        TableState
    > StateChanged;

    public event Action<
        RestaurantTable,
        int,
        int
    > TableIdChanged;

    public int TableId
    {
        get
        {
            return tableId;
        }
    }

    public int Capacity
    {
        get
        {
            return capacity;
        }
    }

    public Transform CustomerApproachPoint
    {
        get
        {
            return customerApproachPoint;
        }
    }

    public Transform WaiterServicePoint
    {
        get
        {
            return waiterServicePoint;
        }
    }

    public CustomerGroup AssignedCustomerGroup
    {
        get
        {
            return assignedCustomerGroup;
        }
    }

    public TableState CurrentState
    {
        get
        {
            return currentState;
        }
    }

    public bool IsAvailable
    {
        get
        {
            return currentState ==
                       TableState.Free &&
                   assignedCustomerGroup == null;
        }
    }

    /// <summary>
    /// Asigna la identidad operativa de la mesa.
    ///
    /// Debe utilizarlo RestaurantTableRegistry o el sistema de carga.
    /// </summary>
    public bool AssignTableId(
        int newTableId
    )
    {
        if (newTableId < 1 ||
            tableId == newTableId)
        {
            return false;
        }

        int previousTableId =
            tableId;

        tableId =
            newTableId;

        TableIdChanged?.Invoke(
            this,
            previousTableId,
            tableId
        );

        return true;
    }

    public void SetState(
        TableState newState
    )
    {
        if (currentState == newState)
        {
            return;
        }

        currentState =
            newState;

        Debug.Log(
            "Mesa " +
            tableId +
            ": estado cambiado a " +
            currentState +
            ".",
            this
        );

        StateChanged?.Invoke(
            this,
            currentState
        );
    }

    public bool CanSeatGroup(
        int groupSize
    )
    {
        return IsAvailable &&
               groupSize > 0 &&
               groupSize <= capacity;
    }

    public bool TryAssignCustomerGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null ||
            !CanSeatGroup(
                customerGroup.GroupSize
            ))
        {
            return false;
        }

        assignedCustomerGroup =
            customerGroup;

        Debug.Log(
            "Mesa " +
            tableId +
            ": grupo " +
            customerGroup.GroupId +
            " registrado.",
            this
        );

        return true;
    }

    public void ReleaseCustomerGroup(
        CustomerGroup customerGroup
    )
    {
        if (!ReferenceEquals(
                assignedCustomerGroup,
                customerGroup
            ))
        {
            return;
        }

        assignedCustomerGroup =
            null;

        Debug.Log(
            "Mesa " +
            tableId +
            ": grupo liberado.",
            this
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        tableId =
            Mathf.Max(
                1,
                tableId
            );

        capacity =
            Mathf.Max(
                1,
                capacity
            );
    }
#endif
}
