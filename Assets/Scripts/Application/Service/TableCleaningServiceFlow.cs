using System.Collections;
using UnityEngine;

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

    private IEnumerator CleanTableRoutine()
    {
        RestaurantTable table = waiter.AssignedTable;

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

        waiter.SetState(WaiterState.CleaningTable);

        Debug.Log(
            $"Camarero {waiter.WaiterId} comienza a limpiar " +
            $"la mesa {table.TableId}.",
            this
        );

        yield return new WaitForSeconds(cleaningDuration);

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

        table.SetState(TableState.Free);

        Debug.Log(
            $"Camarero {waiter.WaiterId} ha terminado de limpiar " +
            $"la mesa {table.TableId}. La mesa vuelve a estar libre.",
            this
        );

        activeRoutine = null;
        waiter.ClearAssignment();
    }

    private void ValidateConfiguration()
    {
        if (waiter == null)
        {
            Debug.LogError(
                "TableCleaningServiceFlow necesita una referencia a Waiter.",
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