using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Transient memo of exact static traversability queries during ONE
/// operational-dock search. The expensive area/collider/OBB checks are
/// independent of each candidate endpoint whenever a point is outside its
/// endpoint exemption. Cached values never survive the synchronous query.
/// </summary>
public sealed partial class BistroBuilderNavigationService
{
    private readonly Dictionary<DockStructuralKey, bool> dockStructuralCache =
        new Dictionary<DockStructuralKey, bool>(8192);
    private bool dockStructuralCacheActive;
    public long B11DockCacheHits { get; private set; }
    public long B11DockCacheMisses { get; private set; }
#if UNITY_EDITOR
    // Acceptance-test-only control: compare original 80-candidate search
    // with the optimized ordering and pruning in the same real scene.
    public bool B11UseLegacyDockForQA { get; set; }
#endif

    private readonly struct DockStructuralKey : IEquatable<DockStructuralKey>
    {
        private readonly Vector3 position;
        private readonly float radius;
        private readonly BistroBuilderNavigationAgentMask agent;

        public DockStructuralKey(
            Vector3 point, float clearanceRadius,
            BistroBuilderNavigationAgentMask requestedAgent)
        {
            position = point;
            radius = clearanceRadius;
            agent = requestedAgent;
        }

        public bool Equals(DockStructuralKey other)
            => position.Equals(other.position) &&
               radius.Equals(other.radius) && agent == other.agent;

        public override bool Equals(object obj)
            => obj is DockStructuralKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = position.GetHashCode();
                hash = hash * 31 + radius.GetHashCode();
                return hash * 31 + (int)agent;
            }
        }
    }

    private void BeginDockStructuralCache()
    {
        // All 80 candidate trials run synchronously, with no scene updates
        // between them. The scope is deliberately not shared between routes.
        dockStructuralCache.Clear();
        dockStructuralCacheActive = true;
    }

    private void EndDockStructuralCache()
    {
        dockStructuralCacheActive = false;
        dockStructuralCache.Clear();
    }

    private bool TryGetDockStructuralCached(
        Vector3 point, float radius, BistroBuilderNavigationAgentMask agent,
        out bool allowed)
    {
        if (dockStructuralCacheActive &&
            dockStructuralCache.TryGetValue(
                new DockStructuralKey(point, radius, agent), out allowed))
        {
            B11DockCacheHits++;
            return true;
        }
        allowed = false;
        return false;
    }

    private void RememberDockStructural(
        Vector3 point, float radius, BistroBuilderNavigationAgentMask agent,
        bool allowed)
    {
        if (!dockStructuralCacheActive) return;
        B11DockCacheMisses++;
        // Bounded memory; conservatively re-evaluate rather than evicting
        // results or retaining them beyond the active query.
        if (dockStructuralCache.Count < 65536)
            dockStructuralCache[new DockStructuralKey(point, radius, agent)] = allowed;
    }
}
