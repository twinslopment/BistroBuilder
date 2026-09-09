using System;
using System.Text;
using UnityEngine;

/// <summary>Fachada jugable de Mejoras y Progresión. Toda compra pasa por UpgradeService.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Progression/Progression Player Facade")]
public sealed class BistroBuilderProgressionPlayerFacade : MonoBehaviour
{
    [SerializeField] private BistroBuilderUpgradeService upgradeService;
    [SerializeField] private BistroBuilderProgressionMilestoneService milestoneService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private BistroBuilderFinanceService financeService;

    public event Action ViewInvalidated;

    private void Awake() => CacheDependencies();
    private void OnEnable() { CacheDependencies(); Subscribe(); }
    private void OnDisable() => Unsubscribe();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (upgradeService == null || milestoneService == null ||
            generalGameStateService == null || reputationService == null || financeService == null)
        {
            error = "La UI 9E necesita Mejoras, Hitos, Estado General, Reputación y Finanzas.";
            return false;
        }
        if (!upgradeService.ValidateConfiguration(out error) ||
            !milestoneService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error) ||
            !financeService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool TryBuildSnapshot(
        out BistroBuilderProgressionPlayerUiSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!ValidateConfiguration(out error) ||
            !upgradeService.DiscretionaryFinanceService.TryGetAvailableCashCents(
                out long availableCash, out error))
            return false;

        var built = new BistroBuilderProgressionPlayerUiSnapshot
        {
            dayIndex = generalGameStateService.DayIndex,
            stageId = generalGameStateService.ProgressionStageId,
            progressionLevel = generalGameStateService.ProgressionLevel,
            reputationBasisPoints = reputationService.GlobalScoreBasisPoints,
            availableCashCents = availableCash,
            purchasedCount = upgradeService.PurchasedCount
        };
        var definitions = upgradeService.UpgradeCatalog.Upgrades;
        for (int i = 0; i < definitions.Count; i++)
        {
            BistroBuilderUpgradeDefinition definition = definitions[i];
            if (definition == null ||
                !upgradeService.TryGetAvailability(
                    definition.upgradeId,
                    out BistroBuilderUpgradeAvailability availability,
                    out error))
                return false;

            built.upgrades.Add(new BistroBuilderProgressionPlayerUpgradeRow
            {
                upgradeId = definition.upgradeId,
                displayName = definition.displayName,
                description = definition.description,
                category = definition.category,
                costCents = definition.costCents,
                requiredProgressionLevel = definition.requiredProgressionLevel,
                requiredReputationBasisPoints = definition.requiredReputationBasisPoints,
                state = availability.state,
                affordable = availability.affordable,
                blockedReason = availability.blockedReason,
                effectsSummary = BuildEffectsSummary(definition)
            });
        }
        built.upgrades.Sort(CompareRows);

        if (!milestoneService.TryGetCurrentEvaluation(
                out BistroBuilderProgressionMilestoneEvaluation milestone,
                out error))
            return false;
        if (milestone?.milestone != null)
        {
            built.nextMilestoneName = milestone.milestone.displayName;
            built.nextMilestoneTargetLevel = milestone.milestone.targetLevel;
            built.nextMilestoneComplete = milestone.completed;
            built.milestoneRequirements = milestone.completed
                ? "Requisitos completados."
                : string.Join(" · ", milestone.unmetRequirements);
        }
        else
        {
            built.nextMilestoneName = "Techo V1 alcanzado";
            built.nextMilestoneTargetLevel = built.progressionLevel;
            built.nextMilestoneComplete = true;
            built.milestoneRequirements = "No hay más hitos V1 pendientes.";
        }

        snapshot = built;
        error = string.Empty;
        return true;
    }

    public bool TryPurchaseUpgrade(
        string upgradeId,
        out BistroBuilderPurchasedUpgradeRecord purchased,
        out string error)
    {
        purchased = null;
        if (!ValidateConfiguration(out error)) return false;
        return upgradeService.TryPurchaseUpgrade(upgradeId, out purchased, out error);
    }

    public static string BuildEffectsSummary(BistroBuilderUpgradeDefinition definition)
    {
        if (definition?.effects == null || definition.effects.Count == 0)
            return "Sin efecto funcional.";
        var builder = new StringBuilder();
        for (int i = 0; i < definition.effects.Count; i++)
        {
            BistroBuilderUpgradeEffectDefinition effect = definition.effects[i];
            if (effect == null) continue;
            if (builder.Length > 0) builder.Append(" · ");
            builder.Append(EffectLabel(effect.kind)).Append(' ')
                .Append(FormatSignedBasisPoints(effect.basisPoints));
            if (effect.barServiceOnly) builder.Append(" en barra");
        }
        return builder.Length > 0 ? builder.ToString() : "Sin efecto funcional.";
    }

    private static string EffectLabel(BistroBuilderUpgradeEffectKind kind)
    {
        switch (kind)
        {
            case BistroBuilderUpgradeEffectKind.PreparationDuration: return "Tiempo preparación";
            case BistroBuilderUpgradeEffectKind.AmbienceScore: return "Ambiente";
            case BistroBuilderUpgradeEffectKind.FoodQualityPotential: return "Calidad potencial";
            default: return kind.ToString();
        }
    }

    private static string FormatSignedBasisPoints(int value)
    {
        string sign = value > 0 ? "+" : value < 0 ? "−" : string.Empty;
        return sign + (Math.Abs(value) / 100f).ToString("0.##") + " %";
    }

    private static int CompareRows(
        BistroBuilderProgressionPlayerUpgradeRow a,
        BistroBuilderProgressionPlayerUpgradeRow b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int category = a.category.CompareTo(b.category);
        if (category != 0) return category;
        int level = a.requiredProgressionLevel.CompareTo(b.requiredProgressionLevel);
        return level != 0 ? level : string.Compare(
            a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (upgradeService != null)
        {
            upgradeService.UpgradePurchased += HandleUpgrade;
            upgradeService.UpgradeUnlocked += HandleUpgrade;
            upgradeService.ProgressionRevisionChanged += HandleRevision;
        }
        if (milestoneService != null)
            milestoneService.ProgressionEvaluationChanged += HandleChanged;
        if (generalGameStateService != null)
            generalGameStateService.ProgressionChanged += HandleChanged;
        if (reputationService != null)
        {
            reputationService.ReputationChanged += HandleRevision;
            reputationService.ReputationRestored += HandleChanged;
        }
        if (financeService != null)
        {
            financeService.TransactionPosted += HandleFinance;
            financeService.StateRestored += HandleChanged;
        }
    }

    private void Unsubscribe()
    {
        if (upgradeService != null)
        {
            upgradeService.UpgradePurchased -= HandleUpgrade;
            upgradeService.UpgradeUnlocked -= HandleUpgrade;
            upgradeService.ProgressionRevisionChanged -= HandleRevision;
        }
        if (milestoneService != null)
            milestoneService.ProgressionEvaluationChanged -= HandleChanged;
        if (generalGameStateService != null)
            generalGameStateService.ProgressionChanged -= HandleChanged;
        if (reputationService != null)
        {
            reputationService.ReputationChanged -= HandleRevision;
            reputationService.ReputationRestored -= HandleChanged;
        }
        if (financeService != null)
        {
            financeService.TransactionPosted -= HandleFinance;
            financeService.StateRestored -= HandleChanged;
        }
    }

    private void HandleUpgrade(string _) => ViewInvalidated?.Invoke();
    private void HandleRevision(long _) => ViewInvalidated?.Invoke();
    private void HandleFinance(BistroBuilderFinanceTransactionRecord _) => ViewInvalidated?.Invoke();
    private void HandleChanged() => ViewInvalidated?.Invoke();

    private void CacheDependencies()
    {
        if (upgradeService == null) TryGetComponent(out upgradeService);
        if (milestoneService == null) TryGetComponent(out milestoneService);
        if (generalGameStateService == null) TryGetComponent(out generalGameStateService);
        if (reputationService == null) TryGetComponent(out reputationService);
        if (financeService == null) TryGetComponent(out financeService);
    }
}
