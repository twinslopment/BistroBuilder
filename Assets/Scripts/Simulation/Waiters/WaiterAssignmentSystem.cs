using UnityEngine;

/// <summary>
/// Asigna automáticamente camareros disponibles a las mesas
/// que se encuentran esperando atención.
///
/// El sistema reacciona en dos situaciones:
/// - Cuando una mesa cambia a WaitingForWaiter.
/// - Cuando un camarero vuelve a estar disponible en estado Idle.
///
/// Esto evita que una mesa quede bloqueada si no había camareros
/// libres en el momento exacto en que comenzó a esperar.
/// </summary>
public sealed class WaiterAssignmentSystem : MonoBehaviour
{
    [Header("Elementos gestionados")]
    [SerializeField]
    private RestaurantTable[] tables;

    [SerializeField]
    private Waiter[] waiters;

    private void OnEnable()
    {
        SubscribeToTables();
        SubscribeToWaiters();
    }

    private void Start()
    {
        ValidateConfiguration();

        // Al comenzar la escena, revisamos si ya existe alguna mesa
        // esperando atención.
        TryAssignWaitingTables();
    }

    private void OnDisable()
    {
        UnsubscribeFromTables();
        UnsubscribeFromWaiters();
    }

    /// <summary>
    /// Escucha los cambios de estado de todas las mesas gestionadas.
    /// </summary>
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

    /// <summary>
    /// Deja de escuchar las mesas cuando el sistema se desactiva.
    /// </summary>
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

    /// <summary>
    /// Escucha los cambios de estado de los camareros.
    ///
    /// Esta suscripción permite volver a revisar las mesas pendientes
    /// cuando un camarero termina una tarea.
    /// </summary>
    private void SubscribeToWaiters()
    {
        if (waiters == null)
            return;

        foreach (Waiter waiter in waiters)
        {
            if (waiter != null)
            {
                waiter.StateChanged +=
                    HandleWaiterStateChanged;
            }
        }
    }

    /// <summary>
    /// Deja de escuchar los camareros cuando el sistema se desactiva.
    /// </summary>
    private void UnsubscribeFromWaiters()
    {
        if (waiters == null)
            return;

        foreach (Waiter waiter in waiters)
        {
            if (waiter != null)
            {
                waiter.StateChanged -=
                    HandleWaiterStateChanged;
            }
        }
    }

    /// <summary>
    /// Reacciona cuando una mesa comienza a esperar un camarero.
    /// </summary>
    private void HandleTableStateChanged(
        RestaurantTable table,
        TableState newState
    )
    {
        if (newState != TableState.WaitingForWaiter)
            return;

        TryAssignWaiter(table);
    }

    /// <summary>
    /// Reacciona cuando un camarero termina una tarea y vuelve a Idle.
    ///
    /// En ese momento se revisan de nuevo todas las mesas que quedaron
    /// pendientes por falta de personal disponible.
    /// </summary>
    private void HandleWaiterStateChanged(
        Waiter waiter,
        WaiterState newState
    )
    {
        if (newState != WaiterState.Idle)
            return;

        TryAssignWaitingTables();
    }

    /// <summary>
    /// Recorre todas las mesas y trata de atender las que continúan
    /// esperando un camarero.
    /// </summary>
    private void TryAssignWaitingTables()
    {
        if (tables == null)
            return;

        foreach (RestaurantTable table in tables)
        {
            if (table == null)
                continue;

            if (table.CurrentState !=
                TableState.WaitingForWaiter)
            {
                continue;
            }

            TryAssignWaiter(table);
        }
    }

    /// <summary>
    /// Intenta asignar el camarero libre más cercano a una mesa.
    /// </summary>
    private bool TryAssignWaiter(
        RestaurantTable table
    )
    {
        if (table == null)
            return false;

        if (table.CurrentState !=
            TableState.WaitingForWaiter)
        {
            return false;
        }

        if (IsTableAlreadyAssigned(table))
            return false;

        Waiter closestWaiter =
            FindClosestAvailableWaiter(table);

        if (closestWaiter == null)
        {
            Debug.Log(
                $"No hay camareros libres para la mesa " +
                $"{table.TableId}.",
                this
            );

            return false;
        }

        return closestWaiter.AssignTable(table);
    }

    /// <summary>
    /// Busca el camarero disponible situado más cerca del punto
    /// de servicio de la mesa.
    /// </summary>
    private Waiter FindClosestAvailableWaiter(
        RestaurantTable table
    )
    {
        if (waiters == null ||
            waiters.Length == 0)
        {
            return null;
        }

        Vector3 destinationPosition =
            table.WaiterServicePoint != null
                ? table.WaiterServicePoint.position
                : table.transform.position;

        Waiter closestWaiter = null;

        float shortestDistanceSquared =
            float.MaxValue;

        foreach (Waiter waiter in waiters)
        {
            if (waiter == null ||
                !waiter.IsAvailable)
            {
                continue;
            }

            float distanceSquared =
                (
                    waiter.transform.position -
                    destinationPosition
                ).sqrMagnitude;

            if (distanceSquared >=
                shortestDistanceSquared)
            {
                continue;
            }

            shortestDistanceSquared =
                distanceSquared;

            closestWaiter = waiter;
        }

        return closestWaiter;
    }

    /// <summary>
    /// Comprueba que la mesa no haya sido asignada ya a otro camarero.
    /// </summary>
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

    /// <summary>
    /// Comprueba que el sistema tenga mesas y camareros configurados.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (tables == null ||
            tables.Length == 0)
        {
            Debug.LogError(
                "WaiterAssignmentSystem no tiene mesas configuradas.",
                this
            );
        }

        if (waiters == null ||
            waiters.Length == 0)
        {
            Debug.LogError(
                "WaiterAssignmentSystem no tiene camareros configurados.",
                this
            );
        }
    }
}