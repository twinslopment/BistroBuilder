using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderNewGame16PlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.NewGame16.Play.Stage";
    private const string SuccessKey = "BB.NewGame16.Play.Success";
    private const string ReportPath = "NewGame16PlayModeReport.txt";
    private const int DiagnosticSlot = 99;

    private static BistroBuilderNewGameOpeningService service;
    private static BistroBuilderSaveGameService save;
    private static double stageStarted;

    static BistroBuilderNewGame16PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Opening/16 - PlayMode real", false, 16003)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 16 ya esta ejecutandose.");
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
                service = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningService>();
                save = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
                string config = string.Empty;
                if (service == null || save == null || !service.ValidateConfiguration(out config))
                    throw new InvalidOperationException("Bloque 16 no esta operativo: " + config);
                SetPrivate(service, "defaultSaveSlot", DiagnosticSlot);
                if (save.SlotExists(DiagnosticSlot) && !save.TryDeleteSlot(DiagnosticSlot, out string deleteError))
                    throw new InvalidOperationException("No pudo limpiar slot diagnostico previo: " + deleteError);
                stageStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "wait_clean_cli" : "wait_clean_menu");
                return;
            }
            if (stage.StartsWith("wait_clean_", StringComparison.Ordinal))
            {
                if (save.IsBusy)
                {
                    Timeout(stageStarted, 8d, "limpieza inicial del slot");
                    return;
                }
                if (!service.TryCreateNewGame("Bistro Apertura Test",
                        BistroBuilderStartingPremisesProfile.Balanced, out string createError))
                    throw new InvalidOperationException("Crear nueva partida fallo: " + createError);
                stageStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "wait_save_cli" : "wait_save_menu");
                return;
            }
            if (stage.StartsWith("wait_save_", StringComparison.Ordinal))
            {
                if (save.IsBusy)
                {
                    Timeout(stageStarted, 12d, "guardado inicial");
                    return;
                }
                if (!save.SlotExists(DiagnosticSlot) || !service.CanContinue)
                    throw new InvalidOperationException("El guardado inicial no habilito Continuar.");
                if (!service.TryAcknowledgeBriefing(out string mutateError) ||
                    service.Phase != BistroBuilderNewGamePhase.ReadyToOpen)
                    throw new InvalidOperationException("No pudo mutarse el estado antes de probar Continuar: " + mutateError);
                if (!service.TryContinue(out string continueError))
                    throw new InvalidOperationException("Continuar no pudo iniciar la carga: " + continueError);
                stageStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "wait_load_cli" : "wait_load_menu");
                return;
            }
            if (stage.StartsWith("wait_load_", StringComparison.Ordinal))
            {
                if (save.IsBusy)
                {
                    Timeout(stageStarted, 12d, "carga de Continuar");
                    return;
                }
                BistroBuilderNewGameStateSnapshot restored = service.CreateSnapshot();
                if (!restored.setupCompleted || restored.phase != BistroBuilderNewGamePhase.Briefing ||
                    restored.briefingAcknowledged || !string.Equals(restored.restaurantName, "Bistro Apertura Test", StringComparison.Ordinal))
                    throw new InvalidOperationException("Continuar no restauro el checkpoint inicial de nueva partida.");
                if (!service.TryRunOpeningPreflight(out BistroBuilderOpeningPreflightReport report, out string preflightError))
                    throw new InvalidOperationException("Preflight tras Continuar fallo: " + preflightError);
                if (!report.CanOpen || report.passedCount < 7 || report.blockerCount != 0)
                    throw new InvalidOperationException("El restaurante restaurado no supera la validacion previa.");
                if (!service.TryAcknowledgeBriefing(out string briefingError))
                    throw new InvalidOperationException("Briefing fallo: " + briefingError);
                if (!service.TryOpenFirstService(out string openError))
                    throw new InvalidOperationException("Primera apertura fallo: " + openError);
                if (!service.TryTransitionToNormalPlay(out string normalError))
                    throw new InvalidOperationException("Transicion a juego normal fallo: " + normalError);
                BistroBuilderNewGameStateSnapshot snapshot = service.CreateSnapshot();
                if (!snapshot.setupCompleted || !snapshot.briefingAcknowledged ||
                    !snapshot.firstOpeningCompleted || !snapshot.firstServiceStarted ||
                    !snapshot.transitionedToNormalPlay ||
                    snapshot.phase != BistroBuilderNewGamePhase.NormalPlay)
                    throw new InvalidOperationException("El flujo inicial no alcanzo juego normal.");
                if (!save.TryDeleteSlot(DiagnosticSlot, out string cleanupError))
                    throw new InvalidOperationException("No pudo eliminarse el slot diagnostico: " + cleanupError);
                stageStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "cleanup_cli" : "cleanup_menu");
                return;
            }
            if (stage.StartsWith("cleanup_", StringComparison.Ordinal))
            {
                if (save.IsBusy)
                {
                    Timeout(stageStarted, 8d, "limpieza final del slot");
                    return;
                }
                if (save.SlotExists(DiagnosticSlot))
                    throw new InvalidOperationException("El slot diagnostico quedo persistido.");
                Finish(true,
                    "PASS - nueva partida, identidad, configuracion inicial, stock, personal, carta, horarios, preflight, briefing, guardado inicial, Continuar, primera apertura y transicion al juego normal funcionan en Play Mode real.", cli);
            }
        }
        catch (Exception exception)
        {
            Finish(false, "16 PlayMode: " + Unwrap(exception).Message, cli);
        }
    }

    private static void Timeout(double started, double seconds, string action)
    {
        if (EditorApplication.timeSinceStartup - started > seconds)
            throw new TimeoutException("Timeout durante " + action + ".");
    }

    private static void Finish(bool ok, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER - BLOQUE 16 / PLAY MODE REAL ===\n" +
                        (ok ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, ok);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) throw new MissingFieldException(target.GetType().Name, field);
        info.SetValue(target, value);
    }

    private static Exception Unwrap(Exception e) =>
        e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
}
