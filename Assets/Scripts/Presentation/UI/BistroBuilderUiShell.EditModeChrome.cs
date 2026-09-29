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
    RectTransform editModeBrandBlock, editModeModeBlock, editModeClockBlock;
    BistroBuilderTopBarArtwork editModeBrandArtwork;
    TMP_Text editModeBrandFallback, editModeModeTitleText, editModeModeSubtitleText;
    TMP_Text editModeMoneyText, editModeClockText, editModeToolStatusText;
    BistroBuilderConstructionAuthoringRuntimeTool editModeConstructionTool;
    RestaurantPlaceableCatalogPanel editModeCatalogPanel;
    RestaurantEditInteractionController editModeFurnitureController;
    RestaurantPlaceableDeletionService editChromeDeletion;
    RestaurantPlacementHistoryService editChromeHistory;
    BistroBuilderEditGridOverlay editChromeGrid;
    BistroBuilderConstructionPlayerPanel editConstructionPanel;
    RestaurantPlaceableInspectorPanel editPlaceableInspector;
    bool editModeChromeBuilt;
    float editChromeMessageUntil;
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
        LayoutEditModeChrome();
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
        if(editConstructionPanel==null)editConstructionPanel=FindScene<BistroBuilderConstructionPlayerPanel>();
        if(editPlaceableInspector==null)editPlaceableInspector=FindScene<RestaurantPlaceableInspectorPanel>();
    }
    RectTransform EditBar(string name,bool top,float side,float edge,float height)
    {
        var root=NewUi(name,shellRoot).GetComponent<RectTransform>();
        root.gameObject.AddComponent<BistroBuilderEditChromeSurface>();
        root.anchorMin=new Vector2(0,top?1:0);root.anchorMax=new Vector2(1,top?1:0);
        root.pivot=new Vector2(.5f,top?1:0);
        root.offsetMin=new Vector2(side,top?-edge-height:edge);root.offsetMax=new Vector2(-side,top?-edge:edge+height);

        if(top)
        {
            // Mismo soporte marfil/latón que la TopBar aprobada normal.
            // El Image transparente solo captura el puntero en los huecos.
            var hit=root.gameObject.AddComponent<Image>();
            hit.color=Color.clear;
            hit.raycastTarget=true;

            var frame=NewUi("ApprovedFrame",root).AddComponent<BistroBuilderTopBarPlate>();
            frame.raycastTarget=false;
            StretchChrome(frame.rectTransform,0,0,0,0);
            var frameLayout=frame.gameObject.AddComponent<LayoutElement>();
            frameLayout.ignoreLayout=true;
            frame.transform.SetAsFirstSibling();
        }
        else
        {
            var bg=root.gameObject.AddComponent<Image>();
            bg.sprite=EditChromeRoundedSprite();
            bg.type=Image.Type.Sliced;
            bg.color=EditChromeSurface;

            var shadow=root.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(0,0,0,.08f);
            shadow.effectDistance=new Vector2(0,-2);
        }

        var layout=root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding=new RectOffset(14,14,6,6);
        layout.spacing=8;
        layout.childAlignment=TextAnchor.MiddleCenter;
        layout.childControlWidth=layout.childControlHeight=true;
        layout.childForceExpandWidth=false;
        layout.childForceExpandHeight=false;
        return root;
    }

    void BuildEditTopChrome(RectTransform root)
    {
        ChromeButton(root,"EditHome",Symbol.Home,"",44,46,
            "Menú de partida: guardar, cargar y volver al inicio",
            OpenEditOptionsFromChrome);

        ChromeDivider(root,34);

        editModeBrandBlock=ChromeBlock(root,"EditBrand",221,50);
        BuildEditModeBrand();

        ChromeDivider(root,34);

        editModeModeBlock=ChromeBlock(root,"EditModeTitle",278,50);
        AddApprovedCellSurface(editModeModeBlock,true);
        ChromeIcon(editModeModeBlock,"Pencil",Symbol.Pencil,EditChromeOlive,new Vector2(22,25),30);
        editModeModeTitleText=ChromeText(editModeModeBlock,"Title","Modo Edición",21,EditChromeText);
        editModeModeTitleText.font=BistroBuilderTypography.Title??BistroBuilderTypography.Body;
        ChromeBox(editModeModeTitleText.rectTransform,48,0,230,29);
        editModeModeSubtitleText=ChromeText(
            editModeModeBlock,
            "Subtitle",
            "Diseña el restaurante de tus sueños",
            12,
            EditChromeMuted);
        ChromeBox(editModeModeSubtitleText.rectTransform,48,28,230,20);

        ChromeSpacer(root,"EditTopSpacerA");

        ChromeButton(root,"EditUndo",Symbol.Undo,"",46,46,
            "Deshacer el último cambio",
            ()=>{if(IsFurnitureTool())editModeFurnitureController?.TryUndoLastPlacement();else editModeConstructionTool?.TryUndo(out _);});
        ChromeButton(root,"EditRedo",Symbol.Redo,"",46,46,
            "Rehacer el último cambio",
            ()=>{if(IsFurnitureTool())editModeFurnitureController?.TryRedoLastPlacement();else editModeConstructionTool?.TryRedo(out _);});
        ChromeButton(root,"EditPan",Symbol.Hand,"",48,46,
            "Explorar: arrastra con el botón central del ratón",
            ()=>{editModeFurnitureController?.CancelActivePlacement();editModeConstructionTool?.SetMode(Mode.Select);});
        ChromeButton(root,"EditMove",Symbol.Move,"",44,46,
            "Mover el artículo seleccionado",
            ()=>editModeFurnitureController?.TryBeginMoveSelected());

        ChromeDivider(root,30);

        ChromeButton(root,"EditGrid",Symbol.Grid,"",44,46,
            "Mostrar u ocultar la cuadrícula",
            ()=>{if(editChromeGrid!=null)editChromeGrid.enabled=!editChromeGrid.enabled;});

        ChromeDivider(root,30);

        ChromeButton(root,"EditTerrain",Symbol.Terrain,"",44,46,
            "Dibujar una habitación arrastrando sobre el terreno",
            ()=>{editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Walls);editModeConstructionTool?.SetMode(Mode.Room);});
        ChromeButton(root,"EditPaint",Symbol.Paint,"",48,46,
            "Abrir catálogo de superficies",
            ()=>editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Surfaces));

        ChromeSpacer(root,"EditTopSpacerB");

        editModeClockBlock=ChromeBlock(root,"EditClock",132,46);
        ChromeIcon(editModeClockBlock,"Sun",Symbol.Sun,new Color32(196,132,42,255),new Vector2(17,23),31);
        editModeClockText=ChromeText(editModeClockBlock,"Time","—",16,EditChromeMuted);
        ChromeBox(editModeClockText.rectTransform,39,0,93,46);

        ChromeDivider(root,34);

        editModeMoneyText=ChromeText(root,"EditMoney","—",22,EditChromeOlive);
        editModeMoneyText.font=BistroBuilderTypography.Emphasis;
        ChromeWidth(editModeMoneyText.gameObject,135,46);

        ChromeDivider(root,34);

        ChromeButton(root,"EditPlay",Symbol.Play,"",49,46,
            "Terminar la edición y volver al restaurante",
            HandleEditModeClicked,false,false,EditChromeOlive);
    }

    void BuildEditModeBrand()
    {
        if(editModeBrandBlock==null)return;

        if(approvedArt==null)
        {
            TextAsset catalog=
                Resources.Load<TextAsset>(
                    "BistroBuilder/UI/TopBar/Parts/catalog");
            if(catalog!=null)
                approvedArt=
                    JsonUtility.FromJson<BistroBuilderTopBarArtCatalog>(
                        catalog.text);
        }

        if(approvedArt!=null &&
            approvedArt.entries!=null &&
            approvedArt.entries.Length>10)
        {
            editModeBrandArtwork=
                CreateApprovedArtwork(
                    editModeBrandBlock,
                    "EditBrandArtwork",
                    10);
            return;
        }

        // Fallback únicamente si el arte aprobado no puede resolverse.
        editModeBrandFallback=
            ChromeText(
                editModeBrandBlock,
                "EditBrandFallback",
                "BistroBuilder",
                24,
                new Color32(73,62,47,255));
        editModeBrandFallback.font=
            BistroBuilderTypography.Title;
        editModeBrandFallback.alignment=
            TextAlignmentOptions.Center;
        StretchChrome(
            editModeBrandFallback.rectTransform,
            4,2,4,2);
    }

    BistroBuilderTopBarPlate AddApprovedCellSurface(
        RectTransform host,
        bool selected=false)
    {
        if(host==null)return null;

        var go=NewUi("ApprovedCell",host);
        var plate=go.AddComponent<BistroBuilderTopBarPlate>();
        plate.Cell=true;
        plate.raycastTarget=false;
        StretchChrome(plate.rectTransform,0,0,0,0);
        plate.transform.SetAsFirstSibling();
        plate.State(0f,selected?1f:0f,0f);
        return plate;
    }

    void LayoutEditModeChrome()
    {
        if(editModeTopBar==null||shellRoot==null)return;

        ResolveApprovedTopBarMetrics(
            out float scale,
            out float height,
            out float margin,
            out float physicalWidth);

        editModeTopBar.anchorMin=new Vector2(0,1);
        editModeTopBar.anchorMax=new Vector2(1,1);
        editModeTopBar.pivot=new Vector2(.5f,1);
        editModeTopBar.anchoredPosition=
            new Vector2(0,-8f/scale);
        editModeTopBar.sizeDelta=
            new Vector2(-margin*2f,height);

        HorizontalLayoutGroup layout=
            editModeTopBar.GetComponent<HorizontalLayoutGroup>();
        if(layout!=null)
        {
            int horizontal=Mathf.Max(4,Mathf.RoundToInt(8f/scale));
            int vertical=Mathf.Max(3,Mathf.RoundToInt(6f/scale));
            layout.padding=
                new RectOffset(
                    horizontal,
                    horizontal,
                    vertical,
                    vertical);
            layout.spacing=
                Mathf.Max(2f,5f/scale);
        }

        bool medium=physicalWidth<1500f;
        bool compact=physicalWidth<1120f;
        bool veryCompact=physicalWidth<900f;

        float brandWidth=
            veryCompact?112f:
            compact?146f:
            medium?182f:
            221f;

        float modeWidth=
            veryCompact?142f:
            compact?174f:
            medium?220f:
            278f;

        LayoutEditBrand(
            brandWidth,
            50f,
            scale);

        LayoutEditModeIdentity(
            modeWidth,
            50f,
            scale,
            compact);

        LayoutEditTopIconButton("EditHome",44f,28f,scale);
        LayoutEditTopIconButton("EditUndo",46f,28f,scale);
        LayoutEditTopIconButton("EditRedo",46f,28f,scale);
        LayoutEditTopIconButton("EditPan",48f,28f,scale);
        LayoutEditTopIconButton("EditMove",44f,28f,scale);
        LayoutEditTopIconButton("EditGrid",44f,28f,scale);
        LayoutEditTopIconButton("EditTerrain",44f,28f,scale);
        LayoutEditTopIconButton("EditPaint",48f,28f,scale);
        LayoutEditTopIconButton("EditPlay",49f,28f,scale);

        SetChromeVisible(
            "EditPan",
            !medium);
        SetChromeVisible(
            "EditTerrain",
            !medium);
        SetChromeVisible(
            "EditPaint",
            physicalWidth>=1320f);

        // En anchuras compactas la acción Mover vive en el inspector derecho.
        if(compact)
            SetChromeVisible(
                "EditMove",
                false);

        if(editModeClockBlock!=null)
        {
            bool showClock=
                physicalWidth>=1260f;

            editModeClockBlock.gameObject.SetActive(
                showClock);

            if(showClock)
                LayoutEditClock(
                    scale);
        }

        if(editModeMoneyText!=null)
        {
            float moneyWidth=
                veryCompact?96f:
                compact?110f:
                135f;

            ChromeResize(
                editModeMoneyText.gameObject,
                moneyWidth/scale,
                46f/scale);

            editModeMoneyText.fontSize=
                Mathf.Clamp(
                    20f/scale,
                    18f,
                    28f);
        }

        // La barra inferior sigue siendo funcional; solo ajustamos el bloque
        // informativo para no competir con acciones en resoluciones estrechas.
        Transform venue=
            editModeBottomBar!=null
                ? editModeBottomBar.Find("EditVenue")
                : null;

        if(venue!=null)
            venue.gameObject.SetActive(
                physicalWidth>=1540f);
    }

    void LayoutEditBrand(
        float physicalWidth,
        float physicalHeight,
        float scale)
    {
        if(editModeBrandBlock==null)return;

        ChromeResize(
            editModeBrandBlock.gameObject,
            physicalWidth/scale,
            physicalHeight/scale);

        if(editModeBrandArtwork!=null)
        {
            float availableW=
                Mathf.Max(
                    20f,
                    physicalWidth-12f);
            float availableH=
                Mathf.Max(
                    18f,
                    physicalHeight-8f);
            float artH=
                Mathf.Min(
                    availableH,
                    availableW/
                    Mathf.Max(.01f,editModeBrandArtwork.Aspect));
            float artW=
                artH*editModeBrandArtwork.Aspect;

            PlaceHeader(
                editModeBrandArtwork.rectTransform,
                (physicalWidth-artW)/(2f*scale),
                (physicalHeight-artH)/(2f*scale),
                artW/scale,
                artH/scale);
        }

        if(editModeBrandFallback!=null)
            editModeBrandFallback.fontSize=
                Mathf.Clamp(
                    22f/scale,
                    18f,
                    30f);
    }

    void LayoutEditModeIdentity(
        float physicalWidth,
        float physicalHeight,
        float scale,
        bool compact)
    {
        if(editModeModeBlock==null)return;

        ChromeResize(
            editModeModeBlock.gameObject,
            physicalWidth/scale,
            physicalHeight/scale);

        RectTransform pencil=
            editModeModeBlock.Find("Pencil")
                as RectTransform;

        if(pencil!=null)
        {
            float size=compact?24f:28f;
            ChromeBox(
                pencil,
                10f/scale,
                (physicalHeight-size)/(2f*scale),
                size/scale,
                size/scale);
        }

        float textLeft=
            compact?40f:46f;

        if(editModeModeTitleText!=null)
        {
            editModeModeTitleText.fontSize=
                Mathf.Clamp(
                    (compact?17f:19f)/scale,
                    15f,
                    27f);

            ChromeBox(
                editModeModeTitleText.rectTransform,
                textLeft/scale,
                (compact?10f:5f)/scale,
                Mathf.Max(
                    30f,
                    physicalWidth-textLeft-8f)/scale,
                (compact?30f:24f)/scale);
        }

        if(editModeModeSubtitleText!=null)
        {
            editModeModeSubtitleText.gameObject.SetActive(
                !compact);

            if(!compact)
            {
                editModeModeSubtitleText.fontSize=
                    Mathf.Clamp(
                        11f/scale,
                        10f,
                        17f);

                ChromeBox(
                    editModeModeSubtitleText.rectTransform,
                    textLeft/scale,
                    27f/scale,
                    Mathf.Max(
                        30f,
                        physicalWidth-textLeft-8f)/scale,
                    18f/scale);
            }
        }
    }

    void LayoutEditClock(float scale)
    {
        if(editModeClockBlock==null)return;

        ChromeResize(
            editModeClockBlock.gameObject,
            126f/scale,
            46f/scale);

        RectTransform sun=
            editModeClockBlock.Find("Sun")
                as RectTransform;

        if(sun!=null)
            ChromeBox(
                sun,
                5f/scale,
                8f/scale,
                30f/scale,
                30f/scale);

        if(editModeClockText!=null)
        {
            editModeClockText.fontSize=
                Mathf.Clamp(
                    15f/scale,
                    13f,
                    22f);

            ChromeBox(
                editModeClockText.rectTransform,
                39f/scale,
                0,
                82f/scale,
                46f/scale);
        }
    }

    void LayoutEditTopIconButton(
        string key,
        float physicalWidth,
        float physicalIconSize,
        float scale)
    {
        if(!editChromeButtons.TryGetValue(
                key,
                out Button button)||
            button==null)
        {
            return;
        }

        ChromeResize(
            button.gameObject,
            physicalWidth/scale,
            46f/scale);

        RectTransform icon=
            button.transform.Find("Icon")
                as RectTransform;

        if(icon==null)return;

        icon.anchorMin=
            icon.anchorMax=
            icon.pivot=
                new Vector2(.5f,.5f);
        icon.anchoredPosition=
            Vector2.zero;
        icon.sizeDelta=
            new Vector2(
                physicalIconSize/scale,
                physicalIconSize/scale);
    }

    static void ChromeResize(
        GameObject go,
        float width,
        float height)
    {
        if(go==null)return;

        LayoutElement element=
            go.GetComponent<LayoutElement>();

        if(element==null)
            element=
                go.AddComponent<LayoutElement>();

        element.minWidth=
            element.preferredWidth=
                Mathf.Max(1f,width);
        element.minHeight=
            element.preferredHeight=
                Mathf.Max(1f,height);
        element.flexibleWidth=0f;
        element.flexibleHeight=0f;
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

        // Diseño inicial y construcción comparten esta misma franja.
        // Los controles aparecen solo cuando su contexto existe; no se apilan
        // paneles flotantes sobre el viewport.
        ChromeButton(root,"EditInitialSave",Symbol.Save,"Guardar",136,58,
            "Guardar un punto de recuperación del diseño inicial",
            SaveInitialFromChrome,true);
        ChromeButton(root,"EditInitialContinue",Symbol.Play,"Validar y continuar",196,58,
            "Validar el restaurante y comenzar la partida",
            CompleteInitialFromChrome,true,false,EditChromeOlive);

        ChromeButton(root,"EditDiscardDraft",Symbol.Undo,"Descartar",132,58,
            "Descartar los cambios de construcción aún no aplicados",
            DiscardConstructionDraft,true);
        ChromeButton(root,"EditApplyDraft",Symbol.Confirm,"Aplicar cambios",162,58,
            "Aplicar los cambios de construcción pendientes",
            ApplyConstructionDraft,true,false,EditChromeOlive);

        ChromeButton(root,"EditDelete",Symbol.Delete,"Eliminar",127,58,"Eliminar la selección; aplica la devolución o coste indicado en el inspector",DeleteChromeSelection,true);
        ChromeButton(root,"EditRotate",Symbol.Rotate,"Rotar",118,58,"Girar artículo, pared o módulo. Paredes y módulos: 90° (R)",RotateChromeSelection,true);
        ChromeButton(root,"EditDuplicate",Symbol.Duplicate,"Duplicar",131,58,"Preparar otra unidad para colocar; se cobra al confirmar",DuplicateChromeSelection,true);

        SetChromeVisible("EditInitialSave",false);
        SetChromeVisible("EditInitialContinue",false);
        SetChromeVisible("EditDiscardDraft",false);
        SetChromeVisible("EditApplyDraft",false);
        SetChromeVisible("EditDelete",false);
        SetChromeVisible("EditRotate",false);
        SetChromeVisible("EditDuplicate",false);
    }
    string EditPlotDimensions()
    {
        var floor=GameObject.Find("Floor_Test");var renderer=floor!=null?floor.GetComponent<Renderer>():null;
        return renderer!=null?$"{renderer.bounds.size.x:0.#} × {renderer.bounds.size.z:0.#} m":"Modo Edición";
    }
    bool IsFurnitureTool()=>editModeConstructionTool==null||editModeConstructionTool.Mode==Mode.Furniture;
    RestaurantPlaceableObject SelectedChromePlaceable()
    {var editable=editModeFurnitureController!=null?editModeFurnitureController.SelectedEditableObject:null;return editable!=null?editable.GetComponent<RestaurantPlaceableObject>():null;}
    void OpenEditOptionsFromChrome()
    {
        var options=
            GetComponent<BistroBuilderOptionsScreen>();

        if(options==null)
            return;

        options.Open();
        RefreshReadModels();
    }

    void SaveInitialFromChrome()
    {
        ResolveEditChrome();
        if(editConstructionPanel==null)return;
        editConstructionPanel.TryRequestInitialSaveFromChrome(out var message);
        ChromeMessage(message);
    }
    void CompleteInitialFromChrome()
    {
        ResolveEditChrome();
        if(editConstructionPanel==null)return;
        editConstructionPanel.TryCompleteInitialDesignFromChrome(out var message);
        ChromeMessage(message);
    }
    void ApplyConstructionDraft()
    {
        ResolveEditChrome();
        if(editConstructionPanel==null)return;
        editConstructionPanel.TryApplyDraftFromChrome(out var message);
        ChromeMessage(message);
    }
    void DiscardConstructionDraft()
    {
        ResolveEditChrome();
        if(editConstructionPanel==null)return;
        editConstructionPanel.TryDiscardDraftFromChrome(out var message);
        ChromeMessage(message);
    }
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
    void ChromeMessage(string message)
    {
        if(editModeToolStatusText==null||string.IsNullOrWhiteSpace(message))return;
        editModeToolStatusText.text=message;
        editModeToolStatusText.transform.parent.gameObject.SetActive(true);
        editChromeMessageUntil=Time.unscaledTime+2.8f;
    }
    void RefreshEditModeChrome(bool editing,bool managing)
    {
        EnsureEditModeChrome();
        bool visible=editing&&!managing;
        ReconcileOverlayVisibility();

        if(editModeTopBar!=null)
            editModeTopBar.gameObject.SetActive(
                visible);

        if(editModeBottomBar!=null)
            editModeBottomBar.gameObject.SetActive(
                visible);

        // Las pantallas de gestión conservan la navegación global incluso si se
        // abrieron desde Modo Edición. Al volver al viewport reaparece el chrome
        // de edición sin duplicar barras ni dejar al jugador sin navegación.
        if(topNavigation!=null)
            topNavigation.gameObject.SetActive(
                !editing||managing);

        if(bottomOperations!=null)
            bottomOperations.gameObject.SetActive(
                !editing);

        if(visible&&editModeTopBar!=null)
            editModeTopBar.SetAsLastSibling();
        else if(topNavigation!=null&&topNavigation.gameObject.activeSelf)
            topNavigation.SetAsLastSibling();

        if(!visible)
        {
            if(editModeToolStatusText!=null)
                editModeToolStatusText.transform.parent.gameObject.SetActive(false);
            return;
        }
        ResolveEditChrome();
        editModeMoneyText.text=finance!=null?BistroBuilderFinanceUiFormat.Money(finance.CurrentBalanceCents):"—";
        editModeClockText.text=EditChromeClock();
        var mode=editModeConstructionTool!=null?editModeConstructionTool.Mode:Mode.Furniture;
        var section=editModeCatalogPanel!=null?editModeCatalogPanel.CurrentSection:RestaurantEditCatalogSection.Build;
        bool furniture=mode==Mode.Furniture;

        string constructionStatus=
            !furniture&&editModeConstructionTool!=null
                ? editModeConstructionTool.StatusMessage
                : string.Empty;
        if(!string.IsNullOrWhiteSpace(constructionStatus)&&editModeToolStatusText!=null)
        {
            editModeToolStatusText.text=constructionStatus;
            editModeToolStatusText.transform.parent.gameObject.SetActive(true);
        }
        else if(editModeToolStatusText!=null&&Time.unscaledTime>editChromeMessageUntil)
        {
            editModeToolStatusText.transform.parent.gameObject.SetActive(false);
        }
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
        bool initial=editConstructionPanel!=null&&editConstructionPanel.IsInitialDesignPhase;
        bool draft=!furniture&&editConstructionPanel!=null&&editConstructionPanel.HasDraftChanges;

        editChromeButtons["EditMove"].interactable=furniture&&selected&&!placement;
        editChromeButtons["EditDelete"].interactable=!placement&&(furniture?SelectedChromePlaceable()!=null:structure);
        editChromeButtons["EditRotate"].interactable=furniture?(placement||selected):editModeConstructionTool!=null&&editModeConstructionTool.CanRotateArchitecture;
        editChromeButtons["EditDuplicate"].interactable=!placement&&(furniture?SelectedChromePlaceable()!=null:structure);
        editChromeButtons["EditUndo"].interactable=furniture?editChromeHistory!=null&&editChromeHistory.CanUndo:editModeConstructionTool!=null&&editModeConstructionTool.CanUndo;
        editChromeButtons["EditRedo"].interactable=furniture?editChromeHistory!=null&&editChromeHistory.CanRedo:editModeConstructionTool!=null&&editModeConstructionTool.CanRedo;

        // Una selección de mobiliario tiene una sola autoridad visible: el
        // inspector derecho. La franja inferior conserva acciones estructurales
        // y Rotar durante una colocación activa, cuando el inspector se compacta.
        bool unifiedFurnitureInspector=editPlaceableInspector!=null;
        SetChromeVisible(
            "EditMove",
            furniture&&selected&&!placement&&!unifiedFurnitureInspector);

        bool structureActions=
            !initial&&!furniture&&structure;

        SetChromeVisible(
            "EditDelete",
            structureActions);

        SetChromeVisible(
            "EditDuplicate",
            structureActions);

        SetChromeVisible(
            "EditRotate",
            !initial&&(
                structureActions&&editModeConstructionTool!=null&&editModeConstructionTool.CanRotateArchitecture ||
                furniture&&placement ||
                furniture&&selected&&!unifiedFurnitureInspector));

        SetChromeVisible("EditInitialSave",initial);
        SetChromeVisible("EditInitialContinue",initial);
        if(initial)
        {
            bool saveReady=editConstructionPanel!=null&&!editConstructionPanel.IsInitialSaveBusy;
            editChromeButtons["EditInitialSave"].interactable=saveReady;
            editChromeButtons["EditInitialContinue"].interactable=saveReady;
        }

        SetChromeVisible("EditDiscardDraft",!initial&&draft);
        SetChromeVisible("EditApplyDraft",!initial&&draft);
        if(!initial&&draft)
        {
            editChromeButtons["EditDiscardDraft"].interactable=true;
            editChromeButtons["EditApplyDraft"].interactable=true;
        }

        LayoutEditModeChrome();
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
    void SetChromeVisible(string key,bool visible)
    {
        if(editChromeButtons.TryGetValue(key,out var button)&&button!=null)
            button.gameObject.SetActive(visible);
    }
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
        var root=ChromeBlock(parent,key,width,height);
        var bg=root.gameObject.AddComponent<Image>();bg.sprite=EditChromeRoundedSprite();bg.type=Image.Type.Sliced;bg.color=Color.clear;

        bool topBarControl=
            editModeTopBar!=null&&
            parent==editModeTopBar;

        if(topBarControl)
            AddApprovedCellSurface(
                root,
                false);

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
    static void ChromeWidth(GameObject go,float width,float height){var e=go.GetComponent<LayoutElement>()??go.AddComponent<LayoutElement>();e.minWidth=e.preferredWidth=width;e.minHeight=e.preferredHeight=height;e.flexibleWidth=0;e.flexibleHeight=0;}
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
