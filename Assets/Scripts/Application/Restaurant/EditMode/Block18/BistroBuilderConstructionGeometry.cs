using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.ConstructionAuthoring
{
    public readonly struct WallPose
    {
        public readonly BistroBuilderEditId WallId;
        public readonly Vector2 Start, End;
        public WallPose(BistroBuilderEditId id, Vector2 start, Vector2 end) { WallId = id; Start = start; End = end; }
        public float Length => Vector2.Distance(Start, End);
    }
    public readonly struct ConstructionDimensions
    {
        public readonly float Length, AngleDegrees, Width, Depth, Area, Perimeter;
        public ConstructionDimensions(Vector2 start, Vector2 end, bool rectangle)
        {
            Vector2 d = end - start;
            Length = d.magnitude;
            AngleDegrees = (float)(Math.Atan2(d.y, d.x) * 180d / Math.PI);
            Width = Math.Abs(d.x); Depth = Math.Abs(d.y);
            Area = rectangle ? Width * Depth : 0f;
            Perimeter = rectangle ? 2f * (Width + Depth) : Length;
        }
    }

    public static class ConstructionGeometry
    {
        private static readonly BistroBuilderArchitectureGeometryPolicy Policy = BistroBuilderArchitectureGeometryPolicy.Default;
        public static float Tolerance => Policy.pointTolerance;
        public static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        public static bool Finite(Vector2 p) => Finite(p.x) && Finite(p.y);
        public static bool Same(Vector2 a, Vector2 b) => (a - b).sqrMagnitude <= Tolerance * Tolerance;
        public static Vector2 Project(Vector2 point, Vector2 start, Vector2 end, out float t)
        {
            Vector2 axis = end - start;
            t = axis.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, axis) / axis.sqrMagnitude) : 0f;
            return start + axis * t;
        }
        public static bool TryWall(Vector2 start, Vector2 end, out WallPose preview, out string error)
        {
            preview = new WallPose(default, start, end); error = string.Empty;
            if (!Finite(start) || !Finite(end) || preview.Length < Policy.minimumWallLength)
            { error = "WALL_TOO_SHORT_OR_NONFINITE"; return false; }
            return true;
        }
        public static bool TryRectangle(Vector2 first, Vector2 opposite, float minimumSide, List<WallPose> output, out string error)
        {
            output.Clear(); error = string.Empty;
            if (!Finite(first) || !Finite(opposite) || !Finite(minimumSide) || minimumSide < 0.05f)
            { error = "RECTANGLE_INVALID_INPUT"; return false; }
            Vector2 min = Vector2.Min(first, opposite), max = Vector2.Max(first, opposite);
            if (max.x - min.x < minimumSide || max.y - min.y < minimumSide)
            { error = "RECTANGLE_TOO_SMALL"; return false; }
            Vector2 b = new Vector2(max.x, min.y), d = new Vector2(min.x, max.y);
            output.Add(new WallPose(default, min, b)); output.Add(new WallPose(default, b, max));
            output.Add(new WallPose(default, max, d)); output.Add(new WallPose(default, d, min)); return true;
        }
        public static Vector2 PerpendicularDisplacement(Vector2 start, Vector2 end, Vector2 pointerDelta)
        {
            Vector2 axis = (end - start).normalized;
            Vector2 normal = new Vector2(-axis.y, axis.x);
            return normal * Vector2.Dot(pointerDelta, normal);
        }

        // A straight host cannot bend at a derived interior T/X node. Motion along
        // its span is supported; off-host motion is rejected instead of silently
        // splitting WallIds, detaching neighbours, or translating the entire room.
        public static bool TryMoveJunction(ArchitectureQueryCache cache, Vector2 junction, Vector2 target,
            string plane, List<WallPose> output, out string error)
        {
            output.Clear(); error = string.Empty;
            if (!Finite(junction) || !Finite(target)) { error = "JUNCTION_NONFINITE"; return false; }
            int incident = 0;
            foreach (var wall in cache.Walls)
            {
                if (wall.buildPlaneId != plane || !Same(Project(junction, wall.axisStart, wall.axisEnd, out _), junction)) continue;
                incident++;
                bool start = Same(junction, wall.axisStart), end = Same(junction, wall.axisEnd);
                if (!start && !end)
                {
                    if (!Same(Project(target, wall.axisStart, wall.axisEnd, out _), target))
                    { output.Clear(); error = "INTERIOR_JUNCTION_MUST_STAY_ON_STRAIGHT_HOST"; return false; }
                    continue;
                }
                var pose = new WallPose(wall.wallId, start ? target : wall.axisStart, end ? target : wall.axisEnd);
                if (pose.Length < 0.05f) { output.Clear(); error = "CONNECTED_WALL_TOO_SHORT"; return false; }
                output.Add(pose);
            }
            if (incident == 0 || output.Count == 0) { error = "NO_MOVABLE_JUNCTION"; return false; }
            return true;
        }

        public static bool TryMoveWall(ArchitectureQueryCache cache, BistroBuilderEditId wallId, Vector2 pointerDelta,
            List<WallPose> output, out string error)
        {
            output.Clear(); error = string.Empty;
            var selected = cache.Wall(wallId);
            if (selected == null || !Finite(pointerDelta)) { error = "WALL_MOVE_INVALID"; return false; }
            Vector2 delta = PerpendicularDisplacement(selected.axisStart, selected.axisEnd, pointerDelta);
            output.Add(new WallPose(wallId, selected.axisStart + delta, selected.axisEnd + delta));
            foreach (var wall in cache.Walls)
            {
                if (wall.wallId == wallId || wall.buildPlaneId != selected.buildPlaneId) continue;
                bool startTouches = Same(Project(wall.axisStart, selected.axisStart, selected.axisEnd, out _), wall.axisStart);
                bool endTouches = Same(Project(wall.axisEnd, selected.axisStart, selected.axisEnd, out _), wall.axisEnd);
                Vector2 start = wall.axisStart + (startTouches ? delta : Vector2.zero);
                Vector2 end = wall.axisEnd + (endTouches ? delta : Vector2.zero);
                // Selected endpoints hosted inside another wall must remain hosted.
                if (!ValidateHostedEndpoint(selected.axisStart, selected.axisStart + delta, wall, start, end) ||
                    !ValidateHostedEndpoint(selected.axisEnd, selected.axisEnd + delta, wall, start, end))
                { output.Clear(); error = "MOVE_WOULD_DETACH_HOSTED_ENDPOINT"; return false; }
                if (startTouches || endTouches)
                {
                    if (Vector2.Distance(start, end) < 0.05f) { output.Clear(); error = "CONNECTED_WALL_TOO_SHORT"; return false; }
                    output.Add(new WallPose(wall.wallId, start, end));
                }
                else if (SegmentsCrossInside(selected.axisStart, selected.axisEnd, wall.axisStart, wall.axisEnd) &&
                         !SegmentsIntersect(selected.axisStart + delta, selected.axisEnd + delta, start, end))
                { output.Clear(); error = "MOVE_WOULD_LOSE_CROSSING"; return false; }
            }
            return true;
        }
        private static bool ValidateHostedEndpoint(Vector2 before, Vector2 after, BistroBuilderWallRecord host, Vector2 start, Vector2 end)
        {
            if (Same(before, host.axisStart) || Same(before, host.axisEnd)) return true;
            return !Same(Project(before, host.axisStart, host.axisEnd, out _), before) ||
                Same(Project(after, start, end, out _), after);
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool IntersectionParameters(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float t, out float u)
        {
            Vector2 r = b-a, s = d-c; float cross = Cross(r,s); t = u = 0f;
            if (Math.Abs(cross) < 0.000001f) return false;
            t = Cross(c-a,s)/cross; u = Cross(c-a,r)/cross; return true;
        }
        private static bool SegmentsCrossInside(Vector2 a, Vector2 b, Vector2 c, Vector2 d) =>
            IntersectionParameters(a,b,c,d,out float t,out float u) && t > 0f && t < 1f && u > 0f && u < 1f;
        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d) =>
            IntersectionParameters(a,b,c,d,out float t,out float u) && t >= 0f && t <= 1f && u >= 0f && u <= 1f;

        public static bool TryOpening(BistroBuilderWallRecord host, Vector2 cursor, float width, float bottom, float height,
            out float alongHost, out Vector2 center, out string error)
        {
            alongHost = 0f; center = default; error = string.Empty;
            if (host == null || !Finite(cursor) || !Finite(width) || !Finite(bottom) || !Finite(height) ||
                width <= 0f || bottom < 0f || height <= 0f || bottom + height > host.height)
            { error = "OPENING_INVALID_DIMENSIONS"; return false; }
            float margin = Policy.openingEdgeMargin;
            if (host.Length < width + 2f * margin) { error = "OPENING_DOES_NOT_FIT_HOST"; return false; }
            Project(cursor, host.axisStart, host.axisEnd, out float t);
            float half = (width * 0.5f + margin) / host.Length;
            alongHost = Mathf.Clamp(t, half, 1f-half);
            center = Vector2.Lerp(host.axisStart, host.axisEnd, alongHost); return true;
        }
    }
}
