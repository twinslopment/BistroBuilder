using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderBBSISPhase1PlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.BBSIS.Phase1.Play.Stage";
    private const string SuccessKey = "BB.BBSIS.Phase1.Play.Success";
    private const string ReportPath = "BBSISPhase1PlayModeReport.txt";

    static BistroBuilderBBSISPhase1PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Bistro Builder/BBSIS/Fase 1/PlayMode real")]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode BBSIS Fase 1 ya está ejecutándose.");
        File.Delete(Path.GetFullPath(ReportPath));
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
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            if (cli) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        try
        {
            RunRuntimeProbe();
            Finish(true,
                "PASS - BBSIS coordina Claims/Leases/Episodes y Bloque 17 consume " +
                "su semántica espacial en runtime sin duplicar la autoridad de rutas.", cli);
        }
        catch (Exception exception)
        {
            Finish(false, "BBSIS Fase 1 PlayMode: " + exception.Message, cli);
        }
    }

    private static void RunRuntimeProbe()
    {
        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        BistroBuilderNavigationService navigation =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        if (spatial == null || navigation == null)
            throw new InvalidOperationException("Faltan las autoridades BBSIS o Navegación 17.");
        if (!spatial.ValidateConfiguration(out string error))
            throw new InvalidOperationException(error);
        spatial.ResetTransientRuntimeStateAfterLoad();
        navigation.RebuildNavigationTopology();

        Vector3 preferred = new Vector3(300f, 0f, 300f);
        if (!navigation.TryReserveDestination(
                "bbsis:runtime:a", BistroBuilderNavigationAgentMask.Waiter,
                preferred, 0.3f, 40, out Vector3 reservedA) ||
            !navigation.TryReserveDestination(
                "bbsis:runtime:b", BistroBuilderNavigationAgentMask.Waiter,
                preferred, 0.3f, 40, out Vector3 reservedB))
            throw new InvalidOperationException("Bloque 17 no pudo delegar reservas de destino en BBSIS.");
        if ((reservedA - reservedB).sqrMagnitude < 0.01f)
            throw new InvalidOperationException("BBSIS permitió dos destinos reservables incompatibles.");
        if (spatial.CountLeases(BistroBuilderSpatialClaimKind.Destination) != 2 ||
            navigation.ActiveDestinationReservationCount != 2)
            throw new InvalidOperationException("La autoridad de reservas de destino está duplicada o desincronizada.");
        navigation.RefreshDestination("bbsis:runtime:a");
        navigation.ReleaseDestination("bbsis:runtime:a");
        navigation.ReleaseDestination("bbsis:runtime:b");
        if (spatial.CountLeases(BistroBuilderSpatialClaimKind.Destination) != 0)
            throw new InvalidOperationException("ReleaseDestination no liberó los Spatial Leases.");

        GameObject obstacleObject = new GameObject("__BBSIS_RuntimeSweep__");
        obstacleObject.transform.position = new Vector3(320f, 0f, 320f);
        BistroBuilderDynamicCirculationEnvelope envelope =
            obstacleObject.AddComponent<BistroBuilderDynamicCirculationEnvelope>();
        envelope.ConfigureForEditor(
            Vector3.zero, new Vector2(1f, 1f),
            BistroBuilderNavigationAgentMask.All,
            BistroBuilderDynamicSpaceKind.DoorSwing);
        envelope.SetActiveWindow("bbsis:sweep-owner", true);
        if (!envelope.HasSpatialLease ||
            spatial.CountLeases(BistroBuilderSpatialClaimKind.DynamicSweep) != 1)
            throw new InvalidOperationException("El barrido dinámico no adquirió Spatial Lease.");
        if (navigation.CanAdvance(
                "bbsis:outsider", BistroBuilderNavigationAgentMask.Waiter,
                obstacleObject.transform.position, 0.2f, 40))
            throw new InvalidOperationException("Bloque 17 ignoró un Dynamic Sweep de BBSIS.");
        if (!navigation.CanAdvance(
                "bbsis:sweep-owner", BistroBuilderNavigationAgentMask.Waiter,
                obstacleObject.transform.position, 0.2f, 40))
            throw new InvalidOperationException("El propietario quedó bloqueado por su propio Spatial Lease.");
        envelope.SetActiveWindow("bbsis:sweep-owner", false);
        UnityEngine.Object.Destroy(obstacleObject);

        if (!spatial.TryBeginEpisode(
                "bbsis:episode-owner", "runtime.probe", string.Empty,
                out BistroBuilderSpatialEpisode episode) || episode == null)
            throw new InvalidOperationException("No pudo iniciarse Spatial Episode en runtime.");
        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "bbsis:episode-owner",
            episodeId = episode.episodeId,
            kind = BistroBuilderSpatialClaimKind.Interaction,
            conflictMode = BistroBuilderSpatialConflictMode.Reservable,
            volume = BistroBuilderSpatialVolume.Circle(new Vector3(340f, 0f, 340f), 0.3f),
            durationSeconds = 10f
        };
        if (!spatial.TryAcquireLease(request, out _, out _))
            throw new InvalidOperationException("Spatial Episode no pudo adquirir su Lease.");
        if (!spatial.EndEpisode(
                episode.episodeId, BistroBuilderSpatialEpisodeState.Completed) ||
            spatial.ActiveEpisodeCount != 0 || spatial.ActiveLeaseCount != 0)
            throw new InvalidOperationException("Spatial Episode no cerró limpiamente sus Claims/Leases.");

        spatial.ResetTransientRuntimeStateAfterLoad();
        if (spatial.ActiveLeaseCount != 0 || spatial.ActiveEpisodeCount != 0)
            throw new InvalidOperationException("La reconstrucción post-Load conservó estado espacial transitorio.");
    }

    private static void Finish(bool success, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER - BBSIS FASE 1 / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
