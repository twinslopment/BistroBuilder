using System.Collections;
using UnityEngine;

public sealed class TableStateTester : MonoBehaviour
{
    [SerializeField]
    private RestaurantTable restaurantTable;

    [SerializeField, Min(0.5f)]
    private float secondsBetweenStates = 2f;

    private readonly TableState[] testSequence =
    {
        TableState.WaitingForWaiter,
        TableState.TakingOrder,
        TableState.WaitingForFood,
        TableState.Eating,
        TableState.WaitingForBill,
        TableState.Dirty,
        TableState.Free
    };

    private IEnumerator Start()
    {
        if (restaurantTable == null)
        {
            Debug.LogError(
                "TableStateTester necesita una referencia a RestaurantTable.",
                this
            );

            yield break;
        }

        foreach (TableState state in testSequence)
        {
            yield return new WaitForSeconds(secondsBetweenStates);
            restaurantTable.SetState(state);
        }
    }
}