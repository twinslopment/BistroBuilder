using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The single native Galería Viva presentation. Coordinates are authored in the
/// approved 557 x 730 full-screen reference and scale once with the actual UI canvas.
/// B9 remains the query/pooling authority; card commands still pass through B8/B10.
/// </summary>
[DisallowMultipleComponent, DefaultExecutionOrder(2550)]
public sealed class BistroBuilderGalleryViva : MonoBehaviour
{
    private const float Width = 557, Height = 730;
    private static readonly string[] CategoryNames = { "Todos", "Mesas", "Asientos", "Barra", "Cocina", "Almacenamiento", "Iluminación", "Decoración", "Exterior" };
    private static readonly int[] CategoryCodes = { -1, 0, 1, 2001, 4, 2002, 2, 3, 2003 };
    private static readonly string[] SortNames = { "relevancia", "precio menor", "precio mayor", "nombre" };
    private readonly HashSet<string> favorites = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> recents = new List<string>(12);
    private readonly List<RestaurantPlaceableItemDefinition> related = new List<RestaurantPlaceableItemDefinition>();
    private RestaurantPlaceableCatalogPanel panel;
    private RestaurantPlaceableInspectorPanel inspector;
    private RestaurantEditInteractionController interaction;
    private RestaurantPlaceableRegistry registry;
    private string lastWorldPrimary;
    private BistroBuilderEditorV2SelectionCoordinator worldSelection;
    private BistroBuilderEditorV2Coordinator coordinator;
    private BistroBuilderUiShell shell;
    private RectTransform topBar, bottomBar;
    private readonly Vector3[] barCorners = new Vector3[4];
    private RectTransform host, root, expanded, folded, filterPopup, orderPopup;
    private TMP_FontAsset recoleta, inter, semi;
    private TMP_InputField search, minimum, maximum;
    private TMP_Text allTitle, resultNote, featuredName, featuredDescription, featuredPrice, orderLabel, filterNotice;
    private Image featuredImage;
    private Image[] categoryImages, tabImages, scopeImages;
    private BistroBuilderGalleryVivaSymbol featuredHeart;
    private BistroBuilderGalleryVivaCard[] relatedCards;
    private Button detailsButton;
    private readonly Button[] relatedButtons = new Button[4];
    private readonly List<Button> placementButtons = new List<Button>();
    private int featuredIndex, relatedOffset, tab, sort;
    private int minPrice, maxPrice = int.MaxValue;
    private string selectedItemId;
    private bool collapsed, showing, lastPlacement;
    private RestaurantPlaceableEnvironmentScope scope;
    private RestaurantPlaceableItemDefinition featured;
    private RestaurantEditCatalogSection previousSection = (RestaurantEditCatalogSection)(-1);
    public RectTransform ItemContainer { get; private set; }
    public RestaurantPlaceableCatalogItemView ItemTemplate { get; private set; }
    public bool IsReady => root != null;
    public bool OwnsPresentation => IsReady && !RestaurantEditCatalogSections.IsArchitecture(panel.CurrentSection);
    public int SelectedTab => tab;
    public int FilteredCount => panel != null ? panel.GalleryResults.Count : 0;
    public RestaurantPlaceableItemDefinition Featured => featured;
    public bool IsCollapsed => collapsed;
    public TMP_InputField SearchField => search;
    public int RelatedCount => related.Count;

    private void Awake()
    {
        panel = GetComponent<RestaurantPlaceableCatalogPanel>();
        recoleta = BistroBuilderEditorV2ReferenceLayout.Heading;
        inter = BistroBuilderEditorV2ReferenceLayout.Body;
        semi = BistroBuilderEditorV2ReferenceLayout.BodyStrong;
        EnsureBuilt();
    }

    private void Start()
    {
        inspector = GetComponent<RestaurantPlaceableInspectorPanel>();
        interaction = FindFirstObjectByType<RestaurantEditInteractionController>(FindObjectsInactive.Include);
        worldSelection = FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>(FindObjectsInactive.Include);
        registry=FindFirstObjectByType<RestaurantPlaceableRegistry>(FindObjectsInactive.Include);
        coordinator = FindFirstObjectByType<BistroBuilderEditorV2Coordinator>(FindObjectsInactive.Include);
        shell = FindFirstObjectByType<BistroBuilderUiShell>(FindObjectsInactive.Include);
        var oldApproved = GetComponent<RestaurantPlaceableCatalogApprovedSkin>();
        var oldPreview = GetComponent<RestaurantPlaceableCatalogPreviewSkin>();
        var oldShowcase = GetComponent<BistroBuilderEditorV2GalleryShowcase>();
        var oldLayout = GetComponent<BistroBuilderEditorV2PremiumLayout>();
        if (oldApproved != null) oldApproved.enabled = false;
        if (oldPreview != null) oldPreview.enabled = false;
        if (oldShowcase != null) oldShowcase.enabled = false;
        if (oldLayout != null) oldLayout.enabled = false;
        panel.ItemSelected += Remember;
        panel.SectionChanged += OnSectionChanged;
        ApplyQuery();
    }

    private void OnDestroy()
    {
        if (panel == null) return;
        panel.ItemSelected -= Remember; panel.SectionChanged -= OnSectionChanged;
    }

    public void EnsureBuilt()
    {
        if (root != null) return;
        host = transform.Find("CatalogContent") as RectTransform;
        if (host == null) return;
        root = Node("BB_GaleriaViva_Native", host, 0, 0, Width, Height);
        root.gameObject.AddComponent<BistroBuilderEditChromeSurface>();
        expanded = Node("Expanded", root, 0, 0, Width, Height);
        var frame = Surface("IvoryFrame", expanded, 0, 0, Width, Height);
        BistroBuilderEditorV2BrassArt.DecorateReferenceFrame(frame, "Catalog");
        BistroBuilderApprovedGlyphsR4.Create(expanded,"FleurDeLis","fleur",20,12,39,45,BistroBuilderGalleryVivaArt.Brass);
        var title = Label("Title", expanded, "Catálogo", 71, 9, 417, 34, 27, true);
        title.overflowMode = TextOverflowModes.Overflow;
        Label("Subtitle", expanded, "Mobiliario y equipamiento para tu restaurante", 73, 43, 425, 18, 13, false, BistroBuilderGalleryVivaArt.Muted);
        SymbolButton("Collapse", expanded, 514, 12, 31, 33, BistroBuilderGalleryVivaSymbol.Shape.Close, () => SetCollapsed(true));
        search = Input("Search", expanded, "Buscar artículos…", 21, 65, 467, 38);
        Symbol("SearchIcon", search.transform, 12, 8, 20, 20, BistroBuilderGalleryVivaSymbol.Shape.Search);
        Place(search.textComponent.rectTransform, 42, 4, 402, 28);
        Place(((TMP_Text)search.placeholder).rectTransform, 42, 4, 402, 28);
        search.onValueChanged.AddListener(_ => ApplyQuery());
        SymbolButton("Filters", expanded, 495, 65, 48, 38, BistroBuilderGalleryVivaSymbol.Shape.Filter, ToggleFilters);

        // BB catalog options R8 begin
        tabImages = new Image[3];
        string[] tabs = { "Todos", "Favoritos", "Recientes" };
        string[] tabKeys = { "tab-all", "tab-favorites", "tab-recent" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int captured = i;
            var button = TextButton("Tab_" + tabs[i], expanded, tabs[i], 21 + i * 176, 110, 169, 38, () => SetTab(captured), 14);
            tabImages[i] = button.GetComponent<Image>();
            BistroBuilderCatalogOptionStyle.Create(button.transform, tabKeys[i], 20, 7, 24, 24);
            var caption = button.transform.Find("Caption").GetComponent<TMP_Text>();
            caption.font = semi ?? inter; caption.fontStyle = FontStyles.Normal;
            caption.fontSize = 14; caption.fontSizeMin = caption.fontSizeMax = 14; caption.enableAutoSizing = false;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            Place(caption.rectTransform, 54, 0, 108, 38);
        }

        categoryImages = new Image[CategoryNames.Length];
        string[] categoryKeys = { "category-all", "category-tables", "category-seating", "category-bar", "category-kitchen",
            "category-storage", "category-lighting", "category-decoration", "category-exterior" };
        for (int i = 0; i < CategoryNames.Length; i++)
        {
            int captured = i; float y = 155 + i * 46;
            var row = TextButton("Category_" + CategoryNames[i], expanded, CategoryNames[i], 13, y, 158, 45, () => SelectCategory(captured), 12.5f, false);
            categoryImages[i] = row.GetComponent<Image>();
            row.gameObject.AddComponent<BistroBuilderGalleryVivaCategoryFocus>();
            BistroBuilderCatalogOptionStyle.Create(row.transform, categoryKeys[i], 7, 6, 30, 32);
            var caption = row.transform.Find("Caption").GetComponent<TMP_Text>();
            caption.font = semi ?? inter; caption.fontStyle = FontStyles.Normal;
            caption.fontSize = 12.5f; caption.fontSizeMin = caption.fontSizeMax = 12.5f;
            caption.enableAutoSizing = false; caption.characterSpacing = 0;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            Place(caption.rectTransform, 44, 0, 110, 45);
            placementButtons.Add(row);
        }
        // BB catalog options R8 end

        RectTransform hero = Surface("Featured", expanded, 146, 155, 399, 193, false, true).rectTransform;
        var heroBackground = Node("BistroBackdrop", hero, 3, 3, 393, 187).gameObject.AddComponent<RawImage>();
        heroBackground.texture = Resources.Load<Texture2D>("BistroBuilder/UI/GalleryViva/BistroBackdropR2");
        heroBackground.raycastTarget = false;
        // A small engraved corner and double warm rim, without baked text or products.
        for (int i = 0; i < 4; i++) Symbol("Engraving" + i, hero, i % 2 == 0 ? 5 : 367, i < 2 ? 5 : 165, 17, 17, BistroBuilderGalleryVivaSymbol.Shape.Spark, new Color32(175, 118, 42, 255));
        featuredImage = Picture("CanonicalPreview", hero, 204, 14, 180, 169, null);
        featuredImage.preserveAspect = true;
        var featuredBadge = Surface("FeaturedBadge", hero, 20, 11, 110, 24, true);
        Label("Caption", featuredBadge.transform, "DESTACADO", 18, 0, 90, 24, 11.5f, true);
        Symbol("BadgeStar", featuredBadge.transform, 6, 7, 11, 11, BistroBuilderGalleryVivaSymbol.Shape.Bag);
        featuredName = Label("Name", hero, "", 44, 41, 153, 46, 22, true, new Color32(255, 250, 235, 255));
        featuredName.textWrappingMode = TextWrappingModes.Normal; featuredName.alignment=TextAlignmentOptions.TopLeft;
        featuredName.enableAutoSizing = true; featuredName.fontSizeMax = 22; featuredName.fontSizeMin = 14;
        featuredDescription = Label("Description", hero, "", 44, 91, 142, 58, 13, false, new Color32(239, 229, 211, 255));
        featuredDescription.textWrappingMode = TextWrappingModes.Normal; featuredDescription.alignment=TextAlignmentOptions.TopLeft;
        featuredPrice = Label("Price", hero, "", 22, 149, 74, 30, 21, true, new Color32(255, 244, 208, 255));
        featuredPrice.enableAutoSizing = true; featuredPrice.fontSizeMin = 11; featuredPrice.fontSizeMax = 21;
        detailsButton = TextButton("Details", hero, "Ver detalles", 95, 150, 121, 30, () => ShowDetails(featured), 13, true, true);
        Symbol("Arrow", detailsButton.transform, 95, 10, 12, 12, BistroBuilderGalleryVivaSymbol.Shape.Right);
        Place(detailsButton.transform.Find("Caption") as RectTransform, 4, 0, 91, 30);
        var favorite = SymbolButton("FeaturedFavorite", hero, 363, 7, 29, 28, BistroBuilderGalleryVivaSymbol.Shape.Heart, () => ToggleFavorite(featured), false);
        featuredHeart = favorite.GetComponentInChildren<BistroBuilderGalleryVivaSymbol>(); featuredHeart.color = new Color32(252, 247, 231, 255);
        CircleArrow("FeaturedPrevious", expanded, 140, 236, () => CycleFeatured(-1), false, 33);
        CircleArrow("FeaturedNext", expanded, 517, 236, () => CycleFeatured(1), true, 33);
        for (int i = 0; i < 5; i++)
        {
            int captured = i;
            var dot = TextButton("FeaturedDot_" + i, hero, "", 255 + i * 19, 174, 10, 10, () => ChooseFeatured(captured), 8, true);
            var dotImage=dot.GetComponent<Image>();dotImage.sprite=BistroBuilderGalleryVivaArt.Circle();dotImage.type=Image.Type.Simple;
            dotImage.color = new Color32(236, 226, 205, 255);
        }
        Label("RelatedHeading", expanded, "Artículos relacionados", 162, 353, 270, 29, 17, true);
        Symbol("RelatedSpark", expanded, 143, 359, 8, 11, BistroBuilderGalleryVivaSymbol.Shape.Spark, new Color32(201, 157, 81, 255));
        TextButton("SeeAll", expanded, "Ver todos", 451, 355, 90, 26, () => { ItemContainer.anchoredPosition = Vector2.zero; expanded.Find("ItemsScroll").GetComponent<ScrollRect>().verticalNormalizedPosition = 1; }, 12, false);
        relatedCards = new BistroBuilderGalleryVivaCard[4];
        for (int i = 0; i < 4; i++)
        {
            int captured = i;
            relatedCards[i] = CreateCard(expanded, "Related_" + i, 150 + 99 * i, 385, 94, 98, false);
            relatedButtons[i] = relatedCards[i].Button;
            relatedButtons[i].onClick.AddListener(() => ShowRelated(captured));
        }
        CircleArrow("RelatedPrevious", expanded, 143, 402, () => CycleRelated(-1), false, 25);
        CircleArrow("RelatedNext", expanded, 522, 402, () => CycleRelated(1), true, 25);
        Line("Separator", expanded, 146, 492, 399, 1, new Color32(224, 203, 174, 255));
        Symbol("AllSpark", expanded, 143, 501, 8, 11, BistroBuilderGalleryVivaSymbol.Shape.Spark, new Color32(201, 157, 81, 255));
        allTitle = Label("AllHeading", expanded, "Todos los artículos", 155, 494, 260, 28, 18, true);
        allTitle.enableAutoSizing = true; allTitle.fontSizeMax = 18; allTitle.fontSizeMin = 13;
        var orderButton = TextButton("Order", expanded, "", 416, 494, 129, 25, () => orderPopup.gameObject.SetActive(!orderPopup.gameObject.activeSelf), 10.5f);
        orderLabel = orderButton.transform.Find("Caption").GetComponent<TMP_Text>();
        Place(orderLabel.rectTransform, 5, 0, 101, 24);
        Symbol("Arrow", orderButton.transform, 108, 8, 10, 10, BistroBuilderGalleryVivaSymbol.Shape.Down);
        BuildScroll();
        resultNote = Label("EmptyState", expanded, "", 153, 546, 382, 135, 16, false, BistroBuilderGalleryVivaArt.Muted);
        resultNote.alignment = TextAlignmentOptions.Center; resultNote.textWrappingMode = TextWrappingModes.Normal;
        BuildFilters(); BuildOrder();
        folded = Surface("CollapsedRail", root, 0, 0, 48, Height).rectTransform;
        BistroBuilderApprovedGlyphsR4.Create(folded,"Logo","fleur",9,14,30,36,BistroBuilderGalleryVivaArt.Brass);
        SymbolButton("Expand", folded, 7, 64, 34, 36, BistroBuilderGalleryVivaSymbol.Shape.Right, () => SetCollapsed(false));
        folded.gameObject.SetActive(false);
        BistroBuilderCatalogOptionStyle.ApplyContentGeometry(expanded);
        HideLegacy(); RefreshGeometry();
    }

    private void BuildScroll()
    {
        var sr = Node("ItemsScroll", expanded, 146, 523, 399, 207).gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 28;
        var viewport = Node("Viewport", sr.transform, 0, 0, 399, 207);
        var shield = viewport.gameObject.AddComponent<Image>(); shield.color = Color.clear; shield.raycastTarget = true;
        shield.canvasRenderer.cullTransparentMesh = false;
        viewport.gameObject.AddComponent<RectMask2D>();
        ItemContainer = Node("Items", viewport, 0, 0, 399, 207);
        var grid = ItemContainer.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(94, 119); grid.spacing = new Vector2(6, 5); grid.padding = new RectOffset(1, 0, 0, 4);
        sr.viewport = viewport; sr.content = ItemContainer;
        BistroBuilderGalleryVivaScrollChrome.Install(sr);
        var template = CreateCard(ItemContainer, "NativeCardTemplate", 0, 0, 94, 117, true);
        ItemTemplate = template.gameObject.AddComponent<RestaurantPlaceableCatalogItemView>();
        ItemTemplate.ConfigureNativeButton(template.Button);
        template.gameObject.SetActive(false);
    }

    private void BuildFilters()
    {
        filterPopup = Surface("FiltersPopup", expanded, 145, 106, 378, 234).rectTransform;
        Label("Heading", filterPopup, "Filtrar artículos", 16, 7, 300, 35, 20, true);
        SymbolButton("Close", filterPopup, 340, 11, 26, 26, BistroBuilderGalleryVivaSymbol.Shape.Close, ToggleFilters);
        Label("Scope", filterPopup, "Ubicación", 16, 49, 310, 20, 13);
        string[] scopes = { "Todas", "Interior", "Exterior" }; scopeImages = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            var b = TextButton("Scope_" + i, filterPopup, scopes[i], 16 + i * 116, 75, 110, 31, () => SetScope((RestaurantPlaceableEnvironmentScope)captured), 12);
            scopeImages[i] = b.GetComponent<Image>();
        }
        Label("Price", filterPopup, "Precio (€)", 16, 116, 300, 20, 13);
        minimum = Input("MinimumPrice", filterPopup, "Desde", 16, 142, 166, 31);
        maximum = Input("MaximumPrice", filterPopup, "Hasta", 194, 142, 166, 31);
        minimum.contentType = maximum.contentType = TMP_InputField.ContentType.IntegerNumber;
        minimum.onEndEdit.AddListener(_ => ApplyPriceFields()); maximum.onEndEdit.AddListener(_ => ApplyPriceFields());
        filterNotice = Label("FilterNotice", filterPopup, "", 16, 179, 344, 18, 11, false, BistroBuilderGalleryVivaArt.Muted);
        TextButton("ResetFilters", filterPopup, "Limpiar filtros", 16, 201, 165, 25, ResetFilters, 12);
        TextButton("ApplyFilters", filterPopup, "Ver resultados", 194, 201, 166, 25, () => { if (ApplyPriceFields()) ToggleFilters(); }, 12, true, true);
        filterPopup.gameObject.SetActive(false);
    }

    private void BuildOrder()
    {
        orderPopup = Surface("OrderPopup", expanded, 367, 508, 157, 128).rectTransform;
        string[] captions = { "Relevancia", "Precio: menor primero", "Precio: mayor primero", "Nombre: A–Z" };
        for (int i = 0; i < captions.Length; i++)
        {
            int captured = i;
            TextButton("Sort_" + i, orderPopup, captions[i], 4, 4 + i * 30, 149, 29, () => { SetSort(captured); orderPopup.gameObject.SetActive(false); }, 11, false);
        }
        orderPopup.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!IsReady) { EnsureBuilt(); return; }
        showing = host.gameObject.activeInHierarchy && OwnsPresentation;
        if (root.gameObject.activeSelf != OwnsPresentation) root.gameObject.SetActive(OwnsPresentation);
        if (!showing) return;
        HideLegacy();
        if (previousSection != panel.CurrentSection) { previousSection = panel.CurrentSection; HideLegacy(); ApplyQuery(); }
        RefreshGeometry();
        // Highlight the same canonical object shown in B8, without turning a
        // world selection into a browsing/recents event or replacing B10's candidate.
        var primary=worldSelection!=null?worldSelection.PrimarySelection:default;
        if(primary.IsValid && primary.family==BistroBuilderEditorV2ToolFamily.Furniture &&
            (inspector==null||!inspector.IsReplacementPreview) && primary.stableId!=lastWorldPrimary &&
            registry!=null&&registry.TryGetByInstanceId(primary.stableId,out var selectedObject)) {
            lastWorldPrimary=primary.stableId;selectedItemId=selectedObject.ItemDefinition!=null?selectedObject.ItemDefinition.ItemId:null;
            panel.VirtualList?.RefreshStyledCards();
        }
        if(!primary.IsValid)lastWorldPrimary=null;
        if (coordinator != null && coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.None)
            panel.EnsureEditorV2DefaultAuthority();
        bool placement = interaction != null && interaction.HasActivePlacement;
        if (placement != lastPlacement)
        {
            lastPlacement = placement;
            foreach (var button in placementButtons) button.interactable = !placement;
            panel.VirtualList?.SetInteractable(!placement);
        }
        bool detailsAllowed = !placement && (worldSelection == null || worldSelection.SelectionCount == 0);
        detailsButton.interactable = detailsAllowed && featured != null;
        for (int i = 0; i < relatedButtons.Length; i++) relatedButtons[i].interactable = detailsAllowed && relatedCards[i].Definition != null;
    }

    private void RefreshGeometry()
    {
        if (host == null || !(host.parent is RectTransform parent)) return;
        if ((topBar == null || bottomBar == null) && shell != null)
        {
            var chrome = shell.transform.Find(BistroBuilderUiShell.RootName);
            if (chrome == null) chrome = shell.GetComponentInParent<Canvas>()?.transform.Find(BistroBuilderUiShell.RootName);
            topBar = chrome?.Find(BistroBuilderUiShell.EditModeTopBarName) as RectTransform;
            bottomBar = chrome?.Find(BistroBuilderUiShell.EditModeBottomBarName) as RectTransform;
        }
        float scale = BistroBuilderEditorV2ReferenceLayout.Scale(parent);
        float available = Height;
        host.anchorMin = host.anchorMax = host.pivot = new Vector2(0,1);
        host.anchoredPosition = new Vector2(9*scale,-92*scale);
        float galleryScale = 557f / Width;
        host.sizeDelta = new Vector2((collapsed ? 48 : Width)*galleryScale*scale,available*scale);
        root.localScale = Vector3.one * (scale*galleryScale);
        root.anchoredPosition = Vector2.zero; root.sizeDelta = new Vector2(Width,Height);
        expanded.sizeDelta = new Vector2(Width,available/galleryScale);
        expanded.Find("IvoryFrame").GetComponent<RectTransform>().sizeDelta = expanded.sizeDelta;
        var items = expanded.Find("ItemsScroll") as RectTransform;
        items.sizeDelta = new Vector2(366,Mathf.Max(80,available/galleryScale-523-14));
        BistroBuilderGalleryVivaScrollChrome.Layout(items.GetComponent<ScrollRect>());
        Place(expanded.Find("AllHeading") as RectTransform, 187, 494, 229, 29);
        allTitle.fontSizeMax = 16; allTitle.fontSizeMin = 13;
        Place(expanded.Find("Order") as RectTransform, 424, 494, 121, 25);
        Place(orderLabel.rectTransform, 4, 0, 100, 25);
        Place(expanded.Find("Order/Arrow") as RectTransform, 105, 8, 10, 10);
    }

    private void HideLegacy()
    {
        // Suppress late-created children too: the native gallery is the only
        // presentation allowed under CatalogContent, including after theme scans.
        for (int i = 0; i < host.childCount; i++)
        {
            var child = host.GetChild(i);
            if (child != root && child.gameObject.activeSelf) child.gameObject.SetActive(false);
        }
        var background = host.GetComponent<Image>(); if (background != null) background.enabled = false;
    }

    private void OnSectionChanged(RestaurantEditCatalogSection section)
    {
        if (!OwnsPresentation) return;
        HideLegacy(); SetCollapsed(false);
        var category = RestaurantEditCatalogSections.Category(section);
        panel.SelectGalleryCategory(category.HasValue ? (int)category.Value : -1);
        ApplyQuery();
    }

    public void SetCollapsed(bool value)
    {
        collapsed = value; expanded.gameObject.SetActive(!value); folded.gameObject.SetActive(value);
        RefreshGeometry();
    }

    public void SetTab(int value) { tab = Mathf.Clamp(value, 0, 2); featuredIndex = relatedOffset = 0; ApplyQuery(); }
    public void SetSort(int value) { sort = Mathf.Clamp(value, 0, 3); ApplyQuery(); }
    public void SetScope(RestaurantPlaceableEnvironmentScope value) { scope = value; ApplyQuery(); }
    public void SetPriceRange(int minimumPrice, int maximumPrice)
    {
        minPrice = Mathf.Max(0, minimumPrice); maxPrice = Mathf.Max(minPrice, maximumPrice); ApplyQuery();
    }
    public void SelectCategory(int index)
    {
        if (index < 0 || index >= CategoryCodes.Length) return;
        featuredIndex = relatedOffset = 0; panel.SelectGalleryCategory(CategoryCodes[index]);
    }
    public bool IsFavorite(RestaurantPlaceableItemDefinition item) => item != null && favorites.Contains(item.ItemId);
    public void ToggleFavorite(RestaurantPlaceableItemDefinition item)
    {
        if (item == null) return;
        if (!favorites.Add(item.ItemId)) favorites.Remove(item.ItemId);
        ApplyQuery(); panel.VirtualList?.RefreshStyledCards();
    }
    public void Remember(RestaurantPlaceableItemDefinition item)
    {
        if (item == null) return;
        selectedItemId = item.ItemId; recents.Remove(item.ItemId); recents.Insert(0, item.ItemId);
        if (recents.Count > 12) recents.RemoveAt(recents.Count - 1);
        if (tab == 2) ApplyQuery();
        panel.VirtualList?.RefreshStyledCards();
    }
    public void ResetFilters()
    {
        scope = RestaurantPlaceableEnvironmentScope.InteriorAndExterior; minPrice = 0; maxPrice = int.MaxValue; sort = 0; tab = 0;
        search.SetTextWithoutNotify(""); minimum.SetTextWithoutNotify(""); maximum.SetTextWithoutNotify(""); filterNotice.text = "";
        ApplyQuery();
    }
    private bool ApplyPriceFields()
    {
        int lo = 0, hi = int.MaxValue;
        if ((!string.IsNullOrWhiteSpace(minimum.text) && !int.TryParse(minimum.text, out lo)) ||
            (!string.IsNullOrWhiteSpace(maximum.text) && !int.TryParse(maximum.text, out hi)) || lo < 0 || hi < lo)
        { filterNotice.text = "Revisa el intervalo de precios."; return false; }
        filterNotice.text = ""; SetPriceRange(lo, hi); return true;
    }
    private void ToggleFilters() { filterPopup.gameObject.SetActive(!filterPopup.gameObject.activeSelf); orderPopup.gameObject.SetActive(false); }

    public void ApplyQuery()
    {
        if (!IsReady || panel.VirtualList == null || !panel.VirtualList.IsReady || !OwnsPresentation) return;
        panel.ApplyB9Query(search.text, scope, tab == 1, tab == 2, favorites, recents, sort, minPrice, maxPrice);
        var matches = panel.GalleryResults;
        resultNote.text = matches.Count == 0 ? (tab == 1 ? "Todavía no hay favoritos en esta selección.\nMarca un corazón para añadir un artículo." : tab == 2 ? "Todavía no has consultado artículos en esta selección." : "No hay artículos con estos filtros.\nPrueba otra búsqueda o limpia los filtros.") : "";
        resultNote.gameObject.SetActive(matches.Count == 0);
        int categoryIndex = Array.IndexOf(CategoryCodes, panel.SelectedCategoryCode);
        string category = categoryIndex > 0 ? CategoryNames[categoryIndex] : "";
        allTitle.text = "Todos los artículos" + (category.Length > 0 ? " — " + category : "");
        orderLabel.text = "Orden: " + SortNames[sort];
        for (int i = 0; i < tabImages.Length; i++)
        {
            tabImages[i].sprite = BistroBuilderGalleryVivaArt.Surface(i == tab);
            BistroBuilderCatalogOptionStyle.SetSelected(tabImages[i].transform, i == tab);
        }
        for (int i = 0; i < categoryImages.Length; i++)
        {
            categoryImages[i].enabled = true;
            categoryImages[i].GetComponent<BistroBuilderGalleryVivaCategoryFocus>().SetSelected(i == categoryIndex);
            BistroBuilderCatalogOptionStyle.SetSelected(categoryImages[i].transform, i == categoryIndex);
            var caption = categoryImages[i].transform.Find("Caption").GetComponent<TMP_Text>();
            caption.font = (semi ?? inter) ?? TMP_Settings.defaultFontAsset;
            caption.fontStyle = FontStyles.Normal;
            caption.fontSize = 12.5f; caption.fontSizeMin = caption.fontSizeMax = 12.5f;
            caption.enableAutoSizing = false;
        }
        for (int i = 0; i < scopeImages.Length; i++) scopeImages[i].sprite = BistroBuilderGalleryVivaArt.Surface(i == (int)scope);
        BindEditorial();
    }

    public void StyleCard(RestaurantPlaceableCatalogItemView view)
    {
        var card = view.GetComponent<BistroBuilderGalleryVivaCard>();
        if (card == null) return;
        card.Bind(view.Definition, this, string.Equals(selectedItemId, view.Definition?.ItemId, StringComparison.Ordinal));
    }

    private void BindEditorial()
    {
        var matches = panel.GalleryResults;
        if (matches.Count == 0) { featured = null; featuredName.text = "Sin resultados"; featuredDescription.text = "Ajusta los filtros para descubrir más artículos."; featuredPrice.text = ""; featuredImage.enabled = false; }
        else
        {
            featuredIndex = ((featuredIndex % matches.Count) + matches.Count) % matches.Count;
            featured = matches[featuredIndex];
            featuredName.text = featured.DisplayName; featuredDescription.text = featured.Description;
            // Published items without a price use a compact status label, rather
            // than a large monetary heading that can touch the details button.
            bool priced = featured.PurchasePrice > 0;
            featuredPrice.font = (priced ? recoleta : inter) ?? TMP_Settings.defaultFontAsset;
            featuredPrice.fontStyle = priced ? FontStyles.Bold : FontStyles.Normal;
            featuredPrice.fontSize = priced ? 21 : 12;
            featuredPrice.fontSizeMax = priced ? 21 : 12;
            featuredPrice.fontSizeMin = 10;
            featuredPrice.enableAutoSizing = true;
            featuredPrice.text = Price(featured); featuredImage.sprite = featured.InspectorPreview; featuredImage.enabled = featuredImage.sprite != null;
            featuredHeart.Filled = IsFavorite(featured); featuredHeart.SetVerticesDirty();
        }
        for(int i=0;i<5;i++) {
            var dot=expanded.Find("Featured/FeaturedDot_"+i).GetComponent<Image>();
            dot.color=i==featuredIndex%5 && featured!=null?Color.white:new Color32(168,151,130,255);
        }
        related.Clear();
        // Related respects the SAME query (including favorites/search/price).
        // Prefer the canonical finish family, then the actual item category.
        for (int pass = 0; pass < 2; pass++) for (int i = 0; i < matches.Count; i++)
        {
            var item = matches[i]; if (item == featured || featured == null || item.Category != featured.Category) continue;
            bool variant = featured.FinishProfile != null && item.FinishProfile == featured.FinishProfile;
            if ((pass == 0) != variant || related.Contains(item)) continue; related.Add(item);
        }
        for (int i = 0; i < 4; i++)
        {
            int index = relatedOffset + i;
            var item = index >= 0 && index < related.Count ? related[index] : null;
            relatedCards[i].gameObject.SetActive(item != null); relatedCards[i].Bind(item, this, false);
        }
    }
    private void CycleFeatured(int direction) { featuredIndex += direction; relatedOffset = 0; BindEditorial(); }
    private void ChooseFeatured(int index) { if (index < panel.GalleryResults.Count) { featuredIndex = index; BindEditorial(); } }
    private void CycleRelated(int direction) { relatedOffset = Mathf.Clamp(relatedOffset + direction, 0, Mathf.Max(0, related.Count - 4)); BindEditorial(); }
    private void ShowRelated(int index) { if (index >= 0 && index < relatedCards.Length) ShowDetails(relatedCards[index].Definition); }
    private void ShowDetails(RestaurantPlaceableItemDefinition item)
    {
        if (item == null || (interaction != null && interaction.HasActivePlacement) || (worldSelection != null && worldSelection.SelectionCount > 0)) return;
        Remember(item); if (inspector == null) inspector = GetComponent<RestaurantPlaceableInspectorPanel>(); inspector?.ShowForDefinition(item);
    }
    public static string Price(RestaurantPlaceableItemDefinition item) => item == null ? "" : item.PurchasePrice > 0 ? item.PurchasePrice.ToString("N0") + " €" : "Disponible";

    public BistroBuilderGalleryVivaCard CreateCard(Transform parent, string name, float x, float y, float w, float h, bool grid)
    {
        var rect = Surface(name, parent, x, y, w, h).rectTransform;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.transition = Selectable.Transition.ColorTint;
        var card = rect.gameObject.AddComponent<BistroBuilderGalleryVivaCard>();
        card.Configure(this, button, grid, w, h); return card;
    }
    public TMP_Text Label(string name, Transform parent, string text, float x, float y, float w, float h, float size, bool heading = false, Color? color = null)
    {
        var label = Node(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text; label.font = (heading ? recoleta : inter) ?? TMP_Settings.defaultFontAsset;
        label.fontStyle = FontStyles.Normal; // Approved Recoleta and actual Inter weights.
        label.fontSize = size; label.color = color ?? BistroBuilderGalleryVivaArt.Ink;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }
    public static RectTransform Node(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>(); rect.SetParent(parent, false); Place(rect, x, y, w, h); return rect;
    }
    public static void Place(RectTransform rect, float x, float y, float w, float h)
    { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
    public static Image Surface(string name, Transform parent, float x, float y, float w, float h, bool selected = false, bool dark = false)
    {
        var image = Node(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); image.sprite = BistroBuilderGalleryVivaArt.Surface(selected, dark); image.type = Image.Type.Sliced; image.canvasRenderer.cullTransparentMesh = false; return image;
    }
    public static Image Picture(string name, Transform parent, float x, float y, float w, float h, Sprite sprite)
    { var image = Node(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; image.enabled = sprite != null; return image; }
    public static BistroBuilderGalleryVivaSymbol Symbol(string name, Transform parent, float x, float y, float w, float h, BistroBuilderGalleryVivaSymbol.Shape kind, Color? color = null)
    { var icon = Node(name, parent, x, y, w, h).gameObject.AddComponent<BistroBuilderGalleryVivaSymbol>(); icon.Kind = kind; icon.color = color ?? BistroBuilderGalleryVivaArt.Brass; icon.raycastTarget = false; return icon; }
    private static void Line(string name, Transform parent, float x, float y, float w, float h, Color color)
    { var image = Node(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; }
    public Button TextButton(string name, Transform parent, string text, float x, float y, float w, float h, Action click, float size = 12, bool framed = true, bool selected = false)
    {
        var image = Surface(name, parent, x, y, w, h, selected); if (!framed) image.color = Color.clear;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color32(255, 236, 200, 255); colors.pressedColor = new Color32(240, 203, 141, 255); colors.selectedColor = new Color32(255, 235, 192, 255); colors.fadeDuration = .09f; button.colors = colors;
        if (click != null) button.onClick.AddListener(() => click());
        var label = Label("Caption", button.transform, text, 3, 0, w - 6, h, size); label.alignment = TextAlignmentOptions.Center;
        return button;
    }
    public Button SymbolButton(string name, Transform parent, float x, float y, float w, float h, BistroBuilderGalleryVivaSymbol.Shape symbol, Action click, bool framed = true)
    { var button = TextButton(name, parent, "", x, y, w, h, click, 12, framed); Symbol("Icon", button.transform, (w - 20) / 2, (h - 20) / 2, 20, 20, symbol); return button; }
    private void CircleArrow(string name, Transform parent, float x, float y, Action click, bool right, float size = 32)
    { var button = SymbolButton(name, parent, x, y, size, size, right ? BistroBuilderGalleryVivaSymbol.Shape.Right : BistroBuilderGalleryVivaSymbol.Shape.Left, click); button.GetComponent<Image>().sprite = BistroBuilderGalleryVivaArt.Circle(); button.GetComponent<Image>().type = Image.Type.Simple; }
    private TMP_InputField Input(string name, Transform parent, string placeholder, float x, float y, float w, float h)
    {
        var image = Surface(name, parent, x, y, w, h); var input = image.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = image;
        var area = Node("TextArea", input.transform, 0, 0, w, h); area.gameObject.AddComponent<RectMask2D>();
        var text = Label("Value", area, "", 11, 4, w - 22, h - 8, 13); text.richText = false;
        var hint = Label("Placeholder", area, placeholder, 11, 4, w - 22, h - 8, 13, false, BistroBuilderGalleryVivaArt.Muted);
        input.textViewport = area; input.textComponent = (TextMeshProUGUI)text; input.placeholder = hint; input.fontAsset = inter; input.pointSize = 13;
        return input;
    }
}

public sealed class BistroBuilderGalleryVivaCategoryFocus : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.ISelectHandler, UnityEngine.EventSystems.IDeselectHandler
{
    private bool selected, hovered, focused;
    public void SetSelected(bool value) { selected = value; Refresh(); }
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData _) { hovered = true; Refresh(); }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData _) { hovered = false; Refresh(); }
    public void OnSelect(UnityEngine.EventSystems.BaseEventData _) { focused = true; Refresh(); }
    public void OnDeselect(UnityEngine.EventSystems.BaseEventData _) { focused = false; Refresh(); }
    private void Refresh()
    {
        var image = GetComponent<Image>();
        image.sprite = BistroBuilderGalleryVivaArt.Surface(selected);
        image.color = selected || hovered || focused ? Color.white : Color.clear;
    }
}

/// <summary>One recycled card. Its favorite listener reads the current binding,
/// avoiding captured item IDs when B9 reuses this GameObject.</summary>
public sealed class BistroBuilderGalleryVivaCard : MonoBehaviour
{
    private BistroBuilderGalleryViva owner;
    private Image image, background;
    private TMP_Text nameLabel, priceLabel;
    private BistroBuilderGalleryVivaSymbol heart;
    private bool gridCard;
    public Button Button { get; private set; }
    public RestaurantPlaceableItemDefinition Definition { get; private set; }
    public void Configure(BistroBuilderGalleryViva gallery, Button button, bool grid, float w, float h)
    {
        owner = gallery; Button = button; background = GetComponent<Image>(); gridCard = grid;
        image = BistroBuilderGalleryViva.Picture("ItemImage", transform, grid?4:0, grid?4:0, grid?w-8:w, 61, null);
        nameLabel = gallery.Label("Name", transform, "", grid?9:7, grid?68:65, w-16, grid?25:11, grid?11:9.6f);
        nameLabel.textWrappingMode = grid?TextWrappingModes.Normal:TextWrappingModes.NoWrap;
        priceLabel = gallery.Label("Price", transform, "", grid?9:7, grid?100:78, w-16, 14, 12);
        var favorite = gallery.SymbolButton("Favorite", transform, w - 27, 2, 25, 25, BistroBuilderGalleryVivaSymbol.Shape.Heart, () => owner.ToggleFavorite(Definition), false);
        heart = favorite.GetComponentInChildren<BistroBuilderGalleryVivaSymbol>(); heart.color = new Color32(176, 111, 64, 255);
        LayoutCard();
    }
    public void SetCompactGridRow(bool compact)
    {
        if(image==null)return;
        LayoutCard(); // Uniform rows retain three lines and a separate price.
    }
    private void LayoutCard()
    {
        if (image == null) return;
        float w = ((RectTransform)transform).rect.width;
        BistroBuilderGalleryViva.Place(image.rectTransform, 4, 4, w-8, gridCard ? 49 : 43);
        if(Definition != null && Definition.Category == RestaurantPlaceableItemCategory.Seating)
        {
            image.rectTransform.pivot = new Vector2(.5f,.5f);
            image.rectTransform.anchoredPosition += new Vector2((w-8)*.5f,-(gridCard ? 49 : 43)*.5f);
        }
        BistroBuilderGalleryViva.Place(nameLabel.rectTransform, 3, gridCard ? 55 : 49, w-6, gridCard ? 40 : 29);
        nameLabel.fontSize = 10;
        nameLabel.textWrappingMode = TextWrappingModes.Normal;
        nameLabel.alignment = TextAlignmentOptions.TopLeft;
        BistroBuilderGalleryViva.Place(priceLabel.rectTransform, 7, gridCard ? 99 : 79, w-14, 18);
        priceLabel.fontSize = 11;
        BistroBuilderGalleryViva.Place(transform.Find("Favorite") as RectTransform, w-24, 2, 22, 22);
    }
    public void Bind(RestaurantPlaceableItemDefinition item, BistroBuilderGalleryViva gallery, bool selected)
    {
        if (nameLabel == null)
        {
            Button = GetComponent<Button>(); background = GetComponent<Image>();
            image = transform.Find("ItemImage").GetComponent<Image>();
            nameLabel = transform.Find("Name").GetComponent<TMP_Text>();
            priceLabel = transform.Find("Price").GetComponent<TMP_Text>();
            var favorite = transform.Find("Favorite").GetComponent<Button>();
            heart = favorite.GetComponentInChildren<BistroBuilderGalleryVivaSymbol>();
            favorite.onClick.RemoveAllListeners();
            favorite.onClick.AddListener(() => owner.ToggleFavorite(Definition));
        }
        owner = gallery; Definition = item; gridCard = GetComponent<RestaurantPlaceableCatalogItemView>() != null; LayoutCard();
        string cardName = item != null ? item.DisplayName : "";
        // The rail identifies Asientos already; retain the full published name in the inspector.
        if(item != null && item.Category == RestaurantPlaceableItemCategory.Seating && cardName.StartsWith("Silla ", StringComparison.OrdinalIgnoreCase))
        {
            cardName = cardName.Substring(6).Replace(" — ", "\n");
            if(cardName.Length>0)cardName=char.ToUpperInvariant(cardName[0])+cardName.Substring(1);
        }
        nameLabel.text = cardName; priceLabel.text = BistroBuilderGalleryViva.Price(item);
        image.sprite = item?.CatalogIcon; image.enabled = image.sprite != null;
        background.sprite = BistroBuilderGalleryVivaArt.CardSurface(selected);
        heart.Filled = gallery.IsFavorite(item); heart.SetVerticesDirty();
    }
}
