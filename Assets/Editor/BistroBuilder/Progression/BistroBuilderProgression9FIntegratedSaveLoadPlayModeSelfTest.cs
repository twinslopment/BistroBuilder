using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderProgression9FIntegratedSaveLoadPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Progression.9F.Stage";
    private const string SuccessKey = "BB.Progression.9F.Success";
    private const string FailureKey = "BB.Progression.9F.Failure";
    private const string ReportPath = "Progression9FSaveLoadReport.txt";

    private static BistroBuilderSaveGameService saveGame;
    private static BistroBuilderUpgradeService upgrades;
    private static BistroBuilderFinanceService finance;
    private static BistroBuilderReputationService reputation;
    private static BistroBuilderGeneralGameStateService general;
    private static BistroBuilderProgressionMilestoneService milestones;
    private static BistroBuilderUpgradeEffectsService effects;
    private static RestaurantServiceStateService serviceState;

    private static BistroBuilderUpgradeSnapshot originalUpgrades;
    private static BistroBuilderFinanceSnapshot originalFinance;
    private static BistroBuilderFinanceSnapshot checkpointFinance;
    private static BistroBuilderReputationSnapshot originalReputation;
    private static BistroBuilderUpgradeSnapshot checkpointUpgrades;
    private static long checkpointBalance;
    private static string checkpointStage;
    private static int checkpointLevel;
    private static int slot = -1;
    private static string originalGameId;
    private static string originalRestaurantName;
    private static string originalCreatedUtc;
    private static int originalDayIndex;
    private static int originalYear;
    private static int originalMonth;
    private static int originalDay;
    private static string originalStage;
    private static int originalLevel;

    static BistroBuilderProgression9FIntegratedSaveLoadPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Progression/9F - SaveLoad integral", false, 9050)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 9F ya está ejecutándose.");
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
        reputation = Find<BistroBuilderReputationService>();
        general = Find<BistroBuilderGeneralGameStateService>();
        milestones = Find<BistroBuilderProgressionMilestoneService>();
        effects = Find<BistroBuilderUpgradeEffectsService>();
        serviceState = Find<RestaurantServiceStateService>();

        if (saveGame == null || upgrades == null || finance == null ||
            reputation == null || general == null || milestones == null || effects == null ||
            serviceState == null)
        {
            Finish(false, "9F: faltan autoridades runtime.", commandLine);
            return;
        }
        saveGame.RefreshExtensions();
        if (!serviceState.IsClosed ||
            !saveGame.HasProvider(BistroBuilderUpgradeSaveSectionProvider.StableSectionId))
        {
            Finish(false, "9F: SaveGame/servicio no están preparados.", commandLine);
            return;
        }
        if (!TryFindFreeSlot(out slot))
        {
            Finish(false, "9F: no hay slot diagnóstico libre 930-949.", commandLine);
            return;
        }

        originalUpgrades = upgrades.CreateSnapshot();
        originalFinance = finance.CreateSnapshot();
        originalReputation = reputation.CreateSnapshot();
        originalGameId = general.GameId;
        originalRestaurantName = general.RestaurantName;
        originalCreatedUtc = general.CreatedUtc;
        originalDayIndex = general.DayIndex;
        originalYear = general.CalendarYear;
        originalMonth = general.CalendarMonth;
        originalDay = general.CalendarDay;
        originalStage = general.ProgressionStageId;
        originalLevel = general.ProgressionLevel;

        string resetUpgradeError = string.Empty;
        string resetReputationError = string.Empty;
        bool fixturePrepared =
            upgrades.TryResetForLegacyLoad(out resetUpgradeError) &&
            reputation.TryRestoreSnapshot(
                BistroBuilderReputationEngine.CreateInitialSnapshot(),
                out resetReputationError) &&
            general.TrySetProgression("new_restaurant", 1);
        if (!fixturePrepared)
        {
            RestoreOriginalState(out _);
            Finish(false, "9F: no pudo preparar fixture. " +
                resetUpgradeError + " " + resetReputationError, commandLine);
            return;
        }

        string saleBuildError = string.Empty;
        string salePostError = string.Empty;
        BistroBuilderFinanceTransactionRequest sale;
        bool saleBuilt = BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                "order_9f_progression_001",
                BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Lunch,
                50000L,
                general.DayIndex,
                720,
                out sale,
                out saleBuildError);
        if (!saleBuilt || !finance.TryPostTransaction(sale, out _, out salePostError))
        {
            RestoreOriginalState(out _);
            Finish(false, "9F: no pudo publicar ingreso fixture. " +
                saleBuildError + " " + salePostError, commandLine);
            return;
        }

        string firstError = string.Empty;
        string secondError = string.Empty;
        bool firstPurchased = upgrades.TryPurchaseUpgrade(
            "dining.comfort_seating", out _, out firstError);
        bool secondPurchased = firstPurchased && upgrades.TryPurchaseUpgrade(
            "kitchen.prep_organization", out _, out secondError);
        if (!firstPurchased || !secondPurchased)
        {
            RestoreOriginalState(out _);
            Finish(false, "9F: no pudo comprar dos mejoras fixture. " +
                firstError + " " + secondError, commandLine);
            return;
        }

        bool automaticMilestone =
            general.ProgressionLevel == 2 &&
            string.Equals(general.ProgressionStageId,
                "restaurant.established", StringComparison.Ordinal);
        if (!automaticMilestone)
        {
            RestoreOriginalState(out _);
            Finish(false,
                "9F: dos mejoras + ingresos no activaron automáticamente el primer hito.",
                commandLine);
            return;
        }

        var adjustmentContext = new BistroBuilderPreparationDurationAdjustmentContext
        {
            canonicalOrderId = "order_9f_effect_check",
            dishId = "dish_9f",
            serviceMode = BistroBuilderServiceMode.TableService,
            mealService = BistroBuilderMealServiceAvailability.Lunch,
            baseDurationSeconds = 100f,
            minimumDurationSeconds = 1f,
            maximumDurationSeconds = 300f
        };
        if (!effects.TryGetAdjustmentBasisPoints(
                adjustmentContext, out int prepAdjustment, out string effectError) ||
            prepAdjustment >= 0 || effects.AmbienceBonusBasisPoints <= 0)
        {
            RestoreOriginalState(out _);
            Finish(false, "9F: los efectos derivados no están activos. " + effectError,
                commandLine);
            return;
        }

        checkpointUpgrades = upgrades.CreateSnapshot();
        checkpointFinance = finance.CreateSnapshot();
        checkpointBalance = finance.CurrentBalanceCents;
        checkpointStage = general.ProgressionStageId;
        checkpointLevel = general.ProgressionLevel;
        if (checkpointUpgrades == null || checkpointFinance == null)
        {
            RestoreOriginalState(out _);
            Finish(false, "9F: no pudo capturar el checkpoint previo a Save.", commandLine);
            return;
        }

        saveGame.OperationCompleted -= HandleOperationCompleted;
        saveGame.OperationCompleted += HandleOperationCompleted;
        SessionState.SetString(StageKey, commandLine ? "save_cli" : "save_menu");
        if (!saveGame.TrySaveSlot(slot, "BB 9F PROGRESSION INTEGRATED TEST",
                out string saveRejection))
        {
            FailAndCleanup("9F: Save fue rechazado. " + saveRejection, commandLine);
        }
    }

    private static void HandleOperationCompleted(BistroBuilderSaveOperationResult result)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
        if (result == null || !result.Succeeded)
        {
            FailAndCleanup("9F: operación Save/Load falló. " +
                (result != null ? result.Message : "resultado nulo"), commandLine);
            return;
        }

        if (stage.StartsWith("save_", StringComparison.Ordinal))
        {
            string upgradeResetError = string.Empty;
            string financeRestoreError = string.Empty;
            bool destroyed =
                upgrades.TryResetForLegacyLoad(out upgradeResetError) &&
                finance.TryRestoreSnapshot(originalFinance, out financeRestoreError) &&
                general.TrySetProgression("new_restaurant", 1);
            if (!destroyed || upgrades.PurchasedCount != 0 ||
                general.ProgressionLevel != 1 ||
                finance.CurrentBalanceCents == checkpointBalance)
            {
                FailAndCleanup(
                    "9F: no pudo destruir el checkpoint antes de Load. " +
                    upgradeResetError + " " + financeRestoreError,
                    commandLine);
                return;
            }

            SessionState.SetString(StageKey, commandLine ? "load_cli" : "load_menu");
            if (!saveGame.TryLoadSlot(slot, out string loadRejection))
                FailAndCleanup("9F: Load fue rechazado. " + loadRejection, commandLine);
            return;
        }

        if (stage.StartsWith("load_", StringComparison.Ordinal))
        {
            ValidateLoadedCheckpoint(commandLine);
            return;
        }

        if (stage.StartsWith("delete_", StringComparison.Ordinal))
        {
            if (!RestoreOriginalState(out string restoreError))
            {
                Finish(false,
                    "9F: el gate pasó pero no restauró el fixture original. " + restoreError,
                    commandLine);
                return;
            }
            Finish(true,
                "PASS — ingresos reales + dos mejoras activan nivel 2 automáticamente; " +
                "Save/Load restaura nivel, mejoras, ledger, saldo y efectos derivados.",
                commandLine);
            return;
        }

        if (stage.StartsWith("cleanup_", StringComparison.Ordinal))
        {
            string failure = SessionState.GetString(FailureKey, "9F falló.");
            RestoreOriginalState(out string restoreError);
            if (!string.IsNullOrWhiteSpace(restoreError))
                failure += " Restauración: " + restoreError;
            Finish(false, failure, commandLine);
        }
    }

    private static void ValidateLoadedCheckpoint(bool commandLine)
    {
        BistroBuilderUpgradeSnapshot loadedUpgrades = upgrades.CreateSnapshot();
        BistroBuilderFinanceSnapshot loadedFinance = finance.CreateSnapshot();
        bool restored = loadedUpgrades != null && loadedFinance != null &&
            checkpointUpgrades != null && checkpointFinance != null &&
            JsonUtility.ToJson(loadedUpgrades) == JsonUtility.ToJson(checkpointUpgrades) &&
            JsonUtility.ToJson(loadedFinance) == JsonUtility.ToJson(checkpointFinance) &&
            finance.CurrentBalanceCents == checkpointBalance &&
            general.ProgressionLevel == checkpointLevel &&
            string.Equals(general.ProgressionStageId, checkpointStage, StringComparison.Ordinal);
        if (!restored)
        {
            FailAndCleanup(
                "9F: Load no restauró exactamente progreso, mejoras y Finanzas.",
                commandLine);
            return;
        }

        var context = new BistroBuilderPreparationDurationAdjustmentContext
        {
            canonicalOrderId = "order_9f_after_load",
            dishId = "dish_9f",
            serviceMode = BistroBuilderServiceMode.TableService,
            mealService = BistroBuilderMealServiceAvailability.Lunch,
            baseDurationSeconds = 100f,
            minimumDurationSeconds = 1f,
            maximumDurationSeconds = 300f
        };
        if (!effects.TryGetAdjustmentBasisPoints(
                context, out int adjustment, out string effectError) ||
            adjustment >= 0 || effects.AmbienceBonusBasisPoints <= 0)
        {
            FailAndCleanup("9F: los efectos no reaparecieron tras Load. " + effectError,
                commandLine);
            return;
        }

        SessionState.SetString(StageKey, commandLine ? "delete_cli" : "delete_menu");
        if (!saveGame.TryDeleteSlot(slot, out string deleteRejection))
            FailAndCleanup("9F: no pudo limpiar el slot diagnóstico. " + deleteRejection,
                commandLine);
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
        string reputationError = string.Empty;
        bool upgradesOk = originalUpgrades == null ||
            upgrades.TryRestoreSnapshot(originalUpgrades, out upgradeError);
        bool financeOk = originalFinance == null ||
            finance.TryRestoreSnapshot(originalFinance, out financeError);
        bool reputationOk = originalReputation == null ||
            reputation.TryRestoreSnapshot(originalReputation, out reputationError);
        bool generalOk = general == null || general.TryRestoreState(
            originalGameId,
            originalRestaurantName,
            originalCreatedUtc,
            originalDayIndex,
            originalYear,
            originalMonth,
            originalDay,
            originalStage,
            originalLevel);

        error = (upgradeError + " " + financeError + " " + reputationError).Trim();
        if (!generalOk)
            error = (error + " GeneralGameState no pudo restaurarse.").Trim();
        return upgradesOk && financeOk && reputationOk && generalOk;
    }

    private static bool TryFindFreeSlot(out int found)
    {
        found = -1;
        for (int candidate = 930; candidate <= 949; candidate++)
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

        string report = "=== BISTRO BUILDER — 9F / CIERRE INTEGRAL ===\n" +
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
