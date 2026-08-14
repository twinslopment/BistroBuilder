using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderFinance3ERuntimeTest
{
    private const string ArmedKey =
        "BB.Finance.3E.Runtime.Armed";

    private const string ResultKey =
        "BB.Finance.3E.Runtime.Result";

    private const double StartupTimeoutSeconds = 20d;
    private const long DiagnosticPayrollCents = 43210L;

    private static double startupDeadline;
    private static int capturedErrors;

    private static BistroBuilderOperatingExpenseService service;
    private static BistroBuilderFinanceService finance;
    private static BistroBuilderGeneralGameStateService generalState;
    private static GameClock clock;

    private static BistroBuilderFinanceSnapshot originalFinance;
    private static string originalGameId;
    private static string originalRestaurantName;
    private static string originalCreatedUtc;
    private static int originalDayIndex;
    private static int originalYear;
    private static int originalMonth;
    private static int originalDay;
    private static string originalProgressionStageId;
    private static int originalProgressionLevel;

    static BistroBuilderFinance3ERuntimeTest()
    {
        EditorApplication.playModeStateChanged -=
            HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged +=
            HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3E - Prueba runtime real",
        false,
        3043)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3E",
                "La prueba runtime 3E ya está en ejecución.",
                "Aceptar");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3E",
                "Sal de Play Mode antes de iniciar la prueba 3E.",
                "Aceptar");
            return;
        }

        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3E",
                "No se pudo guardar la escena antes de iniciar la prueba.",
                "Aceptar");
            return;
        }

        SessionState.SetBool(ArmedKey, true);
        SessionState.EraseString(ResultKey);
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            startupDeadline =
                EditorApplication.timeSinceStartup +
                StartupTimeoutSeconds;

            EditorApplication.update -= TryRunWhenReady;
            EditorApplication.update += TryRunWhenReady;
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            Cleanup();
            SessionState.SetBool(ArmedKey, false);
            SessionState.SetString(
                ResultKey,
                "Prueba cancelada antes de completar 3E.");
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            string result =
                SessionState.GetString(
                    ResultKey,
                    string.Empty);

            if (!string.IsNullOrEmpty(result))
            {
                SessionState.EraseString(ResultKey);
                EditorUtility.DisplayDialog(
                    "Bistro Builder — 3E",
                    result,
                    "Aceptar");
            }
        }
    }

    private static void TryRunWhenReady()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false))
        {
            Cleanup();
            return;
        }

        service =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderOperatingExpenseService>();
        finance =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderFinanceService>();
        generalState =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderGeneralGameStateService>();
        clock =
            UnityEngine.Object.FindFirstObjectByType<GameClock>();
        BistroBuilderSaveGameService save =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSaveGameService>();

        bool ready =
            service != null &&
            finance != null &&
            finance.IsInitialized &&
            generalState != null &&
            clock != null &&
            save != null &&
            !save.IsBusy;

        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >=
                startupDeadline)
            {
                FinishFailure(
                    "Las autoridades runtime de 3E no estuvieron listas a tiempo.");
            }

            return;
        }

        EditorApplication.update -= TryRunWhenReady;

        if (!service.ValidateConfiguration(out string error))
        {
            FinishFailure(
                "La configuración runtime de 3E no es válida. " +
                error);
            return;
        }

        CaptureOriginalState();
        capturedErrors = 0;

        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;

        if (!TryFindUnpostedExpense(
                out BistroBuilderRecurringExpenseDefinition expense,
                out int dueDayIndex,
                out error))
        {
            FinishFailure(error);
            return;
        }

        DateTime currentDate;
        DateTime dueDate;

        try
        {
            currentDate = new DateTime(
                originalYear,
                originalMonth,
                originalDay);

            dueDate = currentDate.AddDays(
                dueDayIndex - originalDayIndex);
        }
        catch (ArgumentOutOfRangeException)
        {
            FinishFailure(
                "El vencimiento 3E queda fuera del calendario soportado.");
            return;
        }

        int baselineTransactions = finance.TransactionCount;
        long baselineBalance = finance.CurrentBalanceCents;

        if (!generalState.TrySetCalendar(
                dueDayIndex,
                dueDate.Year,
                dueDate.Month,
                dueDate.Day))
        {
            FinishFailure(
                "No se pudo avanzar al vencimiento del gasto 3E.");
            return;
        }

        string expenseOperationId =
            BistroBuilderOperatingExpensePolicy
                .BuildOperatingOperationId(
                    expense.ExpenseId,
                    dueDayIndex);

        bool expensePosted =
            finance.TryGetTransactionByOperationId(
                expenseOperationId,
                out BistroBuilderFinanceTransactionRecord
                    expenseTransaction) &&
            expenseTransaction != null &&
            expenseTransaction.kind ==
                BistroBuilderFinanceTransactionKind.Debit &&
            expenseTransaction.amountCents ==
                expense.AmountCents &&
            expenseTransaction.categoryId ==
                expense.CategoryId &&
            expenseTransaction.dayIndex ==
                dueDayIndex;

        bool expenseBalanceOk =
            finance.TransactionCount ==
                baselineTransactions + 1 &&
            finance.CurrentBalanceCents ==
                baselineBalance - expense.AmountCents;

        if (!expensePosted || !expenseBalanceOk)
        {
            FinishFailure(
                "El vencimiento real no produjo exactamente un débito." +
                "\nMovimiento: " + expensePosted +
                "\nCaja/ledger: " + expenseBalanceOk);
            return;
        }

        string payrollRunId =
            "runtime_3e_" +
            dueDayIndex.ToString("D8") + "_" +
            baselineTransactions.ToString("D6");

        var payroll = new BistroBuilderPayrollBatchRequest
        {
            payrollRunId = payrollRunId,
            periodStartDayIndex =
                Math.Max(1, dueDayIndex - 6),
            periodEndDayIndex = dueDayIndex,
            employeeCount = 3,
            totalCents = DiagnosticPayrollCents
        };

        if (!service.TryPostPayrollBatch(
                payroll,
                out BistroBuilderFinanceTransactionRecord payrollPosted,
                out bool payrollReplayed,
                out error) ||
            payrollReplayed ||
            payrollPosted == null)
        {
            FinishFailure(
                "El contrato de nómina no produjo un débito nuevo. " +
                error);
            return;
        }

        long expectedFinalBalance =
            baselineBalance -
            expense.AmountCents -
            DiagnosticPayrollCents;

        bool payrollOk =
            payrollPosted.categoryId ==
                BistroBuilderOperatingExpensePolicy
                    .PayrollCategoryId &&
            payrollPosted.sourceSystemId ==
                BistroBuilderOperatingExpensePolicy
                    .PayrollSourceSystemId &&
            payrollPosted.amountCents ==
                DiagnosticPayrollCents &&
            finance.TransactionCount ==
                baselineTransactions + 2 &&
            finance.CurrentBalanceCents ==
                expectedFinalBalance;

        if (!payrollOk)
        {
            FinishFailure(
                "La nómina no quedó registrada con el contrato financiero esperado.");
            return;
        }

        if (!service.TryProcessCurrentDay(
                out int repeatedExpenses,
                out error) ||
            repeatedExpenses != 0 ||
            finance.TransactionCount !=
                baselineTransactions + 2)
        {
            FinishFailure(
                "Reprocesar el vencimiento duplicó un gasto. " +
                error);
            return;
        }

        if (!service.TryPostPayrollBatch(
                payroll,
                out _,
                out bool replayedPayroll,
                out error) ||
            !replayedPayroll ||
            finance.TransactionCount !=
                baselineTransactions + 2)
        {
            FinishFailure(
                "Reintentar la nómina no fue idempotente. " +
                error);
            return;
        }

        var conflict = new BistroBuilderPayrollBatchRequest
        {
            payrollRunId = payroll.payrollRunId,
            periodStartDayIndex =
                payroll.periodStartDayIndex,
            periodEndDayIndex =
                payroll.periodEndDayIndex,
            employeeCount = payroll.employeeCount,
            totalCents = payroll.totalCents + 1L
        };

        if (service.TryPostPayrollBatch(
                conflict,
                out _,
                out _,
                out _) ||
            finance.TransactionCount !=
                baselineTransactions + 2)
        {
            FinishFailure(
                "Un PayrollRunId reutilizado con otro importe no fue rechazado.");
            return;
        }

        if (capturedErrors != 0)
        {
            FinishFailure(
                "La prueba generó Error/Exception/Assert: " +
                capturedErrors + ".");
            return;
        }

        FinishSuccess(
            "PRUEBA RUNTIME 3E SUPERADA" +
            "\n\nGasto recurrente: " +
            expense.DisplayName +
            " = " + FormatMoney(expense.AmountCents) +
            "\nDía de vencimiento: " + dueDayIndex +
            "\nCategoría: " + expense.CategoryId +
            "\nNómina contrato: " +
            FormatMoney(DiagnosticPayrollCents) +
            " / 3 personas" +
            "\nCaja: " + FormatMoney(baselineBalance) +
            " -> " + FormatMoney(expectedFinalBalance) +
            "\nMovimientos: " + baselineTransactions +
            " -> " + (baselineTransactions + 2) +
            "\nReintentos idempotentes: OK" +
            "\nPersonal no duplicado en Finanzas: OK" +
            "\nError/Exception/Assert: 0");
    }

    private static bool TryFindUnpostedExpense(
        out BistroBuilderRecurringExpenseDefinition selected,
        out int dueDayIndex,
        out string error)
    {
        selected = null;
        dueDayIndex = 0;
        error = string.Empty;

        BistroBuilderOperatingExpenseProfile profile =
            service.ExpenseProfile;

        if (profile == null || profile.Expenses == null)
        {
            error = "3E no tiene perfil de gastos.";
            return false;
        }

        int bestDay = int.MaxValue;

        for (int index = 0;
             index < profile.Expenses.Count;
             index++)
        {
            BistroBuilderRecurringExpenseDefinition expense =
                profile.Expenses[index];

            if (expense == null ||
                !expense.Active ||
                !BistroBuilderOperatingExpensePolicy
                    .TryGetNextDueDay(
                        expense,
                        Math.Max(1, originalDayIndex),
                        out int candidate))
            {
                continue;
            }

            for (int attempt = 0; attempt < 64; attempt++)
            {
                string operationId =
                    BistroBuilderOperatingExpensePolicy
                        .BuildOperatingOperationId(
                            expense.ExpenseId,
                            candidate);

                if (!finance.TryGetTransactionByOperationId(
                        operationId,
                        out _))
                {
                    break;
                }

                long next =
                    (long)candidate +
                    expense.IntervalDays;

                if (next > int.MaxValue)
                {
                    candidate = 0;
                    break;
                }

                candidate = (int)next;
            }

            if (candidate >= 1 && candidate < bestDay)
            {
                bestDay = candidate;
                selected = expense;
            }
        }

        if (selected == null)
        {
            error =
                "No existe un vencimiento 3E disponible para la prueba.";
            return false;
        }

        dueDayIndex = bestDay;
        return true;
    }

    private static void CaptureOriginalState()
    {
        originalFinance = finance.CreateSnapshot();

        originalGameId = generalState.GameId;
        originalRestaurantName =
            generalState.RestaurantName;
        originalCreatedUtc =
            generalState.CreatedUtc;
        originalDayIndex =
            generalState.DayIndex;
        originalYear =
            generalState.CalendarYear;
        originalMonth =
            generalState.CalendarMonth;
        originalDay =
            generalState.CalendarDay;
        originalProgressionStageId =
            generalState.ProgressionStageId;
        originalProgressionLevel =
            generalState.ProgressionLevel;
    }

    private static bool RestoreOriginalState(
        out string error)
    {
        error = string.Empty;

        if (finance == null ||
            generalState == null ||
            originalFinance == null)
        {
            return true;
        }

        if (!finance.TryRestoreSnapshot(
                originalFinance,
                out error))
        {
            return false;
        }

        if (!generalState.TryRestoreState(
                originalGameId,
                originalRestaurantName,
                originalCreatedUtc,
                originalDayIndex,
                originalYear,
                originalMonth,
                originalDay,
                originalProgressionStageId,
                originalProgressionLevel,
                false))
        {
            error =
                "No se pudo restaurar el calendario original.";
            return false;
        }

        return true;
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

    private static void FinishSuccess(string message)
    {
        if (!RestoreOriginalState(out string restoreError))
        {
            Finish(
                "PRUEBA RUNTIME 3E NO SUPERADA" +
                "\n\nLa prueba pasó, pero el rollback diagnóstico falló. " +
                restoreError);
            return;
        }

        Finish(message);
    }

    private static void FinishFailure(string message)
    {
        string final =
            "PRUEBA RUNTIME 3E NO SUPERADA\n\n" +
            message;

        if (!RestoreOriginalState(out string restoreError))
        {
            final +=
                "\n\nAdemás, falló el rollback diagnóstico: " +
                restoreError;
        }

        Finish(final);
    }

    private static void Finish(string result)
    {
        Cleanup();
        SessionState.SetBool(ArmedKey, false);
        SessionState.SetString(ResultKey, result);

        if (EditorApplication.isPlaying)
        {
            EditorApplication.delayCall +=
                () => EditorApplication.isPlaying = false;
        }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= TryRunWhenReady;
        Application.logMessageReceived -= HandleLog;

        service = null;
        finance = null;
        generalState = null;
        clock = null;
        originalFinance = null;
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("N2") + " €";
    }
}
