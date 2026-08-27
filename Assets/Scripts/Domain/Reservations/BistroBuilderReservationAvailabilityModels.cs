using System;

/// <summary>
/// Proyección pura de una mesa real para calcular disponibilidad.
/// No contiene GameObject ni componentes de escena.
/// </summary>
[Serializable]
public sealed class BistroBuilderReservationTableCandidate
{
    public int tableId;
    public int capacity;
    public int associatedSeatCount;

    public BistroBuilderReservationTableCandidate DeepClone()
    {
        return (BistroBuilderReservationTableCandidate)MemberwiseClone();
    }

    public bool CanSeat(int partySize)
    {
        return tableId > 0 &&
               partySize > 0 &&
               capacity >= partySize &&
               associatedSeatCount >= partySize;
    }
}

/// <summary>
/// Resultado presentable de disponibilidad de una mesa.
/// </summary>[Serializable]
public sealed class BistroBuilderReservationTableAvailability
{
    public int tableId;
    public int capacity;
    public int associatedSeatCount;
    public int unusedCapacity;

    public static BistroBuilderReservationTableAvailability FromCandidate(
        BistroBuilderReservationTableCandidate candidate,
        int partySize)
    {
        if (candidate == null)
            return null;

        return new BistroBuilderReservationTableAvailability
        {
            tableId = candidate.tableId,
            capacity = candidate.capacity,
            associatedSeatCount = candidate.associatedSeatCount,
            unusedCapacity = Math.Max(0, candidate.capacity - partySize)
        };
    }
}
