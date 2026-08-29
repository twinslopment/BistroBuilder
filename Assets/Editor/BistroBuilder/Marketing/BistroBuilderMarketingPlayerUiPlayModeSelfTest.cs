using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gate funcional real de la UI jugable de Marketing.
/// Abre la pantalla, contrata una campaña, verifica Finanzas,
/// cancela la campaña y restaura los snapshots originales.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderMarketingPlayerUiPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Marketing.PlayerUI.Play.Stage";
    private const string SuccessKey = "BB.Marketing.PlayerUI.Play.Success";
    private const string ReportPath = "MarketingPlayerUiPlayModeReport.txt";

    private static BistroBuilderMarketingSnapshot originalMarketing;
    private static BistroBuilderFinanceSnapshot originalFinance;

    static BistroBuilderMarketingPlayerUiPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }
    [MenuItem("Tools/Bistro Builder/Marketing/UI jugable - PlayMode funcional", false, 7264)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("La prueba UI de Marketing ya está ejecutándose.");

        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, commandLine ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stage)) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "run_cli" : "run_menu");
        }

        if (state == PlayModeStateChange.EnteredEditMode)
            FinishEditor(stage.Contains("cli", StringComparison.Ordinal));
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 4) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;

        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        SessionState.SetString(StageKey, cli ? "executing_cli" : "executing_menu");
        RunScenario(cli);
    }

    private static void RunScenario(bool commandLine)
    {
        BistroBuilderMarketingPlayerScreen screen = Find<BistroBuilderMarketingPlayerScreen>();
        BistroBuilderMarketingPlayerFacade facade = Find<BistroBuilderMarketingPlayerFacade>();
        BistroBuilderMarketingService marketing = Find<BistroBuilderMarketingService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();

        if (screen == null || facade == null || marketing == null || finance == null)
        {
            Finish(false, "Faltan componentes runtime de la UI de Marketing.", commandLine);
            return;
        }

        string screenError = string.Empty;
        string facadeError = string.Empty;
        bool screenValid = screen.ValidateConfiguration(out screenError);
        bool facadeValid = facade.ValidateConfiguration(out facadeError);
        if (!screenValid || !facadeValid)
        {
            Finish(false,
                "Configuración inválida. Screen=" + screenError +
                " | Facade=" + facadeError,
                commandLine);
            return;
        }

        originalMarketing = marketing.CreateSnapshot();
        originalFinance = finance.CreateSnapshot();
        if (originalMarketing == null || originalFinance == null)
        {
            Finish(false, "No pudieron capturarse snapshots de rollback.", commandLine);
            return;
        }

        if (!marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(),
                out string resetError))
        {
            Finish(false, "No pudo limpiarse Marketing: " + resetError, commandLine);
            return;
        }

        long balanceBefore = finance.CreateSnapshot().currentBalanceCents;
        screen.Show();
        if (!screen.IsVisible)
        {
            Finish(false, "La pantalla no se abrió en Play Mode.", commandLine);
            return;
        }

        screen.ShowCatalog();
        screen.FilterAll();
        TMP_InputField[] inputs = screen.GetComponentsInChildren<TMP_InputField>(true);
        TMP_InputField search = null;
        for (int index = 0; index < inputs.Length; index++)
        {
            if (inputs[index] != null &&
                string.Equals(inputs[index].name, "Search", StringComparison.Ordinal))
            {
                search = inputs[index];
                break;
            }
        }
        if (search == null)
        {
            Finish(false, "La UI no expone su buscador jugable.", commandLine);
            return;
        }
        search.text = "flyers";
        screen.Refresh();
        screen.StartSelectedCampaign();

        BistroBuilderMarketingSnapshot afterStart = marketing.CreateSnapshot();
        BistroBuilderFinanceSnapshot financeAfterStart = finance.CreateSnapshot();
        if (afterStart == null || afterStart.campaigns.Count != 1)
        {
            Finish(false, "La UI no inició exactamente una campaña.", commandLine);
            return;
        }

        if (financeAfterStart == null ||
            financeAfterStart.currentBalanceCents >= balanceBefore)
        {
            Finish(false, "La contratación no generó el gasto real en Finanzas.", commandLine);
            return;
        }

        long paidBalance = financeAfterStart.currentBalanceCents;
        screen.ShowActiveCampaigns();
        screen.CancelSelectedActive();
        screen.CancelSelectedActive();

        BistroBuilderMarketingSnapshot afterCancel = marketing.CreateSnapshot();
        BistroBuilderFinanceSnapshot financeAfterCancel = finance.CreateSnapshot();
        if (afterCancel == null || afterCancel.campaigns.Count != 0)
        {
            Finish(false, "La cancelación desde UI no retiró la campaña activa.", commandLine);
            return;
        }

        if (financeAfterCancel == null ||
            financeAfterCancel.currentBalanceCents != paidBalance)
        {
            Finish(false,
                "Cancelar devolvió dinero o alteró Finanzas indebidamente.",
                commandLine);
            return;
        }

        screen.Hide();
        if (screen.IsVisible)
        {
            Finish(false, "La pantalla no se cerró correctamente.", commandLine);
            return;
        }

        long spent = balanceBefore - paidBalance;
        Finish(true,
            "PASS — UI abrió, contrató 1 campaña, cargó " +
            (spent / 100.0).ToString("F2") +
            " € en Finanzas, la canceló sin reembolso y cerró correctamente.",
            commandLine);
    }

    private static T Find<T>() where T : Component
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(
            FindObjectsInactive.Include);
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        BistroBuilderMarketingService marketing = Find<BistroBuilderMarketingService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();

        string rollbackError = string.Empty;
        if (marketing != null && originalMarketing != null &&
            !marketing.TryRestoreSnapshot(originalMarketing, out string marketingError))
            rollbackError += " Marketing=" + marketingError;
        if (finance != null && originalFinance != null &&
            !finance.TryRestoreSnapshot(originalFinance, out string financeError))
            rollbackError += " Finance=" + financeError;

        bool finalSuccess = success && string.IsNullOrWhiteSpace(rollbackError);
        string report = "=== BISTRO BUILDER — MARKETING UI PLAY MODE ===\n" +
            (finalSuccess ? "[PASS] " : "[FAIL] ") + message + rollbackError;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        Debug.Log(report);
        SessionState.SetBool(SuccessKey, finalSuccess);
        SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }

    private static void FinishEditor(bool commandLine)
    {
        bool success = SessionState.GetBool(SuccessKey, false);
        SessionState.EraseString(StageKey);
        if (commandLine)
            EditorApplication.Exit(success ? 0 : 1);
    }
}
