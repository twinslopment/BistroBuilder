using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedCustomers10CSaveLoadPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Customers.10C.SaveLoad.Stage";
    private const string SuccessKey = "BB.Customers.10C.SaveLoad.Success";
    private const string FailureKey = "BB.Customers.10C.SaveLoad.Failure";
    private const string ReportPath = "Customers10CSaveLoadReport.txt";

    private static BistroBuilderSaveGameService saveGame;
    private static BistroBuilderAdvancedCustomerHistoryService history;
    private static RestaurantServiceStateService serviceState;
    private static BistroBuilderAdvancedCustomerHistorySnapshot original;
    private static BistroBuilderAdvancedCustomerHistorySnapshot expected;
    private static int slot = -1;

    static BistroBuilderAdvancedCustomers10CSaveLoadPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Customers/10C - SaveLoad real", false, 10023)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El Save/Load 10C ya está ejecutándose.");
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
        history = Find<BistroBuilderAdvancedCustomerHistoryService>();
        serviceState = Find<RestaurantServiceStateService>();
        if (saveGame == null || history == null || serviceState == null)
        {
            Finish(false, "10C SaveLoad: faltan autoridades runtime.", commandLine);
            return;
        }

        saveGame.RefreshExtensions();
        string historyError = string.Empty;
        bool valid = history.ValidateConfiguration(out historyError);
        if (!serviceState.IsClosed || !valid ||
            !saveGame.HasProvider(BistroBuilderAdvancedCustomerHistorySaveSectionProvider.StableSectionId))
        {
            Finish(false,
                "10C SaveLoad: configuración inválida. " + historyError,
                commandLine);
            return;
        }
        if (!TryFindFreeSlot(out slot))
        {
            Finish(false, "10C SaveLoad: no hay slot diagnóstico libre.", commandLine);
            return;
        }

        original = history.CreateSnapshot();
        string fixtureError = string.Empty;
        string restoreError = string.Empty;
        if (!TryBuildFixture(out expected, out fixtureError) ||
            !history.TryRestoreSnapshot(expected, out restoreError))
        {
            Finish(false,
                "10C SaveLoad: no pudo preparar historial. " +
                fixtureError + " " + restoreError,
                commandLine);
            return;
        }

        saveGame.OperationCompleted -= HandleOperationCompleted;
        saveGame.OperationCompleted += HandleOperationCompleted;
        SessionState.SetString(StageKey, commandLine ? "save_cli" : "save_menu");
        if (!saveGame.TrySaveSlot(slot, "BB 10C CUSTOMER HISTORY TEST", out string rejection))
            FailAndCleanup("10C SaveLoad: Save rechazado. " + rejection, commandLine);
    }

    private static void HandleOperationCompleted(BistroBuilderSaveOperationResult result)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
        if (result == null || !result.Succeeded)
        {
            FailAndCleanup(
                "10C SaveLoad: operación falló. " +
                (result != null ? result.Message : "resultado nulo"),
                commandLine);
            return;
        }

        if (stage.StartsWith("save_", StringComparison.Ordinal))
        {
            if (!history.TryResetForLegacyLoad(out string resetError) ||
                history.CustomerRecordCount != 0)
            {
                FailAndCleanup(
                    "10C SaveLoad: no pudo destruir el historial antes de Load. " +
                    resetError, commandLine);
                return;
            }
            SessionState.SetString(StageKey, commandLine ? "load_cli" : "load_menu");
            if (!saveGame.TryLoadSlot(slot, out string rejection))
                FailAndCleanup("10C SaveLoad: Load rechazado. " + rejection, commandLine);
            return;
        }

        if (stage.StartsWith("load_", StringComparison.Ordinal))
        {
            BistroBuilderAdvancedCustomerHistorySnapshot loaded = history.CreateSnapshot();
            bool restored = loaded != null && expected != null &&
                JsonUtility.ToJson(loaded) == JsonUtility.ToJson(expected) &&
                loaded.customers.Count == 1 &&
                loaded.customers[0].visitCount == 1 &&
                loaded.customers[0].lifetimeSpendCents == 12345L;
            if (!restored)
            {
                FailAndCleanup(
                    "10C SaveLoad: Load no restauró exactamente advanced_customers.state.",
                    commandLine);
                return;
            }

            SessionState.SetString(StageKey, commandLine ? "delete_cli" : "delete_menu");
            if (!saveGame.TryDeleteSlot(slot, out string rejection))
                FailAndCleanup(
                    "10C SaveLoad: no pudo limpiar el slot. " + rejection,
                    commandLine);
            return;
        }

        if (stage.StartsWith("delete_", StringComparison.Ordinal))
        {
            if (!RestoreOriginal(out string restoreError))
            {
                Finish(false,
                    "10C SaveLoad: pasó pero no restauró el fixture. " + restoreError,
                    commandLine);
                return;
            }
            Finish(true,
                "PASS — advanced_customers.state sobrevive Save -> destrucción -> Load " +
                "sin perder visitas, gasto ni fidelidad.",
                commandLine);
            return;
        }

        if (stage.StartsWith("cleanup_", StringComparison.Ordinal))
        {
            string failure = SessionState.GetString(FailureKey, "10C SaveLoad falló.");
            RestoreOriginal(out string restoreError);
            if (!string.IsNullOrWhiteSpace(restoreError))
                failure += " Restauración: " + restoreError;
            Finish(false, failure, commandLine);
        }
    }

    private static bool TryBuildFixture(
        out BistroBuilderAdvancedCustomerHistorySnapshot snapshot,
        out string error)
    {
        snapshot = BistroBuilderAdvancedCustomerHistoryEngine.CreateEmptySnapshot();
        var outcome = new BistroBuilderAdvancedCustomerVisitOutcome
        {
            groupId = 990001,
            experienceId = "visit.day7.group990001",
            segmentId = "general",
            dayIndex = 7,
            paidAmountCents = 12345L,
            overallSatisfactionBasisPoints = 8200,
            waitingScoreBasisPoints = 7600,
            foodQualityScoreBasisPoints = 8800,
            valueForMoneyScoreBasisPoints = 8100
        };
        if (!BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterOutcome(
                snapshot, outcome, out var afterOutcome, out _, out error))
            return false;

        var assignment = new BistroBuilderAdvancedCustomerCohortAssignment
        {
            groupId = 990001,
            cohortId = "guest.cohort.990001",
            segmentId = "general",
            dayIndex = 7
        };
        if (!BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterAssignment(
                afterOutcome, assignment, out snapshot, out _, out error))
            return false;

        return BistroBuilderAdvancedCustomerHistoryEngine.TryValidateSnapshot(
            snapshot, out error);
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
        RestoreOriginal(out string restoreError);
        if (!string.IsNullOrWhiteSpace(restoreError))
            message += " Restauración: " + restoreError;
        Finish(false, message, commandLine);
    }

    private static bool RestoreOriginal(out string error)
    {
        if (original == null)
        {
            error = string.Empty;
            return true;
        }
        return history.TryRestoreSnapshot(original, out error);
    }

    private static bool TryFindFreeSlot(out int found)
    {
        found = -1;
        for (int candidate = 970; candidate <= 989; candidate++)
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
        string report = "=== BISTRO BUILDER — 10C / SAVE LOAD REAL ===\n" +
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
