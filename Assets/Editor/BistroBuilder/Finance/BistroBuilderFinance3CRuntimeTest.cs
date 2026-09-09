using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderFinance3CRuntimeTest
{
    private const string ArmedKey = "BB.Finance.3C.Runtime.Armed";
    private const string ResultKey = "BB.Finance.3C.Runtime.Result";
    private const double StartupTimeoutSeconds = 20d;

    private static double startupDeadline;
    private static int capturedErrors;

    static BistroBuilderFinance3CRuntimeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Bistro Builder/Finanzas/3C - Prueba runtime real", false, 3023)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3C",
                "La prueba runtime 3C ya está en ejecución.",
                "Aceptar");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3C",
                "Sal de Play Mode antes de iniciar la prueba automática 3C.",
                "Aceptar");
            return;
        }

        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3C",
                "No se pudo guardar la escena antes de iniciar la prueba.",
                "Aceptar");
            return;
        }

        SessionState.SetBool(ArmedKey, true);
        SessionState.EraseString(ResultKey);
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            startupDeadline = EditorApplication.timeSinceStartup + StartupTimeoutSeconds;
            EditorApplication.update -= TryRunWhenReady;
            EditorApplication.update += TryRunWhenReady;
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            Cleanup();
            SessionState.SetBool(ArmedKey, false);
            SessionState.SetString(
                ResultKey,
                "Prueba cancelada antes de completar 3C.");
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            string result = SessionState.GetString(ResultKey, string.Empty);
            if (!string.IsNullOrEmpty(result))
            {
                SessionState.EraseString(ResultKey);
                EditorUtility.DisplayDialog(
                    "Bistro Builder — 3C",
                    result,
                    "Aceptar");
            }
        }
    }

    private static void TryRunWhenReady()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false))
        {
            Cleanup();
            return;
        }

        BistroBuilderFinanceService finance =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();
        BistroBuilderSupplierPurchaseFinanceBridge bridge =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseFinanceBridge>();
        BistroBuilderSupplierPurchaseOrderService orders =
            BistroBuilderSupplierPurchaseOrderService.Instance;
        BistroBuilderSupplierMarketService market =
            BistroBuilderSupplierMarketService.Instance;
        BistroBuilderSupplierCommercialIntelligenceService commercial =
            BistroBuilderSupplierCommercialIntelligenceService.Instance;
        BistroBuilderSupplierLogisticsService logistics =
            BistroBuilderSupplierLogisticsService.Instance;
        BistroBuilderGeneralGameStateService general =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        GameClock clock = UnityEngine.Object.FindFirstObjectByType<GameClock>();

        bool ready =
            finance != null && finance.IsInitialized &&
            bridge != null && bridge.IsBound &&
            orders != null && orders.IsInitialized &&
            market != null && market.IsInitialized &&
            commercial != null && commercial.IsInitialized &&
            logistics != null && logistics.IsInitialized &&
            general != null && clock != null;

        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >= startupDeadline)
            {
                Fail("Las autoridades runtime de Finanzas/Proveedores no estuvieron listas a tiempo.");
            }
            return;
        }

        EditorApplication.update -= TryRunWhenReady;
        Execute(finance, bridge, orders, commercial, logistics, general, clock);
    }

    private static void Execute(
        BistroBuilderFinanceService finance,
        BistroBuilderSupplierPurchaseFinanceBridge bridge,
        BistroBuilderSupplierPurchaseOrderService orderService,
        BistroBuilderSupplierCommercialIntelligenceService commercialService,
        BistroBuilderSupplierLogisticsService logisticsService,
        BistroBuilderGeneralGameStateService generalState,
        GameClock clock)
    {
        capturedErrors = 0;
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;

        try
        {
            Require(finance.ValidateConfiguration(out string error),
                "La autoridad financiera 3A no es válida. " + error);
            Require(bridge.ValidateConfiguration(out error),
                "El bridge 3C no es válido. " + error);

            BistroBuilderFinanceSnapshot originalFinance = finance.CreateSnapshot();
            BistroBuilderSupplierPurchaseOrdersSnapshot originalOrders =
                orderService.CreateSnapshot();
            BistroBuilderSupplierLogisticsSnapshot originalLogistics =
                logisticsService.CreateSnapshot();
            Require(originalFinance != null &&
                    originalOrders != null &&
                    originalLogistics != null,
                "No se pudieron capturar los snapshots de seguridad.");

            Require(bridge.TryGetFinancialPosition(
                    out long baselineCommitted,
                    out long baselineAvailable,
                    out error),
                "No se pudo leer la posición financiera inicial. " + error);

            long baselineBalance = finance.CurrentBalanceCents;
            int baselineTransactions = finance.TransactionCount;
            Require(baselineAvailable == baselineBalance - baselineCommitted,
                "La caja disponible inicial no coincide con saldo menos compromisos.");

            Require(TryCreateAffordableDraft(
                    orderService,
                    commercialService,
                    finance,
                    out BistroBuilderSupplierAuthoringRecord supplier,
                    out BistroBuilderSupplierBaseOfferAuthoringRecord offer,
                    out int packageCount,
                    out BistroBuilderPurchaseOrderRecord draft,
                    out BistroBuilderPurchaseOrderConfirmationPreview preview,
                    out error),
                "No se pudo crear un PurchaseOrder real asequible. " + error);

            long targetInsufficientBalance = preview.totalCents - 1L;
            long diagnosticDebit = baselineBalance - targetInsufficientBalance;
            Require(diagnosticDebit > 0L,
                "La precondición de fondos insuficientes no pudo construirse.");

            BistroBuilderFinanceTransactionRequest diagnosticRequest =
                new BistroBuilderFinanceTransactionRequest
                {
                    operationId = "diagnostic_3c_insufficient_funds",
                    sourceSystemId = "finance.test",
                    sourceReferenceId = "3c_runtime",
                    categoryId = "diagnostic.finance",
                    kind = BistroBuilderFinanceTransactionKind.Debit,
                    amountCents = diagnosticDebit,
                    dayIndex = generalState.DayIndex,
                    minuteOfDay = clock.Hour * 60 + clock.Minute,
                    description = "Precondición temporal 3C"
                };
            Require(finance.TryPostTransaction(diagnosticRequest, out _, out error),
                "No se pudo preparar la precondición financiera temporal. " + error);
            Require(!orderService.TryConfirmOrder(draft.purchaseOrderId, out _, out error),
                "2.3E permitió confirmar un pedido sin fondos disponibles.");
            Require(orderService.TryGetOrder(draft.purchaseOrderId, out var stillDraft) &&
                    stillDraft.status == BistroBuilderPurchaseOrderStatus.Draft,
                "El pedido cambió de estado pese al bloqueo financiero.");
            Require(finance.TryRestoreSnapshot(originalFinance, out error),
                "No se pudo retirar la precondición financiera temporal. " + error);

            Require(orderService.TryConfirmOrder(
                    draft.purchaseOrderId,
                    out BistroBuilderPurchaseOrderConfirmationReceipt confirmedReceipt,
                    out error),
                "La confirmación real con fondos disponibles falló. " + error);
            Require(confirmedReceipt != null && confirmedReceipt.totalCents == preview.totalCents,
                "El receipt confirmado no conserva el total económico real.");
            Require(finance.CurrentBalanceCents == baselineBalance &&
                    finance.TransactionCount == baselineTransactions,
                "Confirmar un pedido debitó caja antes de la expedición.");
            Require(bridge.TryGetFinancialPosition(
                    out long committedAfterConfirm,
                    out long availableAfterConfirm,
                    out error),
                "No se pudo leer el compromiso tras confirmar. " + error);
            Require(committedAfterConfirm == baselineCommitted + preview.totalCents &&
                    availableAfterConfirm == baselineBalance - committedAfterConfirm,
                "Confirmed no reservó exactamente el total del PurchaseOrder.");

            Require(orderService.TryCancelOrder(
                    draft.purchaseOrderId,
                    "Prueba runtime 3C",
                    out _,
                    out error),
                "No se pudo cancelar el pedido de prueba. " + error);
            Require(bridge.TryGetFinancialPosition(
                    out long committedAfterCancel,
                    out _,
                    out error) && committedAfterCancel == baselineCommitted,
                "Cancelar no liberó el compromiso de caja. " + error);
            Require(finance.CurrentBalanceCents == baselineBalance &&
                    finance.TransactionCount == baselineTransactions,
                "Cancelar un compromiso alteró la caja real.");

            Require(orderService.TryCreateDraft(
                    supplier.SupplierId,
                    out BistroBuilderPurchaseOrderRecord dispatchDraft,
                    out error),
                "No se pudo crear el pedido de expedición. " + error);
            Require(orderService.TrySetDraftLine(
                    dispatchDraft.purchaseOrderId,
                    offer.SupplierOfferId,
                    packageCount,
                    out _,
                    out error),
                "No se pudo reproducir la línea real del pedido. " + error);
            Require(orderService.TryBuildConfirmationPreview(
                    dispatchDraft.purchaseOrderId,
                    out BistroBuilderPurchaseOrderConfirmationPreview dispatchPreview,
                    out error) && dispatchPreview.canConfirm,
                "El segundo pedido no produjo una cotización confirmable. " + error);
            Require(orderService.TryConfirmOrder(
                    dispatchDraft.purchaseOrderId,
                    out _,
                    out error),
                "No se pudo confirmar el pedido de expedición. " + error);

            // 2.3G escucha OrderConfirmed y normalmente crea el LogisticsPlan de
            // forma síncrona. La prueba 3C no debe intentar adjuntar un segundo
            // plan artificial al mismo PurchaseOrder. TryCreatePlanForOrder es
            // idempotente: devuelve el plan existente o lo crea si aún no existe.
            Require(logisticsService.TryCreatePlanForOrder(
                    dispatchDraft.purchaseOrderId,
                    out BistroBuilderSupplierLogisticsPlanRecord logisticsPlan,
                    out error),
                "2.3G no pudo resolver el LogisticsPlan real del pedido. " + error);
            Require(logisticsPlan != null &&
                    orderService.TryGetOrder(
                        dispatchDraft.purchaseOrderId,
                        out BistroBuilderPurchaseOrderRecord pendingOrder) &&
                    pendingOrder.status == BistroBuilderPurchaseOrderStatus.PendingDelivery &&
                    string.Equals(
                        pendingOrder.logisticsPlanId,
                        logisticsPlan.logisticsPlanId,
                        StringComparison.Ordinal),
                "El PurchaseOrder y su LogisticsPlan 2.3G no convergen en PendingDelivery.");

            int currentDay = orderService.CurrentGameDay;
            Require(bridge.TryGetFinancialPosition(
                    out long committedPending,
                    out _,
                    out error) &&
                    committedPending == baselineCommitted + dispatchPreview.totalCents,
                "PendingDelivery no conservó el compromiso económico. " + error);

            long balanceBeforeDispatch = finance.CurrentBalanceCents;
            int transactionsBeforeDispatch = finance.TransactionCount;
            Require(orderService.TryMarkInDelivery(
                    dispatchDraft.purchaseOrderId,
                    currentDay,
                    0,
                    out BistroBuilderPurchaseOrderRecord inDelivery,
                    out error),
                "No se pudo iniciar InDelivery. " + error);
            Require(inDelivery != null &&
                    finance.CurrentBalanceCents ==
                        balanceBeforeDispatch - dispatchPreview.totalCents &&
                    finance.TransactionCount == transactionsBeforeDispatch + 1,
                "InDelivery no convirtió el compromiso en una única salida real.");
            Require(bridge.TryGetFinancialPosition(
                    out long committedAfterDispatch,
                    out _,
                    out error) && committedAfterDispatch == baselineCommitted,
                "El compromiso no se liberó al convertirse en pago real. " + error);

            string paymentOperationId =
                BistroBuilderSupplierPurchaseFinancePolicy.BuildDebitOperationId(
                    dispatchDraft.purchaseOrderId);
            Require(finance.TryGetTransactionByOperationId(
                    paymentOperationId,
                    out BistroBuilderFinanceTransactionRecord payment) &&
                    payment.kind == BistroBuilderFinanceTransactionKind.Debit &&
                    payment.amountCents == dispatchPreview.totalCents &&
                    payment.sourceSystemId ==
                        BistroBuilderSupplierPurchaseFinancePolicy.SourceSystemId &&
                    payment.categoryId ==
                        BistroBuilderSupplierPurchaseFinancePolicy.CategoryId,
                "El ledger no conserva el pago canónico esperado del proveedor.");

            Require(orderService.TryMarkInDelivery(
                    dispatchDraft.purchaseOrderId,
                    currentDay,
                    0,
                    out _,
                    out error) &&
                    finance.TransactionCount == transactionsBeforeDispatch + 1,
                "Reintentar InDelivery duplicó el pago financiero. " + error);

            Require(orderService.TryMarkDelivered(
                    dispatchDraft.purchaseOrderId,
                    "receipt_3c_runtime",
                    currentDay,
                    out _,
                    out error),
                "No se pudo cerrar el estado Delivered de diagnóstico. " + error);
            Require(finance.TransactionCount == transactionsBeforeDispatch + 1 &&
                    finance.CurrentBalanceCents ==
                        balanceBeforeDispatch - dispatchPreview.totalCents,
                "Delivered volvió a cobrar un PurchaseOrder ya pagado.");

            Require(capturedErrors == 0,
                "La prueba capturó Error/Exception/Assert: " + capturedErrors + ".");

            string result =
                "PRUEBA RUNTIME 3C SUPERADA" +
                "\n\nPedido real: " + dispatchPreview.displayCode +
                "\nTotal: " + FormatMoney(dispatchPreview.totalCents) +
                " (portes: " + FormatMoney(dispatchPreview.shippingCostCents) + ")" +
                "\nBloqueo sin fondos: OK" +
                "\nPlan logístico 2.3G: " + logisticsPlan.logisticsPlanId +
                "\nCompromiso: " + FormatMoney(baselineCommitted) + " -> " +
                FormatMoney(baselineCommitted + dispatchPreview.totalCents) + " -> " +
                FormatMoney(baselineCommitted) +
                "\nCaja: " + FormatMoney(balanceBeforeDispatch) + " -> " +
                FormatMoney(finance.CurrentBalanceCents) +
                "\nMovimientos: " + transactionsBeforeDispatch + " -> " +
                finance.TransactionCount +
                "\nError/Exception/Assert: 0";

            // El test es enteramente transitorio: restauramos también 2.3G,
            // porque confirmar pedidos crea LogisticsPlans reales automáticamente.
            Require(orderService.TryRestoreSnapshot(originalOrders, out error),
                "No se pudo restaurar el snapshot original de pedidos. " + error);
            Require(logisticsService.TryRestoreSnapshot(originalLogistics, out error),
                "No se pudo restaurar el snapshot original de logística. " + error);
            Require(finance.TryRestoreSnapshot(originalFinance, out error),
                "No se pudo restaurar el snapshot financiero original. " + error);

            Complete(result);
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private static bool TryCreateAffordableDraft(
        BistroBuilderSupplierPurchaseOrderService orderService,
        BistroBuilderSupplierCommercialIntelligenceService commercialService,
        BistroBuilderFinanceService finance,
        out BistroBuilderSupplierAuthoringRecord selectedSupplier,
        out BistroBuilderSupplierBaseOfferAuthoringRecord selectedOffer,
        out int selectedPackageCount,
        out BistroBuilderPurchaseOrderRecord selectedDraft,
        out BistroBuilderPurchaseOrderConfirmationPreview selectedPreview,
        out string error)
    {
        selectedSupplier = null;
        selectedOffer = null;
        selectedPackageCount = 0;
        selectedDraft = null;
        selectedPreview = null;
        error = string.Empty;

        BistroBuilderSupplierAuthoringDatabase suppliers =
            Resources.Load<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSupplierPurchaseOrderService.SupplierAuthoringResourcePath);
        if (suppliers == null)
        {
            error = "No se encontró supplier.authoring.";
            return false;
        }

        if (!commercialService.TrySynchronizeCurrentMarketState(out error))
        {
            return false;
        }

        for (int supplierIndex = 0;
             supplierIndex < suppliers.Suppliers.Count;
             supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier =
                suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null)
            {
                continue;
            }

            for (int offerIndex = 0;
                 offerIndex < supplier.baseOffers.Count;
                 offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                    supplier.baseOffers[offerIndex];
                if (offer == null || !offer.isActive ||
                    !commercialService.TryGetCommercialQuote(
                        offer.SupplierOfferId,
                        out BistroBuilderSupplierCommercialQuote quote) ||
                    quote == null || !quote.availableForNewOrders ||
                    quote.effectivePriceCents <= 0L)
                {
                    continue;
                }

                if (!TryCalculatePackageCount(
                        supplier,
                        offer,
                        quote.effectivePriceCents,
                        out int packageCount))
                {
                    continue;
                }

                if (!orderService.TryCreateDraft(
                        supplier.SupplierId,
                        out BistroBuilderPurchaseOrderRecord draft,
                        out error))
                {
                    continue;
                }

                if (!orderService.TrySetDraftLine(
                        draft.purchaseOrderId,
                        offer.SupplierOfferId,
                        packageCount,
                        out _,
                        out error) ||
                    !orderService.TryBuildConfirmationPreview(
                        draft.purchaseOrderId,
                        out BistroBuilderPurchaseOrderConfirmationPreview preview,
                        out error))
                {
                    orderService.TryCancelOrder(
                        draft.purchaseOrderId,
                        "Descartado por prueba 3C",
                        out _,
                        out _);
                    continue;
                }

                if (preview.canConfirm &&
                    preview.totalCents > 0L &&
                    preview.totalCents <= finance.CurrentBalanceCents)
                {
                    selectedSupplier = supplier;
                    selectedOffer = offer;
                    selectedPackageCount = packageCount;
                    selectedDraft = draft;
                    selectedPreview = preview;
                    return true;
                }

                orderService.TryCancelOrder(
                    draft.purchaseOrderId,
                    "Descartado por prueba 3C",
                    out _,
                    out _);
            }
        }

        error = "No existe una oferta runtime disponible cuyo pedido mínimo sea asequible.";
        return false;
    }

    private static bool TryCalculatePackageCount(
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        long effectivePriceCents,
        out int packageCount)
    {
        packageCount = 0;
        if (supplier == null || offer == null || effectivePriceCents <= 0L)
        {
            return false;
        }

        long target = Math.Max(1L, supplier.minimumOrderValueCents);
        long raw = (target + effectivePriceCents - 1L) / effectivePriceCents;
        int minimum = Math.Max(1, offer.minimumPackageCount);
        int increment = Math.Max(1, offer.orderIncrement);
        if (raw > int.MaxValue)
        {
            return false;
        }

        packageCount = (int)Math.Max(minimum, raw);
        if (packageCount > minimum)
        {
            int remainder = (packageCount - minimum) % increment;
            if (remainder != 0)
            {
                long aligned = (long)packageCount + increment - remainder;
                if (aligned > int.MaxValue)
                {
                    return false;
                }
                packageCount = (int)aligned;
            }
        }

        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error ||
            type == LogType.Exception ||
            type == LogType.Assert)
        {
            capturedErrors++;
        }
    }

    private static void Fail(string message)
    {
        Complete("PRUEBA RUNTIME 3C NO SUPERADA\n\n" + message);
    }

    private static void Complete(string result)
    {
        Cleanup();
        SessionState.SetBool(ArmedKey, false);
        SessionState.SetString(ResultKey, result);

        if (EditorApplication.isPlaying)
        {
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= TryRunWhenReady;
        Application.logMessageReceived -= HandleLog;
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("N2") + " €";
    }
}
