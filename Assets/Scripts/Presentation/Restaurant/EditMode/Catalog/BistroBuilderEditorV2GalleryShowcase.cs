using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual-only Gallery Viva editorial strip. Reads the canonical catalog, never
/// creates items/prices or bypasses B9 filtering or placement/finance authority.
/// Reserved space is restored when hidden so the scrollable catalog remains usable.
/// </summary>
[DisallowMultipleComponent, DefaultExecutionOrder(800)]
public sealed class BistroBuilderEditorV2GalleryShowcase : MonoBehaviour
{
    private static readonly Color Ivory = new Color32(255, 246, 229, 255);
    private static readonly Color Cream = new Color32(246, 231, 207, 255);
    private static readonly Color Honey = new Color32(239, 185, 93, 255);
    private static readonly Color Brass = new Color32(168, 117, 59, 255);
    private static readonly Color Ink = new Color32(72, 44, 26, 255);
    private static readonly Color Muted = new Color32(130, 98, 68, 255);
    private RestaurantPlaceableCatalogPanel panel;
    private RestaurantPlaceableCatalogService service;
    private RestaurantPlaceableInspectorPanel inspector;
    private BistroBuilderEditorV2SelectionCoordinator selection;
    private RectTransform root;
    private ScrollRect itemsScroll;
    private TMP_FontAsset recoleta, inter;
    private GameObject relatedArea;
    private Image mainImage;
    private TMP_Text mainName, mainDescription, mainPrice, mainAction;
    private readonly Image[] relatedImages = new Image[3];
    private readonly TMP_Text[] relatedNames = new TMP_Text[3];
    private readonly Button[] relatedButtons = new Button[3];
    private readonly RestaurantPlaceableItemDefinition[] relatedDefinitions =
        new RestaurantPlaceableItemDefinition[3];
    private RestaurantPlaceableItemDefinition featured;
    private readonly List<RestaurantPlaceableItemDefinition> candidates =
        new List<RestaurantPlaceableItemDefinition>(24);
    private Button mainButton;
    private int previousCategory = int.MinValue;
    private int previousCatalogCount = -1;
    private RestaurantEditCatalogSection previousSection;
    private bool previousCompact;
    private bool showing;
    public bool IsShowing => showing && root != null && root.gameObject.activeSelf;
    public RestaurantPlaceableItemDefinition Featured => featured;
    public int RelatedCount { get; private set; }

    private void Awake()
    {
        panel = GetComponent<RestaurantPlaceableCatalogPanel>();
        inspector = GetComponent<RestaurantPlaceableInspectorPanel>();
        recoleta = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Recoleta-SDF");
        inter = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Inter-Regular-SDF");
        if (recoleta == null) recoleta = TMP_Settings.defaultFontAsset;
        if (inter == null) inter = TMP_Settings.defaultFontAsset;
    }

    private void OnDisable()
    {
        showing = false;
        if (root != null) root.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (GetComponent<BistroBuilderGalleryViva>() != null) return;
        if (panel == null) return;
        if (service == null)
            service = FindFirstObjectByType<RestaurantPlaceableCatalogService>(
                FindObjectsInactive.Include);
        if (selection == null)
            selection = FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>(
                FindObjectsInactive.Include);
        var content = transform.Find("CatalogContent") as RectTransform;
        if (content == null) return;
        if (root == null) Build(content);
        if (itemsScroll == null)
            itemsScroll = content.Find("ItemsScroll")?.GetComponent<ScrollRect>();
        if (root == null || itemsScroll == null) return;

        RectTransform scrollRect = itemsScroll.transform as RectTransform;
        bool premium = GetComponent<BistroBuilderEditorV2PremiumLayout>() != null;
        bool compact = premium ? Screen.width < 1500 || Screen.height < 840 :
            content.rect.height < 635f;
        bool hasOverlay = content.Find("ApprovedFilters")?.gameObject.activeSelf == true ||
            content.Find("ApprovedPlacement")?.gameObject.activeSelf == true;
        var list = service != null ? service.AvailableItems : null;
        int count = list != null ? list.Count : 0;
        bool allowed = panel.CurrentSection == RestaurantEditCatalogSection.Build &&
            count > 0 && !hasOverlay;
        if (allowed && (previousCategory != panel.SelectedCategoryCode ||
            previousCatalogCount != count || previousSection != panel.CurrentSection))
        {
            previousCategory = panel.SelectedCategoryCode;
            previousCatalogCount = count;
            previousSection = panel.CurrentSection;
            Rebind(list);
        }
        showing = allowed && featured != null;
        if (root.gameObject.activeSelf != showing)
            root.gameObject.SetActive(showing);
        if (previousCompact != compact)
        {
            previousCompact = compact;
            if (relatedArea != null) relatedArea.SetActive(!compact && RelatedCount > 0);
        }

        if (!showing) return;
        bool catalogClickAllowed = selection == null ||
            selection.SelectionCount == 0;
        if (mainButton != null) mainButton.interactable = catalogClickAllowed;
        for (int i = 0; i < relatedButtons.Length; i++)
            if (relatedButtons[i] != null)
                relatedButtons[i].interactable = catalogClickAllowed;
        float top = premium ? 222f : 286f;
        float block = premium ? (compact ? 194f : 300f) :
            (compact ? 84f : (RelatedCount == 0 ? 142f : 208f));
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        float rail = premium ? (compact ? 162f : 183f) : 0f;
        root.anchoredPosition = new Vector2(premium ? rail + 10f : 20f, -top);
        root.sizeDelta = new Vector2(Mathf.Max(220f,
            content.rect.width - (premium ? rail + 23f : 40f)), block);
        var featuredCard = root.Find("FeaturedCard") as RectTransform;
        if (featuredCard != null)
        {
            featuredCard.anchorMin = new Vector2(0, 1);
            featuredCard.anchorMax = new Vector2(1, 1);
            featuredCard.pivot = new Vector2(.5f, 1);
            featuredCard.offsetMin = new Vector2(0, -(premium
                ? (compact ? 194f : 190f)
                : (compact ? 114f : 142f)));
            featuredCard.offsetMax = Vector2.zero;
        }
        if (mainImage != null)
        {
            var imgRect = mainImage.rectTransform;
            imgRect.sizeDelta = new Vector2(premium
                ? (compact ? 132f : 160f) : (compact ? 68f : 120f),
                premium ? (compact ? 169f : 169f)
                    : (compact ? 90f : 109f));
        }
        if (mainDescription != null)
            mainDescription.gameObject.SetActive(premium || !compact);
        if (mainName != null)
        {
            float labelX = premium ? (compact ? 150f : 178f) :
                (compact ? 83f : 139f);
            Fixed(mainName.rectTransform, labelX,
                premium ? (compact ? 107f : 105f) : (compact ? 38f : 73f),
                premium ? (compact ? 270f : 275f) :
                    (compact ? 152f : 225f),
                premium ? 48f : (compact ? 30f : 42f));
            if (premium && mainDescription != null)
                Fixed(mainDescription.rectTransform, labelX,
                    compact ? 63f : 54f, compact ? 255f : 265f, 46f);
            Fixed(mainPrice.rectTransform, labelX,
                premium ? 15f : (compact ? 4f : 8f), 105f, 30f);
            Fixed(mainAction.rectTransform,
                premium ? (compact ? 290f : 327f) :
                    (compact ? 151f : 244f),
                premium ? 15f : (compact ? 4f : 8f),
                premium ? 132f : (compact ? 95f : 104f), 30f);
            var badge = featuredCard != null ?
                featuredCard.Find("Badge") as RectTransform : null;
            if (badge != null)
                Fixed(badge, labelX,
                    premium ? (compact ? 164f : 165f) :
                        (compact ? 79f : 125f), 156f, 19f);
        }
        if (relatedArea != null)
            relatedArea.SetActive(!compact && RelatedCount > 0);
        var scroll = scrollRect;
        float minimum = premium ? (compact ? 434f : 532f) :
            top + block + 9f;
        // The preview skin sets the ordinary top inset; reserve ONLY the extra
        // height occupied by this strip and preserve the existing bottom edge.
        if (scroll.offsetMax.y > -minimum)
            scroll.offsetMax = new Vector2(scroll.offsetMax.x, -minimum);
    }

    private void Rebind(IReadOnlyList<RestaurantPlaceableItemDefinition> all)
    {
        candidates.Clear();
        int category = panel.SelectedCategoryCode;
        for (int i = 0; i < all.Count; i++)
        {
            var definition = all[i];
            if (definition == null || !definition.HasValidPrefab) continue;
            if (category >= 0 && (int)definition.Category != category) continue;
            candidates.Add(definition);
        }
        // A gallery highlight must not blindly promote a test cube priced
        // at zero when authored, purchasable furniture exists. All values
        // still come from the live catalog, with deterministic tie-breaking.
        int bestIndex = -1;
        int bestScore = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            var item = candidates[i];
            int score = item.InspectorPreview != null ? 4 : 0;
            if (item.PurchasePrice > 0) score += 4;
            if (item.Category == RestaurantPlaceableItemCategory.Furniture)
                score += 2;
            if (item.DisplayName.IndexOf("bistr", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 1;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        featured = bestIndex >= 0 ? candidates[bestIndex] : null;
        if (featured == null)
        {
            RelatedCount = 0;
            return;
        }
        mainName.text = featured.DisplayName;
        mainDescription.text = string.IsNullOrWhiteSpace(featured.Description)
            ? "Descubre este artículo en el inspector."
            : featured.Description;
        mainPrice.text = featured.PurchasePrice.ToString("N0") + " €";
        mainImage.sprite = featured.InspectorPreview;
        mainImage.enabled = mainImage.sprite != null;
        // Related articles prefer the same family, then other genuine
        // catalog items. No duplicate featured card or placeholder records.
        RelatedCount = 0;
        for (int pass = 0; pass < 2 && RelatedCount < 3; pass++)
        {
            for (int i = 0; i < candidates.Count && RelatedCount < 3; i++)
            {
                var item = candidates[i];
                if (item == featured) continue;
                bool sameFamily = item.Category == featured.Category;
                if ((pass == 0) != sameFamily) continue;
                relatedDefinitions[RelatedCount++] = item;
            }
        }
        for (int i = 0; i < relatedDefinitions.Length; i++)
        {
            if (i >= RelatedCount) relatedDefinitions[i] = null;
            relatedButtons[i].gameObject.SetActive(i < RelatedCount);
            if (i >= RelatedCount) continue;
            var definition = relatedDefinitions[i];
            relatedNames[i].text = definition.DisplayName;
            relatedImages[i].sprite = definition.InspectorPreview;
            relatedImages[i].enabled = relatedImages[i].sprite != null;
        }
    }

    private void OpenDefinition(RestaurantPlaceableItemDefinition definition)
    {
        if (definition == null || inspector == null) return;
        // World selection has precedence over an unrelated catalog detail.
        if (selection != null && selection.SelectionCount > 0) return;
        inspector.ShowForDefinition(definition);
    }

    public void OpenFeatured() => OpenDefinition(featured);

    public void OpenRelated(int index)
    {
        if (index < 0 || index >= RelatedCount) return;
        OpenDefinition(relatedDefinitions[index]);
    }

    private void Build(RectTransform content)
    {
        root = NewRect("BB_EditorV2_GalleryShowcase", content);
        var card = NewRect("FeaturedCard", root);
        var cardImage = card.gameObject.AddComponent<Image>();
        BistroBuilderEditorV2BrassArt.Decorate(cardImage,
            BistroBuilderEditorV2BrassArt.Style.Dark);
        var border = card.gameObject.AddComponent<Outline>();
        border.effectColor = Brass;
        border.effectDistance = new Vector2(1f, -1f);
        mainButton = card.gameObject.AddComponent<Button>();
        mainButton.targetGraphic = cardImage;
        mainButton.onClick.AddListener(OpenFeatured);
        mainImage = NewRect("ArticleImage", card).gameObject.AddComponent<Image>();
        mainImage.preserveAspect = true;
        mainImage.raycastTarget = false;
        var imageRect = mainImage.rectTransform;
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0f, .5f);
        imageRect.pivot = new Vector2(0f, .5f);
        imageRect.anchoredPosition = new Vector2(8, 0);
        var badge = CreateText(card, "Badge", "DESTACADO", 11,
            new Color32(253, 204, 115, 255), inter);
        Fixed(badge.rectTransform, 139, 104, 135, 16);
        mainName = CreateText(card, "Name", "Artículo", 22,
            new Color32(255, 246, 224, 255), recoleta);
        Fixed(mainName.rectTransform, 139, 65, 225, 35);
        mainDescription = CreateText(card, "Description", "", 15,
            new Color32(244, 224, 192, 255), inter);
        mainDescription.textWrappingMode = TextWrappingModes.Normal;
        Fixed(mainDescription.rectTransform, 139, 28, 222, 37);
        mainPrice = CreateText(card, "Price", "", 19,
            new Color32(255, 208, 121, 255), inter);
        Fixed(mainPrice.rectTransform, 139, 7, 82, 24);
        mainAction = CreateText(card, "Action", "Ver ficha ›", 15, Brass, inter);
        Fixed(mainAction.rectTransform, 244, 7, 112, 24);

        relatedArea = NewRect("Related", root).gameObject;
        var relatedRect = relatedArea.GetComponent<RectTransform>();
        relatedRect.anchorMin = new Vector2(0, 0);
        relatedRect.anchorMax = new Vector2(1, 0);
        relatedRect.pivot = new Vector2(.5f, 0);
        relatedRect.offsetMin = new Vector2(0, 0);
        relatedRect.offsetMax = new Vector2(0, 103);
        var relatedTitle = CreateText(relatedRect, "Heading", "Artículos relacionados",
            17, Ink, recoleta);
        relatedTitle.rectTransform.anchorMin = new Vector2(0, 1);
        relatedTitle.rectTransform.anchorMax = new Vector2(1, 1);
        relatedTitle.rectTransform.offsetMin = new Vector2(5, -24);
        relatedTitle.rectTransform.offsetMax = new Vector2(-5, 0);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var mini = NewRect("Related_" + i, relatedRect);
            mini.anchorMin = new Vector2(i / 3f, 0f);
            mini.anchorMax = new Vector2((i + 1) / 3f, 0f);
            mini.pivot = new Vector2(.5f, 0);
            mini.offsetMin = new Vector2(2f, 0);
            mini.offsetMax = new Vector2(-2f, 76);
            mini.gameObject.AddComponent<Image>().color = Cream;
            relatedButtons[i] = mini.gameObject.AddComponent<Button>();
            relatedButtons[i].onClick.AddListener(() => OpenRelated(index));
            relatedImages[i] = NewRect("Thumbnail", mini).gameObject.AddComponent<Image>();
            relatedImages[i].preserveAspect = true;
            relatedImages[i].raycastTarget = false;
            Fixed(relatedImages[i].rectTransform, 6, 5, 68, 58);
            relatedNames[i] = CreateText(mini, "Title", "", 13, Ink, inter);
            relatedNames[i].rectTransform.anchorMin = Vector2.zero;
            relatedNames[i].rectTransform.anchorMax = Vector2.one;
            relatedNames[i].rectTransform.offsetMin = new Vector2(77, 4);
            relatedNames[i].rectTransform.offsetMax = new Vector2(-2, -2);
        }
        root.gameObject.SetActive(false);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static TMP_Text CreateText(Transform parent, string name, string value,
        float size, Color color, TMP_FontAsset font)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void Fixed(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
    }
}
