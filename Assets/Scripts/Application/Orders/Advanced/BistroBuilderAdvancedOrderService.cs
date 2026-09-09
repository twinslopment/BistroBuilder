using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad de aplicación del bloque 11. No sustituye a la comanda canónica:
/// coordina revisiones, incidencias e inventario mediante sus autoridades.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Orders/Advanced Order Service 11")]
public sealed class BistroBuilderAdvancedOrderService : MonoBehaviour
{
    [SerializeField] private BistroBuilderCanonicalOrderService canonicalOrderService;
    [SerializeField] private OrderSystem orderSystem;
    [SerializeField] private BistroBuilderOrderInventoryLifecycleService inventoryLifecycle;

    public event Action AdvancedOrdersChanged;

    public BistroBuilderCanonicalOrderService CanonicalOrderService => canonicalOrderService;
    public BistroBuilderOrderInventoryLifecycleService InventoryLifecycle => inventoryLifecycle;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (canonicalOrderService == null || orderSystem == null || inventoryLifecycle == null)
        {
            error = "11 necesita comandas canónicas, OrderSystem e inventario 368CD.";
            return false;
        }
        if (!canonicalOrderService.ValidateConfiguration(out error) ||
            !orderSystem.ValidateConfiguration(out error) ||
            !inventoryLifecycle.ValidateConfiguration(out error))
            return false;
        if (orderSystem.CanonicalIntegrationService == null ||
            !ReferenceEquals(
                orderSystem.CanonicalIntegrationService.CanonicalOrderService,
                canonicalOrderService))
        {
            error = "11 no comparte la autoridad canónica de OrderSystem.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryGetAvailability(
        string orderId,
        string lineId,
        out BistroBuilderAdvancedOrderActionAvailability availability,
        out string error)
    {
        availability = null;
        if (!TryResolve(orderId, lineId, out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderLine line, out _, out error))
            return false;
        availability = BistroBuilderAdvancedOrderMutationPolicy.Evaluate(order, line);
        return true;
    }

    /// <summary>
    /// Corrige un plato aún no preparado. La línea original se conserva como
    /// cancelada y la nueva referencia conserva SourceLineId para auditoría.
    /// </summary>
    public BistroBuilderAdvancedOrderMutationResult TryCorrectLine(
        string orderId,
        string lineId,
        string replacementDishId,
        BistroBuilderAdvancedOrderIncidentKind incidentKind,
        string reason,
        string actorReferenceId)
    {
        if (!TryResolve(orderId, lineId, out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderLine source,
                out RestaurantOrder legacyOrder, out string error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                error, orderId, lineId);
        if (order.IsTerminal)
            return Fail(BistroBuilderAdvancedOrderRestriction.OrderTerminal,
                "La comanda ya está cerrada.", order.OrderId, source.LineId);
        if (!BistroBuilderAdvancedOrderMutationPolicy.CanCorrect(source.State))
            return Fail(RestrictionFor(source.State),
                BistroBuilderAdvancedOrderMutationPolicy.ResolveRestrictionLabel(source.State),
                order.OrderId, source.LineId);

        string newLineId = BistroBuilderOrderIdUtility.NewLineId();
        if (!TryReserveNewLine(
                legacyOrder, order, newLineId, replacementDishId,
                out _, out error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InventoryUnavailable,
                error, order.OrderId, source.LineId);

        BistroBuilderCanonicalOrderLineState targetState =
            BistroBuilderAdvancedOrderMutationPolicy.ResolveNewLineState(
                source.State, BistroBuilderAdvancedOrderLineOriginKind.Correction);
        var added = canonicalOrderService.TryAddAdvancedLineFromSource(
            order.OrderId, source.LineId, newLineId, replacementDishId,
            BistroBuilderAdvancedOrderLineOriginKind.Correction,
            BistroBuilderAdvancedOrderBillingMode.Standard,
            incidentKind,
            reason,
            actorReferenceId,
            targetState,
            out _);
        if (!added.Succeeded)
        {
            RollbackNewReservation(legacyOrder, newLineId);
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                added.Message, order.OrderId, source.LineId);
        }

        var cancelled = canonicalOrderService.TryCancelAdvancedLine(
            source.LineId,
            incidentKind == BistroBuilderAdvancedOrderIncidentKind.None
                ? BistroBuilderAdvancedOrderIncidentKind.CustomerChange : incidentKind,
            reason,
            actorReferenceId);
        if (!cancelled.Succeeded)
        {
            RollbackAddedLine(legacyOrder, newLineId, actorReferenceId);
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                cancelled.Message, order.OrderId, source.LineId);
        }
        if (!inventoryLifecycle.TryReleaseCanonicalLineReservation(
                legacyOrder, source.LineId,
                "Corrección de comanda antes de preparación.", out error))
        {
            return Fail(BistroBuilderAdvancedOrderRestriction.InventoryUnavailable,
                error, order.OrderId, source.LineId);
        }
        NotifyChanged();
        return BistroBuilderAdvancedOrderMutationResult.Success(
            "Plato corregido sin perder la trazabilidad de la comanda.",
            order.OrderId, source.LineId, newLineId);
    }

    /// <summary>Añade otra unidad del mismo plato como nueva línea facturable.</summary>
    public BistroBuilderAdvancedOrderMutationResult TryRepeatLine(
        string orderId,
        string lineId,
        string actorReferenceId)
    {
        if (!TryResolve(orderId, lineId, out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderLine source,
                out RestaurantOrder legacyOrder, out string error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                error, orderId, lineId);
        if (order.IsTerminal)
            return Fail(BistroBuilderAdvancedOrderRestriction.OrderTerminal,
                "La comanda ya está cerrada.", order.OrderId, source.LineId);
        if (!BistroBuilderAdvancedOrderMutationPolicy.CanRepeat(source.State))
            return Fail(RestrictionFor(source.State),
                "Esta consumición todavía no puede repetirse.",
                order.OrderId, source.LineId);

        string newLineId = BistroBuilderOrderIdUtility.NewLineId();
        if (!TryReserveNewLine(
                legacyOrder, order, newLineId, source.DishId,
                out _, out error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InventoryUnavailable,
                error, order.OrderId, source.LineId);

        var added = canonicalOrderService.TryAddAdvancedLineFromSource(
            order.OrderId, source.LineId, newLineId, source.DishId,
            BistroBuilderAdvancedOrderLineOriginKind.Repeat,
            BistroBuilderAdvancedOrderBillingMode.Standard,
            BistroBuilderAdvancedOrderIncidentKind.None,
            "Repetición solicitada por el cliente.",
            actorReferenceId,
            BistroBuilderCanonicalOrderLineState.Queued,
            out _);
        if (!added.Succeeded)
        {
            RollbackNewReservation(legacyOrder, newLineId);
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                added.Message, order.OrderId, source.LineId);
        }
        NotifyChanged();
        return BistroBuilderAdvancedOrderMutationResult.Success(
            "Consumición repetida y añadida a la cuenta.",
            order.OrderId, source.LineId, newLineId);
    }

    /// <summary>Cancelación parcial segura de una sola línea no preparada.</summary>
    public BistroBuilderAdvancedOrderMutationResult TryCancelLine(
        string orderId,
        string lineId,
        BistroBuilderAdvancedOrderIncidentKind incidentKind,
        string reason,
        string actorReferenceId)
    {
        if (!TryResolve(orderId, lineId, out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderLine source,
                out RestaurantOrder legacyOrder, out string error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                error, orderId, lineId);
        if (!BistroBuilderAdvancedOrderMutationPolicy.CanCancel(source.State))
            return Fail(RestrictionFor(source.State),
                BistroBuilderAdvancedOrderMutationPolicy.ResolveRestrictionLabel(source.State),
                order.OrderId, source.LineId);

        var cancelled = canonicalOrderService.TryCancelAdvancedLine(
            source.LineId, incidentKind, reason, actorReferenceId);
        if (!cancelled.Succeeded)
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                cancelled.Message, order.OrderId, source.LineId);
        if (!inventoryLifecycle.TryReleaseCanonicalLineReservation(
                legacyOrder, source.LineId,
                "Cancelación parcial antes de preparación.", out error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InventoryUnavailable,
                error, order.OrderId, source.LineId);

        NotifyChanged();
        return BistroBuilderAdvancedOrderMutationResult.Success(
            "Línea cancelada; el resto de la comanda continúa.",
            order.OrderId, source.LineId);
    }

    /// <summary>
    /// Reemplaza un plato cuando ya existe coste físico. La línea defectuosa
    /// termina en Failed y la nueva genera su propia reserva; no se repone stock.
    /// </summary>
    public BistroBuilderAdvancedOrderMutationResult TryReplaceAfterIncident(
        string orderId,
        string lineId,
        string replacementDishId,
        BistroBuilderAdvancedOrderIncidentKind incidentKind,
        string reason,
        bool courtesy,
        string actorReferenceId)
    {
        if (!TryResolve(orderId, lineId, out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderLine source,
                out RestaurantOrder legacyOrder, out string error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                error, orderId, lineId);
        if (!BistroBuilderAdvancedOrderMutationPolicy.CanReplaceAfterIncident(source.State))
            return Fail(RestrictionFor(source.State),
                BistroBuilderAdvancedOrderMutationPolicy.ResolveRestrictionLabel(source.State),
                order.OrderId, source.LineId);
        if (incidentKind == BistroBuilderAdvancedOrderIncidentKind.None)
            incidentKind = BistroBuilderAdvancedOrderIncidentKind.QualityIssue;

        string dishId = string.IsNullOrWhiteSpace(replacementDishId)
            ? source.DishId : replacementDishId;
        string newLineId = BistroBuilderOrderIdUtility.NewLineId();
        if (!TryReserveNewLine(
                legacyOrder, order, newLineId, dishId,
                out _, out error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InventoryUnavailable,
                error, order.OrderId, source.LineId);

        var added = canonicalOrderService.TryAddAdvancedLineFromSource(
            order.OrderId, source.LineId, newLineId, dishId,
            courtesy
                ? BistroBuilderAdvancedOrderLineOriginKind.Courtesy
                : BistroBuilderAdvancedOrderLineOriginKind.Replacement,
            courtesy
                ? BistroBuilderAdvancedOrderBillingMode.Courtesy
                : BistroBuilderAdvancedOrderBillingMode.Standard,
            incidentKind,
            reason,
            actorReferenceId,
            BistroBuilderCanonicalOrderLineState.Queued,
            out _);
        if (!added.Succeeded)
        {
            RollbackNewReservation(legacyOrder, newLineId);
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                added.Message, order.OrderId, source.LineId);
        }

        var failed = canonicalOrderService.TryFailAdvancedLine(
            source.LineId, incidentKind, reason, actorReferenceId);
        if (!failed.Succeeded)
        {
            RollbackAddedLine(legacyOrder, newLineId, actorReferenceId);
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                failed.Message, order.OrderId, source.LineId);
        }
        NotifyChanged();
        return BistroBuilderAdvancedOrderMutationResult.Success(
            courtesy
                ? "Incidencia resuelta con reposición de cortesía."
                : "Incidencia resuelta con una reposición facturable una sola vez.",
            order.OrderId, source.LineId, newLineId);
    }

    /// <summary>Devuelve un plato servido y lo retira de la cuenta sin reponer stock.</summary>
    public BistroBuilderAdvancedOrderMutationResult TryReturnWithoutReplacement(
        string orderId,
        string lineId,
        BistroBuilderAdvancedOrderIncidentKind incidentKind,
        string reason,
        string actorReferenceId)
    {
        if (!TryResolve(orderId, lineId, out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderLine source,
                out _, out string error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                error, orderId, lineId);
        if (!BistroBuilderAdvancedOrderMutationPolicy.CanReturn(source.State))
            return Fail(RestrictionFor(source.State),
                "Solo puede devolverse un plato que ya fue servido y aún no consumido.",
                order.OrderId, source.LineId);
        if (incidentKind == BistroBuilderAdvancedOrderIncidentKind.None)
            incidentKind = BistroBuilderAdvancedOrderIncidentKind.QualityIssue;
        var failed = canonicalOrderService.TryFailAdvancedLine(
            source.LineId, incidentKind, reason, actorReferenceId);
        if (!failed.Succeeded)
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                failed.Message, order.OrderId, source.LineId);
        NotifyChanged();
        return BistroBuilderAdvancedOrderMutationResult.Success(
            "Plato devuelto: no se cobra y el inventario consumido no reaparece.",
            order.OrderId, source.LineId);
    }

    public BistroBuilderAdvancedOrderMutationResult TryReportIncident(
        string orderId,
        string lineId,
        BistroBuilderAdvancedOrderIncidentKind incidentKind,
        string reason,
        string actorReferenceId)
    {
        if (!TryResolve(orderId, lineId, out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderLine source, out _, out string error))
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                error, orderId, lineId);
        var result = canonicalOrderService.TryRegisterAdvancedLineIncident(
            source.LineId, incidentKind, reason, actorReferenceId);
        if (!result.Succeeded)
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                result.Message, order.OrderId, source.LineId);
        NotifyChanged();
        return BistroBuilderAdvancedOrderMutationResult.Success(
            "Incidencia registrada sin alterar artificialmente la producción.",
            order.OrderId, source.LineId);
    }

    /// <summary>
    /// Cancela la comanda completa solo si ninguna línea ha empezado a
    /// prepararse. Evita el antiguo ForceCancel sobre producto ya cocinado.
    /// </summary>
    public BistroBuilderAdvancedOrderMutationResult TryCancelWholeOrderSafely(
        string orderId,
        string reason,
        string actorReferenceId)
    {
        if (!canonicalOrderService.TryGetOrderSnapshot(
                orderId, out BistroBuilderCanonicalOrder order) || order == null)
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                "No existe la comanda indicada.", orderId, string.Empty);
        if (order.IsTerminal)
            return Fail(BistroBuilderAdvancedOrderRestriction.OrderTerminal,
                "La comanda ya está cerrada.", order.OrderId, string.Empty);
        RestaurantOrder legacy = FindLegacyOrder(order.OrderId);
        if (legacy == null)
            return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                "No existe la comanda operativa enlazada.", order.OrderId, string.Empty);

        var cancellable = new List<string>();
        for (int index = 0; index < order.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = order.Lines[index];
            if (line == null || line.IsTerminal) continue;
            if (!BistroBuilderAdvancedOrderMutationPolicy.CanCancel(line.State))
                return Fail(BistroBuilderAdvancedOrderRestriction.PreparationStarted,
                    "No puede cancelarse toda la comanda: al menos un plato ya se está preparando.",
                    order.OrderId, line.LineId);
            cancellable.Add(line.LineId);
        }
        if (cancellable.Count == 0)
            return Fail(BistroBuilderAdvancedOrderRestriction.LineTerminal,
                "No quedan líneas cancelables.", order.OrderId, string.Empty);

        for (int index = 0; index < cancellable.Count; index++)
        {
            var result = canonicalOrderService.TryCancelAdvancedLine(
                cancellable[index],
                BistroBuilderAdvancedOrderIncidentKind.CustomerChange,
                reason,
                actorReferenceId);
            if (!result.Succeeded)
                return Fail(BistroBuilderAdvancedOrderRestriction.InvalidRequest,
                    result.Message, order.OrderId, cancellable[index]);
            if (!inventoryLifecycle.TryReleaseCanonicalLineReservation(
                    legacy, cancellable[index],
                    "Cancelación completa segura del bloque 11.", out string releaseError))
                return Fail(BistroBuilderAdvancedOrderRestriction.InventoryUnavailable,
                    releaseError, order.OrderId, cancellable[index]);
        }
        NotifyChanged();
        return BistroBuilderAdvancedOrderMutationResult.Success(
            "Comanda completa cancelada antes de comenzar producción.",
            order.OrderId, string.Empty);
    }

    private bool TryReserveNewLine(
        RestaurantOrder legacyOrder,
        BistroBuilderCanonicalOrder order,
        string newLineId,
        string dishId,
        out BistroBuilderResolvedOrderDish resolvedDish,
        out string error)
    {
        resolvedDish = default(BistroBuilderResolvedOrderDish);
        if (!canonicalOrderService.TryResolveOrderableDishForExistingOrder(
                order.OrderId, dishId, out resolvedDish, out error))
            return false;
        return inventoryLifecycle.TryReserveAdditionalCanonicalLine(
            legacyOrder, newLineId, resolvedDish.DishId, out error);
    }

    private bool TryResolve(
        string orderId,
        string lineId,
        out BistroBuilderCanonicalOrder order,
        out BistroBuilderCanonicalOrderLine line,
        out RestaurantOrder legacyOrder,
        out string error)
    {
        order = null;
        line = null;
        legacyOrder = null;
        if (!ValidateConfiguration(out error)) return false;
        if (!canonicalOrderService.TryGetOrderAndLineSnapshot(
                orderId, lineId, out order, out line) || order == null || line == null)
        {
            error = "No se encontró la comanda/línea solicitada.";
            return false;
        }
        legacyOrder = FindLegacyOrder(order.OrderId);
        if (legacyOrder == null)
        {
            error = "La línea no pertenece a una comanda operativa activa.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private RestaurantOrder FindLegacyOrder(string canonicalOrderId)
    {
        if (orderSystem == null) return null;
        IReadOnlyList<RestaurantOrder> active = orderSystem.ActiveOrders;
        for (int index = 0; index < active.Count; index++)
        {
            RestaurantOrder candidate = active[index];
            if (candidate != null &&
                string.Equals(candidate.CanonicalOrderId, canonicalOrderId,
                    StringComparison.Ordinal))
                return candidate;
        }
        return null;
    }

    private void RollbackNewReservation(RestaurantOrder order, string lineId)
    {
        if (inventoryLifecycle != null && order != null)
            inventoryLifecycle.TryReleaseCanonicalLineReservation(
                order, lineId, "Rollback de revisión 11.", out _);
    }

    private void RollbackAddedLine(
        RestaurantOrder order, string lineId, string actorReferenceId)
    {
        canonicalOrderService.TryCancelAdvancedLine(
            lineId,
            BistroBuilderAdvancedOrderIncidentKind.ServiceError,
            "Rollback de una revisión que no pudo completarse.",
            actorReferenceId);
        RollbackNewReservation(order, lineId);
    }

    private static BistroBuilderAdvancedOrderRestriction RestrictionFor(
        BistroBuilderCanonicalOrderLineState state)
    {
        if (state == BistroBuilderCanonicalOrderLineState.Consumed)
            return BistroBuilderAdvancedOrderRestriction.AlreadyConsumed;
        if (state == BistroBuilderCanonicalOrderLineState.Served)
            return BistroBuilderAdvancedOrderRestriction.PreparationStarted;
        if (state == BistroBuilderCanonicalOrderLineState.Cancelled ||
            state == BistroBuilderCanonicalOrderLineState.Failed)
            return BistroBuilderAdvancedOrderRestriction.LineTerminal;
        if (!BistroBuilderAdvancedOrderMutationPolicy.CanCorrect(state))
            return BistroBuilderAdvancedOrderRestriction.PreparationStarted;
        return BistroBuilderAdvancedOrderRestriction.InvalidRequest;
    }

    private static BistroBuilderAdvancedOrderMutationResult Fail(
        BistroBuilderAdvancedOrderRestriction restriction,
        string message,
        string orderId,
        string lineId) =>
        BistroBuilderAdvancedOrderMutationResult.Failure(
            restriction, message, orderId, lineId);

    private void NotifyChanged() => AdvancedOrdersChanged?.Invoke();

    private void CacheDependencies()
    {
        if (canonicalOrderService == null) TryGetComponent(out canonicalOrderService);
        if (orderSystem == null) TryGetComponent(out orderSystem);
        if (inventoryLifecycle == null) TryGetComponent(out inventoryLifecycle);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
