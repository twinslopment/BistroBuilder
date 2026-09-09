using System;
using System.Collections.Generic;

/// <summary>
/// Motor puro de disponibilidad 6B.
/// Cruza reservas persistentes con proyecciones de mesas sin conocer Unity.
/// </summary>
public static class BistroBuilderReservationAvailabilityEngine
{
    public const int MaximumTurnoverBufferMinutes = 120;

    public static bool TrySelectBestTable(
        BistroBuilderReservationDraft request,
        BistroBuilderReservationsSnapshot reservations,
        IReadOnlyList<BistroBuilderReservationTableCandidate> candidates,
        int turnoverBufferMinutes,
        string excludedReservationId,
        out BistroBuilderReservationTableCandidate selected,
        out string error)
    {
        selected = null;
        error = string.Empty;

        if (!TryValidateInputs(
                request,
                reservations,
                candidates,
                turnoverBufferMinutes,
                out error))
            return false;
        int bestUnusedCapacity = int.MaxValue;
        int bestTableId = int.MaxValue;
        string excluded = BistroBuilderReservationEngine.NormalizeId(
            excludedReservationId);

        for (int index = 0; index < candidates.Count; index++)
        {
            BistroBuilderReservationTableCandidate candidate = candidates[index];
            if (candidate == null || !candidate.CanSeat(request.partySize))
                continue;

            if (HasConflict(
                    request,
                    reservations,
                    candidate.tableId,
                    turnoverBufferMinutes,
                    excluded))
                continue;

            int unusedCapacity = candidate.capacity - request.partySize;
            if (selected != null &&
                (unusedCapacity > bestUnusedCapacity ||
                 (unusedCapacity == bestUnusedCapacity &&
                  candidate.tableId >= bestTableId)))
                continue;

            selected = candidate.DeepClone();
            bestUnusedCapacity = unusedCapacity;
            bestTableId = candidate.tableId;
        }
        if (selected == null)
        {
            error = "No existe ninguna mesa compatible y libre de conflictos.";
            return false;
        }

        return true;
    }

    public static bool IsTableAvailable(
        BistroBuilderReservationDraft request,
        BistroBuilderReservationsSnapshot reservations,
        BistroBuilderReservationTableCandidate candidate,
        int turnoverBufferMinutes,
        string excludedReservationId,
        out string error)
    {
        error = string.Empty;
        var single = new[] { candidate };
        if (!TryValidateInputs(
                request,
                reservations,
                single,
                turnoverBufferMinutes,
                out error))
            return false;

        if (candidate == null || !candidate.CanSeat(request.partySize))
        {
            error = "La mesa no dispone de capacidad física suficiente.";
            return false;
        }
        string excluded = BistroBuilderReservationEngine.NormalizeId(
            excludedReservationId);
        if (HasConflict(
                request,
                reservations,
                candidate.tableId,
                turnoverBufferMinutes,
                excluded))
        {
            error = "La mesa tiene otra reserva incompatible en esa franja.";
            return false;
        }

        return true;
    }

    public static bool IntervalsConflict(
        int firstStart,
        int firstEnd,
        int secondStart,
        int secondEnd,
        int turnoverBufferMinutes)
    {
        int buffer = Math.Max(0, turnoverBufferMinutes);
        return firstStart < secondEnd + buffer &&
               secondStart < firstEnd + buffer;
    }

    public static bool ReservationBlocksTable(
        BistroBuilderReservationRecord reservation)
    {
        return reservation != null && !reservation.IsTerminal;
    }
    private static bool HasConflict(
        BistroBuilderReservationDraft request,
        BistroBuilderReservationsSnapshot reservations,
        int tableId,
        int turnoverBufferMinutes,
        string excludedReservationId)
    {
        for (int index = 0; index < reservations.reservations.Count; index++)
        {
            BistroBuilderReservationRecord existing =
                reservations.reservations[index];
            if (!ReservationBlocksTable(existing) ||
                existing.tableId != tableId ||
                existing.dayIndex != request.dayIndex)
                continue;

            if (!string.IsNullOrWhiteSpace(excludedReservationId) &&
                string.Equals(
                    BistroBuilderReservationEngine.NormalizeId(
                        existing.reservationId),
                    excludedReservationId,
                    StringComparison.Ordinal))
                continue;

            if (IntervalsConflict(
                    request.arrivalMinute,
                    request.arrivalMinute + request.durationMinutes,
                    existing.arrivalMinute,
                    existing.EndMinute,
                    turnoverBufferMinutes))
                return true;
        }

        return false;
    }
    private static bool TryValidateInputs(
        BistroBuilderReservationDraft request,
        BistroBuilderReservationsSnapshot reservations,
        IReadOnlyList<BistroBuilderReservationTableCandidate> candidates,
        int turnoverBufferMinutes,
        out string error)
    {
        error = string.Empty;
        if (!BistroBuilderReservationEngine.TryValidateDraft(request, out error))
            return false;

        if (!BistroBuilderReservationEngine.TryValidateSnapshot(
                reservations,
                out error))
            return false;

        if (candidates == null)
        {
            error = "La disponibilidad necesita una colección de mesas.";
            return false;
        }

        if (turnoverBufferMinutes < 0 ||
            turnoverBufferMinutes > MaximumTurnoverBufferMinutes)
        {
            error = "El margen de rotación de mesa queda fuera de rango.";
            return false;
        }

        return true;
    }
}
