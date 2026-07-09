using System.Collections.Generic;
using UnityEngine;

public sealed class FoodDeliveryAssignmentSystem : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private KitchenSystem kitchenSystem;

    [SerializeField]
    private Waiter[] waiters;

    private readonly List<RestaurantOrder> waitingOrders = new();

    private void OnEnable()
    {
        if (kitchenSystem != null)
            kitchenSystem.OrderReady += HandleOrderReady;

        if (waiters == null)
            return;

        foreach (Waiter waiter in waiters)
        {
            if (waiter != null)
                waiter.StateChanged += HandleWaiterStateChanged;
        }
    }

    private void Start()
    {
        ValidateConfiguration();
    }

    private void OnDisable()
    {
        if (kitchenSystem != null)
            kitchenSystem.OrderReady -= HandleOrderReady;

        if (waiters == null)
            return;

        foreach (Waiter waiter in waiters)
        {
            if (waiter != null)
                waiter.StateChanged -= HandleWaiterStateChanged;
        }
    }

    private void HandleOrderReady(RestaurantOrder order)
    {
        if (order == null)
            return;

        if (!waitingOrders.Contains(order))
            waitingOrders.Add(order);

        Debug.Log(
            $"Comanda {order.OrderId} esperando camarero para reparto.",
            this
        );

        TryDispatchOrders();
    }

    private void HandleWaiterStateChanged(
        Waiter waiter,
        WaiterState newState
    )
    {
        if (newState == WaiterState.Idle)
            TryDispatchOrders();
    }

    private void TryDispatchOrders()
    {
        int orderIndex = 0;

        while (orderIndex < waitingOrders.Count)
        {
            RestaurantOrder order = waitingOrders[orderIndex];

            if (order == null ||
                order.CurrentState != OrderState.ReadyForPickup)
            {
                waitingOrders.RemoveAt(orderIndex);
                continue;
            }

            Waiter closestWaiter = FindClosestAvailableWaiter();

            if (closestWaiter == null)
            {
                Debug.Log(
                    "No hay camareros libres para recoger platos.",
                    this
                );

                return;
            }

            bool assigned =
                closestWaiter.AssignOrderForPickup(order);

            if (!assigned)
            {
                orderIndex++;
                continue;
            }

            waitingOrders.RemoveAt(orderIndex);

            Debug.Log(
                $"Comanda {order.OrderId} asignada al camarero " +
                $"{closestWaiter.WaiterId} para recogida.",
                this
            );
        }
    }

    private Waiter FindClosestAvailableWaiter()
    {
        if (waiters == null || waiters.Length == 0)
            return null;

        Vector3 pickupPosition =
            kitchenSystem != null &&
            kitchenSystem.PickupPoint != null
                ? kitchenSystem.PickupPoint.position
                : transform.position;

        Waiter closestWaiter = null;
        float shortestDistanceSquared = float.MaxValue;

        foreach (Waiter waiter in waiters)
        {
            if (waiter == null || !waiter.IsAvailable)
                continue;

            float distanceSquared =
                (waiter.transform.position - pickupPosition)
                .sqrMagnitude;

            if (distanceSquared >= shortestDistanceSquared)
                continue;

            shortestDistanceSquared = distanceSquared;
            closestWaiter = waiter;
        }

        return closestWaiter;
    }

    private void ValidateConfiguration()
    {
        if (kitchenSystem == null)
        {
            Debug.LogError(
                "FoodDeliveryAssignmentSystem necesita KitchenSystem.",
                this
            );
        }

        if (waiters == null || waiters.Length == 0)
        {
            Debug.LogError(
                "FoodDeliveryAssignmentSystem no tiene camareros.",
                this
            );
        }
    }
}