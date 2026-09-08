using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderEditDiagnosticSeverity { Info = 0, Risk = 1, Blocking = 2 }

[Serializable]
public sealed class BistroBuilderEditDiagnostic
{
    public string sourceSystem = "EditMode";
    public string code = string.Empty;
    public BistroBuilderEditDiagnosticSeverity severity;
    public BistroBuilderEditId targetId;
    public Vector2 location;
    public string message = string.Empty;
    public long draftRevision;
}

public sealed class BistroBuilderTopologyVertex
{
    public int vertexId;
    public Vector2 position;
    public readonly List<BistroBuilderEditId> incidentWalls = new List<BistroBuilderEditId>();
}

public sealed class BistroBuilderTopologySpan
{
    public BistroBuilderEditId sourceWallId;
    public int spanIndex;
    public int startVertexId;
    public int endVertexId;
    public Vector2 start;
    public Vector2 end;
    public float t0;
    public float t1;
}
public sealed class BistroBuilderWallTopologyProjection
{
    public readonly List<BistroBuilderTopologyVertex> vertices = new List<BistroBuilderTopologyVertex>();
    public readonly List<BistroBuilderTopologySpan> spans = new List<BistroBuilderTopologySpan>();
    public readonly List<BistroBuilderEditDiagnostic> diagnostics = new List<BistroBuilderEditDiagnostic>();
    public bool HasBlockingDiagnostics
    {
        get
        {
            for (int i = 0; i < diagnostics.Count; i++)
                if (diagnostics[i].severity == BistroBuilderEditDiagnosticSeverity.Blocking) return true;
            return false;
        }
    }
}

public sealed class BistroBuilderWallTopologyBuilder
{
    private readonly BistroBuilderArchitectureGeometryPolicy policy;
    public BistroBuilderWallTopologyBuilder(BistroBuilderArchitectureGeometryPolicy policy = null)
    { this.policy = policy ?? BistroBuilderArchitectureGeometryPolicy.Default; }

    private sealed class WorkWall
    {
        public BistroBuilderWallRecord wall;
        public readonly List<float> splits = new List<float> { 0f, 1f };
    }

    public BistroBuilderWallTopologyProjection Build(IReadOnlyList<BistroBuilderWallRecord> sourceWalls, long revision = 0)
    {
        var result = new BistroBuilderWallTopologyProjection();
        var work = new List<WorkWall>();
        if (sourceWalls == null) return result;
        var ordered = new List<BistroBuilderWallRecord>();
        for (int i = 0; i < sourceWalls.Count; i++) if (sourceWalls[i] != null) ordered.Add(sourceWalls[i]);
        ordered.Sort((a, b) => a.wallId.CompareTo(b.wallId));
        for (int i = 0; i < ordered.Count; i++)
        {
            var wall = ordered[i];
            if (!wall.wallId.IsValid || !policy.IsWallLengthValid(wall.axisStart, wall.axisEnd))
            {
                result.diagnostics.Add(new BistroBuilderEditDiagnostic
                {
                    code = "ARCH_WALL_DEGENERATE", severity = BistroBuilderEditDiagnosticSeverity.Blocking,
                    targetId = wall.wallId, location = wall.axisStart,
                    message = "La pared no tiene una longitud arquitectónica válida.", draftRevision = revision
                });
                continue;
            }
            work.Add(new WorkWall { wall = wall });
        }

        for (int i = 0; i < work.Count; i++)
        for (int j = i + 1; j < work.Count; j++)
        {
            if (!string.Equals(work[i].wall.buildPlaneId, work[j].wall.buildPlaneId, StringComparison.Ordinal)) continue;
            ProcessPair(work[i], work[j], result, revision);
        }

        for (int i = 0; i < work.Count; i++)
        {
            WorkWall current = work[i];
            NormalizeSplits(current.splits);
            int spanIndex = 0;
            for (int s = 0; s + 1 < current.splits.Count; s++)
            {
                float t0 = current.splits[s], t1 = current.splits[s + 1];
                if (t1 - t0 <= 0.000001f) continue;
                Vector2 a = Vector2.Lerp(current.wall.axisStart, current.wall.axisEnd, t0);
                Vector2 b = Vector2.Lerp(current.wall.axisStart, current.wall.axisEnd, t1);
                if (!policy.IsWallLengthValid(a, b)) continue;
                int va = FindOrAddVertex(result.vertices, a);
                int vb = FindOrAddVertex(result.vertices, b);
                AddIncidentWall(result.vertices[va], current.wall.wallId);
                AddIncidentWall(result.vertices[vb], current.wall.wallId);
                result.spans.Add(new BistroBuilderTopologySpan
                {
                    sourceWallId = current.wall.wallId, spanIndex = spanIndex++,
                    startVertexId = va, endVertexId = vb, start = a, end = b, t0 = t0, t1 = t1
                });
            }
        }
        return result;
    }

    private void ProcessPair(WorkWall a, WorkWall b, BistroBuilderWallTopologyProjection result, long revision)
    {
        Vector2 p = a.wall.axisStart, r = a.wall.axisEnd - a.wall.axisStart;
        Vector2 q = b.wall.axisStart, s = b.wall.axisEnd - b.wall.axisStart;
        float rxs = Cross(r, s);
        float qpxr = Cross(q - p, r);
        float eps = policy.pointTolerance;

        if (Mathf.Abs(rxs) <= eps && Mathf.Abs(qpxr) <= eps)
        {
            float rr = Vector2.Dot(r, r);
            if (rr <= 0f) return;
            float t0 = Vector2.Dot(q - p, r) / rr;
            float t1 = Vector2.Dot(q + s - p, r) / rr;
            float lo = Mathf.Max(0f, Mathf.Min(t0, t1));
            float hi = Mathf.Min(1f, Mathf.Max(t0, t1));
            if (hi - lo > eps / Mathf.Max(a.wall.Length, eps))
            {
                result.diagnostics.Add(new BistroBuilderEditDiagnostic
                {
                    code = "ARCH_WALL_COLLINEAR_OVERLAP", severity = BistroBuilderEditDiagnosticSeverity.Blocking,
                    targetId = a.wall.wallId, location = Vector2.Lerp(p, p + r, (lo + hi) * 0.5f),
                    message = "Dos paredes ocupan el mismo intervalo colineal.", draftRevision = revision
                });
            }
            return;
        }
        if (Mathf.Abs(rxs) <= eps) return;
        float t = Cross(q - p, s) / rxs;
        float u = Cross(q - p, r) / rxs;
        float ta = eps / Mathf.Max(a.wall.Length, eps);
        float tb = eps / Mathf.Max(b.wall.Length, eps);
        if (t < -ta || t > 1f + ta || u < -tb || u > 1f + tb) return;
        AddSplit(a.splits, Mathf.Clamp01(t));
        AddSplit(b.splits, Mathf.Clamp01(u));
    }

    private void AddSplit(List<float> values, float value)
    {
        for (int i = 0; i < values.Count; i++) if (Mathf.Abs(values[i] - value) <= 0.000001f) return;
        values.Add(value);
    }

    private static void NormalizeSplits(List<float> values)
    {
        values.Sort();
        for (int i = values.Count - 1; i > 0; i--)
            if (Mathf.Abs(values[i] - values[i - 1]) <= 0.000001f) values.RemoveAt(i);
    }

    private int FindOrAddVertex(List<BistroBuilderTopologyVertex> vertices, Vector2 point)
    {
        for (int i = 0; i < vertices.Count; i++) if (policy.SamePoint(vertices[i].position, point)) return i;
        int id = vertices.Count;
        vertices.Add(new BistroBuilderTopologyVertex { vertexId = id, position = point });
        return id;
    }

    private static void AddIncidentWall(BistroBuilderTopologyVertex vertex, BistroBuilderEditId wallId)
    {
        for (int i = 0; i < vertex.incidentWalls.Count; i++) if (vertex.incidentWalls[i] == wallId) return;
        vertex.incidentWalls.Add(wallId); vertex.incidentWalls.Sort();
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
public static class BistroBuilderOpeningIntrinsicValidator
{
    public static void Validate(BistroBuilderEditDocument document, BistroBuilderArchitectureGeometryPolicy policy,
        List<BistroBuilderEditDiagnostic> diagnostics, long revision)
    {
        if (document == null || diagnostics == null) return;
        policy = policy ?? BistroBuilderArchitectureGeometryPolicy.Default;
        for (int i = 0; i < document.openings.Count; i++)
        {
            var opening = document.openings[i];
            if (opening == null) continue;
            var wall = document.FindWall(opening.hostWallId);
            if (wall == null)
            {
                Add(diagnostics, "ARCH_OPENING_HOST_MISSING", opening.openingId, Vector2.zero,
                    "El opening no tiene una pared anfitriona válida.", revision); continue;
            }
            float center = Mathf.Clamp01(opening.axisPosition01) * wall.Length;
            float half = Mathf.Max(0f, opening.width) * 0.5f;
            float margin = policy.openingEdgeMargin;
            if (opening.width <= 0f || opening.height <= 0f || center - half < margin || center + half > wall.Length - margin)
                Add(diagnostics, "ARCH_OPENING_OUTSIDE_HOST", opening.openingId,
                    Vector2.Lerp(wall.axisStart, wall.axisEnd, opening.axisPosition01),
                    "El opening no cabe dentro de su pared anfitriona.", revision);
        }
        ValidateOverlaps(document, diagnostics, revision);
    }
    private static void ValidateOverlaps(BistroBuilderEditDocument document,
        List<BistroBuilderEditDiagnostic> diagnostics, long revision)
    {
        for (int i = 0; i < document.openings.Count; i++)
        for (int j = i + 1; j < document.openings.Count; j++)
        {
            var a = document.openings[i]; var b = document.openings[j];
            if (a == null || b == null || a.hostWallId != b.hostWallId) continue;
            var wall = document.FindWall(a.hostWallId); if (wall == null) continue;
            float ac = a.axisPosition01 * wall.Length, bc = b.axisPosition01 * wall.Length;
            if (Mathf.Abs(ac - bc) < (a.width + b.width) * 0.5f)
                Add(diagnostics, "ARCH_OPENING_OVERLAP", a.openingId,
                    Vector2.Lerp(wall.axisStart, wall.axisEnd, a.axisPosition01),
                    "Dos openings estructurales se solapan sobre la misma pared.", revision);
        }
    }

    private static void Add(List<BistroBuilderEditDiagnostic> diagnostics, string code,
        BistroBuilderEditId target, Vector2 location, string message, long revision)
    {
        diagnostics.Add(new BistroBuilderEditDiagnostic
        {
            code = code, severity = BistroBuilderEditDiagnosticSeverity.Blocking,
            targetId = target, location = location, message = message, draftRevision = revision
        });
    }
}
