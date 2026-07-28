using System;

/// <summary>
/// Fase pública y estable de una sesión de barra. No expone la implementación
/// interna del autómata y puede utilizarse en UI, pruebas y futura persistencia.
/// </summary>
public enum BistroBuilderBarSessionPhase
{
    Allocated = 0,
    WalkingToBar = 1,
    WaitingForOrder = 2,
    TakingOrder = 3,
    WaitingForItems = 4,
    Consuming = 5,
    WaitingForPayment = 6,
    Paying = 7,
    ClosingForTable = 8,
    WaitingForTableAfterConsumption = 9,
    Completed = 10,
    Cancelled = 11
}

public readonly struct BistroBuilderBarSessionSnapshot
{
    public int GroupId { get; }
    public string AnchorBarSpotId { get; }
    public int ReservedSpotCount { get; }
    public int ReservedCapacity { get; }
    public BistroBuilderServiceMode ServiceMode { get; }
    public string CanonicalOrderId { get; }
    public BistroBuilderBarSessionPhase Phase { get; }
    public int ChargeCents { get; }
    public int ServedLineCount { get; }
    public int TotalLineCount { get; }

    public BistroBuilderBarSessionSnapshot(
        int groupId,
        string anchorBarSpotId,
        int reservedSpotCount,
        int reservedCapacity,
        BistroBuilderServiceMode serviceMode,
        string canonicalOrderId,
        BistroBuilderBarSessionPhase phase,
        int chargeCents,
        int servedLineCount,
        int totalLineCount
    )
    {
        GroupId = groupId;
        AnchorBarSpotId = anchorBarSpotId ?? string.Empty;
        ReservedSpotCount = Math.Max(0, reservedSpotCount);
        ReservedCapacity = Math.Max(0, reservedCapacity);
        ServiceMode = serviceMode;
        CanonicalOrderId = canonicalOrderId ?? string.Empty;
        Phase = phase;
        ChargeCents = Math.Max(0, chargeCents);
        ServedLineCount = Math.Max(0, servedLineCount);
        TotalLineCount = Math.Max(0, totalLineCount);
    }
}

public readonly struct BistroBuilderBarServiceCompletedEvent
{
    public int GroupId { get; }
    public string CanonicalOrderId { get; }
    public BistroBuilderServiceMode ServiceMode { get; }
    public int AmountCents { get; }
    public bool ChargeTransferredToTableBill { get; }

    public BistroBuilderBarServiceCompletedEvent(
        int groupId,
        string canonicalOrderId,
        BistroBuilderServiceMode serviceMode,
        int amountCents,
        bool chargeTransferredToTableBill
    )
    {
        GroupId = groupId;
        CanonicalOrderId = canonicalOrderId ?? string.Empty;
        ServiceMode = serviceMode;
        AmountCents = Math.Max(0, amountCents);
        ChargeTransferredToTableBill = chargeTransferredToTableBill;
    }
}
