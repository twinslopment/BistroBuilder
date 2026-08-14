using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderFinance3DRuntimeTest
{
    private const string ArmedKey = "BB.Finance.3D.Runtime.Armed";
    private const string ResultKey = "BB.Finance.3D.Runtime.Result";
    private const double StartupTimeoutSeconds = 20d;
    private const double ConsumptionTimeoutSeconds = 90d;
    private const long MinimumDiagnosticStockMilliUnits = 1000000L;
    private const string DiagnosticSourceId = "finance_3d_runtime";
    private const string DiagnosticReceiptId = "receipt_3d_runtime";
    private const string DiagnosticPurchaseOrderId = "purchase_order_3d_runtime";

    private static double startupDeadline;
    private static double consumptionDeadline;
    private static int capturedErrors;
    private static BistroBuilderProductCostService productCost;
    private static BistroBuilderFinanceService finance;
    private static long baselineBalance;
    private static int baselineFinanceTransactions;
    private static int baselineLineCosts;
    private static int diagnosticActualLotCount;
    private static long diagnosticSubtotalCents;

    static BistroBuilderFinance3DRuntimeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Bistro Builder/Finanzas/3D - Prueba runtime real", false, 3033)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3D",
                "La prueba runtime 3D ya está en ejecución.",
                "Aceptar");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3D",
                "Sal de Play Mode antes de iniciar la prueba automática 3D.",
                "Aceptar");
            return;
        }

        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3D",
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
            startupDeadline =
                EditorApplication.timeSinceStartup + StartupTimeoutSeconds;
            EditorApplication.update -= TryArmWhenReady;
            EditorApplication.update += TryArmWhenReady;
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            Cleanup();
            SessionState.SetBool(ArmedKey, false);
            SessionState.SetString(
                ResultKey,
                "Prueba cancelada antes de completar 3D.");
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            string result = SessionState.GetString(ResultKey, string.Empty);
            if (!string.IsNullOrEmpty(result))
            {
                SessionState.EraseString(ResultKey);
                EditorUtility.DisplayDialog(
                    "Bistro Builder — 3D",
                    result,
                    "Aceptar");
            }
        }
    }

    private static void TryArmWhenReady()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false))
        {
            Cleanup();
            return;
        }

        productCost =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderProductCostService>();
        finance =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();
        BistroBuilderGoodsReceivingService receiving =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderGoodsReceivingService>();
        BistroBuilderInventoryService inventory =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderInventoryService>();
        BistroBuilderRecipeCatalogService recipes =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderRecipeCatalogService>();
        RestaurantServiceStateService serviceState =
            UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();
        CustomerGroupSpawner spawner =
            UnityEngine.Object.FindFirstObjectByType<CustomerGroupSpawner>();

        bool ready =
            productCost != null && productCost.IsInitialized &&
            finance != null && finance.IsInitialized &&
            receiving != null &&
            inventory != null && inventory.IsInitialized &&
            recipes != null &&
            serviceState != null &&
            spawner != null;

        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >= startupDeadline)
            {
                Fail(
                    "Las autoridades runtime de 3D no estuvieron listas a tiempo.");
            }
            return;
        }

        EditorApplication.update -= TryArmWhenReady;

        if (!PrepareActualSupplierStock(
                receiving,
                inventory,
                recipes,
                out string error))
        {
            Fail(error);
            return;
        }

        baselineBalance = finance.CurrentBalanceCents;
        baselineFinanceTransactions = finance.TransactionCount;
        baselineLineCosts = productCost.ConsumedLineCostCount;
        capturedErrors = 0;

        productCost.LineCostRecorded -= HandleLineCostRecorded;
        productCost.LineCostRecorded += HandleLineCostRecorded;
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;

        if (!spawner.TryConfigureDiagnosticGroupSizes(new[] { 1 }, out error) ||
            !spawner.TryConfigureDiagnosticServiceModes(
                new[] { BistroBuilderServiceMode.TableService },
                out error))
        {
            Fail("No se pudo preparar una llegada determinista. " + error);
            return;
        }

        if (serviceState.CurrentState == RestaurantServiceState.Closing)
        {
            Fail(
                "El servicio está cerrándose y no puede iniciar la prueba 3D.");
            return;
        }

        if (serviceState.CurrentState != RestaurantServiceState.Open &&
            !serviceState.TryOpenService())
        {
            Fail("No se pudo abrir automáticamente el servicio para 3D.");
            return;
        }

        consumptionDeadline =
            EditorApplication.timeSinceStartup + ConsumptionTimeoutSeconds;
        EditorApplication.update -= CheckConsumptionTimeout;
        EditorApplication.update += CheckConsumptionTimeout;

        Debug.Log(
            "3D — Prueba runtime armada. Todo el stock disponible usa " +
            "lotes de proveedor con coste real; esperando la primera " +
            "preparación real de un plato.");
    }

    private static bool PrepareActualSupplierStock(
        BistroBuilderGoodsReceivingService receiving,
        BistroBuilderInventoryService inventory,
        BistroBuilderRecipeCatalogService recipes,
        out string error)
    {
        error = string.Empty;

        var stock = new List<BistroBuilderInventoryStockSnapshot>();
        inventory.CopyStockSnapshotsTo(stock);
        if (stock.Count == 0)
        {
            error =
                "El inventario no contiene ingredientes para la prueba 3D.";
            return false;
        }

        var receiptLines =
            new List<BistroBuilderInventoryQuantityLine>(stock.Count);
        var confirmedLines =
            new List<BistroBuilderPurchaseOrderConfirmedLineSnapshot>(
                stock.Count);

        diagnosticSubtotalCents = 0L;

        for (int index = 0; index < stock.Count; index++)
        {
            BistroBuilderInventoryStockSnapshot entry = stock[index];
            if (entry.ReservedCanonicalMilliUnits != 0L)
            {
                error =
                    "La prueba 3D necesita iniciar sin reservas activas; " +
                    entry.IngredientId + " tiene stock reservado.";
                return false;
            }

            long originalOnHand = entry.OnHandCanonicalMilliUnits;
            if (originalOnHand > 0L &&
                !inventory.TryRegisterWaste(
                    "diagnostic_3d_clear_" + entry.IngredientId,
                    DiagnosticSourceId,
                    entry.IngredientId,
                    originalOnHand,
                    "Sustitución transitoria por lote de proveedor para 3D.",
                    out error))
            {
                error =
                    "No se pudo retirar el stock de referencia de " +
                    entry.IngredientId + ". " + error;
                return false;
            }

            long replacementQuantity =
                Math.Max(originalOnHand, MinimumDiagnosticStockMilliUnits);

            if (!recipes.TryGetIngredient(
                    entry.IngredientId,
                    out BistroBuilderIngredientDefinition ingredient) ||
                ingredient == null ||
                !ingredient.TryCalculateCostMicroCents(
                    replacementQuantity,
                    out long referenceMicroCents,
                    out error))
            {
                error =
                    "No se pudo valorar el stock diagnóstico de " +
                    entry.IngredientId + ". " + error;
                return false;
            }

            long referenceCents =
                BistroBuilderProductCostEngine.RoundMicroCentsToCents(
                    referenceMicroCents);
            long supplierSubtotal;
            long quantityMicrounits;

            try
            {
                long uplift = Math.Max(1L, referenceCents / 4L);
                supplierSubtotal = checked(referenceCents + uplift);
                quantityMicrounits = checked(replacementQuantity * 1000L);
                diagnosticSubtotalCents =
                    checked(diagnosticSubtotalCents + supplierSubtotal);
            }
            catch (OverflowException)
            {
                error =
                    "La valoración diagnóstica de " + entry.IngredientId +
                    " queda fuera de rango.";
                return false;
            }

            receiptLines.Add(
                new BistroBuilderInventoryQuantityLine(
                    entry.IngredientId,
                    replacementQuantity));

            confirmedLines.Add(
                new BistroBuilderPurchaseOrderConfirmedLineSnapshot
                {
                    ingredientId = entry.IngredientId,
                    totalNetQuantityMicrounits = quantityMicrounits,
                    lineSubtotalCents = supplierSubtotal
                });
        }

        if (!receiving.TryReceiveGoods(
                DiagnosticReceiptId,
                DiagnosticSourceId,
                receiptLines,
                "Recepción diagnóstica transitoria 3D.",
                out BistroBuilderGoodsReceiptSnapshot receipt,
                out error))
        {
            error =
                "2.2B rechazó la recepción diagnóstica 3D. " + error;
            return false;
        }

        if (receipt == null ||
            receipt.WasReplayed ||
            receipt.CreatedLots.Count != receiptLines.Count)
        {
            error =
                "2.2B no devolvió exactamente los lotes creados para 3D.";
            return false;
        }

        const long diagnosticShippingCents = 800L;
        long diagnosticTotalCents;
        try
        {
            diagnosticTotalCents =
                checked(diagnosticSubtotalCents + diagnosticShippingCents);
        }
        catch (OverflowException)
        {
            error =
                "El total diagnóstico del PurchaseOrder queda fuera de rango.";
            return false;
        }

        var deliveredOrder = new BistroBuilderPurchaseOrderRecord
        {
            purchaseOrderId = DiagnosticPurchaseOrderId,
            deliveryReceiptId = receipt.ReceiptId,
            status = BistroBuilderPurchaseOrderStatus.Delivered,
            subtotalCents = diagnosticSubtotalCents,
            shippingCostCents = diagnosticShippingCents,
            totalCents = diagnosticTotalCents,
            confirmedLines = confirmedLines
        };

        if (!productCost.TryApplySupplierReceipt(
                receipt,
                deliveredOrder,
                out error))
        {
            error =
                "3D no aceptó la valoración real de los lotes diagnósticos. " +
                error;
            return false;
        }

        long actualBasisTotalMicroCents = 0L;
        for (int index = 0; index < receipt.CreatedLots.Count; index++)
        {
            BistroBuilderInventoryLotSnapshot lot = receipt.CreatedLots[index];
            if (!productCost.TryGetLotCostBasis(
                    lot.LotId,
                    out BistroBuilderLotCostBasisRecord basis) ||
                basis == null ||
                basis.basisKind != BistroBuilderLotCostBasisKind.SupplierActual ||
                !string.Equals(
                    basis.sourceReferenceId,
                    DiagnosticPurchaseOrderId,
                    StringComparison.Ordinal))
            {
                error =
                    "El lote " + lot.LotId +
                    " no conserva una base real de proveedor.";
                return false;
            }

            try
            {
                actualBasisTotalMicroCents =
                    checked(
                        actualBasisTotalMicroCents +
                        basis.totalCostMicroCents);
            }
            catch (OverflowException)
            {
                error =
                    "La suma de bases reales de proveedor queda fuera de rango.";
                return false;
            }
        }

        long expectedBasisTotalMicroCents;
        try
        {
            expectedBasisTotalMicroCents =
                checked(
                    diagnosticSubtotalCents *
                    BistroBuilderIngredientDefinition.MicroCentsPerCent);
        }
        catch (OverflowException)
        {
            error =
                "La valoración total de proveedor queda fuera de rango.";
            return false;
        }

        if (actualBasisTotalMicroCents != expectedBasisTotalMicroCents)
        {
            error =
                "La suma de costes reales de los lotes no coincide con el " +
                "subtotal congelado del PurchaseOrder diagnóstico.";
            return false;
        }

        diagnosticActualLotCount = receipt.CreatedLots.Count;
        return true;
    }

    private static void HandleLineCostRecorded(
        BistroBuilderConsumedLineCostRecord record)
    {
        if (record == null)
        {
            Fail("3D publicó un registro de coste nulo.");
            return;
        }

        bool identityOk =
            productCost.TryGetLineCost(
                record.lineId,
                out BistroBuilderConsumedLineCostRecord stored) &&
            stored != null &&
            stored.costRecordId == record.costRecordId;

        bool mathOk =
            record.theoreticalCostCents ==
                BistroBuilderProductCostEngine.RoundMicroCentsToCents(
                    record.theoreticalCostMicroCents) &&
            record.actualCostCents ==
                BistroBuilderProductCostEngine.RoundMicroCentsToCents(
                    record.actualCostMicroCents) &&
            record.theoreticalMarginCents ==
                record.salePriceCents - record.theoreticalCostCents &&
            record.actualMarginCents ==
                record.salePriceCents - record.actualCostCents;

        bool countOk =
            productCost.ConsumedLineCostCount == baselineLineCosts + 1;
        bool cashUntouched =
            finance.CurrentBalanceCents == baselineBalance &&
            finance.TransactionCount == baselineFinanceTransactions;
        bool actualCostProven =
            record.costQuality == BistroBuilderProductCostQuality.Actual &&
            record.actualCostMicroCents > 0L &&
            record.actualCostMicroCents != record.theoreticalCostMicroCents;

        if (!identityOk ||
            !mathOk ||
            !countOk ||
            !cashUntouched ||
            !actualCostProven ||
            capturedErrors != 0)
        {
            Fail(
                "Se registró consumo real, pero la auditoría 3D no fue limpia." +
                "\nIdentidad: " + identityOk +
                "\nCálculos: " + mathOk +
                "\nRegistro único: " + countOk +
                "\nCaja intacta: " + cashUntouched +
                "\nCoste proveedor realmente consumido: " + actualCostProven +
                "\nCalidad: " + record.costQuality +
                "\nError/Exception/Assert: " + capturedErrors);
            return;
        }

        Complete(
            "PRUEBA RUNTIME 3D SUPERADA" +
            "\n\nPlato real: " + record.dishId +
            "\nPrecio de venta: " + FormatMoney(record.salePriceCents) +
            "\nCoste teórico: " + FormatMoney(record.theoreticalCostCents) +
            "\nCoste real consumido: " + FormatMoney(record.actualCostCents) +
            "\nCalidad coste: Actual" +
            "\nMargen teórico: " +
            FormatMoney(record.theoreticalMarginCents) +
            "\nMargen real: " + FormatMoney(record.actualMarginCents) +
            " (" +
            (record.actualMarginBasisPoints / 100m).ToString("N2") + "%)" +
            "\nLotes reales de proveedor: " +
            diagnosticActualLotCount +
            " / subtotal " + FormatMoney(diagnosticSubtotalCents) +
            " (portes excluidos: OK)" +
            "\nCaja/ledger financiero: sin cambios por COGS" +
            "\nError/Exception/Assert: 0");
    }

    private static void CheckConsumptionTimeout()
    {
        if (EditorApplication.timeSinceStartup >= consumptionDeadline)
        {
            Fail(
                "No comenzó la preparación de ningún plato real dentro del " +
                "tiempo de prueba.");
        }
    }

    private static void HandleLog(
        string condition,
        string stackTrace,
        LogType type)
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
        Complete("PRUEBA RUNTIME 3D NO SUPERADA\n\n" + message);
    }

    private static void Complete(string result)
    {
        Cleanup();
        SessionState.SetBool(ArmedKey, false);
        SessionState.SetString(ResultKey, result);
        if (EditorApplication.isPlaying)
        {
            EditorApplication.delayCall += () =>
                EditorApplication.isPlaying = false;
        }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= TryArmWhenReady;
        EditorApplication.update -= CheckConsumptionTimeout;
        Application.logMessageReceived -= HandleLog;

        if (productCost != null)
        {
            productCost.LineCostRecorded -= HandleLineCostRecorded;
        }

        productCost = null;
        finance = null;
        diagnosticActualLotCount = 0;
        diagnosticSubtotalCents = 0L;
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("N2") + " €";
    }
}
