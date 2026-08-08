using System;
using System.Collections.Generic;

/// <summary>
/// Estado agregado del stock utilizable respecto al mínimo configurado.
/// No sustituye al balance autoritativo del inventario canónico.
/// </summary>
public enum BistroBuilderInventoryStockLevelState
{
    Normal = 0,
    Low = 1,
    Critical = 2,
    OutOfStock = 3
}

/// <summary>
/// Disponibilidad de la previsión básica de consumo.
/// </summary>
public enum BistroBuilderInventoryForecastState
{
    InsufficientHistory = 0,
    NoConsumption = 1,
    Available = 2
}

/// <summary>
/// Tipos de alerta derivados del inventario. Son estados deduplicados, no
/// entradas del ledger ni notificaciones persistentes.
/// </summary>
public enum BistroBuilderInventoryAlertKind
{
    LowStock = 0,
    CriticalStock = 1,
    OutOfStock = 2,
    NearExpiry = 3
}

public enum BistroBuilderInventoryAlertSeverity
{
    Information = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>
/// Lectura agregada y no autoritativa para UI, alertas y planificación.
/// Todas las cantidades proceden de BistroBuilderInventoryService.
/// </summary>
public readonly struct BistroBuilderInventoryPlanningSnapshot
{
    public string IngredientId { get; }
    public string DisplayName { get; }
    public BistroBuilderMeasurementUnit BaseUnit { get; }
    public long OnHandCanonicalMilliUnits { get; }
    public long ReservedCanonicalMilliUnits { get; }
    public long AvailableCanonicalMilliUnits { get; }
    public long MinimumStockCanonicalMilliUnits { get; }
    public BistroBuilderInventoryStockLevelState StockLevelState { get; }
    public int CurrentDayIndex { get; }
    public int NextExpirationDayIndex { get; }
    public long NearExpiryAvailableCanonicalMilliUnits { get; }
    public BistroBuilderInventoryForecastState ForecastState { get; }
    public int ConsumptionHistoryDays { get; }
    public double AverageDailyConsumptionCanonicalMilliUnits { get; }
    public double CoverageDays { get; }
    public long Revision { get; }

    public BistroBuilderInventoryPlanningSnapshot(
        string ingredientId,
        string displayName,
        BistroBuilderMeasurementUnit baseUnit,
        long onHandCanonicalMilliUnits,
        long reservedCanonicalMilliUnits,
        long availableCanonicalMilliUnits,
        long minimumStockCanonicalMilliUnits,
        BistroBuilderInventoryStockLevelState stockLevelState,
        int currentDayIndex,
        int nextExpirationDayIndex,
        long nearExpiryAvailableCanonicalMilliUnits,
        BistroBuilderInventoryForecastState forecastState,
        int consumptionHistoryDays,
        double averageDailyConsumptionCanonicalMilliUnits,
        double coverageDays,
        long revision
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
        CurrentDayIndex = currentDayIndex;
        NextExpirationDayIndex = nextExpirationDayIndex;
        NearExpiryAvailableCanonicalMilliUnits =
            nearExpiryAvailableCanonicalMilliUnits;
        ForecastState = forecastState;
        ConsumptionHistoryDays = consumptionHistoryDays;
        AverageDailyConsumptionCanonicalMilliUnits =
            averageDailyConsumptionCanonicalMilliUnits;
        CoverageDays = coverageDays;
        Revision = revision;
    }
}

/// <summary>
/// Alerta activa derivada del estado agregado. AlertKey es estable mientras
/// se mantenga el mismo tipo de alerta para el mismo ingrediente.
/// </summary>
public readonly struct BistroBuilderInventoryAlertSnapshot
{
    public string AlertKey { get; }
    public string IngredientId { get; }
    public BistroBuilderInventoryAlertKind Kind { get; }
    public BistroBuilderInventoryAlertSeverity Severity { get; }
    public string Message { get; }
    public long Revision { get; }

    public BistroBuilderInventoryAlertSnapshot(
        string alertKey,
        string ingredientId,
        BistroBuilderInventoryAlertKind kind,
        BistroBuilderInventoryAlertSeverity severity,
        string message,
        long revision
    )
    {
        AlertKey = alertKey ?? string.Empty;
        IngredientId = ingredientId ?? string.Empty;
        Kind = kind;
        Severity = severity;
        Message = message ?? string.Empty;
        Revision = revision;
    }
}

/// <summary>
/// Resultado informativo de la comprobación de inventario previa a apertura.
/// 2.2C no bloquea la apertura: informa y deja la decisión al jugador.
/// </summary>
public sealed class BistroBuilderInventoryOpeningReadinessSnapshot
{
    private readonly List<BistroBuilderInventoryAlertSnapshot> warnings;

    public int DayIndex { get; }
    public int LowStockCount { get; }
    public int CriticalStockCount { get; }
    public int OutOfStockCount { get; }
    public int NearExpiryCount { get; }
    public bool HasWarnings => warnings.Count > 0;
    public IReadOnlyList<BistroBuilderInventoryAlertSnapshot> Warnings =>
        warnings;
    public string Summary { get; }

    public BistroBuilderInventoryOpeningReadinessSnapshot(
        int dayIndex,
        int lowStockCount,
        int criticalStockCount,
        int outOfStockCount,
        int nearExpiryCount,
        List<BistroBuilderInventoryAlertSnapshot> warnings,
        string summary
    )
    {
        DayIndex = Math.Max(1, dayIndex);
        LowStockCount = Math.Max(0, lowStockCount);
        CriticalStockCount = Math.Max(0, criticalStockCount);
        OutOfStockCount = Math.Max(0, outOfStockCount);
        NearExpiryCount = Math.Max(0, nearExpiryCount);
        this.warnings = warnings != null
            ? new List<BistroBuilderInventoryAlertSnapshot>(warnings)
            : new List<BistroBuilderInventoryAlertSnapshot>();
        Summary = summary ?? string.Empty;
    }
}

/// <summary>
/// Matemática pura de 2.2C. Se mantiene separada para que los cálculos
/// puedan probarse sin GameObjects ni dependencias de presentación.
/// </summary>
public static class BistroBuilderInventoryPlanningMath
{
    public static BistroBuilderInventoryStockLevelState EvaluateStockLevel(
        long availableCanonicalMilliUnits,
        long minimumStockCanonicalMilliUnits,
        double criticalThresholdRatio
    )
    {
        long available = Math.Max(0L, availableCanonicalMilliUnits);
        long minimum = Math.Max(0L, minimumStockCanonicalMilliUnits);
        double ratio = Math.Max(0d, Math.Min(1d, criticalThresholdRatio));

        if (available <= 0L)
        {
            return BistroBuilderInventoryStockLevelState.OutOfStock;
        }

        if (minimum <= 0L || available >= minimum)
        {
            return BistroBuilderInventoryStockLevelState.Normal;
        }

        decimal criticalLimit = decimal.Floor(
            (decimal)minimum * (decimal)ratio
        );

        return available <= criticalLimit
            ? BistroBuilderInventoryStockLevelState.Critical
            : BistroBuilderInventoryStockLevelState.Low;
    }

    public static BistroBuilderInventoryForecastState CalculateForecast(
        long availableCanonicalMilliUnits,
        long consumedCanonicalMilliUnits,
        int currentDayIndex,
        int minimumHistoryDays,
        out int historyDays,
        out double averageDailyConsumptionCanonicalMilliUnits,
        out double coverageDays
    )
    {
        historyDays = Math.Max(1, currentDayIndex);
        averageDailyConsumptionCanonicalMilliUnits = 0d;
        coverageDays = -1d;

        if (currentDayIndex < Math.Max(1, minimumHistoryDays))
        {
            return BistroBuilderInventoryForecastState.InsufficientHistory;
        }

        long consumed = Math.Max(0L, consumedCanonicalMilliUnits);
        if (consumed == 0L)
        {
            return BistroBuilderInventoryForecastState.NoConsumption;
        }

        averageDailyConsumptionCanonicalMilliUnits =
            (double)consumed / historyDays;

        if (averageDailyConsumptionCanonicalMilliUnits <= 0d ||
            double.IsNaN(averageDailyConsumptionCanonicalMilliUnits) ||
            double.IsInfinity(averageDailyConsumptionCanonicalMilliUnits))
        {
            averageDailyConsumptionCanonicalMilliUnits = 0d;
            return BistroBuilderInventoryForecastState.NoConsumption;
        }

        coverageDays = Math.Max(0L, availableCanonicalMilliUnits) /
                       averageDailyConsumptionCanonicalMilliUnits;

        return BistroBuilderInventoryForecastState.Available;
    }

    public static string BuildAlertKey(
        string ingredientId,
        BistroBuilderInventoryAlertKind kind
    )
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            ingredientId
        );
        return "inventory_alert_" + normalized + "_" +
               kind.ToString().ToLowerInvariant();
    }
}
