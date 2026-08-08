using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Planificación y alertas de inventario 2.2C.
///
/// Esta capa no posee cantidades físicas. Lee siempre de
/// BistroBuilderInventoryService y únicamente conserva políticas del jugador
/// (stock mínimo). La previsión se deriva del consumo autoritativo acumulado
/// y del DayIndex, sin contar compras, correcciones, mermas ni caducidad.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Inventory/Inventory Planning Service 2.2C")]
public sealed class BistroBuilderInventoryPlanningService : MonoBehaviour
{
    public const string DefaultRestaurantId =
        BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderInventoryService inventoryService;

    [SerializeField]
    private BistroBuilderRecipeCatalogService recipeCatalogService;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private RestaurantServiceStateService serviceStateService;

    [Header("Reglas de planificación")]

    [SerializeField]
    [Range(0.05f, 0.95f)]
    private float criticalThresholdRatio = 0.5f;

    [SerializeField]
    [Min(1)]
    private int minimumHistoryDaysForForecast = 2;

    [Header("Depuración")]

    [SerializeField]
    private bool logOpeningWarnings = true;

    private readonly Dictionary<string, long> minimumByIngredientId =
        new Dictionary<string, long>(StringComparer.Ordinal);

    private readonly Dictionary<string, BistroBuilderInventoryPlanningSnapshot>
        planningByIngredientId =
            new Dictionary<string, BistroBuilderInventoryPlanningSnapshot>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, BistroBuilderInventoryAlertSnapshot>
        activeAlertsByKey =
            new Dictionary<string, BistroBuilderInventoryAlertSnapshot>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderInventoryStockSnapshot> stockBuffer =
        new List<BistroBuilderInventoryStockSnapshot>(64);

    private readonly List<BistroBuilderInventoryLotSnapshot> lotBuffer =
        new List<BistroBuilderInventoryLotSnapshot>(128);

    private readonly List<BistroBuilderIngredientDefinition> ingredientBuffer =
        new List<BistroBuilderIngredientDefinition>(64);

    private readonly Dictionary<string, long> nearExpiryByIngredientId =
        new Dictionary<string, long>(StringComparer.Ordinal);

    private long runtimeRevision;
    private long policyRevision;
    private bool initialized;
    private bool subscribed;
    private bool rebuilding;

    public event Action PlanningChanged;

    public event Action<BistroBuilderInventoryAlertSnapshot> AlertActivated;

    public event Action<BistroBuilderInventoryAlertSnapshot> AlertCleared;

    public event Action<BistroBuilderInventoryOpeningReadinessSnapshot>
        OpeningReadinessEvaluated;

    public BistroBuilderInventoryService InventoryService => inventoryService;

    public BistroBuilderRecipeCatalogService RecipeCatalogService =>
        recipeCatalogService;

    public BistroBuilderGeneralGameStateService GeneralGameStateService =>
        generalGameStateService;

    public RestaurantServiceStateService ServiceStateService =>
        serviceStateService;

    public float CriticalThresholdRatio => criticalThresholdRatio;

    public int MinimumHistoryDaysForForecast =>
        minimumHistoryDaysForForecast;

    public bool IsInitialized => initialized;

    public int IngredientCount => planningByIngredientId.Count;

    public int ActiveAlertCount => activeAlertsByKey.Count;

    public long RuntimeRevision => runtimeRevision;

    public long PolicyRevision => policyRevision;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
    }

    private void Start()
    {
        if (!TryInitialize(out string error))
        {
            Debug.LogError(error, this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (inventoryService == null)
        {
            error = "Falta BistroBuilderInventoryService.";
            return false;
        }

        if (!inventoryService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (recipeCatalogService == null)
        {
            error = "Falta BistroBuilderRecipeCatalogService.";
            return false;
        }

        if (!recipeCatalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (generalGameStateService == null)
        {
            error = "Falta BistroBuilderGeneralGameStateService.";
            return false;
        }

        if (!generalGameStateService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (serviceStateService == null)
        {
            error = "Falta RestaurantServiceStateService para la comprobación previa a apertura.";
            return false;
        }

        if (criticalThresholdRatio <= 0f || criticalThresholdRatio >= 1f)
        {
            error = "El umbral de stock crítico debe quedar entre 0 y 1.";
            return false;
        }

        if (minimumHistoryDaysForForecast < 1)
        {
            error = "El historial mínimo de previsión es inválido.";
            return false;
        }

        return true;
    }

    public bool TryInitialize(out string error)
    {
        error = string.Empty;

        if (!ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        if (!inventoryService.IsInitialized)
        {
            error = "El inventario canónico todavía no está inicializado.";
            initialized = false;
            return false;
        }

        ingredientBuffer.Clear();
        recipeCatalogService.CopyIngredientsTo(ingredientBuffer);

        if (ingredientBuffer.Count == 0)
        {
            error = "No hay ingredientes canónicos para planificar.";
            initialized = false;
            return false;
        }

        // Solo añade políticas ausentes. Una carga que haya aplicado
        // inventory.policy antes de una reinicialización no pierde datos.
        for (int index = 0; index < ingredientBuffer.Count; index++)
        {
            BistroBuilderIngredientDefinition ingredient =
                ingredientBuffer[index];
            if (ingredient == null)
            {
                continue;
            }

            if (!minimumByIngredientId.ContainsKey(ingredient.IngredientId))
            {
                minimumByIngredientId.Add(ingredient.IngredientId, 0L);
            }
        }

        RemoveUnknownMinimumPolicies();
        initialized = true;
        Subscribe();
        return TryRecalculateAll(out error);
    }

    public bool EnsureInitialized(out string error)
    {
        if (initialized)
        {
            error = string.Empty;
            return true;
        }

        return TryInitialize(out error);
    }

    public bool TryGetMinimumStock(
        string ingredientId,
        out long minimumCanonicalMilliUnits
    )
    {
        minimumCanonicalMilliUnits = 0L;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            ingredientId
        );

        return initialized &&
               minimumByIngredientId.TryGetValue(
                   normalized,
                   out minimumCanonicalMilliUnits
               );
    }

    public bool TrySetMinimumStock(
        string ingredientId,
        long minimumCanonicalMilliUnits,
        out string error
    )
    {
        error = string.Empty;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            ingredientId
        );
        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized) ||
            !recipeCatalogService.TryGetIngredient(normalized, out _))
        {
            error = "El ingrediente indicado no existe en el catálogo canónico.";
            return false;
        }

        if (minimumCanonicalMilliUnits < 0L ||
            minimumCanonicalMilliUnits >
                BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
        {
            error = "El stock mínimo queda fuera del rango permitido.";
            return false;
        }

        bool hadCurrent = minimumByIngredientId.TryGetValue(
            normalized,
            out long current
        );
        if (hadCurrent && current == minimumCanonicalMilliUnits)
        {
            return true;
        }

        long previousPolicyRevision = policyRevision;
        minimumByIngredientId[normalized] = minimumCanonicalMilliUnits;
        policyRevision = policyRevision == long.MaxValue
            ? long.MaxValue
            : policyRevision + 1L;

        if (TryRecalculateAll(out error))
        {
            return true;
        }

        // La política y la planificación se actualizan como una sola
        // operación lógica. Si el recálculo no puede completarse, el mínimo
        // no queda parcialmente aplicado.
        if (hadCurrent)
        {
            minimumByIngredientId[normalized] = current;
        }
        else
        {
            minimumByIngredientId.Remove(normalized);
        }
        policyRevision = previousPolicyRevision;
        return false;
    }

    public bool TryGetPlanningSnapshot(
        string ingredientId,
        out BistroBuilderInventoryPlanningSnapshot snapshot
    )
    {
        snapshot = default;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            ingredientId
        );

        return initialized && planningByIngredientId.TryGetValue(
            normalized,
            out snapshot
        );
    }

    public void CopyPlanningSnapshotsTo(
        List<BistroBuilderInventoryPlanningSnapshot> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        var ids = new List<string>(planningByIngredientId.Keys);
        ids.Sort(StringComparer.Ordinal);
        for (int index = 0; index < ids.Count; index++)
        {
            destination.Add(planningByIngredientId[ids[index]]);
        }
    }

    public void CopyActiveAlertsTo(
        List<BistroBuilderInventoryAlertSnapshot> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        var keys = new List<string>(activeAlertsByKey.Keys);
        keys.Sort(StringComparer.Ordinal);
        for (int index = 0; index < keys.Count; index++)
        {
            destination.Add(activeAlertsByKey[keys[index]]);
        }
    }

    public bool TryRecalculateAll(out string error)
    {
        error = string.Empty;

        if (!initialized)
        {
            error = "La planificación de inventario no está inicializada.";
            return false;
        }

        if (rebuilding)
        {
            return true;
        }

        rebuilding = true;
        try
        {
            return RebuildState(out error);
        }
        finally
        {
            rebuilding = false;
        }
    }

    public bool TryEvaluateOpeningReadiness(
        out BistroBuilderInventoryOpeningReadinessSnapshot snapshot,
        out string error
    )
    {
        snapshot = null;
        error = string.Empty;

        if (!EnsureInitialized(out error) || !TryRecalculateAll(out error))
        {
            return false;
        }

        var warnings = new List<BistroBuilderInventoryAlertSnapshot>();
        CopyActiveAlertsTo(warnings);

        int low = 0;
        int critical = 0;
        int outOfStock = 0;
        int nearExpiry = 0;

        for (int index = 0; index < warnings.Count; index++)
        {
            switch (warnings[index].Kind)
            {
                case BistroBuilderInventoryAlertKind.LowStock:
                    low++;
                    break;
                case BistroBuilderInventoryAlertKind.CriticalStock:
                    critical++;
                    break;
                case BistroBuilderInventoryAlertKind.OutOfStock:
                    outOfStock++;
                    break;
                case BistroBuilderInventoryAlertKind.NearExpiry:
                    nearExpiry++;
                    break;
            }
        }

        string summary = warnings.Count == 0
            ? "Inventario sin avisos relevantes para la apertura."
            : "Apertura permitida con avisos: " + outOfStock +
              " sin stock, " + critical + " críticos, " + low +
              " bajos y " + nearExpiry + " próximos a caducar.";

        snapshot = new BistroBuilderInventoryOpeningReadinessSnapshot(
            Math.Max(1, generalGameStateService.DayIndex),
            low,
            critical,
            outOfStock,
            nearExpiry,
            warnings,
            summary
        );
        return true;
    }

    public bool TryCapturePolicySnapshot(
        out BistroBuilderInventoryPolicySaveData snapshot,
        out string error
    )
    {
        snapshot = null;
        error = string.Empty;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        var data = new BistroBuilderInventoryPolicySaveData
        {
            schemaVersion = BistroBuilderInventoryPolicySaveData
                .CurrentSchemaVersion,
            restaurantId = DefaultRestaurantId,
            policyRevision = policyRevision
        };

        var ids = new List<string>(minimumByIngredientId.Keys);
        ids.Sort(StringComparer.Ordinal);
        for (int index = 0; index < ids.Count; index++)
        {
            long minimum = minimumByIngredientId[ids[index]];
            if (minimum <= 0L)
            {
                continue;
            }

            data.minimumStocks.Add(
                new BistroBuilderInventoryMinimumStockSaveRecord
                {
                    ingredientId = ids[index],
                    minimumCanonicalMilliUnits = minimum
                }
            );
        }

        if (!data.TryValidateBasic(out error))
        {
            return false;
        }

        snapshot = data;
        return true;
    }

    public bool TryReplacePolicySnapshot(
        BistroBuilderInventoryPolicySaveData snapshot,
        bool publishChanges,
        out string error
    )
    {
        error = string.Empty;

        if (snapshot == null || !snapshot.TryValidateBasic(out error))
        {
            return false;
        }

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!string.Equals(
                snapshot.restaurantId,
                DefaultRestaurantId,
                StringComparison.Ordinal
            ))
        {
            error = "inventory.policy pertenece a otro restaurante.";
            return false;
        }

        var candidate = new Dictionary<string, long>(StringComparer.Ordinal);
        ingredientBuffer.Clear();
        recipeCatalogService.CopyIngredientsTo(ingredientBuffer);

        for (int index = 0; index < ingredientBuffer.Count; index++)
        {
            BistroBuilderIngredientDefinition ingredient =
                ingredientBuffer[index];
            if (ingredient != null)
            {
                candidate[ingredient.IngredientId] = 0L;
            }
        }

        for (int index = 0; index < snapshot.minimumStocks.Count; index++)
        {
            BistroBuilderInventoryMinimumStockSaveRecord record =
                snapshot.minimumStocks[index];

            if (!candidate.ContainsKey(record.ingredientId))
            {
                error = "inventory.policy referencia un ingrediente desconocido: " +
                        record.ingredientId + ".";
                return false;
            }

            candidate[record.ingredientId] =
                record.minimumCanonicalMilliUnits;
        }

        var previousMinimums = new Dictionary<string, long>(
            minimumByIngredientId,
            StringComparer.Ordinal
        );
        long previousPolicyRevision = policyRevision;
        bool previousInitialized = initialized;

        minimumByIngredientId.Clear();
        foreach (KeyValuePair<string, long> pair in candidate)
        {
            minimumByIngredientId.Add(pair.Key, pair.Value);
        }

        policyRevision = Math.Max(0L, snapshot.policyRevision);
        initialized = inventoryService != null && inventoryService.IsInitialized;
        if (!initialized)
        {
            planningByIngredientId.Clear();
            activeAlertsByKey.Clear();
            return true;
        }

        if (RebuildState(out error, publishChanges, true))
        {
            return true;
        }

        // Rollback de política: una carga inválida o un fallo de recálculo
        // nunca deja mínimos parcialmente sustituidos. RebuildState solo
        // publica sus diccionarios al final, por lo que la vista anterior
        // continúa intacta si falla antes.
        minimumByIngredientId.Clear();
        foreach (KeyValuePair<string, long> pair in previousMinimums)
        {
            minimumByIngredientId.Add(pair.Key, pair.Value);
        }
        policyRevision = previousPolicyRevision;
        initialized = previousInitialized;
        return false;
    }

    public bool TryResetPolicy(out string error)
    {
        var empty = new BistroBuilderInventoryPolicySaveData
        {
            schemaVersion = BistroBuilderInventoryPolicySaveData
                .CurrentSchemaVersion,
            restaurantId = DefaultRestaurantId,
            policyRevision = 0L
        };
        return TryReplacePolicySnapshot(empty, true, out error);
    }

    private bool RebuildState(
        out string error,
        bool publishChanges = true,
        bool incrementRevision = true
    )
    {
        error = string.Empty;

        if (inventoryService == null || !inventoryService.IsInitialized)
        {
            error = "El inventario canónico no está disponible para recalcular la planificación.";
            return false;
        }

        stockBuffer.Clear();
        lotBuffer.Clear();
        nearExpiryByIngredientId.Clear();
        inventoryService.CopyStockSnapshotsTo(stockBuffer);
        inventoryService.CopyLotSnapshotsTo(lotBuffer);

        int currentDay = Math.Max(1, generalGameStateService.DayIndex);

        // 2.2C no redefine qué significa "próximo a caducar". Consume el
        // estado autoritativo ya calculado por los lotes de 2.2A.
        for (int index = 0; index < lotBuffer.Count; index++)
        {
            BistroBuilderInventoryLotSnapshot lot = lotBuffer[index];
            if (lot.AvailableCanonicalMilliUnits <= 0L ||
                lot.FreshnessState !=
                    BistroBuilderInventoryFreshnessState.NearExpiry)
            {
                continue;
            }

            nearExpiryByIngredientId.TryGetValue(
                lot.IngredientId,
                out long current
            );
            try
            {
                nearExpiryByIngredientId[lot.IngredientId] = checked(
                    current + lot.AvailableCanonicalMilliUnits
                );
            }
            catch (OverflowException)
            {
                error = "La cantidad próxima a caducar excede el rango permitido.";
                return false;
            }
        }

        long nextRevision = runtimeRevision;
        if (incrementRevision)
        {
            nextRevision = runtimeRevision == long.MaxValue
                ? long.MaxValue
                : runtimeRevision + 1L;
        }

        var nextPlanning =
            new Dictionary<string, BistroBuilderInventoryPlanningSnapshot>(
                StringComparer.Ordinal
            );
        var nextAlerts =
            new Dictionary<string, BistroBuilderInventoryAlertSnapshot>(
                StringComparer.Ordinal
            );

        for (int index = 0; index < stockBuffer.Count; index++)
        {
            BistroBuilderInventoryStockSnapshot stock = stockBuffer[index];
            if (!recipeCatalogService.TryGetIngredient(
                    stock.IngredientId,
                    out BistroBuilderIngredientDefinition ingredient
                ) || ingredient == null)
            {
                error = "El inventario contiene un ingrediente sin definición: " +
                        stock.IngredientId + ".";
                return false;
            }

            minimumByIngredientId.TryGetValue(
                stock.IngredientId,
                out long minimum
            );
            nearExpiryByIngredientId.TryGetValue(
                stock.IngredientId,
                out long nearExpiry
            );

            BistroBuilderInventoryStockLevelState stockLevel =
                BistroBuilderInventoryPlanningMath.EvaluateStockLevel(
                    stock.AvailableCanonicalMilliUnits,
                    minimum,
                    criticalThresholdRatio
                );

            BistroBuilderInventoryForecastState forecastState =
                BistroBuilderInventoryPlanningMath.CalculateForecast(
                    stock.AvailableCanonicalMilliUnits,
                    stock.ConsumedCanonicalMilliUnits,
                    currentDay,
                    minimumHistoryDaysForForecast,
                    out int historyDays,
                    out double averageDaily,
                    out double coverageDays
                );

            var planning = new BistroBuilderInventoryPlanningSnapshot(
                stock.IngredientId,
                ingredient.DisplayName,
                stock.BaseUnit,
                stock.OnHandCanonicalMilliUnits,
                stock.ReservedCanonicalMilliUnits,
                stock.AvailableCanonicalMilliUnits,
                minimum,
                stockLevel,
                currentDay,
                stock.NextExpirationDayIndex,
                nearExpiry,
                forecastState,
                historyDays,
                averageDaily,
                coverageDays,
                nextRevision
            );
            nextPlanning.Add(stock.IngredientId, planning);

            AddStockAlertIfNeeded(planning, nextAlerts, nextRevision);
            AddExpiryAlertIfNeeded(planning, nextAlerts, nextRevision);
        }

        if (nextPlanning.Count != minimumByIngredientId.Count)
        {
            error = "La planificación no cubre exactamente los ingredientes del inventario.";
            return false;
        }

        if (publishChanges)
        {
            PublishAlertTransitions(nextAlerts);
        }

        planningByIngredientId.Clear();
        foreach (KeyValuePair<string, BistroBuilderInventoryPlanningSnapshot>
                 pair in nextPlanning)
        {
            planningByIngredientId.Add(pair.Key, pair.Value);
        }

        activeAlertsByKey.Clear();
        foreach (KeyValuePair<string, BistroBuilderInventoryAlertSnapshot>
                 pair in nextAlerts)
        {
            activeAlertsByKey.Add(pair.Key, pair.Value);
        }

        runtimeRevision = nextRevision;
        if (publishChanges)
        {
            PlanningChanged?.Invoke();
        }

        return true;
    }

    private void AddStockAlertIfNeeded(
        BistroBuilderInventoryPlanningSnapshot planning,
        Dictionary<string, BistroBuilderInventoryAlertSnapshot> destination,
        long revision
    )
    {
        // El mínimo cero desactiva deliberadamente los avisos de reposición.
        // El estado visual puede seguir siendo Sin stock, pero 2.2C no debe
        // convertir ingredientes sin política configurada en ruido de alertas.
        if (planning.MinimumStockCanonicalMilliUnits <= 0L)
        {
            return;
        }

        BistroBuilderInventoryAlertKind kind;
        BistroBuilderInventoryAlertSeverity severity;
        string label;

        switch (planning.StockLevelState)
        {
            case BistroBuilderInventoryStockLevelState.OutOfStock:
                kind = BistroBuilderInventoryAlertKind.OutOfStock;
                severity = BistroBuilderInventoryAlertSeverity.Critical;
                label = "Sin stock";
                break;

            case BistroBuilderInventoryStockLevelState.Critical:
                kind = BistroBuilderInventoryAlertKind.CriticalStock;
                severity = BistroBuilderInventoryAlertSeverity.Critical;
                label = "Stock crítico";
                break;

            case BistroBuilderInventoryStockLevelState.Low:
                kind = BistroBuilderInventoryAlertKind.LowStock;
                severity = BistroBuilderInventoryAlertSeverity.Warning;
                label = "Stock bajo";
                break;

            default:
                return;
        }

        string key = BistroBuilderInventoryPlanningMath.BuildAlertKey(
            planning.IngredientId,
            kind
        );
        destination[key] = new BistroBuilderInventoryAlertSnapshot(
            key,
            planning.IngredientId,
            kind,
            severity,
            label + ": " + planning.DisplayName + ".",
            revision
        );
    }

    private void AddExpiryAlertIfNeeded(
        BistroBuilderInventoryPlanningSnapshot planning,
        Dictionary<string, BistroBuilderInventoryAlertSnapshot> destination,
        long revision
    )
    {
        if (planning.NearExpiryAvailableCanonicalMilliUnits <= 0L ||
            planning.NextExpirationDayIndex <= planning.CurrentDayIndex)
        {
            return;
        }

        string key = BistroBuilderInventoryPlanningMath.BuildAlertKey(
            planning.IngredientId,
            BistroBuilderInventoryAlertKind.NearExpiry
        );
        destination[key] = new BistroBuilderInventoryAlertSnapshot(
            key,
            planning.IngredientId,
            BistroBuilderInventoryAlertKind.NearExpiry,
            BistroBuilderInventoryAlertSeverity.Warning,
            "Próximo a caducar: " + planning.DisplayName + ".",
            revision
        );
    }

    private void PublishAlertTransitions(
        Dictionary<string, BistroBuilderInventoryAlertSnapshot> nextAlerts
    )
    {
        foreach (KeyValuePair<string, BistroBuilderInventoryAlertSnapshot>
                 current in activeAlertsByKey)
        {
            if (!nextAlerts.ContainsKey(current.Key))
            {
                AlertCleared?.Invoke(current.Value);
            }
        }

        foreach (KeyValuePair<string, BistroBuilderInventoryAlertSnapshot>
                 next in nextAlerts)
        {
            if (!activeAlertsByKey.ContainsKey(next.Key))
            {
                AlertActivated?.Invoke(next.Value);
            }
        }
    }

    private void HandleInventoryChanged(
        BistroBuilderInventoryStockSnapshot ignoredSnapshot
    )
    {
        TryRecalculateAll(out _);
    }

    private void HandleLotChanged(
        BistroBuilderInventoryLotSnapshot ignoredSnapshot
    )
    {
        TryRecalculateAll(out _);
    }

    private void HandleTransactionRecorded(
        BistroBuilderInventoryTransactionSnapshot ignoredSnapshot
    )
    {
        // StockChanged ya cubre cantidad, pero TransactionRecorded es la
        // señal que hace explícita la actualización de la previsión por
        // consumo incluso si un futuro movimiento no alterase disponibilidad.
        TryRecalculateAll(out _);
    }

    private void HandleCalendarChanged()
    {
        TryRecalculateAll(out _);
    }

    private void HandleServiceOpeningRequested()
    {
        if (!TryEvaluateOpeningReadiness(
                out BistroBuilderInventoryOpeningReadinessSnapshot snapshot,
                out string error
            ))
        {
            Debug.LogWarning(
                "No pudo comprobarse el inventario antes de abrir: " + error,
                this
            );
            return;
        }

        OpeningReadinessEvaluated?.Invoke(snapshot);

        if (logOpeningWarnings && snapshot.HasWarnings)
        {
            Debug.LogWarning(snapshot.Summary, this);
        }
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        CacheDependenciesIfNeeded();
        if (inventoryService != null)
        {
            inventoryService.StockChanged += HandleInventoryChanged;
            inventoryService.LotChanged += HandleLotChanged;
            inventoryService.TransactionRecorded += HandleTransactionRecorded;
        }

        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged += HandleCalendarChanged;
        }

        if (serviceStateService != null)
        {
            serviceStateService.ServiceOpeningRequested +=
                HandleServiceOpeningRequested;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (inventoryService != null)
        {
            inventoryService.StockChanged -= HandleInventoryChanged;
            inventoryService.LotChanged -= HandleLotChanged;
            inventoryService.TransactionRecorded -= HandleTransactionRecorded;
        }

        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
        }

        if (serviceStateService != null)
        {
            serviceStateService.ServiceOpeningRequested -=
                HandleServiceOpeningRequested;
        }

        subscribed = false;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (inventoryService == null)
        {
            TryGetComponent(out inventoryService);
        }
        if (recipeCatalogService == null)
        {
            TryGetComponent(out recipeCatalogService);
        }
        if (generalGameStateService == null)
        {
            TryGetComponent(out generalGameStateService);
        }
        if (serviceStateService == null)
        {
            TryGetComponent(out serviceStateService);
        }
    }

    private void RemoveUnknownMinimumPolicies()
    {
        var validIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < ingredientBuffer.Count; index++)
        {
            if (ingredientBuffer[index] != null)
            {
                validIds.Add(ingredientBuffer[index].IngredientId);
            }
        }

        var keys = new List<string>(minimumByIngredientId.Keys);
        for (int index = 0; index < keys.Count; index++)
        {
            if (!validIds.Contains(keys[index]))
            {
                minimumByIngredientId.Remove(keys[index]);
            }
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        criticalThresholdRatio = Mathf.Clamp(
            criticalThresholdRatio,
            0.05f,
            0.95f
        );
        minimumHistoryDaysForForecast = Math.Max(
            1,
            minimumHistoryDaysForForecast
        );
        CacheDependenciesIfNeeded();
    }
#endif
}
