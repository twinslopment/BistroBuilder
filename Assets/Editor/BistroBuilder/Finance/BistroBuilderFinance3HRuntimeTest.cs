using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderFinance3HRuntimeTest
{
    private const string ArmedKey = "BB.Finance.3H.Runtime.Armed";
    private const string ResultKey = "BB.Finance.3H.Runtime.Result";
    private const double StartupTimeoutSeconds = 20d;

    private static double startupDeadline;
    private static int capturedErrors;
    private static int historyChangeCount;

    private static BistroBuilderFinanceService finance;
    private static BistroBuilderProductCostService productCost;
    private static BistroBuilderFinancialResultsService results;
    private static BistroBuilderFinancialHistoryService history;
    private static BistroBuilderGeneralGameStateService generalState;
    private static RestaurantServiceStateService serviceState;

    private static BistroBuilderFinanceSnapshot baselineFinance;
    private static BistroBuilderProductCostSnapshot baselineProductCost;
    private static BistroBuilderDayFinancialResult baselineCurrentDay;

    private static string baselineGameId;
    private static string baselineRestaurantName;
    private static string baselineCreatedUtc;
    private static int baselineDayIndex;
    private static int baselineYear;
    private static int baselineMonth;
    private static int baselineDay;
    private static string baselineProgressionStage;
    private static int baselineProgressionLevel;

    static BistroBuilderFinance3HRuntimeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3H - Prueba runtime real",
        false,
        3073)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3H",
                "La prueba runtime 3H ya está en ejecución.",
                "Aceptar");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3H",
                "Sal de Play Mode antes de iniciar la prueba automática.",
                "Aceptar");
            return;
        }

        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3H",
                "No se pudieron guardar las escenas antes de la prueba.",
                "Aceptar");
            return;
        }

        SessionState.SetBool(ArmedKey, true);
        SessionState.EraseString(ResultKey);
        capturedErrors = 0;
        historyChangeCount = 0;
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            startupDeadline =
                EditorApplication.timeSinceStartup + StartupTimeoutSeconds;
            Application.logMessageReceived -= HandleLog;
            Application.logMessageReceived += HandleLog;
            EditorApplication.update -= TryRunWhenReady;
            EditorApplication.update += TryRunWhenReady;
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            CleanupSubscriptions();
            if (SessionState.GetBool(ArmedKey, false))
            {
                SessionState.SetBool(ArmedKey, false);
                SessionState.SetString(
                    ResultKey,
                    "Prueba runtime 3H cancelada antes de completarse.");
            }
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        string message = SessionState.GetString(ResultKey, string.Empty);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        SessionState.EraseString(ResultKey);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3H",
            message,
            "Aceptar");
    }

    private static void TryRunWhenReady()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false))
        {
            EditorApplication.update -= TryRunWhenReady;
            return;
        }

        finance = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderFinanceService>();
        productCost = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderProductCostService>();
        results = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderFinancialResultsService>();
        history = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderFinancialHistoryService>();
        generalState = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderGeneralGameStateService>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<
            RestaurantServiceStateService>();

        bool ready =
            finance != null && finance.IsInitialized &&
            productCost != null && productCost.IsInitialized &&
            results != null && history != null &&
            generalState != null && serviceState != null;

        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >= startupDeadline)
            {
                Fail("3H no encontró todas sus autoridades runtime inicializadas.");
            }
            return;
        }

        EditorApplication.update -= TryRunWhenReady;

        if (!serviceState.IsClosed)
        {
            Fail("La prueba 3H necesita comenzar con el restaurante Closed.");
            return;
        }

        string resultsError = string.Empty;
        string historyError = string.Empty;
        bool resultsValid = results.ValidateConfiguration(out resultsError);
        bool historyValid = history.ValidateConfiguration(out historyError);
        if (!resultsValid || !historyValid)
        {
            Fail(
                "La configuración runtime de 3G/3H no es válida. " +
                resultsError + " " + historyError);
            return;
        }

        Execute();
    }

    private static void Execute()
    {
        CaptureBaseline();
        if (baselineFinance == null || baselineProductCost == null ||
            baselineCurrentDay == null)
        {
            Fail("No se pudieron capturar los snapshots iniciales de 3A/3D/3G.");
            return;
        }

        if (baselineDayIndex > int.MaxValue - 4)
        {
            Fail("El DayIndex actual no deja espacio para tres días diagnósticos.");
            return;
        }

        int firstDay = baselineDayIndex + 1;
        int secondDay = baselineDayIndex + 2;
        int thirdDay = baselineDayIndex + 3;

        history.HistoryChanged -= HandleHistoryChanged;
        history.HistoryChanged += HandleHistoryChanged;

        // Ampliamos silenciosamente el límite de consulta histórica. No usamos
        // TrySetCalendar porque CalendarChanged podría disparar gastos u otros
        // sistemas de gameplay que reaccionan al avance real de día.
        if (!generalState.TryRestoreState(
                baselineGameId,
                baselineRestaurantName,
                baselineCreatedUtc,
                thirdDay,
                baselineYear,
                baselineMonth,
                baselineDay,
                baselineProgressionStage,
                baselineProgressionLevel,
                false))
        {
            Fail("No se pudo abrir la ventana temporal diagnóstica de 3H.");
            return;
        }

        if (!history.TryGetPeriodReport(
                firstDay,
                thirdDay,
                out BistroBuilderFinancialPeriodReport before,
                out string error))
        {
            Fail("3H no pudo leer el periodo diagnóstico vacío. " + error);
            return;
        }

        if (before.activeDayCount != 0 ||
            before.revenueCents != 0L ||
            before.operatingResultCents != 0L)
        {
            Fail("La partida contiene actividad futura antes de la prueba 3H.");
            return;
        }

        string token = Guid.NewGuid().ToString("N").ToLowerInvariant();
        var requests = new List<BistroBuilderFinanceTransactionRequest>(8);

        if (!AddSale(
                requests,
                "order_3h_lunch_table_" + token,
                BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Lunch,
                10000L,
                firstDay,
                out error) ||
            !AddSale(
                requests,
                "order_3h_lunch_bar_" + token,
                BistroBuilderServiceMode.BarService,
                BistroBuilderMealServiceAvailability.Lunch,
                5000L,
                firstDay,
                out error) ||
            !AddSale(
                requests,
                "order_3h_breakfast_" + token,
                BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Breakfast,
                8000L,
                secondDay,
                out error) ||
            !AddSale(
                requests,
                "order_3h_dinner_" + token,
                BistroBuilderServiceMode.BarService,
                BistroBuilderMealServiceAvailability.Dinner,
                20000L,
                thirdDay,
                out error))
        {
            Fail("No se pudieron preparar los cobros diagnósticos. " + error);
            return;
        }

        requests.Add(Request(
            "3h_operating_" + token,
            BistroBuilderOperatingExpensePolicy.OperatingSourceSystemId,
            "utilities_3h_" + token,
            "expense.operating.utilities",
            BistroBuilderFinanceTransactionKind.Debit,
            3000L,
            firstDay));
        requests.Add(Request(
            "3h_payroll_" + token,
            BistroBuilderOperatingExpensePolicy.PayrollSourceSystemId,
            "payroll_3h_" + token,
            BistroBuilderOperatingExpensePolicy.PayrollCategoryId,
            BistroBuilderFinanceTransactionKind.Debit,
            6000L,
            secondDay));
        requests.Add(Request(
            "3h_marketing_" + token,
            "marketing",
            "campaign_3h_" + token,
            "expense.marketing.local",
            BistroBuilderFinanceTransactionKind.Debit,
            4000L,
            thirdDay));
        requests.Add(Request(
            "3h_investment_" + token,
            "placeable_economy",
            "asset_3h_" + token,
            "investment.furniture",
            BistroBuilderFinanceTransactionKind.Debit,
            5000L,
            thirdDay));

        long startBalance = finance.CurrentBalanceCents;
        int startTransactions = finance.TransactionCount;
        int startCostLines = productCost.ConsumedLineCostCount;

        if (!finance.TryPostTransactions(requests, out _, out error))
        {
            Fail("No se pudo aplicar el ledger diagnóstico de 3H. " + error);
            return;
        }

        BistroBuilderProductCostSnapshot candidate =
            baselineProductCost.DeepClone();
        AppendCostLine(
            candidate,
            "order_3h_lunch_table_" + token,
            "line_3h_lunch_table_" + token,
            "dish_3h_lunch_table",
            BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderServiceMode.TableService,
            firstDay,
            10000,
            2500L);
        AppendCostLine(
            candidate,
            "order_3h_lunch_bar_" + token,
            "line_3h_lunch_bar_" + token,
            "dish_3h_lunch_bar",
            BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderServiceMode.BarService,
            firstDay,
            5000,
            1500L);
        AppendCostLine(
            candidate,
            "order_3h_breakfast_" + token,
            "line_3h_breakfast_" + token,
            "dish_3h_breakfast",
            BistroBuilderMealServiceAvailability.Breakfast,
            BistroBuilderServiceMode.TableService,
            secondDay,
            8000,
            3000L);
        AppendCostLine(
            candidate,
            "order_3h_dinner_" + token,
            "line_3h_dinner_" + token,
            "dish_3h_dinner",
            BistroBuilderMealServiceAvailability.Dinner,
            BistroBuilderServiceMode.BarService,
            thirdDay,
            20000,
            6000L);

        if (!productCost.TryRestoreSnapshot(candidate, out error))
        {
            Fail("No se pudo aplicar el COGS diagnóstico de 3H. " + error);
            return;
        }

        if (!history.TryGetPeriodReport(
                firstDay,
                thirdDay,
                out BistroBuilderFinancialPeriodReport period,
                out error))
        {
            Fail("3H no pudo construir el histórico real. " + error);
            return;
        }

        if (period.dayCount != 3 ||
            period.activeDayCount != 3 ||
            period.profitableDayCount != 2 ||
            period.lossDayCount != 1)
        {
            Fail("3H no clasificó correctamente los tres días diagnósticos.");
            return;
        }

        if (period.revenueCents != 43000L ||
            period.productCostCents != 13000L ||
            period.grossProfitCents != 30000L ||
            period.totalPeriodExpensesCents != 13000L ||
            period.operatingResultCents != 17000L)
        {
            Fail("Los acumulados económicos históricos de 3H no cuadran.");
            return;
        }

        if (period.grossMarginBasisPoints != 6977 ||
            period.operatingMarginBasisPoints != 3953 ||
            period.paidOrderCount != 4 ||
            period.averageTicketCents != 10750L ||
            period.averageDailyRevenueCents != 14333L ||
            period.averageDailyOperatingResultCents != 5667L)
        {
            Fail("Los indicadores KPI de 3H no cuadran.");
            return;
        }

        if (period.bestRevenueDayIndex != thirdDay ||
            period.bestRevenueCents != 20000L ||
            period.bestOperatingResultDayIndex != thirdDay ||
            period.bestOperatingResultCents != 10000L ||
            period.worstOperatingResultDayIndex != secondDay ||
            period.worstOperatingResultCents != -1000L)
        {
            Fail("3H no identificó correctamente mejores/peores jornadas.");
            return;
        }

        if (period.topRevenueMealService !=
                BistroBuilderMealServiceAvailability.Dinner ||
            period.topGrossProfitMealService !=
                BistroBuilderMealServiceAvailability.Dinner)
        {
            Fail("3H no identificó Dinner como franja líder del periodo.");
            return;
        }

        if (!period.IsCostCoverageComplete ||
            !period.HasCompleteSupplierPaymentBreakdown ||
            period.costQuality != BistroBuilderFinancialResultCostQuality.Actual)
        {
            Fail("Los indicadores de calidad/cobertura de 3H no son correctos.");
            return;
        }

        if (period.totalCashInCents != 43000L ||
            period.totalCashOutCents != 18000L ||
            period.netCashChangeCents != 25000L ||
            period.investmentCashOutCents != 5000L)
        {
            Fail("3H mezcló resultado operativo y flujo de caja histórico.");
            return;
        }

        if (!history.TryGetCurrentRollingReport(
                3,
                out BistroBuilderFinancialPeriodReport rolling,
                out error) ||
            !EquivalentPeriod(period, rolling))
        {
            Fail("La ventana móvil de tres días no coincide con el histórico. " + error);
            return;
        }

        if (!history.TryCompareWithPreviousPeriod(
                thirdDay,
                thirdDay,
                out BistroBuilderFinancialPeriodComparison comparison,
                out error))
        {
            Fail("3H no pudo comparar el último día con el anterior. " + error);
            return;
        }

        if (comparison.revenueDeltaCents != 12000L ||
            comparison.revenueTrend != BistroBuilderFinancialTrendDirection.Up ||
            comparison.operatingResultDeltaCents != 11000L ||
            comparison.operatingResultTrend != BistroBuilderFinancialTrendDirection.Up ||
            comparison.averageTicketDeltaCents != 12000L ||
            comparison.averageTicketTrend != BistroBuilderFinancialTrendDirection.Up ||
            comparison.netCashChangeDeltaCents != 9000L ||
            comparison.netCashTrend != BistroBuilderFinancialTrendDirection.Up)
        {
            Fail("La comparativa histórica día contra día no es correcta.");
            return;
        }

        bool futureRejected = !history.TryGetPeriodReport(
            thirdDay,
            thirdDay + 1,
            out _,
            out _);
        if (!futureRejected)
        {
            Fail("3H aceptó un informe que incluye un día futuro.");
            return;
        }

        if (finance.TransactionCount - startTransactions != 8 ||
            productCost.ConsumedLineCostCount - startCostLines != 4 ||
            finance.CurrentBalanceCents - startBalance != 25000L ||
            historyChangeCount < 1)
        {
            Fail("Las autoridades o la reactividad diagnóstica de 3H no cuadran.");
            return;
        }

        long observedBalance = finance.CurrentBalanceCents;
        int observedHistoryChanges = historyChangeCount;

        if (!RestoreBaseline(out error))
        {
            Fail("La prueba fue correcta pero no pudo restaurar el estado. " + error);
            return;
        }

        if (!results.TryGetDayResult(
                baselineDayIndex,
                out BistroBuilderDayFinancialResult restoredCurrentDay,
                out error) ||
            !EquivalentDay(baselineCurrentDay, restoredCurrentDay) ||
            finance.CurrentBalanceCents != baselineFinance.currentBalanceCents ||
            finance.TransactionCount != baselineFinance.transactions.Count ||
            productCost.ConsumedLineCostCount !=
                baselineProductCost.consumedLineCosts.Count ||
            generalState.DayIndex != baselineDayIndex ||
            generalState.CalendarYear != baselineYear ||
            generalState.CalendarMonth != baselineMonth ||
            generalState.CalendarDay != baselineDay)
        {
            Fail("El estado inicial no quedó idéntico tras restaurar 3H. " + error);
            return;
        }

        if (capturedErrors != 0)
        {
            Fail("La prueba registró Error/Exception/Assert: " + capturedErrors + ".");
            return;
        }

        Complete(
            "PRUEBA RUNTIME 3H SUPERADA\n\n" +
            "Histórico: 3 días / 3 activos" +
            "\nIngresos acumulados: 430,00 €" +
            "\nCOGS acumulado: 130,00 €" +
            "\nMargen bruto: 300,00 € (69,77 %)" +
            "\nGastos del periodo: 130,00 €" +
            "\nResultado operativo: 170,00 € (39,53 %)" +
            "\nTicket medio: 107,50 €" +
            "\nDías rentables/pérdida: 2 / 1" +
            "\nMejor jornada: día " + thirdDay +
            "\nPeor jornada: día " + secondDay +
            "\nFranja líder: Dinner" +
            "\nCaja neta del periodo: +250,00 €" +
            "\nVentana móvil 3 días: OK" +
            "\nComparativa último día vs anterior: Up" +
            "\nDías futuros bloqueados: OK" +
            "\nEventos de actualización observados: " + observedHistoryChanges +
            "\nCaja diagnóstica: " + FormatMoney(startBalance) +
            " → " + FormatMoney(observedBalance) +
            "\nEstado inicial restaurado: OK" +
            "\nError/Exception/Assert: 0");
    }

    private static void CaptureBaseline()
    {
        baselineFinance = finance.CreateSnapshot();
        baselineProductCost = productCost.CreateSnapshot();

        baselineGameId = generalState.GameId;
        baselineRestaurantName = generalState.RestaurantName;
        baselineCreatedUtc = generalState.CreatedUtc;
        baselineDayIndex = generalState.DayIndex;
        baselineYear = generalState.CalendarYear;
        baselineMonth = generalState.CalendarMonth;
        baselineDay = generalState.CalendarDay;
        baselineProgressionStage = generalState.ProgressionStageId;
        baselineProgressionLevel = generalState.ProgressionLevel;

        baselineCurrentDay = null;
        results.TryGetDayResult(
            baselineDayIndex,
            out baselineCurrentDay,
            out _);
    }

    private static bool AddSale(
        List<BistroBuilderFinanceTransactionRequest> requests,
        string orderId,
        BistroBuilderServiceMode mode,
        BistroBuilderMealServiceAvailability mealService,
        long amountCents,
        int dayIndex,
        out string error)
    {
        if (!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                orderId,
                mode,
                mealService,
                amountCents,
                dayIndex,
                720,
                out BistroBuilderFinanceTransactionRequest request,
                out error))
        {
            return false;
        }

        requests.Add(request);
        return true;
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
            minuteOfDay = 720,
            description = "3H runtime diagnostic"
        };
    }

    private static void AppendCostLine(
        BistroBuilderProductCostSnapshot snapshot,
        string orderId,
        string lineId,
        string dishId,
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        int dayIndex,
        int salePriceCents,
        long actualCostCents)
    {
        long sequence = snapshot.nextLineCostSequence;
        long microPerCent = BistroBuilderIngredientDefinition.MicroCentsPerCent;
        long actualMicro = checked(actualCostCents * microPerCent);
        long margin = salePriceCents - actualCostCents;
        int marginBasisPoints =
            BistroBuilderProductCostEngine.CalculateMarginBasisPoints(
                salePriceCents,
                actualCostCents);

        snapshot.consumedLineCosts.Add(
            new BistroBuilderConsumedLineCostRecord
            {
                sequence = sequence,
                costRecordId =
                    BistroBuilderProductCostEngine.BuildCostRecordId(sequence),
                orderId = orderId,
                lineId = lineId,
                dishId = dishId,
                mealService = mealService,
                serviceMode = serviceMode,
                dayIndex = dayIndex,
                minuteOfDay = 720,
                salePriceCents = salePriceCents,
                theoreticalCostMicroCents = actualMicro,
                theoreticalCostCents = actualCostCents,
                actualCostMicroCents = actualMicro,
                actualCostCents = actualCostCents,
                theoreticalMarginCents = margin,
                theoreticalMarginBasisPoints = marginBasisPoints,
                theoreticalMarginBand =
                    BistroBuilderProductCostEngine.ResolveMarginBand(
                        margin,
                        marginBasisPoints),
                actualMarginCents = margin,
                actualMarginBasisPoints = marginBasisPoints,
                actualMarginBand =
                    BistroBuilderProductCostEngine.ResolveMarginBand(
                        margin,
                        marginBasisPoints),
                costQuality = BistroBuilderProductCostQuality.Actual
            });

        snapshot.nextLineCostSequence = checked(sequence + 1L);
        snapshot.revision = checked(snapshot.revision + 1L);
    }

    private static bool RestoreBaseline(out string error)
    {
        error = string.Empty;
        string productError = string.Empty;
        string financeError = string.Empty;
        bool productRestored = true;
        bool financeRestored = true;
        bool generalRestored = true;

        if (baselineProductCost != null && productCost != null)
        {
            productRestored = productCost.TryRestoreSnapshot(
                baselineProductCost,
                out productError);
        }

        if (baselineFinance != null && finance != null)
        {
            financeRestored = finance.TryRestoreSnapshot(
                baselineFinance,
                out financeError);
        }

        if (!string.IsNullOrWhiteSpace(baselineGameId) && generalState != null)
        {
            generalRestored = generalState.TryRestoreState(
                baselineGameId,
                baselineRestaurantName,
                baselineCreatedUtc,
                baselineDayIndex,
                baselineYear,
                baselineMonth,
                baselineDay,
                baselineProgressionStage,
                baselineProgressionLevel,
                false);
        }

        if (!productRestored || !financeRestored || !generalRestored)
        {
            error =
                (productRestored ? string.Empty : "3D: " + productError + " ") +
                (financeRestored ? string.Empty : "3A: " + financeError + " ") +
                (generalRestored ? string.Empty : "Calendario: restauración rechazada.");
            return false;
        }

        return true;
    }

    private static bool EquivalentPeriod(
        BistroBuilderFinancialPeriodReport a,
        BistroBuilderFinancialPeriodReport b)
    {
        return a != null && b != null &&
               a.startDayIndex == b.startDayIndex &&
               a.endDayIndex == b.endDayIndex &&
               a.dayCount == b.dayCount &&
               a.activeDayCount == b.activeDayCount &&
               a.revenueCents == b.revenueCents &&
               a.productCostCents == b.productCostCents &&
               a.grossProfitCents == b.grossProfitCents &&
               a.totalPeriodExpensesCents == b.totalPeriodExpensesCents &&
               a.operatingResultCents == b.operatingResultCents &&
               a.netCashChangeCents == b.netCashChangeCents &&
               a.averageTicketCents == b.averageTicketCents &&
               a.bestRevenueDayIndex == b.bestRevenueDayIndex &&
               a.worstOperatingResultDayIndex == b.worstOperatingResultDayIndex &&
               a.topRevenueMealService == b.topRevenueMealService;
    }

    private static bool EquivalentDay(
        BistroBuilderDayFinancialResult a,
        BistroBuilderDayFinancialResult b)
    {
        return a != null && b != null &&
               a.dayIndex == b.dayIndex &&
               a.revenueCents == b.revenueCents &&
               a.productCostCents == b.productCostCents &&
               a.grossProfitCents == b.grossProfitCents &&
               a.totalPeriodExpensesCents == b.totalPeriodExpensesCents &&
               a.operatingResultCents == b.operatingResultCents &&
               a.totalCashInCents == b.totalCashInCents &&
               a.totalCashOutCents == b.totalCashOutCents &&
               a.netCashChangeCents == b.netCashChangeCents &&
               a.paidOrderCount == b.paidOrderCount &&
               a.consumedLineCount == b.consumedLineCount &&
               a.costCoverageGapCents == b.costCoverageGapCents;
    }

    private static void HandleHistoryChanged()
    {
        historyChangeCount++;
    }

    private static void HandleLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type == LogType.Error ||
            type == LogType.Exception ||
            type == LogType.Assert)
        {
            capturedErrors++;
        }
    }

    private static void Fail(string message)
    {
        RestoreBaseline(out _);
        Complete(
            "PRUEBA RUNTIME 3H FALLIDA\n\n" + message +
            "\n\nError/Exception/Assert observados: " + capturedErrors);
    }

    private static void Complete(string message)
    {
        CleanupSubscriptions();
        SessionState.SetString(ResultKey, message);
        SessionState.SetBool(ArmedKey, false);
        ClearBaselines();
        EditorApplication.isPlaying = false;
    }

    private static void CleanupSubscriptions()
    {
        EditorApplication.update -= TryRunWhenReady;
        Application.logMessageReceived -= HandleLog;
        if (history != null)
        {
            history.HistoryChanged -= HandleHistoryChanged;
        }
    }

    private static void ClearBaselines()
    {
        baselineFinance = null;
        baselineProductCost = null;
        baselineCurrentDay = null;
        baselineGameId = string.Empty;
        baselineRestaurantName = string.Empty;
        baselineCreatedUtc = string.Empty;
        baselineProgressionStage = string.Empty;
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("N2") + " €";
    }
}
