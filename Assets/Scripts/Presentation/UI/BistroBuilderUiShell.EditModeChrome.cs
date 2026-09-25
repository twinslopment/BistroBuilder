using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Symbol = BistroBuilderEditChromeSymbol;
using Mode = BistroBuilderConstructionRuntimeMode;

public sealed partial class BistroBuilderUiShell
{
    public const string EditModeTopBarName = "BB_UIUX_EditModeTopBar";
    public const string EditModeBottomBarName = "BB_UIUX_EditModeBottomBar";
    RectTransform editModeTopBar, editModeBottomBar;
    TMP_Text editModeMoneyText, editModeClockText, editModeToolStatusText;
    BistroBuilderConstructionAuthoringRuntimeTool editModeConstructionTool;
    RestaurantPlaceableCatalogPanel editModeCatalogPanel;
    RestaurantEditInteractionController editModeFurnitureController;
    RestaurantPlaceableDeletionService editChromeDeletion;
    RestaurantPlacementHistoryService editChromeHistory;
    BistroBuilderEditGridOverlay editChromeGrid;
    bool editModeChromeBuilt;
    readonly Dictionary<string, Button> editChromeButtons = new Dictionary<string, Button>();
    readonly Dictionary<string, BistroBuilderEditChromeControl> editChromeControls = new Dictionary<string, BistroBuilderEditChromeControl>();
    static readonly Color EditChromeSurface = new Color32(250,248,244,250);
    static readonly Color EditChromeText = new Color32(36,39,35,255);
    static readonly Color EditChromeMuted = new Color32(110,109,105,255);
    static readonly Color EditChromeOlive = new Color32(103,128,70,255);
    static readonly Color EditChromeLine = new Color32(226,221,212,255);
    static Sprite editChromeRounded;

    void EnsureEditModeChrome()
    {
        if(shellRoot==null||editModeChromeBuilt)return;
        ResolveEditChrome();
        editModeTopBar=EditBar(EditModeTopBarName,true,30,14,62);
        editModeBottomBar=EditBar(EditModeBottomBarName,false,20,12,80);
        var hintRoot=NewUi("EditChromeHint",shellRoot).GetComponent<RectTransform>();
        hintRoot.anchorMin=new Vector2(.5f,0);hintRoot.anchorMax=hintRoot.anchorMin;hintRoot.pivot=new Vector2(.5f,0);
        hintRoot.anchoredPosition=new Vector2(0,104);hintRoot.sizeDelta=new Vector2(610,36);
        hintRoot.gameObject.AddComponent<BistroBuilderEditChromeSurface>();
        var hintBg=hintRoot.gameObject.AddComponent<Image>();hintBg.sprite=EditChromeRoundedSprite();hintBg.type=Image.Type.Sliced;hintBg.color=EditChromeSurface;hintBg.raycastTarget=false;
        editModeToolStatusText=ChromeText(hintRoot,"Hint","",13,EditChromeMuted);StretchChrome(editModeToolStatusText.rectTransform,12,4,12,4);
        hintRoot.gameObject.SetActive(false);
        BuildEditTopChrome(editModeTopBar);BuildEditBottomChrome(editModeBottomBar);
        editModeTopBar.gameObject.SetActive(false);editModeBottomBar.gameObject.SetActive(false);
        editModeChromeBuilt=true;
    }
    void ResolveEditChrome()
    {
        if(editModeConstructionTool==null)editModeConstructionTool=FindScene<BistroBuilderConstructionAuthoringRuntimeTool>();
        if(editModeCatalogPanel==null)editModeCatalogPanel=FindScene<RestaurantPlaceableCatalogPanel>();
        if(editModeFurnitureController==null)editModeFurnitureController=FindScene<RestaurantEditInteractionController>();
        if(editChromeHistory==null)editChromeHistory=FindScene<RestaurantPlacementHistoryService>();
        if(editChromeDeletion==null)editChromeDeletion=FindScene<RestaurantPlaceableDeletionService>();
        if(editChromeGrid==null)editChromeGrid=FindScene<BistroBuilderEditGridOverlay>();
    }
    RectTransform EditBar(string name,bool top,float side,float edge,float height)
    {
        var root=NewUi(name,shellRoot).GetComponent<RectTransform>();
        root.gameObject.AddComponent<BistroBuilderEditChromeSurface>();
        root.anchorMin=new Vector2(0,top?1:0);root.anchorMax=new Vector2(1,top?1:0);
        root.pivot=new Vector2(.5f,top?1:0);
        root.offsetMin=new Vector2(side,top?-edge-height:edge);root.offsetMax=new Vector2(-side,top?-edge:edge+height);
        var bg=root.gameObject.AddComponent<Image>();bg.sprite=EditChromeRoundedSprite();bg.type=Image.Type.Sliced;bg.color=EditChromeSurface;
        var shadow=root.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.08f);shadow.effectDistance=new Vector2(0,-2);
        var layout=root.gameObject.AddComponent<HorizontalLayoutGroup>();layout.padding=new RectOffset(14,14,6,6);layout.spacing=8;
        layout.childAlignment=TextAnchor.MiddleCenter;layout.childControlWidth=layout.childControlHeight=true;
        layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
        return root;
    }
    void BuildEditTopChrome(RectTransform root)
    {
        ChromeButton(root,"EditHome",Symbol.Home,"",44,46,"Menú de partida: guardar, cargar y volver al inicio",()=>GetComponent<BistroBuilderOptionsScreen>()?.Open());
        ChromeDivider(root,34);
        var brand=ChromeText(root,"EditBrand","Bistro<color=#5E7D44>Builder</color>",30,EditChromeText);
        brand.font=BistroBuilderTypography.Title;brand.richText=true;ChromeWidth(brand.gameObject,221,48);
        ChromeDivider(root,34);
        var mode=ChromeBlock(root,"EditModeTitle",278,50);
        ChromeIcon(mode,"Pencil",Symbol.Pencil,EditChromeOlive,new Vector2(22,25),30);
        var title=ChromeText(mode,"Title","Modo Edición",21,EditChromeText);ChromeBox(title.rectTransform,48,0,230,29);
        var subtitle=ChromeText(mode,"Subtitle","Diseña el restaurante de tus sueños",12,EditChromeMuted);ChromeBox(subtitle.rectTransform,48,28,230,20);
        ChromeSpacer(root,"EditTopSpacerA");
        ChromeButton(root,"EditUndo",Symbol.Undo,"",46,46,"Deshacer el último cambio",()=>{if(IsFurnitureTool())editModeFurnitureController?.TryUndoLastPlacement();else editModeConstructionTool?.TryUndo(out _);});
        ChromeButton(root,"EditRedo",Symbol.Redo,"",46,46,"Rehacer el último cambio",()=>{if(IsFurnitureTool())editModeFurnitureController?.TryRedoLastPlacement();else editModeConstructionTool?.TryRedo(out _);});
        ChromeButton(root,"EditPan",Symbol.Hand,"",48,46,"Explorar: arrastra con el botón central del ratón",()=>{editModeFurnitureController?.CancelActivePlacement();editModeConstructionTool?.SetMode(Mode.Select);});
        ChromeButton(root,"EditMove",Symbol.Move,"",44,46,"Mover el artículo seleccionado",()=>editModeFurnitureController?.TryBeginMoveSelected());
        ChromeDivider(root,30);
        ChromeButton(root,"EditGrid",Symbol.Grid,"",44,46,"Mostrar u ocultar la cuadrícula",()=>{if(editChromeGrid!=null)editChromeGrid.enabled=!editChromeGrid.enabled;});
        ChromeDivider(root,30);
        ChromeButton(root,"EditTerrain",Symbol.Terrain,"",44,46,"Dibujar una habitación arrastrando sobre el terreno",()=>{editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Walls);editModeConstructionTool?.SetMode(Mode.Room);});
        var paint=ChromeButton(root,"EditPaint",Symbol.Paint,"",48,46,"Abrir catálogo de superficies",()=>editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Surfaces));
        ChromeSpacer(root,"EditTopSpacerB");
        var clock=ChromeBlock(root,"EditClock",132,46);ChromeIcon(clock,"Sun",Symbol.Sun,new Color32(246,171,40,255),new Vector2(17,23),31);
        editModeClockText=ChromeText(clock,"Time","—",16,EditChromeMuted);ChromeBox(editModeClockText.rectTransform,39,0,93,46);
        ChromeDivider(root,34);
        editModeMoneyText=ChromeText(root,"EditMoney","—",22,EditChromeOlive);editModeMoneyText.font=BistroBuilderTypography.Emphasis;ChromeWidth(editModeMoneyText.gameObject,135,46);
        ChromeDivider(root,34);
        ChromeButton(root,"EditPlay",Symbol.Play,"",49,46,"Terminar la edición y volver al restaurante",HandleEditModeClicked,false,false,EditChromeOlive);
    }
    void BuildEditBottomChrome(RectTransform root)
    {
        var venue=ChromeBlock(root,"EditVenue",330,64);
        ChromeIcon(venue,"Plot",Symbol.Plot,EditChromeOlive,new Vector2(26,32),30);
        var title=ChromeText(venue,"Title","Terreno de Restaurante",15,EditChromeText);ChromeBox(title.rectTransform,64,12,260,23);
        var dimensions=ChromeText(venue,"Dimensions",EditPlotDimensions(),14,EditChromeMuted);ChromeBox(dimensions.rectTransform,64,35,260,22);
        ChromeSpacer(root,"EditBottomLeadingSpace");
        ChromeTool(root,"EditBuild",Symbol.Chair,"Construir",Mode.Furniture,null);
        ChromeTool(root,"EditSurfaces",Symbol.Surfaces,"Superficies",Mode.Room,null);
        ChromeTool(root,"EditWalls",Symbol.Walls,"Paredes",Mode.Wall,null);
        ChromeTool(root,"EditDecor",Symbol.Plant,"Decoración",Mode.Furniture,RestaurantPlaceableItemCategory.Decoration);
        ChromeTool(root,"EditLighting",Symbol.Bulb,"Iluminación",Mode.Furniture,RestaurantPlaceableItemCategory.Lighting);
        ChromeTool(root,"EditServices",Symbol.Settings,"Servicios",Mode.Furniture,RestaurantPlaceableItemCategory.ServiceEquipment);
        ChromeTool(root,"EditOther",Symbol.More,"Otro",Mode.Furniture,RestaurantPlaceableItemCategory.Other);
        ChromeSpacer(root,"EditBottomSpacer");
        ChromeButton(root,"EditDelete",Symbol.Delete,"Eliminar",127,58,"Eliminar la selección; aplica la devolución o coste indicado en el inspector",DeleteChromeSelection,true);
        ChromeButton(root,"EditRotate",Symbol.Rotate,"Rotar",118,58,"Girar artículo, pared o módulo. Paredes y módulos: 90° (R)",RotateChromeSelection,true);
        ChromeButton(root,"EditDuplicate",Symbol.Duplicate,"Duplicar",131,58,"Preparar otra unidad para colocar; se cobra al confirmar",DuplicateChromeSelection,true);
    }
    string EditPlotDimensions()
    {
        var floor=GameObject.Find("Floor_Test");var renderer=floor!=null?floor.GetComponent<Renderer>():null;
        return renderer!=null?$"{renderer.bounds.size.x:0.#} × {renderer.bounds.size.z:0.#} m":"Modo Edición";
    }
    bool IsFurnitureTool()=>editModeConstructionTool==null||editModeConstructionTool.Mode==Mode.Furniture;
    RestaurantPlaceableObject SelectedChromePlaceable()
    {var editable=editModeFurnitureController!=null?editModeFurnitureController.SelectedEditableObject:null;return editable!=null?editable.GetComponent<RestaurantPlaceableObject>():null;}
    void DeleteChromeSelection()
    {
        var selected=SelectedChromePlaceable();
        if(IsFurnitureTool()&&selected!=null&&editChromeDeletion!=null){if(editChromeDeletion.TryDelete(selected,out var result))editModeFurnitureController.ClearSelection();else ChromeMessage(result.Message);}
        else if(editModeConstructionTool!=null&&!editModeConstructionTool.TryDeleteSelection(out var error))ChromeMessage(error);
    }
    void RotateChromeSelection()
    {
        if (!IsFurnitureTool() && editModeConstructionTool != null)
        {
            if (!editModeConstructionTool.TryRotateArchitecture(out var error)) ChromeMessage(error);
            return;
        }
        if(editModeFurnitureController==null)return;
        if(!editModeFurnitureController.HasActivePlacement&&!editModeFurnitureController.TryBeginMoveSelected())return;
        editModeFurnitureController.RotateActiveCandidateFromInterface();
    }
    void DuplicateChromeSelection()
    {
        var selected=SelectedChromePlaceable();
        if(IsFurnitureTool()&&selected!=null&&selected.ItemDefinition!=null)editModeFurnitureController.TryBeginPlaceableCreation(selected.ItemDefinition);
        else if(editModeConstructionTool!=null&&!editModeConstructionTool.TryCopySelection(out var error))ChromeMessage(error);
    }
    void ChromeMessage(string message){if(editModeToolStatusText==null||string.IsNullOrEmpty(message))return;editModeToolStatusText.text=message;editModeToolStatusText.transform.parent.gameObject.SetActive(true);}
    void RefreshEditModeChrome(bool editing,bool managing)
    {
        EnsureEditModeChrome();bool visible=editing&&!managing;
        var timeDock = canvas != null ? canvas.transform.Find("BB_368B_TimeControlsDock") : null;
        if(timeDock != null){var group=timeDock.GetComponent<CanvasGroup>();if(group==null)group=timeDock.gameObject.AddComponent<CanvasGroup>();group.alpha=editing?0:1;group.interactable=group.blocksRaycasts=!editing;}
        if(editModeTopBar!=null)editModeTopBar.gameObject.SetActive(visible);
        if(editModeBottomBar!=null)editModeBottomBar.gameObject.SetActive(visible);
        if(topNavigation!=null)topNavigation.gameObject.SetActive(!editing);
        if(bottomOperations!=null)bottomOperations.gameObject.SetActive(!editing);
        if(!visible){if(editModeToolStatusText!=null)editModeToolStatusText.transform.parent.gameObject.SetActive(false);return;}
        ResolveEditChrome();
        editModeMoneyText.text=finance!=null?BistroBuilderFinanceUiFormat.Money(finance.CurrentBalanceCents):"—";
        editModeClockText.text=EditChromeClock();
        var mode=editModeConstructionTool!=null?editModeConstructionTool.Mode:Mode.Furniture;
        var section=editModeCatalogPanel!=null?editModeCatalogPanel.CurrentSection:RestaurantEditCatalogSection.Build;
        bool furniture=mode==Mode.Furniture;
        ChromeSelected("EditBuild",section==RestaurantEditCatalogSection.Build);
        ChromeSelected("EditSurfaces",section==RestaurantEditCatalogSection.Surfaces);
        ChromeSelected("EditWalls",section==RestaurantEditCatalogSection.Walls);
        ChromeSelected("EditDecor",section==RestaurantEditCatalogSection.Decoration);
        ChromeSelected("EditLighting",section==RestaurantEditCatalogSection.Lighting);
        ChromeSelected("EditServices",section==RestaurantEditCatalogSection.Services);
        ChromeSelected("EditOther",section==RestaurantEditCatalogSection.Other);
        ChromeSelected("EditPan",mode==Mode.Select);ChromeSelected("EditGrid",editChromeGrid!=null&&editChromeGrid.enabled);
        ChromeSelected("EditTerrain",mode==Mode.Room);
        bool selected=editModeFurnitureController!=null&&editModeFurnitureController.HasSelection;
        bool placement=editModeFurnitureController!=null&&editModeFurnitureController.HasActivePlacement;
        bool structure=editModeConstructionTool!=null&&(editModeConstructionTool.SelectedKind==BistroBuilder.ConstructionAuthoring.EntityKind.Wall||editModeConstructionTool.SelectedKind==BistroBuilder.ConstructionAuthoring.EntityKind.Opening);
        editChromeButtons["EditMove"].interactable=furniture&&selected&&!placement;
        editChromeButtons["EditDelete"].interactable=!placement&&(furniture?SelectedChromePlaceable()!=null:structure);
        editChromeButtons["EditRotate"].interactable=furniture?(placement||selected):editModeConstructionTool!=null&&editModeConstructionTool.CanRotateArchitecture;
        editChromeButtons["EditDuplicate"].interactable=!placement&&(furniture?SelectedChromePlaceable()!=null:structure);
        editChromeButtons["EditUndo"].interactable=furniture?editChromeHistory!=null&&editChromeHistory.CanUndo:editModeConstructionTool!=null&&editModeConstructionTool.CanUndo;
        editChromeButtons["EditRedo"].interactable=furniture?editChromeHistory!=null&&editChromeHistory.CanRedo:editModeConstructionTool!=null&&editModeConstructionTool.CanRedo;
    }
    string EditChromeClock()
    {
        if(topClock==null)topClock=FindScene<GameClock>();
        if(topGameState==null)topGameState=FindScene<BistroBuilderGeneralGameStateService>();
        string day="";
        if(topGameState!=null)
        {
            int year=Mathf.Clamp(topGameState.CalendarYear,1,9999),month=Mathf.Clamp(topGameState.CalendarMonth,1,12);
            var date=new System.DateTime(year,month,Mathf.Clamp(topGameState.CalendarDay,1,System.DateTime.DaysInMonth(year,month)));
            day=date.ToString("ddd",System.Globalization.CultureInfo.GetCultureInfo("es-ES")).TrimEnd('.')+" ";
        }
        return topClock!=null?day+$"{topClock.Hour:00}:{topClock.Minute:00}":"—";
    }
    void ChromeSelected(string key,bool value)=>editChromeControls[key].SetSelected(value);
    void ChromeTool(Transform root,string key,Symbol symbol,string title,Mode mode,RestaurantPlaceableItemCategory? category)
    {
        RestaurantEditCatalogSection section = key switch
        {
            "EditSurfaces" => RestaurantEditCatalogSection.Surfaces,
            "EditWalls" => RestaurantEditCatalogSection.Walls,
            "EditDecor" => RestaurantEditCatalogSection.Decoration,
            "EditLighting" => RestaurantEditCatalogSection.Lighting,
            "EditServices" => RestaurantEditCatalogSection.Services,
            "EditOther" => RestaurantEditCatalogSection.Other,
            _ => RestaurantEditCatalogSection.Build
        };
        ChromeButton(root,key,symbol,title,96,68,title,()=>editModeCatalogPanel?.SelectSection(section),false,true);
    }
    Button ChromeButton(Transform parent,string key,Symbol symbol,string title,float width,float height,string help,UnityEngine.Events.UnityAction action,bool outlined=false,bool vertical=false,Color? tint=null)
    {
        var root=ChromeBlock(parent,key,width,height);var bg=root.gameObject.AddComponent<Image>();bg.sprite=EditChromeRoundedSprite();bg.type=Image.Type.Sliced;bg.color=Color.clear;
        var button=root.gameObject.AddComponent<Button>();button.targetGraphic=bg;button.transition=Selectable.Transition.None;
        button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Automatic};if(action!=null)button.onClick.AddListener(action);
        var icon=ChromeIcon(root,"Icon",symbol,tint??EditChromeMuted,vertical?new Vector2(width/2,23):string.IsNullOrEmpty(title)?new Vector2(width/2,height/2):new Vector2(26,height/2),vertical?28:outlined?26:30);
        TMP_Text label=null;if(!string.IsNullOrEmpty(title)){label=ChromeText(root,"Label",title,outlined?15:14,EditChromeText);if(outlined)label.fontStyle=FontStyles.Bold;if(vertical){ChromeBox(label.rectTransform,0,43,width,22);label.alignment=TextAlignmentOptions.Center;}else ChromeBox(label.rectTransform,48,0,width-53,height);}
        var control=root.gameObject.AddComponent<BistroBuilderEditChromeControl>();control.Configure(button,icon,label,editModeToolStatusText,help,tint??EditChromeMuted,outlined);
        editChromeButtons.Add(key,button);editChromeControls.Add(key,control);return button;
    }
    BistroBuilderEditChromeIcon ChromeIcon(Transform parent,string name,Symbol symbol,Color color,Vector2 center,float size)
    {var r=NewUi(name,parent).GetComponent<RectTransform>();ChromeBox(r,center.x-size/2,center.y-size/2,size,size);var icon=r.gameObject.AddComponent<BistroBuilderEditChromeIcon>();icon.Configure(symbol,color);return icon;}
    TMP_Text ChromeText(Transform parent,string name,string value,float size,Color color)
    {var text=NewUi(name,parent).AddComponent<TextMeshProUGUI>();text.font=BistroBuilderTypography.Body;text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;return text;}
    RectTransform ChromeBlock(Transform parent,string name,float width,float height){var r=NewUi(name,parent).GetComponent<RectTransform>();ChromeWidth(r.gameObject,width,height);return r;}
    static void ChromeWidth(GameObject go,float width,float height){var e=go.AddComponent<LayoutElement>();e.minWidth=e.preferredWidth=width;e.minHeight=e.preferredHeight=height;e.flexibleWidth=0;e.flexibleHeight=0;}
    static void ChromeBox(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    static void StretchChrome(RectTransform r,float left,float bottom,float right,float top){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);}
    void ChromeSpacer(Transform parent,string name){var e=NewUi(name,parent).AddComponent<LayoutElement>();e.minWidth=0;e.preferredWidth=0;e.flexibleWidth=1;}
    void ChromeDivider(Transform root,float height){var r=ChromeBlock(root,"Divider"+root.childCount,1,height);var image=r.gameObject.AddComponent<Image>();image.color=EditChromeLine;image.raycastTarget=false;}

    private static Sprite EditChromeRoundedSprite()
    {
        if(editChromeRounded!=null)return editChromeRounded;
        const int size=64;const float radius=15;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="BB Edit Chrome Rounded",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=x-Mathf.Clamp(x,radius-.5f,size-radius-.5f),dy=y-Mathf.Clamp(y,radius-.5f,size-radius-.5f);pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy)));}
        texture.SetPixels32(pixels);texture.Apply(false,true);editChromeRounded=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(17,17,17,17));return editChromeRounded;
    }
}
