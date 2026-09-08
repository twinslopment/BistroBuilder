using System;
using System.Collections.Generic;

/// <summary>
/// Grafo determinista de dependencias de bloqueo. Solo describe quién espera a quién;
/// no concede recursos ni sustituye Claims/Spatial Leases de BBSIS.
/// </summary>
public sealed class BistroBuilderNavigationBlockDependencyGraph
{
    private readonly Dictionary<string, Edge> edges =
        new Dictionary<string, Edge>(StringComparer.Ordinal);
    private readonly List<string> scratchPath = new List<string>(16);
    private readonly Dictionary<string, int> scratchIndex =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<string> scratchIds = new List<string>(32);

    public int EdgeCount => edges.Count;

    public void SetDependency(string ownerId, string blockerId, float now)
    {
        if (string.IsNullOrWhiteSpace(ownerId) ||
            string.IsNullOrWhiteSpace(blockerId) ||
            string.Equals(ownerId, blockerId, StringComparison.Ordinal))
        {
            ClearDependency(ownerId);
            return;
        }

        edges[ownerId] = new Edge
        {
            ownerId = ownerId,
            blockerId = blockerId,
            lastSeenAt = now
        };
    }

    public void ClearDependency(string ownerId)
    {
        if (!string.IsNullOrWhiteSpace(ownerId))
            edges.Remove(ownerId);
    }

    public void RemoveOwner(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        edges.Remove(ownerId);
        scratchIds.Clear();
        foreach (KeyValuePair<string, Edge> pair in edges)
            if (pair.Value == null ||
                string.Equals(pair.Value.blockerId, ownerId, StringComparison.Ordinal))
                scratchIds.Add(pair.Key);
        for (int i = 0; i < scratchIds.Count; i++)
            edges.Remove(scratchIds[i]);
    }

    public bool TryFindCycle(
        string startOwnerId,
        List<string> cycle,
        out string signature)
    {
        signature = string.Empty;
        if (cycle == null) return false;
        cycle.Clear();
        if (string.IsNullOrWhiteSpace(startOwnerId)) return false;

        scratchPath.Clear();
        scratchIndex.Clear();
        string cursor = startOwnerId;

        while (!string.IsNullOrWhiteSpace(cursor))
        {
            if (scratchIndex.TryGetValue(cursor, out int repeatedAt))
            {
                for (int i = repeatedAt; i < scratchPath.Count; i++)
                    cycle.Add(scratchPath[i]);
                if (cycle.Count < 2)
                {
                    cycle.Clear();
                    return false;
                }
                Canonicalize(cycle);
                signature = string.Join(">", cycle);
                return true;
            }

            scratchIndex[cursor] = scratchPath.Count;
            scratchPath.Add(cursor);
            if (!edges.TryGetValue(cursor, out Edge edge) || edge == null)
                break;
            if (!edges.ContainsKey(edge.blockerId))
                break;
            cursor = edge.blockerId;
        }

        return false;
    }

    public string SelectRecoveryCandidate(
        IReadOnlyList<string> cycle,
        Func<string, float> effectivePriorityResolver)
    {
        if (cycle == null || cycle.Count == 0) return string.Empty;
        string selected = string.Empty;
        float selectedPriority = float.PositiveInfinity;

        for (int i = 0; i < cycle.Count; i++)
        {
            string ownerId = cycle[i];
            float priority = effectivePriorityResolver != null
                ? effectivePriorityResolver(ownerId)
                : 0f;
            if (string.IsNullOrEmpty(selected) ||
                priority < selectedPriority - 0.0001f ||
                (Math.Abs(priority - selectedPriority) <= 0.0001f &&
                 string.CompareOrdinal(ownerId, selected) > 0))
            {
                selected = ownerId;
                selectedPriority = priority;
            }
        }

        return selected;
    }

    public float ResolveInheritedPriority(
        IReadOnlyList<string> cycle,
        Func<string, float> effectivePriorityResolver)
    {
        float inherited = 0f;
        if (cycle == null || effectivePriorityResolver == null) return inherited;
        for (int i = 0; i < cycle.Count; i++)
            inherited = Math.Max(inherited, effectivePriorityResolver(cycle[i]));
        return inherited;
    }

    public void Cleanup(float now, float staleSeconds = 1.5f)
    {
        scratchIds.Clear();
        foreach (KeyValuePair<string, Edge> pair in edges)
            if (pair.Value == null || now - pair.Value.lastSeenAt > staleSeconds)
                scratchIds.Add(pair.Key);
        for (int i = 0; i < scratchIds.Count; i++)
            edges.Remove(scratchIds[i]);
    }

    private static void Canonicalize(List<string> cycle)
    {
        if (cycle == null || cycle.Count <= 1) return;
        int best = 0;
        for (int i = 1; i < cycle.Count; i++)
            if (string.CompareOrdinal(cycle[i], cycle[best]) < 0)
                best = i;
        if (best == 0) return;

        string[] copy = cycle.ToArray();
        for (int i = 0; i < copy.Length; i++)
            cycle[i] = copy[(best + i) % copy.Length];
    }

    private sealed class Edge
    {
        public string ownerId;
        public string blockerId;
        public float lastSeenAt;
    }
}