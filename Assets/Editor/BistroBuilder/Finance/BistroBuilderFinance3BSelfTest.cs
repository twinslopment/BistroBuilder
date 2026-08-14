using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderFinance3BSelfTest
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3B - Autotest ingresos por ventas", false, 3012)]
    public static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3B",
            "Autotest de ventas: " + passed + " correctos, " + failed + " fallos.",
            "Aceptar");

        if (!ok)
        {
            Debug.LogError("3B — El autotest de ingresos por ventas ha fallado.");
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
            "3B — AUTOTEST INGRESOS POR VENTAS\n" +
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
        Check(BistroBuilderSalesRevenuePolicy.IsPayableServiceMode(
                BistroBuilderServiceMode.TableService),
            "TableService es un cobro final contabilizable.",
            ref passed, ref failed, builder);
        Check(BistroBuilderSalesRevenuePolicy.IsPayableServiceMode(
                BistroBuilderServiceMode.BarService),
            "BarService es un cobro final contabilizable.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSalesRevenuePolicy.IsPayableServiceMode(
                BistroBuilderServiceMode.WaitingAtBar),
            "WaitingAtBar no genera ingreso antes de pagar la mesa.",
            ref passed, ref failed, builder);

        Check(BistroBuilderSalesRevenuePolicy.TryCalculateTablePaymentAmount(
                12000, 2500, out long combinedAmount, out _) &&
              combinedAmount == 14500L,
            "La cuenta de mesa consolida una sola vez el cargo transferido de barra.",
            ref passed, ref failed, builder);
        Check(BistroBuilderSalesRevenuePolicy.TryCalculateTablePaymentAmount(
                0, 2500, out long freeTableWithBar, out _) &&
              freeTableWithBar == 2500L,
            "Una mesa gratuita conserva el cargo real transferido desde barra.",
            ref passed, ref failed, builder);
        Check(BistroBuilderSalesRevenuePolicy.TryCalculateTablePaymentAmount(
                0, 0, out long complimentaryAmount, out _) &&
              complimentaryAmount == 0L,
            "Una comanda totalmente gratuita es un pago válido de 0 €.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSalesRevenuePolicy.TryCalculateTablePaymentAmount(
                -1, 0, out _, out _),
            "Un importe canónico negativo se rechaza.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSalesRevenuePolicy.TryCalculateTablePaymentAmount(
                12000, -1, out _, out _),
            "Un cargo transferido negativo se rechaza.",
            ref passed, ref failed, builder);

        CheckCategory(
            BistroBuilderServiceMode.TableService,
            BistroBuilderMealServiceAvailability.Breakfast,
            "sales.breakfast.table",
            ref passed, ref failed, builder);
        CheckCategory(
            BistroBuilderServiceMode.TableService,
            BistroBuilderMealServiceAvailability.Lunch,
            "sales.lunch.table",
            ref passed, ref failed, builder);
        CheckCategory(
            BistroBuilderServiceMode.TableService,
            BistroBuilderMealServiceAvailability.Dinner,
            "sales.dinner.table",
            ref passed, ref failed, builder);
        CheckCategory(
            BistroBuilderServiceMode.BarService,
            BistroBuilderMealServiceAvailability.Breakfast,
            "sales.breakfast.bar",
            ref passed, ref failed, builder);
        CheckCategory(
            BistroBuilderServiceMode.BarService,
            BistroBuilderMealServiceAvailability.Lunch,
            "sales.lunch.bar",
            ref passed, ref failed, builder);
        CheckCategory(
            BistroBuilderServiceMode.BarService,
            BistroBuilderMealServiceAvailability.Dinner,
            "sales.dinner.bar",
            ref passed, ref failed, builder);

        bool built = BistroBuilderSalesRevenuePolicy.TryBuildRequest(
            "order_policy_table",
            BistroBuilderServiceMode.TableService,
            BistroBuilderMealServiceAvailability.Lunch,
            14500L,
            7,
            825,
            out BistroBuilderFinanceTransactionRequest request,
            out _);
        Check(built &&
              request.operationId == "sale_table_order_policy_table" &&
              request.sourceSystemId == BistroBuilderSalesRevenuePolicy.SourceSystemId &&
              request.sourceReferenceId == "order_policy_table" &&
              request.kind == BistroBuilderFinanceTransactionKind.Credit,
            "El cobro genera identidad, origen y sentido financiero deterministas.",
            ref passed, ref failed, builder);
        Check(built && request.amountCents == 14500L &&
              request.dayIndex == 7 && request.minuteOfDay == 825,
            "El cobro conserva importe y tiempo de juego exactos.",
            ref passed, ref failed, builder);

        Check(!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                "order_waiting", BistroBuilderServiceMode.WaitingAtBar,
                BistroBuilderMealServiceAvailability.Lunch, 1000L, 1, 720,
                out _, out _),
            "WaitingAtBar no puede construir un movimiento de venta final.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                "order_all", BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.All, 1000L, 1, 720,
                out _, out _),
            "Un servicio temporal no concreto se rechaza.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                "order_zero", BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Lunch, 0L, 1, 720,
                out _, out _),
            "Un pago de 0 € no genera una transacción monetaria.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                "order_time", BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Lunch, 1000L, 0, 1440,
                out _, out _),
            "Una fecha de juego inválida se rechaza.",
            ref passed, ref failed, builder);
        Check(!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                "?", BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Lunch, 1000L, 1, 720,
                out _, out _),
            "Un CanonicalOrderId inválido se rechaza.",
            ref passed, ref failed, builder);
    }

    private static void RunFinanceIntegrationTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject testObject = new GameObject("BB_3B_SalesRevenueSelfTest");
        try
        {
            BistroBuilderFinanceService finance =
                testObject.AddComponent<BistroBuilderFinanceService>();

            Check(finance.TryInitializeFresh(out _),
                "La autoridad financiera temporal inicializa.",
                ref passed, ref failed, builder);

            BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                "order_finance_table",
                BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Lunch,
                14500L,
                7,
                825,
                out BistroBuilderFinanceTransactionRequest tableRequest,
                out _);

            Check(finance.TryPostTransaction(tableRequest, out var tableRecord, out _) &&
                  tableRecord != null && tableRecord.amountCents == 14500L,
                "La cuenta consolidada entra una sola vez en finance.runtime.",
                ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == 5014500L,
                "El ingreso de mesa aumenta exactamente la caja.",
                ref passed, ref failed, builder);

            Check(finance.TryPostTransaction(tableRequest, out var tableRetry, out _) &&
                  tableRetry != null && tableRetry.sequence == tableRecord.sequence &&
                  finance.TransactionCount == 1,
                "Repetir el mismo cobro de mesa es idempotente.",
                ref passed, ref failed, builder);

            BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                "order_finance_bar",
                BistroBuilderServiceMode.BarService,
                BistroBuilderMealServiceAvailability.Dinner,
                3200L,
                7,
                1260,
                out BistroBuilderFinanceTransactionRequest barRequest,
                out _);

            Check(finance.TryPostTransaction(barRequest, out _, out _),
                "Un cobro directo de barra entra en finance.runtime.",
                ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == 5017700L &&
                  finance.TransactionCount == 2,
                "Mesa y barra producen dos cobros reales y ningún duplicado.",
                ref passed, ref failed, builder);

            BistroBuilderFinanceTransactionRequest conflict =
                new BistroBuilderFinanceTransactionRequest
                {
                    operationId = barRequest.operationId,
                    sourceSystemId = barRequest.sourceSystemId,
                    sourceReferenceId = barRequest.sourceReferenceId,
                    categoryId = barRequest.categoryId,
                    kind = barRequest.kind,
                    amountCents = 3300L,
                    dayIndex = barRequest.dayIndex,
                    minuteOfDay = barRequest.minuteOfDay,
                    description = barRequest.description
                };
            Check(!finance.TryPostTransaction(conflict, out _, out _),
                "El mismo cobro no puede reaparecer con otro importe.",
                ref passed, ref failed, builder);

            BistroBuilderFinanceSnapshot snapshot = finance.CreateSnapshot();
            Check(snapshot != null && snapshot.transactions.Count == 2 &&
                  snapshot.transactions[0].categoryId == "sales.lunch.table" &&
                  snapshot.transactions[1].categoryId == "sales.dinner.bar" &&
                  snapshot.transactions[0].dayIndex == 7 &&
                  snapshot.transactions[1].minuteOfDay == 1260,
                "El ledger conserva canal, servicio y momento del cobro.",
                ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static void CheckCategory(
        BistroBuilderServiceMode mode,
        BistroBuilderMealServiceAvailability mealService,
        string expectedCategory,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        bool built = BistroBuilderSalesRevenuePolicy.TryBuildRequest(
            "order_category_test",
            mode,
            mealService,
            1000L,
            2,
            600,
            out BistroBuilderFinanceTransactionRequest request,
            out _);

        Check(built && request.categoryId == expectedCategory,
            "Categoría estable: " + expectedCategory + ".",
            ref passed, ref failed, builder);
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
