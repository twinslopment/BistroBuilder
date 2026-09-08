using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BistroBuilderBBSISPhase2DPlayModeSelfTest
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey =
        "BB.BBSIS.Phase2D.Play.Stage";
    private const string SuccessKey =
        "BB.BBSIS.Phase2D.Play.Success";
    private const string ReportPath =
        "BBSISPhase2DPlayModeReport.txt";

    static BistroBuilderBBSISPhase2DPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Bistro Builder/BBSIS/Fase 2D/PlayMode real")]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "El PlayMode BBSIS 2D ya esta ejecutandose.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(
            StageKey,
            cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void OnPlayModeChanged(
        PlayModeStateChange state)
    {
        string stage =
            SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage))
            return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith(
                "cli", StringComparison.Ordinal);
            SessionState.SetString(
                StageKey,
                cli ? "run_cli" : "run_menu");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains(
                "cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            if (cli)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying ||
            Time.frameCount < 10)
            return;
        string stage =
            SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith(
                "run_", StringComparison.Ordinal))
            return;
        bool cli = stage.EndsWith(
            "cli", StringComparison.Ordinal);
        try
        {
            RunRuntimeProbe();
            Finish(
                true,
                "PASS - Mobility/Carry, relocalizacion, conflictos y " +
                "reconstruccion post-Load funcionan en runtime.",
                cli);
        }
        catch (Exception exception)
        {
            Finish(
                false,
                "BBSIS Fase 2D PlayMode: " + exception.Message,
                cli);
        }
    }
    private static void RunRuntimeProbe()
    {
        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderMobilitySpatialCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderMobilitySpatialCoordinator>();
        BistroBuilderSpatialContractDefinition contract =
            Resources.Load<BistroBuilderSpatialContractDefinition>(
                "BistroBuilder/Spatial/Contracts/" +
                "BB_SpatialContract_Logistics_Cart");
        BistroBuilderMobilitySpatialProfileDefinition profile =
            Resources.Load<BistroBuilderMobilitySpatialProfileDefinition>(
                "BistroBuilder/Spatial/Profiles/" +
                "BB_MobilityProfile_Logistics_Cart");
        if (spatial == null || coordinator == null ||
            contract == null || profile == null)
            throw new InvalidOperationException(
                "Faltan dependencias BBSIS 2D.");
        if (!coordinator.ValidateConfiguration(out string error))
            throw new InvalidOperationException(error);
        BistroBuilderSupplierDeliveryPresentationService[] presentations =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderSupplierDeliveryPresentationService>(
                FindObjectsSortMode.None);
        coordinator.ReconcileNow();
        if (presentations.Length != 1 ||
            !coordinator.HasPresentationAuthority)
            throw new InvalidOperationException(
                "La autoridad runtime de logistica no quedo enlazada.");

        int leasesBefore = spatial.ActiveLeaseCount;
        GameObject source = new GameObject(
            "__BBSIS_2D_PlayCart");
        source.transform.position =
            new Vector3(14000f, 0f, 14000f);
        BistroBuilderAdaptiveSpatialProxy proxy =
            source.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
        BistroBuilderSpatialSubject subject =
            source.AddComponent<BistroBuilderSpatialSubject>();
        subject.Configure(
            "spatial.play.logistics.cart",
            contract,
            proxy);
        BistroBuilderMobileSpatialAdapter adapter =
            source.AddComponent<BistroBuilderMobileSpatialAdapter>();
        adapter.Configure(
            subject,
            profile,
            "bbsis.play.logistics.cart");
        spatial.RegisterSubject(subject);
        if (!adapter.TickSpatial(1f))
            throw new InvalidOperationException(
                "No se concedio Mobility Lease inicial.");
        adapter.SetLoadUnits(4);
        source.transform.position += Vector3.forward * 1.25f;
        if (!adapter.TickSpatial(1f) ||
            string.IsNullOrEmpty(adapter.CarryLeaseId))
            throw new InvalidOperationException(
                "No se reconstruyo Carry Envelope cargado.");
        if (spatial.ActiveLeaseCount != leasesBefore + 2)
            throw new InvalidOperationException(
                "La movilidad dejo un numero incorrecto de leases.");

        spatial.ResetTransientRuntimeStateAfterLoad();
        if (spatial.ActiveLeaseCount != 0)
            throw new InvalidOperationException(
                "Load no limpio leases moviles.");
        if (!adapter.TickSpatial(1f) ||
            spatial.ActiveLeaseCount != 2)
            throw new InvalidOperationException(
                "Load no reconstruyo movilidad y carga.");

        adapter.ReleaseSpatialState(
            BistroBuilderSpatialEpisodeState.Completed);
        UnityEngine.Object.Destroy(source);
        if (spatial.ActiveLeaseCount != 0)
            throw new InvalidOperationException(
                "La limpieza dejo leases huerfanos.");
    }

    private static void Finish(
        bool success,
        string message,
        bool cli)
    {
        SessionState.SetBool(SuccessKey, success);
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 2D / " +
            "PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") +
            message;
        File.WriteAllText(
            Path.GetFullPath(ReportPath),
            report);
        if (success)
            Debug.Log(report);
        else
            Debug.LogError(report);
        SessionState.SetString(
            StageKey,
            cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
