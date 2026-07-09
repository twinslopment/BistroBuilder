using UnityEngine;

public sealed class WaiterAssignmentSystem : MonoBehaviour
{
    [Header("Elementos gestionados")]
    [SerializeField]
    private RestaurantTable[] tables;

    [SerializeField]
    private Waiter[] waiters;

    private void OnEnable()
    {
        if (tables == null)
            return;

        foreach (RestaurantTable table in tables)
        {
            if (table != null)
                table.StateChanged += HandleTableStateChanged;
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
                table.CurrentState == TableState.WaitingForWaiter)
            {
                TryAssignWaiter(table);
            }
        }
    }

    private void OnDisable()
    {
        if (tables == null)
            return;

        foreach (RestaurantTable table in tables)
        {
            if (table != null)
                table.StateChanged -= HandleTableStateChanged;
        }
    }

    private void HandleTableStateChanged(
        RestaurantTable table,
        TableState newState
    )
    {
        if (newState != TableState.WaitingForWaiter)
            return;

        TryAssignWaiter(table);
    }

    private bool TryAssignWaiter(RestaurantTable table)
    {
        if (table == null)
            return false;

        if (table.CurrentState != TableState.WaitingForWaiter)
            return false;

        if (IsTableAlreadyAssigned(table))
            return false;

        Waiter closestWaiter = FindClosestAvailableWaiter(table);

        if (closestWaiter == null)
        {
            Debug.Log(
                $"No hay camareros libres para la mesa {table.TableId}.",
                this
            );

            return false;
        }

        return closestWaiter.AssignTable(table);
    }

    private Waiter FindClosestAvailableWaiter(RestaurantTable table)
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

    private bool IsTableAlreadyAssigned(RestaurantTable table)
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
                "WaiterAssignmentSystem no tiene mesas configuradas.",
                this
            );
        }

        if (waiters == null || waiters.Length == 0)
        {
            Debug.LogError(
                "WaiterAssignmentSystem no tiene camareros configurados.",
                this
            );
        }
    }
}