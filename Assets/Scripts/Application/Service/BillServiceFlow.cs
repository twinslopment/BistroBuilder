using System.Collections;
using UnityEngine;

/// <summary>
/// Entrega la cuenta y completa el pago.
///
/// Desde 367E la cuenta está protegida por la autoridad de consumo individual:
/// no puede iniciarse mientras exista un CustomerId, pase o línea pendiente.
/// </summary>
[DisallowMultipleComponent]
public sealed class BillServiceFlow : MonoBehaviour
{
    [Header("Referencias")]

    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private WaiterMovementView waiterMovementView;

    [SerializeField]
    private OrderSystem orderSystem;

    [SerializeField]
    private BistroBuilderCustomerDiningService customerDiningService;

    [Header("Duraciones provisionales")]

    [SerializeField, Min(0.1f)]
    private float billDeliveryDuration = 1.5f;

    [SerializeField, Min(0.1f)]
    private float paymentDuration = 2.5f;

    private Coroutine activeRoutine;

    public Waiter Waiter => waiter;
    public WaiterMovementView MovementView => waiterMovementView;
    public OrderSystem OrderSystem => orderSystem;
    public BistroBuilderCustomerDiningService CustomerDiningService =>
        customerDiningService;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        if (waiterMovementView != null)
        {
            waiterMovementView.DestinationReached -=
                HandleDestinationReached;
            waiterMovementView.DestinationReached +=
                HandleDestinationReached;
        }
    }

    private void OnDisable()
    {
        if (waiterMovementView != null)
        {
            waiterMovementView.DestinationReached -=
                HandleDestinationReached;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    private void Start()
    {
        ResolveDependencies();

        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
            enabled = false;
        }
    }

    private void HandleDestinationReached(
        WaiterMovementView movementView
    )
    {
        if (waiter == null || activeRoutine != null)
        {
            return;
        }

        if (waiter.CurrentState != WaiterState.WalkingToBill)
        {
            return;
        }

        activeRoutine = StartCoroutine(DeliverBillAndPayRoutine());
    }

    private IEnumerator DeliverBillAndPayRoutine()
    {
        RestaurantTable table = waiter.AssignedTable;

        if (table == null)
        {
            Debug.LogError(
                "El camarero " + waiter.WaiterId +
                " no tiene mesa asignada.",
                waiter
            );
            activeRoutine = null;
            yield break;
        }

        CustomerGroup customerGroup = table.AssignedCustomerGroup;

        if (customerGroup == null)
        {
            Debug.LogError(
                "La mesa " + table.TableId + " no tiene grupo asignado.",
                table
            );
            activeRoutine = null;
            yield break;
        }

        if (orderSystem == null || customerDiningService == null)
        {
            Debug.LogError(
                "BillServiceFlow no tiene todas sus autoridades asignadas.",
                this
            );
            activeRoutine = null;
            yield break;
        }

        RestaurantOrder order = orderSystem.GetActiveOrderForTable(table);

        if (order == null)
        {
            Debug.LogError(
                "La mesa " + table.TableId +
                " no tiene una comanda activa.",
                table
            );
            activeRoutine = null;
            yield break;
        }

        if (!customerDiningService.TryValidateBillReady(
                order,
                out string billGuardError
            ))
        {
            Debug.LogError(
                "367E bloqueó una cuenta prematura para la comanda " +
                order.OrderId + ". " + billGuardError,
                this
            );
            activeRoutine = null;
            yield break;
        }

        waiter.SetState(WaiterState.DeliveringBill);
        table.SetState(TableState.Paying);
        customerGroup.SetState(CustomerGroupState.Paying);

        Debug.Log(
            "Camarero " + waiter.WaiterId +
            " entrega la cuenta a la mesa " + table.TableId + ".",
            this
        );

        yield return new WaitForSeconds(billDeliveryDuration);

        if (!customerDiningService.TryValidateBillReady(
                order,
                out billGuardError
            ))
        {
            Debug.LogError(
                "La cuenta dejó de ser válida durante su entrega. " +
                billGuardError,
                this
            );
            activeRoutine = null;
            yield break;
        }

        Debug.Log(
            "Grupo " + customerGroup.GroupId +
            " está realizando el pago.",
            this
        );

        yield return new WaitForSeconds(paymentDuration);

        if (!customerDiningService.TryValidateBillReady(
                order,
                out billGuardError
            ))
        {
            Debug.LogError(
                "367E bloqueó la finalización de un pago inconsistente. " +
                billGuardError,
                this
            );
            activeRoutine = null;
            yield break;
        }

        bool completed = orderSystem.CompleteOrder(order);

        if (!completed)
        {
            Debug.LogError(
                "No se pudo completar la comanda " + order.OrderId + ".",
                this
            );
            activeRoutine = null;
            yield break;
        }

        customerGroup.SetState(CustomerGroupState.Leaving);

        Debug.Log(
            "Grupo " + customerGroup.GroupId +
            " ha pagado la comanda " + order.OrderId +
            " y se prepara para abandonar la mesa.",
            this
        );

        waiter.ClearAssignment();
        activeRoutine = null;
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (waiter == null)
        {
            error = "BillServiceFlow necesita una referencia a Waiter.";
            return false;
        }

        if (waiterMovementView == null)
        {
            error =
                "BillServiceFlow necesita una referencia a " +
                "WaiterMovementView.";
            return false;
        }

        if (orderSystem == null)
        {
            error = "BillServiceFlow necesita una referencia a OrderSystem.";
            return false;
        }

        if (customerDiningService == null)
        {
            error =
                "BillServiceFlow necesita " +
                "BistroBuilderCustomerDiningService.";
            return false;
        }

        if (!customerDiningService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (float.IsNaN(billDeliveryDuration) ||
            float.IsInfinity(billDeliveryDuration) ||
            billDeliveryDuration <= 0f ||
            float.IsNaN(paymentDuration) ||
            float.IsInfinity(paymentDuration) ||
            paymentDuration <= 0f)
        {
            error = "Las duraciones de cuenta y pago deben ser positivas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ResolveDependencies()
    {
        // El instalador 367E asigna referencias explícitas. Los GetComponent
        // locales se conservan como recuperación segura para prefabs.
        if (waiter == null)
        {
            TryGetComponent(out waiter);
        }

        if (waiterMovementView == null)
        {
            TryGetComponent(out waiterMovementView);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        billDeliveryDuration = Mathf.Max(0.1f, billDeliveryDuration);
        paymentDuration = Mathf.Max(0.1f, paymentDuration);
    }
#endif
}
