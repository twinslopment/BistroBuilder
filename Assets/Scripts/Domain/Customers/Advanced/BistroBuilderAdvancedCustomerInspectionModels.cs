using System;

public enum BistroBuilderCustomerPatienceBand
{
    High = 0,
    Medium = 1,
    Low = 2,
    Exhausted = 3
}

[Serializable]
public sealed class BistroBuilderAdvancedCustomerInspectionSnapshot
{
    public int groupId;
    public int memberIndex;
    public string customerId = string.Empty;
    public string displayName = string.Empty;
    public string archetypeId = string.Empty;
    public string archetypeLabel = string.Empty;
    public CustomerGroupState serviceState;
    public string serviceStateLabel = string.Empty;
    public BistroBuilderCustomerBehaviorMood mood;
    public string moodLabel = string.Empty;
    public BistroBuilderCustomerBehaviorReason behaviorReason;
    public string behaviorReasonLabel = string.Empty;
    public int patiencePressureBasisPoints;
    public int remainingPatienceBasisPoints;
    public BistroBuilderCustomerPatienceBand patienceBand;
    public string patienceLabel = string.Empty;
    public int satisfactionBasisPoints = -1;
    public string satisfactionLabel = "En curso";
    public BistroBuilderCustomerLoyaltyTier loyaltyTier;
    public string loyaltyLabel = "Nuevo";
    public BistroBuilderCustomerSpecialNeed specialNeeds;
    public string specialNeedsLabel = string.Empty;
    public string contextualMessage = string.Empty;

    public BistroBuilderAdvancedCustomerInspectionSnapshot DeepClone() =>
        (BistroBuilderAdvancedCustomerInspectionSnapshot)MemberwiseClone();
}
