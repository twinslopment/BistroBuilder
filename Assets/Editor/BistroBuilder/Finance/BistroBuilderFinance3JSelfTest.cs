using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro de contratos de presentación 3J. No modifica la escena ni
/// publica movimientos financieros.
/// </summary>
public static class BistroBuilderFinance3JSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3J - Autotest",
        false,
        3102)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3J",
            "Autotest: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest 3J ha fallado.");
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
        builder.AppendLine("=== BISTRO BUILDER — AUTOTEST 3J FINANZAS Y CAJA ===");

        RunPeriodTests(ref passed, ref failed, builder);
        RunFormatTests(ref passed, ref failed, builder);
        RunCloneTests(ref passed, ref failed, builder);
        RunChartTests(ref passed, ref failed, builder);
        RunContractTests(ref passed, ref failed, builder);

        builder.AppendLine();
        builder.AppendLine(
            "Resultado: " + passed + " OK / " + failed + " fallos");
        report = builder.ToString();
        return failed == 0;
    }

    private static void RunPeriodTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        BistroBuilderFinanceDashboardService.ResolvePeriodRange(
            BistroBuilderFinanceDashboardPeriod.Last7Days,
            1,
            out int start,
            out int end);
        Check(start == 1 && end == 1,
            "7 días recorta correctamente al inicio de partida",
            ref passed, ref failed, builder);

        BistroBuilderFinanceDashboardService.ResolvePeriodRange(
            BistroBuilderFinanceDashboardPeriod.Last7Days,
            10,
            out start,
            out end);
        Check(start == 4 && end == 10,
            "Ventana 7 días inclusiva",
            ref passed, ref failed, builder);

        BistroBuilderFinanceDashboardService.ResolvePeriodRange(
            BistroBuilderFinanceDashboardPeriod.Last30Days,
            40,
            out start,
            out end);
        Check(start == 11 && end == 40,
            "Ventana 30 días inclusiva",
            ref passed, ref failed, builder);

        BistroBuilderFinanceDashboardService.ResolvePeriodRange(
            BistroBuilderFinanceDashboardPeriod.Last90Days,
            100,
            out start,
            out end);
        Check(start == 11 && end == 100,
            "Ventana 90 días inclusiva",
            ref passed, ref failed, builder);

        BistroBuilderFinanceDashboardService.ResolvePeriodRange(
            BistroBuilderFinanceDashboardPeriod.AllTime,
            123,
            out start,
            out end);
        Check(start == 1 && end == 123,
            "Todo el histórico comienza en día 1",
            ref passed, ref failed, builder);

        BistroBuilderFinanceDashboardService.ResolvePeriodRange(
            BistroBuilderFinanceDashboardPeriod.AllTime,
            0,
            out start,
            out end);
        Check(start == 1 && end == 1,
            "Periodo protege DayIndex inferior a 1",
            ref passed, ref failed, builder);
    }

    private static void RunFormatTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        Check(BistroBuilderFinanceUiFormat.Money(123456L) == "1.234,56 €",
            "Dinero EUR usa céntimos y formato es-ES",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Money(-123456L) == "−1.234,56 €",
            "Importe negativo visible sin alterar signo contable",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Money(123L, true) == "+1,23 €",
            "Importe firmado positivo",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Money(-123L, true) == "−1,23 €",
            "Importe firmado negativo",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Money(0L, true) == "0,00 €",
            "Cero no muestra signo artificial",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Percent(6977) == "69,77 %",
            "Basis points se presentan como porcentaje correcto",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Liquidity(
                  BistroBuilderLiquidityStatus.Unknown) == "Información incompleta",
            "Liquidez Unknown nunca aparece como Sana",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Liquidity(
                  BistroBuilderLiquidityStatus.Critical) == "Crítica",
            "Liquidez Critical humanizada",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Risk(
                  BistroBuilderFinancialRiskLevel.Severe) == "Severo",
            "Riesgo Severe humanizado",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Trend(
                  BistroBuilderFinancialTrendDirection.Up) == "Sube" &&
              BistroBuilderFinanceUiFormat.Trend(
                  BistroBuilderFinancialTrendDirection.Down) == "Baja",
            "Tendencias no exponen enum técnico",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.MealService(
                  BistroBuilderMealServiceAvailability.Lunch) == "Comida",
            "Lunch se presenta en español",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.CostQuality(
                  BistroBuilderFinancialResultCostQuality.Mixed) == "Coste mixto",
            "Calidad de coste visible y comprensible",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.LoanStatus(
                  BistroBuilderLoanStatus.Defaulted) == "Impagado",
            "Defaulted se presenta como Impagado",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Clock(615) == "10:15",
            "Hora financiera formateada",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceUiFormat.Clock(9999) == "23:59",
            "Hora visible se limita a un día",
            ref passed, ref failed, builder);
    }

    private static void RunCloneTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        var dashboard = new BistroBuilderFinanceDashboardSnapshot
        {
            dayIndex = 9,
            currencyCode = "EUR",
            financeRevision = 8L,
            financingRevision = 3L,
            cashBalanceCents = 12345L,
            currentDay = new BistroBuilderDayFinancialResult
            {
                dayIndex = 9,
                revenueCents = 2500L,
                operatingResultCents = 900L
            },
            periodReport = new BistroBuilderFinancialPeriodReport
            {
                startDayIndex = 3,
                endDayIndex = 9,
                dayCount = 7,
                revenueCents = 10000L
            },
            liquidity = new BistroBuilderLiquidityPosition
            {
                dayIndex = 9,
                projectionComplete = true,
                status = BistroBuilderLiquidityStatus.Healthy,
                cashBalanceCents = 12345L
            },
            stress = new BistroBuilderFinancialStressSnapshot
            {
                dayIndex = 9,
                riskLevel = BistroBuilderFinancialRiskLevel.Low
            }
        };
        dashboard.recentMovements.Add(new BistroBuilderFinanceMovementView
        {
            sequence = 1L,
            operationId = "ui_clone_move",
            amountCents = 500L
        });
        dashboard.financingOffers.Add(new BistroBuilderFinancingOfferView
        {
            offerId = "bridge",
            principalCents = 500000L,
            eligible = true
        });
        var loan = new BistroBuilderLoanRecord
        {
            loanId = "loan_00000001",
            principalCents = 500000L
        };
        loan.installments.Add(new BistroBuilderLoanInstallmentRecord
        {
            installmentNumber = 1,
            dueDayIndex = 7,
            principalCents = 100000L,
            interestCents = 5000L
        });
        dashboard.loans.Add(loan);

        BistroBuilderFinanceDashboardSnapshot clone = dashboard.DeepClone();
        clone.currentDay.revenueCents = 999999L;
        clone.periodReport.revenueCents = 888888L;
        clone.liquidity.status = BistroBuilderLiquidityStatus.Critical;
        clone.recentMovements[0].amountCents = 1L;
        clone.financingOffers[0].principalCents = 1L;
        clone.loans[0].installments[0].principalCents = 1L;

        Check(dashboard.currentDay.revenueCents == 2500L,
            "Dashboard clone aísla resultado diario",
            ref passed, ref failed, builder);
        Check(dashboard.periodReport.revenueCents == 10000L,
            "Dashboard clone aísla histórico",
            ref passed, ref failed, builder);
        Check(dashboard.liquidity.status == BistroBuilderLiquidityStatus.Healthy,
            "Dashboard clone aísla liquidez",
            ref passed, ref failed, builder);
        Check(dashboard.recentMovements[0].amountCents == 500L,
            "Dashboard clone aísla movimientos",
            ref passed, ref failed, builder);
        Check(dashboard.financingOffers[0].principalCents == 500000L,
            "Dashboard clone aísla ofertas",
            ref passed, ref failed, builder);
        Check(dashboard.loans[0].installments[0].principalCents == 100000L,
            "Dashboard clone aísla cuotas de deuda",
            ref passed, ref failed, builder);
        Check(dashboard.HasCompleteLiquidityProjection,
            "Read-model expone completitud de liquidez",
            ref passed, ref failed, builder);
    }

    private static void RunChartTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject root = new GameObject(
            "Finance3JChartSelfTest",
            typeof(RectTransform));
        try
        {
            BistroBuilderFinanceHistoryChartGraphic chart =
                root.AddComponent<BistroBuilderFinanceHistoryChartGraphic>();
            var days = new List<BistroBuilderDayFinancialResult>(365);
            for (int index = 0; index < 365; index++)
            {
                days.Add(new BistroBuilderDayFinancialResult
                {
                    dayIndex = index + 1,
                    revenueCents = index * 100L,
                    operatingResultCents = index % 3 == 0
                        ? -index * 10L
                        : index * 12L,
                    netCashChangeCents = index % 2 == 0
                        ? index * 5L
                        : -index * 4L
                });
            }

            chart.Bind(days, BistroBuilderFinanceChartMetric.Revenue);
            Check(chart.SourcePointCount == 365,
                "Gráfico conserva número de días fuente",
                ref passed, ref failed, builder);
            Check(chart.RenderedPointCount > 0 && chart.RenderedPointCount <= 180,
                "Gráfico limita buckets para históricos largos",
                ref passed, ref failed, builder);
            Check(chart.Metric == BistroBuilderFinanceChartMetric.Revenue,
                "Gráfico enlaza ingresos",
                ref passed, ref failed, builder);

            chart.Bind(days, BistroBuilderFinanceChartMetric.OperatingResult);
            Check(chart.Metric == BistroBuilderFinanceChartMetric.OperatingResult &&
                  chart.RenderedPointCount <= 180,
                "Gráfico soporta resultado firmado",
                ref passed, ref failed, builder);

            chart.Bind(days, BistroBuilderFinanceChartMetric.NetCash);
            Check(chart.Metric == BistroBuilderFinanceChartMetric.NetCash &&
                  chart.RenderedPointCount <= 180,
                "Gráfico soporta caja firmada",
                ref passed, ref failed, builder);

            chart.Clear();
            Check(chart.SourcePointCount == 0 && chart.RenderedPointCount == 0,
                "Gráfico se limpia sin estado residual",
                ref passed, ref failed, builder);
        }
        catch (Exception exception)
        {
            Check(false,
                "Gráfico 3J no debe lanzar excepción: " + exception.Message,
                ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void RunContractTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        string tokenA = BistroBuilderFinanceDashboardService.CreateAcceptanceToken();
        string tokenB = BistroBuilderFinanceDashboardService.CreateAcceptanceToken();
        Check(!string.IsNullOrWhiteSpace(tokenA) && tokenA.Length == 32,
            "Token de confirmación tiene identidad suficiente",
            ref passed, ref failed, builder);
        Check(!string.Equals(tokenA, tokenB, StringComparison.Ordinal),
            "Confirmaciones nuevas reciben identidad distinta",
            ref passed, ref failed, builder);

        Check(Enum.GetValues(typeof(BistroBuilderFinancePlayerSection)).Length == 5,
            "3J publica exactamente cinco secciones jugables",
            ref passed, ref failed, builder);
        Check(Enum.GetValues(typeof(BistroBuilderFinanceDashboardPeriod)).Length == 4,
            "3J publica cuatro ventanas históricas",
            ref passed, ref failed, builder);
        Check(Enum.GetValues(typeof(BistroBuilderFinanceChartMetric)).Length == 3,
            "3J publica tres métricas de gráfico",
            ref passed, ref failed, builder);

        Check(typeof(IBistroBuilderSaveSectionProvider).IsAssignableFrom(
                  typeof(BistroBuilderFinanceDashboardService)) == false,
            "Dashboard 3J no implementa persistencia Save",
            ref passed, ref failed, builder);
        Check(typeof(IBistroBuilderSaveSectionProvider).IsAssignableFrom(
                  typeof(BistroBuilderFinanceRuntimeView)) == false,
            "Vista 3J no implementa persistencia Save",
            ref passed, ref failed, builder);
        Check(BistroBuilderFinanceRuntimeView.RuntimeRevision == "FINANCE-3J-UI-V1",
            "Contrato de revisión runtime estable",
            ref passed, ref failed, builder);
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
            builder.AppendLine("[FALLO] " + label);
        }
    }
}
