using UnityEngine;
using UnityEngine.UI;

/// <summary>Small rounded mesh; no per-frame textures, materials, outlines or layout rebuilds.</summary>
public sealed class BistroBuilderStateGraphic : MaskableGraphic
{
    public BistroBuilderSurfaceState State;
    public bool Focus, Card, Navigation;
    public static readonly Color Gold = new Color(0.82f, 0.64f, 0.28f);
    public static readonly Color Green = new Color(0.40f, 0.59f, 0.29f);
    public static readonly Color Blue = new Color(0.09f, 0.67f, 1f);
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); Rect rect = rectTransform.rect;
        if (State == BistroBuilderSurfaceState.Selected && !Navigation)
        {
            Fill(vh, Inset(rect, 4), new Color(0.49f,0.63f,0.30f,0.94f), new Color(0.20f,0.40f,0.26f,0.94f));
            Ring(vh, Inset(rect, 3), 1, Gold);
        }
        if (State == BistroBuilderSurfaceState.Hover) Ring(vh, Inset(rect, 3), 1, new Color(1,0.96f,0.89f,0.16f));
        if (State == BistroBuilderSurfaceState.Disabled)
        {
            Fill(vh, Inset(rect, 4), new Color(0.54f,0.54f,0.52f,0.55f), new Color(0.43f,0.43f,0.42f,0.55f));
            Ring(vh, Inset(rect, 3), 1.5f, new Color(0.65f,0.65f,0.63f,0.7f));
        }
        if (Focus)
        {
            Ring(vh, rect, 3, new Color(Blue.r,Blue.g,Blue.b,0.22f));
            Ring(vh, Inset(rect, 1.5f), 2.5f, Blue);
        }
    }
    internal static Rect Inset(Rect r, float x) => new Rect(r.x+x,r.y+x,Mathf.Max(0,r.width-x*2),Mathf.Max(0,r.height-x*2));
    private static Vector2 Point(Rect r, int index)
    {
        int corner = index / 8; float a = (corner * 90 + index % 8 * 90f / 7) * Mathf.Deg2Rad;
        float radius = Mathf.Min(7, r.width * 0.5f, r.height * 0.5f);
        var center = new Vector2(corner == 0 || corner == 3 ? r.xMax-radius : r.xMin+radius,
            corner < 2 ? r.yMax-radius : r.yMin+radius);
        return center + new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
    }
    internal static void Ring(VertexHelper vh, Rect r, float width, Color color)
    {
        int start = vh.currentVertCount; Rect inner = Inset(r,width);
        for (int i=0;i<32;i++) { vh.AddVert(Point(r,i),color,Vector2.zero); vh.AddVert(Point(inner,i),color,Vector2.zero); }
        for (int i=0;i<32;i++) { int a=start+i*2,b=start+((i+1)%32)*2; vh.AddTriangle(a,b,a+1); vh.AddTriangle(a+1,b,b+1); }
    }
    private static void Fill(VertexHelper vh, Rect r, Color top, Color bottom)
    {
        int start=vh.currentVertCount; vh.AddVert(r.center,Color.Lerp(bottom,top,0.5f),Vector2.zero);
        for(int i=0;i<32;i++) {var p=Point(r,i);vh.AddVert(p,Color.Lerp(bottom,top,Mathf.InverseLerp(r.yMin,r.yMax,p.y)),Vector2.zero);}
        for(int i=0;i<32;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%32);
    }
}
