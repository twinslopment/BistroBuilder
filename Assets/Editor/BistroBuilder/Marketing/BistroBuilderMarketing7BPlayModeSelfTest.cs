using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prueba funcional 7B: activa campañas reales, exige una reserva nueva,
/// abre servicio y observa CustomerGroup reales captados por Marketing.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderMarketing7BPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Marketing.7B.Play.Stage";
    private const string SuccessKey = "BB.Marketing.7B.Play.Success";
    private const string ReportPath = "Block7BPlayModeReport.txt";

    private static BistroBuilderMarketingSnapshot originalMarketing;
    private static BistroBuilderReservationsSnapshot originalReservations;
    private static string originalProgressionStage = string.Empty;
    private static int originalProgressionLevel;
    private static int initialReservationCount;
    private static int initialNextGroupId;
    private static int baselineGroups;
    private static int expectedGroups;
    private static int maxObservedGroups;
    private static int maxObservedMarketingTags;
    private static double deadline;
    private static bool commandLineRun;

    static BistroBuilderMarketing7BPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem(
        "Tools/Bistro Builder/Marketing/7B - PlayMode funcional",
        false,
        7213)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "7B PlayMode self-test ya está ejecutándose.");

        File.Delete(Path.GetFullPath(ReportPath));
        commandLineRun = commandLine;
        originalMarketing = null;
        originalReservations = null;
        originalProgressionStage = string.Empty;
        originalProgressionLevel = 0;
        initialReservationCount = 0;
        initialNextGroupId = 0;
        baselineGroups = 0;
        expectedGroups = 0;
        maxObservedGroups = 0;
        maxObservedMarketingTags = 0;
        deadline = 0d;
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(
            StageKey,
            commandLine ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stage)) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            commandLineRun = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(
                StageKey,
                commandLineRun ? "run_cli" : "run_menu");
        }

        if (state == PlayModeStateChange.EnteredEditMode)
            FinishEditor(stage.Contains("cli", StringComparison.Ordinal));
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 4) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (stage.StartsWith("run_", StringComparison.Ordinal))
        {
            SessionState.SetString(
                StageKey,
                stage.EndsWith("cli", StringComparison.Ordinal)
                    ? "observe_cli"
                    : "observe_menu");
            StartFunctionalScenario(
                stage.EndsWith("cli", StringComparison.Ordinal));
            return;
        }

        if (stage.StartsWith("observe_", StringComparison.Ordinal))
            ObserveRuntime(stage.EndsWith("cli", StringComparison.Ordinal));
    }

    private static void StartFunctionalScenario(bool commandLine)
    {
        BistroBuilderMarketingService marketing = Find<BistroBuilderMarketingService>();
        BistroBuilderMarketingDemandIntegrationService integration =
            Find<BistroBuilderMarketingDemandIntegrationService>();
        BistroBuilderGeneralGameStateService general =
            Find<BistroBuilderGeneralGameStateService>();
        RestaurantServiceStateService service =
            Find<RestaurantServiceStateService>();
        CustomerGroupSpawner spawner = Find<CustomerGroupSpawner>();
        BistroBuilderReservationService reservations =
            Find<BistroBuilderReservationService>();

        if (marketing == null || integration == null || general == null ||
            service == null || spawner == null || reservations == null)
        {
            Finish(false,
                "7B: faltan autoridades runtime requeridas.", commandLine);
            return;
        }

        if (!integration.ValidateConfiguration(out string configError))
        {
            Finish(false,
                "7B: configuración inválida: " + configError,
                commandLine);
            return;
        }

        originalMarketing = marketing.CreateSnapshot();
        originalReservations = reservations.CreateSnapshot();
        originalProgressionStage = general.ProgressionStageId;
        originalProgressionLevel = general.ProgressionLevel;

        if (!service.IsClosed)
        {
            Finish(false,
                "7B: el escenario funcional debe comenzar con el restaurante cerrado.",
                commandLine);
            return;
        }

        initialReservationCount = reservations.ReservationCount;
        initialNextGroupId = spawner.NextGroupId;
        baselineGroups = spawner.BaselineGroupCount;
        maxObservedGroups = 0;
        maxObservedMarketingTags = 0;

        if (!marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(),
                out string resetError))
        {
            Finish(false, "7B: no pudo limpiar Marketing: " + resetError,
                commandLine);
            return;
        }

        int testLevel = Math.Max(5, originalProgressionLevel);
        if (!general.TrySetProgression(originalProgressionStage, testLevel))
        {
            Finish(false,
                "7B: no pudo elevar temporalmente la progresión.",
                commandLine);
            return;
        }

        ConfigureSpawnerTiming(spawner);

        if (!StartCampaign(marketing, "marketing.local.city", out string error) ||
            !StartCampaign(marketing, "marketing.local.flyers", out error) ||
            !StartCampaign(
                marketing,
                "marketing.digital.online_reservations",
                out error))
        {
            Finish(false, "7B: activar campañas falló: " + error, commandLine);
            return;
        }

        BistroBuilderMarketingDemandProjection projection =
            integration.LastProjection;
        if (projection == null ||
            projection.baselineWalkInGroups != baselineGroups ||
            projection.adjustedWalkInGroups != 4 ||
            integration.GeneratedReservationLeadsToday != 1 ||
            reservations.ReservationCount != initialReservationCount + 1)
        {
            Finish(false,
                "7B: las campañas no produjeron 4 walk-ins planificados y " +
                "1 reserva incremental. Base=" + baselineGroups +
                ", proyectados=" +
                (projection != null ? projection.adjustedWalkInGroups : -1) +
                ", leads=" + integration.GeneratedReservationLeadsToday +
                ", reservasDelta=" +
                (reservations.ReservationCount - initialReservationCount) + ".",
                commandLine);
            return;
        }
        expectedGroups = projection.adjustedWalkInGroups;

        if (!spawner.TryGetQueuedDemandPlan(out var queuedPlan) ||
            queuedPlan == null || queuedPlan.walkInGroupCount != expectedGroups)
        {
            Finish(false,
                "7B: CustomerGroupSpawner no recibió el plan de 4 grupos.",
                commandLine);
            return;
        }

        if (!service.TryOpenService())
        {
            Finish(false, "7B: no pudo abrir el servicio real.", commandLine);
            return;
        }

        if (spawner.LastPlannedGroupCount != expectedGroups ||
            string.IsNullOrWhiteSpace(spawner.LastConsumedDemandPlanId))
        {
            Finish(false,
                "7B: al abrir, el Spawner no consumió el plan Marketing.",
                commandLine);
            return;
        }

        deadline = EditorApplication.timeSinceStartup + 8d;
    }

    private static void ObserveRuntime(bool commandLine)
    {
        CustomerGroupSpawner spawner = Find<CustomerGroupSpawner>();
        if (spawner == null)
        {
            Finish(false, "7B: desapareció CustomerGroupSpawner.", commandLine);
            return;
        }

        CustomerGroup[] groups = UnityEngine.Object.FindObjectsByType<CustomerGroup>(
            FindObjectsSortMode.None);
        BistroBuilderCustomerAcquisitionTag[] tags =
            UnityEngine.Object.FindObjectsByType<BistroBuilderCustomerAcquisitionTag>(
                FindObjectsSortMode.None);

        int marketingTags = 0;
        for (int index = 0; index < tags.Length; index++)
            if (tags[index] != null && tags[index].MarketingInfluenced)
                marketingTags++;

        maxObservedGroups = Math.Max(maxObservedGroups, groups.Length);
        maxObservedMarketingTags = Math.Max(
            maxObservedMarketingTags,
            marketingTags);

        bool identitiesConsumed =
            spawner.NextGroupId - initialNextGroupId >= expectedGroups;
        if (spawner.HasCompletedSpawnSchedule && identitiesConsumed)
        {
            if (maxObservedGroups < expectedGroups ||
                maxObservedMarketingTags < expectedGroups)
            {
                Finish(false,
                    "7B: el calendario terminó, pero no se observaron los " +
                    expectedGroups + " CustomerGroup reales etiquetados. " +
                    "Máx grupos=" + maxObservedGroups +
                    ", máx Marketing=" + maxObservedMarketingTags + ".",
                    commandLine);
                return;
            }

            Finish(true,
                "PASS — demanda " + baselineGroups + "→" + expectedGroups +
                " grupos, 1 reserva incremental y " +
                maxObservedMarketingTags +
                " CustomerGroup reales atribuibles a Marketing.",
                commandLine);
            return;
        }

        if (EditorApplication.timeSinceStartup > deadline)
            Finish(false,
                "7B: timeout esperando el calendario real. Grupos=" +
                maxObservedGroups + ", Marketing=" +
                maxObservedMarketingTags + ".",
                commandLine);
    }

    private static bool StartCampaign(
        BistroBuilderMarketingService marketing,
        string campaignId,
        out string error)
    {
        return marketing.TryStartCampaign(
            campaignId,
            out _,
            out error);
    }

    private static void ConfigureSpawnerTiming(CustomerGroupSpawner spawner)
    {
        var serialized = new SerializedObject(spawner);
        SerializedProperty first = serialized.FindProperty("firstSpawnDelay");
        SerializedProperty between = serialized.FindProperty("timeBetweenGroups");
        if (first != null) first.floatValue = 0f;
        if (between != null) between.floatValue = 0.1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Finish(
        bool success,
        string message,
        bool commandLine)
    {
        if (!RestoreRuntimeState(out string restoreError))
        {
            success = false;
            message += " Rollback funcional falló: " + restoreError;
        }

        string report =
            "=== BISTRO BUILDER — 7B / PLAY MODE FUNCIONAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n" +
            "Base grupos: " + baselineGroups +
            " | Plan Marketing: " + expectedGroups +
            " | Máx grupos reales: " + maxObservedGroups +
            " | Máx tags Marketing: " + maxObservedMarketingTags + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(
            StageKey,
            commandLine ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static bool RestoreRuntimeState(out string error)
    {
        error = string.Empty;
        RestaurantServiceStateService service =
            Find<RestaurantServiceStateService>();
        if (service != null && !service.IsClosed)
            service.TryCloseServiceImmediately();

        BistroBuilderMarketingService marketing =
            Find<BistroBuilderMarketingService>();
        if (marketing != null && originalMarketing != null &&
            !marketing.TryRestoreSnapshot(originalMarketing, out error))
            return false;

        BistroBuilderGeneralGameStateService general =
            Find<BistroBuilderGeneralGameStateService>();
        if (general != null &&
            !string.IsNullOrWhiteSpace(originalProgressionStage) &&
            !general.TrySetProgression(
                originalProgressionStage,
                Math.Max(1, originalProgressionLevel)))
        {
            error = "No pudo restaurarse la progresión original.";
            return false;
        }

        BistroBuilderReservationService reservations =
            Find<BistroBuilderReservationService>();
        if (reservations != null && originalReservations != null &&
            !reservations.TryRestoreSnapshot(originalReservations, out error))
            return false;

        originalMarketing = null;
        originalReservations = null;
        originalProgressionStage = string.Empty;
        originalProgressionLevel = 0;
        error = string.Empty;
        return true;
    }

    private static T Find<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(
            FindObjectsInactive.Include);
    }

    private static void FinishEditor(bool commandLine)
    {
        bool success = SessionState.GetBool(SuccessKey, false);
        SessionState.EraseString(StageKey);
        if (commandLine)
            EditorApplication.Exit(success ? 0 : 1);
    }
}
