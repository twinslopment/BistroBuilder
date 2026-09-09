using System;
using System.Collections.Generic;

[Serializable]
public sealed class BistroBuilderReputationPlayerReviewRow
{
    public string reviewId = string.Empty;
    public int dayIndex;
    public int stars;
    public int sentimentBasisPoints;
    public string summaryKey = string.Empty;
}

[Serializable]
public sealed class BistroBuilderReputationPlayerUiSnapshot
{
    public int dayIndex;
    public long reputationRevision;
    public int globalScoreBasisPoints;
    public BistroBuilderCustomerSatisfactionBand satisfactionBand;
    public int serviceScoreBasisPoints;
    public int waitingScoreBasisPoints;
    public int foodScoreBasisPoints;
    public int valueScoreBasisPoints;
    public int ambienceScoreBasisPoints;
    public int wordOfMouthBasisPoints;
    public int persistentDemandBasisPoints;
    public int organicRepeatVisitBasisPoints;
    public int totalExperiences;
    public int positiveExperiences;
    public int negativeExperiences;
    public int reviewCount;
    public int activeVisitCount;
    public int recurrentCohortCount;
    public int organicDiscoveries;
    public int marketingDiscoveries;
    public int wordOfMouthDiscoveries;
    public int returningGuestDiscoveries;
    public int reservationDiscoveries;
    public int latestSatisfactionBasisPoints;
    public List<BistroBuilderReputationPlayerReviewRow> recentReviews =
        new List<BistroBuilderReputationPlayerReviewRow>();
}
