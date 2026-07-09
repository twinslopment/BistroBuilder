using System.Collections;
using UnityEngine;

public sealed class WaiterTableServiceFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private WaiterMovementView waiterMovementView;

    [Header("Duraciones provisionales")]
    [SerializeField, Min(0.1f)]
    private float takingOrderDuration = 3f;

    private Coroutine activeServiceRoutine;

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

        if (activeServiceRoutine != null)
        {
            StopCoroutine(activeServiceRoutine);
            activeServiceRoutine = null;
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
        if (waiter == null)
            return;

        if (waiter.CurrentState != WaiterState.WalkingToTable)
            return;

        if (activeServiceRoutine != null)
            return;

        activeServiceRoutine =
            StartCoroutine(WaitForCustomersAndTakeOrderRoutine());
    }

    private IEnumerator WaitForCustomersAndTakeOrderRoutine()
    {
        RestaurantTable table = waiter.AssignedTable;

        if (table == null)
        {
            Debug.LogError(
                $"El camarero {waiter.WaiterId} no tiene mesa asignada.",
                waiter
            );

            activeServiceRoutine = null;
            yield break;
        }

        CustomerGroup customerGroup = table.AssignedCustomerGroup;

        if (customerGroup == null)
        {
            Debug.LogError(
                $"La mesa {table.TableId} no tiene grupo asignado.",
                table
            );

            activeServiceRoutine = null;
            yield break;
        }

        Debug.Log(
            $"Camarero {waiter.WaiterId} espera a que el grupo " +
            $"{customerGroup.GroupId} esté preparado.",
            this
        );

        yield return new WaitUntil(() =>
            customerGroup != null &&
            customerGroup.CurrentState ==
                CustomerGroupState.WaitingForWaiter
        );

        if (waiter.AssignedTable != table ||
            table.AssignedCustomerGroup != customerGroup)
        {
            Debug.LogWarning(
                "La asignación cambió mientras el camarero esperaba.",
                this
            );

            activeServiceRoutine = null;
            yield break;
        }

        waiter.SetState(WaiterState.TakingOrder);
        table.SetState(TableState.TakingOrder);
        customerGroup.SetState(CustomerGroupState.Ordering);

        yield return new WaitForSeconds(takingOrderDuration);

        table.SetState(TableState.WaitingForFood);
        customerGroup.SetState(CustomerGroupState.WaitingForFood);

        Debug.Log(
            $"Camarero {waiter.WaiterId} ha tomado el pedido " +
            $"del grupo {customerGroup.GroupId} en la mesa {table.TableId}.",
            this
        );

        activeServiceRoutine = null;
    }

    private void ValidateConfiguration()
    {
        if (waiter == null)
        {
            Debug.LogError(
                "WaiterTableServiceFlow necesita una referencia a Waiter.",
                this
            );
        }

        if (waiterMovementView == null)
        {
            Debug.LogError(
                "WaiterTableServiceFlow necesita una referencia " +
                "a WaiterMovementView.",
                this
            );
        }
    }
}