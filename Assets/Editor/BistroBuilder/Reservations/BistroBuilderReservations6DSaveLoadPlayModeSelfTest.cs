using System;
using System.IO;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 6D — Prueba Save/Load real en Closed y Open.
/// Demuestra reservas futuras y enlace ReservationId -> GroupId activo.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderReservations6DSaveLoadPlayModeSelfTest
{
    private const string MainScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Reservations.6D.Play.Stage";
    private const string SuccessKey = "BB.Reservations.6D.Play.Success";
    private const string ReportPath = "Block6DSaveLoadReport.txt";

    private static BistroBuilderSaveGameService saveGame;
    private static BistroBuilderReservationService reservations;
    private static BistroBuilderReservationAvailabilityService availability;
    private static BistroBuilderReservationServiceIntegration integration;
    private static BistroBuilderGeneralGameStateService gameState;
    private static GameClock clock;
    private static RestaurantServiceStateService serviceState;

    private static string futureReservationId = string.Empty;
    private static string activeReservationId = string.Empty;
    private static int futureTableId;
    private static int activeTableId;
    private static int activeGroupId;
    private static int closedSlot = -1;
    private static int activeSlot = -1;
    private static double startedAt;

    static BistroBuilderReservations6DSaveLoadPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Reservations/6D - SaveLoad PlayMode", false, 643)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("6D SaveLoad ya está en Play Mode.");

        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
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
                stage.EndsWith("cli", StringComparison.Ordinal)
                    ? "init_cli" : "init_menu");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool success = SessionState.GetBool(SuccessKey, false);
            bool commandLine = stage.Contains("cli");
            SessionState.EraseString(StageKey);
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
            Initialize(stage.EndsWith("cli", StringComparison.Ordinal));
            return;
        }

        if (stage.StartsWith("wait_active_", StringComparison.Ordinal))
            PollActive(stage.EndsWith("cli", StringComparison.Ordinal));
    }

    private static void Initialize(bool commandLine)
    {
        saveGame = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
        reservations = UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationService>();
        availability = UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationAvailabilityService>();
        integration = UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationServiceIntegration>();
        gameState = UnityEngine.Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        clock = UnityEngine.Object.FindFirstObjectByType<GameClock>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();

        if (saveGame == null || reservations == null || availability == null ||
            integration == null || gameState == null || clock == null ||
            serviceState == null)
        {
            Finish(false, "6D SaveLoad: faltan autoridades runtime.", commandLine);
            return;
        }

        if (!TryFindTwoFreeSlots(out closedSlot, out activeSlot))
        {
            Finish(false, "6D SaveLoad: no hay dos slots diagnósticos libres 940-959.", commandLine);
            return;
        }

        saveGame.OperationCompleted -= HandleSaveOperationCompleted;
        saveGame.OperationCompleted += HandleSaveOperationCompleted;

        var futureDraft = new BistroBuilderReservationDraft
        {
            guestName = "6D Future",
            partySize = 2,
            dayIndex = gameState.DayIndex + 2,
            arrivalMinute = 780,
            durationMinutes = 90,
            notes = "closed save/load"
        };

        string createError = string.Empty;
        if (!reservations.TryCreateReservation(
                futureDraft,
                out BistroBuilderReservationRecord created,
                out createError))
        {
            Finish(false,
                "6D SaveLoad: no pudo crear reserva futura. " + createError,
                commandLine);
            return;
        }

        string assignError = string.Empty;
        if (!availability.TryAssignBestTable(
                created.reservationId,
                out BistroBuilderReservationRecord assigned,
                out assignError))
        {
            Finish(false,
                "6D SaveLoad: no pudo asignar reserva futura. " + assignError,
                commandLine);
            return;
        }

        futureReservationId = assigned.reservationId;
        futureTableId = assigned.tableId;
        SessionState.SetString(StageKey, commandLine ? "save_closed_cli" : "save_closed_menu");
        if (!saveGame.TrySaveSlot(closedSlot, "BB 6D CLOSED CHECKPOINT", out string rejection))
        {
            Finish(false, "6D SaveLoad: guardar Closed fue rechazado: " + rejection, commandLine);
        }
    }

    private static void HandleSaveOperationCompleted(BistroBuilderSaveOperationResult result)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(stage) || stage.StartsWith("exit_"))
            return;

        if (result == null || !result.Succeeded)
        {
            Finish(false,
                "6D SaveLoad: operación SaveGame falló en " + stage +
                ". " + (result != null ? result.Message : "resultado nulo"),
                commandLine);
            return;
        }

        if (stage.StartsWith("save_closed_", StringComparison.Ordinal))
        {
            if (!reservations.TryCancel(
                    futureReservationId,
                    out _,
                    out string cancelError))
            {
                Finish(false, "6D SaveLoad: mutación Closed falló: " + cancelError, commandLine);
                return;
            }

            SessionState.SetString(StageKey, commandLine ? "load_closed_cli" : "load_closed_menu");
            if (!saveGame.TryLoadSlot(closedSlot, out string rejection))
                Finish(false, "6D SaveLoad: Load Closed rechazado: " + rejection, commandLine);
            return;
        }

        if (stage.StartsWith("load_closed_", StringComparison.Ordinal))
        {
            ContinueAfterClosedLoad(commandLine);
            return;
        }

        if (stage.StartsWith("save_active_", StringComparison.Ordinal))
        {
            if (!reservations.TryResetForLegacyLoad(out string resetError))
            {
                Finish(false, "6D SaveLoad: mutación Open falló: " + resetError, commandLine);
                return;
            }

            SessionState.SetString(StageKey, commandLine ? "load_active_cli" : "load_active_menu");
            if (!saveGame.TryLoadSlot(activeSlot, out string rejection))
                Finish(false, "6D SaveLoad: Load Open rechazado: " + rejection, commandLine);
            return;
        }

        if (stage.StartsWith("load_active_", StringComparison.Ordinal))
        {
            ValidateActiveLoadAndCleanup(commandLine);
            return;
        }

        if (stage.StartsWith("delete_closed_", StringComparison.Ordinal))
        {
            SessionState.SetString(StageKey, commandLine ? "delete_active_cli" : "delete_active_menu");
            if (!saveGame.TryDeleteSlot(activeSlot, out string rejection))
                Finish(false, "6D SaveLoad: limpiar slot Open falló: " + rejection, commandLine);
            return;
        }

        if (stage.StartsWith("delete_active_", StringComparison.Ordinal))
        {
            Finish(true,
                "PASS — reservations.state restaura futuro en Closed y " +
                "ReservationId/GroupId/mesa durante Open.",
                commandLine);
        }
    }

    private static void ContinueAfterClosedLoad(bool commandLine)
    {
        if (!reservations.TryGetReservation(
                futureReservationId,
                out BistroBuilderReservationRecord future) ||
            future == null ||
            future.status != BistroBuilderReservationStatus.Booked ||
            future.tableId != futureTableId ||
            future.dayIndex != gameState.DayIndex + 2)
        {
            Finish(false,
                "6D SaveLoad: la reserva futura no volvió exactamente tras Load Closed.",
                commandLine);
            return;
        }

        int minute = clock.Hour * 60 + clock.Minute;
        var draft = new BistroBuilderReservationDraft
        {
            guestName = "6D Active",
            partySize = 4,
            dayIndex = gameState.DayIndex,
            arrivalMinute = minute,
            durationMinutes = 90,
            notes = "open save/load"
        };

        string createError = string.Empty;
        if (!reservations.TryCreateReservation(
                draft,
                out BistroBuilderReservationRecord created,
                out createError))
        {
            Finish(false, "6D SaveLoad: crear reserva Open falló: " + createError, commandLine);
            return;
        }

        string assignError = string.Empty;
        if (!availability.TryAssignBestTable(
                created.reservationId,
                out BistroBuilderReservationRecord assigned,
                out assignError))
        {
            Finish(false, "6D SaveLoad: asignar mesa Open falló: " + assignError, commandLine);
            return;
        }

        activeReservationId = assigned.reservationId;
        activeTableId = assigned.tableId;
        if (!serviceState.TryOpenService())
        {
            Finish(false, "6D SaveLoad: no se pudo abrir el servicio.", commandLine);
            return;
        }

        integration.RequestEvaluation();
        startedAt = EditorApplication.timeSinceStartup;
        SessionState.SetString(StageKey,
            commandLine ? "wait_active_cli" : "wait_active_menu");
    }

    private static void PollActive(bool commandLine)
    {
        if (reservations.TryGetReservation(
                activeReservationId,
                out BistroBuilderReservationRecord reservation) &&
            reservation != null &&
            reservation.status == BistroBuilderReservationStatus.Seated &&
            integration.TryGetActiveGroup(activeReservationId, out CustomerGroup group) &&
            group != null && group.HasAssignedTable &&
            group.AssignedTable.TableId == reservation.tableId)
        {
            activeGroupId = group.GroupId;
            activeTableId = reservation.tableId;
            clock.SetPaused(true);
            SessionState.SetString(StageKey,
                commandLine ? "save_active_cli" : "save_active_menu");
            if (!saveGame.TrySaveSlot(
                    activeSlot,
                    "BB 6D ACTIVE CHECKPOINT",
                    out string rejection))
            {
                Finish(false,
                    "6D SaveLoad: guardar Open fue rechazado: " + rejection,
                    commandLine);
            }
            return;
        }

        if (EditorApplication.timeSinceStartup - startedAt > 30d)
        {
            Finish(false,
                "6D SaveLoad: timeout esperando reserva activa sentada.",
                commandLine);
        }
    }

    private static void ValidateActiveLoadAndCleanup(bool commandLine)
    {
        if (!reservations.TryGetReservation(
                activeReservationId,
                out BistroBuilderReservationRecord restored) ||
            restored == null ||
            restored.status != BistroBuilderReservationStatus.Seated ||
            restored.tableId != activeTableId ||
            !integration.TryGetActiveGroup(activeReservationId, out CustomerGroup group) ||
            group == null || group.GroupId != activeGroupId ||
            !group.HasAssignedTable ||
            group.AssignedTable.TableId != activeTableId ||
            group.AssignedTable.AssignedCustomerGroup != group)
        {
            Finish(false,
                "6D SaveLoad: Load Open no restauró ReservationId/GroupId/mesa exactos.",
                commandLine);
            return;
        }

        SessionState.SetString(StageKey,
            commandLine ? "delete_closed_cli" : "delete_closed_menu");
        if (!saveGame.TryDeleteSlot(closedSlot, out string rejection))
        {
            Finish(false,
                "6D SaveLoad: limpiar slot Closed falló: " + rejection,
                commandLine);
        }
    }

    private static bool TryFindTwoFreeSlots(out int first, out int second)
    {
        first = -1;
        second = -1;
        for (int slot = 940; slot <= 959; slot++)
        {
            if (saveGame.SlotExists(slot))
                continue;
            if (first < 0)
                first = slot;
            else
            {
                second = slot;
                return true;
            }
        }
        return false;
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (saveGame != null)
            saveGame.OperationCompleted -= HandleSaveOperationCompleted;

        string report =
            "=== BISTRO BUILDER — 6D / SAVE LOAD REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(
            StageKey,
            commandLine ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }
}
