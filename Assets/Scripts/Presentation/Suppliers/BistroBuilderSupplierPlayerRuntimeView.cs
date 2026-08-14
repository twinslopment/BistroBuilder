using System;
using System.Collections.Generic;
using System.Text;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum BistroBuilderSupplierPlayerSection
{
    Suppliers = 0,
    Catalog = 1,
    SmartPurchase = 2,
    Orders = 3
}

/// <summary>
/// UI jugable definitiva de Proveedores 2.3K.
///
/// Es Presentation pura: lee 2.3A/2.3C/2.3D/2.3E/2.3F/2.3G/2.3H/2.3I y
/// canaliza acciones exclusivamente por las APIs públicas de 2.3E/2.3F/2.3I.
/// No modifica inventario, mercado, promociones, logística ni datos de autoría.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Suppliers/Supplier Player Runtime View 2.3K")]
public sealed class BistroBuilderSupplierPlayerRuntimeView : MonoBehaviour
{
    public const string RuntimeRevision = "SUPPLIERS-2.3K-B1-UI-POLISH";

    [Header("Integración de input")]
    [SerializeField] private BistroBuilderProfessionalCameraController cameraController;
    [SerializeField] private RestaurantEditInteractionController editInteractionController;
    [SerializeField] private bool showOpenButton = true;

    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private BistroBuilderIngredientAuthoringDatabase ingredientDatabase;
    private BistroBuilderSupplierMarketService marketService;
    private BistroBuilderSupplierCommercialIntelligenceService commercialService;
    private BistroBuilderSupplierPurchaseOrderService orderService;
    private BistroBuilderSupplierSmartPurchaseService smartPurchaseService;
    private BistroBuilderSupplierProgressionService progressionService;
    private BistroBuilderSupplierLogisticsService logisticsService;
    private BistroBuilderSupplierDeliveryPresentationService deliveryService;
    private BistroBuilderSupplierReceivingBridge23L receivingBridge;

    private readonly List<BistroBuilderPurchaseOrderRecord> orders =
        new List<BistroBuilderPurchaseOrderRecord>(64);
    private readonly List<BistroBuilderSupplierPromotionRecord> activePromotions =
        new List<BistroBuilderSupplierPromotionRecord>(64);

    private Button openButton;
    private RectTransform modalRoot;
    private RectTransform listContent;
    private RectTransform listViewport;
    private RectTransform detailRoot;
    private Text headerSummary;
    private Text detailTitle;
    private Text detailBody;
    private Text statusText;
    private Image logoImage;
    private Button primaryAction;
    private Button secondaryAction;
    private Button tertiaryAction;
    private Button suppliersTab;
    private Button catalogTab;
    private Button smartTab;
    private Button ordersTab;
    private Text quantityText;
    private Button quantityMinus;
    private Button quantityPlus;
    private RectTransform confirmationOverlay;
    private Text confirmationBody;
    private string pendingConfirmationOrderId = string.Empty;

    private bool built;
    private bool cameraWasEnabled;
    private bool editWasEnabled;
    private bool inputGateApplied;
    private float nextRefreshAt;
    private long lastRevisionFingerprint = long.MinValue;
    private int visibleRowCount;

    private BistroBuilderSupplierPlayerSection currentSection =
        BistroBuilderSupplierPlayerSection.Suppliers;
    private string selectedSupplierId = string.Empty;
    private string selectedOfferId = string.Empty;
    private string selectedOrderId = string.Empty;
    private BistroBuilderSmartPurchaseStrategy selectedStrategy =
        BistroBuilderSmartPurchaseStrategy.Equilibrado;
    private BistroBuilderSmartPurchaseReport smartReport;
    private int selectedPackageCount = 1;

    public bool VisualTreeBuilt => built;
    public bool IsOpen => modalRoot != null && modalRoot.gameObject.activeSelf;
    public int VisibleRowCount => visibleRowCount;
    public BistroBuilderSupplierPlayerSection CurrentSection => currentSection;
    public string SelectedSupplierId => selectedSupplierId;
    public string SelectedOrderId => selectedOrderId;

    private void Awake()
    {
        ResolveDependencies();
        EnsureVisualTree();
        SetVisible(false);
    }

    private void Start()
    {
        EnsureVisualTree();
        if (openButton != null)
        {
            openButton.gameObject.SetActive(showOpenButton);
        }
    }

    private void Update()
    {
        if (!IsOpen || Time.unscaledTime < nextRefreshAt)
        {
            return;
        }
        nextRefreshAt = Time.unscaledTime + 0.35f;
        ResolveDependencies();
        long fingerprint = ComputeRevisionFingerprint();
        if (fingerprint != lastRevisionFingerprint)
        {
            Refresh(false);
        }
    }

    private void OnDisable()
    {
        RestoreInputGate();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        LoadStaticDatabases();
        if (supplierDatabase == null)
        {
            error = "Falta supplier.authoring para la UI 2.3K.";
            return false;
        }
        if (ingredientDatabase == null)
        {
            error = "Falta ingredient.authoring para la UI 2.3K.";
            return false;
        }
        return true;
    }

    public bool TryOpenFromInterface(out string error)
    {
        error = string.Empty;
        EnsureVisualTree();
        ResolveDependencies();
        if (!ValidateConfiguration(out error))
        {
            return false;
        }
        if (!RuntimeAuthoritiesReady(out error))
        {
            return false;
        }

        EnsureDefaultSelection();
        SetVisible(true);
        ApplyInputGate();
        Refresh(true);
        return true;
    }

    public void Close()
    {
        CloseOrderConfirmation();
        SetVisible(false);
        RestoreInputGate();
    }

    public bool TrySelectSectionForTests(
        BistroBuilderSupplierPlayerSection section,
        out string error)
    {
        error = string.Empty;
        if (!IsOpen && !TryOpenFromInterface(out error))
        {
            return false;
        }
        SelectSection(section);
        if (visibleRowCount <= 0)
        {
            error = "La sección " + section + " no generó contenido visible.";
            return false;
        }
        return true;
    }

    public bool TryValidateVisibleContent(out string error)
    {
        error = string.Empty;
        bool wasOpen = IsOpen;
        if (!TryOpenFromInterface(out error))
        {
            return false;
        }
        try
        {
            Canvas.ForceUpdateCanvases();
            if (listViewport == null || listViewport.GetComponent<RectMask2D>() == null ||
                listViewport.GetComponent<Mask>() != null)
            {
                error = "El listado 2.3K no usa RectMask2D correctamente.";
                return false;
            }
            if (suppliersTab == null || catalogTab == null || smartTab == null ||
                ordersTab == null || detailBody == null || primaryAction == null ||
                statusText == null || confirmationOverlay == null || confirmationBody == null)
            {
                error = "La UI 2.3K no contiene todos sus controles esenciales.";
                return false;
            }
            if (visibleRowCount <= 0)
            {
                error = "La UI 2.3K no ha construido filas jugables.";
                return false;
            }
            return true;
        }
        finally
        {
            if (!wasOpen)
            {
                Close();
            }
        }
    }

    public bool TryValidateStableInteractionVisuals(out string error)
    {
        error = string.Empty;
        EnsureVisualTree();
        Button[] buttons = GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
        {
            error = "La UI 2.3K no contiene botones para validar.";
            return false;
        }

        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button == null) continue;
            if (button.transition != Selectable.Transition.None)
            {
                error = "El botón " + button.name +
                        " conserva una transición uGUI transitoria.";
                return false;
            }
            if (button.navigation.mode != Navigation.Mode.None)
            {
                error = "El botón " + button.name +
                        " conserva navegación Selected automática.";
                return false;
            }
        }
        return true;
    }

    private void EnsureVisualTree()
    {
        if (built)
        {
            return;
        }
        RectTransform host = transform as RectTransform;
        if (host == null)
        {
            return;
        }
        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.offsetMin = Vector2.zero;
        host.offsetMax = Vector2.zero;
        host.localScale = Vector3.one;

        openButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "OpenSuppliers", host, "PROVEEDORES", HandleOpen,
            new Color(0.18f, 0.21f, 0.25f, 1f), 13);
        SetRect(openButton.GetComponent<RectTransform>(),
            0f, 1f, 0f, 1f, new Vector2(408f, -58f), new Vector2(552f, -18f));

        modalRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "SuppliersModal", host, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BistroBuilderMenuEditorUiFactory.AddImage(modalRoot, BistroBuilderMenuEditorUiFactory.Overlay);

        RectTransform panel = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Panel", modalRoot, Vector2.zero, Vector2.one,
            new Vector2(32f, 24f), new Vector2(-32f, -24f));
        BistroBuilderMenuEditorUiFactory.AddImage(panel, BistroBuilderMenuEditorUiFactory.Surface);

        Text title = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title", panel, "PROVEEDORES", 24, TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary, FontStyle.Bold);
        SetRect(title.rectTransform, 0.02f, 0.93f, 0.55f, 0.99f, 0f);

        headerSummary = BistroBuilderMenuEditorUiFactory.CreateText(
            "HeaderSummary", panel, string.Empty, 12, TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary);
        SetRect(headerSummary.rectTransform, 0.02f, 0.885f, 0.80f, 0.93f, 0f);

        Button closeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close", panel, "CERRAR", Close,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 12);
        SetRect(closeButton.GetComponent<RectTransform>(), 0.87f, 0.935f, 0.98f, 0.985f, 0f);

        BuildTabs(panel);

        RectTransform listRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ListRoot", panel, new Vector2(0.02f, 0.08f), new Vector2(0.48f, 0.80f),
            Vector2.zero, Vector2.zero);
        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "MainScroll", listRoot, out listContent);
        listViewport = scroll.viewport;

        BuildDetail(panel);
        BuildConfirmationOverlay(modalRoot);

        statusText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Status", panel, string.Empty, 12, TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary);
        SetRect(statusText.rectTransform, 0.02f, 0.015f, 0.98f, 0.065f, 0f);

        // B1: ningún Selectable de la UI de Proveedores depende de los estados
        // Highlighted/Pressed/Selected de uGUI. La selección persistente la
        // pinta esta vista de forma determinista tras cada Refresh.
        StabilizeButtonTree(host);

        built = true;
    }

    private void BuildTabs(RectTransform panel)
    {
        suppliersTab = CreateTab(panel, "TabSuppliers", "PROVEEDORES", 0.02f,
            () => SelectSection(BistroBuilderSupplierPlayerSection.Suppliers));
        catalogTab = CreateTab(panel, "TabCatalog", "CATÁLOGO", 0.175f,
            () => SelectSection(BistroBuilderSupplierPlayerSection.Catalog));
        smartTab = CreateTab(panel, "TabSmart", "COMPRA INTELIGENTE", 0.33f,
            () => SelectSection(BistroBuilderSupplierPlayerSection.SmartPurchase), 0.22f);
        ordersTab = CreateTab(panel, "TabOrders", "PEDIDOS", 0.56f,
            () => SelectSection(BistroBuilderSupplierPlayerSection.Orders));
    }

    private Button CreateTab(
        RectTransform panel, string name, string label, float x, Action callback,
        float width = 0.145f)
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            name, panel, label, () => callback(),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 11);
        SetRect(button.GetComponent<RectTransform>(), x, 0.815f, x + width, 0.865f, 0f);
        return button;
    }

    private void BuildDetail(RectTransform panel)
    {
        detailRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "DetailRoot", panel, new Vector2(0.50f, 0.08f), new Vector2(0.98f, 0.80f),
            Vector2.zero, Vector2.zero);
        BistroBuilderMenuEditorUiFactory.AddImage(
            detailRoot, new Color(0.055f, 0.065f, 0.062f, 0.88f));

        logoImage = BistroBuilderMenuEditorUiFactory.AddImage(
            BistroBuilderMenuEditorUiFactory.CreateRect(
                "Logo", detailRoot, new Vector2(0.80f, 0.83f), new Vector2(0.97f, 0.97f),
                Vector2.zero, Vector2.zero),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised);
        logoImage.preserveAspect = true;

        detailTitle = BistroBuilderMenuEditorUiFactory.CreateText(
            "DetailTitle", detailRoot, "", 20, TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary, FontStyle.Bold);
        SetRect(detailTitle.rectTransform, 0.04f, 0.83f, 0.78f, 0.98f, 0f);

        detailBody = BistroBuilderMenuEditorUiFactory.CreateText(
            "DetailBody", detailRoot, "", 13, TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary);
        detailBody.verticalOverflow = VerticalWrapMode.Truncate;
        detailBody.resizeTextForBestFit = true;
        detailBody.resizeTextMinSize = 10;
        detailBody.resizeTextMaxSize = 13;
        detailBody.lineSpacing = 0.94f;
        SetRect(detailBody.rectTransform, 0.04f, 0.24f, 0.96f, 0.84f, 0f);

        quantityMinus = BistroBuilderMenuEditorUiFactory.CreateButton(
            "QuantityMinus", detailRoot, "−", DecreaseQuantity,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 18);
        SetRect(quantityMinus.GetComponent<RectTransform>(), 0.04f, 0.17f, 0.12f, 0.23f, 0f);
        quantityText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Quantity", detailRoot, "1 paquete", 13, TextAnchor.MiddleCenter,
            BistroBuilderMenuEditorUiFactory.TextPrimary, FontStyle.Bold);
        SetRect(quantityText.rectTransform, 0.13f, 0.17f, 0.30f, 0.23f, 0f);
        quantityPlus = BistroBuilderMenuEditorUiFactory.CreateButton(
            "QuantityPlus", detailRoot, "+", IncreaseQuantity,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 18);
        SetRect(quantityPlus.GetComponent<RectTransform>(), 0.31f, 0.17f, 0.39f, 0.23f, 0f);

        primaryAction = BistroBuilderMenuEditorUiFactory.CreateButton(
            "PrimaryAction", detailRoot, "ACCIÓN", HandlePrimaryAction,
            new Color(0.22f, 0.38f, 0.29f, 1f), 12);
        SetRect(primaryAction.GetComponent<RectTransform>(), 0.42f, 0.15f, 0.96f, 0.23f, 0f);

        secondaryAction = BistroBuilderMenuEditorUiFactory.CreateButton(
            "SecondaryAction", detailRoot, "SECUNDARIA", HandleSecondaryAction,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 11);
        SetRect(secondaryAction.GetComponent<RectTransform>(), 0.04f, 0.05f, 0.49f, 0.13f, 0f);

        tertiaryAction = BistroBuilderMenuEditorUiFactory.CreateButton(
            "TertiaryAction", detailRoot, "TERCIARIA", HandleTertiaryAction,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 11);
        SetRect(tertiaryAction.GetComponent<RectTransform>(), 0.51f, 0.05f, 0.96f, 0.13f, 0f);
    }

    private void BuildConfirmationOverlay(RectTransform parent)
    {
        confirmationOverlay = BistroBuilderMenuEditorUiFactory.CreateRect(
            "OrderConfirmationOverlay", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BistroBuilderMenuEditorUiFactory.AddImage(
            confirmationOverlay, new Color(0.01f, 0.015f, 0.012f, 0.88f));

        RectTransform card = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ConfirmationCard", confirmationOverlay,
            new Vector2(0.28f, 0.24f), new Vector2(0.72f, 0.76f), Vector2.zero, Vector2.zero);
        BistroBuilderMenuEditorUiFactory.AddImage(card, BistroBuilderMenuEditorUiFactory.Surface);

        Text title = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title", card, "CONFIRMAR PEDIDO", 21, TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary, FontStyle.Bold);
        SetRect(title.rectTransform, 0.06f, 0.83f, 0.94f, 0.96f, 0f);

        confirmationBody = BistroBuilderMenuEditorUiFactory.CreateText(
            "Body", card, string.Empty, 14, TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary);
        confirmationBody.resizeTextForBestFit = true;
        confirmationBody.resizeTextMinSize = 11;
        confirmationBody.resizeTextMaxSize = 14;
        confirmationBody.lineSpacing = 1.0f;
        SetRect(confirmationBody.rectTransform, 0.06f, 0.23f, 0.94f, 0.82f, 0f);

        Button cancel = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Cancel", card, "VOLVER", CloseOrderConfirmation,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 12);
        SetRect(cancel.GetComponent<RectTransform>(), 0.06f, 0.06f, 0.43f, 0.18f, 0f);

        Button confirm = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Confirm", card, "CONFIRMAR PEDIDO", ConfirmPendingOrder,
            new Color(0.22f, 0.42f, 0.29f, 1f), 12);
        SetRect(confirm.GetComponent<RectTransform>(), 0.48f, 0.06f, 0.94f, 0.18f, 0f);

        confirmationOverlay.gameObject.SetActive(false);
    }

    private void OpenOrderConfirmation(
        BistroBuilderPurchaseOrderRecord order,
        BistroBuilderPurchaseOrderConfirmationPreview preview)
    {
        if (order == null || preview == null || confirmationOverlay == null)
        {
            SetStatus("No se pudo abrir la confirmación del pedido.", true);
            return;
        }

        BistroBuilderSupplierAuthoringRecord supplier;
        supplierDatabase.TryGetSupplier(order.supplierId, out supplier);
        // Inicialización explícita: la llamada está detrás de un cortocircuito
        // (supplier != null && ...). Sin valores iniciales, C# no puede garantizar
        // la asignación de los out cuando supplier es null.
        int estimatedDay = 0;
        int windowStart = 0;
        int windowEnd = 0;
        bool hasEstimate = supplier != null && TryEstimateDeliveryWindow(
            supplier, preview.quotedLeadTimeGameHours, orderService.CurrentGameDay,
            out estimatedDay, out windowStart, out windowEnd);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(order.displayCode + " · " + preview.supplierDisplayName);
        sb.AppendLine();
        sb.AppendLine("Líneas: " + preview.lineCount);
        sb.AppendLine("Producto: " + BistroBuilderSupplierPlayerUiFormat.Money(preview.subtotalCents));
        sb.AppendLine("Portes: " + BistroBuilderSupplierPlayerUiFormat.Money(preview.shippingCostCents));
        sb.AppendLine("TOTAL: " + BistroBuilderSupplierPlayerUiFormat.Money(preview.totalCents));
        sb.AppendLine();
        if (hasEstimate)
        {
            sb.AppendLine("Entrega estimada: " +
                BistroBuilderSupplierPlayerUiFormat.DayWindow(estimatedDay, windowStart, windowEnd));
        }
        else
        {
            sb.AppendLine("Plazo estimado: " + BistroBuilderSupplierPlayerUiFormat.Hours(preview.quotedLeadTimeGameHours));
        }
        sb.AppendLine("Fiabilidad: " + (supplier != null
            ? BistroBuilderSupplierPlayerUiFormat.Reliability(supplier.reliabilityTier)
            : "—"));
        sb.AppendLine();
        sb.AppendLine("Al confirmar se congelan precios, promociones, portes, condiciones y plazo. " +
                      "2.3G fijará el LogisticsPlan definitivo inmediatamente después.");

        confirmationBody.text = sb.ToString();
        pendingConfirmationOrderId = order.purchaseOrderId;
        confirmationOverlay.SetAsLastSibling();
        confirmationOverlay.gameObject.SetActive(true);
    }

    private void CloseOrderConfirmation()
    {
        pendingConfirmationOrderId = string.Empty;
        if (confirmationOverlay != null) confirmationOverlay.gameObject.SetActive(false);
    }

    private void ConfirmPendingOrder()
    {
        string orderId = pendingConfirmationOrderId;
        if (string.IsNullOrWhiteSpace(orderId))
        {
            CloseOrderConfirmation();
            return;
        }

        // Revalida la cotización justo al pulsar confirmar; el preview mostrado
        // puede haber envejecido por una revisión de mercado/promoción.
        if (!orderService.TryBuildConfirmationPreview(
                orderId, out BistroBuilderPurchaseOrderConfirmationPreview preview, out string previewError) ||
            preview == null || !preview.canConfirm)
        {
            CloseOrderConfirmation();
            SetStatus(previewError ?? "La cotización ya no es confirmable; revisa el borrador.", true);
            Refresh(true);
            return;
        }

        if (!orderService.TryConfirmOrder(
                orderId, out BistroBuilderPurchaseOrderConfirmationReceipt receipt, out string error))
        {
            CloseOrderConfirmation();
            SetStatus(error, true);
            Refresh(true);
            return;
        }

        CloseOrderConfirmation();
        SetStatus("Pedido " + receipt.displayCode +
                  " confirmado. Precio y condiciones congelados.", false);
        Refresh(true);
    }

    private static bool TryEstimateDeliveryWindow(
        BistroBuilderSupplierAuthoringRecord supplier,
        float leadTimeGameHours,
        int currentGameDay,
        out int deliveryDay,
        out int windowStart,
        out int windowEnd)
    {
        deliveryDay = 0;
        windowStart = 0;
        windowEnd = 0;
        if (supplier == null) return false;

        BistroBuilderSupplierLogisticsPlanningSettings settings =
            Resources.Load<BistroBuilderSupplierLogisticsPlanningSettings>(
                BistroBuilderSupplierLogisticsService.SettingsResourcePath);
        int firstWeekday = settings != null ? settings.FirstGameDayWeekday : 0;
        int searchDays = settings != null ? settings.MaximumWindowSearchDays : 21;
        int fallbackStart = settings != null ? settings.FallbackWindowStartMinuteOfDay : 8 * 60;
        int fallbackEnd = settings != null ? settings.FallbackWindowEndMinuteOfDay : 12 * 60;

        long leadMinutes = (long)Math.Ceiling(Math.Max(0.1f, leadTimeGameHours) * 60d);
        long etaAbsolute = (long)(Math.Max(1, currentGameDay) - 1) * 1440L + leadMinutes;
        int etaDay = (int)(etaAbsolute / 1440L) + 1;
        int etaMinute = (int)(etaAbsolute % 1440L);
        bool hasWindows = supplier.deliveryWindows != null && supplier.deliveryWindows.Count > 0;

        for (int offset = 0; offset <= searchDays; offset++)
        {
            int day = etaDay + offset;
            if (!hasWindows)
            {
                if (offset == 0 && etaMinute >= fallbackEnd) continue;
                int start = offset == 0 ? Math.Max(fallbackStart, etaMinute) : fallbackStart;
                if (start < fallbackEnd)
                {
                    deliveryDay = day; windowStart = start; windowEnd = fallbackEnd;
                    return true;
                }
                continue;
            }

            for (int i = 0; i < supplier.deliveryWindows.Count; i++)
            {
                BistroBuilderSupplierDeliveryWindowAuthoring window = supplier.deliveryWindows[i];
                if (window == null || !IsDeliveryWindowEnabled(window, day, firstWeekday)) continue;
                int start = Mathf.Clamp(window.startMinuteOfDay, 0, 1439);
                int end = Mathf.Clamp(window.endMinuteOfDay, start + 1, 1440);
                if (offset == 0 && etaMinute >= end) continue;
                int candidateStart = offset == 0 ? Math.Max(start, etaMinute) : start;
                if (candidateStart >= end) continue;
                deliveryDay = day; windowStart = candidateStart; windowEnd = end;
                return true;
            }
        }
        return false;
    }

    private static bool IsDeliveryWindowEnabled(
        BistroBuilderSupplierDeliveryWindowAuthoring window,
        int gameDay,
        int firstWeekday)
    {
        int weekday = ((Math.Max(1, gameDay) - 1 + firstWeekday) % 7 + 7) % 7;
        switch (weekday)
        {
            case 0: return window.monday;
            case 1: return window.tuesday;
            case 2: return window.wednesday;
            case 3: return window.thursday;
            case 4: return window.friday;
            case 5: return window.saturday;
            default: return window.sunday;
        }
    }

    private void HandleOpen()
    {
        if (!TryOpenFromInterface(out string error))
        {
            SetStatus(error, true);
        }
    }

    private void SelectSection(BistroBuilderSupplierPlayerSection section)
    {
        currentSection = section;
        if (section == BistroBuilderSupplierPlayerSection.SmartPurchase && smartReport == null)
        {
            RefreshSmartReport();
        }
        Refresh(true);
    }

    private void Refresh(bool force)
    {
        if (!IsOpen)
        {
            return;
        }
        ResolveDependencies();
        if (!RuntimeAuthoritiesReady(out string error))
        {
            SetStatus(error, true);
            return;
        }
        EnsureDefaultSelection();
        long revision = ComputeRevisionFingerprint();
        if (!force && revision == lastRevisionFingerprint)
        {
            return;
        }
        lastRevisionFingerprint = revision;
        RefreshHeader();
        RefreshTabs();
        RebuildList();
        RefreshDetail();
    }

    private void RefreshHeader()
    {
        List<BistroBuilderSupplierAccessEvaluation> access =
            new List<BistroBuilderSupplierAccessEvaluation>();
        progressionService.CopySupplierAccess(access, true);
        int unlocked = 0;
        for (int i = 0; i < access.Count; i++)
        {
            if (access[i] != null && access[i].isUnlocked) unlocked++;
        }
        commercialService.CopyActivePromotions(activePromotions);
        orderService.CopyOrders(orders);
        int openOrders = 0;
        for (int i = 0; i < orders.Count; i++)
        {
            if (orders[i] != null && !orders[i].IsTerminal) openOrders++;
        }
        headerSummary.text = unlocked + "/" + access.Count + " proveedores disponibles  ·  " +
                             activePromotions.Count + " promociones activas  ·  " +
                             openOrders + " pedidos abiertos";
    }

    private void RefreshTabs()
    {
        SetTabColor(suppliersTab, currentSection == BistroBuilderSupplierPlayerSection.Suppliers);
        SetTabColor(catalogTab, currentSection == BistroBuilderSupplierPlayerSection.Catalog);
        SetTabColor(smartTab, currentSection == BistroBuilderSupplierPlayerSection.SmartPurchase);
        SetTabColor(ordersTab, currentSection == BistroBuilderSupplierPlayerSection.Orders);
    }

    private void RebuildList()
    {
        ClearChildren(listContent);
        visibleRowCount = 0;
        switch (currentSection)
        {
            case BistroBuilderSupplierPlayerSection.Suppliers:
                BuildSupplierRows();
                break;
            case BistroBuilderSupplierPlayerSection.Catalog:
                BuildCatalogRows();
                break;
            case BistroBuilderSupplierPlayerSection.SmartPurchase:
                BuildSmartRows();
                break;
            case BistroBuilderSupplierPlayerSection.Orders:
                BuildOrderRows();
                break;
        }
    }

    private void BuildSupplierRows()
    {
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = supplierDatabase.Suppliers;
        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier == null || !supplier.isActive) continue;
            progressionService.TryGetSupplierAccess(supplier.SupplierId, out BistroBuilderSupplierAccessEvaluation access);
            bool unlocked = access != null && access.isUnlocked;
            int promos = CountPromotionsForSupplier(supplier.SupplierId);
            string stateLabel = unlocked ? "Disponible" : "Bloqueado";
            string secondary = unlocked
                ? BistroBuilderSupplierPlayerUiFormat.Reliability(supplier.reliabilityTier) +
                  " · " + BistroBuilderSupplierPlayerUiFormat.Hours(supplier.defaultLeadTimeGameHours) +
                  (promos > 0 ? " · " + promos + " promoción(es)" : string.Empty)
                : BuildUnlockProgressRow(access) + " · Fiabilidad " +
                  BistroBuilderSupplierPlayerUiFormat.Reliability(supplier.reliabilityTier);
            string label = stateLabel + " · " + supplier.displayName + "\n" + secondary;
            string id = supplier.SupplierId;
            Color baseColor = BuildSupplierRowColor(supplier, unlocked);
            Button row = CreateListButton("Supplier_" + id, label, () =>
            {
                selectedSupplierId = id;
                selectedOfferId = string.Empty;
                ClearEventSystemSelection();
                Refresh(true);
            }, baseColor);
            ApplyPersistentRowSelection(
                row,
                baseColor,
                string.Equals(id, selectedSupplierId, StringComparison.Ordinal));
        }
    }

    private void BuildCatalogRows()
    {
        BistroBuilderSupplierAuthoringRecord supplier;
        if (!TryGetSelectedSupplier(out supplier))
        {
            return;
        }
        bool unlocked = progressionService.IsSupplierUnlocked(supplier.SupplierId);
        if (!unlocked)
        {
            CreateInfoRow("CatalogLocked", "Proveedor bloqueado\n" + GetUnlockSummary(supplier.SupplierId));
            return;
        }

        if (supplier.baseOffers == null) return;
        for (int index = 0; index < supplier.baseOffers.Count; index++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[index];
            if (offer == null || !offer.isActive) continue;
            BistroBuilderSupplierCommercialQuote quote;
            if (!commercialService.TryGetCommercialQuote(offer.SupplierOfferId, out quote) || quote == null)
                continue;
            BistroBuilderIngredientAuthoringRecord ingredient;
            BistroBuilderCommercialPackageAuthoringRecord package;
            if (!TryResolveOfferVisual(offer, out ingredient, out package)) continue;
            string price = BistroBuilderSupplierPlayerUiFormat.Money(quote.effectivePriceCents);
            string normalized = BistroBuilderSupplierPlayerUiFormat.NormalizedPrice(
                quote.effectivePriceCents, package.netQuantityMicrounits, ingredient.canonicalUnitSnapshot);
            string promo = quote.hasActivePromotion ? " · −" + (quote.discountBasisPoints / 100f).ToString("0.#") + "%" : string.Empty;
            string label = ingredient.displayNameSnapshot + " · " + package.displayName + "\n" +
                           price + promo + " · " + normalized + " · " +
                           BistroBuilderSupplierPlayerUiFormat.Availability(quote.availability);
            string id = offer.SupplierOfferId;
            Color baseColor = CatalogRowColor(quote.availability, quote.hasActivePromotion);
            Button row = CreateListButton("Offer_" + id, label, () =>
            {
                selectedOfferId = id;
                selectedPackageCount = Math.Max(1, offer.minimumPackageCount);
                ClearEventSystemSelection();
                Refresh(true);
            }, baseColor);
            ApplyPersistentRowSelection(
                row,
                baseColor,
                string.Equals(id, selectedOfferId, StringComparison.Ordinal));
        }
    }

    private void BuildSmartRows()
    {
        if (smartReport == null)
        {
            CreateInfoRow("SmartEmpty", "Pulsa ACTUALIZAR ANÁLISIS para generar las tres estrategias.");
            return;
        }
        for (int index = 0; index < smartReport.plans.Count; index++)
        {
            BistroBuilderSmartPurchasePlan plan = smartReport.plans[index];
            if (plan == null) continue;
            string label = BistroBuilderSupplierPlayerUiFormat.Strategy(plan.strategy) +
                           (plan.strategy == smartReport.recommendedStrategy ? " · RECOMENDADA" : string.Empty) + "\n" +
                           BistroBuilderSupplierPlayerUiFormat.Money(plan.totalCents) + " · " +
                           plan.ingredientsRecommended + " ingredientes · " + plan.supplierCount + " proveedor(es)";
            BistroBuilderSmartPurchaseStrategy strategy = plan.strategy;
            Color baseColor = plan.strategy == smartReport.recommendedStrategy
                ? new Color(0.19f, 0.34f, 0.24f, 1f)
                : BistroBuilderMenuEditorUiFactory.SurfaceRaised;
            Button row = CreateListButton("Plan_" + strategy, label, () =>
            {
                selectedStrategy = strategy;
                ClearEventSystemSelection();
                Refresh(true);
            }, baseColor);
            ApplyPersistentRowSelection(row, baseColor, strategy == selectedStrategy);
        }
    }

    private void BuildOrderRows()
    {
        orderService.CopyOrders(orders);
        orders.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int c = b.lastModifiedGameDay.CompareTo(a.lastModifiedGameDay);
            if (c != 0) return c;
            return string.Compare(b.purchaseOrderId, a.purchaseOrderId, StringComparison.Ordinal);
        });

        for (int index = 0; index < orders.Count; index++)
        {
            BistroBuilderPurchaseOrderRecord order = orders[index];
            if (order == null) continue;
            string supplierName = ResolveSupplierName(order.supplierId);
            string label = order.displayCode + " · " + supplierName + "\n" +
                           BistroBuilderSupplierPlayerUiFormat.OrderStatus(order.status) +
                           " · " + BistroBuilderSupplierPlayerUiFormat.Money(order.totalCents);
            string id = order.purchaseOrderId;
            Color baseColor = OrderRowColor(order.status);
            Button row = CreateListButton("Order_" + id, label, () =>
            {
                selectedOrderId = id;
                ClearEventSystemSelection();
                Refresh(true);
            }, baseColor);
            ApplyPersistentRowSelection(
                row,
                baseColor,
                string.Equals(id, selectedOrderId, StringComparison.Ordinal));
        }

        if (visibleRowCount == 0)
        {
            CreateInfoRow("NoOrders", "Todavía no hay pedidos. Puedes crear uno desde Catálogo o Compra Inteligente.");
        }
    }

    private void RefreshDetail()
    {
        ResetDetailActions();
        switch (currentSection)
        {
            case BistroBuilderSupplierPlayerSection.Suppliers:
                RefreshSupplierDetail();
                break;
            case BistroBuilderSupplierPlayerSection.Catalog:
                RefreshCatalogDetail();
                break;
            case BistroBuilderSupplierPlayerSection.SmartPurchase:
                RefreshSmartDetail();
                break;
            case BistroBuilderSupplierPlayerSection.Orders:
                RefreshOrderDetail();
                break;
        }
    }

    private void RefreshSupplierDetail()
    {
        BistroBuilderSupplierAuthoringRecord supplier;
        if (!TryGetSelectedSupplier(out supplier))
        {
            detailTitle.text = "Selecciona un proveedor";
            detailBody.text = string.Empty;
            return;
        }
        ApplyLogo(supplier);
        progressionService.TryGetSupplierAccess(supplier.SupplierId, out BistroBuilderSupplierAccessEvaluation access);
        bool unlocked = access != null && access.isUnlocked;
        int promos = CountPromotionsForSupplier(supplier.SupplierId);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>ESTADO</b>");
        sb.AppendLine(unlocked ? "Disponible" : "Bloqueado");
        if (!string.IsNullOrWhiteSpace(supplier.description))
        {
            sb.AppendLine();
            sb.AppendLine(supplier.description);
        }
        sb.AppendLine();
        sb.AppendLine("<b>CONDICIONES COMERCIALES</b>");
        sb.AppendLine("Fiabilidad: " + BistroBuilderSupplierPlayerUiFormat.Reliability(supplier.reliabilityTier) +
                      " (" + BistroBuilderSupplierPlayerUiFormat.Percent01(supplier.reliabilityValue) + ")");
        sb.AppendLine("Plazo habitual: " + BistroBuilderSupplierPlayerUiFormat.Hours(supplier.defaultLeadTimeGameHours));
        sb.AppendLine("Pedido mínimo: " + BistroBuilderSupplierPlayerUiFormat.Money(supplier.minimumOrderValueCents));
        sb.AppendLine("Portes: " + BistroBuilderSupplierPlayerUiFormat.Money(supplier.shippingCostCents));
        if (supplier.freeShippingEnabled)
            sb.AppendLine("Portes gratis desde: " + BistroBuilderSupplierPlayerUiFormat.Money(supplier.freeShippingThresholdCents));
        sb.AppendLine("Promociones activas: " + promos);
        sb.AppendLine();
        sb.AppendLine("<b>PERFIL COMERCIAL</b>");
        sb.AppendLine("Catálogo: " + BistroBuilderSupplierPlayerUiFormat.HumanizeFlagsText(supplier.catalogFlags.ToString()));
        sb.AppendLine("Modelo: " + BistroBuilderSupplierPlayerUiFormat.HumanizeFlagsText(supplier.commercialModelFlags.ToString()));
        if (!unlocked && access != null)
        {
            sb.AppendLine();
            sb.AppendLine("<b>DESBLOQUEO</b>");
            sb.AppendLine(access.summary);
            for (int i = 0; i < access.conditions.Count; i++)
            {
                BistroBuilderSupplierUnlockConditionResult c = access.conditions[i];
                if (c != null) sb.AppendLine("• " + c.reasonText);
            }
        }
        detailTitle.text = supplier.displayName;
        detailBody.text = sb.ToString();
        SetButton(primaryAction, unlocked ? "VER CATÁLOGO" : "PROGRESO DE DESBLOQUEO", true);
        SetButton(secondaryAction, "COMPRA INTELIGENTE", unlocked);
        SetButton(tertiaryAction, "VER PEDIDOS", true);
    }

    private void RefreshCatalogDetail()
    {
        BistroBuilderSupplierAuthoringRecord supplier;
        if (!TryGetSelectedSupplier(out supplier))
        {
            detailTitle.text = "Selecciona proveedor";
            return;
        }
        ApplyLogo(supplier);
        if (!progressionService.IsSupplierUnlocked(supplier.SupplierId))
        {
            detailTitle.text = supplier.displayName + " · Bloqueado";
            detailBody.text = GetUnlockSummary(supplier.SupplierId);
            return;
        }

        BistroBuilderSupplierBaseOfferAuthoringRecord offer;
        if (!TryGetSelectedOffer(supplier, out offer))
        {
            detailTitle.text = supplier.displayName + " · Catálogo";
            detailBody.text = "Selecciona un producto para comparar precio, formato, disponibilidad y promoción.";
            SetButton(secondaryAction, "VOLVER A PROVEEDORES", true);
            return;
        }
        BistroBuilderIngredientAuthoringRecord ingredient;
        BistroBuilderCommercialPackageAuthoringRecord package;
        BistroBuilderSupplierCommercialQuote quote;
        if (!TryResolveOfferVisual(offer, out ingredient, out package) ||
            !commercialService.TryGetCommercialQuote(offer.SupplierOfferId, out quote) || quote == null)
        {
            detailTitle.text = "Oferta no disponible";
            detailBody.text = "No se pudo reconstruir la cotización comercial actual.";
            return;
        }

        // En catálogo la imagen del ingrediente tiene prioridad sobre el logo del proveedor.
        // Si todavía no existe imagen de ingrediente, se conserva el branding del proveedor.
        if (logoImage != null && ingredient.displayImage != null)
        {
            logoImage.sprite = ingredient.displayImage;
            logoImage.color = Color.white;
        }

        long lineSubtotal;
        try { lineSubtotal = checked(quote.effectivePriceCents * selectedPackageCount); }
        catch (OverflowException) { lineSubtotal = long.MaxValue; }
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ingredient.displayNameSnapshot + " · " + package.displayName);
        sb.AppendLine();
        sb.AppendLine("<b>PRECIO ACTUAL</b>");
        sb.AppendLine("Mercado: " + BistroBuilderSupplierPlayerUiFormat.Money(quote.marketPriceCents));
        sb.AppendLine("Efectivo: " + BistroBuilderSupplierPlayerUiFormat.Money(quote.effectivePriceCents));
        sb.AppendLine("Normalizado: " + BistroBuilderSupplierPlayerUiFormat.NormalizedPrice(
            quote.effectivePriceCents, package.netQuantityMicrounits, ingredient.canonicalUnitSnapshot));
        if (quote.hasActivePromotion)
        {
            sb.AppendLine("PROMOCIÓN: −" + (quote.discountBasisPoints / 100f).ToString("0.#") + "% · hasta día " +
                          (quote.promotionEndGameDayExclusive - 1));
            if (!string.IsNullOrWhiteSpace(quote.reasonText)) sb.AppendLine(quote.reasonText);
        }
        sb.AppendLine();
        sb.AppendLine("<b>CONDICIONES DE COMPRA</b>");
        sb.AppendLine("Disponibilidad: " + BistroBuilderSupplierPlayerUiFormat.Availability(quote.availability));
        sb.AppendLine("Formato: " + BistroBuilderSupplierPlayerUiFormat.HumanizeIdentifier(package.packageType.ToString()) +
                      " · " + package.NetQuantityInBaseUnits.ToString("0.###") + " " + ingredient.canonicalUnitSnapshot);
        sb.AppendLine("Mínimo de formato: " + Math.Max(1, offer.minimumPackageCount) + " · incremento " + Math.Max(1, offer.orderIncrement));
        sb.AppendLine("Plazo: " + BistroBuilderSupplierPlayerUiFormat.Hours(
            offer.overrideLeadTime ? offer.leadTimeOverrideGameHours : supplier.defaultLeadTimeGameHours));
        sb.AppendLine();
        sb.AppendLine("<b>SELECCIÓN</b>");
        sb.AppendLine(selectedPackageCount + " paquete(s) · " +
                      BistroBuilderSupplierPlayerUiFormat.Money(lineSubtotal));
        sb.AppendLine("El pedido mínimo y los portes se validarán sobre la cesta completa al confirmar.");
        detailTitle.text = supplier.displayName;
        detailBody.text = sb.ToString();

        quantityMinus.gameObject.SetActive(true);
        quantityPlus.gameObject.SetActive(true);
        quantityText.gameObject.SetActive(true);
        quantityText.text = selectedPackageCount + " paquete(s)";
        SetButton(primaryAction, "AÑADIR / ACTUALIZAR BORRADOR", quote.availableForNewOrders);
        SetButton(secondaryAction, "VER PEDIDOS", true);
        SetButton(tertiaryAction, "COMPRA INTELIGENTE", true);
    }

    private void RefreshSmartDetail()
    {
        ApplyLogo(null);
        if (smartReport == null)
        {
            detailTitle.text = "Compra Inteligente";
            detailBody.text = "Analiza stock real, previsión, pedidos en camino, ofertas, portes, mínimos, plazos y fiabilidad.\n\nPulsa ACTUALIZAR ANÁLISIS.";
            SetButton(primaryAction, "ACTUALIZAR ANÁLISIS", true);
            return;
        }
        BistroBuilderSmartPurchasePlan plan = FindPlan(selectedStrategy);
        if (plan == null && smartReport.plans.Count > 0)
        {
            plan = smartReport.plans[0];
            selectedStrategy = plan.strategy;
        }
        if (plan == null)
        {
            detailTitle.text = "Sin plan";
            detailBody.text = smartReport.recommendedReason;
            SetButton(primaryAction, "ACTUALIZAR ANÁLISIS", true);
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(plan.strategy == smartReport.recommendedStrategy
            ? "<b>ESTRATEGIA RECOMENDADA</b>"
            : "<b>ESTRATEGIA ALTERNATIVA</b>");
        sb.AppendLine(smartReport.recommendedReason);
        sb.AppendLine();
        sb.AppendLine("Total estimado: " + BistroBuilderSupplierPlayerUiFormat.Money(plan.totalCents) +
                      " (producto " + BistroBuilderSupplierPlayerUiFormat.Money(plan.subtotalCents) +
                      " + portes " + BistroBuilderSupplierPlayerUiFormat.Money(plan.shippingCents) + ")");
        sb.AppendLine("Ingredientes: " + plan.ingredientsRecommended + " · proveedores: " + plan.supplierCount);
        sb.AppendLine("Riesgo crítico/alto: " + plan.criticalIngredients + "/" + plan.highRiskIngredients);
        sb.AppendLine("Cestas confirmables: " + (!plan.containsMinimumOrderGap ? "Sí" : "NO"));
        sb.AppendLine();
        sb.AppendLine("<b>RECOMENDACIONES PRINCIPALES</b>");

        int shown = 0;
        for (int i = 0; i < plan.ingredients.Count && shown < 7; i++)
        {
            BistroBuilderSmartPurchaseIngredientRecommendation rec = plan.ingredients[i];
            if (rec == null || rec.selected == null || rec.selected.packageCount <= 0) continue;
            BistroBuilderSmartPurchaseCandidate c = rec.selected;
            sb.AppendLine("• " + rec.ingredientDisplayName + " → " + c.supplierDisplayName +
                          " · " + c.packageCount + " × " + c.packageDisplayName +
                          " · " + BistroBuilderSupplierPlayerUiFormat.Money(c.lineSubtotalCents) +
                          " · " + BistroBuilderSupplierPlayerUiFormat.Hours(c.leadTimeGameHours) +
                          " · riesgo " + BistroBuilderSupplierPlayerUiFormat.Risk(c.stockoutRisk));
            if (c.reasons != null && c.reasons.Count > 0)
                sb.AppendLine("  " + c.reasons[0]);
            shown++;
        }
        if (plan.summaryReasons != null)
        {
            for (int i = 0; i < plan.summaryReasons.Count && i < 3; i++)
                sb.AppendLine("• " + plan.summaryReasons[i]);
        }

        detailTitle.text = "Compra Inteligente · " + BistroBuilderSupplierPlayerUiFormat.Strategy(plan.strategy);
        detailBody.text = sb.ToString();
        SetButton(primaryAction, "CREAR BORRADOR(ES) DE ESTE PLAN", plan.ingredientsRecommended > 0 && !plan.containsMinimumOrderGap);
        SetButton(secondaryAction, "ACTUALIZAR ANÁLISIS", true);
        SetButton(tertiaryAction, "VER PEDIDOS", true);
    }

    private void RefreshOrderDetail()
    {
        ApplyLogo(null);
        BistroBuilderPurchaseOrderRecord order;
        if (!TryGetSelectedOrder(out order))
        {
            detailTitle.text = "Pedidos";
            detailBody.text = "Selecciona un pedido. Los borradores pueden editarse/confirmarse; los pedidos confirmados conservan precios y condiciones congeladas.";
            SetButton(secondaryAction, "COMPRA INTELIGENTE", true);
            return;
        }
        BistroBuilderSupplierAuthoringRecord supplier;
        if (supplierDatabase.TryGetSupplier(order.supplierId, out supplier)) ApplyLogo(supplier);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>ESTADO</b>");
        sb.AppendLine(BistroBuilderSupplierPlayerUiFormat.OrderStatus(order.status));
        sb.AppendLine("Proveedor: " + ResolveSupplierName(order.supplierId));
        if (order.status == BistroBuilderPurchaseOrderStatus.Draft)
        {
            sb.AppendLine("Importes todavía no congelados; se recalculan con la cotización vigente.");
        }
        else
        {
            sb.AppendLine("Total congelado: " + BistroBuilderSupplierPlayerUiFormat.Money(order.totalCents) +
                          " · producto " + BistroBuilderSupplierPlayerUiFormat.Money(order.subtotalCents) +
                          " · portes " + BistroBuilderSupplierPlayerUiFormat.Money(order.shippingCostCents));
        }
        sb.AppendLine("Creado día " + order.createdGameDay +
                      (order.confirmedGameDay > 0 ? " · confirmado día " + order.confirmedGameDay : string.Empty));

        if (order.status == BistroBuilderPurchaseOrderStatus.Draft)
        {
            BistroBuilderPurchaseOrderConfirmationPreview preview;
            string previewError;
            if (orderService.TryBuildConfirmationPreview(order.purchaseOrderId, out preview, out previewError) && preview != null)
            {
                sb.AppendLine();
                sb.AppendLine("<b>PREVISUALIZACIÓN EN TIEMPO REAL</b>");
                sb.AppendLine("Líneas: " + preview.lineCount + " · total " + BistroBuilderSupplierPlayerUiFormat.Money(preview.totalCents));
                sb.AppendLine("Mínimo: " + BistroBuilderSupplierPlayerUiFormat.Money(preview.minimumOrderValueCents) +
                              " · " + (preview.minimumOrderSatisfied ? "cumplido" : "NO cumplido"));
                sb.AppendLine("Confirmable: " + (preview.canConfirm ? "Sí" : "No"));
                for (int i = 0; i < preview.blockers.Count; i++) sb.AppendLine("• " + preview.blockers[i]);
                SetButton(primaryAction, "REVISAR Y CONFIRMAR", preview.canConfirm);
            }
            else
            {
                sb.AppendLine("No se pudo construir preview: " + previewError);
                SetButton(primaryAction, "CONFIRMAR PEDIDO", false);
            }
            SetButton(secondaryAction, "CANCELAR BORRADOR", true);
            SetButton(tertiaryAction, "VOLVER AL CATÁLOGO", true);
        }
        else
        {
            if (order.confirmedLines != null)
            {
                sb.AppendLine();
                sb.AppendLine("<b>CONDICIONES CONGELADAS</b>");
                for (int i = 0; i < order.confirmedLines.Count && i < 7; i++)
                {
                    BistroBuilderPurchaseOrderConfirmedLineSnapshot line = order.confirmedLines[i];
                    if (line == null) continue;
                    sb.AppendLine("• " + line.ingredientDisplayName + " · " + line.packageCount + " × " +
                                  line.packageDisplayName + " · " + BistroBuilderSupplierPlayerUiFormat.Money(line.lineSubtotalCents) +
                                  (line.hadActivePromotion ? " · promo congelada" : string.Empty));
                }
            }
            BistroBuilderSupplierLogisticsPlanRecord plan;
            if (logisticsService.TryGetPlanByOrder(order.purchaseOrderId, out plan) && plan != null)
            {
                sb.AppendLine();
                sb.AppendLine("<b>LOGÍSTICA</b>");
                sb.AppendLine(BistroBuilderSupplierPlayerUiFormat.LogisticsStatus(plan.status) + " · " +
                              BistroBuilderSupplierPlayerUiFormat.DayWindow(plan.plannedDeliveryGameDay, plan.windowStartMinuteOfDay, plan.windowEndMinuteOfDay));
                sb.AppendLine("Fiabilidad: " + BistroBuilderSupplierPlayerUiFormat.Reliability(plan.reliabilityTier) +
                              (plan.decidedDelayGameMinutes > 0 ? " · retraso previsto " + plan.decidedDelayGameMinutes + " min" : " · puntual"));
                sb.AppendLine("Vehículo: " + BistroBuilderSupplierPlayerUiFormat.HumanizeIdentifier(plan.resolvedVehicle.ToString()) +
                              " · viajes visuales: " + plan.suggestedTripCount);
            }
            BistroBuilderSupplierDeliveryPresentationRecord presentation;
            if (deliveryService.TryGetPresentationByOrder(order.purchaseOrderId, out presentation) && presentation != null)
            {
                sb.AppendLine("Entrega física: " + BistroBuilderSupplierPlayerUiFormat.HumanizeIdentifier(presentation.state.ToString()) +
                              " · viaje " + presentation.currentTrip + "/" + presentation.totalTrips);
            }
            if (!string.IsNullOrWhiteSpace(order.deliveryReceiptId))
                sb.AppendLine("ReceiptId: " + order.deliveryReceiptId);

            if (order.status == BistroBuilderPurchaseOrderStatus.Confirmed ||
                order.status == BistroBuilderPurchaseOrderStatus.PendingDelivery)
            {
                SetButton(primaryAction, "CANCELAR PEDIDO", true);
            }
            else
            {
                SetButton(primaryAction, "ACTUALIZAR", true);
            }
            SetButton(secondaryAction, "COMPRA INTELIGENTE", true);
            SetButton(tertiaryAction, "VER PROVEEDOR", true);
        }
        detailTitle.text = order.displayCode;
        detailBody.text = sb.ToString();
    }

    private void HandlePrimaryAction()
    {
        switch (currentSection)
        {
            case BistroBuilderSupplierPlayerSection.Suppliers:
                if (progressionService.IsSupplierUnlocked(selectedSupplierId))
                    SelectSection(BistroBuilderSupplierPlayerSection.Catalog);
                else
                    Refresh(true);
                break;
            case BistroBuilderSupplierPlayerSection.Catalog:
                AddSelectedOfferToDraft();
                break;
            case BistroBuilderSupplierPlayerSection.SmartPurchase:
                if (smartReport == null) RefreshSmartReport();
                else CreateDraftsFromSelectedPlan();
                break;
            case BistroBuilderSupplierPlayerSection.Orders:
                HandleOrderPrimary();
                break;
        }
    }

    private void HandleSecondaryAction()
    {
        switch (currentSection)
        {
            case BistroBuilderSupplierPlayerSection.Suppliers:
                SelectSection(BistroBuilderSupplierPlayerSection.SmartPurchase);
                break;
            case BistroBuilderSupplierPlayerSection.Catalog:
                SelectSection(BistroBuilderSupplierPlayerSection.Orders);
                break;
            case BistroBuilderSupplierPlayerSection.SmartPurchase:
                RefreshSmartReport();
                break;
            case BistroBuilderSupplierPlayerSection.Orders:
                BistroBuilderPurchaseOrderRecord order;
                if (TryGetSelectedOrder(out order) && order.status == BistroBuilderPurchaseOrderStatus.Draft)
                    CancelSelectedOrder("Borrador cancelado por el jugador.");
                else
                    SelectSection(BistroBuilderSupplierPlayerSection.SmartPurchase);
                break;
        }
    }

    private void HandleTertiaryAction()
    {
        switch (currentSection)
        {
            case BistroBuilderSupplierPlayerSection.Suppliers:
                SelectSection(BistroBuilderSupplierPlayerSection.Orders);
                break;
            case BistroBuilderSupplierPlayerSection.Catalog:
                SelectSection(BistroBuilderSupplierPlayerSection.SmartPurchase);
                break;
            case BistroBuilderSupplierPlayerSection.SmartPurchase:
                SelectSection(BistroBuilderSupplierPlayerSection.Orders);
                break;
            case BistroBuilderSupplierPlayerSection.Orders:
                BistroBuilderPurchaseOrderRecord order;
                if (TryGetSelectedOrder(out order)) selectedSupplierId = order.supplierId;
                SelectSection(BistroBuilderSupplierPlayerSection.Suppliers);
                break;
        }
    }

    private void HandleOrderPrimary()
    {
        BistroBuilderPurchaseOrderRecord order;
        if (!TryGetSelectedOrder(out order)) return;
        if (order.status == BistroBuilderPurchaseOrderStatus.Draft)
        {
            if (!orderService.TryBuildConfirmationPreview(
                    order.purchaseOrderId,
                    out BistroBuilderPurchaseOrderConfirmationPreview preview,
                    out string previewError) || preview == null || !preview.canConfirm)
            {
                SetStatus(previewError ?? "El pedido no es confirmable.", true);
                Refresh(true);
                return;
            }
            OpenOrderConfirmation(order, preview);
            return;
        }
        else if (order.status == BistroBuilderPurchaseOrderStatus.Confirmed ||
                 order.status == BistroBuilderPurchaseOrderStatus.PendingDelivery)
        {
            CancelSelectedOrder("Cancelado por el jugador desde Proveedores.");
            return;
        }
        Refresh(true);
    }

    private void CancelSelectedOrder(string reason)
    {
        BistroBuilderPurchaseOrderRecord order;
        if (!TryGetSelectedOrder(out order)) return;
        if (!orderService.TryCancelOrder(order.purchaseOrderId, reason, out _, out string error))
        {
            SetStatus(error, true);
            return;
        }
        SetStatus("Pedido cancelado.", false);
        Refresh(true);
    }

    private void AddSelectedOfferToDraft()
    {
        BistroBuilderSupplierAuthoringRecord supplier;
        BistroBuilderSupplierBaseOfferAuthoringRecord offer;
        if (!TryGetSelectedSupplier(out supplier) || !TryGetSelectedOffer(supplier, out offer))
        {
            SetStatus("Selecciona una oferta.", true);
            return;
        }
        if (!progressionService.IsSupplierUnlocked(supplier.SupplierId))
        {
            SetStatus("El proveedor todavía está bloqueado por 2.3I.", true);
            return;
        }

        BistroBuilderPurchaseOrderRecord draft = FindDraftForSupplier(supplier.SupplierId);
        if (draft == null)
        {
            if (!progressionService.TryCreatePlayerDraft(supplier.SupplierId, out draft, out string createError))
            {
                SetStatus(createError, true);
                return;
            }
        }
        if (!orderService.TrySetDraftLine(
                draft.purchaseOrderId,
                offer.SupplierOfferId,
                selectedPackageCount,
                out BistroBuilderPurchaseOrderRecord updated,
                out string error))
        {
            SetStatus(error, true);
            return;
        }
        selectedOrderId = updated.purchaseOrderId;
        SetStatus("Añadido al borrador " + updated.displayCode + ".", false);
        Refresh(true);
    }

    private void RefreshSmartReport()
    {
        ResolveDependencies();
        string error = string.Empty;
        if (smartPurchaseService == null ||
            !smartPurchaseService.TryBuildRecommendations(out smartReport, out error))
        {
            smartReport = null;
            SetStatus(!string.IsNullOrWhiteSpace(error)
                ? error
                : "No se pudo construir la recomendación.", true);
            Refresh(true);
            return;
        }
        selectedStrategy = smartReport.recommendedStrategy;
        SetStatus("Compra Inteligente actualizada con stock y cotizaciones reales.", false);
        Refresh(true);
    }

    private void CreateDraftsFromSelectedPlan()
    {
        BistroBuilderSmartPurchasePlan plan = FindPlan(selectedStrategy);
        if (plan == null)
        {
            SetStatus("No hay plan seleccionado.", true);
            return;
        }
        if (!smartPurchaseService.TryCreateDraftFromPlan(
                plan,
                out List<string> created,
                out string error))
        {
            SetStatus(error, true);
            return;
        }
        if (created.Count > 0) selectedOrderId = created[0];
        SetStatus("Creados " + created.Count + " borrador(es). Revisa y confirma desde Pedidos.", false);
        currentSection = BistroBuilderSupplierPlayerSection.Orders;
        Refresh(true);
    }

    private void IncreaseQuantity()
    {
        BistroBuilderSupplierAuthoringRecord supplier;
        BistroBuilderSupplierBaseOfferAuthoringRecord offer;
        if (!TryGetSelectedSupplier(out supplier) || !TryGetSelectedOffer(supplier, out offer)) return;
        int step = Math.Max(1, offer.orderIncrement);
        selectedPackageCount = Math.Min(9999, selectedPackageCount + step);
        RefreshDetail();
    }

    private void DecreaseQuantity()
    {
        BistroBuilderSupplierAuthoringRecord supplier;
        BistroBuilderSupplierBaseOfferAuthoringRecord offer;
        if (!TryGetSelectedSupplier(out supplier) || !TryGetSelectedOffer(supplier, out offer)) return;
        int step = Math.Max(1, offer.orderIncrement);
        int min = Math.Max(1, offer.minimumPackageCount);
        selectedPackageCount = Math.Max(min, selectedPackageCount - step);
        RefreshDetail();
    }

    private void ResetDetailActions()
    {
        ApplyLogo(null);
        quantityMinus.gameObject.SetActive(false);
        quantityPlus.gameObject.SetActive(false);
        quantityText.gameObject.SetActive(false);
        SetButton(primaryAction, "", false);
        SetButton(secondaryAction, "", false);
        SetButton(tertiaryAction, "", false);
    }

    private void EnsureDefaultSelection()
    {
        if (string.IsNullOrWhiteSpace(selectedSupplierId))
        {
            IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = supplierDatabase.Suppliers;
            for (int i = 0; i < suppliers.Count; i++)
            {
                if (suppliers[i] != null && suppliers[i].isActive && progressionService.IsSupplierUnlocked(suppliers[i].SupplierId))
                {
                    selectedSupplierId = suppliers[i].SupplierId;
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(selectedSupplierId) && suppliers.Count > 0 && suppliers[0] != null)
                selectedSupplierId = suppliers[0].SupplierId;
        }
        if (currentSection == BistroBuilderSupplierPlayerSection.Orders && string.IsNullOrWhiteSpace(selectedOrderId))
        {
            orderService.CopyOrders(orders);
            if (orders.Count > 0 && orders[orders.Count - 1] != null)
                selectedOrderId = orders[orders.Count - 1].purchaseOrderId;
        }
    }

    private bool RuntimeAuthoritiesReady(out string error)
    {
        error = string.Empty;
        if (marketService == null || !marketService.IsInitialized) error = "2.3C no está listo.";
        else if (commercialService == null || !commercialService.IsInitialized) error = "2.3D no está listo.";
        else if (orderService == null || !orderService.IsInitialized) error = "2.3E no está listo.";
        else if (smartPurchaseService == null || !smartPurchaseService.IsInitialized) error = "2.3F no está listo.";
        else if (logisticsService == null || !logisticsService.IsInitialized) error = "2.3G no está listo.";
        else if (deliveryService == null || !deliveryService.IsInitialized) error = "2.3H no está listo.";
        else if (progressionService == null || !progressionService.IsInitialized) error = "2.3I no está listo.";
        return string.IsNullOrEmpty(error);
    }

    private void ResolveDependencies()
    {
        LoadStaticDatabases();
        marketService = BistroBuilderSupplierMarketService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierMarketService>();
        commercialService = BistroBuilderSupplierCommercialIntelligenceService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierCommercialIntelligenceService>();
        orderService = BistroBuilderSupplierPurchaseOrderService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        smartPurchaseService = BistroBuilderSupplierSmartPurchaseService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierSmartPurchaseService>();
        progressionService = BistroBuilderSupplierProgressionService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierProgressionService>();
        logisticsService = BistroBuilderSupplierLogisticsService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierLogisticsService>();
        deliveryService = BistroBuilderSupplierDeliveryPresentationService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierDeliveryPresentationService>();
        if (receivingBridge == null) receivingBridge = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierReceivingBridge23L>();
        if (cameraController == null) cameraController = UnityEngine.Object.FindFirstObjectByType<BistroBuilderProfessionalCameraController>();
        if (editInteractionController == null) editInteractionController = UnityEngine.Object.FindFirstObjectByType<RestaurantEditInteractionController>();
    }

    private void LoadStaticDatabases()
    {
        if (supplierDatabase == null)
            supplierDatabase = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSupplierCommercialIntelligenceService.SupplierAuthoringResourcePath);
        if (ingredientDatabase == null)
            ingredientDatabase = Resources.Load<BistroBuilderIngredientAuthoringDatabase>(BistroBuilderSupplierCommercialIntelligenceService.IngredientAuthoringResourcePath);
    }

    private long ComputeRevisionFingerprint()
    {
        unchecked
        {
            long value = 17L;
            value = value * 31L + (marketService != null ? marketService.MarketRevision : 0L);
            value = value * 31L + (commercialService != null ? commercialService.CommercialRevision : 0L);
            value = value * 31L + (orderService != null ? orderService.OrdersRevision : 0L);
            value = value * 31L + (logisticsService != null ? logisticsService.LogisticsRevision : 0L);
            value = value * 31L + (progressionService != null ? progressionService.ProgressionRevision : 0L);
            value = value * 31L + (deliveryService != null ? deliveryService.PresentationCount : 0);
            value = value * 31L + (receivingBridge != null ? receivingBridge.AcceptedHandoffCount : 0L);
            return value;
        }
    }

    private bool TryGetSelectedSupplier(out BistroBuilderSupplierAuthoringRecord supplier)
    {
        supplier = null;
        return supplierDatabase != null && !string.IsNullOrWhiteSpace(selectedSupplierId) &&
               supplierDatabase.TryGetSupplier(selectedSupplierId, out supplier) && supplier != null;
    }

    private bool TryGetSelectedOffer(
        BistroBuilderSupplierAuthoringRecord supplier,
        out BistroBuilderSupplierBaseOfferAuthoringRecord offer)
    {
        offer = null;
        if (supplier == null || supplier.baseOffers == null || string.IsNullOrWhiteSpace(selectedOfferId)) return false;
        for (int i = 0; i < supplier.baseOffers.Count; i++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord current = supplier.baseOffers[i];
            if (current != null && string.Equals(current.SupplierOfferId, selectedOfferId, StringComparison.Ordinal))
            {
                offer = current;
                return true;
            }
        }
        return false;
    }

    private bool TryResolveOfferVisual(
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        out BistroBuilderIngredientAuthoringRecord ingredient,
        out BistroBuilderCommercialPackageAuthoringRecord package)
    {
        // Ambos out deben quedar asignados en todas las rutas, incluso cuando
        // offer/ingredientDatabase sean nulos y el || corte la evaluación antes
        // de ejecutar TryGetIngredient.
        ingredient = null;
        package = null;
        if (offer == null || ingredientDatabase == null ||
            !ingredientDatabase.TryGetIngredient(offer.ingredientId, out ingredient) ||
            ingredient == null)
        {
            return false;
        }
        if (ingredient.commercialPackages == null) return false;
        for (int i = 0; i < ingredient.commercialPackages.Count; i++)
        {
            BistroBuilderCommercialPackageAuthoringRecord current = ingredient.commercialPackages[i];
            if (current != null && current.isActive && string.Equals(current.PackageFormatId, offer.packageFormatId, StringComparison.Ordinal))
            {
                package = current;
                return true;
            }
        }
        return false;
    }

    private bool TryGetSelectedOrder(out BistroBuilderPurchaseOrderRecord order)
    {
        order = null;
        return orderService != null && !string.IsNullOrWhiteSpace(selectedOrderId) &&
               orderService.TryGetOrder(selectedOrderId, out order) && order != null;
    }

    private BistroBuilderPurchaseOrderRecord FindDraftForSupplier(string supplierId)
    {
        orderService.CopyOrders(orders);
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            BistroBuilderPurchaseOrderRecord order = orders[i];
            if (order != null && order.status == BistroBuilderPurchaseOrderStatus.Draft &&
                string.Equals(order.supplierId, supplierId, StringComparison.Ordinal)) return order;
        }
        return null;
    }

    private BistroBuilderSmartPurchasePlan FindPlan(BistroBuilderSmartPurchaseStrategy strategy)
    {
        if (smartReport == null || smartReport.plans == null) return null;
        for (int i = 0; i < smartReport.plans.Count; i++)
            if (smartReport.plans[i] != null && smartReport.plans[i].strategy == strategy) return smartReport.plans[i];
        return null;
    }

    private string ResolveSupplierName(string supplierId)
    {
        BistroBuilderSupplierAuthoringRecord supplier;
        return supplierDatabase != null && supplierDatabase.TryGetSupplier(supplierId, out supplier) && supplier != null
            ? supplier.displayName
            : supplierId;
    }

    private string GetUnlockSummary(string supplierId)
    {
        BistroBuilderSupplierAccessEvaluation access;
        if (progressionService.TryGetSupplierAccess(supplierId, out access) && access != null)
            return access.summary;
        return "Sin información de progresión.";
    }

    private int CountPromotionsForSupplier(string supplierId)
    {
        int count = 0;
        for (int i = 0; i < activePromotions.Count; i++)
            if (activePromotions[i] != null && string.Equals(activePromotions[i].supplierId, supplierId, StringComparison.Ordinal)) count++;
        return count;
    }

    private void ApplyLogo(BistroBuilderSupplierAuthoringRecord supplier)
    {
        if (logoImage == null) return;
        if (supplier == null)
        {
            logoImage.sprite = null;
            logoImage.color = BistroBuilderMenuEditorUiFactory.SurfaceRaised;
            return;
        }
        logoImage.sprite = supplier.logo;
        logoImage.color = supplier.logo != null ? Color.white : supplier.primaryBrandColor;
    }

    private Button CreateListButton(string name, string label, Action callback, Color color)
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            name, listContent, label, () => callback(), color, 12);
        StabilizeButton(button);
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(button, 62f);
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.alignment = TextAnchor.MiddleLeft;
            text.fontStyle = FontStyle.Normal;
        }
        visibleRowCount++;
        return button;
    }

    private void CreateInfoRow(string name, string value)
    {
        Text text = BistroBuilderMenuEditorUiFactory.CreateText(
            name, listContent, value, 12, TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary);
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(text, 72f);
        visibleRowCount++;
    }

    private static string BuildUnlockProgressRow(BistroBuilderSupplierAccessEvaluation access)
    {
        if (access == null) return "Desbloqueo pendiente";
        if (access.isUnlocked) return "Disponible";

        if (access.conditions != null)
        {
            for (int index = 0; index < access.conditions.Count; index++)
            {
                BistroBuilderSupplierUnlockConditionResult condition = access.conditions[index];
                if (condition == null || condition.satisfied) continue;

                if (condition.sourceAvailable && !string.IsNullOrWhiteSpace(condition.reasonText))
                {
                    string reason = condition.reasonText.Trim().TrimEnd('.');
                    if (reason.StartsWith("Volumen de compras:", StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "Compras:" + reason.Substring("Volumen de compras:".Length);
                    }
                    return reason;
                }

                return BistroBuilderSupplierPlayerUiFormat.HumanizeIdentifier(condition.kind.ToString()) +
                       ": pendiente";
            }
        }

        return !string.IsNullOrWhiteSpace(access.summary)
            ? access.summary.Trim().TrimEnd('.')
            : "Desbloqueo pendiente";
    }

    private static Color CatalogRowColor(
        BistroBuilderSupplierOfferAvailability availability,
        bool hasPromotion)
    {
        Color color;
        switch (availability)
        {
            case BistroBuilderSupplierOfferAvailability.StockLimitado:
                color = Color.Lerp(
                    BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                    BistroBuilderMenuEditorUiFactory.Warning,
                    0.24f);
                break;
            case BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado:
                color = Color.Lerp(
                    BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                    BistroBuilderMenuEditorUiFactory.Negative,
                    0.34f);
                break;
            default:
                color = BistroBuilderMenuEditorUiFactory.SurfaceRaised;
                break;
        }

        if (hasPromotion &&
            availability != BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado)
        {
            color = Color.Lerp(color, BistroBuilderMenuEditorUiFactory.Positive, 0.20f);
        }
        return color;
    }

    private static void ApplyPersistentRowSelection(
        Button row,
        Color baseColor,
        bool selected)
    {
        if (row == null || row.image == null) return;
        row.image.color = selected
            ? Color.Lerp(baseColor, BistroBuilderMenuEditorUiFactory.Accent, 0.30f)
            : baseColor;

        Text text = row.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private static void StabilizeButtonTree(Component root)
    {
        if (root == null) return;
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            StabilizeButton(buttons[index]);
        }
    }

    private static void StabilizeButton(Button button)
    {
        if (button == null) return;
        button.transition = Selectable.Transition.None;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }

    private static void ClearEventSystemSelection()
    {
        EventSystem current = EventSystem.current;
        if (current != null && current.currentSelectedGameObject != null)
        {
            current.SetSelectedGameObject(null);
        }
    }

    private static Color BuildSupplierRowColor(BistroBuilderSupplierAuthoringRecord supplier, bool unlocked)
    {
        if (!unlocked) return new Color(0.12f, 0.12f, 0.12f, 1f);
        return Color.Lerp(BistroBuilderMenuEditorUiFactory.SurfaceRaised, supplier.primaryBrandColor, 0.22f);
    }

    private static Color OrderRowColor(BistroBuilderPurchaseOrderStatus status)
    {
        switch (status)
        {
            case BistroBuilderPurchaseOrderStatus.Delivered:
                return new Color(0.16f, 0.30f, 0.20f, 1f);
            case BistroBuilderPurchaseOrderStatus.InDelivery:
                return new Color(0.18f, 0.26f, 0.36f, 1f);
            case BistroBuilderPurchaseOrderStatus.Cancelled:
                return new Color(0.25f, 0.13f, 0.13f, 1f);
            case BistroBuilderPurchaseOrderStatus.Draft:
                return new Color(0.26f, 0.23f, 0.14f, 1f);
            default:
                return BistroBuilderMenuEditorUiFactory.SurfaceRaised;
        }
    }

    private static void SetTabColor(Button button, bool selected)
    {
        if (button == null || button.image == null) return;
        StabilizeButton(button);
        button.image.color = selected
            ? Color.Lerp(
                BistroBuilderMenuEditorUiFactory.SurfaceSelected,
                BistroBuilderMenuEditorUiFactory.Accent,
                0.18f)
            : BistroBuilderMenuEditorUiFactory.SurfaceRaised;
        Text text = button.GetComponentInChildren<Text>();
        if (text != null) text.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
    }

    private static void SetButton(Button button, string label, bool enabled)
    {
        if (button == null) return;
        StabilizeButton(button);
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = label ?? string.Empty;
            text.color = enabled
                ? BistroBuilderMenuEditorUiFactory.TextPrimary
                : new Color(
                    BistroBuilderMenuEditorUiFactory.TextSecondary.r,
                    BistroBuilderMenuEditorUiFactory.TextSecondary.g,
                    BistroBuilderMenuEditorUiFactory.TextSecondary.b,
                    0.55f);
        }
        button.interactable = enabled;
        button.gameObject.SetActive(!string.IsNullOrEmpty(label));
    }

    private void SetStatus(string value, bool error)
    {
        if (statusText == null) return;
        statusText.text = value ?? string.Empty;
        statusText.color = error
            ? BistroBuilderMenuEditorUiFactory.Negative
            : BistroBuilderMenuEditorUiFactory.TextSecondary;
    }

    private void ApplyInputGate()
    {
        if (inputGateApplied) return;
        if (cameraController != null)
        {
            cameraWasEnabled = cameraController.enabled;
            cameraController.enabled = false;
        }
        if (editInteractionController != null)
        {
            editWasEnabled = editInteractionController.enabled;
            editInteractionController.enabled = false;
        }
        inputGateApplied = true;
    }

    private void RestoreInputGate()
    {
        if (!inputGateApplied) return;
        if (cameraController != null) cameraController.enabled = cameraWasEnabled;
        if (editInteractionController != null) editInteractionController.enabled = editWasEnabled;
        inputGateApplied = false;
    }

    private void SetVisible(bool visible)
    {
        if (modalRoot != null) modalRoot.gameObject.SetActive(visible);
    }

    private static void ClearChildren(RectTransform root)
    {
        if (root == null) return;
        for (int index = root.childCount - 1; index >= 0; index--)
        {
            Transform child = root.GetChild(index);
            if (child == null) continue;

            // Destroy() se materializa al final del frame. Desactivar primero
            // evita que la fila vieja (todavía en estado Pressed) se solape un
            // frame con la fila recién creada durante un Refresh por click.
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void SetRect(
        RectTransform rect,
        float minX, float minY, float maxX, float maxY,
        float offset)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(offset, offset);
        rect.offsetMax = new Vector2(-offset, -offset);
    }

    private static void SetRect(
        RectTransform rect,
        float minX, float minY, float maxX, float maxY,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
