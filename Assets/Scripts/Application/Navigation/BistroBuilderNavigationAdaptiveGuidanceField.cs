using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guidance espacio-direccional de baja frecuencia. Aprende velocidad, presión
/// contraria e incidentes sin convertirse en autoridad espacial ni de reservas.
/// </summary>
public sealed class BistroBuilderNavigationAdaptiveGuidanceField
{
    private const float CellSize = 1.5f;
    private const float StaleSeconds = 2.0f;
    private const float SampleSmoothing = 0.18f;
    private const float IncidentHalfLife = 4.0f;
    private readonly Dictionary<long, CellState> cells = new Dictionary<long, CellState>(128);
    private readonly Dictionary<string, AgentSample> agents = new Dictionary<string, AgentSample>(StringComparer.Ordinal);
    private readonly List<string> scratchIds = new List<string>(128);

    public int ActiveCellCount => cells.Count;
    public long IncidentCount { get; private set; }

    public void Observe(string ownerId, Vector3 position, Vector3 velocity,
        Vector3 desiredDirection, bool waiting, float now)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        velocity.y = 0f;
        desiredDirection.y = 0f;
        agents[ownerId] = new AgentSample { position = position, velocity = velocity,
            desired = desiredDirection, waiting = waiting, seenAt = now };
    }
    public bool Rebuild(float now)
    {
        scratchIds.Clear();
        foreach (KeyValuePair<string, AgentSample> pair in agents)
            if (now - pair.Value.seenAt > StaleSeconds) scratchIds.Add(pair.Key);
        for (int i = 0; i < scratchIds.Count; i++) agents.Remove(scratchIds[i]);

        foreach (AgentSample sample in agents.Values)
        {
            long key = Key(sample.position);
            if (!cells.TryGetValue(key, out CellState state))
            {
                state = new CellState { lastUpdatedAt = now };
                cells[key] = state;
            }
            float speed = sample.velocity.magnitude;
            Vector3 desired = sample.desired.sqrMagnitude > 0.0001f
                ? sample.desired.normalized : Vector3.zero;
            Vector3 actual = speed > 0.02f ? sample.velocity / speed : Vector3.zero;
            float alignment = desired.sqrMagnitude > 0f && actual.sqrMagnitude > 0f
                ? Mathf.Clamp(Vector3.Dot(desired, actual), -1f, 1f) : 0f;
            float opposing = desired.sqrMagnitude > 0f && actual.sqrMagnitude > 0f
                ? Mathf.Clamp01(-alignment) : 0f;
            state.meanSpeed = Mathf.Lerp(state.meanSpeed <= 0f ? speed : state.meanSpeed,
                speed, SampleSmoothing);
            state.waitPressure = Mathf.Lerp(state.waitPressure, sample.waiting ? 1f : 0f, SampleSmoothing);
            state.opposingPressure = Mathf.Lerp(state.opposingPressure, opposing, SampleSmoothing);
            state.samplePressure = Mathf.Lerp(state.samplePressure, 1f, SampleSmoothing);
            state.lastUpdatedAt = now;
        }

        bool changed = false;
        foreach (CellState state in cells.Values)
        {
            float old = state.cachedPenalty;
            state.cachedPenalty = ComputePenalty(state, now);
            if (Mathf.Abs(old - state.cachedPenalty) > 0.12f) changed = true;
        }
        return changed;
    }
    public void ReportIncident(Vector3 position, float severity, float now)
    {
        long key = Key(position);
        if (!cells.TryGetValue(key, out CellState state))
        {
            state = new CellState { lastUpdatedAt = now };
            cells[key] = state;
        }
        state.incidentPressure = Mathf.Clamp(state.incidentPressure + Mathf.Max(0f, severity), 0f, 8f);
        state.lastIncidentAt = now;
        IncidentCount++;
    }

    public float QueryPenalty(Vector3 point, Vector3 desiredDirection, float now)
    {
        long key = Key(point);
        if (!cells.TryGetValue(key, out CellState state)) return 0f;
        float penalty = ComputePenalty(state, now);
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude > 0.0001f && state.dominantDirection.sqrMagnitude > 0.0001f)
        {
            float opposition = Mathf.Clamp01(-Vector3.Dot(desiredDirection.normalized, state.dominantDirection.normalized));
            penalty += opposition * state.directionConfidence * 1.2f;
        }
        return penalty;
    }

    public void ObserveDirection(Vector3 point, Vector3 velocity)
    {
        velocity.y = 0f;
        if (velocity.sqrMagnitude <= 0.0025f) return;
        long key = Key(point);
        if (!cells.TryGetValue(key, out CellState state))
        {
            state = new CellState();
            cells[key] = state;
        }
        Vector3 dir = velocity.normalized;
        state.dominantDirection = Vector3.Lerp(state.dominantDirection, dir, 0.12f);
        state.directionConfidence = Mathf.Clamp01(state.directionConfidence + 0.05f);
    }
    private static float ComputePenalty(CellState state, float now)
    {
        if (state == null) return 0f;
        float age = Mathf.Max(0f, now - state.lastIncidentAt);
        float incidentDecay = Mathf.Pow(0.5f, age / IncidentHalfLife);
        float incident = state.incidentPressure * incidentDecay;
        float slow = Mathf.Clamp01(1f - state.meanSpeed / 1.8f);
        return state.samplePressure * (slow * 0.55f + state.waitPressure * 0.9f +
               state.opposingPressure * 1.15f) + incident * 0.65f;
    }

    private static long Key(Vector3 point)
    {
        int x = Mathf.FloorToInt(point.x / CellSize);
        int z = Mathf.FloorToInt(point.z / CellSize);
        return ((long)x << 32) ^ (uint)z;
    }

    private struct AgentSample
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 desired;
        public bool waiting;
        public float seenAt;
    }

    private sealed class CellState
    {
        public float meanSpeed;
        public float waitPressure;
        public float opposingPressure;
        public float samplePressure;
        public float incidentPressure;
        public float lastIncidentAt;
        public float lastUpdatedAt;
        public float cachedPenalty;
        public Vector3 dominantDirection;
        public float directionConfidence;
    }
}
