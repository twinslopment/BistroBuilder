using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum BistroBuilderNavigationAgentMask
{
    None = 0,
    Customer = 1 << 0,
    Waiter = 1 << 1,
    Staff = 1 << 2,
    Delivery = 1 << 3,
    Other = 1 << 4,
    All = Customer | Waiter | Staff | Delivery | Other
}

public enum BistroBuilderNavigationRouteKind
{
    None = 0,
    NavMesh = 1,
    GridFallback = 2,
    DirectDegraded = 3,
    OperationalDock = 4
}

public enum BistroBuilderDynamicSpaceKind
{
    Destination = 0,
    DoorSwing = 1,
    SeatMovement = 2,
    AgentPresence = 3,
    TemporaryObstacle = 4
}

public enum BistroBuilderCirculationIssueSeverity
{
    Info = 0,
    Warning = 1,
    Blocking = 2
}

[Serializable]
public sealed class BistroBuilderNavigationRoute
{
    public BistroBuilderNavigationRouteKind kind;
    public List<Vector3> points = new List<Vector3>();
    public float lengthMeters;
    public float congestionCost;
    public float totalScore;
    public int navigationRevision;
    public bool isComplete;

    public BistroBuilderNavigationRoute DeepClone()
    {
        return new BistroBuilderNavigationRoute
        {
            kind = kind,
            points = points != null ? new List<Vector3>(points) : new List<Vector3>(),
            lengthMeters = lengthMeters,
            congestionCost = congestionCost,
            totalScore = totalScore,
            navigationRevision = navigationRevision,
            isComplete = isComplete
        };
    }
}

[Serializable]
public sealed class BistroBuilderCirculationIssue
{
    public string issueId = string.Empty;
    public BistroBuilderCirculationIssueSeverity severity;
    public string message = string.Empty;
    public string sourceName = string.Empty;
}

[Serializable]
public sealed class BistroBuilderCirculationHealthReport
{
    public int revision;
    public int checkedConnections;
    public int reachableConnections;
    public int warningCount;
    public int blockingCount;
    public List<BistroBuilderCirculationIssue> issues = new List<BistroBuilderCirculationIssue>();

    public bool IsOperational => blockingCount == 0;
    public float ReachabilityRatio => checkedConnections <= 0 ? 1f : (float)reachableConnections / checkedConnections;

    public string BuildSummary()
    {
        return "Circulacion: " + reachableConnections + "/" + checkedConnections +
               " conexiones; " + warningCount + " avisos; " + blockingCount + " bloqueos.";
    }
}

[Serializable]
public sealed class BistroBuilderNavigationRuntimeSnapshot
{
    public const string CurrentSchemaId = "navigation.runtime";
    public const int CurrentSchemaVersion = 1;
    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public int revision;
}
