using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puente final 2.3L entre la presentación física 2.3H y la autoridad canónica
/// de recepción 2.2B.
///
/// Flujo autoritativo:
/// ReceivingHandoffReady -> BistroBuilderGoodsReceivingService.TryReceiveGoods
/// -> Inventory canonical/lotes/ledger -> PurchaseOrder.TryMarkDelivered.
///
/// NO crea un inventario paralelo. NO marca Delivered si 2.2B no aceptó antes
/// la recepción. El ReceiptId es determinista por PurchaseOrder y por ello el
/// flujo completo es idempotente ante reintentos/cargas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Suppliers/Receiving Bridge 2.3L")]
public sealed class BistroBuilderSupplierReceivingBridge23L : MonoBehaviour
{
    public const string ReceiptIdPrefix = "receipt_supplier_";

    [SerializeField]
    private BistroBuilderGoodsReceivingService goodsReceivingService;

    private BistroBuilderSupplierDeliveryPresentationService deliveryService;
    private BistroBuilderSupplierPurchaseOrderService orderService;
    private BistroBuilderSupplierLogisticsService logisticsService;
    private bool subscribed;
    private float nextReconcileAt;
    private readonly List<BistroBuilderPurchaseOrderRecord> orderBuffer =
        new List<BistroBuilderPurchaseOrderRecord>(32);

    public string LastReceiptId { get; private set; }
    public string LastPurchaseOrderId { get; private set; }
    public string LastError { get; private set; }
    public long AcceptedHandoffCount { get; private set; }

    public event Action<BistroBuilderGoodsReceiptSnapshot, BistroBuilderPurchaseOrderRecord>
        SupplierReceiptIntegrated;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
    }

    private void Update()
    {
        ResolveDependencies();
        Subscribe();

        // Recovery/idempotencia: si 2.3H completó la presentación pero el evento
        // ocurrió antes de que este bridge estuviera suscrito, o 2.2B aceptó la
        // recepción y 2.3E no pudo cerrar Delivered en ese mismo frame, se
        // reconstruye el handoff desde las autoridades persistentes. El ReceiptId
        // determinista hace que 2.2B jamás duplique stock durante el reintento.
        if (Time.unscaledTime >= nextReconcileAt)
        {
            nextReconcileAt = Time.unscaledTime + 1f;
            TryReconcileCompletedPresentations();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        ResolveDependencies();
        if (goodsReceivingService == null)
        {
            error = "Falta BistroBuilderGoodsReceivingService 2.2B.";
            return false;
        }
        return goodsReceivingService.ValidateConfiguration(out error);
    }

    public bool TryAcceptHandoff(
        BistroBuilderSupplierReceivingHandoff handoff,
        out BistroBuilderGoodsReceiptSnapshot receipt,
        out BistroBuilderPurchaseOrderRecord deliveredOrder,
        out string error)
    {
        receipt = null;
        deliveredOrder = null;
        error = string.Empty;
        LastError = string.Empty;

        ResolveDependencies();
        if (!ValidateConfiguration(out error))
        {
            LastError = error;
            return false;
        }
        if (deliveryService == null || !deliveryService.IsInitialized ||
            orderService == null || !orderService.IsInitialized ||
            logisticsService == null || !logisticsService.IsInitialized)
        {
            error = "2.3L espera a 2.3E/2.3G/2.3H inicializados.";
            LastError = error;
            return false;
        }
        if (handoff == null || string.IsNullOrWhiteSpace(handoff.purchaseOrderId) ||
            string.IsNullOrWhiteSpace(handoff.logisticsPlanId) ||
            string.IsNullOrWhiteSpace(handoff.supplierId))
        {
            error = "ReceivingHandoff 2.3H incompleto.";
            LastError = error;
            return false;
        }

        BistroBuilderPurchaseOrderRecord order;
        if (!orderService.TryGetOrder(handoff.purchaseOrderId, out order) || order == null)
        {
            error = "El handoff referencia un PurchaseOrder inexistente.";
            LastError = error;
            return false;
        }

        string expectedReceiptId = BuildReceiptId(order.purchaseOrderId);

        // Repetición posterior a una integración ya finalizada: éxito idempotente.
        if (order.status == BistroBuilderPurchaseOrderStatus.Delivered)
        {
            if (!string.Equals(order.deliveryReceiptId, expectedReceiptId, StringComparison.Ordinal))
            {
                error = "El PurchaseOrder ya está Delivered con un ReceiptId distinto.";
                LastError = error;
                return false;
            }
            deliveredOrder = order;
            LastReceiptId = order.deliveryReceiptId;
            LastPurchaseOrderId = order.purchaseOrderId;
            return true;
        }

        if (order.status != BistroBuilderPurchaseOrderStatus.InDelivery)
        {
            error = "2.3L solo recibe un PurchaseOrder InDelivery; estado actual: " +
                    order.status + ".";
            LastError = error;
            return false;
        }
        if (!string.Equals(order.supplierId, handoff.supplierId, StringComparison.Ordinal) ||
            !string.Equals(order.logisticsPlanId, handoff.logisticsPlanId, StringComparison.Ordinal))
        {
            error = "El handoff no coincide con proveedor/LogisticsPlan del PurchaseOrder.";
            LastError = error;
            return false;
        }

        BistroBuilderSupplierLogisticsPlanRecord plan;
        if (!logisticsService.TryGetPlanByOrder(order.purchaseOrderId, out plan) || plan == null ||
            !string.Equals(plan.logisticsPlanId, handoff.logisticsPlanId, StringComparison.Ordinal))
        {
            error = "No se pudo verificar el LogisticsPlan real de 2.3G.";
            LastError = error;
            return false;
        }

        List<BistroBuilderInventoryQuantityLine> lines;
        if (!TryConvertHandoffLines(handoff, out lines, out error))
        {
            LastError = error;
            return false;
        }

        if (!goodsReceivingService.TryReceiveGoods(
                expectedReceiptId,
                handoff.supplierId,
                lines,
                "Recepción proveedor " + handoff.supplierDisplayName +
                " / " + handoff.orderDisplayCode + ".",
                out receipt,
                out error))
        {
            LastError = "2.2B rechazó la recepción: " + error;
            error = LastError;
            return false;
        }

        int deliveredGameDay = Math.Max(1, handoff.gameDay);
        if (!orderService.TryMarkDelivered(
                order.purchaseOrderId,
                receipt.ReceiptId,
                deliveredGameDay,
                out deliveredOrder,
                out error))
        {
            // La recepción ya está en el ledger canónico. Esto es crítico y se
            // expone claramente; un reintento usará el mismo ReceiptId y 2.2B no
            // duplicará stock.
            LastError = "2.2B aceptó la recepción pero 2.3E no pudo cerrar Delivered: " + error;
            error = LastError;
            return false;
        }

        // 2.3G normalmente reconcilia en Update al avanzar día. Forzamos una
        // reconciliación en el mismo día para que la UI vea Delivered sin esperar.
        if (!logisticsService.TryAdvanceToGameDay(
                Math.Max(1, logisticsService.CurrentGameDay),
                out string logisticsError))
        {
            Debug.LogWarning(
                "2.3L recibió el pedido pero 2.3G no pudo reconciliar el plan en el mismo frame: " +
                logisticsError,
                this
            );
        }

        LastReceiptId = receipt.ReceiptId;
        LastPurchaseOrderId = order.purchaseOrderId;
        LastError = string.Empty;
        AcceptedHandoffCount++;
        SupplierReceiptIntegrated?.Invoke(receipt, deliveredOrder.DeepClone());
        return true;
    }

    public static string BuildReceiptId(string purchaseOrderId)
    {
        string value = string.IsNullOrWhiteSpace(purchaseOrderId)
            ? "unknown"
            : purchaseOrderId.Trim().ToLowerInvariant();
        value = value.Replace(' ', '_').Replace('-', '_');
        return ReceiptIdPrefix + value;
    }

    /// <summary>
    /// Conversión exacta: supplier package usa micro-unidades (1.000.000/base)
    /// e Inventario usa mili-unidades canónicas (1.000/base), por tanto /1000.
    /// Se rechaza cualquier cantidad no divisible para no perder precisión.
    /// </summary>
    public static bool TryConvertHandoffLines(
        BistroBuilderSupplierReceivingHandoff handoff,
        out List<BistroBuilderInventoryQuantityLine> lines,
        out string error)
    {
        lines = new List<BistroBuilderInventoryQuantityLine>();
        error = string.Empty;
        if (handoff == null || handoff.lines == null || handoff.lines.Count == 0)
        {
            error = "El handoff no contiene líneas para 2.2B.";
            return false;
        }

        SortedDictionary<string, long> aggregated =
            new SortedDictionary<string, long>(StringComparer.Ordinal);

        for (int index = 0; index < handoff.lines.Count; index++)
        {
            BistroBuilderSupplierDeliveryManifestLine line = handoff.lines[index];
            if (line == null || string.IsNullOrWhiteSpace(line.ingredientId) ||
                line.totalNetQuantityMicrounits <= 0L)
            {
                error = "Línea de handoff inválida en posición " + index + ".";
                return false;
            }
            if (line.totalNetQuantityMicrounits % 1000L != 0L)
            {
                error = "La cantidad micro de " + line.ingredientId +
                        " no puede convertirse sin pérdida a inventory.canonical.";
                return false;
            }

            long milli = line.totalNetQuantityMicrounits / 1000L;
            if (milli <= 0L || milli > BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
            {
                error = "Cantidad fuera de rango al convertir " + line.ingredientId + ".";
                return false;
            }

            aggregated.TryGetValue(line.ingredientId, out long current);
            try
            {
                long total = checked(current + milli);
                if (total > BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
                {
                    error = "La recepción agregada excede el rango para " + line.ingredientId + ".";
                    return false;
                }
                aggregated[line.ingredientId] = total;
            }
            catch (OverflowException)
            {
                error = "Overflow agregando recepción de " + line.ingredientId + ".";
                return false;
            }
        }

        foreach (KeyValuePair<string, long> pair in aggregated)
        {
            lines.Add(new BistroBuilderInventoryQuantityLine(pair.Key, pair.Value));
        }
        return lines.Count > 0;
    }

    private void TryReconcileCompletedPresentations()
    {
        if (deliveryService == null || !deliveryService.IsInitialized ||
            orderService == null || !orderService.IsInitialized ||
            logisticsService == null || !logisticsService.IsInitialized ||
            goodsReceivingService == null)
        {
            return;
        }

        orderService.CopyOrders(orderBuffer);
        for (int i = 0; i < orderBuffer.Count; i++)
        {
            BistroBuilderPurchaseOrderRecord order = orderBuffer[i];
            if (order == null || order.status != BistroBuilderPurchaseOrderStatus.InDelivery)
            {
                continue;
            }

            BistroBuilderSupplierDeliveryPresentationRecord presentation;
            if (!deliveryService.TryGetPresentationByOrder(order.purchaseOrderId, out presentation) ||
                presentation == null ||
                presentation.state != BistroBuilderSupplierDeliveryPresentationState.Completed ||
                !presentation.receivingHandoffEmitted)
            {
                continue;
            }

            BistroBuilderSupplierLogisticsPlanRecord plan;
            if (!logisticsService.TryGetPlanByOrder(order.purchaseOrderId, out plan) || plan == null)
            {
                continue;
            }

            BistroBuilderSupplierReceivingHandoff recovered;
            if (!TryBuildRecoveryHandoff(order, plan, presentation, out recovered, out string buildError))
            {
                LastError = buildError;
                continue;
            }

            if (!TryAcceptHandoff(
                    recovered,
                    out BistroBuilderGoodsReceiptSnapshot receipt,
                    out BistroBuilderPurchaseOrderRecord delivered,
                    out string error))
            {
                LastError = error;
                continue;
            }

            if (receipt != null)
            {
                Debug.Log(
                    "2.3L — Reconciliación idempotente completó una recepción pendiente. PO=" +
                    order.purchaseOrderId + " ReceiptId=" + receipt.ReceiptId + ".",
                    this
                );
            }
        }
    }

    private static bool TryBuildRecoveryHandoff(
        BistroBuilderPurchaseOrderRecord order,
        BistroBuilderSupplierLogisticsPlanRecord plan,
        BistroBuilderSupplierDeliveryPresentationRecord presentation,
        out BistroBuilderSupplierReceivingHandoff handoff,
        out string error)
    {
        handoff = null;
        error = string.Empty;
        if (order == null || plan == null || presentation == null ||
            order.confirmedLines == null || order.confirmedLines.Count == 0)
        {
            error = "No hay datos suficientes para reconstruir el handoff de recepción.";
            return false;
        }

        BistroBuilderSupplierReceivingHandoff result = new BistroBuilderSupplierReceivingHandoff
        {
            handoffId = !string.IsNullOrWhiteSpace(presentation.handoffId)
                ? presentation.handoffId
                : "handoff_recovery_" + order.purchaseOrderId,
            logisticsPlanId = plan.logisticsPlanId,
            purchaseOrderId = order.purchaseOrderId,
            orderDisplayCode = order.displayCode,
            supplierId = order.supplierId,
            supplierDisplayName = order.supplierTerms != null
                ? order.supplierTerms.supplierDisplayName
                : plan.supplierDisplayName,
            gameDay = Math.Max(
                1,
                Math.Max(
                    presentation.completedGameDay,
                    Math.Max(order.inDeliveryGameDay, plan.plannedDeliveryGameDay))),
            visualTripsCompleted = Math.Max(1, presentation.totalTrips)
        };

        long totalMicro = 0L;
        int totalPackages = 0;
        try
        {
            for (int i = 0; i < order.confirmedLines.Count; i++)
            {
                BistroBuilderPurchaseOrderConfirmedLineSnapshot line = order.confirmedLines[i];
                if (line == null || line.packageCount <= 0 ||
                    line.totalNetQuantityMicrounits <= 0L ||
                    string.IsNullOrWhiteSpace(line.ingredientId))
                {
                    error = "PurchaseOrder confirmado contiene una línea no recuperable para 2.2B.";
                    return false;
                }

                totalPackages = checked(totalPackages + line.packageCount);
                totalMicro = checked(totalMicro + line.totalNetQuantityMicrounits);
                result.lines.Add(new BistroBuilderSupplierDeliveryManifestLine
                {
                    purchaseOrderLineId = line.purchaseOrderLineId,
                    supplierOfferId = line.supplierOfferId,
                    ingredientId = line.ingredientId,
                    ingredientDisplayName = line.ingredientDisplayName,
                    canonicalUnit = line.canonicalUnit,
                    packageFormatId = line.packageFormatId,
                    packageDisplayName = line.packageDisplayName,
                    packageCount = line.packageCount,
                    totalNetQuantityMicrounits = line.totalNetQuantityMicrounits
                });
            }
        }
        catch (OverflowException)
        {
            error = "Overflow reconstruyendo el handoff persistente del PurchaseOrder.";
            return false;
        }

        result.totalPackageCount = totalPackages;
        result.totalNetQuantityMicrounits = totalMicro;
        handoff = result;
        return true;
    }

    private void HandleReceivingHandoff(BistroBuilderSupplierReceivingHandoff handoff)
    {
        BistroBuilderGoodsReceiptSnapshot receipt;
        BistroBuilderPurchaseOrderRecord delivered;
        string error;
        if (!TryAcceptHandoff(handoff, out receipt, out delivered, out error))
        {
            Debug.LogError("2.3L — No se pudo integrar ReceivingHandoff. " + error, this);
        }
        else if (receipt != null)
        {
            Debug.Log(
                "2.3L — Recepción canónica completada. PO=" + delivered.purchaseOrderId +
                " ReceiptId=" + receipt.ReceiptId +
                " replay=" + receipt.WasReplayed + ".",
                this
            );
        }
    }

    private void ResolveDependencies()
    {
        if (goodsReceivingService == null)
        {
            TryGetComponent(out goodsReceivingService);
            if (goodsReceivingService == null)
            {
                goodsReceivingService = UnityEngine.Object.FindFirstObjectByType<BistroBuilderGoodsReceivingService>();
            }
        }
        deliveryService = BistroBuilderSupplierDeliveryPresentationService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierDeliveryPresentationService>();
        orderService = BistroBuilderSupplierPurchaseOrderService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        logisticsService = BistroBuilderSupplierLogisticsService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierLogisticsService>();
    }

    private void Subscribe()
    {
        if (subscribed || deliveryService == null)
        {
            return;
        }
        deliveryService.ReceivingHandoffReady += HandleReceivingHandoff;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || deliveryService == null)
        {
            subscribed = false;
            return;
        }
        deliveryService.ReceivingHandoffReady -= HandleReceivingHandoff;
        subscribed = false;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }

    private void OnValidate()
    {
        if (goodsReceivingService == null)
        {
            TryGetComponent(out goodsReceivingService);
        }
    }
#endif
}
