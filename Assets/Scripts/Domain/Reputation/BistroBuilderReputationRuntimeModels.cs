using System;
using System.Collections.Generic;

[Serializable]
public sealed class BistroBuilderReputationVisitRuntimeRecord
{
    public int groupId;
    public string canonicalOrderId = string.Empty;
    public string segmentId = "general";
    public int partySize = 1;
    public BistroBuilderRestaurantDiscoverySource discoverySource;
    public float tableWaitSeconds;
    public float waiterWaitSeconds;
    public float foodWaitSeconds;
    public float billWaitSeconds;
    public long paidAmountCents;
    public long referenceAmountCents;
    public float expectedFoodSeconds;
    public int foodQualityPotentialBasisPoints = 7000;
    public int ambienceScoreBasisPoints = 5000;
    public bool orderCompleted;
    public bool financeCaptured;

    public BistroBuilderReputationVisitRuntimeRecord DeepClone() =>
        (BistroBuilderReputationVisitRuntimeRecord)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderReputationRuntimeSnapshot
{
    public const string CurrentSchemaId = "reputation.runtime";
    public const int CurrentSchemaVersion = 1;
    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public List<BistroBuilderReputationVisitRuntimeRecord> visits =
        new List<BistroBuilderReputationVisitRuntimeRecord>();

    public BistroBuilderReputationRuntimeSnapshot DeepClone()
    {
        var clone = new BistroBuilderReputationRuntimeSnapshot();
        if (visits != null)
            for (int i = 0; i < visits.Count; i++)
                clone.visits.Add(visits[i]?.DeepClone());
        return clone;
    }
}
