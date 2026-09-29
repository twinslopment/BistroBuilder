using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BistroBuilderBBSISPhase2BPlayModeSelfTest
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey =
        "BB.BBSIS.Phase2B.Play.Stage";
    private const string SuccessKey =
        "BB.BBSIS.Phase2B.Play.Success";
    private const string ReportPath =
        "BBSISPhase2BPlayModeReport.txt";
    private const double PlayReadyDelaySeconds = 0.25d;
    private static double playReadyAt;

    static BistroBuilderBBSISPhase2BPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -=
            OnPlayModeChanged;
        EditorApplication.playModeStateChanged +=
            OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem(
        "Bistro Builder/BBSIS/Fase 2B/PlayMode real")]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "El PlayMode BBSIS 2B ya está ejecutándose.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(
            StageKey,
            cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(
            ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(
        PlayModeStateChange state)
    {
        string stage = SessionState.GetString(
            StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith(
                "cli", StringComparison.Ordinal);
            SessionState.SetString(
                StageKey,
                cli ? "run_cli" : "run_menu");
            playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
        }
        else if (state ==
                 PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains(
                "cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(
                SuccessKey, false);
            SessionState.EraseString(StageKey);
            if (cli)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        string stage = SessionState.GetString(
            StageKey, string.Empty);
        if (!stage.StartsWith(
                "run_", StringComparison.Ordinal))
            return;
        bool cli = stage.EndsWith(
            "cli", StringComparison.Ordinal);
        if (cli) EditorApplication.QueuePlayerLoopUpdate();
        if (playReadyAt <= 0d)
            playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
        if (EditorApplication.timeSinceStartup < playReadyAt) return;
        try
        {
            RunRuntimeProbe();
            Finish(
                true,
                "PASS - cocina, pass y barra reales publican " +
                "semántica operacional; Work/Service/Transfer " +
                "Leases bloquean conflictos y se reconstruyen.",
                cli);
        }
        catch (Exception exception)
        {
            Finish(
                false,
                "BBSIS Fase 2B PlayMode: " +
                exception.Message,
                cli);
        }
    }

    private static void RunRuntimeProbe()
    {
        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderOperationalSpatialCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderOperationalSpatialCoordinator>();
        BistroBuilderKitchenSpatialAdapter kitchen =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderKitchenSpatialAdapter>();
        BistroBuilderSpatialAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialAssessmentService>();
        if (spatial == null || coordinator == null ||
            kitchen == null || assessment == null)
            throw new InvalidOperationException(
                "Faltan servicios BBSIS 2B.");

        if (!coordinator.ValidateConfiguration(out string error))
            throw new InvalidOperationException(error);

        spatial.ResetTransientRuntimeStateAfterLoad();
        ValidateKitchenWork(
            spatial, coordinator);
        ValidatePass(
            spatial, coordinator);
        ValidateBarSemantics();
        ValidateQuality(assessment);

        spatial.ResetTransientRuntimeStateAfterLoad();
        coordinator.ReconcileOperationalClaims();
        if (spatial.ActiveEpisodeCount != 0)
            throw new InvalidOperationException(
                "La reconstrucción creó Episodes huérfanos.");
        spatial.ResetTransientRuntimeStateAfterLoad();
    }

    private static void ValidateKitchenWork(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderOperationalSpatialCoordinator coordinator)
    {
        if (!coordinator.TryAcquireKitchenWork(
                "station.range",
                "phase2b.runtime.first",
                0,
                out string rejection))
            throw new InvalidOperationException(
                "Work Lease runtime rechazado: " + rejection);

        if (coordinator.TryAcquireKitchenWork(
                "station.range",
                "phase2b.runtime.blocked",
                0,
                out _))
            throw new InvalidOperationException(
                "Dos trabajos compartieron el mismo Work Port.");

        if (spatial.CountLeases(
                BistroBuilderSpatialClaimKind.Work) != 1)
            throw new InvalidOperationException(
                "Work Lease no registrado.");

        coordinator.ReleaseKitchenWork(
            "phase2b.runtime.first");
        if (!coordinator.TryAcquireKitchenWork(
                "station.range",
                "phase2b.runtime.blocked",
                0,
                out _))
            throw new InvalidOperationException(
                "El Work Port no se liberó.");
        coordinator.ReleaseKitchenWork(
            "phase2b.runtime.blocked");
    }

    private static void ValidatePass(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderOperationalSpatialCoordinator coordinator)
    {
        if (!coordinator.TryAcquirePassTransfer(
                "phase2b.runtime.pass.a",
                1f,
                out BistroBuilderSpatialLeaseDecision first) ||
            !first.granted)
            throw new InvalidOperationException(
                "El pass no concede transferencia.");

        if (coordinator.TryAcquirePassTransfer(
                "phase2b.runtime.pass.b",
                1f,
                out _))
            throw new InvalidOperationException(
                "El pass aceptó transferencias incompatibles.");

        coordinator.ReleaseOperationalOwner(
            "phase2b.runtime.pass.a");
        if (spatial.CountLeases(
                BistroBuilderSpatialClaimKind.Transfer) != 0)
            throw new InvalidOperationException(
                "El pass no liberó su lease.");
    }

    private static void ValidateBarSemantics()
    {
        BistroBuilderBarSpatialAdapter[] adapters =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderBarSpatialAdapter>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.InstanceID);
        if (adapters.Length == 0)
            throw new InvalidOperationException(
                "No hay barra real vinculada.");
        for (int i = 0; i < adapters.Length; i++)
        {
            var volumes =
                new List<BistroBuilderSpatialSemanticVolume>();
            if (adapters[i].WriteSemanticVolumes(volumes) != 3)
                throw new InvalidOperationException(
                    "Una plaza de barra no publica 3 zonas.");
        }
    }

    private static void ValidateQuality(
        BistroBuilderSpatialAssessmentService assessment)
    {
        BistroBuilderSpatialQualityResult first =
            assessment.EvaluateCurrentLayout();
        int count = first != null
            ? first.diagnostics.Count
            : -1;
        float quality = first != null
            ? first.quality
            : -1f;
        BistroBuilderSpatialQualityResult second =
            assessment.EvaluateCurrentLayout();
        if (first == null || second == null ||
            assessment.LastLedger == null ||
            count != second.diagnostics.Count ||
            !Mathf.Approximately(
                quality, second.quality))
            throw new InvalidOperationException(
                "Spatial Quality operacional no es estable.");
    }

    private static void Finish(
        bool success,
        string message,
        bool cli)
    {
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 2B / " +
            "PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") +
            message;
        File.WriteAllText(
            Path.GetFullPath(ReportPath), report);
        if (success)
            Debug.Log(report);
        else
            Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(
            StageKey,
            cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
