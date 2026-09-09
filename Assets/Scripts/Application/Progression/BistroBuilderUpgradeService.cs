using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad de mejoras adquiridas. Consume Finanzas, Reputación y el nivel global,
/// pero no duplica ninguna de esas autoridades.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Progression/Upgrade Service")]
public sealed class BistroBuilderUpgradeService : MonoBehaviour
{
    [SerializeField] private BistroBuilderUpgradeCatalog upgradeCatalog;
    [SerializeField] private BistroBuilderDiscretionaryFinanceService discretionaryFinanceService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private List<string> localCapabilityIds = new List<string>
    {
        "restaurant.base",
        "facility.dining_room",
        "facility.kitchen"
    };

    private BistroBuilderUpgradeSnapshot state;
    private readonly HashSet<string> knownUnlocked =
        new HashSet<string>(StringComparer.Ordinal);

    public event Action<string> UpgradeUnlocked;
    public event Action<string> UpgradePurchased;
    public event Action<long> ProgressionRevisionChanged;
    public event Action<string> BusinessMilestoneReached;
    public event Action<string> ProgressionPathChanged;

    public BistroBuilderUpgradeCatalog UpgradeCatalog => upgradeCatalog;
    public BistroBuilderDiscretionaryFinanceService DiscretionaryFinanceService =>
        discretionaryFinanceService;
    public BistroBuilderGeneralGameStateService GeneralGameStateService =>
        generalGameStateService;
    public BistroBuilderReputationService ReputationService => reputationService;
    public long Revision => state != null ? state.revision : 0L;
    public int PurchasedCount => state?.purchased != null ? state.purchased.Count : 0;

    private void Awake()
    {
        EnsureState();
        RebuildKnownUnlocked(false);
    }

    private void OnEnable()
    {
        if (generalGameStateService != null)
        {
            generalGameStateService.ProgressionChanged -= HandleExternalProgressionChanged;
            generalGameStateService.ProgressionChanged += HandleExternalProgressionChanged;
        }
        if (reputationService != null)
        {
            reputationService.ReputationChanged -= HandleReputationChanged;
            reputationService.ReputationChanged += HandleReputationChanged;
        }
    }

    private void OnDisable()
    {
        if (generalGameStateService != null)
            generalGameStateService.ProgressionChanged -= HandleExternalProgressionChanged;
        if (reputationService != null)
            reputationService.ReputationChanged -= HandleReputationChanged;
    }

    public bool ValidateConfiguration(out string error)
    {
        EnsureState();
        if (upgradeCatalog == null || discretionaryFinanceService == null ||
            generalGameStateService == null || reputationService == null)
        {
            error = "9A necesita catálogo, Finanzas discrecionales, estado general y Reputación.";
            return false;
        }
        if (!upgradeCatalog.ValidateConfiguration(out error) ||
            !discretionaryFinanceService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error) ||
            !BistroBuilderProgressionEngine.TryValidateSnapshot(state, out error))
            return false;
        return ValidateCapabilities(out error);
    }

    public BistroBuilderUpgradeSnapshot CreateSnapshot()
    {
        EnsureState();
        return state.DeepClone();
    }

    public bool TryRestoreSnapshot(BistroBuilderUpgradeSnapshot snapshot, out string error)
    {
        if (!BistroBuilderProgressionEngine.TryValidateSnapshot(snapshot, out error))
            return false;
        if (upgradeCatalog == null || !upgradeCatalog.ValidateConfiguration(out error))
            return false;
        for (int i = 0; i < snapshot.purchased.Count; i++)
            if (!upgradeCatalog.TryGetUpgrade(snapshot.purchased[i].upgradeId, out _))
            {
                error = "El snapshot referencia una mejora inexistente: " +
                    snapshot.purchased[i].upgradeId + ".";
                return false;
            }
        state = snapshot.DeepClone();
        RebuildKnownUnlocked(false);
        ProgressionRevisionChanged?.Invoke(state.revision);
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        state = BistroBuilderProgressionEngine.CreateInitialSnapshot();
        RebuildKnownUnlocked(false);
        error = string.Empty;
        ProgressionRevisionChanged?.Invoke(state.revision);
        return true;
    }

    public bool IsPurchased(string upgradeId)
    {
        EnsureState();
        string id = BistroBuilderProgressionEngine.NormalizeId(upgradeId);
        for (int i = 0; i < state.purchased.Count; i++)
            if (BistroBuilderProgressionEngine.NormalizeId(state.purchased[i].upgradeId) == id)
                return true;
        return false;
    }

    public bool TryGetAvailability(
        string upgradeId,
        out BistroBuilderUpgradeAvailability availability,
        out string error)
    {
        availability = null;
        if (!ValidateConfiguration(out error)) return false;
        if (!upgradeCatalog.TryGetUpgrade(upgradeId, out var definition))
        {
            error = "La mejora solicitada no existe.";
            return false;
        }
        if (!TryBuildContext(out var context, out error)) return false;
        availability = BistroBuilderProgressionEngine.EvaluateAvailability(definition, context);
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
        if (!upgradeCatalog.TryGetUpgrade(upgradeId, out var definition))
        {
            error = "La mejora solicitada no existe.";
            return false;
        }
        if (!TryBuildContext(out var context, out error)) return false;
        var availability = BistroBuilderProgressionEngine.EvaluateAvailability(definition, context);
        if (availability.state != BistroBuilderUpgradeAvailabilityState.Available ||
            !availability.affordable)
        {
            error = string.IsNullOrWhiteSpace(availability.blockedReason)
                ? "La mejora no está disponible."
                : availability.blockedReason;
            return false;
        }
        if (!BistroBuilderProgressionEngine.TryCreatePurchaseCandidate(
                state, definition, generalGameStateService.DayIndex,
                out var candidate, out error))
            return false;

        string id = BistroBuilderProgressionEngine.NormalizeId(definition.upgradeId);
        var expense = new BistroBuilderDiscretionaryExpenseRequest
        {
            operationId = "upgrade.purchase." + id + ".r" + candidate.revision,
            sourceSystemId = "progression.upgrades",
            sourceReferenceId = id,
            categoryId = "investment.improvement." + CategorySlug(definition.category),
            amountCents = definition.costCents,
            description = "Mejora: " + definition.displayName
        };
        if (!discretionaryFinanceService.TryPostExpense(expense, out _, out error))
            return false;

        state = candidate;
        purchased = state.purchased[state.purchased.Count - 1].DeepClone();
        UpgradePurchased?.Invoke(id);
        ProgressionRevisionChanged?.Invoke(state.revision);
        RebuildKnownUnlocked(true);
        error = string.Empty;
        return true;
    }

    public void CopyLocalCapabilities(ICollection<string> destination)
    {
        if (destination == null) return;
        destination.Clear();
        if (localCapabilityIds == null) return;
        for (int i = 0; i < localCapabilityIds.Count; i++)
            destination.Add(BistroBuilderProgressionEngine.NormalizeId(localCapabilityIds[i]));
    }

    private bool TryBuildContext(
        out BistroBuilderUpgradeAvailabilityContext context,
        out string error)
    {
        context = new BistroBuilderUpgradeAvailabilityContext
        {
            progressionLevel = generalGameStateService.ProgressionLevel,
            reputationBasisPoints = reputationService.GlobalScoreBasisPoints
        };
        if (!discretionaryFinanceService.TryGetAvailableCashCents(
                out context.availableCashCents, out error))
            return false;
        for (int i = 0; i < state.purchased.Count; i++)
            context.purchasedUpgradeIds.Add(
                BistroBuilderProgressionEngine.NormalizeId(state.purchased[i].upgradeId));
        if (localCapabilityIds != null)
            for (int i = 0; i < localCapabilityIds.Count; i++)
                context.capabilityIds.Add(
                    BistroBuilderProgressionEngine.NormalizeId(localCapabilityIds[i]));
        return true;
    }

    private bool ValidateCapabilities(out string error)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (localCapabilityIds == null || localCapabilityIds.Count == 0)
        {
            error = "El local no declara capacidades de progresión.";
            return false;
        }
        for (int i = 0; i < localCapabilityIds.Count; i++)
        {
            string id = BistroBuilderProgressionEngine.NormalizeId(localCapabilityIds[i]);
            if (!BistroBuilderProgressionEngine.IsSafeStableId(id) || !ids.Add(id))
            {
                error = "Las capacidades locales contienen IDs inválidos o duplicados.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private void RebuildKnownUnlocked(bool publishNew)
    {
        if (upgradeCatalog == null || generalGameStateService == null ||
            reputationService == null || state == null) return;
        if (!TryBuildContext(out var context, out _)) return;
        var currentlyUnlocked = new HashSet<string>(StringComparer.Ordinal);
        var definitions = upgradeCatalog.Upgrades;
        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null) continue;
            var availability = BistroBuilderProgressionEngine.EvaluateAvailability(definition, context);
            if (availability.state == BistroBuilderUpgradeAvailabilityState.Available ||
                availability.state == BistroBuilderUpgradeAvailabilityState.Purchased)
            {
                string id = BistroBuilderProgressionEngine.NormalizeId(definition.upgradeId);
                currentlyUnlocked.Add(id);
                if (publishNew && !knownUnlocked.Contains(id)) UpgradeUnlocked?.Invoke(id);
            }
        }
        knownUnlocked.Clear();
        foreach (string id in currentlyUnlocked) knownUnlocked.Add(id);
    }

    private void HandleExternalProgressionChanged() => RebuildKnownUnlocked(true);
    private void HandleReputationChanged(long _) => RebuildKnownUnlocked(true);

    private void EnsureState()
    {
        if (state == null) state = BistroBuilderProgressionEngine.CreateInitialSnapshot();
    }

    private static string CategorySlug(BistroBuilderUpgradeCategory category)
    {
        switch (category)
        {
            case BistroBuilderUpgradeCategory.DiningRoom: return "dining_room";
            case BistroBuilderUpgradeCategory.Kitchen: return "kitchen";
            case BistroBuilderUpgradeCategory.Terrace: return "terrace";
            case BistroBuilderUpgradeCategory.Bar: return "bar";
            case BistroBuilderUpgradeCategory.Infrastructure: return "infrastructure";
            case BistroBuilderUpgradeCategory.AmbienceIdentity: return "ambience_identity";
            default: return "other";
        }
    }
}