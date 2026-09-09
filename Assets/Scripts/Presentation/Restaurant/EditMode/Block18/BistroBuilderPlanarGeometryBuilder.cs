using System;
using System.Collections.Generic;
using UnityEngine;

public static class BistroBuilderPlanarGeometryBuilder
{
    public static Mesh BuildHorizontalPolygon(IReadOnlyList<Vector2> boundary, float elevation = 0f)
    {
        if (boundary == null) throw new ArgumentNullException(nameof(boundary));
        if (boundary.Count < 3) throw new ArgumentException("El polígono necesita al menos tres puntos.", nameof(boundary));

        var points = new List<Vector2>(boundary.Count);
        for (int i = 0; i < boundary.Count; i++)
        {
            Vector2 p = boundary[i];
            if (points.Count == 0 || (points[points.Count - 1] - p).sqrMagnitude > 1e-10f)
                points.Add(p);
        }
        if (points.Count >= 2 && (points[0] - points[points.Count - 1]).sqrMagnitude <= 1e-10f)
            points.RemoveAt(points.Count - 1);
        if (points.Count < 3) throw new ArgumentException("El polígono está degenerado.", nameof(boundary));

        var triangles = Triangulate(points);
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

        var mesh = new Mesh { name = "BB_PlanarPolygon" };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static List<int> Triangulate(List<Vector2> points)
    {
        int count = points.Count;
        var result = new List<int>((count - 2) * 3);
        var indices = new List<int>(count);
        bool ccw = SignedArea(points) > 0f;
        for (int i = 0; i < count; i++) indices.Add(ccw ? i : count - 1 - i);

        int guard = count * count;
        while (indices.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int ia = indices[(i - 1 + indices.Count) % indices.Count];
                int ib = indices[i];
                int ic = indices[(i + 1) % indices.Count];
                Vector2 a = points[ia], b = points[ib], c = points[ic];
                if (Cross(b - a, c - b) <= 1e-8f) continue;

                bool contains = false;
                for (int j = 0; j < indices.Count; j++)
                {
                    int candidate = indices[j];
                    if (candidate == ia || candidate == ib || candidate == ic) continue;
                    if (PointInTriangle(points[candidate], a, b, c)) { contains = true; break; }
                }
                if (contains) continue;

                result.Add(ia); result.Add(ib); result.Add(ic);
                indices.RemoveAt(i); clipped = true; break;
            }
            if (!clipped) break;
        }

        if (indices.Count == 3)
        {
            result.Add(indices[0]); result.Add(indices[1]); result.Add(indices[2]);
        }
        if (result.Count != (count - 2) * 3)
            throw new InvalidOperationException("No se pudo triangular el polígono arquitectónico sin ambigüedad.");
        return result;
    }
    private static float SignedArea(IReadOnlyList<Vector2> points)
    {
        double area = 0d;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Count];
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
