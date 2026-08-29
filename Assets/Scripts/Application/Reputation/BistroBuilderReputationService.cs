using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad canónica del Bloque 8 para reputación del restaurante.
/// GuestRelations conserva cohortes; Marketing y gameplay aportan evidencia aquí.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Reputation/Reputation Service")]
public sealed class BistroBuilderReputationService : MonoBehaviour
{
    private BistroBuilderReputationSnapshot state;

    public event Action<long> ReputationChanged;
    public event Action ReputationRestored;

    public long Revision => state != null ? state.revision : 0L;
    public int GlobalScoreBasisPoints => state != null
        ? state.globalScoreBasisPoints
        : BistroBuilderReputationEngine.NeutralScoreBasisPoints;
    public int ExternalReputationPoints => state != null
        ? state.externalReputationPoints
        : 0;
    public int WordOfMouthBasisPoints => state != null
        ? state.wordOfMouthBasisPoints
        : 0;
    public int PersistentDemandBasisPoints =>
        BistroBuilderReputationEngine.ComputePersistentDemandBasisPoints(state);
    public int OrganicRepeatVisitBasisPoints =>
        BistroBuilderReputationEngine.ComputeOrganicRepeatVisitBasisPoints(state);
    public int TotalExperiences => state != null ? state.totalExperiences : 0;
    public int PositiveExperiences => state != null ? state.positiveExperiences : 0;
    public int NegativeExperiences => state != null ? state.negativeExperiences : 0;
    public int ReviewCount => state?.reviews != null ? state.reviews.Count : 0;
    public int OrganicDiscoveries => state != null ? state.organicDiscoveries : 0;
    public int MarketingDiscoveries => state != null ? state.marketingDiscoveries : 0;
    public int WordOfMouthDiscoveries => state != null ? state.wordOfMouthDiscoveries : 0;
    public int ReturningGuestDiscoveries => state != null ? state.returningGuestDiscoveries : 0;
    public int ReservationDiscoveries => state != null ? state.reservationDiscoveries : 0;

    private void Awake() => EnsureState();

    public bool ValidateConfiguration(out string error)
    {
        EnsureState();
        return BistroBuilderReputationEngine.TryValidateSnapshot(state, out error);
    }

    public BistroBuilderReputationSnapshot CreateSnapshot()
    {
        EnsureState();
        return state.DeepClone();
    }

    public bool TryRestoreSnapshot(
        BistroBuilderReputationSnapshot snapshot,
        out string error)
    {
        if (!BistroBuilderReputationEngine.TryValidateSnapshot(snapshot, out error))
            return false;
        state = snapshot.DeepClone();
        ReputationRestored?.Invoke();
        ReputationChanged?.Invoke(state.revision);
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        state = BistroBuilderReputationEngine.CreateInitialSnapshot();
        ReputationRestored?.Invoke();
        ReputationChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryApplyExternalReputationCredit(
        string sourceId,
        int points,
        out bool changed,
        out string error)
    {
        EnsureState();
        if (!BistroBuilderReputationEngine.TryApplyExternalReputationPoints(
                state, sourceId, points,
                out BistroBuilderReputationSnapshot candidate,
                out changed, out error))
            return false;
        if (!changed) return true;
        state = candidate;
        ReputationChanged?.Invoke(state.revision);
        return true;
    }

    public bool TryRecordExperience(
        BistroBuilderCustomerExperienceRecord experience,
        out bool changed,
        out string error)
    {
        EnsureState();
        if (!BistroBuilderReputationEngine.TryApplyExperience(
                state, experience,
                out BistroBuilderReputationSnapshot candidate,
                out changed, out error))
            return false;
        if (!changed) return true;
        state = candidate;
        ReputationChanged?.Invoke(state.revision);
        return true;
    }

    public int GetAspectScore(BistroBuilderReputationAspect aspect)
    {
        EnsureState();
        return BistroBuilderReputationEngine.GetAspectScore(state, aspect);
    }

    public void CopyAspectStates(List<BistroBuilderReputationAspectState> destination)
    {
        if (destination == null) return;
        destination.Clear();
        EnsureState();
        for (int i = 0; i < state.aspects.Count; i++)
            destination.Add(state.aspects[i]?.DeepClone());
    }

    public void CopyRecentExperiences(List<BistroBuilderCustomerExperienceRecord> destination)
    {
        if (destination == null) return;
        destination.Clear();
        EnsureState();
        for (int i = 0; i < state.recentExperiences.Count; i++)
            destination.Add(state.recentExperiences[i]?.DeepClone());
    }

    public void CopyReviews(List<BistroBuilderReputationReviewRecord> destination)
    {
        if (destination == null) return;
        destination.Clear();
        EnsureState();
        for (int i = 0; i < state.reviews.Count; i++)
            destination.Add(state.reviews[i]?.DeepClone());
    }

    private void EnsureState()
    {
        if (state == null)
            state = BistroBuilderReputationEngine.CreateInitialSnapshot();
    }
}
