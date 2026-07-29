using System;
using System.Collections.Generic;

/// <summary>
/// Tipos de movimiento autoritativos del libro de inventario.
///
/// Cada modificación de existencias debe quedar representada por uno de
/// estos movimientos. El libro es append-only durante la sesión runtime.
/// </summary>
public enum BistroBuilderInventoryTransactionType
{
    InitialStock = 0,
    Purchase = 1,
    Reservation = 2,
    ReservationRelease = 3,
    Consumption = 4,
    Waste = 5,
    Correction = 6
}

/// <summary>
/// Ciclo de vida de una reserva atómica de ingredientes.
/// </summary>
public enum BistroBuilderInventoryReservationStatus
{
    Active = 0,
    Released = 1,
    Consumed = 2
}

/// <summary>
/// Identidades estables de almacenamiento inicial.
///
/// 368B utiliza una ubicación agregada por familia. Los lotes, cámaras y
/// ubicaciones físicas concretas podrán especializar estos IDs más adelante
/// sin cambiar el contrato del stock.
/// </summary>
public static class BistroBuilderInventoryStorageLocationIds
{
    public const string DryStorage = "storage_dry";
    public const string Refrigerated = "storage_refrigerated";
    public const string Frozen = "storage_frozen";
    public const string BeverageCellar = "storage_beverage_cellar";
    public const string Ambient = "storage_ambient";

    public static string FromIngredientStorage(
        BistroBuilderIngredientStorageType storageType
    )
    {
        switch (storageType)
        {
            case BistroBuilderIngredientStorageType.DryStorage:
                return DryStorage;

            case BistroBuilderIngredientStorageType.Refrigerated:
                return Refrigerated;

            case BistroBuilderIngredientStorageType.Frozen:
                return Frozen;

            case BistroBuilderIngredientStorageType.BeverageCellar:
                return BeverageCellar;

            case BistroBuilderIngredientStorageType.Ambient:
            default:
                return Ambient;
        }
    }
}

/// <summary>
/// Línea canónica utilizada para solicitar reservas transaccionales.
/// La cantidad ya está normalizada a milésimas de la unidad base.
/// </summary>
[Serializable]
public sealed class BistroBuilderInventoryQuantityLine
{
    private readonly string ingredientId;
    private readonly long canonicalMilliUnits;

    public string IngredientId => ingredientId;

    public long CanonicalMilliUnits => canonicalMilliUnits;

    public BistroBuilderInventoryQuantityLine(
        string ingredientId,
        long canonicalMilliUnits
    )
    {
        this.ingredientId = ingredientId != null
            ? ingredientId.Trim()
            : string.Empty;
        this.canonicalMilliUnits = canonicalMilliUnits;
    }
}

/// <summary>
/// Lectura inmutable del balance de un ingrediente.
/// </summary>
public readonly struct BistroBuilderInventoryStockSnapshot
{
    public string IngredientId { get; }
    public string StorageLocationId { get; }
    public BistroBuilderMeasurementUnit BaseUnit { get; }
    public long OnHandCanonicalMilliUnits { get; }
    public long ReservedCanonicalMilliUnits { get; }
    public long AvailableCanonicalMilliUnits { get; }
    public long ConsumedCanonicalMilliUnits { get; }
    public long WastedCanonicalMilliUnits { get; }
    public long Revision { get; }

    public BistroBuilderInventoryStockSnapshot(
        string ingredientId,
        string storageLocationId,
        BistroBuilderMeasurementUnit baseUnit,
        long onHandCanonicalMilliUnits,
        long reservedCanonicalMilliUnits,
        long consumedCanonicalMilliUnits,
        long wastedCanonicalMilliUnits,
        long revision
    )
    {
        IngredientId = ingredientId ?? string.Empty;
        StorageLocationId = storageLocationId ?? string.Empty;
        BaseUnit = baseUnit;
        OnHandCanonicalMilliUnits = onHandCanonicalMilliUnits;
        ReservedCanonicalMilliUnits = reservedCanonicalMilliUnits;
        AvailableCanonicalMilliUnits =
            onHandCanonicalMilliUnits - reservedCanonicalMilliUnits;
        ConsumedCanonicalMilliUnits = consumedCanonicalMilliUnits;
        WastedCanonicalMilliUnits = wastedCanonicalMilliUnits;
        Revision = revision;
    }
}

/// <summary>
/// Línea inmutable de una reserva.
/// </summary>
public readonly struct BistroBuilderInventoryReservationLineSnapshot
{
    public string IngredientId { get; }
    public long CanonicalMilliUnits { get; }

    public BistroBuilderInventoryReservationLineSnapshot(
        string ingredientId,
        long canonicalMilliUnits
    )
    {
        IngredientId = ingredientId ?? string.Empty;
        CanonicalMilliUnits = canonicalMilliUnits;
    }
}

/// <summary>
/// Lectura inmutable de una reserva completa.
/// </summary>
public sealed class BistroBuilderInventoryReservationSnapshot
{
    private readonly List<BistroBuilderInventoryReservationLineSnapshot>
        lines;

    public string ReservationId { get; }
    public string SourceId { get; }
    public BistroBuilderInventoryReservationStatus Status { get; }
    public long Revision { get; }

    public IReadOnlyList<BistroBuilderInventoryReservationLineSnapshot>
        Lines => lines;

    public BistroBuilderInventoryReservationSnapshot(
        string reservationId,
        string sourceId,
        BistroBuilderInventoryReservationStatus status,
        long revision,
        List<BistroBuilderInventoryReservationLineSnapshot> lines
    )
    {
        ReservationId = reservationId ?? string.Empty;
        SourceId = sourceId ?? string.Empty;
        Status = status;
        Revision = revision;
        this.lines = lines != null
            ? new List<BistroBuilderInventoryReservationLineSnapshot>(lines)
            : new List<BistroBuilderInventoryReservationLineSnapshot>();
    }
}

/// <summary>
/// Registro inmutable del libro de movimientos.
///
/// Los deltas permiten reconstruir y auditar balances. La secuencia es la
/// autoridad de orden dentro de la partida; el timestamp es informativo.
/// </summary>
public readonly struct BistroBuilderInventoryTransactionSnapshot
{
    public long Sequence { get; }
    public string TransactionId { get; }
    public string OperationId { get; }
    public string IngredientId { get; }
    public BistroBuilderInventoryTransactionType TransactionType { get; }
    public long QuantityCanonicalMilliUnits { get; }
    public long OnHandDeltaCanonicalMilliUnits { get; }
    public long ReservedDeltaCanonicalMilliUnits { get; }
    public long PreviousOnHandCanonicalMilliUnits { get; }
    public long NewOnHandCanonicalMilliUnits { get; }
    public long PreviousReservedCanonicalMilliUnits { get; }
    public long NewReservedCanonicalMilliUnits { get; }
    public string SourceId { get; }
    public string Reason { get; }
    public long TimestampUtcTicks { get; }

    public BistroBuilderInventoryTransactionSnapshot(
        long sequence,
        string transactionId,
        string operationId,
        string ingredientId,
        BistroBuilderInventoryTransactionType transactionType,
        long quantityCanonicalMilliUnits,
        long onHandDeltaCanonicalMilliUnits,
        long reservedDeltaCanonicalMilliUnits,
        long previousOnHandCanonicalMilliUnits,
        long newOnHandCanonicalMilliUnits,
        long previousReservedCanonicalMilliUnits,
        long newReservedCanonicalMilliUnits,
        string sourceId,
        string reason,
        long timestampUtcTicks
    )
    {
        Sequence = sequence;
        TransactionId = transactionId ?? string.Empty;
        OperationId = operationId ?? string.Empty;
        IngredientId = ingredientId ?? string.Empty;
        TransactionType = transactionType;
        QuantityCanonicalMilliUnits = quantityCanonicalMilliUnits;
        OnHandDeltaCanonicalMilliUnits = onHandDeltaCanonicalMilliUnits;
        ReservedDeltaCanonicalMilliUnits = reservedDeltaCanonicalMilliUnits;
        PreviousOnHandCanonicalMilliUnits =
            previousOnHandCanonicalMilliUnits;
        NewOnHandCanonicalMilliUnits = newOnHandCanonicalMilliUnits;
        PreviousReservedCanonicalMilliUnits =
            previousReservedCanonicalMilliUnits;
        NewReservedCanonicalMilliUnits =
            newReservedCanonicalMilliUnits;
        SourceId = sourceId ?? string.Empty;
        Reason = reason ?? string.Empty;
        TimestampUtcTicks = timestampUtcTicks;
    }
}
