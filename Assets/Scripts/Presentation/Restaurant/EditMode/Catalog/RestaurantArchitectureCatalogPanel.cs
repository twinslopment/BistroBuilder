using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Architecture catalogue/inspector. All edits go through the existing construction tool.</summary>
public sealed class RestaurantArchitectureCatalogPanel : MonoBehaviour
{
    public sealed class Entry
    {
        public string Id, Name, Description, Tariff, Role;
        public BistroBuilderConstructionRuntimeMode Mode;
        public float Height=2.5f, Thickness=.12f, Length;
        public bool Available=true;
        public string PreviewResource => "BistroBuilder/UI/Architecture/"+Id;
    }
    static readonly Color Paper=new Color32(250,248,244,255), Ink=new Color32(31,35,29,255), Muted=new Color32(111,115,108,255), Olive=new Color32(103,128,70,255), Field=new Color32(240,237,231,255);
    public static readonly Entry[] WallEntries = {
        new Entry{Id="wall-interior",Name="Pared",Description="Divide espacios y define ambientes. Arrastra sobre el suelo para trazar una pared.",Tariff="wall.default",Mode=BistroBuilderConstructionRuntimeMode.Wall},
        new Entry{Id="door",Name="Puerta",Description="Inserta una puerta sobre una pared existente. La abertura respeta los lÃ­mites y otros huecos.",Tariff="door",Mode=BistroBuilderConstructionRuntimeMode.Door},
        new Entry{Id="window",Name="Ventana",Description="Inserta una ventana en una pared existente.",Tariff="window",Mode=BistroBuilderConstructionRuntimeMode.Window},
        new Entry{Id="divider",Name="Separador",Description="Delimita ambientes con un tabique bajo. Arrastra para definir su longitud.",Tariff="wall.default",Mode=BistroBuilderConstructionRuntimeMode.Wall,Height=1.2f,Thickness=.1f},
        new Entry{Id="wall-module",Name="MÃ³dulo de pared",Description="Coloca tramos sueltos de pared con longitud y orientaciÃ³n ajustables.",Tariff="wall.default",Mode=BistroBuilderConstructionRuntimeMode.WallModule,Length=1}
    };
    public static readonly Entry[] SurfaceEntries = {
        new Entry{Id="floor-limestone",Name="Suelo de caliza",Description="Acabado de suelo del kit de construcciÃ³n integrado. Selecciona una habitaciÃ³n cerrada para aplicar el acabado.",Tariff="finish.floor.default",Role="floor",Mode=BistroBuilderConstructionRuntimeMode.Select},
        new Entry{Id="wall-plaster",Name="Enlucido cÃ¡lido",Description="Acabado de pared del kit integrado. La aplicaciÃ³n independiente sobre las caras de pared todavÃ­a no estÃ¡ disponible.",Tariff="finish.wall.default",Role="wall",Mode=BistroBuilderConstructionRuntimeMode.Select,Available=false}
    };
    RestaurantPlaceableCatalogPanel catalog;
    BistroBuilderConstructionAuthoringRuntimeTool tool;
    RestaurantEditModeService editMode;
    BistroBuilderUiShell shell;
    RectTransform left,right,tabs,grid,details,scopes;
    TMP_Text title,empty,inspectorTitle,description,price,dimensions,rules,status,selectionText;
    TMP_InputField search;
    Image preview;
    Button apply,commit;
    readonly List<Button> tabButtons=new List<Button>();
    readonly List<GameObject> cards=new List<GameObject>();
    readonly Dictionary<Entry,Image> cardImages=new Dictionary<Entry,Image>();
    Entry selected;
    RestaurantEditCatalogSection section;
    int selectedTab,scopeIndex;
    bool closed,inspectorClosed;
    string lastStatus;
    static Sprite rounded;
    public bool IsVisible => left!=null&&left.gameObject.activeInHierarchy;
    public string InspectorTitle => inspectorTitle!=null?inspectorTitle.text:"";
    void Start()
    {
        catalog=GetComponent<RestaurantPlaceableCatalogPanel>();
        tool=FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        editMode=FindFirstObjectByType<RestaurantEditModeService>();
        shell=FindFirstObjectByType<BistroBuilderUiShell>();
        catalog.SectionChanged+=OpenSection;
        Build();
    }
    void OnDestroy(){if(catalog!=null)catalog.SectionChanged-=OpenSection;if(left!=null)Destroy(left.gameObject);if(right!=null)Destroy(right.gameObject);}
    void Build()
    {
        var canvas=GetComponentInParent<Canvas>(true);if(canvas==null)return;
        left=Panel("BB_ArchitectureCatalog",canvas.transform,false);
        right=Panel("BB_ArchitectureInspector",canvas.transform,true);
        title=Text(left,"Title","",27,42);At(title.rectTransform,22,14,365,42);
        ButtonAt(left,"Close","Ã—",377,17,30,32,()=>{closed=true;});
        var input=Node("Search",left);At(input,20,66,387,46);Background(input,Color.white);
        search=input.gameObject.AddComponent<TMP_InputField>();
        var searchText=Text(input,"Text","",15,46);Stretch(searchText.rectTransform,15,0,15,0);
        var placeholder=Text(input,"Placeholder","",14,46);Stretch(placeholder.rectTransform,15,0,15,0);placeholder.color=Muted;
        search.textComponent=(TextMeshProUGUI)searchText;search.placeholder=placeholder;search.textViewport=input;
        search.onValueChanged.AddListener(_=>Filter());
        tabs=Node("Categories",left);At(tabs,18,125,391,72);
        var tabLayout=tabs.gameObject.AddComponent<HorizontalLayoutGroup>();tabLayout.spacing=5;tabLayout.childControlWidth=tabLayout.childControlHeight=true;tabLayout.childForceExpandWidth=true;
        var scope=Node("Scopes",left);scopes=scope;At(scope,20,207,387,38);
        ButtonAt(scope,"All","Todos",0,0,94,36,()=>{scopeIndex=0;Filter();});
        ButtonAt(scope,"Interior","Interior",104,0,98,36,()=>{scopeIndex=1;Filter();});
        ButtonAt(scope,"Exterior","Exterior",212,0,98,36,()=>{scopeIndex=2;Filter();});
        var viewport=Node("Viewport",left);Stretch(viewport,18,78,18,259);viewport.gameObject.AddComponent<RectMask2D>();
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=25;scroll.movementType=ScrollRect.MovementType.Clamped;
        grid=Node("Cards",viewport);grid.anchorMin=new Vector2(0,1);grid.anchorMax=new Vector2(1,1);grid.pivot=new Vector2(.5f,1);grid.sizeDelta=Vector2.zero;
        var layout=grid.gameObject.AddComponent<GridLayoutGroup>();layout.cellSize=new Vector2(185,228);layout.spacing=new Vector2(10,12);layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;
        grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=grid;
        empty=Text(viewport,"Empty","No hay materiales disponibles en esta categorÃ­a.",16,120);At(empty.rectTransform,14,16,355,120);empty.alignment=TextAlignmentOptions.Center;
        var foot=Node("ConstructionActions",left);foot.anchorMin=new Vector2(0,0);foot.anchorMax=new Vector2(1,0);foot.pivot=new Vector2(.5f,0);foot.offsetMin=new Vector2(20,14);foot.offsetMax=new Vector2(-20,65);
        commit=ButtonAt(foot,"ApplyDraft","Aplicar cambios",0,0,235,48,()=>{tool.TryCommitDraft(out var error);status.text=error;});
        ButtonAt(foot,"Discard","Descartar",245,0,140,48,()=>{tool.TryCancelDraft(out var error);status.text=error;});
        // The inspector scrolls on shorter viewports; its close control stays outside the scrolling content.
        inspectorTitle=Text(right,"Title","",26,44);At(inspectorTitle.rectTransform,20,14,310,44);
        ButtonAt(right,"Close","Ã—",342,18,30,30,()=>inspectorClosed=true);
        var detailViewport=Node("Viewport",right);Stretch(detailViewport,16,14,16,64);detailViewport.gameObject.AddComponent<RectMask2D>();
        var detailScroll=detailViewport.gameObject.AddComponent<ScrollRect>();detailScroll.horizontal=false;detailScroll.scrollSensitivity=25;detailScroll.movementType=ScrollRect.MovementType.Clamped;
        details=Node("Content",detailViewport);details.anchorMin=new Vector2(0,1);details.anchorMax=new Vector2(1,1);details.pivot=new Vector2(.5f,1);details.sizeDelta=Vector2.zero;
        var vl=details.gameObject.AddComponent<VerticalLayoutGroup>();vl.spacing=10;vl.childControlWidth=vl.childControlHeight=true;vl.childForceExpandHeight=false;
        details.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;detailScroll.viewport=detailViewport;detailScroll.content=details;
        var picture=Node("Preview",details);Height(picture,238);Background(picture,Field);var art=Node("Image",picture);Stretch(art,8,8,8,8);preview=art.gameObject.AddComponent<Image>();preview.preserveAspect=true;preview.raycastTarget=false;
        description=Text(details,"Description","",16,70);
        price=Text(details,"Price","",27,39);price.color=Olive;
        dimensions=Text(details,"Dimensions","",15,62);
        rules=Text(details,"Rules","",15,115);
        selectionText=Text(details,"Selection","",14,75);selectionText.color=Muted;
        apply=ButtonAt(details,"Choose","",0,0,350,48,ExecuteSelected);Height((RectTransform)apply.transform,48);
        status=Text(details,"Status","",15,70);status.color=Olive;
        var room=ButtonAt(details,"CreateRoom","Dibujar habitaciÃ³n",0,0,350,42,()=>tool.SetMode(BistroBuilderConstructionRuntimeMode.Room));Height((RectTransform)room.transform,42);
        var module=ButtonAt(details,"ModuleLength","Longitud de mÃ³dulo: 1 / 2 / 3 / 4 m",0,0,350,42,()=>tool.ConfigureModule(tool.ModuleLength>=4?1:tool.ModuleLength+1,tool.ModuleAngle));Height((RectTransform)module.transform,42);
        var rotate=ButtonAt(details,"ModuleRotate","Girar mÃ³dulo 90Â°",0,0,350,42,()=>tool.ConfigureModule(tool.ModuleLength,tool.ModuleAngle+90));Height((RectTransform)rotate.transform,42);
        var previous=ButtonAt(details,"OpeningLeft","Mover hueco âˆ’10 cm",0,0,350,42,()=>{tool.TryAdjustOpening(-.1f,false,out var e);status.text=e;});Height((RectTransform)previous.transform,42);
        var following=ButtonAt(details,"OpeningRight","Mover hueco +10 cm",0,0,350,42,()=>{tool.TryAdjustOpening(.1f,false,out var e);status.text=e;});Height((RectTransform)following.transform,42);
        var flip=ButtonAt(details,"OpeningFlip","Invertir apertura",0,0,350,42,()=>{tool.TryAdjustOpening(0,true,out var e);status.text=e;});Height((RectTransform)flip.transform,42);
        foreach(var zone in new[]{new[]{"SalÃ³n","zone.dining"},new[]{"Cocina","zone.kitchen"},new[]{"BaÃ±o","zone.bathroom"},new[]{"Barra","zone.bar"},new[]{"Terraza","zone.terrace"}}){string id=zone[1];var z=ButtonAt(details,"Zone_"+id,zone[0],0,0,350,38,()=>tool.SetRoomZone(id));Height((RectTransform)z.transform,38);}
        left.gameObject.SetActive(false);right.gameObject.SetActive(false);
    }
    public void OpenSection(RestaurantEditCatalogSection value)
    {
        section=value;closed=inspectorClosed=false;selectedTab=0;scopeIndex=0;selected=null;lastStatus=null;
        if(left==null||!RestaurantEditCatalogSections.IsArchitecture(value))return;
        title.text=RestaurantEditCatalogSections.Title(value);((TMP_Text)search.placeholder).text=RestaurantEditCatalogSections.SearchHint(value);search.SetTextWithoutNotify("");
        foreach(Transform child in tabs){child.gameObject.SetActive(false);Destroy(child.gameObject);}tabButtons.Clear();
        var labels=RestaurantEditCatalogSections.Tabs(value);
        for(int i=0;i<labels.Length;i++)
        {int tab=i;var b=ButtonAt(tabs,"Tab"+i,labels[i],0,0,60,value==RestaurantEditCatalogSection.Walls?43:68,()=>{selectedTab=tab;if(value==RestaurantEditCatalogSection.Walls&&tab==1)tool.SetMode(BistroBuilderConstructionRuntimeMode.Select);Filter();});Height((RectTransform)b.transform,value==RestaurantEditCatalogSection.Walls?43:68);if(value==RestaurantEditCatalogSection.Surfaces){var label=b.GetComponentInChildren<TMP_Text>();label.alignment=TextAlignmentOptions.Bottom;var iconRect=Node("Icon",b.transform);At(iconRect,18,8,25,25);var icon=iconRect.gameObject.AddComponent<BistroBuilderEditChromeIcon>();var symbols=new[]{BistroBuilderEditChromeSymbol.Grid,BistroBuilderEditChromeSymbol.Surfaces,BistroBuilderEditChromeSymbol.Walls,BistroBuilderEditChromeSymbol.Home,BistroBuilderEditChromeSymbol.Terrain,BistroBuilderEditChromeSymbol.Sun};icon.Configure(symbols[i],Muted);}tabButtons.Add(b);}
        scopes.gameObject.SetActive(value==RestaurantEditCatalogSection.Surfaces);RebuildCards();Select((section==RestaurantEditCatalogSection.Surfaces?SurfaceEntries:WallEntries)[0]);
    }
    void RebuildCards()
    {
        foreach(var go in cards){go.SetActive(false);Destroy(go);}cards.Clear();cardImages.Clear();
        foreach(var entry in section==RestaurantEditCatalogSection.Surfaces?SurfaceEntries:WallEntries)
        {
            var card=Node("ArchitectureCard_"+entry.Id,grid);var bg=Background(card,Color.white);var outline=card.gameObject.AddComponent<Outline>();outline.effectColor=new Color32(225,221,212,255);outline.effectDistance=new Vector2(1,-1);
            var button=card.gameObject.AddComponent<Button>();button.targetGraphic=bg;button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>Select(entry));
            var art=Node("Preview",card);At(art,7,7,171,163);var image=art.gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>(entry.PreviewResource);image.preserveAspect=true;image.raycastTarget=false;image.enabled=image.sprite!=null;
            var label=Text(card,"Name",entry.Name,15,26);At(label.rectTransform,11,175,164,26);
            var cost=Text(card,"Price",Price(entry),14,24);cost.color=Olive;At(cost.rectTransform,11,201,164,24);
            cards.Add(card.gameObject);cardImages[entry]=bg;
        }
        Filter();
    }
    void Filter()
    {
        int count=0;int i=0;string query=search.text.Trim();
        foreach(var entry in section==RestaurantEditCatalogSection.Surfaces?SurfaceEntries:WallEntries)
        {
            bool matches=entry.Name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||entry.Description.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
            if(section==RestaurantEditCatalogSection.Surfaces)matches&=selectedTab==0||selectedTab==1&&entry.Role=="floor"||selectedTab==2&&entry.Role=="wall";
            if(section==RestaurantEditCatalogSection.Surfaces&&scopeIndex==2)matches=false; if(i<cards.Count)cards[i++].SetActive(matches);if(matches)count++;
        }
        empty.gameObject.SetActive(count==0);
        for(int s=0;s<scopes.childCount;s++){var child=scopes.GetChild(s);child.GetComponent<Image>().color=s==scopeIndex?Olive:Field;child.GetComponentInChildren<TMP_Text>().color=s==scopeIndex?Color.white:Ink;}
        for(int t=0;t<tabButtons.Count;t++){tabButtons[t].GetComponent<Image>().color=t==selectedTab?Olive:Field;tabButtons[t].GetComponentInChildren<TMP_Text>().color=t==selectedTab?Color.white:Ink;}
    }
    public void SelectById(string id){foreach(var entry in section==RestaurantEditCatalogSection.Surfaces?SurfaceEntries:WallEntries)if(entry.Id==id){Select(entry);return;}}
    void Select(Entry entry)
    {
        selected=entry;inspectorClosed=false;inspectorTitle.text=entry.Name;description.text=entry.Description;price.text=Price(entry);
        preview.sprite=Resources.Load<Sprite>(entry.PreviewResource);preview.enabled=preview.sprite!=null;
        bool surface=section==RestaurantEditCatalogSection.Surfaces;
        dimensions.text=surface?"Dimensiones\nCobertura continua Â· precio por mÂ²":entry.Mode==BistroBuilderConstructionRuntimeMode.Door||entry.Mode==BistroBuilderConstructionRuntimeMode.Window?"Dimensiones\nSe ajusta al hueco y a la pared de destino":$"Dimensiones\nGrosor {entry.Thickness*100:0} cm  |  Altura {entry.Height*100:0} cm";
        rules.text=surface?"Reglas de aplicaciÃ³n\nâ€¢ Selecciona una habitaciÃ³n cerrada\nâ€¢ El acabado respeta su contorno\nâ€¢ Confirma el borrador para aplicar el coste":"Reglas de colocaciÃ³n\nâ€¢ Encaja con la construcciÃ³n existente\nâ€¢ Respeta paredes y otros huecos\nâ€¢ Confirma el borrador para construir";
        apply.GetComponentInChildren<TMP_Text>().text=surface?"Aplicar a la habitaciÃ³n":selectedTab==1?"Editar en el restaurante":"Usar herramienta";
        status.text=entry.Available?(surface?"Selecciona una habitaciÃ³n en el restaurante.":"Lista para trazar. Usa la herramienta sobre la escena."):"AplicaciÃ³n en paredes pendiente de integraciÃ³n.";
        foreach(var pair in cardImages)pair.Value.GetComponent<Outline>().effectColor=pair.Key==entry?Olive:new Color32(225,221,212,255);
        if(!surface)ExecuteSelected();
    }
    void ExecuteSelected()
    {
        if(selected==null||tool==null||!selected.Available)return;
        if(section==RestaurantEditCatalogSection.Surfaces){tool.TryApplySelectedRoomFloorFinish(out var error);status.text=string.IsNullOrEmpty(error)?"Acabado preparado. Pulsa Aplicar cambios para confirmar.":error;return;}
        tool.ConfigureWallFromInterface(selected.Height,selected.Thickness);
        if(selected.Length>0)tool.ConfigureModule(selected.Length,tool.ModuleAngle);
        tool.SetMode(selectedTab==1?BistroBuilderConstructionRuntimeMode.Select:selected.Mode);
    }
    void LateUpdate()
    {
        if(left==null)return; if(tool==null)tool=FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        bool visible=catalog!=null&&RestaurantEditCatalogSections.IsArchitecture(catalog.CurrentSection)&&editMode!=null&&editMode.IsEditModeActive&&(shell==null||!shell.HasManagementScreenOpen);
        left.gameObject.SetActive(visible&&!closed);right.gameObject.SetActive(visible&&selected!=null&&!inspectorClosed);
        if(!visible)return;
        foreach(Transform child in details){if(child.name.StartsWith("Module"))child.gameObject.SetActive(section==RestaurantEditCatalogSection.Walls&&tool!=null&&tool.Mode==BistroBuilderConstructionRuntimeMode.WallModule);if(child.name.StartsWith("Opening"))child.gameObject.SetActive(tool!=null&&tool.SelectedKind==BistroBuilder.ConstructionAuthoring.EntityKind.Opening);if(child.name.StartsWith("Zone_"))child.gameObject.SetActive(tool!=null&&(tool.Mode==BistroBuilderConstructionRuntimeMode.Room||tool.SelectedKind==BistroBuilder.ConstructionAuthoring.EntityKind.Room));}
        commit.interactable=tool!=null&&tool.HasDraftChanges;
        apply.interactable=selected!=null&&selected.Available&&(section!=RestaurantEditCatalogSection.Surfaces||tool!=null&&tool.SelectedKind==BistroBuilder.ConstructionAuthoring.EntityKind.Room);
        if(tool!=null&&lastStatus!=tool.StatusMessage){lastStatus=tool.StatusMessage;selectionText.text=tool.SelectionDescription()+"\n"+lastStatus;}
    }
    static string Price(Entry entry)
    {
        var tariffs=Resources.Load<BistroBuilderEditFinanceTariffTable>("BistroBuilder/Finance/BB_EditMode_PlaytestTariffs");
        if(tariffs!=null)foreach(var rate in tariffs.Rates)if(rate.definitionId==entry.Tariff)return BistroBuilderFinanceUiFormat.Money(rate.addedSignedCentsPerUnit)+(rate.unit==BistroBuilderEditFinanceRateUnit.Area?" / mÂ²":rate.unit==BistroBuilderEditFinanceRateUnit.Length?" / m":"");
        return "Precio no disponible";
    }
    static RectTransform Node(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return(RectTransform)go.transform;}
    static RectTransform Panel(string name,Transform parent,bool right)
    {
        var r=Node(name,parent);r.gameObject.AddComponent<BistroBuilderEditChromeSurface>();r.anchorMin=new Vector2(right?1:0,0);r.anchorMax=new Vector2(right?1:0,1);
        r.offsetMin=new Vector2(right?-412:18,106);r.offsetMax=new Vector2(right?-24:445,-88);Background(r,Paper);return r;
    }
    static Image Background(RectTransform r,Color c){var i=r.gameObject.AddComponent<Image>();i.sprite=Rounded();i.type=Image.Type.Sliced;i.color=c;return i;}
    static TMP_Text Text(Transform parent,string name,string value,float size,float height){var r=Node(name,parent);Height(r,height);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=BistroBuilderTypography.Body;t.text=value;t.fontSize=size;t.color=Ink;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;}
    static Button ButtonAt(Transform parent,string name,string value,float x,float y,float w,float h,Action action){var r=Node(name,parent);At(r,x,y,w,h);var bg=Background(r,Field);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=bg;b.onClick.AddListener(()=>action());var t=Text(r,"Label",value,14,h);t.alignment=TextAlignmentOptions.Center;Stretch(t.rectTransform,4,4,4,4);return b;}
    static void At(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    static void Stretch(RectTransform r,float l,float b,float rr,float t){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(-rr,-t);}
    static void Height(RectTransform r,float h){var le=r.GetComponent<LayoutElement>();if(le==null)le=r.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=h;le.flexibleHeight=0;}
    static Sprite Rounded()
    {
        if(rounded!=null)return rounded;const int n=48;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);var pixels=new Color32[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=x-Mathf.Clamp(x,10,n-11),dy=y-Mathf.Clamp(y,10,n-11);pixels[y*n+x]=new Color(1,1,1,Mathf.Clamp01(11-Mathf.Sqrt(dx*dx+dy*dy)));}
        t.SetPixels32(pixels);t.Apply(false,true);rounded=Sprite.Create(t,new Rect(0,0,n,n),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,Vector4.one*12);return rounded;
    }
}
