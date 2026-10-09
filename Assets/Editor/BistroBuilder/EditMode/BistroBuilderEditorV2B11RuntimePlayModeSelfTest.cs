using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>B11 acceptance in a running restaurant. Never saves a source scene.</summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2B11RuntimePlayModeSelfTest
{
    private const string Scene = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string Stage = "BB.B11.RunStage";
    private const string Result = "BB.B11.Result";
    private const string Report = "EditorV2_B11_RuntimePlay_Report.txt";

    static BistroBuilderEditorV2B11RuntimePlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= Changed;
        EditorApplication.playModeStateChanged += Changed;
    }

    [MenuItem("Bistro Builder/QA/Editor V2/B11 Runtime Play Test")]
    public static void RunFromMenu() => Launch(false);

    public static void RunFromCommandLine() => Launch(true);

    private static void Launch(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Editor already entering Play Mode");
        File.WriteAllText(Path.GetFullPath(Report),
            "B11 Play Mode runtime test started.\n");
        SessionState.SetBool(Result, false);
        SessionState.SetString(Stage, cli ? "enter_cli" : "enter_manual");
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void Changed(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(Stage, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var host = new GameObject("__B11_DiagnosticPlayRunner");
            host.AddComponent<BistroBuilderEditorV2B11RuntimeDriver>();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(Stage);
            if (stage.EndsWith("cli", StringComparison.Ordinal))
                EditorApplication.Exit(SessionState.GetBool(Result, false) ? 0 : 1);
        }
    }

    public static void Finish(bool passed, string info)
    {
        string stage = SessionState.GetString(Stage, string.Empty);
        string line = (passed ? "PASS " : "FAIL ") + info;
        File.WriteAllText(Path.GetFullPath(Report),
            "B11 REAL RESTAURANT PLAY MODE\n" + line + "\n");
        if (passed) Debug.Log("[B11 PLAY] " + line);
        else Debug.LogError("[B11 PLAY] " + line);
        SessionState.SetBool(Result, passed);
        SessionState.SetString(Stage, stage.EndsWith("cli", StringComparison.Ordinal)
            ? "exit_cli" : "exit_manual");
        EditorApplication.ExitPlaymode();
    }
}

public sealed class BistroBuilderEditorV2B11RuntimeDriver : MonoBehaviour
{
    private IEnumerator Start()
    {
        for (int frame = 0; frame < 12; frame++) yield return null;
        var diagnostic = Object.FindFirstObjectByType<
            BistroBuilderEditorV2DiagnosisService>(FindObjectsInactive.Include);
        var edit = Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var placed = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
            FindObjectsInactive.Include);
        var footprints = Object.FindFirstObjectByType<RestaurantPlacementRegistry>(
            FindObjectsInactive.Include);
        var history = Object.FindFirstObjectByType<RestaurantPlacementHistoryService>(
            FindObjectsInactive.Include);
        var seats = Object.FindFirstObjectByType<RestaurantSeatRegistry>(
            FindObjectsInactive.Include);
        var overlay = Object.FindFirstObjectByType<
            BistroBuilderEditorV2DiagnosisOverlay>(FindObjectsInactive.Include);
        if (diagnostic == null || edit == null || placed == null ||
            footprints == null || history == null || seats == null ||
            overlay == null)
        {
            BistroBuilderEditorV2B11RuntimePlayModeSelfTest.Finish(false,
                "Missing runtime B11 authorities");
            yield break;
        }

        if (!edit.IsEditModeActive &&
            !edit.TryEnterEditMode(out _, out string enterError))
        {
            BistroBuilderEditorV2B11RuntimePlayModeSelfTest.Finish(false,
                "Cannot enter edit: " + enterError);
            yield break;
        }
        var before = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var obj in placed.RegisteredPlaceables)
            if (obj != null)
                before[obj.InstanceId] = obj.ItemDefinition.ItemId + "|" +
                    obj.transform.position.ToString("F4") + "|" +
                    obj.transform.rotation.eulerAngles.ToString("F3");
        if (before.Count < 20 || footprints.RegisteredFootprintCount < 20)
        {
            BistroBuilderEditorV2B11RuntimePlayModeSelfTest.Finish(false,
                "Real placement registry not ready: placed=" + before.Count +
                " footprints=" + footprints.RegisteredFootprintCount);
            yield break;
        }

        int historyBefore = history.UndoCount;
        int seatsBefore = seats.RegisteredSeatCount;
        int scanBefore = diagnostic.ScanCount;
        var qualityBefore = Object.FindFirstObjectByType<
            BistroBuilderSpatialInteractionService>(FindObjectsInactive.Include);
        int revisionBefore = qualityBefore != null ? qualityBefore.Revision : -1;

        var timer = System.Diagnostics.Stopwatch.StartNew();
        bool ok = diagnostic.TryScan(BistroBuilderEditorV2DiagnosisLayer.All,
            out var report, out string error);
        timer.Stop();
        long firstScanMs = timer.ElapsedMilliseconds;
        if (!ok || report == null)
        {
            BistroBuilderEditorV2B11RuntimePlayModeSelfTest.Finish(false,
                "Scan failed: " + error);
            yield break;
        }

        int examined = report.scannedObjects;
        bool startedProgressive = report.navigationPending;
        yield return null;
        yield return null;
        bool noBackground = diagnostic.ScanCount == scanBefore + 1;
        int progressionFrames = 0;
        while (report.navigationPending && progressionFrames++ < 140)
            yield return null;
        bool allRoutesChecked = !report.navigationPending &&
            report.complete && report.scannedRoutes >= 20;
        int found = report.findings.Count;
        var routeAuthority = Object.FindFirstObjectByType<
            BistroBuilderNavigationService>(FindObjectsInactive.Include);
        if (routeAuthority != null)
        {
            string timingFile = System.IO.Path.GetFullPath(
                "EditorV2_B11_NavRouteTimings.txt");
            System.IO.File.WriteAllLines(timingFile,
                routeAuthority.B11RouteTimings);
        }

        bool single = diagnostic.TryScan(
            BistroBuilderEditorV2DiagnosisLayer.Layout,
            out var layout, out error);
        bool onlyLayout = single && layout != null &&
            layout.scannedLayers == BistroBuilderEditorV2DiagnosisLayer.Layout;
        if (onlyLayout)
            foreach (var item in layout.findings)
                onlyLayout &= item.layer == BistroBuilderEditorV2DiagnosisLayer.Layout;
        bool noMutation = history.UndoCount == historyBefore &&
            seats.RegisteredSeatCount == seatsBefore &&
            placed.RegisteredPlaceables.Count == before.Count &&
            (qualityBefore == null || qualityBefore.Revision == revisionBefore);
        foreach (var obj in placed.RegisteredPlaceables)
        {
            if (obj == null ||
                !before.TryGetValue(obj.InstanceId, out string state) ||
                state != obj.ItemDefinition.ItemId + "|" +
                    obj.transform.position.ToString("F4") + "|" +
                    obj.transform.rotation.eulerAngles.ToString("F3"))
                noMutation = false;
        }

        // Close during a second progressive scan: no hidden route workload
        // may survive the panel's dismissal.
        overlay.SetVisible(true);
        var cancelled = diagnostic.LastReport;
        bool cancelStarted = cancelled != null && cancelled.navigationPending;
        overlay.SetVisible(false);
        int countAfterClose = diagnostic.ScanCount;
        yield return null;
        yield return null;
        bool cancelledCleanly = cancelStarted && !overlay.IsVisible &&
            !cancelled.navigationPending && !cancelled.complete &&
            diagnostic.ScanCount == countAfterClose &&
            cancelled.scannedRoutes == 0;

        bool valid = noBackground && noMutation && onlyLayout &&
            startedProgressive && allRoutesChecked && cancelledCleanly &&
            examined >= 20 && firstScanMs < 8000;
        BistroBuilderEditorV2B11RuntimePlayModeSelfTest.Finish(valid,
            "objects=" + before.Count + " placementExamined=" + examined +
            " routes=" + report.scannedRoutes + " findings=" + found +
            " scanMs=" + firstScanMs +
            " spatialMs=" + report.spatialMilliseconds +
            " navigationMs=" + report.navigationMilliseconds +
            " placementMs=" + report.placementMilliseconds +
            " capacityMs=" + report.capacityMilliseconds +
            " architectureMs=" + report.architectureMilliseconds +
            " complete=" + report.complete +
            " noBackground=" + noBackground + " noMutation=" + noMutation +
            " progressive=" + startedProgressive +
            " fullRoutes=" + allRoutesChecked +
            " cancelWhenClosed=" + cancelledCleanly +
            " frames=" + progressionFrames +
            " layoutFilter=" + onlyLayout);
    }
}
