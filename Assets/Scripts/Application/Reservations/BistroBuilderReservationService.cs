using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad persistente de Reservas. Mantiene exclusivamente datos V1;
/// la asignación física de mesas pertenece a la integración 6B.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Reservations/Reservation Service")]
public sealed class BistroBuilderReservationService : MonoBehaviour
{
    private BistroBuilderReservationsSnapshot state;

    public event Action<long> ReservationsChanged;
    public event Action ReservationsRestored;

    public long Revision => state != null ? state.revision : 0L;
    public int ReservationCount =>
        state != null && state.reservations != null ? state.reservations.Count : 0;

    private void Awake()
    {
        EnsureStateInitialized();
    }

    private void EnsureStateInitialized()
    {
        if (state == null)
            state = BistroBuilderReservationEngine.CreateEmptySnapshot();
    }
    public bool ValidateConfiguration(out string error)
    {
        EnsureStateInitialized();
        return BistroBuilderReservationEngine.TryValidateSnapshot(state, out error);
    }

    public bool TryCreateReservation(
        BistroBuilderReservationDraft draft,
        out BistroBuilderReservationRecord created,
        out string error)
    {
        created = null;
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderReservationEngine.TryValidateDraft(draft, out error))
            return false;

        var record = new BistroBuilderReservationRecord
        {
            reservationId = "reservation_" + Guid.NewGuid().ToString("N"),
            guestName = draft.guestName.Trim(),
            partySize = draft.partySize,
            dayIndex = draft.dayIndex,
            arrivalMinute = draft.arrivalMinute,
            durationMinutes = draft.durationMinutes,
            notes = string.IsNullOrWhiteSpace(draft.notes) ? string.Empty : draft.notes.Trim(),
            tableId = 0,
            status = BistroBuilderReservationStatus.Booked,
            revision = 1L
        };

        if (!BistroBuilderReservationEngine.TryAddReservation(
                state, record, out BistroBuilderReservationsSnapshot candidate, out error) ||
            !Commit(candidate, out error))
            return false;
        return TryGetReservation(record.reservationId, out created);
    }

    public bool TryEditReservation(
        string reservationId,
        BistroBuilderReservationDraft draft,
        out BistroBuilderReservationRecord edited,
        out string error)
    {
        edited = null;
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderReservationEngine.TryReplaceDraft(
                state, reservationId, draft,
                out BistroBuilderReservationsSnapshot candidate, out error) ||
            !Commit(candidate, out error))
            return false;

        return TryGetReservation(reservationId, out edited);
    }

    /// <summary>
    /// API de integración para 6B. Solo persiste TableId; no toca la mesa runtime.
    /// El servicio de disponibilidad debe validar capacidad/conflictos antes de llamarla.
    /// </summary>
    public bool TryAssignTable(
        string reservationId,
        int tableId,
        out BistroBuilderReservationRecord updated,
        out string error)
    {
        updated = null;
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderReservationEngine.TryAssignTable(
                state, reservationId, tableId,
                out BistroBuilderReservationsSnapshot candidate, out error) ||
            !Commit(candidate, out error))
            return false;
        return TryGetReservation(reservationId, out updated);
    }

    public bool TryTransition(
        string reservationId,
        BistroBuilderReservationStatus target,
        out BistroBuilderReservationRecord updated,
        out string error)
    {
        updated = null;
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderReservationEngine.TryTransition(
                state, reservationId, target,
                out BistroBuilderReservationsSnapshot candidate, out error) ||
            !Commit(candidate, out error))
            return false;

        return TryGetReservation(reservationId, out updated);
    }

    public bool TryCancel(
        string reservationId,
        out BistroBuilderReservationRecord updated,
        out string error)
    {
        return TryTransition(
            reservationId,
            BistroBuilderReservationStatus.Cancelled,
            out updated,
            out error);
    }

    public bool TryGetReservation(
        string reservationId,
        out BistroBuilderReservationRecord record)
    {        EnsureStateInitialized();
        return BistroBuilderReservationEngine.TryFind(state, reservationId, out record);
    }

    public void CopyReservationsForDay(
        int dayIndex,
        List<BistroBuilderReservationRecord> destination)
    {
        EnsureStateInitialized();
        BistroBuilderReservationEngine.CopyForDay(state, dayIndex, destination);
    }

    public void CopyAllReservations(List<BistroBuilderReservationRecord> destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        EnsureStateInitialized();
        for (int index = 0; index < state.reservations.Count; index++)
        {
            BistroBuilderReservationRecord record = state.reservations[index];
            if (record != null) destination.Add(record.DeepClone());
        }
    }

    public BistroBuilderReservationsSnapshot CreateSnapshot()
    {
        EnsureStateInitialized();
        return state.DeepClone();
    }

    public bool TryRestoreSnapshot(
        BistroBuilderReservationsSnapshot snapshot,
        out string error)
    {
        error = string.Empty;

        if (!BistroBuilderReservationEngine.TryValidateSnapshot(snapshot, out error))
            return false;
        state = snapshot.DeepClone();
        ReservationsRestored?.Invoke();
        ReservationsChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        state = BistroBuilderReservationEngine.CreateEmptySnapshot();
        ReservationsRestored?.Invoke();
        ReservationsChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    private bool Commit(
        BistroBuilderReservationsSnapshot candidate,
        out string error)
    {
        error = string.Empty;

        if (candidate == null)
        {
            error = "La mutación de Reservas no produjo un snapshot válido.";
            return false;
        }

        if (!BistroBuilderReservationEngine.TryValidateSnapshot(candidate, out error))
            return false;
        if (state != null && candidate.revision < state.revision)
        {
            error = "La mutación de Reservas intentó retroceder la revisión.";
            return false;
        }
        state = candidate.DeepClone();
        ReservationsChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }
}