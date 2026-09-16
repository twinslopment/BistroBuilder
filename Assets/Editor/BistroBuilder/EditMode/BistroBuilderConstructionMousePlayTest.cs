using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Exercises real Mouse events and the production Update loop in the canonical scene.</summary>
[InitializeOnLoad]
public static class BistroBuilderConstructionMousePlayTest
{
    private const string Armed="BB.Construction.MouseTest";
    private static BistroBuilderConstructionAuthoringRuntimeTool tool;
    private static BistroBuilderEditRuntimeCoordinator coordinator;
    private static Mouse mouse;
    private static Camera camera;
    private static int stage, baselineWalls, baselineOpenings, lastFrame;
    private static double next, deadline;
    private static string failure;
    private static BistroBuilderNewGameOpeningService opening;
    private static BistroBuilderSaveGameService save;
    private static int emptySlot;
    static BistroBuilderConstructionMousePlayTest() { EditorApplication.playModeStateChanged += State; }
    public static void RunBatch()
    {
        SessionState.SetBool(Armed,true);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying=true;
    }
    private static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Armed,false)) return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            stage=0; failure=null; emptySlot=0; deadline=EditorApplication.timeSinceStartup+240;
            next=EditorApplication.timeSinceStartup+4; EditorApplication.update+=Tick;
            Application.logMessageReceived+=Log;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Armed,false); EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
            EditorApplication.Exit(SessionState.GetBool(Armed+".Pass",false)?0:1);
        }
    }
    private static void Log(string message,string stack,LogType type)
    { if(type==LogType.Exception || type==LogType.Assert) failure=message; }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next || Time.frameCount==lastFrame) return;
        next=EditorApplication.timeSinceStartup+0.25; lastFrame=Time.frameCount;
        try
        {
            if(failure!=null) throw new Exception(failure);
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Mouse test timed out");
            switch(stage)
            {
                case 0:
                    tool=UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
                    coordinator=UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
                    if(tool==null || coordinator==null || BistroBuilderConstructionPlayerPanel.Instance==null || !BistroBuilderConstructionPlayerPanel.Instance.IsReady) return;
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                    Check(UnityEngine.Object.FindFirstObjectByType<RestaurantEditInteractionController>().TryEnterEditMode(),"enter edit");
                    mouse=InputSystem.AddDevice<Mouse>();
                    InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    camera=new GameObject("ConstructionTestCamera").AddComponent<Camera>();
                    camera.orthographic=true; camera.orthographicSize=12; camera.transform.position=new Vector3(33,30,32);
                    camera.transform.rotation=Quaternion.Euler(90,0,0); camera.depth=100;
                    typeof(BistroBuilderConstructionAuthoringRuntimeTool).GetField("interactionCamera",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(tool,camera);
                    tool.SetRoomZone("zone.dining"); break;
                case 1:
                    Check(coordinator.HasSession,"session initialized"); baselineWalls=coordinator.Session.Draft.walls.Count;
                    baselineOpenings=coordinator.Session.Draft.openings.Count; Pointer(30,30,true); break;
                case 2: Pointer(36,34,true); break;
                case 3: Check(coordinator.Session.Draft.walls.Count==baselineWalls,"drag preview isolated"); Pointer(36,34,false); break;
                case 4:
                    Check(coordinator.Session.Draft.walls.Count==baselineWalls+4,"drag release creates four walls: "+tool.StatusMessage);
                    Check(tool.TryUndo(out _),"undo room"); Check(coordinator.Session.Draft.walls.Count==baselineWalls,"one undo removes room");
                    Check(tool.TryRedo(out _),"redo room"); tool.SetMode(BistroBuilderConstructionRuntimeMode.Door);
                    camera.transform.position=new Vector3(33,10,20);camera.transform.LookAt(new Vector3(33,0,32));break;
                case 5: Pointer(33,30,true); break;
                case 6: Pointer(33,30,false); break;
                case 7:
                    Check(coordinator.Session.Draft.openings.Count==baselineOpenings+1,"door insertion on visible wall face");
                    camera.transform.position=new Vector3(33,30,32);camera.transform.rotation=Quaternion.Euler(90,0,0);
                    tool.SetMode(BistroBuilderConstructionRuntimeMode.Window); break;
                case 8: Pointer(33,34,true); break;
                case 9: Pointer(33,34,false); break;
                case 10:
                    Check(coordinator.Session.Draft.openings.Count==baselineOpenings+2,"window insertion");
                    tool.ConfigureModule(2,90);tool.SetMode(BistroBuilderConstructionRuntimeMode.WallModule);break;
                case 11: Pointer(39,31,true);break;
                case 12: Pointer(39,31,false);break;
                case 13:
                    Check(coordinator.Session.Draft.walls.Count==baselineWalls+5,"single module");
                    CapturePreview();
                    Check(BistroBuilderConstructionPlayerPanel.InterceptExit(),"dirty exit protected");
                    var keep=GameObject.Find("Seguir editando"); Check(keep!=null,"exit dialog");
                    keep.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Check(tool.HasDraftChanges,"keep editing preserves draft");
                    Check(tool.TryCancelDraft(out _),"discard");
                    tool.SetMode(BistroBuilderConstructionRuntimeMode.Furniture);break;
                case 14:
                    Check(UnityEngine.Object.FindFirstObjectByType<RestaurantEditInteractionController>().enabled,"furniture input restored");
                    Check(!coordinator.HasSession,"discard clears session");
                    UnityEngine.Object.FindFirstObjectByType<RestaurantEditInteractionController>().TryExitEditMode(true);
                    opening=UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningService>();
                    save=UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
                    for(int candidate=999;candidate>=900;candidate--)
                        if(!save.SlotExists(candidate)) { emptySlot=candidate;break; }
                    Check(emptySlot!=0,"unused diagnostic save slot available");
                    typeof(BistroBuilderNewGameOpeningService).GetField("defaultSaveSlot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(opening,emptySlot);
                    Check(opening.TryCreateNewGame("Construction Empty Regression",BistroBuilderStartingPremisesProfile.Empty,out string createError),"empty new game: "+createError);
                    break;
                case 15:
                    if(save.IsBusy) return;
                    Check(UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>().RegisteredPlaceables.Count==0,"empty removes all furniture");
                    Check(UnityEngine.Object.FindObjectsByType<BistroBuilder367HInstalledFixture>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Length==0,"empty removes legacy counter and stools");
                    Check(save.SlotExists(emptySlot),"empty initial save: "+opening.SaveStatusMessage);
                    var demand=UnityEngine.Object.FindFirstObjectByType<BistroBuilderDynamicDemandService>();
                    Check(!demand.HasOperationalCapacity,"empty has no demand capacity");
                    Check(!demand.TryBuildProjection(out _,out _),"empty cannot generate service demand");
                    Check(!opening.TryValidateInitialDesign(out _),"empty cannot pass opening preflight");
                    Check(opening.TryContinue(out string loadError),"load empty checkpoint: "+loadError);break;
                case 16:
                    if(save.IsBusy) return;
                    Check(save.LastResult!=null && save.LastResult.Succeeded,"empty checkpoint load: "+opening.SaveStatusMessage);
                    Check(opening.IsInitialDesignPhase,"empty load restores design phase");
                    Check(UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>().RegisteredPlaceables.Count==0,"empty load preserves absent furniture");
                    Check(save.TryDeleteSlot(emptySlot,out string deleteError),"cleanup diagnostic slot: "+deleteError);break;
                case 17:
                    if(save.IsBusy) return;
                    Check(!save.SlotExists(emptySlot),"diagnostic slot cleaned");
                    Finish(true,"drag / atomic undo-redo / wall-face doors / windows / modules / protected exit / furniture transition / empty new game save-load / empty opening blocked");return;
            }
            stage++;
        }
        catch(Exception error) { Finish(false,"Stage "+stage+": "+error); }
    }
    private static void Pointer(float x,float z,bool down)
    {
        Vector2 screen=camera.WorldToScreenPoint(new Vector3(x,stage==5||stage==6?1.1f:0,z));
        var state=new MouseState { position=screen };
        if(down) state=state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse,state);
        Debug.Log("CONSTRUCTION_POINTER stage="+stage+" screen="+screen+" size="+Screen.width+"x"+Screen.height+" down="+down+" gesture="+tool.HasActiveGesture);
    }
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    private static void CapturePreview()
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var shell=UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();
        var canvas=shell.GetComponentInParent<Canvas>();
        var previousMode=canvas.renderMode; var previousCamera=canvas.worldCamera;
        var previousPosition=camera.transform.position;var previousRotation=camera.transform.rotation;
        var previousTarget=RenderTexture.active;
        var target=new RenderTexture(1920,1080,24);
        camera.transform.position=new Vector3(43,12,20);camera.transform.LookAt(new Vector3(33,0,32));camera.orthographicSize=7;
        camera.backgroundColor=new Color(0.09f,0.13f,0.12f);camera.clearFlags=CameraClearFlags.SolidColor;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=0.5f;
        camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
        File.WriteAllBytes("Logs/ConstructionWorkspace.png",image.EncodeToPNG());
        RenderTexture.active=previousTarget;camera.targetTexture=null;canvas.renderMode=previousMode;canvas.worldCamera=previousCamera;
        camera.transform.SetPositionAndRotation(previousPosition,previousRotation);camera.orthographicSize=12;
        UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);
    }
    private static void Finish(bool pass,string message)
    {
        EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
        if(mouse!=null) InputSystem.RemoveDevice(mouse);
        string report=(pass?"PASS ":"FAIL ")+message;
        File.WriteAllText("Logs/ConstructionMousePlayTest.txt",report);
        Debug.Log("BB_CONSTRUCTION_MOUSE_"+report);
        SessionState.SetBool(Armed+".Pass",pass); EditorApplication.isPlaying=false;
    }
}
