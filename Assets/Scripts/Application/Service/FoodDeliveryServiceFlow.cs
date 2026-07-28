using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Ejecuta la recogida y entrega de platos físicos.
///
/// Desde 367G1 puede transportar una ronda con varias líneas y varias mesas.
/// La autoridad de estados sigue perteneciendo a las comandas canónicas; esta
/// clase solo coordina movimiento, tiempos y confirmación transaccional.
/// </summary>
[DisallowMultipleComponent]
public sealed class FoodDeliveryServiceFlow : MonoBehaviour
{
    public const string RuntimeRevision = "367H";

    [Header("Referencias")]

    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private WaiterMovementView waiterMovementView;

    [SerializeField]
    private WaiterTaskCoordinator taskCoordinator;

    [SerializeField]
    private BistroBuilderOrderLineExecutionService lineExecutionService;

    [SerializeField]
    private BistroBuilderCustomerDiningService customerDiningService;

    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;

    [Header("Duraciones provisionales")]

    [SerializeField, Min(0.1f)]
    private float pickupDuration = 1f;

    [Tooltip(
        "Tiempo adicional de recogida por cada plato posterior al primero " +
        "dentro de una ronda."
    )]
    [SerializeField, Min(0f)]
    private float additionalPickupDurationPerLine = 0.2f;

    [SerializeField, Min(0.1f)]
    private float servingDuration = 2f;

    private Coroutine activeRoutine;

    public Waiter Waiter => waiter;
    public WaiterMovementView MovementView => waiterMovementView;
    public WaiterTaskCoordinator TaskCoordinator => taskCoordinator;
    public BistroBuilderOrderLineExecutionService LineExecutionService =>
        lineExecutionService;
    public BistroBuilderCustomerDiningService CustomerDiningService =>
        customerDiningService;
    public float AdditionalPickupDurationPerLine =>
        additionalPickupDurationPerLine;

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

        BistroBuilderDeliveryRun interruptedRun = waiter != null
            ? waiter.AssignedDeliveryRun
            : null;
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

        if (!Application.isPlaying || !wasExecutingDelivery)
            return;

        if (interruptedRun != null)
        {
            RecoverInterruptedRun(interruptedRun, true);
            return;
        }

        RecoverInterruptedLine(
            interruptedOrder,
            interruptedLineId,
            true
        );
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
            error = "FoodDeliveryServiceFlow necesita WaiterMovementView.";
            return false;
        }

        if (taskCoordinator == null)
        {
            error = "FoodDeliveryServiceFlow necesita WaiterTaskCoordinator.";
            return false;
        }

        if (lineExecutionService == null)
        {
            error = "FoodDeliveryServiceFlow necesita " +
                    "BistroBuilderOrderLineExecutionService.";
            return false;
        }

        if (!lineExecutionService.ValidateConfiguration(out error))
            return false;

        if (customerDiningService == null)
        {
            error = "FoodDeliveryServiceFlow necesita " +
                    "BistroBuilderCustomerDiningService.";
            return false;
        }

        if (!customerDiningService.ValidateConfiguration(out error))
            return false;

        if (barServiceSystem == null)
        {
            error = "FoodDeliveryServiceFlow necesita " +
                    "BistroBuilderBarServiceSystem para destinos de barra.";
            return false;
        }

        if (float.IsNaN(pickupDuration) ||
            float.IsInfinity(pickupDuration) ||
            pickupDuration <= 0f ||
            float.IsNaN(additionalPickupDurationPerLine) ||
            float.IsInfinity(additionalPickupDurationPerLine) ||
            additionalPickupDurationPerLine < 0f ||
            float.IsNaN(servingDuration) ||
            float.IsInfinity(servingDuration) ||
            servingDuration <= 0f)
        {
            error = "Las duraciones de reparto son inválidas.";
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
            activeRoutine = waiter.AssignedDeliveryRun != null
                ? StartCoroutine(PickupDeliveryRunRoutine())
                : StartCoroutine(PickupLineRoutine());
            return;
        }

        if (waiter.CurrentState == WaiterState.WalkingToServeTable)
        {
            activeRoutine = waiter.AssignedDeliveryRun != null
                ? StartCoroutine(ServeDeliveryRunStopRoutine())
                : StartCoroutine(ServeLineRoutine());
        }
    }

    /// <summary>
    /// Recoge todas las líneas de una ronda en una sola operación de cocina.
    /// </summary>
    private IEnumerator PickupDeliveryRunRoutine()
    {
        BistroBuilderDeliveryRun deliveryRun = waiter.AssignedDeliveryRun;

        if (!ValidateAssignedRun(
                deliveryRun,
                BistroBuilderDeliveryRunState.Assigned,
                out string error
            ))
        {
            Debug.LogError(error, this);
            AbortDeliveryRun(deliveryRun, true);
            yield break;
        }

        if (!deliveryRun.TryBeginPickup())
        {
            Debug.LogError(
                "La ronda " + deliveryRun.RunId +
                " no pudo comenzar la recogida.",
                this
            );
            AbortDeliveryRun(deliveryRun, true);
            yield break;
        }

        waiter.SetState(WaiterState.WaitingForDish);

        Debug.Log(
            "Camarero " + waiter.WaiterId + " recoge la ronda " +
            deliveryRun.RunId + " con " + deliveryRun.Items.Count +
            " plato(s).",
            this
        );

        float totalPickupDuration = pickupDuration +
            Mathf.Max(0, deliveryRun.Items.Count - 1) *
            additionalPickupDurationPerLine;

        yield return new WaitForSeconds(totalPickupDuration);

        if (!IsDeliveryRunAssignmentStillValid(deliveryRun))
        {
            Debug.LogWarning(
                "La ronda cambió durante la recogida.",
                this
            );
            AbortDeliveryRun(deliveryRun, false);
            yield break;
        }

        for (int index = 0; index < deliveryRun.Items.Count; index++)
        {
            BistroBuilderDeliveryRunItem item = deliveryRun.Items[index];

            if (!lineExecutionService.TryMarkLineInTransit(
                    item.Order,
                    item.OrderLineId,
                    waiter,
                    out error
                ) ||
                !deliveryRun.TryMarkLineInTransit(
                    item.Order,
                    item.OrderLineId
                ))
            {
                Debug.LogError(
                    "No se pudo retirar toda la ronda del pase. " + error,
                    this
                );
                AbortDeliveryRun(deliveryRun, true);
                yield break;
            }
        }

        if (!waiter.TryBeginDeliveryRunStops())
        {
            Debug.LogError(
                "La ronda no pudo preparar su primera parada.",
                this
            );
            AbortDeliveryRun(deliveryRun, true);
            yield break;
        }

        activeRoutine = null;
        waiter.SetState(WaiterState.WalkingToServeTable);
    }

    /// <summary>
    /// Sirve secuencialmente todas las líneas de la mesa actual y continúa con
    /// la siguiente parada sin regresar a cocina.
    /// </summary>
    private IEnumerator ServeDeliveryRunStopRoutine()
    {
        BistroBuilderDeliveryRun deliveryRun = waiter.AssignedDeliveryRun;

        if (!ValidateAssignedRun(
                deliveryRun,
                BistroBuilderDeliveryRunState.InTransit,
                out string error
            ))
        {
            Debug.LogError(error, this);
            AbortDeliveryRun(deliveryRun, true);
            yield break;
        }

        BistroBuilderDeliveryStop stop = deliveryRun.CurrentStop;

        if (stop == null ||
            (stop.Table == null) == (stop.BarSpot == null) ||
            stop.WaiterServicePoint == null)
        {
            Debug.LogError("La ronda no tiene una parada válida.", this);
            AbortDeliveryRun(deliveryRun, true);
            yield break;
        }

        waiter.SetState(WaiterState.ServingFood);

        for (int index = 0; index < stop.Items.Count; index++)
        {
            BistroBuilderDeliveryRunItem item = stop.Items[index];

            if (item.IsFinished)
                continue;

            if (!waiter.TrySelectDeliveryLine(
                    item.Order,
                    item.OrderLineId
                ) ||
                !ValidateRunLineInTransit(item, out error))
            {
                Debug.LogError(error, this);
                AbortDeliveryRun(deliveryRun, true);
                yield break;
            }

            Debug.Log(
                "Camarero " + waiter.WaiterId + " sirve la línea " +
                item.OrderLineId + " de la ronda " + deliveryRun.RunId +
                " en " + DescribeDestination(stop.Table, stop.BarSpot) + ".",
                this
            );

            yield return new WaitForSeconds(servingDuration);

            if (!IsDeliveryRunAssignmentStillValid(deliveryRun) ||
                !ReferenceEquals(deliveryRun.CurrentStop, stop))
            {
                Debug.LogWarning(
                    "La ronda cambió durante el servicio de una parada.",
                    this
                );
                AbortDeliveryRun(deliveryRun, false);
                yield break;
            }

            bool markedServed = lineExecutionService.TryMarkLineServed(
                item.Order,
                item.OrderLineId,
                waiter,
                out _,
                out error
            );

            if (!markedServed)
            {
                bool alreadyServed = IsLineServedOrConsumed(
                    item.Order,
                    item.OrderLineId
                );

                Debug.LogError(
                    "La línea " + item.OrderLineId +
                    " no pudo completar toda la sincronización. " + error,
                    this
                );

                if (!alreadyServed)
                {
                    AbortDeliveryRun(deliveryRun, true);
                    yield break;
                }
            }

            // Si Served ya era irreversible, el modelo de transporte también
            // debe cerrarla para que nunca se reprograme como plato pendiente.
            if (!deliveryRun.TryMarkLineServed(
                    item.Order,
                    item.OrderLineId
                ))
            {
                Debug.LogError(
                    "La ronda no pudo registrar como servida la línea " +
                    item.OrderLineId + ".",
                    this
                );
                AbortDeliveryRun(deliveryRun, true);
                yield break;
            }

            CompleteDeliveryTaskWithoutRetry(item);
            NotifyDiningLineServed(item.Order, item.OrderLineId);
        }

        if (deliveryRun.RemainingLineCount == 0)
        {
            if (!deliveryRun.TryComplete())
            {
                Debug.LogError(
                    "La ronda terminó sus líneas, pero no pudo cerrarse.",
                    this
                );
                AbortDeliveryRun(deliveryRun, true);
                yield break;
            }

            Debug.Log(
                "Ronda 367H " + deliveryRun.RunId + " completada por el " +
                "camarero " + waiter.WaiterId + ".",
                this
            );

            activeRoutine = null;
            waiter.ClearAssignment();
            yield break;
        }

        if (!waiter.TryAdvanceDeliveryRunStop())
        {
            Debug.LogError(
                "La ronda conserva platos, pero no pudo avanzar de destino.",
                this
            );
            AbortDeliveryRun(deliveryRun, true);
            yield break;
        }

        activeRoutine = null;
        waiter.SetState(WaiterState.WalkingToServeTable);
    }

    // ---------------------------------------------------------------------
    // Compatibilidad con la entrega individual 367D.
    // ---------------------------------------------------------------------

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

        activeRoutine = null;
        waiter.SetState(WaiterState.WalkingToServeTable);
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

        if (!order.HasValidDestination || order.CustomerGroup == null)
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
            orderLineId + " en " +
            DescribeDestination(order.Table, order.BarSpot) + ".",
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
            out _,
            out error
        );

        if (!markedServed)
        {
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

        CompleteOrCancelTaskWithoutRetry(order, orderLineId);
        NotifyDiningLineServed(order, orderLineId);

        activeRoutine = null;
        waiter.ClearAssignment();
    }

    private bool ValidateAssignedRun(
        BistroBuilderDeliveryRun deliveryRun,
        BistroBuilderDeliveryRunState expectedState,
        out string error
    )
    {
        if (deliveryRun == null)
        {
            error = "El camarero no tiene una ronda asignada.";
            return false;
        }

        if (!IsDeliveryRunAssignmentStillValid(deliveryRun))
        {
            error = "La asignación camarero-ronda no es coherente.";
            return false;
        }

        if (deliveryRun.State != expectedState)
        {
            error = "La ronda está en " + deliveryRun.State +
                    " y se esperaba " + expectedState + ".";
            return false;
        }

        if (deliveryRun.SourceKitchen == null ||
            deliveryRun.SourceKitchen.PickupPoint == null)
        {
            error = "La ronda no tiene un punto de recogida válido.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool ValidateRunLineInTransit(
        BistroBuilderDeliveryRunItem item,
        out string error
    )
    {
        if (item == null)
        {
            error = "La línea de la ronda es nula.";
            return false;
        }

        if (!lineExecutionService.TryGetLineSnapshot(
                item.Order,
                item.OrderLineId,
                out _,
                out BistroBuilderCanonicalOrderLine line,
                out error
            ))
        {
            return false;
        }

        if (line.State != BistroBuilderCanonicalOrderLineState.InTransit)
        {
            error = "La línea " + item.OrderLineId + " está en " +
                    line.State + " y no puede servirse desde la bandeja.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool IsDeliveryRunAssignmentStillValid(
        BistroBuilderDeliveryRun deliveryRun
    )
    {
        return waiter != null &&
               deliveryRun != null &&
               !deliveryRun.IsTerminal &&
               ReferenceEquals(waiter.AssignedDeliveryRun, deliveryRun) &&
               ReferenceEquals(deliveryRun.AssignedWaiter, waiter);
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
               ReferenceEquals(waiter.AssignedBarSpot, order?.BarSpot) &&
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

    private void CompleteDeliveryTaskWithoutRetry(
        BistroBuilderDeliveryRunItem item
    )
    {
        if (item == null || taskCoordinator == null)
            return;

        if (!taskCoordinator.TryCompleteFoodDeliveryTask(
                item.Order,
                item.OrderLineId
            ))
        {
            Debug.LogWarning(
                "No se encontró la tarea activa de la línea " +
                item.OrderLineId + " dentro de la ronda.",
                this
            );
        }
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

    private void NotifyDiningLineServed(
        RestaurantOrder order,
        string orderLineId
    )
    {
        if (order != null && order.HasBarDestination)
        {
            string barError = string.Empty;
            bool reconciled = barServiceSystem != null &&
                barServiceSystem.TryNotifyLineServed(
                    order,
                    orderLineId,
                    out barError
                );

            if (!reconciled)
            {
                Debug.LogError(
                    "La línea " + orderLineId +
                    " se sirvió en barra, pero la sesión no pudo " +
                    "reconciliarse. " + barError,
                    this
                );
            }
            return;
        }

        if (!customerDiningService.TryNotifyLineServed(
                order,
                orderLineId,
                out BistroBuilderCustomerDiningNotificationResult dining,
                out string diningError
            ))
        {
            Debug.LogError(
                "La línea " + orderLineId +
                " se sirvió, pero no pudo reconciliarse con el consumo " +
                "individual. " + diningError,
                this
            );
            return;
        }

        if (dining.StartedCustomerCount > 0)
        {
            Debug.Log(
                "Línea " + orderLineId + " servida; " +
                dining.StartedCustomerCount +
                " cliente(s) comienzan a comer de forma individual.",
                this
            );
            return;
        }

        Debug.Log(
            "Línea " + orderLineId +
            " servida y reconciliada; otros clientes o pases " +
            "continúan pendientes.",
            this
        );
    }

    private static string DescribeDestination(
        RestaurantTable table,
        BistroBuilderBarServiceSpot barSpot
    )
    {
        if (table != null)
        {
            return "la mesa " + table.TableId;
        }

        if (barSpot != null)
        {
            return "la plaza de barra " + barSpot.BarSpotId;
        }

        return "un destino desconocido";
    }

    private void AbortDeliveryRun(
        BistroBuilderDeliveryRun deliveryRun,
        bool clearWaiterAssignment
    )
    {
        activeRoutine = null;

        bool recovered = taskCoordinator != null &&
            taskCoordinator.ReportFoodDeliveryRunFailure(
                waiter,
                deliveryRun
            );

        if (!recovered && deliveryRun != null)
        {
            string actorReference = waiter != null
                ? BistroBuilderServiceOrderIdentityUtility
                    .BuildWaiterReference(waiter.WaiterId)
                : "delivery_run_failure";

            for (int index = 0; index < deliveryRun.Items.Count; index++)
            {
                BistroBuilderDeliveryRunItem item = deliveryRun.Items[index];

                if (item.State == BistroBuilderDeliveryRunItemState.Served)
                    continue;

                if (lineExecutionService != null)
                {
                    lineExecutionService.TryReturnLineToPickup(
                        item.Order,
                        item.OrderLineId,
                        actorReference,
                        out _
                    );
                }
            }

            deliveryRun.TryCancel();
        }

        if (!clearWaiterAssignment || waiter == null)
            return;

        if (deliveryRun == null ||
            ReferenceEquals(waiter.AssignedDeliveryRun, deliveryRun))
        {
            waiter.ClearAssignment();
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
            waiter.ClearAssignment();
    }

    private void RecoverInterruptedRun(
        BistroBuilderDeliveryRun deliveryRun,
        bool clearAssignment
    )
    {
        AbortDeliveryRun(deliveryRun, clearAssignment);
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
        if (waiter == null)
            TryGetComponent(out waiter);

        if (waiterMovementView == null)
            TryGetComponent(out waiterMovementView);

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

        if (customerDiningService == null && taskCoordinator != null)
        {
            taskCoordinator.TryGetComponent(out customerDiningService);
        }

        if (customerDiningService == null)
        {
            customerDiningService = FindFirstObjectByType<
                BistroBuilderCustomerDiningService
            >();
        }

        if (barServiceSystem == null)
        {
            barServiceSystem = FindFirstObjectByType<
                BistroBuilderBarServiceSystem
            >();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        pickupDuration = Mathf.Max(0.1f, pickupDuration);
        additionalPickupDurationPerLine = Mathf.Max(
            0f,
            additionalPickupDurationPerLine
        );
        servingDuration = Mathf.Max(0.1f, servingDuration);
        ResolveDependencies();
    }
#endif
}
