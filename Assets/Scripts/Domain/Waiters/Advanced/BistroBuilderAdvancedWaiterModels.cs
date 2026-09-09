using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderWaiterResponsibilityKind
{
    Primary = 0,
    Secondary = 1,
    Support = 2
}

public enum BistroBuilderWaiterSaturationLevel
{
    Free = 0,
    Light = 1,
    Busy = 2,
    Saturated = 3
}

public enum BistroBuilderWaiterContextActionKind
{
    None = 0,
    SuggestiveSale = 1,
    Apologize = 2,
    ExplainDelay = 3,
    CalmCustomer = 4,
    ReviewOrder = 5,
    AccelerateBill = 6
}

public enum BistroBuilderWaiterRouteKind
{
    DirectFallback = 0,
    NavMeshOptimal = 1,
    ExternalProfessional = 2
}

[Serializable]
public sealed class BistroBuilderAdvancedWaiterTaskPlan
{
    public int taskId;
    public WaiterTaskType taskType;
    public string destinationReferenceId = string.Empty;
    public BistroBuilderWaiterResponsibilityKind responsibility;
    public BistroBuilderWaiterContextActionKind contextualAction;
    public float score;
    public float estimatedRouteMeters;
    public bool activeAssignment;
}

[Serializable]
public sealed class BistroBuilderAdvancedWaiterSnapshot
{
    public int waiterId;
    public string primaryZoneId = string.Empty;
    public BistroBuilderWaiterSaturationLevel saturation;
    public int plannedTaskCount;
    public int planCapacity;
    public float saturation01;
    public string currentDestinationReservation = string.Empty;
    public readonly List<BistroBuilderAdvancedWaiterTaskPlan> plans =
        new List<BistroBuilderAdvancedWaiterTaskPlan>();
}

public readonly struct BistroBuilderWaiterTaskEvaluation
{
    public readonly float Score;
    public readonly float RouteMeters;
    public readonly BistroBuilderWaiterResponsibilityKind Responsibility;
    public readonly BistroBuilderWaiterContextActionKind ContextAction;

    public BistroBuilderWaiterTaskEvaluation(
        float score,
        float routeMeters,
        BistroBuilderWaiterResponsibilityKind responsibility,
        BistroBuilderWaiterContextActionKind contextAction)
    {
        Score = score;
        RouteMeters = routeMeters;
        Responsibility = responsibility;
        ContextAction = contextAction;
    }
}

public static class BistroBuilderAdvancedWaiterPolicy
{
    public static float PriorityScore(WaiterTaskPriority priority)
    {
        return Mathf.Max(0, (int)priority) * 10f;
    }

    public static float ResolveSaturation01(int activeCount, int plannedCount, int capacity)
    {
        int safeCapacity = Mathf.Max(1, capacity);
        return Mathf.Clamp01((activeCount + plannedCount) / (float)safeCapacity);
    }

    public static BistroBuilderWaiterSaturationLevel ResolveSaturation(float value01)
    {
        if (value01 < 0.2f) return BistroBuilderWaiterSaturationLevel.Free;
        if (value01 < 0.5f) return BistroBuilderWaiterSaturationLevel.Light;
        if (value01 < 0.85f) return BistroBuilderWaiterSaturationLevel.Busy;
        return BistroBuilderWaiterSaturationLevel.Saturated;
    }

    public static BistroBuilderWaiterContextActionKind ResolveContextAction(
        WaiterTask task,
        float waitSeconds,
        float saturation01)
    {
        if (task == null) return BistroBuilderWaiterContextActionKind.None;
        if (task.Priority == WaiterTaskPriority.Urgent)
            return BistroBuilderWaiterContextActionKind.ReviewOrder;
        if (task.Type == WaiterTaskType.DeliverBill && waitSeconds >= 4f)
            return BistroBuilderWaiterContextActionKind.AccelerateBill;
        if (task.Type == WaiterTaskType.TakeOrder && waitSeconds >= 14f)
            return BistroBuilderWaiterContextActionKind.CalmCustomer;
        if (task.Type == WaiterTaskType.TakeOrder && waitSeconds >= 7f)
            return BistroBuilderWaiterContextActionKind.Apologize;
        if (task.Type == WaiterTaskType.DeliverFood && waitSeconds >= 5f)
            return BistroBuilderWaiterContextActionKind.ExplainDelay;
        if (task.Type == WaiterTaskType.TakeOrder && waitSeconds < 3f && saturation01 < 0.45f)
            return BistroBuilderWaiterContextActionKind.SuggestiveSale;
        return BistroBuilderWaiterContextActionKind.None;
    }

    public static float ResponsibilityBonus(BistroBuilderWaiterResponsibilityKind kind)
    {
        return kind switch
        {
            BistroBuilderWaiterResponsibilityKind.Primary => 900f,
            BistroBuilderWaiterResponsibilityKind.Secondary => 350f,
            _ => 0f
        };
    }

    public static float ContextUrgencyBonus(BistroBuilderWaiterContextActionKind action)
    {
        return action switch
        {
            BistroBuilderWaiterContextActionKind.CalmCustomer => 850f,
            BistroBuilderWaiterContextActionKind.ReviewOrder => 700f,
            BistroBuilderWaiterContextActionKind.AccelerateBill => 600f,
            BistroBuilderWaiterContextActionKind.ExplainDelay => 500f,
            BistroBuilderWaiterContextActionKind.Apologize => 350f,
            _ => 0f
        };
    }
}
