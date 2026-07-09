using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class TableAssignmentSystem : MonoBehaviour
{
    [Header("Elementos iniciales")]
    [FormerlySerializedAs("customerGroups")]
    [SerializeField]
    private CustomerGroup[] initialCustomerGroups;

    [Header("Mesas gestionadas")]
    [SerializeField]
    private RestaurantTable[] tables;

    private readonly List<CustomerGroup> registeredGroups = new();
    private readonly List<CustomerGroup> waitingGroups = new();

    public IReadOnlyList<CustomerGroup> RegisteredGroups =>
        registeredGroups;

    private void OnEnable()
    {
        SubscribeToTables();
        RegisterInitialCustomerGroups();
    }

    private void Start()
    {
        ValidateConfiguration();
        TryAssignWaitingGroups();
    }

    private void OnDisable()
    {
        UnsubscribeFromTables();
        UnsubscribeFromCustomerGroups();

        registeredGroups.Clear();
        waitingGroups.Clear();
    }

    public bool RegisterCustomerGroup(CustomerGroup customerGroup)
    {
        if (customerGroup == null)
            return false;

        if (registeredGroups.Contains(customerGroup))
            return false;

        registeredGroups.Add(customerGroup);

        customerGroup.StateChanged +=
            HandleCustomerGroupStateChanged;

        Debug.Log(
            $"Grupo {customerGroup.GroupId} registrado " +
            "en el sistema de asignación de mesas.",
            customerGroup
        );

        if (customerGroup.CurrentState ==
            CustomerGroupState.WaitingForTable)
        {
            AddWaitingGroup(customerGroup);
            TryAssignWaitingGroups();
        }

        return true;
    }

    public bool UnregisterCustomerGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null)
            return false;

        if (!registeredGroups.Remove(customerGroup))
            return false;

        customerGroup.StateChanged -=
            HandleCustomerGroupStateChanged;

        waitingGroups.Remove(customerGroup);

        Debug.Log(
            $"Grupo {customerGroup.GroupId} eliminado " +
            "del sistema de asignación de mesas.",
            customerGroup
        );

        return true;
    }

    private void RegisterInitialCustomerGroups()
    {
        if (initialCustomerGroups == null)
            return;

        foreach (CustomerGroup customerGroup in initialCustomerGroups)
        {
            RegisterCustomerGroup(customerGroup);
        }
    }

    private void UnsubscribeFromCustomerGroups()
    {
        foreach (CustomerGroup customerGroup in registeredGroups)
        {
            if (customerGroup != null)
            {
                customerGroup.StateChanged -=
                    HandleCustomerGroupStateChanged;
            }
        }
    }

    private void SubscribeToTables()
    {
        if (tables == null)
            return;

        foreach (RestaurantTable table in tables)
        {
            if (table != null)
            {
                table.StateChanged +=
                    HandleTableStateChanged;
            }
        }
    }

    private void UnsubscribeFromTables()
    {
        if (tables == null)
            return;

        foreach (RestaurantTable table in tables)
        {
            if (table != null)
            {
                table.StateChanged -=
                    HandleTableStateChanged;
            }
        }
    }

    private void HandleCustomerGroupStateChanged(
        CustomerGroup customerGroup,
        CustomerGroupState newState
    )
    {
        if (newState == CustomerGroupState.WaitingForTable)
        {
            AddWaitingGroup(customerGroup);
            TryAssignWaitingGroups();
            return;
        }

        waitingGroups.Remove(customerGroup);

        if (newState == CustomerGroupState.Finished)
        {
            UnregisterCustomerGroup(customerGroup);
        }
    }

    private void HandleTableStateChanged(
        RestaurantTable table,
        TableState newState
    )
    {
        if (newState == TableState.Free)
            TryAssignWaitingGroups();
    }

    private void AddWaitingGroup(CustomerGroup customerGroup)
    {
        if (customerGroup == null)
            return;

        if (!waitingGroups.Contains(customerGroup))
            waitingGroups.Add(customerGroup);
    }

    private void TryAssignWaitingGroups()
    {
        int groupIndex = 0;

        while (groupIndex < waitingGroups.Count)
        {
            CustomerGroup customerGroup =
                waitingGroups[groupIndex];

            if (customerGroup == null ||
                customerGroup.CurrentState !=
                    CustomerGroupState.WaitingForTable ||
                customerGroup.HasAssignedTable)
            {
                waitingGroups.RemoveAt(groupIndex);
                continue;
            }

            RestaurantTable bestTable =
                FindBestTableForGroup(customerGroup);

            if (bestTable == null)
            {
                Debug.Log(
                    $"No hay una mesa adecuada disponible para " +
                    $"el grupo {customerGroup.GroupId}.",
                    this
                );

                groupIndex++;
                continue;
            }

            bool assigned =
                customerGroup.AssignTable(bestTable);

            if (!assigned)
            {
                groupIndex++;
                continue;
            }

            waitingGroups.RemoveAt(groupIndex);
            customerGroup.ResetWaitingTime();

            bestTable.SetState(
                TableState.WaitingForWaiter
            );

            customerGroup.SetState(
                CustomerGroupState.WalkingToTable
            );

            Debug.Log(
                $"TableAssignmentSystem asignó la mesa " +
                $"{bestTable.TableId} al grupo " +
                $"{customerGroup.GroupId}.",
                this
            );
        }
    }

    private RestaurantTable FindBestTableForGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null ||
            tables == null ||
            tables.Length == 0)
        {
            return null;
        }

        RestaurantTable bestTable = null;
        int lowestUnusedCapacity = int.MaxValue;
        float shortestDistanceSquared = float.MaxValue;

        foreach (RestaurantTable table in tables)
        {
            if (table == null)
                continue;

            if (!table.CanSeatGroup(customerGroup.GroupSize))
                continue;

            int unusedCapacity =
                table.Capacity -
                customerGroup.GroupSize;

            Vector3 destinationPosition =
                table.CustomerApproachPoint != null
                    ? table.CustomerApproachPoint.position
                    : table.transform.position;

            float distanceSquared =
                (
                    customerGroup.transform.position -
                    destinationPosition
                ).sqrMagnitude;

            bool hasBetterCapacity =
                unusedCapacity < lowestUnusedCapacity;

            bool sameCapacityButCloser =
                unusedCapacity == lowestUnusedCapacity &&
                distanceSquared < shortestDistanceSquared;

            if (!hasBetterCapacity &&
                !sameCapacityButCloser)
            {
                continue;
            }

            bestTable = table;
            lowestUnusedCapacity = unusedCapacity;
            shortestDistanceSquared = distanceSquared;
        }

        return bestTable;
    }

    private void ValidateConfiguration()
    {
        if (tables == null || tables.Length == 0)
        {
            Debug.LogError(
                "TableAssignmentSystem no tiene mesas configuradas.",
                this
            );
        }
    }
}