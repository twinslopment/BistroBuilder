using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderNewGameOpeningPlayerScreen
{
    Canvas menuCanvas;
    CanvasScaler menuScaler;
    TMP_InputField menuName;
    TMP_Text menuDescription, menuStatus;
    Button menuContinue;
    readonly Button[] premiseButtons=new Button[4];
    readonly BistroBuilderPremisesIcon[] premiseIcons=new BistroBuilderPremisesIcon[4];
    readonly TMP_Text[] premiseLabels=new TMP_Text[4];
    static readonly BistroBuilderStartingPremisesProfile[] Profiles={BistroBuilderStartingPremisesProfile.Empty,BistroBuilderStartingPremisesProfile.Compact,BistroBuilderStartingPremisesProfile.Balanced,BistroBuilderStartingPremisesProfile.Spacious};
    static readonly Color MenuPaper=new Color32(250,248,244,255),MenuInk=new Color32(36,40,32,255),MenuMuted=new Color32(102,106,97,255),MenuOlive=new Color32(96,123,63,255),MenuField=new Color32(237,234,226,255);
    static Sprite menuRound;

    void LateUpdate()
    {
        bool show=Application.isPlaying&&IsVisible&&openingService!=null&&openingService.Phase==BistroBuilderNewGamePhase.StartMenu;
        if(show){EnsureNewGameMenu();RestoreModalInputState();RefreshNewGameMenu();}
        if(menuCanvas!=null)menuCanvas.gameObject.SetActive(show);
    }

    void EnsureNewGameMenu()
    {
        if(menuCanvas!=null)return;
        var root=new GameObject("BB_NewGameMenu",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(BistroBuilderEditChromeSurface));
        root.transform.SetParent(transform,false);menuCanvas=root.GetComponent<Canvas>();menuCanvas.renderMode=RenderMode.ScreenSpaceOverlay;menuCanvas.sortingOrder=25000;
        menuScaler=root.GetComponent<CanvasScaler>();menuScaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
        var veil=MenuRect(root.transform,"Backdrop",0,0,0,0);veil.anchorMin=Vector2.zero;veil.anchorMax=Vector2.one;veil.offsetMin=veil.offsetMax=Vector2.zero;
        veil.gameObject.AddComponent<Image>().color=new Color(.12f,.15f,.10f,1f);
        var panel=MenuRect(root.transform,"NewGameCard",0,0,640,544);panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
        MenuSurface(panel,MenuPaper);var shadow=panel.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.18f);shadow.effectDistance=new Vector2(0,-6);
        MenuText(panel,"Brand","BistroBuilder",22,32,23,576,32,true).color=MenuOlive;
        MenuText(panel,"Title","Nueva partida",32,32,65,576,42,true);
        MenuText(panel,"Subtitle","Dale nombre a tu restaurante y elige cómo empezar.",15,32,112,576,30).color=MenuMuted;
        MenuText(panel,"NameLabel","Nombre del restaurante",14,32,155,576,24);
        var field=MenuRect(panel,"RestaurantName",32,184,576,44);MenuSurface(field,MenuField);
        menuName=field.gameObject.AddComponent<TMP_InputField>();menuName.targetGraphic=field.GetComponent<Image>();menuName.characterLimit=80;
        var viewport=MenuRect(field,"Viewport",13,4,550,36);viewport.gameObject.AddComponent<RectMask2D>();
        var nameText=MenuText(viewport,"Value","",16,0,0,550,36);var placeholder=MenuText(viewport,"Placeholder","Ej. La Esquina",16,0,0,550,36);placeholder.color=MenuMuted;
        menuName.textViewport=viewport;menuName.textComponent=nameText;menuName.placeholder=placeholder;menuName.SetTextWithoutNotify(restaurantName);
        menuName.onValueChanged.AddListener(value=>restaurantName=value);
        MenuText(panel,"ProfileLabel","Tu punto de partida",14,32,244,576,24);
        for(int i=0;i<Profiles.Length;i++)
        {
            int index=i;var button=MenuButton(panel,"Premises_"+Profiles[i],"",32+i*147,276,135,102,()=>{premises=Profiles[index];RefreshNewGameMenu();});premiseButtons[i]=button;
            var icon=MenuRect(button.transform,"PlanIcon",47,12,40,40).gameObject.AddComponent<BistroBuilderPremisesIcon>();icon.Profile=Profiles[i];icon.raycastTarget=false;premiseIcons[i]=icon;
            premiseLabels[i]=MenuText(button.transform,"ProfileName",PremisesLabel(Profiles[i]),15,2,65,131,26);premiseLabels[i].alignment=TextAlignmentOptions.Center;
        }
        menuDescription=MenuText(panel,"Description","",14,32,393,576,47);menuDescription.textWrappingMode=TextWrappingModes.Normal;menuDescription.color=MenuMuted;
        menuStatus=MenuText(panel,"Status","",13,32,442,576,27);menuStatus.textWrappingMode=TextWrappingModes.Normal;menuStatus.color=new Color32(158,65,45,255);
        MenuButton(panel,"CreateNewGame","Crear restaurante",32,480,352,42,()=>
        {
            if(openingService.TryCreateNewGame(restaurantName,premises,out statusMessage)){statusMessage=string.Empty;Hide();}
            else RefreshNewGameMenu();
        },true);
        menuContinue=MenuButton(panel,"ContinueGame","Continuar partida",398,480,210,42,()=>{if(openingService.TryContinue(out statusMessage))statusMessage="Cargando partida…";RefreshNewGameMenu();});
        RefreshNewGameMenu();
    }
    void RefreshNewGameMenu()
    {
        if(menuCanvas==null)return;
        menuScaler.scaleFactor=Mathf.Clamp(Mathf.Min(Screen.width/1280f,Screen.height/720f),.65f,1.15f);
        for(int i=0;i<Profiles.Length;i++)
        {
            bool selected=premises==Profiles[i];premiseButtons[i].targetGraphic.color=selected?MenuOlive:MenuField;
            premiseIcons[i].color=premiseLabels[i].color=selected?Color.white:MenuOlive;
        }
        menuDescription.text=premises switch {
            BistroBuilderStartingPremisesProfile.Empty=>"Un lienzo en blanco: distribuye los espacios y coloca cada pieza a tu manera.",
            BistroBuilderStartingPremisesProfile.Compact=>"Un local compacto con distribución inicial. Un comienzo sencillo que puedes adaptar.",
            BistroBuilderStartingPremisesProfile.Spacious=>"Más espacio para desarrollar tu idea, con una distribución inicial editable.",
            _=>"Una base equilibrada para empezar: adapta los espacios y el equipamiento a tu estilo."};
        menuStatus.text=statusMessage;menuContinue.interactable=openingService.CanContinue;
    }
    static RectTransform MenuRect(Transform parent,string name,float x,float y,float width,float height)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
    }
    static Image MenuSurface(RectTransform rect,Color color)
    {
        var image=rect.gameObject.AddComponent<Image>();image.sprite=MenuRoundSprite();image.type=Image.Type.Sliced;image.color=color;return image;
    }
    static TMP_Text MenuText(Transform parent,string name,string value,float size,float x,float y,float width,float height,bool heading=false)
    {
        var text=MenuRect(parent,name,x,y,width,height).gameObject.AddComponent<TextMeshProUGUI>();text.text=value;text.font=heading?BistroBuilderTypography.Title:BistroBuilderTypography.Body;
        text.fontSize=size;text.color=MenuInk;text.alignment=TextAlignmentOptions.MidlineLeft;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;return text;
    }
    static Button MenuButton(Transform parent,string name,string title,float x,float y,float width,float height,UnityEngine.Events.UnityAction action,bool primary=false)
    {
        var rect=MenuRect(parent,name,x,y,width,height);var image=MenuSurface(rect,primary?MenuOlive:MenuField);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;var colors=button.colors;colors.highlightedColor=new Color(.91f,.94f,.86f);colors.pressedColor=new Color(.78f,.83f,.72f);colors.selectedColor=Color.white;colors.disabledColor=new Color(.8f,.8f,.8f,1);button.colors=colors;
        var focus=rect.gameObject.AddComponent<Outline>();focus.effectColor=new Color(.35f,.46f,.24f,.16f);focus.effectDistance=new Vector2(1,-1);
        if(!string.IsNullOrEmpty(title)){var text=MenuText(rect,"Label",title,15,8,0,width-16,height);text.font=BistroBuilderTypography.Emphasis;text.alignment=TextAlignmentOptions.Center;text.color=primary?Color.white:MenuInk;}
        button.onClick.AddListener(action);return button;
    }
    static Sprite MenuRoundSprite()
    {
        if(menuRound!=null)return menuRound;
        const int size=64;const float radius=12;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=x-Mathf.Clamp(x,radius-.5f,size-radius-.5f),dy=y-Mathf.Clamp(y,radius-.5f,size-radius-.5f);pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy)));}
        tex.SetPixels32(pixels);tex.Apply(false,true);menuRound=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(14,14,14,14));return menuRound;
    }
}
