using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estados lógicos públicos de un viaje de Navigation & Crowd Flow v1.
/// Animation puede consumir una proyección simplificada, pero no gobierna estos estados.
/// </summary>
public enum BistroBuilderNavigationTravelState
{
    Idle = 0,
    RequestingRoute = 1,
    FollowingRoute = 2,
    ApproachingGate = 3,
    Queueing = 4,
    Encounter = 5,
    Yielding = 6,
    WaitingTransient = 7,
    ControlledPassage = 8,
    Recovering = 9,
    Replanning = 10,
    Arrived = 11,
    Failed = 12,
    Cancelled = 13
}

public enum BistroBuilderNavigationWaitingReason
{
    None = 0,
    TransientSweep = 1,
    OpposingTraffic = 2,
    ControlledPassage = 3,
    Queue = 4,
    Yield = 5,
    AwaitingCorridor = 6,
    TopologyUpdate = 7,
    Recovery = 8,
    DestinationHold = 9
}

public enum BistroBuilderNavigationFailureReason
{
    None = 0,
    DestinationInvalidated = 1,
    SpatialContextChanged = 2,
    TemporarilyBlocked = 3,
    RouteResolutionFailed = 4,
    NavigationAborted = 5
}

public enum BistroBuilderNavigationReplanLevel
{
    Steering = 0,
    CorridorRepair = 1,
    RouteSuffixRepair = 2,
    FullReplan = 3
}

public enum BistroBuilderNavigationRecoveryStage
{
    None = 0,
    ReciprocalCorrection = 1,
    EncounterCommitment = 2,
    Yield = 3,
    PriorityInheritance = 4,
    ControlledPassageDrain = 5,
    EscapePocket = 6,
    LocalBackoff = 7,
    Retreat = 8,
    CorridorRepair = 9,
    RouteSuffixReplan = 10,
    FullReplan = 11,
    ExplicitFailure = 12
}

public enum BistroBuilderNavigationQueryPriority
{
    SafetyEmergency = 0,
    Recovery = 1,
    InvalidatedRoute = 2,
    NewTrip = 3,
    Optimization = 4
}

/// <summary>
/// Revisión coherente de las autoridades que afectan a una decisión de navegación.
/// </summary>
[Serializable]
public struct BistroBuilderNavigationTopologySnapshot
{
    public int spatialRevision;
    public int navigationRevision;
    public int trafficEpoch;
    [Range(0f, 1f)] public float flowQuality;

    public bool MatchesStructuralState(
        BistroBuilderNavigationTopologySnapshot other)
    {
        return spatialRevision == other.spatialRevision &&
               navigationRevision == other.navigationRevision;
    }
}

/// <summary>
/// Petición pública de viaje. Navigation no puede cambiar destination por iniciativa propia.
/// externalUrgency pertenece al sistema solicitante; Navigation solo la consume.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationRequest
{
    public string requestId = string.Empty;
    public string ownerId = string.Empty;
    public BistroBuilderNavigationAgentMask agentMask =
        BistroBuilderNavigationAgentMask.Other;
    public Vector3 origin;
    public Vector3 destination;
    [Min(0.1f)] public float nominalSpeed = 2f;
    [Min(0.05f)] public float mobilityRadius = 0.28f;
    public int externalUrgency;
    public string groupId = string.Empty;
    public int destinationRevision;

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            error = "NavigationRequest sin ownerId estable.";
            return false;
        }

        if (nominalSpeed <= 0f || mobilityRadius <= 0f)
        {
            error = ownerId + ": velocidad o Mobility Envelope inválido.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

/// <summary>
/// Desglose temporal interpretable del coste de una ruta.
/// Todos los términos se expresan en segundos equivalentes.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationRouteCostBreakdown
{
    [Min(0f)] public float freeFlowTime;
    [Min(0f)] public float flowQualityPenalty;
    [Min(0f)] public float congestionDelay;
    [Min(0f)] public float queueDelay;
    [Min(0f)] public float transientDelay;
    [Min(0f)] public float maneuverDelay;
    [Min(0f)] public float incidentPenalty;
    [Min(0f)] public float routeSwitchPenalty;

    public float TotalExpectedTime =>
        freeFlowTime +
        flowQualityPenalty +
        congestionDelay +
        queueDelay +
        transientDelay +
        maneuverDelay +
        incidentPenalty +
        routeSwitchPenalty;
}

/// <summary>
/// Plan público de navegación. Contiene el corredor geométrico inicial,
/// su coste y las revisiones contra las que fue calculado.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationPlan
{
    public string requestId = string.Empty;
    public string ownerId = string.Empty;
    public BistroBuilderNavigationTopologySnapshot topology;
    public BistroBuilderNavigationRoute route = new BistroBuilderNavigationRoute();
    public BistroBuilderNavigationRouteCorridor corridor =
        new BistroBuilderNavigationRouteCorridor();
    public BistroBuilderNavigationRouteCostBreakdown cost =
        new BistroBuilderNavigationRouteCostBreakdown();
    public bool structurallyValid;
}

/// <summary>
/// Entrada del solver local recíproco.
/// </summary>
[Serializable]
public struct BistroBuilderNavigationLocalMoveInput
{
    public string ownerId;
    public BistroBuilderNavigationAgentMask agentMask;
    public Vector3 position;
    public Vector3 currentVelocity;
    public Vector3 preferredVelocity;
    public float radius;
    public int externalUrgency;
    public float waitingAgeSeconds;
    public float recoveryDebt;
    public float commitment;
}

/// <summary>
/// Salida del solver local. Nunca modifica la validez espacial de BBSIS.
/// </summary>
[Serializable]
public struct BistroBuilderNavigationLocalMoveDecision
{
    public Vector3 velocity;
    public bool shouldYield;
    public string yieldingTo;
    public int passingSide;
    public float effectivePriority;
    public BistroBuilderNavigationWaitingReason waitingReason;
}

/// <summary>
/// Traza compacta y consultable de una decisión de viaje.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationDecisionTrace
{
    public string ownerId = string.Empty;
    public string requestId = string.Empty;
    public BistroBuilderNavigationTravelState state;
    public BistroBuilderNavigationWaitingReason waitingReason;
    public BistroBuilderNavigationRecoveryStage recoveryStage;
    public string blockerId = string.Empty;
    public string yieldingTo = string.Empty;
    public string lastDecision = string.Empty;
    public float routeProgressMeters;
    public float routeLengthMeters;
    public float secondsWithoutProgress;
    public int replanCount;
    public int recoveryCount;
    public BistroBuilderNavigationTopologySnapshot topology;

    public BistroBuilderNavigationDecisionTrace DeepClone()
    {
        return (BistroBuilderNavigationDecisionTrace)MemberwiseClone();
    }
}

/// <summary>
/// Resultado final explícito de un viaje.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationResult
{
    public string requestId = string.Empty;
    public string ownerId = string.Empty;
    public BistroBuilderNavigationTravelState finalState;
    public BistroBuilderNavigationFailureReason failureReason;
    public float actualTravelSeconds;
    public float actualDistanceMeters;
    public float idealTravelSeconds;
}

/// <summary>
/// Métricas acumuladas del runtime de Navigation v1.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationMetricsSnapshot
{
    public long tripsStarted;
    public long tripsCompleted;
    public long tripsFailed;
    public long yieldCount;
    public long stallCount;
    public long recoveryCount;
    public long corridorRepairCount;
    public long routeSuffixRepairCount;
    public long fullReplanCount;
    public long transientChangeCount;
    public long topologyChangeCount;
    public long encounterCount;
    public long controlledPassageWaitCount;
    public long controlledPassageSwitchCount;
    public long deadlockCount;
    public long localBackoffCount;
    public long corridorBypassCount;
    public long conflictHorizonWaitCount;
    public long conflictHorizonArbitrationCount;
    public long failureMemorySuppressedCount;
    public long reciprocalConstraintSolveCount;
    public long reciprocalConstraintCount;
    public long escapePocketCount;
    public long retreatCount;
    public long physicalQueueOverflowCount;
    public long replayEventCount;
    public long routeCacheHitCount;
    public long routeCacheMissCount;
    public long negativeRouteCacheHitCount;
    public long topologicalPlanCount;
    public long topologicalFallbackCount;
    public long pathQueryEnqueuedCount;
    public long pathQueryCompletedCount;
    public long pathQueryCancelledCount;
    public long pathQueryCoalescedCount;
    public int pathQueryPendingPeak;
    public float accumulatedWaitingSeconds;
    public float accumulatedActualTravelSeconds;
    public float accumulatedIdealTravelSeconds;

    public float TripCompletionRatio =>
        tripsStarted <= 0 ? 1f : (float)tripsCompleted / tripsStarted;

    public float NavigationDelayRatio =>
        accumulatedIdealTravelSeconds <= 0.001f
            ? 1f
            : accumulatedActualTravelSeconds / accumulatedIdealTravelSeconds;

    public BistroBuilderNavigationMetricsSnapshot DeepClone()
    {
        return (BistroBuilderNavigationMetricsSnapshot)MemberwiseClone();
    }
}

/// <summary>
/// Estado de un encuentro bilateral. El lado y la precedencia permanecen estables
/// hasta que los participantes se separan, eliminando el baile izquierda/derecha.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationEncounterSnapshot
{
    public string firstOwnerId = string.Empty;
    public string secondOwnerId = string.Empty;
    public string preferredOwnerId = string.Empty;
    public int passingSide;
    public float createdAt;
    public float lastSeenAt;
}

public enum BistroBuilderControlledPassageState
{
    Free = 0,
    ServingPositive = 1,
    ServingNegative = 2,
    Draining = 3,
    Switching = 4
}

/// <summary>
/// Descriptor de un Spatial Gate consumido por Navigation. La geometría procede de BBSIS.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationGateDescriptor
{
    public string gateId = string.Empty;
    public string subjectId = string.Empty;
    public Vector3 start;
    public Vector3 end;
    [Min(0.1f)] public float minimumWidth = 0.75f;
    public bool criticalRoute;
}

[Serializable]
public struct BistroBuilderNavigationControlledPassageDecision
{
    public bool granted;
    public string gateId;
    public int direction;
    public BistroBuilderControlledPassageState state;
    public BistroBuilderNavigationWaitingReason waitingReason;
    public string blockerId;
}

[Serializable]
public sealed class BistroBuilderNavigationControlledPassageSnapshot
{
    public string gateId = string.Empty;
    public string subjectId = string.Empty;
    public BistroBuilderControlledPassageState state;
    public float width;
    public int activeCount;
    public int waitingPositive;
    public int waitingNegative;
    public int servedBurst;
}

[Serializable]
public sealed class BistroBuilderNavigationDeadlockSnapshot
{
    public string episodeId = string.Empty;
    public List<string> participants = new List<string>();
    public BistroBuilderNavigationRecoveryStage recoveryStage;
    public float startedAt;
    public float lastObservedAt;
}
[Serializable]
public sealed class BistroBuilderNavigationTrafficCellSnapshot
{
    public int x;
    public int z;
    public Vector3 center;
    public float smoothedOccupancy;
    public float smoothedMeanSpeed;
    public float penalty;
}

/// <summary>
/// Estado observable de una cola física. Los slots proceden de posiciones certificadas externamente.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationPhysicalQueueSnapshot
{
    public string queueId = string.Empty;
    public int capacity;
    public int queuedCount;
    public int overflowCount;
    public List<BistroBuilderNavigationPhysicalQueueEntrySnapshot> entries =
        new List<BistroBuilderNavigationPhysicalQueueEntrySnapshot>();
}

[Serializable]
public sealed class BistroBuilderNavigationPhysicalQueueEntrySnapshot
{
    public string ownerId = string.Empty;
    public int slotIndex = -1;
    public Vector3 target;
    public bool overflow;
    public int logicalOrder;
}

/// <summary>
/// Evento inmutable del stream de replay/diagnóstico de Navigation.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationReplayEvent
{
    public long sequence;
    public string eventType = string.Empty;
    public string ownerId = string.Empty;
    public Vector3 position;
    public BistroBuilderNavigationTravelState state;
    public BistroBuilderNavigationWaitingReason waitingReason;
    public BistroBuilderNavigationRecoveryStage recoveryStage;
    public string blockerId = string.Empty;
    public string detail = string.Empty;
    public BistroBuilderNavigationTopologySnapshot topology;
}

[Serializable]
public sealed class BistroBuilderNavigationReplayBundle
{
    public string label = string.Empty;
    public string deterministicDigest = string.Empty;
    public List<BistroBuilderNavigationReplayEvent> events =
        new List<BistroBuilderNavigationReplayEvent>();
}