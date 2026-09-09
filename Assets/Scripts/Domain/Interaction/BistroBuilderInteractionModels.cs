using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tipos de derecho lógico del BB Interaction & Reservation System.
/// Ninguno de ellos representa una reserva espacial BBSIS.
/// </summary>
public enum BistroBuilderInteractionGrantKind
{
    Assignment = 0,
    TaskClaim = 1,
    UsePermit = 2,
    Custody = 3,
    WaitTicket = 4
}

public enum BistroBuilderInteractionHolderKind
{
    Actor = 0,
    Group = 1,
    Task = 2,
    Process = 3,
    Container = 4,
    Station = 5
}

public enum BistroBuilderInteractionScopeKind
{
    Generic = 0,
    Target = 1,
    Channel = 2,
    Slot = 3,
    CapacityPool = 4
}
public enum BistroBuilderInteractionGrantState
{
    Assigned = 0,
    Claimed = 1,
    Committed = 2,
    Granted = 3,
    Engaged = 4,
    Held = 5,
    Queued = 6,
    Called = 7,
    Recovery = 8,
    Completed = 20,
    Cancelled = 21,
    Failed = 22,
    Expired = 23,
    Invalidated = 24,
    Released = 25
}

public enum BistroBuilderInteractionRequestOutcome
{
    Pending = 0,
    Granted = 1,
    Rejected = 2,
    Cancelled = 3
}

public enum BistroBuilderInteractionTargetAdmissionState
{
    Open = 0,
    Draining = 1,
    Closed = 2
}

public enum BistroBuilderInteractionSpatialBindingKind
{
    None = 0,
    Port = 1,
    WorkEdge = 2
}
public enum BistroBuilderInteractionReasonCode
{
    None = 0,
    NotEligible = 1,
    TargetUnavailable = 2,
    SlotUnavailable = 3,
    CapacityFull = 4,
    AssignmentConflict = 5,
    HigherRankedContender = 6,
    BundleUnavailable = 7,
    SpatialDenied = 8,
    NavigationFailed = 9,
    NoProgress = 10,
    TargetInvalidated = 11,
    HolderInvalidated = 12,
    TaskCancelled = 13,
    ShiftEnded = 14,
    SessionEnded = 15,
    Interrupted = 16,
    Expired = 17,
    StaleHandle = 18,
    LoadReconciled = 19,
    InvalidRequest = 20,
    DependencyInvalid = 21,
    TargetDraining = 22,
    TargetClosed = 23,
    SpatialBindingMissing = 24,
    CustodyCycle = 25,
    ReentrantMutationDeferred = 26,
    Completed = 27,
    Released = 28
}

[Serializable]
public struct BistroBuilderInteractionGrantHandle : IEquatable<BistroBuilderInteractionGrantHandle>
{
    public string grantId;
    public long generation;

    public BistroBuilderInteractionGrantHandle(string id, long value)
    {
        grantId = id ?? string.Empty;
        generation = value;
    }
    public bool IsValid => !string.IsNullOrWhiteSpace(grantId) && generation > 0;

    public bool Equals(BistroBuilderInteractionGrantHandle other)
    {
        return generation == other.generation &&
               string.Equals(grantId, other.grantId, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is BistroBuilderInteractionGrantHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((grantId != null ? grantId.GetHashCode() : 0) * 397) ^
                   generation.GetHashCode();
        }
    }

    public override string ToString()
    {
        return IsValid ? grantId + "@" + generation : "<invalid>";
    }
}

/// <summary>
/// Recurso candidato. La consulta de candidatos nunca consume capacidad.
/// </summary>
[Serializable]
public sealed class BistroBuilderInteractionCandidate
{
    public string targetId = string.Empty;
    public string resourceId = string.Empty;
    public string channelId = string.Empty;
    public string slotId = string.Empty;
    public int suitability;
    public int travelCostHint;
}
/// <summary>
/// Solicitud de adquisición lógica. La prioridad de tarea llega desde Gameplay/IA.
/// </summary>
[Serializable]
public sealed class BistroBuilderInteractionAcquisitionRequest
{
    public string requestId = string.Empty;
    public BistroBuilderInteractionGrantKind grantKind =
        BistroBuilderInteractionGrantKind.UsePermit;
    public BistroBuilderInteractionHolderKind holderKind =
        BistroBuilderInteractionHolderKind.Actor;
    public string holderId = string.Empty;
    public string interactionId = string.Empty;
    public string sessionId = string.Empty;
    public string parentGrantId = string.Empty;
    public string parentRequestId = string.Empty;
    public long parentGeneration;
    public int taskPriorityClass;
    public int capacityUnits = 1;
    public bool countsCapacity = true;
    public bool persistCanonical;
    public bool requiresSpatialAdmission;
    [Min(0f)] public float engageWithinSeconds = 3f;
    public List<BistroBuilderInteractionCandidate> candidates =
        new List<BistroBuilderInteractionCandidate>();

    public BistroBuilderInteractionAcquisitionRequest Clone()
    {
        var clone = (BistroBuilderInteractionAcquisitionRequest)MemberwiseClone();
        clone.candidates = new List<BistroBuilderInteractionCandidate>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            BistroBuilderInteractionCandidate c = candidates[i];
            if (c == null) continue;
            clone.candidates.Add(new BistroBuilderInteractionCandidate
            {
                targetId = c.targetId,
                resourceId = c.resourceId,
                channelId = c.channelId,
                slotId = c.slotId,
                suitability = c.suitability,
                travelCostHint = c.travelCostHint
            });
        }
        return clone;
    }
}
/// <summary>
/// Bundle indivisible: todas sus adquisiciones se conceden o ninguna.
/// </summary>
[Serializable]
public sealed class BistroBuilderInteractionBundleRequest
{
    public string bundleId = string.Empty;
    public List<BistroBuilderInteractionAcquisitionRequest> requests =
        new List<BistroBuilderInteractionAcquisitionRequest>();
}

[Serializable]
public sealed class BistroBuilderInteractionDecision
{
    public string requestId = string.Empty;
    public string bundleId = string.Empty;
    public BistroBuilderInteractionRequestOutcome outcome =
        BistroBuilderInteractionRequestOutcome.Pending;
    public BistroBuilderInteractionReasonCode reason =
        BistroBuilderInteractionReasonCode.None;
    public BistroBuilderInteractionGrantHandle handle;
    public string selectedTargetId = string.Empty;
    public string selectedResourceId = string.Empty;
    public string selectedChannelId = string.Empty;
    public string selectedSlotId = string.Empty;
    public string blockingGrantId = string.Empty;
    public long arbitrationEpoch;
    public string message = string.Empty;
}

[Serializable]
public sealed class BistroBuilderInteractionBundleDecision
{
    public string bundleId = string.Empty;
    public BistroBuilderInteractionRequestOutcome outcome;
    public BistroBuilderInteractionReasonCode reason;
    public long arbitrationEpoch;
    public List<BistroBuilderInteractionDecision> decisions =
        new List<BistroBuilderInteractionDecision>();
}

[Serializable]
public sealed class BistroBuilderInteractionGrantRecord
{
    public string grantId = string.Empty;
    public long generation;
    public long sequence;
    public long grantedEpoch;
    public BistroBuilderInteractionGrantKind kind;
    public BistroBuilderInteractionGrantState state;
    public BistroBuilderInteractionHolderKind holderKind;
    public string holderId = string.Empty;
    public string interactionId = string.Empty;
    public string targetId = string.Empty;
    public string resourceId = string.Empty;
    public string channelId = string.Empty;
    public string slotId = string.Empty;
    public string sessionId = string.Empty;
    public string parentGrantId = string.Empty;
    public long parentGeneration;
    public int taskPriorityClass;
    public int suitability;
    public int travelCostHint;
    public int capacityUnits = 1;
    public bool countsCapacity = true;
    public bool persistCanonical;
    public string conflictKey = string.Empty;
    public int logicalCapacity = 1;
    public bool targetExclusive;
    public double engageBySimulationTime;
    public string spatialLeaseId = string.Empty;
    public BistroBuilderInteractionReasonCode lastReason;

    public BistroBuilderInteractionGrantHandle Handle =>
        new BistroBuilderInteractionGrantHandle(grantId, generation);

    public bool IsTerminal =>
        state == BistroBuilderInteractionGrantState.Completed ||
        state == BistroBuilderInteractionGrantState.Cancelled ||
        state == BistroBuilderInteractionGrantState.Failed ||
        state == BistroBuilderInteractionGrantState.Expired ||
        state == BistroBuilderInteractionGrantState.Invalidated ||
        state == BistroBuilderInteractionGrantState.Released;
}
/// <summary>
/// Estado canónico persistible. No contiene permits de approach ni handles BBSIS.
/// </summary>
[Serializable]
public sealed class BistroBuilderInteractionCanonicalGrant
{
    public BistroBuilderInteractionGrantKind kind;
    public BistroBuilderInteractionGrantState state;
    public BistroBuilderInteractionHolderKind holderKind;
    public string holderId = string.Empty;
    public string interactionId = string.Empty;
    public string targetId = string.Empty;
    public string resourceId = string.Empty;
    public string channelId = string.Empty;
    public string slotId = string.Empty;
    public string sessionId = string.Empty;
    public string parentCanonicalKey = string.Empty;
    public long queueSequence;
}

[Serializable]
public sealed class BistroBuilderInteractionCanonicalSnapshot
{
    public const string CurrentSchemaId = "interaction.reservation.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public List<BistroBuilderInteractionCanonicalGrant> grants =
        new List<BistroBuilderInteractionCanonicalGrant>();
}

[Serializable]
public sealed class BistroBuilderInteractionTraceRecord
{
    public long epoch;
    public double simulationTime;
    public string subjectId = string.Empty;
    public string action = string.Empty;
    public string grantId = string.Empty;
    public string requestId = string.Empty;
    public BistroBuilderInteractionReasonCode reason;
    public string detail = string.Empty;
}