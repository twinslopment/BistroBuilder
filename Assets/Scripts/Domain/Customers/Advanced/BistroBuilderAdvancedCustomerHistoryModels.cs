using System;
using System.Collections.Generic;

public enum BistroBuilderCustomerLoyaltyTier
{
    New = 0,
    Returning = 1,
    Regular = 2,
    Vip = 3
}

[Serializable]
public sealed class BistroBuilderAdvancedCustomerVisitOutcome
{
    public int groupId;
    public string experienceId = string.Empty;
    public string segmentId = "general";
    public int dayIndex;
    public long paidAmountCents;
    public int overallSatisfactionBasisPoints;
    public int waitingScoreBasisPoints;
    public int foodQualityScoreBasisPoints;
    public int valueForMoneyScoreBasisPoints;

    public BistroBuilderAdvancedCustomerVisitOutcome DeepClone() =>
        (BistroBuilderAdvancedCustomerVisitOutcome)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderAdvancedCustomerCohortAssignment
{
    public int groupId;
    public string cohortId = string.Empty;
    public string segmentId = "general";
    public int dayIndex;

    public BistroBuilderAdvancedCustomerCohortAssignment DeepClone() =>
        (BistroBuilderAdvancedCustomerCohortAssignment)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderAdvancedCustomerVisitHistoryRecord
{
    public string experienceId = string.Empty;
    public int dayIndex;
    public long paidAmountCents;
    public int satisfactionBasisPoints;
    public int waitingBasisPoints;
    public int foodQualityBasisPoints;
    public int valueBasisPoints;

    public BistroBuilderAdvancedCustomerVisitHistoryRecord DeepClone() =>
        (BistroBuilderAdvancedCustomerVisitHistoryRecord)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderAdvancedCustomerHistoryRecord
{
    public string cohortId = string.Empty;
    public string segmentId = "general";
    public int firstVisitDay;
    public int lastVisitDay;
    public int visitCount;
    public long lifetimeSpendCents;
    public long satisfactionTotalBasisPoints;
    public BistroBuilderCustomerLoyaltyTier loyaltyTier;
    public List<BistroBuilderAdvancedCustomerVisitHistoryRecord> recentVisits =
        new List<BistroBuilderAdvancedCustomerVisitHistoryRecord>();

    public int AverageSatisfactionBasisPoints => visitCount > 0
        ? (int)Math.Round(
            satisfactionTotalBasisPoints / (double)visitCount,
            MidpointRounding.AwayFromZero)
        : 0;

    public BistroBuilderAdvancedCustomerHistoryRecord DeepClone()
    {
        var clone = (BistroBuilderAdvancedCustomerHistoryRecord)MemberwiseClone();
        clone.recentVisits = new List<BistroBuilderAdvancedCustomerVisitHistoryRecord>();
        if (recentVisits != null)
            for (int i = 0; i < recentVisits.Count; i++)
                clone.recentVisits.Add(recentVisits[i]?.DeepClone());
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderAdvancedCustomerHistorySnapshot
{
    public const string CurrentSchemaId = "advanced_customers.state";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public List<BistroBuilderAdvancedCustomerHistoryRecord> customers =
        new List<BistroBuilderAdvancedCustomerHistoryRecord>();
    public List<BistroBuilderAdvancedCustomerVisitOutcome> pendingOutcomes =
        new List<BistroBuilderAdvancedCustomerVisitOutcome>();
    public List<BistroBuilderAdvancedCustomerCohortAssignment> pendingAssignments =
        new List<BistroBuilderAdvancedCustomerCohortAssignment>();

    public BistroBuilderAdvancedCustomerHistorySnapshot DeepClone()
    {
        var clone = new BistroBuilderAdvancedCustomerHistorySnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision
        };
        if (customers != null)
            for (int i = 0; i < customers.Count; i++)
                clone.customers.Add(customers[i]?.DeepClone());
        if (pendingOutcomes != null)
            for (int i = 0; i < pendingOutcomes.Count; i++)
                clone.pendingOutcomes.Add(pendingOutcomes[i]?.DeepClone());
        if (pendingAssignments != null)
            for (int i = 0; i < pendingAssignments.Count; i++)
                clone.pendingAssignments.Add(pendingAssignments[i]?.DeepClone());
        return clone;
    }
}
