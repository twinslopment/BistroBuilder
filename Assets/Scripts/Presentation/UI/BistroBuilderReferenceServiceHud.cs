using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de servicio que replica la referencia aprobada:
/// actividad estructurada a la izquierda y ficha viva de la mesa seleccionada a la derecha.
/// Solo Presentation; no decide ni muta gameplay.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Reference Service HUD")]
public sealed class BistroBuilderReferenceServiceHud : MonoBehaviour
{
    public const string RuntimeRevision = "21A-REFERENCE-SERVICE-HUD-V1.0";
    private const float ActivityWidth = 340f;
    private const float ContextWidth = 390f;
    private const int MaxActivityRows = 8;
    private const int MaxOrderRows = 5;

    private BistroBuilderUiShell shell;
    private BistroBuilderTableSelectionController selection;
    private TableAssignmentSystem tableAssignments;
    private OrderSystem orderSystem;
    private BistroBuilderCanonicalOrderService canonicalOrders;
    private BistroBuilderDishCatalogService dishCatalog;
    private BistroBuilderCustomerExperienceTrackingService experience;
    private BistroBuilderGeneralGameStateService generalGame;
    private GameClock gameClock;

    private RectTransform activityPanel;
    private TMP_Text legacyActivityText;
    private RectTransform activityRowsRoot;
    private TMP_Text activityEmptyText;

    private RectTransform contextPanel;
    private TMP_Text legacyContextTitle;
    private TMP_Text legacyContextBody;
    private RectTransform selectedRoot;
    private TMP_Text tableTitle;
    private TMP_Text tableStatus;
    private TMP_Text tablePeople;
    private TMP_Text tableElapsed;
    private TMP_Text clientCount;
    private TMP_Text clientBreakdown;
    private TMP_Text arrivalText;
    private TMP_Text stateText;
    private TMP_Text satisfactionText;
    private Image satisfactionFill;
    private TMP_Text orderSummary;
    private RectTransform orderRowsRoot;
    private Button addOrderButton;
    private Button splitBillButton;
    private Button closeTableButton;

    private readonly Dictionary<int, int> arrivalMinuteByGroup = new Dictionary<int, int>();
    private readonly Dictionary<string, string> activityClockByMessage =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<string> activityMessages = new List<string>(16);
    private readonly List<OrderDisplayRow> orderRows = new List<OrderDisplayRow>(16);

    private bool subscribed;
    private float nextRefreshAt;

    public bool IsShowingSelectedTable =>
        selectedRoot != null && selectedRoot.gameObject.activeSelf;
    public string SelectedTableTitle => tableTitle != null ? tableTitle.text : string.Empty;
    public string SelectedTableStatus => tableStatus != null ? tableStatus.text : string.Empty;
    public int VisibleActivityRowCount => activityRowsRoot != null ? activityRowsRoot.childCount : 0;

    private void Awake()
    {
        ResolveDependencies();
        EnsurePresentation();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        EnsurePresentation();
        Subscribe();
        PrimeArrivalTracking();
        RefreshAll(true);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextRefreshAt) return;
        ResolveDependencies();
        EnsurePresentation();
        Subscribe();
        PrimeArrivalTracking();
        RefreshAll(false);
        nextRefreshAt = Time.unscaledTime + 0.20f;
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        EnsurePresentation();
        if (shell == null || selection == null || activityPanel == null ||
            contextPanel == null || activityRowsRoot == null || selectedRoot == null)
        {
            error = "El HUD de referencia necesita shell, selección de mesas y ambos paneles.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void ResolveDependencies()
    {
        if (shell == null) shell = GetComponent<BistroBuilderUiShell>();
        if (selection == null) selection = GetComponent<BistroBuilderTableSelectionController>();
        if (tableAssignments == null)
            tableAssignments = FindFirstObjectByType<TableAssignmentSystem>(FindObjectsInactive.Include);
        if (orderSystem == null)
            orderSystem = FindFirstObjectByType<OrderSystem>(FindObjectsInactive.Include);
        if (canonicalOrders == null)
            canonicalOrders = FindFirstObjectByType<BistroBuilderCanonicalOrderService>(FindObjectsInactive.Include);
        if (dishCatalog == null)
            dishCatalog = FindFirstObjectByType<BistroBuilderDishCatalogService>(FindObjectsInactive.Include);
        if (experience == null)
            experience = FindFirstObjectByType<BistroBuilderCustomerExperienceTrackingService>(FindObjectsInactive.Include);
        if (generalGame == null)
            generalGame = FindFirstObjectByType<BistroBuilderGeneralGameStateService>(FindObjectsInactive.Include);
        if (gameClock == null)
            gameClock = FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (selection == null || tableAssignments == null) return;
        selection.SelectionChanged -= HandleSelectionChanged;
        selection.SelectionChanged += HandleSelectionChanged;
        tableAssignments.TableAssigned -= HandleTableAssigned;
        tableAssignments.TableAssigned += HandleTableAssigned;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (selection != null) selection.SelectionChanged -= HandleSelectionChanged;
        if (tableAssignments != null) tableAssignments.TableAssigned -= HandleTableAssigned;
        subscribed = false;
    }

    private void HandleSelectionChanged(RestaurantTable table)
    {
        RefreshSelectedTable(table);
    }

    private void HandleTableAssigned(CustomerGroup group, RestaurantTable table)
    {
        if (group != null) arrivalMinuteByGroup[group.GroupId] = CurrentClockMinute();
        RefreshAll(true);
    }

    private int CurrentClockMinute()
    {
        return gameClock != null ? gameClock.Hour * 60 + gameClock.Minute : 0;
    }

    private string ClockText()
    {
        return gameClock != null ? gameClock.Hour.ToString("00") + ":" + gameClock.Minute.ToString("00") : "--:--";
    }

    private void PrimeArrivalTracking()
    {
        if (tableAssignments == null) return;
        int now = CurrentClockMinute();
        IReadOnlyList<CustomerGroup> groups = tableAssignments.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null || !group.HasAssignedTable || arrivalMinuteByGroup.ContainsKey(group.GroupId)) continue;
            arrivalMinuteByGroup[group.GroupId] = now;
        }
    }

    private void EnsurePresentation()
    {
        if (shell == null) return;
        shell.EnsureShell();
        Transform shellRoot = transform.Find(BistroBuilderUiShell.RootName);
        if (shellRoot == null) return;

        activityPanel = shellRoot.Find(BistroBuilderUiShell.ActivityPanelName) as RectTransform;
        contextPanel = shellRoot.Find(BistroBuilderUiShell.ContextPanelName) as RectTransform;
        if (activityPanel != null) EnsureActivityPanel();
        if (contextPanel != null) EnsureContextPanel();
    }

    private void EnsureActivityPanel()
    {
        activityPanel.sizeDelta = new Vector2(ActivityWidth, -152f);
        legacyActivityText = activityPanel.Find("ActivityText")?.GetComponent<TMP_Text>();
        if (legacyActivityText != null) legacyActivityText.enabled = false;

        TMP_Text heading = activityPanel.Find("ActivityHeading")?.GetComponent<TMP_Text>();
        if (heading != null)
        {
            heading.text = "Actividad";
            heading.fontSize = 22f;
        }

        Transform existing = activityPanel.Find("BB_ReferenceActivity");
        if (existing != null)
        {
            activityRowsRoot = existing.Find("Rows") as RectTransform;
            activityEmptyText = existing.Find("Empty")?.GetComponent<TMP_Text>();
            return;
        }

        RectTransform root = NewRect("BB_ReferenceActivity", activityPanel);
        Stretch(root);
        root.offsetMin = new Vector2(12f, 12f);
        root.offsetMax = new Vector2(-12f, -52f);

        Button today = CreateButton(root, "Today", "Hoy", BistroBuilderUiTokens.Surface1);
        RectTransform todayRect = today.GetComponent<RectTransform>();
        todayRect.anchorMin = todayRect.anchorMax = new Vector2(1f, 1f);
        todayRect.pivot = new Vector2(1f, 1f);
        todayRect.anchoredPosition = new Vector2(0f, 40f);
        todayRect.sizeDelta = new Vector2(128f, 36f);
        today.interactable = false;

        activityRowsRoot = NewRect("Rows", root);
        activityRowsRoot.anchorMin = new Vector2(0f, 0f);
        activityRowsRoot.anchorMax = new Vector2(1f, 1f);
        activityRowsRoot.offsetMin = new Vector2(0f, 42f);
        activityRowsRoot.offsetMax = Vector2.zero;
        VerticalLayoutGroup list = activityRowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 4f;
        list.childControlHeight = false;
        list.childControlWidth = true;
        list.childForceExpandHeight = false;
        list.childForceExpandWidth = true;

        activityEmptyText = CreateText(root, "Empty", "Sin actividad reciente", 13f,
            BistroBuilderUiTokens.TextMuted, TextAlignmentOptions.Center);
        Stretch(activityEmptyText.rectTransform);
        activityEmptyText.rectTransform.offsetMin = new Vector2(0f, 70f);
        activityEmptyText.rectTransform.offsetMax = new Vector2(0f, -70f);

        Button footer = CreateButton(root, "Footer", "Ver toda la actividad   →", BistroBuilderUiTokens.Surface1);
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = Vector2.zero;
        footerRect.sizeDelta = new Vector2(0f, 36f);
        footer.interactable = false;
    }

    private void EnsureContextPanel()
    {
        contextPanel.sizeDelta = new Vector2(ContextWidth, -152f);
        legacyContextTitle = contextPanel.Find("Title")?.GetComponent<TMP_Text>();
        legacyContextBody = contextPanel.Find("Body")?.GetComponent<TMP_Text>();

        Transform existing = contextPanel.Find("BB_SelectedTableReference");
        if (existing != null)
        {
            selectedRoot = existing as RectTransform;
            CacheSelectedReferences();
            return;
        }

        selectedRoot = NewRect("BB_SelectedTableReference", contextPanel);
        Stretch(selectedRoot);
        selectedRoot.offsetMin = new Vector2(12f, 12f);
        selectedRoot.offsetMax = new Vector2(-12f, -12f);

        tableTitle = CreateText(selectedRoot, "TableTitle", "Mesa —", 28f,
            BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.TopLeft);
        Place(tableTitle.rectTransform, 0f, -2f, 182f, 42f);

        tableStatus = CreatePill(selectedRoot, "TableStatus", "Libre",
            new Color32(57, 86, 43, 255), new Color32(168, 212, 126, 255));
        Place(tableStatus.rectTransform.parent as RectTransform, 196f, -2f, 118f, 34f);

        TMP_Text more = CreateText(selectedRoot, "More", "⋮", 22f,
            BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.Center);
        Place(more.rectTransform, 324f, -2f, 36f, 34f);

        tablePeople = CreateText(selectedRoot, "People", "0 personas", 13f,
            BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.MidlineLeft);
        Place(tablePeople.rectTransform, 0f, -48f, 152f, 28f);

        tableElapsed = CreateText(selectedRoot, "Elapsed", "Tiempo en mesa: 0 min", 13f,
            BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.MidlineRight);
        Place(tableElapsed.rectTransform, 164f, -48f, 196f, 28f);

        RectTransform tabs = NewRect("Tabs", selectedRoot);
        Place(tabs, 0f, -82f, 360f, 42f);
        AddSurface(tabs.gameObject, new Color32(37, 36, 31, 255));
        string[] tabNames = { "Detalles", "Pedido", "Clientes", "Historial" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            TMP_Text tab = CreateText(tabs, "Tab" + i, tabNames[i], 12.5f,
                i == 0 ? BistroBuilderUiTokens.ContentLight : BistroBuilderUiTokens.TextSecondary,
                TextAlignmentOptions.Center);
            RectTransform rect = tab.rectTransform;
            rect.anchorMin = new Vector2(i / 4f, 0f);
            rect.anchorMax = new Vector2((i + 1) / 4f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        Image underline = NewRect("ActiveUnderline", tabs).gameObject.AddComponent<Image>();
        underline.color = BistroBuilderUiTokens.Success;
        RectTransform underlineRect = underline.rectTransform;
        underlineRect.anchorMin = new Vector2(0f, 0f);
        underlineRect.anchorMax = new Vector2(0.25f, 0f);
        underlineRect.pivot = new Vector2(0.5f, 0f);
        underlineRect.anchoredPosition = Vector2.zero;
        underlineRect.sizeDelta = new Vector2(0f, 3f);

        RectTransform details = NewRect("DetailsCard", selectedRoot);
        Place(details, 0f, -132f, 360f, 168f);
        AddSurface(details.gameObject, new Color32(42, 40, 34, 255));

        clientCount = CreateText(details, "ClientCount", "0 clientes", 14f,
            BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.MidlineLeft);
        Place(clientCount.rectTransform, 46f, -12f, 280f, 25f);
        clientBreakdown = CreateText(details, "ClientBreakdown", "0 personas", 11.5f,
            BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.MidlineLeft);
        Place(clientBreakdown.rectTransform, 46f, -34f, 280f, 22f);
        AddInfoIcon(details, "👥", -10f);

        arrivalText = CreateDetailRow(details, "Arrival", "Llegada", "—", 68f, "◷");
        stateText = CreateDetailRow(details, "State", "Estado", "Libre", 103f, "🍴");

        TMP_Text satisfactionLabel = CreateText(details, "SatisfactionLabel", "Satisfacción", 12.5f,
            BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.MidlineLeft);
        Place(satisfactionLabel.rectTransform, 46f, -138f, 120f, 22f);
        TMP_Text smile = CreateText(details, "Smile", "●", 19f,
            BistroBuilderUiTokens.Success, TextAlignmentOptions.Center);
        Place(smile.rectTransform, 8f, -136f, 26f, 24f);
        satisfactionText = CreateText(details, "SatisfactionValue", "—", 12f,
            BistroBuilderUiTokens.Success, TextAlignmentOptions.MidlineLeft);
        Place(satisfactionText.rectTransform, 188f, -134f, 150f, 20f);
        RectTransform track = NewRect("SatisfactionTrack", details);
        Place(track, 188f, -157f, 150f, 7f);
        AddSurface(track.gameObject, new Color32(25, 25, 22, 255));
        satisfactionFill = NewRect("Fill", track).gameObject.AddComponent<Image>();
        satisfactionFill.color = BistroBuilderUiTokens.Success;
        RectTransform fillRect = satisfactionFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0.75f, 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        RectTransform orderCard = NewRect("OrderCard", selectedRoot);
        Place(orderCard, 0f, -310f, 360f, 224f);
        AddSurface(orderCard.gameObject, new Color32(42, 40, 34, 255));
        TMP_Text orderTitle = CreateText(orderCard, "OrderTitle", "Pedido actual", 15f,
            BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.MidlineLeft);
        Place(orderTitle.rectTransform, 12f, -8f, 190f, 28f);
        orderSummary = CreateText(orderCard, "OrderSummary", "0 / 0 servidos", 11.5f,
            BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.MidlineRight);
        Place(orderSummary.rectTransform, 210f, -8f, 138f, 28f);
        orderRowsRoot = NewRect("OrderRows", orderCard);
        Place(orderRowsRoot, 8f, -44f, 344f, 170f);
        VerticalLayoutGroup orderList = orderRowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        orderList.spacing = 2f;
        orderList.childControlHeight = false;
        orderList.childControlWidth = true;
        orderList.childForceExpandHeight = false;
        orderList.childForceExpandWidth = true;

        addOrderButton = CreateButton(selectedRoot, "AddOrder", "⊕   Añadir al pedido", BistroBuilderUiTokens.Surface2);
        Place(addOrderButton.GetComponent<RectTransform>(), 0f, -544f, 360f, 42f);
        addOrderButton.onClick.AddListener(OpenOrders);

        splitBillButton = CreateButton(selectedRoot, "SplitBill", "▤   Dividir cuenta", BistroBuilderUiTokens.Surface1);
        Place(splitBillButton.GetComponent<RectTransform>(), 0f, -594f, 172f, 42f);
        splitBillButton.interactable = false;

        closeTableButton = CreateButton(selectedRoot, "CloseTable", "▣   Cerrar mesa", BistroBuilderUiTokens.Primary);
        Place(closeTableButton.GetComponent<RectTransform>(), 188f, -594f, 172f, 42f);
        closeTableButton.interactable = false;

        selectedRoot.gameObject.SetActive(false);
    }

    private void CacheSelectedReferences()
    {
        tableTitle = FindText(selectedRoot, "TableTitle");
        tableStatus = selectedRoot.Find("TableStatus")?.Find("Label")?.GetComponent<TMP_Text>();
        tablePeople = FindText(selectedRoot, "People");
        tableElapsed = FindText(selectedRoot, "Elapsed");
        RectTransform details = selectedRoot.Find("DetailsCard") as RectTransform;
        if (details != null)
        {
            clientCount = FindText(details, "ClientCount");
            clientBreakdown = FindText(details, "ClientBreakdown");
            arrivalText = details.Find("Arrival")?.Find("Value")?.GetComponent<TMP_Text>();
            stateText = details.Find("State")?.Find("Value")?.GetComponent<TMP_Text>();
            satisfactionText = FindText(details, "SatisfactionValue");
            satisfactionFill = details.Find("SatisfactionTrack")?.Find("Fill")?.GetComponent<Image>();
        }
        RectTransform orderCard = selectedRoot.Find("OrderCard") as RectTransform;
        if (orderCard != null)
        {
            orderSummary = FindText(orderCard, "OrderSummary");
            orderRowsRoot = orderCard.Find("OrderRows") as RectTransform;
        }
        addOrderButton = selectedRoot.Find("AddOrder")?.GetComponent<Button>();
        splitBillButton = selectedRoot.Find("SplitBill")?.GetComponent<Button>();
        closeTableButton = selectedRoot.Find("CloseTable")?.GetComponent<Button>();
    }

    private void RefreshAll(bool force)
    {
        RefreshActivity();
        RefreshSelectedTable(selection != null ? selection.SelectedTable : null);
    }

    private void RefreshActivity()
    {
        if (activityRowsRoot == null) return;
        activityMessages.Clear();
        if (legacyActivityText != null)
        {
            string clean = Regex.Replace(legacyActivityText.text ?? string.Empty, "<.*?>", string.Empty);
            string[] lines = clean.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string message = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(message) ||
                    message.StartsWith("Lo importante", StringComparison.OrdinalIgnoreCase) ||
                    message.StartsWith("Sin incidencias", StringComparison.OrdinalIgnoreCase))
                    continue;
                message = message.TrimStart('•', ' ', '●');
                if (!string.IsNullOrWhiteSpace(message) && !activityMessages.Contains(message))
                    activityMessages.Add(message);
            }
        }

        int shown = Mathf.Min(MaxActivityRows, activityMessages.Count);
        EnsureChildCount(activityRowsRoot, shown, CreateActivityRow);
        for (int i = 0; i < shown; i++)
            BindActivityRow(activityRowsRoot.GetChild(i), activityMessages[i], i == 0);

        if (activityEmptyText != null) activityEmptyText.gameObject.SetActive(shown == 0);
    }

    private GameObject CreateActivityRow()
    {
        RectTransform row = NewRect("ActivityRow", activityRowsRoot);
        LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 57f;
        layout.preferredHeight = 57f;
        AddSurface(row.gameObject, new Color32(42, 40, 34, 242));

        TMP_Text marker = CreateText(row, "Marker", "●", 17f,
            BistroBuilderUiTokens.TextMuted, TextAlignmentOptions.Center);
        Place(marker.rectTransform, 6f, -8f, 34f, 40f);
        TMP_Text title = CreateText(row, "Title", "Actividad", 13.2f,
            BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.MidlineLeft);
        Place(title.rectTransform, 44f, -7f, 210f, 24f);
        TMP_Text subtitle = CreateText(row, "Subtitle", string.Empty, 11f,
            BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.MidlineLeft);
        Place(subtitle.rectTransform, 44f, -29f, 238f, 21f);
        TMP_Text time = CreateText(row, "Time", "--:--", 10.5f,
            BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.MidlineRight);
        Place(time.rectTransform, 258f, -7f, 50f, 24f);
        return row.gameObject;
    }

    private void BindActivityRow(Transform row, string message, bool newest)
    {
        TMP_Text marker = row.Find("Marker")?.GetComponent<TMP_Text>();
        TMP_Text title = row.Find("Title")?.GetComponent<TMP_Text>();
        TMP_Text subtitle = row.Find("Subtitle")?.GetComponent<TMP_Text>();
        TMP_Text time = row.Find("Time")?.GetComponent<TMP_Text>();

        SplitActivity(message, out string rowTitle, out string rowSubtitle, out Color color);
        if (marker != null) marker.color = color;
        if (title != null) title.text = rowTitle;
        if (subtitle != null) subtitle.text = rowSubtitle;
        if (!activityClockByMessage.TryGetValue(message, out string stamp))
        {
            stamp = ClockText();
            activityClockByMessage[message] = stamp;
        }
        if (time != null) time.text = stamp;

        Image image = row.GetComponent<Image>();
        if (image != null)
            image.color = newest ? new Color32(49, 52, 39, 248) : new Color32(42, 40, 34, 242);
    }

    private static void SplitActivity(string message, out string title, out string subtitle, out Color color)
    {
        string[] pieces = message.Split(new[] { " · " }, 2, StringSplitOptions.None);
        title = pieces.Length > 0 ? pieces[0].Trim() : "Actividad";
        subtitle = pieces.Length > 1 ? pieces[1].Trim() : string.Empty;
        string key = title.ToLowerInvariant();
        if (key.Contains("cocina")) color = new Color32(238, 222, 194, 255);
        else if (key.Contains("inventario") || key.Contains("stock")) color = BistroBuilderUiTokens.Success;
        else if (key.Contains("reserva")) color = BistroBuilderUiTokens.Info;
        else if (key.Contains("mesa")) color = BistroBuilderUiTokens.Success;
        else if (key.Contains("cierre") || key.Contains("incidencia")) color = BistroBuilderUiTokens.Attention;
        else color = BistroBuilderUiTokens.TextSecondary;
    }

    private void RefreshSelectedTable(RestaurantTable table)
    {
        bool show = table != null && contextPanel != null && contextPanel.gameObject.activeInHierarchy;
        if (selectedRoot != null) selectedRoot.gameObject.SetActive(show);
        if (legacyContextTitle != null) legacyContextTitle.enabled = !show;
        if (legacyContextBody != null) legacyContextBody.enabled = !show;
        if (!show) return;

        CustomerGroup group = table.AssignedCustomerGroup;
        int groupSize = group != null ? group.GroupSize : 0;
        string status = TableStateLabel(table.CurrentState);

        if (tableTitle != null) tableTitle.text = "Mesa " + table.TableId;
        if (tableStatus != null)
        {
            tableStatus.text = status;
            tableStatus.color = StatusColor(table.CurrentState);
        }
        if (tablePeople != null) tablePeople.text = groupSize + " personas";
        if (tableElapsed != null) tableElapsed.text = "Tiempo en mesa: " + ElapsedMinutes(group) + " min";
        if (clientCount != null) clientCount.text = groupSize + " clientes";
        if (clientBreakdown != null) clientBreakdown.text = groupSize + " personas";
        if (arrivalText != null) arrivalText.text = ArrivalText(group);
        if (stateText != null) stateText.text = status;

        int satisfaction = ResolveLiveSatisfaction(group);
        if (satisfactionText != null) satisfactionText.text = SatisfactionLabel(satisfaction);
        if (satisfactionFill != null)
        {
            RectTransform fill = satisfactionFill.rectTransform;
            fill.anchorMax = new Vector2(Mathf.Clamp01(satisfaction / 100f), 1f);
            satisfactionFill.color = satisfaction >= 70 ? BistroBuilderUiTokens.Success :
                satisfaction >= 45 ? BistroBuilderUiTokens.Attention : BistroBuilderUiTokens.Critical;
        }

        BuildOrderRows(table);
        if (addOrderButton != null) addOrderButton.interactable = group != null;
        if (splitBillButton != null) splitBillButton.interactable = false;
        if (closeTableButton != null) closeTableButton.interactable = false;
    }

    private string ArrivalText(CustomerGroup group)
    {
        if (group == null) return "—";
        if (!arrivalMinuteByGroup.TryGetValue(group.GroupId, out int minute))
            minute = CurrentClockMinute();
        minute = ((minute % 1440) + 1440) % 1440;
        return (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
    }

    private int ElapsedMinutes(CustomerGroup group)
    {
        if (group == null || !arrivalMinuteByGroup.TryGetValue(group.GroupId, out int arrival)) return 0;
        int now = CurrentClockMinute();
        int elapsed = now - arrival;
        if (elapsed < 0) elapsed += 1440;
        return Mathf.Clamp(elapsed, 0, 1439);
    }

    private int ResolveLiveSatisfaction(CustomerGroup group)
    {
        if (group == null || experience == null) return 0;
        if (!experience.TryGetRuntimeVisit(group.GroupId, out BistroBuilderReputationVisitRuntimeRecord visit) || visit == null)
            return experience.LastRecordedSatisfactionBasisPoints > 0
                ? Mathf.Clamp(Mathf.RoundToInt(experience.LastRecordedSatisfactionBasisPoints / 100f), 0, 100)
                : 0;
        int day = generalGame != null ? generalGame.DayIndex : 1;
        if (BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
                visit, Mathf.Max(1, day), out BistroBuilderCustomerExperienceRecord result, out _) &&
            result != null)
            return Mathf.Clamp(Mathf.RoundToInt(result.overallSatisfactionBasisPoints / 100f), 0, 100);
        return 0;
    }

    private static string SatisfactionLabel(int percent)
    {
        if (percent >= 85) return "Excelente";
        if (percent >= 70) return "Muy buena";
        if (percent >= 55) return "Buena";
        if (percent >= 40) return "Regular";
        if (percent > 0) return "Baja";
        return "—";
    }

    private void BuildOrderRows(RestaurantTable table)
    {
        orderRows.Clear();
        RestaurantOrder active = orderSystem != null ? orderSystem.GetActiveOrderForTable(table) : null;
        if (active != null && active.HasCanonicalOrder && canonicalOrders != null &&
            canonicalOrders.TryGetOrderSnapshot(active.CanonicalOrderId, out BistroBuilderCanonicalOrder order) &&
            order != null)
        {
            var byKey = new Dictionary<string, OrderDisplayRow>(StringComparer.Ordinal);
            for (int i = 0; i < order.Lines.Count; i++)
            {
                BistroBuilderCanonicalOrderLine line = order.Lines[i];
                if (line == null ||
                    line.State == BistroBuilderCanonicalOrderLineState.Cancelled ||
                    line.State == BistroBuilderCanonicalOrderLineState.Failed)
                    continue;
                string name = ResolveDishName(line.DishId);
                string status = OrderStatus(line.State);
                string key = name + "|" + status;
                if (!byKey.TryGetValue(key, out OrderDisplayRow row))
                {
                    row = new OrderDisplayRow { DishName = name, Status = status, Quantity = 0 };
                    byKey.Add(key, row);
                    orderRows.Add(row);
                }
                row.Quantity++;
            }
        }

        int served = 0;
        int total = 0;
        for (int i = 0; i < orderRows.Count; i++)
        {
            total += orderRows[i].Quantity;
            if (orderRows[i].Status == "Servido") served += orderRows[i].Quantity;
        }
        if (orderSummary != null) orderSummary.text = served + " / " + total + " servidos";

        int shown = Mathf.Min(MaxOrderRows, orderRows.Count);
        EnsureChildCount(orderRowsRoot, shown, CreateOrderRow);
        for (int i = 0; i < shown; i++)
            BindOrderRow(orderRowsRoot.GetChild(i), orderRows[i]);
    }

    private string ResolveDishName(string dishId)
    {
        if (dishCatalog != null && dishCatalog.TryGetDefinition(dishId, out BistroBuilderDishDefinition dish) &&
            dish != null && !string.IsNullOrWhiteSpace(dish.DisplayName))
            return dish.DisplayName;
        return string.IsNullOrWhiteSpace(dishId) ? "Plato" : dishId;
    }

    private static string OrderStatus(BistroBuilderCanonicalOrderLineState state)
    {
        switch (state)
        {
            case BistroBuilderCanonicalOrderLineState.Served:
            case BistroBuilderCanonicalOrderLineState.Consumed:
                return "Servido";
            case BistroBuilderCanonicalOrderLineState.Preparing:
            case BistroBuilderCanonicalOrderLineState.ReadyForPickup:
            case BistroBuilderCanonicalOrderLineState.AssignedForDelivery:
            case BistroBuilderCanonicalOrderLineState.InTransit:
                return "En cocina";
            default:
                return "Pendiente";
        }
    }

    private GameObject CreateOrderRow()
    {
        RectTransform row = NewRect("OrderRow", orderRowsRoot);
        LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 29f;
        layout.preferredHeight = 29f;
        TMP_Text quantity = CreateText(row, "Quantity", "1", 11.5f,
            BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.MidlineLeft);
        Place(quantity.rectTransform, 4f, -2f, 24f, 25f);
        TMP_Text dish = CreateText(row, "Dish", "Plato", 11.8f,
            BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.MidlineLeft);
        Place(dish.rectTransform, 32f, -2f, 182f, 25f);
        TMP_Text status = CreateText(row, "Status", "Pendiente", 11.3f,
            BistroBuilderUiTokens.Attention, TextAlignmentOptions.MidlineRight);
        Place(status.rectTransform, 216f, -2f, 120f, 25f);
        return row.gameObject;
    }

    private static void BindOrderRow(Transform row, OrderDisplayRow data)
    {
        TMP_Text quantity = row.Find("Quantity")?.GetComponent<TMP_Text>();
        TMP_Text dish = row.Find("Dish")?.GetComponent<TMP_Text>();
        TMP_Text status = row.Find("Status")?.GetComponent<TMP_Text>();
        if (quantity != null) quantity.text = data.Quantity.ToString();
        if (dish != null) dish.text = data.DishName;
        if (status != null)
        {
            status.text = (data.Status == "Servido" ? "●  " :
                data.Status == "En cocina" ? "◷  " : "○  ") + data.Status;
            status.color = data.Status == "Servido" ? BistroBuilderUiTokens.Success : BistroBuilderUiTokens.Attention;
        }
    }

    private void OpenOrders()
    {
        BistroBuilderAdvancedOrderPlayerScreen screen =
            FindFirstObjectByType<BistroBuilderAdvancedOrderPlayerScreen>(FindObjectsInactive.Include);
        if (screen != null) screen.Show();
    }

    private static string TableStateLabel(TableState state)
    {
        switch (state)
        {
            case TableState.WaitingForWaiter: return "Esperando";
            case TableState.TakingOrder: return "Pidiendo";
            case TableState.WaitingForFood: return "Esperando platos";
            case TableState.Eating: return "Comiendo";
            case TableState.WaitingForBill: return "Pidiendo cuenta";
            case TableState.Paying: return "Pagando";
            case TableState.Dirty: return "Por limpiar";
            default: return "Libre";
        }
    }

    private static Color StatusColor(TableState state)
    {
        switch (state)
        {
            case TableState.Dirty: return BistroBuilderUiTokens.Critical;
            case TableState.WaitingForWaiter:
            case TableState.WaitingForFood:
            case TableState.WaitingForBill: return BistroBuilderUiTokens.Attention;
            default: return BistroBuilderUiTokens.Success;
        }
    }

    private static TMP_Text CreateDetailRow(RectTransform parent, string name, string label, string value, float top, string symbol)
    {
        RectTransform row = NewRect(name, parent);
        Place(row, 0f, -top, 360f, 30f);
        TMP_Text icon = CreateText(row, "Icon", symbol, 17f, BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.Center);
        Place(icon.rectTransform, 8f, -2f, 26f, 26f);
        TMP_Text caption = CreateText(row, "Caption", label, 12.5f, BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.MidlineLeft);
        Place(caption.rectTransform, 46f, -2f, 104f, 26f);
        TMP_Text text = CreateText(row, "Value", value, 12.5f, BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.MidlineRight);
        Place(text.rectTransform, 176f, -2f, 160f, 26f);
        return text;
    }

    private static void AddInfoIcon(RectTransform parent, string value, float y)
    {
        TMP_Text icon = CreateText(parent, "ClientsIcon", value, 17f, BistroBuilderUiTokens.TextSecondary, TextAlignmentOptions.Center);
        Place(icon.rectTransform, 8f, y, 26f, 34f);
    }

    private static TMP_Text CreatePill(RectTransform parent, string name, string value, Color background, Color foreground)
    {
        RectTransform root = NewRect(name, parent);
        AddSurface(root.gameObject, background);
        TMP_Text text = CreateText(root, "Label", value, 12.5f, foreground, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string text, Color color)
    {
        RectTransform root = NewRect(name, parent);
        Image image = root.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Color hover = Color.Lerp(color, Color.white, 0.08f);
        Color pressed = Color.Lerp(color, Color.black, 0.10f);
        button.colors = BistroBuilderUiTokens.ButtonColors(color, hover, pressed);
        TMP_Text label = CreateText(root, "Label", text, 12.5f, BistroBuilderUiTokens.ContentLight, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent != null ? parent.gameObject.layer : 5;
        return go.GetComponent<RectTransform>();
    }

    private static void AddSurface(GameObject go, Color color)
    {
        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static TMP_Text FindText(Transform parent, string name)
    {
        return parent != null ? parent.Find(name)?.GetComponent<TMP_Text>() : null;
    }

    private static void EnsureChildCount(RectTransform root, int count, Func<GameObject> create)
    {
        if (root == null) return;
        while (root.childCount < count) create();
        for (int i = 0; i < root.childCount; i++)
            root.GetChild(i).gameObject.SetActive(i < count);
    }

    private sealed class OrderDisplayRow
    {
        public int Quantity;
        public string DishName;
        public string Status;
    }
}
