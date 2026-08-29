using System;
using System.Collections.Generic;

/// <summary>
/// Motor puro de reputación. No conoce escena, Marketing, Finanzas ni NPCs.
/// Agrega experiencias verificables y mantiene reputación global y por aspectos.
/// </summary>
public static class BistroBuilderReputationEngine
{
    public const int MinimumScoreBasisPoints = 0;
    public const int NeutralScoreBasisPoints = 5000;
    public const int MaximumScoreBasisPoints = 10000;
    public const int MaximumExternalReputationPoints = 100;
    public const int MaximumPersistentDemandBasisPoints = 5000;
    public const int MaximumAppliedExternalSources = 512;
    public const int MaximumRecentExperiences = 256;
    public const int MaximumReviews = 128;

    public static BistroBuilderReputationSnapshot CreateInitialSnapshot()
    {
        var snapshot = new BistroBuilderReputationSnapshot();
        foreach (BistroBuilderReputationAspect aspect in
                 Enum.GetValues(typeof(BistroBuilderReputationAspect)))
        {
            snapshot.aspects.Add(new BistroBuilderReputationAspectState
            {
                aspect = aspect,
                scoreBasisPoints = NeutralScoreBasisPoints,
                evidenceWeight = 0L
            });
        }
        return snapshot;
    }

    public static bool TryValidateSnapshot(
        BistroBuilderReputationSnapshot snapshot,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(snapshot.schemaId, BistroBuilderReputationSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderReputationSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 0L ||
            !IsScore(snapshot.globalScoreBasisPoints) ||
            snapshot.externalReputationPoints < 0 ||
            snapshot.externalReputationPoints > MaximumExternalReputationPoints ||
            snapshot.wordOfMouthBasisPoints < -MaximumPersistentDemandBasisPoints ||
            snapshot.wordOfMouthBasisPoints > MaximumPersistentDemandBasisPoints ||
            snapshot.totalExperiences < 0 || snapshot.positiveExperiences < 0 ||
            snapshot.negativeExperiences < 0 || snapshot.organicDiscoveries < 0 ||
            snapshot.marketingDiscoveries < 0 || snapshot.wordOfMouthDiscoveries < 0 ||
            snapshot.returningGuestDiscoveries < 0 || snapshot.reservationDiscoveries < 0 ||
            snapshot.nextReviewSequence < 1 ||
            snapshot.aspects == null || snapshot.appliedExternalSourceIds == null ||
            snapshot.recentExperiences == null || snapshot.reviews == null)
        {
            error = "reputation.state contiene una cabecera inválida.";
            return false;
        }

        if (snapshot.aspects.Count != Enum.GetValues(typeof(BistroBuilderReputationAspect)).Length ||
            snapshot.appliedExternalSourceIds.Count > MaximumAppliedExternalSources ||
            snapshot.recentExperiences.Count > MaximumRecentExperiences ||
            snapshot.reviews.Count > MaximumReviews)
        {
            error = "reputation.state excede límites o no contiene todos los aspectos.";
            return false;
        }

        var aspects = new HashSet<BistroBuilderReputationAspect>();
        for (int i = 0; i < snapshot.aspects.Count; i++)
        {
            BistroBuilderReputationAspectState item = snapshot.aspects[i];
            if (item == null || !Enum.IsDefined(typeof(BistroBuilderReputationAspect), item.aspect) ||
                !aspects.Add(item.aspect) || !IsScore(item.scoreBasisPoints) ||
                item.evidenceWeight < 0L)
            {
                error = "Existe un aspecto de reputación inválido o duplicado.";
                return false;
            }
        }

        var sources = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.appliedExternalSourceIds.Count; i++)
        {
            string id = NormalizeId(snapshot.appliedExternalSourceIds[i]);
            if (!IsSafeId(id) || !sources.Add(id))
            {
                error = "Existe una fuente externa de reputación inválida o duplicada.";
                return false;
            }
        }

        var experiences = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.recentExperiences.Count; i++)
        {
            BistroBuilderCustomerExperienceRecord item = snapshot.recentExperiences[i];
            if (!ValidateExperience(item, experiences, out error)) return false;
        }

        var reviews = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.reviews.Count; i++)
        {
            BistroBuilderReputationReviewRecord item = snapshot.reviews[i];
            if (item == null || !IsSafeId(NormalizeId(item.reviewId)) ||
                !reviews.Add(NormalizeId(item.reviewId)) || item.dayIndex < 1 ||
                item.stars < 1 || item.stars > 5 ||
                item.sentimentBasisPoints < -10000 || item.sentimentBasisPoints > 10000)
            {
                error = "Existe una reseña inválida o duplicada.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryApplyExternalReputationPoints(
        BistroBuilderReputationSnapshot source,
        string sourceId,
        int points,
        out BistroBuilderReputationSnapshot candidate,
        out bool changed,
        out string error)
    {
        candidate = null;
        changed = false;
        if (!TryValidateSnapshot(source, out error)) return false;
        string id = NormalizeId(sourceId);
        if (!IsSafeId(id) || points < -100 || points > 100)
        {
            error = "La aportación externa de reputación es inválida.";
            return false;
        }

        candidate = source.DeepClone();
        if (candidate.appliedExternalSourceIds.Contains(id))
        {
            error = string.Empty;
            return true;
        }

        candidate.appliedExternalSourceIds.Add(id);
        if (candidate.appliedExternalSourceIds.Count > MaximumAppliedExternalSources)
            candidate.appliedExternalSourceIds.RemoveAt(0);
        candidate.externalReputationPoints = Math.Max(0,
            Math.Min(MaximumExternalReputationPoints,
                candidate.externalReputationPoints + points));
        candidate.revision = checked(source.revision + 1L);
        changed = true;
        return TryValidateSnapshot(candidate, out error);
    }

    public static bool TryApplyExperience(
        BistroBuilderReputationSnapshot source,
        BistroBuilderCustomerExperienceRecord experience,
        out BistroBuilderReputationSnapshot candidate,
        out bool changed,
        out string error)
    {
        candidate = null;
        changed = false;
        if (!TryValidateSnapshot(source, out error)) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!ValidateExperience(experience, ids, out error)) return false;

        string id = NormalizeId(experience.experienceId);
        for (int i = 0; i < source.recentExperiences.Count; i++)
            if (NormalizeId(source.recentExperiences[i].experienceId) == id)
            {
                candidate = source.DeepClone();
                error = string.Empty;
                return true;
            }

        candidate = source.DeepClone();
        int weight = Math.Max(1, Math.Min(32, experience.partySize));
        UpdateAspect(candidate, BistroBuilderReputationAspect.Service,
            experience.serviceScoreBasisPoints, weight);
        UpdateAspect(candidate, BistroBuilderReputationAspect.WaitingTime,
            experience.waitingScoreBasisPoints, weight);
        UpdateAspect(candidate, BistroBuilderReputationAspect.FoodQuality,
            experience.foodQualityScoreBasisPoints, weight);
        UpdateAspect(candidate, BistroBuilderReputationAspect.ValueForMoney,
            experience.valueForMoneyScoreBasisPoints, weight);
        UpdateAspect(candidate, BistroBuilderReputationAspect.Ambience,
            experience.ambienceScoreBasisPoints, weight);

        candidate.globalScoreBasisPoints = ComputeGlobalScore(candidate);
        candidate.wordOfMouthBasisPoints = ComputeWordOfMouthBasisPoints(
            candidate.globalScoreBasisPoints);
        candidate.totalExperiences = checked(candidate.totalExperiences + 1);
        if (experience.overallSatisfactionBasisPoints >= 6500)
            candidate.positiveExperiences = checked(candidate.positiveExperiences + 1);
        else if (experience.overallSatisfactionBasisPoints <= 3500)
            candidate.negativeExperiences = checked(candidate.negativeExperiences + 1);

        IncrementDiscovery(candidate, experience.discoverySource);
        candidate.recentExperiences.Add(experience.DeepClone());
        if (candidate.recentExperiences.Count > MaximumRecentExperiences)
            candidate.recentExperiences.RemoveAt(0);

        candidate.reviews.Add(CreateReview(candidate, experience));
        candidate.nextReviewSequence = checked(candidate.nextReviewSequence + 1);
        if (candidate.reviews.Count > MaximumReviews)
            candidate.reviews.RemoveAt(0);
        candidate.revision = checked(source.revision + 1L);
        changed = true;
        return TryValidateSnapshot(candidate, out error);
    }

    public static int ComputePersistentDemandBasisPoints(
        BistroBuilderReputationSnapshot snapshot)
    {
        if (snapshot == null) return 0;
        long value = (long)snapshot.externalReputationPoints * 100L +
                     snapshot.wordOfMouthBasisPoints;
        return (int)Math.Max(-MaximumPersistentDemandBasisPoints,
            Math.Min(MaximumPersistentDemandBasisPoints, value));
    }

    public static int ComputeWordOfMouthBasisPoints(int globalScoreBasisPoints)
    {
        int safe = Math.Max(MinimumScoreBasisPoints,
            Math.Min(MaximumScoreBasisPoints, globalScoreBasisPoints));
        return (safe - NeutralScoreBasisPoints) * 2 / 5;
    }

    public static int ComputeOrganicRepeatVisitBasisPoints(
        BistroBuilderReputationSnapshot snapshot)
    {
        if (snapshot == null || snapshot.totalExperiences <= 0)
            return 0;

        int excess = Math.Max(0, snapshot.globalScoreBasisPoints - 6000);
        int positiveShare = snapshot.positiveExperiences * 10000 /
            Math.Max(1, snapshot.totalExperiences);
        int result = excess / 2 + Math.Max(0, positiveShare - 5000) / 5;
        return Math.Max(0, Math.Min(2500, result));
    }

    private static void IncrementDiscovery(
        BistroBuilderReputationSnapshot snapshot,
        BistroBuilderRestaurantDiscoverySource source)
    {
        switch (source)
        {
            case BistroBuilderRestaurantDiscoverySource.Marketing:
                snapshot.marketingDiscoveries++;
                break;
            case BistroBuilderRestaurantDiscoverySource.WordOfMouth:
                snapshot.wordOfMouthDiscoveries++;
                break;
            case BistroBuilderRestaurantDiscoverySource.ReturningGuest:
                snapshot.returningGuestDiscoveries++;
                break;
            case BistroBuilderRestaurantDiscoverySource.Reservation:
                snapshot.reservationDiscoveries++;
                break;
            default:
                snapshot.organicDiscoveries++;
                break;
        }
    }

    private static BistroBuilderReputationReviewRecord CreateReview(
        BistroBuilderReputationSnapshot snapshot,
        BistroBuilderCustomerExperienceRecord experience)
    {
        int score = experience.overallSatisfactionBasisPoints;
        int stars = Math.Max(1, Math.Min(5,
            1 + (int)Math.Round(score / 2500d,
                MidpointRounding.AwayFromZero)));
        int sentiment = Math.Max(-10000, Math.Min(10000,
            (score - NeutralScoreBasisPoints) * 2));
        return new BistroBuilderReputationReviewRecord
        {
            reviewId = "review." + snapshot.nextReviewSequence.ToString("D6"),
            experienceId = NormalizeId(experience.experienceId),
            dayIndex = experience.dayIndex,
            stars = stars,
            sentimentBasisPoints = sentiment,
            summaryKey = BuildReviewSummaryKey(experience)
        };
    }

    private static string BuildReviewSummaryKey(
        BistroBuilderCustomerExperienceRecord experience)
    {
        int[] scores =
        {
            experience.serviceScoreBasisPoints,
            experience.waitingScoreBasisPoints,
            experience.foodQualityScoreBasisPoints,
            experience.valueForMoneyScoreBasisPoints,
            experience.ambienceScoreBasisPoints
        };
        string[] keys = { "service", "waiting", "food", "value", "ambience" };
        bool positive = experience.overallSatisfactionBasisPoints >= 5000;
        int selected = 0;
        for (int i = 1; i < scores.Length; i++)
        {
            if (positive ? scores[i] > scores[selected] : scores[i] < scores[selected])
                selected = i;
        }
        return (positive ? "positive." : "negative.") + keys[selected];
    }

    public static BistroBuilderCustomerSatisfactionBand GetSatisfactionBand(int score)
    {
        if (score < 2500) return BistroBuilderCustomerSatisfactionBand.VeryBad;
        if (score < 4500) return BistroBuilderCustomerSatisfactionBand.Bad;
        if (score < 6500) return BistroBuilderCustomerSatisfactionBand.Neutral;
        if (score < 8500) return BistroBuilderCustomerSatisfactionBand.Good;
        return BistroBuilderCustomerSatisfactionBand.Excellent;
    }

    public static int ScoreWaitSeconds(float seconds, float goodSeconds, float badSeconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f ||
            goodSeconds < 0f || badSeconds <= goodSeconds)
            return NeutralScoreBasisPoints;
        if (seconds <= goodSeconds) return 9000;
        if (seconds >= badSeconds) return 1500;
        double t = (seconds - goodSeconds) / (badSeconds - goodSeconds);
        return (int)Math.Round(9000d + (1500d - 9000d) * t,
            MidpointRounding.AwayFromZero);
    }

    private static void UpdateAspect(
        BistroBuilderReputationSnapshot snapshot,
        BistroBuilderReputationAspect aspect,
        int score,
        int weight)
    {
        BistroBuilderReputationAspectState state = FindAspect(snapshot, aspect);
        long total = state.evidenceWeight + weight;
        long weighted = (long)state.scoreBasisPoints * state.evidenceWeight +
                        (long)score * weight;
        state.scoreBasisPoints = total > 0L
            ? (int)Math.Round(weighted / (double)total, MidpointRounding.AwayFromZero)
            : NeutralScoreBasisPoints;
        state.evidenceWeight = total;
    }

    public static int GetAspectScore(
        BistroBuilderReputationSnapshot snapshot,
        BistroBuilderReputationAspect aspect)
    {
        BistroBuilderReputationAspectState state = FindAspect(snapshot, aspect);
        return state != null ? state.scoreBasisPoints : NeutralScoreBasisPoints;
    }

    private static int ComputeGlobalScore(BistroBuilderReputationSnapshot snapshot)
    {
        long sum =
            (long)GetAspectScore(snapshot, BistroBuilderReputationAspect.FoodQuality) * 35L +
            (long)GetAspectScore(snapshot, BistroBuilderReputationAspect.Service) * 25L +
            (long)GetAspectScore(snapshot, BistroBuilderReputationAspect.WaitingTime) * 15L +
            (long)GetAspectScore(snapshot, BistroBuilderReputationAspect.ValueForMoney) * 20L +
            (long)GetAspectScore(snapshot, BistroBuilderReputationAspect.Ambience) * 5L;
        return (int)Math.Round(sum / 100d, MidpointRounding.AwayFromZero);
    }

    private static BistroBuilderReputationAspectState FindAspect(
        BistroBuilderReputationSnapshot snapshot,
        BistroBuilderReputationAspect aspect)
    {
        if (snapshot?.aspects == null) return null;
        for (int i = 0; i < snapshot.aspects.Count; i++)
            if (snapshot.aspects[i] != null && snapshot.aspects[i].aspect == aspect)
                return snapshot.aspects[i];
        return null;
    }

    private static bool ValidateExperience(
        BistroBuilderCustomerExperienceRecord item,
        HashSet<string> ids,
        out string error)
    {
        string id = item != null ? NormalizeId(item.experienceId) : string.Empty;
        if (item == null || !IsSafeId(id) || !ids.Add(id) || item.dayIndex < 1 ||
            item.partySize < 1 || item.partySize > 32 ||
            !Enum.IsDefined(typeof(BistroBuilderRestaurantDiscoverySource), item.discoverySource) ||
            !IsSafeId(NormalizeId(item.segmentId)) ||
            !IsFiniteNonNegative(item.tableWaitSeconds) ||
            !IsFiniteNonNegative(item.waiterWaitSeconds) ||
            !IsFiniteNonNegative(item.foodWaitSeconds) ||
            !IsFiniteNonNegative(item.billWaitSeconds) ||
            !IsScore(item.serviceScoreBasisPoints) ||
            !IsScore(item.waitingScoreBasisPoints) ||
            !IsScore(item.foodQualityScoreBasisPoints) ||
            !IsScore(item.valueForMoneyScoreBasisPoints) ||
            !IsScore(item.ambienceScoreBasisPoints) ||
            !IsScore(item.overallSatisfactionBasisPoints))
        {
            error = "Existe una experiencia de cliente inválida o duplicada.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool IsScore(int value) =>
        value >= MinimumScoreBasisPoints && value <= MaximumScoreBasisPoints;

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    public static string NormalizeId(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    public static bool IsSafeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')) return false;
        }
        return true;
    }
}
