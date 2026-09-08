using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye y consulta Route Corridors certificados por callbacks de la
/// autoridad espacial existente. No declara por sí mismo espacio transitable.
/// </summary>
public sealed class BistroBuilderNavigationRouteCorridorBuilder
{
    private const float SampleSpacing = 0.42f;
    private const float ProbeStep = 0.12f;
    private const float MaximumLateralProbe = 0.96f;
    private const float Epsilon = 0.0001f;

    public bool TryBuild(
        Vector3 origin,
        IReadOnlyList<Vector3> routePoints,
        float mobilityRadius,
        BistroBuilderNavigationTopologySnapshot topology,
        Func<Vector3, Vector3, bool> segmentAllowed,
        out BistroBuilderNavigationRouteCorridor corridor)
    {
        corridor = new BistroBuilderNavigationRouteCorridor
        {
            mobilityRadius = Mathf.Max(0.05f, mobilityRadius),
            spatialRevision = topology.spatialRevision,
            navigationRevision = topology.navigationRevision
        };
        if (routePoints == null || routePoints.Count == 0 || segmentAllowed == null)
            return false;

        var centers = new List<Vector3>(Mathf.Max(8, routePoints.Count * 3));
        centers.Add(origin);
        Vector3 previous = origin;
        float totalLength = 0f;
        for (int i = 0; i < routePoints.Count; i++)
        {
            Vector3 end = routePoints[i];
            float length = HorizontalDistance(previous, end);
            if (length <= Epsilon)
            {
                previous = end;
                continue;
            }

            int steps = Mathf.Max(1, Mathf.CeilToInt(length / SampleSpacing));
            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                Vector3 sample = Vector3.Lerp(previous, end, t);
                if ((centers[centers.Count - 1] - sample).sqrMagnitude > Epsilon)
                    centers.Add(sample);
            }
            totalLength += length;
            previous = end;
        }
        if (centers.Count < 2)
            return false;

        float cumulative = 0f;
        for (int i = 0; i < centers.Count; i++)
        {
            Vector3 center = centers[i];
            if (i > 0)
                cumulative += HorizontalDistance(centers[i - 1], center);
            Vector3 tangent = ResolveTangent(centers, i);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float left = ProbeClearance(center, -right, segmentAllowed);
            float rightClearance = ProbeClearance(center, right, segmentAllowed);
            corridor.samples.Add(new BistroBuilderNavigationCorridorSample
            {
                center = center,
                tangent = tangent,
                cumulativeMeters = cumulative,
                leftClearance = left,
                rightClearance = rightClearance
            });
        }

        corridor.lengthMeters = cumulative;
        return corridor.IsUsable;
    }

    public bool TryProject(
        BistroBuilderNavigationRouteCorridor corridor,
        Vector3 position,
        out BistroBuilderNavigationCorridorProjection projection)
    {
        projection = default;
        if (corridor == null || !corridor.IsUsable)
            return false;

        float bestDistanceSq = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < corridor.samples.Count - 1; i++)
        {
            BistroBuilderNavigationCorridorSample a = corridor.samples[i];
            BistroBuilderNavigationCorridorSample b = corridor.samples[i + 1];
            if (a == null || b == null) continue;

            Vector3 ab = Horizontal(b.center - a.center);
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= Epsilon) continue;
            Vector3 ap = Horizontal(position - a.center);
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / lengthSq);
            Vector3 center = Vector3.Lerp(a.center, b.center, t);
            float distanceSq = Horizontal(position - center).sqrMagnitude;
            if (distanceSq >= bestDistanceSq) continue;

            Vector3 tangent = ab.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float signed = Vector3.Dot(Horizontal(position - center), right);
            projection = new BistroBuilderNavigationCorridorProjection
            {
                center = center,
                tangent = tangent,
                right = right,
                signedLateral = signed,
                leftClearance = Mathf.Lerp(a.leftClearance, b.leftClearance, t),
                rightClearance = Mathf.Lerp(a.rightClearance, b.rightClearance, t),
                progressMeters = Mathf.Lerp(a.cumulativeMeters, b.cumulativeMeters, t)
            };
            bestDistanceSq = distanceSq;
            found = true;
        }
        return found;
    }

    public bool Contains(
        BistroBuilderNavigationRouteCorridor corridor,
        Vector3 position,
        float safetyMargin = 0f)
    {
        if (!TryProject(corridor, position, out var projection))
            return false;
        float margin = Mathf.Max(0f, safetyMargin);
        return projection.signedLateral <= projection.rightClearance - margin &&
               projection.signedLateral >= -projection.leftClearance + margin;
    }

    public bool TryFindBypass(
        BistroBuilderNavigationRouteCorridor corridor,
        Vector3 current,
        float forwardMeters,
        int preferredSide,
        Func<Vector3, Vector3, bool> segmentAllowed,
        out Vector3 target)
    {
        target = current;
        if (segmentAllowed == null ||
            !TryProject(corridor, current, out var projection))
            return false;

        float desiredProgress = Mathf.Min(
            corridor.lengthMeters,
            projection.progressMeters + Mathf.Max(0.15f, forwardMeters));
        if (!TrySampleAtProgress(corridor, desiredProgress, out var ahead))
            return false;

        int firstSide = preferredSide < 0 ? -1 : 1;
        for (int pass = 0; pass < 3; pass++)
        {
            int side = pass == 0 ? firstSide : (pass == 1 ? -firstSide : 0);
            float clearance = side < 0 ? ahead.leftClearance : ahead.rightClearance;
            float offset = side == 0
                ? 0f
                : Mathf.Min(clearance * 0.72f,
                    Mathf.Max(corridor.mobilityRadius * 1.15f, 0.18f));
            Vector3 candidate = ahead.center + ahead.right * (side * offset);
            candidate.y = ahead.center.y;
            if (segmentAllowed(current, candidate))
            {
                target = candidate;
                return true;
            }
        }
        return false;
    }

    public bool TrySampleAtProgress(
        BistroBuilderNavigationRouteCorridor corridor,
        float progressMeters,
        out BistroBuilderNavigationCorridorProjection sample)
    {
        sample = default;
        if (corridor == null || !corridor.IsUsable)
            return false;

        float progress = Mathf.Clamp(progressMeters, 0f, corridor.lengthMeters);
        for (int i = 0; i < corridor.samples.Count - 1; i++)
        {
            BistroBuilderNavigationCorridorSample a = corridor.samples[i];
            BistroBuilderNavigationCorridorSample b = corridor.samples[i + 1];
            if (a == null || b == null) continue;
            if (progress > b.cumulativeMeters && i < corridor.samples.Count - 2)
                continue;

            float span = Mathf.Max(Epsilon, b.cumulativeMeters - a.cumulativeMeters);
            float t = Mathf.Clamp01((progress - a.cumulativeMeters) / span);
            Vector3 tangent = Horizontal(b.center - a.center);
            if (tangent.sqrMagnitude <= Epsilon)
                tangent = a.tangent;
            tangent = tangent.sqrMagnitude > Epsilon ? tangent.normalized : Vector3.forward;
            sample = new BistroBuilderNavigationCorridorProjection
            {
                center = Vector3.Lerp(a.center, b.center, t),
                tangent = tangent,
                right = Vector3.Cross(Vector3.up, tangent).normalized,
                signedLateral = 0f,
                leftClearance = Mathf.Lerp(a.leftClearance, b.leftClearance, t),
                rightClearance = Mathf.Lerp(a.rightClearance, b.rightClearance, t),
                progressMeters = progress
            };
            return true;
        }
        return false;
    }

    private static float ProbeClearance(
        Vector3 center,
        Vector3 direction,
        Func<Vector3, Vector3, bool> segmentAllowed)
    {
        if (direction.sqrMagnitude <= Epsilon)
            return 0f;
        direction = Horizontal(direction).normalized;
        float lastValid = 0f;
        for (float distance = ProbeStep;
             distance <= MaximumLateralProbe + Epsilon;
             distance += ProbeStep)
        {
            Vector3 candidate = center + direction * distance;
            candidate.y = center.y;
            if (!segmentAllowed(center, candidate))
                break;
            lastValid = distance;
        }
        return lastValid;
    }

    private static Vector3 ResolveTangent(List<Vector3> centers, int index)
    {
        Vector3 tangent;
        if (index <= 0)
            tangent = centers[1] - centers[0];
        else if (index >= centers.Count - 1)
            tangent = centers[centers.Count - 1] - centers[centers.Count - 2];
        else
            tangent = centers[index + 1] - centers[index - 1];
        tangent = Horizontal(tangent);
        return tangent.sqrMagnitude > Epsilon
            ? tangent.normalized
            : Vector3.forward;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 delta = Horizontal(a - b);
        return delta.magnitude;
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
