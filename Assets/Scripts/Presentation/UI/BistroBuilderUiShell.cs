using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Shell definitivo del HUD 21A. Presentation pura: organiza accesos ya existentes,
/// muestra read-models globales y mantiene el restaurante como superficie principal.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Definitive HUD Shell")]
public sealed class BistroBuilderUiShell : MonoBehaviour
{
    public const string RuntimeRevision = "21A-UIUX-SHELL-V1.0";
    public const string RootName = "BB_UIUX_Shell";
    public const string TopBarName = "BB_UIUX_TopNavigation";
    public const string BottomBarName = "BB_UIUX_BottomOperations";
    public const string ActivityPanelName = "BB_UIUX_ActivityPanel";

    private static readonly NavSpec[] Navigation =
    {
        new NavSpec("Actividad", null),
        new NavSpec("Personal", "OpenStaffButton"),
        new NavSpec("Carta", "OpenMenuPortfolio", "OpenMenuEditor"),
        new NavSpec("Inventario", "OpenInventoryWarehouse", "OpenInventoryPlanning"),
        new NavSpec("Proveedores", "OpenSuppliers"),
        new NavSpec("Reservas", "OpenReservationsButton"),
        new NavSpec("Economía", "OpenFinance"),
        new NavSpec("Marketing", "OpenMarketingButton"),
        new NavSpec("Reputación", "OpenReputationButton"),
        new NavSpec("Progreso", "OpenProgressionButton")
    };

    [SerializeField] private RectTransform shellRoot;
    [SerializeField] private RectTransform topNavigation;
    [SerializeField] private RectTransform navContent;
    [SerializeField] private RectTransform bottomOperations;
    [SerializeField] private RectTransform bottomStatusContent;
    [SerializeField] private RectTransform activityPanel;
    [SerializeField] private TMP_Text activityText;
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private TMP_Text satisfactionText;
    [SerializeField] private TMP_Text kitchenText;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private Button closeManagementButton;

    private readonly Dictionary<string, Button> proxyButtons =
        new Dictionary<string, Button>(StringComparer.Ordinal);
    private readonly List<string> recentActivity = new List<string>(8);
    private readonly List<BistroBuilderInventoryAlertSnapshot> inventoryAlerts =
        new List<BistroBuilderInventoryAlertSnapshot>(24);

    private Canvas canvas;
    private BistroBuilderUiDesignSystem designSystem;
    private BistroBuilderFinanceService finance;
    private BistroBuilderInventoryPlanningService inventoryPlanning;
    private BistroBuilderAdvancedKitchenService kitchen;
    private BistroBuilderAdvancedFrontOfHouseService frontOfHouse;
    private BistroBuilderCustomerExperienceTrackingService experience;
    private RestaurantEditInteractionController editController;
    private float nextRefreshAt;
    private bool activityVisible = true;
    private bool subscribed;

    private void Awake()
    {
        ResolveDependencies();
        EnsureShell();
        BindRuntime();
        ReconcileNavigation();
        RefreshReadModels();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        EnsureShell();
        BindRuntime();
        ReconcileNavigation();
        RefreshReadModels();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && IsAnyManagementScreenOpen())
        {
            CloseCurrentManagementScreen();
            return;
        }
        if (Time.unscaledTime < nextRefreshAt) return;
        ResolveDependencies();
        BindRuntime();
        ReconcileNavigation();
        RefreshReadModels();
        nextRefreshAt = Time.unscaledTime + 0.35f;
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        EnsureShell();
        if (canvas == null || shellRoot == null || topNavigation == null ||
            bottomOperations == null || activityPanel == null)
        {
            error = "El shell 21A no pudo resolver su Canvas o sus superficies principales.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void EnsureShell()
    {
        ResolveDependencies();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find(RootName);
        if (existing != null) shellRoot = existing as RectTransform;
        if (shellRoot == null)
        {
            GameObject root = NewUi(RootName, canvas.transform);
            shellRoot = root.GetComponent<RectTransform>();
            Stretch(shellRoot);
            shellRoot.SetAsLastSibling();
        }

        topNavigation = EnsureBar(shellRoot, TopBarName, true);
        bottomOperations = EnsureBar(shellRoot, BottomBarName, false);
        EnsureNavigationContent();
        EnsureBottomStatus();
        EnsureActivityPanel();
        EnsureNavigationButtons();
        ReconcileTimeDock();

        if (designSystem != null) designSystem.ApplyAllNow(true);
    }

    private RectTransform EnsureBar(RectTransform parent, string name, bool top)
    {
        Transform found = parent.Find(name);
        RectTransform rect = found as RectTransform;
        if (rect == null)
        {
            GameObject go = NewUi(name, parent);
            rect = go.GetComponent<RectTransform>();
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.09f, 0.13f, 0.12f, 0.94f);
            image.raycastTarget = true;
        }
        rect.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, top ? 64f : 64f);
        return rect;
    }

    private void EnsureNavigationContent()
    {
        Transform found = topNavigation.Find("NavigationContent");
        navContent = found as RectTransform;
        if (navContent == null)
        {
            GameObject go = NewUi("NavigationContent", topNavigation);
            navContent = go.GetComponent<RectTransform>();
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
        }
        Stretch(navContent);
    }

    private void EnsureNavigationButtons()
    {
        EnsureBrandLabel();
        for (int i = 0; i < Navigation.Length; i++)
        {
            NavSpec spec = Navigation[i];
            string key = "BBNav_" + Sanitize(spec.Label);
            Button proxy = EnsureButton(navContent, key, spec.Label, 92f);
            proxyButtons[spec.Label] = proxy;
        }
        Button edit = EnsureButton(navContent, "BBNav_Edicion", "Edición", 112f);
        proxyButtons["Edición"] = edit;
        closeManagementButton = EnsureButton(navContent, "BBNav_Cerrar", "Cerrar", 80f);
    }

    private void EnsureBrandLabel()
    {
        Transform found = navContent.Find("Brand");
        TMP_Text label = found != null ? found.GetComponent<TMP_Text>() : null;
        if (label == null)
        {
            GameObject go = NewUi("Brand", navContent);
            label = go.AddComponent<TextMeshProUGUI>();
            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minWidth = 178f;
            element.preferredWidth = 178f;
            element.flexibleWidth = 0f;
        }
        label.text = "BISTRO BUILDER";
        label.fontSize = 19f;
        label.fontStyle = FontStyles.Bold;
        label.color = BistroBuilderUiTokens.ContentLight;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
    }

    private void EnsureBottomStatus()
    {
        Transform found = bottomOperations.Find("StatusContent");
        bottomStatusContent = found as RectTransform;
        if (bottomStatusContent == null)
        {
            GameObject go = NewUi("StatusContent", bottomOperations);
            bottomStatusContent = go.GetComponent<RectTransform>();
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 360, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
        }
        Stretch(bottomStatusContent);

        cashText = EnsureStatusPill("Cash", "Caja —", 150f);
        satisfactionText = EnsureStatusPill("Satisfaction", "Satisfacción —", 170f);
        kitchenText = EnsureStatusPill("Kitchen", "Cocina —", 150f);
        waitingText = EnsureStatusPill("Waiting", "Espera: 0 clientes", 165f);
    }

    private TMP_Text EnsureStatusPill(string name, string value, float width)
    {
        Transform found = bottomStatusContent.Find(name);
        GameObject container = found != null ? found.gameObject : NewUi(name, bottomStatusContent);
        Image image = container.GetComponent<Image>();
        if (image == null) image = container.AddComponent<Image>();
        image.color = new Color(0.17f, 0.23f, 0.21f, 0.96f);
        image.raycastTarget = false;

        LayoutElement element = container.GetComponent<LayoutElement>();
        if (element == null) element = container.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;

        Transform labelTransform = container.transform.Find("Label");
        TMP_Text text = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        if (text == null)
        {
            GameObject label = NewUi("Label", container.transform);
            text = label.AddComponent<TextMeshProUGUI>();
            Stretch(label.GetComponent<RectTransform>());
        }        text.text = value;
        text.fontSize = 14f;
        text.color = BistroBuilderUiTokens.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private void EnsureActivityPanel()
    {
        Transform found = shellRoot.Find(ActivityPanelName);
        activityPanel = found as RectTransform;
        if (activityPanel == null)
        {
            GameObject panel = NewUi(ActivityPanelName, shellRoot);
            activityPanel = panel.GetComponent<RectTransform>();
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.09f, 0.13f, 0.12f, 0.90f);
            image.raycastTarget = true;
        }

        activityPanel.anchorMin = new Vector2(0f, 0f);
        activityPanel.anchorMax = new Vector2(0f, 1f);
        activityPanel.pivot = new Vector2(0f, 0.5f);
        activityPanel.anchoredPosition = new Vector2(12f, 0f);
        activityPanel.sizeDelta = new Vector2(284f, -152f);

        Transform textTransform = activityPanel.Find("ActivityText");
        activityText = textTransform != null ? textTransform.GetComponent<TMP_Text>() : null;
        if (activityText == null)
        {
            GameObject go = NewUi("ActivityText", activityPanel);
            activityText = go.AddComponent<TextMeshProUGUI>();
            RectTransform rect = go.GetComponent<RectTransform>();
            Stretch(rect);
            rect.offsetMin = new Vector2(16f, 16f);
            rect.offsetMax = new Vector2(-16f, -16f);
        }
        activityText.fontSize = 13.5f;
        activityText.color = BistroBuilderUiTokens.TextPrimary;
        activityText.alignment = TextAlignmentOptions.TopLeft;
        activityText.textWrappingMode = TextWrappingModes.Normal;
        activityText.raycastTarget = false;
        activityPanel.gameObject.SetActive(activityVisible);
    }

    private void ReconcileNavigation()
    {
        if (canvas == null || navContent == null) return;

        for (int i = 0; i < Navigation.Length; i++)
        {
            NavSpec spec = Navigation[i];
            if (!proxyButtons.TryGetValue(spec.Label, out Button proxy) || proxy == null) continue;
            proxy.onClick.RemoveAllListeners();

            if (spec.TargetNames == null || spec.TargetNames.Length == 0)
            {
                proxy.interactable = true;
                proxy.onClick.AddListener(ToggleActivityPanel);
                continue;
            }

            Button legacy = FindLegacyLauncher(spec.TargetNames);
            if (legacy != null) SuppressLegacyLauncher(legacy);

            string capturedLabel = spec.Label;
            if (HasDirectNavigation(capturedLabel))
            {
                proxy.interactable = true;
                proxy.onClick.AddListener(() => OpenDirectNavigation(capturedLabel));
            }
            else if (legacy != null)
            {
                proxy.interactable = legacy.interactable;
                Button captured = legacy;
                proxy.onClick.AddListener(() => captured.onClick.Invoke());
            }
            else
            {
                proxy.interactable = false;
            }
        }

        if (proxyButtons.TryGetValue("Edición", out Button editProxy) && editProxy != null)
        {
            editProxy.onClick.RemoveAllListeners();
            editProxy.interactable = editController != null;
            editProxy.onClick.AddListener(HandleEditModeClicked);
        }

        if (closeManagementButton != null)
        {
            closeManagementButton.onClick.RemoveAllListeners();
            closeManagementButton.interactable = IsAnyManagementScreenOpen();
            closeManagementButton.onClick.AddListener(CloseCurrentManagementScreen);
        }
    }

    private static bool HasDirectNavigation(string label)
    {
        switch (label)
        {
            case "Personal": return FindScene<BistroBuilderStaffPlayerScreen>() != null;
            case "Carta":
                return FindScene<BistroBuilderMenuPortfolioRuntimeView>() != null ||
                    FindScene<BistroBuilderMenuEditorRuntimeView>() != null;
            case "Inventario":
                return FindScene<BistroBuilderInventoryWarehouseRuntimeView>() != null ||
                    FindScene<BistroBuilderInventoryPlanningRuntimeView>() != null;
            case "Proveedores": return FindScene<BistroBuilderSupplierPlayerRuntimeView>() != null;
            case "Reservas": return FindScene<BistroBuilderReservationPlayerScreen>() != null;
            case "Economía": return FindScene<BistroBuilderFinanceRuntimeView>() != null;
            case "Marketing": return FindScene<BistroBuilderMarketingPlayerScreen>() != null;
            case "Reputación": return FindScene<BistroBuilderReputationPlayerScreen>() != null;
            case "Progreso": return FindScene<BistroBuilderProgressionPlayerScreen>() != null;
            default: return false;
        }
    }

    private void OpenDirectNavigation(string label)
    {
        if (!TryCloseManagementScreensBeforeOpening(label)) return;

        string error = string.Empty;
        bool opened = false;
        switch (label)
        {
            case "Personal":
                var staff = FindScene<BistroBuilderStaffPlayerScreen>();
                if (staff != null) { staff.Show(); opened = staff.IsVisible; }
                break;
            case "Carta":
                var portfolio = FindScene<BistroBuilderMenuPortfolioRuntimeView>();
                opened = portfolio != null && portfolio.TryOpen(out error);
                if (!opened)
                {
                    var editor = FindScene<BistroBuilderMenuEditorRuntimeView>();
                    if (editor != null) opened = editor.TryOpenFromInterface(out error);
                }
                break;
            case "Inventario":
                var warehouse = FindScene<BistroBuilderInventoryWarehouseRuntimeView>();
                opened = warehouse != null && warehouse.TryOpenFromInterface(out error);
                if (!opened)
                {
                    var planning = FindScene<BistroBuilderInventoryPlanningRuntimeView>();
                    if (planning != null) opened = planning.TryOpenFromInterface(out error);
                }
                break;
            case "Proveedores":
                var suppliers = FindScene<BistroBuilderSupplierPlayerRuntimeView>();
                opened = suppliers != null && suppliers.TryOpenFromInterface(out error);
                break;
            case "Reservas":
                var reservations = FindScene<BistroBuilderReservationPlayerScreen>();
                if (reservations != null) { reservations.Show(); opened = reservations.IsVisible; }
                break;
            case "Economía":
                var financeView = FindScene<BistroBuilderFinanceRuntimeView>();
                opened = financeView != null && financeView.TryOpenFromInterface(out error);
                break;
            case "Marketing":
                var marketing = FindScene<BistroBuilderMarketingPlayerScreen>();
                if (marketing != null) { marketing.Show(); opened = marketing.IsVisible; }
                break;
            case "Reputación":
                var reputation = FindScene<BistroBuilderReputationPlayerScreen>();
                if (reputation != null) { reputation.Show(); opened = reputation.IsVisible; }
                break;
            case "Progreso":
                var progression = FindScene<BistroBuilderProgressionPlayerScreen>();
                if (progression != null) { progression.Show(); opened = progression.IsVisible; }
                break;
        }

        if (!opened)
        {
            if (string.IsNullOrWhiteSpace(error)) error = "La pantalla todavía no está disponible.";
            AddActivity(label + " · " + error);
            RefreshActivityText();
        }
    }

    private bool TryCloseManagementScreensBeforeOpening(string nextLabel)
    {
        var editor = FindScene<BistroBuilderMenuEditorRuntimeView>();
        if (editor != null && editor.IsOpen && nextLabel != "Carta")
        {
            editor.RequestCloseFromInterface();
            if (editor.IsOpen) return false;
        }
        CloseSimpleManagementScreens(nextLabel);
        return true;
    }

    private void CloseSimpleManagementScreens(string exceptLabel = null)
    {
        var staff = FindScene<BistroBuilderStaffPlayerScreen>();
        if (staff != null && staff.IsVisible && exceptLabel != "Personal") staff.Hide();
        var portfolio = FindScene<BistroBuilderMenuPortfolioRuntimeView>();
        if (portfolio != null && portfolio.IsOpen && exceptLabel != "Carta") portfolio.Close();
        var warehouse = FindScene<BistroBuilderInventoryWarehouseRuntimeView>();
        if (warehouse != null && warehouse.IsOpen && exceptLabel != "Inventario") warehouse.Close();
        var planning = FindScene<BistroBuilderInventoryPlanningRuntimeView>();
        if (planning != null && planning.IsOpen && exceptLabel != "Inventario") planning.Close();
        var suppliers = FindScene<BistroBuilderSupplierPlayerRuntimeView>();
        if (suppliers != null && suppliers.IsOpen && exceptLabel != "Proveedores") suppliers.Close();
        var reservations = FindScene<BistroBuilderReservationPlayerScreen>();
        if (reservations != null && reservations.IsVisible && exceptLabel != "Reservas") reservations.Hide();
        var financeView = FindScene<BistroBuilderFinanceRuntimeView>();
        if (financeView != null && financeView.IsOpen && exceptLabel != "Economía") financeView.Close();
        var marketing = FindScene<BistroBuilderMarketingPlayerScreen>();
        if (marketing != null && marketing.IsVisible && exceptLabel != "Marketing") marketing.Hide();
        var reputation = FindScene<BistroBuilderReputationPlayerScreen>();
        if (reputation != null && reputation.IsVisible && exceptLabel != "Reputación") reputation.Hide();
        var progression = FindScene<BistroBuilderProgressionPlayerScreen>();
        if (progression != null && progression.IsVisible && exceptLabel != "Progreso") progression.Hide();
    }

    private bool IsAnyManagementScreenOpen()
    {
        var editor = FindScene<BistroBuilderMenuEditorRuntimeView>();
        if (editor != null && editor.IsOpen) return true;
        var portfolio = FindScene<BistroBuilderMenuPortfolioRuntimeView>();
        if (portfolio != null && portfolio.IsOpen) return true;
        var warehouse = FindScene<BistroBuilderInventoryWarehouseRuntimeView>();
        if (warehouse != null && warehouse.IsOpen) return true;
        var planning = FindScene<BistroBuilderInventoryPlanningRuntimeView>();
        if (planning != null && planning.IsOpen) return true;
        var suppliers = FindScene<BistroBuilderSupplierPlayerRuntimeView>();
        if (suppliers != null && suppliers.IsOpen) return true;
        var financeView = FindScene<BistroBuilderFinanceRuntimeView>();
        if (financeView != null && financeView.IsOpen) return true;
        var staff = FindScene<BistroBuilderStaffPlayerScreen>();
        if (staff != null && staff.IsVisible) return true;
        var reservations = FindScene<BistroBuilderReservationPlayerScreen>();
        if (reservations != null && reservations.IsVisible) return true;
        var marketing = FindScene<BistroBuilderMarketingPlayerScreen>();
        if (marketing != null && marketing.IsVisible) return true;
        var reputation = FindScene<BistroBuilderReputationPlayerScreen>();
        if (reputation != null && reputation.IsVisible) return true;
        var progression = FindScene<BistroBuilderProgressionPlayerScreen>();
        return progression != null && progression.IsVisible;
    }

    private void CloseCurrentManagementScreen()
    {
        var editor = FindScene<BistroBuilderMenuEditorRuntimeView>();
        if (editor != null && editor.IsOpen) { editor.RequestCloseFromInterface(); return; }
        CloseSimpleManagementScreens();
    }
    private Button FindLegacyLauncher(string[] names)
    {
        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];
                if (candidate == null || candidate.transform.IsChildOf(shellRoot)) continue;
                if (string.Equals(candidate.name, names[n], StringComparison.Ordinal)) return candidate;
            }
        }
        return null;
    }

    private static void SuppressLegacyLauncher(Button target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void ToggleActivityPanel()
    {
        activityVisible = !activityVisible;
        if (activityPanel != null) activityPanel.gameObject.SetActive(activityVisible);
    }

    private void HandleEditModeClicked()
    {
        if (editController == null) return;
        RestaurantEditModeService editMode =
            UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>(FindObjectsInactive.Include);
        if (editMode != null && editMode.IsEditModeActive)
            editController.TryExitEditMode(false);
        else
            editController.TryEnterEditMode();
    }

    private void RefreshReadModels()
    {
        if (cashText != null)
        {
            cashText.text = finance != null
                ? "Caja  " + BistroBuilderFinanceUiFormat.Money(finance.CurrentBalanceCents)
                : "Caja  —";
        }

        if (satisfactionText != null)
        {
            int satisfaction = experience != null
                ? experience.LastRecordedSatisfactionBasisPoints : 0;
            satisfactionText.text = satisfaction > 0
                ? "Satisfacción  " + (satisfaction / 100f).ToString("0") + "%"
                : "Satisfacción  —";
        }

        if (kitchenText != null)
        {
            kitchenText.text = "Cocina  " + KitchenLabel(kitchen != null
                ? kitchen.LoadState : BistroBuilderKitchenLoadState.Fluid);
            kitchenText.color = KitchenColor(kitchen != null
                ? kitchen.LoadState : BistroBuilderKitchenLoadState.Fluid);
        }

        if (waitingText != null)
        {
            int waiting = ResolveWaitingClientCount();
            waitingText.text = "Espera: " + waiting + " clientes";
            waitingText.color = waiting > 0 ? BistroBuilderUiTokens.Attention : BistroBuilderUiTokens.TextPrimary;
        }
        RefreshActivityText();
    }

    private void ResolveDependencies()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (designSystem == null && canvas != null) designSystem = canvas.GetComponent<BistroBuilderUiDesignSystem>();
        if (finance == null) finance = FindScene<BistroBuilderFinanceService>();
        if (inventoryPlanning == null) inventoryPlanning = FindScene<BistroBuilderInventoryPlanningService>();
        if (kitchen == null) kitchen = FindScene<BistroBuilderAdvancedKitchenService>();
        if (frontOfHouse == null) frontOfHouse = FindScene<BistroBuilderAdvancedFrontOfHouseService>();
        if (experience == null) experience = FindScene<BistroBuilderCustomerExperienceTrackingService>();
        if (editController == null) editController = FindScene<RestaurantEditInteractionController>();
    }

    private void BindRuntime()
    {
        if (!Application.isPlaying || subscribed) return;
        if (inventoryPlanning != null)
        {
            inventoryPlanning.AlertActivated += HandleInventoryAlertActivated;
            inventoryPlanning.AlertCleared += HandleInventoryAlertCleared;
        }
        if (kitchen != null) kitchen.LoadStateChanged += HandleKitchenStateChanged;
        if (frontOfHouse != null) frontOfHouse.QueueChanged += HandleQueueChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (inventoryPlanning != null)
        {
            inventoryPlanning.AlertActivated -= HandleInventoryAlertActivated;
            inventoryPlanning.AlertCleared -= HandleInventoryAlertCleared;
        }
        if (kitchen != null) kitchen.LoadStateChanged -= HandleKitchenStateChanged;
        if (frontOfHouse != null) frontOfHouse.QueueChanged -= HandleQueueChanged;
        subscribed = false;
    }

    private void HandleInventoryAlertActivated(BistroBuilderInventoryAlertSnapshot alert)
    {
        AddActivity("Inventario · " + alert.Message);
        RefreshReadModels();
    }

    private void HandleInventoryAlertCleared(BistroBuilderInventoryAlertSnapshot alert)
    {
        AddActivity("Resuelto · " + alert.Message);
        RefreshReadModels();
    }

    private void HandleKitchenStateChanged(BistroBuilderKitchenLoadState state)
    {
        AddActivity("Cocina · " + KitchenLabel(state));
        RefreshReadModels();
    }

    private void HandleQueueChanged()
    {
        int waitingClients = ResolveWaitingClientCount();
        if (waitingClients > 0) AddActivity("Sala · Espera: " + waitingClients + " clientes");
        RefreshReadModels();
    }

    private void AddActivity(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (recentActivity.Count > 0 && string.Equals(recentActivity[0], message, StringComparison.Ordinal)) return;
        recentActivity.Insert(0, message.Trim());
        if (recentActivity.Count > 6) recentActivity.RemoveAt(recentActivity.Count - 1);
    }

    private void RefreshActivityText()
    {
        if (activityText == null) return;
        inventoryAlerts.Clear();
        if (inventoryPlanning != null) inventoryPlanning.CopyActiveAlertsTo(inventoryAlerts);

        System.Text.StringBuilder builder = new System.Text.StringBuilder(256);
        builder.AppendLine("<b>Actividad</b>");
        builder.AppendLine("<color=#BDC6C2>Lo importante del servicio, sin ruido.</color>");
        builder.AppendLine();

        if (recentActivity.Count == 0 && inventoryAlerts.Count == 0)
        {
            builder.AppendLine("<color=#3F9A69>●</color> Sin incidencias prioritarias");
        }
        else
        {
            for (int i = 0; i < recentActivity.Count; i++)
                builder.AppendLine("• " + recentActivity[i]);
            if (recentActivity.Count == 0)
            {
                int shown = Mathf.Min(5, inventoryAlerts.Count);
                for (int i = 0; i < shown; i++) builder.AppendLine("• " + inventoryAlerts[i].Message);
            }
        }
        activityText.text = builder.ToString();
    }

    private int ResolveWaitingClientCount()
    {
        if (frontOfHouse == null) return 0;
        var entries = new List<BistroBuilderFrontOfHouseQueueEntry>(16);
        frontOfHouse.CopyQueueSnapshot(entries);
        int total = 0;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null) total += Mathf.Max(0, entries[i].partySize);
        return total;
    }

    private void ReconcileTimeDock()
    {
        if (canvas == null) return;
        Transform dock = canvas.transform.Find("BB_368B_TimeControlsDock");
        RectTransform rect = dock as RectTransform;
        if (rect == null) return;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-16f, 10f);
        rect.sizeDelta = new Vector2(324f, 46f);
        rect.SetAsLastSibling();
    }

    private static string KitchenLabel(BistroBuilderKitchenLoadState state)
    {
        switch (state)
        {
            case BistroBuilderKitchenLoadState.Loaded: return "Cargada";
            case BistroBuilderKitchenLoadState.Saturated: return "Saturada";
            case BistroBuilderKitchenLoadState.Blocked: return "Bloqueada";
            default: return "Fluida";
        }
    }

    private static Color KitchenColor(BistroBuilderKitchenLoadState state)
    {
        switch (state)
        {
            case BistroBuilderKitchenLoadState.Loaded: return BistroBuilderUiTokens.Attention;
            case BistroBuilderKitchenLoadState.Saturated:
            case BistroBuilderKitchenLoadState.Blocked: return BistroBuilderUiTokens.Critical;
            default: return BistroBuilderUiTokens.Success;
        }
    }

    private static Button EnsureButton(Transform parent, string name, string label, float width)
    {
        Transform found = parent.Find(name);
        GameObject go = found != null ? found.gameObject : NewUi(name, parent);
        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        button.targetGraphic = image;

        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;

        TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
        {
            GameObject textGo = NewUi("Label", go.transform);
            text = textGo.AddComponent<TextMeshProUGUI>();
            Stretch(textGo.GetComponent<RectTransform>());
        }
        text.text = label;
        text.fontSize = 14f;
        text.color = BistroBuilderUiTokens.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return button;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent != null ? parent.gameObject.layer : 5;
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static T FindScene<T>() where T : Component
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Item";
        return value.Replace("ó", "o").Replace("í", "i").Replace("é", "e")
            .Replace("á", "a").Replace("ú", "u").Replace(" ", string.Empty);
    }

    private readonly struct NavSpec
    {
        public NavSpec(string label, params string[] targetNames)
        {
            Label = label;
            TargetNames = targetNames;
        }

        public string Label { get; }
        public string[] TargetNames { get; }
    }
}
