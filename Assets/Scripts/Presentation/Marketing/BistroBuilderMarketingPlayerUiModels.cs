using System;
using System.Collections.Generic;

/// <summary>
/// Objetivo seleccionable expuesto a Presentation. No es autoridad de carta.
/// </summary>
[Serializable]
public sealed class BistroBuilderMarketingPlayerTargetOption
{
    public BistroBuilderMarketingTargetKind kind;
    public string targetId = string.Empty;
    public string displayName = string.Empty;
}

/// <summary>
/// Campaña de catálogo proyectada para la pantalla jugable de Marketing.
/// </summary>
[Serializable]
public sealed class BistroBuilderMarketingPlayerCampaignRow
{
    public string campaignId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public BistroBuilderMarketingCampaignType type;
    public BistroBuilderMarketingTargetKind targetKind;
    public long costCents;
    public int durationDays;
    public int minProgressionLevel;
    public string effectsSummary = string.Empty;
    public bool progressionUnlocked;
    public string blockedReason = string.Empty;
}

/// <summary>
/// Campaña contratada y activa proyectada para Presentation.
/// </summary>
[Serializable]
public sealed class BistroBuilderMarketingPlayerActiveRow
{
    public string instanceId = string.Empty;
    public string campaignId = string.Empty;
    public string displayName = string.Empty;
    public string targetId = string.Empty;
    public string targetDisplayName = string.Empty;
    public BistroBuilderMarketingCampaignType type;
    public int startDayIndex;
    public int endDayExclusive;
    public int daysRemaining;
    public long paidCostCents;
    public string effectsSummary = string.Empty;
}

/// <summary>
/// Fotografía completa de lectura para la UI de Marketing.
/// Se reconstruye siempre desde autoridades runtime.
/// </summary>
[Serializable]
public sealed class BistroBuilderMarketingPlayerUiSnapshot
{
    public int currentDayIndex;
    public int progressionLevel;
    public long marketingRevision;
    public int activeCampaignCount;
    public int reputationPoints;
    public int reputationDemandBasisPoints;
    public int recurrentCohortCount;
    public List<BistroBuilderMarketingPlayerCampaignRow> campaigns =
        new List<BistroBuilderMarketingPlayerCampaignRow>();
    public List<BistroBuilderMarketingPlayerActiveRow> activeCampaigns =
        new List<BistroBuilderMarketingPlayerActiveRow>();
    public List<BistroBuilderMarketingPlayerTargetOption> dishTargets =
        new List<BistroBuilderMarketingPlayerTargetOption>();
    public List<BistroBuilderMarketingPlayerTargetOption> menuTargets =
        new List<BistroBuilderMarketingPlayerTargetOption>();

    public List<BistroBuilderMarketingPlayerTargetOption> TargetsFor(
        BistroBuilderMarketingTargetKind kind)
    {
        switch (kind)
        {
            case BistroBuilderMarketingTargetKind.Dish:
                return dishTargets;
            case BistroBuilderMarketingTargetKind.Menu:
                return menuTargets;
            default:
                return null;
        }
    }
}
