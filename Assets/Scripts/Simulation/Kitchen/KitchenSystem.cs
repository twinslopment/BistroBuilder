using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class KitchenSystem : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private OrderSystem orderSystem;

    [SerializeField]
    private Transform pickupPoint;

    [Header("Preparación provisional")]
    [SerializeField, Min(0.1f)]
    private float preparationDuration = 5f;

    [Header("Estado actual")]
    [SerializeField]
    private KitchenState currentState = KitchenState.Idle;

    private readonly Queue<RestaurantOrder> pendingOrders = new();

    private RestaurantOrder activeOrder;
    private Coroutine processingRoutine;

    public event Action<KitchenState> StateChanged;
    public event Action<RestaurantOrder> OrderReady;

    public KitchenState CurrentState => currentState;
    public RestaurantOrder ActiveOrder => activeOrder;
    public int PendingOrderCount => pendingOrders.Count;
    public Transform PickupPoint => pickupPoint;

    private void OnEnable()
    {
        if (orderSystem != null)
            orderSystem.OrderCreated += HandleOrderCreated;
    }

    private void Start()
    {
        if (orderSystem == null)
        {
            Debug.LogError(
                "KitchenSystem necesita una referencia a OrderSystem.",
                this
            );

            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (orderSystem != null)
            orderSystem.OrderCreated -= HandleOrderCreated;

        foreach (RestaurantOrder order in orderSystem?.ActiveOrders ??
                 Array.Empty<RestaurantOrder>())
        {
            order.StateChanged -= HandleOrderStateChanged;
        }

        if (processingRoutine != null)
        {
            StopCoroutine(processingRoutine);
            processingRoutine = null;
        }
    }

    private void HandleOrderCreated(RestaurantOrder order)
    {
        if (order == null)
            return;

        order.StateChanged += HandleOrderStateChanged;
    }

    private void HandleOrderStateChanged(
        RestaurantOrder order,
        OrderState newState
    )
    {
        if (newState != OrderState.SentToKitchen)
            return;

        pendingOrders.Enqueue(order);

        Debug.Log(
            $"Comanda {order.OrderId} añadida a la cola de cocina.",
            this
        );

        UpdateKitchenState();

        if (processingRoutine == null)
            processingRoutine = StartCoroutine(ProcessOrdersRoutine());
    }

    private IEnumerator ProcessOrdersRoutine()
    {
        while (pendingOrders.Count > 0)
        {
            activeOrder = pendingOrders.Dequeue();

            bool startedPreparing =
                activeOrder.TrySetState(OrderState.Preparing);

            if (!startedPreparing)
            {
                activeOrder = null;
                UpdateKitchenState();
                continue;
            }

            Debug.Log(
                $"Cocina empieza a preparar la comanda " +
                $"{activeOrder.OrderId}.",
                this
            );

            UpdateKitchenState();

            yield return new WaitForSeconds(preparationDuration);

            bool readyForPickup =
                activeOrder.TrySetState(OrderState.ReadyForPickup);

            if (readyForPickup)
            {
                Debug.Log(
                    $"Comanda {activeOrder.OrderId} lista para recoger.",
                    this
                );

                OrderReady?.Invoke(activeOrder);
            }

            activeOrder = null;
            UpdateKitchenState();
        }

        processingRoutine = null;
        UpdateKitchenState();
    }

    private void UpdateKitchenState()
    {
        int workload =
            pendingOrders.Count +
            (activeOrder != null ? 1 : 0);

        KitchenState newState = workload switch
        {
            0 => KitchenState.Idle,
            1 => KitchenState.Working,
            <= 3 => KitchenState.Busy,
            _ => KitchenState.Overloaded
        };

        if (currentState == newState)
            return;

        currentState = newState;

        Debug.Log(
            $"Estado de cocina cambiado a {currentState}.",
            this
        );

        StateChanged?.Invoke(currentState);
    }
}