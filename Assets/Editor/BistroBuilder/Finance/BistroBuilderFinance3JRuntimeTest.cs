using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prueba runtime automática de 3J. Recorre las cinco pantallas, comprueba
/// aislamiento modal y realiza una aceptación real de financiación mediante
/// la API pública de la UI. Finance/Financing se restauran exactamente antes
/// de salir de Play Mode.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderFinance3JRuntimeTest
{
    private const string ArmedKey = "BB.Finance.3J.Runtime.Armed";
    private const string ResultKey = "BB.Finance.3J.Runtime.Result";
    private const string CommandLineKey = "BB.Finance.3J.Runtime.CommandLine";
    private const string ReportPath = "Block3JFinanceRuntimeReport.txt";
    private const double StartupTimeoutSeconds = 25d;

    private static double startupDeadline;
    private static int capturedErrors;
    private static bool executionStarted;

    private static BistroBuilderFinanceService finance;
    private static BistroBuilderFinancingService financing;
    private static BistroBuilderFinanceDashboardService dashboard;
    private static BistroBuilderFinanceRuntimeView view;
    private static BistroBuilderFinanceUiModalCoordinator coordinator;

    private static BistroBuilderFinanceSnapshot baselineFinance;
    private static BistroBuilderFinancingSnapshot baselineFinancing;

    static BistroBuilderFinance3JRuntimeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3J - Prueba runtime real",
        false,
        3103)]
    private static void Run() => Begin(false);

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        Begin(true);
    }

    private static void Begin(bool commandLine)
    {
        SessionState.SetBool(CommandLineKey, commandLine);
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3J",
                "La prueba runtime 3J ya está en ejecución.",
                "Aceptar");
            return;
        }
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3J",
                "Sal de Play Mode antes de iniciar la prueba automática.",
                "Aceptar");
            return;
        }
        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3J",
                "No se pudieron guardar las escenas antes de la prueba.",
                "Aceptar");
            return;
        }

        executionStarted = false;
        capturedErrors = 0;
        baselineFinance = null;
        baselineFinancing = null;
        SessionState.SetBool(ArmedKey, true);
        SessionState.EraseString(ResultKey);
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
                    "PRUEBA RUNTIME 3J CANCELADA antes de completarse.");
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
        bool commandLine = SessionState.GetBool(CommandLineKey, false);
        SessionState.SetBool(CommandLineKey, false);
        if (commandLine)
        {
            File.WriteAllText(Path.GetFullPath(ReportPath), message);
            EditorApplication.Exit(message.Contains("SUPERADA") ? 0 : 1);
            return;
        }
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3J",
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
        if (executionStarted)
        {
            return;
        }

        ResolveDependencies();
        bool ready =
            finance != null && finance.IsInitialized &&
            financing != null && financing.IsInitialized &&
            dashboard != null &&
            view != null &&
            coordinator != null;

        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >= startupDeadline)
            {
                Fail("3J no encontró todas sus autoridades/vista runtime inicializadas.");
            }
            return;
        }

        executionStarted = true;
        EditorApplication.update -= TryRunWhenReady;
        Execute();
    }

    private static void Execute()
    {
        baselineFinance = finance.CreateSnapshot();
        baselineFinancing = financing.CreateSnapshot();
        if (baselineFinance == null || baselineFinancing == null)
        {
            Fail("No se pudieron capturar los snapshots iniciales de 3A/3I.");
            return;
        }

        if (!BistroBuilderFinanceHardeningValidator.ValidateCurrentScene(
                out _,
                out int baseFailures,
                out string baseReport) ||
            baseFailures != 0)
        {
            Fail("La base endurecida 3A-3I no es limpia en runtime.\n" + baseReport);
            return;
        }

        if (!dashboard.ValidateConfiguration(out string error) ||
            !view.ValidateConfiguration(out error) ||
            !coordinator.ValidateConfiguration(out error))
        {
            Fail("La configuración runtime 3J no es válida. " + error);
            return;
        }

        long startingCash = finance.CurrentBalanceCents;
        int startingTransactions = finance.TransactionCount;
        int startingLoans = baselineFinancing.loans != null
            ? baselineFinancing.loans.Count
            : 0;

        if (!view.TryOpenFromInterface(out error) ||
            !view.IsOpen ||
            !view.VisualTreeBuilt)
        {
            Fail("La UI 3J no pudo abrirse. " + error);
            return;
        }

        coordinator.ApplyVisibilityForTests();
        if (!coordinator.AreExistingAccessButtonsHiddenForTests())
        {
            Fail("3J no aisló los accesos globales existentes al abrir su modal.");
            return;
        }

        if (!view.TryValidateVisibleContent(out error) ||
            !view.TryValidateStableInteractionVisuals(out error))
        {
            Fail("La UI 3J no superó su validación visual funcional. " + error);
            return;
        }

        BistroBuilderFinancePlayerSection[] sections =
        {
            BistroBuilderFinancePlayerSection.Overview,
            BistroBuilderFinancePlayerSection.Results,
            BistroBuilderFinancePlayerSection.Cash,
            BistroBuilderFinancePlayerSection.History,
            BistroBuilderFinancePlayerSection.Financing
        };
        for (int index = 0; index < sections.Length; index++)
        {
            if (!view.TrySelectSectionForTests(sections[index], out error) ||
                view.CurrentSection != sections[index] ||
                view.VisibleElementCount <= 0)
            {
                Fail("La sección 3J " + sections[index] +
                     " no quedó funcional. " + error);
                return;
            }
        }

        BistroBuilderFinanceDashboardPeriod[] periods =
        {
            BistroBuilderFinanceDashboardPeriod.Last7Days,
            BistroBuilderFinanceDashboardPeriod.Last30Days,
            BistroBuilderFinanceDashboardPeriod.Last90Days,
            BistroBuilderFinanceDashboardPeriod.AllTime
        };
        for (int index = 0; index < periods.Length; index++)
        {
            if (!view.TrySetPeriodForTests(periods[index], out error) ||
                view.SelectedPeriod != periods[index])
            {
                Fail("La ventana histórica " + periods[index] +
                     " no quedó funcional. " + error);
                return;
            }
        }

        if (!view.TrySelectSectionForTests(
                BistroBuilderFinancePlayerSection.History,
                out error))
        {
            Fail("No se pudo volver a Históricos. " + error);
            return;
        }

        BistroBuilderFinanceChartMetric[] metrics =
        {
            BistroBuilderFinanceChartMetric.Revenue,
            BistroBuilderFinanceChartMetric.OperatingResult,
            BistroBuilderFinanceChartMetric.NetCash
        };
        for (int index = 0; index < metrics.Length; index++)
        {
            if (!view.TrySetChartMetricForTests(metrics[index], out error) ||
                view.SelectedChartMetric != metrics[index])
            {
                Fail("La métrica gráfica " + metrics[index] +
                     " no quedó funcional. " + error);
                return;
            }
        }

        BistroBuilderFinanceDashboardSnapshot beforeAction =
            view.DashboardSnapshot;
        if (beforeAction == null ||
            beforeAction.cashBalanceCents != startingCash ||
            beforeAction.currentDay == null ||
            beforeAction.periodReport == null ||
            beforeAction.liquidity == null ||
            beforeAction.stress == null)
        {
            Fail("El read-model visible de 3J no refleja las autoridades actuales.");
            return;
        }

        if (!view.TrySelectFirstEligibleOfferForTests(out error))
        {
            Fail("La escena base no ofrece una financiación elegible para probar la acción 3J. " + error);
            return;
        }
        if (!view.TryOpenFinancingConfirmationForTests(out error))
        {
            Fail("3J no abrió la confirmación de financiación. " + error);
            return;
        }

        // Abrir/revisar una confirmación jamás puede mover dinero.
        if (finance.CurrentBalanceCents != startingCash ||
            finance.TransactionCount != startingTransactions)
        {
            Fail("La previsualización de financiación modificó caja/ledger.");
            return;
        }

        if (!view.TryConfirmSelectedFinancingForTests(
                out BistroBuilderLoanRecord acceptedLoan,
                out error) ||
            acceptedLoan == null)
        {
            Fail("La aceptación real desde 3J no llegó a 3I. " + error);
            return;
        }

        if (finance.CurrentBalanceCents !=
                checked(startingCash + acceptedLoan.principalCents) ||
            finance.TransactionCount != startingTransactions + 1)
        {
            Fail("3J/3I no publicaron exactamente un desembolso canónico de préstamo.");
            return;
        }

        BistroBuilderFinancingSnapshot financed = financing.CreateSnapshot();
        if (financed == null || financed.loans == null ||
            financed.loans.Count != startingLoans + 1 ||
            !financing.TryValidateLedgerConsistency(out error))
        {
            Fail("La deuda aceptada desde 3J no converge con el ledger. " + error);
            return;
        }

        if (!RestoreBaseline(out error))
        {
            Fail("3J funcionó pero no pudo restaurar 3A/3I. " + error);
            return;
        }

        view.Close();
        coordinator.ApplyVisibilityForTests();
        if (view.IsOpen ||
            coordinator.SuppressedExistingAccessCount != 0 ||
            coordinator.IsFinanceAccessSuppressed)
        {
            Fail("Cerrar 3J no restauró correctamente el contexto modal.");
            return;
        }

        if (!SnapshotsEqual(baselineFinance, finance.CreateSnapshot()) ||
            !SnapshotsEqual(baselineFinancing, financing.CreateSnapshot()))
        {
            Fail("El estado inicial 3A/3I no quedó exactamente restaurado.");
            return;
        }

        if (capturedErrors != 0)
        {
            Fail("Se observaron " + capturedErrors +
                 " Error/Exception/Assert durante la prueba 3J.");
            return;
        }

        Complete(
            "PRUEBA RUNTIME 3J SUPERADA\n\n" +
            "Resumen / Resultados / Caja / Históricos / Financiación: OK\n" +
            "Periodos 7 / 30 / 90 / Todo: OK\n" +
            "Gráficos Ingresos / Resultado / Caja: OK\n" +
            "Read-model 3A/3G/3H/3I: OK\n" +
            "Aislamiento modal y restauración de input: OK\n" +
            "Confirmación previa sin movimiento de caja: OK\n" +
            "Financiación real UI → 3I → ledger 3A: OK\n" +
            "Desembolso: +" +
            BistroBuilderFinanceUiFormat.Money(acceptedLoan.principalCents) + "\n" +
            "Deuda / ledger bidireccional: OK\n" +
            "Estado inicial 3A/3I restaurado exactamente: OK\n" +
            "Error/Exception/Assert: 0");
    }

    private static bool RestoreBaseline(out string error)
    {
        error = string.Empty;
        if (baselineFinance == null || baselineFinancing == null)
        {
            error = "No existe baseline de restauración.";
            return false;
        }

        if (!finance.TryRestoreSnapshot(baselineFinance, out error))
        {
            return false;
        }
        if (!financing.TryRestoreSnapshot(baselineFinancing, out error))
        {
            return false;
        }
        return financing.TryValidateLedgerConsistency(out error);
    }

    private static void RestoreBestEffort()
    {
        try
        {
            if (finance != null && baselineFinance != null)
            {
                finance.TryRestoreSnapshot(baselineFinance, out _);
            }
            if (financing != null && baselineFinancing != null)
            {
                financing.TryRestoreSnapshot(baselineFinancing, out _);
            }
            if (view != null)
            {
                view.Close();
            }
            if (coordinator != null)
            {
                coordinator.ApplyVisibilityForTests();
            }
        }
        catch
        {
            // Best effort solo en la ruta de fallo de una prueba diagnóstica.
        }
    }

    private static bool SnapshotsEqual(
        BistroBuilderFinanceSnapshot left,
        BistroBuilderFinanceSnapshot right)
    {
        return left != null && right != null &&
               string.Equals(
                   JsonUtility.ToJson(left),
                   JsonUtility.ToJson(right),
                   StringComparison.Ordinal);
    }

    private static bool SnapshotsEqual(
        BistroBuilderFinancingSnapshot left,
        BistroBuilderFinancingSnapshot right)
    {
        return left != null && right != null &&
               string.Equals(
                   JsonUtility.ToJson(left),
                   JsonUtility.ToJson(right),
                   StringComparison.Ordinal);
    }

    private static void ResolveDependencies()
    {
        finance = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();
        financing = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinancingService>();
        dashboard = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceDashboardService>();
        view = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceRuntimeView>();
        coordinator = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderFinanceUiModalCoordinator>();
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
        RestoreBestEffort();
        Complete(
            "PRUEBA RUNTIME 3J FALLIDA\n\n" +
            message +
            "\n\nError/Exception/Assert observados: " + capturedErrors);
    }

    private static void Complete(string message)
    {
        CleanupSubscriptions();
        SessionState.SetString(ResultKey, message);
        SessionState.SetBool(ArmedKey, false);
        EditorApplication.isPlaying = false;
    }

    private static void CleanupSubscriptions()
    {
        EditorApplication.update -= TryRunWhenReady;
        Application.logMessageReceived -= HandleLog;
    }
}
