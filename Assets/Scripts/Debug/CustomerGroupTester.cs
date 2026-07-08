using System.Collections;
using UnityEngine;

public sealed class CustomerGroupTester : MonoBehaviour
{
    [SerializeField]
    private CustomerGroup customerGroup;

    [SerializeField]
    private RestaurantTable restaurantTable;

    [SerializeField, Min(0.5f)]
    private float delayBeforeAssignment = 2f;

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
        customerGroup.SetState(CustomerGroupState.WalkingToTable);

        yield return new WaitForSeconds(2f);

        customerGroup.SetState(CustomerGroupState.Seated);
        customerGroup.SetState(CustomerGroupState.WaitingForWaiter);
    }
}