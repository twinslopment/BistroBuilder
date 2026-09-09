using System;
using System.Collections.Generic;

/// <summary>Hito data-driven que permite avanzar un nivel de progresión.</summary>
[Serializable]
public sealed class BistroBuilderProgressionMilestoneDefinition
{
    public string milestoneId = string.Empty;
    public string stageId = string.Empty;
    public string displayName = string.Empty;
    public int targetLevel = 2;
    public int requiredReputationBasisPoints;
    public int requiredPurchasedUpgradeCount;
    public long requiredCumulativeRevenueCents;
    public int requiredProfitableDays;

    public BistroBuilderProgressionMilestoneDefinition DeepClone()
    {
        return new BistroBuilderProgressionMilestoneDefinition
        {
            milestoneId = milestoneId,
            stageId = stageId,
            displayName = displayName,
            targetLevel = targetLevel,
            requiredReputationBasisPoints = requiredReputationBasisPoints,
            requiredPurchasedUpgradeCount = requiredPurchasedUpgradeCount,
            requiredCumulativeRevenueCents = requiredCumulativeRevenueCents,
            requiredProfitableDays = requiredProfitableDays
        };
    }
}

/// <summary>Métricas canónicas usadas para evaluar un hito.</summary>
public sealed class BistroBuilderProgressionMilestoneContext
{
    public int currentLevel = 1;
    public int reputationBasisPoints;
    public int purchasedUpgradeCount;
    public long cumulativeRevenueCents;
    public int profitableDays;
}

/// <summary>Diagnóstico puro del siguiente hito.</summary>
public sealed class BistroBuilderProgressionMilestoneEvaluation
{
    public BistroBuilderProgressionMilestoneDefinition milestone;
    public bool completed;
    public readonly List<string> unmetRequirements = new List<string>();
}
