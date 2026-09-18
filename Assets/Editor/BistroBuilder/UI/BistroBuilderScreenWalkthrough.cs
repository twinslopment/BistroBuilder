using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BistroBuilder.UI.Iconography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderScreenWalkthrough
{
    const string Key = "BB.ScreenWalkthrough";
    static readonly List<Action> steps = new();
    static readonly List<string> report = new();
    static int stage;
    static double next;
    static string failure;
    static BistroBuilderUiShell shell;
    static BistroBuilderOptionsScreen options;
    static BistroBuilderConstructionAuthoringRuntimeTool construction;
    static BistroBuilderScreenWalkthrough() => EditorApplication.playModeStateChanged += State;
    public static void RunBatch()
    {
        BistroBuilderTypographyInstaller.Prepare();
        BistroBuilder.Editor.UI.Iconography.BBIconographyInstaller.PrepareForBatch();
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity"); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { stage = 0; failure = null; report.Clear(); steps.Clear(); next = EditorApplication.timeSinceStartup + 5; Application.runInBackground = true; BuildSteps(); EditorApplication.update += Tick; Application.logMessageReceived += Log; }
        if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key, false); EditorApplication.Exit(SessionState.GetBool(Key + ".Pass", false) ? 0 : 1); }
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Log(string message, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Assert) failure = message; }
    static Button Button(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);
    static void Click(string name) { var button = Button(name); Check(button != null && button.interactable, "Missing/unavailable button: " + name); button.onClick.Invoke(); }
    static void Capture(string name)
    {
        BistroBuilderOptionsPlayTest.Capture("Recorrido/" + name + ".png", 1920, 1080);
        report.Add("REVISADA " + name);
        var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            var group = button.GetComponent<CanvasGroup>();
            if (group != null && group.alpha == 0) continue;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null && label.isTextOverflowing && label.overflowMode != TextOverflowModes.Ellipsis)
                report.Add("TEXTO A REVISAR " + name + " / " + button.name + " / " + label.text.Replace('\n', ' '));
        }
    }
    static void Screen(string name, Action open, Action check, Action close)
    {
        steps.Add(open);
        steps.Add(() => { check?.Invoke(); Capture(name); close?.Invoke(); });
    }
    static void BuildSteps()
    {
        Directory.CreateDirectory("docs/Images/Recorrido");
        steps.Add(() => {
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>().Hide();
            shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>(); shell.EnsureShell(); options = shell.GetComponent<BistroBuilderOptionsScreen>();
            construction = UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        });
        foreach (string name in new[] { "Actividad", "Personal", "Carta", "Inventario", "Proveedores", "Reservas", "Economia", "Marketing", "Reputacion" })
        {
            string screen = name;
            Screen(screen, () => Click("BBNav_" + screen), () => { if (screen != "Actividad") Check(shell.HasManagementScreenOpen, screen + " did not open"); }, null);
            steps.Add(() => {
                var tabs = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Where(b => b.interactable && b.name.EndsWith("Tab", StringComparison.Ordinal) && !b.transform.IsChildOf(shell.transform)).ToArray();
                int position = stage;
                foreach (var tab in tabs)
                { var button = tab; steps.Insert(position++, () => button.onClick.Invoke()); steps.Insert(position++, () => Capture(screen + "_" + button.name)); }
            });
            steps.Add(() => Click("BBNav_Actividad"));
        }
        foreach (string name in new[] { "Progreso", "Comandas", "Cocina", "Camareros", "Sala", "Cierredeldia", "Horarios" })
        {
            string screen = name;
            Screen(screen, () => { Click("RestaurantIdentity"); Click("Menu_" + screen); }, () => Check(shell.HasManagementScreenOpen, screen + " not registered"), () => {
                if (BistroBuilderOperationalPanel.AnyOpen)
                {
                    var close = UnityEngine.Object.FindObjectsByType<BistroBuilderOperationalPanel>(FindObjectsSortMode.None).First().GetComponentsInChildren<Button>().First(b => b.name == "CloseOperations");
                    Check(((RectTransform)close.transform).rect.height >= 30, "Close button clipped"); close.onClick.Invoke(); Check(!BistroBuilderOperationalPanel.AnyOpen, "Close button failed");
                }
                Click("BBNav_Actividad");
            });
            steps.Add(() => Check(!shell.HasManagementScreenOpen, "Management screen did not close: " + screen));
        }
        for (int i = 0; i < 9; i++)
        { int page = i; Screen("Opciones_" + i, () => { options.Open(); options.ShowPage(page); }, () => Check(options.IsOpen, "Options page"), optionsClose); }
        steps.Add(() => Check(UnityEngine.Object.FindFirstObjectByType<RestaurantEditInteractionController>().TryEnterEditMode(), "Cannot enter edit mode"));
        foreach (var mode in new[] { BistroBuilderConstructionRuntimeMode.Select, BistroBuilderConstructionRuntimeMode.Wall, BistroBuilderConstructionRuntimeMode.WallModule, BistroBuilderConstructionRuntimeMode.Room, BistroBuilderConstructionRuntimeMode.Door, BistroBuilderConstructionRuntimeMode.Window, BistroBuilderConstructionRuntimeMode.Furniture })
        { var chosen = mode; Screen("Construccion_" + mode, () => construction.SetMode(chosen), null, null); }
        steps.Add(() => {
            var categories = UnityEngine.Object.FindObjectsByType<RestaurantPlaceableCatalogCategoryView>(FindObjectsSortMode.None);
            Check(categories.Length >= 4, "Catalog categories missing");
            int position = stage;
            foreach (var category in categories)
            {
                var item = category; Check(item.GetComponent<BBIconButton>() != null, "Category without icon " + item.name);
                steps.Insert(position++, () => item.GetComponent<Button>().onClick.Invoke());
                steps.Insert(position++, () => {
                    foreach (var card in UnityEngine.Object.FindObjectsByType<RestaurantPlaceableCatalogItemView>(FindObjectsSortMode.None))
                    {
                        var name = card.transform.Find("Name").GetComponent<Text>();
                        Check(name.preferredHeight <= name.rectTransform.rect.height + 2, "Clipped product name: " + name.text);
                    }
                    Capture("Catalogo_" + item.name);
                });
            }
        });
        steps.Add(() => { Click("RestaurantIdentity"); Click("Menu_Sala"); });
        steps.Add(() => {
            Capture("BarraEnEdicion"); Check(BistroBuilderOperationalPanel.AnyOpen, "Bar screen missing");
            Check(UnityEngine.Object.FindObjectsByType<RestaurantPlaceableCatalogItemView>(FindObjectsSortMode.None).Length == 0, "Catalog overlays operational screen");
            Click("CloseOperations"); Check(!BistroBuilderOperationalPanel.AnyOpen, "Bar did not close during edit mode");
        });
        steps.Add(() => { Capture("CatalogoRestaurado"); BistroBuilderOptionsPlayTest.Capture("Recorrido/Catalogo1280.png", 1280, 720); Finish(true); });
    }
    static void optionsClose() => options.Close();
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .9;
        try { Check(failure == null, failure); if (stage < steps.Count) steps[stage++](); }
        catch (Exception e) { report.Add(e.ToString()); Finish(false); }
    }
    static void Finish(bool pass)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllLines("Logs/ScreenWalkthrough.txt", new[] { pass ? "PASS" : "FAIL" }.Concat(report));
        Debug.Log("BB_SCREEN_WALKTHROUGH_" + (pass ? "PASS" : "FAIL") + " at step " + stage);
        SessionState.SetBool(Key + ".Pass", pass); EditorApplication.isPlaying = false;
    }
}
