using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderEndOfDay15PlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.EndDay15.Play.Stage";
    private const string SuccessKey = "BB.EndDay15.Play.Success";
    private const string ReportPath = "EndOfDay15PlayModeReport.txt";
    private static BistroBuilderEndOfDayService service;
    private static RestaurantServiceStateService serviceState;
    private static CustomerGroupSpawner spawner;
    private static int initialDay;
    private static double started;

    static BistroBuilderEndOfDay15PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Tools/Bistro Builder/End Of Day/15 - PlayMode real", false, 15003)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 15 ya esta ejecutandose.");
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
            SessionState.SetString(StageKey, stage.EndsWith("cli", StringComparison.Ordinal) ? "setup_cli" : "setup_menu");
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
        if (string.IsNullOrEmpty(stage) || stage.StartsWith("exit_", StringComparison.Ordinal)) return;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        try
        {
            if (stage.StartsWith("setup_", StringComparison.Ordinal))
            {
                Setup();
                if (!service.TryBeginEndOfService(out string closeError))
                    throw new InvalidOperationException("No pudo iniciar cierre real: " + closeError);
                started = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "closing_cli" : "closing_menu");
                return;
            }
            if (stage.StartsWith("closing_", StringComparison.Ordinal))
            {
                if (service.Phase != BistroBuilderEndOfDayPhase.SummaryReady)
                {
                    if (EditorApplication.timeSinceStartup - started > 8d)
                        throw new TimeoutException("El cierre operativo no alcanzo SummaryReady.");
                    return;
                }
                if (!serviceState.IsClosed)
                    throw new InvalidOperationException("El cierre dejo el restaurante activo.");
                string summaryError = string.Empty;
                if (!service.TryGetLatestSummary(out BistroBuilderEndOfDaySummary summary) ||
                    !BistroBuilderEndOfDayEngine.TryValidateSummary(summary, out summaryError))
                    throw new InvalidOperationException("Resumen real invalido: " + summaryError);
                if (summary.dayIndex != initialDay)
                    throw new InvalidOperationException("El resumen se atribuyo a un dia incorrecto.");

                BistroBuilderEndOfDayHistorySnapshot saved = service.CreateSnapshot();
                if (!BistroBuilderEndOfDayEngine.TryValidateSnapshot(saved, out string snapshotError))
                    throw new InvalidOperationException("Snapshot de cierre invalido: " + snapshotError);
                string restoreError = string.Empty;
                if (!service.TryResetForLegacyLoad(out _) ||
                    !service.TryRestoreSnapshot(saved, out restoreError) ||
                    !service.TryGetLatestSummary(out BistroBuilderEndOfDaySummary restored) ||
                    restored.dayIndex != initialDay)
                    throw new InvalidOperationException("Roundtrip persistente del cierre fallo: " + restoreError);

                if (!service.TryAdvanceToNextDay(out string dayError))
                    throw new InvalidOperationException("Avance al siguiente dia fallo: " + dayError);
                BistroBuilderGeneralGameStateService calendar =
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
                if (calendar == null || calendar.DayIndex != initialDay + 1 ||
                    serviceState.CurrentState != RestaurantServiceState.Preparing)
                    throw new InvalidOperationException("El nuevo dia no quedo preparado correctamente.");

                Finish(true,
                    "PASS - cierre operativo, bloqueo de nuevas entradas, consolidacion, resumen, comparacion/persistencia y preparacion del siguiente dia funcionan en Play Mode real.", cli);
            }
        }
        catch (Exception exception)
        {
            Finish(false, "15 PlayMode: " + exception.Message, cli);
        }
    }

    private static void Setup()
    {
        service = UnityEngine.Object.FindFirstObjectByType<BistroBuilderEndOfDayService>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();
        spawner = UnityEngine.Object.FindFirstObjectByType<CustomerGroupSpawner>();
        BistroBuilderGeneralGameStateService calendar =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        if (service == null || serviceState == null || spawner == null || calendar == null)
            throw new InvalidOperationException("Faltan autoridades runtime del Bloque 15.");
        if (!service.ValidateConfiguration(out string error))
            throw new InvalidOperationException(error);
        spawner.enabled = false;
        initialDay = calendar.DayIndex;
        if (!serviceState.IsClosed) serviceState.TryCloseServiceImmediately();
        if (!serviceState.TryOpenService())
            throw new InvalidOperationException("No pudo abrirse servicio de diagnostico 15.");
        if (service.Phase != BistroBuilderEndOfDayPhase.ServiceOpen)
            throw new InvalidOperationException("Bloque 15 no capturo la apertura del servicio.");
        if (!serviceState.AcceptsNewCustomers)
            throw new InvalidOperationException("El fixture no alcanzo estado Open.");
    }

    private static void Finish(bool ok, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER - BLOQUE 15 / PLAY MODE REAL ===\n" +
                        (ok ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, ok);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
