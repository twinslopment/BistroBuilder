using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opt-in screenshot gate for real Windows player UI.
/// Never runs in normal gameplay: invoke with -bbCartaVisualQA.
/// Opens a reversible menu draft without saving or editing the restaurant.
/// </summary>
public sealed class BistroBuilderCartaV3VisualQARunner : MonoBehaviour
{
    private string outputDirectory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool requested = Array.Exists(args, a => a == "-bbCartaVisualQA");
        if (!requested) return;
        var go = new GameObject("Carta V3 Screenshot Gate");
        DontDestroyOnLoad(go);
        go.AddComponent<BistroBuilderCartaV3VisualQARunner>().Initialize(args);
    }

    private void Initialize(string[] args)
    {
        outputDirectory = Path.Combine(Application.persistentDataPath, "CartaVisualQA");
        foreach (string arg in args)
            if (arg.StartsWith("-bbCartaQAOut=", StringComparison.Ordinal))
                outputDirectory = arg.Substring("-bbCartaQAOut=".Length);
        Directory.CreateDirectory(outputDirectory);
        StartCoroutine(CaptureBothResolutions());
    }

    private IEnumerator CaptureBothResolutions()
    {
        // The initial player scene sets up service catalogs asynchronously.
        BistroBuilderMenuEditorRuntimeView editor = null;
        for (int frame = 0; frame < 600; frame++)
        {
            editor = FindFirstObjectByType<BistroBuilderMenuEditorRuntimeView>(
                FindObjectsInactive.Include);
            if (editor != null && Time.frameCount >= 60) break;
            yield return null;
        }

        var opening = FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(
            FindObjectsInactive.Include);
        if (opening != null) opening.Hide();

        bool opened = false;
        string error = string.Empty;
        for (int attempt = 0; attempt < 50 && !opened; attempt++)
        {
            if (editor == null)
                editor = FindFirstObjectByType<BistroBuilderMenuEditorRuntimeView>(
                    FindObjectsInactive.Include);
            if (editor != null)
                opened = editor.TryOpenFromInterface(out error);
            if (!opened) yield return new WaitForSecondsRealtime(.2f);
        }
        if (!opened)
        {
            Debug.LogError("CARTA_SCREENSHOT_FAILED|OPEN|" + error);
            Application.Quit(2);
            yield break;
        }
        // Force the same data-binding refresh that the real UI performs
        // after opening; avoid capturing a partially populated input form.
        editor.TryValidateVisibleContent(out _);
        for (int i = 0; i < 65; i++) yield return null;
        editor.TryValidateVisibleContent(out _);
        Debug.Log("CARTA_SCREENSHOT_OPENED");

        foreach (var size in new[] { new Vector2Int(1280, 720),
                                     new Vector2Int(1920, 1080) })
        {
            Screen.SetResolution(size.x, size.y, false);
            // Allow the CanvasScaler and all SDF mirrors to settle.
            for (int frame = 0; frame < 40; frame++) yield return null;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            string filename = "CartaV3_Unity_" + size.x + "x" + size.y + ".png";
            string path = Path.Combine(outputDirectory, filename);
            ScreenCapture.CaptureScreenshot(path, 1);
            Transform detail = editor.transform.Find(
                "MenuEditorModal/Panel/Body/Detail/DetailCanvas");
            var price = detail?.Find("Price")?.GetComponent<InputField>();
            var category = detail?.Find("CategoryValuePlate/CategoryValue")
                ?.GetComponent<Text>();
            var sharp = category?.transform.Find("CartaV3SharpText")
                ?.GetComponent<TMPro.TextMeshProUGUI>();
            Debug.Log("CARTA_SCREENSHOT_FORM|" + size.x +
                "|price=" + (price?.text ?? "<null>") +
                "|nativePrice=" + (price?.textComponent?.text ?? "<null>") +
                "|category=" + (category?.text ?? "<null>") +
                "|categorySDF=" + (sharp?.text ?? "<null>") +
                "|sdfRect=" + (sharp == null ? "none" :
                    sharp.rectTransform.rect.ToString()) +
                "|canvasScale=" +
                (editor.GetComponentInParent<Canvas>()?.scaleFactor.ToString() ?? "none"));
            Transform panelNode = editor.transform.Find("MenuEditorModal/Panel");
            Transform modalNode = editor.transform.Find("MenuEditorModal");
            foreach (Transform node in new[] { modalNode, panelNode })
            {
                RectTransform rect = node as RectTransform;
                if (rect == null) continue;
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Debug.Log("CARTA_SCREENSHOT_RECT|" + node.name +
                    "|bounds=" + corners[0] + " to " + corners[2] +
                    "|anchors=" + rect.anchorMin + " to " + rect.anchorMax +
                    "|offsets=" + rect.offsetMin + " to " + rect.offsetMax);
            }
            Transform level = editor.transform;
            for (int depth = 0; depth < 4 && level != null; depth++)
            {
                RectTransform rect = level as RectTransform;
                Vector3[] corners = new Vector3[4];
                if (rect != null) rect.GetWorldCorners(corners);
                Debug.Log("CARTA_SCREENSHOT_HIERARCHY|" + depth + "|" +
                    level.name + "|rect=" +
                    (rect != null ? rect.rect.ToString() : "none") +
                    "|corner0=" + (rect != null ? corners[0].ToString() : "none") +
                    "|corner2=" + (rect != null ? corners[2].ToString() : "none"));
                level = level.parent;
            }
            Debug.Log("CARTA_SCREENSHOT_SAVED|" + path +
                "|CURRENT=" + Screen.width + "x" + Screen.height);
            yield return new WaitForSecondsRealtime(2f);
        }
        editor.RequestCloseFromInterface();
        Debug.Log("CARTA_SCREENSHOT_DONE");
        Application.Quit(0);
    }
}
