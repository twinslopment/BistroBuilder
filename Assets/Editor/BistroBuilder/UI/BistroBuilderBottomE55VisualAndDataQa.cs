using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// E5.5 QA: verifies live data binding and captures the real Unity uGUI
/// at the requested output resolutions. No demo UI or mocked presentation.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderBottomE55VisualAndDataQa
{
    private const string Key = "BB.E55.VisualDataQA";
    private static double due;
    private static string fault;

    static BistroBuilderBottomE55VisualAndDataQa()
    {
        EditorApplication.playModeStateChanged += OnMode;
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/E55VisualDataQa.txt", "RUNNING");
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnMode(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            due = EditorApplication.timeSinceStartup + 3.5;
            fault = null;
            Application.runInBackground = true;
            EditorApplication.update += Run;
            Application.logMessageReceived += Watch;
        }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + ".Pass", false) ? 0 : 1);
        }
    }

    private static void Watch(string message, string stack, LogType type)
    {
        if (type != LogType.Assert && type != LogType.Exception) return;
        if (message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal) &&
            stack.IndexOf("UnityEditor.Search.SearchDatabase", StringComparison.Ordinal) >= 0)
            return;
        if (fault == null) fault = message + "\n" + stack;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static TMP_Text Label(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        Check(child != null, "Missing label: " + name);
        TMP_Text label = child.GetComponent<TMP_Text>();
        Check(label != null, "Missing TMP component: " + name);
        return label;
    }

    private static void Refresh(BistroBuilderBottomE55Hud hud)
    {
        MethodInfo method = typeof(BistroBuilderBottomE55Hud).GetMethod(
            "RefreshData", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(method != null, "RefreshData missing");
        method.Invoke(hud, null);
    }

    private static void Run()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
        try
        {
            Check(fault == null, "Unrelated runtime exception: " + fault);
            var opening = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderNewGameOpeningPlayerScreen>(FindObjectsInactive.Include);
            if (opening != null) opening.Hide();

            var shell = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderUiShell>(FindObjectsInactive.Include);
            Check(shell != null, "Missing UI shell");
            shell.EnsureShell();
            var hud = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderBottomE55Hud>(FindObjectsInactive.Include);
            Check(hud != null, "Missing E5.5 HUD");
            Check(hud.ValidateConfiguration(out string err), "Invalid HUD: " + err);
            RectTransform visual = hud.transform.Find(
                BistroBuilderBottomE55Hud.VisualRootName) as RectTransform;
            Check(visual != null, "E5.5 visual root is missing");
            Transform row = visual.Find("ContenidoE55");
            Check(row != null && row.childCount == 4, "Expected precisely four modules");
            Canvas canvas = hud.GetComponentInParent<Canvas>();
            Check(canvas != null, "Canvas missing");

            Transform cash = row.Find("CajaE55");
            Transform satisfaction = row.Find("SatisfaccionE55");
            Transform weather = row.Find("ClimaE55");
            var cashText = Label(cash, "Valor");
            var satisfactionText = Label(satisfaction, "Valor");
            var weatherText = Label(weather, "Estado");
            var temperatureText = Label(weather, "Temperatura");
            var financial = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderFinancialResultsService>(FindObjectsInactive.Include);
            var experience = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderCustomerExperienceTrackingService>(FindObjectsInactive.Include);
            var climate = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderClimateService>(FindObjectsInactive.Include);
            Check(financial != null && experience != null && climate != null,
                "Missing one or more canonical data sources");

            Refresh(hud);
            string expectedCash = financial.TryGetCurrentServiceResult(
                out BistroBuilderServiceFinancialResult result, out _) && result != null
                ? BistroBuilderFinanceUiFormat.Money(result.revenueCents) : "—";
            Check(cashText.text == expectedCash,
                "Cash HUD does not equal canonical service revenue: HUD='" +
                cashText.text + "' service='" + expectedCash + "'");

            var original = experience.CreateRuntimeSnapshot();
            bool priorOverride = climate.HasDebugOverride;
            Check(!priorOverride, "Scene must start without a climate debug override");

            string satisfactionBefore = satisfactionText.text;
            int hi = -1, lo = -1;
            try
            {
                var visit = new BistroBuilderReputationVisitRuntimeRecord
                {
                    groupId = 876543,
                    partySize = 3,
                    segmentId = "general",
                    discoverySource = (BistroBuilderRestaurantDiscoverySource)0,
                    expectedFoodSeconds = 25f,
                    foodQualityPotentialBasisPoints = 9300,
                    ambienceScoreBasisPoints = 7800,
                    referenceAmountCents = 2200,
                    paidAmountCents = 1900,
                    tableWaitSeconds = 1f,
                    waiterWaitSeconds = 1f,
                    foodWaitSeconds = 15f,
                    billWaitSeconds = 1f
                };
                var high = new BistroBuilderReputationRuntimeSnapshot();
                high.visits.Add(visit);
                Check(experience.TryRestoreRuntimeSnapshot(high, out string reason),
                    "Synthetic high-satisfaction visit was rejected: " + reason);
                climate.SetDebugOverride(BistroBuilderWeatherDebugPreset.Rain, true, 8f);
                Refresh(hud);
                Check(weatherText.text == "Lluvia" && temperatureText.text == "8 °C",
                    "Weather rain override not reflected in HUD: " +
                    weatherText.text + "/" + temperatureText.text);
                Check(satisfactionText.text.EndsWith("%", StringComparison.Ordinal),
                    "Satisfaction must display percent during active visit: " +
                    satisfactionText.text);
                hi = ParsePercent(satisfactionText.text);
                Capture(canvas, visual, row, 1920, 1080);
                Capture(canvas, visual, row, 1280, 720);

                // El menú de gestión no debe tapar ni sustituir el dock.
                GameObject personalButton = GameObject.Find("BBNav_Personal");
                Check(personalButton != null, "Navigation Personal unavailable");
                personalButton.GetComponent<Button>().onClick.Invoke();
                Check(shell.HasManagementScreenOpen, "Personal navigation did not open");
                Check(visual.gameObject.activeInHierarchy,
                    "E5.5 became hidden after opening Personal");
                Capture(canvas, visual, row, 1920, 1080, "_Personal");
                GameObject activityButton = GameObject.Find("BBNav_Actividad");
                Check(activityButton != null, "Navigation Actividad unavailable");
                activityButton.GetComponent<Button>().onClick.Invoke();
                Check(!shell.HasManagementScreenOpen, "Personal did not close");

                visit.foodQualityPotentialBasisPoints = 5100;
                visit.ambienceScoreBasisPoints = 3200;
                visit.referenceAmountCents = 900;
                visit.paidAmountCents = 2100;
                visit.tableWaitSeconds = 180f;
                visit.waiterWaitSeconds = 130f;
                visit.foodWaitSeconds = 230f;
                visit.billWaitSeconds = 120f;
                var low = new BistroBuilderReputationRuntimeSnapshot();
                low.visits.Add(visit);
                Check(experience.TryRestoreRuntimeSnapshot(low, out reason),
                    "Synthetic low-satisfaction visit was rejected: " + reason);
                climate.SetDebugOverride(BistroBuilderWeatherDebugPreset.Snow, true, -4f);
                Refresh(hud);
                lo = ParsePercent(satisfactionText.text);
                Check(lo < hi, "Satisfaction did not fall when service quality worsened: high=" + hi + " low=" + lo);
                Check(weatherText.text == "Nieve" && temperatureText.text == "-4 °C",
                    "Weather snow override not reflected: " +
                    weatherText.text + "/" + temperatureText.text);
            }
            finally
            {
                experience.TryRestoreRuntimeSnapshot(original, out _);
                climate.ClearDebugOverride();
                Refresh(hud);
            }
            Check(satisfactionText.text == satisfactionBefore,
                "Original satisfaction was not restored after testing");

            // En edición se debe ocultar E5.5, conservando el cromado propio.
            string editResult = "not available";
            var edit = UnityEngine.Object.FindFirstObjectByType<
                RestaurantEditModeService>(FindObjectsInactive.Include);
            if (edit != null && !edit.IsEditModeActive &&
                edit.TryEnterEditMode(out _, out string editRejection))
            {
                var late = typeof(BistroBuilderBottomE55Hud).GetMethod(
                    "LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(late != null, "LateUpdate E5.5 not found");
                try
                {
                    late.Invoke(hud, null);
                    Check(!visual.gameObject.activeSelf,
                        "E5.5 remains visible in edit mode");
                    editResult = "hidden";
                }
                finally
                {
                    Check(edit.TryExitEditMode(true, out _),
                        "Failed to restore game mode");
                    late.Invoke(hud, null);
                    Check(visual.gameObject.activeSelf,
                        "E5.5 does not return to normal game mode");
                }
            }
            string report = "PASS E5.5 visual+data QA: canonical Caja='" + expectedCash +
                "', Satisfaction high=" + hi + "% low=" + lo +
                "%, climate rain/snow updated; 1920x1080/1280x720 and Personal screenshots generated, edit-mode=" + editResult + ".";
            Finish(true, report);
        }
        catch (Exception e)
        {
            Finish(false, e.ToString());
        }
    }

    private static int ParsePercent(string raw)
    {
        Check(raw.Contains("%"), "Not a percentage: " + raw);
        return int.Parse(raw.Replace("%", "").Trim());
    }

    private static void Capture(Canvas ownCanvas, RectTransform visual,
        Transform row, int width, int height, string suffix = "")
    {
        var camera = Camera.main;
        if (camera == null)
            camera = UnityEngine.Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        Check(camera != null, "No camera for screenshot");
        Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null,
            "Screenshot requires a real GraphicsDevice; do not pass -nographics");

        RenderTexture target = new RenderTexture(width, height, 24);
        target.Create();
        RenderTexture priorActive = RenderTexture.active;
        RenderTexture priorTarget = camera.targetTexture;
        Canvas[] roots = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var saved = new List<(Canvas c, RenderMode m, Camera cam, float dist)>();
        try
        {
            camera.targetTexture = target;
            foreach (Canvas c in roots)
            {
                if (!c.isRootCanvas || c.renderMode == RenderMode.WorldSpace ||
                    !c.gameObject.activeInHierarchy) continue;
                saved.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = camera;
                c.planeDistance = Mathf.Max(camera.nearClipPlane + 0.2f, 0.5f);
            }
            Canvas.ForceUpdateCanvases();
            // Capturar la geometría que Unity ya ha construido en Play Mode;
            // ForceMeshUpdate(ignoreActiveState:true) invalida glifos en batch.
            var corners = new Vector3[4];
            visual.GetWorldCorners(corners);
            float visualWidth = visual.rect.width;
            float visualHeight = visual.rect.height;
            Check(visualWidth > 0f && visualHeight > 0f, "Visual bar has zero dimensions");
            var blocks = new List<string>();
            for (int i = 0; i < row.childCount; i++)
            {
                RectTransform part = row.GetChild(i) as RectTransform;
                Check(part != null && part.rect.width >= 140f, "Module too narrow at " + width + ": " + row.GetChild(i).name);
                blocks.Add(part.name + "=" + part.rect.width.ToString("0") + "x" +
                    part.rect.height.ToString("0"));
            }
            camera.Render();
            RenderTexture.active = target;
            Texture2D snapshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                snapshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                snapshot.Apply();
                File.WriteAllBytes("Logs/E55_RealUnity_" + width + "x" + height + suffix + ".png",
                    snapshot.EncodeToPNG());
            }
            finally { UnityEngine.Object.Destroy(snapshot); }
            File.AppendAllText("Logs/E55VisualGeometry.txt",
                width + "x" + height + " CanvasPixelRect=" + ownCanvas.pixelRect +
                " logicalBar=" + visualWidth.ToString("0.0") + "x" +
                visualHeight.ToString("0.0") + " modules=" + string.Join(", ", blocks) + "\n");
        }
        finally
        {
            RenderTexture.active = priorActive;
            camera.targetTexture = priorTarget;
            foreach (var item in saved)
            {
                if (item.c == null) continue;
                item.c.renderMode = item.m;
                item.c.worldCamera = item.cam;
                item.c.planeDistance = item.dist;
            }
            target.Release();
            UnityEngine.Object.Destroy(target);
        }
    }

    private static void Finish(bool pass, string message)
    {
        EditorApplication.update -= Run;
        Application.logMessageReceived -= Watch;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/E55VisualDataQa.txt", (pass ? "PASS " : "FAIL ") + message);
        Debug.Log("BB_E55_VISUAL_DATA_" + (pass ? "PASS " : "FAIL ") + message);
        SessionState.SetBool(Key + ".Pass", pass);
        EditorApplication.isPlaying = false;
    }
}
