using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Explicit developer graphics capture. Ordinary player sessions never run it.</summary>
public sealed partial class BistroBuilderGalleryVivaCapture : MonoBehaviour
{
    private bool capture, startupCapture, headerCapture, toolbarCapture;
    private bool blackFrame;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        bool headerCapture=Array.Exists(args,a=>a=="-bb-gallery-viva-capture-header");
        bool toolbarCapture=Array.Exists(args,a=>a=="-bb-gallery-viva-capture-toolbar");
        bool panelsCapture=Array.Exists(args,a=>a=="-bb-gallery-viva-capture-panels");
        bool chairsCapture=Array.Exists(args,a=>a=="-bb-gallery-viva-capture-chairs");
        bool startupCapture=chairsCapture||panelsCapture||toolbarCapture||headerCapture||Array.Exists(args,a=>a=="-bb-gallery-viva-capture-startup");
        bool capture = startupCapture||Array.Exists(args, a => a == "-bb-gallery-viva-capture");
        bool nativeReview=!Application.isEditor && (Application.productName=="BistroBuilder Editor V2 R4" || Application.productName=="BistroBuilder Editor V2 R5" || Application.productName=="BistroBuilder Editor V2 R6" || Application.productName=="BistroBuilder Editor V2 R7" || Application.productName=="BistroBuilder Editor V2 R8" || Application.productName=="BistroBuilder Editor V2 R9");
        if (!capture && !nativeReview && !Array.Exists(args, a => a == "-bb-gallery-viva-review")) return;
        Debug.Log("R8_CAPTURE_BOOT|capture="+capture+"|startup="+startupCapture+"|panels="+panelsCapture);
        Application.runInBackground = true; Application.targetFrameRate = 60;
        // Review starts directly in the existing scene, without an opening-menu
        // click creating a new game or writing a save while captures resize.
        var opening = FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(FindObjectsInactive.Include);
        if (opening != null) { opening.Hide(); opening.enabled = false; }
        var probe = new GameObject("__GaleriaVivaCapture").AddComponent<BistroBuilderGalleryVivaCapture>();
        probe.capture = capture;probe.startupCapture=startupCapture;probe.headerCapture=headerCapture;probe.toolbarCapture=toolbarCapture;probe.panelsCapture=panelsCapture;probe.chairsCapture=chairsCapture; DontDestroyOnLoad(probe.gameObject);
        var reviewDisplay=probe.gameObject.AddComponent<BistroBuilderEditorV2ReviewDisplay>();
        // Automated runs own display changes; interactive reviews retain F11.
        reviewDisplay.enabled=!capture;
        if(Array.Exists(args,a=>a=="-bb-editor-v2-fullscreen") || (nativeReview && !capture && !Array.Exists(args,a=>a=="-bb-editor-v2-windowed")))BistroBuilderEditorV2ReviewDisplay.EnterFullscreen();
    }
    private IEnumerator Start()
    {
        var routine = RunCapture();
        while(true) {
            object current=null; bool more=false; Exception failure=null;
            try { more=routine.MoveNext(); if(more)current=routine.Current; } catch(Exception e) { failure=e; }
            if(failure!=null) {
                Debug.LogError("R8_CAPTURE_FAILED|"+failure);
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..","GalleryVivaQA"));
                Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path,"failure.txt"),failure.ToString());
                Application.Quit(1); yield break;
            }
            if(!more)yield break; yield return current;
        }
    }
    private void LateUpdate()
    {
        if(!capture)return;
        // Isolate deterministic QA from physical clicks, only under explicit capture flags.
        foreach(var events in FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))events.enabled=false;
        var interaction=FindFirstObjectByType<RestaurantEditInteractionController>();
        if(interaction!=null)interaction.enabled=false;
    }
    private IEnumerator RunCapture()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "GalleryVivaQA"));
        if(capture)Directory.CreateDirectory(folder);
        if(capture)File.WriteAllText(Path.Combine(folder,"boot_trace.txt"),"capture="+capture+"|startup="+startupCapture+"|panels="+panelsCapture+"|mode="+Screen.fullScreenMode);
        Debug.Log("R8_CAPTURE_START|capture="+capture+"|startup="+startupCapture+"|panels="+panelsCapture);
        if (capture&&!startupCapture) Screen.SetResolution(1280,720,FullScreenMode.Windowed);
        for (int i = 0; i < 60; i++) yield return null;
        FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(FindObjectsInactive.Include)?.Hide();
        var mode = FindFirstObjectByType<RestaurantEditModeService>(FindObjectsInactive.Include);
        if (mode == null || (!mode.IsEditModeActive && !mode.TryEnterEditMode(out _, out _)))
        { File.WriteAllText(Path.Combine(folder, "capture.txt"), "FAIL: enter edit mode"); Application.Quit(1); yield break; }
        var panel = FindFirstObjectByType<RestaurantPlaceableCatalogPanel>(FindObjectsInactive.Include);
        panel.SelectSection(RestaurantEditCatalogSection.Build);
        for (int i = 0; i < 30; i++) yield return null;
        var gallery = panel.GetComponent<BistroBuilderGalleryViva>();
        var report = new StringBuilder("REAL UNITY PLAYER\nGPU=" + SystemInfo.graphicsDeviceName + "\n");
        if (gallery == null || !gallery.IsReady) { File.WriteAllText(Path.Combine(folder, "capture.txt"), "FAIL: native gallery"); Application.Quit(1); yield break; }
        if(!capture||startupCapture) {
            // The review opens in the approved catalogue+details composition,
            // using the actual published table, without fabricated selection.
            FindFirstObjectByType<BistroBuilderEditorV2Coordinator>()?.TryActivateTool(BistroBuilderEditorV2ToolFamily.Furniture,"select",out _);
            if(capture)gallery.ResetFilters();
            gallery.SelectCategory(chairsCapture || (!capture && Application.productName=="BistroBuilder Editor V2 R9") ? 2 : 1);
            for(int i=0;i<8;i++)yield return null;
            var detail=gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded/Featured/Details");
            detail.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            for(int i=0;i<8;i++)yield return null;
            if(startupCapture) {
                Debug.Log("R8_CAPTURE_BEFORE_FRAME|focused="+Application.isFocused+"|size="+Screen.width+"x"+Screen.height);
                File.AppendAllText(Path.Combine(folder,"boot_trace.txt"),"\nBEFORE_FRAME|focused="+Application.isFocused);
                yield return Capture(folder,"ReviewStartup_Fullscreen",panel,report);
                if(headerCapture) {
                    var shell=FindFirstObjectByType<BistroBuilderUiShell>();
                    var header=shell.GetComponentInParent<Canvas>().transform.Find(BistroBuilderUiShell.RootName+"/"+BistroBuilderUiShell.EditModeTopBarName);
                    var day=header.Find("EditDay").GetComponent<UnityEngine.UI.Button>();
                    var menu=header.Find("LightingPreviewMenu");
                    string[] ids={"Dia","Atardecer","Noche","Dia_Return"};int[] presets={0,1,2,0};
                    for(int i=0;i<presets.Length;i++) {
                        day.onClick.Invoke();
                        for(int frame=0;frame<3;frame++)yield return null;
                        if(i==1)yield return Capture(folder,"Header_Lighting_Menu",panel,report);
                        menu.Find("LightPreset"+presets[i]).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                        for(int frame=0;frame<8;frame++)yield return null;
                        var label=day.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
                        var sun=day.transform.Find("Icon/ApprovedVectorGlyph").GetComponent<TMPro.TMP_Text>();
                        BistroBuilderApprovedGlyphsR4.Resolve("sun",out var expectedSun,out _);
                        if(sun.text!=expectedSun || label.fontSize!=14 || label.preferredWidth>label.rectTransform.rect.width+.5f || menu.gameObject.activeSelf)blackFrame=true;
                        report.AppendLine("HeaderLighting="+ids[i]+"|Label="+label.text+"|FontSize="+label.fontSize+"|SunPreserved="+(sun.text==expectedSun));
                        yield return Capture(folder,"Header_"+ids[i],panel,report);
                    }
                }
                if(toolbarCapture) {
                    var toolbarChecks=CaptureToolbar(folder,panel,report);
                    while(toolbarChecks.MoveNext())yield return toolbarChecks.Current;
                }
                if(panelsCapture) {
                    var panels=CapturePanels(folder,panel,report);
                    while(panels.MoveNext())yield return panels.Current;
                }
                if(chairsCapture) {
                    var photos=CaptureChairPhotos(folder,panel,report);
                    while(photos.MoveNext())yield return photos.Current;
                }
                report.AppendLine("StartupFullScreen="+Screen.fullScreenMode);
                report.AppendLine("StartupTool="+FindFirstObjectByType<BistroBuilderEditorV2Coordinator>()?.ActiveToolId);
                if(Screen.fullScreenMode!=FullScreenMode.FullScreenWindow)blackFrame=true;
                report.AppendLine(blackFrame?"CAPTURE_FAILED":"CAPTURE_COMPLETE");
                File.WriteAllText(Path.Combine(folder,chairsCapture?"chairs.txt":panelsCapture?"panels.txt":toolbarCapture?"toolbar.txt":headerCapture?"header.txt":"startup.txt"),report.ToString());
                Application.Quit(blackFrame?1:0);
            }
            else Destroy(this);
            yield break;
        }
        int[] widths = { 1672, 1280 }; int[] heights = { 941, 720 };
        for (int i = 0; i < 2; i++)
        {
            Screen.SetResolution(widths[i], heights[i], FullScreenMode.Windowed);
            gallery.ResetFilters(); gallery.SelectCategory(0);
            for (int frame = 0; frame < 45; frame++) yield return null;
            yield return Capture(folder, "All_" + widths[i], panel, report);
            gallery.SelectCategory(2);
            for (int frame = 0; frame < 8; frame++) yield return null;
            yield return Capture(folder, "Seating_" + widths[i], panel, report);
        }
        gallery.ResetFilters(); gallery.SelectCategory(0);
        gallery.SearchField.text = "básica";
        for (int i = 0; i < 10; i++) yield return null;
        yield return Capture(folder, "Search_Basica_1280", panel, report);
        gallery.SearchField.text = "sin resultados";
        for (int i = 0; i < 10; i++) yield return null;
        yield return Capture(folder, "Empty_1280", panel, report);
        gallery.ResetFilters(); gallery.SelectCategory(2);
        var expanded = gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded");
        expanded.Find("Filters").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        for (int i = 0; i < 8; i++) yield return null;
        yield return Capture(folder, "Filters_1280", panel, report);
        expanded.Find("Filters").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        expanded.Find("Featured/Details").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        for (int i = 0; i < 8; i++) yield return null;
        yield return Capture(folder, "Inspector_1280", panel, report);
        panel.SelectSection(RestaurantEditCatalogSection.Walls);
        for (int i = 0; i < 20; i++) yield return null;
        yield return Capture(folder, "Construction_1280", panel, report);
        panel.SelectSection(RestaurantEditCatalogSection.Surfaces);
        for (int i = 0; i < 20; i++) yield return null;
        yield return Capture(folder, "Surfaces_1280", panel, report);
        bool selectedSurface = PrepareSurfaceSelection(out string surfaceError);
        report.AppendLine("SurfaceSelection="+selectedSurface+" "+surfaceError);
        Screen.SetResolution(1908,1042,FullScreenMode.Windowed);
        for(int i=0;i<30;i++) yield return null;
        yield return Capture(folder,"SurfaceSelection_1908",panel,report);
        panel.SelectSection(RestaurantEditCatalogSection.Build);
        var coordinator = FindFirstObjectByType<BistroBuilderEditorV2Coordinator>();
        coordinator.TryActivateTool(BistroBuilderEditorV2ToolFamily.Furniture, "select", out _);
        var interaction = FindFirstObjectByType<RestaurantEditInteractionController>();
        var worldItems = FindObjectsByType<RestaurantPlaceableObject>(FindObjectsSortMode.None);
        if (worldItems.Length > 1)
        {
            interaction.TrySelectPlaceable(worldItems[0]);
            interaction.TrySelectPlaceable(worldItems[1], true, false);
            Screen.SetResolution(1672, 941, FullScreenMode.Windowed);
            for (int i = 0; i < 35; i++) yield return null;
            yield return Capture(folder, "WorldSelection_1672", panel, report);
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            for(int i=0;i<40;i++)yield return null;
            yield return Capture(folder,"WorldSelection_4K",panel,report,2);
        }
        BistroBuilderEditorV2ReviewDisplay.EnterFullscreen();
        for(int i=0;i<50;i++)yield return null;
        bool nativeFullscreen=Screen.fullScreenMode==FullScreenMode.FullScreenWindow &&
            Screen.width==Display.main.systemWidth && Screen.height==Display.main.systemHeight;
        report.AppendLine("FULLSCREEN_NATIVE="+nativeFullscreen+"|Mode="+Screen.fullScreenMode+"|Desktop="+Display.main.systemWidth+"x"+Display.main.systemHeight);
        if(!nativeFullscreen)blackFrame=true;
        yield return Capture(folder,"Fullscreen_Native",panel,report);
        BistroBuilderEditorV2ReviewDisplay.ExitFullscreen();
        for(int i=0;i<35;i++)yield return null;
        report.AppendLine("FULLSCREEN_RETURN_WINDOW="+(Screen.fullScreenMode==FullScreenMode.Windowed));
        if(Screen.fullScreenMode!=FullScreenMode.Windowed)blackFrame=true;
        report.AppendLine(blackFrame ? "CAPTURE_FAILED" : "CAPTURE_COMPLETE");
        File.WriteAllText(Path.Combine(folder, "capture.txt"), report.ToString());
        Application.Quit(blackFrame ? 1 : 0);
    }
    public static bool SelectToolbarFixture(out string itemId)
    {
        itemId="";
        var interaction=FindFirstObjectByType<RestaurantEditInteractionController>();
        var authority=FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>();
        if(interaction==null||authority==null)return false;
        foreach(var item in FindObjectsByType<RestaurantPlaceableObject>(FindObjectsSortMode.None)) {
            interaction.ClearSelection();
            if(!interaction.TrySelectPlaceable(item))continue;
            authority.RefreshSelection();
            if(authority.Current.Supports(BistroBuilderEditorV2SelectionCapability.Delete) &&
                authority.Current.Supports(BistroBuilderEditorV2SelectionCapability.Move)) {
                itemId=item.ItemDefinition?.ItemId ?? item.name;return true;
            }
        }
        interaction.ClearSelection();return false;
    }
    private IEnumerator CaptureToolbar(string folder,RestaurantPlaceableCatalogPanel panel,StringBuilder report)
    {
        var shell=FindFirstObjectByType<BistroBuilderUiShell>();
        var footer=shell.GetComponentInParent<Canvas>().transform.Find(BistroBuilderUiShell.RootName+"/"+BistroBuilderUiShell.EditModeBottomBarName);
        var delete=footer.Find("EditSelectionTools/Buttons/EditDelete").GetComponent<UnityEngine.UI.Button>();
        var style=delete.GetComponent<BistroBuilderEditChromeControl>();
        report.AppendLine("DeleteInitialDisabled="+!delete.IsInteractable()+"|State="+style.ToolbarState);
        if(delete.IsInteractable())blackFrame=true;
        yield return Capture(folder,"Toolbar_Disabled_Fullscreen",panel,report);
        bool selected=SelectToolbarFixture(out string itemId);
        for(int frame=0;frame<60&&!delete.IsInteractable();frame++)yield return null;
        for(int frame=0;frame<3;frame++)yield return null;
        report.AppendLine("RealDeleteSelection="+selected+"|Item="+itemId+"|Interactable="+delete.IsInteractable());
        if(!selected || !delete.IsInteractable())blackFrame=true;
        yield return Capture(folder,"Toolbar_Available_Fullscreen",panel,report);
        var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
            button=UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        UnityEngine.EventSystems.ExecuteEvents.Execute(delete.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
        for(int frame=0;frame<3;frame++)yield return null;
        report.AppendLine("DeleteHover="+style.ToolbarState);
        yield return Capture(folder,"Toolbar_Delete_Hover",panel,report);
        UnityEngine.EventSystems.ExecuteEvents.Execute(delete.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
        for(int frame=0;frame<3;frame++)yield return null;
        report.AppendLine("DeletePress="+style.ToolbarState);
        yield return Capture(folder,"Toolbar_Delete_Pressed",panel,report);
        UnityEngine.EventSystems.ExecuteEvents.Execute(delete.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
        UnityEngine.EventSystems.ExecuteEvents.Execute(delete.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
        for(int frame=0;frame<3;frame++)yield return null;
        yield return Capture(folder,"Toolbar_Available_4K",panel,report,2);
        report.AppendLine("ToolbarCaptureActions=pointer hover/down/up only; no delete click");
    }
    public static bool PrepareSurfaceSelection(out string error)
    {
        error="";
        var session=FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
        var tool=FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        if(session==null||tool==null) { error="Missing construction authority";return false; }
        if(!session.HasSession&&!session.TryBeginSession(out error))return false;
        if(session.Session.RoomProjections.Count==0)
        {
            // QA-only, reversible, off-site room fixture; never saved or run in normal play.
            var points=new[]{new Vector2(120,120),new Vector2(124,120),new Vector2(124,124),new Vector2(120,124)};
            var commands=new IBistroBuilderEditCommand[4];
            for(int i=0;i<4;i++) commands[i]=new BistroBuilderCreateWallCommand(new BistroBuilderWallRecord {
                wallId=new BistroBuilderEditId("v2_visual_qa_wall_"+i),buildPlaneId="default",axisStart=points[i],axisEnd=points[(i+1)%4],
                baseElevation=0,height=2.5f,thickness=.12f,wallDefinitionId="wall.default" });
            if(!session.TryExecute(new BistroBuilderAtomicEditCommand("Visual QA room",commands),out _,out error))return false;
        }
        if(session.Session.RoomProjections.Count==0) { error="Room topology unavailable";return false; }
        bool selected=tool.TrySelectArchitecture(BistroBuilder.ConstructionAuthoring.EntityKind.Room,session.Session.RoomProjections[0].room.roomId,out error);
        FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>()?.RefreshSelection();
        return selected;
    }

    private IEnumerator Capture(string folder, string name, RestaurantPlaceableCatalogPanel panel, StringBuilder report,int supersize=1)
    {
        yield return new WaitForEndOfFrame();
        var screen = ScreenCapture.CaptureScreenshotAsTexture(supersize);
        float luminance = 0;
        for (int y = 1; y < 8; y++) for (int x = 1; x < 8; x++) luminance += screen.GetPixel(screen.width * x / 8, screen.height * y / 8).grayscale;
        if (luminance < .01f) { blackFrame = true; report.AppendLine("FAIL_BLACK_FRAME=" + name); Debug.LogError("Black frame: " + name); }
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), screen.EncodeToPNG());
        if (panel.TryGetGuiRect(out var gui))
        {
            float sx=(float)screen.width/Screen.width,sy=(float)screen.height/Screen.height;
            int x = Mathf.Clamp(Mathf.RoundToInt(gui.x*sx), 0, screen.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(screen.height - gui.yMax*sy), 0, screen.height - 1);
            int width = Mathf.Min(Mathf.RoundToInt(gui.width*sx), screen.width - x);
            int height = Mathf.Min(Mathf.RoundToInt(gui.height*sy), screen.height - y);
            var crop = new Texture2D(width, height, TextureFormat.RGB24, false);
            crop.SetPixels(screen.GetPixels(x, y, width, height)); crop.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + "_Catalog.png"), crop.EncodeToPNG()); Destroy(crop);
        }
        var canvas = panel.GetComponentInParent<Canvas>().transform;
        int inspectors=0;
        foreach(string id in new[]{"BB_UIUX_PlaceableInspector","BB_EditorV2_SelectionInspector","BB_ArchitectureInspector"})
        { var node=canvas.Find(id);if(node!=null&&node.gameObject.activeInHierarchy)inspectors++; }
        report.AppendLine(name+"|VisibleInspectors="+inspectors);
        if(inspectors>1) { blackFrame=true;report.AppendLine("FAIL_OVERLAPPING_INSPECTORS="+name); }
        report.AppendLine(name + "|Resolution=" + Screen.width + "x" + Screen.height + "|Results=" + panel.GalleryResults.Count + "|Columns=" + panel.VirtualList.ColumnCount + "|Pool=" + panel.VirtualList.PoolCount);
        report.AppendLine(name+"|CapturePixels="+screen.width+"x"+screen.height+"|Supersize="+supersize);
        Destroy(screen);
    }
}

/// <summary>Display-only control for explicitly launched V2 review sessions.</summary>
public sealed class BistroBuilderEditorV2ReviewDisplay : MonoBehaviour
{
    public static void EnterFullscreen()
    {
        Screen.SetResolution(Display.main.systemWidth,Display.main.systemHeight,FullScreenMode.FullScreenWindow);
    }
    public static void ExitFullscreen()
    {
        int width=Mathf.Min(1672,Mathf.RoundToInt(Display.main.systemWidth*.87f));
        Screen.SetResolution(width,Mathf.RoundToInt(width*941f/1672f),FullScreenMode.Windowed);
    }
    public static void Toggle()
    {
        if(Screen.fullScreenMode==FullScreenMode.Windowed)EnterFullscreen();else ExitFullscreen();
    }
    private void Update()
    {
        var keyboard=UnityEngine.InputSystem.Keyboard.current;
        if(keyboard!=null&&keyboard.f11Key.wasPressedThisFrame)Toggle();
    }
}
