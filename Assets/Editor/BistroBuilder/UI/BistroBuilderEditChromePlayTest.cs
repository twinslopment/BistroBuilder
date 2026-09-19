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
                    Check(GameObject.Find(BistroBuilderUiShell.EditModeTopBarName)!=null,"Edit chrome visible");
                    foreach(var rootName in new[]{BistroBuilderUiShell.EditModeTopBarName,BistroBuilderUiShell.EditModeBottomBarName}){
                        var root=GameObject.Find(rootName);
                        Check(root.GetComponentsInChildren<BistroBuilderEditChromeIcon>().Length>=10,"Vector icon coverage");
                        Check(root.GetComponentsInChildren<BistroBuilder.UI.Iconography.BBIconButton>().Length==0,"No duplicate legacy icons");
                        foreach(var text in root.GetComponentsInChildren<TMP_Text>())Check(!text.text.Any(c=>"☀✎✋✥▦▣↶↷⚙".Contains(c)),"No placeholder glyphs");
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
                    Check(B("EditDuplicate").interactable,"Duplicate enabled for furniture");B("EditDuplicate").onClick.Invoke();Check(edit.HasActivePlacement,"Duplicate uses creation preview");edit.CancelActivePlacement();
                    Capture("BarrasEdicion1920.png",1920,1080);
                    Capture("BarrasEdicion1280.png",1280,720);
                    Capture("BarrasEdicionUltrawide.png",3440,1440);
                    B("EditHome").onClick.Invoke();break;
                case 5:
                    Check(shell.GetComponent<BistroBuilderOptionsScreen>().IsOpen,"Home exposes safe game menu");
                    shell.GetComponent<BistroBuilderOptionsScreen>().Close();break;
                case 6: Finish(true,"Vector icons, theme isolation, category states, real tools, grid toggle, duplicate preview, game menu and 1920/1280/ultrawide captures.");break;
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
