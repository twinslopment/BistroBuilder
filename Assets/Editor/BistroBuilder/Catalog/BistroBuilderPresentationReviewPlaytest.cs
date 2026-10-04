using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

/// <summary>Reversible acceptance of the demonstrated catalogue/drag/close regressions.
/// Uses production scene, catalogue, creation and EventSystem; saves no gameplay.</summary>
[InitializeOnLoad]
public static class BistroBuilderPresentationReviewPlaytest
{
    const string Key = "BB.PresentationReview.Playtest";
    static int stage, checks, frame;
    static double deadline, next;
    static string failure;
    static Mouse mouse;
    static RestaurantEditInteractionController edit;
    static RestaurantPlaceableCatalogPanel catalog;
    static RestaurantPlaceableInspectorPanel inspector;
    static RestaurantPlaceableCreationService creation;
    static RestaurantPlaceableCatalogDefinition definition;
    static BistroBuilderMenuPortfolioRuntimeView portfolio;
    static BistroBuilderMenuEditorRuntimeView menu;
    static Button close;
    static GameObject compatibilityFixture;
    static readonly List<string> evidence = new List<string>();
    static readonly string[] colours = {"white", "yellow", "red", "olive"};
    static BistroBuilderPresentationReviewPlaytest() { EditorApplication.playModeStateChanged += State; }
    public static void RunBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Playtest requires a clean Editor scene.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = checks = frame = 0; failure = null; evidence.Clear();
            deadline = EditorApplication.timeSinceStartup + 180;
            next = EditorApplication.timeSinceStartup + 4;
            EditorApplication.update += Tick; Application.logMessageReceived += Log;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            SessionState.SetBool(Key, false);
            bool pass = SessionState.GetBool(Key + ".Pass", false) && string.IsNullOrEmpty(failure);
            string report = DateTime.UtcNow.ToString("O") + "\n" + (pass ? "PASS" : "FAIL") + " checks=" + checks + "\n" + string.Join("\n", evidence) + "\n" + failure;
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/presentation-review-runtime.txt", report);
            Debug.Log("[PRESENTATION REVIEW] " + report);
            EditorApplication.Exit(pass ? 0 : 1);
        }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = (failure ?? "") + "\n" + message + "\n" + stack; }
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); checks++; evidence.Add("PASS " + message); }
    static T Field<T>(object host, string name)
    { return (T)host.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host); }
    static T Find<T>() where T : UnityEngine.Component => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next || Time.frameCount == frame) return;
        next = EditorApplication.timeSinceStartup + .30; frame = Time.frameCount;
        try
        {
            if (!string.IsNullOrEmpty(failure)) throw new Exception(failure);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Runtime acceptance timeout.");
            Canvas.ForceUpdateCanvases();
            switch(stage)
            {
                case 0:
                    Find<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                    edit = Find<RestaurantEditInteractionController>(); catalog = Find<RestaurantPlaceableCatalogPanel>();
                    inspector = Find<RestaurantPlaceableInspectorPanel>(); creation = Find<RestaurantPlaceableCreationService>();
                    definition = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableCatalogDefinition>("Assets/Data/Restaurant/EditMode/Catalog/RestaurantPlaceableCatalog_Main.asset");
                    Check(edit != null && catalog != null && inspector != null && creation != null && definition != null, "Canonical scene authorities and MainCatalog present");
                    Check(!UnityEngine.Object.FindObjectsByType<BistroBuilder367HInstalledFixture>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(x => x.IsRetired), "No retired bar fixture in the canonical scene");
                    Check(Find<BistroBuilderBarServiceSystem>().enabled && Find<BistroBuilderBarServiceRegistry>().FreeCapacity == 0 && Find<BistroBuilderBarServiceRegistry>().ValidateConfiguration(out _), "Empty bar layout keeps native service enabled without inventing capacity");
                    Check(edit.TryEnterEditMode(), "Native entry to edit mode");
                    catalog.SelectSection(RestaurantEditCatalogSection.Build);
                    catalog.SelectCategoryFromInterface(RestaurantPlaceableItemCategory.Seating);
                    break;
                case 1:
                    AuditColours();
                    catalog.SelectCategoryFromInterface(RestaurantPlaceableItemCategory.Furniture);
                    break;
                case 2:
                    SelectCard("table_basic"); break;
                case 3:
                    Check(creation.HasActiveCreation && edit.HasActivePlacement, "Table card starts canonical provisional creation");
                    var ghost = creation.ActiveProvisionalPlaceable;
                    var visual = ghost.transform.Find("BB_Presentation_TableVisual");
                    Check(visual != null && visual.GetComponentsInChildren<MeshRenderer>().Length == 6 && visual.Find("Top") != null,
                        "Table drag ghost uses tabletop, apron and four legs");
                    Check(visual.GetComponentsInChildren<Collider>(true).Length == 0, "Presentation adds no physical collider");
                    AuditInspector("table_basic");
                    edit.TryPreviewActivePlacementAtWorldPose(new Vector3(1000, 0, 1000), Quaternion.identity, out var rejected, out _);
                    Check(!rejected.IsValid, "Outside-area pose remains rejected by native placement");
                    break;
                case 4:
                    AuditInspector("table_basic");
                    var message = Field<TMP_Text>(inspector, "statusMessage");
                    Check(!string.IsNullOrWhiteSpace(message.text) && message.rectTransform.rect.height + 1 >= message.GetPreferredValues(message.text, message.rectTransform.rect.width, 0).y,
                        "Invalid-placement reason fits the visible status text");
                    Capture("presentation-review-table-drag.png");
                    Check(edit.CancelActivePlacement(), "Native cancellation of provisional table"); break;
                case 5:
                    Check(!creation.HasActiveCreation && !edit.HasActivePlacement, "Cancellation leaves no active creation");
                    definition.TryGetItem("table_basic_4", out var otherTable); inspector.ShowForDefinition(otherTable);
                    break;
                case 6:
                    Check(Field<RectTransform>(inspector, "previewSection").gameObject.activeSelf,
                        "Expanded inspector restores preview after cancelling drag");
                    SelectCard("table_basic_4"); break;
                case 7:
                    AuditInspector("table_basic_4");
                    Check(edit.CancelActivePlacement(), "Second table cancels through the same lifecycle");
                    Check(edit.TryExitEditMode(true), "Native return to normal mode");
                    mouse = InputSystem.AddDevice<Mouse>();
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    portfolio = Find<BistroBuilderMenuPortfolioRuntimeView>(); menu = Find<BistroBuilderMenuEditorRuntimeView>();
                    Check(portfolio.TryOpen(out var openError), "Carta opens through canonical service: " + openError);
                    foreach(var scroll in Field<RectTransform>(portfolio, "modalRoot").GetComponentsInChildren<ScrollRect>()) BistroBuilderUiScrollRegion.Configure(scroll);
                    break;
                case 8: SetClose(portfolio); Pointer(false); break;
                case 9: Pointer(true); break;
                case 10: Pointer(false); break;
                case 11:
                    TracePointer();
                    Check(!portfolio.IsOpen, "Real Mouse press/release closes Carta without opening another screen");
                    Check(portfolio.TryOpen(out _), "Carta reopens after a real mouse close");
                    foreach (var scroll in Field<RectTransform>(portfolio, "modalRoot").GetComponentsInChildren<ScrollRect>())
                    { BistroBuilderUiScrollRegion.Configure(scroll); scroll.verticalNormalizedPosition = 0; }
                    break;
                case 12: SetClose(portfolio); Pointer(false); break;
                case 13: Pointer(true); break;
                case 14: Pointer(false); break;
                case 15:
                    Check(!portfolio.IsOpen, "Real mouse close still works after reopening and scrolling Carta");
                    Check(menu.TryOpenFromInterface(out _), "Carta dish editor opens through its native service");
                    foreach(var scroll in Field<RectTransform>(menu, "modalRoot").GetComponentsInChildren<ScrollRect>()) BistroBuilderUiScrollRegion.Configure(scroll); break;
                case 16: SetClose(menu); Pointer(false); break;
                case 17: Pointer(true); break;
                case 18: Pointer(false); break;
                case 19:
                    Check(!menu.IsOpen, "Real mouse closes the dish editor without another navigation option");
                    compatibilityFixture = new GameObject("RetiredBarCompatibilityProbe"); compatibilityFixture.SetActive(false);
                    var marker = compatibilityFixture.AddComponent<BistroBuilder367HInstalledFixture>();
                    Check(marker.EditorAssignFixtureId(BistroBuilder367HInstalledFixture.RetiredBarFixtureId), "Legacy compatibility fixture identity assigned");
                    compatibilityFixture.SetActive(true);
                    Check(!compatibilityFixture.activeSelf, "Old loaded fixture disables before rendering"); break;
                case 20:
                    Check(compatibilityFixture == null, "Old loaded fixture is destroyed, not hidden indefinitely");
                    Finish(true); return;
            }
            stage++;
        }
        catch(Exception error) { failure = "Stage " + stage + ": " + error; evidence.Add(failure); Debug.Log("[PRESENTATION REVIEW] " + failure); Finish(false); }
    }
    static void AuditColours()
    {
        var items = Field<RectTransform>(catalog, "itemContainer");
        var views = items.GetComponentsInChildren<RestaurantPlaceableCatalogItemView>();
        foreach (string colour in colours)
            Check(views.Any(v => v.Definition != null && v.Definition.ItemId == "pf_bb_chair_master_001_" + colour && v.Definition.HasValidPrefab),
                "Live chair card and real prefab present: " + colour);
        ScrollRect scroll = items.GetComponentInParent<ScrollRect>(); Check(scroll != null, "Native chair catalogue has a scroll viewport");
        LayoutRebuilder.ForceRebuildLayoutImmediate(items); Canvas.ForceUpdateCanvases();
        Debug.Log("[PRESENTATION REVIEW] Scroll content=" + items.rect + " viewport=" + scroll.viewport.rect);
        var reachable = new HashSet<string>();
        for(int index=0; index<=8; index++)
        {
            scroll.verticalNormalizedPosition = 1f - index / 8f; Canvas.ForceUpdateCanvases();
            foreach(var view in views)
            {
                var rect = view.transform as RectTransform;
                Vector3 centre = scroll.viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                if (scroll.viewport.rect.Contains(centre)) reachable.Add(view.Definition.ItemId);
                if(index == 0 || index == 8) Debug.Log("[PRESENTATION REVIEW] Scroll=" + index + " item=" + view.Definition.ItemId + " centre=" + centre);
            }
        }
        foreach(string colour in colours) Check(reachable.Contains("pf_bb_chair_master_001_" + colour), "Chair colour reachable by native scrolling: " + colour);
        scroll.verticalNormalizedPosition=1; Capture("presentation-review-chair-catalogue.png");
    }
    static void SelectCard(string id)
    {
        var view = Field<RectTransform>(catalog, "itemContainer").GetComponentsInChildren<RestaurantPlaceableCatalogItemView>()
            .FirstOrDefault(v => v.Definition != null && v.Definition.ItemId == id);
        Check(view != null, "Live catalogue card present: " + id);
        Field<Button>(view, "button").onClick.Invoke();
    }
    static void AuditInspector(string id)
    {
        definition.TryGetItem(id, out var item);
        var root = Field<RectTransform>(inspector, "root");
        Check(root.gameObject.activeInHierarchy && Field<TMP_Text>(inspector, "titleText").text == item.DisplayName,
            "Drag inspector refreshes actual asset title: " + id);
        Check(Field<RectTransform>(inspector, "rulesSection").gameObject.activeInHierarchy,
            "Placement rules remain visible during drag: " + id);
        var status = Field<Image>(inspector, "statusBackground").rectTransform;
        var corners = new Vector3[4]; status.GetWorldCorners(corners);
        Check(corners.All(point => root.rect.Contains((Vector2)root.InverseTransformPoint(point))),
            "Status box stays inside the inspector panel: " + id);
        var canvas = root.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        root.GetWorldCorners(corners);
        Check(corners.All(point => canvas.rect.Contains((Vector2)canvas.InverseTransformPoint(point))),
            "Drag inspector stays inside the screen canvas: " + id);
        Check(!Field<RectTransform>(inspector, "previewSection").gameObject.activeSelf,
            "Compact drag inspector hides only the large preview: " + id);
    }
    static void SetClose(Component host)
    {
        foreach(var scroll in Field<RectTransform>(host, "modalRoot").GetComponentsInChildren<ScrollRect>()) BistroBuilderUiScrollRegion.Configure(scroll);
        Canvas.ForceUpdateCanvases();
        close = Field<RectTransform>(host, "modalRoot").Find("Panel/Header/Close").GetComponent<Button>();
        Check((close.transform as RectTransform).rect.width >= 44f && (close.transform as RectTransform).rect.height >= 44f,
            "Close has a usable 44-unit minimum pointer target: " + host.GetType().Name);
        var pointer = new PointerEventData(EventSystem.current) { position = ClosePosition() };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        var lift = close.GetComponentInParent<Canvas>();
        Debug.Log("[PRESENTATION REVIEW] Close nearestCanvas="+lift.name+" sorting="+lift.sortingOrder+" raycaster="+(lift.GetComponent<GraphicRaycaster>()!=null)+" hitCount="+hits.Count+" graphics="+GraphicRegistry.GetGraphicsForCanvas(lift).Count+" closeDepth="+close.GetComponent<Image>().depth+" culled="+close.GetComponent<Image>().canvasRenderer.cull+" graphicCanvas="+close.GetComponent<Image>().canvas.name);
        foreach(var hit in hits) Debug.Log("[PRESENTATION REVIEW] Actual close hit="+hit.gameObject.name+" parent="+hit.gameObject.transform.parent?.name);
        Check(lift.GetComponent<GraphicRaycaster>() != null, "Sticky header retains native pointer raycaster");
        int order = lift.sortingOrder;
        foreach(var scroll in Field<RectTransform>(host, "modalRoot").GetComponentsInChildren<ScrollRect>()) BistroBuilderUiScrollRegion.Configure(scroll);
        Check(lift.sortingOrder == order, "Repeated scroll configuration preserves header sorting order");
        close.onClick.AddListener(() => Debug.Log("[PRESENTATION REVIEW] Native close onClick received"));
        var trigger = close.GetComponent<EventTrigger>() ?? close.gameObject.AddComponent<EventTrigger>();
        foreach(var type in new[]{EventTriggerType.PointerDown, EventTriggerType.PointerUp, EventTriggerType.PointerClick})
        { var entry = new EventTrigger.Entry{eventID=type}; entry.callback.AddListener(data => Debug.Log("[PRESENTATION REVIEW] Native close event " + type)); trigger.triggers.Add(entry); }
        TracePointer();
        Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>() == close,
            "Native EventSystem reaches close first: " + host.GetType().Name);
    }
    static void TracePointer()
    {
        var module = EventSystem.current.currentInputModule as InputSystemUIInputModule;
        Debug.Log("[PRESENTATION REVIEW] Pointer stage="+stage+" screen="+Screen.width+"x"+Screen.height+" desired="+ClosePosition()+" mouse="+mouse.position.ReadValue()+" pressed="+mouse.leftButton.isPressed+" module="+module+" enabled="+EventSystem.current.enabled+" focused="+EventSystem.current.isFocused+" pointEnabled="+module?.point?.action?.enabled+" point="+module?.point?.action?.ReadValue<Vector2>()+" clickEnabled="+module?.leftClick?.action?.enabled+" click="+module?.leftClick?.action?.ReadValue<float>()+" hit="+module?.GetLastRaycastResult(mouse.deviceId).gameObject);
    }
    static Vector2 ClosePosition()
    {
        var rect = close.transform as RectTransform; var canvas = close.GetComponentInParent<Canvas>();
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
    }
    static void Pointer(bool down)
    {
        var state = new MouseState { position = ClosePosition() }; if(down) state = state.WithButton(MouseButton.Left);
        TracePointer(); InputSystem.QueueStateEvent(mouse, state);
        // Paused Editor batch stepping skips the native input update. Feed the real
        // action/module pipeline before the next runtime frame, never invoke its click handler.
        bool paused = EditorApplication.isPaused;
        try
        {
            // Input Manager deliberately skips player updates while the batch pump is paused.
            // Dispatch one genuine Dynamic input frame, then let EventSystem.Update consume it.
            EditorApplication.isPaused = false;
            var update = typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[]{typeof(InputUpdateType)}, null);
            if(update == null) throw new Exception("Native Dynamic input update API unavailable.");
            update.Invoke(null, new object[]{InputUpdateType.Dynamic});
            Check(Vector2.Distance(mouse.position.ReadValue(), state.position) < .1f && mouse.leftButton.isPressed == down,
                "Native mouse state consumed at stage " + stage);
        }
        finally { EditorApplication.isPaused = paused; }
    }
    static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) throw new Exception("No native graphics device for evidence.");
        var shell = Find<BistroBuilderUiShell>(); var canvas = shell.GetComponentInParent<Canvas>().rootCanvas;
        var camera = Camera.main; Check(camera != null, "Native rendering camera available for " + name);
        var roots=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas).ToArray();
        var oldModes=roots.Select(c=>c.renderMode).ToArray(); var oldCameras=roots.Select(c=>c.worldCamera).ToArray(); var oldDistances=roots.Select(c=>c.planeDistance).ToArray();
        var oldTarget=camera.targetTexture; var oldActive=RenderTexture.active;
        var target=new RenderTexture(Screen.width,Screen.height,24); var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target; foreach(var rootCanvas in roots){ rootCanvas.renderMode=RenderMode.ScreenSpaceCamera; rootCanvas.worldCamera=camera; rootCanvas.planeDistance=Mathf.Max(camera.nearClipPlane+.1f,.5f); }
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine("Logs",name),image.EncodeToPNG());
        }
        finally
        { RenderTexture.active=oldActive;camera.targetTexture=oldTarget; for(int i=0;i<roots.Length;i++){ roots[i].renderMode=oldModes[i];roots[i].worldCamera=oldCameras[i];roots[i].planeDistance=oldDistances[i]; }
            UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target); }
    }
    static void Finish(bool pass)
    {
        EditorApplication.update -= Tick;
        if(mouse != null) InputSystem.RemoveDevice(mouse);
        if(edit != null && edit.HasActivePlacement) edit.CancelActivePlacement();
        SessionState.SetBool(Key + ".Pass", pass); EditorApplication.ExitPlaymode();
    }
}