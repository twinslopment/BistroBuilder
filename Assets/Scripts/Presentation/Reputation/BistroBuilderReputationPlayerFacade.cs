using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada consultiva de la UI de Reputación. No muta gameplay.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Reputation/Reputation Player Facade")]
public sealed class BistroBuilderReputationPlayerFacade : MonoBehaviour
{
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private BistroBuilderGuestRelationsService guestRelationsService;
    [SerializeField] private BistroBuilderCustomerExperienceTrackingService trackingService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;

    private readonly List<BistroBuilderReputationReviewRecord> reviews =
        new List<BistroBuilderReputationReviewRecord>(16);

    public event Action ViewInvalidated;

    private void Awake() => CacheDependencies();
    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }
    private void OnDisable() => Unsubscribe();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (reputationService == null || guestRelationsService == null ||
            trackingService == null || generalGameStateService == null)
        {
            error = "La UI de Reputación necesita Reputación, GuestRelations, Experience Tracking y calendario.";
            return false;
        }
        if (!reputationService.ValidateConfiguration(out error) ||
            !guestRelationsService.ValidateConfiguration(out error) ||
            !trackingService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool TryBuildSnapshot(
        out BistroBuilderReputationPlayerUiSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!ValidateConfiguration(out error))
            return false;

        var built = new BistroBuilderReputationPlayerUiSnapshot
        {
            dayIndex = generalGameStateService.DayIndex,
            reputationRevision = reputationService.Revision,
            globalScoreBasisPoints = reputationService.GlobalScoreBasisPoints,
            satisfactionBand = BistroBuilderReputationEngine.GetSatisfactionBand(
                reputationService.GlobalScoreBasisPoints),
            serviceScoreBasisPoints = reputationService.GetAspectScore(
                BistroBuilderReputationAspect.Service),
            waitingScoreBasisPoints = reputationService.GetAspectScore(
                BistroBuilderReputationAspect.WaitingTime),
            foodScoreBasisPoints = reputationService.GetAspectScore(
                BistroBuilderReputationAspect.FoodQuality),
            valueScoreBasisPoints = reputationService.GetAspectScore(
                BistroBuilderReputationAspect.ValueForMoney),
            ambienceScoreBasisPoints = reputationService.GetAspectScore(
                BistroBuilderReputationAspect.Ambience),
            wordOfMouthBasisPoints = reputationService.WordOfMouthBasisPoints,
            persistentDemandBasisPoints = reputationService.PersistentDemandBasisPoints,
            organicRepeatVisitBasisPoints = reputationService.OrganicRepeatVisitBasisPoints,
            totalExperiences = reputationService.TotalExperiences,
            positiveExperiences = reputationService.PositiveExperiences,
            negativeExperiences = reputationService.NegativeExperiences,
            reviewCount = reputationService.ReviewCount,
            activeVisitCount = trackingService.ActiveVisitCount,
            recurrentCohortCount = guestRelationsService.CohortCount,
            organicDiscoveries = reputationService.OrganicDiscoveries,
            marketingDiscoveries = reputationService.MarketingDiscoveries,
            wordOfMouthDiscoveries = reputationService.WordOfMouthDiscoveries,
            returningGuestDiscoveries = reputationService.ReturningGuestDiscoveries,
            reservationDiscoveries = reputationService.ReservationDiscoveries,
            latestSatisfactionBasisPoints = trackingService.LastRecordedSatisfactionBasisPoints
        };

        reviews.Clear();
        reputationService.CopyReviews(reviews);
        int first = Math.Max(0, reviews.Count - 8);
        for (int i = reviews.Count - 1; i >= first; i--)
        {
            BistroBuilderReputationReviewRecord review = reviews[i];
            if (review == null) continue;
            built.recentReviews.Add(new BistroBuilderReputationPlayerReviewRow
            {
                reviewId = review.reviewId,
                dayIndex = review.dayIndex,
                stars = review.stars,
                sentimentBasisPoints = review.sentimentBasisPoints,
                summaryKey = review.summaryKey
            });
        }

        snapshot = built;
        error = string.Empty;
        return true;
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (reputationService != null)
        {
            reputationService.ReputationChanged += HandleRevision;
            reputationService.ReputationRestored += HandleChanged;
        }
        if (guestRelationsService != null)
        {
            guestRelationsService.RelationsChanged += HandleRevision;
            guestRelationsService.RelationsRestored += HandleChanged;
        }
        if (trackingService != null)
            trackingService.ExperienceRuntimeChanged += HandleChanged;
        if (generalGameStateService != null)
            generalGameStateService.CalendarChanged += HandleChanged;
    }

    private void Unsubscribe()
    {
        if (reputationService != null)
        {
            reputationService.ReputationChanged -= HandleRevision;
            reputationService.ReputationRestored -= HandleChanged;
        }
        if (guestRelationsService != null)
        {
            guestRelationsService.RelationsChanged -= HandleRevision;
            guestRelationsService.RelationsRestored -= HandleChanged;
        }
        if (trackingService != null)
            trackingService.ExperienceRuntimeChanged -= HandleChanged;
        if (generalGameStateService != null)
            generalGameStateService.CalendarChanged -= HandleChanged;
    }

    private void HandleRevision(long _) => ViewInvalidated?.Invoke();
    private void HandleChanged() => ViewInvalidated?.Invoke();

    private void CacheDependencies()
    {
        if (reputationService == null) TryGetComponent(out reputationService);
        if (guestRelationsService == null) TryGetComponent(out guestRelationsService);
        if (trackingService == null) TryGetComponent(out trackingService);
        if (generalGameStateService == null) TryGetComponent(out generalGameStateService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
