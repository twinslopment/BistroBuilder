using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution-independent brass silhouettes. No rectangular image or background.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BistroBuilderOpeningActionIcon : MaskableGraphic
{
    public enum Shape { BackArrow, ServiceBell }
    [SerializeField] private Shape shape;
    public void Configure(Shape value) { shape = value; raycastTarget = false; SetVerticesDirty(); }
    private static readonly Color32 Rim = new Color32(103, 63, 21, 255);
    private static readonly Color32 Shadow = new Color32(53, 39, 23, 80);

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (shape == Shape.BackArrow) Arrow(mesh); else Bell(mesh);
    }
    private void Arrow(VertexHelper mesh)
    {
        var outer = new[] { new Vector2(.07f,.50f), new Vector2(.44f,.90f), new Vector2(.44f,.68f),
            new Vector2(.94f,.68f), new Vector2(.94f,.32f), new Vector2(.44f,.32f), new Vector2(.44f,.10f) };
        Polygon(mesh, outer, Shadow, new Vector2(0,-.035f));
        Polygon(mesh, outer, Rim);
        Polygon(mesh, new[] { new Vector2(.13f,.50f), new Vector2(.395f,.795f), new Vector2(.395f,.625f),
            new Vector2(.885f,.625f), new Vector2(.885f,.385f), new Vector2(.395f,.385f), new Vector2(.395f,.205f) }, null);
        Polygon(mesh, new[] { new Vector2(.16f,.51f), new Vector2(.405f,.78f), new Vector2(.405f,.75f), new Vector2(.18f,.50f) }, new Color32(255,238,181,230));
        Quad(mesh,.445f,.599f,.875f,.62f,new Color32(255,236,171,220));
    }
    private void Bell(VertexHelper mesh)
    {
        Ellipse(mesh, new Vector2(.5f,.115f), .445f,.085f, Shadow);
        Ellipse(mesh, new Vector2(.5f,.155f), .43f,.087f, new Color32(54,35,18,255));
        Ellipse(mesh, new Vector2(.5f,.192f), .405f,.056f, null);
        Quad(mesh,.465f,.725f,.535f,.855f,Rim);
        Ellipse(mesh,new Vector2(.5f,.855f),.068f,.062f,Rim);
        Ellipse(mesh,new Vector2(.49f,.87f),.05f,.042f,null);
        Dome(mesh,.355f,.49f,.25f,Rim);
        Dome(mesh,.325f,.455f,.27f,null);
        Ellipse(mesh,new Vector2(.5f,.265f),.363f,.042f,Rim);
        Ellipse(mesh,new Vector2(.5f,.287f),.345f,.025f,null);
        // Narrow reflected highlight follows the curved brass surface.
        var shine = new List<Vector2>();
        for(int i=0;i<=12;i++) { float a = Mathf.Lerp(1.85f,2.45f,i/12f); shine.Add(new Vector2(.5f+Mathf.Cos(a)*.28f,.29f+Mathf.Sin(a)*.40f)); }
        for(int i=12;i>=0;i--) { float a = Mathf.Lerp(1.85f,2.45f,i/12f); shine.Add(new Vector2(.5f+Mathf.Cos(a)*.25f,.29f+Mathf.Sin(a)*.39f)); }
        Strip(mesh,shine,new Color32(255,246,201,170));
    }
    private void Dome(VertexHelper mesh,float rx,float ry,float bottom,Color32? tint)
    {
        var points=new List<Vector2> { new Vector2(.5f,bottom) };
        for(int i=0;i<=40;i++) { float angle=Mathf.PI*i/40; points.Add(new Vector2(.5f+Mathf.Cos(angle)*rx,bottom+Mathf.Sin(angle)*ry)); }
        Polygon(mesh,points,tint);
    }
    private void Ellipse(VertexHelper mesh,Vector2 center,float rx,float ry,Color32? tint)
    {
        var points=new List<Vector2> { center };
        for(int i=0;i<=48;i++) { float angle=Mathf.PI*2*i/48; points.Add(center+new Vector2(Mathf.Cos(angle)*rx,Mathf.Sin(angle)*ry)); }
        Polygon(mesh,points,tint);
    }
    private void Quad(VertexHelper mesh,float x0,float y0,float x1,float y1,Color32 tint) =>
        Polygon(mesh,new[]{new Vector2(x0,y0),new Vector2(x0,y1),new Vector2(x1,y1),new Vector2(x1,y0)},tint);
    private void Polygon(VertexHelper mesh,IList<Vector2> points,Color32? tint,Vector2 offset=default)
    {
        int start=mesh.currentVertCount;
        foreach(var point in points) Vertex(mesh,point+offset,tint);
        for(int i=1;i<points.Count-1;i++) mesh.AddTriangle(start,start+i,start+i+1);
    }
    private void Strip(VertexHelper mesh,IList<Vector2> points,Color32 tint)
    {
        int start=mesh.currentVertCount, half=points.Count/2;
        foreach(var point in points) Vertex(mesh,point,tint);
        for(int i=0;i<half-1;i++) { int a=start+i,b=start+i+1,c=start+points.Count-1-i,d=c-1; mesh.AddTriangle(a,b,c);mesh.AddTriangle(b,d,c); }
    }
    private void Vertex(VertexHelper mesh,Vector2 point,Color32? tint)
    {
        Rect rect=rectTransform.rect;
        Color shaded=tint.HasValue?(Color)tint.Value:Brass(point);
        shaded*=color;
        mesh.AddVert(new Vector3(rect.x+point.x*rect.width,rect.y+point.y*rect.height),shaded,Vector2.zero);
    }
    private static Color Brass(Vector2 point)
    {
        var dark=new Color32(143,81,16,255); var gold=new Color32(230,168,56,255); var light=new Color32(255,225,149,255);
        Color tone=point.x<.40f?Color.Lerp(gold,light,Mathf.InverseLerp(.10f,.40f,point.x)):
            Color.Lerp(light,dark,Mathf.InverseLerp(.40f,.92f,point.x));
        return Color.Lerp(tone,new Color32(113,66,15,255),Mathf.Clamp01((.5f-point.y)*.7f));
    }
}
