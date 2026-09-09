using System;
using System.Collections.Generic;

/// <summary>
/// Cohorte persistente que representa a un grupo real que ya completó una visita.
/// No persiste GameObjects ni NPCs concretos.
/// </summary>
[Serializable]
public sealed class BistroBuilderGuestVisitCohortRecord
{
    public string cohortId = string.Empty;
    public string segmentId = "general";
    public int partySize = 1;
    public int visitCount = 1;
    public int lastVisitDay = 1;

    public BistroBuilderGuestVisitCohortRecord DeepClone() =>
        (BistroBuilderGuestVisitCohortRecord)MemberwiseClone();
}

/// <summary>
/// Estado persistente de relación con clientes y reputación del restaurante.
/// Es una autoridad independiente de Marketing.
/// </summary>
[Serializable]
public sealed class BistroBuilderGuestRelationsSnapshot
{
    public const string CurrentSchemaId = "guest_relations.state";
    public const int CurrentSchemaVersion = 1;
    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public int reputationPoints;
    public int nextCohortSequence = 1;
    public List<string> appliedReputationSourceIds = new List<string>();
    public List<BistroBuilderGuestVisitCohortRecord> cohorts =
        new List<BistroBuilderGuestVisitCohortRecord>();

    public BistroBuilderGuestRelationsSnapshot DeepClone()
    {
        var clone = new BistroBuilderGuestRelationsSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            reputationPoints = reputationPoints,
            nextCohortSequence = nextCohortSequence,
            appliedReputationSourceIds = appliedReputationSourceIds != null
                ? new List<string>(appliedReputationSourceIds)
                : null,
            cohorts = new List<BistroBuilderGuestVisitCohortRecord>()
        };

        if (cohorts != null)
            for (int i = 0; i < cohorts.Count; i++)
                clone.cohorts.Add(cohorts[i]?.DeepClone());
        return clone;
    }
}
