using System;
using System.Collections.Generic;
using System.Text;

public static class BistroBuilderFinance3HSelfTest
{
    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER 3H — AUTOTEST ===");

        List<BistroBuilderDayFinancialResult> days = BuildFixture();
        bool built = BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            days,
            10,
            13,
            out BistroBuilderFinancialPeriodReport period,
            out string error);

        Check(built, "Construcción del periodo 10-13", ref passed, ref failed, builder);
        Check(string.IsNullOrEmpty(error), "Periodo sin error", ref passed, ref failed, builder);

        if (period != null)
        {
            Check(period.startDayIndex == 10 && period.endDayIndex == 13,
                "Identidad temporal inclusiva", ref passed, ref failed, builder);
            Check(period.dayCount == 4, "Cuatro días históricos", ref passed, ref failed, builder);
            Check(period.dailyResults.Count == 4, "Serie diaria completa", ref passed, ref failed, builder);
            Check(period.activeDayCount == 3, "Tres días con actividad", ref passed, ref failed, builder);
            Check(period.profitableDayCount == 2, "Dos días rentables", ref passed, ref failed, builder);
            Check(period.lossDayCount == 1, "Un día con pérdida", ref passed, ref failed, builder);
            Check(period.breakEvenDayCount == 0, "Sin falsos break-even por día inactivo", ref passed, ref failed, builder);
            Check(period.revenueCents == 33000L, "Ingresos históricos 330,00 €", ref passed, ref failed, builder);
            Check(period.productCostCents == 11500L, "COGS histórico 115,00 €", ref passed, ref failed, builder);
            Check(period.theoreticalProductCostCents == 10300L, "Coste teórico histórico", ref passed, ref failed, builder);
            Check(period.grossProfitCents == 21500L, "Margen bruto 215,00 €", ref passed, ref failed, builder);
            Check(period.grossMarginBasisPoints == 6515, "Margen bruto 65,15 %", ref passed, ref failed, builder);
            Check(period.totalPeriodExpensesCents == 13000L, "Gastos del periodo 130,00 €", ref passed, ref failed, builder);
            Check(period.operatingResultCents == 8500L, "Resultado operativo 85,00 €", ref passed, ref failed, builder);
            Check(period.operatingMarginBasisPoints == 2576, "Margen operativo 25,76 %", ref passed, ref failed, builder);
            Check(period.paidOrderCount == 6, "Seis cuentas pagadas", ref passed, ref failed, builder);
            Check(period.averageTicketCents == 5500L, "Ticket medio 55,00 €", ref passed, ref failed, builder);
            Check(period.averageDailyRevenueCents == 8250L, "Media diaria 82,50 €", ref passed, ref failed, builder);
            Check(period.averageRevenuePerActiveDayCents == 11000L, "Media por día activo 110,00 €", ref passed, ref failed, builder);
            Check(period.averageDailyOperatingResultCents == 2125L, "Resultado medio diario 21,25 €", ref passed, ref failed, builder);
            Check(period.bestRevenueDayIndex == 11 && period.bestRevenueCents == 15000L,
                "Mejor día por ventas", ref passed, ref failed, builder);
            Check(period.bestOperatingResultDayIndex == 11 && period.bestOperatingResultCents == 5500L,
                "Mejor día por resultado", ref passed, ref failed, builder);
            Check(period.worstOperatingResultDayIndex == 13 && period.worstOperatingResultCents == -2000L,
                "Peor día por resultado", ref passed, ref failed, builder);
            Check(period.totalCashInCents == 34000L, "Entradas de caja 340,00 €", ref passed, ref failed, builder);
            Check(period.totalCashOutCents == 19000L, "Salidas de caja 190,00 €", ref passed, ref failed, builder);
            Check(period.netCashChangeCents == 15000L, "Variación neta de caja 150,00 €", ref passed, ref failed, builder);
            Check(period.investmentCashOutCents == 6000L, "Inversión separada 60,00 €", ref passed, ref failed, builder);
            Check(period.assetResaleCashInCents == 1000L, "Reventa separada 10,00 €", ref passed, ref failed, builder);
            Check(period.costQuality == BistroBuilderFinancialResultCostQuality.Mixed,
                "Calidad de coste agregada Mixed", ref passed, ref failed, builder);
            Check(period.IsCostCoverageComplete, "Cobertura COGS completa", ref passed, ref failed, builder);
            Check(period.HasCompleteSupplierPaymentBreakdown,
                "Desglose de proveedor completo", ref passed, ref failed, builder);
            Check(period.servicePerformance.Count == 3,
                "Tres franjas de servicio en informe", ref passed, ref failed, builder);
            Check(period.topRevenueMealService == BistroBuilderMealServiceAvailability.Lunch,
                "Lunch lidera ingresos", ref passed, ref failed, builder);
            Check(period.topGrossProfitMealService == BistroBuilderMealServiceAvailability.Lunch,
                "Lunch lidera margen bruto", ref passed, ref failed, builder);

            BistroBuilderMealServicePerformance breakfast = FindService(
                period,
                BistroBuilderMealServiceAvailability.Breakfast);
            BistroBuilderMealServicePerformance lunch = FindService(
                period,
                BistroBuilderMealServiceAvailability.Lunch);
            BistroBuilderMealServicePerformance dinner = FindService(
                period,
                BistroBuilderMealServiceAvailability.Dinner);

            Check(breakfast != null && breakfast.revenueCents == 10000L &&
                  breakfast.activeDayCount == 1 && breakfast.bestRevenueDayIndex == 10,
                "Histórico Breakfast correcto", ref passed, ref failed, builder);
            Check(lunch != null && lunch.revenueCents == 15000L &&
                  lunch.tableRevenueCents == 10000L && lunch.barRevenueCents == 5000L,
                "Histórico Lunch mesa/barra correcto", ref passed, ref failed, builder);
            Check(dinner != null && dinner.revenueCents == 8000L &&
                  dinner.grossProfitCents == 4000L,
                "Histórico Dinner correcto", ref passed, ref failed, builder);
        }

        BuildComparisonTests(ref passed, ref failed, builder);
        BuildGuardTests(ref passed, ref failed, builder);
        BuildCloneTest(ref passed, ref failed, builder);

        builder.AppendLine();
        builder.AppendLine(
            "Resultado: " + passed + " OK / " + failed + " fallos");
        report = builder.ToString();
        return failed == 0;
    }

    private static void BuildComparisonTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        List<BistroBuilderDayFinancialResult> days = BuildFixture();
        var previousDays = new List<BistroBuilderDayFinancialResult>
        {
            days[0], days[1]
        };
        var currentDays = new List<BistroBuilderDayFinancialResult>
        {
            days[2], days[3]
        };

        bool previousOk = BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            previousDays, 10, 11,
            out BistroBuilderFinancialPeriodReport previous,
            out _);
        bool currentOk = BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            currentDays, 12, 13,
            out BistroBuilderFinancialPeriodReport current,
            out _);
        string compareError = string.Empty;
        BistroBuilderFinancialPeriodComparison comparison = null;
        bool compareOk = previousOk && currentOk;
        if (compareOk)
        {
            compareOk = BistroBuilderFinancialHistoryEngine.TryBuildComparison(
                previous,
                current,
                out comparison,
                out compareError);
        }

        Check(compareOk, "Comparación de periodos equivalentes", ref passed, ref failed, builder);
        Check(string.IsNullOrEmpty(compareError), "Comparación sin error", ref passed, ref failed, builder);

        if (comparison != null)
        {
            Check(comparison.revenueDeltaCents == -17000L,
                "Delta ingresos -170,00 €", ref passed, ref failed, builder);
            Check(comparison.hasRevenueChangeRate &&
                  comparison.revenueChangeBasisPoints == -6800,
                "Variación ingresos -68,00 %", ref passed, ref failed, builder);
            Check(comparison.revenueTrend == BistroBuilderFinancialTrendDirection.Down,
                "Tendencia de ingresos Down", ref passed, ref failed, builder);
            Check(comparison.grossProfitDeltaCents == -13500L,
                "Delta margen bruto -135,00 €", ref passed, ref failed, builder);
            Check(comparison.grossMarginDeltaBasisPoints == -2000,
                "Delta margen bruto -20 puntos", ref passed, ref failed, builder);
            Check(comparison.operatingResultDeltaCents == -12500L,
                "Delta resultado operativo -125,00 €", ref passed, ref failed, builder);
            Check(comparison.operatingMarginDeltaBasisPoints == -6700,
                "Delta margen operativo -67 puntos", ref passed, ref failed, builder);
            Check(comparison.averageTicketDeltaCents == 3000L,
                "Ticket medio sube 30,00 €", ref passed, ref failed, builder);
            Check(comparison.hasAverageTicketChangeRate &&
                  comparison.averageTicketChangeBasisPoints == 6000,
                "Ticket medio sube 60,00 %", ref passed, ref failed, builder);
            Check(comparison.averageTicketTrend == BistroBuilderFinancialTrendDirection.Up,
                "Tendencia ticket Up", ref passed, ref failed, builder);
            Check(comparison.netCashChangeDeltaCents == -13000L &&
                  comparison.netCashTrend == BistroBuilderFinancialTrendDirection.Down,
                "Tendencia caja separada", ref passed, ref failed, builder);
            Check(comparison.activeDayDelta == -1 && comparison.paidOrderDelta == -4,
                "Deltas de actividad y cuentas", ref passed, ref failed, builder);
        }
    }

    private static void BuildGuardTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        List<BistroBuilderDayFinancialResult> days = BuildFixture();

        bool nullRejected = !BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            null, 1, 1, out _, out _);
        Check(nullRejected, "Rechaza histórico nulo", ref passed, ref failed, builder);

        bool invalidRangeRejected = !BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            days, 13, 10, out _, out _);
        Check(invalidRangeRejected, "Rechaza rango invertido", ref passed, ref failed, builder);

        bool wrongCountRejected = !BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            days, 10, 12, out _, out _);
        Check(wrongCountRejected, "Rechaza número de días incoherente", ref passed, ref failed, builder);

        List<BistroBuilderDayFinancialResult> reordered = BuildFixture();
        BistroBuilderDayFinancialResult swap = reordered[0];
        reordered[0] = reordered[1];
        reordered[1] = swap;
        bool orderRejected = !BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            reordered, 10, 13, out _, out _);
        Check(orderRejected, "Rechaza serie diaria desordenada", ref passed, ref failed, builder);

        BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            new List<BistroBuilderDayFinancialResult> { days[0] },
            10, 10, out BistroBuilderFinancialPeriodReport oneDay, out _);
        BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            new List<BistroBuilderDayFinancialResult> { days[1], days[2] },
            11, 12, out BistroBuilderFinancialPeriodReport twoDays, out _);
        bool unequalRejected = !BistroBuilderFinancialHistoryEngine.TryBuildComparison(
            oneDay, twoDays, out _, out _);
        Check(unequalRejected, "Rechaza comparación de distinta duración", ref passed, ref failed, builder);

        bool zeroBase = BistroBuilderFinancialHistoryEngine.TryCalculateRelativeChangeBasisPoints(
            0L, 100L, out int zeroRate);
        Check(!zeroBase && zeroRate == 0,
            "No inventa porcentaje con base cero", ref passed, ref failed, builder);

        bool positiveRate = BistroBuilderFinancialHistoryEngine.TryCalculateRelativeChangeBasisPoints(
            100L, 125L, out int rate);
        Check(positiveRate && rate == 2500,
            "Ratio relativo +25,00 %", ref passed, ref failed, builder);

        Check(BistroBuilderFinancialHistoryEngine.ResolveTrend(0L) ==
              BistroBuilderFinancialTrendDirection.Flat,
            "Trend Flat", ref passed, ref failed, builder);
        Check(BistroBuilderFinancialHistoryEngine.ResolveTrend(1L) ==
              BistroBuilderFinancialTrendDirection.Up,
            "Trend Up", ref passed, ref failed, builder);
        Check(BistroBuilderFinancialHistoryEngine.ResolveTrend(-1L) ==
              BistroBuilderFinancialTrendDirection.Down,
            "Trend Down", ref passed, ref failed, builder);
    }

    private static void BuildCloneTest(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        List<BistroBuilderDayFinancialResult> days = BuildFixture();
        BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            days, 10, 13,
            out BistroBuilderFinancialPeriodReport report,
            out _);

        long original = report.dailyResults[0].revenueCents;
        days[0].revenueCents = 999999L;
        Check(report.dailyResults[0].revenueCents == original,
            "El informe posee snapshots y no alias de días", ref passed, ref failed, builder);

        BistroBuilderFinancialPeriodReport clone = report.DeepClone();
        clone.dailyResults[0].revenueCents = 123L;
        Check(report.dailyResults[0].revenueCents == original,
            "DeepClone del informe aísla la serie", ref passed, ref failed, builder);
    }

    private static List<BistroBuilderDayFinancialResult> BuildFixture()
    {
        return new List<BistroBuilderDayFinancialResult>
        {
            MakeDay(
                10,
                BistroBuilderMealServiceAvailability.Breakfast,
                10000L, 10000L, 0L, 2,
                3000L, 2800L, 2000L,
                10000L, 2000L,
                BistroBuilderFinancialResultCostQuality.Actual),
            MakeDay(
                11,
                BistroBuilderMealServiceAvailability.Lunch,
                15000L, 10000L, 5000L, 3,
                4500L, 4000L, 5000L,
                15000L, 9000L,
                BistroBuilderFinancialResultCostQuality.Mixed),
            MakeDay(
                12,
                BistroBuilderMealServiceAvailability.Dinner,
                0L, 0L, 0L, 0,
                0L, 0L, 0L,
                0L, 0L,
                BistroBuilderFinancialResultCostQuality.None),
            MakeDay(
                13,
                BistroBuilderMealServiceAvailability.Dinner,
                8000L, 0L, 8000L, 1,
                4000L, 3500L, 6000L,
                9000L, 8000L,
                BistroBuilderFinancialResultCostQuality.Estimated)
        };
    }

    private static BistroBuilderDayFinancialResult MakeDay(
        int dayIndex,
        BistroBuilderMealServiceAvailability activeMeal,
        long revenue,
        long tableRevenue,
        long barRevenue,
        int orders,
        long cogs,
        long theoreticalCogs,
        long expenses,
        long cashIn,
        long cashOut,
        BistroBuilderFinancialResultCostQuality quality)
    {
        var day = new BistroBuilderDayFinancialResult
        {
            dayIndex = dayIndex,
            revenueCents = revenue,
            costedSalesCents = revenue,
            productCostCents = cogs,
            theoreticalProductCostCents = theoreticalCogs,
            paidOrderCount = orders,
            consumedLineCount = orders,
            costQuality = quality,
            costCoverageGapCents = 0L,
            grossProfitCents = revenue - cogs,
            grossMarginBasisPoints = Ratio(revenue - cogs, revenue),
            totalPeriodExpensesCents = expenses,
            operatingResultCents = revenue - cogs - expenses,
            totalCashInCents = cashIn,
            totalCashOutCents = cashOut,
            netCashChangeCents = cashIn - cashOut,
            investmentCashOutCents = Math.Max(0L, cashOut - expenses),
            assetResaleCashInCents = Math.Max(0L, cashIn - revenue)
        };

        ApplyQualityCounts(day, quality, orders);

        day.serviceResults.Add(MakeService(
            dayIndex,
            BistroBuilderMealServiceAvailability.Breakfast,
            activeMeal == BistroBuilderMealServiceAvailability.Breakfast,
            revenue, tableRevenue, barRevenue, orders, cogs, theoreticalCogs, quality));
        day.serviceResults.Add(MakeService(
            dayIndex,
            BistroBuilderMealServiceAvailability.Lunch,
            activeMeal == BistroBuilderMealServiceAvailability.Lunch,
            revenue, tableRevenue, barRevenue, orders, cogs, theoreticalCogs, quality));
        day.serviceResults.Add(MakeService(
            dayIndex,
            BistroBuilderMealServiceAvailability.Dinner,
            activeMeal == BistroBuilderMealServiceAvailability.Dinner,
            revenue, tableRevenue, barRevenue, orders, cogs, theoreticalCogs, quality));

        return day;
    }

    private static BistroBuilderServiceFinancialResult MakeService(
        int dayIndex,
        BistroBuilderMealServiceAvailability meal,
        bool active,
        long revenue,
        long tableRevenue,
        long barRevenue,
        int orders,
        long cogs,
        long theoreticalCogs,
        BistroBuilderFinancialResultCostQuality quality)
    {
        var service = new BistroBuilderServiceFinancialResult
        {
            dayIndex = dayIndex,
            mealService = meal
        };

        if (!active)
        {
            return service;
        }

        service.revenueCents = revenue;
        service.tableRevenueCents = tableRevenue;
        service.barRevenueCents = barRevenue;
        service.paidOrderCount = orders;
        service.costedSalesCents = revenue;
        service.productCostCents = cogs;
        service.theoreticalProductCostCents = theoreticalCogs;
        service.consumedLineCount = orders;
        service.costQuality = quality;
        service.costCoverageGapCents = 0L;
        service.grossProfitCents = revenue - cogs;
        service.grossMarginBasisPoints = Ratio(revenue - cogs, revenue);
        ApplyQualityCounts(service, quality, orders);
        return service;
    }

    private static void ApplyQualityCounts(
        BistroBuilderDayFinancialResult day,
        BistroBuilderFinancialResultCostQuality quality,
        int count)
    {
        if (quality == BistroBuilderFinancialResultCostQuality.Actual)
        {
            day.actualLineCount = count;
        }
        else if (quality == BistroBuilderFinancialResultCostQuality.Estimated)
        {
            day.estimatedLineCount = count;
        }
        else if (quality == BistroBuilderFinancialResultCostQuality.Mixed)
        {
            day.mixedLineCount = count;
        }
    }

    private static void ApplyQualityCounts(
        BistroBuilderServiceFinancialResult service,
        BistroBuilderFinancialResultCostQuality quality,
        int count)
    {
        if (quality == BistroBuilderFinancialResultCostQuality.Actual)
        {
            service.actualLineCount = count;
        }
        else if (quality == BistroBuilderFinancialResultCostQuality.Estimated)
        {
            service.estimatedLineCount = count;
        }
        else if (quality == BistroBuilderFinancialResultCostQuality.Mixed)
        {
            service.mixedLineCount = count;
        }
    }

    private static int Ratio(long margin, long revenue)
    {
        if (revenue <= 0L)
        {
            return 0;
        }
        return (int)decimal.Round(
            margin * 10000m / revenue,
            0,
            MidpointRounding.AwayFromZero);
    }

    private static BistroBuilderMealServicePerformance FindService(
        BistroBuilderFinancialPeriodReport report,
        BistroBuilderMealServiceAvailability meal)
    {
        for (int index = 0; index < report.servicePerformance.Count; index++)
        {
            if (report.servicePerformance[index] != null &&
                report.servicePerformance[index].mealService == meal)
            {
                return report.servicePerformance[index];
            }
        }
        return null;
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
