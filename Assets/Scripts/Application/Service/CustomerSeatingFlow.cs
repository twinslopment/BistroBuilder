using UnityEngine;

public sealed class CustomerSeatingFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private CustomerGroup customerGroup;

    [SerializeField]
    private CustomerMovementView customerMovementView;

    private void Awake()
    {
        if (customerGroup == null)
            customerGroup = GetComponent<CustomerGroup>();

        if (customerMovementView == null)
        {
            customerMovementView =
                GetComponent<CustomerMovementView>();
        }
    }

    private void OnEnable()
    {
        if (customerGroup == null)
        {
            Debug.LogError(
                "CustomerSeatingFlow necesita una referencia " +
                "a CustomerGroup.",
                this
            );

            enabled = false;
            return;
        }

        if (customerMovementView == null)
        {
            Debug.LogError(
                "CustomerSeatingFlow necesita una referencia " +
                "a CustomerMovementView.",
                this
            );

            enabled = false;
            return;
        }

        customerMovementView.DestinationReached +=
            HandleDestinationReached;
    }

    private void OnDisable()
    {
        if (customerMovementView != null)
        {
            customerMovementView.DestinationReached -=
                HandleDestinationReached;
        }
    }

    private void HandleDestinationReached(
        CustomerMovementView movementView
    )
    {
        if (customerGroup.CurrentState !=
            CustomerGroupState.WalkingToTable)
        {
            return;
        }

        CompleteSeating();
    }

    private void CompleteSeating()
    {
        RestaurantTable assignedTable =
            customerGroup.AssignedTable;

        if (assignedTable == null)
        {
            Debug.LogError(
                $"El grupo {customerGroup.GroupId} llegó a la mesa, " +
                "pero no tiene ninguna asignada.",
                this
            );

            return;
        }

        customerGroup.SetState(
            CustomerGroupState.Seated
        );

        customerGroup.SetState(
            CustomerGroupState.WaitingForWaiter
        );

        Debug.Log(
            $"Grupo {customerGroup.GroupId} se ha sentado " +
            $"en la mesa {assignedTable.TableId} y espera atención.",
            this
        );
    }
}