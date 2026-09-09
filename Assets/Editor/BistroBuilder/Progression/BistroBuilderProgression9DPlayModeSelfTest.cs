using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderProgression9DPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Progression.9D.Play.Stage";
    private const string SuccessKey = "BB.Progression.9D.Play.Success";
    private const string ReportPath = "Progression9DPlayModeReport.txt";

    static BistroBuilderProgression9DPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Progression/9D - PlayMode real", false, 9033)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 9D ya está ejecutándose.");
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
        BistroBuilderUpgradeService upgrades = Find<BistroBuilderUpgradeService>();
        BistroBuilderUpgradeEffectsService effects = Find<BistroBuilderUpgradeEffectsService>();
        BistroBuilderOrderLineExecutionService execution =
            Find<BistroBuilderOrderLineExecutionService>();

        if (upgrades == null || effects == null || execution == null)
        {
            Finish(false, "9D: faltan autoridades runtime.", commandLine);
            return;
        }
        string effectsError = string.Empty;
        string executionError = string.Empty;
        if (!effects.ValidateConfiguration(out effectsError) ||
            !execution.ValidateConfiguration(out executionError))
        {
            Finish(false, "9D: configuración runtime inválida. " +
                effectsError + " " + executionError, commandLine);
            return;
        }

        BistroBuilderUpgradeSnapshot original = upgrades.CreateSnapshot();
        BistroBuilderUpgradeSnapshot fixture =
            BistroBuilderProgressionEngine.CreateInitialSnapshot();
        fixture.purchased.Add(P("kitchen.prep_organization"));
        fixture.purchased.Add(P("bar.storage_upgrade"));
        fixture.purchased.Add(P("ambience.lighting_plan"));
        fixture.purchased.Add(P("infrastructure.storage_efficiency"));

        if (!upgrades.TryRestoreSnapshot(fixture, out string restoreFixtureError))
        {
            Finish(false, "9D: no pudo preparar fixture runtime. " +
                restoreFixtureError, commandLine);
            return;
        }

        var tableContext = new BistroBuilderPreparationDurationAdjustmentContext
        {
            canonicalOrderId = "test.order",
            customerGroupReferenceId = "group.1",
            acquisitionSegmentId = "general",
            dishId = "dish.test",
            serviceMode = BistroBuilderServiceMode.TableService,
            mealService = BistroBuilderMealServiceAvailability.Lunch,
            baseDurationSeconds = 10f,
            minimumDurationSeconds = 1f,
            maximumDurationSeconds = 30f
        };
        var barContext = new BistroBuilderPreparationDurationAdjustmentContext
        {
            canonicalOrderId = "test.order.bar",
            customerGroupReferenceId = "group.2",
            acquisitionSegmentId = "general",
            dishId = "dish.test",
            serviceMode = BistroBuilderServiceMode.BarService,
            mealService = BistroBuilderMealServiceAvailability.Dinner,
            baseDurationSeconds = 10f,
            minimumDurationSeconds = 1f,
            maximumDurationSeconds = 30f
        };

        bool tableOk = effects.TryGetAdjustmentBasisPoints(
            tableContext, out int tableAdjustment, out string tableError);
        bool barOk = effects.TryGetAdjustmentBasisPoints(
            barContext, out int barAdjustment, out string barError);
        bool runtimeOk = tableOk && barOk &&
            tableAdjustment < 0 && barAdjustment < tableAdjustment &&
            effects.AmbienceBonusBasisPoints > 0 &&
            effects.FoodQualityPotentialBonusBasisPoints > 0;

        string restoreOriginalError = string.Empty;
        bool restored = upgrades.TryRestoreSnapshot(original, out restoreOriginalError);
        if (!runtimeOk || !restored)
        {
            Finish(false,
                "9D: los efectos runtime no coinciden con el catálogo. " +
                tableError + " " + barError + " " + restoreOriginalError,
                commandLine);
            return;
        }

        Finish(true,
            "PASS — cocina, barra, ambiente e infraestructura reaccionan " +
            "a mejoras adquiridas y el estado original se restaura.",
            commandLine);
    }

    private static BistroBuilderPurchasedUpgradeRecord P(string id)
    {
        return new BistroBuilderPurchasedUpgradeRecord
        {
            upgradeId = id,
            purchasedDayIndex = 1,
            paidCents = 1
        };
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        string report = "=== BISTRO BUILDER — 9D / PLAY MODE REAL ===\n" +
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
