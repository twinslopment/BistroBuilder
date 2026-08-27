using System;
using System.Collections.Generic;

/// <summary>
/// Motor puro de Reservas V1. No conoce escena, mesas, clientes, SaveGame
/// ni Presentation; solo valida y transforma snapshots inmutables.
/// </summary>
public static class BistroBuilderReservationEngine
{
    public const int MinimumPartySize = 1;
    public const int MaximumPartySize = 12;
    public const int MinimumDurationMinutes = 30;
    public const int MaximumDurationMinutes = 240;
    public const int MaximumGuestNameLength = 80;
    public const int MaximumNotesLength = 280;

    public static BistroBuilderReservationsSnapshot CreateEmptySnapshot()
    {
        return new BistroBuilderReservationsSnapshot();
    }

    public static bool TryValidateDraft(
        BistroBuilderReservationDraft draft,
        out string error)
    {
        error = string.Empty;
        if (draft == null)
        {
            error = "La reserva necesita un borrador válido.";
            return false;
        }        string guest = NormalizeText(draft.guestName);
        if (guest.Length < 1 || guest.Length > MaximumGuestNameLength)
        {
            error = "El nombre de la reserva debe tener entre 1 y " +
                    MaximumGuestNameLength + " caracteres.";
            return false;
        }
        if (draft.partySize < MinimumPartySize ||
            draft.partySize > MaximumPartySize)
        {
            error = "El tamaño del grupo queda fuera del rango V1.";
            return false;
        }
        if (draft.dayIndex < 1)
        {
            error = "La reserva necesita un DayIndex positivo.";
            return false;
        }
        if (draft.arrivalMinute < 0 || draft.arrivalMinute > 1439)
        {
            error = "La hora de llegada debe pertenecer al día planificado.";
            return false;
        }
        if (draft.durationMinutes < MinimumDurationMinutes ||
            draft.durationMinutes > MaximumDurationMinutes ||
            draft.arrivalMinute + draft.durationMinutes > 1440)
        {
            error = "La duración de la reserva no cabe en el día o queda fuera de rango.";
            return false;
        }        if (NormalizeText(draft.notes).Length > MaximumNotesLength)
        {
            error = "Las notas de la reserva superan el máximo V1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateRecord(
        BistroBuilderReservationRecord record,
        out string error)
    {
        error = string.Empty;
        if (record == null)
        {
            error = "El registro de reserva es nulo.";
            return false;
        }
        if (!IsValidReservationId(record.reservationId))
        {
            error = "ReservationId inválido.";
            return false;
        }

        var draft = new BistroBuilderReservationDraft
        {
            guestName = record.guestName,
            partySize = record.partySize,
            dayIndex = record.dayIndex,
            arrivalMinute = record.arrivalMinute,
            durationMinutes = record.durationMinutes,
            notes = record.notes
        };        if (!TryValidateDraft(draft, out error)) return false;
        if (record.tableId < 0)
        {
            error = "TableId no puede ser negativo.";
            return false;
        }
        if (!Enum.IsDefined(typeof(BistroBuilderReservationStatus), record.status))
        {
            error = "Estado de reserva no reconocido.";
            return false;
        }
        if (record.revision < 1L)
        {
            error = "La revisión de la reserva debe ser positiva.";
            return false;
        }
        return true;
    }

    public static bool TryValidateSnapshot(
        BistroBuilderReservationsSnapshot snapshot,
        out string error)
    {
        error = string.Empty;
        if (snapshot == null)
        {
            error = "reservations.state es nulo.";
            return false;
        }
        if (!string.Equals(snapshot.schemaId,
                BistroBuilderReservationsSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderReservationsSnapshot.CurrentSchemaVersion)
        {
            error = "Schema de Reservas incompatible.";
            return false;
        }        if (snapshot.revision < 0L || snapshot.reservations == null)
        {
            error = "reservations.state tiene revisión o colección inválidas.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < snapshot.reservations.Count; index++)
        {
            BistroBuilderReservationRecord record = snapshot.reservations[index];
            if (!TryValidateRecord(record, out error))
            {
                error = "Reserva índice " + index + ": " + error;
                return false;
            }
            string id = NormalizeId(record.reservationId);
            if (!ids.Add(id))
            {
                error = "ReservationId duplicado: " + id + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryAddReservation(
        BistroBuilderReservationsSnapshot current,
        BistroBuilderReservationRecord record,
        out BistroBuilderReservationsSnapshot result,
        out string error)
    {
        result = null;
        error = string.Empty;        if (!TryValidateSnapshot(current, out error) ||
            !TryValidateRecord(record, out error))
            return false;

        if (TryFind(current, record.reservationId, out _))
        {
            error = "ReservationId ya existe.";
            return false;
        }

        BistroBuilderReservationsSnapshot candidate = current.DeepClone();
        candidate.reservations.Add(NormalizeRecord(record));
        candidate.revision = checked(current.revision + 1L);
        Sort(candidate.reservations);
        if (!TryValidateSnapshot(candidate, out error)) return false;
        result = candidate;
        return true;
    }

    public static bool TryReplaceDraft(
        BistroBuilderReservationsSnapshot current,
        string reservationId,
        BistroBuilderReservationDraft draft,
        out BistroBuilderReservationsSnapshot result,
        out string error)
    {
        result = null;
        error = string.Empty;
        if (!TryValidateSnapshot(current, out error) ||
            !TryValidateDraft(draft, out error))
            return false;

        int index = FindIndex(current, reservationId);
        if (index < 0)
        {            error = "No existe la reserva indicada.";
            return false;
        }
        BistroBuilderReservationRecord currentRecord = current.reservations[index];
        if (currentRecord.IsTerminal ||
            currentRecord.status == BistroBuilderReservationStatus.Seated)
        {
            error = "La reserva ya no admite edición de planificación.";
            return false;
        }

        BistroBuilderReservationsSnapshot candidate = current.DeepClone();
        BistroBuilderReservationRecord edited = candidate.reservations[index];
        edited.guestName = NormalizeText(draft.guestName);
        edited.partySize = draft.partySize;
        edited.dayIndex = draft.dayIndex;
        edited.arrivalMinute = draft.arrivalMinute;
        edited.durationMinutes = draft.durationMinutes;
        edited.notes = NormalizeText(draft.notes);
        edited.tableId = 0;
        edited.revision = checked(edited.revision + 1L);
        candidate.revision = checked(current.revision + 1L);
        Sort(candidate.reservations);
        if (!TryValidateSnapshot(candidate, out error)) return false;
        result = candidate;
        return true;
    }

    public static bool TryAssignTable(
        BistroBuilderReservationsSnapshot current,
        string reservationId,
        int tableId,
        out BistroBuilderReservationsSnapshot result,
        out string error)
    {        result = null;
        error = string.Empty;
        if (tableId < 1 || !TryValidateSnapshot(current, out error))
        {
            if (tableId < 1) error = "TableId asignado debe ser positivo.";
            return false;
        }
        int index = FindIndex(current, reservationId);
        if (index < 0)
        {
            error = "No existe la reserva indicada.";
            return false;
        }
        BistroBuilderReservationRecord source = current.reservations[index];
        if (source.IsTerminal || source.status == BistroBuilderReservationStatus.Seated)
        {
            error = "La reserva ya no admite reasignación de mesa.";
            return false;
        }
        if (source.tableId == tableId)
        {
            result = current.DeepClone();
            return true;
        }

        BistroBuilderReservationsSnapshot candidate = current.DeepClone();
        candidate.reservations[index].tableId = tableId;
        candidate.reservations[index].revision =
            checked(candidate.reservations[index].revision + 1L);
        candidate.revision = checked(current.revision + 1L);
        if (!TryValidateSnapshot(candidate, out error)) return false;
        result = candidate;
        return true;
    }
    public static bool TryTransition(
        BistroBuilderReservationsSnapshot current,
        string reservationId,
        BistroBuilderReservationStatus target,
        out BistroBuilderReservationsSnapshot result,
        out string error)
    {
        result = null;
        error = string.Empty;
        if (!TryValidateSnapshot(current, out error) ||
            !Enum.IsDefined(typeof(BistroBuilderReservationStatus), target))
        {
            if (string.IsNullOrWhiteSpace(error)) error = "Estado objetivo inválido.";
            return false;
        }
        int index = FindIndex(current, reservationId);
        if (index < 0)
        {
            error = "No existe la reserva indicada.";
            return false;
        }
        BistroBuilderReservationRecord source = current.reservations[index];
        if (source.status == target)
        {
            result = current.DeepClone();
            return true;
        }
        if (!CanTransition(source.status, target))
        {
            error = "Transición de reserva no permitida: " + source.status + " -> " + target + ".";
            return false;
        }
        BistroBuilderReservationsSnapshot candidate = current.DeepClone();
        candidate.reservations[index].status = target;
        candidate.reservations[index].revision =
            checked(candidate.reservations[index].revision + 1L);
        candidate.revision = checked(current.revision + 1L);
        if (!TryValidateSnapshot(candidate, out error)) return false;
        result = candidate;
        return true;
    }

    public static bool TryFind(
        BistroBuilderReservationsSnapshot snapshot,
        string reservationId,
        out BistroBuilderReservationRecord record)
    {
        record = null;
        int index = FindIndex(snapshot, reservationId);
        if (index < 0) return false;
        record = snapshot.reservations[index]?.DeepClone();
        return record != null;
    }

    public static void CopyForDay(
        BistroBuilderReservationsSnapshot snapshot,
        int dayIndex,
        List<BistroBuilderReservationRecord> destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        if (snapshot?.reservations == null) return;
        foreach (BistroBuilderReservationRecord record in snapshot.reservations)
            if (record != null && record.dayIndex == dayIndex)
                destination.Add(record.DeepClone());
    }
    public static bool CanTransition(
        BistroBuilderReservationStatus source,
        BistroBuilderReservationStatus target)
    {
        switch (source)
        {
            case BistroBuilderReservationStatus.Booked:
                return target == BistroBuilderReservationStatus.Due ||
                       target == BistroBuilderReservationStatus.Cancelled;
            case BistroBuilderReservationStatus.Due:
                return target == BistroBuilderReservationStatus.Arrived ||
                       target == BistroBuilderReservationStatus.NoShow ||
                       target == BistroBuilderReservationStatus.Cancelled;
            case BistroBuilderReservationStatus.Arrived:
                return target == BistroBuilderReservationStatus.Seated;
            case BistroBuilderReservationStatus.Seated:
                return target == BistroBuilderReservationStatus.Completed;
            default:
                return false;
        }
    }

    public static string NormalizeId(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    public static bool IsValidReservationId(string value)
    {
        string normalized = NormalizeId(value);
        return normalized.StartsWith("reservation_", StringComparison.Ordinal) &&
               normalized.Length > "reservation_".Length;
    }
    private static BistroBuilderReservationRecord NormalizeRecord(
        BistroBuilderReservationRecord record)
    {
        BistroBuilderReservationRecord clone = record.DeepClone();
        clone.reservationId = NormalizeId(clone.reservationId);
        clone.guestName = NormalizeText(clone.guestName);
        clone.notes = NormalizeText(clone.notes);
        clone.revision = Math.Max(1L, clone.revision);
        return clone;
    }

    private static string NormalizeText(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static int FindIndex(
        BistroBuilderReservationsSnapshot snapshot,
        string reservationId)
    {
        if (snapshot?.reservations == null) return -1;
        string normalized = NormalizeId(reservationId);
        if (normalized.Length == 0) return -1;
        for (int index = 0; index < snapshot.reservations.Count; index++)
        {
            BistroBuilderReservationRecord record = snapshot.reservations[index];
            if (record != null && string.Equals(
                    NormalizeId(record.reservationId), normalized,
                    StringComparison.Ordinal)) return index;
        }
        return -1;
    }
    private static void Sort(List<BistroBuilderReservationRecord> records)
    {
        records.Sort((left, right) =>
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            int day = left.dayIndex.CompareTo(right.dayIndex);
            if (day != 0) return day;
            int minute = left.arrivalMinute.CompareTo(right.arrivalMinute);
            if (minute != 0) return minute;
            return string.Compare(
                NormalizeId(left.reservationId),
                NormalizeId(right.reservationId),
                StringComparison.Ordinal);
        });
    }
}
