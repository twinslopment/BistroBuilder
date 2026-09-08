using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Campo de congestión de baja frecuencia. Resume tráfico observado por Navigation
/// sin redefinir Flow Quality ni accesibilidad espacial de BBSIS.
/// </summary>
public sealed class BistroBuilderNavigationTrafficHeatField
{
    private const float CellSize = 1.25f;
    private const float SampleStaleSeconds = 1.1f;
    private const float Smoothing = 0.42f;
    private const float SignificantChange = 0.18f;

    private readonly Dictionary<string, AgentSample> agents =
        new Dictionary<string, AgentSample>(StringComparer.Ordinal);
    private readonly Dictionary<long, CellState> cells =
        new Dictionary<long, CellState>();
    private readonly Dictionary<long, CellAccumulator> current =
        new Dictionary<long, CellAccumulator>();
    private readonly List<string> scratchAgentIds = new List<string>(64);
    private readonly List<long> scratchCellIds = new List<long>(64);

    public int ActiveCellCount => cells.Count;
    public int ActiveAgentCount => agents.Count;
    public int Epoch { get; private set; } = 1;

    public void UpsertAgent(
        string ownerId,
        Vector3 position,
        Vector3 velocity,
        float radius,
        float now)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        velocity.y = 0f;
        agents[ownerId] = new AgentSample
        {
            position = position,
            speed = velocity.magnitude,
            radius = Mathf.Max(0.05f, radius),
            seenAt = now
        };
    }

    public void RemoveAgent(string ownerId)
    {
        if (!string.IsNullOrWhiteSpace(ownerId))
            agents.Remove(ownerId);
    }

    public bool Rebuild(float now)
    {
        CleanupAgents(now);
        current.Clear();
        foreach (AgentSample sample in agents.Values)
        {
            if (sample == null) continue;
            long key = Key(sample.position, out _, out _);
            if (!current.TryGetValue(key, out CellAccumulator accumulator) ||
                accumulator == null)
            {
                accumulator = new CellAccumulator();
                current[key] = accumulator;
            }
            accumulator.count++;
            accumulator.speedSum += sample.speed;
        }

        bool changed = false;
        scratchCellIds.Clear();
        foreach (long key in cells.Keys)
            scratchCellIds.Add(key);
        foreach (long key in current.Keys)
            if (!cells.ContainsKey(key)) scratchCellIds.Add(key);
        scratchCellIds.Sort();

        for (int i = 0; i < scratchCellIds.Count; i++)
        {
            long key = scratchCellIds[i];
            current.TryGetValue(key, out CellAccumulator accumulator);
            float occupancy = accumulator != null ? accumulator.count : 0f;
            float meanSpeed = accumulator != null && accumulator.count > 0
                ? accumulator.speedSum / accumulator.count
                : 2f;

            if (!cells.TryGetValue(key, out CellState state) || state == null)
            {
                state = new CellState();
                cells[key] = state;
            }

            float oldPenalty = state.Penalty;
            state.smoothedOccupancy = Mathf.Lerp(
                state.smoothedOccupancy,
                occupancy,
                Smoothing);
            state.smoothedMeanSpeed = Mathf.Lerp(
                state.smoothedMeanSpeed <= 0f ? meanSpeed : state.smoothedMeanSpeed,
                meanSpeed,
                Smoothing);
            state.lastUpdatedAt = now;
            if (Mathf.Abs(state.Penalty - oldPenalty) >= SignificantChange)
                changed = true;


        }

        scratchCellIds.Clear();
        foreach (KeyValuePair<long, CellState> pair in cells)
            if (pair.Value == null || pair.Value.smoothedOccupancy < 0.03f)
                scratchCellIds.Add(pair.Key);
        for (int i = 0; i < scratchCellIds.Count; i++)
            cells.Remove(scratchCellIds[i]);

        if (changed)
            Epoch = Epoch == int.MaxValue ? 1 : Epoch + 1;
        return changed;
    }

    public float QueryPenalty(Vector3 point)
    {
        Key(point, out int centerX, out int centerZ);
        float total = 0f;
        for (int z = -1; z <= 1; z++)
        {
            for (int x = -1; x <= 1; x++)
            {
                long key = Key(centerX + x, centerZ + z);
                if (!cells.TryGetValue(key, out CellState state) || state == null)
                    continue;
                float distanceWeight = x == 0 && z == 0
                    ? 1f
                    : (x == 0 || z == 0 ? 0.42f : 0.2f);
                total += state.Penalty * distanceWeight;
            }
        }
        return total;
    }

    public int WriteSnapshots(List<BistroBuilderNavigationTrafficCellSnapshot> results)
    {
        if (results == null) return 0;
        int before = results.Count;
        scratchCellIds.Clear();
        foreach (long key in cells.Keys) scratchCellIds.Add(key);
        scratchCellIds.Sort();
        for (int i = 0; i < scratchCellIds.Count; i++)
        {
            long key = scratchCellIds[i];
            CellState state = cells[key];
            DecodeKey(key, out int x, out int z);
            results.Add(new BistroBuilderNavigationTrafficCellSnapshot
            {
                x = x,
                z = z,
                center = new Vector3(
                    (x + 0.5f) * CellSize,
                    0f,
                    (z + 0.5f) * CellSize),
                smoothedOccupancy = state.smoothedOccupancy,
                smoothedMeanSpeed = state.smoothedMeanSpeed,
                penalty = state.Penalty
            });
        }
        return results.Count - before;
    }

    private void CleanupAgents(float now)
    {
        scratchAgentIds.Clear();
        foreach (KeyValuePair<string, AgentSample> pair in agents)
            if (pair.Value == null || now - pair.Value.seenAt > SampleStaleSeconds)
                scratchAgentIds.Add(pair.Key);
        for (int i = 0; i < scratchAgentIds.Count; i++)
            agents.Remove(scratchAgentIds[i]);
    }

    private static long Key(Vector3 position, out int x, out int z)
    {
        x = Mathf.FloorToInt(position.x / CellSize);
        z = Mathf.FloorToInt(position.z / CellSize);
        return Key(x, z);
    }

    private static long Key(int x, int z)
    {
        return ((long)x << 32) ^ (uint)z;
    }

    private static void DecodeKey(long key, out int x, out int z)
    {
        x = (int)(key >> 32);
        z = (int)(key & 0xffffffff);
    }

    private sealed class AgentSample
    {
        public Vector3 position;
        public float speed;
        public float radius;
        public float seenAt;
    }

    private sealed class CellAccumulator
    {
        public int count;
        public float speedSum;
    }

    private sealed class CellState
    {
        public float smoothedOccupancy;
        public float smoothedMeanSpeed = 2f;
        public float lastUpdatedAt;

        public float Penalty
        {
            get
            {
                float density = Mathf.Max(0f, smoothedOccupancy - 0.25f);
                float slow = Mathf.Clamp01(1f - smoothedMeanSpeed / 2f);
                return density * (0.65f + slow * 0.75f);
            }
        }
    }
}