using System.Collections;
using UnityEngine;

public sealed class WaiterTableServiceFlow : MonoBehaviour
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

        RestaurantOrder order =
            orderSystem.CreateOrder(table, waiter);

        if (order == null)
        {
            Debug.LogError(
                $"No se pudo crear la comanda de la mesa {table.TableId}.",
                this
            );

            RecoverFailedOrderTaking(
                table,
                customerGroup,
                waiter
            );

            activeServiceRoutine = null;
            yield break;
        }

        bool sentToKitchen =
            order.TrySetState(OrderState.SentToKitchen);

        if (!sentToKitchen)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} no pudo enviarse a cocina. " +
                order.LastTransitionError,
                this
            );

            if (orderSystem.CancelOrder(order))
            {
                RecoverFailedOrderTaking(
                    table,
                    customerGroup,
                    waiter
                );
            }
            else
            {
                Debug.LogError(
                    "La comanda fallida tampoco pudo cancelarse. " +
                    "Se mantiene el estado actual para evitar ocultar " +
                    "una divergencia.",
                    this
                );
            }

            activeServiceRoutine = null;
            yield break;
        }

        table.SetState(TableState.WaitingForFood);
        customerGroup.SetState(CustomerGroupState.WaitingForFood);

        Debug.Log(
            $"Comanda {order.OrderId} enviada a cocina para la mesa " +
            $"{table.TableId}.",
            this
        );

        waiter.ClearAssignment();

        activeServiceRoutine = null;
    }

    /// <summary>
    /// Devuelve mesa, grupo y camarero a un estado operativo coherente cuando
    /// la creación o el envío de una comanda fallan.
    ///
    /// Antes de 367C el flujo podía quedar bloqueado en TakingOrder.
    /// </summary>
    private static void RecoverFailedOrderTaking(
        RestaurantTable table,
        CustomerGroup customerGroup,
        Waiter waiter
    )
    {
        if (table != null &&
            table.AssignedCustomerGroup == customerGroup)
        {
            table.SetState(TableState.WaitingForWaiter);
        }

        if (customerGroup != null &&
            customerGroup.AssignedTable == table)
        {
            customerGroup.SetState(
                CustomerGroupState.WaitingForWaiter
            );
        }

        if (waiter != null &&
            waiter.AssignedTable == table)
        {
            waiter.ClearAssignment();
        }
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

        if (orderSystem == null)
        {
            Debug.LogError(
                "WaiterTableServiceFlow necesita una referencia a OrderSystem.",
                this
            );
        }
        else if (!orderSystem.ValidateConfiguration(
                     out string orderSystemError
                 ))
        {
            Debug.LogError(
                "OrderSystem no está preparado para tomar pedidos. " +
                orderSystemError,
                this
            );
        }
    }
}