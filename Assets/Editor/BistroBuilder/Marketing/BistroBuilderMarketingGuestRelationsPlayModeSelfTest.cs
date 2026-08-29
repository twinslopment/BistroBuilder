using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gate funcional real de Reputation, RepeatVisit y ReservationDemand.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderMarketingGuestRelationsPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Marketing.GuestRelations.Play.Stage";
    private const string SuccessKey = "BB.Marketing.GuestRelations.Play.Success";
    private const string ReportPath = "GuestRelationsPlayModeReport.txt";

    private static BistroBuilderMarketingSnapshot originalMarketing;
    private static BistroBuilderFinanceSnapshot originalFinance;
    private static BistroBuilderGuestRelationsSnapshot originalRelations;
    private static BistroBuilderReputationSnapshot originalReputation;
    private static BistroBuilderReservationsSnapshot originalReservations;
    private static int originalLeadDay;
    private static int originalLeadCount;
    private static string originalGameId;
    private static string originalRestaurantName;
    private static string originalCreatedUtc;
    private static int originalDayIndex;
    private static int originalYear;
    private static int originalMonth;
    private static int originalDay;
    private static string originalStage;
    private static int originalLevel;

    static BistroBuilderMarketingGuestRelationsPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Marketing/Guest Relations - PlayMode funcional", false, 7253)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "GuestRelations PlayMode ya está ejecutándose.");

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
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "run_cli" : "run_menu");
        }

        if (state == PlayModeStateChange.EnteredEditMode)
            FinishEditor(stage.Contains("cli", StringComparison.Ordinal));
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 4) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;

        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        SessionState.SetString(StageKey, cli ? "executing_cli" : "executing_menu");
        RunScenario(cli);
    }

    private static void RunScenario(bool commandLine)
    {
        BistroBuilderMarketingService marketing = Find<BistroBuilderMarketingService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();
        BistroBuilderGuestRelationsService relations = Find<BistroBuilderGuestRelationsService>();
        BistroBuilderReputationService reputation = Find<BistroBuilderReputationService>();
        BistroBuilderMarketingGuestRelationsBridge bridge =
            Find<BistroBuilderMarketingGuestRelationsBridge>();
        BistroBuilderMarketingDemandIntegrationService demand =
            Find<BistroBuilderMarketingDemandIntegrationService>();
        BistroBuilderReservationService reservations = Find<BistroBuilderReservationService>();
        BistroBuilderGeneralGameStateService general =
            Find<BistroBuilderGeneralGameStateService>();
        CustomerGroupSpawner spawner = Find<CustomerGroupSpawner>();
        RestaurantServiceStateService service = Find<RestaurantServiceStateService>();

        if (marketing == null || finance == null || relations == null ||
            bridge == null || demand == null || reservations == null ||
            general == null || spawner == null || service == null)
        {
            Finish(false, "Faltan autoridades runtime para GuestRelations.", commandLine);
            return;
        }

        if (service.AcceptsNewCustomers)
        {
            Finish(false, "El fixture requiere el restaurante cerrado.", commandLine);
            return;
        }

        string bridgeError = string.Empty;
        string demandError = string.Empty;
        string relationsError = string.Empty;
        if (!bridge.ValidateConfiguration(out bridgeError) ||
            !demand.ValidateConfiguration(out demandError) ||
            !relations.ValidateConfiguration(out relationsError))
        {
            Finish(false,
                "Configuración inválida. Bridge=" + bridgeError +
                " | Demand=" + demandError + " | Relations=" + relationsError,
                commandLine);
            return;
        }

        CaptureOriginalState(marketing, finance, relations, reputation, reservations, demand, general);
        if (originalMarketing == null || originalFinance == null ||
            originalRelations == null || originalReputation == null || originalReservations == null)
        {
            Finish(false, "No pudieron capturarse snapshots para rollback.", commandLine);
            return;
        }

        string resetMarketingError = string.Empty;
        string resetRelationsError = string.Empty;
        string resetReputationError = string.Empty;
        string resetDemandError = string.Empty;
        if (!general.TrySetCalendar(2, 2026, 1, 2) ||
            !general.TrySetProgression("marketing_guest_relations_test", 5) ||
            !marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(), out resetMarketingError) ||
            !relations.TryRestoreSnapshot(
                BistroBuilderGuestRelationsEngine.CreateEmptySnapshot(), out resetRelationsError) ||
            !reputation.TryRestoreSnapshot(
                BistroBuilderReputationEngine.CreateInitialSnapshot(), out resetReputationError) ||
            !demand.TryRestorePersistenceState(0, 0, out resetDemandError))
        {
            Finish(false,
                "No pudo prepararse el fixture. Marketing=" + resetMarketingError +
                " | Relations=" + resetRelationsError +
                " | Reputation=" + resetReputationError +
                " | Demand=" + resetDemandError,
                commandLine);
            return;
        }

        if (!marketing.TryStartCampaign(
                "marketing.local.press",
                out _,
                out string reputationCampaignError))
        {
            Finish(false,
                "No pudo iniciarse la campaña de reputación: " +
                reputationCampaignError,
                commandLine);
            return;
        }

        if (relations.ReputationPoints != 1 || bridge.LastCreditsApplied != 1)
        {
            Finish(false,
                "La campaña no acreditó exactamente +1 punto de reputación.",
                commandLine);
            return;
        }

        if (!bridge.TrySynchronizeReputationCredits(out string resyncError) ||
            relations.ReputationPoints != 1 || bridge.LastCreditsApplied != 0)
        {
            Finish(false,
                "La reputación no es idempotente: " + resyncError,
                commandLine);
            return;
        }

        string clearCampaignError = string.Empty;
        string reputationProjectionError = string.Empty;
        BistroBuilderMarketingDemandProjection reputationProjection = null;
        if (!marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(),
                out clearCampaignError) ||
            !demand.TryBuildProjection(
                out reputationProjection,
                out reputationProjectionError) ||
            reputationProjection == null ||
            reputationProjection.effectiveWalkInBasisPoints != 100)
        {
            Finish(false,
                "La reputación no dejó +1 % de demanda duradera. Marketing=" +
                clearCampaignError + " | Projection=" + reputationProjectionError,
                commandLine);
            return;
        }

        BistroBuilderGuestRelationsSnapshot cohortState =
            BistroBuilderGuestRelationsEngine.CreateEmptySnapshot();
        if (!BistroBuilderGuestRelationsEngine.TryRecordCompletedVisit(
                cohortState,
                "localresidents",
                2,
                1,
                string.Empty,
                out cohortState,
                out string cohortId,
                out string cohortError))
        {
            Finish(false, "No pudo prepararse una cohorte previa: " + cohortError,
                commandLine);
            return;
        }

        if (!relations.TryRestoreSnapshot(
                cohortState,
                out string cohortRestoreError))
        {
            Finish(false,
                "No pudo restaurarse la cohorte previa: " + cohortRestoreError,
                commandLine);
            return;
        }

        if (!marketing.TryStartCampaign(
                "marketing.loyalty.card",
                out _,
                out string loyaltyError))
        {
            Finish(false,
                "No pudo iniciarse la campaña de fidelización: " + loyaltyError,
                commandLine);
            return;
        }

        if (!demand.TryRefreshDemandForNextService(
                out string repeatDemandError) ||
            !spawner.TryGetQueuedDemandPlan(
                out BistroBuilderCustomerDemandPlan returnPlan) ||
            returnPlan == null)
        {
            Finish(false,
                "RepeatVisit no pudo generar el plan recurrente: " +
                repeatDemandError,
                commandLine);
            return;
        }

        int returningCount = 0;
        BistroBuilderCustomerAcquisitionProfile returningProfile = null;
        for (int index = 0; index < returnPlan.profiles.Count; index++)
        {
            BistroBuilderCustomerAcquisitionProfile profile =
                returnPlan.profiles[index];
            if (profile != null && profile.returningVisit)
            {
                returningCount++;
                returningProfile = profile;
            }
        }

        if (returningCount != 1 || returningProfile == null ||
            returningProfile.guestRelationsReferenceId != cohortId ||
            returningProfile.preferredGroupSize != 2 ||
            returnPlan.walkInGroupCount != returnPlan.profiles.Count)
        {
            Finish(false,
                "RepeatVisit no sustituyó exactamente un walk-in por la " +
                "cohorte previa conservando tamaño y cardinalidad.",
                commandLine);
            return;
        }

        string preReservationMarketingError = string.Empty;
        string preReservationRelationsError = string.Empty;
        string preReservationReputationError = string.Empty;
        string preReservationRestoreError = string.Empty;
        string preLeadError = string.Empty;
        if (!marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(),
                out preReservationMarketingError) ||
            !relations.TryRestoreSnapshot(
                BistroBuilderGuestRelationsEngine.CreateEmptySnapshot(),
                out preReservationRelationsError) ||
            !reputation.TryRestoreSnapshot(
                BistroBuilderReputationEngine.CreateInitialSnapshot(),
                out preReservationReputationError) ||
            !reservations.TryRestoreSnapshot(
                originalReservations,
                out preReservationRestoreError) ||
            !demand.TryRestorePersistenceState(0, 0, out preLeadError))
        {
            Finish(false,
                "No pudo aislarse ReservationDemand: " +
                preReservationMarketingError + " | " +
                preReservationRelationsError + " | " +
                preReservationRestoreError + " | " + preLeadError,
                commandLine);
            return;
        }

        int reservationsBefore = reservations.ReservationCount;
        string reservationCampaignError = string.Empty;
        string reservationDemandError = string.Empty;
        if (!marketing.TryStartCampaign(
                "marketing.digital.online_reservations",
                out _,
                out reservationCampaignError) ||
            !demand.TryRefreshDemandForNextService(
                out reservationDemandError))
        {
            Finish(false,
                "ReservationDemand no pudo ejecutarse. Campaign=" +
                reservationCampaignError + " | Demand=" + reservationDemandError,
                commandLine);
            return;
        }

        BistroBuilderMarketingDemandProjection reservationProjection =
            demand.LastProjection;
        int reservationsDelta = reservations.ReservationCount - reservationsBefore;
        if (reservationProjection == null ||
            reservationProjection.reservationLeadCount != 1 ||
            reservationsDelta != 1)
        {
            Finish(false,
                "ReservationDemand no materializó exactamente una reserva real. " +
                "Leads=" +
                (reservationProjection != null
                    ? reservationProjection.reservationLeadCount
                    : -1) + ", delta=" + reservationsDelta + ".",
                commandLine);
            return;
        }

        Finish(true,
            "PASS — Reputation: +1 punto persiste como +1 % de demanda; " +
            "RepeatVisit: 1 cohorte real vuelve sin aumentar el número de grupos; " +
            "ReservationDemand: +17 % materializa 1 reserva real.",
            commandLine);
    }

    private static void CaptureOriginalState(
        BistroBuilderMarketingService marketing,
        BistroBuilderFinanceService finance,
        BistroBuilderGuestRelationsService relations,
        BistroBuilderReputationService reputation,
        BistroBuilderReservationService reservations,
        BistroBuilderMarketingDemandIntegrationService demand,
        BistroBuilderGeneralGameStateService general)
    {
        originalMarketing = marketing.CreateSnapshot();
        originalFinance = finance.CreateSnapshot();
        originalRelations = relations.CreateSnapshot();
        originalReputation = reputation.CreateSnapshot();
        originalReservations = reservations.CreateSnapshot();
        demand.TryCapturePersistenceState(
            out originalLeadDay,
            out originalLeadCount,
            out _);

        originalGameId = general.GameId;
        originalRestaurantName = general.RestaurantName;
        originalCreatedUtc = general.CreatedUtc;
        originalDayIndex = general.DayIndex;
        originalYear = general.CalendarYear;
        originalMonth = general.CalendarMonth;
        originalDay = general.CalendarDay;
        originalStage = general.ProgressionStageId;
        originalLevel = general.ProgressionLevel;
    }

    private static bool RestoreOriginalState(out string error)
    {
        error = string.Empty;
        BistroBuilderMarketingService marketing = Find<BistroBuilderMarketingService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();
        BistroBuilderGuestRelationsService relations = Find<BistroBuilderGuestRelationsService>();
        BistroBuilderReputationService reputation = Find<BistroBuilderReputationService>();
        BistroBuilderReservationService reservations = Find<BistroBuilderReservationService>();
        BistroBuilderMarketingDemandIntegrationService demand =
            Find<BistroBuilderMarketingDemandIntegrationService>();
        BistroBuilderGeneralGameStateService general =
            Find<BistroBuilderGeneralGameStateService>();

        if (marketing == null || finance == null || relations == null ||
            reputation == null || reservations == null || demand == null || general == null)
        {
            error = "Faltan autoridades para restaurar el fixture.";
            return false;
        }

        if (originalMarketing != null &&
            !marketing.TryRestoreSnapshot(originalMarketing, out error))
            return false;
        if (originalFinance != null &&
            !finance.TryRestoreSnapshot(originalFinance, out error))
            return false;
        if (originalRelations != null &&
            !relations.TryRestoreSnapshot(originalRelations, out error))
            return false;
        if (originalReputation != null &&
            !reputation.TryRestoreSnapshot(originalReputation, out error))
            return false;
        if (originalReservations != null &&
            !reservations.TryRestoreSnapshot(originalReservations, out error))
            return false;
        if (!demand.TryRestorePersistenceState(
                originalLeadDay,
                originalLeadCount,
                out error))
            return false;

        if (!general.TryRestoreState(
                originalGameId,
                originalRestaurantName,
                originalCreatedUtc,
                originalDayIndex,
                originalYear,
                originalMonth,
                originalDay,
                originalStage,
                Math.Max(1, originalLevel)))
        {
            error = "No pudo restaurarse el estado general original.";
            return false;
        }

        return true;
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (originalMarketing != null &&
            !RestoreOriginalState(out string restoreError))
        {
            success = false;
            message += " Rollback funcional falló: " + restoreError;
        }

        string report =
            "=== BISTRO BUILDER — MARKETING / GUEST RELATIONS PLAY MODE ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        ClearSnapshots();
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(
            StageKey,
            commandLine ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static void ClearSnapshots()
    {
        originalMarketing = null;
        originalFinance = null;
        originalRelations = null;
        originalReputation = null;
        originalReservations = null;
        originalGameId = string.Empty;
        originalRestaurantName = string.Empty;
        originalCreatedUtc = string.Empty;
        originalStage = string.Empty;
        originalLeadDay = 0;
        originalLeadCount = 0;
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
