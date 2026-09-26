using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable] public sealed class BistroBuilderTopBarArtEntry
{
    public string texture;
    public float x, y, width, height;
    public float[] outline;
    public float dx, dy, rotation, zoom = 1.06f, duration = .9f;
}
[Serializable] public sealed class BistroBuilderTopBarArtCatalog
{
    public BistroBuilderTopBarArtEntry[] entries;
}

/// <summary>Clips the original artwork with authored UI geometry. Source pixels are never altered.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BistroBuilderTopBarArtwork : MaskableGraphic
{
    private Texture2D art;
    private Vector2[] points;
    private int[] triangles;
    private Rect uv;
    public float Aspect { get; private set; }
    public override Texture mainTexture => art != null ? art : base.mainTexture;
    public void Configure(BistroBuilderTopBarArtEntry entry)
    {
        art = Resources.Load<Texture2D>("BistroBuilder/UI/TopBar/Parts/" + entry.texture);
        if (art == null) return;
        float w = entry.width > 0 ? entry.width : art.width;
        float h = entry.height > 0 ? entry.height : art.height;
        Aspect = w / h;
        uv = new Rect(entry.x / art.width, 1 - (entry.y + h) / art.height, w / art.width, h / art.height);
        points = new Vector2[entry.outline.Length / 2];
        for (int i = 0; i < points.Length; i++)
            points[i] = new Vector2(entry.outline[i * 2] / w, 1 - entry.outline[i * 2 + 1] / h);
        triangles = Triangulate(points);
        raycastTarget = false;
        SetAllDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (points == null) return;
        Rect r = rectTransform.rect;
        foreach (Vector2 p in points)
            mesh.AddVert(new Vector3(r.x + p.x * r.width, r.y + p.y * r.height), color,
                new Vector2(uv.x + p.x * uv.width, uv.y + p.y * uv.height));
        for (int i = 0; i < triangles.Length; i += 3) mesh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }
    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    private static int[] Triangulate(Vector2[] p)
    {
        var indices = new List<int>();
        float area = 0;
        for (int i = 0; i < p.Length; i++) area += Cross(p[i], p[(i + 1) % p.Length]);
        for (int i = 0; i < p.Length; i++) indices.Add(area > 0 ? i : p.Length - i - 1);
        var result = new List<int>();
        int guard = p.Length * p.Length;
        while (indices.Count > 2 && guard-- > 0)
        {
            bool found = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int a = indices[(i + indices.Count - 1) % indices.Count], b = indices[i], c = indices[(i + 1) % indices.Count];
                if (Cross(p[b] - p[a], p[c] - p[b]) <= .000001f) continue;
                bool inside = false;
                foreach (int v in indices)
                {
                    if (v == a || v == b || v == c) continue;
                    if (Cross(p[b] - p[a], p[v] - p[a]) >= 0 && Cross(p[c] - p[b], p[v] - p[b]) >= 0 && Cross(p[a] - p[c], p[v] - p[c]) >= 0) { inside = true; break; }
                }
                if (inside) continue;
                result.Add(a); result.Add(b); result.Add(c); indices.RemoveAt(i); found = true; break;
            }
            if (!found) break;
        }
        return result.ToArray();
    }
}
