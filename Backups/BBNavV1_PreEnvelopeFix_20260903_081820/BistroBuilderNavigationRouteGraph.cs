using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grafo topológico ligero de Navigation v1. Resume áreas y Spatial Gates de BBSIS.
/// No declara accesibilidad espacial: organiza alternativas entre regiones ya existentes.
/// </summary>
public sealed class BistroBuilderNavigationRouteGraph
{
    private const float AreaConnectionTolerance = 0.55f;
    private const float GateAssociationTolerance = 0.9f;

    private readonly Dictionary<string, Node> nodes =
        new Dictionary<string, Node>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Edge>> edges =
        new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
    private readonly List<Node> areaNodes = new List<Node>(16);
    private readonly List<string> scratchIds = new List<string>(32);

    public int NodeCount => nodes.Count;
    public int EdgeCount { get; private set; }
    public int Revision { get; private set; } = 1;

    public void Rebuild(
        IReadOnlyList<RestaurantArea> areas,
        IReadOnlyList<BistroBuilderNavigationAccessZone> accessZones,
        IReadOnlyList<BistroBuilderNavigationGateDescriptor> gates)
    {
        nodes.Clear();
        edges.Clear();
        areaNodes.Clear();
        EdgeCount = 0;

        if (areas != null)
        {
            var sortedAreas = new List<RestaurantArea>();
            for (int i = 0; i < areas.Count; i++)
                if (areas[i] != null && areas[i].IsOperational)
                    sortedAreas.Add(areas[i]);
            sortedAreas.Sort((a, b) =>
                string.CompareOrdinal(a.AreaId ?? string.Empty, b.AreaId ?? string.Empty));

            for (int i = 0; i < sortedAreas.Count; i++)
            {
                RestaurantArea area = sortedAreas[i];
                if (!TryGetAreaBounds(area, out Bounds bounds)) continue;
                BistroBuilderNavigationAccessZone zone = FindZone(accessZones, area);
                Node node = new Node
                {
                    id = "area:" + (area.AreaId ?? i.ToString()),
                    kind = NodeKind.Area,
                    area = area,
                    position = bounds.center,
                    bounds = bounds,
                    allowedAgents = zone != null
                        ? zone.AllowedAgents
                        : BistroBuilderNavigationAgentMask.All,
                    traversalCost = zone != null
                        ? Mathf.Max(0.1f, zone.TraversalCost)
                        : 1f
                };
                AddNode(node);
                areaNodes.Add(node);
            }
        }

        for (int i = 0; i < areaNodes.Count; i++)
        {
            for (int j = i + 1; j < areaNodes.Count; j++)
            {
                Node first = areaNodes[i];
                Node second = areaNodes[j];
                if (!BoundsNear2D(first.bounds, second.bounds, AreaConnectionTolerance))
                    continue;
                AddBidirectionalEdge(
                    first.id,
                    second.id,
                    HorizontalDistance(first.position, second.position),
                    false);
            }
        }

        if (gates != null)
        {
            var sortedGates = new List<BistroBuilderNavigationGateDescriptor>();
            for (int i = 0; i < gates.Count; i++)
                if (gates[i] != null && !string.IsNullOrWhiteSpace(gates[i].gateId))
                    sortedGates.Add(gates[i]);
            sortedGates.Sort((a, b) => string.CompareOrdinal(a.gateId, b.gateId));

            for (int i = 0; i < sortedGates.Count; i++)
            {
                BistroBuilderNavigationGateDescriptor gate = sortedGates[i];
                Node gateNode = new Node
                {
                    id = "gate:" + gate.gateId,
                    kind = NodeKind.Gate,
                    position = (gate.start + gate.end) * 0.5f,
                    gate = gate,
                    allowedAgents = BistroBuilderNavigationAgentMask.All,
                    traversalCost = 1f
                };
                AddNode(gateNode);

                for (int a = 0; a < areaNodes.Count; a++)
                {
                    Node area = areaNodes[a];
                    if (!GateTouchesArea(gate, area, GateAssociationTolerance))
                        continue;
                    AddBidirectionalEdge(
                        area.id,
                        gateNode.id,
                        Mathf.Max(0.1f, HorizontalDistance(area.position, gateNode.position)),
                        true);
                }
            }
        }

        Revision = Revision == int.MaxValue ? 1 : Revision + 1;
    }

    public bool TryFindGateAnchors(
        Vector3 origin,
        Vector3 destination,
        BistroBuilderNavigationAgentMask agent,
        float mobilityRadius,
        Func<Vector3, float> congestionPenalty,
        List<Vector3> anchors,
        out float topologicalCost)
    {
        topologicalCost = 0f;
        if (anchors == null) return false;
        anchors.Clear();

        Node start = FindContainingArea(origin, agent);
        Node goal = FindContainingArea(destination, agent);
        if (start == null || goal == null) return false;
        if (ReferenceEquals(start, goal) || string.Equals(start.id, goal.id, StringComparison.Ordinal))
            return true;

        var records = new Dictionary<string, SearchRecord>(StringComparer.Ordinal);
        var open = new List<SearchRecord>(32);
        SearchRecord first = new SearchRecord
        {
            nodeId = start.id,
            g = 0f,
            f = Heuristic(start.position, goal.position),
            parentId = string.Empty
        };
        records[start.id] = first;
        open.Add(first);

        while (open.Count > 0)
        {
            int bestIndex = FindBestOpenIndex(open);
            SearchRecord current = open[bestIndex];
            open.RemoveAt(bestIndex);
            if (!records.TryGetValue(current.nodeId, out SearchRecord authoritative) ||
                authoritative.closed || current.g > authoritative.g + 0.0001f)
                continue;

            authoritative.closed = true;
            records[current.nodeId] = authoritative;
            if (string.Equals(current.nodeId, goal.id, StringComparison.Ordinal))
                break;

            if (!edges.TryGetValue(current.nodeId, out List<Edge> outgoing))
                continue;
            for (int i = 0; i < outgoing.Count; i++)
            {
                Edge edge = outgoing[i];
                if (!nodes.TryGetValue(edge.to, out Node next) || next == null)
                    continue;
                if ((next.allowedAgents & agent) == 0) continue;

                float maneuverPenalty = 0f;
                if (next.kind == NodeKind.Gate && next.gate != null)
                {
                    float diameter = Mathf.Max(0.1f, mobilityRadius * 2f);
                    float width = Mathf.Max(0.1f, next.gate.minimumWidth);
                    float tightness = Mathf.Clamp01(diameter / width);
                    maneuverPenalty = tightness * tightness * 0.65f;
                }

                float congestion = congestionPenalty != null
                    ? Mathf.Max(0f, congestionPenalty(next.position))
                    : 0f;
                float newG = authoritative.g +
                             edge.baseCost * Mathf.Max(0.1f, next.traversalCost) +
                             congestion * 1.25f +
                             maneuverPenalty;

                if (records.TryGetValue(next.id, out SearchRecord existing) &&
                    existing.closed && newG >= existing.g - 0.0001f)
                    continue;
                if (!records.TryGetValue(next.id, out existing) ||
                    newG < existing.g - 0.0001f)
                {
                    SearchRecord record = new SearchRecord
                    {
                        nodeId = next.id,
                        parentId = authoritative.nodeId,
                        g = newG,
                        f = newG + Heuristic(next.position, goal.position)
                    };
                    records[next.id] = record;
                    open.Add(record);
                }
            }
        }

        if (!records.TryGetValue(goal.id, out SearchRecord goalRecord) ||
            !goalRecord.closed)
            return false;

        topologicalCost = goalRecord.g;
        scratchIds.Clear();
        string cursor = goal.id;
        int guard = 0;
        while (!string.IsNullOrEmpty(cursor) && guard++ < nodes.Count + 2)
        {
            scratchIds.Add(cursor);
            if (!records.TryGetValue(cursor, out SearchRecord record) ||
                string.IsNullOrEmpty(record.parentId))
                break;
            cursor = record.parentId;
        }
        scratchIds.Reverse();

        for (int i = 0; i < scratchIds.Count; i++)
        {
            if (!nodes.TryGetValue(scratchIds[i], out Node node) ||
                node == null || node.kind != NodeKind.Gate)
                continue;
            anchors.Add(node.position);
        }
        return true;
    }

    private void AddNode(Node node)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.id)) return;
        nodes[node.id] = node;
        if (!edges.ContainsKey(node.id))
            edges[node.id] = new List<Edge>(8);
    }

    private void AddBidirectionalEdge(string first, string second, float cost, bool viaGate)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second) ||
            string.Equals(first, second, StringComparison.Ordinal))
            return;
        AddEdge(first, second, cost, viaGate);
        AddEdge(second, first, cost, viaGate);
    }

    private void AddEdge(string from, string to, float cost, bool viaGate)
    {
        if (!edges.TryGetValue(from, out List<Edge> list)) return;
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i].to, to, StringComparison.Ordinal))
                return;
        list.Add(new Edge
        {
            to = to,
            baseCost = Mathf.Max(0.1f, cost),
            viaGate = viaGate
        });
        EdgeCount++;
    }

    private Node FindContainingArea(Vector3 point, BistroBuilderNavigationAgentMask agent)
    {
        Node nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < areaNodes.Count; i++)
        {
            Node node = areaNodes[i];
            if (node == null || (node.allowedAgents & agent) == 0) continue;
            if (node.area != null && node.area.ContainsPosition(point))
                return node;

            Vector3 closest = node.bounds.ClosestPoint(point);
            float distance = HorizontalDistance(point, closest);
            if (distance <= GateAssociationTolerance && distance < nearestDistance)
            {
                nearest = node;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    private static BistroBuilderNavigationAccessZone FindZone(
        IReadOnlyList<BistroBuilderNavigationAccessZone> zones,
        RestaurantArea area)
    {
        if (zones == null || area == null) return null;
        for (int i = 0; i < zones.Count; i++)
            if (zones[i] != null && ReferenceEquals(zones[i].Area, area))
                return zones[i];
        return null;
    }

    private static bool TryGetAreaBounds(RestaurantArea area, out Bounds bounds)
    {
        bounds = default;
        if (area == null || area.BoundaryColliders == null ||
            area.BoundaryColliders.Count == 0)
            return false;

        bool initialized = false;
        for (int i = 0; i < area.BoundaryColliders.Count; i++)
        {
            Collider collider = area.BoundaryColliders[i];
            if (collider == null || !collider.enabled) continue;
            if (!initialized)
            {
                bounds = collider.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }
        return initialized;
    }

    private static bool GateTouchesArea(
        BistroBuilderNavigationGateDescriptor gate,
        Node area,
        float tolerance)
    {
        if (gate == null || area == null || area.area == null) return false;
        Vector3 center = (gate.start + gate.end) * 0.5f;
        if (area.area.ContainsPosition(center) ||
            area.area.ContainsPosition(gate.start) ||
            area.area.ContainsPosition(gate.end))
            return true;

        Vector3 closest = area.bounds.ClosestPoint(center);
        return HorizontalDistance(center, closest) <= tolerance;
    }

    private static bool BoundsNear2D(Bounds first, Bounds second, float tolerance)
    {
        float dx = Mathf.Max(
            0f,
            Mathf.Max(first.min.x - second.max.x, second.min.x - first.max.x));
        float dz = Mathf.Max(
            0f,
            Mathf.Max(first.min.z - second.max.z, second.min.z - first.max.z));
        return dx <= tolerance && dz <= tolerance;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        float x = first.x - second.x;
        float z = first.z - second.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private static float Heuristic(Vector3 first, Vector3 second)
    {
        return HorizontalDistance(first, second);
    }

    private static int FindBestOpenIndex(List<SearchRecord> open)
    {
        int best = 0;
        for (int i = 1; i < open.Count; i++)
        {
            if (open[i].f < open[best].f - 0.0001f ||
                (Mathf.Abs(open[i].f - open[best].f) <= 0.0001f &&
                 string.CompareOrdinal(open[i].nodeId, open[best].nodeId) < 0))
                best = i;
        }
        return best;
    }

    private enum NodeKind
    {
        Area = 0,
        Gate = 1
    }

    private sealed class Node
    {
        public string id;
        public NodeKind kind;
        public RestaurantArea area;
        public BistroBuilderNavigationGateDescriptor gate;
        public Vector3 position;
        public Bounds bounds;
        public BistroBuilderNavigationAgentMask allowedAgents;
        public float traversalCost;
    }

    private sealed class Edge
    {
        public string to;
        public float baseCost;
        public bool viaGate;
    }

    private struct SearchRecord
    {
        public string nodeId;
        public string parentId;
        public float g;
        public float f;
        public bool closed;
    }
}