using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;

public static class BistroBuilderFinance3ISelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3I - Autotest",
        false,
        3082)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        UnityEngine.Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3I",
            "Autotest: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();
        builder.AppendLine("Bistro Builder — Autotest 3I");

        List<BistroBuilderFinancingOfferDefinition> offers =
            BistroBuilderFinancingEngine.CreateDefaultOffers();
        Check(offers.Count == 3, "Tres ofertas base", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.TryValidateOffers(offers, out _),
            "Ofertas válidas", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.CalculateInterestCents(500000L, 500) == 25000L,
            "Interés puente 250,00 €", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.CalculateInterestCents(1500000L, 800) == 120000L,
            "Interés growth 1.200,00 €", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.CalculateInterestCents(3000000L, 1200) == 360000L,
            "Interés expansion 3.600,00 €", ref passed, ref failed, builder);

        var snapshot = new BistroBuilderFinancingSnapshot();
        Check(BistroBuilderFinancingEngine.TryValidateSnapshot(snapshot, out _),
            "Snapshot vacío válido", ref passed, ref failed, builder);

        bool createdOk = BistroBuilderFinancingEngine.TryCreateLoan(
            snapshot,
            offers[0],
            "accept_test_bridge",
            1,
            out BistroBuilderLoanRecord loan,
            out _);
        Check(createdOk && loan != null, "Creación préstamo puente", ref passed, ref failed, builder);
        Check(loan != null && loan.loanId == "loan_00000001",
            "LoanId secuencial", ref passed, ref failed, builder);
        Check(loan != null && loan.installments.Count == 5,
            "Cinco cuotas", ref passed, ref failed, builder);
        Check(loan != null && loan.totalInterestCents == 25000L,
            "Interés total congelado", ref passed, ref failed, builder);
        Check(loan != null && loan.totalPayableCents == 525000L,
            "Total pagable congelado", ref passed, ref failed, builder);
        Check(loan != null &&
              loan.installments[0].dueDayIndex == 7 &&
              loan.installments[4].dueDayIndex == 31,
            "Calendario de cuotas determinista", ref passed, ref failed, builder);

        long principalSum = 0L;
        long interestSum = 0L;
        if (loan != null)
        {
            for (int index = 0; index < loan.installments.Count; index++)
            {
                principalSum += loan.installments[index].principalCents;
                interestSum += loan.installments[index].interestCents;
            }
        }
        Check(principalSum == 500000L,
            "Cuotas suman principal exacto", ref passed, ref failed, builder);
        Check(interestSum == 25000L,
            "Cuotas suman interés exacto", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.TryValidateSnapshot(snapshot, out _),
            "Snapshot con préstamo válido", ref passed, ref failed, builder);

        int countBeforeRetry = snapshot.loans.Count;
        bool retryOk = BistroBuilderFinancingEngine.TryCreateLoan(
            snapshot,
            offers[0],
            "accept_test_bridge",
            1,
            out BistroBuilderLoanRecord retryLoan,
            out _);
        Check(retryOk && snapshot.loans.Count == countBeforeRetry &&
              retryLoan.loanId == loan.loanId,
            "Aceptación idempotente", ref passed, ref failed, builder);
        Check(!BistroBuilderFinancingEngine.TryCreateLoan(
                snapshot,
                offers[1],
                "accept_test_bridge",
                1,
                out _,
                out _),
            "OperationId no reutilizable entre ofertas", ref passed, ref failed, builder);

        BistroBuilderFinanceTransactionRequest disbursement =
            BistroBuilderFinancingEngine.BuildDisbursementRequest(loan, 600);
        Check(disbursement.kind == BistroBuilderFinanceTransactionKind.Credit &&
              disbursement.amountCents == 500000L,
            "Desembolso es crédito de principal", ref passed, ref failed, builder);
        Check(disbursement.categoryId == BistroBuilderFinancingEngine.LoanProceedsCategoryId &&
              disbursement.sourceSystemId == BistroBuilderFinancingEngine.SourceSystemId,
            "Desembolso categorizado como financiación", ref passed, ref failed, builder);

        var requests = new List<BistroBuilderFinanceTransactionRequest>();
        BistroBuilderFinancingEngine.BuildInstallmentRequests(
            loan,
            loan.installments[0],
            7,
            600,
            requests);
        Check(requests.Count == 2,
            "Cuota separa principal e interés", ref passed, ref failed, builder);
        Check(requests[0].categoryId == BistroBuilderFinancingEngine.PrincipalRepaymentCategoryId &&
              requests[0].kind == BistroBuilderFinanceTransactionKind.Debit,
            "Principal es salida de caja no gasto", ref passed, ref failed, builder);
        Check(requests[1].categoryId == BistroBuilderFinancingEngine.InterestExpenseCategoryId &&
              requests[1].kind == BistroBuilderFinanceTransactionKind.Debit,
            "Interés es gasto financiero", ref passed, ref failed, builder);

        BistroBuilderFinancingEngine.CalculateDebtTotals(
            snapshot,
            1,
            7,
            out long dueToday,
            out long dueHorizon,
            out long overdue,
            out long outstandingPrincipal,
            out long outstandingInterest);
        Check(outstandingPrincipal == 500000L && outstandingInterest == 25000L,
            "Deuda pendiente total", ref passed, ref failed, builder);
        Check(dueToday == 0L && dueHorizon == 105000L,
            "Vencimiento dentro de 7 días", ref passed, ref failed, builder);
        Check(overdue == 0L,
            "Sin deuda vencida inicialmente", ref passed, ref failed, builder);

        Check(Resolve(100000L, 100000L, 0L, 0L) == BistroBuilderLiquidityStatus.Healthy,
            "Liquidez Healthy sin vencimientos", ref passed, ref failed, builder);
        Check(Resolve(300000L, 200000L, 100000L, 0L) == BistroBuilderLiquidityStatus.Healthy,
            "Liquidez Healthy con cobertura 2x", ref passed, ref failed, builder);
        Check(Resolve(200000L, 150000L, 100000L, 0L) == BistroBuilderLiquidityStatus.Watch,
            "Liquidez Watch", ref passed, ref failed, builder);
        Check(Resolve(100000L, 75000L, 100000L, 0L) == BistroBuilderLiquidityStatus.Tight,
            "Liquidez Tight", ref passed, ref failed, builder);
        Check(Resolve(100000L, 25000L, 100000L, 0L) == BistroBuilderLiquidityStatus.Critical,
            "Liquidez Critical por baja cobertura", ref passed, ref failed, builder);
        Check(Resolve(100000L, 100000L, 0L, 1L) == BistroBuilderLiquidityStatus.Critical,
            "Liquidez Critical por deuda vencida", ref passed, ref failed, builder);
        Check(Resolve(-1L, -1L, 0L, 0L) == BistroBuilderLiquidityStatus.Insolvent,
            "Liquidez Insolvent con caja negativa", ref passed, ref failed, builder);

        Check(BistroBuilderFinancingEngine.ResolveRisk(
                BistroBuilderLiquidityStatus.Healthy, 0, 1000L, 0L, false) ==
              BistroBuilderFinancialRiskLevel.Low,
            "Riesgo Low", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.ResolveRisk(
                BistroBuilderLiquidityStatus.Watch, 0, 1000L, 0L, false) ==
              BistroBuilderFinancialRiskLevel.Moderate,
            "Riesgo Moderate por liquidez", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.ResolveRisk(
                BistroBuilderLiquidityStatus.Healthy, 1, -100L, 0L, false) ==
              BistroBuilderFinancialRiskLevel.Moderate,
            "Riesgo Moderate por pérdida", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.ResolveRisk(
                BistroBuilderLiquidityStatus.Critical, 0, 100L, 0L, false) ==
              BistroBuilderFinancialRiskLevel.High,
            "Riesgo High por liquidez crítica", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.ResolveRisk(
                BistroBuilderLiquidityStatus.Healthy, 3, -100L, 1000L, false) ==
              BistroBuilderFinancialRiskLevel.High,
            "Riesgo High por tres pérdidas", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.ResolveRisk(
                BistroBuilderLiquidityStatus.Healthy, 5, -100L, 1000L, false) ==
              BistroBuilderFinancialRiskLevel.Severe,
            "Riesgo Severe por cinco pérdidas", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.ResolveRisk(
                BistroBuilderLiquidityStatus.Healthy, 0, 100L, 1000L, true) ==
              BistroBuilderFinancialRiskLevel.Severe,
            "Riesgo Severe por default", ref passed, ref failed, builder);

        BistroBuilderFinancingSnapshot delinquent = snapshot.DeepClone();
        BistroBuilderFinancingEngine.RefreshLoanStatuses(delinquent, 8, 7);
        Check(delinquent.loans[0].status == BistroBuilderLoanStatus.Delinquent &&
              delinquent.loans[0].installments[0].status == BistroBuilderLoanInstallmentStatus.Overdue,
            "Cuota vencida pasa a Delinquent", ref passed, ref failed, builder);

        BistroBuilderFinancingSnapshot defaulted = snapshot.DeepClone();
        BistroBuilderFinancingEngine.RefreshLoanStatuses(defaulted, 15, 7);
        Check(defaulted.loans[0].status == BistroBuilderLoanStatus.Defaulted,
            "Impago supera gracia y pasa a Defaulted", ref passed, ref failed, builder);

        BistroBuilderFinancingSnapshot paid = snapshot.DeepClone();
        for (int index = 0; index < paid.loans[0].installments.Count; index++)
        {
            paid.loans[0].installments[index].status = BistroBuilderLoanInstallmentStatus.Paid;
            paid.loans[0].installments[index].paidDayIndex = 31;
        }
        BistroBuilderFinancingEngine.RefreshLoanStatuses(paid, 31, 7);
        Check(paid.loans[0].status == BistroBuilderLoanStatus.PaidOff,
            "Todas las cuotas pagadas cierran préstamo", ref passed, ref failed, builder);
        Check(paid.loans[0].paidOffDayIndex == 31,
            "PaidOff conserva día de cierre", ref passed, ref failed, builder);

        BistroBuilderFinancingSnapshot clone = snapshot.DeepClone();
        clone.loans[0].offerId = "mutated";
        Check(snapshot.loans[0].offerId == "bridge",
            "DeepClone no comparte préstamos", ref passed, ref failed, builder);

        BistroBuilderFinancingSnapshot badSequence = snapshot.DeepClone();
        badSequence.nextLoanSequence = 99L;
        Check(!BistroBuilderFinancingEngine.TryValidateSnapshot(badSequence, out _),
            "Rechaza secuencia rota", ref passed, ref failed, builder);

        BistroBuilderFinancingSnapshot badSchedule = snapshot.DeepClone();
        badSchedule.loans[0].installments[1].dueDayIndex =
            badSchedule.loans[0].installments[0].dueDayIndex;
        Check(!BistroBuilderFinancingEngine.TryValidateSnapshot(badSchedule, out _),
            "Rechaza calendario no creciente", ref passed, ref failed, builder);

        Check(BistroBuilderFinancingEngine.CalculateInterestCents(10000L, 0) == 0L,
            "Interés cero soportado", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.BuildLoanId(42L) == "loan_00000042",
            "Formato LoanId estable", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.BuildDisbursementOperationId("loan_00000001") ==
              "financing_disbursement_loan_00000001",
            "OperationId desembolso estable", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.BuildPrincipalOperationId("loan_00000001", 2) ==
              "financing_repayment_loan_00000001_i002_principal",
            "OperationId principal estable", ref passed, ref failed, builder);
        Check(BistroBuilderFinancingEngine.BuildInterestOperationId("loan_00000001", 2) ==
              "financing_repayment_loan_00000001_i002_interest",
            "OperationId interés estable", ref passed, ref failed, builder);

        report = builder.ToString();
        return failed == 0;
    }

    private static BistroBuilderLiquidityStatus Resolve(
        long cash,
        long available,
        long due,
        long overdue)
    {
        return BistroBuilderFinancingEngine.ResolveLiquidityStatus(
            cash,
            available,
            due,
            overdue,
            out _);
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
