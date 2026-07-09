using System.Collections;
using UnityEngine;

public sealed class CustomerDiningFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private CustomerGroup customerGroup;

    [Header("Duraciones provisionales")]
    [SerializeField, Min(0.1f)]
    private float eatingDuration = 6f;

    private Coroutine activeRoutine;

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
                "CustomerDiningFlow necesita una referencia a CustomerGroup.",
                this
            );

            enabled = false;
            return;
        }

        customerGroup.StateChanged += HandleCustomerStateChanged;
    }

    private void OnDisable()
    {
        if (customerGroup != null)
        {
            customerGroup.StateChanged -=
                HandleCustomerStateChanged;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    private void HandleCustomerStateChanged(
        CustomerGroup changedGroup,
        CustomerGroupState newState
    )
    {
        if (newState != CustomerGroupState.Eating)
            return;

        if (activeRoutine != null)
            return;

        activeRoutine = StartCoroutine(EatingRoutine());
    }

    private IEnumerator EatingRoutine()
    {
        RestaurantTable table = customerGroup.AssignedTable;

        if (table == null)
        {
            Debug.LogError(
                $"El grupo {customerGroup.GroupId} " +
                "no tiene mesa mientras está comiendo.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        Debug.Log(
            $"Grupo {customerGroup.GroupId} comienza a comer.",
            this
        );

        yield return new WaitForSeconds(eatingDuration);

        if (customerGroup.CurrentState !=
            CustomerGroupState.Eating)
        {
            activeRoutine = null;
            yield break;
        }

        if (customerGroup.AssignedTable != table)
        {
            Debug.LogWarning(
                "La mesa del grupo cambió mientras estaba comiendo.",
                this
            );

            activeRoutine = null;
            yield break;
        }

        table.SetState(TableState.WaitingForBill);
        customerGroup.SetState(
            CustomerGroupState.WaitingForBill
        );

        Debug.Log(
            $"Grupo {customerGroup.GroupId} solicita la cuenta " +
            $"en la mesa {table.TableId}.",
            this
        );

        activeRoutine = null;
    }
}