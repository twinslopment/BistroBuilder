using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Autoridad unica de navegacion y circulacion del restaurante.
/// Combina NavMesh con un A* de respaldo consciente de mobiliario,
/// zonas, ocupacion dinamica, reservas y congestion.
/// </summary>
[DisallowMultipleComponent]
public sealed partial class BistroBuilderNavigationService : MonoBehaviour
{
    [Header("Geometria")]
    [SerializeField, Min(0.2f)] private float gridCellSize = 0.45f;
    [SerializeField, Min(0.1f)] private float defaultAgentRadius = 0.28f;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 1.4f;
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;
    [SerializeField, Min(1000)] private int maximumExpandedGridNodes = 24000;

    [Header("Circulacion")]
    [SerializeField, Min(0.05f)] private float presenceStaleSeconds = 0.8f;
    [SerializeField, Min(0.2f)] private float destinationReservationSeconds = 1.5f;
    [SerializeField, Min(0f)] private float congestionWeight = 0.55f;
    [SerializeField, Min(0f)] private float staticClearance = 0.08f;
    [SerializeField, Min(0f)] private float areaBoundaryTolerance = 0.32f;
    [SerializeField, Min(0.3f)] private float interactionEndpointTolerance = 0.9f;
    [SerializeField, Range(0.3f, 1f)] private float parkedSeatObstacleScale = 0.58f;

    private readonly List<RestaurantArea> areas = new List<RestaurantArea>(16);
    private readonly List<BistroBuilderNavigationAccessZone> accessZones =
        new List<BistroBuilderNavigationAccessZone>(16);
    private readonly List<RestaurantPlacementShape> staticShapes =
        new List<RestaurantPlacementShape>(128);
    private readonly List<BistroBuilderDynamicCirculationEnvelope> dynamicEnvelopes =
        new List<BistroBuilderDynamicCirculationEnvelope>(32);
    private readonly Dictionary<string, AgentPresence> presences =
        new Dictionary<string, AgentPresence>(StringComparer.Ordinal);
    private NavMeshPath navPath;
    private readonly List<Vector3> scratchGridRoute = new List<Vector3>(64);
    private float nextCleanup;
    private BistroBuilderSpatialInteractionService spatialService;

    public event Action<int> NavigationChanged;
    public int Revision { get; private set; } = 1;
    public BistroBuilderCirculationHealthReport LastHealthReport { get; private set; }
    public int ActiveAgentCount => presences.Count;
    public int ActiveDestinationReservationCount => spatialService != null
        ? spatialService.CountLeases(BistroBuilderSpatialClaimKind.Destination)
        : 0;
    public int StaticObstacleCount => staticShapes.Count;
    public int DynamicEnvelopeCount => dynamicEnvelopes.Count;

    private void Awake()
    {
        navPath = new NavMeshPath();
        spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        InitializeV1();
        RebuildNavigationTopology();
    }

    private void Update()
    {
        TickV1();
        if (Time.unscaledTime < nextCleanup) return;
        CleanupTransientState();
        nextCleanup = Time.unscaledTime + 0.25f;
    }

    public bool ValidateConfiguration(out string error)
    {
        if (gridCellSize < 0.2f || defaultAgentRadius <= 0f ||
            maximumExpandedGridNodes < 1000)
        {
            error = "La configuracion de navegacion contiene limites invalidos.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void RebuildNavigationTopology()
    {
        if (spatialService == null)
            spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        areas.Clear();
        accessZones.Clear();
        staticShapes.Clear();
        RestaurantArea[] foundAreas = FindObjectsByType<RestaurantArea>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < foundAreas.Length; i++)
            if (foundAreas[i] != null) areas.Add(foundAreas[i]);

        BistroBuilderNavigationAccessZone[] zones =
            FindObjectsByType<BistroBuilderNavigationAccessZone>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
            if (zones[i] != null) accessZones.Add(zones[i]);

        RestaurantPlacementFootprint[] footprints =
            FindObjectsByType<RestaurantPlacementFootprint>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < footprints.Length; i++)
        {
            RestaurantPlacementFootprint footprint = footprints[i];
            if (footprint != null && footprint.BlocksOtherPlacements)
            {
                RestaurantPlacementShape shape = footprint.BuildCurrentShape();
                if (footprint.GetComponent<RestaurantSeat>() != null)
                {
                    shape = new RestaurantPlacementShape(
                        shape.Center, shape.RightAxis, shape.ForwardAxis,
                        shape.HalfExtents * parkedSeatObstacleScale, 0f);
                }
                staticShapes.Add(shape);
            }
        }
        dynamicEnvelopes.RemoveAll(item => item == null);
        BumpRevision();
        HandleTopologyRebuiltV1();
    }

    public void RegisterDynamicEnvelope(BistroBuilderDynamicCirculationEnvelope envelope)
    {
        if (envelope == null || dynamicEnvelopes.Contains(envelope)) return;
        dynamicEnvelopes.Add(envelope);
        NotifyTransientSpaceChangedV1();
    }
    public void UnregisterDynamicEnvelope(BistroBuilderDynamicCirculationEnvelope envelope)
    {
        if (envelope != null && dynamicEnvelopes.Remove(envelope))
            NotifyTransientSpaceChangedV1();
    }

    public void NotifyDynamicSpaceChanged() => NotifyTransientSpaceChangedV1();

    public bool TryBuildRoute(
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        List<Vector3> points,
        out float lengthMeters,
        out BistroBuilderNavigationRouteKind routeKind)
    {
        return TryBuildRoute(
            requesterId, agent, origin, destination, defaultAgentRadius,
            points, out lengthMeters, out routeKind);
    }

    public bool TryBuildRoute(
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float mobilityRadius,
        List<Vector3> points,
        out float lengthMeters,
        out BistroBuilderNavigationRouteKind routeKind)
    {
        lengthMeters = 0f;
        routeKind = BistroBuilderNavigationRouteKind.None;
        if (points == null) return false;
        points.Clear();
        float radius = Mathf.Max(0.05f, mobilityRadius);

        if (TryBuildNavMeshRoute(
                requesterId, agent, origin, destination, radius, points,
                out float navLength, out _))
        {
            lengthMeters = navLength;
            routeKind = BistroBuilderNavigationRouteKind.NavMesh;
            return true;
        }

        scratchGridRoute.Clear();
        if (TryBuildGridRoute(
                requesterId, agent, origin, destination, radius, scratchGridRoute,
                out float gridLength, out _))
        {
            points.Clear();
            points.AddRange(scratchGridRoute);
            lengthMeters = gridLength;
            routeKind = BistroBuilderNavigationRouteKind.GridFallback;
            return true;
        }

        if (TryBuildOperationalDockRoute(
                requesterId, agent, origin, destination, radius, points,
                out lengthMeters))
        {
            routeKind = BistroBuilderNavigationRouteKind.OperationalDock;
            return true;
        }

        if (areas.Count == 0 && staticShapes.Count == 0)
        {
            points.Add(destination);
            lengthMeters = Vector3.Distance(origin, destination);
            routeKind = BistroBuilderNavigationRouteKind.DirectDegraded;
            return true;
        }
        return false;
    }

    public bool TryBuildRouteDetailed(
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        out BistroBuilderNavigationRoute route)
    {
        return TryBuildRouteDetailed(
            requesterId, agent, origin, destination, defaultAgentRadius, out route);
    }

    public bool TryBuildRouteDetailed(
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float mobilityRadius,
        out BistroBuilderNavigationRoute route)
    {
        route = new BistroBuilderNavigationRoute { navigationRevision = Revision };
        if (!TryBuildRoute(
                requesterId, agent, origin, destination, mobilityRadius, route.points,
                out float meters, out BistroBuilderNavigationRouteKind kind))
            return false;
        route.kind = kind;
        route.lengthMeters = meters;
        route.congestionCost = MeasureCongestionAlongRoute(route.points, requesterId);
        route.totalScore = route.lengthMeters + route.congestionCost * congestionWeight;
        route.isComplete = true;
        return true;
    }

    public float EstimateRouteMeters(
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination)
    {
        return EstimateRouteMeters(
            requesterId, agent, origin, destination, defaultAgentRadius);
    }

    public float EstimateRouteMeters(
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float mobilityRadius)
    {
        var points = new List<Vector3>(32);
        return TryBuildRoute(
            requesterId, agent, origin, destination, mobilityRadius, points,
            out float meters, out _) ? meters : float.PositiveInfinity;
    }
    public void UpdateAgentPresence(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 position,
        float radius = -1f,
        int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        presences[ownerId] = new AgentPresence
        {
            ownerId = ownerId,
            agent = agent,
            position = position,
            radius = radius > 0f ? radius : defaultAgentRadius,
            priority = priority != 0 ? priority : DefaultPriority(agent),
            seenAt = Time.unscaledTime
        };
        UpdateAgentPresenceV1(
            ownerId, agent, position,
            radius > 0f ? radius : defaultAgentRadius,
            priority != 0 ? priority : DefaultPriority(agent));
    }

    public void RemoveAgentPresence(string ownerId)
    {
        if (!string.IsNullOrWhiteSpace(ownerId)) presences.Remove(ownerId);
        RemoveAgentPresenceV1(ownerId);
        ReleaseDestination(ownerId);
    }

    public bool CanAdvance(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 proposed,
        float radius = -1f,
        int priority = 0)
    {
        float r = radius > 0f ? radius : defaultAgentRadius;
        if (spatialService != null &&
            spatialService.BlocksTraversalPoint(proposed, r, ownerId))
            return false;
        for (int i = 0; i < dynamicEnvelopes.Count; i++)
        {
            BistroBuilderDynamicCirculationEnvelope envelope = dynamicEnvelopes[i];
            if (envelope == null || (spatialService != null && envelope.HasSpatialLease))
                continue;
            if (envelope.BlocksPoint(proposed, r, agent, ownerId))
                return false;
        }
        Vector3 current = proposed;
        if (presences.TryGetValue(ownerId, out AgentPresence self) && self != null)
            current = self.position;
        return CanAdvanceV1(
            ownerId, agent, current, proposed, r,
            priority != 0 ? priority : DefaultPriority(agent));
    }

    public bool TryReserveDestination(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 preferred,
        float radius,
        int priority,
        out Vector3 reserved)
    {
        reserved = preferred;
        if (string.IsNullOrWhiteSpace(ownerId) || spatialService == null)
            return false;

        float r = Mathf.Max(0.12f, radius);
        int p = priority != 0 ? priority : DefaultPriority(agent);
        return spatialService.TryReservePointWithAlternates(
            ownerId,
            BistroBuilderSpatialClaimKind.Destination,
            preferred,
            r,
            p,
            destinationReservationSeconds,
            candidate => IsPointTraversable(
                candidate, r, agent, ownerId, preferred, candidate, true),
            out reserved,
            out _);
    }

    public void RefreshDestination(string ownerId)
    {
        if (spatialService == null || string.IsNullOrWhiteSpace(ownerId))
            return;

        spatialService.RefreshOwnerLease(
            ownerId,
            BistroBuilderSpatialClaimKind.Destination,
            destinationReservationSeconds);
    }

    public void ReleaseDestination(string ownerId)
    {
        if (spatialService == null || string.IsNullOrWhiteSpace(ownerId))
            return;

        spatialService.ReleaseOwnerLeases(
            ownerId,
            BistroBuilderSpatialClaimKind.Destination);
    }

    public BistroBuilderCirculationHealthReport EvaluateCirculationHealth()
    {
        var report = new BistroBuilderCirculationHealthReport { revision = Revision };
        GameObject entranceObject = GameObject.Find("RestaurantEntrancePoint");
        Transform entrance = entranceObject != null ? entranceObject.transform : null;
        RestaurantTable[] tables = FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        KitchenSystem[] kitchens = FindObjectsByType<KitchenSystem>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

        if (entrance == null)
            AddIssue(report, "entrance_missing",
                BistroBuilderCirculationIssueSeverity.Blocking,
                "No existe un acceso de entrada navegable.", "Entrada");

        if (entrance != null)
        {
            for (int i = 0; i < tables.Length; i++)
            {
                RestaurantTable table = tables[i];
                if (table == null || table.CustomerApproachPoint == null) continue;
                CheckConnection(report, "customer_table_" + table.TableId,
                    entrance.position, table.CustomerApproachPoint.position,
                    BistroBuilderNavigationAgentMask.Customer,
                    "Entrada -> mesa " + table.TableId);
            }
        }

        for (int k = 0; k < kitchens.Length; k++)
        {
            KitchenSystem kitchen = kitchens[k];
            if (kitchen == null || kitchen.PickupPoint == null) continue;
            for (int i = 0; i < tables.Length; i++)
            {
                RestaurantTable table = tables[i];
                if (table == null || table.WaiterServicePoint == null) continue;
                CheckConnection(report,
                    "waiter_kitchen_table_" + k + "_" + table.TableId,
                    kitchen.PickupPoint.position,
                    table.WaiterServicePoint.position,
                    BistroBuilderNavigationAgentMask.Waiter,
                    "Cocina -> mesa " + table.TableId);
            }
        }

        BistroBuilderGoodsReceivingRoute receiving =
            FindFirstObjectByType<BistroBuilderGoodsReceivingRoute>();
        if (receiving != null && receiving.SupplyAccessPoint != null &&
            receiving.WarehouseDropPoint != null)
        {
            CheckConnection(report, "delivery_warehouse",
                receiving.SupplyAccessPoint.position,
                receiving.WarehouseDropPoint.position,
                BistroBuilderNavigationAgentMask.Delivery,
                "Acceso suministros -> almacen");
        }
        LastHealthReport = report;
        return report;
    }

    private void CheckConnection(
        BistroBuilderCirculationHealthReport report,
        string id,
        Vector3 from,
        Vector3 to,
        BistroBuilderNavigationAgentMask agent,
        string label)
    {
        report.checkedConnections++;
        var route = new List<Vector3>(32);
        if (TryBuildRoute("health:" + id, agent, from, to, route,
                out float meters, out _))
        {
            report.reachableConnections++;
            if (meters > Vector3.Distance(from, to) * 2.8f + 2f)
                AddIssue(report, id + "_detour",
                    BistroBuilderCirculationIssueSeverity.Warning,
                    label + " funciona, pero el recorrido es muy indirecto.", label);
            return;
        }
        AddIssue(report, id,
            BistroBuilderCirculationIssueSeverity.Blocking,
            label + " no tiene una ruta transitable.", label);
    }

    private bool TryBuildNavMeshRoute(
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float mobilityRadius,
        List<Vector3> points,
        out float length,
        out float congestion)
    {
        float radius = Mathf.Max(0.05f, mobilityRadius);
        length = 0f;
        congestion = 0f;
        points.Clear();
        if (navPath == null) navPath = new NavMeshPath();
        if (!NavMesh.SamplePosition(origin, out NavMeshHit start,
                navMeshSampleRadius, navMeshAreaMask) ||
            !NavMesh.SamplePosition(destination, out NavMeshHit end,
                navMeshSampleRadius, navMeshAreaMask) ||
            !NavMesh.CalculatePath(start.position, end.position,
                navMeshAreaMask, navPath) ||
            navPath.status != NavMeshPathStatus.PathComplete ||
            navPath.corners == null || navPath.corners.Length < 2)
            return false;

        Vector3 previous = origin;
        for (int i = 1; i < navPath.corners.Length; i++)
        {
            Vector3 corner = navPath.corners[i];
            if (!SegmentAllowedForNavMesh(previous, corner, radius,
                    agent, requesterId, origin, destination))
            {
                points.Clear();
                return false;
            }
            length += Vector3.Distance(previous, corner);
            congestion += CongestionAt(corner, requesterId);
            points.Add(corner);
            previous = corner;
        }
        if ((previous - destination).sqrMagnitude > 0.001f)
        {
            if (!SegmentAllowedForNavMesh(previous, destination, radius,
                    agent, requesterId, origin, destination))
            {
                points.Clear();
                return false;
            }
            length += Vector3.Distance(previous, destination);
            congestion += CongestionAt(destination, requesterId);
            points.Add(destination);
        }
        return points.Count > 0;
    }

    private bool TryBuildGridRoute(
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float mobilityRadius,
        List<Vector3> points,
        out float length,
        out float congestion)
    {
        float radius = Mathf.Max(0.05f, mobilityRadius);
        length = 0f;
        congestion = 0f;
        points.Clear();
        if (Vector3.Distance(origin, destination) <= gridCellSize ||
            SegmentAllowedForNavMesh(origin, destination, radius,
                agent, requesterId, origin, destination))
        {
            points.Add(destination);
            length = Vector3.Distance(origin, destination);
            congestion = CongestionAt(destination, requesterId);
            return true;
        }

        GetSearchBounds(origin, destination,
            out float minX, out float minZ, out int width, out int height);
        if (width <= 0 || height <= 0 ||
            width * height > maximumExpandedGridNodes * 4)
            return false;

        int sx = Mathf.Clamp(Mathf.RoundToInt((origin.x - minX) / gridCellSize), 0, width - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt((origin.z - minZ) / gridCellSize), 0, height - 1);
        int gx = Mathf.Clamp(Mathf.RoundToInt((destination.x - minX) / gridCellSize), 0, width - 1);
        int gz = Mathf.Clamp(Mathf.RoundToInt((destination.z - minZ) / gridCellSize), 0, height - 1);
        int startKey = sz * width + sx;
        int goalKey = gz * width + gx;

        var records = new Dictionary<int, GridRecord>(8192);
        var heap = new GridHeap();
        GridRecord startRecord = new GridRecord
        {
            key = startKey,
            x = sx,
            z = sz,
            g = 0f,
            f = Heuristic(sx, sz, gx, gz),
            parent = -1
        };
        records[startKey] = startRecord;
        heap.Push(startRecord);

        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dz = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int expanded = 0;
        bool found = false;

        while (heap.Count > 0 && expanded < maximumExpandedGridNodes)
        {
            GridRecord current = heap.Pop();
            if (!records.TryGetValue(current.key, out GridRecord authoritative) ||
                authoritative.closed || current.g > authoritative.g + 0.0001f)
                continue;
            authoritative.closed = true;
            records[current.key] = authoritative;
            expanded++;
            if (current.key == goalKey)
            {
                found = true;
                break;
            }

            Vector3 currentWorld = CellWorld(
                current.x, current.z, minX, minZ, origin.y);
            for (int n = 0; n < 8; n++)
            {
                int nx = current.x + dx[n];
                int nz = current.z + dz[n];
                if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                int key = nz * width + nx;
                if (records.TryGetValue(key, out GridRecord existing) &&
                    existing.closed) continue;

                Vector3 world = CellWorld(nx, nz, minX, minZ, origin.y);
                bool isGoal = key == goalKey;
                if (!isGoal && !IsPointStructurallyTraversableForRoute(world, radius,
                        agent, origin, destination))
                    continue;
                if (!SegmentAllowedForNavMesh(currentWorld, world, radius,
                        agent, requesterId, origin, destination))
                    continue;

                float step = (dx[n] == 0 || dz[n] == 0) ? 1f : 1.4142135f;
                float newG = current.g + step * TraversalCostAt(world, agent) +
                    CongestionAt(world, requesterId) * congestionWeight;
                if (!records.TryGetValue(key, out existing) ||
                    newG + 0.0001f < existing.g)
                {
                    GridRecord next = new GridRecord
                    {
                        key = key,
                        x = nx,
                        z = nz,
                        g = newG,
                        f = newG + Heuristic(nx, nz, gx, gz),
                        parent = current.key
                    };
                    records[key] = next;
                    heap.Push(next);
                }
            }
        }

        if (!found || !records.ContainsKey(goalKey)) return false;
        var reverse = new List<Vector3>(64);
        int cursor = goalKey;
        int guard = 0;
        while (cursor != startKey && guard++ < maximumExpandedGridNodes)
        {
            GridRecord record = records[cursor];
            reverse.Add(CellWorld(record.x, record.z, minX, minZ, origin.y));
            cursor = record.parent;
            if (cursor < 0) return false;
        }
        reverse.Reverse();
        SmoothGridRoute(origin, destination, reverse, requesterId, agent, radius, points);
        if (points.Count == 0 ||
            (points[points.Count - 1] - destination).sqrMagnitude > 0.001f)
            points.Add(destination);

        Vector3 previous = origin;
        for (int i = 0; i < points.Count; i++)
        {
            length += Vector3.Distance(previous, points[i]);
            congestion += CongestionAt(points[i], requesterId);
            previous = points[i];
        }
        return true;
    }

    private void SmoothGridRoute(
        Vector3 origin,
        Vector3 destination,
        List<Vector3> raw,
        string requesterId,
        BistroBuilderNavigationAgentMask agent,
        float mobilityRadius,
        List<Vector3> result)
    {
        result.Clear();
        if (raw.Count == 0)
        {
            result.Add(destination);
            return;
        }
        Vector3 anchor = origin;
        int index = 0;
        while (index < raw.Count)
        {
            int best = index;
            for (int test = raw.Count - 1; test >= index; test--)
            {
                Vector3 target = test == raw.Count - 1
                    ? destination
                    : raw[test];
                if (SegmentAllowedForNavMesh(anchor, target, mobilityRadius,
                        agent, requesterId, origin, destination))
                {
                    best = test;
                    break;
                }
            }
            Vector3 chosen = best == raw.Count - 1
                ? destination
                : raw[best];
            result.Add(chosen);
            anchor = chosen;
            index = best + 1;
        }
    }

    private bool SegmentAllowed(
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
            if (!IsPointTraversable(point, radius, agent, requesterId,
                    routeStart, routeEnd, false))
                return false;
        }
        return true;
    }

    private bool IsPointTraversable(
        Vector3 point,
        float radius,
        BistroBuilderNavigationAgentMask agent,
        string requesterId,
        Vector3 routeStart,
        Vector3 routeEnd,
        bool allowEndpoint)
    {
        float endpointToleranceSquared = interactionEndpointTolerance * interactionEndpointTolerance;
        bool nearEndpoint = allowEndpoint ||
            HorizontalDistanceSquared(point, routeStart) < endpointToleranceSquared ||
            HorizontalDistanceSquared(point, routeEnd) < endpointToleranceSquared;
        if (!nearEndpoint && areas.Count > 0 && !IsInsideAllowedArea(point, agent))
            return false;

        if (!nearEndpoint)
        {
            for (int i = 0; i < staticShapes.Count; i++)
            {
                if (PointInsideShape(point, staticShapes[i], radius + staticClearance))
                    return false;
            }
        }
        if (spatialService != null &&
            spatialService.BlocksTraversalPoint(point, radius, requesterId))
            return false;
        for (int i = 0; i < dynamicEnvelopes.Count; i++)
        {
            BistroBuilderDynamicCirculationEnvelope envelope = dynamicEnvelopes[i];
            if (envelope == null || (spatialService != null && envelope.HasSpatialLease))
                continue;
            if (envelope.BlocksPoint(point, radius, agent, requesterId))
                return false;
        }
        return true;
    }

    private bool IsInsideAllowedArea(
        Vector3 point,
        BistroBuilderNavigationAgentMask agent)
    {
        bool insideAny = false;
        bool allowed = false;
        for (int i = 0; i < areas.Count; i++)
        {
            RestaurantArea area = areas[i];
            if (area == null || !area.IsOperational) continue;
            bool inside = area.ContainsPosition(point) ||
                IsNearAreaBoundary(area, point, areaBoundaryTolerance);
            if (!inside) continue;
            insideAny = true;
            BistroBuilderNavigationAccessZone zone = FindZone(area);
            if (zone == null || zone.Allows(agent)) allowed = true;
        }
        if (!insideAny && (agent & BistroBuilderNavigationAgentMask.Delivery) != 0)
            return true;
        return insideAny && allowed;
    }

    private static bool IsNearAreaBoundary(
        RestaurantArea area,
        Vector3 point,
        float tolerance)
    {
        if (area == null || tolerance <= 0f || area.BoundaryColliders == null)
            return false;
        float sqrTolerance = tolerance * tolerance;
        for (int i = 0; i < area.BoundaryColliders.Count; i++)
        {
            Collider collider = area.BoundaryColliders[i];
            if (collider == null || !collider.enabled) continue;
            Vector3 closest = collider.ClosestPoint(point);
            Vector3 delta = closest - point;
            delta.y = 0f;
            if (delta.sqrMagnitude <= sqrTolerance) return true;
        }
        return false;
    }

    private BistroBuilderNavigationAccessZone FindZone(RestaurantArea area)
    {
        for (int i = 0; i < accessZones.Count; i++)
        {
            BistroBuilderNavigationAccessZone zone = accessZones[i];
            if (zone != null && ReferenceEquals(zone.Area, area)) return zone;
        }
        return null;
    }

    private float TraversalCostAt(
        Vector3 point,
        BistroBuilderNavigationAgentMask agent)
    {
        float cost = 1f;
        for (int i = 0; i < accessZones.Count; i++)
        {
            BistroBuilderNavigationAccessZone zone = accessZones[i];
            if (zone != null && zone.Area != null && zone.Allows(agent) &&
                zone.Area.ContainsPosition(point))
                cost = Mathf.Max(cost, zone.TraversalCost);
        }
        return cost;
    }

    private float CongestionAt(Vector3 point, string requesterId)
    {
        return GetTrafficCongestionV1(point);
    }
    private float MeasureCongestionAlongRoute(
        List<Vector3> points,
        string requesterId)
    {
        float total = 0f;
        if (points == null) return total;
        for (int i = 0; i < points.Count; i++)
            total += CongestionAt(points[i], requesterId);
        return total;
    }

    private void CleanupTransientState()
    {
        float now = Time.unscaledTime;
        var staleAgents = new List<string>();
        foreach (KeyValuePair<string, AgentPresence> pair in presences)
        {
            if (pair.Value == null || now - pair.Value.seenAt > presenceStaleSeconds)
                staleAgents.Add(pair.Key);
        }
        for (int i = 0; i < staleAgents.Count; i++) presences.Remove(staleAgents[i]);
        dynamicEnvelopes.RemoveAll(item => item == null);
    }

    private void GetSearchBounds(
        Vector3 origin,
        Vector3 destination,
        out float minX,
        out float minZ,
        out int width,
        out int height)
    {
        float maxX = Mathf.Max(origin.x, destination.x);
        float maxZ = Mathf.Max(origin.z, destination.z);
        minX = Mathf.Min(origin.x, destination.x);
        minZ = Mathf.Min(origin.z, destination.z);
        for (int i = 0; i < areas.Count; i++)
        {
            RestaurantArea area = areas[i];
            if (area == null || area.BoundaryColliders == null) continue;
            for (int c = 0; c < area.BoundaryColliders.Count; c++)
            {
                Collider collider = area.BoundaryColliders[c];
                if (collider == null || !collider.enabled) continue;
                Bounds bounds = collider.bounds;
                minX = Mathf.Min(minX, bounds.min.x);
                minZ = Mathf.Min(minZ, bounds.min.z);
                maxX = Mathf.Max(maxX, bounds.max.x);
                maxZ = Mathf.Max(maxZ, bounds.max.z);
            }
        }
        minX -= 0.6f;
        minZ -= 0.6f;
        maxX += 0.6f;
        maxZ += 0.6f;
        width = Mathf.CeilToInt((maxX - minX) / gridCellSize) + 1;
        height = Mathf.CeilToInt((maxZ - minZ) / gridCellSize) + 1;
    }

    private Vector3 CellWorld(
        int x,
        int z,
        float minX,
        float minZ,
        float y)
    {
        return new Vector3(
            minX + x * gridCellSize,
            y,
            minZ + z * gridCellSize);
    }

    private float Heuristic(int x, int z, int gx, int gz)
    {
        int dx = Mathf.Abs(gx - x);
        int dz = Mathf.Abs(gz - z);
        int diagonal = Mathf.Min(dx, dz);
        int straight = Mathf.Max(dx, dz) - diagonal;
        return (diagonal * 1.4142135f + straight) * gridCellSize;
    }

    private static bool PointInsideShape(
        Vector3 point,
        RestaurantPlacementShape shape,
        float expansion)
    {
        Vector3 delta = point - shape.Center;
        float x = Mathf.Abs(Vector3.Dot(delta, shape.RightAxis));
        float z = Mathf.Abs(Vector3.Dot(delta, shape.ForwardAxis));
        return x <= shape.HalfWidth + expansion &&
               z <= shape.HalfDepth + expansion;
    }

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return x * x + z * z;
    }

    private static int DefaultPriority(BistroBuilderNavigationAgentMask agent)
    {
        return 0;
    }

    private static void AddIssue(
        BistroBuilderCirculationHealthReport report,
        string id,
        BistroBuilderCirculationIssueSeverity severity,
        string message,
        string source)
    {
        report.issues.Add(new BistroBuilderCirculationIssue
        {
            issueId = id,
            severity = severity,
            message = message,
            sourceName = source
        });
        if (severity == BistroBuilderCirculationIssueSeverity.Blocking)
            report.blockingCount++;
        else if (severity == BistroBuilderCirculationIssueSeverity.Warning)
            report.warningCount++;
    }

    private void BumpRevision()
    {
        Revision = Revision == int.MaxValue ? 1 : Revision + 1;
        NavigationChanged?.Invoke(Revision);
    }

    private sealed class AgentPresence
    {
        public string ownerId;
        public BistroBuilderNavigationAgentMask agent;
        public Vector3 position;
        public float radius;
        public int priority;
        public float seenAt;
    }

    private sealed class DestinationReservation
    {
        public string ownerId;
        public Vector3 position;
        public float radius;
        public int priority;
        public float expiresAt;
    }

    private struct GridRecord
    {
        public int key;
        public int x;
        public int z;
        public int parent;
        public float g;
        public float f;
        public bool closed;
    }

    private sealed class GridHeap
    {
        private readonly List<GridRecord> items = new List<GridRecord>(1024);
        public int Count => items.Count;

        public void Push(GridRecord item)
        {
            items.Add(item);
            int index = items.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (items[parent].f <= item.f) break;
                items[index] = items[parent];
                index = parent;
            }
            items[index] = item;
        }

        public GridRecord Pop()
        {
            GridRecord root = items[0];
            GridRecord last = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);
            if (items.Count == 0) return root;
            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= items.Count) break;
                int right = left + 1;
                int child = right < items.Count && items[right].f < items[left].f
                    ? right : left;
                if (items[child].f >= last.f) break;
                items[index] = items[child];
                index = child;
            }
            items[index] = last;
            return root;
        }
    }
}
