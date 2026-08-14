using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime de 2.3G para planificación logística, fiabilidad y retrasos.
///
/// Se apoya en PurchaseOrder 2.3E. No recibe mercancía, no escribe Inventario y
/// no representa físicamente vehículo/repartidor: 2.3H consumirá DispatchTicket.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public sealed class BistroBuilderSupplierLogisticsService : MonoBehaviour
{
    public const string SupplierAuthoringResourcePath = BistroBuilderSupplierPurchaseOrderService.SupplierAuthoringResourcePath;
    public const string SettingsResourcePath = "BistroBuilder/Suppliers/BistroBuilderSupplierLogisticsPlanningSettings";

    private static BistroBuilderSupplierLogisticsService instance;
    private readonly Dictionary<string, BistroBuilderSupplierLogisticsPlanRecord> planById =
        new Dictionary<string, BistroBuilderSupplierLogisticsPlanRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderSupplierLogisticsPlanRecord> planByOrderId =
        new Dictionary<string, BistroBuilderSupplierLogisticsPlanRecord>(StringComparer.Ordinal);

    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private BistroBuilderSupplierLogisticsPlanningSettings settings;
    private BistroBuilderSupplierPurchaseOrderService orderService;
    private BistroBuilderSupplierLogisticsSnapshot state;
    private string lastInitializationError;
    private bool subscribed;

    public static BistroBuilderSupplierLogisticsService Instance => instance;
    public bool IsInitialized => state != null && string.IsNullOrEmpty(lastInitializationError);
    public string LastInitializationError => lastInitializationError;
    public int CurrentGameDay => state != null ? state.currentGameDay : 0;
    public long LogisticsRevision => state != null ? state.logisticsRevision : 0L;
    public ulong LogisticsSeed => state != null ? state.logisticsSeed : 0UL;
    public ulong SourceMarketSeed => state != null ? state.sourceMarketSeed : 0UL;
    public ulong SourceCommercialSeed => state != null ? state.sourceCommercialSeed : 0UL;
    public int PlanCount => state != null && state.plans != null ? state.plans.Count : 0;

    public event Action<BistroBuilderSupplierLogisticsPlanRecord> PlanCreated;
    public event Action<BistroBuilderSupplierLogisticsPlanRecord> PlanChanged;
    public event Action<BistroBuilderSupplierDispatchTicket> DispatchStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeAuthority()
    {
        if (UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierLogisticsService>() != null) return;
        GameObject host = new GameObject("BistroBuilderSupplierLogisticsService");
        DontDestroyOnLoad(host);
        host.AddComponent<BistroBuilderSupplierLogisticsService>();
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
        Unsubscribe();
        if (instance == this) instance = null;
    }

    private void Update()
    {
        BindDependencies();
        if (orderService == null || !orderService.IsInitialized) return;
        if (!IsInitialized)
        {
            TryInitializeFresh();
            return;
        }

        // 2.3G1 / JKL-C: una snapshot puede haberse creado cuando 2.3E todavía
        // no tenía las seeds de mercado/comercial enlazadas (valor 0 = unbound).
        // Eso es un estado válido en los contratos de persistencia. No debe
        // interpretarse como cambio de sesión y, sobre todo, no debe provocar
        // TryInitializeFresh(), porque borraría LogisticsPlans restaurados.
        // Solo una contradicción entre DOS seeds no nulas obliga a reiniciar.
        string bindingError;
        if (!TrySynchronizeSessionBinding(out bindingError))
        {
            TryInitializeFresh();
            return;
        }

        int day = Math.Max(1, orderService.CurrentGameDay);
        if (day > state.currentGameDay)
        {
            string error;
            TryAdvanceToGameDay(day, out error);
        }
    }

    public bool TryInitializeFresh()
    {
        lastInitializationError = null;
        LoadStaticDependencies();
        BindDependencies();
        if (supplierDatabase == null)
        {
            lastInitializationError = "Falta supplier.authoring en Resources.";
            return false;
        }
        if (settings == null)
        {
            lastInitializationError = "Falta supplier.logistics.settings. Ejecuta el instalador 2.3G.";
            return false;
        }
        if (orderService == null || !orderService.IsInitialized)
        {
            lastInitializationError = "2.3G espera a que PurchaseOrder 2.3E esté inicializado.";
            return false;
        }

        state = BistroBuilderSupplierLogisticsPlanningEngine.CreateInitialSnapshot(
            orderService.CurrentGameDay,
            orderService.SourceMarketSeed,
            orderService.SourceCommercialSeed,
            settings);
        RebuildIndexes();
        Subscribe();
        string planError;
        int created;
        TryPlanConfirmedOrders(out created, out planError);
        return true;
    }

    /// <summary>
    /// Alinea una sesión logística todavía no vinculada (source seed = 0) con
    /// la sesión real de PurchaseOrder 2.3E sin destruir planes existentes.
    /// Una contradicción entre seeds no nulas sigue considerándose cambio real
    /// de sesión y devuelve false.
    /// </summary>
    public bool TrySynchronizeSessionBinding(out string error)
    {
        error = null;
        BindDependencies();

        if (state == null)
        {
            error = "2.3G no tiene snapshot runtime que enlazar.";
            return false;
        }
        if (orderService == null || !orderService.IsInitialized)
        {
            error = "2.3E no está disponible para enlazar la sesión logística.";
            return false;
        }

        ulong orderMarketSeed = orderService.SourceMarketSeed;
        ulong orderCommercialSeed = orderService.SourceCommercialSeed;

        if (state.sourceMarketSeed != 0UL && orderMarketSeed != 0UL &&
            state.sourceMarketSeed != orderMarketSeed)
        {
            error = "supplier.logistics.runtime pertenece a otra sesión de mercado.";
            return false;
        }
        if (state.sourceCommercialSeed != 0UL && orderCommercialSeed != 0UL &&
            state.sourceCommercialSeed != orderCommercialSeed)
        {
            error = "supplier.logistics.runtime pertenece a otra sesión comercial.";
            return false;
        }

        bool changed = false;
        if (state.sourceMarketSeed == 0UL && orderMarketSeed != 0UL)
        {
            state.sourceMarketSeed = orderMarketSeed;
            changed = true;
        }
        if (state.sourceCommercialSeed == 0UL && orderCommercialSeed != 0UL)
        {
            state.sourceCommercialSeed = orderCommercialSeed;
            changed = true;
        }

        if (changed)
        {
            state.logisticsRevision = state.logisticsRevision == long.MaxValue
                ? long.MaxValue
                : state.logisticsRevision + 1L;
        }

        return true;
    }

    public bool TryPlanConfirmedOrders(out int createdCount, out string error)
    {
        createdCount = 0;
        error = null;
        if (!EnsureInitialized(out error)) return false;
        if (!TrySynchronizeSessionBinding(out error)) return false;
        List<BistroBuilderPurchaseOrderRecord> orders = new List<BistroBuilderPurchaseOrderRecord>();
        orderService.CopyOrders(orders);
        for (int index = 0; index < orders.Count; index++)
        {
            BistroBuilderPurchaseOrderRecord order = orders[index];
            if (order == null || order.status != BistroBuilderPurchaseOrderStatus.Confirmed || planByOrderId.ContainsKey(order.purchaseOrderId)) continue;
            BistroBuilderSupplierLogisticsPlanRecord plan;
            if (!TryCreatePlanForOrder(order.purchaseOrderId, out plan, out error)) return false;
            createdCount++;
        }
        return true;
    }

    public bool TryCreatePlanForOrder(string purchaseOrderId, out BistroBuilderSupplierLogisticsPlanRecord created, out string error)
    {
        created = null;
        error = null;
        if (!EnsureInitialized(out error)) return false;
        if (!TrySynchronizeSessionBinding(out error)) return false;
        if (string.IsNullOrWhiteSpace(purchaseOrderId))
        {
            error = "PurchaseOrderId vacío.";
            return false;
        }
        BistroBuilderSupplierLogisticsPlanRecord existing;
        if (planByOrderId.TryGetValue(purchaseOrderId, out existing) && existing != null)
        {
            created = existing.DeepClone();
            return true;
        }

        BistroBuilderPurchaseOrderRecord order;
        if (!orderService.TryGetOrder(purchaseOrderId, out order) || order == null)
        {
            error = "No existe el PurchaseOrder solicitado.";
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Confirmed)
        {
            error = "Solo un PurchaseOrder Confirmed sin plan puede planificarse en 2.3G.";
            return false;
        }
        BistroBuilderSupplierAuthoringRecord supplier;
        if (!supplierDatabase.TryGetSupplier(order.supplierId, out supplier) || supplier == null || !supplier.isActive)
        {
            error = "No se puede resolver el proveedor activo del PurchaseOrder.";
            return false;
        }

        BistroBuilderSupplierLogisticsPlanRecord plan;
        if (!BistroBuilderSupplierLogisticsPlanningEngine.TryBuildPlan(state, order, supplier, settings, orderService.CurrentGameDay, out plan, out error))
            return false;

        BistroBuilderPurchaseOrderRecord updated;
        if (!orderService.TryMarkPendingDelivery(
                order.purchaseOrderId,
                plan.logisticsPlanId,
                plan.basePlannedDeliveryGameDay,
                plan.baseWindowStartMinuteOfDay,
                plan.baseWindowEndMinuteOfDay,
                out updated,
                out error))
            return false;

        plan.sourceOrderStateRevision = updated.stateRevision;
        state.plans.Add(plan);
        state.nextPlanSequence++;
        state.logisticsRevision++;
        state.currentGameDay = Math.Max(state.currentGameDay, orderService.CurrentGameDay);
        RebuildIndexes();
        created = plan.DeepClone();
        PlanCreated?.Invoke(created.DeepClone());
        return true;
    }

    public bool TryAdvanceToGameDay(int gameDay, out string error)
    {
        error = null;
        if (!EnsureInitialized(out error)) return false;
        int currentAuthorityDay = Math.Max(1, orderService.CurrentGameDay);
        if (gameDay < state.currentGameDay)
        {
            error = "2.3G no puede retroceder el día sin restaurar snapshot.";
            return false;
        }
        if (gameDay > currentAuthorityDay)
        {
            error = "2.3G no puede adelantarse al día real de PurchaseOrder/mercado.";
            return false;
        }

        bool anyChange = false;
        for (int index = 0; index < state.plans.Count; index++)
        {
            BistroBuilderSupplierLogisticsPlanRecord plan = state.plans[index];
            if (plan == null || plan.IsTerminal) continue;
            BistroBuilderPurchaseOrderRecord order;
            if (!orderService.TryGetOrder(plan.purchaseOrderId, out order) || order == null) continue;

            if (order.status == BistroBuilderPurchaseOrderStatus.Cancelled)
            {
                plan.status = BistroBuilderSupplierLogisticsPlanStatus.Cancelled;
                plan.stateRevision++;
                anyChange = true;
                PlanChanged?.Invoke(plan.DeepClone());
                continue;
            }
            if (order.status == BistroBuilderPurchaseOrderStatus.Delivered)
            {
                plan.status = BistroBuilderSupplierLogisticsPlanStatus.Delivered;
                plan.stateRevision++;
                anyChange = true;
                PlanChanged?.Invoke(plan.DeepClone());
                continue;
            }
            if (order.status == BistroBuilderPurchaseOrderStatus.InDelivery)
            {
                if (plan.status != BistroBuilderSupplierLogisticsPlanStatus.Dispatched)
                {
                    plan.status = BistroBuilderSupplierLogisticsPlanStatus.Dispatched;
                    plan.stateRevision++;
                    anyChange = true;
                    PlanChanged?.Invoke(plan.DeepClone());
                }
                continue;
            }
            if (order.status != BistroBuilderPurchaseOrderStatus.PendingDelivery) continue;

            bool delayChanged;
            if (!BistroBuilderSupplierLogisticsPlanningEngine.TryApplyDelay(plan, gameDay, out delayChanged, out error)) return false;
            if (delayChanged)
            {
                BistroBuilderPurchaseOrderRecord replanned;
                if (!orderService.TryUpdatePendingDeliveryPlan(
                        plan.purchaseOrderId,
                        plan.logisticsPlanId,
                        plan.plannedDeliveryGameDay,
                        plan.windowStartMinuteOfDay,
                        plan.windowEndMinuteOfDay,
                        plan.decidedDelayGameMinutes,
                        out replanned,
                        out error))
                    return false;
                plan.sourceOrderStateRevision = replanned.stateRevision;
                anyChange = true;
                PlanChanged?.Invoke(plan.DeepClone());
            }

            if (gameDay >= plan.plannedDeliveryGameDay && plan.status != BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch)
            {
                plan.status = BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch;
                plan.stateRevision++;
                anyChange = true;
                PlanChanged?.Invoke(plan.DeepClone());
            }
        }
        state.currentGameDay = gameDay;
        if (anyChange) state.logisticsRevision++;
        return true;
    }

    public bool TryBuildDispatchTicket(string purchaseOrderId, out BistroBuilderSupplierDispatchTicket ticket, out string error)
    {
        ticket = null;
        error = null;
        if (!EnsureInitialized(out error)) return false;
        BistroBuilderSupplierLogisticsPlanRecord plan;
        if (!planByOrderId.TryGetValue(purchaseOrderId, out plan) || plan == null)
        {
            error = "El PurchaseOrder no tiene plan logístico 2.3G.";
            return false;
        }
        if (plan.status != BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch)
        {
            error = "El plan todavía no está listo para iniciar la presentación 2.3H.";
            return false;
        }
        ticket = new BistroBuilderSupplierDispatchTicket
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
        return true;
    }

    /// <summary>Contrato que 2.3H invocará al arrancar físicamente la entrega.</summary>
    public bool TryDispatch(string purchaseOrderId, out BistroBuilderSupplierDispatchTicket ticket, out string error)
    {
        ticket = null;
        error = null;
        if (!TryBuildDispatchTicket(purchaseOrderId, out ticket, out error)) return false;
        BistroBuilderSupplierLogisticsPlanRecord plan = planByOrderId[purchaseOrderId];
        BistroBuilderPurchaseOrderRecord updated;
        if (!orderService.TryMarkInDelivery(
                purchaseOrderId,
                orderService.CurrentGameDay,
                ticket.appliedDelayGameMinutes,
                out updated,
                out error))
            return false;
        plan.status = BistroBuilderSupplierLogisticsPlanStatus.Dispatched;
        plan.sourceOrderStateRevision = updated.stateRevision;
        plan.stateRevision++;
        state.logisticsRevision++;
        DispatchStarted?.Invoke(ticket.DeepClone());
        PlanChanged?.Invoke(plan.DeepClone());
        return true;
    }

    public bool TryGetPlanByOrder(string purchaseOrderId, out BistroBuilderSupplierLogisticsPlanRecord plan)
    {
        plan = null;
        if (!IsInitialized || string.IsNullOrWhiteSpace(purchaseOrderId)) return false;
        BistroBuilderSupplierLogisticsPlanRecord stored;
        if (!planByOrderId.TryGetValue(purchaseOrderId, out stored) || stored == null) return false;
        plan = stored.DeepClone();
        return true;
    }

    public int CopyPlans(List<BistroBuilderSupplierLogisticsPlanRecord> buffer)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        buffer.Clear();
        if (!IsInitialized || state.plans == null) return 0;
        for (int index = 0; index < state.plans.Count; index++)
            if (state.plans[index] != null) buffer.Add(state.plans[index].DeepClone());
        return buffer.Count;
    }

    public BistroBuilderSupplierLogisticsSnapshot CreateSnapshot()
    {
        return state != null ? state.DeepClone() : null;
    }

    public bool TryRestoreSnapshot(BistroBuilderSupplierLogisticsSnapshot candidate, out string error)
    {
        error = null;
        LoadStaticDependencies();
        BindDependencies();
        if (candidate == null)
        {
            error = "Snapshot logístico nulo.";
            return false;
        }
        if (orderService == null || !orderService.IsInitialized)
        {
            error = "Restaura primero PurchaseOrder 2.3E.";
            return false;
        }
        BistroBuilderSupplierLogisticsSnapshot owned = candidate.DeepClone();
        if (!BistroBuilderSupplierLogisticsPlanningEngine.ValidateSnapshot(owned, out error)) return false;
        ulong orderMarketSeed = orderService.SourceMarketSeed;
        ulong orderCommercialSeed = orderService.SourceCommercialSeed;
        if (owned.sourceMarketSeed != 0UL && orderMarketSeed != 0UL &&
            owned.sourceMarketSeed != orderMarketSeed)
        {
            error = "supplier.logistics.runtime pertenece a otra sesión de mercado.";
            return false;
        }
        if (owned.sourceCommercialSeed != 0UL && orderCommercialSeed != 0UL &&
            owned.sourceCommercialSeed != orderCommercialSeed)
        {
            error = "supplier.logistics.runtime pertenece a otra sesión comercial.";
            return false;
        }

        // Normaliza snapshots históricos/unbound al restaurar, sin reconstruir
        // ni perder sus LogisticsPlans.
        if (owned.sourceMarketSeed == 0UL && orderMarketSeed != 0UL)
            owned.sourceMarketSeed = orderMarketSeed;
        if (owned.sourceCommercialSeed == 0UL && orderCommercialSeed != 0UL)
            owned.sourceCommercialSeed = orderCommercialSeed;

        if (owned.currentGameDay != orderService.CurrentGameDay)
        {
            error = "supplier.logistics.runtime debe restaurarse después de 2.3C/2.3D/2.3E del mismo día.";
            return false;
        }
        state = owned;
        lastInitializationError = null;
        RebuildIndexes();
        Subscribe();
        return true;
    }

    private void HandleOrderConfirmed(BistroBuilderPurchaseOrderConfirmationReceipt receipt)
    {
        if (receipt == null || string.IsNullOrWhiteSpace(receipt.purchaseOrderId)) return;
        BistroBuilderSupplierLogisticsPlanRecord ignored;
        string error;
        TryCreatePlanForOrder(receipt.purchaseOrderId, out ignored, out error);
        if (!string.IsNullOrWhiteSpace(error)) lastInitializationError = error;
    }

    private bool EnsureInitialized(out string error)
    {
        error = null;
        if (!IsInitialized && !TryInitializeFresh())
        {
            error = lastInitializationError ?? "2.3G no inicializado.";
            return false;
        }
        return true;
    }

    private void LoadStaticDependencies()
    {
        supplierDatabase = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(SupplierAuthoringResourcePath);
        settings = Resources.Load<BistroBuilderSupplierLogisticsPlanningSettings>(SettingsResourcePath);
    }

    private void BindDependencies()
    {
        BistroBuilderSupplierPurchaseOrderService resolved = BistroBuilderSupplierPurchaseOrderService.Instance;
        if (resolved == null) resolved = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        if (orderService != resolved)
        {
            Unsubscribe();
            orderService = resolved;
            Subscribe();
        }
    }

    private void Subscribe()
    {
        if (subscribed || orderService == null) return;
        orderService.OrderConfirmed += HandleOrderConfirmed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || orderService == null) return;
        orderService.OrderConfirmed -= HandleOrderConfirmed;
        subscribed = false;
    }

    private void RebuildIndexes()
    {
        planById.Clear();
        planByOrderId.Clear();
        if (state == null || state.plans == null) return;
        for (int index = 0; index < state.plans.Count; index++)
        {
            BistroBuilderSupplierLogisticsPlanRecord plan = state.plans[index];
            if (plan == null) continue;
            if (!string.IsNullOrWhiteSpace(plan.logisticsPlanId)) planById[plan.logisticsPlanId] = plan;
            if (!string.IsNullOrWhiteSpace(plan.purchaseOrderId)) planByOrderId[plan.purchaseOrderId] = plan;
        }
    }
}
