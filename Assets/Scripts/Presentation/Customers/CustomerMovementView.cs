using System;
using UnityEngine;

public sealed class CustomerMovementView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private CustomerGroup customerGroup;

    [Header("Movimiento")]
    [SerializeField, Min(0.1f)]
    private float movementSpeed = 2f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.05f;

    public event Action<CustomerMovementView> DestinationReached;

    public bool HasReachedDestination { get; private set; }

    private Transform currentDestination;
    private bool isMoving;

    private void Awake()
    {
        if (customerGroup == null)
            customerGroup = GetComponent<CustomerGroup>();
    }

    private void OnEnable()
    {
        if (customerGroup == null)
        {
            Debug.LogError(
                "CustomerMovementView necesita una referencia a CustomerGroup.",
                this
            );

            enabled = false;
            return;
        }

        customerGroup.StateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (customerGroup != null)
            customerGroup.StateChanged -= HandleStateChanged;
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

    private void HandleStateChanged(
        CustomerGroup group,
        CustomerGroupState newState
    )
    {
        if (newState != CustomerGroupState.WalkingToTable)
            return;

        RestaurantTable assignedTable = group.AssignedTable;

        if (assignedTable == null)
        {
            Debug.LogError(
                $"El grupo {group.GroupId} no tiene una mesa asignada.",
                this
            );

            return;
        }

        if (assignedTable.CustomerApproachPoint == null)
        {
            Debug.LogError(
                $"La mesa {assignedTable.TableId} no tiene CustomerApproachPoint.",
                assignedTable
            );

            return;
        }

        currentDestination = assignedTable.CustomerApproachPoint;
        HasReachedDestination = false;
        isMoving = true;
    }

    private void CompleteMovement()
    {
        transform.position = currentDestination.position;

        isMoving = false;
        HasReachedDestination = true;

        Debug.Log(
            $"Grupo {customerGroup.GroupId} ha llegado a la mesa.",
            this
        );

        DestinationReached?.Invoke(this);
    }
}