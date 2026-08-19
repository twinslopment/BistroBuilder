using System;
using System.Collections.Generic;

/// <summary>
/// Ventanas temporales disponibles en la UI financiera 3J.
/// AllTime comprende desde el día 1 hasta el día actual.
/// </summary>
public enum BistroBuilderFinanceDashboardPeriod
{
    Last7Days = 0,
    Last30Days = 1,
    Last90Days = 2,
    AllTime = 3
}

/// <summary>
/// Agrupación exclusivamente de presentación para movimientos de caja.
/// No sustituye CategoryId ni altera la taxonomía canónica del ledger 3A.
/// </summary>
public enum BistroBuilderFinanceMovementGroup
{
    Sales = 0,
    Supplier = 1,
    Operating = 2,
    Payroll = 3,
    Marketing = 4,
    Investment = 5,
    Financing = 6,
    Asset = 7,
    Other = 8
}

/// <summary>
/// Fila de lectura preparada para la pestaña Caja de 3J.
/// Mantiene la identidad original del movimiento para trazabilidad.
/// </summary>
[Serializable]
public sealed class BistroBuilderFinanceMovementView
{
    public long sequence;
    public string transactionId = string.Empty;
    public string operationId = string.Empty;
    public string sourceReferenceId = string.Empty;
    public string categoryId = string.Empty;
    public BistroBuilderFinanceTransactionKind kind;
    public BistroBuilderFinanceMovementGroup group;
    public long amountCents;
    public int dayIndex;
    public int minuteOfDay;
    public string displayLabel = string.Empty;
    public string description = string.Empty;

    public BistroBuilderFinanceMovementView DeepClone()
    {
        return (BistroBuilderFinanceMovementView)MemberwiseClone();
    }
}

/// <summary>
/// Read-model completo para una actualización de la UI 3J.
/// Es derivado y efímero: no tiene sección Save ni autoridad de negocio.
/// </summary>
[Serializable]
public sealed class BistroBuilderFinanceDashboardSnapshot
{
    public int dayIndex = 1;
    public string currencyCode = "EUR";
    public long financeRevision;
    public long financingRevision;

    public BistroBuilderFinanceDashboardPeriod period =
        BistroBuilderFinanceDashboardPeriod.Last7Days;
    public int periodStartDayIndex = 1;
    public int periodEndDayIndex = 1;

    public long cashBalanceCents;
    public BistroBuilderDayFinancialResult currentDay;
    public BistroBuilderFinancialPeriodReport periodReport;
    public BistroBuilderFinancialPeriodComparison periodComparison;
    public BistroBuilderLiquidityPosition liquidity;
    public BistroBuilderFinancialStressSnapshot stress;

    public List<BistroBuilderFinanceMovementView> recentMovements =
        new List<BistroBuilderFinanceMovementView>();
    public List<BistroBuilderFinancingOfferView> financingOffers =
        new List<BistroBuilderFinancingOfferView>();
    public List<BistroBuilderLoanRecord> loans =
        new List<BistroBuilderLoanRecord>();

    public bool HasPeriodComparison => periodComparison != null;
    public bool HasCompleteLiquidityProjection =>
        liquidity != null && liquidity.projectionComplete;

    public BistroBuilderFinanceDashboardSnapshot DeepClone()
    {
        var clone = new BistroBuilderFinanceDashboardSnapshot
        {
            dayIndex = dayIndex,
            currencyCode = currencyCode,
            financeRevision = financeRevision,
            financingRevision = financingRevision,
            period = period,
            periodStartDayIndex = periodStartDayIndex,
            periodEndDayIndex = periodEndDayIndex,
            cashBalanceCents = cashBalanceCents,
            currentDay = currentDay != null ? currentDay.DeepClone() : null,
            periodReport = periodReport != null ? periodReport.DeepClone() : null,
            periodComparison = periodComparison != null
                ? periodComparison.DeepClone()
                : null,
            liquidity = liquidity != null
                ? CloneLiquidity(liquidity)
                : null,
            stress = stress != null
                ? CloneStress(stress)
                : null
        };

        if (recentMovements != null)
        {
            for (int index = 0; index < recentMovements.Count; index++)
            {
                if (recentMovements[index] != null)
                {
                    clone.recentMovements.Add(
                        recentMovements[index].DeepClone());
                }
            }
        }

        if (financingOffers != null)
        {
            for (int index = 0; index < financingOffers.Count; index++)
            {
                if (financingOffers[index] != null)
                {
                    clone.financingOffers.Add(
                        financingOffers[index].DeepClone());
                }
            }
        }

        if (loans != null)
        {
            for (int index = 0; index < loans.Count; index++)
            {
                if (loans[index] != null)
                {
                    clone.loans.Add(loans[index].DeepClone());
                }
            }
        }

        return clone;
    }

    private static BistroBuilderLiquidityPosition CloneLiquidity(
        BistroBuilderLiquidityPosition source)
    {
        return new BistroBuilderLiquidityPosition
        {
            dayIndex = source.dayIndex,
            horizonDays = source.horizonDays,
            cashBalanceCents = source.cashBalanceCents,
            supplierCommitmentsResolved = source.supplierCommitmentsResolved,
            supplierCommittedCents = source.supplierCommittedCents,
            availableCashAfterSupplierCommitmentsCents =
                source.availableCashAfterSupplierCommitmentsCents,
            recurringOperatingObligationsResolved =
                source.recurringOperatingObligationsResolved,
            recurringOperatingObligationsWithinHorizonCents =
                source.recurringOperatingObligationsWithinHorizonCents,
            debtDueTodayCents = source.debtDueTodayCents,
            debtDueWithinHorizonCents = source.debtDueWithinHorizonCents,
            overdueDebtCents = source.overdueDebtCents,
            outstandingPrincipalCents = source.outstandingPrincipalCents,
            outstandingInterestCents = source.outstandingInterestCents,
            totalKnownHorizonObligationsCents =
                source.totalKnownHorizonObligationsCents,
            projectedLiquidityAfterHorizonObligationsCents =
                source.projectedLiquidityAfterHorizonObligationsCents,
            debtCoverageBasisPoints = source.debtCoverageBasisPoints,
            knownObligationCoverageBasisPoints =
                source.knownObligationCoverageBasisPoints,
            projectionComplete = source.projectionComplete,
            status = source.status
        };
    }

    private static BistroBuilderFinancialStressSnapshot CloneStress(
        BistroBuilderFinancialStressSnapshot source)
    {
        return new BistroBuilderFinancialStressSnapshot
        {
            dayIndex = source.dayIndex,
            analysisWindowDays = source.analysisWindowDays,
            rollingRevenueCents = source.rollingRevenueCents,
            rollingOperatingResultCents = source.rollingOperatingResultCents,
            rollingLossDayCount = source.rollingLossDayCount,
            consecutiveLossDays = source.consecutiveLossDays,
            outstandingDebtCents = source.outstandingDebtCents,
            overdueDebtCents = source.overdueDebtCents,
            liquidityStatus = source.liquidityStatus,
            riskLevel = source.riskLevel
        };
    }
}
