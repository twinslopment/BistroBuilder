using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor V2: read-only presentation of the canonical B8 selection.
/// The existing catalog inspector retains authority for catalog item previews.
/// No world edit operations or independent selection model are created here.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEditorV2SelectionInspector : MonoBehaviour
{
    private static readonly Color32 Cream = new Color32(250, 239, 221, 255);
    private static readonly Color32 Ivory = new Color32(255, 249, 237, 255);
    private static readonly Color32 Honey = new Color32(238, 188, 99, 255);
    private static readonly Color32 Ink = new Color32(72, 44, 25, 255);
    private static readonly Color32 Muted = new Color32(124, 93, 69, 255);
    private static readonly Color32 Brass = new Color32(166, 117, 60, 255);

    private BistroBuilderEditorV2SelectionCoordinator selection;
    private RestaurantPlaceableRegistry registry;
    private RestaurantEditModeService editMode;
    private BistroBuilderUiShell shell;
    private RestaurantPlaceableInspectorPanel catalogInspector;
    private RectTransform root;
    private GameObject panelBody;
    private GameObject rail;
    private TMP_Text title;
    private TMP_Text meta;
    private TMP_Text groupSummary;
    private TMP_Text groupCapabilities;
    private TMP_Text description;
    private TMP_Text dimensions;
    private TMP_Text price;
    private Image preview;
    private RectTransform list;
    private GameObject singlePage;
    private GameObject groupPage;
    private Image singleTab;
    private Image groupTab;
    private TMP_Text railCount;
    private TMP_FontAsset regular;
    private TMP_FontAsset heading;
    private bool showingGroup;
    private bool expanded = true;
    private bool compact;
    private long lastRevision = long.MinValue;
    private string lastPrimary = "";
    private readonly List<GameObject> groupRows = new List<GameObject>(16);
    private readonly List<BistroBuilderEditorV2Selection> scratch =
        new List<BistroBuilderEditorV2Selection>(16);

    public bool IsShowing { get; private set; }
    public bool IsExpanded => IsShowing && expanded;
    public int VisibleSelectionCount =>
        IsShowing && selection != null ? Math.Max(1, selection.SelectionCount) : 0;

    private void Start()
    {
        Resolve();
        Build();
    }

    private void OnDisable()
    {
        IsShowing = false;
        if (root != null) root.gameObject.SetActive(false);
    }

    private void Resolve()
    {
        if (selection == null)
            selection = FindFirstObjectByType<
                BistroBuilderEditorV2SelectionCoordinator>(FindObjectsInactive.Include);
        if (registry == null)
            registry = FindFirstObjectByType<RestaurantPlaceableRegistry>(
                FindObjectsInactive.Include);
        if (editMode == null)
            editMode = FindFirstObjectByType<RestaurantEditModeService>(
                FindObjectsInactive.Include);
        if (shell == null)
            shell = FindFirstObjectByType<BistroBuilderUiShell>(
                FindObjectsInactive.Include);
        if (catalogInspector == null)
            catalogInspector = GetComponent<RestaurantPlaceableInspectorPanel>();
    }

    private void LateUpdate()
    {
        if (root == null) Build();
        if (root == null) return;
        Resolve();
        bool active = selection != null && editMode != null &&
            editMode.IsEditModeActive &&
            (selection.SelectionCount > 0 || selection.Current.IsValid) &&
            (shell == null || !shell.HasManagementScreenOpen);
        bool wasShowing = IsShowing;
        IsShowing = active;
        // A world selection replaces the catalog preview; do not re-show a stale card later.
        if (active && !wasShowing) catalogInspector?.Hide();
        if (root.gameObject.activeSelf != active)
            root.gameObject.SetActive(active);
        if (!active) return;

        var parent = root.parent as RectTransform;
        float width = parent != null ? parent.rect.width : Screen.width;
        float height = parent != null ? parent.rect.height : Screen.height;
        bool nextCompact = width < 1450f;
        if (nextCompact != compact)
        {
            compact = nextCompact;
            expanded = !compact;
            ApplyExpansion();
        }
        root.sizeDelta = new Vector2(expanded ? (compact ? 338f : 370f) : 42f,
            Mathf.Max(290f, height - 215f));
        root.anchoredPosition = new Vector2(-12f, -89f);
        root.SetAsLastSibling();

        var primary = selection.PrimarySelection.IsValid ?
            selection.PrimarySelection : selection.Current;
        int count = Math.Max(1, selection.SelectionCount);
        if (lastRevision != selection.SelectionSetRevision ||
            !string.Equals(lastPrimary, primary.stableId, StringComparison.Ordinal))
        {
            lastRevision = selection.SelectionSetRevision;
            lastPrimary = primary.stableId ?? "";
            if (count <= 1) showingGroup = false;
            RefreshContent(primary, count);
        }
        if (railCount != null) railCount.text = count.ToString();
    }

    public void ExpandInspector()
    {
        expanded = true;
        ApplyExpansion();
    }

    public void CollapseInspector()
    {
        expanded = false;
        ApplyExpansion();
    }

    private void ApplyExpansion()
    {
        if (panelBody != null) panelBody.SetActive(expanded);
        if (rail != null) rail.SetActive(!expanded);
        if (root != null)
        {
            root.sizeDelta = new Vector2(expanded ? (compact ? 338f : 370f) : 42f,
                root.sizeDelta.y);
        }
    }

    public void SetViewGroup(bool value)
    {
        if (selection == null || selection.SelectionCount < 2) value = false;
        showingGroup = value;
        ShowPage();
    }

    private void ShowPage()
    {
        if (singlePage != null) singlePage.SetActive(!showingGroup);
        if (groupPage != null) groupPage.SetActive(showingGroup);
        if (singleTab != null) singleTab.color = !showingGroup ? Honey : Ivory;
        if (groupTab != null) groupTab.color = showingGroup ? Honey : Ivory;
    }

    private void RefreshContent(BistroBuilderEditorV2Selection primary, int count)
    {
        if (title == null) return;
        title.text = string.IsNullOrWhiteSpace(primary.displayName)
            ? "Artículo seleccionado" : primary.displayName;
        meta.text = count == 1 ? "1 artículo seleccionado" :
            count + " artículos seleccionados";
        groupSummary.text = count + " artículos seleccionados";
        if (selection != null)
        {
            BistroBuilderEditorV2SelectionCapability caps =
                selection.AggregateCapabilities;
            groupCapabilities.text = "Operaciones comunes disponibles: " +
                (caps == BistroBuilderEditorV2SelectionCapability.None
                    ? "solo consulta"
                    : caps.ToString().Replace(",", " ·")) +
                "\nLas acciones se validan antes de ejecutarse.";
        }

        RestaurantPlaceableItemDefinition definition = null;
        if (registry != null &&
            primary.kind == BistroBuilderEditorV2SelectionKind.Furniture &&
            registry.TryGetByInstanceId(primary.stableId, out var placeable) &&
            placeable != null)
            definition = placeable.ItemDefinition;

        if (definition != null)
        {
            description.text = string.IsNullOrWhiteSpace(definition.Description)
                ? "Artículo de la colección del restaurante." : definition.Description;
            var dims = definition.DimensionsCentimeters;
            dimensions.text = dims == Vector3.zero ? "Sin medidas autoradas" :
                string.Format("Ancho {0:0.#} · Fondo {1:0.#} · Alto {2:0.#} cm",
                    dims.x, dims.z, dims.y);
            price.text = definition.PurchasePrice.ToString("N0") +
                " € · precio de catálogo";
            preview.sprite = definition.InspectorPreview;
            preview.enabled = preview.sprite != null;
        }
        else
        {
            description.text = "Seleccionado en el restaurante. " +
                "Los detalles dependen de la familia del elemento.";
            dimensions.text = "Dimensiones no disponibles";
            price.text = "Coste sujeto a la operación";
            preview.enabled = false;
        }

        RebuildGroupRows();
        if (count < 2) showingGroup = false;
        ShowPage();
    }

    private void RebuildGroupRows()
    {
        for (int i = 0; i < groupRows.Count; i++)
            if (groupRows[i] != null) Destroy(groupRows[i]);
        groupRows.Clear();
        if (selection == null || list == null) return;
        selection.CopySelectionSet(scratch, true);
        string primaryId = selection.PrimarySelection.stableId;
        for (int i = 0; i < scratch.Count; i++)
        {
            var item = scratch[i];
            string id = item.stableId;
            bool main = string.Equals(id, primaryId, StringComparison.Ordinal);
            var row = CreateRect("GroupMember", list);
            row.gameObject.AddComponent<Image>().color = main ? Honey : Ivory;
            var height = row.gameObject.AddComponent<LayoutElement>();
            height.minHeight = height.preferredHeight = 53f;
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => SetPrimaryMember(id));
            string name = string.IsNullOrWhiteSpace(item.displayName) ?
                item.kind.ToString() : item.displayName;
            var rowLabel = Label(row, "MemberLabel",
                (main ? "Principal · " : "  ") + name, 14, Ink, null);
            Stretch(rowLabel.rectTransform, 14, 4, 12, 4);
            groupRows.Add(row.gameObject);
        }
    }

    private void SetPrimaryMember(string stableId)
    {
        if (selection == null || selection.SelectionCount < 2) return;
        selection.CopySelectionSet(scratch, true);
        if (selection.ReplaceSelectionSet(scratch, stableId, out string error))
        {
            lastRevision = long.MinValue;
            return;
        }
        Debug.LogWarning("[EditorV2 Inspector] " + error, this);
    }

    private void Build()
    {
        if (root != null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return;
        regular = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Inter-Regular-SDF");
        heading = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Recoleta-SDF");
        if (regular == null) regular = TMP_Settings.defaultFontAsset;
        if (heading == null) heading = regular;

        root = CreateRect("BB_EditorV2_SelectionInspector", canvas.transform);
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-12f, -89f);
        root.sizeDelta = new Vector2(370, 640);
        root.gameObject.AddComponent<Image>().color = Cream;
        var border = root.gameObject.AddComponent<Outline>();
        border.effectColor = Brass;
        border.effectDistance = new Vector2(2f, -2f);

        panelBody = CreateRect("Panel", root).gameObject;
        Stretch(panelBody.GetComponent<RectTransform>(), 8, 8, 8, 8);
        var column = panelBody.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(8, 8, 8, 8);
        column.spacing = 8;
        column.childControlWidth = true;
        column.childControlHeight = false;
        column.childForceExpandHeight = false;
        var bar = CreateRect("Heading", panelBody.transform);
        SetHeight(bar, 39);
        var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        barLayout.childControlWidth = false;
        barLayout.childControlHeight = true;
        barLayout.spacing = 4;
        title = Label(bar, "SelectedTitle", "Inspector", 19, Ink, heading);
        var titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1;
        AddButton(bar, "Collapse", "‹", CollapseInspector, 32);

        meta = Label(panelBody.transform, "SelectionCount", "", 12, Muted, regular);
        SetHeight(meta.rectTransform, 21);

        var tabs = CreateRect("Tabs", panelBody.transform);
        SetHeight(tabs, 38);
        var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 7;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandWidth = true;
        singleTab = AddButton(tabs, "Principal", "Principal",
            () => SetViewGroup(false), 145).GetComponent<Image>();
        groupTab = AddButton(tabs, "Conjunto", "Conjunto",
            () => SetViewGroup(true), 145).GetComponent<Image>();

        var scrollRoot = CreateRect("ScrollArea", panelBody.transform);
        var flex = scrollRoot.gameObject.AddComponent<LayoutElement>();
        flex.flexibleHeight = 1;
        flex.minHeight = 140;
        var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        var viewport = CreateRect("Viewport", scrollRoot);
        Stretch(viewport, 0, 0, 0, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        var pages = CreateRect("Pages", viewport);
        var pagesLayout = pages.gameObject.AddComponent<VerticalLayoutGroup>();
        pagesLayout.childControlWidth = true;
        pagesLayout.childControlHeight = false;
        pagesLayout.childForceExpandHeight = false;
        pages.anchorMin = new Vector2(0, 1);
        pages.anchorMax = new Vector2(1, 1);
        pages.pivot = new Vector2(.5f, 1);
        pages.offsetMin = Vector2.zero;
        pages.offsetMax = Vector2.zero;
        var fitter = pages.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = pages;
        scroll.viewport = viewport;

        singlePage = BuildPage(pages, "PrincipalDetails");
        groupPage = BuildPage(pages, "GroupDetails");
        preview = CreateRect("RealItemImage", singlePage.transform)
            .gameObject.AddComponent<Image>();
        preview.preserveAspect = true;
        preview.raycastTarget = false;
        SetHeight(preview.rectTransform, 145);
        description = Label(singlePage.transform, "Description", "", 13, Ink, regular);
        description.textWrappingMode = TextWrappingModes.Normal;
        SetHeight(description.rectTransform, 75);
        dimensions = Label(singlePage.transform, "Dimensions", "", 13, Ink, regular);
        SetHeight(dimensions.rectTransform, 42);
        price = Label(singlePage.transform, "Price", "", 13, Muted, regular);
        SetHeight(price.rectTransform, 40);
        groupSummary = Label(groupPage.transform, "GroupCount", "", 16, Ink, heading);
        SetHeight(groupSummary.rectTransform, 39);
        groupCapabilities = Label(groupPage.transform, "Capabilities", "",
            12, Muted, regular);
        groupCapabilities.textWrappingMode = TextWrappingModes.Normal;
        SetHeight(groupCapabilities.rectTransform, 79);
        list = CreateRect("Members", groupPage.transform);
        var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 5;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandHeight = false;
        var listFit = list.gameObject.AddComponent<ContentSizeFitter>();
        listFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var footer = Label(panelBody.transform, "Note",
            "Editar solo operaciones compatibles", 11, Muted, regular);
        SetHeight(footer.rectTransform, 21);

        rail = CreateRect("InspectorRail", root).gameObject;
        Stretch(rail.GetComponent<RectTransform>(), 0, 0, 0, 0);
        var railBg = rail.AddComponent<Image>();
        railBg.color = Honey;
        var railBtn = rail.AddComponent<Button>();
        railBtn.targetGraphic = railBg;
        railBtn.onClick.AddListener(ExpandInspector);
        railCount = Label(rail.transform, "RailLabel", "0", 17, Ink, heading);
        Stretch(railCount.rectTransform, 3, 3, 3, 3);
        railCount.alignment = TextAlignmentOptions.Center;
        ApplyExpansion();
        root.gameObject.SetActive(false);
    }

    private static GameObject BuildPage(Transform parent, string name)
    {
        var page = CreateRect(name, parent);
        var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        page.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        return page.gameObject;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private TMP_Text Label(Transform parent, string name, string value,
        float size, Color color, TMP_FontAsset font)
    {
        var rect = CreateRect(name, parent);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = font != null ? font : regular;
        tmp.fontSize = size;
        tmp.text = value;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button AddButton(Transform parent, string name, string caption,
        Action action, float width)
    {
        var rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Ivory;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => action?.Invoke());
        var elem = rect.gameObject.AddComponent<LayoutElement>();
        elem.preferredWidth = elem.minWidth = width;
        var label = Label(rect, "Caption", caption, 13, Ink, regular);
        Stretch(label.rectTransform, 2, 2, 2, 2);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static void SetHeight(RectTransform rect, float height)
    {
        var layout = rect.GetComponent<LayoutElement>();
        if (layout == null) layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
    }

    private static void Stretch(RectTransform rect, float l, float b, float r, float t)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(l, b);
        rect.offsetMax = new Vector2(-r, -t);
    }
}
