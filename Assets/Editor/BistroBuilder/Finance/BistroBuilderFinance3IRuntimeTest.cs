using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderFinance3IRuntimeTest
{
    private const string ArmedKey = "BB.Finance.3I.Runtime.Armed";
    private const string ResultKey = "BB.Finance.3I.Runtime.Result";
    private const double StartupTimeoutSeconds = 20d;

    private static double startupDeadline;
    private static int capturedErrors;

    private static BistroBuilderFinanceService finance;
    private static BistroBuilderFinancingService financing;
    private static BistroBuilderFinancialResultsService results;
    private static BistroBuilderGeneralGameStateService generalState;
    private static RestaurantServiceStateService serviceState;

    private static BistroBuilderFinanceSnapshot baselineFinance;
    private static BistroBuilderFinancingSnapshot baselineFinancing;
    private static string baselineGameId;
    private static string baselineRestaurantName;
    private static string baselineCreatedUtc;
    private static int baselineDayIndex;
    private static int baselineYear;
    private static int baselineMonth;
    private static int baselineDay;
    private static string baselineProgressionStage;
    private static int baselineProgressionLevel;

    static BistroBuilderFinance3IRuntimeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3I - Prueba runtime real",
        false,
        3083)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3I",
                "La prueba runtime 3I ya está en ejecución.",
                "Aceptar");
            return;
        }
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3I",
                "Sal de Play Mode antes de iniciar la prueba automática.",
                "Aceptar");
            return;
        }
        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3I",
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
            startupDeadline = EditorApplication.timeSinceStartup + StartupTimeoutSeconds;
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
                SessionState.SetString(ResultKey, "Prueba runtime 3I cancelada antes de completarse.");
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
        EditorUtility.DisplayDialog("Bistro Builder — 3I", message, "Aceptar");
    }

    private static void TryRunWhenReady()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(ArmedKey, false))
        {
            EditorApplication.update -= TryRunWhenReady;
            return;
        }

        finance = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();
        financing = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinancingService>();
        results = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinancialResultsService>();
        generalState = UnityEngine.Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();

        bool ready = finance != null && finance.IsInitialized &&
                     financing != null && financing.IsInitialized &&
                     results != null && generalState != null && serviceState != null;
        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >= startupDeadline)
            {
                Fail("3I no encontró todas sus autoridades runtime inicializadas.");
            }
            return;
        }

        EditorApplication.update -= TryRunWhenReady;
        if (!serviceState.IsClosed)
        {
            Fail("La prueba 3I necesita comenzar con el restaurante Closed.");
            return;
        }
        if (!financing.ValidateConfiguration(out string error))
        {
            Fail("La configuración runtime de 3I no es válida. " + error);
            return;
        }

        Execute();
    }

    private static void Execute()
    {
        CaptureBaseline();
        if (baselineFinance == null || baselineFinancing == null)
        {
            Fail("No se pudieron capturar los snapshots iniciales de 3A/3I.");
            return;
        }

        int startTransactionCount = finance.TransactionCount;
        int startLoanCount = baselineFinancing.loans.Count;
        long startBalance = finance.CurrentBalanceCents;

        if (!results.TryGetDayResult(
                baselineDayIndex,
                out BistroBuilderDayFinancialResult beforeOrigination,
                out string error))
        {
            Fail("3G no pudo leer el día inicial. " + error);
            return;
        }

        var offerViews = new List<BistroBuilderFinancingOfferView>();
        if (!financing.TryGetOfferViews(offerViews, out error))
        {
            Fail("3I no pudo publicar ofertas. " + error);
            return;
        }
        BistroBuilderFinancingOfferView bridge = offerViews.Find(
            offer => offer.offerId == "bridge");
        if (bridge == null || !bridge.eligible ||
            bridge.principalCents != 500000L || bridge.totalInterestCents != 25000L)
        {
            Fail("La oferta puente runtime no coincide con el contrato esperado.");
            return;
        }

        string token = Guid.NewGuid().ToString("N").ToLowerInvariant();
        string acceptanceId = "accept_3i_runtime_" + token;
        if (!financing.TryAcceptOffer(
                "bridge",
                acceptanceId,
                out BistroBuilderLoanRecord loan,
                out error))
        {
            Fail("No se pudo aceptar el préstamo puente. " + error);
            return;
        }

        if (finance.CurrentBalanceCents - startBalance != 500000L ||
            finance.TransactionCount - startTransactionCount != 1 ||
            loan == null || loan.installments.Count != 5)
        {
            Fail("El desembolso no produjo exactamente +5.000,00 € y un préstamo.");
            return;
        }

        if (!financing.TryAcceptOffer(
                "bridge",
                acceptanceId,
                out BistroBuilderLoanRecord retry,
                out error) ||
            retry.loanId != loan.loanId ||
            finance.TransactionCount - startTransactionCount != 1)
        {
            Fail("El reintento de aceptación no fue idempotente. " + error);
            return;
        }

        if (!results.TryGetDayResult(
                baselineDayIndex,
                out BistroBuilderDayFinancialResult afterOrigination,
                out error) ||
            afterOrigination.revenueCents != beforeOrigination.revenueCents ||
            afterOrigination.operatingResultCents != beforeOrigination.operatingResultCents ||
            afterOrigination.totalCashInCents - beforeOrigination.totalCashInCents != 500000L ||
            afterOrigination.netCashChangeCents - beforeOrigination.netCashChangeCents != 500000L)
        {
            Fail("3G no separó el préstamo de ingresos y resultado operativo. " + error);
            return;
        }

        if (!financing.TryGetLiquidityPosition(
                out BistroBuilderLiquidityPosition initialLiquidity,
                out error) ||
            initialLiquidity.outstandingPrincipalCents != 500000L ||
            initialLiquidity.outstandingInterestCents != 25000L)
        {
            Fail("La posición de deuda inicial de 3I no es correcta. " + error);
            return;
        }

        int firstDueDay = loan.installments[0].dueDayIndex;
        if (!SetDaySilently(firstDueDay))
        {
            Fail("No se pudo situar la prueba en el primer vencimiento.");
            return;
        }
        if (!results.TryGetDayResult(
                firstDueDay,
                out BistroBuilderDayFinancialResult beforeFirstPayment,
                out error))
        {
            Fail("3G no pudo leer el día del primer vencimiento. " + error);
            return;
        }

        if (!financing.TryProcessDuePayments(
                firstDueDay,
                out BistroBuilderDebtPaymentProcessResult firstPayment,
                out error) ||
            firstPayment.paidInstallments != 1 ||
            firstPayment.principalPaidCents != 100000L ||
            firstPayment.interestPaidCents != 5000L)
        {
            Fail("La primera cuota no se pagó como 1.000,00 € + 50,00 €. " + error);
            return;
        }

        if (finance.CurrentBalanceCents - startBalance != 395000L ||
            finance.TransactionCount - startTransactionCount != 3)
        {
            Fail("La caja tras la primera cuota no coincide con el flujo esperado.");
            return;
        }

        if (!results.TryGetDayResult(
                firstDueDay,
                out BistroBuilderDayFinancialResult afterFirstPayment,
                out error) ||
            afterFirstPayment.totalPeriodExpensesCents -
                beforeFirstPayment.totalPeriodExpensesCents != 5000L ||
            afterFirstPayment.operatingResultCents -
                beforeFirstPayment.operatingResultCents != -5000L ||
            afterFirstPayment.totalCashOutCents -
                beforeFirstPayment.totalCashOutCents != 105000L)
        {
            Fail("3G no separó principal e interés de la cuota. " + error);
            return;
        }

        if (!financing.TryValidateLedgerConsistency(out error))
        {
            Fail("Préstamo y ledger no son consistentes tras pagar la cuota. " + error);
            return;
        }

        int secondDueDay = loan.installments[1].dueDayIndex;
        if (!SetDaySilently(secondDueDay))
        {
            Fail("No se pudo situar la prueba en el segundo vencimiento.");
            return;
        }

        long targetCash = 104999L;
        long drainAmount = finance.CurrentBalanceCents - targetCash;
        if (drainAmount <= 0L)
        {
            Fail("La caja inicial no permite fabricar el escenario de impago.");
            return;
        }

        var drainRequest = new BistroBuilderFinanceTransactionRequest
        {
            operationId = "liquidity_drain_3i_" + token,
            sourceSystemId = "finance.diagnostic",
            sourceReferenceId = "runtime_3i_" + token,
            categoryId = "diagnostic.liquidity_drain",
            kind = BistroBuilderFinanceTransactionKind.Debit,
            amountCents = drainAmount,
            dayIndex = secondDueDay,
            minuteOfDay = 720,
            description = "3I runtime liquidity drain"
        };
        if (!finance.TryPostTransaction(drainRequest, out _, out error))
        {
            Fail("No se pudo preparar el escenario de liquidez insuficiente. " + error);
            return;
        }

        int transactionsBeforeMiss = finance.TransactionCount;
        if (!financing.TryProcessDuePayments(
                secondDueDay,
                out BistroBuilderDebtPaymentProcessResult missed,
                out error) ||
            missed.paidInstallments != 0 ||
            missed.newlyOverdueInstallments != 1 ||
            finance.TransactionCount != transactionsBeforeMiss)
        {
            Fail("La cuota sin fondos no quedó impagada sin mover caja. " + error);
            return;
        }

        int overdueCheckDay = secondDueDay + 1;
        if (!SetDaySilently(overdueCheckDay) ||
            !financing.TryGetLiquidityPosition(
                out BistroBuilderLiquidityPosition stressedLiquidity,
                out error) ||
            stressedLiquidity.overdueDebtCents < 105000L ||
            stressedLiquidity.status != BistroBuilderLiquidityStatus.Critical)
        {
            Fail("3I no detectó deuda vencida y liquidez Critical. " + error);
            return;
        }

        int defaultDay = secondDueDay + financing.DelinquencyGraceDays + 1;
        if (!SetDaySilently(defaultDay) ||
            !financing.TryProcessDuePayments(
                defaultDay,
                out BistroBuilderDebtPaymentProcessResult defaultProcess,
                out error))
        {
            Fail("No se pudo procesar el escenario de default. " + error);
            return;
        }

        var loans = new List<BistroBuilderLoanRecord>();
        financing.CopyLoans(loans);
        BistroBuilderLoanRecord runtimeLoan = loans.Find(item => item.loanId == loan.loanId);
        if (runtimeLoan == null || runtimeLoan.status != BistroBuilderLoanStatus.Defaulted)
        {
            Fail("La deuda no pasó a Defaulted tras superar el periodo de gracia.");
            return;
        }

        if (!financing.TryGetFinancialStress(
                out BistroBuilderFinancialStressSnapshot stress,
                out error) ||
            stress.riskLevel != BistroBuilderFinancialRiskLevel.Severe ||
            stress.overdueDebtCents <= 0L)
        {
            Fail("El riesgo financiero no escaló a Severe tras default. " + error);
            return;
        }

        int diagnosticTransactions = finance.TransactionCount - startTransactionCount;
        long observedBalance = finance.CurrentBalanceCents;

        if (!RestoreBaseline(out error))
        {
            Fail("La prueba fue correcta pero no pudo restaurar el estado inicial. " + error);
            return;
        }

        if (finance.CurrentBalanceCents != startBalance ||
            finance.TransactionCount != startTransactionCount ||
            financing.CreateSnapshot().loans.Count != startLoanCount ||
            generalState.DayIndex != baselineDayIndex)
        {
            Fail("La restauración final de 3I no dejó el estado inicial idéntico.");
            return;
        }

        if (capturedErrors != 0)
        {
            Fail("La prueba registró Error/Exception/Assert: " + capturedErrors + ".");
            return;
        }

        Complete(
            "PRUEBA RUNTIME 3I SUPERADA\n\n" +
            "Préstamo puente: +5.000,00 €\n" +
            "Interés total congelado: 250,00 €\n" +
            "Aceptación idempotente: OK\n" +
            "Préstamo no contado como ingreso/beneficio: OK\n" +
            "Primera cuota: 1.000,00 € principal + 50,00 € interés\n" +
            "Interés reconocido como gasto; principal solo caja: OK\n" +
            "Segunda cuota sin fondos: impagada sin débito\n" +
            "Deuda vencida: Liquidez Critical\n" +
            "Superado periodo de gracia: Defaulted / Riesgo Severe\n" +
            "Movimientos diagnósticos nuevos: " + diagnosticTransactions + "\n" +
            "Caja mínima observada: " + FormatMoney(observedBalance) + "\n" +
            "Ledger y deuda consistentes: OK\n" +
            "Estado inicial restaurado: OK\n" +
            "Error/Exception/Assert: 0");
    }

    private static void CaptureBaseline()
    {
        baselineFinance = finance.CreateSnapshot();
        baselineFinancing = financing.CreateSnapshot();
        baselineGameId = generalState.GameId;
        baselineRestaurantName = generalState.RestaurantName;
        baselineCreatedUtc = generalState.CreatedUtc;
        baselineDayIndex = generalState.DayIndex;
        baselineYear = generalState.CalendarYear;
        baselineMonth = generalState.CalendarMonth;
        baselineDay = generalState.CalendarDay;
        baselineProgressionStage = generalState.ProgressionStageId;
        baselineProgressionLevel = generalState.ProgressionLevel;
    }

    private static bool SetDaySilently(int dayIndex)
    {
        return generalState.TryRestoreState(
            baselineGameId,
            baselineRestaurantName,
            baselineCreatedUtc,
            dayIndex,
            baselineYear,
            baselineMonth,
            baselineDay,
            baselineProgressionStage,
            baselineProgressionLevel,
            false);
    }

    private static bool RestoreBaseline(out string error)
    {
        error = string.Empty;
        if (baselineFinance != null && finance != null &&
            !finance.TryRestoreSnapshot(baselineFinance, out error))
        {
            return false;
        }
        if (baselineFinancing != null && financing != null &&
            !financing.TryRestoreSnapshot(baselineFinancing, out error))
        {
            return false;
        }
        if (generalState != null &&
            !generalState.TryRestoreState(
                baselineGameId,
                baselineRestaurantName,
                baselineCreatedUtc,
                baselineDayIndex,
                baselineYear,
                baselineMonth,
                baselineDay,
                baselineProgressionStage,
                baselineProgressionLevel,
                false))
        {
            error = "No se pudo restaurar GeneralGameState.";
            return false;
        }
        return true;
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors++;
        }
    }

    private static void Fail(string message)
    {
        RestoreBaseline(out _);
        Complete(
            "PRUEBA RUNTIME 3I FALLIDA\n\n" + message +
            "\n\nError/Exception/Assert observados: " + capturedErrors);
    }

    private static void Complete(string message)
    {
        CleanupSubscriptions();
        SessionState.SetString(ResultKey, message);
        SessionState.SetBool(ArmedKey, false);
        baselineFinance = null;
        baselineFinancing = null;
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
