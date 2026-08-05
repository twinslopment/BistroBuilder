using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestador de la vida operativa de cada plato físico.
///
/// La autoridad de estado continúa siendo BistroBuilderCanonicalOrderService.
/// Este servicio coordina transacciones con cocina, camarero y fachada legacy,
/// evitando que esos sistemas muten directamente una línea.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Orders/Order Line Execution Service"
)]
public sealed class BistroBuilderOrderLineExecutionService : MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderCanonicalOrderService canonicalOrderService;

    [SerializeField]
    private BistroBuilderCanonicalOrderIntegrationService integrationService;

    [Header("Depuración")]

    [SerializeField]
    private bool logTransitions = true;

    public BistroBuilderCanonicalOrderService CanonicalOrderService =>
        canonicalOrderService;

    public BistroBuilderCanonicalOrderIntegrationService IntegrationService =>
        integrationService;

    private void Awake()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (canonicalOrderService == null)
        {
            error = "Falta BistroBuilderCanonicalOrderService.";
            return false;
        }

        if (!canonicalOrderService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (integrationService == null)
        {
            error = "Falta BistroBuilderCanonicalOrderIntegrationService.";
            return false;
        }

        if (!integrationService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!integrationService.IndividualLineExecutionEnabled)
        {
            error =
                "La integración 367C no tiene activada la ejecución " +
                "individual de líneas 367D.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetLineSnapshot(
        RestaurantOrder order,
        string orderLineId,
        out BistroBuilderCanonicalOrder orderSnapshot,
        out BistroBuilderCanonicalOrderLine lineSnapshot,
        out string error
    )
    {
        orderSnapshot = null;
        lineSnapshot = null;

        if (!TryValidateLinkedOrder(order, out error))
        {
            return false;
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        if (!BistroBuilderOrderIdUtility.IsValid(normalizedLineId))
        {
            error = "El LineId indicado no es válido.";
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out orderSnapshot
            ))
        {
            error = "No se encontró la comanda canónica enlazada.";
            return false;
        }

        if (orderSnapshot == null)
        {
            error = "La fotografía canónica enlazada es nula.";
            return false;
        }

        if (!orderSnapshot.TryGetLine(
                normalizedLineId,
                out lineSnapshot
            ))
        {
            orderSnapshot = null;
            error = "La línea no pertenece a la comanda indicada.";
            return false;
        }

        if (lineSnapshot == null)
        {
            orderSnapshot = null;
            error = "La fotografía de la línea indicada es nula.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryResolvePreparationDurationSeconds(
        RestaurantOrder order,
        string orderLineId,
        float durationScale,
        float minimumDuration,
        float maximumDuration,
        out float durationSeconds,
        out string error
    )
    {
        durationSeconds = 0f;

        if (!TryGetLineSnapshot(
                order,
                orderLineId,
                out _,
                out BistroBuilderCanonicalOrderLine line,
                out error
            ))
        {
            return false;
        }

        return TryResolveDishPreparationDurationSeconds(
            line.DishId,
            durationScale,
            minimumDuration,
            maximumDuration,
            out durationSeconds,
            out error
        );
    }

    /// <summary>
    /// Resuelve la duración que KitchenSystem captura al encolar una línea.
    /// Una vez creada la unidad de trabajo, sus tiempos total y restante son
    /// autónomos y no cambian aunque la carta se edite después.
    /// </summary>
    public bool TryResolveDishPreparationDurationSeconds(
        string dishId,
        float durationScale,
        float minimumDuration,
        float maximumDuration,
        out float durationSeconds,
        out string error
    )
    {
        durationSeconds = 0f;
        CacheDependenciesIfNeeded();

        if (float.IsNaN(durationScale) ||
            float.IsInfinity(durationScale) ||
            durationScale <= 0f)
        {
            error = "La escala de preparación debe ser positiva.";
            return false;
        }

        if (float.IsNaN(minimumDuration) ||
            float.IsInfinity(minimumDuration) ||
            float.IsNaN(maximumDuration) ||
            float.IsInfinity(maximumDuration) ||
            minimumDuration <= 0f ||
            maximumDuration < minimumDuration)
        {
            error = "Los límites de duración de preparación son inválidos.";
            return false;
        }

        BistroBuilderRestaurantMenuService menu =
            canonicalOrderService != null
                ? canonicalOrderService.MenuService
                : null;

        if (menu == null)
        {
            error = "La comanda canónica no tiene una carta operativa.";
            return false;
        }

        if (!menu.TryResolvePreparationSettings(
                dishId,
                out _,
                out int preparationSeconds,
                out error
            ))
        {
            return false;
        }

        durationSeconds = Mathf.Clamp(
            preparationSeconds * durationScale,
            minimumDuration,
            maximumDuration
        );

        error = string.Empty;
        return true;
    }

    public bool TryBeginPreparation(
        RestaurantOrder order,
        string orderLineId,
        string kitchenReferenceId,
        out string error
    )
    {
        if (!TryGetLineSnapshot(
                order,
                orderLineId,
                out _,
                out BistroBuilderCanonicalOrderLine line,
                out error
            ))
        {
            return false;
        }

        if (line.State != BistroBuilderCanonicalOrderLineState.Queued)
        {
            error = "La línea no está en cola de cocina.";
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryTransitionLine(
                line.LineId,
                BistroBuilderCanonicalOrderLineState.Preparing,
                kitchenReferenceId
            );

        if (!result.Succeeded)
        {
            error = result.Message;
            return false;
        }

        BistroBuilderOrderInventoryLifecycleService inventoryLifecycle =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderOrderInventoryLifecycleService>();

        if (inventoryLifecycle == null)
        {
            BistroBuilderCanonicalOrderOperationResult rollbackInventory =
                canonicalOrderService.TryTransitionLine(
                    line.LineId,
                    BistroBuilderCanonicalOrderLineState.Queued,
                    "inventory_service_missing_rollback"
                );

            error = "No se pudo consumir la reserva de ingredientes porque " +
                    "falta BistroBuilderOrderInventoryLifecycleService.";
            if (!rollbackInventory.Succeeded)
            {
                error += " Además, falló el rollback canónico: " +
                         rollbackInventory.Message;
            }
            return false;
        }

        string inventoryError;
        if (!inventoryLifecycle.TryConsumeLine(
                order,
                line.LineId,
                out inventoryError))
        {
            BistroBuilderCanonicalOrderOperationResult rollbackInventory =
                canonicalOrderService.TryTransitionLine(
                    line.LineId,
                    BistroBuilderCanonicalOrderLineState.Queued,
                    "inventory_consumption_rollback"
                );

            error = "No se pudo consumir la reserva de ingredientes: " +
                    (string.IsNullOrWhiteSpace(inventoryError)
                        ? "error de inventario no especificado."
                        : inventoryError);
            if (!rollbackInventory.Succeeded)
            {
                error += " Además, falló el rollback canónico: " +
                         rollbackInventory.Message;
            }
            return false;
        }

        // La comanda legacy se mueve a Preparing después de que la autoridad
        // canónica haya confirmado al menos una línea en preparación.
        if (order.CurrentState == OrderState.SentToKitchen &&
            !order.TrySetState(OrderState.Preparing))
        {
            BistroBuilderCanonicalOrderOperationResult rollback =
                canonicalOrderService.TryTransitionLine(
                    line.LineId,
                    BistroBuilderCanonicalOrderLineState.Queued,
                    "preparation_rollback"
                );

            error = "No se pudo sincronizar la fachada legacy. " +
                    order.LastTransitionError;

            if (!rollback.Succeeded)
            {
                error += " Además, falló el rollback canónico: " +
                         rollback.Message;
            }

            return false;
        }

        LogTransition(order, line.LineId, "Preparing");
        error = string.Empty;
        return true;
    }

    public bool TryInterruptPreparation(
        RestaurantOrder order,
        string orderLineId,
        string kitchenReferenceId,
        out string error
    )
    {
        if (!TryGetLineSnapshot(
                order,
                orderLineId,
                out _,
                out BistroBuilderCanonicalOrderLine line,
                out error
            ))
        {
            return false;
        }

        if (line.State == BistroBuilderCanonicalOrderLineState.Queued)
        {
            error = string.Empty;
            return true;
        }

        if (line.State != BistroBuilderCanonicalOrderLineState.Preparing)
        {
            error = "Solo puede interrumpirse una línea en preparación.";
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryTransitionLine(
                line.LineId,
                BistroBuilderCanonicalOrderLineState.Queued,
                kitchenReferenceId
            );

        error = result.Succeeded ? string.Empty : result.Message;
        return result.Succeeded;
    }

    public bool TryCompletePreparation(
        RestaurantOrder order,
        string orderLineId,
        string kitchenReferenceId,
        out bool productionComplete,
        out string error
    )
    {
        productionComplete = false;

        if (!TryGetLineSnapshot(
                order,
                orderLineId,
                out _,
                out BistroBuilderCanonicalOrderLine line,
                out error
            ))
        {
            return false;
        }

        if (line.State != BistroBuilderCanonicalOrderLineState.Preparing)
        {
            error = "La línea no está en preparación.";
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryTransitionLine(
                line.LineId,
                BistroBuilderCanonicalOrderLineState.ReadyForPickup,
                kitchenReferenceId
            );

        if (!result.Succeeded)
        {
            error = result.Message;
            return false;
        }

        if (!TrySynchronizeLegacyOrder(
                order,
                out productionComplete,
                out _,
                out string synchronizationError
            ))
        {
            // ReadyForPickup representa un plato físico ya terminado y no se
            // puede deshacer sin duplicar producción. Se conserva el éxito de
            // cocina, se recalcula el agregado desde la autoridad canónica y
            // se devuelve el desfase como advertencia recuperable.
            if (canonicalOrderService.TryGetOrderSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder freshSnapshot
                ) &&
                freshSnapshot != null)
            {
                productionComplete = AreAllLinesPastKitchen(freshSnapshot);
            }

            LogTransition(order, line.LineId, "ReadyForPickup");
            error =
                "La línea quedó lista, pero la fachada legacy necesita " +
                "reconciliación: " + synchronizationError;
            return true;
        }

        LogTransition(order, line.LineId, "ReadyForPickup");
        error = string.Empty;
        return true;
    }

    public bool TryAssignLineForDelivery(
        RestaurantOrder order,
        string orderLineId,
        Waiter waiter,
        out string error
    )
    {
        if (waiter == null)
        {
            error = "No se puede asignar una línea sin camarero.";
            return false;
        }

        if (!waiter.IsAvailable)
        {
            error = "El camarero no está disponible.";
            return false;
        }

        if (!TryGetLineSnapshot(
                order,
                orderLineId,
                out _,
                out BistroBuilderCanonicalOrderLine line,
                out error
            ))
        {
            return false;
        }

        if (line.State !=
            BistroBuilderCanonicalOrderLineState.ReadyForPickup)
        {
            error = "La línea no está lista para recogida.";
            return false;
        }

        string actorReference =
            BistroBuilderServiceOrderIdentityUtility
                .BuildWaiterReference(waiter.WaiterId);

        BistroBuilderCanonicalOrderOperationResult reserveResult =
            canonicalOrderService.TryTransitionLine(
                line.LineId,
                BistroBuilderCanonicalOrderLineState.AssignedForDelivery,
                actorReference
            );

        if (!reserveResult.Succeeded)
        {
            error = reserveResult.Message;
            return false;
        }

        if (!waiter.AssignOrderLineForPickup(order, line.LineId))
        {
            BistroBuilderCanonicalOrderOperationResult rollback =
                canonicalOrderService.TryTransitionLine(
                    line.LineId,
                    BistroBuilderCanonicalOrderLineState.ReadyForPickup,
                    "delivery_assignment_rollback"
                );

            error = "El camarero rechazó la asignación de la línea.";

            if (!rollback.Succeeded)
            {
                error += " Falló el rollback: " + rollback.Message;
            }

            return false;
        }

        LogTransition(order, line.LineId, "AssignedForDelivery");
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reserva de forma transaccional todas las líneas de una ronda 367G.
    ///
    /// Primero valida el lote completo y después realiza las transiciones.
    /// Si cualquier línea falla, las ya reservadas regresan al pase antes de
    /// devolver el control al coordinador.
    /// </summary>
    public bool TryReserveDeliveryRun(
        BistroBuilderDeliveryRun deliveryRun,
        Waiter waiter,
        out string error
    )
    {
        if (deliveryRun == null)
        {
            error = "La ronda de reparto es nula.";
            return false;
        }

        if (waiter == null)
        {
            error = "No se puede reservar una ronda sin camarero.";
            return false;
        }

        if (!waiter.IsAvailable)
        {
            error = "El camarero no está disponible para una nueva ronda.";
            return false;
        }

        if (deliveryRun.State != BistroBuilderDeliveryRunState.Planned ||
            deliveryRun.Items.Count == 0 ||
            deliveryRun.Items.Count > waiter.FoodDeliveryCapacity)
        {
            error = "La ronda no está planificada o supera la capacidad.";
            return false;
        }

        // La validación completa se ejecuta antes de la primera mutación.
        for (int index = 0; index < deliveryRun.Items.Count; index++)
        {
            BistroBuilderDeliveryRunItem item = deliveryRun.Items[index];

            if (!TryGetLineSnapshot(
                    item.Order,
                    item.OrderLineId,
                    out _,
                    out BistroBuilderCanonicalOrderLine line,
                    out error
                ))
            {
                return false;
            }

            if (line.State !=
                BistroBuilderCanonicalOrderLineState.ReadyForPickup)
            {
                error = "La línea " + item.OrderLineId +
                        " ya no está lista para recogida.";
                return false;
            }
        }

        string actorReference =
            BistroBuilderServiceOrderIdentityUtility
                .BuildWaiterReference(waiter.WaiterId);

        int reservedCount = 0;

        for (int index = 0; index < deliveryRun.Items.Count; index++)
        {
            BistroBuilderDeliveryRunItem item = deliveryRun.Items[index];

            BistroBuilderCanonicalOrderOperationResult result =
                canonicalOrderService.TryTransitionLine(
                    item.OrderLineId,
                    BistroBuilderCanonicalOrderLineState.AssignedForDelivery,
                    actorReference
                );

            if (!result.Succeeded)
            {
                string rollbackError = RollbackDeliveryRunReservation(
                    deliveryRun,
                    reservedCount
                );

                error = "No se pudo reservar la línea " +
                        item.OrderLineId + ": " + result.Message;

                if (!string.IsNullOrEmpty(rollbackError))
                {
                    error += " Falló parte del rollback: " + rollbackError;
                }

                return false;
            }

            reservedCount++;
            LogTransition(
                item.Order,
                item.OrderLineId,
                "AssignedForDelivery (ronda " + deliveryRun.RunId + ")"
            );
        }

        error = string.Empty;
        return true;
    }

    public bool TryMarkLineInTransit(
        RestaurantOrder order,
        string orderLineId,
        Waiter waiter,
        out string error
    )
    {
        if (!ValidateWaiterLineAssignment(
                order,
                orderLineId,
                waiter,
                out error
            ))
        {
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryTransitionLine(
                orderLineId,
                BistroBuilderCanonicalOrderLineState.InTransit,
                BistroBuilderServiceOrderIdentityUtility
                    .BuildWaiterReference(waiter.WaiterId)
            );

        error = result.Succeeded ? string.Empty : result.Message;

        if (result.Succeeded)
        {
            LogTransition(order, orderLineId, "InTransit");
        }

        return result.Succeeded;
    }

    public bool TryMarkLineServed(
        RestaurantOrder order,
        string orderLineId,
        Waiter waiter,
        out bool allActiveLinesServed,
        out string error
    )
    {
        allActiveLinesServed = false;

        if (!ValidateWaiterLineAssignment(
                order,
                orderLineId,
                waiter,
                out error
            ))
        {
            return false;
        }

        // Antes de realizar la transición irreversible a Served se pone al
        // día la fachada coarse. Así, cuando esta sea la última línea, la
        // comanda legacy ya estará en ReadyForPickup y podrá avanzar a Served
        // sin dejar una mutación canónica parcial.
        if (!TrySynchronizeLegacyOrder(
                order,
                out _,
                out _,
                out error
            ))
        {
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryTransitionLine(
                orderLineId,
                BistroBuilderCanonicalOrderLineState.Served,
                BistroBuilderServiceOrderIdentityUtility
                    .BuildWaiterReference(waiter.WaiterId)
            );

        if (!result.Succeeded)
        {
            error = result.Message;
            return false;
        }

        if (!TrySynchronizeLegacyOrder(
                order,
                out _,
                out allActiveLinesServed,
                out error
            ))
        {
            return false;
        }

        LogTransition(order, orderLineId, "Served");
        error = string.Empty;
        return true;
    }

    public bool TryReturnLineToPickup(
        RestaurantOrder order,
        string orderLineId,
        Waiter waiter,
        out string error
    )
    {
        string actorReference = waiter != null
            ? BistroBuilderServiceOrderIdentityUtility
                .BuildWaiterReference(waiter.WaiterId)
            : "delivery_recovery";

        return TryReturnLineToPickup(
            order,
            orderLineId,
            actorReference,
            out error
        );
    }

    public bool TryReturnLineToPickup(
        RestaurantOrder order,
        string orderLineId,
        string actorReferenceId,
        out string error
    )
    {
        if (!TryGetLineSnapshot(
                order,
                orderLineId,
                out _,
                out BistroBuilderCanonicalOrderLine line,
                out error
            ))
        {
            return false;
        }

        if (line.State ==
            BistroBuilderCanonicalOrderLineState.ReadyForPickup)
        {
            error = string.Empty;
            return true;
        }

        bool canReturn =
            line.State ==
                BistroBuilderCanonicalOrderLineState.AssignedForDelivery ||
            line.State == BistroBuilderCanonicalOrderLineState.InTransit;

        if (!canReturn)
        {
            error = "La línea no puede regresar al pase desde " +
                    line.State + ".";
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryTransitionLine(
                line.LineId,
                BistroBuilderCanonicalOrderLineState.ReadyForPickup,
                actorReferenceId
            );

        error = result.Succeeded ? string.Empty : result.Message;
        return result.Succeeded;
    }

    public bool IsLineReadyForPickup(
        RestaurantOrder order,
        string orderLineId
    )
    {
        return TryGetLineSnapshot(
                   order,
                   orderLineId,
                   out _,
                   out BistroBuilderCanonicalOrderLine line,
                   out _
               ) &&
               line.State ==
                   BistroBuilderCanonicalOrderLineState.ReadyForPickup;
    }

    /// <summary>
    /// Sincroniza únicamente la fachada coarse. No modifica líneas.
    /// </summary>
    public bool TrySynchronizeLegacyOrder(
        RestaurantOrder order,
        out bool productionComplete,
        out bool allActiveLinesServed,
        out string error
    )
    {
        productionComplete = false;
        allActiveLinesServed = false;

        if (!TryValidateLinkedOrder(order, out error))
        {
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder snapshot
            ))
        {
            error = "No se encontró la comanda canónica enlazada.";
            return false;
        }

        if (snapshot == null)
        {
            error = "La fotografía canónica enlazada es nula.";
            return false;
        }

        productionComplete = AreAllLinesPastKitchen(snapshot);
        allActiveLinesServed = AreAllActiveLinesServed(snapshot);

        bool continueSynchronizing = true;
        int safety = 0;

        while (continueSynchronizing && safety < 3)
        {
            safety++;
            continueSynchronizing = false;

            if (order.CurrentState == OrderState.SentToKitchen &&
                HasAnyLineStartedPreparation(snapshot))
            {
                if (!order.TrySetState(OrderState.Preparing))
                {
                    error = order.LastTransitionError;
                    return false;
                }

                continueSynchronizing = true;
                continue;
            }

            if (order.CurrentState == OrderState.Preparing &&
                productionComplete)
            {
                if (!order.TrySetState(OrderState.ReadyForPickup))
                {
                    error = order.LastTransitionError;
                    return false;
                }

                continueSynchronizing = true;
                continue;
            }

            if (order.CurrentState == OrderState.ReadyForPickup &&
                allActiveLinesServed)
            {
                if (!order.TrySetState(OrderState.Served))
                {
                    error = order.LastTransitionError;
                    return false;
                }

                continueSynchronizing = true;
            }
        }

        error = string.Empty;
        return true;
    }

    private string RollbackDeliveryRunReservation(
        BistroBuilderDeliveryRun deliveryRun,
        int reservedCount
    )
    {
        if (deliveryRun == null || reservedCount <= 0)
            return string.Empty;

        List<string> errors = new List<string>();
        int count = Math.Min(reservedCount, deliveryRun.Items.Count);

        for (int index = count - 1; index >= 0; index--)
        {
            BistroBuilderDeliveryRunItem item = deliveryRun.Items[index];

            BistroBuilderCanonicalOrderOperationResult rollback =
                canonicalOrderService.TryTransitionLine(
                    item.OrderLineId,
                    BistroBuilderCanonicalOrderLineState.ReadyForPickup,
                    "delivery_run_assignment_rollback"
                );

            if (!rollback.Succeeded)
            {
                errors.Add(item.OrderLineId + ": " + rollback.Message);
            }
        }

        return string.Join(" | ", errors);
    }

    private bool ValidateWaiterLineAssignment(
        RestaurantOrder order,
        string orderLineId,
        Waiter waiter,
        out string error
    )
    {
        if (waiter == null)
        {
            error = "El camarero es nulo.";
            return false;
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        bool ownsLegacyLine =
            ReferenceEquals(waiter.AssignedOrder, order) &&
            string.Equals(
                waiter.AssignedOrderLineId,
                normalizedLineId,
                StringComparison.Ordinal
            );

        bool ownsRunLine = waiter.HasDeliveryLine(
            order,
            normalizedLineId
        );

        if (!ownsLegacyLine && !ownsRunLine)
        {
            error = "El camarero no tiene asignada esa línea.";
            return false;
        }

        return TryValidateLinkedOrder(order, out error);
    }

    private bool TryValidateLinkedOrder(
        RestaurantOrder order,
        out string error
    )
    {
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (order == null)
        {
            error = "La comanda legacy es nula.";
            return false;
        }

        if (!order.HasCanonicalOrder)
        {
            error = "La comanda legacy no está enlazada.";
            return false;
        }

        if (!integrationService.TryGetLinkedCanonicalOrderId(
                order.OrderId,
                out string linkedId
            ))
        {
            error = "El enlace legacy-canónico no está registrado.";
            return false;
        }

        if (!string.Equals(
                linkedId,
                order.CanonicalOrderId,
                StringComparison.Ordinal
            ))
        {
            error = "El enlace legacy-canónico no coincide con la comanda.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool HasAnyLineStartedPreparation(
        BistroBuilderCanonicalOrder snapshot
    )
    {
        for (int index = 0; index < snapshot.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLineState state =
                snapshot.Lines[index].State;

            int stateValue = (int)state;

            if (stateValue >=
                    (int)BistroBuilderCanonicalOrderLineState.Preparing &&
                stateValue <=
                    (int)BistroBuilderCanonicalOrderLineState.Consumed)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreAllLinesPastKitchen(
        BistroBuilderCanonicalOrder snapshot
    )
    {
        bool hasActiveLine = false;

        for (int index = 0; index < snapshot.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLineState state =
                snapshot.Lines[index].State;

            if (state == BistroBuilderCanonicalOrderLineState.Cancelled)
            {
                continue;
            }

            if (state == BistroBuilderCanonicalOrderLineState.Failed)
            {
                return false;
            }

            hasActiveLine = true;

            if (state == BistroBuilderCanonicalOrderLineState.Draft ||
                state == BistroBuilderCanonicalOrderLineState.Submitted ||
                state == BistroBuilderCanonicalOrderLineState.Queued ||
                state == BistroBuilderCanonicalOrderLineState.Preparing)
            {
                return false;
            }
        }

        return hasActiveLine;
    }

    private static bool AreAllActiveLinesServed(
        BistroBuilderCanonicalOrder snapshot
    )
    {
        bool hasActiveLine = false;

        for (int index = 0; index < snapshot.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLineState state =
                snapshot.Lines[index].State;

            if (state == BistroBuilderCanonicalOrderLineState.Cancelled)
            {
                continue;
            }

            hasActiveLine = true;

            if (state != BistroBuilderCanonicalOrderLineState.Served &&
                state != BistroBuilderCanonicalOrderLineState.Consumed)
            {
                return false;
            }
        }

        return hasActiveLine;
    }

    private void LogTransition(
        RestaurantOrder order,
        string lineId,
        string target
    )
    {
        if (!logTransitions)
        {
            return;
        }

        Debug.Log(
            "367D sincroniza la línea " + lineId +
            " de la comanda " + order.OrderId +
            " con " + target + ".",
            this
        );
    }

    private void CacheDependenciesIfNeeded()
    {
        if (canonicalOrderService == null)
        {
            TryGetComponent(out canonicalOrderService);
        }

        if (integrationService == null)
        {
            TryGetComponent(out integrationService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
