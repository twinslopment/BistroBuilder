using System;
using System.Collections.Generic;

/// <summary>
/// Motor puro de 3H para históricos, indicadores y comparativas.
/// Solo consume resultados 3G ya calculados y nunca escribe Finanzas.
/// </summary>
public static class BistroBuilderFinancialHistoryEngine
{
    public static bool TryBuildPeriodReport(
        IReadOnlyList<BistroBuilderDayFinancialResult> dailyResults,
        int startDayIndex,
        int endDayIndex,
        out BistroBuilderFinancialPeriodReport report,
        out string error)
    {
        report = null;
        error = string.Empty;

        if (startDayIndex < 1 || endDayIndex < startDayIndex)
        {
            error = "El intervalo histórico no es válido.";
            return false;
        }

        long expectedLong = (long)endDayIndex - startDayIndex + 1L;
        if (expectedLong > int.MaxValue ||
            dailyResults == null ||
            dailyResults.Count != (int)expectedLong)
        {
            error = "El histórico no contiene exactamente un resultado por día.";
            return false;
        }

        try
        {
            var candidate = new BistroBuilderFinancialPeriodReport
            {
                startDayIndex = startDayIndex,
                endDayIndex = endDayIndex,
                dayCount = (int)expectedLong
            };

            BistroBuilderMealServicePerformance breakfast =
                NewServicePerformance(BistroBuilderMealServiceAvailability.Breakfast);
            BistroBuilderMealServicePerformance lunch =
                NewServicePerformance(BistroBuilderMealServiceAvailability.Lunch);
            BistroBuilderMealServicePerformance dinner =
                NewServicePerformance(BistroBuilderMealServiceAvailability.Dinner);

            bool hasServiceDay = false;
            bool hasResultDay = false;

            for (int index = 0; index < dailyResults.Count; index++)
            {
                BistroBuilderDayFinancialResult day = dailyResults[index];
                int expectedDay = checked(startDayIndex + index);

                if (day == null || day.dayIndex != expectedDay ||
                    day.serviceResults == null)
                {
                    error = "El histórico contiene un día nulo, desordenado o incompleto.";
                    return false;
                }

                candidate.dailyResults.Add(day.DeepClone());
                AddDay(candidate, day);

                if (day.HasFinancialActivity)
                {
                    candidate.financialActivityDayCount++;
                }

                if (day.HasServiceActivity)
                {
                    candidate.activeDayCount++;
                    if (!hasServiceDay || day.revenueCents > candidate.bestRevenueCents)
                    {
                        candidate.bestRevenueDayIndex = day.dayIndex;
                        candidate.bestRevenueCents = day.revenueCents;
                    }
                    hasServiceDay = true;
                }

                if (day.HasOperatingResultActivity)
                {
                    candidate.resultDayCount++;
                    if (day.operatingResultCents > 0L)
                    {
                        candidate.profitableDayCount++;
                    }
                    else if (day.operatingResultCents < 0L)
                    {
                        candidate.lossDayCount++;
                    }
                    else
                    {
                        candidate.breakEvenDayCount++;
                    }

                    if (!hasResultDay ||
                        day.operatingResultCents > candidate.bestOperatingResultCents)
                    {
                        candidate.bestOperatingResultDayIndex = day.dayIndex;
                        candidate.bestOperatingResultCents = day.operatingResultCents;
                    }

                    if (!hasResultDay ||
                        day.operatingResultCents < candidate.worstOperatingResultCents)
                    {
                        candidate.worstOperatingResultDayIndex = day.dayIndex;
                        candidate.worstOperatingResultCents = day.operatingResultCents;
                    }
                    hasResultDay = true;
                }

                for (int serviceIndex = 0;
                     serviceIndex < day.serviceResults.Count;
                     serviceIndex++)
                {
                    BistroBuilderServiceFinancialResult service =
                        day.serviceResults[serviceIndex];
                    if (service == null || service.dayIndex != day.dayIndex)
                    {
                        error = "Un día contiene un resultado de servicio incoherente.";
                        return false;
                    }

                    BistroBuilderMealServicePerformance target =
                        ResolveServicePerformance(
                            service.mealService,
                            breakfast,
                            lunch,
                            dinner);
                    if (target == null)
                    {
                        error = "El histórico contiene una franja de servicio no concreta.";
                        return false;
                    }

                    AddService(target, service);
                }
            }

            candidate.costQuality = ResolveCostQuality(
                candidate.consumedLineCount,
                candidate.estimatedLineCount,
                candidate.mixedLineCount,
                candidate.actualLineCount);
            candidate.grossMarginBasisPoints = CalculateMarginBasisPoints(
                candidate.grossProfitCents,
                candidate.revenueCents);
            candidate.operatingMarginBasisPoints = CalculateMarginBasisPoints(
                candidate.operatingResultCents,
                candidate.revenueCents);
            candidate.averageDailyRevenueCents = RoundDivide(
                candidate.revenueCents,
                candidate.dayCount);
            candidate.averageRevenuePerActiveDayCents =
                candidate.activeDayCount > 0
                    ? RoundDivide(candidate.revenueCents, candidate.activeDayCount)
                    : 0L;
            candidate.averageDailyOperatingResultCents = RoundDivide(
                candidate.operatingResultCents,
                candidate.dayCount);
            candidate.averageTicketCents = candidate.paidOrderCount > 0
                ? RoundDivide(candidate.revenueCents, candidate.paidOrderCount)
                : 0L;

            FinalizeServicePerformance(breakfast, candidate.dayCount);
            FinalizeServicePerformance(lunch, candidate.dayCount);
            FinalizeServicePerformance(dinner, candidate.dayCount);
            candidate.servicePerformance.Add(breakfast);
            candidate.servicePerformance.Add(lunch);
            candidate.servicePerformance.Add(dinner);

            candidate.topRevenueMealService = SelectTopRevenueService(
                breakfast,
                lunch,
                dinner);
            candidate.topGrossProfitMealService = SelectTopGrossProfitService(
                breakfast,
                lunch,
                dinner);

            report = candidate;
            return true;
        }
        catch (OverflowException)
        {
            report = null;
            error = "El histórico financiero desborda el rango monetario soportado.";
            return false;
        }
    }

    public static bool TryBuildComparison(
        BistroBuilderFinancialPeriodReport previousPeriod,
        BistroBuilderFinancialPeriodReport currentPeriod,
        out BistroBuilderFinancialPeriodComparison comparison,
        out string error)
    {
        comparison = null;
        error = string.Empty;

        if (!IsValidReport(previousPeriod) ||
            !IsValidReport(currentPeriod) ||
            previousPeriod.dayCount != currentPeriod.dayCount)
        {
            error = "La comparación necesita dos periodos válidos de igual duración.";
            return false;
        }

        try
        {
            long revenueDelta = checked(
                currentPeriod.revenueCents - previousPeriod.revenueCents);
            long grossProfitDelta = checked(
                currentPeriod.grossProfitCents - previousPeriod.grossProfitCents);
            long operatingDelta = checked(
                currentPeriod.operatingResultCents -
                previousPeriod.operatingResultCents);
            long ticketDelta = checked(
                currentPeriod.averageTicketCents -
                previousPeriod.averageTicketCents);
            long cashDelta = checked(
                currentPeriod.netCashChangeCents -
                previousPeriod.netCashChangeCents);

            var candidate = new BistroBuilderFinancialPeriodComparison
            {
                previousPeriod = previousPeriod.DeepClone(),
                currentPeriod = currentPeriod.DeepClone(),
                revenueDeltaCents = revenueDelta,
                revenueTrend = ResolveTrend(revenueDelta),
                grossProfitDeltaCents = grossProfitDelta,
                grossProfitTrend = ResolveTrend(grossProfitDelta),
                grossMarginDeltaBasisPoints = DifferenceBasisPoints(
                    currentPeriod.grossMarginBasisPoints,
                    previousPeriod.grossMarginBasisPoints),
                operatingResultDeltaCents = operatingDelta,
                operatingResultTrend = ResolveTrend(operatingDelta),
                operatingMarginDeltaBasisPoints = DifferenceBasisPoints(
                    currentPeriod.operatingMarginBasisPoints,
                    previousPeriod.operatingMarginBasisPoints),
                averageTicketDeltaCents = ticketDelta,
                averageTicketTrend = ResolveTrend(ticketDelta),
                netCashChangeDeltaCents = cashDelta,
                netCashTrend = ResolveTrend(cashDelta),
                activeDayDelta = checked(
                    currentPeriod.activeDayCount - previousPeriod.activeDayCount),
                paidOrderDelta = checked(
                    currentPeriod.paidOrderCount - previousPeriod.paidOrderCount)
            };

            candidate.grossMarginTrend = ResolveTrend(
                candidate.grossMarginDeltaBasisPoints);
            candidate.operatingMarginTrend = ResolveTrend(
                candidate.operatingMarginDeltaBasisPoints);

            candidate.hasRevenueChangeRate = TryCalculateRelativeChangeBasisPoints(
                previousPeriod.revenueCents,
                currentPeriod.revenueCents,
                out candidate.revenueChangeBasisPoints);
            candidate.hasAverageTicketChangeRate =
                TryCalculateRelativeChangeBasisPoints(
                    previousPeriod.averageTicketCents,
                    currentPeriod.averageTicketCents,
                    out candidate.averageTicketChangeBasisPoints);

            comparison = candidate;
            return true;
        }
        catch (OverflowException)
        {
            comparison = null;
            error = "La comparación histórica desborda el rango soportado.";
            return false;
        }
    }

    public static bool TryCalculateRelativeChangeBasisPoints(
        long previousValue,
        long currentValue,
        out int changeBasisPoints)
    {
        changeBasisPoints = 0;
        if (previousValue <= 0L)
        {
            return false;
        }

        decimal raw =
            ((decimal)currentValue - previousValue) * 10000m /
            previousValue;
        decimal rounded = decimal.Round(
            raw,
            0,
            MidpointRounding.AwayFromZero);

        if (rounded > int.MaxValue)
        {
            changeBasisPoints = int.MaxValue;
        }
        else if (rounded < int.MinValue)
        {
            changeBasisPoints = int.MinValue;
        }
        else
        {
            changeBasisPoints = (int)rounded;
        }

        return true;
    }

    public static BistroBuilderFinancialTrendDirection ResolveTrend(long delta)
    {
        if (delta > 0L)
        {
            return BistroBuilderFinancialTrendDirection.Up;
        }
        if (delta < 0L)
        {
            return BistroBuilderFinancialTrendDirection.Down;
        }
        return BistroBuilderFinancialTrendDirection.Flat;
    }

    private static void AddDay(
        BistroBuilderFinancialPeriodReport report,
        BistroBuilderDayFinancialResult day)
    {
        report.revenueCents = checked(report.revenueCents + day.revenueCents);
        report.productCostCents = checked(
            report.productCostCents + day.productCostCents);
        report.theoreticalProductCostCents = checked(
            report.theoreticalProductCostCents +
            day.theoreticalProductCostCents);
        report.grossProfitCents = checked(
            report.grossProfitCents + day.grossProfitCents);
        report.totalPeriodExpensesCents = checked(
            report.totalPeriodExpensesCents + day.totalPeriodExpensesCents);
        report.operatingResultCents = checked(
            report.operatingResultCents + day.operatingResultCents);
        report.inventoryWriteOffExpensesCents = checked(
            report.inventoryWriteOffExpensesCents +
            day.inventoryWriteOffExpensesCents);
        report.financingInterestExpensesCents = checked(
            report.financingInterestExpensesCents +
            day.financingInterestExpensesCents);
        report.paidOrderCount = checked(
            report.paidOrderCount + day.paidOrderCount);
        report.consumedLineCount = checked(
            report.consumedLineCount + day.consumedLineCount);
        report.estimatedLineCount = checked(
            report.estimatedLineCount + day.estimatedLineCount);
        report.mixedLineCount = checked(
            report.mixedLineCount + day.mixedLineCount);
        report.actualLineCount = checked(
            report.actualLineCount + day.actualLineCount);
        report.costedSalesCents = checked(
            report.costedSalesCents + day.costedSalesCents);
        report.costCoverageGapCents = checked(
            report.costCoverageGapCents + day.costCoverageGapCents);
        report.supplierPaymentBreakdownMissingCount = checked(
            report.supplierPaymentBreakdownMissingCount +
            day.supplierPaymentBreakdownMissingCount);
        report.totalCashInCents = checked(
            report.totalCashInCents + day.totalCashInCents);
        report.totalCashOutCents = checked(
            report.totalCashOutCents + day.totalCashOutCents);
        report.netCashChangeCents = checked(
            report.netCashChangeCents + day.netCashChangeCents);
        report.supplierPurchaseCashOutCents = checked(
            report.supplierPurchaseCashOutCents +
            day.supplierPurchaseCashOutCents);
        report.investmentCashOutCents = checked(
            report.investmentCashOutCents + day.investmentCashOutCents);
        report.debtPrincipalCashOutCents = checked(
            report.debtPrincipalCashOutCents + day.debtPrincipalCashOutCents);
        report.loanProceedsCashInCents = checked(
            report.loanProceedsCashInCents + day.loanProceedsCashInCents);
        report.assetResaleCashInCents = checked(
            report.assetResaleCashInCents + day.assetResaleCashInCents);
    }

    private static void AddService(
        BistroBuilderMealServicePerformance target,
        BistroBuilderServiceFinancialResult service)
    {
        if (service.HasActivity)
        {
            target.activeDayCount++;
        }

        target.revenueCents = checked(
            target.revenueCents + service.revenueCents);
        target.tableRevenueCents = checked(
            target.tableRevenueCents + service.tableRevenueCents);
        target.barRevenueCents = checked(
            target.barRevenueCents + service.barRevenueCents);
        target.paidOrderCount = checked(
            target.paidOrderCount + service.paidOrderCount);
        target.productCostCents = checked(
            target.productCostCents + service.productCostCents);
        target.theoreticalProductCostCents = checked(
            target.theoreticalProductCostCents +
            service.theoreticalProductCostCents);
        target.grossProfitCents = checked(
            target.grossProfitCents + service.grossProfitCents);
        target.consumedLineCount = checked(
            target.consumedLineCount + service.consumedLineCount);
        target.estimatedLineCount = checked(
            target.estimatedLineCount + service.estimatedLineCount);
        target.mixedLineCount = checked(
            target.mixedLineCount + service.mixedLineCount);
        target.actualLineCount = checked(
            target.actualLineCount + service.actualLineCount);
        target.costCoverageGapCents = checked(
            target.costCoverageGapCents + service.costCoverageGapCents);

        if (service.HasActivity &&
            (target.bestRevenueDayIndex == 0 ||
             service.revenueCents > target.bestRevenueCents))
        {
            target.bestRevenueDayIndex = service.dayIndex;
            target.bestRevenueCents = service.revenueCents;
        }
    }

    private static void FinalizeServicePerformance(
        BistroBuilderMealServicePerformance performance,
        int dayCount)
    {
        performance.dayCount = dayCount;
        performance.grossMarginBasisPoints = CalculateMarginBasisPoints(
            performance.grossProfitCents,
            performance.revenueCents);
        performance.costQuality = ResolveCostQuality(
            performance.consumedLineCount,
            performance.estimatedLineCount,
            performance.mixedLineCount,
            performance.actualLineCount);
        performance.averageRevenuePerActiveDayCents =
            performance.activeDayCount > 0
                ? RoundDivide(
                    performance.revenueCents,
                    performance.activeDayCount)
                : 0L;
        performance.averageTicketCents = performance.paidOrderCount > 0
            ? RoundDivide(
                performance.revenueCents,
                performance.paidOrderCount)
            : 0L;
    }

    private static BistroBuilderMealServicePerformance NewServicePerformance(
        BistroBuilderMealServiceAvailability mealService)
    {
        return new BistroBuilderMealServicePerformance
        {
            mealService = mealService
        };
    }

    private static BistroBuilderMealServicePerformance ResolveServicePerformance(
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderMealServicePerformance breakfast,
        BistroBuilderMealServicePerformance lunch,
        BistroBuilderMealServicePerformance dinner)
    {
        switch (mealService)
        {
            case BistroBuilderMealServiceAvailability.Breakfast:
                return breakfast;
            case BistroBuilderMealServiceAvailability.Lunch:
                return lunch;
            case BistroBuilderMealServiceAvailability.Dinner:
                return dinner;
            default:
                return null;
        }
    }

    private static BistroBuilderMealServiceAvailability SelectTopRevenueService(
        BistroBuilderMealServicePerformance breakfast,
        BistroBuilderMealServicePerformance lunch,
        BistroBuilderMealServicePerformance dinner)
    {
        BistroBuilderMealServicePerformance best = null;
        BistroBuilderMealServicePerformance[] items =
            { breakfast, lunch, dinner };

        for (int index = 0; index < items.Length; index++)
        {
            if (items[index].revenueCents <= 0L)
            {
                continue;
            }

            if (best == null || items[index].revenueCents > best.revenueCents)
            {
                best = items[index];
            }
        }

        return best != null
            ? best.mealService
            : BistroBuilderMealServiceAvailability.None;
    }

    private static BistroBuilderMealServiceAvailability SelectTopGrossProfitService(
        BistroBuilderMealServicePerformance breakfast,
        BistroBuilderMealServicePerformance lunch,
        BistroBuilderMealServicePerformance dinner)
    {
        BistroBuilderMealServicePerformance best = null;
        BistroBuilderMealServicePerformance[] items =
            { breakfast, lunch, dinner };

        for (int index = 0; index < items.Length; index++)
        {
            if (items[index].activeDayCount == 0)
            {
                continue;
            }

            if (best == null ||
                items[index].grossProfitCents > best.grossProfitCents)
            {
                best = items[index];
            }
        }

        return best != null
            ? best.mealService
            : BistroBuilderMealServiceAvailability.None;
    }

    private static BistroBuilderFinancialResultCostQuality ResolveCostQuality(
        int consumedLineCount,
        int estimatedLineCount,
        int mixedLineCount,
        int actualLineCount)
    {
        if (consumedLineCount <= 0)
        {
            return BistroBuilderFinancialResultCostQuality.None;
        }
        if (actualLineCount == consumedLineCount)
        {
            return BistroBuilderFinancialResultCostQuality.Actual;
        }
        if (estimatedLineCount == consumedLineCount)
        {
            return BistroBuilderFinancialResultCostQuality.Estimated;
        }
        return BistroBuilderFinancialResultCostQuality.Mixed;
    }

    private static int CalculateMarginBasisPoints(long marginCents, long revenueCents)
    {
        if (revenueCents <= 0L)
        {
            return 0;
        }

        decimal raw = (decimal)marginCents * 10000m / revenueCents;
        decimal rounded = decimal.Round(
            raw,
            0,
            MidpointRounding.AwayFromZero);
        if (rounded > int.MaxValue)
        {
            return int.MaxValue;
        }
        if (rounded < int.MinValue)
        {
            return int.MinValue;
        }
        return (int)rounded;
    }

    private static long RoundDivide(long value, int divisor)
    {
        if (divisor <= 0)
        {
            return 0L;
        }

        decimal rounded = decimal.Round(
            (decimal)value / divisor,
            0,
            MidpointRounding.AwayFromZero);
        if (rounded > long.MaxValue || rounded < long.MinValue)
        {
            throw new OverflowException();
        }
        return (long)rounded;
    }

    private static int DifferenceBasisPoints(int current, int previous)
    {
        long difference = (long)current - previous;
        if (difference > int.MaxValue)
        {
            return int.MaxValue;
        }
        if (difference < int.MinValue)
        {
            return int.MinValue;
        }
        return (int)difference;
    }

    private static bool IsValidReport(BistroBuilderFinancialPeriodReport report)
    {
        return report != null &&
               report.startDayIndex >= 1 &&
               report.endDayIndex >= report.startDayIndex &&
               report.dayCount ==
                   (long)report.endDayIndex - report.startDayIndex + 1L &&
               report.dailyResults != null &&
               report.dailyResults.Count == report.dayCount;
    }
}
