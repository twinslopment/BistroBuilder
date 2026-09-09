using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BistroBuilderEnclosedFaceCandidate
{
    public readonly List<Vector2> boundary = new List<Vector2>();
    public float area;
    public Vector2 centroid;
}

public sealed class BistroBuilderRoomProjection
{
    public BistroBuilderRoomRecord room;
    public readonly List<Vector2> boundary = new List<Vector2>();
    public float area;
    public Vector2 centroid;
}

public enum BistroBuilderRoomLineageKind { Split = 0, Merge = 1, Retired = 2 }

public sealed class BistroBuilderRoomLineageEvent
{
    public BistroBuilderRoomLineageKind kind;
    public readonly List<BistroBuilderEditId> predecessors = new List<BistroBuilderEditId>();
    public readonly List<BistroBuilderEditId> successors = new List<BistroBuilderEditId>();
}

public sealed class BistroBuilderRoomReconciliationResult
{
    public readonly List<BistroBuilderRoomProjection> projections = new List<BistroBuilderRoomProjection>();
    public readonly List<BistroBuilderEditId> created = new List<BistroBuilderEditId>();
    public readonly List<BistroBuilderEditId> retired = new List<BistroBuilderEditId>();
    public readonly List<BistroBuilderRoomLineageEvent> lineage = new List<BistroBuilderRoomLineageEvent>();
}
public sealed class BistroBuilderRoomFaceDetector
{
    private readonly BistroBuilderArchitectureGeometryPolicy policy;
    public BistroBuilderRoomFaceDetector(BistroBuilderArchitectureGeometryPolicy policy = null)
    { this.policy = policy ?? BistroBuilderArchitectureGeometryPolicy.Default; }

    public List<BistroBuilderEnclosedFaceCandidate> Detect(BistroBuilderWallTopologyProjection topology)
    {
        var faces = new List<BistroBuilderEnclosedFaceCandidate>();
        if (topology == null || topology.HasBlockingDiagnostics) return faces;
        var adjacency = new Dictionary<int, List<int>>();
        for (int i = 0; i < topology.spans.Count; i++)
        {
            var span = topology.spans[i];
            AddNeighbor(adjacency, span.startVertexId, span.endVertexId);
            AddNeighbor(adjacency, span.endVertexId, span.startVertexId);
        }
        foreach (var pair in adjacency)
        {
            int v = pair.Key;
            pair.Value.Sort((a,b) => Angle(topology.vertices[v].position, topology.vertices[a].position)
                .CompareTo(Angle(topology.vertices[v].position, topology.vertices[b].position)));
        }

        var visited = new HashSet<ulong>();
        for (int i = 0; i < topology.spans.Count; i++)
        {
            Trace(topology, adjacency, topology.spans[i].startVertexId, topology.spans[i].endVertexId, visited, faces);
            Trace(topology, adjacency, topology.spans[i].endVertexId, topology.spans[i].startVertexId, visited, faces);
        }
        faces.Sort((a,b) => a.centroid.x != b.centroid.x ? a.centroid.x.CompareTo(b.centroid.x) : a.centroid.y.CompareTo(b.centroid.y));
        return faces;
    }
    private void Trace(BistroBuilderWallTopologyProjection topology, Dictionary<int,List<int>> adjacency,
        int startU, int startV, HashSet<ulong> visited, List<BistroBuilderEnclosedFaceCandidate> faces)
    {
        ulong firstKey = EdgeKey(startU, startV); if (visited.Contains(firstKey)) return;
        var cycle = new List<int>(); int u = startU, v = startV;
        int guard = Math.Max(16, topology.spans.Count * 4);
        for (int step = 0; step < guard; step++)
        {
            ulong key = EdgeKey(u, v); if (visited.Contains(key) && !(u == startU && v == startV && step > 0)) return;
            visited.Add(key); cycle.Add(u);
            if (!adjacency.TryGetValue(v, out List<int> neighbors) || neighbors.Count == 0) return;
            int next = SelectClockwise(topology, v, u, neighbors);
            u = v; v = next;
            if (u == startU && v == startV) break;
        }
        if (cycle.Count < 3 || !(u == startU && v == startV)) return;
        float signedArea = SignedArea(topology, cycle);
        if (signedArea <= policy.minimumRoomArea) return;
        var face = new BistroBuilderEnclosedFaceCandidate { area = signedArea };
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < cycle.Count; i++) { Vector2 p = topology.vertices[cycle[i]].position; face.boundary.Add(p); sum += p; }
        face.centroid = PolygonCentroid(face.boundary, sum / cycle.Count);
        faces.Add(face);
    }

    private static int SelectClockwise(BistroBuilderWallTopologyProjection topology, int vertex, int previous, List<int> neighbors)
    {
        Vector2 center = topology.vertices[vertex].position;
        float reverse = Angle(center, topology.vertices[previous].position);
        int selected = neighbors[neighbors.Count - 1];
        for (int i = 0; i < neighbors.Count; i++)
        {
            float a = Angle(center, topology.vertices[neighbors[i]].position);
            if (a < reverse - 0.000001f) selected = neighbors[i]; else break;
        }
        return selected;
    }
    private static void AddNeighbor(Dictionary<int,List<int>> adjacency, int a, int b)
    {
        if (!adjacency.TryGetValue(a, out List<int> list)) { list = new List<int>(); adjacency.Add(a, list); }
        if (!list.Contains(b)) list.Add(b);
    }
    private static float Angle(Vector2 a, Vector2 b) => Mathf.Atan2(b.y - a.y, b.x - a.x);
    private static ulong EdgeKey(int a, int b) => ((ulong)(uint)a << 32) | (uint)b;
    private static float SignedArea(BistroBuilderWallTopologyProjection topology, List<int> cycle)
    {
        double area = 0d;
        for (int i = 0; i < cycle.Count; i++)
        {
            Vector2 a = topology.vertices[cycle[i]].position;
            Vector2 b = topology.vertices[cycle[(i + 1) % cycle.Count]].position;
            area += (double)a.x * b.y - (double)b.x * a.y;
        }
        return (float)(area * 0.5d);
    }
    public static Vector2 PolygonCentroid(IReadOnlyList<Vector2> polygon, Vector2 fallback)
    {
        if (polygon == null || polygon.Count < 3) return fallback;
        double area2 = 0d, cx = 0d, cy = 0d;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            double cross = (double)a.x * b.y - (double)b.x * a.y;
            area2 += cross; cx += (a.x + b.x) * cross; cy += (a.y + b.y) * cross;
        }
        if (Math.Abs(area2) < 1e-9) return fallback;
        return new Vector2((float)(cx / (3d * area2)), (float)(cy / (3d * area2)));
    }
}
public sealed class BistroBuilderRoomIdentityReconciler
{
    public BistroBuilderRoomReconciliationResult Reconcile(
        IReadOnlyList<BistroBuilderRoomProjection> previous,
        IReadOnlyList<BistroBuilderEnclosedFaceCandidate> currentFaces,
        string buildPlaneId, long revision)
    {
        var result = new BistroBuilderRoomReconciliationResult();
        var claimedOld = new HashSet<string>();
        var usedByFace = new List<List<BistroBuilderRoomProjection>>();
        for (int i = 0; i < currentFaces.Count; i++) usedByFace.Add(new List<BistroBuilderRoomProjection>());

        if (previous != null)
        {
            for (int i = 0; i < previous.Count; i++)
            {
                var old = previous[i]; if (old == null || old.room == null) continue;
                for (int f = 0; f < currentFaces.Count; f++)
                    if (PointInPolygon(old.room.identityAnchor, currentFaces[f].boundary)) usedByFace[f].Add(old);
            }
        }

        for (int f = 0; f < currentFaces.Count; f++)
        {
            var face = currentFaces[f]; var candidates = usedByFace[f];
            candidates.Sort((a,b) => a.room.roomId.CompareTo(b.room.roomId));
            BistroBuilderRoomRecord room;
            if (candidates.Count > 0)
            {
                room = candidates[0].room.DeepClone(); claimedOld.Add(room.roomId.Value);
                if (!PointInPolygon(room.identityAnchor, face.boundary)) room.identityAnchor = face.centroid;
                for (int c = 1; c < candidates.Count; c++) claimedOld.Add(candidates[c].room.roomId.Value);
                if (candidates.Count > 1) AddMergeLineage(result, candidates, room.roomId);
            }
            else
            {
                room = new BistroBuilderRoomRecord
                {
                    roomId = BistroBuilderEditId.NewId(), buildPlaneId = buildPlaneId,
                    identityAnchor = face.centroid, createdRevision = revision
                };
                result.created.Add(room.roomId);
            }
            var projection = new BistroBuilderRoomProjection { room = room, area = face.area, centroid = face.centroid };
            projection.boundary.AddRange(face.boundary); result.projections.Add(projection);
        }

        if (previous != null)
        {
            for (int i = 0; i < previous.Count; i++)
            {
                var old = previous[i]; if (old == null || old.room == null || claimedOld.Contains(old.room.roomId.Value)) continue;
                int bestFace = FindContainingOldCentroid(old, currentFaces);
                if (bestFace >= 0 && result.projections[bestFace].room.createdRevision == revision)
                {
                    BistroBuilderEditId newlyCreated = result.projections[bestFace].room.roomId;
                    result.created.Remove(newlyCreated);
                    result.projections[bestFace].room = old.room.DeepClone();
                    claimedOld.Add(old.room.roomId.Value);
                }
                else
                {
                    result.retired.Add(old.room.roomId);
                    var e = new BistroBuilderRoomLineageEvent { kind = BistroBuilderRoomLineageKind.Retired };
                    e.predecessors.Add(old.room.roomId); result.lineage.Add(e);
                }
            }
        }
        AddSplitLineage(previous, result);
        return result;
    }

    private static int FindContainingOldCentroid(BistroBuilderRoomProjection old,
        IReadOnlyList<BistroBuilderEnclosedFaceCandidate> faces)
    {
        for (int i = 0; i < faces.Count; i++) if (PointInPolygon(old.centroid, faces[i].boundary)) return i;
        return -1;
    }
    private static void AddMergeLineage(BistroBuilderRoomReconciliationResult result,
        List<BistroBuilderRoomProjection> predecessors, BistroBuilderEditId successor)
    {
        var e = new BistroBuilderRoomLineageEvent { kind = BistroBuilderRoomLineageKind.Merge };
        for (int i = 0; i < predecessors.Count; i++)
        {
            e.predecessors.Add(predecessors[i].room.roomId);
            if (predecessors[i].room.roomId != successor) result.retired.Add(predecessors[i].room.roomId);
        }
        e.successors.Add(successor); result.lineage.Add(e);
    }

    private static void AddSplitLineage(IReadOnlyList<BistroBuilderRoomProjection> previous,
        BistroBuilderRoomReconciliationResult result)
    {
        if (previous == null) return;
        for (int p = 0; p < previous.Count; p++)
        {
            var old = previous[p]; if (old == null || old.room == null) continue;
            var successors = new List<BistroBuilderEditId>();
            for (int n = 0; n < result.projections.Count; n++)
                if (PointInPolygon(result.projections[n].room.identityAnchor, old.boundary)) successors.Add(result.projections[n].room.roomId);
            if (successors.Count <= 1) continue;
            var e = new BistroBuilderRoomLineageEvent { kind = BistroBuilderRoomLineageKind.Split };
            e.predecessors.Add(old.room.roomId); e.successors.AddRange(successors); result.lineage.Add(e);
        }
    }

    public static bool PointInPolygon(Vector2 p, IReadOnlyList<Vector2> polygon)
    {
        if (polygon == null || polygon.Count < 3) return false;
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            bool crosses = ((a.y > p.y) != (b.y > p.y)) &&
                p.x < (b.x - a.x) * (p.y - a.y) / ((b.y - a.y) + 1e-12f) + a.x;
            if (crosses) inside = !inside;
        }
        return inside;
    }
}
