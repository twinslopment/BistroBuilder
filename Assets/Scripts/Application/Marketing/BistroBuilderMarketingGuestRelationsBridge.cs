using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adapta efectos persistentes de Marketing a la autoridad independiente de
/// Reputación. Cada campaña acredita una sola vez a la autoridad del Bloque 8.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Marketing/Marketing Guest Relations Bridge")]
public sealed class BistroBuilderMarketingGuestRelationsBridge : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderMarketingService marketingService;

    [SerializeField]
    private BistroBuilderReputationService reputationService;

    private readonly Dictionary<string, BistroBuilderMarketingCampaignDefinition>
        definitionsById =
            new Dictionary<string, BistroBuilderMarketingCampaignDefinition>(
                StringComparer.Ordinal);

    public int LastCreditsApplied { get; private set; }

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
        TrySynchronizeReputationCredits(out _);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (marketingService == null || reputationService == null)
        {
            error = "El puente de reputación necesita Marketing y Reputación.";
            return false;
        }

        if (!marketingService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public bool TrySynchronizeReputationCredits(out string error)
    {
        LastCreditsApplied = 0;
        if (!ValidateConfiguration(out error))
            return false;

        BistroBuilderMarketingCampaignCatalog catalog =
            marketingService.CampaignCatalog;
        if (catalog == null)
        {
            error = "No existe catálogo de Marketing.";
            return false;
        }

        definitionsById.Clear();
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> definitions =
            catalog.Campaigns;
        for (int i = 0; i < definitions.Count; i++)
        {
            BistroBuilderMarketingCampaignDefinition definition = definitions[i];
            if (definition == null)
                continue;
            definitionsById[BistroBuilderMarketingEngine.NormalizeId(
                definition.campaignId)] = definition;
        }

        BistroBuilderMarketingSnapshot snapshot = marketingService.CreateSnapshot();
        for (int i = 0; i < snapshot.campaigns.Count; i++)
        {
            BistroBuilderMarketingCampaignRecord record = snapshot.campaigns[i];
            if (record == null ||
                !definitionsById.TryGetValue(
                    BistroBuilderMarketingEngine.NormalizeId(record.campaignId),
                    out BistroBuilderMarketingCampaignDefinition definition))
                continue;

            int reputationPoints = 0;
            for (int j = 0; j < definition.modifiers.Count; j++)
            {
                BistroBuilderMarketingModifier modifier = definition.modifiers[j];
                if (modifier != null &&
                    modifier.kind == BistroBuilderMarketingModifierKind.Reputation)
                    reputationPoints += modifier.flatPoints;
            }

            if (reputationPoints == 0)
                continue;

            string sourceId = "marketing.reputation." + record.instanceId;
            if (!reputationService.TryApplyExternalReputationCredit(
                    sourceId,
                    reputationPoints,
                    out bool changed,
                    out error))
                return false;
            if (changed)
                LastCreditsApplied++;
        }

        error = string.Empty;
        return true;
    }

    private void HandleMarketingChanged(long revision)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;
        if (!TrySynchronizeReputationCredits(out string error))
            Debug.LogError("No pudo sincronizarse reputación: " + error, this);
    }

    private void HandleMarketingRestored()
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;
        HandleMarketingChanged(marketingService != null
            ? marketingService.Revision
            : 0L);
    }

    private void Subscribe()
    {
        if (marketingService == null)
            return;
        marketingService.MarketingChanged -= HandleMarketingChanged;
        marketingService.MarketingChanged += HandleMarketingChanged;
        marketingService.MarketingRestored -= HandleMarketingRestored;
        marketingService.MarketingRestored += HandleMarketingRestored;
    }

    private void Unsubscribe()
    {
        if (marketingService == null)
            return;
        marketingService.MarketingChanged -= HandleMarketingChanged;
        marketingService.MarketingRestored -= HandleMarketingRestored;
    }

    private void CacheDependencies()
    {
        if (marketingService == null)
            TryGetComponent(out marketingService);
        if (reputationService == null)
            TryGetComponent(out reputationService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
