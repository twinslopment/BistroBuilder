using System;
using System.Collections.Generic;

/// <summary>
/// Estado individual de un cliente dentro del consumo de una comanda.
///
/// El estado del grupo continúa existiendo como fachada coarse para los
/// sistemas legacy, pero deja de ser la autoridad del consumo.
/// </summary>
public enum BistroBuilderCustomerDiningCustomerState
{
    WaitingForDish = 0,
    Eating = 1,
    Completed = 2,
    Cancelled = 3,
    Failed = 4
}

/// <summary>
/// Tipos de cambio publicados por el servicio de consumo individual.
/// </summary>
public enum BistroBuilderCustomerDiningChangeType
{
    OrderRegistered = 0,
    CustomerStartedCourse = 1,
    CustomerCompletedCourse = 2,
    LineConsumed = 3,
    GroupCoarseStateChanged = 4,
    BillReady = 5,
    OrderRemoved = 6,
    StateRestored = 7,
    SharedLineProgressed = 8
}

/// <summary>
/// Evento inmutable del runtime de consumo individual.
/// </summary>
public readonly struct BistroBuilderCustomerDiningChangedEvent
{
    public BistroBuilderCustomerDiningChangeType ChangeType { get; }
    public string OrderId { get; }
    public string CustomerId { get; }
    public string LineId { get; }
    public int CourseIndex { get; }
    public int CompletedConsumerCount { get; }
    public int TotalConsumerCount { get; }
    public int Revision { get; }
    public string Description { get; }

    public BistroBuilderCustomerDiningChangedEvent(
        BistroBuilderCustomerDiningChangeType changeType,
        string orderId,
        string customerId,
        string lineId,
        int revision,
        string description
    ) : this(
        changeType,
        orderId,
        customerId,
        lineId,
        0,
        0,
        0,
        revision,
        description
    )
    {
    }

    public BistroBuilderCustomerDiningChangedEvent(
        BistroBuilderCustomerDiningChangeType changeType,
        string orderId,
        string customerId,
        string lineId,
        int courseIndex,
        int completedConsumerCount,
        int totalConsumerCount,
        int revision,
        string description
    )
    {
        ChangeType = changeType;
        OrderId = BistroBuilderOrderIdUtility.Normalize(orderId);
        CustomerId = BistroBuilderOrderIdUtility.Normalize(customerId);
        LineId = BistroBuilderOrderIdUtility.Normalize(lineId);
        CourseIndex = courseIndex;
        CompletedConsumerCount = Math.Max(0, completedConsumerCount);
        TotalConsumerCount = Math.Max(0, totalConsumerCount);
        Revision = revision;
        Description = description ?? string.Empty;
    }
}

/// <summary>
/// Resultado resumido de una notificación de plato servido.
/// </summary>
public readonly struct BistroBuilderCustomerDiningNotificationResult
{
    public int StartedCustomerCount { get; }
    public bool AllCustomersStartedOrCompleted { get; }
    public bool AllCustomersCompleted { get; }
    public string Description { get; }

    public BistroBuilderCustomerDiningNotificationResult(
        int startedCustomerCount,
        bool allCustomersStartedOrCompleted,
        bool allCustomersCompleted,
        string description
    )
    {
        StartedCustomerCount = Math.Max(0, startedCustomerCount);
        AllCustomersStartedOrCompleted = allCustomersStartedOrCompleted;
        AllCustomersCompleted = allCustomersCompleted;
        Description = description ?? string.Empty;
    }
}

/// <summary>
/// Reglas puras compartidas por runtime, validador y autotest.
///
/// No mutan comandas ni objetos de escena.
/// </summary>
public static class BistroBuilderCustomerDiningPolicy
{
    public static bool IsTerminal(
        BistroBuilderCustomerDiningCustomerState state
    )
    {
        return state == BistroBuilderCustomerDiningCustomerState.Completed ||
               state == BistroBuilderCustomerDiningCustomerState.Cancelled ||
               state == BistroBuilderCustomerDiningCustomerState.Failed;
    }

    public static bool LineContainsConsumer(
        BistroBuilderCanonicalOrderLine line,
        string customerId
    )
    {
        if (line == null)
        {
            return false;
        }

        string normalizedCustomerId =
            BistroBuilderOrderIdUtility.Normalize(customerId);

        if (!BistroBuilderOrderIdUtility.IsValid(normalizedCustomerId) ||
            line.ConsumerCustomerIds == null)
        {
            return false;
        }

        for (int index = 0;
             index < line.ConsumerCustomerIds.Count;
             index++)
        {
            if (string.Equals(
                    line.ConsumerCustomerIds[index],
                    normalizedCustomerId,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Un plato puede ser consumido por un cliente cuando está servido,
    /// consumido o cancelado. Cancelled no genera consumo económico adicional,
    /// pero tampoco debe bloquear el avance del pase.
    /// </summary>
    public static bool IsLineReadyForCustomer(
        BistroBuilderCanonicalOrderLine line
    )
    {
        if (line == null)
        {
            return false;
        }

        return line.State == BistroBuilderCanonicalOrderLineState.Served ||
               line.State == BistroBuilderCanonicalOrderLineState.Consumed ||
               line.State == BistroBuilderCanonicalOrderLineState.Cancelled;
    }

    public static bool IsLineResolvedForBill(
        BistroBuilderCanonicalOrderLine line
    )
    {
        if (line == null)
        {
            return false;
        }

        return line.State == BistroBuilderCanonicalOrderLineState.Consumed ||
               line.State == BistroBuilderCanonicalOrderLineState.Cancelled;
    }

    public static bool ContainsNormalizedId(
        IReadOnlyList<string> source,
        string value
    )
    {
        if (source == null)
        {
            return false;
        }

        string normalized = BistroBuilderOrderIdUtility.Normalize(value);

        for (int index = 0; index < source.Count; index++)
        {
            if (string.Equals(
                    source[index],
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }
        }

        return false;
    }
}
