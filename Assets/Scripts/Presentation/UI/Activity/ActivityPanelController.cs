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
    private const float RowHeight = 82f;
    private const float RowSpacing = 6f;

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
    private TMP_Text sectionTitleText;
    private TMP_Text visibleCountText;
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
    private float lastViewportHeight = -1f;

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

        if (scrollRect != null && scrollRect.viewport != null)
        {
            float height = scrollRect.viewport.rect.height;
            if (Mathf.Abs(height - lastViewportHeight) > 0.5f)
            {
                lastViewportHeight = height;
                dirty = true;
            }
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

        ActivityPanelResponsiveLayout responsiveLayout =
            activityPanel.GetComponent<ActivityPanelResponsiveLayout>();
        if (responsiveLayout == null)
            responsiveLayout =
                activityPanel.gameObject.AddComponent<ActivityPanelResponsiveLayout>();
        responsiveLayout.Apply(false);

        TMP_Text legacyText =
            activityPanel.Find("ActivityText")?.GetComponent<TMP_Text>();
        if (legacyText != null)
            legacyText.enabled = false;

        TMP_Text heading =
            activityPanel.Find("ActivityHeading")?.GetComponent<TMP_Text>();
        ActivityPanelVisualStyle.ApplyPanelChrome(activityPanel, heading);

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
        RectTransform rows =
            referenceRoot.Find("Viewport/Rows") as RectTransform ??
            referenceRoot.Find("Rows") as RectTransform;
        if (referenceRect == null || rows == null)
            return;

        bool unchanged =
            usingReferencePresentation &&
            ReferenceEquals(runtimeRoot, referenceRect) &&
            ReferenceEquals(contentRoot, rows);

        if (runtimeRoot != null &&
            !ReferenceEquals(runtimeRoot, referenceRect) &&
            runtimeRoot.name == RuntimeRootName)
            runtimeRoot.gameObject.SetActive(false);

        runtimeRoot = referenceRect;
        contentRoot = rows;
        emptyText =
            referenceRoot.Find("Viewport/Empty")?.GetComponent<TMP_Text>() ??
            referenceRoot.Find("Empty")?.GetComponent<TMP_Text>();
        footerText =
            referenceRoot.Find("OverflowText")?.GetComponent<TMP_Text>();
        sectionTitleText =
            referenceRoot.Find("SectionTitle")?.GetComponent<TMP_Text>();
        visibleCountText =
            referenceRoot.Find("VisibleCount")?.GetComponent<TMP_Text>();
        scrollRect = referenceRoot.GetComponent<ScrollRect>();
        usingReferencePresentation = true;

        CacheReferenceFilters(referenceRoot);
        if (!unchanged)
            dirty = true;
    }

    private void CacheReferenceFilters(Transform referenceRoot)
    {
        Transform filters = referenceRoot.Find("Filters");
        if (filters == null)
            return;

        CacheFilter(filters, ActivityFilter.Today, "Today");
        CacheFilter(filters, ActivityFilter.Incidents, "Incidents");
        CacheFilter(filters, ActivityFilter.Opportunities, "Opportunities");
        CacheFilter(filters, ActivityFilter.Reservations, "Reservations");
        RefreshFilterButtons();
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
        sectionTitleText =
            runtimeRoot.Find("SectionTitle")?.GetComponent<TMP_Text>();
        visibleCountText =
            runtimeRoot.Find("VisibleCount")?.GetComponent<TMP_Text>();
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

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => SetFilter(filter));
        filterButtons[filter] = button;

        TMP_Text text = CreateText(
            root,
            "Label",
            label,
            10.6f,
            ActivityPanelVisualStyle.Ink,
            TextAlignmentOptions.Center);
        ApplyTypographyRole(text, BistroBuilderUiStyleRole.Label);
        Stretch(text.rectTransform);
        ActivityPanelVisualStyle.ApplyFilterButton(
            button,
            filter == activeFilter);
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

        if (visibleCountText != null)
            visibleCountText.text = displayEntries.Count + " visibles";

        if (footerText != null)
        {
            int visibleCapacity = GetVisibleRowCapacity();
            int hiddenBelowFold = Math.Max(0, displayEntries.Count - visibleCapacity);
            footerText.text = hiddenBelowFold > 0
                ? hiddenBelowFold + " más · scroll"
                : string.Empty;
        }

        RefreshFilterButtons();
        Canvas.ForceUpdateCanvases();
    }

    private int GetVisibleRowCapacity()
    {
        if (scrollRect == null || scrollRect.viewport == null)
            return 8;

        float viewportHeight = Mathf.Max(0f, scrollRect.viewport.rect.height);
        float rowPitch = RowHeight + RowSpacing;
        if (rowPitch <= 0f)
            return 8;

        return Mathf.Max(1, Mathf.FloorToInt(
            (viewportHeight + RowSpacing) / rowPitch));
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
            background = row.gameObject.AddComponent<Image>();
        ActivityPanelVisualStyle.ApplyRowBackground(background);

        Outline outline = row.GetComponent<Outline>();
        if (outline == null)
            outline = row.gameObject.AddComponent<Outline>();
        outline.effectColor = ActivityPanelVisualStyle.Line;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        Button button = row.GetComponent<Button>();
        if (button == null)
            button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        ActivityRowHoverFeedback hoverFeedback =
            row.GetComponent<ActivityRowHoverFeedback>();
        if (hoverFeedback == null)
            hoverFeedback =
                row.gameObject.AddComponent<ActivityRowHoverFeedback>();
        hoverFeedback.Configure(background);

        Image iconPlate = row.Find("IconPlate")?.GetComponent<Image>();
        if (iconPlate == null)
        {
            iconPlate = NewRect("IconPlate", row).gameObject.AddComponent<Image>();
            iconPlate.transform.SetAsFirstSibling();
        }
        ActivityPanelVisualStyle.ApplyRounded(iconPlate, ActivityPanelVisualStyle.PanelSoft);
        iconPlate.raycastTarget = false;
        Place(iconPlate.rectTransform, 10f, -11f, 58f, 58f);

        TMP_Text marker = row.Find("Marker")?.GetComponent<TMP_Text>();
        if (marker == null)
            marker = CreateText(row, "Marker", "●", 10f,
                ActivityPanelVisualStyle.Muted, TextAlignmentOptions.Center);
        ApplyTypographyRole(marker, BistroBuilderUiStyleRole.Caption);
        Place(marker.rectTransform, 1f, -31f, 10f, 18f);

        Image icon = row.Find("Icon")?.GetComponent<Image>();
        if (icon == null)
            icon = NewRect("Icon", row).gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Place(icon.rectTransform, 16f, -17f, 46f, 46f);

        TMP_Text title = row.Find("Title")?.GetComponent<TMP_Text>();
        if (title == null)
            title = CreateText(row, "Title", "Actividad", 13.5f,
                ActivityPanelVisualStyle.Ink, TextAlignmentOptions.MidlineLeft);
        title.color = ActivityPanelVisualStyle.Ink;
        title.fontSize = 13.5f;
        ApplyTypographyRole(title, BistroBuilderUiStyleRole.Heading);
        PlaceStretchTop(title.rectTransform, 78f, 108f, 8f, 23f);

        TMP_Text subtitle = row.Find("Subtitle")?.GetComponent<TMP_Text>();
        if (subtitle == null)
            subtitle = CreateText(row, "Subtitle", string.Empty, 10.5f,
                ActivityPanelVisualStyle.Brown, TextAlignmentOptions.MidlineLeft);
        subtitle.color = ActivityPanelVisualStyle.Brown;
        subtitle.fontSize = 10.5f;
        ApplyTypographyRole(subtitle, BistroBuilderUiStyleRole.Body);
        PlaceStretchTop(subtitle.rectTransform, 78f, 108f, 31f, 20f);

        EnsureTargetChip(row);
        EnsureSeverityBadge(row);

        TMP_Text time = row.Find("Time")?.GetComponent<TMP_Text>();
        if (time == null)
            time = CreateText(row, "Time", "--:--", 9.5f,
                ActivityPanelVisualStyle.Muted, TextAlignmentOptions.MidlineRight);
        time.color = ActivityPanelVisualStyle.Muted;
        time.fontSize = 9.5f;
        ApplyTypographyRole(time, BistroBuilderUiStyleRole.Caption);
        PlaceRight(time.rectTransform, 42f, 7f, 58f, 19f);

        TMP_Text chevron = row.Find("Chevron")?.GetComponent<TMP_Text>();
        if (chevron == null)
            chevron = CreateText(row, "Chevron", "›", 22f,
                ActivityPanelVisualStyle.GoldDark, TextAlignmentOptions.Center);
        chevron.color = ActivityPanelVisualStyle.GoldDark;
        ApplyTypographyRole(chevron, BistroBuilderUiStyleRole.Heading);
        PlaceRight(chevron.rectTransform, 5f, 25f, 18f, 30f);

        TMP_Text state = row.Find("State")?.GetComponent<TMP_Text>();
        if (state == null)
            state = CreateText(row, "State", string.Empty, 9f,
                Color.white, TextAlignmentOptions.Center);
        ApplyTypographyRole(state, BistroBuilderUiStyleRole.Caption);
        Place(state.rectTransform, 46f, -59f, 28f, 17f);

        Image countBadge = row.Find("CountBadge")?.GetComponent<Image>();
        if (countBadge == null)
        {
            countBadge = NewRect("CountBadge", row).gameObject.AddComponent<Image>();
            countBadge.transform.SetSiblingIndex(1);
        }
        ActivityPanelVisualStyle.ApplyRounded(
            countBadge,
            ActivityPanelVisualStyle.Ink);
        countBadge.raycastTarget = false;
        Place(countBadge.rectTransform, 44f, -58f, 32f, 18f);
    }

    private void EnsureTargetChip(Transform row)
    {
        Image chip = row.Find("TargetChip")?.GetComponent<Image>();
        if (chip == null)
        {
            chip = NewRect("TargetChip", row).gameObject.AddComponent<Image>();
            chip.transform.SetSiblingIndex(1);
        }
        ActivityPanelVisualStyle.ApplyRounded(
            chip,
            new Color32(251, 240, 220, 255));
        chip.raycastTarget = false;
        Place(chip.rectTransform, 78f, -55f, 126f, 19f);

        TMP_Text target = row.Find("Target")?.GetComponent<TMP_Text>();
        if (target == null)
            target = CreateText(row, "Target", string.Empty, 8.7f,
                ActivityPanelVisualStyle.Muted,
                TextAlignmentOptions.Center);
        target.color = ActivityPanelVisualStyle.Muted;
        ApplyTypographyRole(target, BistroBuilderUiStyleRole.Caption);
        Place(target.rectTransform, 80f, -55f, 122f, 19f);
    }

    private void EnsureSeverityBadge(Transform row)
    {
        Image badge = row.Find("SeverityBadge")?.GetComponent<Image>();
        if (badge == null)
        {
            badge = NewRect("SeverityBadge", row).gameObject.AddComponent<Image>();
            badge.transform.SetSiblingIndex(1);
        }
        ActivityPanelVisualStyle.ApplyRounded(
            badge,
            new Color32(234, 223, 206, 255));
        badge.raycastTarget = false;
        PlaceRight(badge.rectTransform, 28f, 31f, 76f, 20f);

        TMP_Text label = row.Find("SeverityLabel")?.GetComponent<TMP_Text>();
        if (label == null)
            label = CreateText(row, "SeverityLabel", "INFO", 8.7f,
                ActivityPanelVisualStyle.Brown,
                TextAlignmentOptions.Center);
        ApplyTypographyRole(label, BistroBuilderUiStyleRole.Caption);
        PlaceRight(label.rectTransform, 30f, 31f, 72f, 20f);
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
        TMP_Text target = row.Find("Target")?.GetComponent<TMP_Text>();
        TMP_Text time = row.Find("Time")?.GetComponent<TMP_Text>();
        TMP_Text state = row.Find("State")?.GetComponent<TMP_Text>();
        TMP_Text severityLabel = row.Find("SeverityLabel")?.GetComponent<TMP_Text>();
        Image severityBadge = row.Find("SeverityBadge")?.GetComponent<Image>();
        Image countBadge = row.Find("CountBadge")?.GetComponent<Image>();
        Button button = row.GetComponent<Button>();

        Color semanticColor = ResolveSemanticColor(entry.Definition.Severity);
        if (marker != null)
        {
            marker.color = semanticColor;
            bool sticky =
                entry.Definition.Lifetime == ActivityLifetime.StickyUntilResolved &&
                !entry.IsResolved;
            marker.text = sticky ? "●" : (entry.IsResolved ? "○" : "•");
        }

        if (title != null)
            title.text = text.Title;
        if (subtitle != null)
            subtitle.text = text.Body;
        if (target != null)
            target.text = FormatTarget(entry);
        if (time != null)
            time.text = FormatGameTime(entry.Primary.minuteOfDay);

        bool aggregated = entry.AggregatedCount > 1;
        if (state != null)
        {
            state.text = aggregated ? "×" + entry.AggregatedCount : string.Empty;
            state.gameObject.SetActive(aggregated);
        }
        if (countBadge != null)
            countBadge.gameObject.SetActive(aggregated);

        ActivityPanelVisualStyle.GetSeverityStyle(
            entry.Definition.Severity,
            out Color badgeBackground,
            out Color badgeForeground,
            out string badgeText);
        if (severityBadge != null)
            ActivityPanelVisualStyle.ApplyRounded(severityBadge, badgeBackground);
        if (severityLabel != null)
        {
            severityLabel.text = badgeText;
            severityLabel.color = badgeForeground;
        }

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

    private static string FormatTarget(ActivityDisplayEntry entry)
    {
        if (entry == null || entry.Targets == null || entry.Targets.Count == 0)
            return "Actividad";

        ActivityTargetRef target = entry.Targets[0];
        if (target == null || target.IsEmpty)
            return "Actividad";

        string prefix;
        switch (target.targetType)
        {
            case ActivityTargetType.Table: prefix = "Mesa"; break;
            case ActivityTargetType.Order: prefix = "Comanda"; break;
            case ActivityTargetType.Kitchen: prefix = "Cocina"; break;
            case ActivityTargetType.Zone: prefix = "Zona"; break;
            case ActivityTargetType.Bar: prefix = "Barra"; break;
            case ActivityTargetType.Reservation: prefix = "Reserva"; break;
            case ActivityTargetType.ReservationGroup: prefix = "Reserva"; break;
            case ActivityTargetType.Ingredient: prefix = "Ingrediente"; break;
            case ActivityTargetType.Inventory: prefix = "Almacén"; break;
            case ActivityTargetType.Supplier: prefix = "Proveedor"; break;
            case ActivityTargetType.Employee: prefix = "Personal"; break;
            case ActivityTargetType.Reputation: prefix = "Reputación"; break;
            case ActivityTargetType.Marketing: prefix = "Marketing"; break;
            case ActivityTargetType.Entrance: prefix = "Entrada"; break;
            case ActivityTargetType.Group: prefix = "Grupo"; break;
            case ActivityTargetType.Dish: prefix = "Plato"; break;
            default: prefix = target.targetType.ToString(); break;
        }

        if (string.IsNullOrWhiteSpace(target.targetId) ||
            string.Equals(target.targetId, "0", StringComparison.Ordinal))
            return prefix;

        return prefix + " " + target.targetId;
    }

    private void RefreshFilterButtons()
    {
        foreach (KeyValuePair<ActivityFilter, Button> pair in filterButtons)
        {
            Button button = pair.Value;
            if (button == null)
                continue;

            ActivityPanelVisualStyle.ApplyFilterButton(
                button,
                pair.Key == activeFilter);
        }

        if (sectionTitleText != null)
        {
            switch (activeFilter)
            {
                case ActivityFilter.Incidents:
                    sectionTitleText.text = "Incidencias";
                    break;
                case ActivityFilter.Opportunities:
                    sectionTitleText.text = "Oportunidades";
                    break;
                case ActivityFilter.Reservations:
                    sectionTitleText.text = "Reservas";
                    break;
                default:
                    sectionTitleText.text = "Sucesos de hoy";
                    break;
            }
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
        text.textWrappingMode = TextWrappingModes.NoWrap;
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

    private static void PlaceStretchTop(
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

    private static void PlaceRight(
        RectTransform rect,
        float right,
        float top,
        float width,
        float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = new Vector2(width, height);
    }
}
