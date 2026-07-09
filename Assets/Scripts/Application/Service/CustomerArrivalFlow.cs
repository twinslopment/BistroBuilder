using System.Collections;
using UnityEngine;

public sealed class CustomerArrivalFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private CustomerGroup customerGroup;

    [Header("Llegada provisional")]
    [SerializeField, Min(0f)]
    private float arrivalDelay = 2f;

    private Coroutine arrivalRoutine;

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
                "CustomerArrivalFlow necesita una referencia a CustomerGroup.",
                this
            );

            enabled = false;
            return;
        }

        arrivalRoutine = StartCoroutine(ArrivalRoutine());
    }

    private void OnDisable()
    {
        if (arrivalRoutine == null)
            return;

        StopCoroutine(arrivalRoutine);
        arrivalRoutine = null;
    }

    private IEnumerator ArrivalRoutine()
    {
        if (customerGroup.CurrentState != CustomerGroupState.Entering)
        {
            arrivalRoutine = null;
            yield break;
        }

        if (arrivalDelay > 0f)
            yield return new WaitForSeconds(arrivalDelay);

        customerGroup.SetState(
            CustomerGroupState.WaitingForTable
        );

        Debug.Log(
            $"Grupo {customerGroup.GroupId} ha llegado " +
            "y espera una mesa.",
            this
        );

        arrivalRoutine = null;
    }
}