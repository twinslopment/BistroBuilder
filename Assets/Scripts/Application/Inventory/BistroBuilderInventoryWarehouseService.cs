using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada de aplicación de Inventario/Almacén 2.2D.
///
/// No posee stock, lotes, reservas, mínimos ni previsiones. Compone lecturas
/// de los servicios ya validados y canaliza comandos de jugador hacia sus
/// autoridades canónicas. Presentation nunca modifica diccionarios ni campos
/// de inventario directamente.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Inventory/Inventory Warehouse Service 2.2D")]
public sealed class BistroBuilderInventoryWarehouseService : MonoBehaviour
{
    public const string ManualAdjustmentSourceId = "inventory_ui";

    [SerializeField]
    private BistroBuilderInventoryService inventoryService;

    [SerializeField]
    private BistroBuilderInventoryPlanningService planningService;

    [SerializeField]
    private BistroBuilderRecipeCatalogService recipeCatalogService;

    [SerializeField]
    private BistroBuilderGoodsReceivingService goodsReceivingService;

    private readonly List<BistroBuilderInventoryWarehouseIngredientSnapshot>
        ingredientCache =
            new List<BistroBuilderInventoryWarehouseIngredientSnapshot>(64);

    private readonly List<BistroBuilderInventoryWarehouseMovementSnapshot>
        movementCache =
            new List<BistroBuilderInventoryWarehouseMovementSnapshot>(256);

    private readonly List<BistroBuilderInventoryWarehouseReceiptSnapshot>
        receiptCache =
            new List<BistroBuilderInventoryWarehouseReceiptSnapshot>(64);

    private readonly List<BistroBuilderInventoryPlanningSnapshot>
        planningBuffer = new List<BistroBuilderInventoryPlanningSnapshot>(64);

    private readonly List<BistroBuilderInventoryStockSnapshot>
        stockBuffer = new List<BistroBuilderInventoryStockSnapshot>(64);

    private readonly List<BistroBuilderInventoryTransactionSnapshot>
        transactionBuffer =
            new List<BistroBuilderInventoryTransactionSnapshot>(512);

    private readonly Dictionary<string, BistroBuilderInventoryStockSnapshot>
        stockByIngredientId =
            new Dictionary<string, BistroBuilderInventoryStockSnapshot>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, ReceiptBuilder> receiptBuilders =
        new Dictionary<string, ReceiptBuilder>(StringComparer.Ordinal);

    private bool subscribed;
    private bool cacheDirty = true;
    private long composedRevision;

    public event Action DataChanged;

    public BistroBuilderInventoryService InventoryService => inventoryService;
    public BistroBuilderInventoryPlanningService PlanningService =>
        planningService;
    public BistroBuilderRecipeCatalogService RecipeCatalogService =>
        recipeCatalogService;
    public BistroBuilderGoodsReceivingService GoodsReceivingService =>
        goodsReceivingService;
    public long ComposedRevision => composedRevision;

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
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
            enabled = false;
            return;
        }

        MarkDirtyAndPublish(false);
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

        if (planningService == null)
        {
            error = "Falta BistroBuilderInventoryPlanningService 2.2C.";
            return false;
        }

        if (recipeCatalogService == null)
        {
            error = "Falta BistroBuilderRecipeCatalogService.";
            return false;
        }

        if (goodsReceivingService == null)
        {
            error = "Falta BistroBuilderGoodsReceivingService 2.2B.";
            return false;
        }

        if (!inventoryService.ValidateConfiguration(out error) ||
            !planningService.ValidateConfiguration(out error) ||
            !recipeCatalogService.ValidateConfiguration(out error) ||
            !goodsReceivingService.ValidateConfiguration(out error))
        {
            return false;
        }

        return true;
    }

    public bool EnsureReady(out string error)
    {
        error = string.Empty;
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!inventoryService.IsInitialized)
        {
            error = "El inventario canónico todavía no está inicializado.";
            return false;
        }

        if (!planningService.EnsureInitialized(out error))
        {
            return false;
        }

        return EnsureCache(out error);
    }

    public bool TryGetSummary(
        out BistroBuilderInventoryWarehouseSummarySnapshot summary,
        out string error
    )
    {
        summary = default;
        error = string.Empty;

        if (!EnsureReady(out error))
        {
            return false;
        }

        int low = 0;
        int critical = 0;
        int outOfStock = 0;
        int nearExpiry = 0;

        for (int index = 0; index < ingredientCache.Count; index++)
        {
            BistroBuilderInventoryWarehouseIngredientSnapshot item =
                ingredientCache[index];
            switch (item.StockLevelState)
            {
                case BistroBuilderInventoryStockLevelState.Low:
                    low++;
                    break;
                case BistroBuilderInventoryStockLevelState.Critical:
                    critical++;
                    break;
                case BistroBuilderInventoryStockLevelState.OutOfStock:
                    outOfStock++;
                    break;
            }

            if (item.IsNearExpiry)
            {
                nearExpiry++;
            }
        }

        summary = new BistroBuilderInventoryWarehouseSummarySnapshot(
            ingredientCache.Count,
            low,
            critical,
            outOfStock,
            nearExpiry,
            planningService.ActiveAlertCount,
            composedRevision
        );
        return true;
    }

    public bool TryGetIngredient(
        string ingredientId,
        out BistroBuilderInventoryWarehouseIngredientSnapshot snapshot,
        out string error
    )
    {
        snapshot = default;
        error = string.Empty;

        if (!EnsureReady(out error))
        {
            return false;
        }

        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            ingredientId
        );
        for (int index = 0; index < ingredientCache.Count; index++)
        {
            if (string.Equals(
                    ingredientCache[index].IngredientId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                snapshot = ingredientCache[index];
                return true;
            }
        }

        error = "El ingrediente no existe en la lectura de almacén: " +
                normalized + ".";
        return false;
    }

    public bool CopyIngredientsTo(
        List<BistroBuilderInventoryWarehouseIngredientSnapshot> destination,
        BistroBuilderInventoryWarehouseFilter filter,
        BistroBuilderInventoryWarehouseSort sort,
        string searchText,
        out string error
    )
    {
        error = string.Empty;
        if (destination == null)
        {
            error = "La lista de destino de ingredientes es nula.";
            return false;
        }

        destination.Clear();
        if (!EnsureReady(out error))
        {
            return false;
        }

        string search = !string.IsNullOrWhiteSpace(searchText)
            ? searchText.Trim()
            : string.Empty;

        for (int index = 0; index < ingredientCache.Count; index++)
        {
            BistroBuilderInventoryWarehouseIngredientSnapshot item =
                ingredientCache[index];
            if (!BistroBuilderInventoryWarehouseQueryUtility.MatchesFilter(item, filter) ||
                (!string.IsNullOrEmpty(search) &&
                 item.DisplayName.IndexOf(
                     search,
                     StringComparison.OrdinalIgnoreCase
                 ) < 0 &&
                 item.IngredientId.IndexOf(
                     search,
                     StringComparison.OrdinalIgnoreCase
                 ) < 0))
            {
                continue;
            }

            destination.Add(item);
        }

        destination.Sort((a, b) => BistroBuilderInventoryWarehouseQueryUtility.Compare(a, b, sort));
        return true;
    }

    public bool CopyMovementsTo(
        List<BistroBuilderInventoryWarehouseMovementSnapshot> destination,
        int maximumRows,
        bool includeReservationNoise,
        out string error
    )
    {
        error = string.Empty;
        if (destination == null)
        {
            error = "La lista de destino de movimientos es nula.";
            return false;
        }

        destination.Clear();
        if (!EnsureReady(out error))
        {
            return false;
        }

        int maximum = Mathf.Clamp(maximumRows, 1, 1000);
        for (int index = movementCache.Count - 1;
             index >= 0 && destination.Count < maximum;
             index--)
        {
            BistroBuilderInventoryWarehouseMovementSnapshot item =
                movementCache[index];
            if (!includeReservationNoise &&
                !BistroBuilderInventoryWarehouseQueryUtility.IsPlayerFacingMovement(
                    item.TransactionType
                ))
            {
                continue;
            }

            destination.Add(item);
        }
        return true;
    }

    public bool CopyReceiptsTo(
        List<BistroBuilderInventoryWarehouseReceiptSnapshot> destination,
        int maximumRows,
        out string error
    )
    {
        error = string.Empty;
        if (destination == null)
        {
            error = "La lista de destino de recepciones es nula.";
            return false;
        }

        destination.Clear();
        if (!EnsureReady(out error))
        {
            return false;
        }

        int maximum = Mathf.Clamp(maximumRows, 1, 500);
        for (int index = receiptCache.Count - 1;
             index >= 0 && destination.Count < maximum;
             index--)
        {
            destination.Add(receiptCache[index]);
        }
        return true;
    }

    public bool CopyAlertsTo(
        List<BistroBuilderInventoryAlertSnapshot> destination,
        out string error
    )
    {
        error = string.Empty;
        if (destination == null)
        {
            error = "La lista de destino de alertas es nula.";
            return false;
        }

        if (!EnsureReady(out error))
        {
            destination.Clear();
            return false;
        }

        planningService.CopyActiveAlertsTo(destination);
        return true;
    }

    public bool TrySetMinimumStock(
        string ingredientId,
        long minimumCanonicalMilliUnits,
        out string error
    )
    {
        error = string.Empty;
        if (!EnsureReady(out error))
        {
            return false;
        }

        if (!planningService.TrySetMinimumStock(
                ingredientId,
                minimumCanonicalMilliUnits,
                out error
            ))
        {
            return false;
        }

        MarkDirtyAndPublish();
        return true;
    }

    /// <summary>
    /// Ajuste administrativo simple. La UI solicita un delta; esta fachada
    /// calcula el nuevo conteo físico y delega en TryCorrectOnHand para que
    /// lotes, ledger, persistencia, alertas y disponibilidad sigan la ruta
    /// canónica existente.
    /// </summary>
    public bool TryAdjustStock(
        string ingredientId,
        long deltaCanonicalMilliUnits,
        BistroBuilderInventoryManualAdjustmentReason reason,
        string optionalNote,
        out string operationId,
        out string error
    )
    {
        operationId = string.Empty;
        error = string.Empty;

        if (!EnsureReady(out error))
        {
            return false;
        }

        if (deltaCanonicalMilliUnits == 0L ||
            deltaCanonicalMilliUnits == long.MinValue ||
            Math.Abs(deltaCanonicalMilliUnits) >
                BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
        {
            error = "El ajuste debe tener una cantidad distinta de cero y válida.";
            return false;
        }

        if (!Enum.IsDefined(
                typeof(BistroBuilderInventoryManualAdjustmentReason),
                reason
            ))
        {
            error = "El motivo del ajuste es desconocido.";
            return false;
        }

        if (!inventoryService.TryGetStockSnapshot(
                ingredientId,
                out BistroBuilderInventoryStockSnapshot stock
            ))
        {
            error = "No se encontró el balance canónico del ingrediente.";
            return false;
        }

        long target;
        try
        {
            target = checked(
                stock.OnHandCanonicalMilliUnits + deltaCanonicalMilliUnits
            );
        }
        catch (OverflowException)
        {
            error = "El ajuste excede el rango permitido.";
            return false;
        }

        if (target < 0L)
        {
            error = "El ajuste no puede dejar existencias físicas negativas.";
            return false;
        }

        if (target < stock.ReservedCanonicalMilliUnits)
        {
            error = "No puede reducirse el stock por debajo de la cantidad ya reservada.";
            return false;
        }

        operationId = "ui_adjust_" + Guid.NewGuid().ToString("N");
        string reasonText = BuildAdjustmentReason(reason, optionalNote);

        if (!inventoryService.TryCorrectOnHand(
                operationId,
                ManualAdjustmentSourceId,
                ingredientId,
                target,
                reasonText,
                out error
            ))
        {
            operationId = string.Empty;
            return false;
        }

        MarkDirtyAndPublish();
        return true;
    }

    public bool TryEvaluateOpeningReadiness(
        out BistroBuilderInventoryOpeningReadinessSnapshot snapshot,
        out string error
    )
    {
        snapshot = null;
        error = string.Empty;
        return EnsureReady(out error) &&
               planningService.TryEvaluateOpeningReadiness(
                   out snapshot,
                   out error
               );
    }

    public bool ValidateReadModel(out string error)
    {
        error = string.Empty;
        if (!EnsureReady(out error))
        {
            return false;
        }

        if (ingredientCache.Count != inventoryService.StockEntryCount ||
            ingredientCache.Count != planningService.IngredientCount)
        {
            error = "La lectura 2.2D no contiene exactamente los ingredientes del inventario/planning.";
            return false;
        }

        for (int index = 0; index < ingredientCache.Count; index++)
        {
            BistroBuilderInventoryWarehouseIngredientSnapshot item =
                ingredientCache[index];
            if (item.AvailableCanonicalMilliUnits !=
                    item.OnHandCanonicalMilliUnits -
                    item.ReservedCanonicalMilliUnits ||
                item.NearExpiryAvailableCanonicalMilliUnits < 0L ||
                item.NearExpiryAvailableCanonicalMilliUnits >
                    item.AvailableCanonicalMilliUnits)
            {
                error = "La lectura agregada es incoherente para " +
                        item.IngredientId + ".";
                return false;
            }
        }

        return true;
    }

    private bool EnsureCache(out string error)
    {
        error = string.Empty;
        if (!cacheDirty)
        {
            return true;
        }

        try
        {
            RebuildCache();
            cacheDirty = false;
            composedRevision = composedRevision == long.MaxValue
                ? long.MaxValue
                : composedRevision + 1L;
            return true;
        }
        catch (Exception exception)
        {
            error = "No se pudo reconstruir la lectura de almacén: " +
                    exception.Message;
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void RebuildCache()
    {
        ingredientCache.Clear();
        movementCache.Clear();
        receiptCache.Clear();
        stockByIngredientId.Clear();
        receiptBuilders.Clear();

        inventoryService.CopyStockSnapshotsTo(stockBuffer);
        for (int index = 0; index < stockBuffer.Count; index++)
        {
            stockByIngredientId[stockBuffer[index].IngredientId] =
                stockBuffer[index];
        }

        inventoryService.CopyTransactionsTo(transactionBuffer);
        BuildMovementAndReceiptCaches();

        planningService.CopyPlanningSnapshotsTo(planningBuffer);
        for (int index = 0; index < planningBuffer.Count; index++)
        {
            BistroBuilderInventoryPlanningSnapshot planning =
                planningBuffer[index];
            if (!stockByIngredientId.TryGetValue(
                    planning.IngredientId,
                    out BistroBuilderInventoryStockSnapshot stock
                ))
            {
                continue;
            }

            long lastReceiptSequence = 0L;
            long lastReceiptQuantity = 0L;
            long lastReceiptTicks = 0L;
            string lastReceiptSource = string.Empty;
            FindLatestReceiptForIngredient(
                planning.IngredientId,
                ref lastReceiptSequence,
                ref lastReceiptQuantity,
                ref lastReceiptTicks,
                ref lastReceiptSource
            );

            ingredientCache.Add(
                new BistroBuilderInventoryWarehouseIngredientSnapshot(
                    planning.IngredientId,
                    planning.DisplayName,
                    planning.BaseUnit,
                    stock.OnHandCanonicalMilliUnits,
                    stock.ReservedCanonicalMilliUnits,
                    stock.AvailableCanonicalMilliUnits,
                    planning.MinimumStockCanonicalMilliUnits,
                    planning.StockLevelState,
                    stock.FreshnessState,
                    planning.CurrentDayIndex,
                    planning.NextExpirationDayIndex,
                    planning.NearExpiryAvailableCanonicalMilliUnits,
                    planning.ForecastState,
                    planning.ConsumptionHistoryDays,
                    planning.AverageDailyConsumptionCanonicalMilliUnits,
                    planning.CoverageDays,
                    lastReceiptSequence,
                    lastReceiptQuantity,
                    lastReceiptTicks,
                    lastReceiptSource,
                    stock.Revision,
                    planning.Revision
                )
            );
        }
    }

    private void BuildMovementAndReceiptCaches()
    {
        for (int index = 0; index < transactionBuffer.Count; index++)
        {
            BistroBuilderInventoryTransactionSnapshot transaction =
                transactionBuffer[index];
            ResolveIngredientPresentation(
                transaction.IngredientId,
                out string displayName,
                out BistroBuilderMeasurementUnit baseUnit
            );

            movementCache.Add(
                new BistroBuilderInventoryWarehouseMovementSnapshot(
                    transaction.Sequence,
                    transaction.TransactionId,
                    transaction.OperationId,
                    transaction.IngredientId,
                    displayName,
                    baseUnit,
                    transaction.TransactionType,
                    transaction.OnHandDeltaCanonicalMilliUnits,
                    transaction.ReservedDeltaCanonicalMilliUnits,
                    transaction.SourceId,
                    transaction.Reason,
                    transaction.TimestampUtcTicks
                )
            );

            if (transaction.TransactionType !=
                    BistroBuilderInventoryTransactionType.Purchase)
            {
                continue;
            }

            string receiptId = !string.IsNullOrWhiteSpace(
                    transaction.OperationId
                )
                ? transaction.OperationId
                : transaction.TransactionId;
            if (!receiptBuilders.TryGetValue(
                    receiptId,
                    out ReceiptBuilder builder
                ))
            {
                builder = new ReceiptBuilder(
                    receiptId,
                    transaction.SourceId,
                    transaction.Sequence,
                    transaction.TimestampUtcTicks
                );
                receiptBuilders.Add(receiptId, builder);
            }

            builder.Add(
                transaction.Sequence,
                transaction.TimestampUtcTicks,
                new BistroBuilderInventoryWarehouseReceiptLineSnapshot(
                    transaction.IngredientId,
                    displayName,
                    baseUnit,
                    Math.Max(
                        0L,
                        transaction.OnHandDeltaCanonicalMilliUnits
                    )
                )
            );
        }

        var builders = new List<ReceiptBuilder>(receiptBuilders.Values);
        builders.Sort((a, b) => a.FirstSequence.CompareTo(b.FirstSequence));
        for (int index = 0; index < builders.Count; index++)
        {
            receiptCache.Add(builders[index].Build());
        }
    }

    private void FindLatestReceiptForIngredient(
        string ingredientId,
        ref long sequence,
        ref long quantity,
        ref long timestampTicks,
        ref string sourceId
    )
    {
        for (int index = movementCache.Count - 1; index >= 0; index--)
        {
            BistroBuilderInventoryWarehouseMovementSnapshot movement =
                movementCache[index];
            if (movement.TransactionType !=
                    BistroBuilderInventoryTransactionType.Purchase ||
                !string.Equals(
                    movement.IngredientId,
                    ingredientId,
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            sequence = movement.Sequence;
            quantity = Math.Max(0L, movement.OnHandDeltaCanonicalMilliUnits);
            timestampTicks = movement.TimestampUtcTicks;
            sourceId = movement.SourceId;
            return;
        }
    }

    private void ResolveIngredientPresentation(
        string ingredientId,
        out string displayName,
        out BistroBuilderMeasurementUnit baseUnit
    )
    {
        displayName = ingredientId ?? string.Empty;
        baseUnit = BistroBuilderMeasurementUnit.Unit;

        if (recipeCatalogService != null &&
            recipeCatalogService.TryGetIngredient(
                ingredientId,
                out BistroBuilderIngredientDefinition ingredient
            ) && ingredient != null)
        {
            displayName = ingredient.DisplayName;
            baseUnit = ingredient.BaseUnit;
        }
    }

    private static string BuildAdjustmentReason(
        BistroBuilderInventoryManualAdjustmentReason reason,
        string optionalNote
    )
    {
        string label;
        switch (reason)
        {
            case BistroBuilderInventoryManualAdjustmentReason.BreakageOrLoss:
                label = "Rotura/pérdida";
                break;
            case BistroBuilderInventoryManualAdjustmentReason.ReceivingError:
                label = "Error de recepción";
                break;
            case BistroBuilderInventoryManualAdjustmentReason.Other:
                label = "Otro";
                break;
            default:
                label = "Corrección de inventario";
                break;
        }

        string note = !string.IsNullOrWhiteSpace(optionalNote)
            ? optionalNote.Trim()
            : string.Empty;
        if (note.Length > 160)
        {
            note = note.Substring(0, 160);
        }

        return string.IsNullOrEmpty(note)
            ? "Ajuste manual: " + label + "."
            : "Ajuste manual: " + label + ". " + note;
    }

    private void HandlePlanningChanged()
    {
        MarkDirtyAndPublish();
    }

    private void HandleTransactionRecorded(
        BistroBuilderInventoryTransactionSnapshot ignored
    )
    {
        MarkDirtyAndPublish();
    }

    private void HandleReceiptAccepted(BistroBuilderGoodsReceiptSnapshot ignored)
    {
        MarkDirtyAndPublish();
    }

    private void MarkDirtyAndPublish(bool publish = true)
    {
        cacheDirty = true;
        if (publish)
        {
            DataChanged?.Invoke();
        }
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        CacheDependenciesIfNeeded();
        if (planningService != null)
        {
            planningService.PlanningChanged += HandlePlanningChanged;
        }

        if (inventoryService != null)
        {
            inventoryService.TransactionRecorded += HandleTransactionRecorded;
        }

        if (goodsReceivingService != null)
        {
            goodsReceivingService.ReceiptAccepted += HandleReceiptAccepted;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (planningService != null)
        {
            planningService.PlanningChanged -= HandlePlanningChanged;
        }

        if (inventoryService != null)
        {
            inventoryService.TransactionRecorded -= HandleTransactionRecorded;
        }

        if (goodsReceivingService != null)
        {
            goodsReceivingService.ReceiptAccepted -= HandleReceiptAccepted;
        }

        subscribed = false;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (inventoryService == null)
        {
            TryGetComponent(out inventoryService);
        }
        if (planningService == null)
        {
            TryGetComponent(out planningService);
        }
        if (recipeCatalogService == null)
        {
            TryGetComponent(out recipeCatalogService);
        }
        if (goodsReceivingService == null)
        {
            TryGetComponent(out goodsReceivingService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif

    private sealed class ReceiptBuilder
    {
        private readonly List<BistroBuilderInventoryWarehouseReceiptLineSnapshot>
            lines = new List<BistroBuilderInventoryWarehouseReceiptLineSnapshot>();

        public string ReceiptId { get; }
        public string SourceId { get; }
        public long FirstSequence { get; private set; }
        public long LastSequence { get; private set; }
        public long TimestampUtcTicks { get; private set; }

        public ReceiptBuilder(
            string receiptId,
            string sourceId,
            long sequence,
            long timestampUtcTicks
        )
        {
            ReceiptId = receiptId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            FirstSequence = Math.Max(0L, sequence);
            LastSequence = Math.Max(0L, sequence);
            TimestampUtcTicks = Math.Max(0L, timestampUtcTicks);
        }

        public void Add(
            long sequence,
            long timestampUtcTicks,
            BistroBuilderInventoryWarehouseReceiptLineSnapshot line
        )
        {
            FirstSequence = Math.Min(FirstSequence, Math.Max(0L, sequence));
            LastSequence = Math.Max(LastSequence, Math.Max(0L, sequence));
            TimestampUtcTicks = Math.Max(
                TimestampUtcTicks,
                Math.Max(0L, timestampUtcTicks)
            );
            lines.Add(line);
        }

        public BistroBuilderInventoryWarehouseReceiptSnapshot Build()
        {
            return new BistroBuilderInventoryWarehouseReceiptSnapshot(
                ReceiptId,
                SourceId,
                FirstSequence,
                LastSequence,
                TimestampUtcTicks,
                lines
            );
        }
    }
}
