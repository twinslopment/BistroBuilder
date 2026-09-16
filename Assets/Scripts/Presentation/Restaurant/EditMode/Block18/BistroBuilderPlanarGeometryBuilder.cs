using System;
using System.Collections.Generic;
using UnityEngine;

public static class BistroBuilderPlanarGeometryBuilder
{
    private const float DuplicatePointSqrEpsilon = 1e-10f;
    private const float CollinearSinSqrEpsilon = 1e-10f;
    private const float AreaEpsilon = 1e-7f;

    public static Mesh BuildHorizontalPolygon(IReadOnlyList<Vector2> boundary, float elevation = 0f)
    {
        if (boundary == null) throw new ArgumentNullException(nameof(boundary));

        if (TryBuildHorizontalPolygon(boundary, elevation, out Mesh mesh))
            return mesh;

        Debug.LogWarning(
            "[BB 18N] Se omitió un polígono arquitectónico no triangulable durante la representación visual.");
        return new Mesh { name = "BB_PlanarPolygon_Invalid" };
    }

    public static bool TryBuildHorizontalPolygon(
        IReadOnlyList<Vector2> boundary,
        float elevation,
        out Mesh mesh)
    {
        mesh = null;
        if (boundary == null || boundary.Count < 3) return false;

        List<Vector2> points = SanitizeBoundary(boundary);
        if (points.Count < 3 || Mathf.Abs(SignedArea(points)) <= AreaEpsilon)
            return false;

        var triangles = new List<int>((points.Count - 2) * 3);
        if (!TryTriangulate(points, triangles))
            return false;

        // Counter-clockwise XY polygons point down after mapping Y to world Z.
        // Reverse their winding so the floor is visible from above with backface culling.
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int swap = triangles[i + 1];
            triangles[i + 1] = triangles[i + 2];
            triangles[i + 2] = swap;
        }

        var vertices = new List<Vector3>(points.Count);
        var normals = new List<Vector3>(points.Count);
        var uv = new List<Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = points[i];
            vertices.Add(new Vector3(p.x, elevation, p.y));
            normals.Add(Vector3.up);
            uv.Add(p);
        }

        mesh = new Mesh { name = "BB_PlanarPolygon" };
        if (vertices.Count > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateBounds();
        return true;
    }

    private static List<Vector2> SanitizeBoundary(IReadOnlyList<Vector2> boundary)
    {
        var points = new List<Vector2>(boundary.Count);
        for (int i = 0; i < boundary.Count; i++)
        {
            Vector2 point = boundary[i];
            if (!IsFinite(point)) return new List<Vector2>();
            if (points.Count == 0 ||
                (points[points.Count - 1] - point).sqrMagnitude > DuplicatePointSqrEpsilon)
                points.Add(point);
        }

        if (points.Count >= 2 &&
            (points[0] - points[points.Count - 1]).sqrMagnitude <= DuplicatePointSqrEpsilon)
            points.RemoveAt(points.Count - 1);

        bool removed;
        do
        {
            removed = false;
            if (points.Count < 3) break;

            for (int i = 0; i < points.Count; i++)
            {
                int previous = (i - 1 + points.Count) % points.Count;
                int next = (i + 1) % points.Count;
                Vector2 incoming = points[i] - points[previous];
                Vector2 outgoing = points[next] - points[i];
                float incomingSqr = incoming.sqrMagnitude;
                float outgoingSqr = outgoing.sqrMagnitude;

                if (incomingSqr <= DuplicatePointSqrEpsilon ||
                    outgoingSqr <= DuplicatePointSqrEpsilon)
                {
                    points.RemoveAt(i);
                    removed = true;
                    break;
                }

                float cross = Cross(incoming, outgoing);
                float sinSqr = (cross * cross) / (incomingSqr * outgoingSqr);
                if (sinSqr <= CollinearSinSqrEpsilon && Vector2.Dot(incoming, outgoing) > 0f)
                {
                    points.RemoveAt(i);
                    removed = true;
                    break;
                }
            }
        }
        while (removed);

        return points;
    }

    private static bool TryTriangulate(List<Vector2> points, List<int> result)
    {
        int count = points.Count;
        result.Clear();
        if (count < 3) return false;

        var indices = new List<int>(count);
        bool ccw = SignedArea(points) > 0f;
        for (int i = 0; i < count; i++)
            indices.Add(ccw ? i : count - 1 - i);

        int guard = count * count;
        while (indices.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int ia = indices[(i - 1 + indices.Count) % indices.Count];
                int ib = indices[i];
                int ic = indices[(i + 1) % indices.Count];
                Vector2 a = points[ia];
                Vector2 b = points[ib];
                Vector2 c = points[ic];
                if (Cross(b - a, c - b) <= AreaEpsilon) continue;

                bool contains = false;
                for (int j = 0; j < indices.Count; j++)
                {
                    int candidate = indices[j];
                    if (candidate == ia || candidate == ib || candidate == ic) continue;
                    if (PointInTriangle(points[candidate], a, b, c))
                    {
                        contains = true;
                        break;
                    }
                }
                if (contains) continue;

                result.Add(ia);
                result.Add(ib);
                result.Add(ic);
                indices.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped) return false;
        }

        if (indices.Count != 3) return false;
        result.Add(indices[0]);
        result.Add(indices[1]);
        result.Add(indices[2]);
        return result.Count == (count - 2) * 3;
    }

    private static bool IsFinite(Vector2 point)
    {
        return !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
               !float.IsNaN(point.y) && !float.IsInfinity(point.y);
    }

    private static float SignedArea(IReadOnlyList<Vector2> points)
    {
        double area = 0d;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            area += (double)a.x * b.y - (double)b.x * a.y;
        }
        return (float)(area * 0.5d);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float c1 = Cross(b - a, p - a);
        float c2 = Cross(c - b, p - b);
        float c3 = Cross(a - c, p - c);
        const float epsilon = 1e-7f;
        bool negative = c1 < -epsilon || c2 < -epsilon || c3 < -epsilon;
        bool positive = c1 > epsilon || c2 > epsilon || c3 > epsilon;
        return !(negative && positive);
    }
}
