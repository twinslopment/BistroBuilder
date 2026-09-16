using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderPointerPlayTest
{
    private const string Key = "BB.Pointer.Test";
    private static int stage;
    private static double next;
    private static string failure;
    private static Canvas canvas;
    private static BistroBuilderInteractionSurface[] surfaces;
    static BistroBuilderPointerPlayTest() { EditorApplication.playModeStateChanged += State; }
    public static void RunBatch()
    {
        foreach(string name in Enum.GetNames(typeof(BistroBuilderPointerKind))) AssetDatabase.ImportAsset("Assets/Resources/BistroBuilder/UI/Cursors/"+name+".png",ImportAssetOptions.ForceUpdate);
        SessionState.SetBool(Key,true); SessionState.SetBool(Key+".Pass",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");EditorApplication.isPlaying=true;
    }
    private static void State(PlayModeStateChange change)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(change==PlayModeStateChange.EnteredPlayMode){stage=0;failure=null;next=EditorApplication.timeSinceStartup+4;Application.runInBackground=true;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
        if(change==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+".Pass",false)?0:1);}
    }
    private static void Log(string message,string stack,LogType type){if(type!=LogType.Exception&&type!=LogType.Assert)return;if(!string.IsNullOrEmpty(stack)&&stack.Contains("UnityEditor.Search.SearchDatabase"))return;failure=message;}
    private static void Check(bool result,string message){if(!result)throw new Exception(message);}
    private static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+0.6;
        try {
            Check(failure==null,failure);
            switch(stage++) {
                case 0:
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>().Hide();
                    Check(BistroBuilderPointerFeedback.Instance!=null,"Cursor owner installed");
                    foreach(string name in Enum.GetNames(typeof(BistroBuilderPointerKind))){var tex=Resources.Load<Texture2D>("BistroBuilder/UI/Cursors/"+name);Check(tex!=null&&tex.width==64&&tex.isReadable,"Readable cursor "+name);Check(tex.GetPixel(0,0).a==0,"Transparent cursor "+name);}
                    Check(BistroBuilderPointerFeedback.ResolvePlacementCursor(false,true)==BistroBuilderPointerKind.Blocked,"Invalid placement outranks rotation");
                    Check(BistroBuilderPointerFeedback.ResolvePlacementCursor(true,true)==BistroBuilderPointerKind.Rotate,"Rotation feedback");
                    Check(BistroBuilderPointerFeedback.ResolvePlacementCursor(true,false)==BistroBuilderPointerKind.Drag,"Placement feedback");
                    CreateGallery();break;
                case 1:
                    surfaces[1].OnPointerEnter(new PointerEventData(EventSystem.current));
                    surfaces[2].SetSelected(true);surfaces[4].GetComponent<Button>().interactable=false;
                    EventSystem.current.SetSelectedGameObject(surfaces[2].gameObject);
                    BistroBuilderPointerFeedback.MoveKeyboardFocus(false);break;
                case 2:
                    Check(BistroBuilderPointerFeedback.KeyboardFocus,"Tab keyboard modality");
                    Check(EventSystem.current.currentSelectedGameObject==surfaces[3].gameObject,"Tab advances to next control");break;
                case 3:
                    foreach(var s in surfaces)s.Refresh();
                    Check(surfaces[0].State==BistroBuilderSurfaceState.Normal,"Normal");
                    Check(surfaces[1].State==BistroBuilderSurfaceState.Hover,"Hover");
                    Check(surfaces[2].State==BistroBuilderSurfaceState.Selected&&!surfaces[2].HasKeyboardFocus,"Selection separate from focus");
                    Check(surfaces[3].HasKeyboardFocus&&surfaces[3].State==BistroBuilderSurfaceState.Normal,"Blue keyboard focus without selection");
                    Check(surfaces[4].State==BistroBuilderSurfaceState.Disabled&&!surfaces[4].HasKeyboardFocus,"Disabled");
                    Check(surfaces[4].transform.Find("Unavailable badge").gameObject.activeSelf,"Unavailable badge");
                    Capture();surfaces[4].GetComponent<Button>().interactable=true;break;
                case 4:
                    BistroBuilderPointerFeedback.MoveKeyboardFocus(true);
                    Check(EventSystem.current.currentSelectedGameObject==surfaces[2].gameObject,"Shift Tab returns to previous control");
                    surfaces[4].Refresh();Check(surfaces[4].State==BistroBuilderSurfaceState.Normal,"Disabled state restores");
                    Check(!surfaces[4].transform.Find("Unavailable badge").gameObject.activeSelf,"Badge restores");
                    surfaces[1].OnPointerExit(new PointerEventData(EventSystem.current));Check(surfaces[1].State==BistroBuilderSurfaceState.Normal,"Hover exit restores");
                    Finish(true,"5 readable cursors / placement priority / hover / selection / Tab focus / disabled grayscale and badge / restored states");break;
            }
        } catch(Exception e){Finish(false,e.ToString());}
    }
    private static GameObject Ui(string name,Transform parent,Vector2 pos,Vector2 size)
    {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=size;return go;}
    private static void Label(Transform parent,string text,Vector2 pos,Vector2 size,float font,Color color)
    {var go=Ui(text,parent,pos,size);var t=go.AddComponent<TextMeshProUGUI>();t.text=text;t.fontSize=font;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;}
    private static void CreateGallery()
    {
        var go=new GameObject("Pointer acceptance gallery",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=0.5f;
        var background=Ui("Cream",canvas.transform,Vector2.zero,new Vector2(1600,900)).AddComponent<Image>();background.color=new Color(0.96f,0.94f,0.88f);background.raycastTarget=false;
        var ink=new Color(0.20f,0.25f,0.18f);
        Label(canvas.transform,"BISTRO BUILDER  /  FOCUS Y CURSORES",new Vector2(60,-40),new Vector2(1480,70),36,ink);
        string[] names={"Normal","Hover","Seleccionado","Focus teclado","Bloqueado"};surfaces=new BistroBuilderInteractionSurface[5];
        for(int i=0;i<5;i++) {
            float x=75+i*294;
            Label(canvas.transform,names[i],new Vector2(x,-153),new Vector2(260,36),24,ink);
            var card=Ui("Card"+i,canvas.transform,new Vector2(x,-205),new Vector2(260,250));card.AddComponent<Image>().color=new Color(1f,0.99f,0.96f);var button=card.AddComponent<Button>();button.transition=Selectable.Transition.None;
            surfaces[i]=BistroBuilderInteractionSurface.Attach(button);surfaces[i].IsCard=true;
            var icon=Ui("Olive",card.transform,new Vector2(78,-20),new Vector2(104,104)).AddComponent<Image>();var texture=Resources.Load<Texture2D>("BistroBuilder/UI/Cursors/Normal");icon.sprite=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(0.5f,0.5f));icon.raycastTarget=false;
            Label(card.transform,"Bistro · Selección",new Vector2(6,-148),new Vector2(248,35),22,ink);
            Label(card.transform,"13 €",new Vector2(6,-184),new Vector2(248,28),21,ink);
        }
        string[] cursorNames={"Normal","Hover","Blocked","Drag","Rotate"};string[] captions={"Normal","Hover","Bloqueado","Arrastre","Rotar"};
        for(int i=0;i<5;i++) {
            float x=145+i*294;var raw=Ui("Cursor"+i,canvas.transform,new Vector2(x,-575),new Vector2(96,96)).AddComponent<RawImage>();raw.texture=Resources.Load<Texture2D>("BistroBuilder/UI/Cursors/"+cursorNames[i]);raw.raycastTarget=false;
            Label(canvas.transform,captions[i],new Vector2(x-76,-698),new Vector2(248,40),25,ink);
        }
        Label(canvas.transform,"Hoja de olivo · Borde dorado al pasar · Selección verde · Foco azul con Tab",new Vector2(75,-812),new Vector2(1450,40),23,ink);
    }
    private static void Capture()
    {
        var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();var target=new RenderTexture(1600,900,24);var old=camera.targetTexture;var active=RenderTexture.active;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+2f;camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();Directory.CreateDirectory("docs/Images");File.WriteAllBytes("docs/Images/CursoresYFocus.png",image.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=old;canvas.renderMode=RenderMode.ScreenSpaceOverlay;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);
    }
    private static void Finish(bool pass,string message)
    {EditorApplication.update-=Tick;Application.logMessageReceived-=Log;File.WriteAllText("Logs/PointerFeedbackTest.txt",(pass?"PASS ":"FAIL ")+message);Debug.Log("BB_POINTER_"+(pass?"PASS ":"FAIL ")+message);SessionState.SetBool(Key+".Pass",pass);EditorApplication.isPlaying=false;}
}
