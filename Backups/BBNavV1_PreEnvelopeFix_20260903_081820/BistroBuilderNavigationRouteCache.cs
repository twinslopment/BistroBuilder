using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cache estructural de rutas. TrafficEpoch no forma parte de la clave:
/// la congestión cambia el coste, no la validez topológica.
/// </summary>
public sealed class BistroBuilderNavigationRouteCache
{
    private const float PositionQuantum = 0.25f;
    private const float RadiusQuantum = 0.1f;
    private const int MaximumEntries = 512;

    private readonly Dictionary<RouteKey, CacheEntry> positive =
        new Dictionary<RouteKey, CacheEntry>();
    private readonly Dictionary<RouteKey, NegativeEntry> negative =
        new Dictionary<RouteKey, NegativeEntry>();
    private long sequence;

    public long HitCount { get; private set; }
    public long MissCount { get; private set; }
    public long NegativeHitCount { get; private set; }
    public int PositiveCount => positive.Count;
    public int NegativeCount => negative.Count;

    public bool TryGet(
        BistroBuilderNavigationTopologySnapshot topology,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float radius,
        out BistroBuilderNavigationRoute route)
    {
        RouteKey key = BuildKey(topology, agent, origin, destination, radius);
        if (positive.TryGetValue(key, out CacheEntry entry) &&
            entry != null && entry.route != null)
        {
            entry.lastUsedSequence = ++sequence;
            HitCount++;
            route = entry.route.DeepClone();
            return true;
        }

        MissCount++;
        route = null;
        return false;
    }

    public bool IsKnownUnavailable(
        BistroBuilderNavigationTopologySnapshot topology,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float radius)
    {
        RouteKey key = BuildKey(topology, agent, origin, destination, radius);
        if (!negative.TryGetValue(key, out NegativeEntry entry) || entry == null)
            return false;
        entry.lastUsedSequence = ++sequence;
        NegativeHitCount++;
        return true;
    }

    public void Store(
        BistroBuilderNavigationTopologySnapshot topology,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float radius,
        BistroBuilderNavigationRoute route)
    {
        if (route == null || !route.isComplete) return;
        RouteKey key = BuildKey(topology, agent, origin, destination, radius);
        negative.Remove(key);
        positive[key] = new CacheEntry
        {
            route = route.DeepClone(),
            lastUsedSequence = ++sequence
        };
        TrimIfNeeded();
    }

    public void StoreUnavailable(
        BistroBuilderNavigationTopologySnapshot topology,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float radius)
    {
        RouteKey key = BuildKey(topology, agent, origin, destination, radius);
        positive.Remove(key);
        negative[key] = new NegativeEntry
        {
            lastUsedSequence = ++sequence
        };
        TrimIfNeeded();
    }

    public void InvalidateStructuralState(
        int spatialRevision,
        int navigationRevision)
    {
        RemoveMismatched(positive, spatialRevision, navigationRevision);
        RemoveMismatched(negative, spatialRevision, navigationRevision);
    }

    public void Clear()
    {
        positive.Clear();
        negative.Clear();
    }

    private void TrimIfNeeded()
    {
        while (positive.Count + negative.Count > MaximumEntries)
        {
            bool removePositive = false;
            RouteKey selected = default;
            long oldest = long.MaxValue;

            foreach (KeyValuePair<RouteKey, CacheEntry> pair in positive)
            {
                if (pair.Value != null && pair.Value.lastUsedSequence < oldest)
                {
                    oldest = pair.Value.lastUsedSequence;
                    selected = pair.Key;
                    removePositive = true;
                }
            }
            foreach (KeyValuePair<RouteKey, NegativeEntry> pair in negative)
            {
                if (pair.Value != null && pair.Value.lastUsedSequence < oldest)
                {
                    oldest = pair.Value.lastUsedSequence;
                    selected = pair.Key;
                    removePositive = false;
                }
            }

            if (oldest == long.MaxValue) break;
            if (removePositive) positive.Remove(selected);
            else negative.Remove(selected);
        }
    }

    private static void RemoveMismatched<T>(
        Dictionary<RouteKey, T> source,
        int spatialRevision,
        int navigationRevision)
    {
        if (source.Count == 0) return;
        var stale = new List<RouteKey>();
        foreach (RouteKey key in source.Keys)
            if (key.spatialRevision != spatialRevision ||
                key.navigationRevision != navigationRevision)
                stale.Add(key);
        for (int i = 0; i < stale.Count; i++)
            source.Remove(stale[i]);
    }

    private static RouteKey BuildKey(
        BistroBuilderNavigationTopologySnapshot topology,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float radius)
    {
        return new RouteKey
        {
            spatialRevision = topology.spatialRevision,
            navigationRevision = topology.navigationRevision,
            agent = agent,
            ox = Quantize(origin.x, PositionQuantum),
            oz = Quantize(origin.z, PositionQuantum),
            dx = Quantize(destination.x, PositionQuantum),
            dz = Quantize(destination.z, PositionQuantum),
            radius = Quantize(Mathf.Max(0.05f, radius), RadiusQuantum)
        };
    }

    private static int Quantize(float value, float quantum)
    {
        return Mathf.RoundToInt(value / quantum);
    }

    private struct RouteKey : IEquatable<RouteKey>
    {
        public int spatialRevision;
        public int navigationRevision;
        public BistroBuilderNavigationAgentMask agent;
        public int ox;
        public int oz;
        public int dx;
        public int dz;
        public int radius;

        public bool Equals(RouteKey other)
        {
            return spatialRevision == other.spatialRevision &&
                   navigationRevision == other.navigationRevision &&
                   agent == other.agent &&
                   ox == other.ox && oz == other.oz &&
                   dx == other.dx && dz == other.dz &&
                   radius == other.radius;
        }

        public override bool Equals(object obj)
        {
            return obj is RouteKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = spatialRevision;
                hash = hash * 397 ^ navigationRevision;
                hash = hash * 397 ^ (int)agent;
                hash = hash * 397 ^ ox;
                hash = hash * 397 ^ oz;
                hash = hash * 397 ^ dx;
                hash = hash * 397 ^ dz;
                hash = hash * 397 ^ radius;
                return hash;
            }
        }
    }

    private sealed class CacheEntry
    {
        public BistroBuilderNavigationRoute route;
        public long lastUsedSequence;
    }

    private sealed class NegativeEntry
    {
        public long lastUsedSequence;
    }
}