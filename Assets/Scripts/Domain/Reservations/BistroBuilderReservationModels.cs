using System;
using System.Collections.Generic;

/// <summary>Estado persistente V1 de una reserva.</summary>
public enum BistroBuilderReservationStatus
{
    Booked = 0,
    Due = 1,
    Arrived = 2,
    Seated = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6
}

/// <summary>
/// Petición editable del jugador antes de crear una reserva persistente.
/// No contiene identidad ni referencias runtime.
/// </summary>
[Serializable]
public sealed class BistroBuilderReservationDraft
{
    public string guestName = string.Empty;
    public int partySize = 2;
    public int dayIndex = 1;
    public int arrivalMinute = 780;
    public int durationMinutes = 120;
    public string notes = string.Empty;

    public BistroBuilderReservationDraft DeepClone() =>
        (BistroBuilderReservationDraft)MemberwiseClone();
}
/// <summary>
/// Reserva persistente. TableId es identidad lógica de una mesa existente;
/// nunca se persiste un GameObject, Transform ni componente visual.
/// </summary>
[Serializable]
public sealed class BistroBuilderReservationRecord
{
    public string reservationId = string.Empty;
    public string guestName = string.Empty;
    public int partySize = 2;
    public int dayIndex = 1;
    public int arrivalMinute = 780;
    public int durationMinutes = 120;
    public int tableId;
    public BistroBuilderReservationStatus status =
        BistroBuilderReservationStatus.Booked;
    public string notes = string.Empty;
    public long revision = 1L;

    public int EndMinute => arrivalMinute + durationMinutes;
    public bool IsTerminal =>
        status == BistroBuilderReservationStatus.Completed ||
        status == BistroBuilderReservationStatus.Cancelled ||
        status == BistroBuilderReservationStatus.NoShow;

    public BistroBuilderReservationRecord DeepClone() =>
        (BistroBuilderReservationRecord)MemberwiseClone();
}
/// <summary>
/// Fuente de verdad persistente del Bloque 6. La colección se mantiene
/// ordenada de forma determinista por día, hora e identidad.
/// </summary>
[Serializable]
public sealed class BistroBuilderReservationsSnapshot
{
    public const string CurrentSchemaId = "reservations.state";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public List<BistroBuilderReservationRecord> reservations =
        new List<BistroBuilderReservationRecord>();

    public BistroBuilderReservationsSnapshot DeepClone()
    {
        var clone = new BistroBuilderReservationsSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            reservations = new List<BistroBuilderReservationRecord>()
        };
        if (reservations != null)
            for (int i = 0; i < reservations.Count; i++)
                clone.reservations.Add(reservations[i]?.DeepClone());
        return clone;
    }
}