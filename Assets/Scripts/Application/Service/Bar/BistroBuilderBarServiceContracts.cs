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

/// <summary>
/// Política pura de reconciliación de una sesión de barra tras cargar.
///
/// La fase persistida es una pista, pero la autoridad real es la combinación
/// de la comanda legacy, las líneas canónicas y si existe una mesa solicitada.
/// Esto evita reanudar un cierre cuando la cocina todavía está preparando.
/// </summary>
public static class BistroBuilderBarSessionRecoveryPolicy
{
    public static BistroBuilderBarSessionPhase ResolveRestoredPhase(
        BistroBuilderBarSessionPhase persistedPhase,
        BistroBuilderServiceMode serviceMode,
        bool hasOrder,
        OrderState orderState,
        bool allLinesServed,
        bool allLinesConsumed,
        bool tableRequested
    )
    {
        if (!BistroBuilderServiceModeUtility.IsBarMode(serviceMode))
        {
            return BistroBuilderBarSessionPhase.Cancelled;
        }

        if (!hasOrder)
        {
            return persistedPhase == BistroBuilderBarSessionPhase.Allocated ||
                   persistedPhase == BistroBuilderBarSessionPhase.WalkingToBar
                ? BistroBuilderBarSessionPhase.WalkingToBar
                : BistroBuilderBarSessionPhase.WaitingForOrder;
        }

        if (orderState == OrderState.Cancelled)
        {
            return BistroBuilderBarSessionPhase.Cancelled;
        }

        if (allLinesConsumed)
        {
            if (serviceMode == BistroBuilderServiceMode.WaitingAtBar)
            {
                if (orderState == OrderState.Served)
                {
                    return tableRequested
                        ? BistroBuilderBarSessionPhase.ClosingForTable
                        : BistroBuilderBarSessionPhase
                            .WaitingForTableAfterConsumption;
                }

                return orderState == OrderState.Completed
                    ? BistroBuilderBarSessionPhase.Completed
                    : BistroBuilderBarSessionPhase.WaitingForItems;
            }

            if (orderState == OrderState.Completed)
            {
                return BistroBuilderBarSessionPhase.Completed;
            }

            return orderState == OrderState.Served
                ? BistroBuilderBarSessionPhase.WaitingForPayment
                : BistroBuilderBarSessionPhase.WaitingForItems;
        }

        if (allLinesServed || orderState == OrderState.Served)
        {
            return BistroBuilderBarSessionPhase.Consuming;
        }

        if (orderState == OrderState.Completed)
        {
            return serviceMode == BistroBuilderServiceMode.WaitingAtBar
                ? BistroBuilderBarSessionPhase
                    .WaitingForTableAfterConsumption
                : BistroBuilderBarSessionPhase.Completed;
        }

        return BistroBuilderBarSessionPhase.WaitingForItems;
    }

    public static bool CanCompleteWaitingAtBarOrder(
        OrderState orderState,
        bool allLinesConsumed
    )
    {
        return allLinesConsumed && orderState == OrderState.Served;
    }
}

