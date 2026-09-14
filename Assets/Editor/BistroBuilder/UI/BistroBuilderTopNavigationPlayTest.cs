using System;
using System.IO;
using BistroBuilder.UI.Iconography;
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
    static BistroBuilderTopNavigationPlayTest() { EditorApplication.playModeStateChanged += State; }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0; failure = null; next = EditorApplication.timeSinceStartup + 3;
            Application.runInBackground = true;
            EditorApplication.update += Tick; Application.logMessageReceived += Log;
        }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + ".Pass", false) ? 0 : 1);
        }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Assert) failure = message; }
    private static void Check(bool condition, string error) { if (!condition) throw new Exception(error); }
    private static Button Button(string name) => GameObject.Find(name).GetComponent<Button>();
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 0.6;
        try
        {
            Check(failure == null, failure);
            switch (stage++)
            {
                case 0:
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>().Hide();
                    shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();
                    Check(shell != null, "No shell");
                    shell.EnsureShell();
                    Check(GameObject.Find("RestaurantName") != null && GameObject.Find("ServiceLabel") != null, "Identity survives repeated HUD reconciliation");
                    Check(BBIconCatalog.LoadDefault().Entries.Count >= 80, "Iconography catalog incomplete");
                    foreach (string name in new[] { "Actividad", "Personal", "Carta", "Inventario", "Proveedores", "Reservas", "Economia", "Marketing", "Reputacion", "Opciones" })
                    {
                        var button = Button("BBNav_" + name);
                        Check(button.transform.Find("NavigationIcon").GetComponent<Image>().sprite != null, "Missing icon " + name);
                    }
                    Button("BBNav_Personal").GetComponent<BBIconButton>().OnPointerEnter(new PointerEventData(EventSystem.current)); break;
                case 1:
                    var staffFx = Button("BBNav_Personal").GetComponent<BBIconButton>();
                    Check(staffFx.State == BBIconState.Hover, "Hover state");
                    Check(staffFx.transform.Find("NavigationIcon").localScale.x > 1.01f, "Hover animation");
                    Button("BBNav_Personal").onClick.Invoke(); break;
                case 2:
                    Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffPlayerScreen>().IsVisible, "Staff navigation");
                    Check(Button("BBNav_Personal").GetComponent<BBIconButton>().IsSelected, "Active tab highlight");
                    Button("BBNav_Inventario").onClick.Invoke(); break;
                case 3:
                    Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseRuntimeView>().IsOpen, "Inventory navigation");
                    Check(!UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffPlayerScreen>().IsVisible, "Previous panel closes");
                    Button("BBNav_Actividad").onClick.Invoke(); break;
                case 4:
                    Check(!shell.HasManagementScreenOpen, "Activity restores restaurant");
                    Button("BBNav_Opciones").onClick.Invoke(); break;
                case 5:
                    Check(GameObject.Find("TopNavigationMenu") != null, "Options menu");
                    Check(shell.HasManagementScreenOpen, "Menu blocks construction input");
                    Button("BBNav_Opciones").onClick.Invoke();
                    Button("BBNav_Personal").GetComponent<BBIconButton>().OnPointerExit(new PointerEventData(EventSystem.current)); break;
                case 6:
                    Capture(); Finish(true, "Catalog / icons / hover motion / selected state / navigation / options / screenshot"); break;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private static void Capture()
    {
        var canvas = shell.GetComponentInParent<Canvas>();
        var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        var target = new RenderTexture(1920, 1080, 24);
        var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/TopNavigation1920.png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = previousTarget;
        canvas.renderMode = mode; canvas.worldCamera = oldCamera;
        UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
    }
    private static void Finish(bool pass, string message)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        string result = (pass ? "PASS " : "FAIL ") + message;
        File.WriteAllText("Logs/TopNavigationTest.txt", result); Debug.Log("BB_TOP_NAVIGATION_" + result);
        SessionState.SetBool(Key + ".Pass", pass); EditorApplication.isPlaying = false;
    }
}
