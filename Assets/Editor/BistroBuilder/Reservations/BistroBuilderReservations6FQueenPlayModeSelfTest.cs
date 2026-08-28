using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 6F — Queen Test autónomo y reversible de Reservas.
/// Recorre UI/fachada -> disponibilidad -> llegada -> seating -> Save/Load
/// activo -> servicio completo -> rollback integral mediante SaveGame.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderReservations6FQueenPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Reservations.6F.Stage";
    private const string SuccessKey = "BB.Reservations.6F.Success";
    private const string ReportPath = "Block6FQueenReport.txt";
    private const double SeatingTimeoutSeconds = 45d;
    private const double CompletionTimeoutSeconds = 180d;

    private static BistroBuilderSaveGameService saveGame;
    private static BistroBuilderReservationService reservations;
    private static BistroBuilderReservationAvailabilityService availability;
    private static BistroBuilderReservationServiceIntegration integration;
    private static BistroBuilderReservationPlayerFacade playerFacade;
    private static BistroBuilderReservationPlayerScreen playerScreen;
    private static BistroBuilderGeneralGameStateService gameState;
    private static RestaurantServiceStateService serviceState;
    private static GameClock clock;
    private static int rollbackSlot = -1;
    private static int checkpointSlot = -1;
    private static int initialReservationCount;
    private static string reservationId = string.Empty;
    private static int tableId;
    private static int groupId;
    private static double startedAt;
    private static bool desiredSuccess;
    private static string pendingMessage = string.Empty;

    static BistroBuilderReservations6FQueenPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Reservations/6F - QUEEN TEST reversible", false, 661)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("6F Queen Test ya está ejecutándose.");

        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, commandLine ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stage)) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            startedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString(StageKey,
                stage.EndsWith("cli", StringComparison.Ordinal)
                    ? "init_cli" : "init_menu");
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
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

        if (stage.StartsWith("wait_seated_", StringComparison.Ordinal))
            PollSeated(stage.EndsWith("cli", StringComparison.Ordinal));
        else if (stage.StartsWith("wait_completed_", StringComparison.Ordinal))
            PollCompleted(stage.EndsWith("cli", StringComparison.Ordinal));
    }
    private static void Initialize(bool commandLine)
    {
        if (!Resolve(out string error))
        {
            FinalExit(false, "6F: " + error, commandLine);
            return;
        }

        if (!BistroBuilderReservations6FQueenPreflight.Run(
                out int preflightPassed,
                out int preflightFailed,
                out string preflightReport) || preflightFailed > 0)
        {
            FinalExit(false,
                "6F: preflight falló (" + preflightPassed + "/" + preflightFailed + ").\n" +
                preflightReport,
                commandLine);
            return;
        }

        if (!serviceState.IsClosed)
        {
            FinalExit(false, "6F debe comenzar con el restaurante Closed.", commandLine);
            return;
        }

        saveGame.RefreshExtensions();
        if (!saveGame.ValidateConfiguration(out error))
        {
            FinalExit(false, "6F: SaveGame inválido: " + error, commandLine);
            return;
        }

        if (!TryFindTwoFreeSlots(out rollbackSlot, out checkpointSlot))
        {
            FinalExit(false, "6F: hacen falta dos slots libres entre 960 y 979.", commandLine);
            return;
        }

        initialReservationCount = reservations.ReservationCount;
        saveGame.OperationCompleted -= HandleSaveOperationCompleted;
        saveGame.OperationCompleted += HandleSaveOperationCompleted;
        SetStage("save_rollback", commandLine);
        if (!saveGame.TrySaveSlot(
                rollbackSlot,
                "BB 6F RESERVATIONS QUEEN ROLLBACK",
                out string rejection))
            FailWithRollback("6F: no pudo iniciarse el rollback: " + rejection, commandLine);
    }
    private static void HandleSaveOperationCompleted(BistroBuilderSaveOperationResult result)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(stage) || stage.StartsWith("exit_", StringComparison.Ordinal))
            return;

        if (result == null || !result.Succeeded)
        {
            string failure = "6F: SaveGame falló en " + stage + ": " +
                (result != null ? result.Message : "resultado nulo");
            if (stage.StartsWith("load_rollback_", StringComparison.Ordinal))
            {
                pendingMessage += " Además falló rollback: " + failure;
                BeginDeleteCheckpoint(commandLine);
            }
            else
            {
                FailWithRollback(failure, commandLine);
            }
            return;
        }

        if (stage.StartsWith("save_rollback_", StringComparison.Ordinal))
        {
            ExecuteReservationScenario(commandLine);
            return;
        }

        if (stage.StartsWith("save_active_", StringComparison.Ordinal))
        {
            MutateThenLoadActive(commandLine);
            return;
        }

        if (stage.StartsWith("load_active_", StringComparison.Ordinal))
        {
            ValidateActiveLoadAndContinue(commandLine);
            return;
        }
        if (stage.StartsWith("load_rollback_", StringComparison.Ordinal))
        {
            ValidateRollbackThenCleanup(commandLine);
            return;
        }

        if (stage.StartsWith("delete_checkpoint_", StringComparison.Ordinal))
        {
            if (rollbackSlot >= 0 && saveGame.SlotExists(rollbackSlot))
            {
                SetStage("delete_rollback", commandLine);
                if (!saveGame.TryDeleteSlot(rollbackSlot, out string rejection))
                {
                    desiredSuccess = false;
                    pendingMessage += " No se pudo eliminar rollback: " + rejection;
                    FinalExit(false, pendingMessage, commandLine);
                }
            }
            else
            {
                FinalExit(desiredSuccess, pendingMessage, commandLine);
            }
            return;
        }

        if (stage.StartsWith("delete_rollback_", StringComparison.Ordinal))
        {
            FinalExit(desiredSuccess, pendingMessage, commandLine);
        }
    }

    private static void ExecuteReservationScenario(bool commandLine)
    {
        int minute = clock.Hour * 60 + clock.Minute;
        var draft = new BistroBuilderReservationDraft
        {
            guestName = "6F Queen",
            partySize = 4,
            dayIndex = gameState.DayIndex,
            arrivalMinute = minute,
            durationMinutes = 90,
            notes = "Queen Test reversible"
        };

        if (!playerFacade.TryCreateAndAssign(
                draft,
                out BistroBuilderReservationRecord created,
                out string createError) || created == null || created.tableId < 1)
        {
            FailWithRollback("6F: UI/fachada no creó y asignó reserva: " + createError,
                commandLine);
            return;
        }
        reservationId = created.reservationId;
        tableId = created.tableId;

        playerScreen.Show();
        string selectError = string.Empty;
        if (!playerScreen.IsVisible ||
            !playerScreen.TrySelectReservation(reservationId, out selectError))
        {
            FailWithRollback(
                "6F: la agenda jugable no puede seleccionar la reserva Queen: " + selectError,
                commandLine);
            return;
        }
        playerScreen.Hide();

        if (!serviceState.TryOpenService())
        {
            FailWithRollback("6F: no pudo abrirse el servicio real.", commandLine);
            return;
        }

        clock.SetSpeedMultiplier(4f);
        clock.SetPaused(false);
        integration.RequestEvaluation();
        startedAt = EditorApplication.timeSinceStartup;
        SetStage("wait_seated", commandLine);
        Debug.Log(
            "6F Queen: reserva " + reservationId +
            " asignada a mesa " + tableId +
            "; esperando llegada y seating reales.");
    }

    private static void PollSeated(bool commandLine)
    {
        if (reservations.TryGetReservation(
                reservationId,
                out BistroBuilderReservationRecord record) &&
            record != null && record.status == BistroBuilderReservationStatus.Seated &&
            integration.TryGetActiveGroup(reservationId, out CustomerGroup group) &&
            group != null && group.HasAssignedTable &&
            group.AssignedTable != null &&
            group.AssignedTable.TableId == tableId &&
            group.AssignedTable.AssignedCustomerGroup == group)
        {
            groupId = group.GroupId;
            clock.SetPaused(true);
            SetStage("save_active", commandLine);
            if (!saveGame.TrySaveSlot(
                    checkpointSlot,
                    "BB 6F ACTIVE RESERVATION CHECKPOINT",
                    out string rejection))
                FailWithRollback("6F: checkpoint activo rechazado: " + rejection, commandLine);
            return;
        }
        if (EditorApplication.timeSinceStartup - startedAt > SeatingTimeoutSeconds)
            FailWithRollback(
                "6F: timeout esperando que la reserva alcance Seated.",
                commandLine);
    }

    private static void MutateThenLoadActive(bool commandLine)
    {
        if (!reservations.TryResetForLegacyLoad(out string resetError))
        {
            FailWithRollback("6F: mutación previa a Load activo falló: " + resetError,
                commandLine);
            return;
        }

        if (reservations.TryGetReservation(reservationId, out _))
        {
            FailWithRollback("6F: la mutación de control no eliminó la reserva activa.",
                commandLine);
            return;
        }

        SetStage("load_active", commandLine);
        if (!saveGame.TryLoadSlot(checkpointSlot, out string rejection))
            FailWithRollback("6F: Load activo fue rechazado: " + rejection, commandLine);
    }

    private static void ValidateActiveLoadAndContinue(bool commandLine)
    {
        if (!Resolve(out string resolveError))
        {
            FailWithRollback("6F: tras Load activo: " + resolveError, commandLine);
            return;
        }

        if (!reservations.TryGetReservation(
                reservationId,
                out BistroBuilderReservationRecord restored) ||
            restored == null || restored.status != BistroBuilderReservationStatus.Seated ||
            restored.tableId != tableId ||
            !integration.TryGetActiveGroup(reservationId, out CustomerGroup group) ||
            group == null || group.GroupId != groupId || !group.HasAssignedTable ||
            group.AssignedTable == null || group.AssignedTable.TableId != tableId ||
            group.AssignedTable.AssignedCustomerGroup != group)
        {
            FailWithRollback(
                "6F: Load activo no restauró ReservationId/GroupId/mesa exactos.",
                commandLine);
            return;
        }
        playerScreen.Show();
        if (!playerScreen.TrySelectReservation(reservationId, out string selectError))
        {
            FailWithRollback(
                "6F: la UI no reenlazó la reserva tras Load activo: " + selectError,
                commandLine);
            return;
        }
        playerScreen.Hide();

        clock.SetSpeedMultiplier(6f);
        clock.SetPaused(false);
        startedAt = EditorApplication.timeSinceStartup;
        SetStage("wait_completed", commandLine);
        Debug.Log(
            "6F Queen: Save/Load activo validado; esperando servicio completo real.");
    }

    private static void PollCompleted(bool commandLine)
    {
        if (reservations.TryGetReservation(
                reservationId,
                out BistroBuilderReservationRecord record) &&
            record != null && record.status == BistroBuilderReservationStatus.Completed)
        {
            if (integration.TryGetActiveGroup(reservationId, out CustomerGroup active) &&
                active != null)
            {
                FailWithRollback(
                    "6F: reserva Completed conserva un CustomerGroup enlazado.",
                    commandLine);
                return;
            }

            BeginRollback(
                true,
                "PASS — reserva UI -> mesa " + tableId +
                " -> grupo " + groupId +
                " -> Seated -> Save/Load activo exacto -> Completed real -> rollback íntegro.",
                commandLine);
            return;
        }

        if (EditorApplication.timeSinceStartup - startedAt > CompletionTimeoutSeconds)
            FailWithRollback(
                "6F: timeout esperando Completion real del servicio.",
                commandLine);
    }
    private static void FailWithRollback(string message, bool commandLine)
    {
        BeginRollback(false, message, commandLine);
    }

    private static void BeginRollback(
        bool success,
        string message,
        bool commandLine)
    {
        desiredSuccess = success;
        pendingMessage = message;

        if (saveGame == null || rollbackSlot < 0 || !saveGame.SlotExists(rollbackSlot))
        {
            FinalExit(false,
                success
                    ? message + " No existe checkpoint de rollback para restaurar el estado inicial."
                    : message,
                commandLine);
            return;
        }

        SetStage("load_rollback", commandLine);
        if (!saveGame.TryLoadSlot(rollbackSlot, out string rejection))
        {
            desiredSuccess = false;
            pendingMessage += " Rollback rechazado: " + rejection;
            BeginDeleteCheckpoint(commandLine);
        }
    }

    private static void ValidateRollbackThenCleanup(bool commandLine)
    {
        if (!Resolve(out string resolveError))
        {
            desiredSuccess = false;
            pendingMessage += " Tras rollback no se resolvieron autoridades: " + resolveError;
        }
        else
        {
            if (reservations.ReservationCount != initialReservationCount ||
                reservations.TryGetReservation(reservationId, out _) ||
                !serviceState.IsClosed ||
                integration.TryGetActiveGroup(reservationId, out _))
            {
                desiredSuccess = false;
                pendingMessage += " El rollback no restauró exactamente Reservas/servicio.";
            }
        }

        BeginDeleteCheckpoint(commandLine);
    }
    private static void BeginDeleteCheckpoint(bool commandLine)
    {
        if (saveGame != null && checkpointSlot >= 0 && saveGame.SlotExists(checkpointSlot))
        {
            SetStage("delete_checkpoint", commandLine);
            if (!saveGame.TryDeleteSlot(checkpointSlot, out string rejection))
            {
                desiredSuccess = false;
                pendingMessage += " No se pudo eliminar checkpoint activo: " + rejection;
                BeginDeleteRollback(commandLine);
            }
            return;
        }

        BeginDeleteRollback(commandLine);
    }

    private static void BeginDeleteRollback(bool commandLine)
    {
        if (saveGame != null && rollbackSlot >= 0 && saveGame.SlotExists(rollbackSlot))
        {
            SetStage("delete_rollback", commandLine);
            if (!saveGame.TryDeleteSlot(rollbackSlot, out string rejection))
            {
                desiredSuccess = false;
                pendingMessage += " No se pudo eliminar rollback: " + rejection;
                FinalExit(false, pendingMessage, commandLine);
            }
            return;
        }

        FinalExit(desiredSuccess, pendingMessage, commandLine);
    }

    private static bool Resolve(out string error)
    {
        saveGame = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
        reservations = UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationService>();
        availability = UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationAvailabilityService>();
        integration = UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationServiceIntegration>();
        playerFacade = UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationPlayerFacade>(
            FindObjectsInactive.Include);
        playerScreen = UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationPlayerScreen>(
            FindObjectsInactive.Include);
        gameState = UnityEngine.Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();
        clock = UnityEngine.Object.FindFirstObjectByType<GameClock>();
        if (saveGame == null || reservations == null || availability == null ||
            integration == null || playerFacade == null || playerScreen == null ||
            gameState == null || serviceState == null || clock == null)
        {
            error = "faltan autoridades runtime de Reservas/Save/UI/servicio.";
            return false;
        }

        if (!reservations.ValidateConfiguration(out error)) return false;
        if (!availability.ValidateConfiguration(out error)) return false;
        if (!integration.ValidateConfiguration(out error)) return false;
        if (!playerFacade.ValidateConfiguration(out error)) return false;
        if (!playerScreen.ValidateConfiguration(out error)) return false;
        if (!gameState.ValidateConfiguration(out error)) return false;

        error = string.Empty;
        return true;
    }

    private static bool TryFindTwoFreeSlots(out int first, out int second)
    {
        first = -1;
        second = -1;
        for (int slot = 960; slot <= 979; slot++)
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

    private static void SetStage(string stage, bool commandLine)
    {
        SessionState.SetString(
            StageKey,
            stage + (commandLine ? "_cli" : "_menu"));
    }
    private static void FinalExit(bool success, string message, bool commandLine)
    {
        if (saveGame != null)
            saveGame.OperationCompleted -= HandleSaveOperationCompleted;

        string report =
            "=== BISTRO BUILDER — 6F / QUEEN TEST REVERSIBLE ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        SessionState.SetBool(SuccessKey, success);
        SetStage("exit", commandLine);
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }
}
