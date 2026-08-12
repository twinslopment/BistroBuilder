using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime de 2.3I.
///
/// Responsabilidades:
/// - interpretar unlockProfile de supplier.authoring;
/// - mantener desbloqueos permanentes de la partida;
/// - acumular volumen de compra cualificado sin duplicar pedidos;
/// - exponer requisitos/progreso explicables para la futura UI 2.3K;
/// - ofrecer una fachada de creación de Draft para gameplay respetando desbloqueos.
///
/// No modifica supplier.catalog, mercado 2.3C, promociones 2.3D, logística 2.3G,
/// presentación 2.3H, Inventario ni Recepciones.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(55)]
public sealed class BistroBuilderSupplierProgressionService : MonoBehaviour
{
    public const string SupplierAuthoringResourcePath =
        BistroBuilderSupplierPurchaseOrderService.SupplierAuthoringResourcePath;
    public const string SettingsResourcePath =
        "BistroBuilder/Suppliers/BistroBuilderSupplierProgressionSettings";

    private static BistroBuilderSupplierProgressionService instance;

    private readonly Dictionary<string, BistroBuilderSupplierProgressionStateRecord> stateBySupplierId =
        new Dictionary<string, BistroBuilderSupplierProgressionStateRecord>(StringComparer.Ordinal);
    private readonly HashSet<string> countedOrderIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<BistroBuilderPurchaseOrderRecord> orderBuffer =
        new List<BistroBuilderPurchaseOrderRecord>();

    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private BistroBuilderSupplierProgressionSettings settings;
    private BistroBuilderSupplierPurchaseOrderService orderService;
    private BistroBuilderSupplierProgressionSnapshot state;
    private BistroBuilderSupplierProgressionFacts cachedFacts;
    private string lastInitializationError;
    private int lastObservedGameDay = -1;
    private bool orderEventsBound;

#if UNITY_EDITOR
    private bool useControlledFactsForEditorTests;
    private BistroBuilderSupplierProgressionFacts controlledFactsForEditorTests;
#endif

    public static BistroBuilderSupplierProgressionService Instance => instance;
    public bool IsInitialized => state != null && supplierDatabase != null && settings != null && string.IsNullOrEmpty(lastInitializationError);
    public string LastInitializationError => lastInitializationError;
    public int CurrentGameDay => cachedFacts != null ? cachedFacts.currentGameDay : ResolveCurrentGameDay();
    public long ProgressionRevision => state != null ? state.progressionRevision : 0L;
    public long QualifiedPurchaseVolumeCents => state != null ? state.qualifiedPurchaseVolumeCents : 0L;
    public int SupplierStateCount => state != null && state.suppliers != null ? state.suppliers.Count : 0;

    public event Action<BistroBuilderSupplierAccessEvaluation> SupplierUnlocked;
    public event Action ProgressionChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeAuthority()
    {
        if (UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierProgressionService>() != null)
        {
            return;
        }

        GameObject host = new GameObject("BistroBuilderSupplierProgressionService");
        DontDestroyOnLoad(host);
        host.AddComponent<BistroBuilderSupplierProgressionService>();
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

    private void Update()
    {
        if (!IsInitialized || settings == null || !settings.AutomaticEvaluationOnGameDayChange)
        {
            return;
        }

        int day = ResolveCurrentGameDay();
        if (day != lastObservedGameDay)
        {
            RefreshNow();
        }
    }

    private void OnDestroy()
    {
        UnbindOrderEvents();
        if (instance == this)
        {
            instance = null;
        }
    }

    public bool TryInitializeFresh()
    {
        lastInitializationError = null;
        supplierDatabase = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(SupplierAuthoringResourcePath);
        settings = Resources.Load<BistroBuilderSupplierProgressionSettings>(SettingsResourcePath);
        ResolveOrderService();

        if (supplierDatabase == null)
        {
            lastInitializationError = "Falta supplier.authoring en Resources.";
            return false;
        }
        if (settings == null)
        {
            lastInitializationError = "Falta supplier.progression.settings. Ejecuta el instalador 2.3I.";
            return false;
        }

        ulong marketSeed = orderService != null && orderService.IsInitialized ? orderService.SourceMarketSeed : 0UL;
        ulong commercialSeed = orderService != null && orderService.IsInitialized ? orderService.SourceCommercialSeed : 0UL;
        state = BistroBuilderSupplierProgressionEngine.CreateInitialSnapshot(
            supplierDatabase.Suppliers,
            ResolveCurrentGameDay(),
            marketSeed,
            commercialSeed);

        RebuildIndexes();
        BindOrderEvents();
        CaptureQualifiedPurchaseOrders();
        return RefreshNow();
    }

    public bool RefreshNow()
    {
        if (!EnsureInitialized())
        {
            return false;
        }

        ResolveOrderService();
        BindOrderEvents();

#if UNITY_EDITOR
        if (!useControlledFactsForEditorTests)
        {
            CaptureQualifiedPurchaseOrders();
        }
#else
        CaptureQualifiedPurchaseOrders();
#endif

        cachedFacts = BuildFacts();
        state.currentGameDay = Math.Max(1, cachedFacts.currentGameDay);
        lastObservedGameDay = state.currentGameDay;

        bool changed = false;
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = supplierDatabase.Suppliers;
        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier == null || !supplier.isActive || string.IsNullOrWhiteSpace(supplier.SupplierId))
            {
                continue;
            }

            BistroBuilderSupplierProgressionStateRecord supplierState = EnsureSupplierState(supplier);
            if (supplierState.unlocked)
            {
                continue;
            }

            BistroBuilderSupplierAccessEvaluation evaluation =
                BistroBuilderSupplierProgressionEngine.Evaluate(supplier, cachedFacts);
            if (!evaluation.isUnlocked)
            {
                continue;
            }

            supplierState.unlocked = true;
            supplierState.unlockedGameDay = state.currentGameDay;
            supplierState.unlockReasonCode = evaluation.availableFromStart
                ? "available_from_start"
                : "requirements_met";
            supplierState.unlockReasonText = evaluation.summary;
            supplierState.stateRevision = Math.Max(1L, supplierState.stateRevision + 1L);
            changed = true;

            evaluation.status = evaluation.availableFromStart
                ? BistroBuilderSupplierAccessStatus.AvailableFromStart
                : BistroBuilderSupplierAccessStatus.Unlocked;
            evaluation.isUnlocked = true;
            SupplierUnlocked?.Invoke(evaluation.DeepClone());
        }

        if (changed)
        {
            state.progressionRevision = Math.Max(1L, state.progressionRevision + 1L);
            ProgressionChanged?.Invoke();
        }

        return true;
    }

    public bool IsSupplierUnlocked(string supplierId)
    {
        BistroBuilderSupplierAccessEvaluation access;
        return TryGetSupplierAccess(supplierId, out access) && access != null && access.isUnlocked;
    }

    public bool TryGetSupplierAccess(string supplierId, out BistroBuilderSupplierAccessEvaluation access)
    {
        access = null;
        if (!EnsureInitialized())
        {
            return false;
        }

        RefreshIfGameDayChanged();

        BistroBuilderSupplierAuthoringRecord supplier;
        if (!supplierDatabase.TryGetSupplier(supplierId, out supplier) || supplier == null || !supplier.isActive)
        {
            return false;
        }

        if (cachedFacts == null)
        {
            cachedFacts = BuildFacts();
        }

        BistroBuilderSupplierAccessEvaluation evaluation =
            BistroBuilderSupplierProgressionEngine.Evaluate(supplier, cachedFacts);
        BistroBuilderSupplierProgressionStateRecord supplierState = EnsureSupplierState(supplier);

        if (supplierState.unlocked)
        {
            evaluation.isUnlocked = true;
            evaluation.conditionsSatisfied = true;
            evaluation.progress01 = 1f;
            evaluation.status = supplier.unlockProfile != null && supplier.unlockProfile.availableFromStart
                ? BistroBuilderSupplierAccessStatus.AvailableFromStart
                : BistroBuilderSupplierAccessStatus.Unlocked;
            if (!string.IsNullOrWhiteSpace(supplierState.unlockReasonText))
            {
                evaluation.summary = supplierState.unlockReasonText;
            }
        }

        access = evaluation;
        return true;
    }

    public int CopySupplierAccess(List<BistroBuilderSupplierAccessEvaluation> buffer, bool includeLocked = true)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        buffer.Clear();
        if (!EnsureInitialized())
        {
            return 0;
        }

        RefreshIfGameDayChanged();
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = supplierDatabase.Suppliers;
        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier == null || !supplier.isActive)
            {
                continue;
            }

            BistroBuilderSupplierAccessEvaluation access;
            if (TryGetSupplierAccess(supplier.SupplierId, out access) && access != null && (includeLocked || access.isUnlocked))
            {
                buffer.Add(access);
            }
        }
        return buffer.Count;
    }

    /// <summary>
    /// Fachada que deberá utilizar la UI de jugador. 2.3E permanece como autoridad
    /// de pedidos y sus pruebas internas pueden seguir operando sin conocer progresión.
    /// </summary>
    public bool TryCreatePlayerDraft(
        string supplierId,
        out BistroBuilderPurchaseOrderRecord draft,
        out string error)
    {
        draft = null;
        error = null;
        if (!EnsureInitialized())
        {
            error = lastInitializationError;
            return false;
        }

        BistroBuilderSupplierAccessEvaluation access;
        if (!TryGetSupplierAccess(supplierId, out access) || access == null)
        {
            error = "Proveedor inexistente o inactivo.";
            return false;
        }
        if (!access.isUnlocked)
        {
            error = "El proveedor está bloqueado. " + access.summary;
            return false;
        }

        ResolveOrderService();
        if (orderService == null || !orderService.IsInitialized)
        {
            error = "2.3E no está disponible/inicializado.";
            return false;
        }

        return orderService.TryCreateDraft(supplierId, out draft, out error);
    }

    public BistroBuilderSupplierProgressionSnapshot CreateSnapshot()
    {
        return state != null ? state.DeepClone() : null;
    }

    public bool TryRestoreSnapshot(BistroBuilderSupplierProgressionSnapshot snapshot, out string error)
    {
        error = null;
        if (snapshot == null)
        {
            error = "Snapshot 2.3I nulo.";
            return false;
        }
        if (!string.Equals(snapshot.schemaId, BistroBuilderSupplierProgressionSnapshot.CurrentSchemaId, StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderSupplierProgressionSnapshot.CurrentSchemaVersion)
        {
            error = "Snapshot 2.3I usa un schema incompatible.";
            return false;
        }

        ResolveOrderService();
        if (orderService != null && orderService.IsInitialized)
        {
            if (snapshot.sourceMarketSeed != 0UL && orderService.SourceMarketSeed != 0UL &&
                snapshot.sourceMarketSeed != orderService.SourceMarketSeed)
            {
                error = "Snapshot 2.3I pertenece a otra sesión de mercado 2.3C.";
                return false;
            }
            if (snapshot.sourceCommercialSeed != 0UL && orderService.SourceCommercialSeed != 0UL &&
                snapshot.sourceCommercialSeed != orderService.SourceCommercialSeed)
            {
                error = "Snapshot 2.3I pertenece a otra sesión comercial 2.3D.";
                return false;
            }
        }

        state = snapshot.DeepClone();
        RebuildIndexes();
        BindOrderEvents();
        CaptureQualifiedPurchaseOrders();
        return RefreshNow();
    }

#if UNITY_EDITOR
    public bool EditorInitializeControlledState(BistroBuilderSupplierProgressionFacts facts)
    {
        if (supplierDatabase == null || settings == null)
        {
            if (!TryInitializeFresh()) return false;
        }
        if (facts == null) return false;
        ResolveOrderService();
        ulong marketSeed = orderService != null && orderService.IsInitialized ? orderService.SourceMarketSeed : 0UL;
        ulong commercialSeed = orderService != null && orderService.IsInitialized ? orderService.SourceCommercialSeed : 0UL;
        state = BistroBuilderSupplierProgressionEngine.CreateInitialSnapshot(
            supplierDatabase.Suppliers,
            Math.Max(1, facts.currentGameDay),
            marketSeed,
            commercialSeed);
        RebuildIndexes();
        useControlledFactsForEditorTests = true;
        controlledFactsForEditorTests = facts.DeepClone();
        return RefreshNow();
    }

    public bool EditorSetControlledFacts(BistroBuilderSupplierProgressionFacts facts)
    {
        if (!EnsureInitialized() || facts == null)
        {
            return false;
        }
        useControlledFactsForEditorTests = true;
        controlledFactsForEditorTests = facts.DeepClone();
        return RefreshNow();
    }

    public void EditorClearControlledFacts()
    {
        useControlledFactsForEditorTests = false;
        controlledFactsForEditorTests = null;
        RefreshNow();
    }
#endif

    private bool EnsureInitialized()
    {
        return IsInitialized || TryInitializeFresh();
    }

    private void RefreshIfGameDayChanged()
    {
        if (ResolveCurrentGameDay() != lastObservedGameDay)
        {
            RefreshNow();
        }
    }

    private BistroBuilderSupplierProgressionFacts BuildFacts()
    {
#if UNITY_EDITOR
        if (useControlledFactsForEditorTests && controlledFactsForEditorTests != null)
        {
            BistroBuilderSupplierProgressionFacts controlled = controlledFactsForEditorTests.DeepClone();
            controlled.currentGameDay = Math.Max(1, controlled.currentGameDay);
            controlled.daysOpen = Math.Max(0, controlled.daysOpen);
            return controlled;
        }
#endif

        int day = ResolveCurrentGameDay();
        BistroBuilderSupplierProgressionFacts baseFacts = new BistroBuilderSupplierProgressionFacts
        {
            currentGameDay = Math.Max(1, day),
            daysOpen = Math.Max(0, day - 1),
            qualifiedPurchaseVolumeCents = state != null ? Math.Max(0L, state.qualifiedPurchaseVolumeCents) : 0L
        };

        BistroBuilderSupplierProgressionFactBuilder builder =
            new BistroBuilderSupplierProgressionFactBuilder(baseFacts);

        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour == null || ReferenceEquals(behaviour, this))
            {
                continue;
            }

            IBistroBuilderSupplierProgressionFactSource source =
                behaviour as IBistroBuilderSupplierProgressionFactSource;
            if (source == null)
            {
                continue;
            }

            try
            {
                source.ContributeSupplierProgressionFacts(builder);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("2.3I ignoró una fuente externa de hechos que lanzó excepción: " + exception.Message);
            }
        }

        return builder.Build();
    }

    private int ResolveCurrentGameDay()
    {
        ResolveOrderService();
        if (orderService != null && orderService.IsInitialized)
        {
            return Math.Max(1, orderService.CurrentGameDay);
        }

        BistroBuilderSupplierMarketService market = BistroBuilderSupplierMarketService.Instance;
        if (market == null)
        {
            market = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierMarketService>();
        }
        return market != null && market.IsInitialized ? Math.Max(1, market.CurrentGameDay) : 1;
    }

    private void ResolveOrderService()
    {
        if (orderService == null)
        {
            orderService = BistroBuilderSupplierPurchaseOrderService.Instance ??
                           UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        }
    }

    private void BindOrderEvents()
    {
        ResolveOrderService();
        if (orderService == null || orderEventsBound)
        {
            return;
        }

        orderService.OrderStateChanged += HandleOrderStateChanged;
        orderEventsBound = true;
    }

    private void UnbindOrderEvents()
    {
        if (orderService != null && orderEventsBound)
        {
            orderService.OrderStateChanged -= HandleOrderStateChanged;
        }
        orderEventsBound = false;
    }

    private void HandleOrderStateChanged(BistroBuilderPurchaseOrderRecord order)
    {
        if (!IsInitialized)
        {
            return;
        }

        if (TryCountQualifiedOrder(order))
        {
            state.progressionRevision = Math.Max(1L, state.progressionRevision + 1L);
            ProgressionChanged?.Invoke();
        }
        RefreshNow();
    }

    private bool CaptureQualifiedPurchaseOrders()
    {
        ResolveOrderService();
        if (state == null || orderService == null || !orderService.IsInitialized)
        {
            return false;
        }

        orderService.CopyOrders(orderBuffer);
        bool changed = false;
        for (int index = 0; index < orderBuffer.Count; index++)
        {
            changed |= TryCountQualifiedOrder(orderBuffer[index]);
        }
        return changed;
    }

    private bool TryCountQualifiedOrder(BistroBuilderPurchaseOrderRecord order)
    {
        if (state == null || order == null || string.IsNullOrWhiteSpace(order.purchaseOrderId))
        {
            return false;
        }
        if (!BistroBuilderSupplierProgressionEngine.IsPurchaseVolumeQualifiedStatus(order.status, settings))
        {
            return false;
        }
        if (countedOrderIds.Contains(order.purchaseOrderId))
        {
            return false;
        }

        countedOrderIds.Add(order.purchaseOrderId);
        state.countedQualifiedPurchaseOrderIds.Add(order.purchaseOrderId);
        state.qualifiedPurchaseVolumeCents = SafeAddNonNegative(state.qualifiedPurchaseVolumeCents, order.totalCents);
        return true;
    }

    private BistroBuilderSupplierProgressionStateRecord EnsureSupplierState(BistroBuilderSupplierAuthoringRecord supplier)
    {
        BistroBuilderSupplierProgressionStateRecord existing;
        if (supplier != null && stateBySupplierId.TryGetValue(supplier.SupplierId, out existing))
        {
            return existing;
        }

        bool fromStart = supplier != null && supplier.unlockProfile != null && supplier.unlockProfile.availableFromStart;
        BistroBuilderSupplierProgressionStateRecord created = new BistroBuilderSupplierProgressionStateRecord
        {
            supplierId = supplier != null ? supplier.SupplierId : string.Empty,
            unlocked = fromStart,
            unlockedGameDay = fromStart ? Math.Max(1, ResolveCurrentGameDay()) : 0,
            unlockReasonCode = fromStart ? "available_from_start" : string.Empty,
            unlockReasonText = fromStart ? "Disponible desde el inicio." : string.Empty,
            stateRevision = 1
        };
        state.suppliers.Add(created);
        if (!string.IsNullOrWhiteSpace(created.supplierId))
        {
            stateBySupplierId[created.supplierId] = created;
        }
        return created;
    }

    private void RebuildIndexes()
    {
        stateBySupplierId.Clear();
        countedOrderIds.Clear();
        if (state == null)
        {
            return;
        }

        if (state.suppliers == null)
        {
            state.suppliers = new List<BistroBuilderSupplierProgressionStateRecord>();
        }
        if (state.countedQualifiedPurchaseOrderIds == null)
        {
            state.countedQualifiedPurchaseOrderIds = new List<string>();
        }

        for (int index = 0; index < state.suppliers.Count; index++)
        {
            BistroBuilderSupplierProgressionStateRecord record = state.suppliers[index];
            if (record != null && !string.IsNullOrWhiteSpace(record.supplierId))
            {
                stateBySupplierId[record.supplierId] = record;
            }
        }
        for (int index = 0; index < state.countedQualifiedPurchaseOrderIds.Count; index++)
        {
            string orderId = state.countedQualifiedPurchaseOrderIds[index];
            if (!string.IsNullOrWhiteSpace(orderId))
            {
                countedOrderIds.Add(orderId);
            }
        }
    }

    private static long SafeAddNonNegative(long left, long right)
    {
        left = Math.Max(0L, left);
        right = Math.Max(0L, right);
        if (long.MaxValue - left < right)
        {
            return long.MaxValue;
        }
        return left + right;
    }
}
