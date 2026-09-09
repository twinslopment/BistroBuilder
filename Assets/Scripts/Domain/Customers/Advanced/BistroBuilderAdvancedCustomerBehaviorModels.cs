using System;
using System.Collections.Generic;

public enum BistroBuilderCustomerBehaviorMood
{
    Calm = 0,
    Attentive = 1,
    Restless = 2,
    Impatient = 3,
    Critical = 4
}

public enum BistroBuilderCustomerBehaviorReason
{
    None = 0,
    TableWait = 1,
    WaiterWait = 2,
    FoodWait = 3,
    BillWait = 4
}

[Serializable]
public sealed class BistroBuilderAdvancedCustomerIndividualBehavior
{
    public string customerId = string.Empty;
    public int memberIndex;
    public int patiencePressureBasisPoints;
    public BistroBuilderCustomerBehaviorMood mood;
    public BistroBuilderCustomerBehaviorReason reason;

    public BistroBuilderAdvancedCustomerIndividualBehavior DeepClone() =>
        (BistroBuilderAdvancedCustomerIndividualBehavior)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderAdvancedCustomerGroupBehavior
{
    public int groupId;
    public CustomerGroupState serviceState;
    public BistroBuilderCustomerBehaviorMood dominantMood;
    public BistroBuilderCustomerBehaviorReason dominantReason;
    public int averagePressureBasisPoints;
    public int maximumPressureBasisPoints;
    public int impatientMemberCount;
    public List<BistroBuilderAdvancedCustomerIndividualBehavior> individuals =
        new List<BistroBuilderAdvancedCustomerIndividualBehavior>();

    public BistroBuilderAdvancedCustomerGroupBehavior DeepClone()
    {
        var clone = (BistroBuilderAdvancedCustomerGroupBehavior)MemberwiseClone();
        clone.individuals = new List<BistroBuilderAdvancedCustomerIndividualBehavior>();
        if (individuals != null)
            for (int i = 0; i < individuals.Count; i++)
                clone.individuals.Add(individuals[i]?.DeepClone());
        return clone;
    }
}
