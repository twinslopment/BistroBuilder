using UnityEngine;

public sealed class CustomerExitFlow : MonoBehaviour
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
            customerMovementView = GetComponent<CustomerMovementView>();
    }

    private void OnEnable()
    {
        if (customerMovementView == null)
        {
            Debug.LogError(
                "CustomerExitFlow necesita una referencia " +
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
        if (customerGroup == null)
            return;

        // Ignoramos la llegada inicial a la mesa.
        if (customerGroup.CurrentState !=
            CustomerGroupState.Leaving)
        {
            return;
        }

        CompleteCustomerExit();
    }

    private void CompleteCustomerExit()
    {
        RestaurantTable previousTable =
            customerGroup.AssignedTable;

        if (previousTable == null)
        {
            Debug.LogError(
                $"El grupo {customerGroup.GroupId} no tiene una " +
                "mesa asignada al abandonar el restaurante.",
                this
            );

            return;
        }

        customerGroup.SetState(
            CustomerGroupState.Finished
        );

        customerGroup.ClearAssignedTable();

        previousTable.SetState(
            TableState.Dirty
        );

        Debug.Log(
            $"Grupo {customerGroup.GroupId} ha abandonado " +
            $"el restaurante. La mesa {previousTable.TableId} " +
            "ha quedado sucia.",
            this
        );
    }
}