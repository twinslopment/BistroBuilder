using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Proyección visual del feed semántico. No muta gameplay.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Activity/Activity Panel Controller")]
public sealed class ActivityPanelController : MonoBehaviour
{
    private const string RuntimeRootName = "BB_ActivityRuntime";
    private const float RowHeight = 57f;
    private const int VisibleRows = 8;

    public static ActivityPanelController ActiveInstance { get; private set; }
    public static bool HasActiveController =>
        ActiveInstance != null &&
        ActiveInstance.isActiveAndEnabled;

    [SerializeField] private ActivityFeedService feed;
    [SerializeField] private ActivityTargetRouter targetRouter;

    private BistroBuilderUiShell shell;
    private RectTransform activityPanel;
    private RectTransform runtimeRoot;
    private RectTransform contentRoot;
    private TMP_Text emptyText;
    private TMP_Text footerText;
    private ScrollRect scrollRect;
    private bool usingReferencePresentation;

    private readonly List<ActivityDisplayEntry> displayEntries =
        new List<ActivityDisplayEntry>(128);
    private readonly ActivityFeedAggregator aggregator =
        new ActivityFeedAggregator();
    private readonly ActivityTemplateFormatter formatter =
        new ActivityTemplateFormatter();
    private readonly Dictionary<ActivityFilter, Button> filterButtons =
        new Dictionary<ActivityFilter, Button>();

    private ActivityFilter activeFilter = ActivityFilter.Today;
    private bool subscribed;
    private bool dirty = true;
    private float nextResolveAt;

    public bool HasEntries => feed != null && feed.Count > 0;
    public ActivityFilter ActiveFilter => activeFilter;

    private void Awake()
    {
        ActiveInstance = this;
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ActiveInstance = this;
        ResolveDependencies();
        Subscribe();
        dirty = true;
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (ReferenceEquals(ActiveInstance, this))
            ActiveInstance = null;
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(ActiveInstance, this))
            ActiveInstance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextResolveAt)
        {
            ResolveDependencies();
            Subscribe();
            EnsurePresentation();
            nextResolveAt = Time.unscaledTime + 0.5f;
        }

        if (dirty)
            Refresh();
    }

    public void SetLocalizationResolver(IActivityLocalizationResolver resolver)
    {
        formatter.SetLocalizationResolver(resolver);
        dirty = true;
    }

    public void SetFilter(ActivityFilter filter)
    {
        if (activeFilter == filter)
            return;

        activeFilter = filter;
        dirty = true;
        RefreshFilterButtons();
    }

    private void ResolveDependencies()
    {
        if (feed == null)
            feed = FindFirstObjectByType<ActivityFeedService>(FindObjectsInactive.Include);
        if (targetRouter == null)
            targetRouter = FindFirstObjectByType<ActivityTargetRouter>(FindObjectsInactive.Include);
        if (shell == null)
            shell = FindFirstObjectByType<BistroBuilderUiShell>(FindObjectsInactive.Include);
    }

    private void Subscribe()
    {
        if (subscribed || feed == null)
            return;

        feed.Changed += HandleFeedChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (subscribed && feed != null)
            feed.Changed -= HandleFeedChanged;
        subscribed = false;
    }

    private void HandleFeedChanged()
    {
        dirty = true;
    }

    private void EnsurePresentation()
    {
        if (shell == null)
            return;

        Transform shellRoot = shell.transform.Find(BistroBuilderUiShell.RootName);
        if (shellRoot == null)
        {
            shell.EnsureShell();
            shellRoot = shell.transform.Find(BistroBuilderUiShell.RootName);
        }

        if (shellRoot == null)
            return;

        RectTransform nextPanel =
            shellRoot.Find(BistroBuilderUiShell.ActivityPanelName) as RectTransform;
        if (nextPanel == null)
            return;

        if (activityPanel != nextPanel)
        {
            activityPanel = nextPanel;
            runtimeRoot = null;
            contentRoot = null;
            usingReferencePresentation = false;
            filterButtons.Clear();
            dirty = true;
        }

        TMP_Text legacyText =
            activityPanel.Find("ActivityText")?.GetComponent<TMP_Text>();
        if (legacyText != null)
            legacyText.enabled = false;

        TMP_Text heading =
            activityPanel.Find("ActivityHeading")?.GetComponent<TMP_Text>();
        if (heading != null)
        {
            heading.text = "Actividad";
            ApplyTypographyRole(heading, BistroBuilderUiStyleRole.Heading);
        }

        Transform referenceRoot = activityPanel.Find("BB_ReferenceActivity");
        if (referenceRoot != null)
        {
            BindReferencePresentation(referenceRoot);
            return;
        }

        if (runtimeRoot != null)
            return;

        Transform existing = activityPanel.Find(RuntimeRootName);
        if (existing != null)
        {
            runtimeRoot = existing as RectTransform;
            usingReferencePresentation = false;
            CachePresentationReferences();
            return;
        }

        usingReferencePresentation = false;
        BuildPresentation();
    }

    private void BindReferencePresentation(Transform referenceRoot)
    {
        RectTransform referenceRect = referenceRoot as RectTransform;
        RectTransform rows = referenceRoot.Find("Rows") as RectTransform;
        if (referenceRect == null || rows == null)
            return;

        if (usingReferencePresentation &&
            ReferenceEquals(runtimeRoot, referenceRect) &&
            ReferenceEquals(contentRoot, rows))
            return;

        runtimeRoot = referenceRect;
        contentRoot = rows;
        emptyText = referenceRoot.Find("Empty")?.GetComponent<TMP_Text>();
        footerText = null;
        scrollRect = null;
        usingReferencePresentation = true;
        filterButtons.Clear();
        dirty = true;
    }

    private void BuildPresentation()
    {
        runtimeRoot = NewRect(RuntimeRootName, activityPanel);
        Stretch(runtimeRoot);
        runtimeRoot.offsetMin = new Vector2(12f, 12f);
        runtimeRoot.offsetMax = new Vector2(-12f, -52f);

        RectTransform filters = NewRect("Filters", runtimeRoot);
        filters.anchorMin = new Vector2(0f, 1f);
        filters.anchorMax = new Vector2(1f, 1f);
        filters.pivot = new Vector2(0.5f, 1f);
        filters.anchoredPosition = Vector2.zero;
        filters.sizeDelta = new Vector2(0f, 36f);

        CreateFilter(filters, ActivityFilter.Today, "Hoy", 0f, 0.22f);
        CreateFilter(filters, ActivityFilter.Incidents, "Incidencias", 0.23f, 0.52f);
        CreateFilter(filters, ActivityFilter.Opportunities, "Oportunidades", 0.53f, 0.82f);
        CreateFilter(filters, ActivityFilter.Reservations, "Reservas", 0.83f, 1f);

        RectTransform viewport = NewRect("Viewport", runtimeRoot);
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.offsetMin = new Vector2(0f, 34f);
        viewport.offsetMax = new Vector2(0f, -42f);

        Image viewportGraphic = viewport.gameObject.AddComponent<Image>();
        viewportGraphic.color = new Color(0f, 0f, 0f, 0.001f);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        contentRoot = NewRect("Content", viewport);
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout =
            contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter =
            contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect = runtimeRoot.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = contentRoot;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = BistroBuilderUiTokens.ScrollWheelStepPixels;
        scrollRect.decelerationRate = BistroBuilderUiTokens.ScrollDecelerationRate;

        emptyText = CreateText(
            runtimeRoot,
            "Empty",
            "Sin actividad reciente",
            13f,
            BistroBuilderUiTokens.TextMuted,
            TextAlignmentOptions.Center);
        ApplyTypographyRole(emptyText, BistroBuilderUiStyleRole.Caption);
        emptyText.rectTransform.anchorMin = new Vector2(0f, 0f);
        emptyText.rectTransform.anchorMax = new Vector2(1f, 1f);
        emptyText.rectTransform.offsetMin = new Vector2(0f, 55f);
        emptyText.rectTransform.offsetMax = new Vector2(0f, -55f);

        footerText = CreateText(
            runtimeRoot,
            "Footer",
            string.Empty,
            10.5f,
            BistroBuilderUiTokens.TextMuted,
            TextAlignmentOptions.MidlineRight);
        ApplyTypographyRole(footerText, BistroBuilderUiStyleRole.Caption);
        footerText.rectTransform.anchorMin = new Vector2(0f, 0f);
        footerText.rectTransform.anchorMax = new Vector2(1f, 0f);
        footerText.rectTransform.pivot = new Vector2(0.5f, 0f);
        footerText.rectTransform.anchoredPosition = Vector2.zero;
        footerText.rectTransform.sizeDelta = new Vector2(0f, 26f);

        RefreshFilterButtons();
        dirty = true;
    }

    private void CachePresentationReferences()
    {
        contentRoot = runtimeRoot.Find("Viewport/Content") as RectTransform;
        emptyText = runtimeRoot.Find("Empty")?.GetComponent<TMP_Text>();
        footerText = runtimeRoot.Find("Footer")?.GetComponent<TMP_Text>();
        scrollRect = runtimeRoot.GetComponent<ScrollRect>();

        Transform filters = runtimeRoot.Find("Filters");
        if (filters != null)
        {
            CacheFilter(filters, ActivityFilter.Today, "Today");
            CacheFilter(filters, ActivityFilter.Incidents, "Incidents");
            CacheFilter(filters, ActivityFilter.Opportunities, "Opportunities");
            CacheFilter(filters, ActivityFilter.Reservations, "Reservations");
        }

        RefreshFilterButtons();
        dirty = true;
    }

    private void CacheFilter(Transform root, ActivityFilter filter, string name)
    {
        Button button = root.Find(name)?.GetComponent<Button>();
        if (button == null)
            return;
        filterButtons[filter] = button;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SetFilter(filter));
    }

    private void CreateFilter(
        RectTransform parent,
        ActivityFilter filter,
        string label,
        float minX,
        float maxX)
    {
        string name = FilterObjectName(filter);
        RectTransform root = NewRect(name, parent);
        root.anchorMin = new Vector2(minX, 0f);
        root.anchorMax = new Vector2(maxX, 1f);
        root.offsetMin = new Vector2(0f, 0f);
        root.offsetMax = new Vector2(-4f, 0f);

        Image image = root.gameObject.AddComponent<Image>();
        image.color = BistroBuilderUiTokens.Surface1;

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = BistroBuilderUiTokens.ButtonColors(
            BistroBuilderUiTokens.Surface1,
            BistroBuilderUiTokens.Surface2,
            BistroBuilderUiTokens.PrimaryPressed);
        button.onClick.AddListener(() => SetFilter(filter));
        filterButtons[filter] = button;

        TMP_Text text = CreateText(
            root,
            "Label",
            label,
            11f,
            BistroBuilderUiTokens.TextSecondary,
            TextAlignmentOptions.Center);
        ApplyTypographyRole(text, BistroBuilderUiStyleRole.Label);
        Stretch(text.rectTransform);
    }

    private void Refresh()
    {
        dirty = false;
        EnsurePresentation();

        if (feed == null || contentRoot == null)
            return;

        aggregator.Build(
            feed.Events,
            activeFilter,
            feed.CurrentDayIndex,
            displayEntries);

        EnsureRowCount(displayEntries.Count);

        for (int i = 0; i < displayEntries.Count; i++)
            BindRow(contentRoot.GetChild(i), displayEntries[i]);

        if (emptyText != null)
            emptyText.gameObject.SetActive(displayEntries.Count == 0);

        if (footerText != null)
        {
            int hiddenBelowFold = Math.Max(0, displayEntries.Count - VisibleRows);
            footerText.text = hiddenBelowFold > 0
                ? hiddenBelowFold + " más · desplaza para ver"
                : string.Empty;
        }

        RefreshFilterButtons();
        Canvas.ForceUpdateCanvases();
    }

    private void EnsureRowCount(int count)
    {
        while (contentRoot.childCount < count)
            CreateRow(contentRoot);

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            Transform row = contentRoot.GetChild(i);
            bool visible = i < count;
            if (visible)
                EnsureRowStructure(row);
            row.gameObject.SetActive(visible);
        }
    }

    private void EnsureRowStructure(Transform row)
    {
        if (row == null)
            return;

        LayoutElement element = row.GetComponent<LayoutElement>();
        if (element == null)
            element = row.gameObject.AddComponent<LayoutElement>();
        element.minHeight = RowHeight;
        element.preferredHeight = RowHeight;

        Image background = row.GetComponent<Image>();
        if (background == null)
        {
            background = row.gameObject.AddComponent<Image>();
            background.color = new Color32(42, 40, 34, 242);
        }

        Button button = row.GetComponent<Button>();
        if (button == null)
            button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.colors = BistroBuilderUiTokens.ButtonColors(
            background.color,
            new Color32(55, 52, 44, 248),
            new Color32(61, 58, 49, 255));

        TMP_Text marker = row.Find("Marker")?.GetComponent<TMP_Text>();
        if (marker == null)
            marker = CreateText(row, "Marker", "●", 17f,
                BistroBuilderUiTokens.TextMuted, TextAlignmentOptions.Center);
        ApplyTypographyRole(marker, BistroBuilderUiStyleRole.Caption);
        Place(marker.rectTransform, 6f, -8f, 32f, 40f);

        Image icon = row.Find("Icon")?.GetComponent<Image>();
        if (icon == null)
        {
            icon = NewRect("Icon", row).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.enabled = false;
        }
        Place(icon.rectTransform, 40f, -10f, 28f, 28f);

        TMP_Text title = row.Find("Title")?.GetComponent<TMP_Text>();
        if (title == null)
            title = CreateText(row, "Title", "Actividad", 13.2f,
                BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.MidlineLeft);
        ApplyTypographyRole(title, BistroBuilderUiStyleRole.Label);
        Place(title.rectTransform, 74f, -6f, 190f, 24f);

        TMP_Text subtitle = row.Find("Subtitle")?.GetComponent<TMP_Text>();
        if (subtitle == null)
            subtitle = CreateText(row, "Subtitle", string.Empty, 11f,
                BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.MidlineLeft);
        ApplyTypographyRole(subtitle, BistroBuilderUiStyleRole.Caption);
        Place(subtitle.rectTransform, 74f, -29f, 218f, 21f);

        TMP_Text time = row.Find("Time")?.GetComponent<TMP_Text>();
        if (time == null)
            time = CreateText(row, "Time", "--:--", 10.5f,
                BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.MidlineRight);
        ApplyTypographyRole(time, BistroBuilderUiStyleRole.Caption);
        Place(time.rectTransform, 262f, -6f, 48f, 24f);

        TMP_Text state = row.Find("State")?.GetComponent<TMP_Text>();
        if (state == null)
            state = CreateText(row, "State", string.Empty, 10f,
                BistroBuilderUiTokens.TextMuted, TextAlignmentOptions.MidlineRight);
        ApplyTypographyRole(state, BistroBuilderUiStyleRole.Caption);
        Place(state.rectTransform, 286f, -31f, 24f, 18f);
    }

    private void CreateRow(RectTransform parent)
    {
        RectTransform row = NewRect("ActivityRow", parent);
        EnsureRowStructure(row);
    }

    private void BindRow(Transform row, ActivityDisplayEntry entry)
    {
        if (row == null || entry == null || entry.Definition == null || entry.Primary == null)
            return;

        ActivityFormattedText text = formatter.Format(entry);
        TMP_Text marker = row.Find("Marker")?.GetComponent<TMP_Text>();
        Image icon = row.Find("Icon")?.GetComponent<Image>();
        TMP_Text title = row.Find("Title")?.GetComponent<TMP_Text>();
        TMP_Text subtitle = row.Find("Subtitle")?.GetComponent<TMP_Text>();
        TMP_Text time = row.Find("Time")?.GetComponent<TMP_Text>();
        TMP_Text state = row.Find("State")?.GetComponent<TMP_Text>();
        Button button = row.GetComponent<Button>();

        Color semanticColor = ResolveSemanticColor(entry.Definition.Severity);
        if (marker != null)
        {
            marker.color = semanticColor;
            marker.text = entry.IsResolved ? "○" : "●";
        }

        if (title != null)
            title.text = text.Title;
        if (subtitle != null)
            subtitle.text = text.Body;
        if (time != null)
            time.text = FormatGameTime(entry.Primary.minuteOfDay);
        if (state != null)
            state.text = entry.AggregatedCount > 1 ? "×" + entry.AggregatedCount : string.Empty;

        if (icon != null)
        {
            Sprite sprite = ActivityIconResolver.ResolveOrNull(
                entry.Definition.IconFamilyKey);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = entry.Targets.Count > 0;
            ActivityDisplayEntry captured = entry;
            button.onClick.AddListener(() =>
            {
                if (targetRouter != null)
                    targetRouter.TryRoute(captured);
            });
        }
    }

    private void RefreshFilterButtons()
    {
        foreach (KeyValuePair<ActivityFilter, Button> pair in filterButtons)
        {
            Button button = pair.Value;
            if (button == null)
                continue;

            Image image = button.targetGraphic as Image;
            if (image != null)
                image.color = pair.Key == activeFilter
                    ? BistroBuilderUiTokens.Primary
                    : BistroBuilderUiTokens.Surface1;

            TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null)
                label.color = pair.Key == activeFilter
                    ? BistroBuilderUiTokens.ContentLight
                    : BistroBuilderUiTokens.TextSecondary;
        }
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        TMP_Text text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        ApplyTypographyRole(text, BistroBuilderUiStyleRole.Body);
        return text;
    }

    private static void ApplyTypographyRole(
        TMP_Text text,
        BistroBuilderUiStyleRole role)
    {
        if (text == null)
            return;

        BistroBuilderTypography.Apply(text, role, true);
        BistroBuilderUiStyleTag tag = text.GetComponent<BistroBuilderUiStyleTag>();
        if (tag == null)
            tag = text.gameObject.AddComponent<BistroBuilderUiStyleTag>();
        tag.Configure(role, keepFontSize: true);
    }

    private static string FilterObjectName(ActivityFilter filter)
    {
        switch (filter)
        {
            case ActivityFilter.Incidents: return "Incidents";
            case ActivityFilter.Opportunities: return "Opportunities";
            case ActivityFilter.Reservations: return "Reservations";
            default: return "Today";
        }
    }

    private static string FormatGameTime(double minuteOfDay)
    {
        int total = Mathf.Clamp((int)Math.Floor(minuteOfDay), 0, 1439);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    private static Color ResolveSemanticColor(ActivitySeverity severity)
    {
        switch (severity)
        {
            case ActivitySeverity.Critical:
                return BistroBuilderUiTokens.Critical;
            case ActivitySeverity.Attention:
                return BistroBuilderUiTokens.Attention;
            case ActivitySeverity.Positive:
                return BistroBuilderUiTokens.Success;
            case ActivitySeverity.Opportunity:
                return BistroBuilderUiTokens.WarmAccent;
            case ActivitySeverity.Reservation:
                return BistroBuilderUiTokens.Terracotta;
            default:
                return BistroBuilderUiTokens.Info;
        }
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
