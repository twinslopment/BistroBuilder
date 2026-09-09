using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad analítica de 3D para coste de producto y margen por línea servida.
/// No mueve caja y no posee existencias: valora los lotes de Inventario, el
/// consumo FEFO y las salidas no comerciales que ya ejecutan 2.2/368CD.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
[AddComponentMenu("Bistro Builder/Finance/Product Cost Service")]
public sealed class BistroBuilderProductCostService : MonoBehaviour
{
    [SerializeField] private BistroBuilderInventoryService inventoryService;
    [SerializeField] private BistroBuilderRecipeCatalogService recipeCatalogService;
    [SerializeField] private OrderSystem orderSystem;
    [SerializeField] private BistroBuilderCanonicalOrderService canonicalOrderService;
    [SerializeField] private BistroBuilderSupplierReceivingBridge23L supplierReceivingBridge;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private BistroBuilderSaveGameService saveGameService;

    private readonly Dictionary<string, BistroBuilderLotCostBasisRecord> basisByLotId =
        new Dictionary<string, BistroBuilderLotCostBasisRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderConsumedLineCostRecord> lineCostByLineId =
        new Dictionary<string, BistroBuilderConsumedLineCostRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderInventoryLossCostRecord>
        lossCostByOperationId =
            new Dictionary<string, BistroBuilderInventoryLossCostRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderInventoryReservationSnapshot> activeReservationById =
        new Dictionary<string, BistroBuilderInventoryReservationSnapshot>(StringComparer.Ordinal);
    private readonly List<BistroBuilderInventoryLotSnapshot> lotBuffer =
        new List<BistroBuilderInventoryLotSnapshot>(64);

    private BistroBuilderProductCostSnapshot state;

    public event Action<BistroBuilderConsumedLineCostRecord> LineCostRecorded;
    public event Action<BistroBuilderInventoryLossCostRecord> InventoryLossCostRecorded;

    public bool IsInitialized => state != null;
    public long Revision => state != null ? state.revision : 0L;
    public int LotCostBasisCount => basisByLotId.Count;
    public int ConsumedLineCostCount => lineCostByLineId.Count;
    public int InventoryLossCostCount => lossCostByOperationId.Count;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void Start()
    {
        if (state == null && !TryInitializeFresh(out string error))
        {
            Debug.LogError("3D no pudo inicializarse. " + error, this);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        activeReservationById.Clear();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (inventoryService == null || recipeCatalogService == null ||
            orderSystem == null || canonicalOrderService == null ||
            supplierReceivingBridge == null || generalGameStateService == null ||
            gameClock == null || saveGameService == null)
        {
            error = "3D necesita Inventario, catálogo de recetas, Comandas, 2.3L, reloj y guardado.";
            return false;
        }

        if (!inventoryService.ValidateConfiguration(out error) ||
            !recipeCatalogService.ValidateConfiguration(out error) ||
            !orderSystem.ValidateConfiguration(out error) ||
            !canonicalOrderService.ValidateConfiguration(out error) ||
            !supplierReceivingBridge.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (orderSystem.CanonicalIntegrationService == null ||
            !ReferenceEquals(
                orderSystem.CanonicalIntegrationService.CanonicalOrderService,
                canonicalOrderService))
        {
            error = "3D no comparte la autoridad canónica de comandas de OrderSystem.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryInitializeFresh(out string error)
    {
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        state = new BistroBuilderProductCostSnapshot();
        RebuildIndexes();
        activeReservationById.Clear();

        if (!TrySynchronizeWithInventory(out error) ||
            !TryRebuildActiveReservationCache(out error))
        {
            state = null;
            basisByLotId.Clear();
            lineCostByLineId.Clear();
            lossCostByOperationId.Clear();
            activeReservationById.Clear();
            return false;
        }

        return true;
    }

    public BistroBuilderProductCostSnapshot CreateSnapshot()
    {
        return state != null ? state.DeepClone() : null;
    }

    public bool TryRestoreSnapshot(
        BistroBuilderProductCostSnapshot candidate,
        out string error)
    {
        NormalizeCompatibleSnapshot(candidate);
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderProductCostEngine.TryValidateSnapshot(candidate, out error))
        {
            return false;
        }

        state = candidate.DeepClone();
        RebuildIndexes();
        activeReservationById.Clear();
        return true;
    }

    /// <summary>
    /// Normalización aditiva de finance.product_cost.runtime v1. Permite cargar
    /// partidas v1 capturadas antes de existir las bajas económicas sin crear
    /// una migración de versión para un campo opcional recién añadido.
    /// </summary>
    public static void NormalizeCompatibleSnapshot(
        BistroBuilderProductCostSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }
        if (snapshot.inventoryLossCosts == null)
        {
            snapshot.inventoryLossCosts =
                new List<BistroBuilderInventoryLossCostRecord>();
        }
        if (snapshot.nextInventoryLossCostSequence < 1L &&
            snapshot.inventoryLossCosts.Count == 0)
        {
            snapshot.nextInventoryLossCostSequence = 1L;
        }
    }

    public bool TrySynchronizeWithInventory(out string error)
    {
        error = string.Empty;
        if (state == null)
        {
            error = "finance.product_cost.runtime no está inicializado.";
            return false;
        }

        inventoryService.CopyLotSnapshotsTo(lotBuffer);
        for (int index = 0; index < lotBuffer.Count; index++)
        {
            BistroBuilderInventoryLotSnapshot lot = lotBuffer[index];
            if (lot.OnHandCanonicalMilliUnits <= 0L ||
                basisByLotId.ContainsKey(lot.LotId))
            {
                continue;
            }

            if (!TryAddReferenceBasis(lot, out error))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryRebuildActiveReservationCache(out string error)
    {
        error = string.Empty;
        activeReservationById.Clear();
        if (orderSystem == null || canonicalOrderService == null ||
            inventoryService == null)
        {
            error = "No están disponibles las autoridades para reconstruir reservas activas de 3D.";
            return false;
        }

        IReadOnlyList<RestaurantOrder> activeOrders = orderSystem.ActiveOrders;
        for (int orderIndex = 0; orderIndex < activeOrders.Count; orderIndex++)
        {
            RestaurantOrder legacyOrder = activeOrders[orderIndex];
            if (legacyOrder == null || string.IsNullOrWhiteSpace(legacyOrder.CanonicalOrderId) ||
                !canonicalOrderService.TryGetOrderSnapshot(
                    legacyOrder.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder canonicalOrder) ||
                canonicalOrder == null)
            {
                continue;
            }

            for (int lineIndex = 0; lineIndex < canonicalOrder.Lines.Count; lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line = canonicalOrder.Lines[lineIndex];
                if (line == null)
                {
                    continue;
                }

                string reservationId =
                    BistroBuilderOrderInventoryLifecycleService.BuildReservationId(
                        canonicalOrder.OrderId,
                        line.LineId);
                if (inventoryService.TryGetReservationSnapshot(
                        reservationId,
                        out BistroBuilderInventoryReservationSnapshot reservation) &&
                    reservation != null &&
                    reservation.Status == BistroBuilderInventoryReservationStatus.Active)
                {
                    activeReservationById[reservation.ReservationId] = reservation;
                }
            }
        }

        return true;
    }

    public bool TryGetLotCostBasis(
        string lotId,
        out BistroBuilderLotCostBasisRecord basis)
    {
        basis = null;
        if (string.IsNullOrWhiteSpace(lotId) ||
            !basisByLotId.TryGetValue(
                lotId.Trim(),
                out BistroBuilderLotCostBasisRecord stored))
        {
            return false;
        }

        basis = stored.DeepClone();
        return true;
    }

    public bool TryGetLineCost(
        string lineId,
        out BistroBuilderConsumedLineCostRecord cost)
    {
        cost = null;
        if (string.IsNullOrWhiteSpace(lineId) ||
            !lineCostByLineId.TryGetValue(
                BistroBuilderOrderIdUtility.Normalize(lineId),
                out BistroBuilderConsumedLineCostRecord stored))
        {
            return false;
        }

        cost = stored.DeepClone();
        return true;
    }

    public bool TryGetInventoryLossCost(
        string inventoryOperationId,
        out BistroBuilderInventoryLossCostRecord cost)
    {
        cost = null;
        if (string.IsNullOrWhiteSpace(inventoryOperationId) ||
            !lossCostByOperationId.TryGetValue(
                inventoryOperationId.Trim().ToLowerInvariant(),
                out BistroBuilderInventoryLossCostRecord stored))
        {
            return false;
        }
        cost = stored.DeepClone();
        return true;
    }

    public int CopyLineCosts(List<BistroBuilderConsumedLineCostRecord> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        if (state == null || state.consumedLineCosts == null)
        {
            return 0;
        }

        for (int index = 0; index < state.consumedLineCosts.Count; index++)
        {
            destination.Add(state.consumedLineCosts[index].DeepClone());
        }
        return destination.Count;
    }

    public int CopyInventoryLossCosts(
        List<BistroBuilderInventoryLossCostRecord> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        if (state == null || state.inventoryLossCosts == null)
        {
            return 0;
        }

        for (int index = 0; index < state.inventoryLossCosts.Count; index++)
        {
            destination.Add(state.inventoryLossCosts[index].DeepClone());
        }
        return destination.Count;
    }

    /// <summary>
    /// Registra el coste no monetario de inventario que sale sin venta.
    /// La valoración V1 usa el precio de referencia del ingrediente porque el
    /// movimiento agregado de 2.2 no conserva qué lotes exactos fueron dados
    /// de baja. El coste queda congelado y marcado Estimated en 3D.
    /// </summary>
    public bool TryRecordInventoryLoss(
        BistroBuilderInventoryTransactionSnapshot transaction,
        out string error)
    {
        error = string.Empty;
        if (state == null && !TryInitializeFresh(out error))
        {
            return false;
        }

        if (transaction.TransactionType !=
                BistroBuilderInventoryTransactionType.Expiration &&
            transaction.TransactionType !=
                BistroBuilderInventoryTransactionType.Waste)
        {
            error = "El movimiento no representa caducidad ni merma.";
            return false;
        }
        if (transaction.QuantityCanonicalMilliUnits <= 0L ||
            string.IsNullOrWhiteSpace(transaction.OperationId) ||
            string.IsNullOrWhiteSpace(transaction.TransactionId) ||
            string.IsNullOrWhiteSpace(transaction.IngredientId))
        {
            error = "La baja de inventario no contiene identidad o cantidad válida.";
            return false;
        }

        string operationId = transaction.OperationId.Trim().ToLowerInvariant();
        if (lossCostByOperationId.TryGetValue(
                operationId,
                out BistroBuilderInventoryLossCostRecord existing))
        {
            bool equivalent =
                string.Equals(
                    existing.inventoryTransactionId,
                    transaction.TransactionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    existing.ingredientId,
                    transaction.IngredientId,
                    StringComparison.Ordinal) &&
                existing.transactionType == transaction.TransactionType &&
                existing.quantityCanonicalMilliUnits ==
                    transaction.QuantityCanonicalMilliUnits;
            if (!equivalent)
            {
                error = "El OperationId de la baja ya existe con otro contenido económico.";
                return false;
            }
            return true;
        }

        if (!recipeCatalogService.TryGetIngredient(
                transaction.IngredientId,
                out BistroBuilderIngredientDefinition ingredient) ||
            ingredient == null ||
            !ingredient.TryCalculateCostMicroCents(
                transaction.QuantityCanonicalMilliUnits,
                out long costMicroCents,
                out error))
        {
            return false;
        }

        long sequence = state.nextInventoryLossCostSequence;
        long nextSequence;
        long nextRevision;
        try
        {
            nextSequence = checked(sequence + 1L);
            nextRevision = checked(state.revision + 1L);
        }
        catch (OverflowException)
        {
            error = "La secuencia de bajas económicas de inventario quedó fuera de rango.";
            return false;
        }

        var record = new BistroBuilderInventoryLossCostRecord
        {
            sequence = sequence,
            lossCostRecordId =
                BistroBuilderProductCostEngine.BuildInventoryLossCostRecordId(sequence),
            inventoryTransactionId = transaction.TransactionId,
            inventoryOperationId = operationId,
            ingredientId = transaction.IngredientId,
            transactionType = transaction.TransactionType,
            dayIndex = Math.Max(1, generalGameStateService.DayIndex),
            minuteOfDay = Mathf.Clamp(
                gameClock.Hour * 60 + gameClock.Minute,
                0,
                1439),
            quantityCanonicalMilliUnits =
                transaction.QuantityCanonicalMilliUnits,
            costMicroCents = costMicroCents,
            costCents =
                BistroBuilderProductCostEngine.RoundMicroCentsToCents(
                    costMicroCents),
            costQuality = BistroBuilderProductCostQuality.Estimated
        };

        state.inventoryLossCosts.Add(record);
        lossCostByOperationId.Add(record.inventoryOperationId, record);
        state.nextInventoryLossCostSequence = nextSequence;
        state.revision = nextRevision;
        InventoryLossCostRecorded?.Invoke(record.DeepClone());
        return true;
    }

    public bool TryApplySupplierReceipt(
        BistroBuilderGoodsReceiptSnapshot receipt,
        BistroBuilderPurchaseOrderRecord deliveredOrder,
        out string error)
    {
        error = string.Empty;
        if (receipt == null || deliveredOrder == null || receipt.WasReplayed)
        {
            return true;
        }
        if (state == null && !TryInitializeFresh(out error))
        {
            return false;
        }
        if (receipt.CreatedLots == null || receipt.CreatedLots.Count == 0 ||
            deliveredOrder.confirmedLines == null ||
            deliveredOrder.confirmedLines.Count == 0 ||
            !string.Equals(
                deliveredOrder.deliveryReceiptId,
                receipt.ReceiptId,
                StringComparison.Ordinal))
        {
            error = "La recepción de proveedor no contiene lotes/PO trazables para valorar.";
            return false;
        }

        var quantityByIngredient = new Dictionary<string, long>(StringComparer.Ordinal);
        var subtotalByIngredient = new Dictionary<string, long>(StringComparer.Ordinal);
        for (int index = 0; index < deliveredOrder.confirmedLines.Count; index++)
        {
            BistroBuilderPurchaseOrderConfirmedLineSnapshot line =
                deliveredOrder.confirmedLines[index];
            if (line == null || line.totalNetQuantityMicrounits <= 0L ||
                line.totalNetQuantityMicrounits % 1000L != 0L ||
                line.lineSubtotalCents < 0L)
            {
                error = "El PurchaseOrder contiene una línea no valorable.";
                return false;
            }

            long milli = line.totalNetQuantityMicrounits / 1000L;
            try
            {
                quantityByIngredient.TryGetValue(
                    line.ingredientId,
                    out long quantity);
                subtotalByIngredient.TryGetValue(
                    line.ingredientId,
                    out long subtotal);
                quantityByIngredient[line.ingredientId] = checked(quantity + milli);
                subtotalByIngredient[line.ingredientId] =
                    checked(subtotal + line.lineSubtotalCents);
            }
            catch (OverflowException)
            {
                error = "El PurchaseOrder desborda la valoración agregada por ingrediente.";
                return false;
            }
        }

        for (int index = 0; index < receipt.CreatedLots.Count; index++)
        {
            BistroBuilderInventoryLotSnapshot lot = receipt.CreatedLots[index];
            if (!quantityByIngredient.TryGetValue(
                    lot.IngredientId,
                    out long quantity) ||
                !subtotalByIngredient.TryGetValue(
                    lot.IngredientId,
                    out long subtotal) ||
                quantity <= 0L ||
                quantity != lot.OnHandCanonicalMilliUnits)
            {
                error = "El lote " + lot.LotId +
                        " no converge con la cantidad confirmada del PurchaseOrder.";
                return false;
            }

            long totalCostMicroCents;
            try
            {
                totalCostMicroCents = checked(
                    subtotal * BistroBuilderIngredientDefinition.MicroCentsPerCent);
            }
            catch (OverflowException)
            {
                error = "El coste real del lote " + lot.LotId +
                        " queda fuera de rango.";
                return false;
            }

            if (!TrySetSupplierActualBasis(
                    lot,
                    deliveredOrder.purchaseOrderId,
                    quantity,
                    totalCostMicroCents,
                    out error))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryAddReferenceBasis(
        BistroBuilderInventoryLotSnapshot lot,
        out string error)
    {
        error = string.Empty;
        if (lot.OnHandCanonicalMilliUnits <= 0L)
        {
            return true;
        }
        if (!recipeCatalogService.TryGetIngredient(
                lot.IngredientId,
                out BistroBuilderIngredientDefinition ingredient) ||
            ingredient == null ||
            !ingredient.TryCalculateCostMicroCents(
                lot.OnHandCanonicalMilliUnits,
                out long costMicroCents,
                out error))
        {
            return false;
        }

        var basis = new BistroBuilderLotCostBasisRecord
        {
            lotId = lot.LotId,
            ingredientId = lot.IngredientId,
            sourceReferenceId = string.IsNullOrWhiteSpace(lot.SourceId)
                ? "inventory_reference"
                : lot.SourceId,
            basisKind = BistroBuilderLotCostBasisKind.ReferenceEstimate,
            basisQuantityCanonicalMilliUnits = lot.OnHandCanonicalMilliUnits,
            totalCostMicroCents = costMicroCents,
            receivedDayIndex = Math.Max(1, lot.ReceivedDayIndex)
        };

        state.lotCostBases.Add(basis);
        basisByLotId.Add(basis.lotId, basis);
        state.revision++;
        return true;
    }

    private bool TrySetSupplierActualBasis(
        BistroBuilderInventoryLotSnapshot lot,
        string purchaseOrderId,
        long quantity,
        long totalCostMicroCents,
        out string error)
    {
        error = string.Empty;
        if (basisByLotId.TryGetValue(
                lot.LotId,
                out BistroBuilderLotCostBasisRecord existing) &&
            existing.basisKind == BistroBuilderLotCostBasisKind.SupplierActual)
        {
            bool same =
                existing.ingredientId == lot.IngredientId &&
                existing.sourceReferenceId == purchaseOrderId &&
                existing.basisQuantityCanonicalMilliUnits == quantity &&
                existing.totalCostMicroCents == totalCostMicroCents;
            error = same
                ? string.Empty
                : "El lote " + lot.LotId +
                  " ya tiene otra base real de proveedor.";
            return same;
        }

        if (existing == null)
        {
            existing = new BistroBuilderLotCostBasisRecord();
            state.lotCostBases.Add(existing);
            basisByLotId.Add(lot.LotId, existing);
        }

        existing.lotId = lot.LotId;
        existing.ingredientId = lot.IngredientId;
        existing.sourceReferenceId = purchaseOrderId;
        existing.basisKind = BistroBuilderLotCostBasisKind.SupplierActual;
        existing.basisQuantityCanonicalMilliUnits = quantity;
        existing.totalCostMicroCents = totalCostMicroCents;
        existing.receivedDayIndex = Math.Max(1, lot.ReceivedDayIndex);
        state.revision++;
        return true;
    }

    private bool TryRecordConsumedReservation(
        BistroBuilderInventoryReservationSnapshot reservation,
        out string error)
    {
        error = string.Empty;
        if (!TryResolveReservationOwner(
                reservation.ReservationId,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderLine line))
        {
            error = "No se pudo resolver la línea canónica de " +
                    reservation.ReservationId + ".";
            return false;
        }

        if (lineCostByLineId.ContainsKey(line.LineId))
        {
            return true;
        }

        long theoreticalMicroCents = 0L;
        for (int index = 0; index < reservation.Lines.Count; index++)
        {
            BistroBuilderInventoryReservationLineSnapshot reservationLine =
                reservation.Lines[index];
            if (reservationLine == null ||
                !recipeCatalogService.TryGetIngredient(
                    reservationLine.IngredientId,
                    out BistroBuilderIngredientDefinition ingredient) ||
                ingredient == null ||
                !ingredient.TryCalculateCostMicroCents(
                    reservationLine.CanonicalMilliUnits,
                    out long lineCost,
                    out error))
            {
                return false;
            }

            try
            {
                theoreticalMicroCents = checked(theoreticalMicroCents + lineCost);
            }
            catch (OverflowException)
            {
                error = "El escandallo teórico de la línea queda fuera de rango.";
                return false;
            }
        }

        if (!BistroBuilderProductCostEngine.TryCalculateActualCost(
                reservation,
                basisByLotId,
                out long actualMicroCents,
                out BistroBuilderProductCostQuality quality,
                out error))
        {
            return false;
        }

        long theoreticalCents =
            BistroBuilderProductCostEngine.RoundMicroCentsToCents(
                theoreticalMicroCents);
        long actualCents =
            BistroBuilderProductCostEngine.RoundMicroCentsToCents(
                actualMicroCents);
        long theoreticalMargin = line.PriceCentsAtOrder - theoreticalCents;
        long actualMargin = line.PriceCentsAtOrder - actualCents;
        int theoreticalBasisPoints =
            BistroBuilderProductCostEngine.CalculateMarginBasisPoints(
                line.PriceCentsAtOrder,
                theoreticalCents);
        int actualBasisPoints =
            BistroBuilderProductCostEngine.CalculateMarginBasisPoints(
                line.PriceCentsAtOrder,
                actualCents);

        long sequence = state.nextLineCostSequence++;
        var record = new BistroBuilderConsumedLineCostRecord
        {
            sequence = sequence,
            costRecordId = BistroBuilderProductCostEngine.BuildCostRecordId(sequence),
            orderId = order.OrderId,
            lineId = line.LineId,
            dishId = line.DishId,
            mealService = order.MealService,
            serviceMode = order.ServiceMode,
            dayIndex = Math.Max(1, generalGameStateService.DayIndex),
            minuteOfDay = Mathf.Clamp(
                gameClock.Hour * 60 + gameClock.Minute,
                0,
                1439),
            salePriceCents = line.PriceCentsAtOrder,
            theoreticalCostMicroCents = theoreticalMicroCents,
            theoreticalCostCents = theoreticalCents,
            actualCostMicroCents = actualMicroCents,
            actualCostCents = actualCents,
            theoreticalMarginCents = theoreticalMargin,
            theoreticalMarginBasisPoints = theoreticalBasisPoints,
            theoreticalMarginBand =
                BistroBuilderProductCostEngine.ResolveMarginBand(
                    theoreticalMargin,
                    theoreticalBasisPoints),
            actualMarginCents = actualMargin,
            actualMarginBasisPoints = actualBasisPoints,
            actualMarginBand = BistroBuilderProductCostEngine.ResolveMarginBand(
                actualMargin,
                actualBasisPoints),
            costQuality = quality
        };

        state.consumedLineCosts.Add(record);
        lineCostByLineId.Add(record.lineId, record);
        state.revision++;
        LineCostRecorded?.Invoke(record.DeepClone());
        return true;
    }

    private bool TryResolveReservationOwner(
        string reservationId,
        out BistroBuilderCanonicalOrder resolvedOrder,
        out BistroBuilderCanonicalOrderLine resolvedLine)
    {
        resolvedOrder = null;
        resolvedLine = null;
        IReadOnlyList<RestaurantOrder> activeOrders = orderSystem.ActiveOrders;

        for (int orderIndex = 0; orderIndex < activeOrders.Count; orderIndex++)
        {
            RestaurantOrder legacyOrder = activeOrders[orderIndex];
            if (legacyOrder == null ||
                !canonicalOrderService.TryGetOrderSnapshot(
                    legacyOrder.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder order) ||
                order == null)
            {
                continue;
            }

            for (int lineIndex = 0; lineIndex < order.Lines.Count; lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line = order.Lines[lineIndex];
                if (line != null &&
                    string.Equals(
                        BistroBuilderOrderInventoryLifecycleService.BuildReservationId(
                            order.OrderId,
                            line.LineId),
                        reservationId,
                        StringComparison.Ordinal))
                {
                    resolvedOrder = order;
                    resolvedLine = line;
                    return true;
                }
            }
        }

        return false;
    }

    private void HandleReservationChanged(
        BistroBuilderInventoryReservationSnapshot reservation)
    {
        if (reservation == null ||
            (saveGameService != null && saveGameService.IsBusy))
        {
            return;
        }

        if (reservation.Status == BistroBuilderInventoryReservationStatus.Active)
        {
            activeReservationById[reservation.ReservationId] = reservation;
            return;
        }

        if (reservation.Status == BistroBuilderInventoryReservationStatus.Consumed &&
            activeReservationById.TryGetValue(
                reservation.ReservationId,
                out BistroBuilderInventoryReservationSnapshot active))
        {
            if (!TryRecordConsumedReservation(active, out string error))
            {
                Debug.LogError(
                    "3D no pudo valorar una línea consumida. " + error,
                    this);
            }
        }

        activeReservationById.Remove(reservation.ReservationId);
    }

    private void HandleInventoryTransaction(
        BistroBuilderInventoryTransactionSnapshot transaction)
    {
        if ((saveGameService != null && saveGameService.IsBusy) ||
            (transaction.TransactionType !=
                 BistroBuilderInventoryTransactionType.Expiration &&
             transaction.TransactionType !=
                 BistroBuilderInventoryTransactionType.Waste))
        {
            return;
        }

        if (!TryRecordInventoryLoss(transaction, out string error))
        {
            Debug.LogError(
                "3D no pudo valorar una baja de inventario. " + error,
                this);
        }
    }

    private void HandleLotChanged(BistroBuilderInventoryLotSnapshot lot)
    {
        if (state == null || lot.OnHandCanonicalMilliUnits <= 0L ||
            basisByLotId.ContainsKey(lot.LotId) ||
            (saveGameService != null && saveGameService.IsBusy))
        {
            return;
        }

        if (!TryAddReferenceBasis(lot, out string error))
        {
            Debug.LogError(
                "3D no pudo estimar el lote " + lot.LotId + ". " + error,
                this);
        }
    }

    private void HandleSupplierReceiptIntegrated(
        BistroBuilderGoodsReceiptSnapshot receipt,
        BistroBuilderPurchaseOrderRecord deliveredOrder)
    {
        if (saveGameService != null && saveGameService.IsBusy)
        {
            return;
        }

        if (!TryApplySupplierReceipt(receipt, deliveredOrder, out string error))
        {
            Debug.LogError(
                "3D no pudo aplicar el coste real del proveedor. " + error,
                this);
        }
    }

    private void Subscribe()
    {
        if (inventoryService != null)
        {
            inventoryService.ReservationChanged -= HandleReservationChanged;
            inventoryService.ReservationChanged += HandleReservationChanged;
            inventoryService.LotChanged -= HandleLotChanged;
            inventoryService.LotChanged += HandleLotChanged;
            inventoryService.TransactionRecorded -= HandleInventoryTransaction;
            inventoryService.TransactionRecorded += HandleInventoryTransaction;
        }

        if (supplierReceivingBridge != null)
        {
            supplierReceivingBridge.SupplierReceiptIntegrated -=
                HandleSupplierReceiptIntegrated;
            supplierReceivingBridge.SupplierReceiptIntegrated +=
                HandleSupplierReceiptIntegrated;
        }
    }

    private void Unsubscribe()
    {
        if (inventoryService != null)
        {
            inventoryService.ReservationChanged -= HandleReservationChanged;
            inventoryService.LotChanged -= HandleLotChanged;
            inventoryService.TransactionRecorded -= HandleInventoryTransaction;
        }
        if (supplierReceivingBridge != null)
        {
            supplierReceivingBridge.SupplierReceiptIntegrated -=
                HandleSupplierReceiptIntegrated;
        }
    }

    private void RebuildIndexes()
    {
        basisByLotId.Clear();
        lineCostByLineId.Clear();
        lossCostByOperationId.Clear();
        if (state == null)
        {
            return;
        }

        NormalizeCompatibleSnapshot(state);
        for (int index = 0; index < state.lotCostBases.Count; index++)
        {
            BistroBuilderLotCostBasisRecord basis = state.lotCostBases[index];
            if (basis != null)
            {
                basisByLotId.Add(basis.lotId, basis);
            }
        }
        for (int index = 0; index < state.consumedLineCosts.Count; index++)
        {
            BistroBuilderConsumedLineCostRecord record =
                state.consumedLineCosts[index];
            if (record != null)
            {
                lineCostByLineId.Add(record.lineId, record);
            }
        }
        for (int index = 0; index < state.inventoryLossCosts.Count; index++)
        {
            BistroBuilderInventoryLossCostRecord record =
                state.inventoryLossCosts[index];
            if (record != null)
            {
                lossCostByOperationId.Add(record.inventoryOperationId, record);
            }
        }
    }

    private void CacheDependencies()
    {
        if (inventoryService == null) TryGetComponent(out inventoryService);
        if (recipeCatalogService == null) TryGetComponent(out recipeCatalogService);
        if (orderSystem == null) TryGetComponent(out orderSystem);
        if (canonicalOrderService == null) TryGetComponent(out canonicalOrderService);
        if (supplierReceivingBridge == null) TryGetComponent(out supplierReceivingBridge);
        if (generalGameStateService == null) TryGetComponent(out generalGameStateService);
        if (gameClock == null) TryGetComponent(out gameClock);
        if (saveGameService == null) TryGetComponent(out saveGameService);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
