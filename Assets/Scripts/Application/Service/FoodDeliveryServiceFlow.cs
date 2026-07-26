using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Ejecuta la recogida y entrega de un plato físico concreto.
///
/// La asignación pertenece a WaiterTaskCoordinator y la autoridad de estado
/// pertenece a BistroBuilderCanonicalOrderService a través de
/// BistroBuilderOrderLineExecutionService. Este flujo solo coordina movimiento,
/// tiempos provisionales y confirmación transaccional de la tarea.
/// </summary>
[DisallowMultipleComponent]
public sealed class FoodDeliveryServiceFlow : MonoBehaviour
{
    [Header("Referencias")]

    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private WaiterMovementView waiterMovementView;

    [SerializeField]
    private WaiterTaskCoordinator taskCoordinator;

    [SerializeField]
    private BistroBuilderOrderLineExecutionService lineExecutionService;

    [Header("Duraciones provisionales")]

    [SerializeField, Min(0.1f)]
    private float pickupDuration = 1f;

    [SerializeField, Min(0.1f)]
    private float servingDuration = 2f;

    private Coroutine activeRoutine;

    public Waiter Waiter => waiter;
    public WaiterMovementView MovementView => waiterMovementView;
    public WaiterTaskCoordinator TaskCoordinator => taskCoordinator;
    public BistroBuilderOrderLineExecutionService LineExecutionService =>
        lineExecutionService;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        if (waiterMovementView != null)
        {
            waiterMovementView.DestinationReached -= HandleDestinationReached;
            waiterMovementView.DestinationReached += HandleDestinationReached;
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

    private void OnDisable()
    {
        if (waiterMovementView != null)
        {
            waiterMovementView.DestinationReached -= HandleDestinationReached;
        }

        RestaurantOrder interruptedOrder = waiter != null
            ? waiter.AssignedOrder
            : null;
        string interruptedLineId = waiter != null
            ? waiter.AssignedOrderLineId
            : string.Empty;

        bool wasExecutingDelivery = waiter != null &&
            (waiter.CurrentState == WaiterState.WalkingToKitchen ||
             waiter.CurrentState == WaiterState.WaitingForDish ||
             waiter.CurrentState == WaiterState.WalkingToServeTable ||
             waiter.CurrentState == WaiterState.ServingFood);

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (Application.isPlaying && wasExecutingDelivery)
        {
            RecoverInterruptedLine(
                interruptedOrder,
                interruptedLineId,
                true
            );
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (waiter == null)
        {
            error = "FoodDeliveryServiceFlow necesita una referencia a Waiter.";
            return false;
        }

        if (waiterMovementView == null)
        {
            error =
                "FoodDeliveryServiceFlow necesita WaiterMovementView.";
            return false;
        }

        if (taskCoordinator == null)
        {
            error =
                "FoodDeliveryServiceFlow necesita WaiterTaskCoordinator.";
            return false;
        }

        if (lineExecutionService == null)
        {
            error =
                "FoodDeliveryServiceFlow necesita " +
                "BistroBuilderOrderLineExecutionService.";
            return false;
        }

        if (!lineExecutionService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (float.IsNaN(pickupDuration) ||
            float.IsInfinity(pickupDuration) ||
            pickupDuration <= 0f ||
            float.IsNaN(servingDuration) ||
            float.IsInfinity(servingDuration) ||
            servingDuration <= 0f)
        {
            error = "Las duraciones de reparto deben ser positivas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleDestinationReached(WaiterMovementView movementView)
    {
        if (waiter == null || activeRoutine != null)
            return;

        if (waiter.CurrentState == WaiterState.WalkingToKitchen)
        {
            activeRoutine = StartCoroutine(PickupLineRoutine());
            return;
        }

        if (waiter.CurrentState == WaiterState.WalkingToServeTable)
        {
            activeRoutine = StartCoroutine(ServeLineRoutine());
        }
    }

    private IEnumerator PickupLineRoutine()
    {
        RestaurantOrder order = waiter.AssignedOrder;
        string orderLineId = waiter.AssignedOrderLineId;

        if (!ValidateAssignedLine(order, orderLineId, out string error))
        {
            Debug.LogError(error, this);
            AbortDelivery(order, orderLineId, true);
            yield break;
        }

        waiter.SetState(WaiterState.WaitingForDish);

        Debug.Log(
            "Camarero " + waiter.WaiterId + " recoge la línea " +
            orderLineId + " de la comanda " + order.OrderId + ".",
            this
        );

        yield return new WaitForSeconds(pickupDuration);

        if (!IsAssignmentStillValid(order, orderLineId))
        {
            Debug.LogWarning(
                "La asignación de línea cambió durante la recogida.",
                this
            );
            AbortDelivery(order, orderLineId, false);
            yield break;
        }

        if (!lineExecutionService.TryMarkLineInTransit(
                order,
                orderLineId,
                waiter,
                out error
            ))
        {
            Debug.LogError(
                "No se pudo retirar la línea del pase. " + error,
                this
            );
            AbortDelivery(order, orderLineId, true);
            yield break;
        }

        waiter.SetState(WaiterState.WalkingToServeTable);
        activeRoutine = null;
    }

    private IEnumerator ServeLineRoutine()
    {
        RestaurantOrder order = waiter.AssignedOrder;
        string orderLineId = waiter.AssignedOrderLineId;

        if (!ValidateAssignedLine(order, orderLineId, out string error))
        {
            Debug.LogError(error, this);
            AbortDelivery(order, orderLineId, true);
            yield break;
        }

        RestaurantTable table = order.Table;
        CustomerGroup customerGroup = order.CustomerGroup;

        if (table == null || customerGroup == null)
        {
            Debug.LogError(
                "La comanda " + order.OrderId + " tiene datos incompletos.",
                this
            );
            AbortDelivery(order, orderLineId, true);
            yield break;
        }

        waiter.SetState(WaiterState.ServingFood);

        Debug.Log(
            "Camarero " + waiter.WaiterId + " sirve la línea " +
            orderLineId + " en la mesa " + table.TableId + ".",
            this
        );

        yield return new WaitForSeconds(servingDuration);

        if (!IsAssignmentStillValid(order, orderLineId))
        {
            Debug.LogWarning(
                "La asignación de línea cambió durante el servicio.",
                this
            );
            AbortDelivery(order, orderLineId, false);
            yield break;
        }

        bool markedServed = lineExecutionService.TryMarkLineServed(
            order,
            orderLineId,
            waiter,
            out bool allActiveLinesServed,
            out error
        );

        if (!markedServed)
        {
            // Served es irreversible. Si la autoridad aceptó la línea pero la
            // fachada coarse no pudo sincronizarse, se limpia la tarea sin
            // generar un segundo plato duplicado.
            bool alreadyServed = IsLineServedOrConsumed(order, orderLineId);

            Debug.LogError(
                "La línea " + orderLineId +
                " no pudo completar toda la sincronización. " + error,
                this
            );

            if (alreadyServed)
            {
                CompleteOrCancelTaskWithoutRetry(order, orderLineId);
                activeRoutine = null;
                waiter.ClearAssignment();
            }
            else
            {
                AbortDelivery(order, orderLineId, true);
            }

            yield break;
        }

        bool taskCompleted = taskCoordinator != null &&
            taskCoordinator.TryCompleteFoodDeliveryTask(order, orderLineId);

        if (!taskCompleted)
        {
            Debug.LogWarning(
                "No se encontró la tarea activa de la línea " +
                orderLineId + ".",
                this
            );

            // La línea ya está servida, por lo que ReportFailure solo limpia
            // la tarea inconsistente y nunca la recrea.
            taskCoordinator?.ReportFoodDeliveryFailure(
                waiter,
                order,
                orderLineId
            );
        }

        if (allActiveLinesServed)
        {
            table.SetState(TableState.Eating);
            customerGroup.SetState(CustomerGroupState.Eating);

            Debug.Log(
                "Todos los platos de la comanda " + order.OrderId +
                " han sido servidos al grupo " +
                customerGroup.GroupId + ".",
                this
            );
        }
        else
        {
            Debug.Log(
                "Línea " + orderLineId + " servida; el grupo " +
                customerGroup.GroupId + " continúa esperando otros platos.",
                this
            );
        }

        activeRoutine = null;
        waiter.ClearAssignment();
    }

    private bool ValidateAssignedLine(
        RestaurantOrder order,
        string orderLineId,
        out string error
    )
    {
        if (order == null)
        {
            error = "El camarero no tiene comanda asignada.";
            return false;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(orderLineId))
        {
            error = "El camarero no tiene un LineId canónico asignado.";
            return false;
        }

        if (!IsAssignmentStillValid(order, orderLineId))
        {
            error = "La asignación camarero-comanda-línea no es coherente.";
            return false;
        }

        if (!lineExecutionService.TryGetLineSnapshot(
                order,
                orderLineId,
                out _,
                out BistroBuilderCanonicalOrderLine line,
                out error
            ))
        {
            return false;
        }

        bool validOperationalState =
            line.State ==
                BistroBuilderCanonicalOrderLineState.AssignedForDelivery ||
            line.State == BistroBuilderCanonicalOrderLineState.InTransit;

        if (!validOperationalState)
        {
            error = "La línea está en " + line.State +
                    " y no puede ejecutar reparto.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool IsAssignmentStillValid(
        RestaurantOrder order,
        string orderLineId
    )
    {
        return waiter != null &&
               ReferenceEquals(waiter.AssignedOrder, order) &&
               ReferenceEquals(waiter.AssignedTable, order?.Table) &&
               string.Equals(
                   waiter.AssignedOrderLineId,
                   BistroBuilderOrderIdUtility.Normalize(orderLineId),
                   StringComparison.Ordinal
               );
    }

    private bool IsLineServedOrConsumed(
        RestaurantOrder order,
        string orderLineId
    )
    {
        return lineExecutionService != null &&
               lineExecutionService.TryGetLineSnapshot(
                   order,
                   orderLineId,
                   out _,
                   out BistroBuilderCanonicalOrderLine line,
                   out _
               ) &&
               (line.State == BistroBuilderCanonicalOrderLineState.Served ||
                line.State == BistroBuilderCanonicalOrderLineState.Consumed);
    }

    private void CompleteOrCancelTaskWithoutRetry(
        RestaurantOrder order,
        string orderLineId
    )
    {
        if (taskCoordinator == null)
            return;

        if (!taskCoordinator.TryCompleteFoodDeliveryTask(order, orderLineId))
        {
            taskCoordinator.ReportFoodDeliveryFailure(
                waiter,
                order,
                orderLineId
            );
        }
    }

    private void AbortDelivery(
        RestaurantOrder order,
        string orderLineId,
        bool clearWaiterAssignment
    )
    {
        activeRoutine = null;

        if (order != null &&
            BistroBuilderOrderIdUtility.IsValid(orderLineId) &&
            lineExecutionService != null)
        {
            string actorReference = waiter != null
                ? BistroBuilderServiceOrderIdentityUtility
                    .BuildWaiterReference(waiter.WaiterId)
                : "delivery_failure";

            lineExecutionService.TryReturnLineToPickup(
                order,
                orderLineId,
                actorReference,
                out _
            );
        }

        taskCoordinator?.ReportFoodDeliveryFailure(
            waiter,
            order,
            orderLineId
        );

        if (!clearWaiterAssignment || waiter == null)
            return;

        bool canClear = order == null ||
            (ReferenceEquals(waiter.AssignedOrder, order) &&
             string.Equals(
                 waiter.AssignedOrderLineId,
                 BistroBuilderOrderIdUtility.Normalize(orderLineId),
                 StringComparison.Ordinal
             ));

        if (canClear)
        {
            waiter.ClearAssignment();
        }
    }

    private void RecoverInterruptedLine(
        RestaurantOrder order,
        string orderLineId,
        bool clearAssignment
    )
    {
        AbortDelivery(order, orderLineId, clearAssignment);
    }

    private void ResolveDependencies()
    {
        if (taskCoordinator == null)
        {
            taskCoordinator = FindFirstObjectByType<WaiterTaskCoordinator>();
        }

        if (lineExecutionService == null && taskCoordinator != null)
        {
            lineExecutionService = taskCoordinator.LineExecutionService;
        }

        if (lineExecutionService == null)
        {
            lineExecutionService = FindFirstObjectByType<
                BistroBuilderOrderLineExecutionService
            >();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        pickupDuration = Mathf.Max(0.1f, pickupDuration);
        servingDuration = Mathf.Max(0.1f, servingDuration);
    }
#endif
}
