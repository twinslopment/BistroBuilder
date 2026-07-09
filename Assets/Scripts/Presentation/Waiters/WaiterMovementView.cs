using System;
using UnityEngine;

public sealed class WaiterMovementView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private KitchenSystem kitchenSystem;

    [Header("Movimiento")]
    [SerializeField, Min(0.1f)]
    private float movementSpeed = 2.5f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.05f;

    public event Action<WaiterMovementView> DestinationReached;

    public bool HasReachedDestination { get; private set; }

    private Transform currentDestination;
    private bool isMoving;

    private void Awake()
    {
        if (waiter == null)
            waiter = GetComponent<Waiter>();
    }

    private void OnEnable()
    {
        if (waiter == null)
        {
            Debug.LogError(
                "WaiterMovementView necesita una referencia a Waiter.",
                this
            );

            enabled = false;
            return;
        }

        waiter.StateChanged += HandleWaiterStateChanged;
    }

    private void OnDisable()
    {
        if (waiter != null)
            waiter.StateChanged -= HandleWaiterStateChanged;
    }

    private void Update()
    {
        if (!isMoving || currentDestination == null)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            currentDestination.position,
            movementSpeed * Time.deltaTime
        );

        float remainingDistance = Vector3.Distance(
            transform.position,
            currentDestination.position
        );

        if (remainingDistance > arrivalDistance)
            return;

        CompleteMovement();
    }

    private void HandleWaiterStateChanged(
        Waiter changedWaiter,
        WaiterState newState
    )
    {
        Transform destination = newState switch
        {
            WaiterState.WalkingToTable =>
                GetTableServicePoint(changedWaiter),

            WaiterState.WalkingToKitchen =>
                GetKitchenPickupPoint(),

            WaiterState.WalkingToServeTable =>
                GetTableServicePoint(changedWaiter),

            WaiterState.WalkingToBill =>
                GetTableServicePoint(changedWaiter),

            WaiterState.WalkingToCleanTable =>
                GetTableServicePoint(changedWaiter),

            _ => null
        };

        if (destination == null)
            return;

        BeginMovement(destination);
    }

    private Transform GetTableServicePoint(Waiter changedWaiter)
    {
        RestaurantTable assignedTable =
            changedWaiter.AssignedTable;

        if (assignedTable == null)
        {
            Debug.LogError(
                $"El camarero {changedWaiter.WaiterId} " +
                "no tiene mesa asignada.",
                this
            );

            return null;
        }

        if (assignedTable.WaiterServicePoint == null)
        {
            Debug.LogError(
                $"La mesa {assignedTable.TableId} " +
                "no tiene WaiterServicePoint.",
                assignedTable
            );

            return null;
        }

        return assignedTable.WaiterServicePoint;
    }

    private Transform GetKitchenPickupPoint()
    {
        if (kitchenSystem == null)
        {
            Debug.LogError(
                "WaiterMovementView necesita una referencia " +
                "a KitchenSystem para ir a cocina.",
                this
            );

            return null;
        }

        if (kitchenSystem.PickupPoint == null)
        {
            Debug.LogError(
                "KitchenSystem no tiene PickupPoint asignado.",
                kitchenSystem
            );

            return null;
        }

        return kitchenSystem.PickupPoint;
    }

    private void BeginMovement(Transform destination)
    {
        currentDestination = destination;
        HasReachedDestination = false;
        isMoving = true;
    }

    private void CompleteMovement()
    {
        transform.position = currentDestination.position;

        isMoving = false;
        HasReachedDestination = true;

        Debug.Log(
            $"Camarero {waiter.WaiterId} ha llegado a su destino.",
            this
        );

        DestinationReached?.Invoke(this);
    }
}