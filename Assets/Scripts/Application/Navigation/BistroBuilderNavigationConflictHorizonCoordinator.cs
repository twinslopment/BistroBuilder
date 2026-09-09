using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinación predictiva de los próximos cuellos de botella.
/// No reserva espacio: anticipa incompatibilidades antes de que Controlled Passage actúe.
/// </summary>
public sealed class BistroBuilderNavigationConflictHorizonCoordinator
{
    private const float HorizonDepth = 2.2f;
    private const float IntentStaleSeconds = 0.9f;
    private const float FairnessSeconds = 2.4f;
    private readonly Dictionary<string, GateState> gates =
        new Dictionary<string, GateState>(StringComparer.Ordinal);
    private readonly List<string> scratchIds = new List<string>(32);
    private readonly BistroBuilderNavigationMicroConflictPlanner microPlanner =
        new BistroBuilderNavigationMicroConflictPlanner();
    private readonly List<BistroBuilderNavigationMicroConflictIntent> microIntents =
        new List<BistroBuilderNavigationMicroConflictIntent>(12);
    private readonly List<string> microOrder = new List<string>(12);

    public int ActiveEpisodeCount { get; private set; }
    public long ArbitrationCount { get; private set; }
    public long MicroPlanCount { get; private set; }

    public void Configure(IReadOnlyList<BistroBuilderNavigationGateDescriptor> descriptors)
    {
        gates.Clear();
        if (descriptors == null) return;
        for (int i = 0; i < descriptors.Count; i++)
        {
            BistroBuilderNavigationGateDescriptor d = descriptors[i];
            if (d == null || string.IsNullOrWhiteSpace(d.gateId)) continue;
            Vector3 axis = Horizontal(d.end - d.start);
            float width = Mathf.Max(0.1f, Mathf.Max(axis.magnitude, d.minimumWidth));
            axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;
            Vector3 normal = Vector3.Cross(Vector3.up, axis).normalized;
            gates[d.gateId] = new GateState
            {
                id = d.gateId,
                center = (d.start + d.end) * 0.5f,
                axis = axis,
                normal = normal.sqrMagnitude > 0.0001f ? normal : Vector3.forward,
                width = width,
                critical = d.criticalRoute
            };
        }
    }
    public bool TryCoordinate(
        string ownerId,
        Vector3 current,
        Vector3 proposed,
        float radius,
        float effectivePriority,
        float waitingAge,
        float now,
        out BistroBuilderNavigationConflictHorizonDecision decision)
    {
        decision = default;
        if (string.IsNullOrWhiteSpace(ownerId)) return true;
        GateState gate = FindGate(current, proposed, radius);
        if (gate == null) return true;

        CleanupGate(gate, now);
        int direction = ResolveDirection(gate, current, proposed);
        if (direction == 0) return true;
        gate.intents[ownerId] = new Intent
        {
            ownerId = ownerId,
            direction = direction,
            priority = effectivePriority,
            waitingAge = Mathf.Max(0f, waitingAge),
            seenAt = now
        };

        ResolveEpisode(gate, now);
        if (gate.servingDirection == 0 || gate.servingDirection == direction)
        {
            decision = BuildDecision(gate, ownerId, true, direction, string.Empty);
            return true;
        }

        string blocker = ResolvePreferredBlocker(gate, direction);
        decision = BuildDecision(gate, ownerId, false, direction, blocker);
        return false;
    }

    public void RemoveAgent(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        foreach (GateState gate in gates.Values) gate.intents.Remove(ownerId);
    }
    public void Cleanup(float now)
    {
        int active = 0;
        foreach (GateState gate in gates.Values)
        {
            CleanupGate(gate, now);
            if (HasOpposingIntent(gate)) active++;
        }
        ActiveEpisodeCount = active;
    }

    private void ResolveEpisode(GateState gate, float now)
    {
        if (!HasOpposingIntent(gate))
        {
            gate.servingDirection = SoleDirection(gate);
            gate.servingSince = now;
            return;
        }

        ActiveEpisodeCount = Math.Max(1, ActiveEpisodeCount);
        ArbitrationCount++;
        float positive = DirectionScore(gate, 1);
        float negative = DirectionScore(gate, -1);
        int desired = positive > negative + 0.001f ? 1 :
            (negative > positive + 0.001f ? -1 : StableDirectionTie(gate));

        if (gate.intents.Count >= 3)
        {
            microIntents.Clear();
            foreach (Intent intent in gate.intents.Values)
                if (intent != null)
                    microIntents.Add(new BistroBuilderNavigationMicroConflictIntent
                    {
                        ownerId = intent.ownerId, direction = intent.direction,
                        priority = intent.priority, waitingAge = intent.waitingAge
                    });
            if (microPlanner.BuildOrder(microIntents, microOrder) > 0 &&
                gate.intents.TryGetValue(microOrder[0], out Intent first) && first != null)
            {
                desired = first.direction;
                MicroPlanCount++;
            }
        }

        if (gate.servingDirection == 0)
        {
            gate.servingDirection = desired;
            gate.servingSince = now;
            return;
        }

        if (desired != gate.servingDirection &&
            now - gate.servingSince >= FairnessSeconds)
        {
            gate.servingDirection = desired;
            gate.servingSince = now;
        }
    }

    private GateState FindGate(Vector3 current, Vector3 proposed, float radius)
    {
        GateState best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (GateState gate in gates.Values)
        {
            if (!gate.critical && gate.width >= 1.4f) continue;
            Vector3 rc = Horizontal(current - gate.center);
            Vector3 rp = Horizontal(proposed - gate.center);
            float longitudinal = Mathf.Min(Mathf.Abs(Vector3.Dot(rc, gate.normal)),
                                           Mathf.Abs(Vector3.Dot(rp, gate.normal)));
            float lateral = Mathf.Min(Mathf.Abs(Vector3.Dot(rc, gate.axis)),
                                      Mathf.Abs(Vector3.Dot(rp, gate.axis)));
            float halfWidth = gate.width * 0.5f + Mathf.Max(0.05f, radius);
            if (longitudinal > HorizonDepth || lateral > halfWidth) continue;
            if (longitudinal < bestDistance) { best = gate; bestDistance = longitudinal; }
        }
        return best;
    }
    private static int ResolveDirection(GateState gate, Vector3 current, Vector3 proposed)
    {
        float delta = Vector3.Dot(Horizontal(proposed - current), gate.normal);
        if (Mathf.Abs(delta) <= 0.0001f) return 0;
        return delta > 0f ? 1 : -1;
    }

    private static float DirectionScore(GateState gate, int direction)
    {
        float score = 0f;
        foreach (Intent intent in gate.intents.Values)
            if (intent != null && intent.direction == direction)
                score += 1f + Mathf.Max(0f, intent.priority) + intent.waitingAge * 4f;
        return score;
    }

    private static int StableDirectionTie(GateState gate)
    {
        string positiveId = null;
        string negativeId = null;
        foreach (Intent intent in gate.intents.Values)
        {
            if (intent == null) continue;
            if (intent.direction > 0 && (positiveId == null || string.CompareOrdinal(intent.ownerId, positiveId) < 0))
                positiveId = intent.ownerId;
            if (intent.direction < 0 && (negativeId == null || string.CompareOrdinal(intent.ownerId, negativeId) < 0))
                negativeId = intent.ownerId;
        }
        if (positiveId == null) return -1;
        if (negativeId == null) return 1;
        return string.CompareOrdinal(positiveId, negativeId) <= 0 ? 1 : -1;
    }

    private static string ResolvePreferredBlocker(GateState gate, int waitingDirection)
    {
        string best = string.Empty;
        float bestScore = float.NegativeInfinity;
        foreach (Intent intent in gate.intents.Values)
        {
            if (intent == null || intent.direction == waitingDirection) continue;
            float score = intent.priority + intent.waitingAge * 4f;
            if (score > bestScore + 0.001f ||
                (Mathf.Abs(score - bestScore) <= 0.001f &&
                 (string.IsNullOrEmpty(best) || string.CompareOrdinal(intent.ownerId, best) < 0)))
            {
                best = intent.ownerId;
                bestScore = score;
            }
        }
        return best;
    }
    private static bool HasOpposingIntent(GateState gate)
    {
        bool positive = false, negative = false;
        foreach (Intent intent in gate.intents.Values)
        {
            if (intent == null) continue;
            if (intent.direction > 0) positive = true;
            else if (intent.direction < 0) negative = true;
            if (positive && negative) return true;
        }
        return false;
    }

    private static int SoleDirection(GateState gate)
    {
        foreach (Intent intent in gate.intents.Values)
            if (intent != null && intent.direction != 0) return intent.direction;
        return 0;
    }

    private void CleanupGate(GateState gate, float now)
    {
        scratchIds.Clear();
        foreach (KeyValuePair<string, Intent> pair in gate.intents)
            if (pair.Value == null || now - pair.Value.seenAt > IntentStaleSeconds)
                scratchIds.Add(pair.Key);
        for (int i = 0; i < scratchIds.Count; i++) gate.intents.Remove(scratchIds[i]);
        if (gate.intents.Count == 0) gate.servingDirection = 0;
    }

    private static BistroBuilderNavigationConflictHorizonDecision BuildDecision(
        GateState gate, string ownerId, bool allowed, int direction, string blocker)
    {
        return new BistroBuilderNavigationConflictHorizonDecision
        {
            gateId = gate.id,
            ownerId = ownerId,
            allowed = allowed,
            direction = direction,
            blockerId = blocker ?? string.Empty,
            servingDirection = gate.servingDirection,
            participants = gate.intents.Count
        };
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private sealed class GateState
    {
        public string id;
        public Vector3 center;
        public Vector3 axis;
        public Vector3 normal;
        public float width;
        public bool critical;
        public int servingDirection;
        public float servingSince;
        public readonly Dictionary<string, Intent> intents =
            new Dictionary<string, Intent>(StringComparer.Ordinal);
    }

    private sealed class Intent
    {
        public string ownerId;
        public int direction;
        public float priority;
        public float waitingAge;
        public float seenAt;
    }
}

[Serializable]
public struct BistroBuilderNavigationConflictHorizonDecision
{
    public string gateId;
    public string ownerId;
    public bool allowed;
    public int direction;
    public int servingDirection;
    public string blockerId;
    public int participants;
}
