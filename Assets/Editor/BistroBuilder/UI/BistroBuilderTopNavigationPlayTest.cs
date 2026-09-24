using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderTopNavigationPlayTest
{
    private const string Key = "BB.TopNavigation.Test";
    private static int stage;
    private static double next;
    private static BistroBuilderUiShell shell;
    private static string failure;

    static BistroBuilderTopNavigationPlayTest()
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

    private static Button Button(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < next) return;

        next = EditorApplication.timeSinceStartup + 0.6;

        try
        {
            Check(failure == null, failure);

            switch (stage++)
            {
                case 0:
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                    shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();
                    Check(shell != null, "No shell");
                    shell.EnsureShell();

                    GameObject background = GameObject.Find("ApprovedTopBarV3Background");
                    Check(background != null, "Missing approved v3 background");
                    Image image = background.GetComponent<Image>();
                    Check(image != null && image.sprite != null, "Approved v3 sprite missing");                    RectTransform topBar = GameObject.Find(BistroBuilderUiShell.TopBarName)?.GetComponent<RectTransform>();
                    Check(topBar != null && topBar.rect.height > 180f, "Approved top bar aspect/height not applied");

                    foreach (string name in new[]
                    {
                        "Actividad", "Personal", "Carta", "Inventario", "Proveedores",
                        "Reservas", "Economia", "Marketing", "Reputacion", "Opciones"
                    })
                    {
                        Button button = Button("BBNav_" + name);
                        Check(button != null, "Missing hotspot " + name);
                        Check(button.GetComponent<BistroBuilderApprovedTopBarHotspot>() != null,
                            "Missing approved hotspot behaviour " + name);
                    }

                    var personal = Button("BBNav_Personal");
                    personal.GetComponent<BistroBuilderApprovedTopBarHotspot>()
                        .OnPointerEnter(new PointerEventData(EventSystem.current));
                    break;

                case 1:
                    Check(Button("BBNav_Personal").GetComponent<Image>().color.a > 0.05f,
                        "Approved hover highlight missing");
                    Button("BBNav_Personal").onClick.Invoke();
                    break;                case 2:
                    Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffPlayerScreen>().IsVisible,
                        "Staff navigation");
                    Button("BBNav_Inventario").onClick.Invoke();
                    break;

                case 3:
                    Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseRuntimeView>().IsOpen,
                        "Inventory navigation");
                    Check(!UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffPlayerScreen>().IsVisible,
                        "Previous panel closes");
                    Button("BBNav_Actividad").onClick.Invoke();
                    break;

                case 4:
                    Check(!shell.HasManagementScreenOpen, "Activity restores restaurant");
                    Button("BBNav_Opciones").onClick.Invoke();
                    break;

                case 5:
                    Check(GameObject.Find("OptionsPanel") != null, "Options menu");
                    Check(shell.HasManagementScreenOpen, "Options blocks construction input");
                    Button("BBNav_Opciones").onClick.Invoke();
                    Capture();
                    Finish(true,
                        "approved v3 visual / exact hotspots / hover / navigation / options / screenshot");
                    break;
            }
        }
        catch (Exception error)
        {
            Finish(false, error.ToString());
        }
    }    private static void Capture()
    {
        Canvas canvas = shell.GetComponentInParent<Canvas>();
        Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        RenderMode mode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;

        RenderTexture target = new RenderTexture(1920, 1080, 24);
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;

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
        File.WriteAllBytes("Logs/TopNavigation1920.png", image.EncodeToPNG());        RenderTexture.active = previous;
        camera.targetTexture = previousTarget;
        canvas.renderMode = mode;
        canvas.worldCamera = oldCamera;

        UnityEngine.Object.Destroy(target);
        UnityEngine.Object.Destroy(image);
    }

    private static void Finish(bool pass, string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;

        string result = (pass ? "PASS " : "FAIL ") + message;
        File.WriteAllText("Logs/TopNavigationTest.txt", result);
        Debug.Log("BB_TOP_NAVIGATION_" + result);

        SessionState.SetBool(Key + ".Pass", pass);
        EditorApplication.isPlaying = false;
    }
}