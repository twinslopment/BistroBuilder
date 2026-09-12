using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Measures real deletion and save/load, including deferred work on subsequent frames.</summary>
[InitializeOnLoad]
public static class BistroBuilderEditPerformanceProbe
{
    private const string Armed = "BB.EditPerformance.Armed";
    private static BistroBuilderNavigationService navigation;
    private static BistroBuilderSaveGameService save;
    private static RestaurantPlaceableRegistry registry;
    private static RestaurantEditInteractionController edit;
    private static int stage, slot, lastFrame, frames, revisionBefore, healthBefore;
    private static double deadline, stageStarted;
    private static string label;
    private static readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
    private static Report report;
    [Serializable] private sealed class Report
    {
        public string result, error;
        public double topologyMs, assessmentMs, seatingMs, fullHealthMs;
        public double deleteCallMs, deleteMaxFrameMs, loadMs, loadMaxFrameMs;
        public int deleteTopologyBuilds, deleteHealthEvaluations, loadTopologyBuilds, loadHealthEvaluations;
    }

    static BistroBuilderEditPerformanceProbe() { EditorApplication.playModeStateChanged += State; }
    public static void RunBatch()
    {
        SessionState.SetBool(Armed, true);
        SessionState.SetBool(Armed + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Armed, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            label = Array.IndexOf(Environment.GetCommandLineArgs(), "-bistroPerfFinal") >= 0 ? "final" : "baseline";
            stage = frames = slot = 0; lastFrame = -1; report = new Report();
            clock.Restart(); deadline = 600; stageStarted = 0;
            Application.runInBackground = true;
            EditorApplication.update += Tick;
        }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Armed, false);
            EditorApplication.Exit(SessionState.GetBool(Armed + ".Pass", false) ? 0 : 1);
        }
    }
    private static double Measure(Action action)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew(); action(); return timer.Elapsed.TotalMilliseconds;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            Check(clock.Elapsed.TotalSeconds < deadline, "Timeout");
            switch (stage)
            {
                case 0:
                    if (++frames < 20) return;
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                    navigation = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
                    save = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
                    registry = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>();
                    edit = UnityEngine.Object.FindFirstObjectByType<RestaurantEditInteractionController>();
                    report.topologyMs = Measure(navigation.RebuildNavigationTopology);
                    report.assessmentMs = Measure(() => UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialAssessmentService>().EvaluateCurrentLayout());
                    report.seatingMs = Measure(() => UnityEngine.Object.FindFirstObjectByType<RestaurantSeatingTopologyService>().RebuildImmediately());
                    report.fullHealthMs = Measure(() => navigation.EvaluateCirculationHealth());
                    Debug.Log("BB_PERF_COMPONENTS " + JsonUtility.ToJson(report));
                    for (int candidate = 990; candidate >= 980; candidate--)
                        if (!save.SlotExists(candidate)) { slot = candidate; break; }
                    Check(slot > 0, "No free diagnostic slot");
                    Check(save.TrySaveSlot(slot, "Edit performance temporary", out string saveError), saveError);
                    stage++; break;
                case 1:
                    if (save.IsBusy) return;
                    Check(save.LastResult != null && save.LastResult.Succeeded, "Save baseline failed");
                    Check(edit.TryEnterEditMode(), "Enter edit mode");
                    RestaurantPlaceableObject chair = null;
                    foreach (var item in registry.RegisteredPlaceables)
                        if (item != null && item.TryGetComponent<RestaurantSeat>(out _)) { chair = item; break; }
                    Check(chair != null, "No chair");
                    revisionBefore = navigation.TopologyBuildCount; healthBefore = navigation.HealthEvaluationCount;
                    report.deleteCallMs = Measure(() => Check(UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableDeletionService>().TryDelete(chair, out var deletion), deletion.Message));
                    stageStarted = clock.Elapsed.TotalSeconds; frames = 0; stage++; break;
                case 2:
                    report.deleteMaxFrameMs = Math.Max(report.deleteMaxFrameMs, Time.unscaledDeltaTime * 1000d);
                    if (++frames < 30 || clock.Elapsed.TotalSeconds - stageStarted < 1) return;
                    report.deleteTopologyBuilds = navigation.TopologyBuildCount - revisionBefore;
                    report.deleteHealthEvaluations = navigation.HealthEvaluationCount - healthBefore;
                    Check(edit.TryExitEditMode(true), "Exit edit mode");
                    revisionBefore = navigation.TopologyBuildCount; healthBefore = navigation.HealthEvaluationCount;
                    Check(save.TryLoadSlot(slot, out string loadError), loadError);
                    stageStarted = clock.Elapsed.TotalSeconds; stage++; break;
                case 3:
                    report.loadMaxFrameMs = Math.Max(report.loadMaxFrameMs, Time.unscaledDeltaTime * 1000d);
                    if (save.IsBusy) return;
                    Check(save.LastResult != null && save.LastResult.Succeeded, "Load failed: " + save.LastResult?.Message);
                    report.loadMs = save.LastResult.DurationMilliseconds;
                    Check(registry.RegisteredPlaceables.Count == 38, "Load did not restore all furniture");
                    frames = 0; stage++; break;
                case 4:
                    report.loadMaxFrameMs = Math.Max(report.loadMaxFrameMs, Time.unscaledDeltaTime * 1000d);
                    if (++frames < 20) return;
                    report.loadTopologyBuilds = navigation.TopologyBuildCount - revisionBefore;
                    report.loadHealthEvaluations = navigation.HealthEvaluationCount - healthBefore;
                    Check(save.TryDeleteSlot(slot, out string deleteError), deleteError); stage++; break;
                case 5:
                    if (save.IsBusy) return;
                    Check(!save.SlotExists(slot), "Temporary slot not cleaned");
                    if (label == "final")
                    {
                        Check(report.deleteHealthEvaluations == 0, "Deletion ran full circulation diagnostics");
                        Check(report.loadHealthEvaluations == 0, "Loading ran full circulation diagnostics");
                        Check(report.deleteMaxFrameMs < 250, "Deletion frame exceeded 250 ms");
                        Check(report.loadMaxFrameMs < 250, "Loading frame exceeded 250 ms");
                        Check(report.loadMs < 5000, "Load exceeded five seconds");
                    }
                    Finish(true, ""); break;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private static void Finish(bool pass, string error)
    {
        EditorApplication.update -= Tick;
        report.result = pass ? "PASS" : "FAIL"; report.error = error;
        string json = JsonUtility.ToJson(report, true);
        File.WriteAllText("Logs/EditPerformance-" + label + ".json", json);
        Debug.Log("BB_EDIT_PERFORMANCE_" + label + " " + json);
        SessionState.SetBool(Armed + ".Pass", pass); EditorApplication.isPlaying = false;
    }
}
