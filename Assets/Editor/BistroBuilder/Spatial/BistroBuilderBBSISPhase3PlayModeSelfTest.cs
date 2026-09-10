using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BistroBuilderBBSISPhase3PlayModeSelfTest
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.BBSIS.Phase3.Play.Stage";
    private const string SuccessKey = "BB.BBSIS.Phase3.Play.Success";
    private const string ReportPath = "BBSISPhase3PlayModeReport.txt";
    private const double PlayReadyDelaySeconds = 0.25d;
    private static double playReadyAt;

    static BistroBuilderBBSISPhase3PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Bistro Builder/BBSIS/Fase 3/PlayMode real")]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);
    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "El PlayMode BBSIS Fase 3 ya esta ejecutandose.");
        File.Delete(Path.GetFullPath(ReportPath));
        playReadyAt = 0d;
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "run_cli" : "run_menu");
            playReadyAt = EditorApplication.timeSinceStartup +
                PlayReadyDelaySeconds;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            playReadyAt = 0d;
            if (cli) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying)
            return;
        string stage =
            SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith(
                "run_", StringComparison.Ordinal))
            return;
        if (playReadyAt <= 0d)
            playReadyAt = EditorApplication.timeSinceStartup +
                PlayReadyDelaySeconds;
        if (EditorApplication.timeSinceStartup < playReadyAt)
            return;
        playReadyAt = double.MaxValue;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        try
        {
            RunRuntimeProbe();
            Finish(
                true,
                "PASS - determinismo, reset observable y stress denso " +
                "funcionan en runtime.",
                cli);
        }
        catch (Exception exception)
        {
            Finish(false, "BBSIS Fase 3 PlayMode: " + exception.Message, cli);
        }
    }

    private static void RunRuntimeProbe()
    {
        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderSpatialAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialAssessmentService>();
        if (spatial == null || assessment == null)
            throw new InvalidOperationException("Faltan autoridades BBSIS Fase 3.");
        spatial.ResetTransientRuntimeStateAfterLoad();
        List<string> released = new List<string>(128);
        spatial.LeaseReleased += released.Add;
        Vector3 origin = new Vector3(28000f, 0f, 28000f);

        BistroBuilderSpatialLease first = Acquire(
            spatial, "phase3.play.first", origin + Vector3.left * 0.35f);
        BistroBuilderSpatialLease second = Acquire(
            spatial, "phase3.play.second", origin + Vector3.right * 0.35f);
        var probe = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "phase3.play.probe",
            kind = BistroBuilderSpatialClaimKind.Mobility,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Circle(origin, 0.25f),
            durationSeconds = 5f
        };
        if (spatial.TryAcquireLease(
                probe, out _, out BistroBuilderSpatialLeaseDecision decision) ||
            first == null || second == null ||
            !string.Equals(
                decision.blockingLeaseId, first.leaseId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "El blocker elegido no fue determinista.");
        if (!spatial.TryBeginEpisode(
                "phase3.play.episode.owner",
                "phase3.play.reset",
                string.Empty,
                out BistroBuilderSpatialEpisode episode))
            throw new InvalidOperationException("No se creo episodio de reset.");
        int leasesBeforeReset = spatial.ActiveLeaseCount;
        released.Clear();
        spatial.ResetTransientRuntimeStateAfterLoad();
        if (spatial.ActiveLeaseCount != 0 ||
            spatial.ActiveEpisodeCount != 0 ||
            released.Count != leasesBeforeReset ||
            episode.state != BistroBuilderSpatialEpisodeState.Cancelled ||
            !IsSorted(released))
            throw new InvalidOperationException(
                "Reset runtime no fue observable y determinista.");

        const int side = 8;
        List<string> dense = new List<string>(side * side);
        Vector3 denseOrigin = new Vector3(30000f, 0f, 30000f);
        for (int z = 0; z < side; z++)
            for (int x = 0; x < side; x++)
            {
                BistroBuilderSpatialLease lease = Acquire(
                    spatial,
                    "phase3.play.dense." + (z * side + x),
                    denseOrigin + new Vector3(x, 0f, z));
                if (lease == null)
                    throw new InvalidOperationException("Stress denso no concedido.");
                dense.Add(lease.leaseId);
            }
        if (spatial.ActiveLeaseCount != side * side)
            throw new InvalidOperationException("Conteo denso incorrecto.");
        for (int i = 0; i < dense.Count; i++)
            spatial.ReleaseLease(dense[i]);
        if (spatial.ActiveLeaseCount != 0)
            throw new InvalidOperationException("Stress denso dejo leases huerfanos.");

        BistroBuilderSpatialQualityResult firstQuality =
            assessment.EvaluateCurrentLayout();
        List<string> firstLedger = Snapshot(assessment.LastLedger);
        BistroBuilderSpatialQualityResult secondQuality =
            assessment.EvaluateCurrentLayout();
        List<string> secondLedger = Snapshot(assessment.LastLedger);
        if (firstQuality == null || secondQuality == null ||
            !Mathf.Approximately(firstQuality.quality, secondQuality.quality) ||
            !Equal(firstLedger, secondLedger))
            throw new InvalidOperationException(
                "Spatial Quality o Bottleneck Ledger no son repetibles.");
        spatial.LeaseReleased -= released.Add;
    }

    private static BistroBuilderSpatialLease Acquire(
        BistroBuilderSpatialInteractionService spatial,
        string owner,
        Vector3 point)
    {
        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = owner,
            kind = BistroBuilderSpatialClaimKind.Mobility,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Circle(point, 0.2f),
            durationSeconds = 10f
        };
        return spatial.TryAcquireLease(
            request,
            out BistroBuilderSpatialLease lease,
            out _)
            ? lease
            : null;
    }

    private static bool IsSorted(List<string> values)
    {
        for (int i = 1; i < values.Count; i++)
            if (string.CompareOrdinal(values[i - 1], values[i]) > 0)
                return false;
        return true;
    }

    private static List<string> Snapshot(
        BistroBuilderSpatialBottleneckLedger ledger)
    {
        List<string> values = new List<string>();
        if (ledger == null || ledger.records == null) return values;
        for (int i = 0; i < ledger.records.Count; i++)
            if (ledger.records[i] != null)
                values.Add(ledger.records[i].bottleneckId);
        return values;
    }

    private static bool Equal(List<string> first, List<string> second)
    {
        if (first.Count != second.Count) return false;
        for (int i = 0; i < first.Count; i++)
            if (!string.Equals(
                    first[i], second[i], StringComparison.Ordinal))
                return false;
        return true;
    }

    private static void Finish(bool success, string message, bool cli)
    {
        SessionState.SetBool(SuccessKey, success);
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 3 / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report);
        else Debug.LogError(report);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
