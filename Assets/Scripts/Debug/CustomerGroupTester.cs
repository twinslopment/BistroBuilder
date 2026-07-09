using System.Collections;
using UnityEngine;

public sealed class CustomerGroupTester : MonoBehaviour
{
    [SerializeField]
    private CustomerGroup customerGroup;

    [SerializeField]
    private RestaurantTable restaurantTable;

    [SerializeField]
    private CustomerMovementView customerMovementView;

    [SerializeField, Min(0.5f)]
    private float delayBeforeAssignment = 2f;

    private bool destinationReached;

    private void OnEnable()
    {
        if (customerMovementView != null)
        {
            customerMovementView.DestinationReached +=
                HandleDestinationReached;
        }
    }

    private void OnDisable()
    {
        if (customerMovementView != null)
        {
            customerMovementView.DestinationReached -=
                HandleDestinationReached;
        }
    }

    private IEnumerator Start()
    {
        if (customerGroup == null)
        {
            Debug.LogError(
                "CustomerGroupTester necesita una referencia a CustomerGroup.",
                this
            );

            yield break;
        }

        if (restaurantTable == null)
        {
            Debug.LogError(
                "CustomerGroupTester necesita una referencia a RestaurantTable.",
                this
            );

            yield break;
        }

        if (customerMovementView == null)
        {
            Debug.LogError(
                "CustomerGroupTester necesita una referencia a CustomerMovementView.",
                this
            );

            yield break;
        }

        customerGroup.SetState(CustomerGroupState.WaitingForTable);

        yield return new WaitForSeconds(delayBeforeAssignment);

        bool assigned = customerGroup.AssignTable(restaurantTable);

        if (!assigned)
        {
            Debug.LogWarning(
                $"El grupo {customerGroup.GroupId} no pudo usar la mesa.",
                this
            );

            yield break;
        }

        restaurantTable.SetState(TableState.WaitingForWaiter);

        destinationReached = false;
        customerGroup.SetState(CustomerGroupState.WalkingToTable);

        yield return new WaitUntil(() => destinationReached);

        customerGroup.SetState(CustomerGroupState.Seated);
        customerGroup.SetState(CustomerGroupState.WaitingForWaiter);
    }

    private void HandleDestinationReached(
        CustomerMovementView movementView
    )
    {
        destinationReached = true;
    }
}