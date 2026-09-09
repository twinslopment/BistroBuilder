using System;
using System.Collections.Generic;
using System.Text;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum BistroBuilderFinancePlayerSection
{
    Overview = 0,
    Results = 1,
    Cash = 2,
    History = 3,
    Financing = 4
}

/// <summary>
/// 3J — UI jugable de Finanzas y Caja.
///
/// Es Presentation pura. Lee únicamente BistroBuilderFinanceDashboardService
/// y toda acción financiera jugable se canaliza por esa fachada hacia 3I.
/// Nunca modifica ledger, resultados, históricos ni deuda directamente.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Finance Runtime View 3J")]
public sealed class BistroBuilderFinanceRuntimeView : MonoBehaviour
{
    public const string RuntimeRevision = "FINANCE-3J-UI-V1";

    [Header("Dependencias")]
    [SerializeField] private BistroBuilderFinanceDashboardService dashboardService;
    [SerializeField] private BistroBuilderProfessionalCameraController cameraController;
    [SerializeField] private RestaurantEditInteractionController editInteractionController;

    [Header("Comportamiento")]
    [SerializeField] private bool showOpenButton = true;
    [SerializeField, Min(10)] private int maximumRecentMovements = 80;

    private Button openButton;
    private RectTransform modalRoot;
    private RectTransform contentRoot;
    private Text headerSummary;
    private Text statusText;

    private Button overviewTab;
    private Button resultsTab;
    private Button cashTab;
    private Button historyTab;
    private Button financingTab;

    private RectTransform confirmationOverlay;
    private Text confirmationTitle;
    private Text confirmationBody;
    private Button confirmationAcceptButton;

    private BistroBuilderFinanceDashboardSnapshot dashboard;
    private BistroBuilderFinancePlayerSection currentSection =
        BistroBuilderFinancePlayerSection.Overview;
    private BistroBuilderFinanceDashboardPeriod selectedPeriod =
        BistroBuilderFinanceDashboardPeriod.Last7Days;
    private BistroBuilderFinanceChartMetric selectedChartMetric =
        BistroBuilderFinanceChartMetric.Revenue;
    private string selectedOfferId = string.Empty;
    private string financingConfirmationToken = string.Empty;

    private bool built;
    private bool subscribed;
    private bool refreshQueued;
    private bool cameraWasEnabled;
    private bool editWasEnabled;
    private bool inputGateApplied;
    private long lastRenderedRevision = long.MinValue;
    private int visibleElementCount;

    public BistroBuilderFinanceDashboardService DashboardService => dashboardService;
    public bool VisualTreeBuilt => built;
    public bool IsOpen => modalRoot != null && modalRoot.gameObject.activeSelf;
    public BistroBuilderFinancePlayerSection CurrentSection => currentSection;
    public BistroBuilderFinanceDashboardPeriod SelectedPeriod => selectedPeriod;
    public BistroBuilderFinanceChartMetric SelectedChartMetric => selectedChartMetric;
    public string SelectedOfferId => selectedOfferId;
    public int VisibleElementCount => visibleElementCount;
    public BistroBuilderFinanceDashboardSnapshot DashboardSnapshot =>
        dashboard != null ? dashboard.DeepClone() : null;

    private void Awake()
    {
        ResolveDependencies();
        EnsureVisualTree();
        SetVisible(false);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
    }

    private void Start()
    {
        EnsureVisualTree();
        if (openButton != null)
        {
            openButton.gameObject.SetActive(showOpenButton);
        }
    }

    private void LateUpdate()
    {
        if (!IsOpen || dashboardService == null)
        {
            return;
        }

        if (refreshQueued ||
            dashboardService.PresentationRevision != lastRenderedRevision)
        {
            refreshQueued = false;
            Refresh(false);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        CloseConfirmation();
        RestoreInputGate();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        if (dashboardService == null)
        {
            error = "Falta BistroBuilderFinanceDashboardService 3J.";
            return false;
        }
        if (maximumRecentMovements < 10 || maximumRecentMovements > 500)
        {
            error = "3J necesita entre 10 y 500 movimientos recientes configurados.";
            return false;
        }
        return dashboardService.ValidateConfiguration(out error);
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

        SetVisible(true);
        ApplyInputGate();
        if (!Refresh(true, out error))
        {
            SetVisible(false);
            RestoreInputGate();
            return false;
        }
        return true;
    }

    public void Close()
    {
        CloseConfirmation();
        SetVisible(false);
        RestoreInputGate();
        ClearEventSystemSelection();
    }

    public bool TrySelectSectionForTests(
        BistroBuilderFinancePlayerSection section,
        out string error)
    {
        error = string.Empty;
        if (!Enum.IsDefined(typeof(BistroBuilderFinancePlayerSection), section))
        {
            error = "La sección financiera solicitada no existe.";
            return false;
        }
        if (!IsOpen && !TryOpenFromInterface(out error))
        {
            return false;
        }

        currentSection = section;
        return RenderCurrentDashboard(out error);
    }

    public bool TrySetPeriodForTests(
        BistroBuilderFinanceDashboardPeriod period,
        out string error)
    {
        error = string.Empty;
        if (!Enum.IsDefined(typeof(BistroBuilderFinanceDashboardPeriod), period))
        {
            error = "El periodo financiero solicitado no existe.";
            return false;
        }
        selectedPeriod = period;
        return Refresh(true, out error);
    }

    public bool TrySetChartMetricForTests(
        BistroBuilderFinanceChartMetric metric,
        out string error)
    {
        error = string.Empty;
        if (!Enum.IsDefined(typeof(BistroBuilderFinanceChartMetric), metric))
        {
            error = "La métrica de gráfico solicitada no existe.";
            return false;
        }
        selectedChartMetric = metric;
        return RenderCurrentDashboard(out error);
    }

    public bool TrySelectFirstEligibleOfferForTests(out string error)
    {
        error = string.Empty;
        if (!IsOpen && !TryOpenFromInterface(out error))
        {
            return false;
        }
        if (dashboard == null || dashboard.financingOffers == null)
        {
            error = "3J no dispone de ofertas de financiación.";
            return false;
        }

        for (int index = 0; index < dashboard.financingOffers.Count; index++)
        {
            BistroBuilderFinancingOfferView offer = dashboard.financingOffers[index];
            if (offer != null && offer.eligible)
            {
                selectedOfferId = offer.offerId;
                currentSection = BistroBuilderFinancePlayerSection.Financing;
                return RenderCurrentDashboard(out error);
            }
        }

        error = "No existe una oferta de financiación elegible en el estado actual.";
        return false;
    }

    public bool TryOpenFinancingConfirmationForTests(out string error)
    {
        error = string.Empty;
        if (!IsOpen && !TryOpenFromInterface(out error))
        {
            return false;
        }
        return OpenFinancingConfirmation(out error);
    }

    public bool TryConfirmSelectedFinancingForTests(
        out BistroBuilderLoanRecord loan,
        out string error)
    {
        loan = null;
        if (!IsOpen && !TryOpenFromInterface(out error))
        {
            return false;
        }
        if (!confirmationOverlay.gameObject.activeSelf &&
            !OpenFinancingConfirmation(out error))
        {
            return false;
        }
        return ConfirmFinancing(out loan, out error);
    }

    public bool TryValidateVisibleContent(out string error)
    {
        error = string.Empty;
        bool wasOpen = IsOpen;
        BistroBuilderFinancePlayerSection previousSection = currentSection;
        if (!TryOpenFromInterface(out error))
        {
            return false;
        }

        try
        {
            Canvas.ForceUpdateCanvases();
            if (overviewTab == null || resultsTab == null || cashTab == null ||
                historyTab == null || financingTab == null ||
                contentRoot == null || headerSummary == null || statusText == null ||
                confirmationOverlay == null || confirmationBody == null)
            {
                error = "3J no contiene todos sus controles esenciales.";
                return false;
            }
            if (dashboard == null || dashboard.currentDay == null ||
                dashboard.periodReport == null || dashboard.liquidity == null ||
                dashboard.stress == null)
            {
                error = "3J no ha recibido un read-model financiero completo.";
                return false;
            }

            ScrollRect[] scrolls = GetComponentsInChildren<ScrollRect>(true);
            for (int index = 0; index < scrolls.Length; index++)
            {
                ScrollRect scroll = scrolls[index];
                if (scroll == null || scroll.viewport == null ||
                    scroll.viewport.GetComponent<RectMask2D>() == null ||
                    scroll.viewport.GetComponent<Mask>() != null)
                {
                    error = "3J contiene un ScrollRect sin RectMask2D seguro.";
                    return false;
                }
            }

            if (visibleElementCount <= 0)
            {
                error = "3J no ha generado contenido visible.";
                return false;
            }

            return TryValidateStableInteractionVisuals(out error);
        }
        finally
        {
            currentSection = previousSection;
            if (dashboard != null)
            {
                RenderCurrentDashboard(out _);
            }
            if (!wasOpen)
            {
                Close();
            }
        }
    }

    public bool TryValidateStableInteractionVisuals(out string error)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button == null)
            {
                continue;
            }
            if (button.transition != Selectable.Transition.None ||
                button.navigation.mode != Navigation.Mode.None)
            {
                error = "El botón " + button.name +
                        " conserva estados transitorios de uGUI.";
                return false;
            }
        }
        error = string.Empty;
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
            "OpenFinance",
            host,
            "FINANZAS",
            HandleOpen,
            new Color(0.22f, 0.19f, 0.13f, 1f),
            13);
        SetRect(
            openButton.GetComponent<RectTransform>(),
            0f,
            1f,
            0f,
            1f,
            new Vector2(558f, -58f),
            new Vector2(696f, -18f));

        modalRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "FinanceModal",
            host,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        BistroBuilderMenuEditorUiFactory.AddImage(
            modalRoot,
            BistroBuilderMenuEditorUiFactory.Overlay);

        RectTransform panel = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Panel",
            modalRoot,
            Vector2.zero,
            Vector2.one,
            new Vector2(32f, 24f),
            new Vector2(-32f, -24f));
        BistroBuilderMenuEditorUiFactory.AddImage(
            panel,
            BistroBuilderMenuEditorUiFactory.Surface);

        Text title = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title",
            panel,
            "FINANZAS Y CAJA",
            24,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold);
        SetRect(title.rectTransform, 0.02f, 0.93f, 0.52f, 0.985f, 0f);

        headerSummary = BistroBuilderMenuEditorUiFactory.CreateText(
            "HeaderSummary",
            panel,
            string.Empty,
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary);
        SetRect(headerSummary.rectTransform, 0.02f, 0.885f, 0.82f, 0.93f, 0f);

        Button closeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close",
            panel,
            "CERRAR",
            Close,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            12);
        SetRect(
            closeButton.GetComponent<RectTransform>(),
            0.87f,
            0.935f,
            0.98f,
            0.985f,
            0f);

        BuildTabs(panel);

        contentRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "FinanceContent",
            panel,
            new Vector2(0.02f, 0.075f),
            new Vector2(0.98f, 0.79f),
            Vector2.zero,
            Vector2.zero);

        statusText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Status",
            panel,
            string.Empty,
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary);
        SetRect(statusText.rectTransform, 0.02f, 0.015f, 0.98f, 0.062f, 0f);

        BuildConfirmationOverlay(modalRoot);
        StabilizeButtonTree(host);
        built = true;
    }

    private void BuildTabs(RectTransform panel)
    {
        overviewTab = CreateTab(
            panel,
            "TabOverview",
            "RESUMEN",
            0.02f,
            0.145f,
            () => SelectSection(BistroBuilderFinancePlayerSection.Overview));
        resultsTab = CreateTab(
            panel,
            "TabResults",
            "RESULTADOS",
            0.155f,
            0.31f,
            () => SelectSection(BistroBuilderFinancePlayerSection.Results));
        cashTab = CreateTab(
            panel,
            "TabCash",
            "CAJA",
            0.32f,
            0.43f,
            () => SelectSection(BistroBuilderFinancePlayerSection.Cash));
        historyTab = CreateTab(
            panel,
            "TabHistory",
            "HISTÓRICOS",
            0.44f,
            0.60f,
            () => SelectSection(BistroBuilderFinancePlayerSection.History));
        financingTab = CreateTab(
            panel,
            "TabFinancing",
            "FINANCIACIÓN",
            0.61f,
            0.79f,
            () => SelectSection(BistroBuilderFinancePlayerSection.Financing));
    }

    private Button CreateTab(
        RectTransform parent,
        string name,
        string label,
        float minX,
        float maxX,
        Action callback)
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            name,
            parent,
            label,
            () => callback(),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            11);
        SetRect(
            button.GetComponent<RectTransform>(),
            minX,
            0.815f,
            maxX,
            0.865f,
            0f);
        StabilizeButton(button);
        return button;
    }

    private void BuildConfirmationOverlay(RectTransform host)
    {
        confirmationOverlay = BistroBuilderMenuEditorUiFactory.CreateRect(
            "FinanceConfirmationOverlay",
            host,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        BistroBuilderMenuEditorUiFactory.AddImage(
            confirmationOverlay,
            new Color(0.015f, 0.018f, 0.017f, 0.93f));

        RectTransform card = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ConfirmationCard",
            confirmationOverlay,
            new Vector2(0.29f, 0.27f),
            new Vector2(0.71f, 0.73f),
            Vector2.zero,
            Vector2.zero);
        BistroBuilderMenuEditorUiFactory.AddImage(
            card,
            BistroBuilderMenuEditorUiFactory.Surface);

        confirmationTitle = BistroBuilderMenuEditorUiFactory.CreateText(
            "ConfirmationTitle",
            card,
            "CONFIRMAR FINANCIACIÓN",
            19,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold);
        SetRect(confirmationTitle.rectTransform, 0.06f, 0.79f, 0.94f, 0.95f, 0f);

        confirmationBody = BistroBuilderMenuEditorUiFactory.CreateText(
            "ConfirmationBody",
            card,
            string.Empty,
            13,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary);
        confirmationBody.supportRichText = true;
        SetRect(confirmationBody.rectTransform, 0.06f, 0.26f, 0.94f, 0.77f, 0f);

        Button cancel = BistroBuilderMenuEditorUiFactory.CreateButton(
            "CancelFinancing",
            card,
            "CANCELAR",
            CloseConfirmation,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            12);
        SetRect(cancel.GetComponent<RectTransform>(), 0.06f, 0.07f, 0.44f, 0.20f, 0f);

        confirmationAcceptButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "ConfirmFinancing",
            card,
            "ACEPTAR FINANCIACIÓN",
            HandleConfirmFinancing,
            BistroBuilderMenuEditorUiFactory.Accent,
            12);
        SetRect(
            confirmationAcceptButton.GetComponent<RectTransform>(),
            0.48f,
            0.07f,
            0.94f,
            0.20f,
            0f);

        confirmationOverlay.gameObject.SetActive(false);
    }

    private bool Refresh(bool force)
    {
        return Refresh(force, out _);
    }

    private bool Refresh(bool force, out string error)
    {
        error = string.Empty;
        if (dashboardService == null)
        {
            error = "3J no tiene servicio de dashboard.";
            SetStatus(error, true);
            return false;
        }

        if (!force &&
            dashboard != null &&
            dashboardService.PresentationRevision == lastRenderedRevision)
        {
            return true;
        }

        if (!dashboardService.TryBuildDashboard(
                selectedPeriod,
                maximumRecentMovements,
                out dashboard,
                out error))
        {
            SetStatus(error, true);
            return false;
        }

        EnsureSelectedOffer();
        lastRenderedRevision = dashboardService.PresentationRevision;
        return RenderCurrentDashboard(out error);
    }

    private bool RenderCurrentDashboard(out string error)
    {
        error = string.Empty;
        if (dashboard == null || contentRoot == null)
        {
            error = "3J no dispone de un dashboard para renderizar.";
            return false;
        }

        UpdateHeader();
        UpdateTabColors();
        ClearChildren(contentRoot);
        visibleElementCount = 0;

        switch (currentSection)
        {
            case BistroBuilderFinancePlayerSection.Results:
                BuildResultsSection();
                break;
            case BistroBuilderFinancePlayerSection.Cash:
                BuildCashSection();
                break;
            case BistroBuilderFinancePlayerSection.History:
                BuildHistorySection();
                break;
            case BistroBuilderFinancePlayerSection.Financing:
                BuildFinancingSection();
                break;
            default:
                BuildOverviewSection();
                break;
        }

        StabilizeButtonTree(contentRoot);
        Canvas.ForceUpdateCanvases();
        return true;
    }

    private void BuildOverviewSection()
    {
        BistroBuilderDayFinancialResult day = dashboard.currentDay;
        BistroBuilderLiquidityPosition liquidity = dashboard.liquidity;
        BistroBuilderFinancialStressSnapshot stress = dashboard.stress;

        CreateValueCard(
            "CashCard",
            "CAJA",
            BistroBuilderFinanceUiFormat.Money(dashboard.cashBalanceCents),
            0.00f,
            0.80f,
            0.19f,
            1.00f,
            dashboard.cashBalanceCents >= 0L
                ? BistroBuilderMenuEditorUiFactory.Accent
                : BistroBuilderMenuEditorUiFactory.Negative);
        CreateValueCard(
            "AvailableCard",
            "DISPONIBLE TRAS COMPROMISOS",
            BistroBuilderFinanceUiFormat.Money(
                liquidity.availableCashAfterSupplierCommitmentsCents),
            0.202f,
            0.80f,
            0.394f,
            1.00f,
            liquidity.availableCashAfterSupplierCommitmentsCents >= 0L
                ? BistroBuilderMenuEditorUiFactory.Positive
                : BistroBuilderMenuEditorUiFactory.Negative);
        CreateValueCard(
            "RevenueCard",
            "VENTAS HOY",
            BistroBuilderFinanceUiFormat.Money(day.revenueCents),
            0.406f,
            0.80f,
            0.598f,
            1.00f,
            BistroBuilderMenuEditorUiFactory.TextPrimary);
        CreateValueCard(
            "ResultCard",
            "RESULTADO HOY",
            BistroBuilderFinanceUiFormat.Money(day.operatingResultCents, true),
            0.61f,
            0.80f,
            0.802f,
            1.00f,
            MoneyColor(day.operatingResultCents));
        CreateValueCard(
            "RiskCard",
            "RIESGO FINANCIERO",
            BistroBuilderFinanceUiFormat.Risk(stress.riskLevel),
            0.814f,
            0.80f,
            1.00f,
            1.00f,
            RiskColor(stress.riskLevel));

        RectTransform liquidityPanel = CreatePanel(
            "LiquidityPanel",
            0.00f,
            0.00f,
            0.32f,
            0.77f);
        AddPanelTitle(liquidityPanel, "LIQUIDEZ");
        StringBuilder liquidityText = new StringBuilder();
        liquidityText.AppendLine("Estado: <b>" +
            BistroBuilderFinanceUiFormat.Liquidity(liquidity.status) + "</b>");
        liquidityText.AppendLine();
        liquidityText.AppendLine("Caja: " +
            BistroBuilderFinanceUiFormat.Money(liquidity.cashBalanceCents));
        liquidityText.AppendLine("Compromisos proveedor: " +
            BistroBuilderFinanceUiFormat.Money(liquidity.supplierCommittedCents));
        liquidityText.AppendLine("Gastos recurrentes próximos: " +
            BistroBuilderFinanceUiFormat.Money(
                liquidity.recurringOperatingObligationsWithinHorizonCents));
        liquidityText.AppendLine("Deuda próximos " + liquidity.horizonDays + " días: " +
            BistroBuilderFinanceUiFormat.Money(liquidity.debtDueWithinHorizonCents));
        liquidityText.AppendLine("Deuda vencida: " +
            BistroBuilderFinanceUiFormat.Money(liquidity.overdueDebtCents));
        liquidityText.AppendLine();
        liquidityText.AppendLine("Liquidez proyectada: <b>" +
            BistroBuilderFinanceUiFormat.Money(
                liquidity.projectedLiquidityAfterHorizonObligationsCents) + "</b>");
        liquidityText.AppendLine("Cobertura obligaciones: " +
            BistroBuilderFinanceUiFormat.Ratio(
                liquidity.knownObligationCoverageBasisPoints));
        if (!liquidity.projectionComplete)
        {
            liquidityText.AppendLine();
            liquidityText.AppendLine("<b>Información incompleta:</b> no se interpreta como liquidez sana.");
        }
        AddPanelBody(liquidityPanel, liquidityText.ToString());

        RectTransform resultPanel = CreatePanel(
            "TodayPanel",
            0.335f,
            0.00f,
            0.66f,
            0.77f);
        AddPanelTitle(resultPanel, "RESULTADO DEL DÍA");
        StringBuilder resultText = new StringBuilder();
        resultText.AppendLine("Ventas: " + BistroBuilderFinanceUiFormat.Money(day.revenueCents));
        resultText.AppendLine("COGS reconocido: " + BistroBuilderFinanceUiFormat.Money(day.productCostCents));
        resultText.AppendLine("Margen bruto: <b>" +
            BistroBuilderFinanceUiFormat.Money(day.grossProfitCents) +
            " · " + BistroBuilderFinanceUiFormat.Percent(day.grossMarginBasisPoints) + "</b>");
        resultText.AppendLine();
        resultText.AppendLine("Gastos del periodo: " +
            BistroBuilderFinanceUiFormat.Money(day.totalPeriodExpensesCents));
        resultText.AppendLine("Resultado operativo: <b>" +
            BistroBuilderFinanceUiFormat.Money(day.operatingResultCents, true) + "</b>");
        resultText.AppendLine();
        resultText.AppendLine("Calidad de coste: " +
            BistroBuilderFinanceUiFormat.CostQuality(day.costQuality));
        resultText.AppendLine("Cobertura COGS: " +
            (day.IsCostCoverageComplete ? "Completa" :
                "Pendiente " + BistroBuilderFinanceUiFormat.Money(day.costCoverageGapCents)));
        AddPanelBody(resultPanel, resultText.ToString());

        RectTransform servicePanel = CreatePanel(
            "ServicesPanel",
            0.675f,
            0.00f,
            1.00f,
            0.77f);
        AddPanelTitle(servicePanel, "SERVICIOS");
        StringBuilder services = new StringBuilder();
        if (day.serviceResults != null)
        {
            for (int index = 0; index < day.serviceResults.Count; index++)
            {
                BistroBuilderServiceFinancialResult service = day.serviceResults[index];
                if (service == null) continue;
                services.AppendLine("<b>" +
                    BistroBuilderFinanceUiFormat.MealService(service.mealService) + "</b>");
                services.AppendLine("Ventas " +
                    BistroBuilderFinanceUiFormat.Money(service.revenueCents) +
                    " · margen " +
                    BistroBuilderFinanceUiFormat.Money(service.grossProfitCents));
                services.AppendLine("Cuentas " + service.paidOrderCount +
                    " · " + BistroBuilderFinanceUiFormat.Percent(
                        service.grossMarginBasisPoints));
                services.AppendLine();
            }
        }
        AddPanelBody(servicePanel, services.ToString());
    }

    private void BuildResultsSection()
    {
        BistroBuilderDayFinancialResult day = dashboard.currentDay;

        RectTransform income = CreatePanel("Income", 0.00f, 0.00f, 0.32f, 1.00f);
        AddPanelTitle(income, "VENTAS Y MARGEN");
        StringBuilder a = new StringBuilder();
        a.AppendLine("Ventas cobradas: <b>" +
            BistroBuilderFinanceUiFormat.Money(day.revenueCents) + "</b>");
        a.AppendLine("Ventas con coste reconocido: " +
            BistroBuilderFinanceUiFormat.Money(day.costedSalesCents));
        a.AppendLine("COGS real/estimado: " +
            BistroBuilderFinanceUiFormat.Money(day.productCostCents));
        a.AppendLine("COGS teórico: " +
            BistroBuilderFinanceUiFormat.Money(day.theoreticalProductCostCents));
        a.AppendLine();
        a.AppendLine("Margen bruto: <b>" +
            BistroBuilderFinanceUiFormat.Money(day.grossProfitCents) + "</b>");
        a.AppendLine("Margen bruto %: " +
            BistroBuilderFinanceUiFormat.Percent(day.grossMarginBasisPoints));
        a.AppendLine("Ticket/cuentas hoy: " + day.paidOrderCount + " cuentas");
        a.AppendLine();
        a.AppendLine("Calidad: " + BistroBuilderFinanceUiFormat.CostQuality(day.costQuality));
        a.AppendLine(day.IsCostCoverageComplete
            ? "Cobertura COGS completa"
            : "Cobertura pendiente: " +
              BistroBuilderFinanceUiFormat.Money(day.costCoverageGapCents));
        AddPanelBody(income, a.ToString());

        RectTransform expenses = CreatePanel("Expenses", 0.335f, 0.00f, 0.66f, 1.00f);
        AddPanelTitle(expenses, "GASTOS DEL DÍA");
        StringBuilder b = new StringBuilder();
        AppendAmountLine(b, "Portes proveedor", day.procurementShippingExpensesCents);
        AppendAmountLine(b, "Gastos operativos", day.recurringOperatingExpensesCents);
        AppendAmountLine(b, "Nóminas", day.payrollExpensesCents);
        AppendAmountLine(b, "Marketing", day.marketingExpensesCents);
        AppendAmountLine(b, "Bajas de activos", day.assetDisposalExpensesCents);
        AppendAmountLine(b, "Caducidad / merma", day.inventoryWriteOffExpensesCents);
        AppendAmountLine(b, "Intereses", day.financingInterestExpensesCents);
        AppendAmountLine(b, "Otros gastos", day.otherPeriodExpensesCents);
        b.AppendLine();
        b.AppendLine("Total gastos: <b>" +
            BistroBuilderFinanceUiFormat.Money(day.totalPeriodExpensesCents) + "</b>");
        b.AppendLine("Resultado operativo: <b>" +
            BistroBuilderFinanceUiFormat.Money(day.operatingResultCents, true) + "</b>");
        AddPanelBody(expenses, b.ToString());

        RectTransform services = CreatePanel("ServiceResults", 0.675f, 0.00f, 1.00f, 1.00f);
        AddPanelTitle(services, "CONTRIBUCIÓN POR SERVICIO");
        StringBuilder c = new StringBuilder();
        if (day.serviceResults != null)
        {
            for (int index = 0; index < day.serviceResults.Count; index++)
            {
                BistroBuilderServiceFinancialResult service = day.serviceResults[index];
                if (service == null) continue;
                c.AppendLine("<b>" +
                    BistroBuilderFinanceUiFormat.MealService(service.mealService) + "</b>");
                c.AppendLine("Mesa " + BistroBuilderFinanceUiFormat.Money(service.tableRevenueCents) +
                    " · Barra " + BistroBuilderFinanceUiFormat.Money(service.barRevenueCents));
                c.AppendLine("COGS " + BistroBuilderFinanceUiFormat.Money(service.productCostCents) +
                    " · margen " + BistroBuilderFinanceUiFormat.Money(service.grossProfitCents));
                c.AppendLine(BistroBuilderFinanceUiFormat.Percent(service.grossMarginBasisPoints) +
                    " · " + service.paidOrderCount + " cuentas");
                c.AppendLine();
            }
        }
        c.AppendLine("Los gastos generales no se reparten artificialmente entre servicios.");
        AddPanelBody(services, c.ToString());
    }

    private void BuildCashSection()
    {
        BistroBuilderLiquidityPosition liquidity = dashboard.liquidity;
        BistroBuilderDayFinancialResult day = dashboard.currentDay;

        RectTransform summary = CreatePanel("CashSummary", 0.00f, 0.00f, 0.34f, 1.00f);
        AddPanelTitle(summary, "CAJA Y OBLIGACIONES");
        StringBuilder text = new StringBuilder();
        text.AppendLine("Caja actual: <b>" +
            BistroBuilderFinanceUiFormat.Money(dashboard.cashBalanceCents) + "</b>");
        text.AppendLine("Disponible tras proveedor: " +
            BistroBuilderFinanceUiFormat.Money(
                liquidity.availableCashAfterSupplierCommitmentsCents));
        text.AppendLine();
        text.AppendLine("Entradas hoy: " +
            BistroBuilderFinanceUiFormat.Money(day.totalCashInCents));
        text.AppendLine("Salidas hoy: " +
            BistroBuilderFinanceUiFormat.Money(day.totalCashOutCents));
        text.AppendLine("Variación hoy: <b>" +
            BistroBuilderFinanceUiFormat.Money(day.netCashChangeCents, true) + "</b>");
        text.AppendLine();
        text.AppendLine("Compras proveedor: " +
            BistroBuilderFinanceUiFormat.Money(day.supplierPurchaseCashOutCents));
        text.AppendLine("Inversiones: " +
            BistroBuilderFinanceUiFormat.Money(day.investmentCashOutCents));
        text.AppendLine("Principal deuda: " +
            BistroBuilderFinanceUiFormat.Money(day.debtPrincipalCashOutCents));
        text.AppendLine("Préstamos recibidos: " +
            BistroBuilderFinanceUiFormat.Money(day.loanProceedsCashInCents));
        text.AppendLine("Reventa activos: " +
            BistroBuilderFinanceUiFormat.Money(day.assetResaleCashInCents));
        text.AppendLine();
        text.AppendLine("Proyección " + liquidity.horizonDays + " días: <b>" +
            BistroBuilderFinanceUiFormat.Money(
                liquidity.projectedLiquidityAfterHorizonObligationsCents) + "</b>");
        AddPanelBody(summary, text.ToString());

        RectTransform movementPanel = CreatePanel(
            "MovementPanel",
            0.355f,
            0.00f,
            1.00f,
            1.00f);
        AddPanelTitle(movementPanel, "MOVIMIENTOS DE CAJA RECIENTES");

        RectTransform scrollHost = BistroBuilderMenuEditorUiFactory.CreateRect(
            "MovementScrollHost",
            movementPanel,
            new Vector2(0.03f, 0.05f),
            new Vector2(0.97f, 0.90f),
            Vector2.zero,
            Vector2.zero);
        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "MovementScroll",
            scrollHost,
            out RectTransform content);
        scroll.scrollSensitivity = 34f;

        if (dashboard.recentMovements == null || dashboard.recentMovements.Count == 0)
        {
            Text empty = BistroBuilderMenuEditorUiFactory.CreateText(
                "NoMovements",
                content,
                "Todavía no existen movimientos monetarios.",
                13,
                TextAnchor.MiddleLeft,
                BistroBuilderMenuEditorUiFactory.TextSecondary);
            BistroBuilderMenuEditorUiFactory.SetLayoutHeight(empty, 48f);
            visibleElementCount++;
            return;
        }

        for (int index = 0; index < dashboard.recentMovements.Count; index++)
        {
            BistroBuilderFinanceMovementView movement = dashboard.recentMovements[index];
            if (movement == null) continue;
            Text row = BistroBuilderMenuEditorUiFactory.CreateText(
                "Movement_" + movement.sequence,
                content,
                BuildMovementRow(movement),
                12,
                TextAnchor.MiddleLeft,
                movement.kind == BistroBuilderFinanceTransactionKind.Credit
                    ? BistroBuilderMenuEditorUiFactory.Positive
                    : BistroBuilderMenuEditorUiFactory.TextPrimary);
            row.supportRichText = true;
            BistroBuilderMenuEditorUiFactory.SetLayoutHeight(row, 50f);
            visibleElementCount++;
        }
    }

    private void BuildHistorySection()
    {
        BistroBuilderFinancialPeriodReport period = dashboard.periodReport;

        RectTransform selector = CreatePanel("HistorySelectors", 0.00f, 0.88f, 1.00f, 1.00f);
        CreateSmallButton(selector, "Period7", "7 DÍAS", 0.02f, 0.12f,
            () => SetPeriod(BistroBuilderFinanceDashboardPeriod.Last7Days),
            selectedPeriod == BistroBuilderFinanceDashboardPeriod.Last7Days);
        CreateSmallButton(selector, "Period30", "30 DÍAS", 0.13f, 0.25f,
            () => SetPeriod(BistroBuilderFinanceDashboardPeriod.Last30Days),
            selectedPeriod == BistroBuilderFinanceDashboardPeriod.Last30Days);
        CreateSmallButton(selector, "Period90", "90 DÍAS", 0.26f, 0.38f,
            () => SetPeriod(BistroBuilderFinanceDashboardPeriod.Last90Days),
            selectedPeriod == BistroBuilderFinanceDashboardPeriod.Last90Days);
        CreateSmallButton(selector, "PeriodAll", "TODO", 0.39f, 0.49f,
            () => SetPeriod(BistroBuilderFinanceDashboardPeriod.AllTime),
            selectedPeriod == BistroBuilderFinanceDashboardPeriod.AllTime);

        CreateSmallButton(selector, "MetricRevenue", "INGRESOS", 0.58f, 0.70f,
            () => SetChartMetric(BistroBuilderFinanceChartMetric.Revenue),
            selectedChartMetric == BistroBuilderFinanceChartMetric.Revenue);
        CreateSmallButton(selector, "MetricResult", "RESULTADO", 0.71f, 0.84f,
            () => SetChartMetric(BistroBuilderFinanceChartMetric.OperatingResult),
            selectedChartMetric == BistroBuilderFinanceChartMetric.OperatingResult);
        CreateSmallButton(selector, "MetricCash", "CAJA", 0.85f, 0.96f,
            () => SetChartMetric(BistroBuilderFinanceChartMetric.NetCash),
            selectedChartMetric == BistroBuilderFinanceChartMetric.NetCash);

        RectTransform chartPanel = CreatePanel("HistoryChart", 0.00f, 0.22f, 0.70f, 0.86f);
        AddPanelTitle(
            chartPanel,
            ChartMetricTitle(selectedChartMetric) +
            " · días " + dashboard.periodStartDayIndex + "–" + dashboard.periodEndDayIndex);
        RectTransform chartBackground = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ChartBackground",
            chartPanel,
            new Vector2(0.035f, 0.08f),
            new Vector2(0.965f, 0.86f),
            Vector2.zero,
            Vector2.zero);
        BistroBuilderMenuEditorUiFactory.AddImage(
            chartBackground,
            new Color(0.035f, 0.04f, 0.038f, 0.92f));
        RectTransform chartRect = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Chart",
            chartBackground,
            new Vector2(0.015f, 0.05f),
            new Vector2(0.985f, 0.95f),
            Vector2.zero,
            Vector2.zero);
        BistroBuilderFinanceHistoryChartGraphic chart =
            chartRect.gameObject.AddComponent<BistroBuilderFinanceHistoryChartGraphic>();
        chart.Bind(period.dailyResults, selectedChartMetric);
        visibleElementCount++;

        RectTransform indicators = CreatePanel("HistoryIndicators", 0.715f, 0.22f, 1.00f, 0.86f);
        AddPanelTitle(indicators, "INDICADORES");
        StringBuilder values = new StringBuilder();
        values.AppendLine("Ingresos: <b>" +
            BistroBuilderFinanceUiFormat.Money(period.revenueCents) + "</b>");
        values.AppendLine("Margen bruto: " +
            BistroBuilderFinanceUiFormat.Money(period.grossProfitCents) +
            " · " + BistroBuilderFinanceUiFormat.Percent(period.grossMarginBasisPoints));
        values.AppendLine("Resultado operativo: <b>" +
            BistroBuilderFinanceUiFormat.Money(period.operatingResultCents, true) + "</b>");
        values.AppendLine("Margen operativo: " +
            BistroBuilderFinanceUiFormat.Percent(period.operatingMarginBasisPoints));
        values.AppendLine("Caja neta: " +
            BistroBuilderFinanceUiFormat.Money(period.netCashChangeCents, true));
        values.AppendLine();
        values.AppendLine("Ticket medio: " +
            BistroBuilderFinanceUiFormat.Money(period.averageTicketCents));
        values.AppendLine("Días con servicio: " + period.activeDayCount + "/" + period.dayCount);
        values.AppendLine("Rentables / pérdida: " +
            period.profitableDayCount + " / " + period.lossDayCount);
        values.AppendLine("Mejor ventas: día " + period.bestRevenueDayIndex +
            " · " + BistroBuilderFinanceUiFormat.Money(period.bestRevenueCents));
        values.AppendLine("Mejor franja: " +
            BistroBuilderFinanceUiFormat.MealService(period.topRevenueMealService));
        AddPanelBody(indicators, values.ToString());

        RectTransform comparison = CreatePanel("HistoryComparison", 0.00f, 0.00f, 1.00f, 0.19f);
        AddPanelTitle(comparison, "COMPARACIÓN CON EL PERIODO ANTERIOR");
        Text comparisonText = BistroBuilderMenuEditorUiFactory.CreateText(
            "ComparisonBody",
            comparison,
            BuildComparisonText(dashboard.periodComparison),
            12,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary);
        comparisonText.supportRichText = true;
        SetRect(comparisonText.rectTransform, 0.025f, 0.08f, 0.975f, 0.70f, 0f);
        visibleElementCount++;
    }

    private void BuildFinancingSection()
    {
        EnsureSelectedOffer();

        RectTransform offersPanel = CreatePanel("Offers", 0.00f, 0.00f, 0.44f, 1.00f);
        AddPanelTitle(offersPanel, "OPCIONES DE FINANCIACIÓN");
        RectTransform offerScrollHost = BistroBuilderMenuEditorUiFactory.CreateRect(
            "OfferScrollHost",
            offersPanel,
            new Vector2(0.03f, 0.05f),
            new Vector2(0.97f, 0.90f),
            Vector2.zero,
            Vector2.zero);
        BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "OfferScroll",
            offerScrollHost,
            out RectTransform offerContent);

        if (dashboard.financingOffers != null)
        {
            for (int index = 0; index < dashboard.financingOffers.Count; index++)
            {
                BistroBuilderFinancingOfferView offer = dashboard.financingOffers[index];
                if (offer == null) continue;
                string capturedId = offer.offerId;
                Color baseColor = offer.eligible
                    ? BistroBuilderMenuEditorUiFactory.SurfaceRaised
                    : Color.Lerp(
                        BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                        BistroBuilderMenuEditorUiFactory.Negative,
                        0.18f);
                Button row = BistroBuilderMenuEditorUiFactory.CreateButton(
                    "Offer_" + offer.offerId,
                    offerContent,
                    offer.displayName + "\n" +
                    BistroBuilderFinanceUiFormat.Money(offer.principalCents) +
                    " · " + offer.termDays + " días · " +
                    BistroBuilderFinanceUiFormat.Percent(offer.totalInterestBasisPoints),
                    () => SelectOffer(capturedId),
                    baseColor,
                    12);
                BistroBuilderMenuEditorUiFactory.SetLayoutHeight(row, 68f);
                ApplyPersistentSelection(
                    row,
                    baseColor,
                    string.Equals(
                        selectedOfferId,
                        offer.offerId,
                        StringComparison.Ordinal));
                visibleElementCount++;
            }
        }

        RectTransform detail = CreatePanel("FinancingDetail", 0.455f, 0.43f, 1.00f, 1.00f);
        AddPanelTitle(detail, "DETALLE");
        BistroBuilderFinancingOfferView selected = FindSelectedOffer();
        Text body = BistroBuilderMenuEditorUiFactory.CreateText(
            "OfferDetailBody",
            detail,
            BuildOfferDetail(selected),
            13,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary);
        body.supportRichText = true;
        SetRect(body.rectTransform, 0.04f, 0.22f, 0.96f, 0.84f, 0f);
        visibleElementCount++;

        Button accept = BistroBuilderMenuEditorUiFactory.CreateButton(
            "RequestFinancing",
            detail,
            selected != null && selected.eligible
                ? "SOLICITAR FINANCIACIÓN"
                : "NO DISPONIBLE",
            HandleOpenFinancingConfirmation,
            selected != null && selected.eligible
                ? BistroBuilderMenuEditorUiFactory.Accent
                : BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            12);
        accept.interactable = selected != null && selected.eligible;
        SetRect(accept.GetComponent<RectTransform>(), 0.52f, 0.06f, 0.96f, 0.18f, 0f);
        visibleElementCount++;

        RectTransform debt = CreatePanel("ActiveDebt", 0.455f, 0.00f, 1.00f, 0.40f);
        AddPanelTitle(debt, "DEUDA ACTUAL");
        Text debtText = BistroBuilderMenuEditorUiFactory.CreateText(
            "DebtBody",
            debt,
            BuildDebtText(),
            12,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary);
        debtText.supportRichText = true;
        SetRect(debtText.rectTransform, 0.04f, 0.08f, 0.96f, 0.82f, 0f);
        visibleElementCount++;
    }

    private RectTransform CreatePanel(
        string name,
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        RectTransform panel = BistroBuilderMenuEditorUiFactory.CreateRect(
            name,
            contentRoot,
            new Vector2(minX, minY),
            new Vector2(maxX, maxY),
            Vector2.zero,
            Vector2.zero);
        BistroBuilderMenuEditorUiFactory.AddImage(
            panel,
            new Color(0.055f, 0.065f, 0.062f, 0.88f));
        return panel;
    }

    private void CreateValueCard(
        string name,
        string title,
        string value,
        float minX,
        float minY,
        float maxX,
        float maxY,
        Color valueColor)
    {
        RectTransform card = CreatePanel(name, minX, minY, maxX, maxY);
        Text titleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Label",
            card,
            title,
            10,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary,
            FontStyle.Bold);
        SetRect(titleText.rectTransform, 0.06f, 0.58f, 0.94f, 0.91f, 0f);
        Text valueText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Value",
            card,
            value,
            19,
            TextAnchor.MiddleLeft,
            valueColor,
            FontStyle.Bold);
        valueText.resizeTextForBestFit = true;
        valueText.resizeTextMinSize = 12;
        valueText.resizeTextMaxSize = 19;
        SetRect(valueText.rectTransform, 0.06f, 0.12f, 0.94f, 0.58f, 0f);
        visibleElementCount += 2;
    }

    private void AddPanelTitle(RectTransform panel, string value)
    {
        Text title = BistroBuilderMenuEditorUiFactory.CreateText(
            "PanelTitle",
            panel,
            value,
            13,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary,
            FontStyle.Bold);
        SetRect(title.rectTransform, 0.04f, 0.87f, 0.96f, 0.97f, 0f);
        visibleElementCount++;
    }

    private void AddPanelBody(RectTransform panel, string value)
    {
        Text body = BistroBuilderMenuEditorUiFactory.CreateText(
            "PanelBody",
            panel,
            value,
            12,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary);
        body.supportRichText = true;
        body.resizeTextForBestFit = true;
        body.resizeTextMinSize = 9;
        body.resizeTextMaxSize = 12;
        body.lineSpacing = 0.95f;
        SetRect(body.rectTransform, 0.04f, 0.05f, 0.96f, 0.86f, 0f);
        visibleElementCount++;
    }

    private void CreateSmallButton(
        RectTransform parent,
        string name,
        string label,
        float minX,
        float maxX,
        Action callback,
        bool selected)
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            name,
            parent,
            label,
            () => callback(),
            selected
                ? BistroBuilderMenuEditorUiFactory.SurfaceSelected
                : BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            10);
        SetRect(button.GetComponent<RectTransform>(), minX, 0.20f, maxX, 0.80f, 0f);
        StabilizeButton(button);
        visibleElementCount++;
    }

    private void UpdateHeader()
    {
        if (headerSummary == null || dashboard == null)
        {
            return;
        }
        headerSummary.text =
            "Día " + dashboard.dayIndex +
            " · Caja " + BistroBuilderFinanceUiFormat.Money(dashboard.cashBalanceCents) +
            " · Liquidez " + BistroBuilderFinanceUiFormat.Liquidity(dashboard.liquidity.status) +
            " · Riesgo " + BistroBuilderFinanceUiFormat.Risk(dashboard.stress.riskLevel);
    }

    private void UpdateTabColors()
    {
        SetTabColor(overviewTab, currentSection == BistroBuilderFinancePlayerSection.Overview);
        SetTabColor(resultsTab, currentSection == BistroBuilderFinancePlayerSection.Results);
        SetTabColor(cashTab, currentSection == BistroBuilderFinancePlayerSection.Cash);
        SetTabColor(historyTab, currentSection == BistroBuilderFinancePlayerSection.History);
        SetTabColor(financingTab, currentSection == BistroBuilderFinancePlayerSection.Financing);
    }

    private void SelectSection(BistroBuilderFinancePlayerSection section)
    {
        currentSection = section;
        CloseConfirmation();
        RenderCurrentDashboard(out string error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            SetStatus(error, true);
        }
        ClearEventSystemSelection();
    }

    private void SetPeriod(BistroBuilderFinanceDashboardPeriod period)
    {
        selectedPeriod = period;
        if (!Refresh(true, out string error))
        {
            SetStatus(error, true);
        }
        ClearEventSystemSelection();
    }

    private void SetChartMetric(BistroBuilderFinanceChartMetric metric)
    {
        selectedChartMetric = metric;
        if (!RenderCurrentDashboard(out string error))
        {
            SetStatus(error, true);
        }
        ClearEventSystemSelection();
    }

    private void SelectOffer(string offerId)
    {
        selectedOfferId = offerId ?? string.Empty;
        CloseConfirmation();
        if (!RenderCurrentDashboard(out string error))
        {
            SetStatus(error, true);
        }
        ClearEventSystemSelection();
    }

    private void HandleOpen()
    {
        if (!TryOpenFromInterface(out string error))
        {
            SetStatus(error, true);
        }
    }

    private void HandleOpenFinancingConfirmation()
    {
        if (!OpenFinancingConfirmation(out string error))
        {
            SetStatus(error, true);
        }
    }

    private bool OpenFinancingConfirmation(out string error)
    {
        error = string.Empty;
        BistroBuilderFinancingOfferView offer = FindSelectedOffer();
        if (offer == null)
        {
            error = "Selecciona una opción de financiación.";
            return false;
        }
        if (!offer.eligible)
        {
            error = string.IsNullOrWhiteSpace(offer.ineligibilityReason)
                ? "La financiación seleccionada no está disponible."
                : offer.ineligibilityReason;
            return false;
        }

        financingConfirmationToken =
            BistroBuilderFinanceDashboardService.CreateAcceptanceToken();
        confirmationTitle.text = "CONFIRMAR " + offer.displayName.ToUpperInvariant();
        confirmationBody.text =
            "Vas a recibir <b>" +
            BistroBuilderFinanceUiFormat.Money(offer.principalCents) +
            "</b> en caja.\n\n" +
            "Plazo: " + offer.termDays + " días\n" +
            "Cuotas: " + offer.installmentCount + "\n" +
            "Interés total: " +
            BistroBuilderFinanceUiFormat.Money(offer.totalInterestCents) +
            " (" + BistroBuilderFinanceUiFormat.Percent(
                offer.totalInterestBasisPoints) + ")\n" +
            "Total a devolver: <b>" +
            BistroBuilderFinanceUiFormat.Money(offer.totalPayableCents) +
            "</b>\n\n" +
            "El principal recibido no es beneficio. Los intereses sí se " +
            "reconocen como gasto financiero cuando se pagan.";
        confirmationOverlay.gameObject.SetActive(true);
        confirmationOverlay.SetAsLastSibling();
        ClearEventSystemSelection();
        return true;
    }

    private void HandleConfirmFinancing()
    {
        if (!ConfirmFinancing(out _, out string error))
        {
            SetStatus(error, true);
        }
    }

    private bool ConfirmFinancing(
        out BistroBuilderLoanRecord loan,
        out string error)
    {
        loan = null;
        if (dashboardService == null ||
            string.IsNullOrWhiteSpace(financingConfirmationToken))
        {
            error = "La confirmación financiera no conserva una operación válida.";
            return false;
        }

        string token = financingConfirmationToken;
        if (!dashboardService.TryAcceptFinancingOffer(
                selectedOfferId,
                token,
                out loan,
                out error))
        {
            return false;
        }

        CloseConfirmation();
        SetStatus(
            "Financiación confirmada: " +
            BistroBuilderFinanceUiFormat.Money(loan.principalCents) +
            " recibidos en caja.",
            false);
        Refresh(true);
        return true;
    }

    private void CloseConfirmation()
    {
        financingConfirmationToken = string.Empty;
        if (confirmationOverlay != null)
        {
            confirmationOverlay.gameObject.SetActive(false);
        }
    }

    private void EnsureSelectedOffer()
    {
        if (dashboard == null || dashboard.financingOffers == null)
        {
            selectedOfferId = string.Empty;
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedOfferId))
        {
            for (int index = 0; index < dashboard.financingOffers.Count; index++)
            {
                BistroBuilderFinancingOfferView candidate = dashboard.financingOffers[index];
                if (candidate != null && string.Equals(
                        candidate.offerId,
                        selectedOfferId,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }
        }

        selectedOfferId = dashboard.financingOffers.Count > 0 &&
                          dashboard.financingOffers[0] != null
            ? dashboard.financingOffers[0].offerId
            : string.Empty;
    }

    private BistroBuilderFinancingOfferView FindSelectedOffer()
    {
        if (dashboard == null || dashboard.financingOffers == null)
        {
            return null;
        }
        for (int index = 0; index < dashboard.financingOffers.Count; index++)
        {
            BistroBuilderFinancingOfferView offer = dashboard.financingOffers[index];
            if (offer != null && string.Equals(
                    offer.offerId,
                    selectedOfferId,
                    StringComparison.Ordinal))
            {
                return offer;
            }
        }
        return null;
    }

    private string BuildOfferDetail(BistroBuilderFinancingOfferView offer)
    {
        if (offer == null)
        {
            return "No hay una opción de financiación seleccionada.";
        }

        StringBuilder text = new StringBuilder();
        text.AppendLine("<b>" + offer.displayName + "</b>");
        text.AppendLine();
        text.AppendLine("Principal: " +
            BistroBuilderFinanceUiFormat.Money(offer.principalCents));
        text.AppendLine("Plazo: " + offer.termDays + " días");
        text.AppendLine("Cuotas: " + offer.installmentCount);
        text.AppendLine("Interés total: " +
            BistroBuilderFinanceUiFormat.Money(offer.totalInterestCents) +
            " · " + BistroBuilderFinanceUiFormat.Percent(
                offer.totalInterestBasisPoints));
        text.AppendLine("Total a devolver: " +
            BistroBuilderFinanceUiFormat.Money(offer.totalPayableCents));
        text.AppendLine();
        if (offer.eligible)
        {
            text.AppendLine("<b>Disponible</b> según la liquidez y el riesgo actuales.");
        }
        else
        {
            text.AppendLine("<b>No disponible</b>");
            text.AppendLine(offer.ineligibilityReason);
        }
        return text.ToString();
    }

    private string BuildDebtText()
    {
        StringBuilder text = new StringBuilder();
        BistroBuilderLiquidityPosition liquidity = dashboard.liquidity;
        text.AppendLine("Principal pendiente: " +
            BistroBuilderFinanceUiFormat.Money(liquidity.outstandingPrincipalCents));
        text.AppendLine("Intereses pendientes: " +
            BistroBuilderFinanceUiFormat.Money(liquidity.outstandingInterestCents));
        text.AppendLine("Vence hoy: " +
            BistroBuilderFinanceUiFormat.Money(liquidity.debtDueTodayCents));
        text.AppendLine("Vencido: " +
            BistroBuilderFinanceUiFormat.Money(liquidity.overdueDebtCents));
        text.AppendLine();

        if (dashboard.loans == null || dashboard.loans.Count == 0)
        {
            text.AppendLine("No hay préstamos activos ni históricos.");
            return text.ToString();
        }

        int shown = 0;
        for (int index = dashboard.loans.Count - 1;
             index >= 0 && shown < 4;
             index--)
        {
            BistroBuilderLoanRecord loan = dashboard.loans[index];
            if (loan == null) continue;
            text.AppendLine("<b>" + loan.loanId + " · " +
                BistroBuilderFinanceUiFormat.LoanStatus(loan.status) + "</b>");
            text.AppendLine(BistroBuilderFinanceUiFormat.Money(loan.principalCents) +
                " · interés total " +
                BistroBuilderFinanceUiFormat.Money(loan.totalInterestCents));
            BistroBuilderLoanInstallmentRecord next = FindNextUnpaidInstallment(loan);
            if (next != null)
            {
                text.AppendLine("Próxima cuota: día " + next.dueDayIndex +
                    " · " + BistroBuilderFinanceUiFormat.Money(next.TotalCents) +
                    " · " + BistroBuilderFinanceUiFormat.InstallmentStatus(next.status));
            }
            text.AppendLine();
            shown++;
        }
        return text.ToString();
    }

    private static BistroBuilderLoanInstallmentRecord FindNextUnpaidInstallment(
        BistroBuilderLoanRecord loan)
    {
        if (loan == null || loan.installments == null)
        {
            return null;
        }
        for (int index = 0; index < loan.installments.Count; index++)
        {
            BistroBuilderLoanInstallmentRecord installment = loan.installments[index];
            if (installment != null &&
                installment.status != BistroBuilderLoanInstallmentStatus.Paid)
            {
                return installment;
            }
        }
        return null;
    }

    private static string BuildMovementRow(BistroBuilderFinanceMovementView movement)
    {
        string sign = movement.kind == BistroBuilderFinanceTransactionKind.Credit
            ? "+"
            : "−";
        string description = string.IsNullOrWhiteSpace(movement.description)
            ? movement.displayLabel
            : movement.description;
        return "<b>" + sign + BistroBuilderFinanceUiFormat.Money(movement.amountCents) +
               "</b>   " + movement.displayLabel +
               "\nDía " + movement.dayIndex + " · " +
               BistroBuilderFinanceUiFormat.Clock(movement.minuteOfDay) +
               " · " + description;
    }

    private static string BuildComparisonText(
        BistroBuilderFinancialPeriodComparison comparison)
    {
        if (comparison == null)
        {
            return "Todavía no existe un periodo anterior completo de la misma duración.";
        }

        return
            "Ingresos: <b>" + BistroBuilderFinanceUiFormat.Trend(comparison.revenueTrend) +
            "</b> · " + BistroBuilderFinanceUiFormat.Money(comparison.revenueDeltaCents, true) +
            "     Resultado: <b>" +
            BistroBuilderFinanceUiFormat.Trend(comparison.operatingResultTrend) +
            "</b> · " + BistroBuilderFinanceUiFormat.Money(
                comparison.operatingResultDeltaCents, true) +
            "     Ticket: <b>" +
            BistroBuilderFinanceUiFormat.Trend(comparison.averageTicketTrend) +
            "</b> · " + BistroBuilderFinanceUiFormat.Money(
                comparison.averageTicketDeltaCents, true) +
            "     Caja: <b>" +
            BistroBuilderFinanceUiFormat.Trend(comparison.netCashTrend) +
            "</b> · " + BistroBuilderFinanceUiFormat.Money(
                comparison.netCashChangeDeltaCents, true);
    }

    private static string ChartMetricTitle(BistroBuilderFinanceChartMetric metric)
    {
        switch (metric)
        {
            case BistroBuilderFinanceChartMetric.OperatingResult:
                return "RESULTADO OPERATIVO";
            case BistroBuilderFinanceChartMetric.NetCash:
                return "VARIACIÓN DE CAJA";
            default:
                return "INGRESOS";
        }
    }

    private static void AppendAmountLine(
        StringBuilder builder,
        string label,
        long cents)
    {
        builder.AppendLine(label + ": " + BistroBuilderFinanceUiFormat.Money(cents));
    }

    private static Color MoneyColor(long value)
    {
        if (value > 0L) return BistroBuilderMenuEditorUiFactory.Positive;
        if (value < 0L) return BistroBuilderMenuEditorUiFactory.Negative;
        return BistroBuilderMenuEditorUiFactory.TextPrimary;
    }

    private static Color RiskColor(BistroBuilderFinancialRiskLevel risk)
    {
        switch (risk)
        {
            case BistroBuilderFinancialRiskLevel.Low:
                return BistroBuilderMenuEditorUiFactory.Positive;
            case BistroBuilderFinancialRiskLevel.Moderate:
                return BistroBuilderMenuEditorUiFactory.Warning;
            case BistroBuilderFinancialRiskLevel.High:
            case BistroBuilderFinancialRiskLevel.Severe:
                return BistroBuilderMenuEditorUiFactory.Negative;
            default:
                return BistroBuilderMenuEditorUiFactory.TextPrimary;
        }
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
        if (editInteractionController != null)
        {
            editInteractionController.enabled = editWasEnabled;
        }
        inputGateApplied = false;
    }

    private void SetVisible(bool visible)
    {
        if (modalRoot != null)
        {
            modalRoot.gameObject.SetActive(visible);
        }
    }

    private void SetStatus(string value, bool error)
    {
        if (statusText == null) return;
        statusText.text = value ?? string.Empty;
        statusText.color = error
            ? BistroBuilderMenuEditorUiFactory.Negative
            : BistroBuilderMenuEditorUiFactory.TextSecondary;
    }

    private void HandleDashboardChanged()
    {
        refreshQueued = true;
    }

    private void Subscribe()
    {
        if (subscribed) return;
        ResolveDependencies();
        if (dashboardService != null)
        {
            dashboardService.DashboardChanged += HandleDashboardChanged;
        }
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (dashboardService != null)
        {
            dashboardService.DashboardChanged -= HandleDashboardChanged;
        }
        subscribed = false;
    }

    private void ResolveDependencies()
    {
        if (dashboardService == null)
        {
            dashboardService = FindFirstObjectByType<BistroBuilderFinanceDashboardService>();
        }
        if (cameraController == null)
        {
            cameraController =
                FindFirstObjectByType<BistroBuilderProfessionalCameraController>();
        }
        if (editInteractionController == null)
        {
            editInteractionController =
                FindFirstObjectByType<RestaurantEditInteractionController>();
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
        if (text != null)
        {
            text.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private static void ApplyPersistentSelection(
        Button button,
        Color baseColor,
        bool selected)
    {
        if (button == null || button.image == null) return;
        button.image.color = selected
            ? Color.Lerp(baseColor, BistroBuilderMenuEditorUiFactory.Accent, 0.30f)
            : baseColor;
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = TextAnchor.MiddleLeft;
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

    private static void ClearChildren(RectTransform root)
    {
        if (root == null) return;
        for (int index = root.childCount - 1; index >= 0; index--)
        {
            Transform child = root.GetChild(index);
            if (child == null) continue;
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void SetRect(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY,
        float offset)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(offset, offset);
        rect.offsetMax = new Vector2(-offset, -offset);
    }

    private static void SetRect(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }

    private void OnValidate()
    {
        maximumRecentMovements = Mathf.Clamp(maximumRecentMovements, 10, 500);
        ResolveDependencies();
    }
#endif
}
