using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad persistente de reputación y relaciones con clientes.
/// Registra visitas completadas y conserva cohortes reutilizables entre días.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Customers/Guest Relations Service")]
public sealed class BistroBuilderGuestRelationsService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;

    [SerializeField]
    private BistroBuilderReputationService reputationService;

    private BistroBuilderGuestRelationsSnapshot state;
    private readonly HashSet<CustomerGroup> trackedGroups =
        new HashSet<CustomerGroup>();
    private readonly List<CustomerGroup> staleGroups =
        new List<CustomerGroup>(16);

    public event Action<long> RelationsChanged;
    public event Action RelationsRestored;

    public long Revision => state != null ? state.revision : 0L;
    public int ReputationPoints => reputationService != null
        ? reputationService.ExternalReputationPoints
        : (state != null ? state.reputationPoints : 0);
    public int LegacyStoredReputationPoints => state != null ? state.reputationPoints : 0;
    public int CohortCount => state != null && state.cohorts != null
        ? state.cohorts.Count
        : 0;
    public int ReputationDemandBasisPoints => reputationService != null
        ? reputationService.PersistentDemandBasisPoints
        : BistroBuilderGuestRelationsEngine.ComputeReputationDemandBasisPoints(
            ReputationPoints);

    private void Awake()
    {
        EnsureState();
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        SynchronizeTrackedGroups();
    }

    private void Update()
    {
        SynchronizeTrackedGroups();
    }

    private void OnDisable()
    {
        foreach (CustomerGroup group in trackedGroups)
            if (group != null)
                group.StateChanged -= HandleGroupStateChanged;
        trackedGroups.Clear();
        staleGroups.Clear();
    }

    public bool ValidateConfiguration(out string error)
    {
        EnsureState();
        CacheDependencies();
        if (generalGameStateService == null || tableAssignmentSystem == null ||
            reputationService == null)
        {
            error = "GuestRelations necesita calendario, TableAssignmentSystem y Reputación.";
            return false;
        }

        if (!generalGameStateService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error) ||
            !BistroBuilderGuestRelationsEngine.TryValidateSnapshot(state, out error))
            return false;

        error = string.Empty;
        return true;
    }

    public BistroBuilderGuestRelationsSnapshot CreateSnapshot()
    {
        EnsureState();
        return state.DeepClone();
    }

    public bool TryRestoreSnapshot(
        BistroBuilderGuestRelationsSnapshot snapshot,
        out string error)
    {
        if (!BistroBuilderGuestRelationsEngine.TryValidateSnapshot(
                snapshot,
                out error))
            return false;

        state = snapshot.DeepClone();
        RelationsRestored?.Invoke();
        RelationsChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        state = BistroBuilderGuestRelationsEngine.CreateEmptySnapshot();
        RelationsRestored?.Invoke();
        RelationsChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryApplyReputationCredit(
        string sourceId,
        int points,
        out bool changed,
        out string error)
    {
        CacheDependencies();
        if (reputationService == null)
        {
            changed = false;
            error = "La autoridad canónica de Reputación no está instalada.";
            return false;
        }
        return reputationService.TryApplyExternalReputationCredit(
            sourceId, points, out changed, out error);
    }
    public void CopyEligibleCohorts(
        int dayIndex,
        List<BistroBuilderGuestVisitCohortRecord> destination)
    {
        EnsureState();
        BistroBuilderGuestRelationsEngine.CopyEligibleCohorts(
            state,
            dayIndex,
            destination);
    }

    public bool TryRecordCompletedVisit(
        string segmentId,
        int partySize,
        string returningCohortId,
        out string cohortId,
        out string error)
    {
        EnsureState();
        int dayIndex = generalGameStateService != null
            ? generalGameStateService.DayIndex
            : 0;
        if (!BistroBuilderGuestRelationsEngine.TryRecordCompletedVisit(
                state,
                segmentId,
                partySize,
                dayIndex,
                returningCohortId,
                out BistroBuilderGuestRelationsSnapshot candidate,
                out cohortId,
                out error))
            return false;

        state = candidate;
        RelationsChanged?.Invoke(state.revision);
        return true;
    }

    private void SynchronizeTrackedGroups()
    {
        if (tableAssignmentSystem == null)
        {
            CacheDependencies();
            if (tableAssignmentSystem == null)
                return;
        }

        IReadOnlyList<CustomerGroup> groups =
            tableAssignmentSystem.RegisteredGroups;
        for (int index = 0; index < groups.Count; index++)
        {
            CustomerGroup group = groups[index];
            if (group == null || !trackedGroups.Add(group))
                continue;

            group.StateChanged -= HandleGroupStateChanged;
            group.StateChanged += HandleGroupStateChanged;
        }

        staleGroups.Clear();
        foreach (CustomerGroup tracked in trackedGroups)
            if (tracked == null || !ContainsReference(groups, tracked))
                staleGroups.Add(tracked);

        for (int index = 0; index < staleGroups.Count; index++)
        {
            CustomerGroup stale = staleGroups[index];
            if (stale != null)
                stale.StateChanged -= HandleGroupStateChanged;
            trackedGroups.Remove(stale);
        }
    }

    private void HandleGroupStateChanged(
        CustomerGroup group,
        CustomerGroupState newState)
    {
        if (group == null || newState != CustomerGroupState.Finished)
            return;

        BistroBuilderCustomerAcquisitionTag tag =
            group.GetComponent<BistroBuilderCustomerAcquisitionTag>();
        BistroBuilderCustomerAcquisitionProfile profile = tag != null
            ? tag.CreateSnapshot()
            : BistroBuilderCustomerAcquisitionProfile.CreateBaseline();

        string returningReference = profile != null && profile.returningVisit
            ? profile.guestRelationsReferenceId
            : string.Empty;
        string segmentId = profile != null ? profile.segmentId : "general";

        if (!TryRecordCompletedVisit(
                segmentId,
                group.GroupSize,
                returningReference,
                out _,
                out string error))
        {
            Debug.LogError(
                "GuestRelations no pudo registrar una visita: " + error,
                this);
        }
    }

    private static bool ContainsReference(
        IReadOnlyList<CustomerGroup> groups,
        CustomerGroup target)
    {
        for (int index = 0; index < groups.Count; index++)
            if (ReferenceEquals(groups[index], target))
                return true;
        return false;
    }

    private void EnsureState()
    {
        if (state == null)
            state = BistroBuilderGuestRelationsEngine.CreateEmptySnapshot();
    }

    private void CacheDependencies()
    {
        if (generalGameStateService == null)
            TryGetComponent(out generalGameStateService);
        if (tableAssignmentSystem == null)
            tableAssignmentSystem = FindFirstObjectByType<TableAssignmentSystem>();
        if (reputationService == null)
            TryGetComponent(out reputationService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
