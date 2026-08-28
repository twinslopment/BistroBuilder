using System.Collections.Generic;

/// <summary>Fila de agenda proyectada para Presentation 6E.</summary>
public sealed class BistroBuilderReservationPlayerRow
{
    public string reservationId = string.Empty;
    public string guestName = string.Empty;
    public int partySize;
    public int dayIndex;
    public int arrivalMinute;
    public int durationMinutes;
    public int tableId;
    public BistroBuilderReservationStatus status;
    public string notes = string.Empty;

    public bool CanEdit =>
        status == BistroBuilderReservationStatus.Booked ||
        status == BistroBuilderReservationStatus.Due ||
        status == BistroBuilderReservationStatus.Arrived;

    public bool CanCancel =>
        !IsTerminal && status != BistroBuilderReservationStatus.Seated;

    public bool IsTerminal =>
        status == BistroBuilderReservationStatus.Completed ||
        status == BistroBuilderReservationStatus.Cancelled ||
        status == BistroBuilderReservationStatus.NoShow;
}

/// <summary>Snapshot de agenda de un día para la UI jugable 6E.</summary>
public sealed class BistroBuilderReservationPlayerSnapshot
{
    public int dayIndex;
    public int currentDayIndex;
    public int horizonDays;
    public readonly List<BistroBuilderReservationPlayerRow> rows =
        new List<BistroBuilderReservationPlayerRow>();

    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < rows.Count; index++)
                if (rows[index] != null && !rows[index].IsTerminal)
                    count++;
            return count;
        }
    }
}
