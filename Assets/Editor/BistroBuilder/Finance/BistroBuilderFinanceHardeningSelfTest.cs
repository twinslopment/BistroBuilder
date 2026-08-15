using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batería transversal de regresión 3A-3I. Ejecuta los autotests históricos
/// completos y añade invariantes introducidas por el endurecimiento previo a 3J.
/// </summary>
public static class BistroBuilderFinanceHardeningSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3 - Autotest global endurecido",
        false,
        3092)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — Finanzas",
            "Autotest global: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest financiero global endurecido ha fallado.");
        }
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER — AUTOTEST FINANCIERO GLOBAL 3A-3I ===");

        RunLegacySuite("3A", BistroBuilderFinance3ASelfTest.Run,
            ref passed, ref failed, builder);
        RunLegacySuite("3B", BistroBuilderFinance3BSelfTest.Run,
            ref passed, ref failed, builder);
        RunLegacySuite("3C", BistroBuilderFinance3CSelfTest.Run,
            ref passed, ref failed, builder);
        RunLegacySuite("3D", BistroBuilderFinance3DSelfTest.Run,
            ref passed, ref failed, builder);
        RunLegacySuite("3E", BistroBuilderFinance3ESelfTest.Run,
            ref passed, ref failed, builder);
        RunLegacySuite("3F", BistroBuilderFinance3FSelfTest.Run,
            ref passed, ref failed, builder);
        RunLegacySuite("3G", BistroBuilderFinance3GSelfTest.Run,
            ref passed, ref failed, builder);
        RunLegacySuite("3H", BistroBuilderFinance3HSelfTest.Run,
            ref passed, ref failed, builder);
        RunLegacySuite("3I", BistroBuilderFinance3ISelfTest.Run,
            ref passed, ref failed, builder);

        builder.AppendLine();
        builder.AppendLine("--- Invariantes de endurecimiento ---");

        RunFinanceBatchTests(ref passed, ref failed, builder);
        RunProjectionTests(ref passed, ref failed, builder);
        RunDebtStateTests(ref passed, ref failed, builder);
        RunInventoryLossCostTests(ref passed, ref failed, builder);
        RunOperatingProjectionTest(ref passed, ref failed, builder);

        builder.AppendLine();
        builder.AppendLine(
            "TOTAL: " + passed + " OK / " + failed + " fallos");
        report = builder.ToString();
        return failed == 0;
    }

    private delegate bool SuiteRunner(
        out int passed,
        out int failed,
        out string report);

    private static void RunLegacySuite(
        string name,
        SuiteRunner runner,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        try
        {
            bool ok = runner(
                out int suitePassed,
                out int suiteFailed,
                out string suiteReport);
            passed += suitePassed;
            failed += suiteFailed;
            builder.AppendLine(
                name + ": " + suitePassed + " OK / " +
                suiteFailed + " fallos" + (ok ? string.Empty : " [FALLO]"));
            if (!ok)
            {
                builder.AppendLine(suiteReport);
            }
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine(
                "[ERROR] " + name + " lanzó excepción: " + exception.Message);
        }
    }

    private static void RunFinanceBatchTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject root = new GameObject("FinanceHardeningBatchTest");
        try
        {
            BistroBuilderFinanceService finance =
                root.AddComponent<BistroBuilderFinanceService>();
            finance.TryInitializeFresh(out _);
            long initialBalance = finance.CurrentBalanceCents;
            int initialCount = finance.TransactionCount;

            var invalidBatch = new List<BistroBuilderFinanceTransactionRequest>
            {
                Request("hard_batch_valid", "diagnostic.finance", "hard_ref_001",
                    "diagnostic.cash", BistroBuilderFinanceTransactionKind.Debit,
                    100L, 1),
                Request("hard_batch_invalid", "diagnostic.finance", "hard_ref_002",
                    "diagnostic.cash", BistroBuilderFinanceTransactionKind.Debit,
                    0L, 1)
            };

            bool rejected = !finance.TryPostTransactions(
                invalidBatch,
                out _,
                out _);
            Check(rejected &&
                  finance.TransactionCount == initialCount &&
                  finance.CurrentBalanceCents == initialBalance,
                "Batch 3A inválido es all-or-nothing",
                ref passed, ref failed, builder);

            var validBatch = new List<BistroBuilderFinanceTransactionRequest>
            {
                Request("hard_batch_debit", "diagnostic.finance", "hard_ref_003",
                    "diagnostic.cash", BistroBuilderFinanceTransactionKind.Debit,
                    250L, 1),
                Request("hard_batch_credit", "diagnostic.finance", "hard_ref_004",
                    "diagnostic.cash", BistroBuilderFinanceTransactionKind.Credit,
                    50L, 1)
            };
            bool posted = finance.TryPostTransactions(validBatch, out _, out _);
            Check(posted &&
                  finance.TransactionCount == initialCount + 2 &&
                  finance.CurrentBalanceCents == initialBalance - 200L,
                "Batch 3A válido publica todas sus patas",
                ref passed, ref failed, builder);

            bool replayed = finance.TryPostTransactions(validBatch, out _, out _);
            Check(replayed &&
                  finance.TransactionCount == initialCount + 2 &&
                  finance.CurrentBalanceCents == initialBalance - 200L,
                "Batch 3A completo es idempotente",
                ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void RunProjectionTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject root = new GameObject("FinanceHardeningProjectionTest");
        try
        {
            BistroBuilderFinanceService finance =
                root.AddComponent<BistroBuilderFinanceService>();
            finance.TryInitializeFresh(out _);

            var requests = new List<BistroBuilderFinanceTransactionRequest>
            {
                Request("hard_sale_day10", BistroBuilderSalesRevenuePolicy.SourceSystemId,
                    "order_hard_001", "sales.lunch.table",
                    BistroBuilderFinanceTransactionKind.Credit, 10000L, 10),
                Request("hard_loan_day10", BistroBuilderFinancingEngine.SourceSystemId,
                    "loan_hard_001", BistroBuilderFinancingEngine.LoanProceedsCategoryId,
                    BistroBuilderFinanceTransactionKind.Credit, 50000L, 10),
                Request("hard_interest_day10", BistroBuilderFinancingEngine.SourceSystemId,
                    "loan_hard_001", BistroBuilderFinancingEngine.InterestExpenseCategoryId,
                    BistroBuilderFinanceTransactionKind.Debit, 500L, 10),
                Request("hard_principal_day10", BistroBuilderFinancingEngine.SourceSystemId,
                    "loan_hard_001", BistroBuilderFinancingEngine.PrincipalRepaymentCategoryId,
                    BistroBuilderFinanceTransactionKind.Debit, 1000L, 10),
                Request("hard_invest_day10", "placeable_economy", "placeable_hard_001",
                    "investment.equipment", BistroBuilderFinanceTransactionKind.Debit,
                    2000L, 10),
                Request("hard_loan_day11", BistroBuilderFinancingEngine.SourceSystemId,
                    "loan_hard_002", BistroBuilderFinancingEngine.LoanProceedsCategoryId,
                    BistroBuilderFinanceTransactionKind.Credit, 1000L, 11)
            };
            finance.TryPostTransactions(requests, out _, out _);

            BistroBuilderFinanceSnapshot financeSnapshot = finance.CreateSnapshot();
            var costSnapshot = new BistroBuilderProductCostSnapshot
            {
                nextInventoryLossCostSequence = 2L
            };
            costSnapshot.inventoryLossCosts.Add(
                new BistroBuilderInventoryLossCostRecord
                {
                    sequence = 1L,
                    lossCostRecordId =
                        BistroBuilderProductCostEngine.BuildInventoryLossCostRecordId(1L),
                    inventoryTransactionId = "inv_tx_hard_001",
                    inventoryOperationId = "expire_hard_001",
                    ingredientId = "ingredient_hard",
                    transactionType =
                        BistroBuilderInventoryTransactionType.Expiration,
                    dayIndex = 10,
                    minuteOfDay = 600,
                    quantityCanonicalMilliUnits = 1000L,
                    costMicroCents =
                        700L * BistroBuilderIngredientDefinition.MicroCentsPerCent,
                    costCents = 700L,
                    costQuality = BistroBuilderProductCostQuality.Estimated
                });

            Check(BistroBuilderProductCostEngine.TryValidateSnapshot(
                    costSnapshot, out _),
                "3D valida bajas económicas persistentes",
                ref passed, ref failed, builder);

            var range = new List<BistroBuilderDayFinancialResult>();
            bool rangeOk = BistroBuilderFinancialResultsEngine.TryBuildDayResultsRange(
                financeSnapshot,
                costSnapshot,
                null,
                10,
                11,
                range,
                out _);
            Check(rangeOk && range.Count == 2,
                "3G proyecta rango completo en una sola llamada",
                ref passed, ref failed, builder);

            BistroBuilderDayFinancialResult day10 = rangeOk ? range[0] : null;
            BistroBuilderDayFinancialResult day11 = rangeOk ? range[1] : null;
            Check(day10 != null &&
                  day10.revenueCents == 10000L &&
                  day10.loanProceedsCashInCents == 50000L &&
                  day10.debtPrincipalCashOutCents == 1000L &&
                  day10.financingInterestExpensesCents == 500L &&
                  day10.inventoryWriteOffExpensesCents == 700L &&
                  day10.investmentCashOutCents == 2000L,
                "3G separa ventas, deuda, interés, write-off e inversión",
                ref passed, ref failed, builder);
            Check(day10 != null &&
                  day10.totalPeriodExpensesCents == 1200L &&
                  day10.operatingResultCents == 8800L,
                "Principal/inversión no contaminan resultado operativo",
                ref passed, ref failed, builder);
            Check(day10 != null &&
                  day10.totalCashOutCents == 3500L &&
                  day10.netCashChangeCents == 56500L,
                "Write-off de inventario reduce resultado pero no vuelve a sacar caja",
                ref passed, ref failed, builder);
            Check(day11 != null &&
                  !day11.HasServiceActivity &&
                  !day11.HasOperatingResultActivity &&
                  day11.HasFinancialActivity,
                "Día de préstamo puro no es día operativo",
                ref passed, ref failed, builder);

            bool singleOk = BistroBuilderFinancialResultsEngine.TryBuildDayResult(
                financeSnapshot,
                costSnapshot,
                null,
                10,
                out BistroBuilderDayFinancialResult single,
                out _);
            Check(singleOk && day10 != null &&
                  single.revenueCents == day10.revenueCents &&
                  single.operatingResultCents == day10.operatingResultCents &&
                  single.netCashChangeCents == day10.netCashChangeCents,
                "Resultado unitario y proyección por rango convergen",
                ref passed, ref failed, builder);

            bool historyOk = BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
                range,
                10,
                11,
                out BistroBuilderFinancialPeriodReport history,
                out _);
            Check(historyOk &&
                  history.activeDayCount == 1 &&
                  history.resultDayCount == 1 &&
                  history.financialActivityDayCount == 2,
                "3H separa actividad de servicio, resultado y tesorería",
                ref passed, ref failed, builder);
            Check(historyOk &&
                  history.loanProceedsCashInCents == 51000L &&
                  history.debtPrincipalCashOutCents == 1000L &&
                  history.financingInterestExpensesCents == 500L &&
                  history.inventoryWriteOffExpensesCents == 700L,
                "3H conserva desglose explícito de financiación y bajas",
                ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void RunDebtStateTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        List<BistroBuilderFinancingOfferDefinition> offers =
            BistroBuilderFinancingEngine.CreateDefaultOffers();
        var snapshot = new BistroBuilderFinancingSnapshot();
        bool created = BistroBuilderFinancingEngine.TryCreateLoan(
            snapshot,
            offers[0],
            "hard_accept_default",
            1,
            out _,
            out _);
        BistroBuilderFinancingEngine.RefreshLoanStatuses(snapshot, 15, 7);
        BistroBuilderLoanRecord loan = snapshot.loans[0];
        Check(created &&
              loan.status == BistroBuilderLoanStatus.Defaulted &&
              loan.hasEverDefaulted &&
              loan.firstDefaultDayIndex == 15,
            "Default queda registrado históricamente",
            ref passed, ref failed, builder);

        BistroBuilderFinancingEngine.RefreshLoanStatuses(snapshot, 2, 7);
        Check(loan.status == BistroBuilderLoanStatus.Defaulted &&
              loan.hasEverDefaulted,
            "Default no desaparece por retroceder el corte temporal",
            ref passed, ref failed, builder);

        for (int index = 0; index < loan.installments.Count; index++)
        {
            loan.installments[index].status =
                BistroBuilderLoanInstallmentStatus.Paid;
            loan.installments[index].paidDayIndex = 31;
        }
        BistroBuilderFinancingEngine.RefreshLoanStatuses(snapshot, 31, 7);
        Check(loan.status == BistroBuilderLoanStatus.PaidOff &&
              loan.hasEverDefaulted && loan.paidOffDayIndex == 31,
            "Liquidar deuda conserva memoria del default",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.TryValidateSnapshot(snapshot, out _),
            "Snapshot con memoria de default sigue siendo válido",
            ref passed, ref failed, builder);

        Check(BistroBuilderFinancingEngine.ResolveRisk(
                BistroBuilderLiquidityStatus.Unknown,
                0,
                0L,
                0L,
                false) == BistroBuilderFinancialRiskLevel.High,
            "Liquidez desconocida nunca se interpreta como riesgo bajo",
            ref passed, ref failed, builder);
    }

    private static void RunInventoryLossCostTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        var snapshot = new BistroBuilderProductCostSnapshot
        {
            nextInventoryLossCostSequence = 2L
        };
        snapshot.inventoryLossCosts.Add(
            new BistroBuilderInventoryLossCostRecord
            {
                sequence = 1L,
                lossCostRecordId =
                    BistroBuilderProductCostEngine.BuildInventoryLossCostRecordId(1L),
                inventoryTransactionId = "inv_tx_loss_hard_001",
                inventoryOperationId = "expire_loss_hard_001",
                ingredientId = "ingredient_loss_hard",
                transactionType = BistroBuilderInventoryTransactionType.Expiration,
                dayIndex = 9,
                minuteOfDay = 500,
                quantityCanonicalMilliUnits = 1000L,
                costMicroCents =
                    123L * BistroBuilderIngredientDefinition.MicroCentsPerCent,
                costCents = 123L,
                costQuality = BistroBuilderProductCostQuality.Estimated
            });

        Check(BistroBuilderProductCostEngine.TryValidateSnapshot(snapshot, out _),
            "Baja no monetaria forma parte íntegra de Product Cost",
            ref passed, ref failed, builder);
        Check(snapshot.inventoryLossCosts[0].costCents == 123L &&
              snapshot.inventoryLossCosts[0].costQuality ==
                  BistroBuilderProductCostQuality.Estimated,
            "Write-off conserva coste congelado y calidad Estimated",
            ref passed, ref failed, builder);

        var oldV1 = new BistroBuilderProductCostSnapshot
        {
            inventoryLossCosts = null,
            nextInventoryLossCostSequence = 0L
        };
        BistroBuilderProductCostService.NormalizeCompatibleSnapshot(oldV1);
        Check(oldV1.inventoryLossCosts != null &&
              oldV1.nextInventoryLossCostSequence == 1L &&
              BistroBuilderProductCostEngine.TryValidateSnapshot(oldV1, out _),
            "finance.product_cost.runtime v1 anterior normaliza campos aditivos",
            ref passed, ref failed, builder);
    }

    private static void RunOperatingProjectionTest(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        BistroBuilderOperatingExpenseService operating =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderOperatingExpenseService>();
        BistroBuilderGeneralGameStateService general =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        BistroBuilderFinanceService finance =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();

        if (operating == null || general == null || finance == null)
        {
            Check(false,
                "Escena contiene autoridades para proyectar obligaciones 3E",
                ref passed, ref failed, builder);
            return;
        }

        int countBefore = finance.TransactionCount;
        long balanceBefore = finance.CurrentBalanceCents;
        int endDay = general.DayIndex > int.MaxValue - 7
            ? int.MaxValue
            : general.DayIndex + 7;
        bool ok = operating.TryCalculateRecurringObligationsCents(
            general.DayIndex,
            endDay,
            out long projected,
            out _);
        Check(ok && projected >= 0L,
            "3E proyecta obligaciones recurrentes conocidas",
            ref passed, ref failed, builder);
        Check(finance.TransactionCount == countBefore &&
              finance.CurrentBalanceCents == balanceBefore,
            "Proyectar obligaciones 3E no mueve caja",
            ref passed, ref failed, builder);
    }

    private static BistroBuilderFinanceTransactionRequest Request(
        string operationId,
        string sourceSystemId,
        string sourceReferenceId,
        string categoryId,
        BistroBuilderFinanceTransactionKind kind,
        long amountCents,
        int dayIndex)
    {
        return new BistroBuilderFinanceTransactionRequest
        {
            operationId = operationId,
            sourceSystemId = sourceSystemId,
            sourceReferenceId = sourceReferenceId,
            categoryId = categoryId,
            kind = kind,
            amountCents = amountCents,
            dayIndex = dayIndex,
            minuteOfDay = 600,
            description = "Finance hardening self-test"
        };
    }

    private static void Check(
        bool condition,
        string label,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + label);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + label);
        }
    }
}
