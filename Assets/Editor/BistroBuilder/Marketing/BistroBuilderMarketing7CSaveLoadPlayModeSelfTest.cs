using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 7C — Gate funcional Save/Load real. Comprueba Closed y Open con campañas,
/// leads, plan de demanda, grupos materializados y llegadas aún pendientes.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderMarketing7CSaveLoadPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Marketing.7C.SaveLoad.Stage";
    private const string SuccessKey = "BB.Marketing.7C.SaveLoad.Success";
    private const string FailureKey = "BB.Marketing.7C.SaveLoad.Failure";
    private const string ReportPath = "Block7CSaveLoadReport.txt";

    private static BistroBuilderSaveGameService saveGame;
    private static BistroBuilderMarketingService marketing;
    private static BistroBuilderMarketingDemandIntegrationService demand;
    private static BistroBuilderReservationService reservations;
    private static BistroBuilderGeneralGameStateService general;
    private static RestaurantServiceStateService service;
    private static CustomerGroupSpawner spawner;

    private static BistroBuilderMarketingSnapshot checkpointMarketing;
    private static int checkpointLeads;
    private static int checkpointLeadDay;
    private static int checkpointReservations;
    private static int expectedGroups;
    private static int initialNextGroupId;
    private static int closedSlot = -1;
    private static int activeSlot = -1;
    private static int savedPendingArrivals;
    private static double deadline;

    static BistroBuilderMarketing7CSaveLoadPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem(
        "Tools/Bistro Builder/Marketing/7C - SaveLoad PlayMode",
        false,
        7313)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "7C SaveLoad ya está ejecutándose.");

        File.Delete(Path.GetFullPath(ReportPath));
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
            SessionState.SetString(
                StageKey,
                stage.EndsWith("cli", StringComparison.Ordinal)
                    ? "init_cli"
                    : "init_menu");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool success = SessionState.GetBool(SuccessKey, false);
            bool commandLine = stage.Contains("cli", StringComparison.Ordinal);
            SessionState.EraseString(StageKey);
            if (commandLine) EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        string stage = SessionState.GetString(StageKey, string.Empty);

        if (stage.StartsWith("init_", StringComparison.Ordinal))
        {
            if (Time.frameCount < 4) return;
            Initialize(stage.EndsWith("cli", StringComparison.Ordinal));
            return;
        }

        if (stage.StartsWith("wait_first_group_", StringComparison.Ordinal))
            WaitForFirstGroup(stage.EndsWith("cli", StringComparison.Ordinal));
    }

    private static void Initialize(bool commandLine)
    {
        saveGame = Find<BistroBuilderSaveGameService>();
        marketing = Find<BistroBuilderMarketingService>();
        demand = Find<BistroBuilderMarketingDemandIntegrationService>();
        reservations = Find<BistroBuilderReservationService>();
        general = Find<BistroBuilderGeneralGameStateService>();
        service = Find<RestaurantServiceStateService>();
        spawner = Find<CustomerGroupSpawner>();

        if (saveGame == null || marketing == null || demand == null ||
            reservations == null || general == null || service == null ||
            spawner == null)
        {
            Finish(false, "7C SaveLoad: faltan autoridades runtime.", commandLine);
            return;
        }

        if (!service.IsClosed)
        {
            Finish(false,
                "7C SaveLoad: el escenario debe comenzar con el restaurante cerrado.",
                commandLine);
            return;
        }

        if (!demand.ValidateConfiguration(out string configError))
        {
            Finish(false,
                "7C SaveLoad: configuración inválida. " + configError,
                commandLine);
            return;
        }

        if (!TryFindTwoFreeSlots(out closedSlot, out activeSlot))
        {
            Finish(false,
                "7C SaveLoad: no hay dos slots diagnósticos libres 970-989.",
                commandLine);
            return;
        }

        if (!marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(),
                out string resetError))
        {
            Finish(false,
                "7C SaveLoad: no pudo limpiar Marketing. " + resetError,
                commandLine);
            return;
        }

        int testLevel = Math.Max(5, general.ProgressionLevel);
        if (!general.TrySetProgression(general.ProgressionStageId, testLevel))
        {
            Finish(false,
                "7C SaveLoad: no pudo preparar progresión.",
                commandLine);
            return;
        }

        ConfigureSpawnerTiming(spawner);
        initialNextGroupId = spawner.NextGroupId;

        if (!StartCampaign("marketing.local.city", out string error) ||
            !StartCampaign("marketing.local.flyers", out error) ||
            !StartCampaign("marketing.digital.online_reservations", out error))
        {
            Finish(false,
                "7C SaveLoad: activar campañas falló. " + error,
                commandLine);
            return;
        }

        BistroBuilderMarketingDemandProjection projection = demand.LastProjection;
        checkpointMarketing = marketing.CreateSnapshot();
        checkpointLeads = demand.GeneratedReservationLeadsToday;
        checkpointLeadDay = demand.ReservationLeadDay;
        checkpointReservations = reservations.ReservationCount;
        expectedGroups = projection != null
            ? projection.adjustedWalkInGroups
            : -1;

        if (checkpointMarketing.campaigns.Count != 3 ||
            checkpointLeads != 1 || checkpointLeadDay != general.DayIndex ||
            expectedGroups != 4 ||
            !spawner.TryGetQueuedDemandPlan(out var plan) ||
            plan == null || plan.walkInGroupCount != 4)
        {
            Finish(false,
                "7C SaveLoad: checkpoint Closed no contiene 3 campañas, " +
                "1 lead y plan 4 grupos.", commandLine);
            return;
        }

        saveGame.OperationCompleted -= HandleSaveOperationCompleted;
        saveGame.OperationCompleted += HandleSaveOperationCompleted;
        SessionState.SetString(
            StageKey,
            commandLine ? "save_closed_cli" : "save_closed_menu");

        if (!saveGame.TrySaveSlot(
                closedSlot,
                "BB 7C MARKETING CLOSED CHECKPOINT",
                out string rejection))
        {
            Finish(false,
                "7C SaveLoad: Save Closed rechazado. " + rejection,
                commandLine);
        }
    }

    private static void HandleSaveOperationCompleted(
        BistroBuilderSaveOperationResult result)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(stage) || stage.StartsWith("exit_"))
            return;

        if (result == null || !result.Succeeded)
        {
            FailAndCleanup(
                "7C SaveLoad: operación falló en " + stage + ". " +
                (result != null ? result.Message : "resultado nulo"),
                commandLine);
            return;
        }

        if (stage.StartsWith("save_closed_", StringComparison.Ordinal))
        {
            MutateMarketingBeforeLoad();
            SessionState.SetString(
                StageKey,
                commandLine ? "load_closed_cli" : "load_closed_menu");
            if (!saveGame.TryLoadSlot(closedSlot, out string rejection))
                FailAndCleanup(
                    "7C SaveLoad: Load Closed rechazado. " + rejection,
                    commandLine);
            return;
        }

        if (stage.StartsWith("load_closed_", StringComparison.Ordinal))
        {
            ContinueAfterClosedLoad(commandLine);
            return;
        }

        if (stage.StartsWith("save_active_", StringComparison.Ordinal))
        {
            MutateMarketingBeforeLoad();
            SessionState.SetString(
                StageKey,
                commandLine ? "load_active_cli" : "load_active_menu");
            if (!saveGame.TryLoadSlot(activeSlot, out string rejection))
                FailAndCleanup(
                    "7C SaveLoad: Load Active rechazado. " + rejection,
                    commandLine);
            return;
        }

        if (stage.StartsWith("load_active_", StringComparison.Ordinal))
        {
            ValidateActiveLoad(commandLine);
            return;
        }

        if (stage.StartsWith("delete_closed_", StringComparison.Ordinal))
        {
            SessionState.SetString(
                StageKey,
                commandLine ? "delete_active_cli" : "delete_active_menu");
            if (!saveGame.TryDeleteSlot(activeSlot, out string rejection))
                Finish(false,
                    "7C SaveLoad: no pudo limpiar slot Active. " + rejection,
                    commandLine);
            return;
        }

        if (stage.StartsWith("delete_active_", StringComparison.Ordinal))
        {
            Finish(true,
                "PASS — marketing.state restaura Closed y Open sin duplicar " +
                "reservas, y service.runtime conserva atribución de grupos " +
                "y llegadas pendientes.",
                commandLine);
            return;
        }

        if (stage.StartsWith("cleanup_closed_fail_", StringComparison.Ordinal))
        {
            if (activeSlot >= 0 && saveGame.SlotExists(activeSlot))
            {
                SessionState.SetString(
                    StageKey,
                    commandLine ? "cleanup_active_fail_cli" : "cleanup_active_fail_menu");
                if (!saveGame.TryDeleteSlot(activeSlot, out _))
                    Finish(false, SessionState.GetString(FailureKey, "7C SaveLoad falló."), commandLine);
                return;
            }
            Finish(false, SessionState.GetString(FailureKey, "7C SaveLoad falló."), commandLine);
            return;
        }

        if (stage.StartsWith("cleanup_active_fail_", StringComparison.Ordinal))
            Finish(false, SessionState.GetString(FailureKey, "7C SaveLoad falló."), commandLine);
    }

    private static void ContinueAfterClosedLoad(bool commandLine)
    {
        if (!MarketingMatchesCheckpoint() ||
            demand.GeneratedReservationLeadsToday != checkpointLeads ||
            demand.ReservationLeadDay != checkpointLeadDay ||
            reservations.ReservationCount != checkpointReservations ||
            !spawner.TryGetQueuedDemandPlan(out var plan) ||
            plan == null || plan.walkInGroupCount != expectedGroups ||
            !AllProfilesMarketing(plan.profiles))
        {
            FailAndCleanup(
                "7C SaveLoad: Load Closed no restauró campañas/leads/plan " +
                "exactos o duplicó reservas.",
                commandLine);
            return;
        }

        ConfigureSpawnerTiming(spawner);
        if (!service.TryOpenService())
        {
            FailAndCleanup(
                "7C SaveLoad: no pudo abrir servicio tras Load Closed.",
                commandLine);
            return;
        }

        deadline = EditorApplication.timeSinceStartup + 8d;
        SessionState.SetString(
            StageKey,
            commandLine ? "wait_first_group_cli" : "wait_first_group_menu");
    }

    private static void WaitForFirstGroup(bool commandLine)
    {
        BistroBuilderCustomerAcquisitionTag[] tags =
            UnityEngine.Object.FindObjectsByType<BistroBuilderCustomerAcquisitionTag>(
                FindObjectsSortMode.None);
        bool hasMarketingGroup = false;
        for (int index = 0; index < tags.Length; index++)
        {
            CustomerGroup group = tags[index] != null
                ? tags[index].GetComponent<CustomerGroup>()
                : null;
            if (group != null && group.GroupId >= initialNextGroupId &&
                tags[index].MarketingInfluenced)
            {
                hasMarketingGroup = true;
                break;
            }
        }

        if (hasMarketingGroup &&
            spawner.TryCaptureRuntimeSpawnState(
                out BistroBuilderCustomerSpawnerRuntimeSaveRecord runtime,
                out string captureError) &&
            runtime != null && runtime.pendingArrivals.Count > 0)
        {
            Time.timeScale = 0f;
            savedPendingArrivals = runtime.pendingArrivals.Count;
            if (!AllPendingMarketing(runtime.pendingArrivals))
            {
                FailAndCleanup(
                    "7C SaveLoad: las llegadas pendientes perdieron atribución " +
                    "antes del Save Active.", commandLine);
                return;
            }

            SessionState.SetString(
                StageKey,
                commandLine ? "save_active_cli" : "save_active_menu");
            if (!saveGame.TrySaveSlot(
                    activeSlot,
                    "BB 7C MARKETING ACTIVE CHECKPOINT",
                    out string rejection))
            {
                FailAndCleanup(
                    "7C SaveLoad: Save Active rechazado. " + rejection,
                    commandLine);
            }
            return;
        }

        if (EditorApplication.timeSinceStartup > deadline)
        {
            FailAndCleanup(
                "7C SaveLoad: timeout esperando 1 grupo Marketing con " +
                "llegadas pendientes.",
                commandLine);
        }
    }

    private static void ValidateActiveLoad(bool commandLine)
    {
        bool activeTagRestored = false;
        BistroBuilderCustomerAcquisitionTag[] tags =
            UnityEngine.Object.FindObjectsByType<BistroBuilderCustomerAcquisitionTag>(
                FindObjectsSortMode.None);
        for (int index = 0; index < tags.Length; index++)
        {
            CustomerGroup group = tags[index] != null
                ? tags[index].GetComponent<CustomerGroup>()
                : null;
            if (group != null && group.GroupId >= initialNextGroupId &&
                tags[index].MarketingInfluenced &&
                tags[index].SourceSystemId ==
                    BistroBuilderMarketingService.FinanceSourceSystemId)
            {
                activeTagRestored = true;
                break;
            }
        }

        bool runtimeOk = spawner.TryCaptureRuntimeSpawnState(
            out BistroBuilderCustomerSpawnerRuntimeSaveRecord runtime,
            out _);
        if (!service.AcceptsNewCustomers ||
            !MarketingMatchesCheckpoint() ||
            demand.GeneratedReservationLeadsToday != checkpointLeads ||
            demand.ReservationLeadDay != checkpointLeadDay ||
            reservations.ReservationCount != checkpointReservations ||
            !activeTagRestored || !runtimeOk || runtime == null ||
            runtime.pendingArrivals.Count != savedPendingArrivals ||
            !AllPendingMarketing(runtime.pendingArrivals) ||
            spawner.HasQueuedDemandPlan)
        {
            FailAndCleanup(
                "7C SaveLoad: Load Active no restauró exactamente campañas, " +
                "leads, reservas, grupo atribuido y llegadas pendientes.",
                commandLine);
            return;
        }

        Time.timeScale = 1f;
        SessionState.SetString(
            StageKey,
            commandLine ? "delete_closed_cli" : "delete_closed_menu");
        if (!saveGame.TryDeleteSlot(closedSlot, out string rejection))
        {
            Finish(false,
                "7C SaveLoad: no pudo limpiar slot Closed. " + rejection,
                commandLine);
        }
    }

    private static void MutateMarketingBeforeLoad()
    {
        marketing.TryResetForLegacyLoad(out _);
        demand.TryRestorePersistenceState(0, 0, out _);
    }

    private static bool StartCampaign(string campaignId, out string error)
    {
        return marketing.TryStartCampaign(
            campaignId,
            out _,
            out error);
    }

    private static bool MarketingMatchesCheckpoint()
    {
        if (checkpointMarketing == null)
            return false;
        BistroBuilderMarketingSnapshot current = marketing.CreateSnapshot();
        if (current == null ||
            current.revision != checkpointMarketing.revision ||
            current.campaigns.Count != checkpointMarketing.campaigns.Count)
            return false;

        var expected = new Dictionary<string, BistroBuilderMarketingCampaignRecord>(
            StringComparer.Ordinal);
        for (int index = 0; index < checkpointMarketing.campaigns.Count; index++)
            expected[checkpointMarketing.campaigns[index].instanceId] =
                checkpointMarketing.campaigns[index];

        for (int index = 0; index < current.campaigns.Count; index++)
        {
            BistroBuilderMarketingCampaignRecord actual = current.campaigns[index];
            if (actual == null ||
                !expected.TryGetValue(actual.instanceId, out var saved) ||
                saved.campaignId != actual.campaignId ||
                saved.targetId != actual.targetId ||
                saved.startDayIndex != actual.startDayIndex ||
                saved.endDayExclusive != actual.endDayExclusive ||
                saved.paidCostCents != actual.paidCostCents ||
                saved.financeOperationId != actual.financeOperationId ||
                saved.revision != actual.revision)
                return false;
        }
        return true;
    }

    private static bool AllProfilesMarketing(
        IReadOnlyList<BistroBuilderCustomerAcquisitionProfile> profiles)
    {
        if (profiles == null || profiles.Count == 0) return false;
        for (int index = 0; index < profiles.Count; index++)
            if (profiles[index] == null || !profiles[index].marketingInfluenced ||
                profiles[index].sourceSystemId !=
                    BistroBuilderMarketingService.FinanceSourceSystemId)
                return false;
        return true;
    }

    private static bool AllPendingMarketing(
        IReadOnlyList<BistroBuilderCustomerArrivalPlanSaveRecord> arrivals)
    {
        if (arrivals == null || arrivals.Count == 0) return false;
        for (int index = 0; index < arrivals.Count; index++)
            if (arrivals[index] == null ||
                arrivals[index].acquisition == null ||
                !arrivals[index].acquisition.marketingInfluenced ||
                arrivals[index].acquisition.sourceSystemId !=
                    BistroBuilderMarketingService.FinanceSourceSystemId)
                return false;
        return true;
    }

    private static void ConfigureSpawnerTiming(CustomerGroupSpawner target)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty first = serialized.FindProperty("firstSpawnDelay");
        SerializedProperty between = serialized.FindProperty("timeBetweenGroups");
        if (first != null) first.floatValue = 0f;
        if (between != null) between.floatValue = 30f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool TryFindTwoFreeSlots(out int first, out int second)
    {
        first = -1;
        second = -1;
        for (int slot = 970; slot <= 989; slot++)
        {
            if (saveGame.SlotExists(slot)) continue;
            if (first < 0) first = slot;
            else
            {
                second = slot;
                return true;
            }
        }
        return false;
    }

    private static void FailAndCleanup(string message, bool commandLine)
    {
        Time.timeScale = 1f;
        SessionState.SetString(FailureKey, message);

        if (saveGame != null && closedSlot >= 0 && saveGame.SlotExists(closedSlot))
        {
            SessionState.SetString(
                StageKey,
                commandLine ? "cleanup_closed_fail_cli" : "cleanup_closed_fail_menu");
            if (saveGame.TryDeleteSlot(closedSlot, out _)) return;
        }

        if (saveGame != null && activeSlot >= 0 && saveGame.SlotExists(activeSlot))
        {
            SessionState.SetString(
                StageKey,
                commandLine ? "cleanup_active_fail_cli" : "cleanup_active_fail_menu");
            if (saveGame.TryDeleteSlot(activeSlot, out _)) return;
        }

        Finish(false, message, commandLine);
    }

    private static void Finish(
        bool success,
        string message,
        bool commandLine)
    {
        Time.timeScale = 1f;
        if (saveGame != null)
            saveGame.OperationCompleted -= HandleSaveOperationCompleted;

        string report =
            "=== BISTRO BUILDER — 7C / SAVE LOAD REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n" +
            "Campañas: " +
            (checkpointMarketing != null ? checkpointMarketing.campaigns.Count : 0) +
            " | Leads: " + checkpointLeads +
            " | Reservas checkpoint: " + checkpointReservations +
            " | Plan grupos: " + expectedGroups +
            " | Pendientes Active: " + savedPendingArrivals + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        SessionState.SetBool(SuccessKey, success);
        SessionState.EraseString(FailureKey);
        SessionState.SetString(
            StageKey,
            commandLine ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static T Find<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(
            FindObjectsInactive.Include);
    }
}
