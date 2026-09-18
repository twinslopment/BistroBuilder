using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderEmptyPremisesPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.EmptyPremises.Play.Stage";
    private const string SuccessKey = "BB.EmptyPremises.Play.Success";
    private const string ReportPath = "EmptyPremisesPlayModeReport.txt";
    private const int DiagnosticSlot = 98;
    private static BistroBuilderNewGameOpeningService service;
    private static BistroBuilderSaveGameService save;
    private static double stageStarted;

    static BistroBuilderEmptyPremisesPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Empty premises self-test already running.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
            SessionState.SetString(StageKey, stage.EndsWith("cli", StringComparison.Ordinal) ? "setup_cli" : "setup_menu");
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            if (cli) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 6) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage) || stage.StartsWith("exit_", StringComparison.Ordinal)) return;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        try
        {
            if (stage.StartsWith("setup_", StringComparison.Ordinal))
            {
                service = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningService>();
                save = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
                string config = string.Empty;
                if (service == null || save == null || !service.ValidateConfiguration(out config))
                    throw new InvalidOperationException("Opening service unavailable: " + config);
                SetPrivate(service, "defaultSaveSlot", DiagnosticSlot);
                if (save.SlotExists(DiagnosticSlot) && !save.TryDeleteSlot(DiagnosticSlot, out string deleteError))
                    throw new InvalidOperationException("Could not clear diagnostic slot: " + deleteError);
                stageStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "wait_clean_cli" : "wait_clean_menu");
                return;
            }
            if (stage.StartsWith("wait_clean_", StringComparison.Ordinal))
            {
                if (save.IsBusy) { Timeout(8d, "initial cleanup"); return; }
                if (!service.TryCreateNewGame("Empty Premises Test", BistroBuilderStartingPremisesProfile.Empty, out string error))
                    throw new InvalidOperationException("Empty new game failed: " + error);
                stageStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "verify_cli" : "verify_menu");
                return;
            }
            if (stage.StartsWith("verify_", StringComparison.Ordinal))
            {
                if (save.IsBusy) { Timeout(15d, "initial empty save"); return; }
                if (service.Phase != BistroBuilderNewGamePhase.InitialSetup ||
                    service.PremisesProfile != BistroBuilderStartingPremisesProfile.Empty)
                    throw new InvalidOperationException("Empty profile did not enter initial design.");

                RestaurantPlaceableObject[] placeables = UnityEngine.Object.FindObjectsByType<RestaurantPlaceableObject>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (placeables.Length != 0)
                    throw new InvalidOperationException("Empty premises retained " + placeables.Length + " active placeable(s).");

                BistroBuilder367HInstalledFixture[] fixtures = UnityEngine.Object.FindObjectsByType<BistroBuilder367HInstalledFixture>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < fixtures.Length; i++)
                    if (fixtures[i] != null && fixtures[i].GetComponent<RestaurantPlaceableObject>() == null)
                        throw new InvalidOperationException("Empty premises retained fixed fixture " + fixtures[i].name + ".");

                BistroBuilderEditDocumentRuntimeService documents =
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>();
                BistroBuilderEditDocument layout = documents != null ? documents.GetCommittedSnapshot() : null;
                if (layout == null || layout.walls.Count != 0 || layout.openings.Count != 0 ||
                    layout.rooms.Count != 0 || layout.surfaces.Count != 0 || layout.zones.Count != 0)
                    throw new InvalidOperationException("Empty premises retained authored architecture.");
                if (GameObject.Find("RestaurantEntrancePoint") == null)
                    throw new InvalidOperationException("Empty premises removed the technical entrance authority.");

                BistroBuilderStaffService staff = UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffService>();
                if (staff == null || !staff.TryGetRoleDefinition("cook", out BistroBuilderStaffRoleDefinition cook) ||
                    cook == null || !cook.active)
                    throw new InvalidOperationException("Canonical cook role is unavailable.");

                if (!save.TryDeleteSlot(DiagnosticSlot, out string cleanupError))
                    throw new InvalidOperationException("Could not delete diagnostic slot: " + cleanupError);
                stageStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "cleanup_cli" : "cleanup_menu");
                return;
            }
            if (stage.StartsWith("cleanup_", StringComparison.Ordinal))
            {
                if (save.IsBusy) { Timeout(8d, "final cleanup"); return; }
                if (save.SlotExists(DiagnosticSlot)) throw new InvalidOperationException("Diagnostic slot remained on disk.");
                Finish(true, "PASS - Empty removes all active placeables, fixed demo fixtures and authored architecture while preserving the technical entrance and canonical cook role.", cli);
            }
        }
        catch (Exception exception)
        {
            Finish(false, exception.Message, cli);
        }
    }

    private static void Timeout(double seconds, string operation)
    {
        if (EditorApplication.timeSinceStartup - stageStarted > seconds)
            throw new TimeoutException("Timeout during " + operation + ".");
    }

    private static void Finish(bool ok, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER - EMPTY PREMISES PLAY MODE ===\n" + (ok ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, ok);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) throw new MissingFieldException(target.GetType().Name, field);
        info.SetValue(target, value);
    }
}