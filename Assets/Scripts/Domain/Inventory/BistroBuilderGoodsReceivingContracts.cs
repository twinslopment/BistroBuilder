using System;
using System.Collections.Generic;

/// <summary>
/// Identidades únicas del flujo de recepción de mercancía 2.2B.
///
/// El diseño de Bistro Builder mantiene un único almacén genérico por
/// restaurante. Estas identidades no modelan múltiples almacenes jugables:
/// permiten que inventario, presentación y futuras compras compartan el mismo
/// contrato estable sin depender de nombres de GameObject.
/// </summary>
public static class BistroBuilderGoodsReceivingIds
{
    public const string PrimaryWarehouse = "warehouse_primary";
    public const string PrimarySupplyAccess = "supply_access_primary";
}

/// <summary>
/// Línea inmutable de una recepción ya aceptada por inventario.
/// La cantidad está normalizada a milésimas de la unidad canónica.
/// </summary>
public readonly struct BistroBuilderGoodsReceiptLineSnapshot
{
    public string IngredientId { get; }
    public long CanonicalMilliUnits { get; }

    public BistroBuilderGoodsReceiptLineSnapshot(
        string ingredientId,
        long canonicalMilliUnits
    )
    {
        IngredientId = ingredientId ?? string.Empty;
        CanonicalMilliUnits = canonicalMilliUnits;
    }
}

/// <summary>
/// Resultado inmutable de una recepción de mercancía.
///
/// La autoridad de existencias sigue siendo BistroBuilderInventoryService.
/// CreatedLots solo identifica los lotes físicos creados por ESTA recepción;
/// no añade valor económico ni convierte Recepciones en una autoridad de coste.
/// </summary>
public sealed class BistroBuilderGoodsReceiptSnapshot
{
    private readonly List<BistroBuilderGoodsReceiptLineSnapshot> lines;
    private readonly List<BistroBuilderInventoryLotSnapshot> createdLots;

    public string ReceiptId { get; }
    public string SourceId { get; }
    public string WarehouseId { get; }
    public int ReceivedDayIndex { get; }
    public long InventoryRevision { get; }
    public bool WasReplayed { get; }
    public IReadOnlyList<BistroBuilderGoodsReceiptLineSnapshot> Lines => lines;
    public IReadOnlyList<BistroBuilderInventoryLotSnapshot> CreatedLots =>
        createdLots;

    public BistroBuilderGoodsReceiptSnapshot(
        string receiptId,
        string sourceId,
        int receivedDayIndex,
        long inventoryRevision,
        bool wasReplayed,
        List<BistroBuilderGoodsReceiptLineSnapshot> lines
    ) : this(
        receiptId,
        sourceId,
        receivedDayIndex,
        inventoryRevision,
        wasReplayed,
        lines,
        null
    )
    {
    }

    public BistroBuilderGoodsReceiptSnapshot(
        string receiptId,
        string sourceId,
        int receivedDayIndex,
        long inventoryRevision,
        bool wasReplayed,
        List<BistroBuilderGoodsReceiptLineSnapshot> lines,
        List<BistroBuilderInventoryLotSnapshot> createdLots
    )
    {
        ReceiptId = receiptId ?? string.Empty;
        SourceId = sourceId ?? string.Empty;
        WarehouseId = BistroBuilderGoodsReceivingIds.PrimaryWarehouse;
        ReceivedDayIndex = Math.Max(1, receivedDayIndex);
        InventoryRevision = Math.Max(0L, inventoryRevision);
        WasReplayed = wasReplayed;
        this.lines = lines != null
            ? new List<BistroBuilderGoodsReceiptLineSnapshot>(lines)
            : new List<BistroBuilderGoodsReceiptLineSnapshot>();
        this.createdLots = createdLots != null
            ? new List<BistroBuilderInventoryLotSnapshot>(createdLots)
            : new List<BistroBuilderInventoryLotSnapshot>();
    }
}

/// <summary>
/// Estados de la representación visual temporal del reparto.
/// No son estados de personal ni de logística persistente.
/// </summary>
public enum BistroBuilderGoodsReceivingVisualState
{
    Idle = 0,
    Entering = 1,
    GoingToWarehouse = 2,
    Unloading = 3,
    ReturningToSupplyAccess = 4,
    Exiting = 5,
    Completed = 6
}
