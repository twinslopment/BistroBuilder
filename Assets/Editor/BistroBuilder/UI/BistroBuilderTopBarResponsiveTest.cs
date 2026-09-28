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
    static double next, timeout;
    static int stage;
    static BistroBuilderUiShell shell;
    static RectTransform bar;
    static BistroBuilderApprovedTopBarHotspot hover;
    static Vector3 restPosition;
    static RectTransform moving;
    static BistroBuilderTopBarResponsiveTest() { EditorApplication.playModeStateChanged+=state=>{
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){stage=0;next=EditorApplication.timeSinceStartup+4;timeout=next+90;EditorApplication.update+=Tick;}
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
                    Check(GameObject.Find("OptionsPanel")!=null,"Opciones opens settings");Button("Opciones").onClick.Invoke();Finish(true,"BB_TOPBAR_RESPONSIVE_PASS");break;
            }
        }catch(Exception e){Finish(false,e.ToString());}
    }
    static void Capture(int width,int height)
    {
        var canvas=shell.GetComponentInParent<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();
        var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldScale=canvas.scaleFactor;bool scaling=scaler.enabled;
        var sceneCamera=Camera.main;var cameraGo=new GameObject("TopBarCapture",typeof(Camera));var camera=cameraGo.GetComponent<Camera>();
        if(sceneCamera!=null){camera.CopyFrom(sceneCamera);camera.transform.SetPositionAndRotation(sceneCamera.transform.position,sceneCamera.transform.rotation);}
        var transforms=canvas.GetComponentsInChildren<Transform>(true);int[] layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        var rt=new RenderTexture(width,height,24);var previous=RenderTexture.active;Texture2D image=null;
        try{
            foreach(var t in transforms)t.gameObject.layer=31;
            camera.targetTexture=rt;camera.cullingMask=-1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.10f,.11f);
            scaler.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;canvas.scaleFactor=Mathf.Sqrt((width/1920f)*(height/1080f));
            Canvas.ForceUpdateCanvases();Layout();Canvas.ForceUpdateCanvases();camera.Render();
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
            RenderTexture.active=rt;image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();Check(image.GetPixel(width/2,height-15).r>.65f,"Rendered ivory pixels present "+width);File.WriteAllBytes("Logs/TopBarResponsive/bar-"+width+".png",image.EncodeToPNG());
        }finally{
            RenderTexture.active=previous;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.scaleFactor=oldScale;scaler.enabled=scaling;
            for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];
            UnityEngine.Object.DestroyImmediate(cameraGo);rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);Canvas.ForceUpdateCanvases();Layout();
        }
    }
    static void Layout()=>typeof(BistroBuilderUiShell).GetMethod("LayoutApprovedTopBar",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,new object[]{true});
    static void Finish(bool pass,string message){EditorApplication.update-=Tick;File.AppendAllText("Logs/TopBarResponsive/result.txt",message+"\n");Debug.Log(message);SessionState.SetBool(Key+".Pass",pass);EditorApplication.ExitPlaymode();}
}
