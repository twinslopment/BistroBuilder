using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Solver local determinista de trÃƒÂ¡fico humano.
/// Mantiene encuentros bilaterales estables y calcula una velocidad cooperativa
/// sin crear Claims, leases ni modificar Mobility/Carry Envelopes de BBSIS.
/// </summary>
public sealed class BistroBuilderNavigationTrafficCoordinator
{
    private const float MinimumRadius = 0.05f;
    private const float SafetyMargin = 0.06f;
    private const float EncounterLookAhead = 1.15f;
    private const float EncounterReleaseSeconds = 1.25f;
    private const float EncounterPreferenceMinHoldSeconds = 0.65f;
    private const float EncounterPreferenceMaxHoldSeconds = 2.25f;
    private const float EncounterPrioritySwitchMargin = 1.5f;
    private const float PresenceStaleSeconds = 1f;
    private const float SpatialCellSize = 1.5f;
    private const float NeighbourQueryDistance = 4.5f;

    private readonly Dictionary<string, AgentRecord> agents =
        new Dictionary<string, AgentRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, EncounterRecord> encounters =
        new Dictionary<string, EncounterRecord>(StringComparer.Ordinal);
    private readonly Dictionary<long, List<string>> spatialCells =
        new Dictionary<long, List<string>>(128);
    private readonly List<string> scratchIds = new List<string>(64);
    private readonly BistroBuilderNavigationReciprocalConstraintSolver reciprocalSolver =
        new BistroBuilderNavigationReciprocalConstraintSolver();
    private float nextCleanupAt;

    public int ActiveAgentCount => agents.Count;
    public int ActiveEncounterCount => encounters.Count;
    public long EncounterCreatedCount { get; private set; }
    public long ReciprocalConstraintSolveCount { get; private set; }
    public long ReciprocalConstraintCount { get; private set; }

    public void UpsertPresence(
        string ownerId,
        BistroBuilderNavigationAgentMask agentMask,
        Vector3 position,
        Vector3 velocity,
        float radius,
        int externalUrgency,
        float waitingAgeSeconds,
        float recoveryDebt,
        float commitment,
        float now)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return;

        long cellKey = BuildCellKey(position);
        bool hadPrevious = agents.TryGetValue(ownerId, out AgentRecord previous) &&
                           previous != null;
        if (!hadPrevious || previous.cellKey != cellKey)
        {
            if (hadPrevious)
                RemoveFromSpatialCell(previous.cellKey, ownerId);
            AddToSpatialCell(cellKey, ownerId);
        }

        agents[ownerId] = new AgentRecord
        {
            ownerId = ownerId,
            agentMask = agentMask,
            position = position,
            velocity = Horizontal(velocity),
            radius = Mathf.Max(MinimumRadius, radius),
            externalUrgency = externalUrgency,
            waitingAgeSeconds = Mathf.Max(0f, waitingAgeSeconds),
            recoveryDebt = Mathf.Max(0f, recoveryDebt),
            commitment = Mathf.Clamp01(commitment),
            seenAt = now,
            cellKey = cellKey
        };
    }

    public void RemovePresence(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return;

        if (agents.TryGetValue(ownerId, out AgentRecord removed) && removed != null)
            RemoveFromSpatialCell(removed.cellKey, ownerId);
        agents.Remove(ownerId);
        scratchIds.Clear();
        foreach (KeyValuePair<string, EncounterRecord> pair in encounters)
        {
            EncounterRecord encounter = pair.Value;
            if (encounter == null ||
                string.Equals(encounter.firstOwnerId, ownerId, StringComparison.Ordinal) ||
                string.Equals(encounter.secondOwnerId, ownerId, StringComparison.Ordinal))
                scratchIds.Add(pair.Key);
        }

        for (int i = 0; i < scratchIds.Count; i++)
            encounters.Remove(scratchIds[i]);
    }

    public bool IsPositionClear(string ownerId, Vector3 position, float radius)
    {
        float ownRadius = Mathf.Max(MinimumRadius, radius);
        CollectNeighbourIds(position, ownerId);
        for (int i = 0; i < scratchIds.Count; i++)
        {
            if (!agents.TryGetValue(scratchIds[i], out AgentRecord other) || other == null)
                continue;
            float minimum = ownRadius + other.radius + SafetyMargin;
            if (Horizontal(other.position - position).sqrMagnitude < minimum * minimum)
                return false;
        }
        return true;
    }

    public BistroBuilderNavigationLocalMoveDecision Solve(
        BistroBuilderNavigationLocalMoveInput input,
        float now)
    {
        if (now >= nextCleanupAt)
        {
            Cleanup(now);
            nextCleanupAt = now + 0.25f;
        }

        Vector3 preferred = Horizontal(input.preferredVelocity);
        var decision = new BistroBuilderNavigationLocalMoveDecision
        {
            velocity = preferred,
            shouldYield = false,
            yieldingTo = string.Empty,
            passingSide = 0,
            effectivePriority = CalculateEffectivePriority(input),
            waitingReason = BistroBuilderNavigationWaitingReason.None
        };

        if (string.IsNullOrWhiteSpace(input.ownerId) ||
            preferred.sqrMagnitude <= 0.000001f)
            return decision;

        float radius = Mathf.Max(MinimumRadius, input.radius);
        AgentRecord bestConflict = null;
        float bestTime = float.PositiveInfinity;
        float bestSeparation = float.PositiveInfinity;

        CollectNeighbourIds(input.position, input.ownerId);
        reciprocalSolver.Clear();
        float selfPriority = CalculateEffectivePriority(input);

        for (int i = 0; i < scratchIds.Count; i++)
        {
            AgentRecord other = agents[scratchIds[i]];
            if (other == null || now - other.seenAt > PresenceStaleSeconds)
              continue;

            if (!PredictConflict(
                    input.position,
                    preferred,
                    radius,
                    other,
                    out float timeToClosest,
                    out float closestSeparation))
                continue;

            if (reciprocalSolver.ConstraintCount < 8)
            {
                float otherPriority = CalculateEffectivePriority(other);
                float responsibility = selfPriority > otherPriority + 0.5f
                    ? 0.35f : (otherPriority > selfPriority + 0.5f ? 0.65f : 0.5f);
                reciprocalSolver.AddAgentConstraint(
                    input.position,
                    input.currentVelocity,
                    radius,
                    other.position,
                    other.velocity,
                    other.radius,
                    other.ownerId,
                    Mathf.Max(0.45f, EncounterLookAhead),
                    responsibility);
            }

            bool earlier = timeToClosest < bestTime - 0.0001f;
            bool sameTime = Mathf.Abs(timeToClosest - bestTime) <= 0.0001f;
            bool closer = closestSeparation < bestSeparation - 0.0001f;
            bool sameSeparation = Mathf.Abs(closestSeparation - bestSeparation) <= 0.0001f;
            bool stableTieBreak = bestConflict == null ||
                string.CompareOrdinal(other.ownerId, bestConflict.ownerId) < 0;
            if (earlier || (sameTime && (closer || (sameSeparation && stableTieBreak))))
            {
                bestConflict = other;
                bestTime = timeToClosest;
                bestSeparation = closestSeparation;
            }
        }

        if (bestConflict == null)
            return decision;

        EncounterRecord active = GetOrCreateEncounter(input, bestConflict, now);
        active.lastSeenAt = now;
        decision.passingSide = ResolvePassingSide(active, input.ownerId);
        decision.yieldingTo = bestConflict.ownerId;

        Vector3 forward = preferred.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 lateral = right * decision.passingSide;
        float speed = preferred.magnitude;
        float currentDistance = Horizontal(bestConflict.position - input.position).magnitude;
        float combinedRadius = radius + bestConflict.radius + SafetyMargin;
        bool preferredOwner = string.Equals(
            active.preferredOwnerId,
            input.ownerId,
            StringComparison.Ordinal);

        if (preferredOwner)
        {
            float lateralWeight = Mathf.Clamp01(
                (combinedRadius * 2.4f - currentDistance) /
                Mathf.Max(0.1f, combinedRadius * 1.4f));
            decision.velocity =
                forward * speed +
                lateral * speed * 0.32f * lateralWeight;
            decision.velocity = ClampMagnitude(decision.velocity, speed);
            return ApplyReciprocalConstraints(input, decision, speed);
        }

        decision.shouldYield = true;
        decision.waitingReason = BistroBuilderNavigationWaitingReason.Yield;
        float stopBand = combinedRadius * 1.15f;
        float slowBand = combinedRadius * 2.4f;

        if (currentDistance <= stopBand)
        {
            decision.velocity = Vector3.zero;
            return ApplyReciprocalConstraints(input, decision, speed);
        }

        float normalized = Mathf.InverseLerp(stopBand, slowBand, currentDistance);
        float forwardScale = Mathf.Lerp(0.18f, 0.72f, normalized);
        float lateralScale = Mathf.Lerp(0.48f, 0.24f, normalized);
        decision.velocity =
            forward * speed * forwardScale +
            lateral * speed * lateralScale;
        decision.velocity = ClampMagnitude(decision.velocity, speed);
        return ApplyReciprocalConstraints(input, decision, speed);
    }

    private BistroBuilderNavigationLocalMoveDecision ApplyReciprocalConstraints(
        BistroBuilderNavigationLocalMoveInput input,
        BistroBuilderNavigationLocalMoveDecision decision,
        float speed)
    {
        if (reciprocalSolver.ConstraintCount <= 0) return decision;
        decision.velocity = reciprocalSolver.Solve(
            decision.velocity,
            input.currentVelocity,
            Mathf.Max(0.05f, speed),
            14f,
            0.04f);
        ReciprocalConstraintSolveCount++;
        ReciprocalConstraintCount += reciprocalSolver.LastConstraintCount;
        return decision;
    }

    public bool TryGetEncounter(
        string firstOwnerId,
        string secondOwnerId,
        out BistroBuilderNavigationEncounterSnapshot snapshot)
    {
        snapshot = null;
        string key = BuildEncounterKey(firstOwnerId, secondOwnerId);
        if (!encounters.TryGetValue(key, out EncounterRecord record) || record == null)
            return false;

        snapshot = new BistroBuilderNavigationEncounterSnapshot
        {
            firstOwnerId = record.firstOwnerId,
            secondOwnerId = record.secondOwnerId,
            preferredOwnerId = record.preferredOwnerId,
            passingSide = record.passingSide,
            createdAt = record.createdAt,
            lastSeenAt = record.lastSeenAt
        };
        return true;
    }

    public void Cleanup(float now)
    {
        scratchIds.Clear();
        foreach (KeyValuePair<string, AgentRecord> pair in agents)
            if (pair.Value == null ||
                now - pair.Value.seenAt > PresenceStaleSeconds)
                scratchIds.Add(pair.Key);
        for (int i = 0; i < scratchIds.Count; i++)
        {
            string ownerId = scratchIds[i];
            if (agents.TryGetValue(ownerId, out AgentRecord stale) && stale != null)
                RemoveFromSpatialCell(stale.cellKey, ownerId);
            agents.Remove(ownerId);
        }

        scratchIds.Clear();
        foreach (KeyValuePair<string, EncounterRecord> pair in encounters)
        {
            EncounterRecord encounter = pair.Value;
            if (encounter == null ||
                now - encounter.lastSeenAt > EncounterReleaseSeconds ||
                !agents.ContainsKey(encounter.firstOwnerId) ||
                !agents.ContainsKey(encounter.secondOwnerId))
                scratchIds.Add(pair.Key);
        }
        for (int i = 0; i < scratchIds.Count; i++)
            encounters.Remove(scratchIds[i]);
    }

    private void CollectNeighbourIds(Vector3 position, string ownerId)
    {
        scratchIds.Clear();
        int centerX = CellCoordinate(position.x);
        int centerZ = CellCoordinate(position.z);
        int radiusCells = Mathf.Max(1, Mathf.CeilToInt(
            NeighbourQueryDistance / SpatialCellSize));

        for (int dz = -radiusCells; dz <= radiusCells; dz++)
        {
            for (int dx = -radiusCells; dx <= radiusCells; dx++)
            {
                long key = PackCell(centerX + dx, centerZ + dz);
                if (!spatialCells.TryGetValue(key, out List<string> ids) || ids == null)
                    continue;

                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i];
                    if (!string.Equals(id, ownerId, StringComparison.Ordinal))
                        scratchIds.Add(id);
                }
            }
        }
    }

    private void AddToSpatialCell(long cellKey, string ownerId)
    {
        if (!spatialCells.TryGetValue(cellKey, out List<string> ids) || ids == null)
        {
            ids = new List<string>(8);
            spatialCells[cellKey] = ids;
        }
        ids.Add(ownerId);
    }

    private void RemoveFromSpatialCell(long cellKey, string ownerId)
    {
        if (!spatialCells.TryGetValue(cellKey, out List<string> ids) || ids == null)
            return;

        ids.Remove(ownerId);
        if (ids.Count == 0)
            spatialCells.Remove(cellKey);
    }

    private static long BuildCellKey(Vector3 position)
    {
        return PackCell(CellCoordinate(position.x), CellCoordinate(position.z));
    }

    private static int CellCoordinate(float coordinate)
    {
        return Mathf.FloorToInt(coordinate / SpatialCellSize);
    }

    private static long PackCell(int x, int z)
    {
        return ((long)x << 32) ^ (uint)z;
    }
    public static float CalculateEffectivePriority(
        BistroBuilderNavigationLocalMoveInput input)
    {
        return input.externalUrgency +
               Mathf.Max(0f, input.waitingAgeSeconds) * 4f +
               Mathf.Max(0f, input.recoveryDebt) * 10f +
               Mathf.Clamp01(input.commitment) * 20f;
    }

    private EncounterRecord GetOrCreateEncounter(
        BistroBuilderNavigationLocalMoveInput input,
        AgentRecord other,
        float now)
    {
        string key = BuildEncounterKey(input.ownerId, other.ownerId);
        if (encounters.TryGetValue(key, out EncounterRecord existing) &&
            existing != null)
        {
            UpdateEncounterPreference(existing, input, other, now);
            return existing;
        }

        bool inputFirst =
            string.CompareOrdinal(input.ownerId, other.ownerId) <= 0;
        string first = inputFirst ? input.ownerId : other.ownerId;
        string second = inputFirst ? other.ownerId : input.ownerId;
        float myPriority = CalculateEffectivePriority(input);
        float otherPriority = CalculateEffectivePriority(other);

        string preferred;
        if (myPriority > otherPriority + 0.0001f)
            preferred = input.ownerId;
        else if (otherPriority > myPriority + 0.0001f)
            preferred = other.ownerId;
        else
            preferred = string.CompareOrdinal(input.ownerId, other.ownerId) <= 0
                ? input.ownerId
                : other.ownerId;

        var created = new EncounterRecord
        {
            firstOwnerId = first,
            secondOwnerId = second,
            preferredOwnerId = preferred,
            passingSide = 1,
            createdAt = now,
            lastSeenAt = now,
            preferredSince = now,
            lastPriorityCheckAt = now
        };
        encounters[key] = created;
        EncounterCreatedCount++;
        return created;
    }

    private static void UpdateEncounterPreference(
        EncounterRecord encounter,
        BistroBuilderNavigationLocalMoveInput input,
        AgentRecord other,
        float now)
    {
        if (encounter == null || other == null ||
            now - encounter.lastPriorityCheckAt < 0.15f)
            return;

        encounter.lastPriorityCheckAt = now;
        float myPriority = CalculateEffectivePriority(input);
        float otherPriority = CalculateEffectivePriority(other);
        bool inputPreferred = string.Equals(
            encounter.preferredOwnerId, input.ownerId, StringComparison.Ordinal);
        float preferredPriority = inputPreferred ? myPriority : otherPriority;
        float waitingPriority = inputPreferred ? otherPriority : myPriority;
        string waitingOwner = inputPreferred ? other.ownerId : input.ownerId;
        float heldFor = Mathf.Max(0f, now - encounter.preferredSince);
        if (heldFor < EncounterPreferenceMinHoldSeconds)
            return;

        bool priorityOvertake =
            waitingPriority > preferredPriority + EncounterPrioritySwitchMargin;
        bool fairnessTurn = heldFor >= EncounterPreferenceMaxHoldSeconds;
        if (!priorityOvertake && !fairnessTurn)
            return;

        encounter.preferredOwnerId = waitingOwner;
        encounter.preferredSince = now;
    }

    private static bool PredictConflict(
        Vector3 position,
        Vector3 preferredVelocity,
        float radius,
        AgentRecord other,
        out float timeToClosest,
        out float closestSeparation)
    {
        Vector3 relativePosition = Horizontal(other.position - position);
        float currentDistance = relativePosition.magnitude;
        float combinedRadius = radius + other.radius + SafetyMargin;
        if (currentDistance > combinedRadius + 3.25f)
        {
            timeToClosest = float.PositiveInfinity;
            closestSeparation = currentDistance;
            return false;
        }

        Vector3 relativeVelocity =
            Horizontal(preferredVelocity - other.velocity);
        float relativeSpeedSq = relativeVelocity.sqrMagnitude;
        if (relativeSpeedSq <= 0.0001f)
        {
            timeToClosest = 0f;
            closestSeparation = currentDistance;
            return currentDistance < combinedRadius * 1.4f;
        }

        timeToClosest = Mathf.Clamp(
            Vector3.Dot(relativePosition, relativeVelocity) / relativeSpeedSq,
            0f,
            EncounterLookAhead);
        Vector3 futureSeparation =
            relativePosition - relativeVelocity * timeToClosest;
        closestSeparation = futureSeparation.magnitude;
        return closestSeparation < combinedRadius;
    }

    private static float CalculateEffectivePriority(AgentRecord record)
    {
        return record.externalUrgency +
               record.waitingAgeSeconds * 4f +
               record.recoveryDebt * 10f +
               record.commitment * 20f;
    }

    private static int ResolvePassingSide(
        EncounterRecord encounter,
        string ownerId)
    {
        if (encounter == null)
            return 1;

        // Ambos agentes reciben "derecha" respecto a su propio heading.
        // El valor se conserva durante todo el Encounter para romper simetrias.
        return encounter.passingSide == 0 ? 1 : encounter.passingSide;
    }

    private static string BuildEncounterKey(string first, string second)
    {
        first ??= string.Empty;
        second ??= string.Empty;
        return string.CompareOrdinal(first, second) <= 0
            ? first + "|" + second
            : second + "|" + first;
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static Vector3 ClampMagnitude(Vector3 value, float maximum)
    {
        float max = Mathf.Max(0f, maximum);
        return value.sqrMagnitude <= max * max
            ? value
            : value.normalized * max;
    }

    private sealed class AgentRecord
    {
        public string ownerId;
        public BistroBuilderNavigationAgentMask agentMask;
        public Vector3 position;
        public Vector3 velocity;
        public float radius;
      public int externalUrgency;
        public float waitingAgeSeconds;
        public float recoveryDebt;
        public float commitment;
        public float seenAt;
        public long cellKey;
    }

    private sealed class EncounterRecord
    {
        public string firstOwnerId;
        public string secondOwnerId;
        public string preferredOwnerId;
        public int passingSide;
        public float createdAt;
        public float lastSeenAt;
        public float preferredSince;
        public float lastPriorityCheckAt;
    }
}
