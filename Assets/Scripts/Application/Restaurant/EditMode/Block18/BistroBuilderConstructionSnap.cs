using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.ConstructionAuthoring
{
    public enum SnapKind { None, Endpoint, Intersection, Wall, Horizontal, Vertical, Grid }
    public sealed class SnapSettings
    {
        public float CaptureDistance = 0.25f;
        public float ReleaseDistance = 0.4f;
        public float SwitchAdvantage = 0.2f;
        public bool GridEnabled = true;
        public float GridSize = 0.25f;
        public Vector2 GridOrigin;
        public bool AxisEnabled = true;
        public bool IsValid => ConstructionGeometry.Finite(CaptureDistance) && CaptureDistance > 0f &&
            ConstructionGeometry.Finite(ReleaseDistance) && ReleaseDistance >= CaptureDistance &&
            ConstructionGeometry.Finite(SwitchAdvantage) && SwitchAdvantage >= 0f &&
            ConstructionGeometry.Finite(GridOrigin) &&
            (!GridEnabled || ConstructionGeometry.Finite(GridSize) && GridSize > 0f);
    }

    public readonly struct SnapResult
    {
        public readonly SnapKind Kind;
        public readonly BistroBuilderEditId EntityId;
        public readonly Vector2 Point;
        public readonly float Distance;
        internal readonly int Feature;
        internal readonly float Score;
        public bool IsSnapped => Kind != SnapKind.None;
        internal SnapResult(SnapKind kind, BistroBuilderEditId id, int feature, Vector2 point, float distance, float score)
        { Kind = kind; EntityId = id; Feature = feature; Point = point; Distance = distance; Score = score; }
        internal bool SameTarget(SnapResult other) => Kind == other.Kind && EntityId == other.EntityId && Feature == other.Feature;
    }

    /// <summary>Pure candidate ranking adapted to Block 18. Reuses its buffer and
    /// topology projection; no document cloning, rebuilding, or command execution.</summary>
    public sealed class ArchitectureSnapService
    {
        private readonly List<SnapResult> candidates = new List<SnapResult>(128);
        private static readonly Comparison<SnapResult> Ranking = Compare;
        private SnapResult captured;
        private string cacheSession, planeContext;
        private long cacheRevision = -1;
        private BistroBuilderEditId excludedContext;
        private Vector2? anchorContext, excludedPointContext;
        public IReadOnlyList<SnapResult> Candidates => candidates;
        public void Reset() { captured = default; candidates.Clear(); cacheRevision = -1; }

        public SnapResult Resolve(ArchitectureQueryCache cache, Vector2 cursor, SnapSettings settings,
            Vector2? anchor = null, string plane = "default", BistroBuilderEditId excludedWall = default,
            Vector2? excludedPoint = null)
        {
            if (cache == null || settings == null || !settings.IsValid || !ConstructionGeometry.Finite(cursor) ||
                anchor.HasValue && !ConstructionGeometry.Finite(anchor.Value) ||
                excludedPoint.HasValue && !ConstructionGeometry.Finite(excludedPoint.Value))
            { Reset(); return new SnapResult(SnapKind.None, default, 0, cursor, 0f, 0f); }
            if (cacheSession != cache.SessionId || cacheRevision != cache.Revision || planeContext != plane ||
                excludedContext != excludedWall || anchorContext != anchor || excludedPointContext != excludedPoint)
                captured = default;
            cacheSession = cache.SessionId; cacheRevision = cache.Revision; planeContext = plane;
            excludedContext = excludedWall; anchorContext = anchor; excludedPointContext = excludedPoint;
            candidates.Clear();
            float max = settings.ReleaseDistance;

            foreach (var wall in cache.Walls)
            {
                if (wall.buildPlaneId != plane || wall.wallId == excludedWall) continue;
                if (!NearExcluded(wall.axisStart, excludedPoint))
                    Add(SnapKind.Endpoint, wall.wallId, 0, wall.axisStart, cursor, max, settings);
                if (!NearExcluded(wall.axisEnd, excludedPoint))
                    Add(SnapKind.Endpoint, wall.wallId, 1, wall.axisEnd, cursor, max, settings);
                var projected = ConstructionGeometry.Project(cursor, wall.axisStart, wall.axisEnd, out _);
                Add(SnapKind.Wall, wall.wallId, 0, projected, cursor, max, settings);
            }
            if (cache.Topology != null)
                foreach (var vertex in cache.Topology.vertices)
                {
                    if (vertex.incidentWalls.Count < 2 || NearExcluded(vertex.position, excludedPoint)) continue;
                    int matching = 0;
                    bool interior = false;
                    foreach (var id in vertex.incidentWalls)
                    {
                        var wall = cache.Wall(id);
                        if (wall == null || wall.buildPlaneId != plane || id == excludedWall) continue;
                        matching++;
                        if (!ConstructionGeometry.Same(vertex.position, wall.axisStart) &&
                            !ConstructionGeometry.Same(vertex.position, wall.axisEnd)) interior = true;
                    }
                    if (matching >= 2 && interior)
                        Add(SnapKind.Intersection, default, vertex.vertexId, vertex.position, cursor, max, settings);
                }
            if (anchor.HasValue && settings.AxisEnabled)
            {
                Add(SnapKind.Horizontal, default, 0, new Vector2(cursor.x, anchor.Value.y), cursor, max, settings);
                Add(SnapKind.Vertical, default, 0, new Vector2(anchor.Value.x, cursor.y), cursor, max, settings);
            }
            if (settings.GridEnabled)
            {
                Vector2 relative = (cursor - settings.GridOrigin) / settings.GridSize;
                Vector2 grid = settings.GridOrigin + new Vector2((float)Math.Round(relative.x, MidpointRounding.AwayFromZero),
                    (float)Math.Round(relative.y, MidpointRounding.AwayFromZero)) * settings.GridSize;
                Add(SnapKind.Grid, default, 0, grid, cursor, max, settings);
            }
            candidates.Sort(Ranking);
            SnapResult best = default, retained = default;
            foreach (var candidate in candidates)
            {
                if (!best.IsSnapped && candidate.Distance <= settings.CaptureDistance) best = candidate;
                // Grid follows the nearest cell; it is not a persistent geometric target.
                if (captured.IsSnapped && captured.Kind != SnapKind.Grid && candidate.SameTarget(captured)) retained = candidate;
            }
            if (retained.IsSnapped && (!best.IsSnapped || best.Score + settings.SwitchAdvantage >= retained.Score))
                best = retained;
            captured = best;
            return best.IsSnapped ? best : new SnapResult(SnapKind.None, default, 0, cursor, 0f, 0f);
        }

        private static bool NearExcluded(Vector2 point, Vector2? excluded) =>
            excluded.HasValue && ConstructionGeometry.Same(point, excluded.Value);

        private void Add(SnapKind kind, BistroBuilderEditId id, int feature, Vector2 point, Vector2 cursor, float max, SnapSettings settings)
        {
            float distance = Vector2.Distance(point, cursor);
            if (distance > max) return;
            float bias = kind == SnapKind.Endpoint || kind == SnapKind.Intersection ? 0f :
                kind == SnapKind.Wall ? 0.25f : kind == SnapKind.Grid ? 0.65f : 0.4f;
            candidates.Add(new SnapResult(kind, id, feature, point, distance, distance / settings.CaptureDistance + bias));
        }
        private static int Compare(SnapResult a, SnapResult b)
        {
            int c = a.Score.CompareTo(b.Score); if (c != 0) return c;
            c = a.Kind.CompareTo(b.Kind); if (c != 0) return c;
            c = a.EntityId.CompareTo(b.EntityId); return c != 0 ? c : a.Feature.CompareTo(b.Feature);
        }
    }
}
