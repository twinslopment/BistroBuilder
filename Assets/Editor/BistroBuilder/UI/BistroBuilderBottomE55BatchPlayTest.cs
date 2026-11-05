using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Prueba automatica ejecutable en batch, con Play Mode real.
[InitializeOnLoad]
public static class BistroBuilderBottomE55BatchPlayTest
{
    private const string Key = "BB.E55.BatchSmoke";
    private static double next;
    private static string failure;

    static BistroBuilderBottomE55BatchPlayTest()
    {
        EditorApplication.playModeStateChanged += OnModeChanged;
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory("Logs");
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            failure = null;
            next = EditorApplication.timeSinceStartup + 3;
            Application.runInBackground = true;
            EditorApplication.update += RunSmoke;
            Application.logMessageReceived += OnLog;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + ".Pass", false) ? 0 : 1);
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Assert && type != LogType.Exception) return;
        // Incidencia conocida del indexador de Unity Editor, ajena al HUD.
        if (message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal) &&
            stack.IndexOf("UnityEditor.Search.SearchDatabase", StringComparison.Ordinal) >= 0)
            return;
        if (failure == null) failure = message + "\n" + stack;
    }

    private static void Check(bool condition, string error)
    {
        if (!condition) throw new InvalidOperationException(error);
    }

    private static void RunSmoke()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            Check(failure == null, "Exception in play mode: " + failure);

            var opening = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(FindObjectsInactive.Include);
            if (opening != null) opening.Hide();

            var shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>(FindObjectsInactive.Include);
            Check(shell != null, "BistroBuilderUiShell missing");
            shell.EnsureShell();
            var bottom = GameObject.Find(BistroBuilderUiShell.BottomBarName);
            Check(bottom != null, "BottomOperations missing");
            var e55 = bottom.GetComponent<BistroBuilderBottomE55Hud>();
            Check(e55 != null, "E5.5 missing from existing shell");
            Check(e55.ValidateConfiguration(out string error), error);
            var root = bottom.transform.Find(BistroBuilderBottomE55Hud.VisualRootName);
            Check(root != null, "E5.5 root missing");
            var row = root.Find("ContenidoE55");
            Check(row != null && row.childCount == 4, "Exactly four visual modules required");
            foreach (var name in new [] { "CajaE55", "SatisfaccionE55", "ClimaE55", "VelocidadE55" })
                Check(row.Find(name) != null, "Missing module " + name);
            var speed = row.Find("VelocidadE55");
            var pause = speed.Find("Pausa")?.GetComponent<Button>();
            var speed1 = speed.Find("Speed1")?.GetComponent<Button>();
            var speed2 = speed.Find("Speed2")?.GetComponent<Button>();
            var speed3 = speed.Find("Speed3")?.GetComponent<Button>();
            Check(pause != null && speed1 != null && speed2 != null && speed3 != null,
                "Missing time controls");
            var clock = UnityEngine.Object.FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
            Check(clock != null, "GameClock missing");
            Check(!clock.IsRuntimeSuspended, "GameClock runtime suspended during test");
            float originalSpeed = clock.SpeedMultiplier;
            bool originalPause = clock.IsPaused;
            try
            {
                speed2.onClick.Invoke();
                Check(Mathf.Approximately(clock.SpeedMultiplier, 2f), "Speed 2x failed");
                speed3.onClick.Invoke();
                Check(Mathf.Approximately(clock.SpeedMultiplier, 3f), "Speed 3x failed");
                speed1.onClick.Invoke();
                Check(Mathf.Approximately(clock.SpeedMultiplier, 1f), "Speed 1x failed");
                pause.onClick.Invoke();
                Check(clock.IsPaused != originalPause, "Pause failed");
                pause.onClick.Invoke();
                Check(clock.IsPaused == originalPause, "Unpause failed");
            }
            finally
            {
                clock.SetSpeedMultiplier(originalSpeed);
                clock.SetPaused(originalPause);
            }
            var canvas = shell.GetComponentInParent<Canvas>();
            var oldDock = canvas != null ? canvas.transform.Find("BB_368B_TimeControlsDock") : null;
            if (oldDock != null)
            {
                var group = oldDock.GetComponent<CanvasGroup>();
                Check(group != null && group.alpha == 0f && !group.blocksRaycasts,
                    "Old 368B dock still visible or blocking input. " +
                    "E55 active=" + e55.isActiveAndEnabled +
                    ", dock active=" + oldDock.gameObject.activeInHierarchy +
                    ", group=" + (group != null ? group.alpha.ToString("0.00") + "/" + group.blocksRaycasts : "NULL"));
            }
            shell.EnsureShell();
            Check(bottom.GetComponents<BistroBuilderBottomE55Hud>().Length == 1,
                "Non-idempotent HUD installation");
            Check(row.childCount == 4, "Duplicated modules after EnsureShell");
            var legacy = bottom.transform.Find("StatusContent");
            Check(legacy == null || !legacy.gameObject.activeSelf, "Legacy status visible");
            File.WriteAllText("Logs/BottomE55_PlayMode_Result.txt",
                "PASS: 4 modules, responsive hierarchy, canonical clock controls, idempotence and legacy dock.\n");
            Debug.Log("BB_BOTTOM_E55_BATCH_PASS");
            Finish(true, "E55 Play Mode smoke passed");
        }
        catch (Exception ex)
        {
            Finish(false, ex.ToString());
        }
    }

    private static void Finish(bool pass, string message)
    {
        EditorApplication.update -= RunSmoke;
        Application.logMessageReceived -= OnLog;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BottomE55_PlayMode_Result.txt", (pass ? "PASS " : "FAIL ") + message);
        Debug.Log((pass ? "BB_BOTTOM_E55_BATCH_PASS " : "BB_BOTTOM_E55_BATCH_FAIL ") + message);
        SessionState.SetBool(Key + ".Pass", pass);
        EditorApplication.isPlaying = false;
    }
}
