using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderOpeningIvorySelfTest
{
    const string Key = "BB.Ivory.Test";
    const string Scene = "Assets/Scenes/Prototype_Restaurant.unity";
    static string stage;
    static double since;
    static int slot, baselineTables, baselinePlaceables;
    static BistroBuilderNewGameOpeningService service;
    static BistroBuilderSaveGameService saves;
    static int Index => SessionState.GetInt(Key + ".Index", 0);
    static BistroBuilderStartingPremisesProfile Expected => Index == 0 ? BistroBuilderStartingPremisesProfile.Empty : Index == 1 ? BistroBuilderStartingPremisesProfile.Essentials : BistroBuilderStartingPremisesProfile.FinishingTouches;
    static BistroBuilderOpeningIvorySelfTest()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) SetStage("ui");
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                bool ok = SessionState.GetBool(Key + ".Pass", false);
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(ok ? 0 : 1);
            }
        };
    }
    public static void Run()
    {
        Directory.CreateDirectory("Logs/OpeningIvory");
        File.WriteAllText("Logs/OpeningIvory/results.txt", "Opening ivory integration\n");
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".Index", 0); SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene(Scene);
        EditorApplication.EnterPlaymode();
    }
    static void SetStage(string next) { stage = next; since = EditorApplication.timeSinceStartup; }
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); File.AppendAllText("Logs/OpeningIvory/results.txt", "PASS " + message + "\n"); }
    static T Find<T>(string name) where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == name);
    static void Update()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || string.IsNullOrEmpty(stage) || stage == "exit") return;
        try
        {
            if (EditorApplication.timeSinceStartup - since > 150) throw new Exception("Timeout " + stage);
            if (stage == "ui")
            {
                if (EditorApplication.timeSinceStartup - since < 4 || Find<Button>("Choice0") == null) return;
                service = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningService>();
                saves = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
                Assert(service != null && service.ValidateConfiguration(out _), "Opening service configured");
                slot = 999; while (slot > 900 && saves.SlotExists(slot)) slot--;
                Assert(slot > 900, "Unused diagnostic save slot");
                typeof(BistroBuilderNewGameOpeningService).GetField("defaultSaveSlot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(service, slot);
                baselineTables = UnityEngine.Object.FindFirstObjectByType<RestaurantTableRegistry>().RegisteredTableCount;
                baselinePlaceables = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>().RegisteredPlaceables.Count;
                Assert(EventSystem.current != null && EventSystem.current.enabled, "UI input enabled");
                Assert(BistroBuilderNewGameOpeningPlayerScreen.IsOpeningMenuBlocking, "Opening blocks construction input");
                // RuntimeInitializeOnLoadMethod runs only at play entry; restore that composition after each diagnostic scene reload.
                typeof(BistroBuilderConstructionAuthoringRuntimeBootstrap).GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                var construction = UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
                Assert(construction != null && !construction.IsPlaytestPanelVisible, "Construction bar hidden on new game");
                Assert(Find<BistroBuilderOpeningActionIcon>("Arrow") != null && Find<BistroBuilderOpeningActionIcon>("Bell") != null && Find<RawImage>("Arrow") == null && Find<RawImage>("Bell") == null, "Action icons use native silhouettes without image rectangles");
                var choices = Enumerable.Range(0,3).Select(i => (RectTransform)Find<Button>("Choice" + i).transform).ToArray();
                Assert(choices.All(r => r.rect.size == choices[0].rect.size), "Three equal cards");
                Assert(choices[0].rect.width == 626 && choices[0].rect.height == 222, "Card dimensions");
                Assert(choices[2].anchoredPosition.x > choices[0].anchoredPosition.x && choices[2].anchoredPosition.x < choices[1].anchoredPosition.x, "Third option centered below");
                var input = Find<TMP_InputField>("RestaurantName");
                input.text = ""; SetStage("blank"); return;
            }
            if (stage == "blank")
            {
                Assert(!Find<Button>("CreateRestaurant").interactable, "Empty name cannot create");
                Find<Button>("Back").onClick.Invoke();
                Assert(Find<Button>("NewGame").gameObject.activeInHierarchy, "Back returns to main menu");
                Assert(!UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>().IsPlaytestPanelVisible, "Construction bar stays hidden after Back");
                Find<Button>("NewGame").onClick.Invoke();
                Assert(Find<Button>("Choice0").gameObject.activeInHierarchy, "New game reopens");
                Find<TMP_InputField>("RestaurantName").text = "Ivory QA " + Index;
                Find<Button>("Choice" + Index).onClick.Invoke();
                Assert(Enumerable.Range(0,3).Count(i => Find<Button>("Choice" + i).transform.Find("Selected").gameObject.activeSelf) == 1, "Single selection");
                if (Index == 0) { Capture(1920,1080); Capture(1280,720); Capture(3440,1440); }
                SetStage("click"); return;
            }
            if (stage == "click")
            {
                if (EditorApplication.timeSinceStartup - since < 1) return;
                Assert(Find<Button>("CreateRestaurant").interactable, "Create enabled for named restaurant");
                ExecuteEvents.Execute(Find<Button>("CreateRestaurant").gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                SetStage("created"); return;
            }
            if (stage == "created")
            {
                if (service.Phase == BistroBuilderNewGamePhase.StartMenu || saves.IsBusy) return;
                Assert(service.Phase == BistroBuilderNewGamePhase.InitialSetup, "Create enters initial design " + Expected);
                Assert(service.PremisesProfile == Expected && service.RestaurantName == "Ivory QA " + Index, "Selected profile and name applied");
                Assert(!Find<Canvas>("NewGameIvoryCanvas").gameObject.activeSelf, "Opening menu dismissed");
                Assert(!BistroBuilderNewGameOpeningPlayerScreen.IsOpeningMenuBlocking, "Construction input restored after creation");
                Assert(Find<Button>("SaveRecovery").gameObject.activeInHierarchy && Find<Button>("ValidateAndContinue").gameObject.activeInHierarchy, "Initial design retains save and validate actions");
                int tables = UnityEngine.Object.FindFirstObjectByType<RestaurantTableRegistry>().RegisteredTableCount;
                int items = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>().RegisteredPlaceables.Count;
                Assert(Index == 0 ? items == 0 : Index == 1 ? tables == Mathf.Min(2,baselineTables) : items == baselinePlaceables, "Preparation changes scene content " + items + " items, " + tables + " tables");
                Assert(saves.SlotExists(slot), "Initial save written");
                var state = service.CreateSnapshot(); state.restaurantName = "Changed";
                Assert(service.TryRestoreSnapshot(state, out _), "Mutate before roundtrip");
                Assert(service.TryContinue(out _), "Load initial save"); SetStage("loaded"); return;
            }
            if (stage == "loaded")
            {
                if (saves.IsBusy) return;
                Assert(service.RestaurantName == "Ivory QA " + Index && service.PremisesProfile == Expected, "Save/load preserves name and preparation");
                Assert(saves.TryDeleteSlot(slot, out _), "Remove only generated diagnostic save"); SetStage("next"); return;
            }
            if (stage == "next")
            {
                if (saves.IsBusy) return;
                if (Index == 2)
                {
                    SessionState.SetBool(Key + ".Pass", true); SetStage("exit");
                    File.AppendAllText("Logs/OpeningIvory/results.txt", "BB_IVORY_PASS\n");
                    EditorApplication.ExitPlaymode(); return;
                }
                SessionState.SetInt(Key + ".Index", Index + 1);
                EditorSceneManager.LoadSceneInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single)); SetStage("ui");
            }
        }
        catch (Exception e)
        {
            File.AppendAllText("Logs/OpeningIvory/results.txt", "FAIL " + e + "\n"); Debug.LogException(e);
            SetStage("exit"); EditorApplication.ExitPlaymode();
        }
    }
    static void Capture(int width, int height)
    {
        Canvas canvas = Find<Canvas>("NewGameIvoryCanvas");
        float oldScale = canvas.scaleFactor;
        var transforms = canvas.GetComponentsInChildren<Transform>(true);
        int[] layers = transforms.Select(t => t.gameObject.layer).ToArray();
        foreach (var transform in transforms) transform.gameObject.layer = 31;
        var cameraObject = new GameObject("IvoryCaptureCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(width,height,24);
        Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            canvas.scaleFactor = Mathf.Min(width / 1600f,height / 1000f);
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            Find<RectTransform>("IvoryFrame").GetWorldCorners(corners);
            Assert(corners.All(c=>{var p=camera.WorldToScreenPoint(c);return p.x>=0&&p.y>=0&&p.x<=width&&p.y<=height;}),"Panel fits " + width + "x" + height);

            camera.Render(); RenderTexture.active = target;
            image = new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            File.WriteAllBytes("Logs/OpeningIvory/menu-"+width+".png", image.EncodeToPNG());
            Assert(canvas.GetComponentsInChildren<TMP_Text>().All(t=>!t.isTextOverflowing), "No text overflow " + width + ": " + string.Join(",",canvas.GetComponentsInChildren<TMP_Text>().Where(t=>t.isTextOverflowing).Select(t=>t.name + "=" + t.text)));
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; canvas.scaleFactor = oldScale;
            for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];
            UnityEngine.Object.DestroyImmediate(cameraObject); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            if(image!=null)UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
