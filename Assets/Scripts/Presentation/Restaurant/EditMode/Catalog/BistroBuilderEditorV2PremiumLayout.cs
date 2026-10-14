using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single visual geometry authority for the approved five-zone Editor V2.
/// Does not own catalog selection, finance, placement, B8 or construction.
/// Runs after both old visual skins; prevents their fixed horizontal geometry
/// from remaining on screen when the player opens the genuine V2 editor.
/// </summary>
[DisallowMultipleComponent, DefaultExecutionOrder(2500)]
public sealed class BistroBuilderEditorV2PremiumLayout : MonoBehaviour
{
    private RestaurantPlaceableCatalogPanel catalog;
    private RestaurantPlaceableCatalogApprovedSkin approved;
    private RectTransform panel, categories, scroll, showcase;
    private Transform header, scope, summary, search;
    private HorizontalLayoutGroup oldCategoryLayout;
    private BistroBuilderEditorV2GalleryShowcase gallery;
    private BistroBuilderEditorV2SelectionInspector worldInspector;
    private TMP_FontAsset headingFont, bodyFont;
    private GameObject tabs;
    private Button[] tabButtons;
    private bool lastCompact;
    private float lastWidth = -1;
    private int categoryCount = -1;
    private int lastTab = -1;
    private RectTransform lastStyledInspector;
    private TMP_Text[] inspectorTextCache;

    public bool IsReady => panel != null && categories != null &&
        scroll != null && tabs != null;
    public int VisibleCategoryCount => categories != null ? categories.childCount : 0;

    private void Awake()
    {
        catalog = GetComponent<RestaurantPlaceableCatalogPanel>();
        approved = GetComponent<RestaurantPlaceableCatalogApprovedSkin>();
        gallery = GetComponent<BistroBuilderEditorV2GalleryShowcase>();
        worldInspector = GetComponent<BistroBuilderEditorV2SelectionInspector>();
        headingFont = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Recoleta-SDF");
        bodyFont = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Inter-Regular-SDF");
    }

    private void LateUpdate()
    {
        if (catalog == null) return;
        if (GetComponent<BistroBuilderGalleryViva>() != null) { StyleInspector(); return; }
        if (!Bind()) return;
        if (!panel.gameObject.activeInHierarchy) return;
        // First entry has not necessarily pressed a category button yet.
        // Initialise exactly one specialist; do not override active tools.
        catalog.EnsureEditorV2DefaultAuthority();
        bool compact = Screen.width < 1500 || Screen.height < 840;
        // Reference proportions: gallery roughly one third of the window,
        // leaving a true central viewport even when the inspector is expanded.
        float contentWidth = compact ?
            Mathf.Clamp(Screen.width * .50f, 585f, 650f) :
            Mathf.Clamp(Screen.width * .355f, 640f, 725f);
        bool geometryChanged = Mathf.Abs(lastWidth - contentWidth) > .5f ||
            compact != lastCompact || categoryCount != categories.childCount;
        if (geometryChanged)
        {
            lastWidth = contentWidth;
            lastCompact = compact;
            categoryCount = categories.childCount;
            ApplyCategoryLayout(compact);
        }
        // Geometry intentionally applied last every frame, since the legacy
        // category skin still updates its own rects when filters change.
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, .5f);
        panel.offsetMin = new Vector2(9f, 113f);
        panel.offsetMax = new Vector2(9f + contentWidth, -85f);

        DecorateBackground();
        PlaceTop(header as RectTransform, 18f, 15f, contentWidth - 36f, 46);
        PlaceTop(search as RectTransform, 18f, 66f, contentWidth - 36f, 49);
        PlaceTop(tabs.transform as RectTransform,
            18f, 124f, contentWidth - 36f, 40f);
        if (scope is RectTransform scopeRect)
        {
            // Interior/exterior remains accessible as secondary filter.
            PlaceTop(scopeRect, 18f, 170f, contentWidth - 36f, 35f);
        }
        if (summary is RectTransform summaryRect)
        {
            float rail = compact ? 165f : 186f;
            PlaceTop(summaryRect, rail + 13f, compact ? 328f : 504f,
                contentWidth - rail - 27f, 24f);
        }
        float railWidth = compact ? 162f : 183f;
        float top = 222f;
        PlaceTop(categories, 10f, top, railWidth, Mathf.Max(300f,
            categories.childCount * (compact ? 43f : 48f)));
        float scrollTop = compact ? 434f : 532f;
        scroll.anchorMin = Vector2.zero;
        scroll.anchorMax = Vector2.one;
        scroll.offsetMin = new Vector2(railWidth + 16, 12);
        scroll.offsetMax = new Vector2(-13f, -scrollTop);
        var list = GetComponent<BistroBuilderEditorV2CatalogVirtualizedList>();
        if (geometryChanged && list != null && list.IsReady)
            list.InvalidateGeometry();

        RefreshTabVisuals();
        StyleInspector();
    }

    private bool Bind()
    {
        if (panel == null)
        {
            panel = transform.Find("CatalogContent") as RectTransform;
            if (panel == null) return false;
        }
        if (categories == null)
            categories = panel.Find("CategoryBar") as RectTransform;
        if (scroll == null)
            scroll = panel.Find("ItemsScroll") as RectTransform;
        if (header == null) header = panel.Find("Header");
        if (scope == null) scope = panel.Find("ApprovedScopeBar");
        if (summary == null) summary = panel.Find("ApprovedSummary");
        if (search == null) search = panel.Find("ApprovedSearch");
        if (showcase == null)
            showcase = panel.Find("BB_EditorV2_GalleryShowcase") as RectTransform;
        if (categories == null || scroll == null ||
            header == null || search == null) return false;
        if (tabs == null) BuildTabs();
        return tabs != null;
    }

    private void BuildTabs()
    {
        tabs = new GameObject("BB_EditorV2_ApprovedTabs",
            typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabs.transform.SetParent(panel, false);
        var layout = tabs.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 7f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        tabButtons = new Button[3];
        string[] labels = { "Todos", "★ Favoritos", "Recientes" };
        for (int i = 0; i < labels.Length; i++)
        {
            int captured = i;
            var go = new GameObject("Tab_" + i, typeof(RectTransform),
                typeof(Image), typeof(Button));
            go.transform.SetParent(tabs.transform, false);
            var bg = go.GetComponent<Image>();
            BistroBuilderEditorV2BrassArt.Decorate(bg,
                i == 0 ? BistroBuilderEditorV2BrassArt.Style.Selected :
                BistroBuilderEditorV2BrassArt.Style.Button);
            tabButtons[i] = go.GetComponent<Button>();
            tabButtons[i].targetGraphic = bg;
            tabButtons[i].onClick.AddListener(
                () => approved?.SetGalleryTab(captured));
            var child = new GameObject("Caption", typeof(RectTransform),
                typeof(TextMeshProUGUI));
            child.transform.SetParent(go.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2, 1);
            rect.offsetMax = new Vector2(-2, -1);
            var label = child.GetComponent<TextMeshProUGUI>();
            label.text = labels[i];
            label.font = bodyFont != null ? bodyFont : TMP_Settings.defaultFontAsset;
            label.fontSize = 12.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(77, 46, 23, 255);
            label.raycastTarget = false;
        }
    }

    private void RefreshTabVisuals()
    {
        int current = approved != null ? approved.SelectedGalleryTab : 0;
        if (current == lastTab) return;
        lastTab = current;
        for (int i = 0; i < tabButtons.Length; i++)
            BistroBuilderEditorV2BrassArt.Decorate(
                tabButtons[i].GetComponent<Image>(),
                i == current ? BistroBuilderEditorV2BrassArt.Style.Selected
                    : BistroBuilderEditorV2BrassArt.Style.Button);
    }

    private void ApplyCategoryLayout(bool compact)
    {
        oldCategoryLayout = categories.GetComponent<HorizontalLayoutGroup>();
        if (oldCategoryLayout != null) oldCategoryLayout.enabled = false;
        float step = compact ? 47f : 52f;
        int index = 0;
        foreach (Transform child in categories)
        {
            var rt = child as RectTransform;
            if (rt == null || !rt.gameObject.activeSelf) continue;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(.5f, 1f);
            rt.offsetMin = new Vector2(1f, -(index + 1) * step + 3);
            rt.offsetMax = new Vector2(-1f, -index * step);
            index++;
            // The PreviewSkin already builds the canonical category icon
            // and a TMP label. Lay them out as a readable horizontal row.
            var label = child.Find("PreviewLabel") as RectTransform;
            if (label != null)
            {
                label.anchorMin = Vector2.zero;
                label.anchorMax = Vector2.one;
                label.offsetMin = new Vector2(45f, 2f);
                label.offsetMax = new Vector2(-5f, -2f);
                var tmp = label.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.enableAutoSizing = false;
                    tmp.fontSize = compact ? 16f : 17f;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    tmp.alignment = TextAlignmentOptions.MidlineLeft;
                    tmp.color = new Color32(74, 45, 28, 255);
                }
            }
            var icon = child.Find("PreviewIcon") as RectTransform;
            if (icon != null)
            {
                icon.anchorMin = icon.anchorMax = new Vector2(0f, .5f);
                icon.pivot = new Vector2(.5f, .5f);
                icon.anchoredPosition = new Vector2(23f, 0f);
                icon.sizeDelta = new Vector2(28f, 28f);
            }
        }
    }

    private void DecorateBackground()
    {
        var image = panel.GetComponent<Image>();
        if (image != null && image.sprite !=
            BistroBuilderEditorV2BrassArt.Get(
                BistroBuilderEditorV2BrassArt.Style.Frame))
            BistroBuilderEditorV2BrassArt.Decorate(image,
                BistroBuilderEditorV2BrassArt.Style.Frame);
        foreach (Transform t in categories)
        {
            var view = t.GetComponent<RestaurantPlaceableCatalogCategoryView>();
            var categoryBg = t.GetComponent<Image>();
            if (view == null || categoryBg == null) continue;
            BistroBuilderEditorV2BrassArt.Decorate(categoryBg, view.IsSelected
                ? BistroBuilderEditorV2BrassArt.Style.Selected
                : BistroBuilderEditorV2BrassArt.Style.Button);
        }
    }

    private void StyleInspector()
    {
        var inspector = transform.Find("BB_UIUX_PlaceableInspector") as RectTransform;
        if (inspector == null)
        {
            var existing = FindFirstObjectByType<RestaurantPlaceableInspectorPanel>(
                FindObjectsInactive.Include);
            if (existing != null)
                inspector = existing.transform.Find("BB_UIUX_PlaceableInspector")
                    as RectTransform;
        }
        if (inspector != null)
        {
            var bg = inspector.GetComponent<Image>();
            if (bg != null)
                BistroBuilderEditorV2BrassArt.Decorate(bg,
                    BistroBuilderEditorV2BrassArt.Style.Frame);

            // Avoid stale light TMP tints after changing a catalog selection.
            if (lastStyledInspector != inspector || inspectorTextCache == null)
            {
                lastStyledInspector = inspector;
                inspectorTextCache = inspector.GetComponentsInChildren<TMP_Text>(true);
            }
            for (int i = 0; i < inspectorTextCache.Length; i++)
            {
                TMP_Text label = inspectorTextCache[i];
                if (label == null || label.name == "StatusIcon")
                    continue;
                Color ink = label.color;
                if (ink.r > .77f && ink.g > .77f && ink.b > .73f)
                    label.color = new Color32(75, 45, 27, 255);
                else if (ink.a < .9f)
                {
                    ink.a = 1f;
                    label.color = ink;
                }
            }
        }
    }

    private static void PlaceTop(RectTransform rect,
        float x, float y, float width, float height)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(Mathf.Max(30f, width), height);
    }
}
