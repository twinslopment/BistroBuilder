using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extensiones avanzadas de Navegacion 17 para aproximaciones operativas,
/// rutas hibridas y validacion dinamica sin duplicar la geometria del NavMesh.
/// </summary>
public sealed partial class BistroBuilderNavigationService
{
    private bool TryBuildOperationalDockRoute(
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float mobilityRadius,
        List<Vector3> points,
        out float length)
    {
        length = 0f;
        var candidateRoute = new List<Vector3>(48);
        var bestRoute = new List<Vector3>(48);
        float bestScore = float.PositiveInfinity;
        const int angularSamples = 16;
        float[] rings = { 0.55f, 0.8f, 1.05f, 1.3f, 1.6f };

        for (int ring = 0; ring < rings.Length; ring++)
        {
            float distance = rings[ring];
            for (int sample = 0; sample < angularSamples; sample++)
            {
                float angle = sample * Mathf.PI * 2f / angularSamples;
                Vector3 candidate = destination +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                candidateRoute.Clear();
                bool routeOk = TryBuildNavMeshRoute(
                    requesterId,
                    agent,
                    origin,
                    candidate,
                    mobilityRadius,
                    candidateRoute,
                    out float routeMeters,
                    out float routeCongestion);

                if (!routeOk)
                {
                    candidateRoute.Clear();
                    routeOk = TryBuildGridRoute(
                        requesterId,
                        agent,
                        origin,
                        candidate,
                        mobilityRadius,
                        candidateRoute,
                        out routeMeters,
                        out routeCongestion);
                }

                if (!routeOk)
                    continue;

                float score = routeMeters +
                    Vector3.Distance(candidate, destination) * 0.35f +
                    routeCongestion * congestionWeight;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestRoute.Clear();
                bestRoute.AddRange(candidateRoute);
                if (bestRoute.Count == 0 ||
                    (bestRoute[bestRoute.Count - 1] - candidate).sqrMagnitude > 0.001f)
                    bestRoute.Add(candidate);
            }
        }

        if (bestRoute.Count == 0)
            return false;

        points.Clear();
        points.AddRange(bestRoute);
        if ((points[points.Count - 1] - destination).sqrMagnitude > 0.001f)
            points.Add(destination);

        Vector3 previous = origin;
        for (int i = 0; i < points.Count; i++)
        {
            length += Vector3.Distance(previous, points[i]);
            previous = points[i];
        }
        return true;
    }

    private bool SegmentAllowedForNavMesh(
        Vector3 a,
        Vector3 b,
        float radius,
        BistroBuilderNavigationAgentMask agent,
        string requesterId,
        Vector3 routeStart,
        Vector3 routeEnd)
    {
        float distance = Vector3.Distance(a, b);
        int samples = Mathf.Max(1, Mathf.CeilToInt(
            distance / Mathf.Max(0.15f, gridCellSize * 0.45f)));
        for (int i = 1; i <= samples; i++)
        {
            Vector3 point = Vector3.Lerp(a, b, i / (float)samples);
            if (!IsPointStructurallyTraversableForRoute(
                    point, radius, agent, routeStart, routeEnd))
                return false;
        }
        return true;
    }

    private bool IsPointStructurallyTraversableForRoute(
        Vector3 point,
        float radius,
        BistroBuilderNavigationAgentMask agent,
        Vector3 routeStart,
        Vector3 routeEnd)
    {
        float endpointToleranceSquared =
            interactionEndpointTolerance * interactionEndpointTolerance;
        bool nearEndpoint =
            HorizontalDistanceSquared(point, routeStart) < endpointToleranceSquared ||
            HorizontalDistanceSquared(point, routeEnd) < endpointToleranceSquared;

        if (!nearEndpoint && areas.Count > 0 && !IsInsideAllowedArea(point, agent))
            return false;

        if (!nearEndpoint)
        {
            float clearance = Mathf.Max(0.05f, radius) + staticClearance;
            for (int i = 0; i < staticShapes.Count; i++)
            {
                if (PointInsideShape(point, staticShapes[i], clearance))
                    return false;
            }
        }

        // Dynamic Sweeps, Mobility/Carry leases y otros NPC no invalidan
        // topología global: los resuelve el solver local y Traffic Coordinator.
        return true;
    }
}
