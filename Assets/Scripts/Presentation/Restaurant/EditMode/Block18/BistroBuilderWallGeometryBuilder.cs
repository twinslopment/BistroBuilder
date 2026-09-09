using System;
using System.Collections.Generic;
using UnityEngine;

public static class BistroBuilderWallGeometryBuilder
{
    private readonly struct OpeningRect
    {
        public readonly float x0, x1, y0, y1;
        public OpeningRect(float x0, float x1, float y0, float y1)
        { this.x0 = x0; this.x1 = x1; this.y0 = y0; this.y1 = y1; }
    }

    public static Mesh Build(BistroBuilderWallRecord wall, IReadOnlyList<BistroBuilderOpeningRecord> hostedOpenings)
    {
        if (wall == null) throw new ArgumentNullException(nameof(wall));
        float length = wall.Length;
        float height = Mathf.Max(0.001f, wall.height);
        float thickness = Mathf.Max(0.001f, wall.thickness);
        var openings = CollectOpenings(wall, hostedOpenings, length, height);
        var xBreaks = new List<float> { 0f, length };
        for (int i = 0; i < openings.Count; i++)
        { xBreaks.Add(openings[i].x0); xBreaks.Add(openings[i].x1); }
        SortUnique(xBreaks);

        var vertices = new List<Vector3>(96);
        var normals = new List<Vector3>(96);
        var uv = new List<Vector2>(96);
        var triangles = new List<int>(144);
        for (int i = 0; i + 1 < xBreaks.Count; i++)
        {
            float x0 = xBreaks[i], x1 = xBreaks[i + 1];
            if (x1 - x0 <= 0.00001f) continue;
            AddSolidVerticalIntervals(x0, x1, height, thickness, openings, vertices, normals, uv, triangles);
        }
        var mesh = new Mesh { name = "BB_WallMesh_" + wall.wallId.Value };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateBounds(); return mesh;
    }

    private static List<OpeningRect> CollectOpenings(BistroBuilderWallRecord wall,
        IReadOnlyList<BistroBuilderOpeningRecord> source, float length, float height)
    {
        var result = new List<OpeningRect>(); if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
        {
            var o = source[i]; if (o == null || o.hostWallId != wall.wallId || o.width <= 0f || o.height <= 0f) continue;
            float center = Mathf.Clamp01(o.axisPosition01) * length;
            float x0 = Mathf.Clamp(center - o.width * 0.5f, 0f, length);
            float x1 = Mathf.Clamp(center + o.width * 0.5f, 0f, length);
            float y0 = Mathf.Clamp(o.bottomElevation, 0f, height);
            float y1 = Mathf.Clamp(o.bottomElevation + o.height, 0f, height);
            if (x1 - x0 > 0.00001f && y1 - y0 > 0.00001f) result.Add(new OpeningRect(x0,x1,y0,y1));
        }
        result.Sort((a,b) => a.x0.CompareTo(b.x0)); return result;
    }

    private static void SortUnique(List<float> values)
    {
        values.Sort();
        for (int i = values.Count - 1; i > 0; i--)
            if (Mathf.Abs(values[i] - values[i - 1]) <= 0.00001f) values.RemoveAt(i);
    }
    private static void AddSolidVerticalIntervals(float x0, float x1, float height, float thickness,
        List<OpeningRect> openings, List<Vector3> vertices, List<Vector3> normals,
        List<Vector2> uv, List<int> triangles)
    {
        float mid = (x0 + x1) * 0.5f;
        var blocked = new List<Vector2>();
        for (int i = 0; i < openings.Count; i++)
            if (mid > openings[i].x0 && mid < openings[i].x1)
                blocked.Add(new Vector2(openings[i].y0, openings[i].y1));
        blocked.Sort((a,b) => a.x.CompareTo(b.x));

        float cursor = 0f;
        for (int i = 0; i < blocked.Count; i++)
        {
            float start = Mathf.Clamp(blocked[i].x, 0f, height);
            float end = Mathf.Clamp(blocked[i].y, 0f, height);
            if (start > cursor + 0.00001f)
                AddBox(x0, x1, cursor, start, thickness, vertices, normals, uv, triangles);
            cursor = Mathf.Max(cursor, end);
        }
        if (cursor < height - 0.00001f)
            AddBox(x0, x1, cursor, height, thickness, vertices, normals, uv, triangles);
    }

    private static void AddBox(float x0, float x1, float y0, float y1, float thickness,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles)
    {
        float z0 = -thickness * 0.5f, z1 = thickness * 0.5f;
        AddFace(new Vector3(x0,y0,z1), new Vector3(x1,y0,z1), new Vector3(x1,y1,z1), new Vector3(x0,y1,z1), Vector3.forward, vertices,normals,uv,triangles);
        AddFace(new Vector3(x1,y0,z0), new Vector3(x0,y0,z0), new Vector3(x0,y1,z0), new Vector3(x1,y1,z0), Vector3.back, vertices,normals,uv,triangles);
        AddFace(new Vector3(x0,y0,z0), new Vector3(x0,y0,z1), new Vector3(x0,y1,z1), new Vector3(x0,y1,z0), Vector3.left, vertices,normals,uv,triangles);
        AddFace(new Vector3(x1,y0,z1), new Vector3(x1,y0,z0), new Vector3(x1,y1,z0), new Vector3(x1,y1,z1), Vector3.right, vertices,normals,uv,triangles);
        AddFace(new Vector3(x0,y1,z1), new Vector3(x1,y1,z1), new Vector3(x1,y1,z0), new Vector3(x0,y1,z0), Vector3.up, vertices,normals,uv,triangles);
        AddFace(new Vector3(x0,y0,z0), new Vector3(x1,y0,z0), new Vector3(x1,y0,z1), new Vector3(x0,y0,z1), Vector3.down, vertices,normals,uv,triangles);
    }

    private static void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
        float u = Vector3.Distance(a, b), v = Vector3.Distance(a, d);
        uv.Add(new Vector2(0f,0f)); uv.Add(new Vector2(u,0f)); uv.Add(new Vector2(u,v)); uv.Add(new Vector2(0f,v));
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }
}
