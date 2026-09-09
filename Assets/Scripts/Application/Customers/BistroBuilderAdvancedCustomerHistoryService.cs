using System;
using UnityEngine;

/// <summary>
/// Autoridad 10C del historial avanzado de clientes. Une el resultado real de
/// Reputación con la cohorte persistente de GuestRelations sin duplicar ninguna.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Customers/Advanced Customer History Service")]
public sealed class BistroBuilderAdvancedCustomerHistoryService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderCustomerExperienceTrackingService experienceTrackingService;

    [SerializeField]
    private BistroBuilderGuestRelationsService guestRelationsService;

    private BistroBuilderAdvancedCustomerHistorySnapshot state;

    public event Action<long> HistoryChanged;
    public event Action HistoryRestored;

    public long Revision => state != null ? state.revision : 0L;
    public int CustomerRecordCount =>
        state != null && state.customers != null ? state.customers.Count : 0;
    public int PendingOutcomeCount =>
        state != null && state.pendingOutcomes != null ? state.pendingOutcomes.Count : 0;
    public int PendingAssignmentCount =>
        state != null && state.pendingAssignments != null ? state.pendingAssignments.Count : 0;
    public BistroBuilderCustomerExperienceTrackingService ExperienceTrackingService =>
        experienceTrackingService;
    public BistroBuilderGuestRelationsService GuestRelationsService =>
        guestRelationsService;

    private void Awake()
    {
        EnsureState();
        CacheDependencies();
    }

    private void OnEnable()
    {
        EnsureState();
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        EnsureState();
        CacheDependencies();
        if (experienceTrackingService == null || guestRelationsService == null)
        {
            error = "10C necesita Experience Tracking y GuestRelations canónicos.";
            return false;
        }

        if (!experienceTrackingService.ValidateConfiguration(out error) ||
            !guestRelationsService.ValidateConfiguration(out error) ||
            !BistroBuilderAdvancedCustomerHistoryEngine.TryValidateSnapshot(
                state, out error))
            return false;

        error = string.Empty;
        return true;
    }

    public BistroBuilderAdvancedCustomerHistorySnapshot CreateSnapshot()
    {
        EnsureState();
        return state.DeepClone();
    }

    public bool TryRestoreSnapshot(
        BistroBuilderAdvancedCustomerHistorySnapshot snapshot,
        out string error)
    {
        if (!BistroBuilderAdvancedCustomerHistoryEngine.TryValidateSnapshot(
                snapshot, out error))
            return false;

        state = snapshot.DeepClone();
        HistoryRestored?.Invoke();
        HistoryChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        state = BistroBuilderAdvancedCustomerHistoryEngine.CreateEmptySnapshot();
        HistoryRestored?.Invoke();
        HistoryChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryGetCustomer(
        string cohortId,
        out BistroBuilderAdvancedCustomerHistoryRecord record)
    {
        EnsureState();
        return BistroBuilderAdvancedCustomerHistoryEngine.TryGetCustomer(
            state, cohortId, out record);
    }

    private void HandleAdvancedVisitOutcome(
        BistroBuilderAdvancedCustomerVisitOutcome outcome)
    {
        if (!BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterOutcome(
                state, outcome,
                out BistroBuilderAdvancedCustomerHistorySnapshot candidate,
                out bool changed,
                out string error))
        {
            Debug.LogError("10C no pudo registrar resultado de visita: " + error, this);
            return;
        }
        if (!changed) return;
        state = candidate;
        HistoryChanged?.Invoke(state.revision);
    }

    private void HandleCohortRecorded(
        BistroBuilderAdvancedCustomerCohortAssignment assignment)
    {
        if (!BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterAssignment(
                state, assignment,
                out BistroBuilderAdvancedCustomerHistorySnapshot candidate,
                out bool changed,
                out string error))
        {
            Debug.LogError("10C no pudo enlazar la cohorte: " + error, this);
            return;
        }
        if (!changed) return;
        state = candidate;
        HistoryChanged?.Invoke(state.revision);
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (experienceTrackingService != null)
            experienceTrackingService.AdvancedVisitOutcomeCompleted +=
                HandleAdvancedVisitOutcome;
        if (guestRelationsService != null)
            guestRelationsService.VisitCohortRecorded += HandleCohortRecorded;
    }

    private void Unsubscribe()
    {
        if (experienceTrackingService != null)
            experienceTrackingService.AdvancedVisitOutcomeCompleted -=
                HandleAdvancedVisitOutcome;
        if (guestRelationsService != null)
            guestRelationsService.VisitCohortRecorded -= HandleCohortRecorded;
    }

    private void EnsureState()
    {
        if (state == null)
            state = BistroBuilderAdvancedCustomerHistoryEngine.CreateEmptySnapshot();
    }

    private void CacheDependencies()
    {
        if (experienceTrackingService == null)
            TryGetComponent(out experienceTrackingService);
        if (guestRelationsService == null)
            TryGetComponent(out guestRelationsService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
