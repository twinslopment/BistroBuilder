using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la limpieza física de una mesa asignada a un camarero.
///
/// El flujo es:
/// - El camarero llega a la mesa sucia.
/// - Comienza la limpieza.
/// - Espera el tiempo configurado.
/// - Finaliza su tarea y queda disponible.
/// - La mesa vuelve a estar libre.
///
/// El camarero se libera antes de cambiar la mesa a Free para evitar
/// que un nuevo grupo reciba la mesa cuando todavía no hay camarero
/// disponible para atenderlo.
/// </summary>
public sealed class TableCleaningServiceFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private WaiterMovementView waiterMovementView;

    [Header("Duraciones provisionales")]
    [SerializeField, Min(0.1f)]
    private float cleaningDuration = 3f;

    private Coroutine activeRoutine;

    private void OnEnable()
    {
        if (waiterMovementView != null)
        {
            waiterMovementView.DestinationReached +=
                HandleDestinationReached;
        }
    }

    private void OnDisable()
    {
        if (waiterMovementView != null)
        {
            waiterMovementView.DestinationReached -=
                HandleDestinationReached;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    private void Start()
    {
        ValidateConfiguration();
    }

    /// <summary>
    /// Detecta que el camarero ha llegado a su destino.
    /// Solo comienza la limpieza si estaba desplazándose
    /// específicamente hacia una mesa sucia.
    /// </summary>
    private void HandleDestinationReached(
        WaiterMovementView movementView
    )
    {
        if (waiter == null || activeRoutine != null)
            return;

        if (waiter.CurrentState !=
            WaiterState.WalkingToCleanTable)
        {
            return;
        }

        activeRoutine =
            StartCoroutine(CleanTableRoutine());
    }

    /// <summary>
    /// Ejecuta el tiempo de limpieza y devuelve la mesa
    /// al circuito normal de asignación.
    /// </summary>
    private IEnumerator CleanTableRoutine()
    {
        RestaurantTable table =
            waiter.AssignedTable;

        if (table == null)
        {
            Debug.LogError(
                $"El camarero {waiter.WaiterId} no tiene " +
                "una mesa asignada para limpiar.",
                waiter
            );

            activeRoutine = null;
            yield break;
        }

        if (table.CurrentState != TableState.Dirty)
        {
            Debug.LogError(
                $"La mesa {table.TableId} no está sucia.",
                table
            );

            activeRoutine = null;
            yield break;
        }

        if (table.AssignedCustomerGroup != null)
        {
            Debug.LogError(
                $"La mesa {table.TableId} todavía tiene " +
                "un grupo asignado.",
                table
            );

            activeRoutine = null;
            yield break;
        }

        waiter.SetState(
            WaiterState.CleaningTable
        );

        Debug.Log(
            $"Camarero {waiter.WaiterId} comienza a limpiar " +
            $"la mesa {table.TableId}.",
            this
        );

        yield return new WaitForSeconds(
            cleaningDuration
        );

        // Comprobamos que la tarea no haya cambiado mientras
        // transcurría el tiempo de limpieza.
        if (waiter.AssignedTable != table)
        {
            Debug.LogWarning(
                "La asignación del camarero cambió " +
                "durante la limpieza.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        if (table.CurrentState != TableState.Dirty)
        {
            Debug.LogWarning(
                $"La mesa {table.TableId} dejó de estar sucia " +
                "antes de terminar la limpieza.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        Debug.Log(
            $"Camarero {waiter.WaiterId} ha terminado de limpiar " +
            $"la mesa {table.TableId}.",
            this
        );

        // La limpieza ya ha terminado antes de provocar nuevos eventos.
        activeRoutine = null;

        // Primero liberamos al camarero. De esta manera, cuando la mesa
        // pase a Free y sea asignada a otro grupo, el camarero ya estará
        // disponible para atenderla.
        waiter.ClearAssignment();

        // Este cambio activa TableAssignmentSystem. Si hay grupos esperando,
        // uno de ellos podrá recibir inmediatamente la mesa.
        table.SetState(
            TableState.Free
        );
    }

    /// <summary>
    /// Comprueba que el componente dispone de las referencias necesarias.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (waiter == null)
        {
            Debug.LogError(
                "TableCleaningServiceFlow necesita una referencia " +
                "a Waiter.",
                this
            );
        }

        if (waiterMovementView == null)
        {
            Debug.LogError(
                "TableCleaningServiceFlow necesita una referencia " +
                "a WaiterMovementView.",
                this
            );
        }
    }
}