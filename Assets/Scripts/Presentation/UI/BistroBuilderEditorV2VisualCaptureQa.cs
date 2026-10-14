using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Opt-in standalone graphics smoke harness. No effect in ordinary gameplay.
/// Invoke the review build with -bb-editor-v2-visual-qa to enter edit mode
/// and capture the actual rendered screen at two PC resolutions.
/// </summary>
public sealed class BistroBuilderEditorV2VisualCaptureQa : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartIfRequested()
    {
        var args = Environment.GetCommandLineArgs();
        bool enabled = Array.Exists(args, a =>
            string.Equals(a, "-bb-editor-v2-visual-qa",
                StringComparison.OrdinalIgnoreCase));
        if (!enabled) return;
        // Allow deterministic screenshot capture when the standalone game
        // loses foreground focus to the desktop automation console.
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        var go = new GameObject("__BB_EditorV2_VisualCapture_QA");
        DontDestroyOnLoad(go);
        go.AddComponent<BistroBuilderEditorV2VisualCaptureQa>();
    }

    private IEnumerator Start()
    {
        string folder = Path.Combine(Application.dataPath, "..",
            "EditorV2_Visual_Capture");
        Directory.CreateDirectory(folder);
        var log = new StringBuilder(2048);
        log.AppendLine("Editor V2 graphics QA");
        log.AppendLine("Unity " + Application.unityVersion);
        log.AppendLine("GPU " + SystemInfo.graphicsDeviceName);
        RestaurantEditModeService mode = null;
        for (int i = 0; i < 360; i++)
        {
            mode = FindFirstObjectByType<RestaurantEditModeService>(
                FindObjectsInactive.Include);
            if (mode != null) break;
            yield return null;
        }
        if (mode == null)
        {
            log.AppendLine("FAIL: edit mode service missing");
            File.WriteAllText(Path.Combine(folder, "QA_VISUAL_REPORT.txt"),
                log.ToString());
            yield break;
        }

        for (int i = 0; i < 20; i++) yield return null;
        // Visual test only: hide first-run wizard without creating or altering
        // saves. The wizard otherwise covers the entire editor screenshot.
        var opening = FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(
            FindObjectsInactive.Include);
        if (opening != null)
        {
            opening.Hide();
            log.AppendLine("FirstRunWizardHiddenForQA=true");
        }
        if (!mode.IsEditModeActive)
        {
            bool entered = mode.TryEnterEditMode(out _, out string error);
            log.AppendLine("TryEnterEditMode=" + entered + ", error=" + error);
        }
        for (int i = 0; i < 24; i++) yield return null;

        var shell = FindFirstObjectByType<BistroBuilderUiShell>(
            FindObjectsInactive.Include);
        var premium = FindFirstObjectByType<BistroBuilderEditorV2PremiumLayout>(
            FindObjectsInactive.Include);
        var catalog = FindFirstObjectByType<RestaurantPlaceableCatalogPanel>(
            FindObjectsInactive.Include);
        log.AppendLine("Editing=" + mode.IsEditModeActive);
        log.AppendLine("Shell=" + (shell != null));
        log.AppendLine("Premium=" + (premium != null && premium.IsReady));
        log.AppendLine("Catalog=" + (catalog != null));
        if (!mode.IsEditModeActive || shell == null || premium == null ||
            !premium.IsReady || catalog == null)
        {
            log.AppendLine("FAIL: cannot capture a valid V2 editor scene");
            File.WriteAllText(Path.Combine(folder, "QA_VISUAL_REPORT.txt"),
                log.ToString());
            yield break;
        }

        // Select a real registered item solely for the visual comparison.
        // The right inspector and Halo de Huella otherwise remain correctly
        // hidden in an empty selection, which is not the approved reference.
        var registry = FindFirstObjectByType<RestaurantPlaceableRegistry>(
            FindObjectsInactive.Include);
        var selector = FindFirstObjectByType<
            BistroBuilderEditorV2SelectionCoordinator>(FindObjectsInactive.Include);
        if (registry != null && selector != null)
        {
            foreach (var item in registry.RegisteredPlaceables)
            {
                if (item == null || !item.HasInstanceId ||
                    item.ItemDefinition == null) continue;
                var furnitureController =
                    FindFirstObjectByType<RestaurantEditInteractionController>(
                        FindObjectsInactive.Include);
                bool selected = furnitureController != null &&
                    furnitureController.TrySelectPlaceable(item);
                log.AppendLine("QAInspectorSample=" + item.DisplayName +
                    ", controllerSelected=" + selected);
                if (!selected) continue;
                break;
            }
        }

        yield return Capture(folder, log, 1280, 720, "EditorV2_1280x720.png");
        yield return Capture(folder, log, 1920, 1080, "EditorV2_1920x1080.png");
        log.AppendLine("QA_CAPTURE_FINISHED");
        File.WriteAllText(Path.Combine(folder, "QA_VISUAL_REPORT.txt"),
            log.ToString());
        Debug.Log("[EditorV2 VISUAL] " + log);
    }

    private IEnumerator Capture(string folder, StringBuilder log,
        int width, int height, string file)
    {
        Screen.SetResolution(width, height, false);
        for (int i = 0; i < 24; i++) yield return null;
        yield return new WaitForEndOfFrame();
        string path = Path.Combine(folder, file);
        ScreenCapture.CaptureScreenshot(path);
        log.AppendLine("Screenshot=" + file + ", actual=" +
            Screen.width + "x" + Screen.height);
        var rects = FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var rect in rects)
        {
            if (rect.name == "CatalogContent" ||
                rect.name == "CategoryBar" ||
                rect.name == "ItemsScroll" ||
                rect.name == "BB_EditorV2_GalleryShowcase" ||
                rect.name == "BB_EditorV2_ApprovedTabs" ||
                rect.name == BistroBuilderUiShell.EditModeTopBarName ||
                rect.name == BistroBuilderUiShell.EditModeBottomBarName)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                log.AppendLine(rect.name + " active=" +
                    rect.gameObject.activeInHierarchy +
                    " rect=" + rect.rect.size +
                    " worldBL=" + corners[0] +
                    " worldTR=" + corners[2]);
            }
        }
        for (int i = 0; i < 8; i++) yield return null;
        log.AppendLine("CapturePresent=" + File.Exists(path));
    }
}
