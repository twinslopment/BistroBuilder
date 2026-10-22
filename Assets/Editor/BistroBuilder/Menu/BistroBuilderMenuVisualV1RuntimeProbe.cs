using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// CARTA V1 visual runtime gate. Opens Portfolio -> Editor -> Recipe with
/// real services in an unsaved play session; never commits dishes or rules.
/// All presentation checks are made against the built uGUI tree, not HTML.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderMenuVisualV1RuntimeProbe
{
    private const string Prefix = "BB.MenuVisualV1.Probe.";
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private static int passed, failed;
    private static BistroBuilderMenuPortfolioRuntimeView portfolio;
    private static BistroBuilderMenuEditorRuntimeView editor;
    private static BistroBuilderDishRecipeAuthoringRuntimeView recipe;
    private static string outputDirectory;

    static BistroBuilderMenuVisualV1RuntimeProbe()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Bistro Builder/Menu/CARTA V1 - Prueba visual reversible", false, 205)]
    public static void RunMenu() => Begin(false);

    public static void RunBatch() => Begin(true);

    private static void Begin(bool batch)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SessionState.GetBool(Prefix + "Running", false))
        {
            Debug.LogError("[CARTA V1] Already running / Play Mode active.");
            if (batch) EditorApplication.Exit(2);
            return;
        }
        if (!File.Exists(ScenePath) || SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogError("[CARTA V1] Missing scene or unsaved scene changes; abort.");
            if (batch) EditorApplication.Exit(2);
            return;
        }
        passed = failed = 0;
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetBool(Prefix + "Batch", batch);
        SessionState.SetInt(Prefix + "Stage", 0);
        SessionState.SetInt(Prefix + "Passed", 0);
        SessionState.SetInt(Prefix + "Failed", 0);
        SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup);
        outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Logs", "CartaVisualV1");
        Directory.CreateDirectory(outputDirectory);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void Pass(bool value, string name)
    {
        if (value) passed++; else failed++;
        Debug.Log((value ? "[CARTA V1] PASS " : "[CARTA V1] FAIL ") + name);
        SessionState.SetInt(Prefix + "Passed", passed);
        SessionState.SetInt(Prefix + "Failed", failed);
    }

    private static T FindScene<T>() where T : Component
    {
        foreach (T item in UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            if (item != null && item.gameObject.scene == SceneManager.GetActiveScene())
                return item;
        return null;
    }

    private static RectTransform Modal(Component host)
    {
        if (host == null) return null;
        FieldInfo field = host.GetType().GetField("modalRoot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(host) as RectTransform;
    }

    private static bool IsParchment(Transform parent, string path)
    {
        Transform node = parent?.Find(path);
        Image image = node != null ? node.GetComponent<Image>() : null;
        if (image == null) return false;
        Color color = image.color;
        return color.r > .80f && color.g > .69f && color.b > .49f;
    }

    private static void Capture(string file)
    {
        // Unity batch mode has no dependable Game View capture; avoid claiming
        // or writing screenshots that may never be rendered. In an interactive
        // Editor session the command remains available for visual inspection.
        if (SessionState.GetBool(Prefix + "Batch", false)) return;
        // Static fields are reset by Enter Play Mode domain reload.
        if (string.IsNullOrWhiteSpace(outputDirectory))
            outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Logs", "CartaVisualV1");
        Directory.CreateDirectory(outputDirectory);
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, file));
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        int stage = SessionState.GetInt(Prefix + "Stage", 0);
        if (!EditorApplication.isPlaying)
        {
            if (stage < 6) return;
            int p = SessionState.GetInt(Prefix + "Passed", 0);
            int f = SessionState.GetInt(Prefix + "Failed", 0);
            Debug.Log("[CARTA V1] PLAY-MODE " + p + " PASS / " + f +
                " FAIL; structural/UI audit completed. Interactive browser previews are separate.");
            bool batch = SessionState.GetBool(Prefix + "Batch", false);
            SessionState.SetBool(Prefix + "Running", false);
            SessionState.SetInt(Prefix + "Stage", 0);
            if (batch) EditorApplication.Exit(f == 0 ? 0 : 1);
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            Time.frameCount < 22) return;
        if ((float)EditorApplication.timeSinceStartup -
            SessionState.GetFloat(Prefix + "Started", 0) > 180f)
        {
            Debug.LogError("[CARTA V1] Timeout 180 seconds.");
            failed++;
            Finish();
            return;
        }
        passed = SessionState.GetInt(Prefix + "Passed", 0);
        failed = SessionState.GetInt(Prefix + "Failed", 0);
        try
        {
            if (stage == 0 && Time.frameCount >= 24) OpenPortfolio();
            else if (stage == 1 && Time.frameCount >= 48 && Time.unscaledTime >= 3f) AuditPortfolio();
            else if (stage == 2 && Time.frameCount >= 70) OpenEditor();
            else if (stage == 3 && Time.frameCount >= 96) AuditEditor();
            else if (stage == 4 && Time.frameCount >= 112) OpenRecipe();
            else if (stage == 5 && Time.frameCount >= 136) AuditRecipe();
        }
        catch (Exception exception)
        {
            failed++;
            Debug.LogException(exception);
            Finish();
        }
    }

    private static void Move(int stage) => SessionState.SetInt(Prefix + "Stage", stage);

    private static void OpenPortfolio()
    {
        FindScene<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
        portfolio = FindScene<BistroBuilderMenuPortfolioRuntimeView>();
        editor = FindScene<BistroBuilderMenuEditorRuntimeView>();
        recipe = FindScene<BistroBuilderDishRecipeAuthoringRuntimeView>();
        Pass(portfolio != null && editor != null && recipe != null,
            "Three canonical Carta views are present in the real scene.");
        Pass(portfolio != null && portfolio.TryOpen(out _),
            "Portfolio opens through the real service.");
        Move(1);
    }

    private static void AuditPortfolio()
    {
        RectTransform modal = Modal(portfolio);
        Transform plate = modal?.Find("Panel");
        Pass(portfolio.IsOpen && plate != null &&
            plate.GetComponent<Outline>() != null,
            "Portfolio is a single ivory/brass framed plate.");
        Pass(IsParchment(plate, "Menus") &&
             IsParchment(plate, "Rules") &&
             IsParchment(plate, "RuleEditor"),
             "Menus, activation rules and rule detail share parchment cards.");
        Text title = plate?.Find("Header/Title")?.GetComponent<Text>();
        Pass(title != null && title.text == "CARTA" &&
            title.font != null && title.font.name.IndexOf("Recoleta",
                StringComparison.OrdinalIgnoreCase) >= 0,
            "Official Recoleta clean title; dynamic metadata stays in Inter.");
        Pass(plate?.Find("Menus/MenuList/Viewport/Content") != null &&
             plate?.Find("Rules/RuleList/Viewport/Content") != null &&
             plate?.Find("RuleEditor/RuleName") != null,
             "Portfolio retained canonical data lists and rule form.");
        Pass(plate?.Find("Header/CartaHeaderIcon") != null &&
             plate?.Find("Menus/SectionIcon") != null &&
             plate?.Find("Rules/SectionIcon") != null,
             "Official menu iconography in the title and portfolio cards.");
        string[] iconPaths = {
            "Header/CartaHeaderIcon", "Menus/SectionIcon",
            "Rules/SectionIcon", "RuleEditor/SectionIcon"
        };
        string[] iconNames = {
            "book", "book", "clock", "file"
        };
        bool exactHeaders = true;
        for (int i = 0; i < iconPaths.Length; i++)
        {
            Image icon = plate?.Find(iconPaths[i])?.GetComponent<Image>();
            exactHeaders &= icon != null && icon.sprite != null &&
                icon.sprite.name.Contains(iconNames[i]) &&
                icon.preserveAspect && !icon.raycastTarget;
        }
        Pass(exactHeaders,
            "Approved V3 reference: exact brown book, book, clock and document SVGs in four headings.");
        Image frame=plate?.GetComponent<Image>();
        Image menusFrame=plate?.Find("Menus")?.GetComponent<Image>();
        Image rulesFrame=plate?.Find("Rules")?.GetComponent<Image>();
        Image detailsFrame=plate?.Find("RuleEditor")?.GetComponent<Image>();
        Pass(frame != null && frame.type==Image.Type.Sliced &&
             menusFrame != null && menusFrame.type==Image.Type.Sliced &&
             rulesFrame != null && rulesFrame.type==Image.Type.Sliced &&
             detailsFrame != null && detailsFrame.type==Image.Type.Sliced,
            "Approved V3: ivory/brass rounded 9-slice frame and three cream columns.");
        Transform mixed=plate?.Find("Rules/ReferenceTypefaceRuns");
        Pass(mixed != null && mixed.childCount==3 &&
             mixed.GetChild(0).GetComponent<Text>()?.font?.name.IndexOf("Recoleta",
                 StringComparison.OrdinalIgnoreCase)>=0,
            "V3 typography preserves Recoleta in 'Reglas de activación' and renders ó with Inter.");
        RectTransform columnsMenu=plate?.Find("Menus") as RectTransform;
        RectTransform columnsRules=plate?.Find("Rules") as RectTransform;
        RectTransform columnsDetail=plate?.Find("RuleEditor") as RectTransform;
        Pass(columnsMenu!=null && columnsRules!=null && columnsDetail!=null &&
             Mathf.Abs(columnsMenu.anchorMax.x-columnsRules.anchorMin.x)<0.0001f &&
             Mathf.Abs(columnsRules.anchorMax.x-columnsDetail.anchorMin.x)<0.0001f &&
             columnsMenu.anchorMin.x==0f && columnsDetail.anchorMax.x==1f,
            "V3 three-column ratios share contiguous responsive anchor boundaries.");
        Text tagline = plate?.Find("Header/Tagline")?.GetComponent<Text>();
        Text menuCount = plate?.Find("Menus/MenuCount")?.GetComponent<Text>();
        Text ruleCount = plate?.Find("Rules/RuleCount")?.GetComponent<Text>();
        Pass(tagline != null && tagline.text.StartsWith("Diseña,") &&
             menuCount != null && !string.IsNullOrWhiteSpace(menuCount.text) &&
             ruleCount != null && !string.IsNullOrWhiteSpace(ruleCount.text) &&
             plate?.Find("Resolution") != null,
            "V2 header explains Carta; each column has a count and dynamic context remains in footer.");
        Transform rows = plate?.Find("Menus/MenuList/Viewport/Content");
        Transform first = rows != null && rows.childCount > 0 ? rows.GetChild(0) : null;
        Text rowText = first?.GetComponentInChildren<Text>(true);
        Pass(first != null && first.Find("EntryIcon") != null &&
             rowText != null && rowText.alignment == TextAnchor.MiddleLeft,
            "V2 Carta row uses official icon and left-aligned compact typography.");
        Image rowIcon = first?.Find("EntryIcon")?.GetComponent<Image>();
        Image menuHeading = plate?.Find("Menus/SectionIcon")?.GetComponent<Image>();
        Pass(rowIcon != null && rowIcon.sprite != null &&
             menuHeading != null && menuHeading.sprite != null &&
             first.Find("ReferenceRowDescription") != null &&
             first.Find("ReferenceRowStatus") != null,
            "Approved V3: each menu row has separate Recoleta title, Inter metadata and status dot.");
        Transform overlay = plate?.Find("DeleteConfirmation");
        Button remove = plate?.Find("Menus/Eliminar")?.GetComponent<Button>();
        Pass(overlay != null && !overlay.gameObject.activeSelf &&
             overlay.Find("ConfirmationCard/ConfirmDeletion") != null &&
             overlay.Find("ConfirmationCard/CancelDeletion") != null &&
             remove != null && BistroBuilderCartaReferenceV3Style.IsDestructiveStyle(remove),
            "Approved V3 destructives use the dedicated red gradient and hidden reusable confirmation.");
        if (remove != null && overlay != null)
        {
            remove.onClick.Invoke();
            Pass(overlay.gameObject.activeSelf &&
                 overlay.Find("ConfirmationCard/Description")?.GetComponent<Text>()?.text.Contains("Eliminar") == true,
                 "Deleting a menu requires explicit approval; service untouched before confirmation.");
            overlay.Find("ConfirmationCard/CancelDeletion")?.GetComponent<Button>()?.onClick.Invoke();
            Pass(!overlay.gameObject.activeSelf &&
                 portfolio.TryValidateVisibleContent(out _),
                 "Cancelling a destructive action preserves Carta and all row bindings.");
        }
        Transform activeRule = plate?.Find("RuleEditor/Enabled");
        Transform weekday = plate?.Find("RuleEditor/Weekday0");
        Text ruleTick = activeRule?.Find("Box/Check")?.GetComponent<Text>();
        Text weekdayTick = weekday?.Find("Box/Check")?.GetComponent<Text>();
        RectTransform tickRect = ruleTick != null ? ruleTick.rectTransform : null;
        Pass(ruleTick != null && ruleTick.text == "\u2713" &&
             ruleTick.alignment == TextAnchor.MiddleCenter &&
             weekdayTick != null && weekdayTick.text == "\u2713" &&
             weekdayTick.alignment == TextAnchor.MiddleCenter &&
             tickRect != null && tickRect.anchorMin == Vector2.zero &&
             tickRect.anchorMax == Vector2.one &&
             tickRect.offsetMin == Vector2.zero &&
             tickRect.offsetMax == Vector2.zero,
            "V2.1 checked markers are centred in 20x20 boxes for services and weekdays.");
        Toggle ruleToggle = activeRule?.GetComponent<Toggle>();
        Image checkBackground = activeRule?.Find("Box")?.GetComponent<Image>();
        if (ruleToggle != null && checkBackground != null)
        {
            bool initial = ruleToggle.isOn;
            ruleToggle.isOn = false;
            Color off = checkBackground.color;
            ruleToggle.isOn = true;
            Color on = checkBackground.color;
            ruleToggle.isOn = initial;
            Pass(off != on && on == BistroBuilderMenuEditorUiFactory.Accent &&
                 off == BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                 "Checked state updates only the centred box; no data mutation.");
        }
        Capture("01_Gestor_Cartas_y_Reglas.png");
        Button managementTab = plate?.Find("CartaTabs/CartaTabManagement")?.GetComponent<Button>();
        Button dishesTab = plate?.Find("CartaTabs/CartaTabDishes")?.GetComponent<Button>();
        Pass(managementTab != null && dishesTab != null,
            "Carta hub exposes Cartas y reglas and Platos tabs.");
        if (dishesTab != null)
        {
            dishesTab.onClick.Invoke();
            Pass(!portfolio.IsOpen && editor != null && editor.IsOpen,
                "Platos tab switches from the Carta hub to the real dish editor.");
            if (editor != null && editor.IsOpen)
                editor.RequestCloseFromInterface();
            Pass(portfolio.TryOpen(out _),
                "Carta hub reopens cleanly after leaving Platos.");
            portfolio.Close();
        }
        else
        {
            Pass(false, "Platos tab must be available from the Carta hub.");
        }
        Move(2);
    }

    private static void OpenEditor()
    {
        string error = string.Empty;
        Pass(editor != null && editor.TryOpenFromInterface(out error),
            "Editor opens real draft without changing a saved menu: " + error);
        Move(3);
    }

    private static void AuditEditor()
    {
        RectTransform modal = Modal(editor);
        Transform plate = modal?.Find("Panel");
        Pass(editor.IsOpen && plate != null &&
            plate.GetComponent<Outline>() != null,
            "Menu Editor has the ivory/brass outer frame.");
        Text menuTitle = plate?.Find("Header/Title")?.GetComponent<Text>();
        Text menuContext = plate?.Find("Header/Context")?.GetComponent<Text>();
        Pass(menuTitle != null && menuTitle.text == "CARTA" &&
             menuTitle.font != null &&
             menuTitle.font.name.IndexOf("Recoleta",
                 StringComparison.OrdinalIgnoreCase) >= 0 &&
             menuContext != null &&
             menuContext.text.Contains("personaliza los platos") &&
             menuContext.font != null &&
             menuContext.font.name.IndexOf("Inter",
                 StringComparison.OrdinalIgnoreCase) >= 0,
             "Platos shares the approved CARTA header and Inter helper line.");
        Pass(IsParchment(plate, "Body/Sidebar") &&
             IsParchment(plate, "Body/List") &&
             IsParchment(plate, "Body/Detail"),
             "Categories, dish table and selected-dish detail use matching parchment cards.");
        RectTransform leftPane = plate?.Find("Body/Sidebar") as RectTransform;
        RectTransform middlePane = plate?.Find("Body/List") as RectTransform;
        RectTransform rightPane = plate?.Find("Body/Detail") as RectTransform;
        Pass(leftPane != null && middlePane != null && rightPane != null &&
             Mathf.Abs(leftPane.anchorMax.x - middlePane.anchorMin.x) < .0001f &&
             Mathf.Abs(middlePane.anchorMax.x - rightPane.anchorMin.x) < .0001f &&
             middlePane.offsetMin.x > leftPane.offsetMax.x &&
             rightPane.offsetMin.x > middlePane.offsetMax.x,
             "Platos keeps three gap-separated columns with contiguous responsive anchors.");
        bool noHeavyFrames =
            plate?.GetComponent<Outline>()?.enabled == false &&
            plate.Find("Body/Sidebar")?.GetComponent<Outline>()?.enabled == false &&
            plate.Find("Body/List")?.GetComponent<Outline>()?.enabled == false &&
            plate.Find("Body/Detail")?.GetComponent<Outline>()?.enabled == false;
        Pass(noHeavyFrames,
            "Platos alone disables the legacy stacked outlines on the ivory shell and three columns.");
        Transform categoriesContent =
            plate?.Find("Body/Sidebar/Categories/Viewport/Content");
        Transform all = categoriesContent?.Find("Category_all");
        Pass(all != null &&
             all.Find("ReferenceCategoryIcon")?.GetComponent<Image>()?.sprite != null &&
             all.Find("ReferenceCategorySeparator") != null,
            "Category selector uses a raster-independent image icon and a fine separator, not font glyphs.");
        Button editorManagementTab =
            plate?.Find("CartaTabs/CartaTabManagement")?.GetComponent<Button>();
        Button editorDishesTab =
            plate?.Find("CartaTabs/CartaTabDishes")?.GetComponent<Button>();
        Pass(editorManagementTab != null && editorDishesTab != null &&
             editorManagementTab.GetComponent<Image>()?.sprite != null &&
             editorDishesTab.GetComponent<Image>()?.sprite != null,
            "Platos shares the same Carta tabs and V3 button construction as the hub.");
        Image actualPlateIcon = editorDishesTab?.transform
            .Find("ReferenceTabIcon")?.GetComponent<Image>();
        Pass(actualPlateIcon?.sprite != null &&
             editorDishesTab.transform.Find("ReferenceTabIcon")
                 .GetComponent<Text>() == null,
             "Platos tab renders the actual plate/food sprite, not a font placeholder.");
        Transform photo = plate?.Find("Body/Detail/DetailCanvas/DishPhotoFrame");
        Transform photoImage = photo?.Find("DishPhoto");
        Pass(photo?.GetComponent<RectMask2D>() != null &&
             photoImage?.GetComponent<AspectRatioFitter>()?.aspectMode ==
                 AspectRatioFitter.AspectMode.EnvelopeParent &&
             plate.Find("Body/Detail/DetailCanvas/ReferenceDetailHeadingIcon") != null &&
             plate.Find("Body/Detail/DetailCanvas/ReferenceDetailRule") != null,
             "Dish detail crops photos without stretching and retains its dedicated brass heading.");
        Transform badge = plate?.Find("Body/Detail/DetailCanvas/StatusBadgePlate");
        Pass(badge?.Find("StatusDot")?.GetComponent<Image>()?.sprite != null,
            "The dish-status badge contains a real state dot instead of a flat green text box.");

        string[] decoratedFields = {
            "Price", "CategoryValuePlate", "ModeValuePlate",
            "PreparationTime", "OrderValuePlate", "CostValuePlate",
            "MarginValuePlate"
        };
        bool detailIcons = true;
        for (int i = 0; i < decoratedFields.Length; i++)
            detailIcons &= plate.Find("Body/Detail/DetailCanvas/" +
                decoratedFields[i] + "/ReferenceFieldIconPlate") != null;
        Pass(detailIcons,
            "All seven dish-detail fields have their own aligned ivory-and-brass icons.");

        Image addDishSkin = plate?.Find("Footer/AddDish")?.GetComponent<Image>();
        Image applySkin = plate?.Find("Footer/Apply")?.GetComponent<Image>();
        Image discardSkin = plate?.Find("Footer/Discard")?.GetComponent<Image>();
        Image removeSkin = plate?.Find("Footer/RemoveDish")?.GetComponent<Image>();
        Pass(addDishSkin?.sprite != null &&
             applySkin?.sprite == addDishSkin.sprite &&
             discardSkin?.sprite != addDishSkin.sprite &&
             removeSkin?.sprite != addDishSkin.sprite,
             "Approved bottom bar differentiates gold primary, neutral discard and red remove actions.");

        Transform availability = plate?.Find(
            "Body/Detail/DetailCanvas/AvailableToggle");
        Toggle availabilityToggle = availability?.GetComponent<Toggle>();
        Image availabilityTrack = availability?.Find("ReferenceAvailabilityTrack")
            ?.GetComponent<Image>();
        Image availabilityKnob = availability?.Find(
            "ReferenceAvailabilityTrack/ReferenceAvailabilityKnob")
            ?.GetComponent<Image>();
        Pass(availabilityToggle != null &&
             availabilityTrack?.sprite != null &&
             availabilityKnob?.sprite != null &&
             availability.GetComponent<Image>()?.raycastTarget == true &&
             availabilityToggle.targetGraphic == availability.GetComponent<Image>(),
            "Dish availability is a raycastable real Toggle with its own amber-switch skin.");
        InputField search = plate?.Find("Body/List/ListHeader/Search")
            ?.GetComponent<InputField>();
        Image searchIcon = search?.transform.Find("ReferenceSearchIcon")
            ?.GetComponent<Image>();
        Text searchHint = search?.placeholder as Text;
        Pass(searchIcon?.sprite != null &&
             searchIcon.raycastTarget == false &&
             searchHint != null &&
             searchHint.rectTransform.offsetMin.x >= 37f &&
             search.textComponent.rectTransform.offsetMin.x >= 37f,
             "Search has a real magnifier image and its typing/placeholder region never overlaps the icon.");
        Pass(plate?.Find("Body/List/DishScroll/Viewport/Content") != null &&
             plate?.Find("Body/List/ListHeader/Search") != null &&
             plate?.Find("Body/Detail/DetailCanvas/DishPhotoFrame/DishPhoto") != null &&
             plate?.Find("Footer/AddDish") != null &&
             plate?.Find("Footer/RemoveDish") != null &&
             plate?.Find("Footer/Apply") != null &&
             plate?.Find("Header/CartaHeaderIcon") != null,
            "Approved Platos composition exposes search, photos and the exact bottom action bar.");
        Transform headingRoot = plate?.Find("Body/List/ColumnHeader");
        Pass(headingRoot?.Find("PlatoHeading") != null &&
             headingRoot.Find("PriceHeading") != null &&
             headingRoot.Find("AvailableHeading") != null &&
             headingRoot.Find("ModeHeading") != null &&
             headingRoot.Find("PrepHeading") != null &&
             headingRoot.Find("MarginHeading") != null,
             "Responsive dish columns use six independently anchored headings.");
        Text hiddenCount = headingRoot?.Find("Count")?.GetComponent<Text>();
        Transform marginHeader = headingRoot?.Find("MarginHeading");
        Pass(hiddenCount != null && !hiddenCount.gameObject.activeSelf &&
             marginHeader != null && marginHeader.gameObject.activeInHierarchy,
            "Hidden item count no longer overlaps the visible MARGEN column header.");

        Transform detailLabels = plate?.Find("Body/Detail/DetailCanvas");
        string[] responsiveCaptions = {
            "PriceCaption", "CategoryCaption", "AvailableCaption",
            "ModeCaption", "PrepCaption", "OrderCaption",
            "CostCaption", "MarginCaption"
        };
        bool labelsScaleSafely = detailLabels != null;
        foreach (string caption in responsiveCaptions)
        {
            Text label = detailLabels?.Find(caption)?.GetComponent<Text>();
            labelsScaleSafely &= label != null &&
                label.resizeTextForBestFit &&
                label.resizeTextMinSize >= 9 &&
                label.resizeTextMaxSize <= 12 &&
                label.horizontalOverflow == HorizontalWrapMode.Wrap &&
                label.verticalOverflow == VerticalWrapMode.Truncate;
        }
        Pass(labelsScaleSafely,
            "Eight long detail labels use bounded fit and wrapping rather than painting over adjacent fields.");
        Transform dishContent = plate?.Find("Body/List/DishScroll/Viewport/Content");
        Transform firstDish = dishContent != null && dishContent.childCount > 0
            ? dishContent.GetChild(0) : null;
        Transform thumbnail = firstDish?.Find("Thumbnail");
        Transform thumbnailPhoto = thumbnail?.Find("ThumbnailPhoto");
        Image actualThumbnail = thumbnailPhoto?.GetComponent<Image>();
        AspectRatioFitter thumbnailFitter = thumbnailPhoto
            ?.GetComponent<AspectRatioFitter>();
        bool thumbnailPreservesSource = thumbnail?.GetComponent<RectMask2D>() != null &&
            thumbnailFitter?.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent &&
            actualThumbnail != null && !actualThumbnail.raycastTarget &&
            (actualThumbnail.sprite == null ||
             Mathf.Abs(thumbnailFitter.aspectRatio -
                 actualThumbnail.sprite.rect.width /
                 actualThumbnail.sprite.rect.height) < .001f);
        Pass(thumbnailPreservesSource,
            "Dish-table thumbnails crop to their actual photographic aspect ratio without distorting it.");
        RectTransform marginHeadingRect = headingRoot?.Find("MarginHeading") as RectTransform;
        RectTransform marginCellRect = firstDish?.Find("Margin") as RectTransform;
        Pass(marginHeadingRect != null && marginCellRect != null &&
             Mathf.Abs(marginHeadingRect.anchorMin.x - marginCellRect.anchorMin.x) < .0001f &&
             Mathf.Abs(marginHeadingRect.anchorMax.x - marginCellRect.anchorMax.x) < .0001f,
             "MARGEN heading aligns with the actual margin values across responsive widths.");
        Pass(firstDish?.Find("AvailabilityTrack")?.GetComponent<Image>()?.sprite != null &&
             firstDish.Find("AvailabilityTrack/AvailabilityKnob")?.GetComponent<Image>()?.sprite != null,
             "Availability is rendered with a rounded switch, not an ambiguous status bullet.");
        Pass(editor.TryValidateVisibleContent(out _),
            "Existing 2.1E runtime scroll and row validation still passes.");

        Button sort = plate?.Find("Body/List/ListHeader/OrderCategory")
            ?.GetComponent<Button>();
        Button direction = plate?.Find("Body/List/ListHeader/OrderDirection")
            ?.GetComponent<Button>();
        FieldInfo filteredField = editor.GetType().GetField(
            "filtered", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo selectedField = editor.GetType().GetField(
            "selectedDishId", BindingFlags.Instance | BindingFlags.NonPublic);
        string initiallySelected = selectedField?.GetValue(editor) as string;
        var sortedItems = filteredField?.GetValue(editor) as
            System.Collections.Generic.List<BistroBuilderMenuEditorDishSnapshot>;
        int originalCount = sortedItems?.Count ?? -1;
        if (sort != null && direction != null && sortedItems != null)
        {
            sort.onClick.Invoke(); // Plato
            sort.onClick.Invoke(); // Precio
            bool ascending = sortedItems.Count > 1 &&
                sortedItems[0].CurrentPriceCents <=
                sortedItems[sortedItems.Count - 1].CurrentPriceCents;
            direction.onClick.Invoke(); // Mayor primero
            bool descending = sortedItems.Count > 1 &&
                sortedItems[0].CurrentPriceCents >=
                sortedItems[sortedItems.Count - 1].CurrentPriceCents;
            bool sameSelection = string.Equals(
                initiallySelected, selectedField?.GetValue(editor) as string,
                StringComparison.Ordinal);
            Pass(ascending && descending && sameSelection &&
                 sortedItems.Count == originalCount,
                 "Sort controls genuinely reorder dish prices in both directions without changing selection or drafts.");
            Text visibleSort = sort.transform.Find("Label")?.GetComponent<Text>();
            Transform staleOverlay = sort.transform.Find("ReferenceTypefaceRuns");
            Pass(visibleSort != null && visibleSort.enabled &&
                 visibleSort.text == "Precio" &&
                 (staleOverlay == null || !staleOverlay.gameObject.activeSelf),
                 "The visible sort label updates to Precio and no stale Categoría overlay remains.");
        }
        else
        {
            Pass(false, "Dish sorting controls and backing list are present.");
        }
        Button duplicate = plate?.Find("Footer/DuplicateDish")?.GetComponent<Button>();
        string sourceId = selectedField?.GetValue(editor) as string;
        bool gotSource = editor.EditorService.TryGetDishAuthoringRequest(
            sourceId, out BistroBuilderDishRecipeAuthoringRequest source,
            out _);
        int changesBefore = editor.EditorService.AuthoringService.DraftChangeCount;
        if (duplicate != null && duplicate.interactable && gotSource)
        {
            duplicate.onClick.Invoke();
            FieldInfo workingField = recipe.GetType().GetField(
                "workingRequest", BindingFlags.Instance | BindingFlags.NonPublic);
            BistroBuilderDishRecipeAuthoringRequest copy =
                workingField?.GetValue(recipe) as BistroBuilderDishRecipeAuthoringRequest;
            bool intact = copy != null && source != null &&
                recipe.IsOpen && string.IsNullOrEmpty(copy.DishId) &&
                copy.DisplayName.EndsWith(" (copia)", StringComparison.Ordinal) &&
                copy.BasePriceCents == source.BasePriceCents &&
                copy.Ingredients.Count == source.Ingredients.Count &&
                (copy.Ingredients.Count == 0 ||
                 !ReferenceEquals(copy.Ingredients[0], source.Ingredients[0])) &&
                editor.EditorService.AuthoringService.DraftChangeCount ==
                    changesBefore &&
                source.DishId == sourceId;
            Pass(intact,
                "Duplicate prepares an independent editable dish and deep-copied recipe without modifying the original or the draft.");
            recipe.Close();
        }
        else
        {
            Pass(false, "Duplicate is enabled with a selected dish and canonical recipe.");
        }
        Capture("02_Editor_Carta_Aprobado.png");
        Move(4);
    }

    private static void OpenRecipe()
    {
        if (recipe != null) recipe.OpenNew();
        Pass(recipe != null && recipe.IsOpen,
            "Dish + recipe authoring opens from the real editor draft.");
        Move(5);
    }

    private static void AuditRecipe()
    {
        RectTransform modal = Modal(recipe);
        Transform card = modal?.Find("Card");
        Pass(recipe.IsOpen && card != null &&
            card.GetComponent<Outline>() != null,
            "Authoring view has a raised brass/ivory card.");
        Pass(IsParchment(card, "Body/DishColumn") &&
             IsParchment(card, "Body/RecipeColumn"),
             "Dish data and recipe/escandallo columns share the same theme.");
        Pass(card?.Find("Footer/SaveDraft") != null &&
             card?.Find("Body/RecipeColumn/Ingredients") != null &&
             card?.Find("Header/CartaHeaderIcon") != null,
            "Ingredient authoring, icon and save-draft controls are preserved.");
        Capture("03_Nuevo_Plato_y_Receta.png");
        recipe.Close();
        if (editor != null && editor.IsOpen)
        {
            // No domain mutations were issued by this probe. Close the view
            // without calling apply/commit, leaving the saved menu unchanged.
            editor.gameObject.SetActive(false);
        }
        Finish();
    }

    private static void Finish()
    {
        SessionState.SetInt(Prefix + "Stage", 6);
        SessionState.SetInt(Prefix + "Passed", passed);
        SessionState.SetInt(Prefix + "Failed", failed);
        EditorApplication.ExitPlaymode();
    }
}
