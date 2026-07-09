using System.Collections;
using UnityEngine;

public sealed class FoodDeliveryServiceFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private WaiterMovementView waiterMovementView;

    [Header("Duraciones provisionales")]
    [SerializeField, Min(0.1f)]
    private float pickupDuration = 1f;

    [SerializeField, Min(0.1f)]
    private float servingDuration = 2f;

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

        if (waiter.CurrentState == WaiterState.WalkingToKitchen)
        {
            activeRoutine = StartCoroutine(PickupFoodRoutine());
            return;
        }

        if (waiter.CurrentState == WaiterState.WalkingToServeTable)
        {
            activeRoutine = StartCoroutine(ServeFoodRoutine());
        }
    }

    private IEnumerator PickupFoodRoutine()
    {
        RestaurantOrder order = waiter.AssignedOrder;

        if (order == null)
        {
            Debug.LogError(
                $"El camarero {waiter.WaiterId} no tiene comanda asignada.",
                waiter
            );

            activeRoutine = null;
            yield break;
        }

        if (order.CurrentState != OrderState.ReadyForPickup)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} no está lista para recoger.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        waiter.SetState(WaiterState.WaitingForDish);

        Debug.Log(
            $"Camarero {waiter.WaiterId} recoge la comanda " +
            $"{order.OrderId} en cocina.",
            this
        );

        yield return new WaitForSeconds(pickupDuration);

        if (waiter.AssignedOrder != order ||
            waiter.AssignedTable != order.Table)
        {
            Debug.LogWarning(
                "La asignación del camarero cambió durante la recogida.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        waiter.SetState(WaiterState.WalkingToServeTable);

        activeRoutine = null;
    }

    private IEnumerator ServeFoodRoutine()
    {
        RestaurantOrder order = waiter.AssignedOrder;

        if (order == null)
        {
            Debug.LogError(
                $"El camarero {waiter.WaiterId} no tiene comanda asignada.",
                waiter
            );

            activeRoutine = null;
            yield break;
        }

        RestaurantTable table = order.Table;
        CustomerGroup customerGroup = order.CustomerGroup;

        if (table == null || customerGroup == null)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} tiene datos incompletos.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        waiter.SetState(WaiterState.ServingFood);

        Debug.Log(
            $"Camarero {waiter.WaiterId} sirve la comanda " +
            $"{order.OrderId} en la mesa {table.TableId}.",
            this
        );

        yield return new WaitForSeconds(servingDuration);

        bool served = order.TrySetState(OrderState.Served);

        if (!served)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} no pudo pasar a Served.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        table.SetState(TableState.Eating);
        customerGroup.SetState(CustomerGroupState.Eating);

        Debug.Log(
            $"Comanda {order.OrderId} servida al grupo " +
            $"{customerGroup.GroupId}.",
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
                "FoodDeliveryServiceFlow necesita una referencia a Waiter.",
                this
            );
        }

        if (waiterMovementView == null)
        {
            Debug.LogError(
                "FoodDeliveryServiceFlow necesita una referencia " +
                "a WaiterMovementView.",
                this
            );
        }
    }
}