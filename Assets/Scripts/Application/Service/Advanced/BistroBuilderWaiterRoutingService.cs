using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public interface IBistroBuilderWaiterRouteProvider
{
    bool TryBuildRoute(
        Vector3 origin,
        Vector3 destination,
        List<Vector3> points,
        out float lengthMeters);
}

/// <summary>
/// Adaptador de rutas propio del Bloque 13.
/// El bloque decide la mejor ruta local disponible mediante NavMesh,
/// sin asumir la autoridad posterior de Navegación 17.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderWaiterRoutingService :
    MonoBehaviour,
    IBistroBuilderWaiterRouteProvider
{
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 1.25f;
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;

    private NavMeshPath legacyPath;

    public BistroBuilderWaiterRouteKind LastRouteKind { get; private set; } =
        BistroBuilderWaiterRouteKind.DirectFallback;

    private void Awake()
    {
        legacyPath = new NavMeshPath();
    }

    public bool TryBuildRoute(
        Vector3 origin,
        Vector3 destination,
        List<Vector3> points,
        out float lengthMeters)
    {
        lengthMeters = 0f;
        if (points == null) return false;
        points.Clear();

        legacyPath ??= new NavMeshPath();
        if (NavMesh.SamplePosition(origin, out NavMeshHit start,
                navMeshSampleRadius, navMeshAreaMask) &&
            NavMesh.SamplePosition(destination, out NavMeshHit end,
                navMeshSampleRadius, navMeshAreaMask) &&
            NavMesh.CalculatePath(start.position, end.position,
                navMeshAreaMask, legacyPath) &&
            legacyPath.status == NavMeshPathStatus.PathComplete &&
            legacyPath.corners != null && legacyPath.corners.Length >= 2)
        {
            Vector3 previous = origin;
            for (int i = 1; i < legacyPath.corners.Length; i++)
            {
                Vector3 corner = legacyPath.corners[i];
                lengthMeters += Vector3.Distance(previous, corner);
                points.Add(corner);
                previous = corner;
            }

            LastRouteKind = BistroBuilderWaiterRouteKind.NavMeshOptimal;
            return true;
        }

        points.Add(destination);
        lengthMeters = Vector3.Distance(origin, destination);
        LastRouteKind = BistroBuilderWaiterRouteKind.DirectFallback;
        return true;
    }

    public float EstimateRouteMeters(Vector3 origin, Vector3 destination)
    {
        var scratch = new List<Vector3>(12);
        return TryBuildRoute(origin, destination, scratch, out float meters)
            ? meters
            : Vector3.Distance(origin, destination);
    }
}
