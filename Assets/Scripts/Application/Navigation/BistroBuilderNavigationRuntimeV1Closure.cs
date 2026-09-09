using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cierre v1: recuperación avanzada, colas físicas y replay diagnóstico.
/// </summary>
public sealed partial class BistroBuilderNavigationService
{
    private BistroBuilderNavigationRecoveryPlanner v1RecoveryPlanner;
    private BistroBuilderNavigationPhysicalQueueCoordinator v1PhysicalQueues;
    private BistroBuilderNavigationReplayRecorder v1Replay;
    private BistroBuilderNavigationFailureMemory v1FailureMemory;
    private readonly Dictionary<string, RecoveryManeuverRuntime> v1RecoveryManeuvers =
        new Dictionary<string, RecoveryManeuverRuntime>(StringComparer.Ordinal);

    public int PhysicalQueueCount => v1PhysicalQueues != null ? v1PhysicalQueues.QueueCount : 0;
    public int QueuedPhysicalAgentCount => v1PhysicalQueues != null ? v1PhysicalQueues.QueuedAgentCount : 0;
    public int ReplayEventCount => v1Replay != null ? v1Replay.Count : 0;

    private void EnsureClosureV1()
    {
        v1RecoveryPlanner ??= new BistroBuilderNavigationRecoveryPlanner();
        v1PhysicalQueues ??= new BistroBuilderNavigationPhysicalQueueCoordinator();
        v1Replay ??= new BistroBuilderNavigationReplayRecorder();
        v1FailureMemory ??= new BistroBuilderNavigationFailureMemory();
    }

    public bool ConfigurePhysicalQueue(string queueId, IReadOnlyList<Vector3> bbsisCertifiedSlots)
    {
        EnsureClosureV1();
        return v1PhysicalQueues.ConfigureQueue(queueId, bbsisCertifiedSlots);
    }

    public void ClearPhysicalQueueEntries(string queueId)
    {
        EnsureClosureV1();
        v1PhysicalQueues.ClearQueueEntries(queueId);
    }
    public bool EnqueuePhysicalQueue(string queueId, string ownerId, int logicalOrder)
    {
        EnsureClosureV1();
        bool ok = v1PhysicalQueues.Enqueue(queueId, ownerId, logicalOrder, Time.unscaledTime);
        if (ok) RecordReplayV1("queue.enqueue", ownerId, Vector3.zero, "queue=" + queueId);
        return ok;
    }
    public bool TryGetPhysicalQueueTarget(
        string ownerId, out string queueId, out int slotIndex, out Vector3 target, out bool overflow)
    {
        EnsureClosureV1();
        bool ok = v1PhysicalQueues.TryGetTarget(ownerId, out queueId, out slotIndex, out target, out overflow);
        if (v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) && trip != null)
        {
            trip.trace.state = BistroBuilderNavigationTravelState.Queueing;
            trip.trace.waitingReason = BistroBuilderNavigationWaitingReason.Queue;
            trip.trace.blockerId = overflow ? "queue:overflow:" + queueId : "queue:" + queueId;
            trip.trace.lastDecision = overflow
                ? "Physical queue capacity reached; waiting before certified slots."
                : "Advancing to certified physical queue slot " + slotIndex + ".";
        }
        return ok;
    }

    public void LeavePhysicalQueue(string ownerId)
    {
        EnsureClosureV1();
        v1PhysicalQueues.Remove(ownerId);
        RecordReplayV1("queue.leave", ownerId, Vector3.zero, string.Empty);
    }

    public int WritePhysicalQueueSnapshots(List<BistroBuilderNavigationPhysicalQueueSnapshot> results)
    {
        EnsureClosureV1();
        return v1PhysicalQueues.WriteSnapshots(results);
    }

    public void ClearNavigationReplay(string label = null)
    {
        EnsureClosureV1();
        v1Replay.Clear(label);
    }

    public BistroBuilderNavigationReplayBundle CaptureNavigationReplay()
    {
        EnsureClosureV1();
        return v1Replay.CaptureBundle();
    }

    public int WriteNavigationReplayEvents(List<BistroBuilderNavigationReplayEvent> results)
    {
        EnsureClosureV1();
        return v1Replay.WriteEvents(results);
    }
    private void RecordReplayV1(string eventType, string ownerId, Vector3 position, string detail)
    {
        EnsureClosureV1();
        BistroBuilderNavigationDecisionTrace trace = null;
        if (!string.IsNullOrWhiteSpace(ownerId) && v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) && trip != null)
            trace = trip.trace;
        v1Replay.Record(eventType, ownerId, position, trace, CaptureTopologySnapshot(), detail);
    }

    private bool TryScheduleRecoveryManeuverV1(
        string ownerId,
        BistroBuilderNavigationRecoveryStage stage,
        float now,
        string signature = "")
    {
        EnsureClosureV1();
        if (!v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) || trip == null)
            return false;
        if (v1RecoveryManeuvers.TryGetValue(ownerId, out RecoveryManeuverRuntime active) &&
            active != null && now < active.until)
            return true;

        string recoverySignature = string.IsNullOrWhiteSpace(signature) ? "generic" : signature;
        if (v1FailureMemory.IsSuppressed(ownerId, stage, recoverySignature, now))
        {
            trip.trace.lastDecision = "Recovery suppressed by Failure Memory: " + stage + ".";
            return false;
        }

        Vector3 current = trip.lastPosition;
        Vector3 forward = trip.lastPreferredDirection;
        Func<Vector3, bool> validator = candidate =>
            IsRecoveryPathClearV1(ownerId, trip.request.agentMask, current, candidate, trip.request.mobilityRadius);
        bool found = stage == BistroBuilderNavigationRecoveryStage.EscapePocket
            ? v1RecoveryPlanner.TryFindEscapePocket(ownerId, current, forward, validator, out Vector3 target)
            : v1RecoveryPlanner.TryFindRetreat(ownerId, current, forward, validator, out target);
        if (!found)
        {
            v1FailureMemory.RecordFailure(ownerId, stage, recoverySignature, now);
            return false;
        }

        v1RecoveryManeuvers[ownerId] = new RecoveryManeuverRuntime
        {
            target = target,
            stage = stage,
            until = now + (stage == BistroBuilderNavigationRecoveryStage.Retreat ? 2.5f : 1.8f),
            signature = recoverySignature
        };
        trip.trace.state = BistroBuilderNavigationTravelState.Recovering;
        trip.trace.recoveryStage = stage;
        trip.trace.waitingReason = BistroBuilderNavigationWaitingReason.Recovery;
        trip.trace.recoveryCount++;
        trip.recoveryDebt += stage == BistroBuilderNavigationRecoveryStage.Retreat ? 0.9f : 0.6f;
        v1Metrics.recoveryCount++;
        if (stage == BistroBuilderNavigationRecoveryStage.EscapePocket) v1Metrics.escapePocketCount++;
        else v1Metrics.retreatCount++;
        trip.trace.lastDecision = "Recovery maneuver scheduled: " + stage + ".";
        RecordReplayV1("recovery.schedule", ownerId, current, stage + " " + signature);
        return true;
    }
    private bool TryApplyActiveRecoveryManeuverV1(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 current,
        float speed,
        float radius,
        float dt,
        float now,
        out Vector3 proposed,
        out BistroBuilderNavigationLocalMoveDecision decision)
    {
        proposed = current;
        decision = default;
        if (!v1RecoveryManeuvers.TryGetValue(ownerId, out RecoveryManeuverRuntime maneuver) ||
            maneuver == null || now >= maneuver.until)
        {
            v1RecoveryManeuvers.Remove(ownerId);
            return false;
        }

        Vector3 delta = maneuver.target - current;
        delta.y = 0f;
        if (delta.sqrMagnitude <= 0.01f)
        {
            v1RecoveryManeuvers.Remove(ownerId);
            v1BlockGraph?.ClearDependency(ownerId);
            v1FailureMemory.RecordSuccess(ownerId, maneuver.stage, maneuver.signature);
            RecordReplayV1("recovery.complete", ownerId, current, maneuver.stage.ToString());
            return false;
        }

        Vector3 velocity = delta.normalized * Mathf.Max(0.15f, speed * 0.42f);
        Vector3 candidate = current + Vector3.ClampMagnitude(velocity * dt, delta.magnitude);
        candidate.y = current.y;
        if (!IsRecoveryPathClearV1(ownerId, agent, current, candidate, radius) ||
            !v1Traffic.IsPositionClear(ownerId, candidate, radius))
        {
            v1RecoveryManeuvers.Remove(ownerId);
            v1FailureMemory.RecordFailure(ownerId, maneuver.stage, maneuver.signature, now);
            RecordReplayV1("recovery.blocked", ownerId, current, maneuver.stage.ToString());
            return false;
        }

        proposed = candidate;
        decision = new BistroBuilderNavigationLocalMoveDecision
        {
            velocity = velocity,
            shouldYield = false,
            yieldingTo = string.Empty,
            effectivePriority = ResolveEffectivePriorityV1(ownerId),
            waitingReason = BistroBuilderNavigationWaitingReason.Recovery
        };
        v1BlockGraph?.ClearDependency(ownerId);
        return true;
    }
    private bool IsRecoveryPathClearV1(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 start,
        Vector3 end,
        float radius)
    {
        if (v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) &&
            trip != null && trip.plan != null && trip.plan.corridor != null &&
            trip.plan.corridor.IsUsable &&
            trip.plan.corridor.Matches(CaptureTopologySnapshot()) &&
            !v1CorridorBuilder.Contains(trip.plan.corridor, end, 0.01f))
            return false;

        if (!SegmentAllowedForNavMesh(start, end, radius, agent, ownerId, start, end))
            return false;

        const int Samples = 5;
        for (int i = 1; i <= Samples; i++)
        {
            Vector3 sample = Vector3.Lerp(start, end, i / (float)Samples);
            if (!IsHardMovementStepClear(ownerId, agent, sample, radius))
                return false;
        }
        return true;
    }

    private void CleanupAdvancedRecoveryV1(float now)
    {
        v1ScratchIds.Clear();
        foreach (KeyValuePair<string, RecoveryManeuverRuntime> pair in v1RecoveryManeuvers)
            if (pair.Value == null || now >= pair.Value.until)
                v1ScratchIds.Add(pair.Key);
        for (int i = 0; i < v1ScratchIds.Count; i++)
            v1RecoveryManeuvers.Remove(v1ScratchIds[i]);
        v1FailureMemory?.Cleanup(now);
    }

    private void RecordReplayTripV1(
        string eventType,
        NavigationTripRuntime trip,
        Vector3 position,
        string detail)
    {
        EnsureClosureV1();
        v1Replay.Record(
            eventType,
            trip != null && trip.request != null ? trip.request.ownerId : string.Empty,
            position,
            trip != null ? trip.trace : null,
            CaptureTopologySnapshot(),
            detail ?? string.Empty);
    }
    private sealed class RecoveryManeuverRuntime
    {
        public Vector3 target;
        public BistroBuilderNavigationRecoveryStage stage;
        public float until;
        public string signature;
    }
}
