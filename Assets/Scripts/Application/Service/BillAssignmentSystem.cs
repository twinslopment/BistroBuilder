using System.Collections.Generic;
using UnityEngine;

public sealed class BillAssignmentSystem : MonoBehaviour
{
    [Header("Elementos gestionados")]
    [SerializeField]
    private RestaurantTable[] tables;

    [SerializeField]
    private Waiter[] waiters;

    private readonly List<RestaurantTable> waitingTables = new();

    private void OnEnable()
    {
        if (tables != null)
        {
            foreach (RestaurantTable table in tables)
            {
                if (table != null)
                    table.StateChanged += HandleTableStateChanged;
            }
        }

        if (waiters != null)
        {
            foreach (Waiter waiter in waiters)
            {
                if (waiter != null)
                    waiter.StateChanged += HandleWaiterStateChanged;
            }
        }
    }

    private void Start()
    {
        ValidateConfiguration();

        if (tables == null)
            return;

        foreach (RestaurantTable table in tables)
        {
            if (table != null &&
                table.CurrentState == TableState.WaitingForBill)
            {
                AddWaitingTable(table);
            }
        }

        TryDispatchBills();
    }

    private void OnDisable()
    {
        if (tables != null)
        {
            foreach (RestaurantTable table in tables)
            {
                if (table != null)
                    table.StateChanged -= HandleTableStateChanged;
            }
        }

        if (waiters != null)
        {
            foreach (Waiter waiter in waiters)
            {
                if (waiter != null)
                    waiter.StateChanged -= HandleWaiterStateChanged;
            }
        }
    }

    private void HandleTableStateChanged(
        RestaurantTable table,
        TableState newState
    )
    {
        if (newState != TableState.WaitingForBill)
            return;

        AddWaitingTable(table);
        TryDispatchBills();
    }

    private void HandleWaiterStateChanged(
        Waiter waiter,
        WaiterState newState
    )
    {
        if (newState == WaiterState.Idle)
            TryDispatchBills();
    }

    private void AddWaitingTable(RestaurantTable table)
    {
        if (table == null)
            return;

        if (!waitingTables.Contains(table))
            waitingTables.Add(table);
    }

    private void TryDispatchBills()
    {
        int tableIndex = 0;

        while (tableIndex < waitingTables.Count)
        {
            RestaurantTable table = waitingTables[tableIndex];

            if (table == null ||
                table.CurrentState != TableState.WaitingForBill ||
                table.AssignedCustomerGroup == null)
            {
                waitingTables.RemoveAt(tableIndex);
                continue;
            }

            if (IsTableAlreadyAssigned(table))
            {
                waitingTables.RemoveAt(tableIndex);
                continue;
            }

            Waiter closestWaiter =
                FindClosestAvailableWaiter(table);

            if (closestWaiter == null)
            {
                Debug.Log(
                    $"No hay camareros libres para llevar la cuenta " +
                    $"a la mesa {table.TableId}.",
                    this
                );

                return;
            }

            bool assigned =
                closestWaiter.AssignTableForBill(table);

            if (!assigned)
            {
                tableIndex++;
                continue;
            }

            waitingTables.RemoveAt(tableIndex);

            Debug.Log(
                $"Cuenta de la mesa {table.TableId} asignada " +
                $"al camarero {closestWaiter.WaiterId}.",
                this
            );
        }
    }

    private Waiter FindClosestAvailableWaiter(
        RestaurantTable table
    )
    {
        if (waiters == null || waiters.Length == 0)
            return null;

        Vector3 destinationPosition =
            table.WaiterServicePoint != null
                ? table.WaiterServicePoint.position
                : table.transform.position;

        Waiter closestWaiter = null;
        float shortestDistanceSquared = float.MaxValue;

        foreach (Waiter waiter in waiters)
        {
            if (waiter == null || !waiter.IsAvailable)
                continue;

            float distanceSquared =
                (waiter.transform.position - destinationPosition)
                .sqrMagnitude;

            if (distanceSquared >= shortestDistanceSquared)
                continue;

            shortestDistanceSquared = distanceSquared;
            closestWaiter = waiter;
        }

        return closestWaiter;
    }

    private bool IsTableAlreadyAssigned(
        RestaurantTable table
    )
    {
        if (waiters == null)
            return false;

        foreach (Waiter waiter in waiters)
        {
            if (waiter != null &&
                waiter.AssignedTable == table)
            {
                return true;
            }
        }

        return false;
    }

    private void ValidateConfiguration()
    {
        if (tables == null || tables.Length == 0)
        {
            Debug.LogError(
                "BillAssignmentSystem no tiene mesas configuradas.",
                this
            );
        }

        if (waiters == null || waiters.Length == 0)
        {
            Debug.LogError(
                "BillAssignmentSystem no tiene camareros configurados.",
                this
            );
        }
    }
}