using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderFinance3CSelfTest
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3C - Autotest compras a proveedores", false, 3022)]
    public static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3C",
            "Autotest de compras: " + passed + " correctos, " + failed + " fallos.",
            "Aceptar");

        if (!ok)
        {
            Debug.LogError("3C — El autotest de compras a proveedores ha fallado.");
        }
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        int capturedErrors = 0;
        StringBuilder builder = new StringBuilder();

        Application.LogCallback logHandler = (condition, stackTrace, type) =>
        {
            if (type == LogType.Error ||
                type == LogType.Exception ||
                type == LogType.Assert)
            {
                capturedErrors++;
            }
        };

        Application.logMessageReceived += logHandler;
        try
        {
            RunPolicyTests(ref passed, ref failed, builder);
            RunFinanceIntegrationTests(ref passed, ref failed, builder);
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine("[ERROR] Excepción inesperada: " + exception.Message);
        }
        finally
        {
            Application.logMessageReceived -= logHandler;
        }

        Check(capturedErrors == 0,
            "Console sin Error/Exception/Assert durante el autotest.",
            ref passed, ref failed, builder);

        builder.Insert(0,
            "3C — AUTOTEST COMPRAS A PROVEEDORES\n" +
            "Correctos: " + passed + "  Fallos: " + failed +
            "  Error/Exception/Assert: " + capturedErrors + "\n\n");
        report = builder.ToString();
        return failed == 0;
    }

    private static void RunPolicyTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        Check(BistroBuilderSupplierPurchaseFinancePolicy.IsCommittedStatus(
                BistroBuilderPurchaseOrderStatus.Confirmed),
            "Confirmed compromete caja.", ref passed, ref failed, builder);
        Check(BistroBuilderSupplierPurchaseFinancePolicy.IsCommittedStatus(
                BistroBuilderPurchaseOrderStatus.PendingDelivery),
            "PendingDelivery mantiene la caja comprometida.", ref passed, ref failed, builder);
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.IsCommittedStatus(
                BistroBuilderPurchaseOrderStatus.Draft),
            "Draft no compromete caja.", ref passed, ref failed, builder);
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.IsCommittedStatus(
                BistroBuilderPurchaseOrderStatus.InDelivery),
            "InDelivery ya no es compromiso: pasa a salida real.", ref passed, ref failed, builder);
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.IsCommittedStatus(
                BistroBuilderPurchaseOrderStatus.Delivered),
            "Delivered no vuelve a comprometer caja.", ref passed, ref failed, builder);
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.IsCommittedStatus(
                BistroBuilderPurchaseOrderStatus.Cancelled),
            "Cancelled libera el compromiso.", ref passed, ref failed, builder);

        List<BistroBuilderPurchaseOrderRecord> orders =
            new List<BistroBuilderPurchaseOrderRecord>
            {
                CreateOrder("po_confirmed", BistroBuilderPurchaseOrderStatus.Confirmed, 9000L, 1000L, "EUR"),
                CreateOrder("po_pending", BistroBuilderPurchaseOrderStatus.PendingDelivery, 3500L, 500L, "EUR"),
                CreateOrder("po_delivered", BistroBuilderPurchaseOrderStatus.Delivered, 90000L, 0L, "EUR")
            };
        Check(BistroBuilderSupplierPurchaseFinancePolicy.TryCalculateCommittedCents(
                orders, "EUR", out long committed, out _) && committed == 14000L,
            "El compromiso suma solo Confirmed/PendingDelivery e incluye portes.",
            ref passed, ref failed, builder);

        orders[1].currencyCode = "USD";
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.TryCalculateCommittedCents(
                orders, "EUR", out _, out _),
            "Un compromiso en otra moneda se rechaza.",
            ref passed, ref failed, builder);

        BistroBuilderPurchaseOrderConfirmationPreview preview =
            CreatePreview(15000L, "EUR", true);
        Check(BistroBuilderSupplierPurchaseFinancePolicy.TryAuthorizeConfirmation(
                preview, 20000L, 5000L, "EUR", out _),
            "La confirmación acepta exactamente el saldo disponible.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.TryAuthorizeConfirmation(
                preview, 19999L, 5000L, "EUR", out _),
            "La confirmación bloquea un céntimo de sobrecompromiso.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.TryAuthorizeConfirmation(
                preview, 50000L, 0L, "USD", out _),
            "La confirmación rechaza moneda incompatible.",
            ref passed, ref failed, builder);
        preview.canConfirm = false;
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.TryAuthorizeConfirmation(
                preview, 50000L, 0L, "EUR", out _),
            "Una cotización comercial no confirmable sigue bloqueada.",
            ref passed, ref failed, builder);

        BistroBuilderPurchaseOrderRecord inDelivery =
            CreateOrder("PO_RUNTIME_001", BistroBuilderPurchaseOrderStatus.InDelivery,
                12500L, 1500L, "EUR");
        Check(BistroBuilderSupplierPurchaseFinancePolicy.TryBuildDebitRequest(
                inDelivery, 7, 840, "EUR",
                out BistroBuilderFinanceTransactionRequest request, out _),
            "InDelivery construye una salida financiera.",
            ref passed, ref failed, builder);
        Check(request != null && request.amountCents == 14000L,
            "La salida paga subtotal y portes exactamente una vez.",
            ref passed, ref failed, builder);
        Check(request != null &&
              request.operationId == "supplier_purchase_po_runtime_001" &&
              request.sourceSystemId == BistroBuilderSupplierPurchaseFinancePolicy.SourceSystemId &&
              request.categoryId == BistroBuilderSupplierPurchaseFinancePolicy.CategoryId &&
              request.kind == BistroBuilderFinanceTransactionKind.Debit,
            "La identidad y categoría financiera del proveedor son deterministas.",
            ref passed, ref failed, builder);
        Check(request != null && request.dayIndex == 7 && request.minuteOfDay == 840,
            "El pago conserva el momento de juego exacto.",
            ref passed, ref failed, builder);

        BistroBuilderPurchaseOrderRecord stillPending = inDelivery.DeepClone();
        stillPending.status = BistroBuilderPurchaseOrderStatus.PendingDelivery;
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.TryBuildDebitRequest(
                stillPending, 7, 840, "EUR", out _, out _),
            "PendingDelivery no paga antes de la expedición.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.TryBuildDebitRequest(
                inDelivery, 7, 840, "USD", out _, out _),
            "El pago rechaza moneda incompatible.",
            ref passed, ref failed, builder);

        List<BistroBuilderPurchaseOrderRecord> overflowOrders =
            new List<BistroBuilderPurchaseOrderRecord>
            {
                CreateOrder("po_max", BistroBuilderPurchaseOrderStatus.Confirmed, long.MaxValue, 0L, "EUR"),
                CreateOrder("po_plus", BistroBuilderPurchaseOrderStatus.PendingDelivery, 1L, 0L, "EUR")
            };
        Check(!BistroBuilderSupplierPurchaseFinancePolicy.TryCalculateCommittedCents(
                overflowOrders, "EUR", out _, out _),
            "La suma de compromisos protege contra overflow.",
            ref passed, ref failed, builder);
        Check(BistroBuilderSupplierPurchaseFinancePolicy.BuildDebitOperationId("PO_ABC") ==
              "supplier_purchase_po_abc",
            "OperationId de proveedor se normaliza de forma estable.",
            ref passed, ref failed, builder);
    }

    private static void RunFinanceIntegrationTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject testObject = new GameObject("BB_3C_SupplierFinanceSelfTest");
        try
        {
            BistroBuilderFinanceService finance =
                testObject.AddComponent<BistroBuilderFinanceService>();
            Check(finance.TryInitializeFresh(out _),
                "La autoridad financiera temporal inicializa.",
                ref passed, ref failed, builder);

            BistroBuilderPurchaseOrderRecord order =
                CreateOrder("po_finance_001", BistroBuilderPurchaseOrderStatus.InDelivery,
                    10000L, 2500L, "EUR");
            BistroBuilderSupplierPurchaseFinancePolicy.TryBuildDebitRequest(
                order, 3, 720, finance.CurrencyCode,
                out BistroBuilderFinanceTransactionRequest request, out _);

            Check(finance.TryPostTransaction(request, out var posted, out _) &&
                  posted != null && posted.amountCents == 12500L,
                "El despacho registra un débito real en finance.runtime.",
                ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == 4987500L,
                "El pago reduce la caja exactamente por el total del PurchaseOrder.",
                ref passed, ref failed, builder);
            Check(finance.TransactionCount == 1,
                "El pago añade un único movimiento al ledger.",
                ref passed, ref failed, builder);
            Check(finance.TryGetTransactionByOperationId(
                    request.operationId, out var queried) &&
                  queried != null && queried.sequence == posted.sequence,
                "FinanceService consulta el pago por OperationId sin recorrer el ledger.",
                ref passed, ref failed, builder);
            Check(finance.TryPostTransaction(request, out var retry, out _) &&
                  retry != null && retry.sequence == posted.sequence &&
                  finance.TransactionCount == 1,
                "Reintentar el mismo pago es idempotente.",
                ref passed, ref failed, builder);

            BistroBuilderFinanceTransactionRequest conflict =
                new BistroBuilderFinanceTransactionRequest
                {
                    operationId = request.operationId,
                    sourceSystemId = request.sourceSystemId,
                    sourceReferenceId = request.sourceReferenceId,
                    categoryId = request.categoryId,
                    kind = request.kind,
                    amountCents = request.amountCents + 1L,
                    dayIndex = request.dayIndex,
                    minuteOfDay = request.minuteOfDay,
                    description = request.description
                };
            Check(!finance.TryPostTransaction(conflict, out _, out _),
                "Un OperationId existente no admite otro importe.",
                ref passed, ref failed, builder);

            BistroBuilderFinanceSnapshot snapshot = finance.CreateSnapshot();
            Check(snapshot != null &&
                  BistroBuilderFinanceEngine.TryValidateSnapshot(snapshot, out _),
                "El pago de proveedor conserva finance.runtime válido.",
                ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static BistroBuilderPurchaseOrderRecord CreateOrder(
        string id,
        BistroBuilderPurchaseOrderStatus status,
        long subtotalCents,
        long shippingCents,
        string currency)
    {
        return new BistroBuilderPurchaseOrderRecord
        {
            purchaseOrderId = id,
            displayCode = id,
            supplierId = "supplier_test",
            status = status,
            currencyCode = currency,
            subtotalCents = subtotalCents,
            shippingCostCents = shippingCents,
            totalCents = checked(subtotalCents + shippingCents),
            inDeliveryGameDay = status == BistroBuilderPurchaseOrderStatus.InDelivery ? 3 : 0
        };
    }

    private static BistroBuilderPurchaseOrderConfirmationPreview CreatePreview(
        long totalCents,
        string currency,
        bool canConfirm)
    {
        return new BistroBuilderPurchaseOrderConfirmationPreview
        {
            purchaseOrderId = "po_preview",
            displayCode = "PO-PREVIEW",
            supplierId = "supplier_test",
            currencyCode = currency,
            subtotalCents = totalCents,
            totalCents = totalCents,
            canConfirm = canConfirm,
            minimumOrderSatisfied = true
        };
    }

    private static void Check(
        bool condition,
        string message,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + message);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + message);
        }
    }
}
