using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prueba funcional 6E: usa la UI instalada para abrir agenda,
/// crear, editar y cancelar una reserva, y restaura el snapshot original.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderReservations6EPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Reservations.6E.Play.Stage";
    private const string SuccessKey = "BB.Reservations.6E.Play.Success";
    private const string ReportPath = "Block6EPlayModeReport.txt";
    private static BistroBuilderReservationsSnapshot originalSnapshot;

    static BistroBuilderReservations6EPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }
    [MenuItem("Tools/Bistro Builder/Reservations/6E - PlayMode UI self-test", false, 651)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("6E UI self-test ya estÃ¡ ejecutÃ¡ndose.");

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
            SessionState.SetString(StageKey,
                stage.EndsWith("cli", StringComparison.Ordinal) ? "run_cli" : "run_menu");

        if (state == PlayModeStateChange.EnteredEditMode)
            FinishEditor(stage.Contains("cli", StringComparison.Ordinal));
    }
    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 4) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;
        SessionState.SetString(StageKey,
            stage.EndsWith("cli", StringComparison.Ordinal) ? "busy_cli" : "busy_menu");
        Execute(stage.EndsWith("cli", StringComparison.Ordinal));
    }

    private static void Execute(bool commandLine)
    {
        BistroBuilderReservationPlayerScreen screen =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationPlayerScreen>(
                FindObjectsInactive.Include);
        BistroBuilderReservationPlayerFacade facade =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationPlayerFacade>(
                FindObjectsInactive.Include);
        BistroBuilderReservationService reservations =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationService>();

        if (screen == null || facade == null || reservations == null)
        {
            Finish(false, "6E: faltan Screen, Facade o ReservationService.", commandLine);
            return;
        }

        originalSnapshot = reservations.CreateSnapshot();
        if (!screen.ValidateConfiguration(out string configError))
        {
            Finish(false, "6E: configuraciÃ³n invÃ¡lida: " + configError, commandLine);
            return;
        }
        Button launcher = FindButton(screen.gameObject, "OpenReservationsButton");
        TMP_InputField guest = FindInput(screen.gameObject, "GuestInput");
        TMP_InputField notes = FindInput(screen.gameObject, "NotesInput");
        Button partyPlus = FindButton(screen.gameObject, "PartyPlus");
        Button timePlus = FindButton(screen.gameObject, "TimePlus");
        Button save = FindButton(screen.gameObject, "SaveReservation");
        Button cancel = FindButton(screen.gameObject, "CancelReservation");
        Button nextDay = FindButton(screen.gameObject, "NextDay");
        Button previousDay = FindButton(screen.gameObject, "PreviousDay");
        Button close = FindButton(screen.gameObject, "Close");

        if (launcher == null || guest == null || notes == null ||
            partyPlus == null || timePlus == null || save == null || cancel == null ||
            nextDay == null || previousDay == null || close == null)
        {
            Finish(false, "6E: faltan controles jugables requeridos.", commandLine);
            return;
        }

        launcher.onClick.Invoke();
        if (!screen.IsVisible)
        {
            Finish(false, "6E: el launcher no abre la pantalla.", commandLine);
            return;
        }

        int baseCount = reservations.ReservationCount;
        guest.text = "6E UI Test";
        notes.text = "creada desde la pantalla 6E";
        partyPlus.onClick.Invoke();
        save.onClick.Invoke();
        string reservationId = screen.SelectedReservationId;
        if (reservations.ReservationCount != baseCount + 1 ||
            string.IsNullOrWhiteSpace(reservationId) ||
            !reservations.TryGetReservation(reservationId, out BistroBuilderReservationRecord created) ||
            created == null || created.guestName != "6E UI Test" ||
            created.partySize != 3 || created.tableId < 1)
        {
            Finish(false, "6E: crear reserva desde UI no produjo datos/mesa esperados.", commandLine);
            return;
        }

        if (!screen.TrySelectReservation(reservationId, out string selectError))
        {
            Finish(false, "6E: seleccionar fila fallÃ³: " + selectError, commandLine);
            return;
        }

        guest.text = "6E UI Editada";
        notes.text = "ediciÃ³n jugable confirmada";
        timePlus.onClick.Invoke();
        save.onClick.Invoke();
        if (!reservations.TryGetReservation(reservationId, out BistroBuilderReservationRecord edited) ||
            edited == null || edited.guestName != "6E UI Editada" ||
            edited.arrivalMinute != 810 || edited.tableId < 1)
        {
            Finish(false, "6E: editar/reasignar desde UI no quedÃ³ aplicado.", commandLine);
            return;
        }

        int currentDay = screen.SelectedDayIndex;
        nextDay.onClick.Invoke();
        if (screen.SelectedDayIndex != currentDay + 1)
        {
            Finish(false, "6E: navegaciÃ³n al dÃ­a siguiente fallÃ³.", commandLine);
            return;
        }
        previousDay.onClick.Invoke();
        if (screen.SelectedDayIndex != currentDay)
        {
            Finish(false, "6E: navegaciÃ³n de vuelta al dÃ­a actual fallÃ³.", commandLine);
            return;
        }

        if (!screen.TrySelectReservation(reservationId, out selectError))
        {
            Finish(false, "6E: re-selecciÃ³n antes de cancelar fallÃ³: " + selectError, commandLine);
            return;
        }
        cancel.onClick.Invoke();
        cancel.onClick.Invoke();
        if (!reservations.TryGetReservation(reservationId, out BistroBuilderReservationRecord cancelled) ||
            cancelled == null || cancelled.status != BistroBuilderReservationStatus.Cancelled)
        {
            Finish(false, "6E: confirmaciÃ³n doble de cancelaciÃ³n no funcionÃ³.", commandLine);
            return;
        }

        close.onClick.Invoke();
        if (screen.IsVisible)
        {
            Finish(false, "6E: Cerrar no ocultÃ³ la pantalla.", commandLine);
            return;
        }

        if (!reservations.TryRestoreSnapshot(originalSnapshot, out string restoreError))
        {
            Finish(false, "6E: rollback final fallÃ³: " + restoreError, commandLine);
            return;
        }
        originalSnapshot = null;

        Finish(true,
            "PASS â€” launcher, agenda, creaciÃ³n, asignaciÃ³n, ediciÃ³n, navegaciÃ³n, cancelaciÃ³n y rollback UI.",
            commandLine);
    }
    private static Button FindButton(GameObject root, string name)
    {
        if (root == null) return null;
        Button[] values = root.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < values.Length; index++)
            if (values[index] != null && string.Equals(
                    values[index].name, name, StringComparison.Ordinal))
                return values[index];
        return null;
    }

    private static TMP_InputField FindInput(GameObject root, string name)
    {
        if (root == null) return null;
        TMP_InputField[] values = root.GetComponentsInChildren<TMP_InputField>(true);
        for (int index = 0; index < values.Length; index++)
            if (values[index] != null && string.Equals(
                    values[index].name, name, StringComparison.Ordinal))
                return values[index];
        return null;
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (!success && originalSnapshot != null)
        {
            BistroBuilderReservationService reservations =
                UnityEngine.Object.FindFirstObjectByType<BistroBuilderReservationService>();
            if (reservations != null && !reservations.TryRestoreSnapshot(
                    originalSnapshot, out string rollbackError))
                message += " Rollback fallÃ³: " + rollbackError;
            originalSnapshot = null;
        }
        string report =
            "=== BISTRO BUILDER â€” 6E / UI PLAY MODE ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey,
            commandLine ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static void FinishEditor(bool commandLine)
    {
        bool success = SessionState.GetBool(SuccessKey, false);
        SessionState.EraseString(StageKey);
        if (commandLine)
            EditorApplication.Exit(success ? 0 : 1);
    }
}
