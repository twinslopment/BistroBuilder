using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime de 2.3H para la representación física del reparto.
/// Consume DispatchTicket de 2.3G y mantiene PurchaseOrder en InDelivery.
/// El alta física de stock sigue siendo exclusiva de 2.2B.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(70)]
public sealed class BistroBuilderSupplierDeliveryPresentationService : MonoBehaviour
{
    public const string SupplierAuthoringResourcePath = BistroBuilderSupplierLogisticsService.SupplierAuthoringResourcePath;
    public const string SettingsResourcePath = "BistroBuilder/Suppliers/BistroBuilderSupplierDeliveryPresentationSettings";

    private static BistroBuilderSupplierDeliveryPresentationService instance;
    private readonly Dictionary<string, BistroBuilderSupplierDeliveryPresentationRecord> recordByOrderId =
        new Dictionary<string, BistroBuilderSupplierDeliveryPresentationRecord>(StringComparer.Ordinal);

    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private BistroBuilderSupplierDeliveryPresentationSettings settings;
    private BistroBuilderSupplierPurchaseOrderService orderService;
    private BistroBuilderSupplierLogisticsService logisticsService;
    private BistroBuilderSupplierDeliverySceneAnchors anchors;
    private BistroBuilderSupplierDeliveryPresentationSnapshot state;
    private BistroBuilderSupplierDeliveryPresentationController activeController;
    private string lastInitializationError;

    public static BistroBuilderSupplierDeliveryPresentationService Instance => instance;
    public bool IsInitialized => state != null && string.IsNullOrEmpty(lastInitializationError);
    public string LastInitializationError => lastInitializationError;
    public int CurrentGameDay => orderService != null ? orderService.CurrentGameDay : (state != null ? state.currentGameDay : 0);
    public int PresentationCount => state != null && state.presentations != null ? state.presentations.Count : 0;
    public bool HasActivePresentation => activeController != null && activeController.IsInitialized && !activeController.IsCompleted;
    public BistroBuilderSupplierDeliveryPresentationController ActiveController => activeController;
    public BistroBuilderSupplierDeliverySceneAnchors SceneAnchors => anchors;

    public event Action<BistroBuilderSupplierDeliveryPresentationRecord> PresentationStarted;
    public event Action<BistroBuilderSupplierDeliveryPresentationRecord> PresentationChanged;
    public event Action<BistroBuilderSupplierReceivingHandoff> ReceivingHandoffReady;
    public event Action<BistroBuilderSupplierDeliveryPresentationRecord> PresentationCompleted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeAuthority()
    {
        if (UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierDeliveryPresentationService>() != null) return;
        GameObject host = new GameObject("BistroBuilderSupplierDeliveryPresentationService");
        DontDestroyOnLoad(host);
        host.AddComponent<BistroBuilderSupplierDeliveryPresentationService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        TryInitializeFresh();
    }

    private void OnDestroy()
    {
        if (activeController != null) activeController.DisposeVisuals();
        if (instance == this) instance = null;
    }

    private void Update()
    {
        BindDependencies();
        ResolveAnchors();
        if (orderService == null || logisticsService == null ||
            !orderService.IsInitialized || !logisticsService.IsInitialized) return;
        if (!IsInitialized || state.sourceLogisticsSeed != logisticsService.LogisticsSeed)
        {
            TryInitializeFresh();
            return;
        }

        state.currentGameDay = Math.Max(1, orderService.CurrentGameDay);
        ReconcileTerminalOrders();

        if (activeController == null && settings != null && settings.AutoStartReadyDeliveries && anchors != null && anchors.IsComplete)
        {
            string ignored;
            BistroBuilderSupplierDeliveryPresentationRecord resumed;
            if (!TryResumeQueuedDelivery(out resumed, out ignored))
                TryStartNextReadyDelivery(out _, out ignored);
        }
    }

    public bool TryInitializeFresh()
    {
        lastInitializationError = null;
        supplierDatabase = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(SupplierAuthoringResourcePath);
        settings = Resources.Load<BistroBuilderSupplierDeliveryPresentationSettings>(SettingsResourcePath);
        BindDependencies();
        ResolveAnchors();

        if (supplierDatabase == null)
        {
            lastInitializationError = "Falta supplier.authoring en Resources.";
            return false;
        }
        if (settings == null)
        {
            lastInitializationError = "Falta supplier.delivery.presentation.settings. Ejecuta el instalador 2.3H.";
            return false;
        }
        if (orderService == null || !orderService.IsInitialized)
        {
            lastInitializationError = "2.3H espera a PurchaseOrder 2.3E.";
            return false;
        }
        if (logisticsService == null || !logisticsService.IsInitialized)
        {
            lastInitializationError = "2.3H espera a planificación logística 2.3G.";
            return false;
        }

        if (activeController != null)
        {
            activeController.DisposeVisuals();
            Destroy(activeController.gameObject);
            activeController = null;
        }

        state = new BistroBuilderSupplierDeliveryPresentationSnapshot
        {
            currentGameDay = Math.Max(1, orderService.CurrentGameDay),
            sourceLogisticsSeed = logisticsService.LogisticsSeed,
            presentationRevision = 1,
            nextPresentationSequence = 1
        };
        RebuildIndexes();
        return true;
    }

    public void SetSceneAnchors(BistroBuilderSupplierDeliverySceneAnchors explicitAnchors)
    {
        anchors = explicitAnchors;
    }

    private bool TryResumeQueuedDelivery(out BistroBuilderSupplierDeliveryPresentationRecord resumed, out string error)
    {
        resumed = null;
        error = null;
        if (!IsInitialized || state == null || state.presentations == null) return false;
        for (int i = 0; i < state.presentations.Count; i++)
        {
            BistroBuilderSupplierDeliveryPresentationRecord record = state.presentations[i];
            if (record == null || record.state != BistroBuilderSupplierDeliveryPresentationState.Queued) continue;
            BistroBuilderPurchaseOrderRecord order;
            BistroBuilderSupplierLogisticsPlanRecord plan;
            if (!orderService.TryGetOrder(record.purchaseOrderId, out order) || order == null ||
                !logisticsService.TryGetPlanByOrder(record.purchaseOrderId, out plan) || plan == null) continue;
            if (order.status != BistroBuilderPurchaseOrderStatus.InDelivery || plan.status != BistroBuilderSupplierLogisticsPlanStatus.Dispatched) continue;
            return TryStartDelivery(record.purchaseOrderId, out resumed, out error);
        }
        return false;
    }

    public bool TryStartNextReadyDelivery(out BistroBuilderSupplierDeliveryPresentationRecord started, out string error)
    {
        started = null;
        error = null;
        if (!EnsureInitialized(out error)) return false;
        if (activeController != null)
        {
            error = "Ya existe una entrega física 2.3H activa.";
            return false;
        }
        if (!EnsureAnchors(out error)) return false;

        List<BistroBuilderSupplierLogisticsPlanRecord> plans = new List<BistroBuilderSupplierLogisticsPlanRecord>();
        logisticsService.CopyPlans(plans);
        plans.Sort((a, b) =>
        {
            if (a == null) return 1;
            if (b == null) return -1;
            int dayCompare = a.plannedDeliveryGameDay.CompareTo(b.plannedDeliveryGameDay);
            if (dayCompare != 0) return dayCompare;
            return string.CompareOrdinal(a.purchaseOrderId, b.purchaseOrderId);
        });
        for (int i = 0; i < plans.Count; i++)
        {
            BistroBuilderSupplierLogisticsPlanRecord plan = plans[i];
            if (plan == null || plan.status != BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch) continue;
            BistroBuilderSupplierDeliveryPresentationRecord existing;
            if (recordByOrderId.TryGetValue(plan.purchaseOrderId, out existing) && existing != null && !existing.IsTerminal) continue;
            return TryStartDelivery(plan.purchaseOrderId, out started, out error);
        }
        error = "No hay entregas ReadyForDispatch disponibles para 2.3H.";
        return false;
    }

    public bool TryStartDelivery(string purchaseOrderId, out BistroBuilderSupplierDeliveryPresentationRecord started, out string error)
    {
        started = null;
        error = null;
        if (!EnsureInitialized(out error)) return false;
        if (activeController != null)
        {
            error = "Ya existe una entrega física 2.3H activa.";
            return false;
        }
        if (!EnsureAnchors(out error)) return false;
        if (string.IsNullOrWhiteSpace(purchaseOrderId))
        {
            error = "PurchaseOrderId vacío.";
            return false;
        }

        BistroBuilderPurchaseOrderRecord order;
        if (!orderService.TryGetOrder(purchaseOrderId, out order) || order == null)
        {
            error = "No existe el PurchaseOrder solicitado.";
            return false;
        }
        BistroBuilderSupplierLogisticsPlanRecord plan;
        if (!logisticsService.TryGetPlanByOrder(purchaseOrderId, out plan) || plan == null)
        {
            error = "El PurchaseOrder no tiene LogisticsPlan 2.3G.";
            return false;
        }

        // El branding se valida ANTES de mutar PendingDelivery -> InDelivery.
        // Así un proveedor sin identidad visual nunca deja un pedido despachado
        // sin poder construir su presentación física.
        BistroBuilderSupplierAuthoringRecord supplier;
        if (!supplierDatabase.TryGetSupplier(order.supplierId, out supplier) || supplier == null || !supplier.isActive)
        {
            error = "No se puede resolver supplier.authoring para el branding del vehículo.";
            return false;
        }
        BistroBuilderSupplierDeliveryBrandingData branding = BistroBuilderSupplierDeliveryVisualFactory.ResolveBranding(supplier);
        if (branding == null || !branding.HasReadableIdentity)
        {
            error = "El proveedor no tiene nombre válido para el branding obligatorio del vehículo.";
            return false;
        }

        BistroBuilderSupplierDeliveryPresentationRecord existingRecord;
        bool resumingExisting = recordByOrderId.TryGetValue(purchaseOrderId, out existingRecord) && existingRecord != null && !existingRecord.IsTerminal;
        if (resumingExisting && existingRecord.state != BistroBuilderSupplierDeliveryPresentationState.Queued)
        {
            error = "El PurchaseOrder ya tiene una presentación 2.3H no terminal en curso.";
            return false;
        }

        BistroBuilderSupplierDispatchTicket ticket;
        if (plan.status == BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch)
        {
            if (!logisticsService.TryDispatch(purchaseOrderId, out ticket, out error)) return false;
            if (!orderService.TryGetOrder(purchaseOrderId, out order) || order == null)
            {
                error = "2.3G despachó el pedido pero 2.3H no puede releer PurchaseOrder.";
                return false;
            }
            logisticsService.TryGetPlanByOrder(purchaseOrderId, out plan);
        }
        else if (plan.status == BistroBuilderSupplierLogisticsPlanStatus.Dispatched && order.status == BistroBuilderPurchaseOrderStatus.InDelivery)
        {
            ticket = BuildTicketFromPlan(plan);
        }
        else
        {
            error = "La entrega debe estar ReadyForDispatch o Dispatched/InDelivery para iniciar 2.3H.";
            return false;
        }

        BistroBuilderSupplierDeliveryPresentationRecord record = resumingExisting
            ? existingRecord.DeepClone()
            : new BistroBuilderSupplierDeliveryPresentationRecord
            {
                presentationId = "delivery_presentation_" + state.nextPresentationSequence.ToString("D8"),
                logisticsPlanId = ticket.logisticsPlanId,
                purchaseOrderId = ticket.purchaseOrderId,
                orderDisplayCode = ticket.orderDisplayCode,
                supplierId = ticket.supplierId,
                state = BistroBuilderSupplierDeliveryPresentationState.Queued,
                currentTrip = 1,
                totalTrips = Mathf.Clamp(ticket.suggestedTripCount, 1, 3),
                startedGameDay = Math.Max(1, orderService.CurrentGameDay),
                vehicle = ticket.vehicle,
                visualLoadUnits = Math.Max(1, ticket.visualLoadUnits),
                logisticsLoadUnits = Math.Max(1, ticket.logisticsLoadUnits),
                appliedDelayGameMinutes = Math.Max(0, ticket.appliedDelayGameMinutes),
                vehiclePresentationProfileId = ticket.vehiclePresentationProfileId,
                driverPresentationProfileId = ticket.driverPresentationProfileId
            };

        GameObject controllerHost = new GameObject("BB_DeliveryPresentation_" + order.displayCode);
        controllerHost.transform.SetParent(transform, false);
        BistroBuilderSupplierDeliveryPresentationController controller = controllerHost.AddComponent<BistroBuilderSupplierDeliveryPresentationController>();
        if (!controller.Initialize(ticket, order, settings, anchors, branding, record,
                HandleControllerStateChanged, HandleReceivingHandoff, HandleControllerCompleted, out error))
        {
            Destroy(controllerHost);
            return false;
        }

        activeController = controller;
        BistroBuilderSupplierDeliveryPresentationRecord controllerRecord = controller.Record;
        if (controllerRecord != null) CopyRecord(controllerRecord, record);
        if (!resumingExisting)
        {
            state.presentations.Add(record);
            state.nextPresentationSequence++;
        }
        state.presentationRevision++;
        RebuildIndexes();
        started = controllerRecord != null ? controllerRecord.DeepClone() : record.DeepClone();
        PresentationStarted?.Invoke(started.DeepClone());
        return true;
    }

    public bool TryGetPresentationByOrder(string purchaseOrderId, out BistroBuilderSupplierDeliveryPresentationRecord record)
    {
        record = null;
        if (!IsInitialized || string.IsNullOrWhiteSpace(purchaseOrderId)) return false;
        BistroBuilderSupplierDeliveryPresentationRecord stored;
        if (!recordByOrderId.TryGetValue(purchaseOrderId, out stored) || stored == null) return false;
        record = stored.DeepClone();
        return true;
    }

    public int CopyPresentations(List<BistroBuilderSupplierDeliveryPresentationRecord> buffer)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        buffer.Clear();
        if (!IsInitialized || state.presentations == null) return 0;
        for (int i = 0; i < state.presentations.Count; i++)
            if (state.presentations[i] != null) buffer.Add(state.presentations[i].DeepClone());
        return buffer.Count;
    }

    public BistroBuilderSupplierDeliveryPresentationSnapshot CreateSnapshot()
    {
        if (state == null) return null;
        BistroBuilderSupplierDeliveryPresentationSnapshot clone = state.DeepClone();
        // Una presentación visual a mitad de animación se reanuda desde el principio
        // del flujo visual tras Load; PurchaseOrder permanece InDelivery.
        for (int i = 0; i < clone.presentations.Count; i++)
        {
            BistroBuilderSupplierDeliveryPresentationRecord record = clone.presentations[i];
            if (record == null || record.IsTerminal) continue;
            record.state = BistroBuilderSupplierDeliveryPresentationState.Queued;
            record.currentTrip = 1;
            record.receivingHandoffEmitted = false;
            record.handoffId = null;
        }
        return clone;
    }

    public bool TryRestoreSnapshot(BistroBuilderSupplierDeliveryPresentationSnapshot candidate, out string error)
    {
        error = null;
        BindDependencies();
        if (candidate == null) { error = "Snapshot 2.3H nulo."; return false; }
        if (orderService == null || !orderService.IsInitialized || logisticsService == null || !logisticsService.IsInitialized)
        { error = "Restaura primero 2.3C/2.3D/2.3E/2.3G."; return false; }
        if (!string.Equals(candidate.schemaId, BistroBuilderSupplierDeliveryPresentationSnapshot.CurrentSchemaId, StringComparison.Ordinal) ||
            candidate.schemaVersion != BistroBuilderSupplierDeliveryPresentationSnapshot.CurrentSchemaVersion)
        { error = "Schema de supplier.delivery.presentation.runtime incompatible."; return false; }
        if (candidate.sourceLogisticsSeed != 0UL && candidate.sourceLogisticsSeed != logisticsService.LogisticsSeed)
        { error = "El snapshot 2.3H pertenece a otra sesión logística."; return false; }
        if (candidate.currentGameDay != orderService.CurrentGameDay)
        { error = "2.3H debe restaurarse después de 2.3G en el mismo día."; return false; }

        if (activeController != null)
        {
            activeController.DisposeVisuals();
            Destroy(activeController.gameObject);
            activeController = null;
        }
        state = candidate.DeepClone();
        lastInitializationError = null;
        RebuildIndexes();
        return true;
    }

    private void HandleControllerStateChanged(
        BistroBuilderSupplierDeliveryPresentationController controller,
        BistroBuilderSupplierDeliveryPresentationRecord update)
    {
        if (update == null) return;
        BistroBuilderSupplierDeliveryPresentationRecord stored;
        if (!recordByOrderId.TryGetValue(update.purchaseOrderId, out stored) || stored == null) return;
        CopyRecord(update, stored);
        state.presentationRevision++;
        PresentationChanged?.Invoke(stored.DeepClone());
    }

    private void HandleReceivingHandoff(
        BistroBuilderSupplierDeliveryPresentationController controller,
        BistroBuilderSupplierReceivingHandoff handoff)
    {
        if (handoff == null) return;
        BistroBuilderSupplierDeliveryPresentationRecord stored;
        if (recordByOrderId.TryGetValue(handoff.purchaseOrderId, out stored) && stored != null)
        {
            stored.receivingHandoffEmitted = true;
            stored.handoffId = handoff.handoffId;
            stored.stateRevision++;
            state.presentationRevision++;
        }
        ReceivingHandoffReady?.Invoke(handoff.DeepClone());
    }

    private void HandleControllerCompleted(
        BistroBuilderSupplierDeliveryPresentationController controller,
        BistroBuilderSupplierDeliveryPresentationRecord update)
    {
        if (update == null) return;
        BistroBuilderSupplierDeliveryPresentationRecord stored;
        if (recordByOrderId.TryGetValue(update.purchaseOrderId, out stored) && stored != null)
        {
            CopyRecord(update, stored);
            stored.state = BistroBuilderSupplierDeliveryPresentationState.Completed;
            stored.completedGameDay = Math.Max(1, orderService.CurrentGameDay);
            stored.stateRevision++;
            state.presentationRevision++;
            PresentationCompleted?.Invoke(stored.DeepClone());
        }
        if (controller != null)
        {
            controller.DisposeVisuals();
            Destroy(controller.gameObject);
        }
        if (activeController == controller) activeController = null;
    }

    private void ReconcileTerminalOrders()
    {
        if (state == null || state.presentations == null) return;
        for (int i = 0; i < state.presentations.Count; i++)
        {
            BistroBuilderSupplierDeliveryPresentationRecord record = state.presentations[i];
            if (record == null || record.IsTerminal) continue;
            BistroBuilderPurchaseOrderRecord order;
            if (!orderService.TryGetOrder(record.purchaseOrderId, out order) || order == null) continue;
            if (order.status == BistroBuilderPurchaseOrderStatus.Cancelled)
            {
                record.state = BistroBuilderSupplierDeliveryPresentationState.Cancelled;
                record.stateRevision++;
                state.presentationRevision++;
            }
        }
    }

    private bool EnsureInitialized(out string error)
    {
        error = null;
        if (!IsInitialized && !TryInitializeFresh())
        {
            error = lastInitializationError ?? "2.3H no inicializado.";
            return false;
        }
        return true;
    }

    private bool EnsureAnchors(out string error)
    {
        error = null;
        ResolveAnchors();
        if (anchors == null || !anchors.IsComplete)
        {
            error = "Faltan anclajes de escena 2.3H. Ejecuta '2.3H - Crear/actualizar anclajes de escena' y colócalos en el acceso de suministros/almacén.";
            return false;
        }
        return true;
    }

    private void BindDependencies()
    {
        if (orderService == null) orderService = BistroBuilderSupplierPurchaseOrderService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        if (logisticsService == null) logisticsService = BistroBuilderSupplierLogisticsService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierLogisticsService>();
    }

    private void ResolveAnchors()
    {
        if (anchors != null) return;
        anchors = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierDeliverySceneAnchors>();
    }

    private BistroBuilderSupplierDispatchTicket BuildTicketFromPlan(BistroBuilderSupplierLogisticsPlanRecord plan)
    {
        return new BistroBuilderSupplierDispatchTicket
        {
            logisticsPlanId = plan.logisticsPlanId,
            purchaseOrderId = plan.purchaseOrderId,
            orderDisplayCode = plan.orderDisplayCode,
            supplierId = plan.supplierId,
            plannedDeliveryGameDay = plan.plannedDeliveryGameDay,
            windowStartMinuteOfDay = plan.windowStartMinuteOfDay,
            windowEndMinuteOfDay = plan.windowEndMinuteOfDay,
            appliedDelayGameMinutes = plan.delayApplied ? plan.decidedDelayGameMinutes : 0,
            logisticsLoadUnits = plan.logisticsLoadUnits,
            visualLoadUnits = plan.visualLoadUnits,
            suggestedTripCount = plan.suggestedTripCount,
            vehicle = plan.resolvedVehicle,
            vehiclePresentationProfileId = plan.vehiclePresentationProfileId,
            driverPresentationProfileId = plan.driverPresentationProfileId
        };
    }

    private void RebuildIndexes()
    {
        recordByOrderId.Clear();
        if (state == null || state.presentations == null) return;
        for (int i = 0; i < state.presentations.Count; i++)
        {
            BistroBuilderSupplierDeliveryPresentationRecord record = state.presentations[i];
            if (record == null || string.IsNullOrWhiteSpace(record.purchaseOrderId)) continue;
            recordByOrderId[record.purchaseOrderId] = record;
        }
    }

    private static void CopyRecord(BistroBuilderSupplierDeliveryPresentationRecord source, BistroBuilderSupplierDeliveryPresentationRecord target)
    {
        target.presentationId = source.presentationId;
        target.logisticsPlanId = source.logisticsPlanId;
        target.purchaseOrderId = source.purchaseOrderId;
        target.orderDisplayCode = source.orderDisplayCode;
        target.supplierId = source.supplierId;
        target.state = source.state;
        target.currentTrip = source.currentTrip;
        target.totalTrips = source.totalTrips;
        target.receivingHandoffEmitted = source.receivingHandoffEmitted;
        target.handoffId = source.handoffId;
        target.startedGameDay = source.startedGameDay;
        target.completedGameDay = source.completedGameDay;
        target.stateRevision = source.stateRevision;
        target.vehicle = source.vehicle;
        target.visualLoadUnits = source.visualLoadUnits;
        target.logisticsLoadUnits = source.logisticsLoadUnits;
        target.appliedDelayGameMinutes = source.appliedDelayGameMinutes;
        target.vehiclePresentationProfileId = source.vehiclePresentationProfileId;
        target.driverPresentationProfileId = source.driverPresentationProfileId;
    }
}
