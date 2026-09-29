using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderModeSelectorPlayTest
{
    private const string Key = "BB.ModeSelector.Test";
    private static int stage;
    private static double next;
    private static string failure;
    private static BistroBuilderUiShell shell;
    private static RestaurantEditModeService editMode;

    static BistroBuilderModeSelectorPlayTest()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying = true;
    }    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;

        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            failure = null;
            next = EditorApplication.timeSinceStartup + 3.0;
            Application.runInBackground = true;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(
                SessionState.GetBool(Key + ".Pass", false) ? 0 : 1);
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Assert) return;
        if (message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal) &&
            stack.IndexOf("UnityEditor.Search.SearchDatabase", StringComparison.Ordinal) >= 0)
            return;
        failure = message;
    }    private static RectTransform FindRect(string name)
    {
        RectTransform[] all = UnityEngine.Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private static Button FindButton(string name)
    {
        Button[] all = UnityEngine.Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private static void Check(bool condition, string error)
    {
        if (!condition) throw new Exception(error);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < next) return;

        next = EditorApplication.timeSinceStartup + 0.7;
        try
        {
            Check(failure == null, failure);            switch (stage++)
            {
                case 0:
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                    shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();
                    editMode = UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>();
                    Check(shell != null, "No UI shell");
                    Check(editMode != null, "No edit mode service");
                    shell.EnsureShell();
                    Check(FindButton("NormalMode") != null, "Missing Normal mode button");
                    Check(FindButton("EditMode") != null, "Missing Edit mode button");
                    FindButton("EditMode").onClick.Invoke();
                    break;

                case 1:
                    Check(editMode.IsEditModeActive, "Edit mode did not activate");
                    Check(!FindRect(BistroBuilderUiShell.TopBarName).gameObject.activeSelf,
                        "Normal top bar visible in Edit mode");
                    Check(FindRect(BistroBuilderUiShell.EditModeTopBarName).gameObject.activeSelf,
                        "Edit top bar not visible");
                    shell.GetComponent<BistroBuilderOptionsScreen>().Open();
                    break;                case 2:
                    Check(editMode.IsEditModeActive, "Edit mode lost while Options open");
                    Check(!FindRect(BistroBuilderUiShell.TopBarName).gameObject.activeSelf,
                        "Normal top bar leaked into Edit mode with management UI");
                    shell.GetComponent<BistroBuilderOptionsScreen>().Close();
                    FindButton("NormalMode").onClick.Invoke();
                    break;

                case 3:
                    Check(!editMode.IsEditModeActive, "Normal mode did not activate");
                    Check(FindRect(BistroBuilderUiShell.TopBarName).gameObject.activeSelf,
                        "Normal top bar not visible");
                    Check(!FindRect(BistroBuilderUiShell.EditModeTopBarName).gameObject.activeSelf,
                        "Edit top bar leaked into Normal mode");
                    Finish(true,
                        "explicit Normal/Edit switching and mutually exclusive mode UI");
                    break;
            }
        }
        catch (Exception error)
        {
            Finish(false, error.ToString());
        }
    }    private static void Finish(bool pass, string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;

        Directory.CreateDirectory("Logs");
        string result = (pass ? "PASS " : "FAIL ") + message;
        File.WriteAllText("Logs/ModeSelectorTest.txt", result);
        Debug.Log("BB_MODE_SELECTOR_" + result);

        SessionState.SetBool(Key + ".Pass", pass);
        EditorApplication.isPlaying = false;
    }
}