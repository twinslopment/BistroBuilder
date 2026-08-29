using System;
using UnityEngine;

/// <summary>
/// Proyecta efectos funcionales de mejoras adquiridas sin tomar autoridad
/// sobre cocina, reputación, comandas ni persistencia.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Progression/Upgrade Effects Service")]
public sealed class BistroBuilderUpgradeEffectsService : MonoBehaviour,
    IBistroBuilderPreparationDurationAdjustmentProvider
{
    public const string StableProviderId = "progression.upgrades";

    [SerializeField] private BistroBuilderUpgradeService upgradeService;

    public string AdjustmentProviderId => StableProviderId;
    public int AmbienceBonusBasisPoints =>
        SumEffect(BistroBuilderUpgradeEffectKind.AmbienceScore, false);
    public int FoodQualityPotentialBonusBasisPoints =>
        SumEffect(BistroBuilderUpgradeEffectKind.FoodQualityPotential, false);

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (upgradeService == null)
        {
            error = "9D necesita UpgradeService.";
            return false;
        }
        return upgradeService.ValidateConfiguration(out error);
    }

    public bool TryGetAdjustmentBasisPoints(
        BistroBuilderPreparationDurationAdjustmentContext context,
        out int adjustmentBasisPoints,
        out string error)
    {
        adjustmentBasisPoints = 0;
        if (context == null)
        {
            error = "9D recibió un contexto de preparación nulo.";
            return false;
        }
        if (!ValidateConfiguration(out error)) return false;

        bool bar = context.serviceMode == BistroBuilderServiceMode.BarService ||
            context.serviceMode == BistroBuilderServiceMode.WaitingAtBar;
        adjustmentBasisPoints = SumEffect(
            BistroBuilderUpgradeEffectKind.PreparationDuration,
            bar);
        adjustmentBasisPoints = Mathf.Clamp(
            adjustmentBasisPoints,
            BistroBuilderPreparationDurationAdjustmentPolicy.MinimumAdjustmentBasisPoints,
            BistroBuilderPreparationDurationAdjustmentPolicy.MaximumAdjustmentBasisPoints);
        error = string.Empty;
        return true;
    }

    private int SumEffect(BistroBuilderUpgradeEffectKind kind, bool barContext)
    {
        if (upgradeService == null || upgradeService.UpgradeCatalog == null)
            return 0;

        BistroBuilderUpgradeSnapshot snapshot = upgradeService.CreateSnapshot();
        if (snapshot?.purchased == null) return 0;

        int total = 0;
        for (int i = 0; i < snapshot.purchased.Count; i++)
        {
            BistroBuilderPurchasedUpgradeRecord purchase = snapshot.purchased[i];
            if (purchase == null ||
                !upgradeService.UpgradeCatalog.TryGetUpgrade(
                    purchase.upgradeId, out BistroBuilderUpgradeDefinition definition) ||
                definition?.effects == null)
                continue;

            for (int e = 0; e < definition.effects.Count; e++)
            {
                BistroBuilderUpgradeEffectDefinition effect = definition.effects[e];
                if (effect == null || effect.kind != kind) continue;
                if (effect.barServiceOnly && !barContext) continue;
                total = Mathf.Clamp(total + effect.basisPoints, -5000, 5000);
            }
        }
        return total;
    }

    private void CacheDependencies()
    {
        if (upgradeService == null) TryGetComponent(out upgradeService);
    }
}
