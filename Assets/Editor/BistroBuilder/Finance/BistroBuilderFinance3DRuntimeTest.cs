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

    private static double startupDeadline;
    private static double consumptionDeadline;
    private static int capturedErrors;
    private static BistroBuilderProductCostService productCost;
    private static BistroBuilderFinanceService finance;
    private static long baselineBalance;
    private static int baselineFinanceTransactions;
    private static int baselineLineCosts;
    private static string diagnosticLotId;
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
            startupDeadline = EditorApplication.timeSinceStartup + StartupTimeoutSeconds;
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

        productCost = UnityEngine.Object.FindFirstObjectByType<BistroBuilderProductCostService>();
        finance = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();
        BistroBuilderGoodsReceivingService receiving =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderGoodsReceivingService>();
        BistroBuilderInventoryService inventory =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderInventoryService>();
        RestaurantServiceStateService serviceState =
            UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();
        CustomerGroupSpawner spawner =
            UnityEngine.Object.FindFirstObjectByType<CustomerGroupSpawner>();

        bool ready = productCost != null && productCost.IsInitialized &&
                     finance != null && finance.IsInitialized &&
                     receiving != null && inventory != null && inventory.IsInitialized &&
                     serviceState != null && spawner != null;
        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >= startupDeadline)
            {
                Fail("Las autoridades runtime de 3D no estuvieron listas a tiempo.");
            }
            return;
        }

        EditorApplication.update -= TryArmWhenReady;
        if (!PrepareDiagnosticSupplierLot(receiving, inventory, out string error))
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
            Fail("El servicio está cerrándose y no puede iniciar la prueba 3D.");
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
            "3D — Prueba runtime armada. Lote de proveedor valorado; " +
            "esperando la primera preparación real de un plato.");
    }

    private static bool PrepareDiagnosticSupplierLot(
        BistroBuilderGoodsReceivingService receiving,
        BistroBuilderInventoryService inventory,
        out string error
    )
    {
        error = string.Empty;
        var lots = new List<BistroBuilderInventoryLotSnapshot>();
        inventory.CopyLotSnapshotsTo(lots);
        if (lots.Count == 0)
        {
            error = "El inventario no contiene ningún ingrediente para la prueba 3D.";
            return false;
        }

        string ingredientId = lots[0].IngredientId;
        const long receivedMilliUnits = 1000L;
        if (!receiving.TryReceiveGoods(
                "receipt_3d_runtime",
                "supplier_3d_runtime",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        ingredientId,
                        receivedMilliUnits)
                },
                "Recepción diagnóstica transitoria 3D.",
                out BistroBuilderGoodsReceiptSnapshot receipt,
                out error))
        {
            error = "2.2B rechazó la recepción diagnóstica 3D. " + error;
            return false;
        }

        if (receipt == null || receipt.WasReplayed || receipt.CreatedLots.Count != 1)
        {
            error = "2.2B no identificó exactamente el lote creado por la recepción 3D.";
            return false;
        }

        diagnosticSubtotalCents = 1234L;
        const long diagnosticShippingCents = 800L;
        var deliveredOrder = new BistroBuilderPurchaseOrderRecord
        {
            purchaseOrderId = "purchase_order_3d_runtime",
            deliveryReceiptId = receipt.ReceiptId,
            status = BistroBuilderPurchaseOrderStatus.Delivered,
            subtotalCents = diagnosticSubtotalCents,
            shippingCostCents = diagnosticShippingCents,
            totalCents = diagnosticSubtotalCents + diagnosticShippingCents,
            confirmedLines = new List<BistroBuilderPurchaseOrderConfirmedLineSnapshot>
            {
                new BistroBuilderPurchaseOrderConfirmedLineSnapshot
                {
                    ingredientId = ingredientId,
                    totalNetQuantityMicrounits = receivedMilliUnits * 1000L,
                    lineSubtotalCents = diagnosticSubtotalCents
                }
            }
        };

        if (!productCost.TryApplySupplierReceipt(receipt, deliveredOrder, out error))
        {
            error = "3D no aceptó la valoración real del lote diagnóstico. " + error;
            return false;
        }

        diagnosticLotId = receipt.CreatedLots[0].LotId;
        if (!productCost.TryGetLotCostBasis(
                diagnosticLotId,
                out BistroBuilderLotCostBasisRecord basis) ||
            basis == null ||
            basis.basisKind != BistroBuilderLotCostBasisKind.SupplierActual ||
            basis.basisQuantityCanonicalMilliUnits != receivedMilliUnits ||
            basis.totalCostMicroCents !=
                diagnosticSubtotalCents *
                BistroBuilderIngredientDefinition.MicroCentsPerCent)
        {
            error = "El lote recibido no conserva el coste real de producto esperado.";
            return false;
        }

        return true;
    }

    private static void HandleLineCostRecorded(
        BistroBuilderConsumedLineCostRecord record
    )
    {
        if (record == null)
        {
            Fail("3D publicó un registro de coste nulo.");
            return;
        }

        bool identityOk = productCost.TryGetLineCost(
            record.lineId,
            out BistroBuilderConsumedLineCostRecord stored) &&
            stored != null && stored.costRecordId == record.costRecordId;
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
        bool countOk = productCost.ConsumedLineCostCount == baselineLineCosts + 1;
        bool cashUntouched =
            finance.CurrentBalanceCents == baselineBalance &&
            finance.TransactionCount == baselineFinanceTransactions;
        bool supplierBasisStillActual =
            productCost.TryGetLotCostBasis(
                diagnosticLotId,
                out BistroBuilderLotCostBasisRecord supplierBasis) &&
            supplierBasis.basisKind == BistroBuilderLotCostBasisKind.SupplierActual &&
            supplierBasis.totalCostMicroCents ==
                diagnosticSubtotalCents *
                BistroBuilderIngredientDefinition.MicroCentsPerCent;

        if (!identityOk || !mathOk || !countOk || !cashUntouched ||
            !supplierBasisStillActual || capturedErrors != 0)
        {
            Fail(
                "Se registró consumo real, pero la auditoría 3D no fue limpia." +
                "\nIdentidad: " + identityOk +
                "\nCálculos: " + mathOk +
                "\nRegistro único: " + countOk +
                "\nCaja intacta: " + cashUntouched +
                "\nCoste proveedor: " + supplierBasisStillActual +
                "\nError/Exception/Assert: " + capturedErrors);
            return;
        }

        Complete(
            "PRUEBA RUNTIME 3D SUPERADA" +
            "\n\nPlato real: " + record.dishId +
            "\nPrecio de venta: " + FormatMoney(record.salePriceCents) +
            "\nCoste teórico: " + FormatMoney(record.theoreticalCostCents) +
            "\nCoste consumido: " + FormatMoney(record.actualCostCents) +
            "\nCalidad coste: " + record.costQuality +
            "\nMargen real: " + FormatMoney(record.actualMarginCents) +
            " (" + (record.actualMarginBasisPoints / 100m).ToString("N2") + "%)" +
            "\nLote proveedor real: " + diagnosticLotId +
            " = " + FormatMoney(diagnosticSubtotalCents) +
            " (portes excluidos: OK)" +
            "\nCaja/ledger financiero: sin cambios por COGS" +
            "\nError/Exception/Assert: 0");
    }

    private static void CheckConsumptionTimeout()
    {
        if (EditorApplication.timeSinceStartup >= consumptionDeadline)
        {
            Fail("No comenzó la preparación de ningún plato real dentro del tiempo de prueba.");
        }
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
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
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
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
        diagnosticLotId = string.Empty;
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("N2") + " €";
    }
}
