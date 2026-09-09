using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla jugable de Marketing. Presentation gestiona filtros, selección y
/// confirmaciones; todas las mutaciones pasan por MarketingPlayerFacade.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Marketing/Marketing Player Screen")]
public sealed class BistroBuilderMarketingPlayerScreen : MonoBehaviour
{
    private enum ViewMode { Catalog = 0, Active = 1 }

    [Header("Presentation")]
    [SerializeField] private BistroBuilderMarketingPlayerFacade facade;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button catalogTabButton;
    [SerializeField] private Button activeTabButton;
    [SerializeField] private TMP_InputField searchInput;

    [Header("Filtros")]
    [SerializeField] private Button allFilterButton;
    [SerializeField] private Button localFilterButton;
    [SerializeField] private Button promotionsFilterButton;
    [SerializeField] private Button digitalFilterButton;
    [SerializeField] private Button pressFilterButton;
    [SerializeField] private Button eventsFilterButton;
    [SerializeField] private Button loyaltyFilterButton;
    [SerializeField] private Button menuFilterButton;

    [Header("Listado")]
    [SerializeField] private RectTransform listContent;
    [SerializeField] private BistroBuilderMarketingPlayerRowView rowPrefab;
    [SerializeField] private TMP_Text emptyStateText;

    [Header("Cabecera")]
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text reputationText;
    [SerializeField] private TMP_Text recurringText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Detalle")]
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailFamilyText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text detailEffectsText;
    [SerializeField] private TMP_Text detailMetaText;
    [SerializeField] private TMP_Text detailRequirementsText;
    [SerializeField] private GameObject targetPanel;
    [SerializeField] private TMP_Text targetValueText;
    [SerializeField] private Button targetPreviousButton;
    [SerializeField] private Button targetNextButton;
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private TMP_Text primaryActionButtonText;

    private readonly List<BistroBuilderMarketingPlayerRowView> rows =
        new List<BistroBuilderMarketingPlayerRowView>();
    private BistroBuilderMarketingPlayerUiSnapshot snapshot;
    private ViewMode viewMode = ViewMode.Catalog;
    private int familyFilter = -1;
    private string selectedCampaignId = string.Empty;
    private string selectedInstanceId = string.Empty;
    private int selectedTargetIndex;
    private bool cancelArmed;
    private bool bound;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;
    public string SelectedCampaignId => selectedCampaignId;
    public string SelectedInstanceId => selectedInstanceId;

    private void Awake()
    {
        CacheDependencies();
        Bind();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        CacheDependencies();
        Bind();
        if (facade != null)
        {
            facade.ViewInvalidated -= HandleViewInvalidated;
            facade.ViewInvalidated += HandleViewInvalidated;
        }
    }

    private void OnDisable()
    {
        if (facade != null) facade.ViewInvalidated -= HandleViewInvalidated;
        Unbind();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (facade == null || panelRoot == null || canvasGroup == null ||
            closeButton == null || catalogTabButton == null ||
            activeTabButton == null || searchInput == null ||
            listContent == null || rowPrefab == null || emptyStateText == null)
        {
            error = "La estructura principal de la UI de Marketing está incompleta.";
            return false;
        }

        if (allFilterButton == null || localFilterButton == null ||
            promotionsFilterButton == null || digitalFilterButton == null ||
            pressFilterButton == null || eventsFilterButton == null ||
            loyaltyFilterButton == null || menuFilterButton == null)
        {
            error = "La barra de filtros de Marketing está incompleta.";
            return false;
        }

        if (summaryText == null || reputationText == null || recurringText == null ||
            feedbackText == null || detailNameText == null ||
            detailFamilyText == null || detailDescriptionText == null ||
            detailEffectsText == null || detailMetaText == null ||
            detailRequirementsText == null)
        {
            error = "Los textos de la UI de Marketing están incompletos.";
            return false;
        }

        if (targetPanel == null || targetValueText == null ||
            targetPreviousButton == null || targetNextButton == null ||
            primaryActionButton == null || primaryActionButtonText == null)
        {
            error = "Los controles de acción de Marketing están incompletos.";
            return false;
        }

        if (!rowPrefab.ValidateConfiguration(out error) ||
            !facade.ValidateConfiguration(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public void Show()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        cancelArmed = false;
        Refresh();
    }

    public void Hide()
    {
        cancelArmed = false;
        ShowFeedback(string.Empty);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void ShowCatalog()
    {
        viewMode = ViewMode.Catalog;
        cancelArmed = false;
        Refresh();
    }

    public void ShowActiveCampaigns()
    {
        viewMode = ViewMode.Active;
        cancelArmed = false;
        Refresh();
    }

    public void FilterAll() => SetFamilyFilter(-1);
    public void FilterLocal() => SetFamilyFilter((int)BistroBuilderMarketingCampaignType.LocalAwareness);
    public void FilterPromotions() => SetFamilyFilter((int)BistroBuilderMarketingCampaignType.Promotions);
    public void FilterDigital() => SetFamilyFilter((int)BistroBuilderMarketingCampaignType.Digital);
    public void FilterPress() => SetFamilyFilter((int)BistroBuilderMarketingCampaignType.InfluencersPress);
    public void FilterEvents() => SetFamilyFilter((int)BistroBuilderMarketingCampaignType.EventsExperiences);
    public void FilterLoyalty() => SetFamilyFilter((int)BistroBuilderMarketingCampaignType.LoyaltyReferral);
    public void FilterMenu() => SetFamilyFilter((int)BistroBuilderMarketingCampaignType.MenuDishPromotion);

    private void SetFamilyFilter(int value)
    {
        familyFilter = value;
        cancelArmed = false;
        if (viewMode != ViewMode.Catalog) viewMode = ViewMode.Catalog;
        Refresh();
    }

    public void Refresh()
    {
        if (!IsVisible) return;
        if (!ValidateConfiguration(out string error))
        {
            ShowFeedback(error);
            return;
        }

        if (!facade.TryBuildSnapshot(out BistroBuilderMarketingPlayerUiSnapshot built,
                out error))
        {
            ShowFeedback(error);
            return;
        }

        snapshot = built;
        PreserveSelection();
        RebuildRows();
        RenderHeader();
        RenderDetail();
        ApplyTabState();
        ShowFeedback(string.Empty);
    }

    private void PreserveSelection()
    {
        if (snapshot == null) return;
        if (viewMode == ViewMode.Catalog)
        {
            if (FindCampaign(selectedCampaignId) == null ||
                !MatchesCurrentFilter(FindCampaign(selectedCampaignId)))
                selectedCampaignId = FirstVisibleCampaignId();
        }
        else if (FindActive(selectedInstanceId) == null)
        {
            selectedInstanceId = snapshot.activeCampaigns.Count > 0
                ? snapshot.activeCampaigns[0].instanceId
                : string.Empty;
        }
    }

    private void RebuildRows()
    {
        ClearRows();
        int visible = 0;
        if (viewMode == ViewMode.Catalog)
        {
            for (int index = 0; index < snapshot.campaigns.Count; index++)
            {
                BistroBuilderMarketingPlayerCampaignRow campaign = snapshot.campaigns[index];
                if (!MatchesCurrentFilter(campaign)) continue;
                BistroBuilderMarketingPlayerRowView row = Instantiate(rowPrefab, listContent);
                row.gameObject.SetActive(true);
                row.BindCampaign(
                    campaign,
                    string.Equals(campaign.campaignId, selectedCampaignId, StringComparison.Ordinal),
                    HandleCampaignSelected);
                rows.Add(row);
                visible++;
            }
        }
        else
        {
            for (int index = 0; index < snapshot.activeCampaigns.Count; index++)
            {
                BistroBuilderMarketingPlayerActiveRow campaign = snapshot.activeCampaigns[index];
                BistroBuilderMarketingPlayerRowView row = Instantiate(rowPrefab, listContent);
                row.gameObject.SetActive(true);
                row.BindActive(
                    campaign,
                    string.Equals(campaign.instanceId, selectedInstanceId, StringComparison.Ordinal),
                    HandleActiveSelected);
                rows.Add(row);
                visible++;
            }
        }

        emptyStateText.gameObject.SetActive(visible == 0);
        emptyStateText.text = viewMode == ViewMode.Catalog
            ? "No hay campañas que coincidan con el filtro actual."
            : "No hay campañas activas en este momento.";
    }

    private void RenderHeader()
    {
        summaryText.text = snapshot.campaigns.Count + " campañas · " +
            snapshot.activeCampaignCount + " activas · día " + snapshot.currentDayIndex;
        reputationText.text = "Reputación " + snapshot.reputationPoints +
            " pts · " + FormatBasisPoints(snapshot.reputationDemandBasisPoints) +
            " demanda duradera";
        recurringText.text = "Clientes recurrentes · " +
            snapshot.recurrentCohortCount + " cohorte(s) registrada(s)";
    }

    private void RenderDetail()
    {
        if (viewMode == ViewMode.Catalog)
            RenderCampaignDetail(FindCampaign(selectedCampaignId));
        else
            RenderActiveDetail(FindActive(selectedInstanceId));
    }

    private void RenderCampaignDetail(BistroBuilderMarketingPlayerCampaignRow row)
    {
        cancelArmed = false;
        if (row == null)
        {
            ClearDetail("Selecciona una campaña");
            return;
        }

        detailNameText.text = row.displayName;
        detailFamilyText.text = BistroBuilderMarketingPlayerRowView.FamilyLabel(row.type);
        detailDescriptionText.text = row.description;
        detailEffectsText.text = row.effectsSummary;
        detailMetaText.text = FormatMoney(row.costCents) + " · " +
            row.durationDays + " día(s) · nivel " + row.minProgressionLevel;
        detailRequirementsText.text = string.IsNullOrWhiteSpace(row.blockedReason)
            ? "Disponible para contratar."
            : row.blockedReason;

        ConfigureTargetControls(row);
        ConfigureStartAction(row);
    }

    private void RenderActiveDetail(BistroBuilderMarketingPlayerActiveRow row)
    {
        if (row == null)
        {
            ClearDetail("No hay campañas activas");
            return;
        }

        detailNameText.text = row.displayName;
        detailFamilyText.text = BistroBuilderMarketingPlayerRowView.FamilyLabel(row.type);
        detailDescriptionText.text = "Campaña contratada. Sus efectos se evalúan en los sistemas propietarios correspondientes.";
        detailEffectsText.text = row.effectsSummary;
        detailMetaText.text = FormatMoney(row.paidCostCents) + " pagados · " +
            row.daysRemaining + " día(s) restante(s)";
        detailRequirementsText.text = "Objetivo: " + row.targetDisplayName +
            " · activa del día " + row.startDayIndex + " al " +
            (row.endDayExclusive - 1) + ".";

        targetPanel.SetActive(false);
        primaryActionButton.interactable = true;
        primaryActionButtonText.text = cancelArmed
            ? "Confirmar cancelación"
            : "Cancelar campaña";
    }

    private void ClearDetail(string title)
    {
        detailNameText.text = title;
        detailFamilyText.text = string.Empty;
        detailDescriptionText.text = string.Empty;
        detailEffectsText.text = string.Empty;
        detailMetaText.text = string.Empty;
        detailRequirementsText.text = string.Empty;
        targetPanel.SetActive(false);
        primaryActionButton.interactable = false;
        primaryActionButtonText.text = viewMode == ViewMode.Catalog
            ? "Iniciar campaña"
            : "Cancelar campaña";
    }

    private void ConfigureTargetControls(BistroBuilderMarketingPlayerCampaignRow row)
    {
        if (row.targetKind == BistroBuilderMarketingTargetKind.None)
        {
            selectedTargetIndex = 0;
            targetPanel.SetActive(false);
            return;
        }

        List<BistroBuilderMarketingPlayerTargetOption> targets =
            snapshot.TargetsFor(row.targetKind);
        bool available = targets != null && targets.Count > 0;
        targetPanel.SetActive(true);
        selectedTargetIndex = available
            ? Mathf.Clamp(selectedTargetIndex, 0, targets.Count - 1)
            : 0;
        targetValueText.text = available
            ? targets[selectedTargetIndex].displayName
            : "Sin objetivos disponibles";
        targetPreviousButton.interactable = available && targets.Count > 1;
        targetNextButton.interactable = available && targets.Count > 1;
    }

    private void ConfigureStartAction(BistroBuilderMarketingPlayerCampaignRow row)
    {
        string targetId = CurrentTargetId(row);
        string duplicateReason = FindDuplicateReason(row.campaignId, targetId);
        string block = !string.IsNullOrWhiteSpace(row.blockedReason)
            ? row.blockedReason
            : duplicateReason;
        bool targetReady = row.targetKind == BistroBuilderMarketingTargetKind.None ||
            !string.IsNullOrWhiteSpace(targetId);
        primaryActionButton.interactable = string.IsNullOrWhiteSpace(block) && targetReady;
        primaryActionButtonText.text = !string.IsNullOrWhiteSpace(duplicateReason)
            ? "Ya está activa"
            : "Iniciar · " + FormatMoney(row.costCents);
        if (!string.IsNullOrWhiteSpace(block)) detailRequirementsText.text = block;
    }

    private void HandlePrimaryAction()
    {
        if (viewMode == ViewMode.Catalog)
        {
            StartSelectedCampaign();
            return;
        }
        CancelSelectedActive();
    }

    public void StartSelectedCampaign()
    {
        BistroBuilderMarketingPlayerCampaignRow row = FindCampaign(selectedCampaignId);
        if (row == null)
        {
            ShowFeedback("Selecciona una campaña antes de contratarla.");
            return;
        }

        string targetId = CurrentTargetId(row);
        if (!facade.TryStartCampaign(
                row.campaignId,
                targetId,
                out BistroBuilderMarketingCampaignRecord started,
                out string error))
        {
            ShowFeedback(error);
            RefreshDetailOnly();
            return;
        }

        selectedInstanceId = started != null ? started.instanceId : string.Empty;
        cancelArmed = false;
        Refresh();
        ShowFeedback("Campaña iniciada: " + row.displayName + ".");
    }

    public void CancelSelectedActive()
    {
        BistroBuilderMarketingPlayerActiveRow row = FindActive(selectedInstanceId);
        if (row == null)
        {
            ShowFeedback("Selecciona una campaña activa antes de cancelarla.");
            return;
        }

        if (!cancelArmed)
        {
            cancelArmed = true;
            primaryActionButtonText.text = "Confirmar cancelación";
            ShowFeedback(
                "La cancelación detiene efectos futuros. El coste y los efectos ya materializados no se revierten.");
            return;
        }

        if (!facade.TryCancelCampaign(
                row.instanceId,
                out _,
                out string error))
        {
            cancelArmed = false;
            ShowFeedback(error);
            Refresh();
            return;
        }

        selectedInstanceId = string.Empty;
        cancelArmed = false;
        Refresh();
        ShowFeedback("Campaña cancelada. No se ha generado ningún reembolso.");
    }

    private void HandleTargetPrevious()
    {
        ChangeTarget(-1);
    }

    private void HandleTargetNext()
    {
        ChangeTarget(1);
    }

    private void ChangeTarget(int delta)
    {
        BistroBuilderMarketingPlayerCampaignRow row = FindCampaign(selectedCampaignId);
        if (row == null || row.targetKind == BistroBuilderMarketingTargetKind.None)
            return;
        List<BistroBuilderMarketingPlayerTargetOption> targets =
            snapshot.TargetsFor(row.targetKind);
        if (targets == null || targets.Count == 0) return;
        selectedTargetIndex = (selectedTargetIndex + delta + targets.Count) % targets.Count;
        cancelArmed = false;
        RenderCampaignDetail(row);
    }

    private void HandleCampaignSelected(string campaignId)
    {
        selectedCampaignId = campaignId ?? string.Empty;
        selectedTargetIndex = 0;
        cancelArmed = false;
        Refresh();
    }

    private void HandleActiveSelected(string instanceId)
    {
        selectedInstanceId = instanceId ?? string.Empty;
        cancelArmed = false;
        Refresh();
    }

    private void HandleSearchChanged(string _)
    {
        cancelArmed = false;
        if (IsVisible) Refresh();
    }

    private bool MatchesCurrentFilter(BistroBuilderMarketingPlayerCampaignRow row)
    {
        if (row == null) return false;
        if (familyFilter >= 0 && (int)row.type != familyFilter) return false;
        string query = searchInput != null ? searchInput.text.Trim() : string.Empty;
        if (query.Length == 0) return true;
        return ContainsIgnoreCase(row.displayName, query) ||
               ContainsIgnoreCase(row.description, query) ||
               ContainsIgnoreCase(row.effectsSummary, query) ||
               ContainsIgnoreCase(row.campaignId, query);
    }

    private string FirstVisibleCampaignId()
    {
        if (snapshot == null) return string.Empty;
        for (int index = 0; index < snapshot.campaigns.Count; index++)
            if (MatchesCurrentFilter(snapshot.campaigns[index]))
                return snapshot.campaigns[index].campaignId;
        return string.Empty;
    }

    private string CurrentTargetId(BistroBuilderMarketingPlayerCampaignRow row)
    {
        if (row == null || row.targetKind == BistroBuilderMarketingTargetKind.None)
            return string.Empty;
        List<BistroBuilderMarketingPlayerTargetOption> targets = snapshot.TargetsFor(row.targetKind);
        if (targets == null || targets.Count == 0) return string.Empty;
        selectedTargetIndex = Mathf.Clamp(selectedTargetIndex, 0, targets.Count - 1);
        return targets[selectedTargetIndex].targetId;
    }

    private string FindDuplicateReason(string campaignId, string targetId)
    {
        if (snapshot == null) return string.Empty;
        for (int index = 0; index < snapshot.activeCampaigns.Count; index++)
        {
            BistroBuilderMarketingPlayerActiveRow active = snapshot.activeCampaigns[index];
            if (active != null &&
                string.Equals(active.campaignId, campaignId, StringComparison.Ordinal) &&
                string.Equals(active.targetId ?? string.Empty, targetId ?? string.Empty,
                    StringComparison.Ordinal))
                return "Esta campaña ya está activa para el objetivo seleccionado.";
        }
        return string.Empty;
    }

    private BistroBuilderMarketingPlayerCampaignRow FindCampaign(string campaignId)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(campaignId)) return null;
        for (int index = 0; index < snapshot.campaigns.Count; index++)
        {
            BistroBuilderMarketingPlayerCampaignRow row = snapshot.campaigns[index];
            if (row != null && string.Equals(
                    row.campaignId, campaignId, StringComparison.Ordinal))
                return row;
        }
        return null;
    }

    private BistroBuilderMarketingPlayerActiveRow FindActive(string instanceId)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(instanceId)) return null;
        for (int index = 0; index < snapshot.activeCampaigns.Count; index++)
        {
            BistroBuilderMarketingPlayerActiveRow row = snapshot.activeCampaigns[index];
            if (row != null && string.Equals(
                    row.instanceId, instanceId, StringComparison.Ordinal))
                return row;
        }
        return null;
    }

    private void RefreshDetailOnly()
    {
        if (snapshot != null) RenderDetail();
    }

    private void ApplyTabState()
    {
        catalogTabButton.interactable = viewMode != ViewMode.Catalog;
        activeTabButton.interactable = viewMode != ViewMode.Active;
        bool catalog = viewMode == ViewMode.Catalog;
        allFilterButton.gameObject.SetActive(catalog);
        localFilterButton.gameObject.SetActive(catalog);
        promotionsFilterButton.gameObject.SetActive(catalog);
        digitalFilterButton.gameObject.SetActive(catalog);
        pressFilterButton.gameObject.SetActive(catalog);
        eventsFilterButton.gameObject.SetActive(catalog);
        loyaltyFilterButton.gameObject.SetActive(catalog);
        menuFilterButton.gameObject.SetActive(catalog);
        searchInput.gameObject.SetActive(catalog);
    }

    private void ClearRows()
    {
        for (int index = 0; index < rows.Count; index++)
            if (rows[index] != null) Destroy(rows[index].gameObject);
        rows.Clear();
    }

    private void HandleViewInvalidated()
    {
        if (isActiveAndEnabled && IsVisible) Refresh();
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText != null) feedbackText.text = message ?? string.Empty;
    }

    private void Bind()
    {
        if (bound) return;
        closeButton?.onClick.AddListener(Hide);
        catalogTabButton?.onClick.AddListener(ShowCatalog);
        activeTabButton?.onClick.AddListener(ShowActiveCampaigns);
        allFilterButton?.onClick.AddListener(FilterAll);
        localFilterButton?.onClick.AddListener(FilterLocal);
        promotionsFilterButton?.onClick.AddListener(FilterPromotions);
        digitalFilterButton?.onClick.AddListener(FilterDigital);
        pressFilterButton?.onClick.AddListener(FilterPress);
        eventsFilterButton?.onClick.AddListener(FilterEvents);
        loyaltyFilterButton?.onClick.AddListener(FilterLoyalty);
        menuFilterButton?.onClick.AddListener(FilterMenu);
        targetPreviousButton?.onClick.AddListener(HandleTargetPrevious);
        targetNextButton?.onClick.AddListener(HandleTargetNext);
        primaryActionButton?.onClick.AddListener(HandlePrimaryAction);
        searchInput?.onValueChanged.AddListener(HandleSearchChanged);
        bound = true;
    }

    private void Unbind()
    {
        if (!bound) return;
        closeButton?.onClick.RemoveListener(Hide);
        catalogTabButton?.onClick.RemoveListener(ShowCatalog);
        activeTabButton?.onClick.RemoveListener(ShowActiveCampaigns);
        allFilterButton?.onClick.RemoveListener(FilterAll);
        localFilterButton?.onClick.RemoveListener(FilterLocal);
        promotionsFilterButton?.onClick.RemoveListener(FilterPromotions);
        digitalFilterButton?.onClick.RemoveListener(FilterDigital);
        pressFilterButton?.onClick.RemoveListener(FilterPress);
        eventsFilterButton?.onClick.RemoveListener(FilterEvents);
        loyaltyFilterButton?.onClick.RemoveListener(FilterLoyalty);
        menuFilterButton?.onClick.RemoveListener(FilterMenu);
        targetPreviousButton?.onClick.RemoveListener(HandleTargetPrevious);
        targetNextButton?.onClick.RemoveListener(HandleTargetNext);
        primaryActionButton?.onClick.RemoveListener(HandlePrimaryAction);
        searchInput?.onValueChanged.RemoveListener(HandleSearchChanged);
        bound = false;
    }

    private void CacheDependencies()
    {
        if (facade == null) TryGetComponent(out facade);
    }

    private static bool ContainsIgnoreCase(string source, string query)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string FormatMoney(long cents)
    {
        long absolute = Math.Abs(cents);
        return (cents < 0 ? "−" : string.Empty) +
               (absolute / 100L) + "," +
               (absolute % 100L).ToString("00") + " €";
    }

    private static string FormatBasisPoints(int basisPoints)
    {
        int absolute = Math.Abs(basisPoints);
        string sign = basisPoints > 0 ? "+" : basisPoints < 0 ? "−" : string.Empty;
        if (absolute % 100 == 0)
            return sign + (absolute / 100) + " %";
        return sign + (absolute / 100) + "," +
               (absolute % 100).ToString("00") + " %";
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
