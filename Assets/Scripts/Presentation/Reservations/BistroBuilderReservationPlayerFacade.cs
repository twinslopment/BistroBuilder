using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada de Presentation 6E. Proyecta Reservas y delega toda mutación
/// en ReservationService + AvailabilityService canónicos.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Reservations/Reservation Player Facade")]
public sealed class BistroBuilderReservationPlayerFacade : MonoBehaviour
{
    [SerializeField] private BistroBuilderReservationService reservationService;
    [SerializeField] private BistroBuilderReservationAvailabilityService availabilityService;
    [SerializeField] private BistroBuilderGeneralGameStateService gameStateService;

    private readonly List<BistroBuilderReservationRecord> buffer =
        new List<BistroBuilderReservationRecord>();

    public event Action ViewInvalidated;

    public int CurrentDayIndex =>
        gameStateService != null ? gameStateService.DayIndex : 1;

    public int PlanningHorizonDays =>
        availabilityService != null && availabilityService.AvailabilityProfile != null
            ? availabilityService.AvailabilityProfile.PlanningHorizonDays
            : 1;

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (reservationService == null || availabilityService == null ||
            gameStateService == null)
        {
            error = "6E necesita Reservations, Availability y calendario canónicos.";
            return false;
        }

        if (!reservationService.ValidateConfiguration(out error) ||
            !availabilityService.ValidateConfiguration(out error) ||
            !gameStateService.ValidateConfiguration(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public bool TryBuildDaySnapshot(
        int dayIndex,
        out BistroBuilderReservationPlayerSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!ValidateConfiguration(out error))
            return false;

        int firstDay = CurrentDayIndex;
        int lastDay = firstDay + PlanningHorizonDays - 1;
        if (dayIndex < firstDay || dayIndex > lastDay)
        {
            error = "El día queda fuera del horizonte de Reservas.";
            return false;
        }

        var built = new BistroBuilderReservationPlayerSnapshot
        {
            dayIndex = dayIndex,
            currentDayIndex = firstDay,
            horizonDays = PlanningHorizonDays
        };

        buffer.Clear();
        reservationService.CopyReservationsForDay(dayIndex, buffer);
        for (int index = 0; index < buffer.Count; index++)
        {
            BistroBuilderReservationRecord record = buffer[index];
            if (record == null)
                continue;

            built.rows.Add(new BistroBuilderReservationPlayerRow
            {
                reservationId = record.reservationId,
                guestName = record.guestName,
                partySize = record.partySize,
                dayIndex = record.dayIndex,
                arrivalMinute = record.arrivalMinute,
                durationMinutes = record.durationMinutes,
                tableId = record.tableId,
                status = record.status,
                notes = record.notes
            });
        }

        snapshot = built;
        error = string.Empty;
        return true;
    }

    public bool TryCreateAndAssign(
        BistroBuilderReservationDraft draft,
        out BistroBuilderReservationRecord created,
        out string error)
    {
        created = null;
        if (!ValidateConfiguration(out error))
            return false;

        if (!availabilityService.TryFindBestTable(
                draft,
                string.Empty,
                out BistroBuilderReservationTableAvailability selected,
                out error))
            return false;

        if (!reservationService.TryCreateReservation(
                draft,
                out BistroBuilderReservationRecord pending,
                out error))
            return false;
        if (availabilityService.TryAssignSpecificTable(
                pending.reservationId,
                selected.tableId,
                out created,
                out error))
            return true;

        string rollbackError = string.Empty;
        reservationService.TryCancel(
            pending.reservationId,
            out _,
            out rollbackError);
        if (!string.IsNullOrWhiteSpace(rollbackError))
            error += " Rollback de creación: " + rollbackError;
        return false;
    }

    public bool TryEditAndReassign(
        string reservationId,
        BistroBuilderReservationDraft draft,
        out BistroBuilderReservationRecord edited,
        out string error)
    {
        edited = null;
        if (!ValidateConfiguration(out error))
            return false;
        if (!reservationService.TryGetReservation(
                reservationId,
                out BistroBuilderReservationRecord original) ||
            original == null)
        {
            error = "No existe la reserva indicada.";
            return false;
        }

        if (!availabilityService.TryFindBestTable(
                draft,
                reservationId,
                out BistroBuilderReservationTableAvailability selected,
                out error))
            return false;

        if (!reservationService.TryEditReservation(
                reservationId,
                draft,
                out _,
                out error))
            return false;

        if (availabilityService.TryAssignSpecificTable(
                reservationId,
                selected.tableId,
                out edited,
                out error))
            return true;
        var rollbackDraft = new BistroBuilderReservationDraft
        {
            guestName = original.guestName,
            partySize = original.partySize,
            dayIndex = original.dayIndex,
            arrivalMinute = original.arrivalMinute,
            durationMinutes = original.durationMinutes,
            notes = original.notes
        };

        string rollbackError = string.Empty;
        bool rollbackEdited = reservationService.TryEditReservation(
            reservationId,
            rollbackDraft,
            out _,
            out rollbackError);
        if (rollbackEdited && original.tableId > 0)
        {
            rollbackEdited = availabilityService.TryAssignSpecificTable(
                reservationId,
                original.tableId,
                out _,
                out rollbackError);
        }

        if (!rollbackEdited)
            error += " Rollback de edición: " + rollbackError;
        return false;
    }

    public bool TryCancel(
        string reservationId,
        out string error)
    {
        if (!ValidateConfiguration(out error))
            return false;
        return reservationService.TryCancel(
            reservationId,
            out _,
            out error);
    }

    public bool TryGetReservation(
        string reservationId,
        out BistroBuilderReservationRecord reservation)
    {
        reservation = null;
        CacheDependencies();
        return reservationService != null &&
               reservationService.TryGetReservation(reservationId, out reservation);
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (reservationService == null) return;
        reservationService.ReservationsChanged += HandleChanged;
        reservationService.ReservationsRestored += HandleRestored;
    }

    private void Unsubscribe()
    {
        if (reservationService == null) return;
        reservationService.ReservationsChanged -= HandleChanged;
        reservationService.ReservationsRestored -= HandleRestored;
    }

    private void HandleChanged(long _) => ViewInvalidated?.Invoke();
    private void HandleRestored() => ViewInvalidated?.Invoke();

    private void CacheDependencies()
    {
        if (reservationService == null)
            TryGetComponent(out reservationService);
        if (availabilityService == null)
            TryGetComponent(out availabilityService);
        if (gameStateService == null)
            TryGetComponent(out gameStateService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
