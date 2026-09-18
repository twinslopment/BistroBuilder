using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RestaurantPlaceableCatalogApprovedSkin : MonoBehaviour
{
    private static readonly Color Panel = new Color32(251, 249, 244, 252);
    private static readonly Color Card = new Color32(255, 253, 250, 255);
    private static readonly Color Field = new Color32(243, 240, 233, 255);
    private static readonly Color Line = new Color32(222, 217, 207, 255);
    private static readonly Color TextPrimary = new Color32(24, 28, 29, 255);
    private static readonly Color TextMuted = new Color32(119, 123, 120, 255);
    private static readonly Color Olive = new Color32(113, 143, 77, 255);
    private static readonly Color OliveDark = new Color32(95, 123, 66, 255);
    private static readonly Color OliveSoft = new Color32(232, 238, 223, 255);
    private static readonly Color Gold = new Color32(237, 169, 34, 255);

    private RestaurantPlaceableCatalogPanel panel;
    private RestaurantEditInteractionController interactionController;
    private RestaurantPlaceableCatalogService catalogService;
    private GameObject contentRoot;
    private RectTransform categoryBar;
    private RectTransform itemContainer;
    private ScrollRect scrollRect;    private Font titleFont;
    private Font bodyFont;
    private Font bodySemiBold;
    private InputField searchInput;
    private GameObject scopeBar;
    private GameObject filterPanel;
    private GameObject placementStrip;
    private GameObject collapsedTab;
    private GameObject emptyState;
    private Text placementText;
    private Text countText;
    private Text summaryText;
    private Button filterButton;
    private Button scopeAllButton;
    private Button scopeInteriorButton;
    private Button scopeExteriorButton;

    private readonly HashSet<string> favorites = new HashSet<string>();
    private readonly List<string> recents = new List<string>(12);
    private readonly HashSet<int> hookedItemButtons = new HashSet<int>();
    private RestaurantPlaceableEnvironmentScope scopeFilter =
        RestaurantPlaceableEnvironmentScope.InteriorAndExterior;
    private int sortMode;
    private bool favoritesOnly;
    private bool recentsOnly;
    private bool filterOpen;
    private bool collapsed;
    private string activeItemId;
    private int lastItemChildCount = -1;
    private int lastCategoryChildCount = -1;    private void Awake()
    {
        CacheDependencies();
    }

    private void Start()
    {
        CacheDependencies();
        BuildApprovedChrome();
        ApplyStaticLayout();
        RefreshDynamicViews(true);
    }

    private void LateUpdate()
    {
        if (contentRoot == null)
        {
            CacheHierarchy();
            return;
        }

        if (itemContainer != null &&
            itemContainer.childCount != lastItemChildCount)
        {
            RefreshDynamicViews(true);
        }

        if (categoryBar != null &&
            categoryBar.childCount != lastCategoryChildCount)
        {
            RefreshDynamicViews(true);
        }

        RefreshPlacementStrip();
        ApplyCollapsedState();        if (itemContainer != null)
        {
            ApplyFiltersAndOrdering();
        }
    }

    private void CacheDependencies()
    {
        panel = panel != null
            ? panel
            : GetComponent<RestaurantPlaceableCatalogPanel>();
        interactionController = interactionController != null
            ? interactionController
            : FindFirstObjectByType<RestaurantEditInteractionController>();
        catalogService = catalogService != null
            ? catalogService
            : FindFirstObjectByType<RestaurantPlaceableCatalogService>();

        titleFont = titleFont != null
            ? titleFont
            : Resources.Load<Font>("BistroBuilder/UI/Typography/Recoleta");
        bodyFont = bodyFont != null
            ? bodyFont
            : Resources.Load<Font>("BistroBuilder/UI/Typography/Inter-Regular");
        bodySemiBold = bodySemiBold != null
            ? bodySemiBold
            : Resources.Load<Font>("BistroBuilder/UI/Typography/Inter-SemiBold");

        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleFont == null) titleFont = fallback;
        if (bodyFont == null) bodyFont = fallback;
        if (bodySemiBold == null) bodySemiBold = fallback;
        CacheHierarchy();
    }    private void CacheHierarchy()
    {
        Transform content = transform.Find("CatalogContent");
        if (content == null)
        {
            return;
        }

        contentRoot = content.gameObject;
        categoryBar = content.Find("CategoryBar") as RectTransform;
        itemContainer = content.Find("ItemsScroll/Viewport/Items") as RectTransform;
        Transform scroll = content.Find("ItemsScroll");
        scrollRect = scroll != null ? scroll.GetComponent<ScrollRect>() : null;
    }

    private void BuildApprovedChrome()
    {
        if (contentRoot == null ||
            contentRoot.transform.Find("ApprovedSearch") != null)
        {
            return;
        }

        Image rootImage = contentRoot.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.color = Panel;
            BistroBuilderSurface.Apply(rootImage, BistroBuilderSurfaceLevel.Panel);
        }

        BuildCloseButton();
        BuildSearch();
        BuildScopeBar();
        BuildSummary();
        BuildFilterPanel();
        BuildPlacementStrip();
        BuildCollapsedTab();
        BuildEmptyState();
    }    private void BuildCloseButton()
    {
        Transform header = contentRoot.transform.Find("Header");
        if (header == null) return;

        GameObject go = CreateUiObject("ApprovedClose", header);
        RectTransform rect = go.GetComponent<RectTransform>();
        SetAnchored(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-18f, 0f), new Vector2(36f, 36f));

        Image image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        Button button = go.AddComponent<Button>();
        ConfigureButton(button, image, Color.clear, Field);
        Text label = CreateText("Label", go.transform, "×", bodyFont, 28,
            TextMuted, TextAnchor.MiddleCenter, false);
        Stretch(label.rectTransform, 0f);
        button.onClick.AddListener(() =>
        {
            collapsed = true;
            ApplyCollapsedState();
        });
    }

    private void BuildSearch()
    {
        GameObject go = CreateUiObject("ApprovedSearch", contentRoot.transform);
        RectTransform rect = go.GetComponent<RectTransform>();
        SetTopRect(rect, 24f, 24f, 70f, 50f);

        Image image = go.AddComponent<Image>();
        image.color = Card;
        BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Base);        searchInput = go.AddComponent<InputField>();
        Text text = CreateText("Text", go.transform, string.Empty, bodyFont, 14,
            TextPrimary, TextAnchor.MiddleLeft, false);
        SetStretchOffsets(text.rectTransform, 42f, 38f, 3f, 3f);

        Text placeholder = CreateText("Placeholder", go.transform,
            "Buscar muebles, decoración...", bodyFont, 14,
            new Color32(142, 146, 141, 255), TextAnchor.MiddleLeft, false);
        SetStretchOffsets(placeholder.rectTransform, 42f, 38f, 3f, 3f);

        Text icon = CreateText("SearchIcon", go.transform, "⌕", bodySemiBold, 20,
            TextMuted, TextAnchor.MiddleCenter, false);
        SetAnchored(icon.rectTransform, new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(28f, 40f));

        searchInput.textComponent = text;
        searchInput.placeholder = placeholder;
        searchInput.lineType = InputField.LineType.SingleLine;
        searchInput.onValueChanged.AddListener(_ => ApplyFiltersAndOrdering());
    }

    private void BuildScopeBar()
    {
        scopeBar = CreateUiObject("ApprovedScopeBar", contentRoot.transform);
        RectTransform rect = scopeBar.GetComponent<RectTransform>();
        SetTopRect(rect, 24f, 24f, 214f, 38f);

        scopeAllButton = CreateChip(scopeBar.transform, "Todos", 0f, 0.28f);
        scopeInteriorButton = CreateChip(scopeBar.transform, "Interior", 0.30f, 0.58f);
        scopeExteriorButton = CreateChip(scopeBar.transform, "Exterior", 0.60f, 0.88f);
        filterButton = CreateChip(scopeBar.transform, "≡", 0.90f, 1f);        scopeAllButton.onClick.AddListener(() =>
        {
            scopeFilter = RestaurantPlaceableEnvironmentScope.InteriorAndExterior;
            RefreshScopeButtonColors();
            ApplyFiltersAndOrdering();
        });
        scopeInteriorButton.onClick.AddListener(() =>
        {
            scopeFilter = RestaurantPlaceableEnvironmentScope.InteriorOnly;
            RefreshScopeButtonColors();
            ApplyFiltersAndOrdering();
        });
        scopeExteriorButton.onClick.AddListener(() =>
        {
            scopeFilter = RestaurantPlaceableEnvironmentScope.ExteriorOnly;
            RefreshScopeButtonColors();
            ApplyFiltersAndOrdering();
        });
        filterButton.onClick.AddListener(() =>
        {
            filterOpen = !filterOpen;
            filterPanel.SetActive(filterOpen);
            ApplyScrollBounds();
        });

        RefreshScopeButtonColors();
    }

    private void BuildSummary()
    {
        GameObject go = CreateUiObject("ApprovedSummary", contentRoot.transform);
        RectTransform rect = go.GetComponent<RectTransform>();
        SetTopRect(rect, 26f, 26f, 260f, 20f);

        countText = CreateText("Count", go.transform, "0 artículos", bodyFont, 10,
            TextMuted, TextAnchor.MiddleLeft, false);
        summaryText = CreateText("State", go.transform, "Todos los artículos",
            bodySemiBold, 10, OliveDark, TextAnchor.MiddleRight, false);        SetHorizontalSplit(countText.rectTransform, 0f, 0.45f);
        SetHorizontalSplit(summaryText.rectTransform, 0.45f, 1f);
    }

    private void BuildFilterPanel()
    {
        filterPanel = CreateUiObject("ApprovedFilters", contentRoot.transform);
        RectTransform rect = filterPanel.GetComponent<RectTransform>();
        SetTopRect(rect, 24f, 24f, 286f, 74f);

        Image image = filterPanel.AddComponent<Image>();
        image.color = Field;
        BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Base);

        Button favoritesButton = CreateChip(filterPanel.transform,
            "★ Favoritos", 0.02f, 0.32f);
        Button recentsButton = CreateChip(filterPanel.transform,
            "↺ Recientes", 0.34f, 0.66f);
        Button sortButton = CreateChip(filterPanel.transform,
            "Orden: relevancia", 0.68f, 0.98f);

        favoritesButton.onClick.AddListener(() =>
        {
            favoritesOnly = !favoritesOnly;
            SetChipSelected(favoritesButton, favoritesOnly);
            ApplyFiltersAndOrdering();
        });
        recentsButton.onClick.AddListener(() =>
        {
            recentsOnly = !recentsOnly;
            SetChipSelected(recentsButton, recentsOnly);
            ApplyFiltersAndOrdering();
        });
        sortButton.onClick.AddListener(() =>
        {
            sortMode = (sortMode + 1) % 4;
            Text label = sortButton.GetComponentInChildren<Text>();
            if (label != null) label.text = GetSortLabel();
            ApplyFiltersAndOrdering();
        });
        filterPanel.SetActive(false);
    }    private void BuildPlacementStrip()
    {
        placementStrip = CreateUiObject("ApprovedPlacement", contentRoot.transform);
        RectTransform rect = placementStrip.GetComponent<RectTransform>();
        SetTopRect(rect, 24f, 24f, 286f, 52f);

        Image image = placementStrip.AddComponent<Image>();
        image.color = OliveSoft;
        BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Base);

        GameObject iconRoot = CreateUiObject("IconRoot", placementStrip.transform);
        RectTransform iconRootRect = iconRoot.GetComponent<RectTransform>();
        SetAnchored(iconRootRect, new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(34f, 34f));

        Image iconBackground = iconRoot.AddComponent<Image>();
        iconBackground.color = Olive;
        BistroBuilderSurface.Apply(iconBackground, BistroBuilderSurfaceLevel.Base);

        Text icon = CreateText("Icon", iconRoot.transform, "↖",
            bodySemiBold, 20, Color.white, TextAnchor.MiddleCenter, false);
        Stretch(icon.rectTransform, 0f);

        placementText = CreateText("PlacementText", placementStrip.transform,
            "Colocando artículo", bodySemiBold, 11, new Color32(52, 68, 43, 255),
            TextAnchor.MiddleLeft, false);
        SetStretchOffsets(placementText.rectTransform, 56f, 44f, 8f, 8f);

        Button cancel = CreateButton("Cancel", placementStrip.transform, "×",
            bodyFont, 22, TextMuted, Color.clear, Field);
        SetAnchored(cancel.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(30f, 30f));
        cancel.onClick.AddListener(() =>
        {
            if (interactionController != null)
            {
                interactionController.CancelActivePlacement();
            }
        });
        placementStrip.SetActive(false);
    }    private void BuildCollapsedTab()
    {
        collapsedTab = CreateUiObject("ApprovedCatalogTab", transform);
        RectTransform rect = collapsedTab.GetComponent<RectTransform>();
        SetAnchored(rect, new Vector2(0f, 0.65f), new Vector2(0f, 0.65f),
            new Vector2(72f, 0f), new Vector2(108f, 38f));

        Image image = collapsedTab.AddComponent<Image>();
        image.color = Olive;
        BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Floating);
        Button button = collapsedTab.AddComponent<Button>();
        ConfigureButton(button, image, Olive, OliveDark);

        Text label = CreateText("Label", collapsedTab.transform, "Catálogo",
            bodySemiBold, 12, Color.white, TextAnchor.MiddleCenter, false);
        Stretch(label.rectTransform, 0f);
        button.onClick.AddListener(() =>
        {
            collapsed = false;
            ApplyCollapsedState();
        });
        collapsedTab.SetActive(false);
    }

    private void BuildEmptyState()
    {
        if (scrollRect == null) return;

        Transform viewport = scrollRect.transform.Find("Viewport");
        if (viewport == null) return;

        emptyState = CreateUiObject("ApprovedEmptyState", viewport);
        RectTransform rect = emptyState.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.34f);
        rect.anchorMax = new Vector2(0.92f, 0.66f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text title = CreateText("Title", emptyState.transform,
            "No hay artículos", bodySemiBold, 15, TextPrimary,
            TextAnchor.MiddleCenter, false);
        title.rectTransform.anchorMin = new Vector2(0f, 0.52f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = Vector2.zero;
        title.rectTransform.offsetMax = Vector2.zero;

        Text body = CreateText("Body", emptyState.transform,
            "Prueba otra categoría o limpia los filtros.",
            bodyFont, 11, TextMuted, TextAnchor.UpperCenter, false);
        body.rectTransform.anchorMin = Vector2.zero;
        body.rectTransform.anchorMax = new Vector2(1f, 0.52f);
        body.rectTransform.offsetMin = new Vector2(8f, 4f);
        body.rectTransform.offsetMax = new Vector2(-8f, -2f);

        emptyState.SetActive(false);
    }

    private void ApplyStaticLayout()
    {
        if (contentRoot == null) return;

        RectTransform root = contentRoot.transform as RectTransform;
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 0.5f);
        root.offsetMin = new Vector2(18f, 150f);
        root.offsetMax = new Vector2(448f, -86f);

        Transform header = contentRoot.transform.Find("Header");        if (header is RectTransform headerRect)
        {
            SetTopRect(headerRect, 24f, 24f, 18f, 42f);
            Text title = header.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.font = titleFont;
                title.fontSize = 27;
                title.fontStyle = FontStyle.Normal;
                title.color = TextPrimary;
                title.alignment = TextAnchor.MiddleLeft;
                SetHorizontalSplit(title.rectTransform, 0f, 0.84f);
            }

            Text status = header.Find("Status")?.GetComponent<Text>();
            if (status != null) status.gameObject.SetActive(false);
        }

        if (categoryBar != null)
        {
            SetTopRect(categoryBar, 18f, 18f, 132f, 72f);
            HorizontalLayoutGroup layout = categoryBar.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 4f;
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = true;
            }
        }

        if (scrollRect != null)
        {
            scrollRect.scrollSensitivity = 28f;
        }        if (itemContainer != null)
        {
            GridLayoutGroup grid = itemContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = new Vector2(181f, 246f);
                grid.spacing = new Vector2(10f, 10f);
                grid.padding = new RectOffset(0, 8, 0, 12);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
            }
        }

        ApplyScrollBounds();
    }

    private void ApplyScrollBounds()
    {
        if (scrollRect == null) return;

        RectTransform rect = scrollRect.transform as RectTransform;
        float top = filterOpen ? 370f : 292f;
        if (placementStrip != null && placementStrip.activeSelf)
        {
            top = filterOpen ? 428f : 350f;
        }

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(24f, 16f);
        rect.offsetMax = new Vector2(-24f, -top);
    }

    private void RefreshDynamicViews(bool force)
    {
        if (contentRoot == null) return;

        if (force || categoryBar != null)
        {
            StyleCategoryViews();
        }        if (force || itemContainer != null)
        {
            StyleItemViews();
            ApplyFiltersAndOrdering();
        }

        lastItemChildCount = itemContainer != null ? itemContainer.childCount : -1;
        lastCategoryChildCount = categoryBar != null ? categoryBar.childCount : -1;
    }

    private void StyleCategoryViews()
    {
        if (categoryBar == null) return;

        foreach (Transform child in categoryBar)
        {
            RestaurantPlaceableCatalogCategoryView view =
                child.GetComponent<RestaurantPlaceableCatalogCategoryView>();
            if (view == null || !child.gameObject.activeSelf) continue;

            LayoutElement layout = child.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minWidth = 48f;
                layout.preferredWidth = 60f;
                layout.flexibleWidth = 1f;
            }

            Image background = child.GetComponent<Image>();
            if (background != null)
            {
                BistroBuilderSurface.Apply(background, BistroBuilderSurfaceLevel.Base);
            }

            Button categoryButton = child.GetComponent<Button>();
            if (categoryButton != null)
            {
                categoryButton.transition = Selectable.Transition.None;
                BistroBuilderInteractionSurface.Attach(categoryButton);
            }

            Text label = child.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.font = bodyFont;
                label.fontSize = 10;
                label.color = TextPrimary;
                label.alignment = TextAnchor.LowerCenter;
                label.rectTransform.offsetMin = new Vector2(2f, 4f);
                label.rectTransform.offsetMax = new Vector2(-2f, -34f);
            }
        }
    }    private void StyleItemViews()
    {
        if (itemContainer == null) return;

        foreach (Transform child in itemContainer)
        {
            RestaurantPlaceableCatalogItemView view =
                child.GetComponent<RestaurantPlaceableCatalogItemView>();
            if (view == null || view.Definition == null) continue;

            LayoutElement layout = child.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minWidth = 181f;
                layout.preferredWidth = 181f;
                layout.preferredHeight = 246f;
            }

            Image background = child.GetComponent<Image>();
            if (background != null)
            {
                background.color = Card;
                BistroBuilderSurface.Apply(background, BistroBuilderSurfaceLevel.Card);
            }

            Button itemButton = child.GetComponent<Button>();
            if (itemButton != null)
            {
                itemButton.transition = Selectable.Transition.None;
                BistroBuilderInteractionSurface.Attach(itemButton).IsCard = true;
            }

            StyleItemImage(child);
            StyleItemTexts(child, view.Definition);
            EnsureFavoriteButton(view);
            EnsureSelectedIndicator(view);
            HookItemButton(view);
        }
    }    private void StyleItemImage(Transform card)
    {
        Transform iconRoot = card.Find("IconRoot");
        if (iconRoot is RectTransform iconRect)
        {
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, 0f);
            iconRect.sizeDelta = new Vector2(0f, 152f);

            Image rootImage = iconRoot.GetComponent<Image>();
            if (rootImage != null) rootImage.color = new Color32(247, 244, 237, 255);

            Image icon = iconRoot.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                SetStretchOffsets(icon.rectTransform, 12f, 12f, 10f, 10f);
                icon.preserveAspect = true;
            }

            Text fallback = iconRoot.Find("Fallback")?.GetComponent<Text>();
            if (fallback != null)
            {
                fallback.font = bodySemiBold;
                fallback.color = TextMuted;
            }
        }
    }

    private void StyleItemTexts(Transform card, RestaurantPlaceableItemDefinition definition)
    {
        Text name = card.Find("Name")?.GetComponent<Text>();
        if (name != null)
        {
            name.font = bodySemiBold;
            name.fontSize = 13;
            name.color = TextPrimary;
            name.alignment = TextAnchor.UpperLeft;
            SetTopRect(name.rectTransform, 10f, 10f, 160f, 38f);
        }        Text description = card.Find("Description")?.GetComponent<Text>();
        if (description != null)
        {
            description.font = bodyFont;
            description.fontSize = 9;
            description.color = TextMuted;
            description.alignment = TextAnchor.MiddleRight;
            description.text = GetScopeLabel(definition.PlacementScope);
            description.resizeTextForBestFit = false;
            SetAnchored(description.rectTransform, new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-52f, 17f), new Vector2(92f, 22f));
        }

        Text price = card.Find("Price")?.GetComponent<Text>();
        if (price != null)
        {
            price.font = bodySemiBold;
            price.fontSize = 17;
            price.color = new Color32(62, 123, 48, 255);
            price.alignment = TextAnchor.MiddleLeft;
            price.text = definition.PurchasePrice.ToString("N0") + " €";
            SetAnchored(price.rectTransform, new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(48f, 17f), new Vector2(76f, 28f));
        }
    }

    private void EnsureFavoriteButton(RestaurantPlaceableCatalogItemView view)
    {
        Transform card = view.transform;
        Transform existing = card.Find("ApprovedFavorite");
        Button button;

        if (existing == null)
        {
            button = CreateButton("ApprovedFavorite", card, "★", bodySemiBold, 16,
                Color.white, Gold, Gold);
            SetAnchored(button.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(30f, 30f));
            button.onClick.AddListener(() => ToggleFavorite(view.Definition));
        }        else
        {
            button = existing.GetComponent<Button>();
        }

        bool favorite = favorites.Contains(view.Definition.ItemId);
        Image image = button != null ? button.GetComponent<Image>() : null;
        Text text = button != null ? button.GetComponentInChildren<Text>() : null;
        if (image != null) image.color = favorite ? Gold : new Color32(245, 242, 235, 235);
        if (text != null) text.color = favorite ? Color.white : TextMuted;
    }

    private void EnsureSelectedIndicator(RestaurantPlaceableCatalogItemView view)
    {
        Transform existing = view.transform.Find("ApprovedSelected");
        GameObject go = existing != null
            ? existing.gameObject
            : CreateUiObject("ApprovedSelected", view.transform);

        if (existing == null)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            SetAnchored(rect, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -20f), new Vector2(30f, 30f));
            Image image = go.AddComponent<Image>();
            image.color = Olive;
            BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Floating);
            Text check = CreateText("Check", go.transform, "✓", bodySemiBold, 16,
                Color.white, TextAnchor.MiddleCenter, false);
            Stretch(check.rectTransform, 0f);
        }

        bool selected = interactionController != null &&
            interactionController.HasActivePlacement &&
            string.Equals(activeItemId, view.Definition.ItemId, StringComparison.Ordinal);
        go.SetActive(selected);

        BistroBuilderSurface surface = view.GetComponent<BistroBuilderSurface>();
        if (surface != null)
        {
            surface.SetBorder(selected
                ? BistroBuilderBorderState.Selected
                : BistroBuilderBorderState.Normal);
        }
    }    private void HookItemButton(RestaurantPlaceableCatalogItemView view)
    {
        Button button = view.GetComponent<Button>();
        if (button == null) return;

        int id = button.GetInstanceID();
        if (hookedItemButtons.Contains(id)) return;
        hookedItemButtons.Add(id);

        RestaurantPlaceableItemDefinition definition = view.Definition;
        button.onClick.AddListener(() =>
        {
            if (definition == null) return;
            activeItemId = definition.ItemId;
            recents.Remove(activeItemId);
            recents.Insert(0, activeItemId);
            if (recents.Count > 12) recents.RemoveAt(recents.Count - 1);
            RefreshDynamicViews(false);
        });
    }

    private void ToggleFavorite(RestaurantPlaceableItemDefinition definition)
    {
        if (definition == null) return;
        string id = definition.ItemId;

        if (!favorites.Add(id))
        {
            favorites.Remove(id);
        }

        RefreshDynamicViews(false);
    }

    private void ApplyFiltersAndOrdering()
    {
        if (itemContainer == null) return;

        string query = searchInput != null
            ? searchInput.text.Trim()
            : string.Empty;
        List<RestaurantPlaceableCatalogItemView> visible =
            new List<RestaurantPlaceableCatalogItemView>();        foreach (Transform child in itemContainer)
        {
            RestaurantPlaceableCatalogItemView view =
                child.GetComponent<RestaurantPlaceableCatalogItemView>();
            if (view == null || view.Definition == null) continue;

            RestaurantPlaceableItemDefinition definition = view.Definition;
            bool matches = MatchesQuery(definition, query) &&
                MatchesScope(definition) &&
                (!favoritesOnly || favorites.Contains(definition.ItemId)) &&
                (!recentsOnly || recents.Contains(definition.ItemId));

            child.gameObject.SetActive(matches);
            if (matches) visible.Add(view);
        }

        visible.Sort(CompareViews);
        for (int index = 0; index < visible.Count; index++)
        {
            visible[index].transform.SetSiblingIndex(index);
        }

        if (countText != null)
        {
            countText.text = visible.Count == 1
                ? "1 artículo"
                : visible.Count + " artículos";
        }

        if (summaryText != null)
        {
            summaryText.text = GetSummaryLabel();
        }

        if (emptyState != null)
        {
            emptyState.SetActive(visible.Count == 0);

            Text title = emptyState.transform.Find("Title")?.GetComponent<Text>();
            Text body = emptyState.transform.Find("Body")?.GetComponent<Text>();
            bool searching = !string.IsNullOrWhiteSpace(query);

            if (title != null)
            {
                title.text = searching
                    ? "Sin resultados"
                    : "No hay artículos en esta categoría";
            }

            if (body != null)
            {
                body.text = searching
                    ? "Prueba otro término o limpia los filtros."
                    : "Esta categoría todavía no contiene artículos.";
            }
        }
    }

    private bool MatchesQuery(RestaurantPlaceableItemDefinition definition, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return ContainsInvariant(definition.DisplayName, query) ||
            ContainsInvariant(definition.Description, query) ||
            ContainsInvariant(definition.Category.ToString(), query);
    }
    private bool MatchesScope(RestaurantPlaceableItemDefinition definition)
    {
        if (scopeFilter == RestaurantPlaceableEnvironmentScope.InteriorAndExterior)
        {
            return true;
        }

        RestaurantPlaceableEnvironmentScope itemScope = definition.PlacementScope;
        return itemScope == RestaurantPlaceableEnvironmentScope.InteriorAndExterior ||
            itemScope == scopeFilter;
    }

    private int CompareViews(
        RestaurantPlaceableCatalogItemView left,
        RestaurantPlaceableCatalogItemView right)
    {
        RestaurantPlaceableItemDefinition a = left.Definition;
        RestaurantPlaceableItemDefinition b = right.Definition;

        switch (sortMode)
        {
            case 1:
                return a.PurchasePrice != b.PurchasePrice
                    ? a.PurchasePrice.CompareTo(b.PurchasePrice)
                    : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            case 2:
                return a.PurchasePrice != b.PurchasePrice
                    ? b.PurchasePrice.CompareTo(a.PurchasePrice)
                    : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            case 3:
                return string.Compare(a.DisplayName, b.DisplayName,
                    StringComparison.OrdinalIgnoreCase);
            default:
                return 0;
        }
    }

    private void RefreshPlacementStrip()
    {
        if (placementStrip == null) return;

        bool active = interactionController != null &&
            interactionController.HasActivePlacement &&
            !string.IsNullOrWhiteSpace(activeItemId);

        if (!active && !string.IsNullOrWhiteSpace(activeItemId))
        {
            activeItemId = null;
            RefreshDynamicViews(false);
        }

        if (placementStrip.activeSelf != active)
        {
            placementStrip.SetActive(active);
            ApplyScrollBounds();
        }

        if (!active || placementText == null) return;

        RestaurantPlaceableItemDefinition definition = FindDefinition(activeItemId);
        string displayName = definition != null
            ? definition.DisplayName
            : "artículo";
        placementText.text = "Colocando · " + displayName +
            "\nClic para confirmar · Esc para cancelar";
    }

    private RestaurantPlaceableItemDefinition FindDefinition(string itemId)
    {
        if (catalogService == null || string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        IReadOnlyList<RestaurantPlaceableItemDefinition> items =
            catalogService.AvailableItems;
        for (int index = 0; index < items.Count; index++)
        {
            RestaurantPlaceableItemDefinition definition = items[index];
            if (definition != null &&
                string.Equals(definition.ItemId, itemId, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private void RefreshScopeButtonColors()
    {
        SetChipSelected(scopeAllButton,
            scopeFilter == RestaurantPlaceableEnvironmentScope.InteriorAndExterior);
        SetChipSelected(scopeInteriorButton,
            scopeFilter == RestaurantPlaceableEnvironmentScope.InteriorOnly);
        SetChipSelected(scopeExteriorButton,
            scopeFilter == RestaurantPlaceableEnvironmentScope.ExteriorOnly);
    }
    private void ApplyCollapsedState()
    {
        if (contentRoot == null) return;

        CanvasGroup group = contentRoot.GetComponent<CanvasGroup>();
        if (group == null) group = contentRoot.AddComponent<CanvasGroup>();
        group.alpha = collapsed ? 0f : 1f;
        group.interactable = !collapsed;
        group.blocksRaycasts = !collapsed;
        if (collapsedTab != null) collapsedTab.SetActive(collapsed);
    }

    private string GetSummaryLabel()
    {
        if (favoritesOnly) return "Favoritos";
        if (recentsOnly) return "Recientes";
        if (scopeFilter == RestaurantPlaceableEnvironmentScope.InteriorOnly)
            return "Interior";
        if (scopeFilter == RestaurantPlaceableEnvironmentScope.ExteriorOnly)
            return "Exterior";
        return "Todos los artículos";
    }

    private string GetSortLabel()
    {
        switch (sortMode)
        {
            case 1: return "Orden: precio ↑";
            case 2: return "Orden: precio ↓";
            case 3: return "Orden: nombre";
            default: return "Orden: relevancia";
        }
    }

    private static string GetScopeLabel(RestaurantPlaceableEnvironmentScope scope)
    {
        switch (scope)
        {
            case RestaurantPlaceableEnvironmentScope.InteriorOnly:
                return "Interior";
            case RestaurantPlaceableEnvironmentScope.ExteriorOnly:
                return "Exterior";
            default:
                return "Interior / exterior";
        }
    }

    private static bool ContainsInvariant(string source, string query)
    {
        return !string.IsNullOrWhiteSpace(source) &&
            source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Button CreateChip(Transform parent, string label, float xMin, float xMax)
    {
        Button button = CreateButton("Chip_" + label, parent, label,
            bodyFont, 12, TextPrimary, Field, new Color32(232, 228, 220, 255));
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, 0f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return button;
    }

    private void SetChipSelected(Button button, bool selected)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        Text label = button.GetComponentInChildren<Text>();
        if (image != null) image.color = selected ? Olive : Field;
        if (label != null) label.color = selected ? Color.white : TextPrimary;
    }
    private Button CreateButton(
        string name,
        Transform parent,
        string labelText,
        Font font,
        int fontSize,
        Color textColor,
        Color normal,
        Color highlighted)
    {
        GameObject go = CreateUiObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = normal;
        BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Base);

        Button button = go.AddComponent<Button>();
        ConfigureButton(button, image, normal, highlighted);
        BistroBuilderInteractionSurface.Attach(button);

        Text label = CreateText("Label", go.transform, labelText,
            font, fontSize, textColor, TextAnchor.MiddleCenter, false);
        Stretch(label.rectTransform, 0f);
        return button;
    }

    private static void ConfigureButton(
        Button button,
        Graphic target,
        Color normal,
        Color highlighted)
    {
        button.targetGraphic = target;
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = highlighted * 0.92f;
        colors.selectedColor = highlighted;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        Font font,
        int size,
        Color color,
        TextAnchor anchor,
        bool richText)
    {
        GameObject go = CreateUiObject(name, parent);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.supportRichText = richText;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private static void SetTopRect(
        RectTransform rect,
        float left,
        float right,
        float top,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetAnchored(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = (anchorMin + anchorMax) * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetHorizontalSplit(RectTransform rect, float min, float max)
    {
        rect.anchorMin = new Vector2(min, 0f);
        rect.anchorMax = new Vector2(max, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetStretchOffsets(
        RectTransform rect,
        float left,
        float right,
        float bottom,
        float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        SetStretchOffsets(rect, inset, inset, inset, inset);
    }
}
