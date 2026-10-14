using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mode = BistroBuilderConstructionRuntimeMode;
using Symbol = BistroBuilderEditChromeSymbol;

/// <summary>
/// View-only Editor V2 chrome. Existing edit authorities own the actions:
/// Furniture, Construction, Surfaces, B5 renovation, B8 selection and history.
/// No game state is written by the view except through those actions.
/// </summary>
public sealed partial class BistroBuilderUiShell
{
    private TMP_Text editModeSelectionCountText;

    private RectTransform editV2ActionsMenu, editV2SelectionPill;
    private TMP_Text editV2VenueStatus, editV2DayLabel;
    private RectTransform editV2LightingMenu, editV2ViewMenu;
    private BistroBuilder.CameraSystem.BistroBuilderCameraViewService editV2Views;
    private Light editV2Sun;
    private Color editV2OriginalSunColor;
    private float editV2OriginalSunIntensity, editV2OriginalAmbient;
    private bool editV2LightPreview;
    private BistroBuilderNewGameOpeningPlayerScreen editV2OpeningScreen;
    private BistroBuilderNewGameOpeningService editV2Opening;
    private Button editV2InitialSave, editV2InitialContinue, editV2Fullscreen;


    private void BuildEditTopChromeV2(RectTransform root)
    {
        root.GetComponent<HorizontalLayoutGroup>().enabled = false;
        var logo = ChromeBlock(root, "OfficialLogo", 202, 74);
        ChromeBox(logo, 11, 0, 202, 74);
        var brand = logo.gameObject.AddComponent<Image>();
        brand.sprite = BistroBuilderEditorV2BrassArt.Brand;
        brand.preserveAspect = true; brand.raycastTarget = false;

        var badge = ChromeBlock(root, "EditorV2ModeIdentity", 213, 59);
        ChromeBox(badge, 230, 9, 212, 58);
        var badgeBg = badge.gameObject.AddComponent<Image>();
        BistroBuilderEditorV2BrassArt.Decorate(badgeBg, BistroBuilderEditorV2BrassArt.Style.Dark);
        var badgeButton = badge.gameObject.AddComponent<Button>(); badgeButton.targetGraphic = badgeBg;
        badgeButton.onClick.AddListener(() => editV2ActionsMenu.gameObject.SetActive(!editV2ActionsMenu.gameObject.activeSelf));
        ChromeIcon(badge, "EditingMark", Symbol.Tools, new Color32(255,241,208,255), new Vector2(38,29), 43);
        var badgeTitle = ChromeText(badge, "Title", "EDITOR V2", 22, new Color32(255,241,211,255));
        badgeTitle.font = BistroBuilderEditorV2ReferenceLayout.HeadingRegular; badgeTitle.fontStyle = FontStyles.Normal;
        ChromeBox(badgeTitle.rectTransform, 77, 4, 134, 31);
        var badgeSub = ChromeText(badge, "Subtitle", "MODO EDICIÓN", 12, new Color32(255,241,211,255));
        ChromeBox(badgeSub.rectTransform, 79, 32, 132, 22);

        var venue = ChromeBlock(root, "V2Venue", 314, 71);
        venue.anchorMin = venue.anchorMax = new Vector2(.5f,1); venue.pivot = new Vector2(.5f,1);
        venue.anchoredPosition = new Vector2(2,-1); venue.sizeDelta = new Vector2(314,71);
        BistroBuilderEditorV2BrassArt.DecorateReferenceFrame(venue.gameObject.AddComponent<Image>(), "Venue");
        var venueName = ChromeText(venue, "VenueName", "Restaurante", 20, EditChromeText);
        venueName.font = BistroBuilderEditorV2ReferenceLayout.Heading; venueName.fontStyle = FontStyles.Normal;
        venueName.alignment = TextAlignmentOptions.Center;
        ChromeBox(venueName.rectTransform, 15, 13, 271, 29);
        ChromeIcon(venue, "Pencil", Symbol.Pencil, EditChromeMuted, new Vector2(271,31), 20);
        editV2VenueStatus = ChromeText(venue, "RenovationStatus", "Modo edición", 12, EditChromeMuted);
        editV2VenueStatus.alignment = TextAlignmentOptions.Center;
        ChromeBox(editV2VenueStatus.rectTransform, 16, 41, 285, 20);

        var undo = ChromeButton(root,"EditUndo",Symbol.Undo,"",48,48,"Deshacer el último cambio",()=>
        {
            if(editChromeGlobalHistory!=null) { if(!editChromeGlobalHistory.TryUndo(out var error)) ChromeMessage(error); }
            else if(IsFurnitureTool()) editModeFurnitureController?.TryUndoLastPlacement();
            else editModeConstructionTool?.TryUndo(out _);
        });
        RightBox((RectTransform)undo.transform, 430,14,48,48);
        var redo = ChromeButton(root,"EditRedo",Symbol.Redo,"",48,48,"Rehacer el último cambio",()=>
        {
            if(editChromeGlobalHistory!=null) { if(!editChromeGlobalHistory.TryRedo(out var error)) ChromeMessage(error); }
            else if(IsFurnitureTool()) editModeFurnitureController?.TryRedoLastPlacement();
            else editModeConstructionTool?.TryRedo(out _);
        });
        RightBox((RectTransform)redo.transform,372,14,48,48);
        var day = ChromeButton(root,"EditDay",Symbol.Sun,"Día",155,48,"Previsualizar la iluminación sin cambiar la hora del restaurante",()=>editV2LightingMenu.gameObject.SetActive(!editV2LightingMenu.gameObject.activeSelf));
        RightBox((RectTransform)day.transform,171,14,155,48);
        // Bind the named text field. The first TMP descendant is the sun's
        // SDF glyph and must never receive the lighting preset's name.
        editV2DayLabel=day.transform.Find("Label").GetComponent<TMP_Text>();
        editV2DayLabel.fontSize=14;
        editV2DayLabel.enableAutoSizing=false;
        ChromeBox(editV2DayLabel.rectTransform,60,0,68,48);
        ChromeIcon(day.transform,"Chevron",Symbol.Down,EditChromeMuted,new Vector2(136,24),12);
        var dayDivider=ChromeBlock(day.transform,"Divider",1,30); ChromeBox(dayDivider,48,9,1,30);
        var dayLine=dayDivider.gameObject.AddComponent<Image>();dayLine.color=EditChromeLine;dayLine.raycastTarget=false;
        editV2LightingMenu=ChromeBlock(root,"LightingPreviewMenu",188,130);
        RightBox(editV2LightingMenu,171,84,188,130);
        BistroBuilderEditorV2BrassArt.Decorate(editV2LightingMenu.gameObject.AddComponent<Image>(),BistroBuilderEditorV2BrassArt.Style.Frame);
        string[] times={"Día","Atardecer","Noche"};
        for(int i=0;i<times.Length;i++) {
            int preset=i;
            var option=ChromeButton(editV2LightingMenu,"LightPreset"+i,Symbol.Sun,times[i],164,32,"Solo previsualización de luz",()=>SetEditorLightPreset(preset));
            ChromeBox((RectTransform)option.transform,12,12+i*36,164,32);
        }
        editV2LightingMenu.gameObject.SetActive(false);
        editModeClockText = ChromeText(day.transform,"Clock","",1,Color.clear);
        editModeClockText.gameObject.SetActive(false);
        var photo = ChromeButton(root,"EditPhoto",Symbol.Camera,"",51,48,"Guardar una captura",()=>
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath,"EditorV2_"+System.DateTime.Now.ToString("yyyyMMdd_HHmmss")+".png");
            ScreenCapture.CaptureScreenshot(path); ChromeMessage("Captura guardada: "+path);
        });
        RightBox((RectTransform)photo.transform,85,14,51,48);
        var options = ChromeButton(root,"EditOptions",Symbol.Settings,"",51,48,"Opciones y acciones de la reforma",()=>editV2ActionsMenu.gameObject.SetActive(!editV2ActionsMenu.gameObject.activeSelf));
        RightBox((RectTransform)options.transform,16,14,51,48);
        editModeMoneyText=ChromeText(root,"EditMoney","",1,Color.clear); editModeMoneyText.gameObject.SetActive(false);

        editV2ActionsMenu = ChromeBlock(root,"RenovationMenu",254,179);
        RightBox(editV2ActionsMenu,16,84,254,179);
        BistroBuilderEditorV2BrassArt.Decorate(editV2ActionsMenu.gameObject.AddComponent<Image>(),BistroBuilderEditorV2BrassArt.Style.Frame);
        var apply=ChromeButton(editV2ActionsMenu,"EditApply",Symbol.Play,"Aplicar reforma",230,45,"Aplicar la reforma completa",()=>{editV2ActionsMenu.gameObject.SetActive(false);RequestEditV2Confirmation(EditV2Confirmation.Apply);});
        ChromeBox((RectTransform)apply.transform,12,12,230,45);
        var discard=ChromeButton(editV2ActionsMenu,"EditDiscard",Symbol.Delete,"Descartar reforma",230,45,"Revisar antes de descartar",()=>{editV2ActionsMenu.gameObject.SetActive(false);RequestEditV2Confirmation(EditV2Confirmation.Discard);},true);
        ChromeBox((RectTransform)discard.transform,12,65,230,45);
        var exit=ChromeButton(editV2ActionsMenu,"EditPlay",Symbol.Home,"Volver al restaurante",230,45,"Salir protegiendo cambios pendientes",()=>{editV2ActionsMenu.gameObject.SetActive(false);RequestEditV2Confirmation(EditV2Confirmation.Exit);});
        ChromeBox((RectTransform)exit.transform,12,118,230,45);
        editV2ActionsMenu.gameObject.SetActive(false);

        editV2OpeningScreen=FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(FindObjectsInactive.Include);
        editV2Opening=FindFirstObjectByType<BistroBuilderNewGameOpeningService>(FindObjectsInactive.Include);
        editV2InitialSave=ChromeButton(editV2ActionsMenu,"InitialSave",Symbol.Camera,"Guardar recuperación",230,45,"Guardar el diseño inicial",()=>{
            if(editV2OpeningScreen!=null&&!editV2OpeningScreen.TrySaveInitialDesignFromEditor(out var error))ChromeMessage(error);
        });
        ChromeBox((RectTransform)editV2InitialSave.transform,12,171,230,45);
        editV2InitialContinue=ChromeButton(editV2ActionsMenu,"InitialContinue",Symbol.Play,"Validar y continuar",230,45,"Validar el restaurante y continuar",()=>{
            if(editV2OpeningScreen!=null&&!editV2OpeningScreen.TryValidateInitialDesignFromEditor(out var error))ChromeMessage(error);
            else editV2ActionsMenu.gameObject.SetActive(false);
        });
        ChromeBox((RectTransform)editV2InitialContinue.transform,12,224,230,45);
        editV2InitialSave.gameObject.SetActive(false);editV2InitialContinue.gameObject.SetActive(false);
        editV2Fullscreen=ChromeButton(editV2ActionsMenu,"EditFullscreen",Symbol.Plot,"Pantalla completa · F11",230,38,"Alternar pantalla completa a resolución nativa",BistroBuilderEditorV2ReviewDisplay.Toggle);
        ChromeBox((RectTransform)editV2Fullscreen.transform,12,171,230,38);

        editV2SelectionPill=ChromeBlock(root,"SelectionPill",280,43);
        editV2SelectionPill.anchorMin=editV2SelectionPill.anchorMax=new Vector2(.5f,1);
        editV2SelectionPill.pivot=new Vector2(.5f,1); editV2SelectionPill.anchoredPosition=new Vector2(134,-89);
        editV2SelectionPill.sizeDelta=new Vector2(280,43);
        BistroBuilderEditorV2BrassArt.Decorate(editV2SelectionPill.gameObject.AddComponent<Image>(),BistroBuilderEditorV2BrassArt.Style.Dark);
        ChromeIcon(editV2SelectionPill,"SelectionMark",Symbol.Duplicate,new Color32(248,212,110,255),new Vector2(33,21),25);
        editModeSelectionCountText=ChromeText(editV2SelectionPill,"Count","",14,Color.white);
        ChromeBox(editModeSelectionCountText.rectTransform,61,0,174,43);
        ChromeIcon(editV2SelectionPill,"Chevron",Symbol.Down,Color.white,new Vector2(249,21),13);
        var pillButton=editV2SelectionPill.gameObject.AddComponent<Button>();
        pillButton.onClick.AddListener(()=>editModeCatalogPanel?.GetComponent<BistroBuilderEditorV2SelectionInspector>()?.SetViewGroup(true));
        editV2SelectionPill.gameObject.SetActive(false);
        ChromeBox(undo.transform.Find("Icon") as RectTransform,8,8,31,33);
        ChromeBox(redo.transform.Find("Icon") as RectTransform,7,8,32,33);
        ChromeBox(day.transform.Find("Icon") as RectTransform,7,5,39,38);
        ChromeBox(photo.transform.Find("Icon") as RectTransform,8,8,33,33);
        ChromeBox(options.transform.Find("Icon") as RectTransform,7,7,34,35);
        ChromeBox(venue.Find("Pencil") as RectTransform,254,18,30,29);
    }

    private static void RightBox(RectTransform r,float right,float y,float w,float h)
    {
        r.anchorMin=r.anchorMax=new Vector2(1,1);r.pivot=new Vector2(1,1);
        r.anchoredPosition=new Vector2(-right,-y);r.sizeDelta=new Vector2(w,h);
    }

    private void BuildEditBottomChromeV2(RectTransform root)
    {
        root.GetComponent<HorizontalLayoutGroup>().enabled = false;
        var mode = EditV2Group(root, "ModeTools", "MODO");
        ChromeButton(mode, "EditSelect", Symbol.Cursor, "Seleccionar", 124, 64,
            "Seleccionar mobiliario o estructura",
            () =>
            {
                editModeFurnitureController?.CancelActivePlacement();
                var coordinator = FindFirstObjectByType<
                    BistroBuilderEditorV2Coordinator>(FindObjectsInactive.Include);
                if (coordinator != null)
                {
                    if (!coordinator.TryActivateTool(
                        BistroBuilderEditorV2ToolFamily.Furniture,
                        "select", out string error))
                        ChromeMessage(error);
                }
                else
                    editModeConstructionTool?.SetMode(Mode.Select);
            }, false, true);
        ChromeButton(mode, "EditBuild", Symbol.Chair, "Colocar", 116, 64,
            "Abrir Galería Viva para colocar mobiliario",
            () => editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Build),
            false, true);
        ChromeButton(mode, "EditWalls", Symbol.Hammer, "Construir", 120, 64,
            "Abrir Taller de construcción",
            () => editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Walls),
            false, true);
        ChromeButton(mode, "EditSurfaces", Symbol.Layers, "Superficies", 120,
            64, "Abrir Taller de superficies",
            () => editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Surfaces),
            false, true);

        var edit = EditV2Group(root, "EditSelectionTools", "EDITAR");
        ChromeButton(edit, "EditMove", Symbol.Move, "Mover", 116, 64,
            "Mover la selección si es compatible",
            () => editModeFurnitureController?.TryBeginMoveSelected(),
            false, true);
        ChromeButton(edit, "EditRotate", Symbol.Rotate, "Girar", 112, 64,
            "Girar la selección", RotateChromeSelection, false, true);
        ChromeButton(edit, "EditDuplicate", Symbol.Duplicate, "Duplicar", 118,
            64, "Duplicar la selección", DuplicateChromeSelection, false, true);
        ChromeButton(edit, "EditDelete", Symbol.Delete, "Eliminar", 116, 64,
            "Eliminar objetos seleccionados con las reglas reales",
            DeleteChromeSelection, true, true);

        var help = EditV2Group(root, "EditViewTools", "AYUDAS");
        ChromeButton(help, "EditSnap", Symbol.Magnet, "Snapping", 120, 64,
            "Ajuste contextual; no sustituye la validación",
            null, false, true);
        ChromeButton(help, "EditGrid", Symbol.Grid, "Cuadrícula", 116, 64,
            "Mostrar u ocultar la cuadrícula",
            () =>
            {
                if (editChromeGrid != null)
                    editChromeGrid.enabled = !editChromeGrid.enabled;
            }, false, true);
        ChromeButton(help, "EditViews", Symbol.Plot, "Vistas", 116, 64,
            "Elegir una vista del restaurante",
            () => editV2ViewMenu.gameObject.SetActive(!editV2ViewMenu.gameObject.activeSelf), false, true);
        editV2Views=FindFirstObjectByType<BistroBuilder.CameraSystem.BistroBuilderCameraViewService>();
        editV2ViewMenu=ChromeBlock(root,"ViewMenu",210,199);
        RightBox(editV2ViewMenu,16,-207,210,199);
        BistroBuilderEditorV2BrassArt.Decorate(editV2ViewMenu.gameObject.AddComponent<Image>(),BistroBuilderEditorV2BrassArt.Style.Frame);
        string[] views={"General","Isométrica","Planta","Detalle","Volver a vista libre"};
        for(int i=0;i<views.Length;i++) {
            int view=i;
            var option=ChromeButton(editV2ViewMenu,"ViewPreset"+i,Symbol.Plot,views[i],186,31,"Encuadre de cámara",()=>{
                bool changed=editV2Views!=null&&(view==4?editV2Views.TryRestorePreviousView():editV2Views.TryActivateView((BistroBuilder.CameraSystem.BistroBuilderCameraViewId)(view+1)));
                if(!changed)ChromeMessage("Esta vista no está disponible en la escena actual.");
                editV2ViewMenu.gameObject.SetActive(false);
            });
            ChromeBox((RectTransform)option.transform,12,12+i*35,186,31);
        }
        editV2ViewMenu.gameObject.SetActive(false);

        PlaceApprovedBottomButtons(root);
        // Safe before the first refresh: never expose invalid operations.
        foreach (string key in new[]
        {
            "EditMove", "EditRotate", "EditDuplicate", "EditDelete",
            "EditSnap", "EditViews", "EditApply", "EditDiscard",
            "EditUndo", "EditRedo"
        })
        {
            if (editChromeButtons.TryGetValue(key, out Button control))
                control.interactable = false;
        }
    }

    private static void PlaceApprovedBottomButtons(RectTransform root)
    {
        string[] paths={"ModeTools/Buttons/EditSelect","ModeTools/Buttons/EditBuild","ModeTools/Buttons/EditWalls","ModeTools/Buttons/EditSurfaces", "EditSelectionTools/Buttons/EditMove","EditSelectionTools/Buttons/EditRotate","EditSelectionTools/Buttons/EditDuplicate","EditSelectionTools/Buttons/EditDelete", "EditViewTools/Buttons/EditSnap","EditViewTools/Buttons/EditGrid","EditViewTools/Buttons/EditViews"};
        float[] x={60,198,323,453,39,160,282,409,42,173,300};
        float[] w={124,115,120,118,113,114,118,116,120,116,114};
        const float iconHeight=30;
        float[] iw={35,36,40,42,39,40,42,37,42,43,42};
        float[] ih={39,39,39,40,40,40,40,39,41,40,40};
        for(int i=0;i<paths.Length;i++) {
            var button=root.Find(paths[i]) as RectTransform;
            ChromeBox(button,x[i],22,w[i],54);
            float iconWidth=iw[i]*iconHeight/ih[i];
            ChromeBox(button.Find("Icon") as RectTransform,(w[i]-iconWidth)*.5f,3,iconWidth,iconHeight);
            var label=button.Find("Label").GetComponent<TMP_Text>();
            label.fontSize=13;
            ChromeBox(label.rectTransform,0,34,w[i],18);
            button.GetComponent<BistroBuilderEditChromeControl>().ConfigureToolbar();
        }
    }

    private void SetEditorLightPreset(int preset)
    {
        if(!editV2LightPreview) {
            editV2Sun=RenderSettings.sun;
            if(editV2Sun==null)foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional){editV2Sun=light;break;}
            if(editV2Sun==null){ChromeMessage("La escena no tiene una luz de día disponible.");return;}
            editV2OriginalSunColor=editV2Sun.color;editV2OriginalSunIntensity=editV2Sun.intensity;
            editV2OriginalAmbient=RenderSettings.ambientIntensity;editV2LightPreview=true;
        }
        editV2Sun.color=preset==0?editV2OriginalSunColor:preset==1?new Color(1,.68f,.38f):new Color(.38f,.50f,.78f);
        editV2Sun.intensity=editV2OriginalSunIntensity*(preset==0?1:preset==1?.65f:.18f);
        RenderSettings.ambientIntensity=editV2OriginalAmbient*(preset==0?1:preset==1?.75f:.35f);
        editV2DayLabel.text=preset==0?"Día":preset==1?"Atardecer":"Noche";
        editV2LightingMenu.gameObject.SetActive(false);
    }
    private void RestoreEditorLightPreview()
    {
        if(!editV2LightPreview)return;
        if(editV2Sun!=null){editV2Sun.color=editV2OriginalSunColor;editV2Sun.intensity=editV2OriginalSunIntensity;}
        RenderSettings.ambientIntensity=editV2OriginalAmbient;editV2LightPreview=false;
        if(editV2DayLabel!=null)editV2DayLabel.text="Día";
    }

    private Transform EditV2Group(Transform parent, string key, string title)
    {
        float width = key == "ModeTools" ? 611 : key == "EditSelectionTools" ? 570 : 475;
        float x = key == "ModeTools" ? 0 : key == "EditSelectionTools" ? 611 : 1181;
        var group = ChromeBlock(parent,key,width,BistroBuilderEditorV2ReferenceLayout.FooterHeight);
        ChromeBox(group,x,0,width,BistroBuilderEditorV2ReferenceLayout.FooterHeight);
        var caption=ChromeText(group,"Caption",title,14,EditChromeText);
        caption.font=BistroBuilderEditorV2ReferenceLayout.Heading; caption.fontStyle=FontStyles.Normal;
        caption.alignment=TextAlignmentOptions.Center;
        // Center each title in its existing ornamental gap, fully on the opaque paper.
        float captionCenter=key=="ModeTools"?310:key=="EditSelectionTools"?274.5f:250.5f;
        ChromeBox(caption.rectTransform,captionCenter-45,1,90,20);
        var row=NewUi("Buttons",group).GetComponent<RectTransform>();
        ChromeBox(row,0,0,width,BistroBuilderEditorV2ReferenceLayout.FooterHeight);
        // Explicit measured boxes; the single outer ornamental frame supplies the joins.
        return row;
    }

    private void UpdateEditChromeResponsiveV2()
    {
        if(shellRoot==null||editModeTopBar==null||editModeBottomBar==null)return;
        BistroBuilderEditorV2ReferenceLayout.Bar(editModeTopBar,true);
        BistroBuilderEditorV2ReferenceLayout.Bar(editModeBottomBar,false);
        BistroBuilderEditorV2BrassArt.FitFooter(editModeBottomBar);
        float width=editModeBottomBar.rect.width;
        float ratio=width/1656f;
        string[] names={"ModeTools","EditSelectionTools","EditViewTools"};
        float[] sizes={611,570,475}; float x=0;
        for(int i=0;i<3;i++) {
            var group=editModeBottomBar.Find(names[i]) as RectTransform;
            if(group==null)continue;
            ChromeBox(group,x,0,sizes[i],BistroBuilderEditorV2ReferenceLayout.FooterHeight);group.localScale=new Vector3(ratio,1,1);x+=sizes[i]*ratio;
        }
        int count=editModeSelectionCoordinator!=null?editModeSelectionCoordinator.SelectionCount:0;
        editV2SelectionPill.gameObject.SetActive(count>1);
        editV2VenueStatus.text=editChromeRenovation!=null&&editChromeRenovation.HasPendingChanges?"Reforma pendiente":"Modo edición";
        bool initial=editV2Opening!=null&&editV2Opening.IsInitialDesignPhase;
        editV2InitialSave.gameObject.SetActive(initial);editV2InitialContinue.gameObject.SetActive(initial);
        editV2ActionsMenu.sizeDelta=new Vector2(254,initial?330:224);
        ChromeBox((RectTransform)editV2Fullscreen.transform,12,initial?278:171,230,38);
        editV2InitialSave.interactable=editV2InitialContinue.interactable=initial&&!editV2Opening.IsSaveBusy;
    }
}
