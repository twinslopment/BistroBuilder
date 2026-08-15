using System;
using System.Collections.Generic;

public enum BistroBuilderFinancialTrendDirection
{
    InsufficientData = 0,
    Down = 1,
    Flat = 2,
    Up = 3
}

/// <summary>
/// Rendimiento agregado de Breakfast/Lunch/Dinner dentro de un periodo.
/// No reparte gastos generales; conserva la contribución bruta propia de 3G.
/// </summary>
[Serializable]
public sealed class BistroBuilderMealServicePerformance
{
    public BistroBuilderMealServiceAvailability mealService =
        BistroBuilderMealServiceAvailability.Lunch;
    public int dayCount;
    public int activeDayCount;
    public long revenueCents;
    public long tableRevenueCents;
    public long barRevenueCents;
    public int paidOrderCount;
    public long productCostCents;
    public long theoreticalProductCostCents;
    public long grossProfitCents;
    public int grossMarginBasisPoints;
    public int consumedLineCount;
    public int estimatedLineCount;
    public int mixedLineCount;
    public int actualLineCount;
    public BistroBuilderFinancialResultCostQuality costQuality;
    public long costCoverageGapCents;
    public long averageRevenuePerActiveDayCents;
    public long averageTicketCents;
    public int bestRevenueDayIndex;
    public long bestRevenueCents;

    public bool IsCostCoverageComplete => costCoverageGapCents == 0L;

    public BistroBuilderMealServicePerformance DeepClone()
    {
        return (BistroBuilderMealServicePerformance)MemberwiseClone();
    }
}

/// <summary>
/// Informe histórico de un intervalo inclusivo de días.
///
/// Es una proyección derivada de 3G: no posee ledger, no congela resultados y
/// no necesita una sección de guardado propia.
/// </summary>
[Serializable]
public sealed class BistroBuilderFinancialPeriodReport
{
    public int startDayIndex = 1;
    public int endDayIndex = 1;
    public int dayCount = 1;
    public int activeDayCount;
    public int profitableDayCount;
    public int lossDayCount;
    public int breakEvenDayCount;

    public long revenueCents;
    public long productCostCents;
    public long theoreticalProductCostCents;
    public long grossProfitCents;
    public int grossMarginBasisPoints;
    public long totalPeriodExpensesCents;
    public long operatingResultCents;
    public int operatingMarginBasisPoints;

    public int paidOrderCount;
    public int consumedLineCount;
    public int estimatedLineCount;
    public int mixedLineCount;
    public int actualLineCount;
    public BistroBuilderFinancialResultCostQuality costQuality;
    public long costedSalesCents;
    public long costCoverageGapCents;
    public int supplierPaymentBreakdownMissingCount;

    public long totalCashInCents;
    public long totalCashOutCents;
    public long netCashChangeCents;
    public long supplierPurchaseCashOutCents;
    public long investmentCashOutCents;
    public long assetResaleCashInCents;

    public long averageDailyRevenueCents;
    public long averageRevenuePerActiveDayCents;
    public long averageDailyOperatingResultCents;
    public long averageTicketCents;

    public int bestRevenueDayIndex;
    public long bestRevenueCents;
    public int bestOperatingResultDayIndex;
    public long bestOperatingResultCents;
    public int worstOperatingResultDayIndex;
    public long worstOperatingResultCents;

    public BistroBuilderMealServiceAvailability topRevenueMealService =
        BistroBuilderMealServiceAvailability.None;
    public BistroBuilderMealServiceAvailability topGrossProfitMealService =
        BistroBuilderMealServiceAvailability.None;

    public List<BistroBuilderDayFinancialResult> dailyResults =
        new List<BistroBuilderDayFinancialResult>();
    public List<BistroBuilderMealServicePerformance> servicePerformance =
        new List<BistroBuilderMealServicePerformance>(3);

    public bool IsCostCoverageComplete => costCoverageGapCents == 0L;
    public bool HasCompleteSupplierPaymentBreakdown =>
        supplierPaymentBreakdownMissingCount == 0;

    public BistroBuilderFinancialPeriodReport DeepClone()
    {
        var clone = (BistroBuilderFinancialPeriodReport)MemberwiseClone();
        clone.dailyResults = new List<BistroBuilderDayFinancialResult>();
        clone.servicePerformance = new List<BistroBuilderMealServicePerformance>();

        if (dailyResults != null)
        {
            for (int index = 0; index < dailyResults.Count; index++)
            {
                if (dailyResults[index] != null)
                {
                    clone.dailyResults.Add(dailyResults[index].DeepClone());
                }
            }
        }

        if (servicePerformance != null)
        {
            for (int index = 0; index < servicePerformance.Count; index++)
            {
                if (servicePerformance[index] != null)
                {
                    clone.servicePerformance.Add(
                        servicePerformance[index].DeepClone());
                }
            }
        }

        return clone;
    }
}

/// <summary>
/// Comparación entre dos periodos de igual duración.
/// Los ratios relativos solo se publican cuando la base anterior es positiva;
/// nunca se fabrica un porcentaje a partir de división por cero.
/// </summary>
[Serializable]
public sealed class BistroBuilderFinancialPeriodComparison
{
    public BistroBuilderFinancialPeriodReport previousPeriod;
    public BistroBuilderFinancialPeriodReport currentPeriod;

    public long revenueDeltaCents;
    public bool hasRevenueChangeRate;
    public int revenueChangeBasisPoints;
    public BistroBuilderFinancialTrendDirection revenueTrend;

    public long grossProfitDeltaCents;
    public BistroBuilderFinancialTrendDirection grossProfitTrend;
    public int grossMarginDeltaBasisPoints;
    public BistroBuilderFinancialTrendDirection grossMarginTrend;

    public long operatingResultDeltaCents;
    public BistroBuilderFinancialTrendDirection operatingResultTrend;
    public int operatingMarginDeltaBasisPoints;
    public BistroBuilderFinancialTrendDirection operatingMarginTrend;

    public long averageTicketDeltaCents;
    public bool hasAverageTicketChangeRate;
    public int averageTicketChangeBasisPoints;
    public BistroBuilderFinancialTrendDirection averageTicketTrend;

    public long netCashChangeDeltaCents;
    public BistroBuilderFinancialTrendDirection netCashTrend;
    public int activeDayDelta;
    public int paidOrderDelta;

    public BistroBuilderFinancialPeriodComparison DeepClone()
    {
        var clone = (BistroBuilderFinancialPeriodComparison)MemberwiseClone();
        clone.previousPeriod = previousPeriod != null
            ? previousPeriod.DeepClone()
            : null;
        clone.currentPeriod = currentPeriod != null
            ? currentPeriod.DeepClone()
            : null;
        return clone;
    }
}
