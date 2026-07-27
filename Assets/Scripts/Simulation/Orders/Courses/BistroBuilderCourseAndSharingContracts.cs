using System;
using System.Collections.Generic;

/// <summary>
/// Política que determina cuándo pueden liberarse las líneas de un pase
/// posterior hacia cocina.
/// </summary>
public enum BistroBuilderCourseCoordinationPolicy
{
    PerTable = 0,
    PerCustomer = 1,
    Hybrid = 2,
    Manual = 3
}

/// <summary>
/// Forma en la que una regla de composición genera líneas físicas.
/// </summary>
public enum BistroBuilderOrderLineCompositionMode
{
    SharedAllCustomers = 0,
    IndividualPerCustomer = 1,
    SharedGroups = 2
}

/// <summary>
/// Cambios publicados por la autoridad de pases y platos compartidos.
/// </summary>
public enum BistroBuilderCourseAndSharingChangeType
{
    OrderRegistered = 0,
    InitialCourseReleased = 1,
    CourseReleased = 2,
    LineReleased = 3,
    SharedLineProgressed = 4,
    OrderRemoved = 5,
    StateRestored = 6
}

/// <summary>
/// Evento inmutable del runtime 367F.
/// </summary>
public readonly struct BistroBuilderCourseAndSharingChangedEvent
{
    public BistroBuilderCourseAndSharingChangeType ChangeType { get; }
    public string OrderId { get; }
    public string LineId { get; }
    public int CourseIndex { get; }
    public int CompletedConsumerCount { get; }
    public int TotalConsumerCount { get; }
    public int Revision { get; }
    public string Description { get; }

    public BistroBuilderCourseAndSharingChangedEvent(
        BistroBuilderCourseAndSharingChangeType changeType,
        string orderId,
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
        LineId = BistroBuilderOrderIdUtility.Normalize(lineId);
        CourseIndex = courseIndex;
        CompletedConsumerCount = Math.Max(0, completedConsumerCount);
        TotalConsumerCount = Math.Max(0, totalConsumerCount);
        Revision = revision;
        Description = description ?? string.Empty;
    }
}

/// <summary>
/// Reglas puras compartidas por runtime, editor y pruebas.
/// </summary>
public static class BistroBuilderCourseAndSharingPolicy
{
    public const int MaximumCourseIndex = 20;
    public const int MaximumLinesPerOrder = 128;
    public const int MaximumCustomersPerSharedGroup = 32;

    public static bool IsValidCourseIndex(int courseIndex)
    {
        return courseIndex >= 0 && courseIndex <= MaximumCourseIndex;
    }

    public static bool IsLineResolvedForCourseAdvance(
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

    public static bool IsLineReleased(
        BistroBuilderCanonicalOrderLine line
    )
    {
        if (line == null)
        {
            return false;
        }

        return line.State != BistroBuilderCanonicalOrderLineState.Draft &&
               line.State != BistroBuilderCanonicalOrderLineState.Submitted;
    }

    public static bool ContainsCourse(
        IReadOnlyList<int> source,
        int courseIndex
    )
    {
        if (source == null)
        {
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            if (source[index] == courseIndex)
            {
                return true;
            }
        }

        return false;
    }
}
