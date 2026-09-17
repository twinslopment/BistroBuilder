using System;
using System.IO;
using BistroBuilder.UI.Iconography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderOptionsPlayTest
{
    const string Key = "BB.Options.Test";
    static int stage;
    static double next;
    static string failure;
    static BistroBuilderOptionsScreen options;
    static BistroBuilderUiShell shell;
    static BistroBuilderOptionsPlayTest() { EditorApplication.playModeStateChanged += State; }
    public static void RunBatch()
    {
        BistroBuilderTypographyInstaller.Prepare();
        BistroBuilder.Editor.UI.Iconography.BBIconographyInstaller.PrepareForBatch();
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity"); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange value)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (value == PlayModeStateChange.EnteredPlayMode) { stage = 0; next = EditorApplication.timeSinceStartup + 5; failure = null; Application.runInBackground = true; EditorApplication.update += Tick; Application.logMessageReceived += Log; }
        if (value == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key, false); EditorApplication.Exit(SessionState.GetBool(Key + ".Pass", false) ? 0 : 1); }
    }
    static void Log(string text, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Assert) failure = text; }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static Button Button(string name) => GameObject.Find(name).GetComponent<Button>();
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 1;
        try
        {
            Check(failure == null, failure);
            switch (stage++)
            {
                case 0:
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>().Hide();
                    shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>(); shell.EnsureShell();
                    options = shell.GetComponent<BistroBuilderOptionsScreen>();
                    Button("RestaurantIdentity").onClick.Invoke();
                    Check(GameObject.Find("TopNavigationMenu") != null && !options.IsOpen, "Restaurant menu stays independent");
                    Check(!Button("BBNav_Opciones").GetComponent<BBIconButton>().IsSelected, "Restaurant does not select gear");
                    Button("BBNav_Opciones").onClick.Invoke();
                    Check(options.IsOpen && GameObject.Find("TopNavigationMenu") == null, "Gear opens only Options");
                    Check(shell.HasManagementScreenOpen, "Options blocks construction");
                    for (int i = 0; i < 9; i++) Check(Button("OptionsCategory" + i).GetComponent<BBIconButton>() != null, "Category icon " + i);
                    break;
                case 1:
                    Capture("OpcionesPartida.png", 1920, 1080);
                    for (int i = 0; i < 9; i++) { options.ShowPage(i); Check(GameObject.Find("SettingsContent").transform.childCount > 0, "Category content " + i); }
                    options.ShowPage(2); break;
                case 2:
                    Capture("OpcionesVideo.png", 1600, 1000);
                    options.Close(); Button("BBNav_Carta").onClick.Invoke(); break;
                case 3:
                    var view = UnityEngine.Object.FindFirstObjectByType<BistroBuilderMenuPortfolioRuntimeView>();
                    Check(view.IsOpen, "Carta opens canonical portfolio");
                    Check(GameObject.Find(BistroBuilderUiShell.ActivityPanelName) == null && GameObject.Find(BistroBuilderUiShell.ContextPanelName) == null, "Scene side panels cannot cover Carta");
                    Check(view.TryValidateVisibleContent(out var error), error);
                    var modal = GameObject.Find("MenuPortfolioModal").GetComponent<RectTransform>();
                    Check(modal.offsetMax.y <= -76 && modal.offsetMin.y >= 76, "Carta clears both HUD bars");
                    Check(modal.GetComponent<ScrollRect>() != null, "Short screens can scroll the form");
                    foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                        if (button.name == "OpenScheduleButton") Check(button.GetComponent<CanvasGroup>().alpha == 0, "Floating Horarios removed");
                    Capture("CartaCorregida.png", 1920, 1080);
                    Capture("Carta1280.png", 1280, 720);
                    options.Open(); break;
                case 4:
                    Capture("OpcionesSobreCarta.png", 1920, 1080);
                    options.Close(); Check(!options.IsOpen, "Options close");
                    Finish(true, "Independent menus, nine categories, icons, safe Carta layout at 1920 and 1280, construction blocking, hidden launcher, screen captures"); break;
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    public static void Capture(string filename, int width, int height)
    {
        var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        var target = new RenderTexture(width, height, 24); var old = camera.targetTexture; var active = RenderTexture.active;
        var saved = new System.Collections.Generic.List<(Canvas canvas, RenderMode mode, Camera camera, float distance)>();
        foreach (var root in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        { if (!root.isRootCanvas || root.renderMode == RenderMode.WorldSpace) continue; saved.Add((root, root.renderMode, root.worldCamera, root.planeDistance)); root.renderMode = RenderMode.ScreenSpaceCamera; root.worldCamera = camera; root.planeDistance = camera.nearClipPlane + 2; }
        camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        Directory.CreateDirectory("docs/Images"); File.WriteAllBytes("docs/Images/" + filename, image.EncodeToPNG());
        RenderTexture.active = active; camera.targetTexture = old;
        foreach (var item in saved) { item.canvas.renderMode = item.mode; item.canvas.worldCamera = item.camera; item.canvas.planeDistance = item.distance; }
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
    }
    static void Finish(bool pass, string message)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText("Logs/OptionsTest.txt", (pass ? "PASS " : "FAIL ") + message);
        Debug.Log("BB_OPTIONS_" + (pass ? "PASS " : "FAIL ") + message);
        SessionState.SetBool(Key + ".Pass", pass); EditorApplication.isPlaying = false;
    }
}
