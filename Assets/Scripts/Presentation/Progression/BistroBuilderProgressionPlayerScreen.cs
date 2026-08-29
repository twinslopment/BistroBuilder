using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pantalla jugable 9E de mejoras, progreso e hitos.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Progression/Progression Player Screen")]
public sealed class BistroBuilderProgressionPlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderProgressionPlayerFacade facade;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button allButton;
    [SerializeField] private Button diningButton;
    [SerializeField] private Button kitchenButton;
    [SerializeField] private Button terraceButton;
    [SerializeField] private Button barButton;
    [SerializeField] private Button infrastructureButton;
    [SerializeField] private Button ambienceButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text milestoneText;
    [SerializeField] private TMP_Text listText;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text detailMetaText;
    [SerializeField] private TMP_Text detailEffectsText;
    [SerializeField] private TMP_Text detailRequirementsText;
    [SerializeField] private TMP_Text feedbackText;

    private readonly List<BistroBuilderProgressionPlayerUpgradeRow> visible =
        new List<BistroBuilderProgressionPlayerUpgradeRow>();
    private BistroBuilderProgressionPlayerUiSnapshot snapshot;
    private int categoryFilter = -1;
    private int selectedIndex;
    private bool bound;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;
    public string SelectedUpgradeId =>
        selectedIndex >= 0 && selectedIndex < visible.Count
            ? visible[selectedIndex].upgradeId
            : string.Empty;

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
            facade.ViewInvalidated -= HandleInvalidated;
            facade.ViewInvalidated += HandleInvalidated;
        }
    }

    private void OnDisable()
    {
        if (facade != null) facade.ViewInvalidated -= HandleInvalidated;
        Unbind();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (facade == null || panelRoot == null || canvasGroup == null ||
            closeButton == null || allButton == null || diningButton == null ||
            kitchenButton == null || terraceButton == null || barButton == null ||
            infrastructureButton == null || ambienceButton == null ||
            previousButton == null || nextButton == null || buyButton == null ||
            buyButtonText == null || summaryText == null || milestoneText == null ||
            listText == null || detailNameText == null || detailDescriptionText == null ||
            detailMetaText == null || detailEffectsText == null ||
            detailRequirementsText == null || feedbackText == null)
        {
            error = "La UI jugable 9E está incompleta.";
            return false;
        }
        return facade.ValidateConfiguration(out error);
    }

    public void Show()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        Refresh();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Refresh()
    {
        if (!IsVisible) return;
        if (!ValidateConfiguration(out string error) ||
            !facade.TryBuildSnapshot(out snapshot, out error))
        {
            feedbackText.text = error;
            return;
        }
        RebuildVisible();
        Render();
    }

    public void FilterAll() => SetFilter(-1);
    public void FilterDining() => SetFilter((int)BistroBuilderUpgradeCategory.DiningRoom);
    public void FilterKitchen() => SetFilter((int)BistroBuilderUpgradeCategory.Kitchen);
    public void FilterTerrace() => SetFilter((int)BistroBuilderUpgradeCategory.Terrace);
    public void FilterBar() => SetFilter((int)BistroBuilderUpgradeCategory.Bar);
    public void FilterInfrastructure() => SetFilter((int)BistroBuilderUpgradeCategory.Infrastructure);
    public void FilterAmbience() => SetFilter((int)BistroBuilderUpgradeCategory.AmbienceIdentity);

    public void SelectPrevious()
    {
        if (visible.Count == 0) return;
        selectedIndex = (selectedIndex - 1 + visible.Count) % visible.Count;
        Render();
    }

    public void SelectNext()
    {
        if (visible.Count == 0) return;
        selectedIndex = (selectedIndex + 1) % visible.Count;
        Render();
    }

    public bool BuySelected(out string error)
    {
        error = string.Empty;
        if (visible.Count == 0 || selectedIndex < 0 || selectedIndex >= visible.Count)
        {
            error = "No hay una mejora seleccionada.";
            feedbackText.text = error;
            return false;
        }
        BistroBuilderProgressionPlayerUpgradeRow row = visible[selectedIndex];
        if (!facade.TryPurchaseUpgrade(row.upgradeId, out _, out error))
        {
            feedbackText.text = error;
            return false;
        }
        Refresh();
        feedbackText.text = "Mejora adquirida: " + row.displayName + ".";
        return true;
    }

    private void SetFilter(int filter)
    {
        categoryFilter = filter;
        selectedIndex = 0;
        if (IsVisible) Refresh();
    }

    private void RebuildVisible()
    {
        visible.Clear();
        if (snapshot?.upgrades != null)
            for (int i = 0; i < snapshot.upgrades.Count; i++)
            {
                BistroBuilderProgressionPlayerUpgradeRow row = snapshot.upgrades[i];
                if (row != null && (categoryFilter < 0 || (int)row.category == categoryFilter))
                    visible.Add(row);
            }
        selectedIndex = visible.Count == 0 ? -1 : Mathf.Clamp(selectedIndex, 0, visible.Count - 1);
    }

    private void Render()
    {
        if (snapshot == null) return;
        summaryText.text = "Nivel " + snapshot.progressionLevel + " · " +
            snapshot.purchasedCount + "/" + snapshot.upgrades.Count + " mejoras · " +
            FormatMoney(snapshot.availableCashCents) + " disponibles · reputación " +
            (snapshot.reputationBasisPoints / 100f).ToString("0.0") + " %";
        milestoneText.text = "SIGUIENTE HITO · " + snapshot.nextMilestoneName +
            " · nivel " + snapshot.nextMilestoneTargetLevel + "\n" +
            snapshot.milestoneRequirements;
        RenderList();
        RenderDetail();
        feedbackText.text = string.Empty;
    }

    private void RenderList()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < visible.Count; i++)
        {
            BistroBuilderProgressionPlayerUpgradeRow row = visible[i];
            builder.Append(i == selectedIndex ? "▶ " : "  ")
                .Append(StateGlyph(row.state)).Append(' ')
                .Append(row.displayName).Append("  ·  ")
                .Append(FormatMoney(row.costCents));
            if (i < visible.Count - 1) builder.Append('\n');
        }
        listText.text = visible.Count > 0 ? builder.ToString() :
            "No hay mejoras en esta categoría.";
    }

    private void RenderDetail()
    {
        if (selectedIndex < 0 || selectedIndex >= visible.Count)
        {
            detailNameText.text = "Sin selección";
            detailDescriptionText.text = string.Empty;
            detailMetaText.text = string.Empty;
            detailEffectsText.text = string.Empty;
            detailRequirementsText.text = string.Empty;
            buyButton.interactable = false;
            buyButtonText.text = "Comprar";
            return;
        }

        BistroBuilderProgressionPlayerUpgradeRow row = visible[selectedIndex];
        detailNameText.text = row.displayName;
        detailDescriptionText.text = row.description;
        detailMetaText.text = CategoryLabel(row.category) + " · " +
            FormatMoney(row.costCents) + " · nivel " + row.requiredProgressionLevel +
            (row.requiredReputationBasisPoints > 0
                ? " · reputación " + (row.requiredReputationBasisPoints / 100f).ToString("0.#") + " %"
                : string.Empty);
        detailEffectsText.text = "EFECTO\n" + row.effectsSummary;
        detailRequirementsText.text = row.state == BistroBuilderUpgradeAvailabilityState.Purchased
            ? "Ya adquirida."
            : string.IsNullOrWhiteSpace(row.blockedReason)
                ? "Disponible para comprar."
                : row.blockedReason;
        bool canBuy = row.state == BistroBuilderUpgradeAvailabilityState.Available && row.affordable;
        buyButton.interactable = canBuy;
        buyButtonText.text = row.state == BistroBuilderUpgradeAvailabilityState.Purchased
            ? "Comprada"
            : canBuy ? "Comprar · " + FormatMoney(row.costCents) : "No disponible";
    }

    private void Bind()
    {
        if (bound) return;
        closeButton?.onClick.AddListener(Hide);
        allButton?.onClick.AddListener(FilterAll);
        diningButton?.onClick.AddListener(FilterDining);
        kitchenButton?.onClick.AddListener(FilterKitchen);
        terraceButton?.onClick.AddListener(FilterTerrace);
        barButton?.onClick.AddListener(FilterBar);
        infrastructureButton?.onClick.AddListener(FilterInfrastructure);
        ambienceButton?.onClick.AddListener(FilterAmbience);
        previousButton?.onClick.AddListener(SelectPrevious);
        nextButton?.onClick.AddListener(SelectNext);
        buyButton?.onClick.AddListener(HandleBuy);
        bound = true;
    }

    private void Unbind()
    {
        if (!bound) return;
        closeButton?.onClick.RemoveListener(Hide);
        allButton?.onClick.RemoveListener(FilterAll);
        diningButton?.onClick.RemoveListener(FilterDining);
        kitchenButton?.onClick.RemoveListener(FilterKitchen);
        terraceButton?.onClick.RemoveListener(FilterTerrace);
        barButton?.onClick.RemoveListener(FilterBar);
        infrastructureButton?.onClick.RemoveListener(FilterInfrastructure);
        ambienceButton?.onClick.RemoveListener(FilterAmbience);
        previousButton?.onClick.RemoveListener(SelectPrevious);
        nextButton?.onClick.RemoveListener(SelectNext);
        buyButton?.onClick.RemoveListener(HandleBuy);
        bound = false;
    }

    private void HandleBuy() => BuySelected(out _);
    private void HandleInvalidated()
    {
        if (isActiveAndEnabled && IsVisible) Refresh();
    }

    private void CacheDependencies()
    {
        if (facade == null) TryGetComponent(out facade);
    }

    private static string StateGlyph(BistroBuilderUpgradeAvailabilityState state)
    {
        switch (state)
        {
            case BistroBuilderUpgradeAvailabilityState.Purchased: return "✓";
            case BistroBuilderUpgradeAvailabilityState.Available: return "○";
            default: return "×";
        }
    }

    private static string CategoryLabel(BistroBuilderUpgradeCategory category)
    {
        switch (category)
        {
            case BistroBuilderUpgradeCategory.DiningRoom: return "Sala";
            case BistroBuilderUpgradeCategory.Kitchen: return "Cocina";
            case BistroBuilderUpgradeCategory.Terrace: return "Terraza";
            case BistroBuilderUpgradeCategory.Bar: return "Barra";
            case BistroBuilderUpgradeCategory.Infrastructure: return "Infraestructura";
            case BistroBuilderUpgradeCategory.AmbienceIdentity: return "Ambiente e identidad";
            default: return category.ToString();
        }
    }

    private static string FormatMoney(long cents)
    {
        long absolute = Math.Abs(cents);
        return (cents < 0 ? "−" : string.Empty) +
            (absolute / 100L) + "," + (absolute % 100L).ToString("00") + " €";
    }
}
