using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordina el derecho temporal de avance por Spatial Gates estrechos.
/// No crea Claims ni Spatial Leases: BBSIS conserva toda autoridad espacial.
/// </summary>
public sealed class BistroBuilderNavigationControlledPassageCoordinator
{
    private const float MinimumGateWidth = 0.1f;
    private const float ApproachDepth = 0.85f;
    private const float ExitDepth = 1.05f;
    private const float ExtraCoexistenceMargin = 0.12f;
    private const float OppositeWaitPressureSeconds = 2.2f;
    private const int DefaultMaxBurst = 3;

    private readonly Dictionary<string, GateRecord> gates =
        new Dictionary<string, GateRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, AgentPosition> positions =
        new Dictionary<string, AgentPosition>(StringComparer.Ordinal);
    private readonly List<string> scratchIds = new List<string>(32);

    public int GateCount => gates.Count;
    public long SwitchCount { get; private set; }
    public long WaitCount { get; private set; }

    public void Configure(IReadOnlyList<BistroBuilderNavigationGateDescriptor> descriptors)
    {
        gates.Clear();
        if (descriptors == null) return;

        for (int i = 0; i < descriptors.Count; i++)
        {
            BistroBuilderNavigationGateDescriptor source = descriptors[i];
            if (source == null || string.IsNullOrWhiteSpace(source.gateId))
                continue;

            Vector3 axis = Horizontal(source.end - source.start);
            float measuredWidth = axis.magnitude;
            if (measuredWidth <= 0.0001f)
                axis = Vector3.right;
            else
                axis /= measuredWidth;

            Vector3 normal = Vector3.Cross(Vector3.up, axis).normalized;
            if (normal.sqrMagnitude <= 0.0001f)
                normal = Vector3.forward;

            float declaredWidth = Mathf.Max(MinimumGateWidth, source.minimumWidth);
            float width = Mathf.Max(declaredWidth, measuredWidth);
            gates[source.gateId] = new GateRecord
            {
                gateId = source.gateId,
                subjectId = source.subjectId ?? string.Empty,
                center = (source.start + source.end) * 0.5f,
                axis = axis,
                normal = normal,
                width = width,
                criticalRoute = source.criticalRoute,
                state = BistroBuilderControlledPassageState.Free
            };
        }
    }

    public void UpdateAgentPosition(string ownerId, Vector3 position, float now)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        positions[ownerId] = new AgentPosition { position = position, seenAt = now };
        ReleaseExitedPassages(ownerId, position, now);
    }

    public void RemoveAgent(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        positions.Remove(ownerId);
        foreach (GateRecord gate in gates.Values)
        {
            gate.activeOwners.Remove(ownerId);
            gate.positiveWaiting.Remove(ownerId);
            gate.negativeWaiting.Remove(ownerId);
            AdvanceState(gate, Time.unscaledTime);
        }
    }

    public bool TryAuthorizeStep(
        string ownerId,
        Vector3 current,
        Vector3 proposed,
        float radius,
        int externalUrgency,
        float waitingAgeSeconds,
        float now,
        out BistroBuilderNavigationControlledPassageDecision decision)
    {
        decision = default;
        if (string.IsNullOrWhiteSpace(ownerId)) return true;

        GateRecord gate = FindRelevantGate(current, proposed, radius);
        if (gate == null) return true;

        float requiredTwoWayWidth = Mathf.Max(0.05f, radius) * 4f +
                                    ExtraCoexistenceMargin;
        if (!gate.criticalRoute && gate.width >= requiredTwoWayWidth)
            return true;
        if (gate.width >= requiredTwoWayWidth * 1.2f)
            return true;

        int direction = ResolveDirection(gate, current, proposed);
        if (direction == 0) return true;

        AdvanceState(gate, now);
        if (gate.activeOwners.Contains(ownerId))
        {
            decision = BuildDecision(gate, true, direction, ownerId);
            return true;
        }

        bool servingSame =
            (direction > 0 && gate.state == BistroBuilderControlledPassageState.ServingPositive) ||
            (direction < 0 && gate.state == BistroBuilderControlledPassageState.ServingNegative);
        bool free = gate.state == BistroBuilderControlledPassageState.Free;

        if (free)
        {
            StartServing(gate, direction);
            Admit(gate, ownerId, direction);
            decision = BuildDecision(gate, true, direction, ownerId);
            return true;
        }

        if (servingSame && !ShouldDrainForOpposite(gate, direction, now) &&
            gate.servedBurst < DefaultMaxBurst)
        {
            Admit(gate, ownerId, direction);
            decision = BuildDecision(gate, true, direction, ownerId);
            return true;
        }

        Enqueue(gate, ownerId, direction, externalUrgency, waitingAgeSeconds, now);
        if (servingSame && gate.activeOwners.Count == 0)
            gate.state = BistroBuilderControlledPassageState.Draining;

        WaitCount++;
        decision = BuildDecision(gate, false, direction, ownerId);
        return false;
    }

    public bool TryGetSnapshot(
        string gateId,
        out BistroBuilderNavigationControlledPassageSnapshot snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(gateId) ||
            !gates.TryGetValue(gateId, out GateRecord gate) || gate == null)
            return false;

        snapshot = new BistroBuilderNavigationControlledPassageSnapshot
        {
            gateId = gate.gateId,
            subjectId = gate.subjectId,
            state = gate.state,
            width = gate.width,
            activeCount = gate.activeOwners.Count,
            waitingPositive = gate.positiveWaiting.Count,
            waitingNegative = gate.negativeWaiting.Count,
            servedBurst = gate.servedBurst
        };
        return true;
    }

    public void Cleanup(float now)
    {
        scratchIds.Clear();
        foreach (KeyValuePair<string, AgentPosition> pair in positions)
            if (pair.Value == null || now - pair.Value.seenAt > 1.25f)
                scratchIds.Add(pair.Key);

        for (int i = 0; i < scratchIds.Count; i++)
            RemoveAgent(scratchIds[i]);

        foreach (GateRecord gate in gates.Values)
            AdvanceState(gate, now);
    }

    private GateRecord FindRelevantGate(Vector3 current, Vector3 proposed, float radius)
    {
        GateRecord best = null;
        float bestDistance = float.PositiveInfinity;
        Vector3 movement = Horizontal(proposed - current);
        if (movement.sqrMagnitude <= 0.000001f) return null;

        foreach (GateRecord gate in gates.Values)
        {
            Vector3 relativeCurrent = Horizontal(current - gate.center);
            Vector3 relativeProposed = Horizontal(proposed - gate.center);
            float alongCurrent = Vector3.Dot(relativeCurrent, gate.normal);
            float alongProposed = Vector3.Dot(relativeProposed, gate.normal);
            float lateralCurrent = Mathf.Abs(Vector3.Dot(relativeCurrent, gate.axis));
            float lateralProposed = Mathf.Abs(Vector3.Dot(relativeProposed, gate.axis));
            float halfWidth = gate.width * 0.5f + Mathf.Max(0.05f, radius);

            bool laterallyRelevant =
                Mathf.Min(lateralCurrent, lateralProposed) <= halfWidth;
            bool approaching =
                Mathf.Min(Mathf.Abs(alongCurrent), Mathf.Abs(alongProposed)) <= ApproachDepth ||
                Mathf.Sign(alongCurrent) != Mathf.Sign(alongProposed);
            if (!laterallyRelevant || !approaching) continue;

            float distance = Mathf.Min(Mathf.Abs(alongCurrent), Mathf.Abs(alongProposed));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = gate;
            }
        }

        return best;
    }

    private void ReleaseExitedPassages(string ownerId, Vector3 position, float now)
    {
        foreach (GateRecord gate in gates.Values)
        {
            if (!gate.activeOwners.Contains(ownerId)) continue;
            Vector3 relative = Horizontal(position - gate.center);
            float longitudinal = Mathf.Abs(Vector3.Dot(relative, gate.normal));
            if (longitudinal <= ExitDepth) continue;

            gate.activeOwners.Remove(ownerId);
            AdvanceState(gate, now);
        }
    }

    private void AdvanceState(GateRecord gate, float now)
    {
        if (gate == null) return;
        if (gate.activeOwners.Count > 0)
        {
            if (ShouldDrainForOpposite(gate, ActiveDirection(gate), now))
                gate.state = BistroBuilderControlledPassageState.Draining;
            return;
        }

        int currentDirection = ActiveDirection(gate);
        int nextDirection = ChooseNextDirection(gate, currentDirection, now);
        if (nextDirection == 0)
        {
            gate.state = BistroBuilderControlledPassageState.Free;
            gate.servedBurst = 0;
            return;
        }

        if (currentDirection != 0 && currentDirection != nextDirection)
        {
            gate.state = BistroBuilderControlledPassageState.Switching;
            SwitchCount++;
        }

        StartServing(gate, nextDirection);
    }

    private static int ChooseNextDirection(GateRecord gate, int previousDirection, float now)
    {
        WaitingEntry positive = BestWaiting(gate.positiveWaiting, now);
        WaitingEntry negative = BestWaiting(gate.negativeWaiting, now);
        if (positive == null && negative == null) return 0;
        if (positive != null && negative == null) return 1;
        if (negative != null && positive == null) return -1;

        float positiveScore = WaitingScore(positive, now);
        float negativeScore = WaitingScore(negative, now);
        if (positiveScore > negativeScore + 0.0001f) return 1;
        if (negativeScore > positiveScore + 0.0001f) return -1;
        if (previousDirection != 0) return -previousDirection;
        return string.CompareOrdinal(positive.ownerId, negative.ownerId) <= 0 ? 1 : -1;
    }

    private static bool ShouldDrainForOpposite(GateRecord gate, int direction, float now)
    {
        Dictionary<string, WaitingEntry> opposite =
            direction > 0 ? gate.negativeWaiting : gate.positiveWaiting;
        WaitingEntry oldest = BestWaiting(opposite, now);
        if (oldest == null) return false;
        if (gate.servedBurst >= DefaultMaxBurst) return true;
        return now - oldest.firstRequestedAt >= OppositeWaitPressureSeconds;
    }

    private static void StartServing(GateRecord gate, int direction)
    {
        gate.state = direction > 0
            ? BistroBuilderControlledPassageState.ServingPositive
            : BistroBuilderControlledPassageState.ServingNegative;
        gate.servedBurst = 0;
    }

    private static void Admit(GateRecord gate, string ownerId, int direction)
    {
        gate.activeOwners.Add(ownerId);
        gate.positiveWaiting.Remove(ownerId);
        gate.negativeWaiting.Remove(ownerId);
        gate.servedBurst++;
        gate.lastDirection = direction;
    }

    private static void Enqueue(
        GateRecord gate,
        string ownerId,
        int direction,
        int externalUrgency,
        float waitingAgeSeconds,
        float now)
    {
        Dictionary<string, WaitingEntry> target =
            direction > 0 ? gate.positiveWaiting : gate.negativeWaiting;
        if (target.TryGetValue(ownerId, out WaitingEntry existing) && existing != null)
        {
            existing.externalUrgency = externalUrgency;
            existing.reportedWaitingAge = Mathf.Max(existing.reportedWaitingAge, waitingAgeSeconds);
            return;
        }

        target[ownerId] = new WaitingEntry
        {
            ownerId = ownerId,
            firstRequestedAt = now,
            externalUrgency = externalUrgency,
            reportedWaitingAge = Mathf.Max(0f, waitingAgeSeconds)
        };
    }

    private static WaitingEntry BestWaiting(
        Dictionary<string, WaitingEntry> entries,
        float now)
    {
        WaitingEntry best = null;
        float bestScore = float.NegativeInfinity;
        foreach (WaitingEntry entry in entries.Values)
        {
            if (entry == null) continue;
            float score = WaitingScore(entry, now);
            if (best == null || score > bestScore + 0.0001f ||
                (Mathf.Abs(score - bestScore) <= 0.0001f &&
                 string.CompareOrdinal(entry.ownerId, best.ownerId) < 0))
            {
                best = entry;
                bestScore = score;
            }
        }
        return best;
    }

    private static float WaitingScore(WaitingEntry entry, float now)
    {
        return entry.externalUrgency +
               Mathf.Max(entry.reportedWaitingAge, now - entry.firstRequestedAt) * 4f;
    }

    private static int ResolveDirection(GateRecord gate, Vector3 current, Vector3 proposed)
    {
        Vector3 movement = Horizontal(proposed - current);
        float dot = Vector3.Dot(movement, gate.normal);
        if (Mathf.Abs(dot) <= 0.00001f) return 0;
        return dot > 0f ? 1 : -1;
    }

    private static int ActiveDirection(GateRecord gate)
    {
        if (gate.state == BistroBuilderControlledPassageState.ServingPositive) return 1;
        if (gate.state == BistroBuilderControlledPassageState.ServingNegative) return -1;
        return gate.lastDirection;
    }

    private static BistroBuilderNavigationControlledPassageDecision BuildDecision(
        GateRecord gate,
        bool granted,
        int direction,
        string ownerId)
    {
        return new BistroBuilderNavigationControlledPassageDecision
        {
            granted = granted,
            gateId = gate.gateId,
            direction = direction,
            state = gate.state,
            waitingReason = granted
                ? BistroBuilderNavigationWaitingReason.None
                : BistroBuilderNavigationWaitingReason.ControlledPassage,
            blockerId = granted ? string.Empty : "gate:" + gate.gateId
        };
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private sealed class GateRecord
    {
        public string gateId;
        public string subjectId;
        public Vector3 center;
        public Vector3 axis;
        public Vector3 normal;
        public float width;
        public bool criticalRoute;
        public BistroBuilderControlledPassageState state;
        public int lastDirection;
        public int servedBurst;
        public readonly HashSet<string> activeOwners =
            new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, WaitingEntry> positiveWaiting =
            new Dictionary<string, WaitingEntry>(StringComparer.Ordinal);
        public readonly Dictionary<string, WaitingEntry> negativeWaiting =
            new Dictionary<string, WaitingEntry>(StringComparer.Ordinal);
    }

    private sealed class WaitingEntry
    {
        public string ownerId;
        public float firstRequestedAt;
        public int externalUrgency;
        public float reportedWaitingAge;
    }

    private sealed class AgentPosition
    {
        public Vector3 position;
        public float seenAt;
    }
}