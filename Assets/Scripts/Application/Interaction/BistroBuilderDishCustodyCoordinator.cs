using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Custodia lógica de cada línea que ya representa un plato físico.
/// El estado culinario/comercial sigue perteneciendo a OrderLineExecution.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderDishCustodyCoordinator : MonoBehaviour
{
    private const string DishResourcePrefix = "dish:";
    private const string PassPrefix = "kitchen-pass:";
    private const string OriginSessionPrefix = "dish-origin:";

    [SerializeField] private BistroBuilderInteractionService interactionService;
    [SerializeField] private BistroBuilderCanonicalOrderService canonicalOrderService;

    private void Awake()
    {
        ResolveDependencies();
    }
    private void OnEnable()
    {
        ResolveDependencies();
        if (canonicalOrderService != null)
        {
            canonicalOrderService.OrdersChanged -= HandleOrdersChanged;
            canonicalOrderService.OrdersChanged += HandleOrdersChanged;
        }
    }

    private void OnDisable()
    {
        if (canonicalOrderService != null)
            canonicalOrderService.OrdersChanged -= HandleOrdersChanged;
    }

    public void ConfigureForEditor(BistroBuilderInteractionService service)
    {
        interactionService = service;
    }

    public bool EnsureReadyDishCustody(
        string lineId,
        string kitchenId,
        out string error)
    {
        error = string.Empty;
        ResolveDependencies();
        string normalizedLine = BistroBuilderOrderIdUtility.Normalize(lineId);
        string normalizedKitchen = BistroBuilderStaffStableIdUtility.Normalize(kitchenId);
        if (interactionService == null || string.IsNullOrWhiteSpace(normalizedLine) ||
            string.IsNullOrWhiteSpace(normalizedKitchen))
        {
            error = "No puede crearse Custody para un plato sin Interaction, LineId o KitchenId.";
            return false;
        }

        if (TryGetDishCustody(normalizedLine, out var existing, out var grant))
        {
            if (grant.state == BistroBuilderInteractionGrantState.Recovery)
            {
                error = "El plato está pendiente de Custody Recovery.";
                return false;
            }
            return true;
        }

        string requestId = "dish-custody:create:" + normalizedLine;
        interactionService.SubmitAcquisition(new BistroBuilderInteractionAcquisitionRequest
        {
            requestId = requestId,
            grantKind = BistroBuilderInteractionGrantKind.Custody,
            holderKind = BistroBuilderInteractionHolderKind.Station,
            holderId = PassHolderId(normalizedKitchen),
            interactionId = "dish.custody",
            sessionId = OriginSessionPrefix + normalizedKitchen,
            countsCapacity = true,
            capacityUnits = 1,
            candidates = new System.Collections.Generic.List<BistroBuilderInteractionCandidate>
            {
                new BistroBuilderInteractionCandidate
                {
                    resourceId = DishResourceId(normalizedLine)
                }
            }
        });
        interactionService.ResolveArbitrationEpoch();

        if (!interactionService.TryGetDecision(requestId, out var decision) ||
            decision.outcome != BistroBuilderInteractionRequestOutcome.Granted ||
            !decision.handle.IsValid)
        {
            error = "Interaction no pudo establecer Custody del plato: " +
                    (decision != null ? decision.reason.ToString() : "sin decisión") + ".";
            return false;
        }
        return true;
    }

    public bool TryPickupByWaiter(
        string lineId,
        Waiter waiter,
        out string error)
    {
        error = string.Empty;
        if (waiter == null)
        {
            error = "No puede transferirse Custody a un camarero nulo.";
            return false;
        }

        if (!TryGetDishCustody(lineId, out var handle, out var grant))
        {
            error = "El plato no tiene Custody activa.";
            return false;
        }
        if (grant.holderKind != BistroBuilderInteractionHolderKind.Station ||
            !grant.holderId.StartsWith(PassPrefix, StringComparison.Ordinal))
        {
            error = "El plato ya no está bajo Custody del pass.";
            return false;
        }

        if (!interactionService.TryTransferCustody(
                handle,
                BistroBuilderInteractionHolderKind.Actor,
                WaiterHolderId(waiter),
                out _))
        {
            error = "No pudo transferirse Custody del plato al camarero.";
            return false;
        }
        return true;
    }

    public bool TryReturnToOriginPass(
        string lineId,
        out string error)
    {
        error = string.Empty;
        if (!TryGetDishCustody(lineId, out var handle, out var grant))
        {
            error = "El plato no tiene Custody activa.";
            return false;
        }

        string kitchenId = OriginKitchenId(grant.sessionId);
        if (string.IsNullOrWhiteSpace(kitchenId))
        {
            error = "Custody del plato no conserva su pass de origen.";
            return false;
        }

        if (grant.holderKind == BistroBuilderInteractionHolderKind.Station &&
            string.Equals(grant.holderId, PassHolderId(kitchenId), StringComparison.Ordinal))
            return true;

        if (!interactionService.TryTransferCustody(
                handle,
                BistroBuilderInteractionHolderKind.Station,
                PassHolderId(kitchenId),
                out _))
        {
            error = "No pudo devolverse Custody del plato al pass.";
            return false;
        }
        return true;
    }

    public bool TryDeliverToDestination(
        string lineId,
        RestaurantOrder order,
        Waiter waiter,
        out string error)
    {
        error = string.Empty;
        if (order == null || waiter == null || !order.HasValidDestination)
        {
            error = "No puede entregarse Custody sin destino y camarero válidos.";
            return false;
        }

        if (!TryGetDishCustody(lineId, out var handle, out var grant))
        {
            error = "El plato no tiene Custody activa.";
            return false;
        }
        string waiterId = WaiterHolderId(waiter);
        if (grant.holderKind != BistroBuilderInteractionHolderKind.Actor ||
            !string.Equals(grant.holderId, waiterId, StringComparison.Ordinal))
        {
            error = "El camarero no posee Custody vigente sobre el plato.";
            return false;
        }

        if (!interactionService.TryTransferCustody(
                handle,
                BistroBuilderInteractionHolderKind.Station,
                DestinationHolderId(order),
                out var deliveredHandle))
        {
            error = "No pudo transferirse Custody al destino de servicio.";
            return false;
        }

        // El token físico de reparto termina al quedar servido. El estado de
        // consumo posterior pertenece al gameplay de mesa/barra.
        if (!interactionService.CompleteGrant(deliveredHandle))
        {
            error = "La entrega ocurrió, pero no pudo cerrarse Custody.";
            return false;
        }
        return true;
    }

    public bool TryStageDeliveryToDestination(
        string lineId,
        RestaurantOrder order,
        Waiter waiter,
        out string error)
    {
        error = string.Empty;
        if (order == null || waiter == null || !order.HasValidDestination)
        {
            error = "No puede transferirse Custody sin destino y camarero válidos.";
            return false;
        }
        if (!TryGetDishCustody(lineId, out var handle, out var grant))
        {
            error = "El plato no tiene Custody activa.";
            return false;
        }
        if (grant.holderKind != BistroBuilderInteractionHolderKind.Actor ||
            !string.Equals(grant.holderId, WaiterHolderId(waiter), StringComparison.Ordinal))
        {
            error = "El camarero no posee Custody vigente sobre el plato.";
            return false;
        }
        return TryTransfer(handle, BistroBuilderInteractionHolderKind.Station,
            DestinationHolderId(order), out error);
    }

    public bool TryFinalizeDeliveredDish(string lineId, out string error)
    {
        error = string.Empty;
        if (!TryGetDishCustody(lineId, out var handle, out var grant))
        {
            error = "El plato no tiene Custody activa para finalizar.";
            return false;
        }
        if (grant.holderKind != BistroBuilderInteractionHolderKind.Station ||
            !grant.holderId.StartsWith("service-destination:", StringComparison.Ordinal))
        {
            error = "Custody no está en un destino de servicio.";
            return false;
        }
        if (!interactionService.CompleteGrant(handle))
        {
            error = "No pudo finalizarse Custody del plato servido.";
            return false;
        }
        return true;
    }

    public bool TryRestoreHolder(
        string lineId,
        BistroBuilderInteractionHolderKind holderKind,
        string holderId,
        out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(holderId) ||
            !TryGetDishCustody(lineId, out var handle, out _))
        {
            error = "No puede restaurarse Custody: holder o plato inválido.";
            return false;
        }
        return TryTransfer(handle, holderKind, holderId, out error);
    }

    public bool TryGetCurrentHolder(
        string lineId,
        out BistroBuilderInteractionHolderKind holderKind,
        out string holderId,
        out string error)
    {
        holderKind = BistroBuilderInteractionHolderKind.Station;
        holderId = string.Empty;
        error = string.Empty;
        if (!TryGetDishCustody(lineId, out _, out var grant))
        {
            error = "El plato no tiene Custody activa.";
            return false;
        }
        holderKind = grant.holderKind;
        holderId = grant.holderId;
        return true;
    }

    /// <summary>
    /// Reconcilia Custody con el checkpoint canónico de comandas después de Load.
    /// service.runtime puede normalizar un reparto transitorio a ReadyForPickup;
    /// en ese caso el plato vuelve de forma determinista a su pass de origen.
    /// </summary>
    public bool TryReconcileAfterLoad(out string error)
    {
        error = string.Empty;
        ResolveDependencies();
        if (interactionService == null || canonicalOrderService == null)
        {
            error = "Faltan autoridades para reconciliar Custody tras Load.";
            return false;
        }

        var orders = new List<BistroBuilderCanonicalOrder>();
        canonicalOrderService.CopyOrderSnapshotsTo(orders);
        var states = new Dictionary<string, BistroBuilderCanonicalOrderLineState>(
            StringComparer.Ordinal);
        for (int i = 0; i < orders.Count; i++)
        {
            BistroBuilderCanonicalOrder order = orders[i];
            if (order == null) continue;
            for (int j = 0; j < order.Lines.Count; j++)
            {
                BistroBuilderCanonicalOrderLine line = order.Lines[j];
                if (line != null) states[line.LineId] = line.State;
            }
        }

        IReadOnlyList<BistroBuilderInteractionGrantRecord> active =
            interactionService.GetActiveGrantSnapshot();
        for (int i = 0; i < active.Count; i++)
        {
            BistroBuilderInteractionGrantRecord grant = active[i];
            if (!IsDishCustody(grant)) continue;
            string lineId = LineIdFromResource(grant.resourceId);
            if (string.IsNullOrWhiteSpace(lineId) ||
                !states.TryGetValue(lineId, out BistroBuilderCanonicalOrderLineState state))
            {
                interactionService.InvalidateGrant(
                    grant.Handle, BistroBuilderInteractionReasonCode.LoadReconciled);
                continue;
            }

            if (!ReconcileDishForState(lineId, grant, state, out error))
                return false;
        }

        return interactionService.ValidateRuntimeInvariants(out error);
    }

    private bool ReconcileDishForState(
        string lineId,
        BistroBuilderInteractionGrantRecord grant,
        BistroBuilderCanonicalOrderLineState state,
        out string error)
    {
        error = string.Empty;
        switch (state)
        {
            case BistroBuilderCanonicalOrderLineState.ReadyForPickup:
            case BistroBuilderCanonicalOrderLineState.AssignedForDelivery:
                if (TryReturnToOriginPass(lineId, out error)) return true;
                error = "Load no pudo devolver " + lineId + " a su pass: " + error;
                return false;

            case BistroBuilderCanonicalOrderLineState.InTransit:
                if (grant.holderKind == BistroBuilderInteractionHolderKind.Actor ||
                    grant.holderKind == BistroBuilderInteractionHolderKind.Container)
                    return true;
                error = "La línea " + lineId +
                        " está InTransit sin Custody de actor/contenedor.";
                return false;

            case BistroBuilderCanonicalOrderLineState.Served:
            case BistroBuilderCanonicalOrderLineState.Consumed:
                if (interactionService.CompleteGrant(grant.Handle)) return true;
                error = "No pudo cerrarse Custody ya servida para " + lineId + ".";
                return false;

            case BistroBuilderCanonicalOrderLineState.Cancelled:
            case BistroBuilderCanonicalOrderLineState.Failed:
                interactionService.InvalidateGrant(
                    grant.Handle, BistroBuilderInteractionReasonCode.LoadReconciled);
                return true;

            default:
                // Antes de ReadyForPickup todavía no existe plato físico.
                interactionService.InvalidateGrant(
                    grant.Handle, BistroBuilderInteractionReasonCode.LoadReconciled);
                return true;
        }
    }

    private static bool IsDishCustody(BistroBuilderInteractionGrantRecord grant)
    {
        return grant != null &&
               grant.kind == BistroBuilderInteractionGrantKind.Custody &&
               string.Equals(grant.interactionId, "dish.custody", StringComparison.Ordinal) &&
               grant.resourceId != null &&
               grant.resourceId.StartsWith(DishResourcePrefix, StringComparison.Ordinal);
    }

    private static string LineIdFromResource(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId) ||
            !resourceId.StartsWith(DishResourcePrefix, StringComparison.Ordinal))
            return string.Empty;
        return BistroBuilderOrderIdUtility.Normalize(
            resourceId.Substring(DishResourcePrefix.Length));
    }

    private bool TryTransfer(
        BistroBuilderInteractionGrantHandle handle,
        BistroBuilderInteractionHolderKind holderKind,
        string holderId,
        out string error)
    {
        error = string.Empty;
        if (!interactionService.TryTransferCustody(
                handle, holderKind, holderId, out _))
        {
            error = "Interaction rechazó la transferencia de Custody.";
            return false;
        }
        return true;
    }

    private bool TryGetDishCustody(
        string lineId,
        out BistroBuilderInteractionGrantHandle handle,
        out BistroBuilderInteractionGrantRecord grant)
    {
        handle = default;
        grant = null;
        ResolveDependencies();
        string normalizedLine = BistroBuilderOrderIdUtility.Normalize(lineId);
        if (interactionService == null || string.IsNullOrWhiteSpace(normalizedLine))
            return false;

        var grants = interactionService.GetGrantsForResource(DishResourceId(normalizedLine));
        for (int i = 0; i < grants.Count; i++)
        {
            BistroBuilderInteractionGrantRecord candidate = grants[i];
            if (candidate == null ||
                candidate.kind != BistroBuilderInteractionGrantKind.Custody)
                continue;
            if (!interactionService.TryGetGrant(candidate.Handle, out var live))
                continue;
            handle = live.Handle;
            grant = live;
            return true;
        }
        return false;
    }

    private void HandleOrdersChanged(BistroBuilderCanonicalOrderChangedEvent change)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;
        ResolveDependencies();
        if (interactionService == null || canonicalOrderService == null)
            return;

        if (change.ChangeType == BistroBuilderCanonicalOrderChangeType.AllOrdersCleared)
        {
            ReleaseAllDishCustodies(BistroBuilderInteractionReasonCode.SessionEnded);
            return;
        }

        if (change.ChangeType == BistroBuilderCanonicalOrderChangeType.OrderCancelled &&
            canonicalOrderService.TryGetOrderSnapshot(change.OrderId, out var cancelledOrder) &&
            cancelledOrder != null)
        {
            for (int i = 0; i < cancelledOrder.Lines.Count; i++)
                TryReleaseDishCustody(
                    cancelledOrder.Lines[i].LineId,
                    BistroBuilderInteractionReasonCode.TaskCancelled);
            return;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(change.LineId) ||
            !canonicalOrderService.TryGetOrderAndLineSnapshot(
                change.OrderId, change.LineId, out _, out var line) || line == null)
            return;

        if (line.State == BistroBuilderCanonicalOrderLineState.Cancelled ||
            line.State == BistroBuilderCanonicalOrderLineState.Failed)
            TryReleaseDishCustody(
                line.LineId,
                BistroBuilderInteractionReasonCode.TaskCancelled);
    }

    private void ReleaseAllDishCustodies(BistroBuilderInteractionReasonCode reason)
    {
        if (interactionService == null) return;
        var active = interactionService.GetActiveGrantSnapshot();
        for (int i = 0; i < active.Count; i++)
        {
            BistroBuilderInteractionGrantRecord grant = active[i];
            if (grant == null || grant.kind != BistroBuilderInteractionGrantKind.Custody ||
                !string.Equals(grant.interactionId, "dish.custody", StringComparison.Ordinal))
                continue;
            interactionService.InvalidateGrant(grant.Handle, reason);
        }
    }
    private void ResolveDependencies()
    {
        if (interactionService == null)
            interactionService = FindFirstObjectByType<BistroBuilderInteractionService>();
        if (canonicalOrderService == null)
            canonicalOrderService = FindFirstObjectByType<BistroBuilderCanonicalOrderService>();
    }

    private static string DishResourceId(string lineId)
    {
        return DishResourcePrefix + BistroBuilderOrderIdUtility.Normalize(lineId);
    }

    private static string PassHolderId(string kitchenId)
    {
        return PassPrefix + BistroBuilderStaffStableIdUtility.Normalize(kitchenId);
    }

    private static string WaiterHolderId(Waiter waiter)
    {
        return waiter != null ? "waiter:" + waiter.WaiterId : string.Empty;
    }

    private static string DestinationHolderId(RestaurantOrder order)
    {
        return order != null
            ? "service-destination:" + order.ServiceDestinationReferenceId
            : string.Empty;
    }

    private static string OriginKitchenId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) ||
            !sessionId.StartsWith(OriginSessionPrefix, StringComparison.Ordinal))
            return string.Empty;
        return sessionId.Substring(OriginSessionPrefix.Length);
    }
    public bool TryReleaseDishCustody(
        string lineId,
        BistroBuilderInteractionReasonCode reason = BistroBuilderInteractionReasonCode.TaskCancelled)
    {
        if (!TryGetDishCustody(lineId, out var handle, out _))
            return true;
        return interactionService.InvalidateGrant(handle, reason);
    }
}
