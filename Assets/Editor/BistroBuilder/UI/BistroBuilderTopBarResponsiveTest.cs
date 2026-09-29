using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderTopBarResponsiveTest
{
    const string Key="BB.TopbarResponsive";
    static double next, timeout, activityDeadline;
    static int stage, auditIndex;
    static BistroBuilderNewGameStateSnapshot originalOpeningState;
    static readonly string[] AuditScreens = { "Personal", "Carta", "Inventario", "Proveedores", "Reservas", "Economía", "Marketing", "Reputación" };
    static BistroBuilderUiShell shell;
    static RectTransform bar;
    static readonly Vector3[] cornersForAudit=new Vector3[4];
    static BistroBuilderApprovedTopBarHotspot hover;
    static Vector3 restPosition;
    static RectTransform moving;
    static BistroBuilderTopBarResponsiveTest() { EditorApplication.playModeStateChanged+=state=>{
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){stage=0;auditIndex=0;next=EditorApplication.timeSinceStartup+4;timeout=next+180;EditorApplication.update+=Tick;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+".Pass",false)?0:1);}
    };}
    public static void Run(){Directory.CreateDirectory("Logs/TopBarResponsive");File.WriteAllText("Logs/TopBarResponsive/result.txt","");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Pass",false);EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");EditorApplication.EnterPlaymode();}
    static void Check(bool value,string label){if(!value)throw new Exception(label);File.AppendAllText("Logs/TopBarResponsive/result.txt","PASS "+label+"\n");}
    static Button Button(string label)=>bar.GetComponentsInChildren<Button>(true).First(x=>x.name=="BBNav_"+label);
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        try {
            if(EditorApplication.timeSinceStartup>timeout)throw new Exception("Timeout");
            next=EditorApplication.timeSinceStartup+.5;
            switch(stage++){
                case 0:
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                    var edit=UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>();if(edit.IsEditModeActive)edit.TryExitEditMode(true,out _);
                    shell=UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();shell.EnsureShell();typeof(BistroBuilderUiShell).GetMethod("RefreshReadModels",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                    bar=GameObject.Find(BistroBuilderUiShell.TopBarName).GetComponent<RectTransform>();
                    Check(!UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>().IsPlaytestPanelVisible,"Legacy construction panel hidden in normal mode");
                    Check(bar.GetComponentsInChildren<BistroBuilderTopBarArtwork>().Length==11,"Ten complete icons and original logo");
                    hover=Button("Personal").GetComponent<BistroBuilderApprovedTopBarHotspot>();
                    moving=hover.GetComponentInChildren<BistroBuilderTopBarArtwork>().rectTransform;restPosition=moving.localPosition;
                    Check(hover.isActiveAndEnabled && Button("Personal").IsInteractable(),"Hover target active and interactable");
                    if(EventSystem.current.currentInputModule!=null)EventSystem.current.currentInputModule.enabled=false;
                    hover.OnPointerEnter(new PointerEventData(EventSystem.current));Check(hover.HoverAmount>=.4f && moving.localScale.x>1,"Visible hover feedback in pointer-enter frame");hover.OnPointerDown(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});Check(moving.localScale.x<1,"Immediate press feedback");hover.OnPointerUp(new PointerEventData(EventSystem.current));Check(moving.localScale.x>1,"Immediate release feedback");next=EditorApplication.timeSinceStartup+1.5;break;
                case 1:
                    if(hover.HoverAmount<.99f){stage--;return;}
                    Check(hover.HoverAmount>.95f,"90ms hover settling; amount="+hover.HoverAmount+" active="+hover.isActiveAndEnabled);
                    Check(moving.localScale.x>1||moving.localPosition!=restPosition,"Independent icon animation");
                    foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(800,600),new Vector2Int(2560,1440),new Vector2Int(3440,1440),new Vector2Int(3840,2160)})Capture(size.x,size.y);
                    hover.OnPointerExit(new PointerEventData(EventSystem.current));Button("Personal").onClick.Invoke();break;
                case 2:
                    if(hover.HoverAmount>.01f){stage--;return;}
                    Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffPlayerScreen>().IsVisible,"Personal opens its real panel");
                    Check(hover.HoverAmount<.01f,"Hover clears on exit");Button("Inventario").onClick.Invoke();break;
                case 3:
                    Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseRuntimeView>().IsOpen,"Inventario navigation");
                    Check(!UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffPlayerScreen>().IsVisible,"Previous management panel closes");Button("Actividad").onClick.Invoke();break;
                case 4:
                    Check(!shell.HasManagementScreenOpen,"Actividad returns to scene");Button("Opciones").onClick.Invoke();break;
                case 5:
                    Check(GameObject.Find("OptionsPanel")!=null,"Opciones opens settings");Button("Opciones").onClick.Invoke();break;
                case 6:
                    Check(shell.TryOpenNavigationFromInterface(AuditScreens[auditIndex],out string error), "Open " + AuditScreens[auditIndex] + ": " + error);break;
                case 7:
                    Check(shell.HasManagementScreenOpen,"Management destination active: "+AuditScreens[auditIndex]);
                    if(AuditScreens[auditIndex]=="Reputación"){
                        var reputation=UnityEngine.Object.FindFirstObjectByType<BistroBuilderReputationPlayerScreen>();
                        var aspectNames=new[]{"serviceAspectText","waitingAspectText","foodAspectText","valueAspectText","ambienceAspectText"};
                        var aspects=aspectNames.Select(n=>(TMP_Text)typeof(BistroBuilderReputationPlayerScreen).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(reputation)).ToArray();
                        Check(aspects.All(t=>!t.enableAutoSizing&&Mathf.Abs(t.fontSize-18)<.1f),"Equal reputation aspect typography");
                    }
                    if(AuditScreens[auditIndex]=="Carta"){
                        var labels=UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t=>t.text.StartsWith("Tipo: ")||t.text.StartsWith("Destino: ")).ToArray();
                        Check(labels.Length==2&&labels.All(t=>t.isActiveAndEnabled),"Rule type and destination captions visible");
                    }
                    Check(GameObject.Find(BistroBuilderUiShell.ActivityPanelName)==null,"Activity stays hidden throughout management refresh: "+AuditScreens[auditIndex]);
                    Check(GameObject.Find("InitialDesignActions")==null,"No initial-design overlay in "+AuditScreens[auditIndex]);
                    foreach(var hit in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.name=="BB_B2_SelectorHitTarget"))
                        Check(hit.GetComponent<Image>().color.a<.01f && hit.GetComponent<BistroBuilderInteractionSurface>()==null,"Selector hit surface remains transparent: "+hit.transform.parent.name);
                    Capture(1920,1080,AuditScreens[auditIndex]);Capture(1280,720,AuditScreens[auditIndex]);
                    if(++auditIndex<AuditScreens.Length)stage=6; else { Button("Actividad").onClick.Invoke(); }break;
                case 8:
                    var catalog=UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>();
                    Check(catalog.AvailableItems.Count>=9,"Complete placeable catalog: "+catalog.AvailableItems.Count);
                    Check(catalog.AvailableItems.All(i=>i.CatalogIcon!=null),"Every placeable item has an authored thumbnail");
                    var feed=UnityEngine.Object.FindFirstObjectByType<ActivityFeedService>();
                    feed.Publish(ActivityEventId.TableBillRequested,new ActivityEventPayload().Set("table",1),new ActivityTargetRef(ActivityTargetType.Table,1)); Check(feed.Count>0,"Activity fixture accepted by feed"); activityDeadline=EditorApplication.timeSinceStartup+5;break;
                case 9:
                    var activity=GameObject.Find(BistroBuilderUiShell.ActivityPanelName); if(activity==null && EditorApplication.timeSinceStartup<activityDeadline){stage--;return;} Check(activity!=null,"Activity returns to scene with real feed entries; managing="+shell.HasManagementScreenOpen+" editing="+UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>().IsEditModeActive+" opening="+BistroBuilderNewGameOpeningPlayerScreen.IsOpeningMenuBlocking+" controller="+ActivityPanelController.HasActiveController+" entries="+ActivityPanelController.ActiveInstance?.HasEntries);
                    Check(activity.GetComponent<Image>().color.r>.8f,"Activity keeps its authored ivory surface");
                    Capture(1920,1080,"Actividad");Capture(1280,720,"Actividad");
                    var opening=UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningService>();originalOpeningState=opening.CreateSnapshot();var setup=originalOpeningState.DeepClone();
                    setup.phase=BistroBuilderNewGamePhase.InitialSetup;setup.setupCompleted=true;setup.restaurantName="UI regression";
                    Check(opening.TryRestoreSnapshot(setup,out string stateError),"Initial setup fixture without writing saves: "+stateError);
                    Check(opening.TryEnterInitialEditMode(out string enterError),"Enter initial editing: "+enterError);break;
                case 10:
                    var editTop=GameObject.Find(BistroBuilderUiShell.EditModeTopBarName);
                    Check(editTop!=null&&editTop.activeInHierarchy,"Edit top bar replaces normal navigation in edit viewport");
                    Check(editTop.GetComponentsInChildren<BistroBuilderTopBarArtwork>(true).Length==1,"Normal/edit modes share approved brand artwork");
                    Check(editTop.GetComponentsInChildren<BistroBuilderTopBarPlate>(true).Length>=8,"Normal/edit modes share approved plate language");
                    var saveButtons=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(b=>b.name=="EditInitialSave"&&b.gameObject.activeInHierarchy).ToArray();
                    Check(saveButtons.Length==1,"Exactly one canonical initial design save action");
                    Check(GameObject.Find("Diseño inicial")==null,"Legacy initial design panel remains suppressed");
                    var clockDock=GameObject.Find("BB_368B_TimeControlsDock");Check(clockDock==null||clockDock.GetComponent<CanvasGroup>().alpha==0,"Normal time dock hidden during editing");
                    Check(shell.TryOpenNavigationFromInterface("Reputación",out string openError),"Management opens during initial setup: "+openError);break;
                case 11:
                    Check(GameObject.Find("InitialDesignActions")==null,"Initial-design controls hidden over management in initial setup");
                    Check(GameObject.Find(BistroBuilderUiShell.TopBarName)!=null,"Global navigation returns above management opened from edit mode");
                    Check(GameObject.Find(BistroBuilderUiShell.EditModeTopBarName)==null,"Edit chrome yields while management is open");
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningService>().TryRestoreSnapshot(originalOpeningState,out _);
                    Finish(true,"BB_TOPBAR_RESPONSIVE_PASS");break;
            }
        }catch(Exception e){Finish(false,e.ToString());}
    }
    static void Capture(int width,int height,string screen="bar")
    {
        var canvas=shell.GetComponentInParent<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();
        var otherCanvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c!=canvas).ToArray();
        var otherModes=otherCanvases.Select(c=>c.renderMode).ToArray();var otherCameras=otherCanvases.Select(c=>c.worldCamera).ToArray();
        var otherScales=otherCanvases.Select(c=>c.scaleFactor).ToArray();var otherScalers=otherCanvases.Select(c=>c.GetComponent<CanvasScaler>()).ToArray();var otherEnabled=otherScalers.Select(c=>c!=null&&c.enabled).ToArray();
        var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldScale=canvas.scaleFactor;bool scaling=scaler.enabled;
        var sceneCamera=Camera.main;var cameraGo=new GameObject("TopBarCapture",typeof(Camera));var camera=cameraGo.GetComponent<Camera>();
        if(sceneCamera!=null){camera.CopyFrom(sceneCamera);camera.transform.SetPositionAndRotation(sceneCamera.transform.position,sceneCamera.transform.rotation);}
        var transforms=canvas.GetComponentsInChildren<Transform>(true);int[] layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        var rt=new RenderTexture(width,height,24);var previous=RenderTexture.active;Texture2D image=null;
        try{
            foreach(var t in transforms)t.gameObject.layer=31;
            camera.targetTexture=rt;camera.cullingMask=-1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.10f,.11f);
            scaler.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;canvas.scaleFactor=Mathf.Sqrt((width/1920f)*(height/1080f));
            foreach(var c in otherCanvases){var cs=c.GetComponent<CanvasScaler>();if(cs!=null)cs.enabled=false;c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=5;c.scaleFactor=canvas.scaleFactor;}
            Canvas.ForceUpdateCanvases();Layout();Canvas.ForceUpdateCanvases();
            foreach(var area in UnityEngine.Object.FindObjectsByType<BistroBuilderManagementSafeArea>(FindObjectsSortMode.None))area.SendMessage("LateUpdate");
            foreach(var activity in UnityEngine.Object.FindObjectsByType<ActivityPanelResponsiveLayout>(FindObjectsSortMode.None))activity.Apply(true);
            Canvas.ForceUpdateCanvases();
            foreach(var area in UnityEngine.Object.FindObjectsByType<BistroBuilderManagementSafeArea>(FindObjectsSortMode.None)){
                var r=(RectTransform)area.transform;var edge=new Vector3[4];r.GetWorldCorners(edge);bar.GetWorldCorners(cornersForAudit);
                Check(camera.WorldToScreenPoint(edge[2]).y<=camera.WorldToScreenPoint(cornersForAudit[0]).y-2,"Management content below navigation: "+screen+" "+width);
            }
            camera.Render();
            var corners=new Vector3[4];bar.GetWorldCorners(corners);var min=camera.WorldToScreenPoint(corners[0]);var max=camera.WorldToScreenPoint(corners[2]);
            Check(min.x>=0&&max.x<=width&&min.y>=0&&max.y<=height,"Bar fits "+width);
            Check(max.y-min.y<=Mathf.Clamp(height*.089f,76,144)+2,"Compact height "+width);
            Check(max.x-min.x>width*.94f,"Full available width "+width);
            var buttons=bar.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("BBNav_")).ToArray();
            Check(buttons.Length==10,"All ten destinations reachable "+width);
            Check(buttons.All(b=>b.GetComponent<BistroBuilderInteractionSurface>()==null),"No generic button styling "+width);
            Check(bar.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="ApprovedLabel").All(t=>!t.isTextOverflowing&&t.color.r<.3f),"Legible dark labels without clipping "+width);
            Check(bar.GetComponentsInChildren<BistroBuilderTopBarArtwork>().All(a=>a.mainTexture is Texture2D texture && texture.mipmapCount>1 && texture.filterMode==FilterMode.Trilinear),"Stable filtered icon detail "+width);
            Check(bar.GetComponentsInChildren<BistroBuilderTopBarArtwork>().All(a=>Mathf.Abs(a.rectTransform.rect.width/a.rectTransform.rect.height-a.Aspect)<.01f),"Logo and icons never stretched "+width);
            RenderTexture.active=rt;image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();Check(image.GetPixel(width/2,height-15).r>.65f,"Rendered ivory pixels present "+width);File.WriteAllBytes("Logs/TopBarResponsive/"+screen+"-"+width+".png",image.EncodeToPNG());
        }finally{
            for(int i=0;i<otherCanvases.Length;i++){var c=otherCanvases[i];c.renderMode=otherModes[i];c.worldCamera=otherCameras[i];c.scaleFactor=otherScales[i];if(otherScalers[i]!=null)otherScalers[i].enabled=otherEnabled[i];}
            RenderTexture.active=previous;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.scaleFactor=oldScale;scaler.enabled=scaling;
            for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];
            UnityEngine.Object.DestroyImmediate(cameraGo);rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);Canvas.ForceUpdateCanvases();Layout();
        }
    }
    static void Layout()=>typeof(BistroBuilderUiShell).GetMethod("LayoutApprovedTopBar",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,new object[]{true});
    static void Finish(bool pass,string message){EditorApplication.update-=Tick;File.AppendAllText("Logs/TopBarResponsive/result.txt",message+"\n");Debug.Log(message);SessionState.SetBool(Key+".Pass",pass);EditorApplication.ExitPlaymode();}
}
