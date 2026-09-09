using System;
using System.Collections.Generic;

public enum BistroBuilderReputationAspect
{
    Service = 0,
    WaitingTime = 1,
    FoodQuality = 2,
    ValueForMoney = 3,
    Ambience = 4
}

public enum BistroBuilderCustomerSatisfactionBand
{
    VeryBad = 0,
    Bad = 1,
    Neutral = 2,
    Good = 3,
    Excellent = 4
}

public enum BistroBuilderRestaurantDiscoverySource
{
    Organic = 0,
    Marketing = 1,
    WordOfMouth = 2,
    ReturningGuest = 3,
    Reservation = 4
}

[Serializable]
public sealed class BistroBuilderReputationAspectState
{
    public BistroBuilderReputationAspect aspect;
    public int scoreBasisPoints = 5000;
    public long evidenceWeight;

    public BistroBuilderReputationAspectState DeepClone() =>
        (BistroBuilderReputationAspectState)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderCustomerExperienceRecord
{
    public string experienceId = string.Empty;
    public int dayIndex = 1;
    public string segmentId = "general";
    public int partySize = 1;
    public BistroBuilderRestaurantDiscoverySource discoverySource;
    public float tableWaitSeconds;
    public float waiterWaitSeconds;
    public float foodWaitSeconds;
    public float billWaitSeconds;
    public int serviceScoreBasisPoints = 5000;
    public int waitingScoreBasisPoints = 5000;
    public int foodQualityScoreBasisPoints = 5000;
    public int valueForMoneyScoreBasisPoints = 5000;
    public int ambienceScoreBasisPoints = 5000;
    public int overallSatisfactionBasisPoints = 5000;

    public BistroBuilderCustomerExperienceRecord DeepClone() =>
        (BistroBuilderCustomerExperienceRecord)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderReputationReviewRecord
{
    public string reviewId = string.Empty;
    public string experienceId = string.Empty;
    public int dayIndex = 1;
    public int stars = 3;
    public int sentimentBasisPoints;
    public string summaryKey = string.Empty;

    public BistroBuilderReputationReviewRecord DeepClone() =>
        (BistroBuilderReputationReviewRecord)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderReputationSnapshot
{
    public const string CurrentSchemaId = "reputation.state";
    public const int CurrentSchemaVersion = 1;
    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public int globalScoreBasisPoints = 5000;
    public int externalReputationPoints;
    public int wordOfMouthBasisPoints;
    public int totalExperiences;
    public int positiveExperiences;
    public int negativeExperiences;
    public int organicDiscoveries;
    public int marketingDiscoveries;
    public int wordOfMouthDiscoveries;
    public int returningGuestDiscoveries;
    public int reservationDiscoveries;
    public int nextReviewSequence = 1;
    public List<BistroBuilderReputationAspectState> aspects = new List<BistroBuilderReputationAspectState>();
    public List<string> appliedExternalSourceIds = new List<string>();
    public List<BistroBuilderCustomerExperienceRecord> recentExperiences = new List<BistroBuilderCustomerExperienceRecord>();
    public List<BistroBuilderReputationReviewRecord> reviews = new List<BistroBuilderReputationReviewRecord>();

    public BistroBuilderReputationSnapshot DeepClone()
    {
        var clone = new BistroBuilderReputationSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            globalScoreBasisPoints = globalScoreBasisPoints,
            externalReputationPoints = externalReputationPoints,
            wordOfMouthBasisPoints = wordOfMouthBasisPoints,
            totalExperiences = totalExperiences,
            positiveExperiences = positiveExperiences,
            negativeExperiences = negativeExperiences,
            organicDiscoveries = organicDiscoveries,
            marketingDiscoveries = marketingDiscoveries,
            wordOfMouthDiscoveries = wordOfMouthDiscoveries,
            returningGuestDiscoveries = returningGuestDiscoveries,
            reservationDiscoveries = reservationDiscoveries,
            nextReviewSequence = nextReviewSequence,
            aspects = new List<BistroBuilderReputationAspectState>(),
            appliedExternalSourceIds = appliedExternalSourceIds != null ? new List<string>(appliedExternalSourceIds) : null,
            recentExperiences = new List<BistroBuilderCustomerExperienceRecord>(),
            reviews = new List<BistroBuilderReputationReviewRecord>()
        };
        if (aspects != null) foreach (var item in aspects) clone.aspects.Add(item?.DeepClone());
        if (recentExperiences != null) foreach (var item in recentExperiences) clone.recentExperiences.Add(item?.DeepClone());
        if (reviews != null) foreach (var item in reviews) clone.reviews.Add(item?.DeepClone());
        return clone;
    }
}
