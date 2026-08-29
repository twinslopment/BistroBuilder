using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderProgression9EPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Progression.9E.Play.Stage";
    private const string SuccessKey = "BB.Progression.9E.Play.Success";
    private const string ReportPath = "Progression9EPlayModeReport.txt";

    static BistroBuilderProgression9EPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Progression/9E - PlayMode real", false, 9043)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 9E ya está ejecutándose.");
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
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool success = SessionState.GetBool(SuccessKey, false);
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            SessionState.EraseString(StageKey);
            if (cli) EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 4) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;
        RunRuntime(stage.EndsWith("cli", StringComparison.Ordinal));
    }

    private static void RunRuntime(bool commandLine)
    {
        SessionState.SetString(StageKey, commandLine ? "running_cli" : "running_menu");
        BistroBuilderProgressionPlayerFacade facade = Find<BistroBuilderProgressionPlayerFacade>();
        BistroBuilderProgressionPlayerScreen screen = Find<BistroBuilderProgressionPlayerScreen>();
        BistroBuilderUpgradeService upgrades = Find<BistroBuilderUpgradeService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();

        if (facade == null || screen == null || upgrades == null || finance == null)
        {
            Finish(false, "9E: faltan autoridades/UI runtime.", commandLine);
            return;
        }
        string facadeError = string.Empty;
        string screenError = string.Empty;
        if (!facade.ValidateConfiguration(out facadeError) ||
            !screen.ValidateConfiguration(out screenError))
        {
            Finish(false, "9E: configuración runtime inválida. " +
                facadeError + " " + screenError, commandLine);
            return;
        }

        BistroBuilderUpgradeSnapshot originalUpgrades = upgrades.CreateSnapshot();
        BistroBuilderFinanceSnapshot originalFinance = finance.CreateSnapshot();
        if (!upgrades.TryResetForLegacyLoad(out string resetError))
        {
            Finish(false, "9E: no pudo preparar fixture vacío. " + resetError, commandLine);
            return;
        }

        screen.Show();
        screen.FilterDining();
        string selectedId = screen.SelectedUpgradeId;
        if (string.IsNullOrWhiteSpace(selectedId) ||
            !upgrades.UpgradeCatalog.TryGetUpgrade(
                selectedId, out BistroBuilderUpgradeDefinition definition))
        {
            Restore(upgrades, finance, originalUpgrades, originalFinance, out _);
            Finish(false, "9E: la UI no seleccionó una mejora válida.", commandLine);
            return;
        }

        long before = finance.CurrentBalanceCents;
        bool bought = screen.BuySelected(out string buyError);
        bool routeOk = bought && upgrades.IsPurchased(selectedId) &&
            finance.CurrentBalanceCents == before - definition.costCents;

        screen.Hide();
        bool restored = Restore(
            upgrades, finance, originalUpgrades, originalFinance, out string restoreError);
        if (!routeOk || !restored)
        {
            Finish(false,
                "9E: compra desde UI no produjo el resultado canónico. " +
                buyError + " " + restoreError,
                commandLine);
            return;
        }

        Finish(true,
            "PASS — la pantalla abre, selecciona una mejora, compra por la ruta canónica " +
            "y Finanzas registra exactamente su coste.",
            commandLine);
    }

    private static bool Restore(
        BistroBuilderUpgradeService upgrades,
        BistroBuilderFinanceService finance,
        BistroBuilderUpgradeSnapshot upgradeSnapshot,
        BistroBuilderFinanceSnapshot financeSnapshot,
        out string error)
    {
        string upgradeError = string.Empty;
        string financeError = string.Empty;
        bool upgradeOk = upgrades.TryRestoreSnapshot(upgradeSnapshot, out upgradeError);
        bool financeOk = finance.TryRestoreSnapshot(financeSnapshot, out financeError);
        error = (upgradeError + " " + financeError).Trim();
        return upgradeOk && financeOk;
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        string report = "=== BISTRO BUILDER — 9E / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    private static T Find<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}
