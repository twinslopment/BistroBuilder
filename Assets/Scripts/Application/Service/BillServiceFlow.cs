using System.Collections;
using UnityEngine;

public sealed class BillServiceFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private WaiterMovementView waiterMovementView;

    [SerializeField]
    private OrderSystem orderSystem;

    [Header("Duraciones provisionales")]
    [SerializeField, Min(0.1f)]
    private float billDeliveryDuration = 1.5f;

    [SerializeField, Min(0.1f)]
    private float paymentDuration = 2.5f;

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

        if (waiter.CurrentState != WaiterState.WalkingToBill)
            return;

        activeRoutine =
            StartCoroutine(DeliverBillAndPayRoutine());
    }

    private IEnumerator DeliverBillAndPayRoutine()
    {
        RestaurantTable table = waiter.AssignedTable;

        if (table == null)
        {
            Debug.LogError(
                $"El camarero {waiter.WaiterId} no tiene mesa asignada.",
                waiter
            );

            activeRoutine = null;
            yield break;
        }

        CustomerGroup customerGroup =
            table.AssignedCustomerGroup;

        if (customerGroup == null)
        {
            Debug.LogError(
                $"La mesa {table.TableId} no tiene grupo asignado.",
                table
            );

            activeRoutine = null;
            yield break;
        }

        if (orderSystem == null)
        {
            Debug.LogError(
                "BillServiceFlow no tiene OrderSystem asignado.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        RestaurantOrder order =
            orderSystem.GetActiveOrderForTable(table);

        if (order == null)
        {
            Debug.LogError(
                $"La mesa {table.TableId} no tiene una comanda activa.",
                table
            );

            activeRoutine = null;
            yield break;
        }

        if (order.CurrentState != OrderState.Served)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} no está en estado Served.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        waiter.SetState(WaiterState.DeliveringBill);
        table.SetState(TableState.Paying);
        customerGroup.SetState(CustomerGroupState.Paying);

        Debug.Log(
            $"Camarero {waiter.WaiterId} entrega la cuenta " +
            $"a la mesa {table.TableId}.",
            this
        );

        yield return new WaitForSeconds(
            billDeliveryDuration
        );

        Debug.Log(
            $"Grupo {customerGroup.GroupId} está realizando el pago.",
            this
        );

        yield return new WaitForSeconds(
            paymentDuration
        );

        bool completed =
            orderSystem.CompleteOrder(order);

        if (!completed)
        {
            Debug.LogError(
                $"No se pudo completar la comanda {order.OrderId}.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        customerGroup.SetState(
            CustomerGroupState.Leaving
        );

        Debug.Log(
            $"Grupo {customerGroup.GroupId} ha pagado la comanda " +
            $"{order.OrderId} y se prepara para abandonar la mesa.",
            this
        );

        waiter.ClearAssignment();
        activeRoutine = null;
    }

    private void ValidateConfiguration()
    {
        if (waiter == null)
        {
            Debug.LogError(
                "BillServiceFlow necesita una referencia a Waiter.",
                this
            );
        }

        if (waiterMovementView == null)
        {
            Debug.LogError(
                "BillServiceFlow necesita una referencia " +
                "a WaiterMovementView.",
                this
            );
        }

        if (orderSystem == null)
        {
            Debug.LogError(
                "BillServiceFlow necesita una referencia a OrderSystem.",
                this
            );
        }
    }
}