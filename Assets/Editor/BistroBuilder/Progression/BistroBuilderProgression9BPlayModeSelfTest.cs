using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderProgression9BPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Progression.9B.Play.Stage";
    private const string SuccessKey = "BB.Progression.9B.Play.Success";
    private const string FailureKey = "BB.Progression.9B.Play.Failure";
    private const string ReportPath = "Progression9BPlayModeReport.txt";

    private static BistroBuilderSaveGameService saveGame;
    private static BistroBuilderUpgradeService upgrades;
    private static BistroBuilderFinanceService finance;
    private static RestaurantServiceStateService serviceState;
    private static BistroBuilderUpgradeSnapshot originalUpgrades;
    private static BistroBuilderFinanceSnapshot originalFinance;
    private static BistroBuilderUpgradeSnapshot purchasedSnapshot;
    private static long purchasedBalance;
    private static int slot = -1;

    static BistroBuilderProgression9BPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Progression/9B - PlayMode real", false, 9013)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 9B ya está ejecutándose.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.EraseString(FailureKey);
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
            SessionState.SetString(StageKey, cli ? "init_cli" : "init_menu");
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
        if (!EditorApplication.isPlaying) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("init_", StringComparison.Ordinal) || Time.frameCount < 4)
            return;
        Initialize(stage.EndsWith("cli", StringComparison.Ordinal));
    }

    private static void Initialize(bool commandLine)
    {
        SessionState.SetString(StageKey, commandLine ? "running_cli" : "running_menu");
        saveGame = Find<BistroBuilderSaveGameService>();
        upgrades = Find<BistroBuilderUpgradeService>();
        finance = Find<BistroBuilderFinanceService>();
        serviceState = Find<RestaurantServiceStateService>();

        if (saveGame == null || upgrades == null || finance == null || serviceState == null)
        {
            Finish(false, "9B: faltan autoridades runtime.", commandLine);
            return;
        }
        saveGame.RefreshExtensions();
        string configError = string.Empty;
        if (!serviceState.IsClosed ||
            !saveGame.HasProvider(BistroBuilderUpgradeSaveSectionProvider.StableSectionId) ||
            !upgrades.ValidateConfiguration(out configError))
        {
            Finish(false, "9B: configuración runtime inválida. " + configError, commandLine);
            return;
        }
        if (!TryFindFreeSlot(out slot))
        {
            Finish(false, "9B: no hay slot diagnóstico libre 950-969.", commandLine);
            return;
        }

        originalUpgrades = upgrades.CreateSnapshot();
        originalFinance = finance.CreateSnapshot();
        string resetError = string.Empty;
        if (originalUpgrades == null || originalFinance == null ||
            !upgrades.TryResetForLegacyLoad(out resetError))
        {
            Finish(false, "9B: no pudo preparar fixture. " + resetError, commandLine);
            return;
        }

        BistroBuilderUpgradeDefinition first = upgrades.UpgradeCatalog.Upgrades[0];
        long beforeBalance = finance.CurrentBalanceCents;
        if (!upgrades.TryPurchaseUpgrade(first.upgradeId, out var purchased, out string purchaseError))
        {
            RestoreOriginalState(out _);
            Finish(false, "9B: la compra real fue rechazada. " + purchaseError, commandLine);
            return;
        }

        purchasedSnapshot = upgrades.CreateSnapshot();
        purchasedBalance = finance.CurrentBalanceCents;
        bool purchaseValid = purchased != null &&
            purchased.upgradeId == first.upgradeId &&
            purchased.paidCents == first.costCents &&
            purchasedBalance == beforeBalance - first.costCents &&
            upgrades.IsPurchased(first.upgradeId);
        if (!purchaseValid)
        {
            FailAndCleanup("9B: la compra no produjo estado/finanzas canónicos.", commandLine);
            return;
        }

        saveGame.OperationCompleted -= HandleOperationCompleted;
        saveGame.OperationCompleted += HandleOperationCompleted;
        SessionState.SetString(StageKey, commandLine ? "save_cli" : "save_menu");
        if (!saveGame.TrySaveSlot(slot, "BB 9B PROGRESSION TEST", out string rejection))
            FailAndCleanup("9B: Save fue rechazado. " + rejection, commandLine);
    }

    private static void HandleOperationCompleted(BistroBuilderSaveOperationResult result)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
        if (result == null || !result.Succeeded)
        {
            FailAndCleanup("9B: operación Save/Load falló. " +
                (result != null ? result.Message : "resultado nulo"), commandLine);
            return;
        }

        if (stage.StartsWith("save_", StringComparison.Ordinal))
        {
            string resetError = string.Empty;
            string financeError = string.Empty;
            if (!upgrades.TryResetForLegacyLoad(out resetError) ||
                !finance.TryRestoreSnapshot(originalFinance, out financeError))
            {
                FailAndCleanup("9B: no pudo mutar el mundo antes de Load. " +
                    resetError + " " + financeError, commandLine);
                return;
            }
            SessionState.SetString(StageKey, commandLine ? "load_cli" : "load_menu");
            if (!saveGame.TryLoadSlot(slot, out string rejection))
                FailAndCleanup("9B: Load fue rechazado. " + rejection, commandLine);
            return;
        }
        if (stage.StartsWith("load_", StringComparison.Ordinal))
        {
            BistroBuilderUpgradeSnapshot loaded = upgrades.CreateSnapshot();
            bool stateRestored = loaded != null && purchasedSnapshot != null &&
                JsonUtility.ToJson(loaded) == JsonUtility.ToJson(purchasedSnapshot) &&
                finance.CurrentBalanceCents == purchasedBalance;
            if (!stateRestored)
            {
                FailAndCleanup("9B: Load no restauró exactamente mejora y gasto.", commandLine);
                return;
            }

            SessionState.SetString(StageKey, commandLine ? "delete_cli" : "delete_menu");
            if (!saveGame.TryDeleteSlot(slot, out string rejection))
                FailAndCleanup("9B: no pudo limpiar el slot diagnóstico. " + rejection,
                    commandLine);
            return;
        }

        if (stage.StartsWith("delete_", StringComparison.Ordinal))
        {
            if (!RestoreOriginalState(out string restoreError))
            {
                Finish(false, "9B: el gate pasó pero no restauró el fixture. " +
                    restoreError, commandLine);
                return;
            }
            Finish(true,
                "PASS — compra real descuenta Finanzas, persiste progression.upgrades y " +
                "Save/Load restaura exactamente la mejora adquirida.", commandLine);
            return;
        }
        if (stage.StartsWith("cleanup_", StringComparison.Ordinal))
        {
            string failure = SessionState.GetString(FailureKey, "9B falló.");
            RestoreOriginalState(out string restoreError);
            if (!string.IsNullOrWhiteSpace(restoreError))
                failure += " Restauración: " + restoreError;
            Finish(false, failure, commandLine);
        }
    }

    private static void FailAndCleanup(string message, bool commandLine)
    {
        SessionState.SetString(FailureKey, message);
        if (saveGame != null && slot >= 0 && saveGame.SlotExists(slot) && !saveGame.IsBusy)
        {
            SessionState.SetString(StageKey,
                commandLine ? "cleanup_cli" : "cleanup_menu");
            if (saveGame.TryDeleteSlot(slot, out _)) return;
        }
        RestoreOriginalState(out string restoreError);
        if (!string.IsNullOrWhiteSpace(restoreError))
            message += " Restauración: " + restoreError;
        Finish(false, message, commandLine);
    }

    private static bool RestoreOriginalState(out string error)
    {
        string upgradeError = string.Empty;
        string financeError = string.Empty;
        bool upgradeOk = originalUpgrades == null ||
            upgrades.TryRestoreSnapshot(originalUpgrades, out upgradeError);
        bool financeOk = originalFinance == null ||
            finance.TryRestoreSnapshot(originalFinance, out financeError);
        error = (upgradeError + " " + financeError).Trim();
        return upgradeOk && financeOk;
    }

    private static bool TryFindFreeSlot(out int found)
    {
        found = -1;
        for (int candidate = 950; candidate <= 969; candidate++)
        {
            if (saveGame.SlotExists(candidate)) continue;
            found = candidate;
            return true;
        }
        return false;
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (saveGame != null)
            saveGame.OperationCompleted -= HandleOperationCompleted;
        string report = "=== BISTRO BUILDER — 9B / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.EraseString(FailureKey);
        SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static T Find<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}
