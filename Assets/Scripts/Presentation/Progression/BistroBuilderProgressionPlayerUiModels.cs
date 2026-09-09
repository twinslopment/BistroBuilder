using System;
using System.Collections.Generic;

[Serializable]
public sealed class BistroBuilderProgressionPlayerUpgradeRow
{
    public string upgradeId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public BistroBuilderUpgradeCategory category;
    public long costCents;
    public int requiredProgressionLevel;
    public int requiredReputationBasisPoints;
    public BistroBuilderUpgradeAvailabilityState state;
    public bool affordable;
    public string blockedReason = string.Empty;
    public string effectsSummary = string.Empty;
}

[Serializable]
public sealed class BistroBuilderProgressionPlayerUiSnapshot
{
    public int dayIndex;
    public string stageId = string.Empty;
    public int progressionLevel;
    public int reputationBasisPoints;
    public long availableCashCents;
    public int purchasedCount;
    public string nextMilestoneName = string.Empty;
    public int nextMilestoneTargetLevel;
    public bool nextMilestoneComplete;
    public string milestoneRequirements = string.Empty;
    public List<BistroBuilderProgressionPlayerUpgradeRow> upgrades =
        new List<BistroBuilderProgressionPlayerUpgradeRow>();
}
