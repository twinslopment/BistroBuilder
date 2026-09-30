using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BistroBuilderEditChromePlayTest
{
    const string Key="BB.EditChrome.ReferenceTest";
    static int stage;static double next;static string failure;
    static BistroBuilderConstructionAuthoringRuntimeTool tool;
    static RestaurantEditInteractionController edit;
    static BistroBuilderUiShell shell;
    static BistroBuilderEditChromePlayTest(){EditorApplication.playModeStateChanged+=State;}
    public static void RunBatch()
    {
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Pass",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");EditorApplication.isPlaying=true;
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){stage=0;next=EditorApplication.timeSinceStartup+6;failure=null;Application.runInBackground=true;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+".Pass",false)?0:1);}
    }
    static void Log(string message,string stack,LogType type){if(stack.Contains("UnityEditor.Search.SearchDatabase")){Debug.LogWarning("Editor Search indexing exception (outside game runtime): "+message);return;}if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)failure=message;}
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static Button B(string name){var go=GameObject.Find(name);Check(go!=null,"Missing "+name);return go.GetComponent<Button>();}
    static GameObject AnyObject(string name)
    {
        return Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(t=>t!=null&&t.name==name)?.gameObject;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+1;
        try{
            Check(failure==null,failure);
            switch(stage++)
            {
                case 0:
                    Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                    shell=Object.FindFirstObjectByType<BistroBuilderUiShell>();shell.EnsureShell();
                    edit=Object.FindFirstObjectByType<RestaurantEditInteractionController>();edit.TryEnterEditMode();
                    tool=Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();tool.SetMode(BistroBuilderConstructionRuntimeMode.Furniture);break;
                case 1:
                    var editTopObject=GameObject.Find(BistroBuilderUiShell.EditModeTopBarName);
                    Check(editTopObject!=null&&editTopObject.activeInHierarchy,"Edit chrome visible");
                    var editTop=editTopObject.GetComponent<RectTransform>();
                    Check(editTop.GetComponentsInChildren<BistroBuilderTopBarPlate>(true).Length>=8,"Edit top bar reuses approved plate language");
                    Check(editTop.GetComponentsInChildren<BistroBuilderTopBarArtwork>(true).Length==1,"Edit top bar reuses approved Bistro Builder artwork");
                    Check(editTop.Find("EditModeTitle")!=null,"Edit mode identity remains explicit");
                    foreach(var rootName in new[]{BistroBuilderUiShell.EditModeTopBarName,BistroBuilderUiShell.EditModeBottomBarName}){
                        var root=GameObject.Find(rootName);
                        Check(root.GetComponentsInChildren<BistroBuilderEditChromeIcon>(true).Length>=10,"Vector icon coverage");
                        Check(root.GetComponentsInChildren<BistroBuilder.UI.Iconography.BBIconButton>(true).Length==0,"No duplicate legacy icons");
                        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))Check(!text.text.Any(c=>"☀✎✋✥▦▣↶↷⚙".Contains(c)),"No placeholder glyphs");
                    }
                    Object.FindFirstObjectByType<BistroBuilderUiDesignSystem>().ApplyAllNow(true);
                    BistroBuilder.UI.Iconography.BBIconographyRuntime.DecorateAll();
                    B("EditDecor").onClick.Invoke();break;
                case 2:
                    Check(Object.FindFirstObjectByType<RestaurantPlaceableCatalogPanel>().SelectedCategoryCode==(int)RestaurantPlaceableItemCategory.Decoration,"Decoration routing");
                    Check(B("EditDecor").GetComponent<Image>().color.g>.4f,"Selected category visible");
                    B("EditServices").onClick.Invoke();Check(Object.FindFirstObjectByType<RestaurantPlaceableCatalogPanel>().SelectedCategoryCode==(int)RestaurantPlaceableItemCategory.ServiceEquipment,"Services routing");
                    B("EditWalls").onClick.Invoke();Check(tool.Mode==BistroBuilderConstructionRuntimeMode.Wall,"Walls routing");
                    B("EditTerrain").onClick.Invoke();Check(tool.Mode==BistroBuilderConstructionRuntimeMode.Room,"Terrain routing");
                    var grid=Object.FindFirstObjectByType<BistroBuilderEditGridOverlay>();bool enabled=grid.enabled;
                    B("EditGrid").onClick.Invoke();Check(grid.enabled!=enabled&&!grid.IsGridVisible,"Grid hidden");B("EditGrid").onClick.Invoke();Check(grid.IsGridVisible,"Grid restored");
                    B("EditBuild").onClick.Invoke();break;
                case 3:
                    var item=Object.FindObjectsByType<RestaurantPlaceableObject>(FindObjectsSortMode.None).FirstOrDefault(p=>p.ItemDefinition!=null);
                    Check(item!=null&&edit.TrySelectPlaceable(item),"Select real furniture");break;
                case 4:
                    var inspector=GameObject.Find("BB_UIUX_PlaceableInspector");
                    Check(inspector!=null&&inspector.activeInHierarchy,"Unified contextual inspector visible for world selection");
                    Check(inspector.GetComponent<BistroBuilderUiStyleIsolation>()!=null,"Inspector owns an isolated authored visual contract");
                    Object.FindFirstObjectByType<BistroBuilderUiDesignSystem>().ApplyAllNow(true);
                    var inspectorPanel=inspector.GetComponent<Image>();
                    var inspectorTitle=inspector.transform.Find("Viewport/Content/Header/Title")?.GetComponent<TMP_Text>();
                    Check(inspectorPanel!=null&&inspectorPanel.color.r>.9f&&inspectorPanel.color.g>.9f,"Inspector remains light after global design-system pass");
                    Check(inspectorTitle!=null&&inspectorTitle.color.grayscale<.45f,"Inspector title remains dark and legible after global design-system pass");
                    Check(inspector.transform.Find("Viewport/Content/Actions")!=null,"Unified inspector exposes contextual actions");
                    Check(inspector.transform.Find("Viewport/Content/Preview/Favorite")==null,"Inspector must not expose non-persistent fake favorites");
                    var legacy=GameObject.Find("PlaceableContextContent");
                    Check(legacy==null||!legacy.activeInHierarchy,"Legacy context panel suppressed");
                    var inspectorDuplicate=inspector.transform.Find("Viewport/Content/Actions/Secondary/Duplicate")?.GetComponent<Button>();
                    Check(inspectorDuplicate!=null&&inspectorDuplicate.interactable,"Inspector duplicate enabled for furniture");
                    var bottomDuplicate=AnyObject("EditDuplicate");
                    Check(bottomDuplicate!=null&&!bottomDuplicate.activeSelf,"Bottom duplicate hidden when inspector owns furniture actions");
                    var legacyInitial=AnyObject("Diseño inicial");
                    var legacyBottom=AnyObject("Acciones de construcción");
                    Check(legacyInitial==null||!legacyInitial.activeInHierarchy,"Legacy initial-design overlay remains suppressed");
                    Check(legacyBottom==null||!legacyBottom.activeInHierarchy,"Legacy construction bottom overlay remains suppressed");
                    inspectorDuplicate.onClick.Invoke();Check(edit.HasActivePlacement,"Inspector duplicate uses creation preview");edit.CancelActivePlacement();
                    Capture("BarrasEdicion1920.png",1920,1080);
                    Capture("BarrasEdicion1280.png",1280,720);
                    Capture("BarrasEdicion800.png",800,600);
                    Capture("BarrasEdicionUltrawide.png",3440,1440);
                    B("EditHome").onClick.Invoke();break;
                case 5:
                    Check(shell.GetComponent<BistroBuilderOptionsScreen>().IsOpen,"Home exposes safe game menu");
                    Check(GameObject.Find(BistroBuilderUiShell.TopBarName)!=null,"Normal global top bar returns while management is open from edit mode");
                    Check(GameObject.Find(BistroBuilderUiShell.EditModeTopBarName)==null,"Edit tool bar yields to global navigation during management");
                    shell.GetComponent<BistroBuilderOptionsScreen>().Close();break;
                case 6:
                    Check(GameObject.Find(BistroBuilderUiShell.EditModeTopBarName)!=null,"Edit top bar returns after closing management");
                    Check(GameObject.Find(BistroBuilderUiShell.TopBarName)==null,"Normal top bar is not duplicated over the edit viewport");
                    Finish(true,"Approved normal/edit top-bar continuity, responsive edit captures at 1920/1280/800/ultrawide, unified contextual inspector as furniture action authority, no fake favorite state, legacy context/initial/bottom overlays suppressed, category states, real tools, grid toggle, duplicate preview and game-menu handoff.");break;
            }
        }catch(Exception e){Finish(false,e.ToString());}
    }
    static void Capture(string name,int width,int height)
    {
        BistroBuilderOptionsPlayTest.Capture(name,width,height);
    }
    static void Finish(bool pass,string message)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EditChromeReferenceTest.txt",(pass?"PASS ":"FAIL ")+message);
        Debug.Log("BB_EDIT_CHROME_"+(pass?"PASS ":"FAIL ")+message);SessionState.SetBool(Key+".Pass",pass);EditorApplication.isPlaying=false;
    }
}
