using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 6B — Proyecta mesas/asientos canónicos a disponibilidad de Reservas.
/// No crea mesas, no reserva RestaurantSeat y no altera el servicio runtime.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Reservations/Reservation Availability Service")]
public sealed class BistroBuilderReservationAvailabilityService : MonoBehaviour
{
    [SerializeField] private BistroBuilderReservationService reservationService;
    [SerializeField] private RestaurantTableRegistry tableRegistry;
    [SerializeField] private RestaurantSeatRegistry seatRegistry;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderReservationAvailabilityProfile availabilityProfile;

    private readonly List<BistroBuilderReservationTableCandidate>
        candidateBuffer = new List<BistroBuilderReservationTableCandidate>();

    public BistroBuilderReservationService ReservationService => reservationService;
    public RestaurantTableRegistry TableRegistry => tableRegistry;
    public RestaurantSeatRegistry SeatRegistry => seatRegistry;
    public BistroBuilderGeneralGameStateService GeneralGameStateService => generalGameStateService;
    public BistroBuilderReservationAvailabilityProfile AvailabilityProfile => availabilityProfile;

    private void Awake()
    {
        CacheDependencies();
    }
    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (reservationService == null ||
            tableRegistry == null ||
            seatRegistry == null ||
            generalGameStateService == null ||
            availabilityProfile == null)
        {
            error = "6B necesita Reservations, TableRegistry, SeatRegistry, calendario y perfil.";
            return false;
        }

        if (!reservationService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !availabilityProfile.TryValidate(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public bool TryFindBestTable(
        BistroBuilderReservationDraft request,
        string excludedReservationId,
        out BistroBuilderReservationTableAvailability selected,
        out string error)
    {
        selected = null;
        if (!EnsureReadyForRequest(request, out error))
            return false;
        BistroBuilderReservationsSnapshot snapshot =
            reservationService.CreateSnapshot();
        BuildCandidateBuffer();
        if (!BistroBuilderReservationAvailabilityEngine.TrySelectBestTable(
                request,
                snapshot,
                candidateBuffer,
                availabilityProfile.TurnoverBufferMinutes,
                excludedReservationId,
                out BistroBuilderReservationTableCandidate candidate,
                out error))
            return false;

        selected = BistroBuilderReservationTableAvailability.FromCandidate(
            candidate,
            request.partySize);
        return selected != null;
    }

    /// <summary>
    /// Busca y persiste la mejor mesa para una reserva ya creada.
    /// La ocupación física se mantiene fuera de 6B y pertenece a 6C.
    /// </summary>
    public bool TryAssignBestTable(
        string reservationId,
        out BistroBuilderReservationRecord updated,
        out string error)
    {
        updated = null;        if (!ValidateConfiguration(out error))
            return false;
        if (!reservationService.TryGetReservation(
                reservationId,
                out BistroBuilderReservationRecord reservation))
        {
            error = "No existe la reserva indicada.";
            return false;
        }

        BistroBuilderReservationDraft request = ToDraft(reservation);
        if (!TryFindBestTable(
                request,
                reservation.reservationId,
                out BistroBuilderReservationTableAvailability selected,
                out error))
            return false;

        return reservationService.TryAssignTable(
            reservation.reservationId,
            selected.tableId,
            out updated,
            out error);
    }

    public bool TryAssignSpecificTable(
        string reservationId,
        int tableId,
        out BistroBuilderReservationRecord updated,
        out string error)
    {
        updated = null;        if (!ValidateConfiguration(out error))
            return false;
        if (!reservationService.TryGetReservation(
                reservationId,
                out BistroBuilderReservationRecord reservation))
        {
            error = "No existe la reserva indicada.";
            return false;
        }

        BuildCandidateBuffer();
        BistroBuilderReservationTableCandidate candidate =
            candidateBuffer.Find(value => value != null && value.tableId == tableId);
        if (candidate == null)
        {
            error = "La mesa indicada no existe o no es planificable.";
            return false;
        }

        BistroBuilderReservationDraft request = ToDraft(reservation);
        if (!BistroBuilderReservationAvailabilityEngine.IsTableAvailable(
                request,
                reservationService.CreateSnapshot(),
                candidate,
                availabilityProfile.TurnoverBufferMinutes,
                reservation.reservationId,
                out error))
            return false;

        return reservationService.TryAssignTable(
            reservation.reservationId,
            tableId,
            out updated,
            out error);
    }

    public void CopyAvailableTables(
        BistroBuilderReservationDraft request,
        string excludedReservationId,
        List<BistroBuilderReservationTableAvailability> destination)
    {        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        destination.Clear();

        if (!EnsureReadyForRequest(request, out _))
            return;

        BistroBuilderReservationsSnapshot snapshot =
            reservationService.CreateSnapshot();
        BuildCandidateBuffer();
        for (int index = 0; index < candidateBuffer.Count; index++)
        {
            BistroBuilderReservationTableCandidate candidate = candidateBuffer[index];
            if (!BistroBuilderReservationAvailabilityEngine.IsTableAvailable(
                    request,
                    snapshot,
                    candidate,
                    availabilityProfile.TurnoverBufferMinutes,
                    excludedReservationId,
                    out _))
                continue;

            BistroBuilderReservationTableAvailability availability =
                BistroBuilderReservationTableAvailability.FromCandidate(
                    candidate,
                    request.partySize);
            if (availability != null)
                destination.Add(availability);
        }

        destination.Sort((left, right) =>
        {
            int waste = left.unusedCapacity.CompareTo(right.unusedCapacity);
            return waste != 0 ? waste : left.tableId.CompareTo(right.tableId);
        });
    }

    private bool EnsureReadyForRequest(
        BistroBuilderReservationDraft request,
        out string error)
    {        error = string.Empty;
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderReservationEngine.TryValidateDraft(request, out error))
            return false;

        int currentDay = generalGameStateService.DayIndex;
        if (request.dayIndex < currentDay ||
            request.dayIndex >= currentDay + availabilityProfile.PlanningHorizonDays)
        {
            error = "La reserva queda fuera del horizonte de planificación.";
            return false;
        }

        BuildCandidateBuffer();
        if (candidateBuffer.Count == 0)
        {
            error = "No hay mesas de comedor planificables.";
            return false;
        }

        return true;
    }

    private void BuildCandidateBuffer()
    {
        candidateBuffer.Clear();
        if (tableRegistry == null || seatRegistry == null)
            return;

        foreach (RestaurantTable table in tableRegistry.RegisteredTables)
        {
            if (!IsPlanningEligibleTable(table))
                continue;

            int seats = CountAssociatedOperationalSeats(table);
            candidateBuffer.Add(new BistroBuilderReservationTableCandidate
            {
                tableId = table.TableId,
                capacity = table.Capacity,
                associatedSeatCount = seats
            });
        }
    }
    private static bool IsPlanningEligibleTable(RestaurantTable table)
    {
        if (table == null || !table.gameObject.activeInHierarchy || table.TableId < 1)
            return false;

        RestaurantAreaMember member = table.GetComponent<RestaurantAreaMember>();
        RestaurantArea area = member != null ? member.AssignedArea : null;
        if (area == null || !area.IsOperational ||
            !string.Equals(area.AreaId, "dining_main", StringComparison.Ordinal))
            return false;

        RestaurantTableSeatingConfiguration configuration =
            table.GetComponent<RestaurantTableSeatingConfiguration>();
        return configuration != null &&
               configuration.ValidateConfiguration(out _);
    }

    private int CountAssociatedOperationalSeats(RestaurantTable table)
    {
        int count = 0;
        foreach (RestaurantSeat seat in seatRegistry.RegisteredSeats)
        {
            if (seat == null || !seat.gameObject.activeInHierarchy || !seat.IsAssociated)
                continue;
            RestaurantTableSeatingConfiguration configuration = seat.AssociatedTable;
            if (configuration == null || !ReferenceEquals(configuration.Table, table))
                continue;
            RestaurantAreaMember member = seat.GetComponent<RestaurantAreaMember>();
            RestaurantArea area = member != null ? member.AssignedArea : null;
            if (area == null || !area.IsOperational ||
                !string.Equals(area.AreaId, "dining_main", StringComparison.Ordinal))
                continue;
            if (seat.ValidateConfiguration(out _))
                count++;
        }
        return count;
    }
    private static BistroBuilderReservationDraft ToDraft(
        BistroBuilderReservationRecord reservation)
    {
        return new BistroBuilderReservationDraft
        {
            guestName = reservation.guestName,
            partySize = reservation.partySize,
            dayIndex = reservation.dayIndex,
            arrivalMinute = reservation.arrivalMinute,
            durationMinutes = reservation.durationMinutes,
            notes = reservation.notes
        };
    }

    private void CacheDependencies()
    {
        if (reservationService == null)
            TryGetComponent(out reservationService);
        if (tableRegistry == null)
            TryGetComponent(out tableRegistry);
        if (seatRegistry == null)
            TryGetComponent(out seatRegistry);
        if (generalGameStateService == null)
            TryGetComponent(out generalGameStateService);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
