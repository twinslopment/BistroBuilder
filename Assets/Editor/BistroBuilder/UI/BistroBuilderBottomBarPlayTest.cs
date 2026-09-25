using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderBottomBarPlayTest
{
    private const string Key = "BB.BottomBar.Test";
    private static int stage;
    private static double next;
    private static BistroBuilderUiShell shell;
    private static GameClock clock;
    private static string failure;

    static BistroBuilderBottomBarPlayTest()
    {
        EditorApplication.playModeStateChanged += State;
    }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying = true;
    }    private static void State(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;

        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            failure = null;
            next = EditorApplication.timeSinceStartup + 3.0;
            Application.runInBackground = true;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }

        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(
                SessionState.GetBool(Key + ".Pass", false) ? 0 : 1);
        }
    }

    private static void Log(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Assert) return;
        if (message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal) &&
            stack.IndexOf("UnityEditor.Search.SearchDatabase", StringComparison.Ordinal) >= 0)
            return;
        failure = message;
    }    private static void Check(bool condition, string error)
    {
        if (!condition) throw new Exception(error);
    }

    private static GameObject Find(string name)
    {
        return GameObject.Find(name);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < next) return;

        next = EditorApplication.timeSinceStartup + 0.5;

        try
        {
            Check(failure == null, failure);

            switch (stage++)
            {
                case 0:
                    UnityEngine.Object
                        .FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                    shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();
                    clock = UnityEngine.Object.FindFirstObjectByType<GameClock>();
                    Check(shell != null, "No shell");
                    Check(clock != null, "No GameClock");
                    shell.EnsureShell();
                    ValidateStructure();
                    break;                case 1:
                    Button speed2 = Find("Speed2")?.GetComponent<Button>();
                    Check(speed2 != null, "Missing Speed2");
                    speed2.onClick.Invoke();
                    Check(Mathf.Approximately(clock.SpeedMultiplier, 2f),
                        "Speed2 is not connected to GameClock");
                    break;

                case 2:
                    Button pause = Find("Pause")?.GetComponent<Button>();
                    Check(pause != null, "Missing pause button");
                    bool before = clock.IsPaused;
                    pause.onClick.Invoke();
                    Check(clock.IsPaused != before,
                        "Pause is not connected to GameClock");
                    pause.onClick.Invoke();
                    clock.SetSpeedMultiplier(1f);
                    Capture();
                    Finish(true,
                        "responsive frame / 15-icon atlas / runtime status / identity / weather / clock controls / screenshot");
                    break;
            }
        }
        catch (Exception error)
        {
            Finish(false, error.ToString());
        }
    }

    private static void ValidateStructure()
    {
        Texture2D atlas = Resources.Load<Texture2D>(
            "BistroBuilder/UI/BottomBar/BottomBarIconAtlas96");
        Texture2D atlasByPath = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Resources/BistroBuilder/UI/BottomBar/BottomBarIconAtlas96.png");
        Debug.Log("BB_BOTTOM_ATLAS|RES=" +
            (atlas != null ? atlas.width + "x" + atlas.height : "NULL") +
            "|ASSET=" +
            (atlasByPath != null ? atlasByPath.width + "x" + atlasByPath.height : "NULL"));
        Check(atlas != null, "Bottom icon atlas not loadable from Resources");

        RectTransform bar = Find(BistroBuilderUiShell.BottomBarName)
            ?.GetComponent<RectTransform>();
        Check(bar != null, "Missing normal bottom bar");
        Check(bar.rect.height >= 77f && bar.rect.height <= 109f,
            "Bottom bar height outside responsive limits: " + bar.rect.height);        RectTransform content = Find("BottomBarV2_Content")
            ?.GetComponent<RectTransform>();
        RectTransform left = Find("StatusCluster")?.GetComponent<RectTransform>();
        RectTransform center = Find("RestaurantIdentity")?.GetComponent<RectTransform>();
        RectTransform right = Find("TimeCluster")?.GetComponent<RectTransform>();
        Check(content != null && left != null && center != null && right != null,
            "Missing responsive three-zone layout");

        foreach (string tileName in new[]
        {
            "CashTile", "SatisfactionTile", "KitchenTile", "WaitingTile"
        })
        {
            GameObject tile = Find(tileName);
            Check(tile != null, "Missing " + tileName);
            Image icon = tile.transform.Find("Icon")?.GetComponent<Image>();
            Check(icon != null && icon.sprite != null,
                "Missing runtime icon in " + tileName);
        }

        TMP_Text restaurant = Find("RestaurantName")?.GetComponent<TMP_Text>();
        Check(restaurant != null && !string.IsNullOrWhiteSpace(restaurant.text),
            "Missing restaurant identity");

        Image weather = Find("WeatherIcon")?.GetComponent<Image>();
        Check(weather != null && weather.sprite != null, "Missing weather icon");

        foreach (string control in new[] { "Pause", "Speed1", "Speed2", "Speed3" })
            Check(Find(control)?.GetComponent<Button>() != null,
                "Missing control " + control);
    }    private static void Capture()
    {
        Canvas canvas = shell.GetComponentInParent<Canvas>();
        Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        Check(canvas != null && camera != null, "Capture dependencies missing");

        RenderMode oldMode = canvas.renderMode;
        Camera oldCanvasCamera = canvas.worldCamera;
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;

        RenderTexture target = new RenderTexture(1920, 1080, 24);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 0.5f;
        camera.targetTexture = target;

        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;

        Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/BottomBar1920.png", image.EncodeToPNG());

        RenderTexture.active = oldActive;
        camera.targetTexture = oldTarget;
        canvas.renderMode = oldMode;
        canvas.worldCamera = oldCanvasCamera;
        UnityEngine.Object.Destroy(target);
        UnityEngine.Object.Destroy(image);
    }    private static void Finish(bool pass, string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;

        string result = (pass ? "PASS " : "FAIL ") + message;
        File.WriteAllText("Logs/BottomBarTest.txt", result);
        Debug.Log("BB_BOTTOM_BAR_" + result);

        SessionState.SetBool(Key + ".Pass", pass);
        EditorApplication.isPlaying = false;
    }
}
