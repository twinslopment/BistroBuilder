using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RestaurantPlaceableCatalogPreviewSkin : MonoBehaviour
{
    private static readonly Color32 Panel = new Color32(247, 243, 237, 255);
    private static readonly Color32 Card = new Color32(253, 249, 245, 255);
    private static readonly Color32 Field = new Color32(240, 237, 230, 255);
    private static readonly Color32 TextPrimary = new Color32(31, 35, 29, 255);
    private static readonly Color32 TextMuted = new Color32(138, 138, 132, 255);
    private static readonly Color32 Olive = new Color32(107, 128, 74, 255);
    private static readonly Color32 OliveSoft = new Color32(235, 241, 226, 255);
    private static readonly Color32 Gold = new Color32(240, 171, 34, 255);
    private static readonly Color32 PriceGreen = new Color32(63, 122, 49, 255);

    private static Sprite rounded10;
    private static Sprite rounded14;
    private static Sprite rounded16;
    private static Sprite rounded24;

    private Transform contentRoot;
    private RectTransform categoryBar;
    private RectTransform itemContainer;
    private ScrollRect scrollRect;
    private TMP_FontAsset bodyFont;
    private TMP_FontAsset semiBoldFont;
    private TMP_InputField tmpSearch;
    private InputField legacySearch;
    private int categoryCount = -1;
    private int itemCount = -1;
    private bool chromeReady;
    private bool geometryDirty = true;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private bool lastPlacementVisible;
    private bool lastFiltersVisible;
    private string selectedScopeLabel = "Todos";

    private void Awake()
    {
        LoadFonts();
    }

    private void LateUpdate()
    {
        if (!TryCacheHierarchy())
        {
            return;
        }

        if (!chromeReady)
        {
            ApplyChrome();
            chromeReady = true;
            geometryDirty = true;
        }

        if (categoryBar != null && categoryBar.childCount != categoryCount)
        {
            ApplyCategories();
            categoryCount = categoryBar.childCount;
            geometryDirty = true;
        }

        if (itemContainer != null && itemContainer.childCount != itemCount)
        {
            ApplyItemGrid();
            ApplyCards();
            itemCount = itemContainer.childCount;
            geometryDirty = true;
        }

        SyncDynamicText();
        SyncCategoryStates();
        SyncScopeStates();
        SyncCardStates();
        EnforcePreviewVisualAuthority();

        bool placementVisible =
            contentRoot.Find("ApprovedPlacement")?.gameObject.activeSelf == true;
        bool filtersVisible =
            contentRoot.Find("ApprovedFilters")?.gameObject.activeSelf == true;

        if (placementVisible != lastPlacementVisible ||
            filtersVisible != lastFiltersVisible ||
            Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight)
        {
            lastPlacementVisible = placementVisible;
            lastFiltersVisible = filtersVisible;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            geometryDirty = true;
        }

        if (geometryDirty)
        {
            ApplyPreviewGeometry();
            geometryDirty = false;
        }
    }

    private void LoadFonts()
    {
        bodyFont = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Inter-Regular-SDF");
        semiBoldFont = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Inter-SemiBold-SDF");

        TMP_FontAsset fallback = Resources.Load<TMP_FontAsset>(
            "Fonts & Materials/LiberationSans SDF");

        if (bodyFont == null)
        {
            bodyFont = fallback;
        }

        if (semiBoldFont == null)
        {
            semiBoldFont = bodyFont != null ? bodyFont : fallback;
        }

    }

    private bool TryCacheHierarchy()
    {
        if (contentRoot == null)
        {
            Transform found = transform.Find("CatalogContent");
            if (found == null)
            {
                return false;
            }

            contentRoot = found;
        }

        if (categoryBar == null)
        {
            categoryBar = contentRoot.Find("CategoryBar") as RectTransform;
        }

        if (scrollRect == null)
        {
            Transform scroll = contentRoot.Find("ItemsScroll");
            scrollRect = scroll != null ? scroll.GetComponent<ScrollRect>() : null;
        }

        if (itemContainer == null)
        {
            itemContainer = contentRoot.Find(
                "ItemsScroll/Viewport/Items") as RectTransform;
        }

        return contentRoot.Find("ApprovedSearch") != null;
    }

    private void ApplyChrome()
    {
        Image panelImage = contentRoot.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = Panel;
        }

        ApplyHeader();
        ApplySearch();
        ApplyScopeButtons();
        ApplyFilterButtons();
        ApplySummary();
        ApplyPlacementStrip();
        ApplyEmptyState();
        ApplyItemGrid();
        ConfigureCanvasForCrispUi();
    }

    private void ApplyHeader()
    {
        Transform header = contentRoot.Find("Header");
        if (header == null)
        {
            return;
        }

        Text legacyTitle = header.Find("Title")?.GetComponent<Text>();
        if (legacyTitle != null)
        {
            legacyTitle.enabled = false;
        }

        TextMeshProUGUI title = GetOrCreateTmp(
            header,
            "PreviewTitle",
            semiBoldFont,
            27f,
            TextPrimary,
            TextAlignmentOptions.MidlineLeft);
        title.fontWeight = FontWeight.Bold;

        SetAnchors(
            title.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0.82f, 1f),
            Vector2.zero,
            Vector2.zero);

        title.text = "Catálogo de artículos";
        title.enableWordWrapping = false;

        Transform close = header.Find("ApprovedClose");
        if (close != null)
        {
            HideLegacyTexts(close);
            AddVectorIcon(
                close,
                "PreviewIcon",
                RestaurantCatalogPreviewIcon.Close,
                TextMuted,
                1.7f,
                9f);
        }
    }

    private void ApplySearch()
    {
        Transform search = contentRoot.Find("ApprovedSearch");
        if (search == null)
        {
            return;
        }

        legacySearch = search.GetComponent<InputField>();
        if (legacySearch == null)
        {
            return;
        }

        HideLegacyTexts(search);

        RectTransform inputRoot = CreateChildRect(search, "PreviewInputRoot");
        SetStretch(inputRoot, 0f);
        inputRoot.SetAsLastSibling();

        Image inputHitArea = inputRoot.GetComponent<Image>() ??
            inputRoot.gameObject.AddComponent<Image>();
        inputHitArea.color = new Color(1f, 1f, 1f, 0.001f);
        inputHitArea.raycastTarget = true;

        RectTransform iconRect = CreateChildRect(inputRoot, "PreviewSearchIcon");
        SetAnchors(
            iconRect,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(13f, 11f),
            new Vector2(39f, -11f));
        RestaurantCatalogPreviewIconGraphic icon =
            iconRect.gameObject.GetComponent<RestaurantCatalogPreviewIconGraphic>() ??
            iconRect.gameObject.AddComponent<RestaurantCatalogPreviewIconGraphic>();
        icon.Icon = RestaurantCatalogPreviewIcon.Search;
        icon.color = TextMuted;
        icon.LineWidth = 1.75f;
        icon.raycastTarget = false;

        RectTransform trailingIconRect =
            CreateChildRect(inputRoot, "PreviewSearchIconTrailing");
        SetAnchors(
            trailingIconRect,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(-39f, 11f),
            new Vector2(-13f, -11f));
        RestaurantCatalogPreviewIconGraphic trailingIcon =
            trailingIconRect.gameObject.GetComponent<RestaurantCatalogPreviewIconGraphic>() ??
            trailingIconRect.gameObject.AddComponent<RestaurantCatalogPreviewIconGraphic>();
        trailingIcon.Icon = RestaurantCatalogPreviewIcon.Search;
        trailingIcon.color = TextMuted;
        trailingIcon.LineWidth = 1.75f;
        trailingIcon.raycastTarget = false;

        RectTransform area = CreateChildRect(inputRoot, "PreviewTextArea");
        SetAnchors(
            area,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(44f, 4f),
            new Vector2(-44f, -4f));

        TextMeshProUGUI inputText = GetOrCreateTmp(
            area,
            "Text",
            bodyFont,
            14f,
            TextPrimary,
            TextAlignmentOptions.MidlineLeft);
        SetStretch(inputText.rectTransform, 0f);

        TextMeshProUGUI placeholder = GetOrCreateTmp(
            area,
            "Placeholder",
            bodyFont,
            14f,
            new Color32(150, 151, 146, 255),
            TextAlignmentOptions.MidlineLeft);
        SetStretch(placeholder.rectTransform, 0f);
        placeholder.text = "Buscar muebles, decoración...";

        tmpSearch = inputRoot.GetComponent<TMP_InputField>() ??
            inputRoot.gameObject.AddComponent<TMP_InputField>();
        tmpSearch.targetGraphic = inputHitArea;
        tmpSearch.textViewport = area;
        tmpSearch.textComponent = inputText;
        tmpSearch.placeholder = placeholder;
        tmpSearch.lineType = TMP_InputField.LineType.SingleLine;
        tmpSearch.contentType = TMP_InputField.ContentType.Standard;
        tmpSearch.caretColor = Olive;
        tmpSearch.selectionColor = new Color(
            Olive.r / 255f,
            Olive.g / 255f,
            Olive.b / 255f,
            0.22f);

        tmpSearch.onValueChanged.RemoveAllListeners();
        tmpSearch.onValueChanged.AddListener(value =>
        {
            if (legacySearch != null)
            {
                legacySearch.text = value;
            }
        });

        tmpSearch.text = legacySearch.text;
        legacySearch.enabled = false;
    }

    private void ApplyCategories()
    {
        if (categoryBar == null)
        {
            return;
        }

        foreach (Transform child in categoryBar)
        {
            RestaurantPlaceableCatalogCategoryView view =
                child.GetComponent<RestaurantPlaceableCatalogCategoryView>();
            if (view == null)
            {
                continue;
            }

            Text legacy = child.GetComponentInChildren<Text>(true);
            string label = legacy != null ? legacy.text : child.name;
            if (legacy != null)
            {
                legacy.enabled = false;
            }

            TextMeshProUGUI tmp = GetOrCreateTmp(
                child,
                "PreviewLabel",
                bodyFont,
                10.5f,
                TextPrimary,
                TextAlignmentOptions.Bottom);

            SetAnchors(
                tmp.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.42f),
                new Vector2(2f, 4f),
                new Vector2(-2f, -2f));
            tmp.text = label;
            tmp.enableWordWrapping = false;

            RectTransform iconRect = CreateChildRect(child, "PreviewIcon");
            SetAnchors(
                iconRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-10.5f, -19f),
                new Vector2(10.5f, 2f));

            RestaurantCatalogPreviewIconGraphic icon =
                iconRect.gameObject.GetComponent<RestaurantCatalogPreviewIconGraphic>() ??
                iconRect.gameObject.AddComponent<RestaurantCatalogPreviewIconGraphic>();
            icon.Icon = MapCategoryIcon(label);
            icon.color = TextMuted;
            icon.LineWidth = 1.65f;
            icon.raycastTarget = false;
        }
    }

    private void ApplyScopeButtons()
    {
        Transform bar = contentRoot.Find("ApprovedScopeBar");
        if (bar == null)
        {
            return;
        }

        foreach (Transform child in bar)
        {
            Button button = child.GetComponent<Button>();
            if (button == null)
            {
                continue;
            }

            Text legacy = child.GetComponentInChildren<Text>(true);
            string label = legacy != null ? legacy.text : string.Empty;
            if (legacy != null)
            {
                legacy.enabled = false;
            }

            if (label == "≡")
            {
                AddVectorIcon(
                    child,
                    "PreviewFilterIcon",
                    RestaurantCatalogPreviewIcon.Filter,
                    TextPrimary,
                    1.55f,
                    9f);
                continue;
            }

            TextMeshProUGUI tmp = GetOrCreateTmp(
                child,
                "PreviewLabel",
                bodyFont,
                12.5f,
                TextPrimary,
                TextAlignmentOptions.Center);
            SetStretch(tmp.rectTransform, 0f);
            tmp.text = label;

            string capturedLabel = label;
            button.onClick.AddListener(() =>
            {
                selectedScopeLabel = capturedLabel;
            });
        }
    }

    private void ApplyFilterButtons()
    {
        Transform panel = contentRoot.Find("ApprovedFilters");
        if (panel == null)
        {
            return;
        }

        foreach (Transform child in panel)
        {
            Button button = child.GetComponent<Button>();
            if (button == null)
            {
                continue;
            }

            Text legacy = child.GetComponentInChildren<Text>(true);
            string label = legacy != null ? legacy.text : string.Empty;
            if (legacy != null)
            {
                legacy.enabled = false;
            }

            TextMeshProUGUI tmp = GetOrCreateTmp(
                child,
                "PreviewLabel",
                semiBoldFont,
                11f,
                TextPrimary,
                TextAlignmentOptions.Center);
            SetStretch(tmp.rectTransform, 0f);
            tmp.text = label;
        }
    }

    private void ApplySummary()
    {
        Transform summary = contentRoot.Find("ApprovedSummary");
        if (summary == null)
        {
            return;
        }

        ReplaceSyncedText(
            summary.Find("Count"),
            "PreviewCount",
            bodyFont,
            10f,
            TextMuted,
            TextAlignmentOptions.MidlineLeft);

        ReplaceSyncedText(
            summary.Find("State"),
            "PreviewState",
            semiBoldFont,
            10f,
            Olive,
            TextAlignmentOptions.Right);
    }

    private void ApplyPlacementStrip()
    {
        Transform strip = contentRoot.Find("ApprovedPlacement");
        if (strip == null)
        {
            return;
        }

        ReplaceSyncedText(
            strip.Find("PlacementText"),
            "PreviewPlacementText",
            semiBoldFont,
            11f,
            new Color32(52, 68, 43, 255),
            TextAlignmentOptions.MidlineLeft);

        Transform iconRoot = strip.Find("IconRoot");
        if (iconRoot != null)
        {
            Text legacyIcon = iconRoot.GetComponentInChildren<Text>(true);
            if (legacyIcon != null)
            {
                legacyIcon.enabled = false;
            }

            Image iconBackground = iconRoot.GetComponent<Image>();
            if (iconBackground != null)
            {
                iconBackground.color = Olive;
                BistroBuilderSurface.Apply(
                    iconBackground,
                    BistroBuilderSurfaceLevel.Base);
                NeutralizeInteractionVisuals(iconRoot);
            }

            AddVectorIcon(
                iconRoot,
                "PreviewCursor",
                RestaurantCatalogPreviewIcon.Cursor,
                Color.white,
                1.7f,
                8f);
        }

        Transform cancel = strip.Find("Cancel");
        if (cancel != null)
        {
            HideLegacyTexts(cancel);
            Image cancelImage = cancel.GetComponent<Image>();
            if (cancelImage != null)
            {
                cancelImage.color = Card;
                ApplyRounded(cancelImage, 10);
            }

            Button cancelButton = cancel.GetComponent<Button>();
            if (cancelButton != null)
            {
                cancelButton.transition = Selectable.Transition.None;
            }

            NeutralizeInteractionVisuals(cancel);
            AddVectorIcon(
                cancel,
                "PreviewClose",
                RestaurantCatalogPreviewIcon.Close,
                TextMuted,
                1.55f,
                8f);
        }
    }

    private void ApplyEmptyState()
    {
        if (scrollRect == null)
        {
            return;
        }

        Transform empty = scrollRect.transform.Find("Viewport/ApprovedEmptyState");
        if (empty == null)
        {
            return;
        }

        ReplaceSyncedText(
            empty.Find("Title"),
            "PreviewTitle",
            semiBoldFont,
            15f,
            TextPrimary,
            TextAlignmentOptions.Center);

        ReplaceSyncedText(
            empty.Find("Body"),
            "PreviewBody",
            bodyFont,
            11f,
            TextMuted,
            TextAlignmentOptions.Top);
    }

    private void ApplyItemGrid()
    {
        if (itemContainer == null)
        {
            return;
        }

        itemContainer.anchorMin = new Vector2(0f, 1f);
        itemContainer.anchorMax = new Vector2(1f, 1f);
        itemContainer.pivot = new Vector2(0.5f, 1f);
        itemContainer.anchoredPosition = Vector2.zero;
        itemContainer.sizeDelta = new Vector2(0f, itemContainer.sizeDelta.y);

        ContentSizeFitter fitter = itemContainer.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        GridLayoutGroup grid = itemContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.cellSize = new Vector2(178.5f, 245f);
            grid.spacing = new Vector2(11f, 12f);
            grid.padding = new RectOffset(0, 0, 0, 12);
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.12f;
            scrollRect.scrollSensitivity = 24f;
        }
    }

    private void ApplyCards()
    {
        if (itemContainer == null)
        {
            return;
        }

        foreach (Transform child in itemContainer)
        {
            RestaurantPlaceableCatalogItemView view =
                child.GetComponent<RestaurantPlaceableCatalogItemView>();
            if (view == null || view.Definition == null)
            {
                continue;
            }

            ApplyCard(child, view);
        }
    }

    private void ApplyCard(
        Transform card,
        RestaurantPlaceableCatalogItemView view)
    {
        RestaurantPlaceableItemDefinition definition = view.Definition;

        Image background = card.GetComponent<Image>();
        if (background != null)
        {
            background.color = Card;
        }

        Button cardButton = card.GetComponent<Button>();
        if (cardButton != null)
        {
            cardButton.transition = Selectable.Transition.None;
        }

        BistroBuilderInteractionSurface interaction =
            card.GetComponent<BistroBuilderInteractionSurface>();
        if (interaction != null)
        {
            interaction.enabled = false;
        }

        Transform interactionState = card.Find("Interaction state");
        if (interactionState != null)
        {
            interactionState.gameObject.SetActive(false);
        }

        Outline outline = card.GetComponent<Outline>() ??
            card.gameObject.AddComponent<Outline>();
        outline.effectColor = Olive;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        Shadow shadow = card.GetComponent<Shadow>() ??
            card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.07f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        Text oldName = card.Find("Name")?.GetComponent<Text>();
        Text oldDescription = card.Find("Description")?.GetComponent<Text>();
        Text oldPrice = card.Find("Price")?.GetComponent<Text>();

        if (oldName != null) oldName.enabled = false;
        if (oldDescription != null) oldDescription.enabled = false;
        if (oldPrice != null) oldPrice.enabled = false;

        TextMeshProUGUI name = GetOrCreateTmp(
            card,
            "PreviewName",
            semiBoldFont,
            13f,
            TextPrimary,
            TextAlignmentOptions.TopLeft);
        SetAnchors(
            name.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(10f, -199f),
            new Vector2(-10f, -160f));
        name.text = definition.DisplayName;
        name.enableWordWrapping = true;

        TextMeshProUGUI price = GetOrCreateTmp(
            card,
            "PreviewPrice",
            semiBoldFont,
            17f,
            PriceGreen,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(
            price.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0.54f, 0f),
            new Vector2(10f, 6f),
            new Vector2(-2f, 34f));
        price.text = definition.PurchasePrice.ToString("N0") + " €";

        RectTransform scopePill =
            CreateChildRect(card, "PreviewScopePill");
        SetAnchoredRect(
            scopePill,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-52f, 17f),
            new Vector2(82f, 19f));

        Image scopeBackground = scopePill.GetComponent<Image>() ??
            scopePill.gameObject.AddComponent<Image>();
        scopeBackground.color = Field;
        ApplyRounded(scopeBackground, 10);
        NeutralizeInteractionVisuals(scopePill);

        TextMeshProUGUI scope = GetOrCreateTmp(
            scopePill,
            "PreviewScope",
            bodyFont,
            8.5f,
            TextMuted,
            TextAlignmentOptions.Center);
        SetStretch(scope.rectTransform, 5f);
        scope.text = ScopeText(definition.PlacementScope);

        Transform favorite = card.Find("ApprovedFavorite");
        if (favorite != null)
        {
            HideLegacyTexts(favorite);
            Image favoriteImage = favorite.GetComponent<Image>();
            if (favoriteImage != null)
            {
                ApplyRounded(favoriteImage, 16);
            }
            NeutralizeInteractionVisuals(favorite);

            TextMeshProUGUI star = GetOrCreateTmp(
                favorite,
                "PreviewStar",
                semiBoldFont,
                16f,
                Color.white,
                TextAlignmentOptions.Center);
            SetStretch(star.rectTransform, 0f);
            star.text = "★";
        }

        Transform selected = card.Find("ApprovedSelected");
        if (selected != null)
        {
            HideLegacyTexts(selected);
            Image selectedImage = selected.GetComponent<Image>();
            if (selectedImage != null)
            {
                ApplyRounded(selectedImage, 16);
            }
            NeutralizeInteractionVisuals(selected);

            AddVectorIcon(
                selected,
                "PreviewCheck",
                RestaurantCatalogPreviewIcon.Check,
                Color.white,
                2.0f,
                7f);
        }
    }

    private void SyncDynamicText()
    {
        SyncPair(
            contentRoot.Find("ApprovedSummary/Count"),
            "PreviewCount");
        SyncPair(
            contentRoot.Find("ApprovedSummary/State"),
            "PreviewState");
        SyncPair(
            contentRoot.Find("ApprovedPlacement/PlacementText"),
            "PreviewPlacementText");

        if (scrollRect != null)
        {
            SyncPair(
                scrollRect.transform.Find(
                    "Viewport/ApprovedEmptyState/Title"),
                "PreviewTitle");
            SyncPair(
                scrollRect.transform.Find(
                    "Viewport/ApprovedEmptyState/Body"),
                "PreviewBody");
        }
    }

    private void SyncCategoryStates()
    {
        if (categoryBar == null)
        {
            return;
        }

        foreach (Transform child in categoryBar)
        {
            Transform legacyRuntimeIcon = child.Find("BB_Icon21B");
            if (legacyRuntimeIcon != null)
            {
                Destroy(legacyRuntimeIcon.gameObject);
            }

            Image background = child.GetComponent<Image>();
            TextMeshProUGUI label =
                child.Find("PreviewLabel")?.GetComponent<TextMeshProUGUI>();
            RestaurantCatalogPreviewIconGraphic icon =
                child.Find("PreviewIcon")?.GetComponent<
                    RestaurantCatalogPreviewIconGraphic>();

            if (background == null)
            {
                continue;
            }

            RestaurantPlaceableCatalogCategoryView view =
                child.GetComponent<RestaurantPlaceableCatalogCategoryView>();
            bool selected = view != null && view.IsSelected;

            background.color = selected
                ? Olive
                : new Color(1f, 1f, 1f, 0f);
            ResetGraphicTint(background);

            Button button = child.GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
            }

            NeutralizeInteractionVisuals(child);

            Color target = selected ? Color.white : TextPrimary;
            if (label != null) label.color = target;
            if (icon != null) icon.color = selected ? Color.white : TextMuted;
        }
    }

    private void SyncCardStates()
    {
        if (itemContainer == null)
        {
            return;
        }

        foreach (Transform child in itemContainer)
        {
            RestaurantPlaceableCatalogItemView view =
                child.GetComponent<RestaurantPlaceableCatalogItemView>();
            if (view == null || view.Definition == null)
            {
                continue;
            }

            TextMeshProUGUI name =
                child.Find("PreviewName")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI price =
                child.Find("PreviewPrice")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI scope =
                child.Find("PreviewScopePill/PreviewScope")?.GetComponent<TextMeshProUGUI>();

            if (name != null) name.text = view.Definition.DisplayName;
            if (price != null)
            {
                price.text =
                    view.Definition.PurchasePrice.ToString("N0") + " €";
            }
            if (scope != null)
            {
                scope.text = ScopeText(view.Definition.PlacementScope);
            }

            Transform favorite = child.Find("ApprovedFavorite");
            if (favorite != null)
            {
                TextMeshProUGUI star =
                    favorite.Find("PreviewStar")?.GetComponent<TextMeshProUGUI>();
                Image image = favorite.GetComponent<Image>();
                if (star != null && image != null)
                {
                    ResetGraphicTint(image);
                    bool active = ColorDistance(image.color, Gold) < 0.22f;
                    star.color = active ? Color.white : TextMuted;
                }
            }
        }
    }


    private void SyncScopeStates()
    {
        Transform bar = contentRoot != null
            ? contentRoot.Find("ApprovedScopeBar")
            : null;
        if (bar == null)
        {
            return;
        }

        foreach (Transform child in bar)
        {
            Button button = child.GetComponent<Button>();
            Image image = child.GetComponent<Image>();
            if (button == null || image == null)
            {
                continue;
            }

            Text legacy = child.GetComponentInChildren<Text>(true);
            TextMeshProUGUI label =
                child.Find("PreviewLabel")?.GetComponent<TextMeshProUGUI>();

            string text = legacy != null
                ? legacy.text
                : label != null
                    ? label.text
                    : string.Empty;

            button.transition = Selectable.Transition.None;
            ApplyRounded(image, 14);
            NeutralizeInteractionVisuals(child);

            if (text == "≡")
            {
                bool open = contentRoot.Find("ApprovedFilters")?.gameObject.activeSelf == true;
                image.color = open ? OliveSoft : Field;
                ResetGraphicTint(image);
                RestaurantCatalogPreviewIconGraphic icon =
                    child.Find("PreviewFilterIcon")?.GetComponent<RestaurantCatalogPreviewIconGraphic>();
                if (icon != null) icon.color = TextPrimary;
                continue;
            }

            bool selected = string.Equals(
                text,
                selectedScopeLabel,
                StringComparison.OrdinalIgnoreCase);

            image.color = selected ? Olive : Field;
            ResetGraphicTint(image);
            if (label != null)
            {
                label.color = selected ? Color.white : TextPrimary;
            }
        }
    }

    private void EnforcePreviewVisualAuthority()
    {
        if (contentRoot == null)
        {
            return;
        }

        Image panelImage = contentRoot.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = Panel;
            ApplyRounded(panelImage, 24);
            ResetGraphicTint(panelImage);
        }

        NeutralizeInteractionVisuals(contentRoot);

        Shadow panelShadow = contentRoot.GetComponent<Shadow>() ??
            contentRoot.gameObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.08f);
        panelShadow.effectDistance = new Vector2(0f, -2f);
        panelShadow.useGraphicAlpha = true;

        Transform header = contentRoot.Find("Header");
        TextMeshProUGUI title =
            header?.Find("PreviewTitle")?.GetComponent<TextMeshProUGUI>();
        if (title != null)
        {
            title.color = TextPrimary;
            title.font = semiBoldFont;
            title.fontSize = 27f;
            title.fontWeight = FontWeight.Bold;
            title.extraPadding = true;
            title.transform.SetAsLastSibling();
        }

        Transform close = header?.Find("ApprovedClose");
        if (close != null)
        {
            Image closeImage = close.GetComponent<Image>();
            if (closeImage != null)
            {
                closeImage.color = new Color(1f, 1f, 1f, 0f);
            }

            Button closeButton = close.GetComponent<Button>();
            if (closeButton != null)
            {
                closeButton.transition = Selectable.Transition.None;
            }

            NeutralizeInteractionVisuals(close);
        }

        Transform search = contentRoot.Find("ApprovedSearch");
        if (search != null)
        {
            Image searchImage = search.GetComponent<Image>();
            if (searchImage != null)
            {
                searchImage.color = Card;
                ApplyRounded(searchImage, 16);
                ResetGraphicTint(searchImage);
            }

            Outline searchOutline = search.GetComponent<Outline>() ??
                search.gameObject.AddComponent<Outline>();
            searchOutline.enabled = true;
            searchOutline.effectColor = new Color32(215, 209, 199, 255);
            searchOutline.effectDistance = new Vector2(0.75f, -0.75f);
            searchOutline.useGraphicAlpha = true;

            NeutralizeInteractionVisuals(search);
        }

        Transform filterPanel = contentRoot.Find("ApprovedFilters");
        if (filterPanel != null)
        {
            Image image = filterPanel.GetComponent<Image>();
            if (image != null) image.color = Field;
        }

        Transform placement = contentRoot.Find("ApprovedPlacement");
        if (placement != null)
        {
            Image image = placement.GetComponent<Image>();
            if (image != null) image.color = OliveSoft;
        }

        if (itemContainer != null)
        {
            foreach (Transform child in itemContainer)
            {
                RestaurantPlaceableCatalogItemView view =
                    child.GetComponent<RestaurantPlaceableCatalogItemView>();
                if (view == null || view.Definition == null)
                {
                    continue;
                }

                Image background = child.GetComponent<Image>();
                if (background != null)
                {
                    background.color = Card;
                    ApplyRounded(background, 16);
                    ResetGraphicTint(background);
                }

                Button button = child.GetComponent<Button>();
                if (button != null)
                {
                    button.transition = Selectable.Transition.None;
                }

                NeutralizeInteractionVisuals(child);

                Transform iconRoot = child.Find("IconRoot");
                Image iconBackground = iconRoot?.GetComponent<Image>();
                if (iconBackground != null)
                {
                    iconBackground.color = Card;
                    ResetGraphicTint(iconBackground);
                }

                Transform selectedMarker = child.Find("ApprovedSelected");
                bool selected = selectedMarker != null &&
                    selectedMarker.gameObject.activeSelf;

                Outline outline = child.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = true;
                    outline.effectColor = selected
                        ? Olive
                        : new Color32(226, 220, 211, 255);
                    outline.effectDistance = selected
                        ? new Vector2(1.5f, -1.5f)
                        : new Vector2(0.75f, -0.75f);
                }

                TextMeshProUGUI name =
                    child.Find("PreviewName")?.GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI price =
                    child.Find("PreviewPrice")?.GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI scope =
                    child.Find("PreviewScopePill/PreviewScope")?.GetComponent<TextMeshProUGUI>();

                if (name != null)
                {
                    name.color = TextPrimary;
                    name.font = semiBoldFont;
                    name.fontWeight = FontWeight.SemiBold;
                }

                if (price != null)
                {
                    price.color = PriceGreen;
                    price.font = semiBoldFont;
                    price.fontWeight = FontWeight.SemiBold;
                }

                if (scope != null)
                {
                    scope.color = TextMuted;
                    scope.font = bodyFont;
                }
            }
        }
    }

    private void ApplyPreviewGeometry()
    {
        if (contentRoot == null)
        {
            return;
        }

        RectTransform panelRect = contentRoot as RectTransform;
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.offsetMin = new Vector2(4f, 4f);
            panelRect.offsetMax = new Vector2(431f, -4f);
        }

        RectTransform header = contentRoot.Find("Header") as RectTransform;
        if (header != null)
        {
            SetTopRect(header, 20f, 20f, 18f, 40f);

            Transform close = header.Find("ApprovedClose");
            if (close is RectTransform closeRect)
            {
                SetAnchoredRect(
                    closeRect,
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-19f, 0f),
                    new Vector2(32f, 32f));
            }
        }

        RectTransform search = contentRoot.Find("ApprovedSearch") as RectTransform;
        if (search != null)
        {
            SetTopRect(search, 21f, 28f, 70f, 50f);
            BistroBuilderSurface surface = search.GetComponent<BistroBuilderSurface>();
            if (surface != null)
            {
                surface.SetBorder(BistroBuilderBorderState.Normal);
            }
        }

        if (categoryBar != null)
        {
            SetTopRect(categoryBar, 12f, 12f, 133f, 73f);

            HorizontalLayoutGroup layout =
                categoryBar.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 4f;
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = true;
            }

            foreach (Transform child in categoryBar)
            {
                LayoutElement element = child.GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.minWidth = 58f;
                    element.preferredWidth = 62f;
                    element.flexibleWidth = 1f;
                    element.minHeight = 72f;
                    element.preferredHeight = 72f;
                }

                Image image = child.GetComponent<Image>();
                if (image != null)
                {
                    ApplyRounded(image, 14);
                    NeutralizeInteractionVisuals(child);
                }
            }
        }

        EnsureSectionDivider();

        RectTransform scopeBar =
            contentRoot.Find("ApprovedScopeBar") as RectTransform;
        if (scopeBar != null)
        {
            SetTopRect(scopeBar, 21f, 20f, 230f, 37f);
        }

        RectTransform summary =
            contentRoot.Find("ApprovedSummary") as RectTransform;
        if (summary != null)
        {
            SetTopRect(summary, 21f, 28f, 274f, 16f);
        }

        RectTransform filters =
            contentRoot.Find("ApprovedFilters") as RectTransform;
        if (filters != null)
        {
            SetTopRect(filters, 21f, 20f, 294f, 72f);
        }

        RectTransform placement =
            contentRoot.Find("ApprovedPlacement") as RectTransform;
        bool placementActive =
            placement != null && placement.gameObject.activeSelf;
        bool filtersActive =
            filters != null && filters.gameObject.activeSelf;

        if (placement != null)
        {
            float top = filtersActive ? 373f : 296f;
            SetTopRect(placement, 21f, 28f, top, 50f);
        }

        if (scrollRect != null)
        {
            RectTransform scroll = scrollRect.transform as RectTransform;
            float scrollTop;

            if (filtersActive && placementActive)
                scrollTop = 432f;
            else if (filtersActive)
                scrollTop = 373f;
            else if (placementActive)
                scrollTop = 354f;
            else
                scrollTop = 296f;

            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(21f, 18f);
            scroll.offsetMax = new Vector2(-20f, -scrollTop);
            scrollRect.scrollSensitivity = 28f;

            StyleScrollbar();
        }

        ApplyItemGrid();
    }

    private void EnsureSectionDivider()
    {
        RectTransform divider =
            CreateChildRect(contentRoot, "PreviewDivider");
        divider.anchorMin = new Vector2(0f, 1f);
        divider.anchorMax = new Vector2(1f, 1f);
        divider.pivot = new Vector2(0.5f, 1f);
        divider.offsetMin = new Vector2(0f, -218f);
        divider.offsetMax = new Vector2(0f, -217f);

        Image image = divider.GetComponent<Image>() ??
            divider.gameObject.AddComponent<Image>();
        image.color = new Color32(232, 227, 219, 255);
        image.raycastTarget = false;
        divider.SetAsLastSibling();
    }

    private void StyleScrollbar()
    {
        if (scrollRect == null || scrollRect.verticalScrollbar == null)
        {
            return;
        }

        Scrollbar scrollbar = scrollRect.verticalScrollbar;
        RectTransform rect = scrollbar.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 0f);
        }

        Image background = scrollbar.GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(1f, 1f, 1f, 0f);
        }

        Graphic target = scrollbar.targetGraphic;
        if (target != null)
        {
            target.color = new Color32(183, 178, 169, 230);
        }

        scrollbar.transition = Selectable.Transition.None;
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

    private static void SetAnchoredRect(
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

    private void ConfigureCanvasForCrispUi()
    {
        Canvas canvas = contentRoot != null
            ? contentRoot.GetComponentInParent<Canvas>()
            : null;
        if (canvas != null)
        {
            canvas.pixelPerfect = true;
        }

        CanvasScaler scaler = contentRoot != null
            ? contentRoot.GetComponentInParent<CanvasScaler>()
            : null;
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
        }
    }


    private static void ResetGraphicTint(Graphic graphic)
    {
        if (graphic == null || graphic.canvasRenderer == null)
        {
            return;
        }

        // Selectable usa un multiplicador interno en CanvasRenderer. Al pasar
        // a Transition.None puede quedar el tinte disabled/pressed anterior.
        graphic.canvasRenderer.SetColor(Color.white);
    }

    private static void NeutralizeInteractionVisuals(Transform root)
    {
        if (root == null)
        {
            return;
        }

        BistroBuilderInteractionSurface interaction =
            root.GetComponent<BistroBuilderInteractionSurface>();
        if (interaction != null)
        {
            interaction.enabled = false;
        }

        Transform state = root.Find("Interaction state");
        if (state != null)
        {
            state.gameObject.SetActive(false);
        }

        Transform depth = root.Find("Surface depth");
        if (depth != null)
        {
            depth.gameObject.SetActive(false);
        }
    }

    private void ReplaceSyncedText(
        Transform legacyTransform,
        string previewName,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        if (legacyTransform == null)
        {
            return;
        }

        Text legacy = legacyTransform.GetComponent<Text>();
        if (legacy == null)
        {
            return;
        }

        legacy.enabled = false;

        TextMeshProUGUI tmp = GetOrCreateTmp(
            legacyTransform.parent,
            previewName,
            font,
            size,
            color,
            alignment);

        RectTransform source = legacy.rectTransform;
        RectTransform target = tmp.rectTransform;
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.offsetMin = source.offsetMin;
        target.offsetMax = source.offsetMax;
        tmp.text = legacy.text;
    }

    private static void SyncPair(
        Transform legacyTransform,
        string previewName)
    {
        if (legacyTransform == null ||
            legacyTransform.parent == null)
        {
            return;
        }

        Text legacy = legacyTransform.GetComponent<Text>();
        TextMeshProUGUI preview =
            legacyTransform.parent.Find(previewName)?.GetComponent<
                TextMeshProUGUI>();

        if (legacy != null && preview != null)
        {
            preview.text = legacy.text;
            preview.gameObject.SetActive(legacyTransform.gameObject.activeInHierarchy);
        }
    }

    private TextMeshProUGUI GetOrCreateTmp(
        Transform parent,
        string name,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI tmp = existing != null
            ? existing.GetComponent<TextMeshProUGUI>()
            : null;

        if (tmp == null)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            tmp = go.GetComponent<TextMeshProUGUI>();
        }

        tmp.font = font;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Normal;
        tmp.fontWeight = FontWeight.Regular;
        tmp.extraPadding = true;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.richText = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private void AddVectorIcon(
        Transform parent,
        string name,
        RestaurantCatalogPreviewIcon iconType,
        Color color,
        float width,
        float inset)
    {
        RectTransform rect = CreateChildRect(parent, name);
        SetStretch(rect, inset);

        RestaurantCatalogPreviewIconGraphic icon =
            rect.gameObject.GetComponent<RestaurantCatalogPreviewIconGraphic>() ??
            rect.gameObject.AddComponent<RestaurantCatalogPreviewIconGraphic>();
        icon.Icon = iconType;
        icon.color = color;
        icon.LineWidth = width;
        icon.raycastTarget = false;
    }

    private static RectTransform CreateChildRect(
        Transform parent,
        string name)
    {
        Transform existing = parent.Find(name);
        if (existing is RectTransform existingRect)
        {
            return existingRect;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void HideLegacyTexts(Transform root)
    {
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].enabled = false;
        }
    }

    private static RestaurantCatalogPreviewIcon MapCategoryIcon(
        string label)
    {
        string normalized = label != null
            ? label.Trim().ToLowerInvariant()
            : string.Empty;

        if (normalized.Contains("mesa"))
            return RestaurantCatalogPreviewIcon.Table;
        if (normalized.Contains("silla") ||
            normalized.Contains("asiento"))
            return RestaurantCatalogPreviewIcon.Chair;
        if (normalized.Contains("cocina"))
            return RestaurantCatalogPreviewIcon.Kitchen;
        if (normalized.Contains("decor"))
            return RestaurantCatalogPreviewIcon.Decoration;
        if (normalized.Contains("ilumin"))
            return RestaurantCatalogPreviewIcon.Lighting;
        return RestaurantCatalogPreviewIcon.All;
    }

    private static string ScopeText(
        RestaurantPlaceableEnvironmentScope scope)
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

    private static float ColorDistance(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) +
            Mathf.Abs(a.g - b.g) +
            Mathf.Abs(a.b - b.b);
    }


    private static void ApplyRounded(Image image, int radius)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = GetRoundedSprite(radius);
        image.type = Image.Type.Sliced;
    }

    private static Sprite GetRoundedSprite(int radius)
    {
        if (radius <= 10 && rounded10 != null) return rounded10;
        if (radius <= 14 && radius > 10 && rounded14 != null) return rounded14;
        if (radius <= 16 && radius > 14 && rounded16 != null) return rounded16;
        if (radius > 16 && rounded24 != null) return rounded24;

        int actualRadius = radius <= 10
            ? 10
            : radius <= 14
                ? 14
                : radius <= 16
                    ? 16
                    : 24;

        const int size = 64;
        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false)
        {
            name = "BB Catalog rounded " + actualRadius,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        float left = actualRadius - 0.5f;
        float right = size - actualRadius - 0.5f;
        float bottom = actualRadius - 0.5f;
        float top = size - actualRadius - 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x, left, right);
                float cy = Mathf.Clamp(y, bottom, top);
                float dx = x - cx;
                float dy = y - cy;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(actualRadius + 0.5f - distance);
                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float border = actualRadius + 2f;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));

        if (actualRadius == 10) rounded10 = sprite;
        else if (actualRadius == 14) rounded14 = sprite;
        else if (actualRadius == 16) rounded16 = sprite;
        else rounded24 = sprite;

        return sprite;
    }

    private static void SetStretch(
        RectTransform rect,
        float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetAnchors(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
