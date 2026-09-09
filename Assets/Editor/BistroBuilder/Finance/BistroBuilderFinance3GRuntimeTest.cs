using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderFinance3GRuntimeTest
{
    private const string ArmedKey = "BB.Finance.3G.Runtime.Armed";
    private const string ResultKey = "BB.Finance.3G.Runtime.Result";
    private const double StartupTimeoutSeconds = 20d;

    private static double startupDeadline;
    private static int capturedErrors;

    private static BistroBuilderFinanceService finance;
    private static BistroBuilderProductCostService productCost;
    private static BistroBuilderFinancialResultsService results;
    private static BistroBuilderGeneralGameStateService generalState;
    private static BistroBuilderMenuOfferService menuOffer;
    private static RestaurantServiceStateService serviceState;

    private static BistroBuilderFinanceSnapshot baselineFinance;
    private static BistroBuilderProductCostSnapshot baselineProductCost;

    static BistroBuilderFinance3GRuntimeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3G - Prueba runtime real",
        false,
        3063)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3G",
                "La prueba runtime 3G ya está en ejecución.",
                "Aceptar");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3G",
                "Sal de Play Mode antes de iniciar la prueba automática.",
                "Aceptar");
            return;
        }

        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3G",
                "No se pudieron guardar las escenas antes de la prueba.",
                "Aceptar");
            return;
        }

        SessionState.SetBool(ArmedKey, true);
        SessionState.EraseString(ResultKey);
        capturedErrors = 0;
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
                    "Prueba runtime 3G cancelada antes de completarse.");
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
            "Bistro Builder — 3G",
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
        generalState = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderGeneralGameStateService>();
        menuOffer = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderMenuOfferService>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<
            RestaurantServiceStateService>();

        bool ready =
            finance != null && finance.IsInitialized &&
            productCost != null && productCost.IsInitialized &&
            results != null &&
            generalState != null &&
            menuOffer != null &&
            serviceState != null;

        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >= startupDeadline)
            {
                Fail("3G no encontró todas sus autoridades runtime inicializadas.");
            }
            return;
        }

        EditorApplication.update -= TryRunWhenReady;

        if (!serviceState.IsClosed)
        {
            Fail("La prueba 3G necesita comenzar con el restaurante Closed.");
            return;
        }

        if (!results.ValidateConfiguration(out string configurationError))
        {
            Fail("La configuración runtime de 3G no es válida. " + configurationError);
            return;
        }

        BistroBuilderMealServiceAvailability mealService =
            menuOffer.CurrentMealService;
        if (!BistroBuilderFinancialResultsEngine.IsConcreteMealService(mealService))
        {
            Fail("La escena no expone Breakfast, Lunch o Dinner como servicio actual.");
            return;
        }

        Execute(mealService);
    }

    private static void Execute(
        BistroBuilderMealServiceAvailability mealService)
    {
        baselineFinance = finance.CreateSnapshot();
        baselineProductCost = productCost.CreateSnapshot();

        if (baselineFinance == null || baselineProductCost == null)
        {
            Fail("No se pudieron capturar los snapshots iniciales de 3A/3D.");
            return;
        }

        int dayIndex = generalState.DayIndex;
        string error = string.Empty;

        if (!results.TryGetServiceResult(
                dayIndex,
                mealService,
                out BistroBuilderServiceFinancialResult beforeService,
                out error) ||
            !results.TryGetDayResult(
                dayIndex,
                out BistroBuilderDayFinancialResult beforeDay,
                out error))
        {
            Fail("3G no pudo leer el estado inicial. " + error);
            return;
        }

        BistroBuilderMealServiceAvailability otherMeal =
            mealService == BistroBuilderMealServiceAvailability.Breakfast
                ? BistroBuilderMealServiceAvailability.Dinner
                : BistroBuilderMealServiceAvailability.Breakfast;
        if (!results.TryGetServiceResult(
                dayIndex,
                otherMeal,
                out BistroBuilderServiceFinancialResult beforeOther,
                out error))
        {
            Fail("3G no pudo leer el servicio de control. " + error);
            return;
        }

        long startBalance = finance.CurrentBalanceCents;
        int startTransactionCount = finance.TransactionCount;
        int startLineCostCount = productCost.ConsumedLineCostCount;

        string token = Guid.NewGuid().ToString("N").ToLowerInvariant();
        string tableOrderId = "order_3g_table_" + token;
        string barOrderId = "order_3g_bar_" + token;

        var requests = new List<BistroBuilderFinanceTransactionRequest>(9);
        if (!TryAddSaleRequest(
                requests,
                tableOrderId,
                BistroBuilderServiceMode.TableService,
                mealService,
                10000L,
                dayIndex,
                out error) ||
            !TryAddSaleRequest(
                requests,
                barOrderId,
                BistroBuilderServiceMode.BarService,
                mealService,
                5000L,
                dayIndex,
                out error))
        {
            Fail("No se pudieron preparar los cobros diagnósticos. " + error);
            return;
        }

        // Este pago de proveedor es deliberadamente sintético: no crea un PO
        // falso dentro de 2.3E. 3G debe conservarlo como caja y marcar que el
        // desglose de portes no puede resolverse, nunca inventarlo.
        requests.Add(Request(
            "3g_supplier_" + token,
            BistroBuilderSupplierPurchaseFinancePolicy.SourceSystemId,
            "po_3g_diagnostic_" + token,
            BistroBuilderSupplierPurchaseFinancePolicy.CategoryId,
            BistroBuilderFinanceTransactionKind.Debit,
            7000L,
            dayIndex));
        requests.Add(Request(
            "3g_operating_" + token,
            BistroBuilderOperatingExpensePolicy.OperatingSourceSystemId,
            "utilities_3g_" + token,
            "expense.operating.utilities",
            BistroBuilderFinanceTransactionKind.Debit,
            1000L,
            dayIndex));
        requests.Add(Request(
            "3g_payroll_" + token,
            BistroBuilderOperatingExpensePolicy.PayrollSourceSystemId,
            "payroll_3g_" + token,
            BistroBuilderOperatingExpensePolicy.PayrollCategoryId,
            BistroBuilderFinanceTransactionKind.Debit,
            2000L,
            dayIndex));
        requests.Add(Request(
            "3g_marketing_" + token,
            "marketing",
            "campaign_3g_" + token,
            "expense.marketing.local",
            BistroBuilderFinanceTransactionKind.Debit,
            500L,
            dayIndex));
        requests.Add(Request(
            "3g_investment_" + token,
            "placeable.finance",
            "asset_3g_" + token,
            "investment.furniture",
            BistroBuilderFinanceTransactionKind.Debit,
            4000L,
            dayIndex));
        requests.Add(Request(
            "3g_resale_" + token,
            "placeable.finance",
            "asset_3g_" + token,
            "income.asset_resale",
            BistroBuilderFinanceTransactionKind.Credit,
            1000L,
            dayIndex));
        requests.Add(Request(
            "3g_demolition_" + token,
            "placeable.finance",
            "wall_3g_" + token,
            "expense.demolition",
            BistroBuilderFinanceTransactionKind.Debit,
            300L,
            dayIndex));

        if (!finance.TryPostTransactions(requests, out _, out error))
        {
            Fail("No se pudo aplicar el ledger diagnóstico de 3G. " + error);
            return;
        }

        BistroBuilderProductCostSnapshot candidate =
            baselineProductCost.DeepClone();
        AppendCostLine(
            candidate,
            tableOrderId,
            "line_3g_table_" + token,
            "dish_3g_runtime_table",
            mealService,
            BistroBuilderServiceMode.TableService,
            dayIndex,
            10000,
            2500L,
            3000L,
            BistroBuilderProductCostQuality.Actual);
        AppendCostLine(
            candidate,
            barOrderId,
            "line_3g_bar_" + token,
            "dish_3g_runtime_bar",
            mealService,
            BistroBuilderServiceMode.BarService,
            dayIndex,
            5000,
            1000L,
            1000L,
            BistroBuilderProductCostQuality.Mixed);

        if (!productCost.TryRestoreSnapshot(candidate, out error))
        {
            Fail("No se pudo aplicar el COGS diagnóstico de 3G. " + error);
            return;
        }

        if (!results.TryGetServiceResult(
                dayIndex,
                mealService,
                out BistroBuilderServiceFinancialResult afterService,
                out error) ||
            !results.TryGetDayResult(
                dayIndex,
                out BistroBuilderDayFinancialResult afterDay,
                out error) ||
            !results.TryGetServiceResult(
                dayIndex,
                otherMeal,
                out BistroBuilderServiceFinancialResult afterOther,
                out error))
        {
            Fail("3G no pudo proyectar el estado diagnóstico. " + error);
            return;
        }

        if (afterService.revenueCents - beforeService.revenueCents != 15000L ||
            afterService.tableRevenueCents - beforeService.tableRevenueCents != 10000L ||
            afterService.barRevenueCents - beforeService.barRevenueCents != 5000L ||
            afterService.paidOrderCount - beforeService.paidOrderCount != 2)
        {
            Fail("El resultado de servicio no agregó los dos cobros diagnósticos.");
            return;
        }

        if (afterService.productCostCents - beforeService.productCostCents != 4000L ||
            afterService.theoreticalProductCostCents -
                beforeService.theoreticalProductCostCents != 3500L ||
            afterService.costedSalesCents - beforeService.costedSalesCents != 15000L ||
            afterService.consumedLineCount - beforeService.consumedLineCount != 2 ||
            afterService.actualLineCount - beforeService.actualLineCount != 1 ||
            afterService.mixedLineCount - beforeService.mixedLineCount != 1 ||
            afterService.costCoverageGapCents != beforeService.costCoverageGapCents)
        {
            Fail("El resultado de servicio no agregó COGS/calidad/cobertura correctamente.");
            return;
        }

        if (afterService.grossProfitCents - beforeService.grossProfitCents != 11000L)
        {
            Fail("El margen bruto del servicio no aumentó exactamente 110,00 €.");
            return;
        }

        if (afterDay.revenueCents - beforeDay.revenueCents != 15000L ||
            afterDay.productCostCents - beforeDay.productCostCents != 4000L ||
            afterDay.grossProfitCents - beforeDay.grossProfitCents != 11000L ||
            afterDay.totalPeriodExpensesCents - beforeDay.totalPeriodExpensesCents != 3800L ||
            afterDay.operatingResultCents - beforeDay.operatingResultCents != 7200L)
        {
            Fail("El resultado diario no separó margen bruto y gastos de periodo.");
            return;
        }

        if (afterDay.supplierPurchaseCashOutCents -
                beforeDay.supplierPurchaseCashOutCents != 7000L ||
            afterDay.procurementShippingExpensesCents !=
                beforeDay.procurementShippingExpensesCents ||
            afterDay.supplierPaymentBreakdownMissingCount -
                beforeDay.supplierPaymentBreakdownMissingCount != 1 ||
            afterDay.investmentCashOutCents - beforeDay.investmentCashOutCents != 4000L ||
            afterDay.assetResaleCashInCents - beforeDay.assetResaleCashInCents != 1000L ||
            afterDay.netCashChangeCents - beforeDay.netCashChangeCents != 1200L)
        {
            Fail("3G mezcló caja/proveedores o inventó un desglose de portes para el PO diagnóstico.");
            return;
        }

        if (!EquivalentService(beforeOther, afterOther))
        {
            Fail("La actividad diagnóstica contaminó otro servicio del día.");
            return;
        }

        if (finance.CurrentBalanceCents - startBalance != 1200L ||
            finance.TransactionCount - startTransactionCount != 9 ||
            productCost.ConsumedLineCostCount - startLineCostCount != 2)
        {
            Fail("Las autoridades diagnósticas no contienen exactamente 9 movimientos y 2 COGS.");
            return;
        }

        long observedBalance = finance.CurrentBalanceCents;

        if (!RestoreBaseline(out error))
        {
            Fail("La prueba fue correcta pero no pudo restaurar 3A/3D. " + error);
            return;
        }

        if (!results.TryGetServiceResult(
                dayIndex,
                mealService,
                out BistroBuilderServiceFinancialResult restoredService,
                out error) ||
            !results.TryGetDayResult(
                dayIndex,
                out BistroBuilderDayFinancialResult restoredDay,
                out error) ||
            !EquivalentService(beforeService, restoredService) ||
            !EquivalentDay(beforeDay, restoredDay) ||
            finance.CurrentBalanceCents != startBalance ||
            finance.TransactionCount != startTransactionCount ||
            productCost.ConsumedLineCostCount != startLineCostCount)
        {
            Fail("El estado inicial no quedó idéntico tras restaurar la prueba. " + error);
            return;
        }

        if (capturedErrors != 0)
        {
            Fail("La prueba registró Error/Exception/Assert: " + capturedErrors + ".");
            return;
        }

        Complete(
            "PRUEBA RUNTIME 3G SUPERADA\n\n" +
            "Día: " + dayIndex + " / Servicio: " + mealService +
            "\nIngresos diagnósticos: +150,00 €" +
            "\nCOGS reconocido: 40,00 €" +
            "\nMargen bruto añadido: 110,00 €" +
            "\nGastos de periodo: 38,00 €" +
            "\nResultado operativo añadido: 72,00 €" +
            "\nCompra proveedor: 70,00 € (solo caja, no segundo COGS)" +
            "\nDesglose portes PO diagnóstica: marcado como no resoluble, sin inventar coste" +
            "\nInversión: 40,00 € (solo caja)" +
            "\nReventa activo: +10,00 € (caja separada)" +
            "\nVariación neta de caja: +12,00 €" +
            "\nCaja diagnóstica: " + FormatMoney(startBalance) +
            " → " + FormatMoney(observedBalance) +
            "\nCobertura de coste del servicio: conservada al 100 %" +
            "\nOtro servicio del día: sin contaminación" +
            "\nEstado inicial restaurado: OK" +
            "\nError/Exception/Assert: 0");
    }

    private static bool TryAddSaleRequest(
        List<BistroBuilderFinanceTransactionRequest> requests,
        string orderId,
        BistroBuilderServiceMode serviceMode,
        BistroBuilderMealServiceAvailability mealService,
        long amountCents,
        int dayIndex,
        out string error)
    {
        if (!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                orderId,
                serviceMode,
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
            description = "3G runtime diagnostic"
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
        long theoreticalCostCents,
        long actualCostCents,
        BistroBuilderProductCostQuality quality)
    {
        long sequence = snapshot.nextLineCostSequence;
        long microPerCent = BistroBuilderIngredientDefinition.MicroCentsPerCent;
        long theoreticalMicro = checked(theoreticalCostCents * microPerCent);
        long actualMicro = checked(actualCostCents * microPerCent);
        long theoreticalMargin = salePriceCents - theoreticalCostCents;
        long actualMargin = salePriceCents - actualCostCents;
        int theoreticalBp =
            BistroBuilderProductCostEngine.CalculateMarginBasisPoints(
                salePriceCents, theoreticalCostCents);
        int actualBp =
            BistroBuilderProductCostEngine.CalculateMarginBasisPoints(
                salePriceCents, actualCostCents);

        snapshot.consumedLineCosts.Add(
            new BistroBuilderConsumedLineCostRecord
            {
                sequence = sequence,
                costRecordId = BistroBuilderProductCostEngine.BuildCostRecordId(sequence),
                orderId = orderId,
                lineId = lineId,
                dishId = dishId,
                mealService = mealService,
                serviceMode = serviceMode,
                dayIndex = dayIndex,
                minuteOfDay = 720,
                salePriceCents = salePriceCents,
                theoreticalCostMicroCents = theoreticalMicro,
                theoreticalCostCents = theoreticalCostCents,
                actualCostMicroCents = actualMicro,
                actualCostCents = actualCostCents,
                theoreticalMarginCents = theoreticalMargin,
                theoreticalMarginBasisPoints = theoreticalBp,
                theoreticalMarginBand =
                    BistroBuilderProductCostEngine.ResolveMarginBand(
                        theoreticalMargin, theoreticalBp),
                actualMarginCents = actualMargin,
                actualMarginBasisPoints = actualBp,
                actualMarginBand =
                    BistroBuilderProductCostEngine.ResolveMarginBand(
                        actualMargin, actualBp),
                costQuality = quality
            });

        snapshot.nextLineCostSequence = checked(sequence + 1L);
        snapshot.revision = checked(snapshot.revision + 1L);
    }

    private static bool EquivalentService(
        BistroBuilderServiceFinancialResult a,
        BistroBuilderServiceFinancialResult b)
    {
        return a != null && b != null &&
               a.dayIndex == b.dayIndex &&
               a.mealService == b.mealService &&
               a.revenueCents == b.revenueCents &&
               a.tableRevenueCents == b.tableRevenueCents &&
               a.barRevenueCents == b.barRevenueCents &&
               a.paidOrderCount == b.paidOrderCount &&
               a.costedSalesCents == b.costedSalesCents &&
               a.productCostCents == b.productCostCents &&
               a.theoreticalProductCostCents == b.theoreticalProductCostCents &&
               a.consumedLineCount == b.consumedLineCount &&
               a.estimatedLineCount == b.estimatedLineCount &&
               a.mixedLineCount == b.mixedLineCount &&
               a.actualLineCount == b.actualLineCount &&
               a.costQuality == b.costQuality &&
               a.costCoverageGapCents == b.costCoverageGapCents &&
               a.grossProfitCents == b.grossProfitCents &&
               a.grossMarginBasisPoints == b.grossMarginBasisPoints;
    }

    private static bool EquivalentDay(
        BistroBuilderDayFinancialResult a,
        BistroBuilderDayFinancialResult b)
    {
        return a != null && b != null &&
               a.dayIndex == b.dayIndex &&
               a.revenueCents == b.revenueCents &&
               a.costedSalesCents == b.costedSalesCents &&
               a.productCostCents == b.productCostCents &&
               a.theoreticalProductCostCents == b.theoreticalProductCostCents &&
               a.paidOrderCount == b.paidOrderCount &&
               a.consumedLineCount == b.consumedLineCount &&
               a.costQuality == b.costQuality &&
               a.costCoverageGapCents == b.costCoverageGapCents &&
               a.grossProfitCents == b.grossProfitCents &&
               a.grossMarginBasisPoints == b.grossMarginBasisPoints &&
               a.procurementShippingExpensesCents == b.procurementShippingExpensesCents &&
               a.supplierPaymentBreakdownMissingCount == b.supplierPaymentBreakdownMissingCount &&
               a.totalPeriodExpensesCents == b.totalPeriodExpensesCents &&
               a.operatingResultCents == b.operatingResultCents &&
               a.supplierPurchaseCashOutCents == b.supplierPurchaseCashOutCents &&
               a.investmentCashOutCents == b.investmentCashOutCents &&
               a.assetResaleCashInCents == b.assetResaleCashInCents &&
               a.totalCashInCents == b.totalCashInCents &&
               a.totalCashOutCents == b.totalCashOutCents &&
               a.netCashChangeCents == b.netCashChangeCents;
    }

    private static bool RestoreBaseline(out string error)
    {
        error = string.Empty;
        string productError = string.Empty;
        string financeError = string.Empty;

        bool productRestored =
            baselineProductCost != null && productCost != null;
        if (productRestored)
        {
            productRestored = productCost.TryRestoreSnapshot(
                baselineProductCost,
                out productError);
        }

        bool financeRestored = baselineFinance != null && finance != null;
        if (financeRestored)
        {
            financeRestored = finance.TryRestoreSnapshot(
                baselineFinance,
                out financeError);
        }

        if (productRestored && financeRestored)
        {
            return true;
        }

        error =
            (productRestored ? string.Empty : "3D: " + productError + " ") +
            (financeRestored ? string.Empty : "3A: " + financeError);
        return false;
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
            "PRUEBA RUNTIME 3G FALLIDA\n\n" + message +
            "\n\nError/Exception/Assert observados: " + capturedErrors);
    }

    private static void Complete(string message)
    {
        CleanupSubscriptions();
        SessionState.SetString(ResultKey, message);
        SessionState.SetBool(ArmedKey, false);
        baselineFinance = null;
        baselineProductCost = null;
        EditorApplication.isPlaying = false;
    }

    private static void CleanupSubscriptions()
    {
        EditorApplication.update -= TryRunWhenReady;
        Application.logMessageReceived -= HandleLog;
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("N2") + " €";
    }
}
