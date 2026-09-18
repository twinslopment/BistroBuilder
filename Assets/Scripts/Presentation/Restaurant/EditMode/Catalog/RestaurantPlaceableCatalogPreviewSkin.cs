using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RestaurantPlaceableCatalogPreviewSkin : MonoBehaviour
{
    private static readonly Color32 Panel = new Color32(251, 249, 244, 255);
    private static readonly Color32 Card = new Color32(255, 253, 250, 255);
    private static readonly Color32 Field = new Color32(243, 240, 233, 255);
    private static readonly Color32 TextPrimary = new Color32(24, 28, 29, 255);
    private static readonly Color32 TextMuted = new Color32(119, 123, 120, 255);
    private static readonly Color32 Olive = new Color32(113, 143, 77, 255);
    private static readonly Color32 Gold = new Color32(237, 169, 34, 255);

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
        }

        if (categoryBar != null && categoryBar.childCount != categoryCount)
        {
            ApplyCategories();
            categoryCount = categoryBar.childCount;
        }

        if (itemContainer != null && itemContainer.childCount != itemCount)
        {
            ApplyItemGrid();
            ApplyCards();
            itemCount = itemContainer.childCount;
        }

        SyncDynamicText();
        SyncCategoryStates();
        SyncCardStates();
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

        RectTransform area = CreateChildRect(inputRoot, "PreviewTextArea");
        SetAnchors(
            area,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(44f, 4f),
            new Vector2(-14f, -4f));

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
                semiBoldFont,
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
                new Vector2(-12f, -21f),
                new Vector2(12f, 3f));

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
                semiBoldFont,
                12.5f,
                TextPrimary,
                TextAlignmentOptions.Center);
            SetStretch(tmp.rectTransform, 0f);
            tmp.text = label;
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

        Transform cancel = strip.Find("Cancel");
        if (cancel != null)
        {
            HideLegacyTexts(cancel);
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
            grid.cellSize = new Vector2(181f, 246f);
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(0, 0, 0, 12);
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        if (scrollRect != null)
        {
            scrollRect.scrollSensitivity = 28f;
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
            new Color32(62, 123, 48, 255),
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(
            price.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0.54f, 0f),
            new Vector2(10f, 6f),
            new Vector2(-2f, 34f));
        price.text = definition.PurchasePrice.ToString("N0") + " €";

        TextMeshProUGUI scope = GetOrCreateTmp(
            card,
            "PreviewScope",
            bodyFont,
            9f,
            TextMuted,
            TextAlignmentOptions.Right);
        SetAnchors(
            scope.rectTransform,
            new Vector2(0.50f, 0f),
            new Vector2(1f, 0f),
            new Vector2(2f, 6f),
            new Vector2(-10f, 34f));
        scope.text = ScopeText(definition.PlacementScope);

        Transform favorite = card.Find("ApprovedFavorite");
        if (favorite != null)
        {
            HideLegacyTexts(favorite);
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

            bool selected = ColorDistance(background.color, Olive) < 0.20f;
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
                child.Find("PreviewScope")?.GetComponent<TextMeshProUGUI>();

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
                    bool active = ColorDistance(image.color, Gold) < 0.22f;
                    star.color = active ? Color.white : TextMuted;
                }
            }
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
