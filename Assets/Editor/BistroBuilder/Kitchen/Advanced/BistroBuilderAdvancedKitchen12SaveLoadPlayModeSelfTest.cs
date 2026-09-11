using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedKitchen12SaveLoadPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Kitchen12.SaveLoad.Stage";
    private const string SuccessKey = "BB.Kitchen12.SaveLoad.Success";
    private const string ReportPath = "AdvancedKitchen12SaveLoadReport.txt";
    private const double PlayReadyDelaySeconds = 0.25d;
    private static double playReadyAt;
    private const double TimeoutSeconds = 270d;

    private static BistroBuilderActiveServicePersistenceFunctionalTestWindow window;
    private static MethodInfo beginMethod;
    private static MethodInfo updateMethod;
    private static FieldInfo phaseField;
    private static FieldInfo reportField;
    private static double startedAt;
    private static double nextBatchSimulationStepAt;

    static BistroBuilderAdvancedKitchen12SaveLoadPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Kitchen/12 - SaveLoad real", false, 9104)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El Save/Load 12 ya está ejecutándose.");
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
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "run_cli" : "run_menu");
            playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
            startedAt = EditorApplication.timeSinceStartup;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            CleanupWindow();
            if (cli) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool batchCli = stage.EndsWith("cli", StringComparison.Ordinal);
        if (batchCli) EditorApplication.QueuePlayerLoopUpdate();
        if (playReadyAt <= 0d) playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
        if (EditorApplication.timeSinceStartup < playReadyAt) return;
        if (stage.StartsWith("run_", StringComparison.Ordinal))
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            try
            {
                CreateHarnessWindow();
                beginMethod.Invoke(window, null);
                SessionState.SetString(StageKey, cli ? "monitor_cli" : "monitor_menu");
                startedAt = EditorApplication.timeSinceStartup;
            }
            catch (Exception exception)
            {
                Finish(false, "No pudo iniciar el Save/Load real 12: " + Unwrap(exception).Message, cli);
            }
            return;
        }

        if (!stage.StartsWith("monitor_", StringComparison.Ordinal)) return;
        bool commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
        if (commandLine && window != null && phaseField != null)
        {
            string innerPhase = Convert.ToString(phaseField.GetValue(window));
            double now = EditorApplication.timeSinceStartup;
            if (string.Equals(innerPhase, "WaitingForPreparingOrder", StringComparison.Ordinal) && now >= nextBatchSimulationStepAt)
            {
                if (!EditorApplication.isPaused) EditorApplication.isPaused = true;
                EditorApplication.Step();
                nextBatchSimulationStepAt = now + 0.02d;
            }
        }
        if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
        {
            Finish(false, "Timeout del Save/Load real 12.", commandLine);
            return;
        }

        try
        {
            updateMethod.Invoke(window, null);
            string phase = Convert.ToString(phaseField.GetValue(window));
            if (string.Equals(phase, "Completed", StringComparison.Ordinal))
            {
                string report = Convert.ToString(reportField.GetValue(window));
                Finish(true, "PASS — " + report, commandLine);
            }
            else if (string.Equals(phase, "Failed", StringComparison.Ordinal))
            {
                string report = Convert.ToString(reportField.GetValue(window));
                Finish(false, report, commandLine);
            }
        }
        catch (Exception exception)
        {
            Finish(false, "Excepción en Save/Load real 12: " + Unwrap(exception).Message, commandLine);
        }
    }

    private static void CreateHarnessWindow()
    {
        window = ScriptableObject.CreateInstance<BistroBuilderActiveServicePersistenceFunctionalTestWindow>();
        Type type = typeof(BistroBuilderActiveServicePersistenceFunctionalTestWindow);
        beginMethod = type.GetMethod("BeginTest", BindingFlags.Instance | BindingFlags.NonPublic);
        updateMethod = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        phaseField = type.GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic);
        reportField = type.GetField("report", BindingFlags.Instance | BindingFlags.NonPublic);
        if (beginMethod == null || updateMethod == null || phaseField == null || reportField == null)
            throw new InvalidOperationException("El harness 368EF no expone el contrato de diagnóstico esperado.");
    }

    private static void Finish(bool success, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER — BLOQUE 12 / SAVE-LOAD REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        CleanupWindow();
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    private static void CleanupWindow()
    {
        if (window != null)
        {
            UnityEngine.Object.DestroyImmediate(window);
            window = null;
        }
        beginMethod = null;
        updateMethod = null;
        phaseField = null;
        reportField = null;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException tie && tie.InnerException != null)
            exception = tie.InnerException;
        return exception;
    }
}