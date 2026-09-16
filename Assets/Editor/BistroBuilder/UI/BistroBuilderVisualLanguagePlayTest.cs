using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderVisualLanguagePlayTest
{
    const string Key="BB.VisualLanguage.Test";
    static int stage;static double next;static string failure;static BistroBuilderUiShell shell;static Canvas gallery;
    static BistroBuilderVisualLanguagePlayTest(){EditorApplication.playModeStateChanged+=State;}
    public static void RunBatch()
    {
        BistroBuilderTypographyInstaller.Prepare();SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Pass",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");EditorApplication.isPlaying=true;
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){stage=0;failure=null;next=EditorApplication.timeSinceStartup+4;Application.runInBackground=true;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+".Pass",false)?0:1);}
    }
    static void Log(string text,string stack,LogType type){if(type==LogType.Exception||type==LogType.Assert)failure=text;}
    static void Check(bool value,string text){if(!value)throw new Exception(text);}
    static Button Button(string name)=>GameObject.Find(name).GetComponent<Button>();
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+0.8;
        try
        {
            Check(failure==null,failure);
            switch(stage++)
            {
                case 0:
                    UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>().Hide();
                    shell=UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();shell.EnsureShell();
                    Check(BistroBuilderTypography.HasRecoleta,"Recoleta source installed");
                    Check(BistroBuilderTypography.Title.faceInfo.familyName.Contains("Recoleta"),"Real Recoleta title family");
                    Check(BistroBuilderTypography.Body.faceInfo.familyName.Contains("Inter"),"Real Inter body family");
                    Check(Resources.Load<Shader>("BistroBuilder/UI/HudGlass").isSupported,"HUD blur shader supported");
                    Check(BistroBuilderSurface.Opacity(BistroBuilderHudGlass.Light)==0.7f&&BistroBuilderSurface.Blur(BistroBuilderHudGlass.Strong)==24,"HUD preset tokens");
                    Check(GameObject.Find(BistroBuilderUiShell.TopBarName).GetComponent<BistroBuilderSurface>().Hud,"Top bar HUD translucency");
                    Check(GameObject.Find("Wordmark").GetComponent<TMP_Text>().font.faceInfo.familyName.Contains("Recoleta"),"Header wordmark uses Recoleta");
                    foreach (var depth in UnityEngine.Object.FindObjectsByType<BistroBuilderDepthGraphic>(FindObjectsSortMode.None)) Check(depth.canvasRenderer != null,"Border/shadow renderer exists");
                    Check(!BistroBuilderTypography.Title.characterLookupTable.ContainsKey('ó'),"Demo accented glyph uses fallback instead of publisher mark");
                    break;
                case 1:
                    Capture(shell.GetComponentInParent<Canvas>(),"UIProfundidadJuego.png");Button("BBNav_Personal").onClick.Invoke();break;
                case 2:
                    Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffPlayerScreen>().IsVisible,"Staff opens with new presentation");
                    Button("CandidatesTab").onClick.Invoke();break;
                case 3:
                    Capture(shell.GetComponentInParent<Canvas>(),"UITipografiaPersonal.png");Button("BBNav_Actividad").onClick.Invoke();
                    Button("BBNav_Opciones").onClick.Invoke();break;
                case 4:
                    var menu=GameObject.Find("TopNavigationMenu");Check(menu.GetComponent<BistroBuilderSurface>().Level==BistroBuilderSurfaceLevel.Floating,"Floating menu elevation");
                    Check(!menu.GetComponent<BistroBuilderSurface>().Hud,"Menu stays solid");
                    Button("BBNav_Opciones").onClick.Invoke();CreateGallery();break;
                case 5:
                    Capture(gallery,"TipografiaYProfundidad.png");
                    Finish(true,"Recoleta / Inter / H1 H2 H3 body label caption KPI / semantic badges / four surfaces / six borders / shadows / three HUD glass presets / real staff navigation");break;
            }
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static TMP_Text Text(Transform parent,string text,float x,float y,float w,float h,BistroBuilderUiStyleRole role,bool dark=false)
    {var r=Rect(text,parent,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.text=text;t.color=dark?BistroBuilderUiTokens.TextOnLight:BistroBuilderUiTokens.TextPrimary;t.raycastTarget=false;BistroBuilderTypography.Apply(t,role);return t;}
    static RectTransform Panel(Transform parent,string name,float x,float y,float w,float h,BistroBuilderSurfaceLevel level,bool glass=false)
    {
        var r=Rect(name,parent,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=level==BistroBuilderSurfaceLevel.Base?BistroBuilderUiTokens.Background:level==BistroBuilderSurfaceLevel.Panel?BistroBuilderUiTokens.Surface1:level==BistroBuilderSurfaceLevel.Card?BistroBuilderUiTokens.Surface2:BistroBuilderUiTokens.SurfaceElevated;
        BistroBuilderSurface.Apply(image,level,glass);return r;
    }
    static void CreateGallery()
    {
        var go=new GameObject("Visual language acceptance gallery",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));gallery=go.GetComponent<Canvas>();gallery.renderMode=RenderMode.ScreenSpaceOverlay;gallery.sortingOrder=31000;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=0.5f;
        var bg=Rect("Background",gallery.transform,0,0,1600,1000).gameObject.AddComponent<Image>();bg.color=BistroBuilderUiTokens.Background;
        Text(gallery.transform,"Bistro Builder · Tipografía y profundidad",44,30,1510,64,BistroBuilderUiStyleRole.Title);
        Text(gallery.transform,"Recoleta para títulos · Inter para la interfaz · Superficies cálidas y estados legibles",44,99,1510,32,BistroBuilderUiStyleRole.Body);
        var typography=Panel(gallery.transform,"Typography",40,159,690,403,BistroBuilderSurfaceLevel.Panel);
        Text(typography,"Tu restaurante, a tu manera",25,20,640,55,BistroBuilderUiStyleRole.Title);
        Text(typography,"Gestión del personal",25,92,640,44,BistroBuilderUiStyleRole.Heading);
        Text(typography,"Inventario de ingredientes",25,153,640,30,BistroBuilderUiStyleRole.Subheading);
        Text(typography,"Controla reservas, inventario y finanzas desde un solo lugar.\nDiseñado para adaptarse a cualquier restaurante.",25,203,640,57,BistroBuilderUiStyleRole.Body);
        Text(typography,"Nombre del ingrediente",25,281,310,26,BistroBuilderUiStyleRole.Label);
        Text(typography,"Última actualización: hace 2 horas",25,322,340,24,BistroBuilderUiStyleRole.Caption);
        Text(typography,"€ 12.480",435,283,200,46,BistroBuilderUiStyleRole.Kpi);
        var statuses=Panel(gallery.transform,"Semantic states",755,159,805,403,BistroBuilderSurfaceLevel.Panel);
        Text(statuses,"Estados y jerarquía visual",25,18,755,46,BistroBuilderUiStyleRole.Heading);
        string[] descriptions={"Operación completada. Todo en orden.","Requiere revisión o una acción.","Hay un problema que necesita atención.","Información relevante para el usuario.","Elemento no disponible temporalmente."};
        for(int i=0;i<5;i++)
        {var badge=Rect("Badge",statuses,25,86+i*57,190,35);var image=badge.gameObject.AddComponent<Image>();var t=Text(badge,"",4,3,182,29,BistroBuilderUiStyleRole.Label,true);BistroBuilderStatusBadge.Apply(image,t,(BistroBuilderSemanticState)i);Text(statuses,descriptions[i],237,91+i*57,535,35,BistroBuilderUiStyleRole.Body);}
        Text(gallery.transform,"Capas de superficie y sombras",43,596,950,40,BistroBuilderUiStyleRole.Heading);
        string[] layers={"Fondo base","Superficie 1 · Panel","Superficie 2 · Tarjeta","Superficie 3 · Flotante"};
        string[] shadows={"Sin sombra","Y 2 · Blur 8 · 8%","Y 4 · Blur 16 · 10%","Y 8 · Blur 24 · 12%"};
        for(int i=0;i<4;i++){var p=Panel(gallery.transform,layers[i],42+i*387,663,354,111,(BistroBuilderSurfaceLevel)i);Text(p,layers[i],19,17,315,32,BistroBuilderUiStyleRole.Subheading,i==3);Text(p,shadows[i],19,61,315,28,BistroBuilderUiStyleRole.Caption,i==3);}
        Text(gallery.transform,"Bordes de 1 px · El color comunica el estado",43,807,950,35,BistroBuilderUiStyleRole.Heading);
        string[] borders={"Normal","Hover","Seleccionado","Atención","Crítico","Desactivado"};
        for(int i=0;i<6;i++){var p=Panel(gallery.transform,borders[i],43+i*255,868,235,73,BistroBuilderSurfaceLevel.Panel);p.GetComponent<BistroBuilderSurface>().SetBorder((BistroBuilderBorderState)i);Text(p,borders[i],15,23,205,28,BistroBuilderUiStyleRole.Label);}
    }
    static void Capture(Canvas canvas,string filename)
    {
        var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();var target=new RenderTexture(1600,1000,24);var old=camera.targetTexture;var active=RenderTexture.active;var mode=canvas.renderMode;var priorCamera=canvas.worldCamera;
        var roots=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var saved=new System.Collections.Generic.List<(Canvas canvas,RenderMode mode,Camera camera,float distance)>();
        foreach(var root in roots){if(!root.isRootCanvas||root.renderMode==RenderMode.WorldSpace)continue;saved.Add((root,root.renderMode,root.worldCamera,root.planeDistance));root.renderMode=RenderMode.ScreenSpaceCamera;root.worldCamera=camera;root.planeDistance=camera.nearClipPlane+2;}
        camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);image.Apply();Directory.CreateDirectory("docs/Images");File.WriteAllBytes("docs/Images/"+filename,image.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=old;
        foreach(var restore in saved){restore.canvas.renderMode=restore.mode;restore.canvas.worldCamera=restore.camera;restore.canvas.planeDistance=restore.distance;}
        UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);
    }
    static void Finish(bool pass,string message)
    {EditorApplication.update-=Tick;Application.logMessageReceived-=Log;File.WriteAllText("Logs/VisualLanguageTest.txt",(pass?"PASS ":"FAIL ")+message);Debug.Log("BB_VISUAL_LANGUAGE_"+(pass?"PASS ":"FAIL ")+message);SessionState.SetBool(Key+".Pass",pass);EditorApplication.isPlaying=false;}
}
