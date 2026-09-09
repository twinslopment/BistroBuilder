using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prueba funcional real de 6C. Entra en Play Mode, crea una reserva para
/// el minuto actual, abre servicio y exige grupo/mesa/seating canónicos.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderReservations6CPlayModeSelfTest
{
    private const string MainScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Reservations.6C.Play.Stage";
    private const string ReservationKey = "BB.Reservations.6C.Play.Reservation";
    private const string SuccessKey = "BB.Reservations.6C.Play.Success";
    private const string ReportPath = "Block6CPlayModeReport.txt";

    private static double startedAt;

    static BistroBuilderReservations6CPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Reservations/6C - PlayMode self-test", false, 633)]
    private static void RunFromMenu()
    {
        Begin(false);
    }

    public static void RunFromCommandLine()
    {
        Begin(true);
    }

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("6C PlayMode self-test ya está en Play Mode.");

        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(ReservationKey, string.Empty);
        SessionState.SetString(StageKey, commandLine ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stage))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            startedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString(StageKey,
                stage.EndsWith("cli", StringComparison.Ordinal) ? "init_cli" : "init_menu");
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool success = SessionState.GetBool(SuccessKey, false);
            bool commandLine = stage.Contains("cli");
            SessionState.EraseString(StageKey);
            SessionState.EraseString(ReservationKey);
            if (commandLine)
                EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying)
            return;

        string stage = SessionState.GetString(StageKey, string.Empty);
        if (stage.StartsWith("init_", StringComparison.Ordinal))
        {
            if (Time.frameCount < 3)
                return;
            InitializeRuntimeTest(stage.EndsWith("cli", StringComparison.Ordinal));
            return;
        }

        if (!stage.StartsWith("wait_", StringComparison.Ordinal))
            return;

        PollRuntimeTest(stage.EndsWith("cli", StringComparison.Ordinal));
    }

    private static void InitializeRuntimeTest(bool commandLine)
    {
        BistroBuilderReservationService reservations =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationService>();
        BistroBuilderReservationAvailabilityService availability =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationAvailabilityService>();
        BistroBuilderReservationServiceIntegration integration =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationServiceIntegration>();
        BistroBuilderGeneralGameStateService gameState =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        GameClock clock = UnityEngine.Object.FindFirstObjectByType<GameClock>();
        RestaurantServiceStateService serviceState =
            UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();

        if (reservations == null || availability == null || integration == null ||
            gameState == null || clock == null || serviceState == null)
        {
            Finish(false, "6C PlayMode: faltan autoridades runtime.", commandLine);
            return;
        }

        int minute = clock.Hour * 60 + clock.Minute;
        var draft = new BistroBuilderReservationDraft
        {
            guestName = "6C Queen",
            partySize = 4,
            dayIndex = gameState.DayIndex,
            arrivalMinute = minute,
            durationMinutes = 60,
            notes = "runtime self-test"
        };

        if (!reservations.TryCreateReservation(
                draft,
                out BistroBuilderReservationRecord created,
                out string createError))
        {
            Finish(false, "6C PlayMode: crear reserva falló: " + createError, commandLine);
            return;
        }

        if (!availability.TryAssignBestTable(
                created.reservationId,
                out BistroBuilderReservationRecord assigned,
                out string assignError))
        {
            Finish(false, "6C PlayMode: asignar mesa falló: " + assignError, commandLine);
            return;
        }

        SessionState.SetString(ReservationKey, assigned.reservationId);
        if (!serviceState.TryOpenService())
        {
            Finish(false, "6C PlayMode: no se pudo abrir el servicio.", commandLine);
            return;
        }

        integration.RequestEvaluation();
        startedAt = EditorApplication.timeSinceStartup;
        SessionState.SetString(StageKey, commandLine ? "wait_cli" : "wait_menu");
        Debug.Log("6C PlayMode: esperando llegada y seating reales de " + assigned.reservationId + ".");
    }

    private static void PollRuntimeTest(bool commandLine)
    {
        string reservationId = SessionState.GetString(ReservationKey, string.Empty);
        BistroBuilderReservationService reservations =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationService>();
        BistroBuilderReservationServiceIntegration integration =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationServiceIntegration>();

        if (reservations == null || integration == null ||
            string.IsNullOrWhiteSpace(reservationId))
        {
            Finish(false, "6C PlayMode: se perdió el contexto de prueba.", commandLine);
            return;
        }

        if (reservations.TryGetReservation(
                reservationId,
                out BistroBuilderReservationRecord reservation) &&
            reservation != null &&
            reservation.status == BistroBuilderReservationStatus.Seated &&
            integration.TryGetActiveGroup(reservationId, out CustomerGroup group) &&
            group != null && group.HasAssignedTable &&
            group.AssignedTable.TableId == reservation.tableId &&
            group.AssignedTable.AssignedCustomerGroup == group)
        {
            Finish(true,
                "PASS — reserva real -> CustomerGroup -> mesa " +
                reservation.tableId + " -> Seated.",
                commandLine);
            return;
        }

        if (EditorApplication.timeSinceStartup - startedAt > 25d)
            Finish(false, "6C PlayMode: timeout esperando seating real.", commandLine);
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (!EditorApplication.isPlaying)
            return;

        string report =
            "=== BISTRO BUILDER — 6C / PLAY MODE ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report);
        else Debug.LogError(report);

        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(
            StageKey,
            commandLine ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
