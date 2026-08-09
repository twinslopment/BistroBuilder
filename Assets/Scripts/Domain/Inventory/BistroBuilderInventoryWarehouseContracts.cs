using System;
using System.Collections.Generic;

/// <summary>
/// Filtros funcionales de la pantalla de almacén 2.2D.
/// Presentation solicita uno de estos filtros; la clasificación procede
/// siempre de 2.2A/2.2C y no se recalcula en la UI.
/// </summary>
public enum BistroBuilderInventoryWarehouseFilter
{
    All = 0,
    LowStock = 1,
    CriticalOrOutOfStock = 2,
    NearExpiry = 3
}

/// <summary>
/// Ordenaciones deliberadamente pequeñas de la lista de ingredientes.
/// </summary>
public enum BistroBuilderInventoryWarehouseSort
{
    Name = 0,
    AvailableStock = 1,
    Status = 2,
    Expiration = 3
}

public enum BistroBuilderInventoryWarehouseSection
{
    Stock = 0,
    Alerts = 1,
    Movements = 2,
    Receipts = 3
}

/// <summary>
/// Motivo administrativo de una corrección manual. Todos se registran como
/// Correction en el ledger canónico para no convertir 2.2D en un sistema
/// avanzado de desperdicio.
/// </summary>
public enum BistroBuilderInventoryManualAdjustmentReason
{
    InventoryCorrection = 0,
    BreakageOrLoss = 1,
    ReceivingError = 2,
    Other = 3
}

/// <summary>
/// Lectura consolidada para una fila/detalle de almacén. Todas las cantidades
/// proceden de inventory.canonical v2 y toda la planificación de 2.2C.
/// </summary>
public readonly struct BistroBuilderInventoryWarehouseIngredientSnapshot
{
    public string IngredientId { get; }
    public string DisplayName { get; }
    public BistroBuilderMeasurementUnit BaseUnit { get; }
    public long OnHandCanonicalMilliUnits { get; }
    public long ReservedCanonicalMilliUnits { get; }
    public long AvailableCanonicalMilliUnits { get; }
    public long MinimumStockCanonicalMilliUnits { get; }
    public BistroBuilderInventoryStockLevelState StockLevelState { get; }
    public BistroBuilderInventoryFreshnessState FreshnessState { get; }
    public int CurrentDayIndex { get; }
    public int NextExpirationDayIndex { get; }
    public long NearExpiryAvailableCanonicalMilliUnits { get; }
    public BistroBuilderInventoryForecastState ForecastState { get; }
    public int ConsumptionHistoryDays { get; }
    public double AverageDailyConsumptionCanonicalMilliUnits { get; }
    public double CoverageDays { get; }
    public long LastReceiptSequence { get; }
    public long LastReceiptQuantityCanonicalMilliUnits { get; }
    public long LastReceiptTimestampUtcTicks { get; }
    public string LastReceiptSourceId { get; }
    public long InventoryRevision { get; }
    public long PlanningRevision { get; }

    public bool IsNearExpiry =>
        NearExpiryAvailableCanonicalMilliUnits > 0L ||
        FreshnessState == BistroBuilderInventoryFreshnessState.NearExpiry;

    public int DaysUntilNextExpiration =>
        NextExpirationDayIndex > 0
            ? NextExpirationDayIndex - Math.Max(1, CurrentDayIndex)
            : int.MaxValue;

    public BistroBuilderInventoryWarehouseIngredientSnapshot(
        string ingredientId,
        string displayName,
        BistroBuilderMeasurementUnit baseUnit,
        long onHandCanonicalMilliUnits,
        long reservedCanonicalMilliUnits,
        long availableCanonicalMilliUnits,
        long minimumStockCanonicalMilliUnits,
        BistroBuilderInventoryStockLevelState stockLevelState,
        BistroBuilderInventoryFreshnessState freshnessState,
        int currentDayIndex,
        int nextExpirationDayIndex,
        long nearExpiryAvailableCanonicalMilliUnits,
        BistroBuilderInventoryForecastState forecastState,
        int consumptionHistoryDays,
        double averageDailyConsumptionCanonicalMilliUnits,
        double coverageDays,
        long lastReceiptSequence,
        long lastReceiptQuantityCanonicalMilliUnits,
        long lastReceiptTimestampUtcTicks,
        string lastReceiptSourceId,
        long inventoryRevision,
        long planningRevision
    )
    {
        IngredientId = ingredientId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        BaseUnit = baseUnit;
        OnHandCanonicalMilliUnits = onHandCanonicalMilliUnits;
        ReservedCanonicalMilliUnits = reservedCanonicalMilliUnits;
        AvailableCanonicalMilliUnits = availableCanonicalMilliUnits;
        MinimumStockCanonicalMilliUnits = minimumStockCanonicalMilliUnits;
        StockLevelState = stockLevelState;
        FreshnessState = freshnessState;
        CurrentDayIndex = Math.Max(1, currentDayIndex);
        NextExpirationDayIndex = nextExpirationDayIndex;
        NearExpiryAvailableCanonicalMilliUnits =
            Math.Max(0L, nearExpiryAvailableCanonicalMilliUnits);
        ForecastState = forecastState;
        ConsumptionHistoryDays = Math.Max(0, consumptionHistoryDays);
        AverageDailyConsumptionCanonicalMilliUnits =
            Math.Max(0d, averageDailyConsumptionCanonicalMilliUnits);
        CoverageDays = coverageDays;
        LastReceiptSequence = Math.Max(0L, lastReceiptSequence);
        LastReceiptQuantityCanonicalMilliUnits =
            Math.Max(0L, lastReceiptQuantityCanonicalMilliUnits);
        LastReceiptTimestampUtcTicks =
            Math.Max(0L, lastReceiptTimestampUtcTicks);
        LastReceiptSourceId = lastReceiptSourceId ?? string.Empty;
        InventoryRevision = Math.Max(0L, inventoryRevision);
        PlanningRevision = Math.Max(0L, planningRevision);
    }
}

public readonly struct BistroBuilderInventoryWarehouseSummarySnapshot
{
    public int IngredientCount { get; }
    public int LowStockCount { get; }
    public int CriticalCount { get; }
    public int OutOfStockCount { get; }
    public int NearExpiryCount { get; }
    public int ActiveAlertCount { get; }
    public long Revision { get; }

    public BistroBuilderInventoryWarehouseSummarySnapshot(
        int ingredientCount,
        int lowStockCount,
        int criticalCount,
        int outOfStockCount,
        int nearExpiryCount,
        int activeAlertCount,
        long revision
    )
    {
        IngredientCount = Math.Max(0, ingredientCount);
        LowStockCount = Math.Max(0, lowStockCount);
        CriticalCount = Math.Max(0, criticalCount);
        OutOfStockCount = Math.Max(0, outOfStockCount);
        NearExpiryCount = Math.Max(0, nearExpiryCount);
        ActiveAlertCount = Math.Max(0, activeAlertCount);
        Revision = Math.Max(0L, revision);
    }
}

/// <summary>
/// Movimiento traducible a una fila jugable. Se conserva la identidad del
/// movimiento canónico para trazabilidad, pero Presentation no necesita
/// interpretar el ledger directamente.
/// </summary>
public readonly struct BistroBuilderInventoryWarehouseMovementSnapshot
{
    public long Sequence { get; }
    public string TransactionId { get; }
    public string OperationId { get; }
    public string IngredientId { get; }
    public string IngredientDisplayName { get; }
    public BistroBuilderMeasurementUnit BaseUnit { get; }
    public BistroBuilderInventoryTransactionType TransactionType { get; }
    public long OnHandDeltaCanonicalMilliUnits { get; }
    public long ReservedDeltaCanonicalMilliUnits { get; }
    public string SourceId { get; }
    public string Reason { get; }
    public long TimestampUtcTicks { get; }

    public BistroBuilderInventoryWarehouseMovementSnapshot(
        long sequence,
        string transactionId,
        string operationId,
        string ingredientId,
        string ingredientDisplayName,
        BistroBuilderMeasurementUnit baseUnit,
        BistroBuilderInventoryTransactionType transactionType,
        long onHandDeltaCanonicalMilliUnits,
        long reservedDeltaCanonicalMilliUnits,
        string sourceId,
        string reason,
        long timestampUtcTicks
    )
    {
        Sequence = Math.Max(0L, sequence);
        TransactionId = transactionId ?? string.Empty;
        OperationId = operationId ?? string.Empty;
        IngredientId = ingredientId ?? string.Empty;
        IngredientDisplayName = ingredientDisplayName ?? string.Empty;
        BaseUnit = baseUnit;
        TransactionType = transactionType;
        OnHandDeltaCanonicalMilliUnits = onHandDeltaCanonicalMilliUnits;
        ReservedDeltaCanonicalMilliUnits = reservedDeltaCanonicalMilliUnits;
        SourceId = sourceId ?? string.Empty;
        Reason = reason ?? string.Empty;
        TimestampUtcTicks = Math.Max(0L, timestampUtcTicks);
    }
}

public readonly struct BistroBuilderInventoryWarehouseReceiptLineSnapshot
{
    public string IngredientId { get; }
    public string IngredientDisplayName { get; }
    public BistroBuilderMeasurementUnit BaseUnit { get; }
    public long CanonicalMilliUnits { get; }

    public BistroBuilderInventoryWarehouseReceiptLineSnapshot(
        string ingredientId,
        string ingredientDisplayName,
        BistroBuilderMeasurementUnit baseUnit,
        long canonicalMilliUnits
    )
    {
        IngredientId = ingredientId ?? string.Empty;
        IngredientDisplayName = ingredientDisplayName ?? string.Empty;
        BaseUnit = baseUnit;
        CanonicalMilliUnits = Math.Max(0L, canonicalMilliUnits);
    }
}

/// <summary>
/// Recepción reconstruida del ledger Purchase. No constituye una segunda
/// persistencia: OperationId agrupa las líneas de la recepción 2.2B.
/// </summary>
public sealed class BistroBuilderInventoryWarehouseReceiptSnapshot
{
    private readonly List<BistroBuilderInventoryWarehouseReceiptLineSnapshot>
        lines;

    public string ReceiptId { get; }
    public string SourceId { get; }
    public long FirstSequence { get; }
    public long LastSequence { get; }
    public long TimestampUtcTicks { get; }
    public IReadOnlyList<BistroBuilderInventoryWarehouseReceiptLineSnapshot>
        Lines => lines;

    public BistroBuilderInventoryWarehouseReceiptSnapshot(
        string receiptId,
        string sourceId,
        long firstSequence,
        long lastSequence,
        long timestampUtcTicks,
        List<BistroBuilderInventoryWarehouseReceiptLineSnapshot> lines
    )
    {
        ReceiptId = receiptId ?? string.Empty;
        SourceId = sourceId ?? string.Empty;
        FirstSequence = Math.Max(0L, firstSequence);
        LastSequence = Math.Max(0L, lastSequence);
        TimestampUtcTicks = Math.Max(0L, timestampUtcTicks);
        this.lines = lines != null
            ? new List<BistroBuilderInventoryWarehouseReceiptLineSnapshot>(lines)
            : new List<BistroBuilderInventoryWarehouseReceiptLineSnapshot>();
    }
}

/// <summary>
/// Reglas puras de consulta 2.2D. Permiten probar filtros/ordenación sin
/// levantar escena ni duplicar lógica en Presentation.
/// </summary>
public static class BistroBuilderInventoryWarehouseQueryUtility
{
    public static bool MatchesFilter(
        BistroBuilderInventoryWarehouseIngredientSnapshot item,
        BistroBuilderInventoryWarehouseFilter filter
    )
    {
        switch (filter)
        {
            case BistroBuilderInventoryWarehouseFilter.LowStock:
                return item.StockLevelState ==
                           BistroBuilderInventoryStockLevelState.Low ||
                       item.StockLevelState ==
                           BistroBuilderInventoryStockLevelState.Critical ||
                       item.StockLevelState ==
                           BistroBuilderInventoryStockLevelState.OutOfStock;

            case BistroBuilderInventoryWarehouseFilter.CriticalOrOutOfStock:
                return item.StockLevelState ==
                           BistroBuilderInventoryStockLevelState.Critical ||
                       item.StockLevelState ==
                           BistroBuilderInventoryStockLevelState.OutOfStock;

            case BistroBuilderInventoryWarehouseFilter.NearExpiry:
                return item.IsNearExpiry;

            default:
                return true;
        }
    }

    public static int Compare(
        BistroBuilderInventoryWarehouseIngredientSnapshot a,
        BistroBuilderInventoryWarehouseIngredientSnapshot b,
        BistroBuilderInventoryWarehouseSort sort
    )
    {
        int value;
        switch (sort)
        {
            case BistroBuilderInventoryWarehouseSort.AvailableStock:
                value = a.AvailableCanonicalMilliUnits.CompareTo(
                    b.AvailableCanonicalMilliUnits
                );
                break;

            case BistroBuilderInventoryWarehouseSort.Status:
                value = GetStatusRank(b).CompareTo(GetStatusRank(a));
                break;

            case BistroBuilderInventoryWarehouseSort.Expiration:
                value = NormalizeExpiration(a.NextExpirationDayIndex)
                    .CompareTo(NormalizeExpiration(b.NextExpirationDayIndex));
                break;

            default:
                value = string.Compare(
                    a.DisplayName,
                    b.DisplayName,
                    StringComparison.CurrentCultureIgnoreCase
                );
                break;
        }

        if (value != 0)
        {
            return value;
        }

        return string.Compare(
            a.IngredientId,
            b.IngredientId,
            StringComparison.Ordinal
        );
    }

    public static bool IsPlayerFacingMovement(
        BistroBuilderInventoryTransactionType type
    )
    {
        return type != BistroBuilderInventoryTransactionType.Reservation &&
               type !=
                   BistroBuilderInventoryTransactionType.ReservationRelease;
    }

    private static int GetStatusRank(
        BistroBuilderInventoryWarehouseIngredientSnapshot item
    )
    {
        int stockRank;
        switch (item.StockLevelState)
        {
            case BistroBuilderInventoryStockLevelState.OutOfStock:
                stockRank = 400;
                break;
            case BistroBuilderInventoryStockLevelState.Critical:
                stockRank = 300;
                break;
            case BistroBuilderInventoryStockLevelState.Low:
                stockRank = 200;
                break;
            default:
                stockRank = 0;
                break;
        }

        return stockRank + (item.IsNearExpiry ? 50 : 0);
    }

    private static int NormalizeExpiration(int dayIndex)
    {
        return dayIndex > 0 ? dayIndex : int.MaxValue;
    }
}
