using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Development-build-only visual regression gate for Editor V2.
/// Opt-in by "-bb-editor-v2-capture". Runs in a real Windows Player with GPU,
/// not the nographics Editor. Does not execute in a normal game session.
/// </summary>
public sealed class BistroBuilderEditorV2VisualCaptureProbe : MonoBehaviour
{
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private static bool running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (running ||
            !Environment.GetCommandLineArgs().Any(
                arg => string.Equals(arg, "-bb-editor-v2-capture",
                    StringComparison.OrdinalIgnoreCase)))
            return;
        running = true;
        var go = new GameObject("__BB_EditorV2_VisualCaptureProbe");
        DontDestroyOnLoad(go);
        go.AddComponent<BistroBuilderEditorV2VisualCaptureProbe>();
    }

    private IEnumerator Start()
    {
        string folder = Path.Combine(Application.persistentDataPath,
            "EditorV2VisualQA");
        Directory.CreateDirectory(folder);
        string report = Path.Combine(folder, "REPORT.txt");
        File.WriteAllText(report, "STATUS=RUNNING\n");
        for (int i = 0; i < 80; i++) yield return null;

        // The opening screen belongs to New Game. This visual probe bypasses
        // it ONLY when explicitly requested; normal sessions are unchanged.
        var opening = FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(
            FindObjectsInactive.Include);
        if (opening != null)
            opening.gameObject.SetActive(false);

        var mode = FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var catalog = FindFirstObjectByType<RestaurantPlaceableCatalogPanel>(
            FindObjectsInactive.Include);
        if (mode == null || catalog == null)
        {
            File.WriteAllText(report, "STATUS=FAIL\nREASON=Missing mode/catalog\n");
            yield break;
        }

        if (!mode.IsEditModeActive &&
            !mode.TryEnterEditMode(out _, out string reason))
        {
            File.WriteAllText(report, "STATUS=FAIL\nREASON=" + reason + "\n");
            yield break;
        }
        catalog.SelectSection(RestaurantEditCatalogSection.Build);
        for (int i = 0; i < 60; i++) yield return null;

        var service = FindFirstObjectByType<RestaurantPlaceableCatalogService>(
            FindObjectsInactive.Include);
        var inspector = catalog.GetComponent<RestaurantPlaceableInspectorPanel>();
        var definition = service?.AvailableItems?.FirstOrDefault(item =>
            item != null && item.HasValidPrefab &&
            item.InspectorPreview != null && item.PurchasePrice > 0);
        if (definition != null && inspector != null)
            inspector.ShowForDefinition(definition);

        int[] widths = { 1280, 1920 };
        int[] heights = { 720, 1080 };
        string lines = "STATUS=PASS\nMODE=EditV2\n";
        for (int index = 0; index < widths.Length; index++)
        {
            Screen.SetResolution(widths[index], heights[index],
                FullScreenMode.Windowed);
            for (int i = 0; i < 50; i++) yield return null;
            yield return new WaitForEndOfFrame();

            string screenshot = Path.Combine(folder,
                "EditorV2_" + widths[index] + "x" + heights[index] + ".png");
            ScreenCapture.CaptureScreenshot(screenshot, 1);
            for (int i = 0; i < 18; i++) yield return null;
            lines += widths[index] + "x" + heights[index] +
                "|Screenshot=" + File.Exists(screenshot) +
                "|EditorActive=" + mode.IsEditModeActive +
                "|Catalog=" + catalog.CurrentSection +
                "|Inspector=" + (inspector != null) + "\n";
            if (!File.Exists(screenshot))
                lines = lines.Replace("STATUS=PASS", "STATUS=FAIL");
        }
        File.WriteAllText(report, lines);
        Debug.Log("[BB EditorV2 Capture] " + lines);
    }
#endif
}