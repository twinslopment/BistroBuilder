using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum BistroBuilderEditChromeSymbol
{
    Home, Pencil, Undo, Redo, Hand, Move, Grid, Terrain, Paint,
    Sun, Play, Plot, Chair, Surfaces, Walls, Plant, Bulb, Settings, More, Delete, Rotate, Duplicate, Cursor, Hammer, Layers, Magnet, Camera, Tools, Down, Spark
}

/// <summary>Original, resolution-independent silhouettes from the approved edit-mode reference.
/// Approved silhouettes use a cached native SDF vector font; remaining symbols use a 32-unit mesh.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BistroBuilderEditChromeIcon : MaskableGraphic
{
    public BistroBuilderEditChromeSymbol Symbol { get; private set; }
    VertexHelper mesh; Color ink;
    TMP_Text approved;
    bool toolbarDelete;
    public bool UsesApprovedVector => approved != null;
    static string ApprovedKey(BistroBuilderEditChromeSymbol symbol)
    {
        switch(symbol)
        {
            case BistroBuilderEditChromeSymbol.Tools:return "tools";
            case BistroBuilderEditChromeSymbol.Cursor:return "cursor";
            case BistroBuilderEditChromeSymbol.Chair:return "chair";
            case BistroBuilderEditChromeSymbol.Hammer:return "hammer";
            case BistroBuilderEditChromeSymbol.Layers:return "layers";
            case BistroBuilderEditChromeSymbol.Move:return "move";
            case BistroBuilderEditChromeSymbol.Rotate:return "rotate";
            case BistroBuilderEditChromeSymbol.Duplicate:return "duplicate";
            case BistroBuilderEditChromeSymbol.Delete:return "trash";
            case BistroBuilderEditChromeSymbol.Magnet:return "magnet";
            case BistroBuilderEditChromeSymbol.Grid:return "grid";
            case BistroBuilderEditChromeSymbol.Plot:return "cube";
            case BistroBuilderEditChromeSymbol.Undo:return "undo";
            case BistroBuilderEditChromeSymbol.Redo:return "redo";
            case BistroBuilderEditChromeSymbol.Sun:return "sun";
            case BistroBuilderEditChromeSymbol.Camera:return "camera";
            case BistroBuilderEditChromeSymbol.Settings:return "gear";
            case BistroBuilderEditChromeSymbol.Pencil:return "pencil";
            default:return null;
        }
    }
    void LateUpdate(){if(approved!=null)approved.color=color;}
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        FitApprovedGlyph();
    }
    void FitApprovedGlyph()
    {
        if(approved==null || !BistroBuilderApprovedGlyphsR4.Resolve(ApprovedKey(Symbol),out _,out float aspect))return;
        if(toolbarDelete)aspect=24f/28f;
        var rect=approved.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        approved.fontSize=Mathf.Min(rectTransform.rect.height,rectTransform.rect.width/aspect);
    }

    public void ConfigureToolbarGlyph()
    {
        if(Symbol!=BistroBuilderEditChromeSymbol.Delete || approved==null)return;
        var font=BistroBuilderEditorV2ToolbarArt.DeleteFont;if(font==null)return;
        toolbarDelete=true;approved.font=font;FitApprovedGlyph();
    }

    public void Configure(BistroBuilderEditChromeSymbol symbol, Color tint)
    {
        Symbol=symbol;color=tint;raycastTarget=false;
        string key=ApprovedKey(symbol);
        if(key!=null && approved==null)
            approved=BistroBuilderApprovedGlyphsR4.Create(transform,"ApprovedVectorGlyph",key,0,0,rectTransform.rect.width,rectTransform.rect.height,tint);
        FitApprovedGlyph(); SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        mesh = vh; ink = color; vh.Clear();
        if(approved!=null)return;
        switch (Symbol)
        {
            case BistroBuilderEditChromeSymbol.Down: Stroke(6,11,16,21,26,11);break;
            case BistroBuilderEditChromeSymbol.Spark: Poly(16,1,21,11,31,16,21,21,16,31,11,21,1,16,11,11);break;
            case BistroBuilderEditChromeSymbol.Cursor:
                Poly(5,1,28,19,18,20,23,29,17,32,12,23,5,29); break;
            case BistroBuilderEditChromeSymbol.Hammer:
                Poly(3,8,10,1,15,4,18,2,27,11,22,17,17,13,13,14,8,9);
                Poly(13,13,19,18,8,31,2,26); break;
            case BistroBuilderEditChromeSymbol.Layers:
                Poly(16,2,31,10,16,18,1,10); Poly(1,16,5,14,16,20,27,14,31,16,16,24);
                Poly(1,23,5,21,16,27,27,21,31,23,16,31); break;
            case BistroBuilderEditChromeSymbol.Magnet:
                Arc(16,17,10,0,180,7); Line(6,17,6,4,7); Line(26,17,26,4,7);
                CutLine(3,9,9,9); CutLine(23,9,29,9); break;
            case BistroBuilderEditChromeSymbol.Camera:
                Poly(2,8,9,8,12,3,22,3,25,8,30,8,30,29,2,29);
                var cameraInk=ink;ink=new Color32(250,242,226,255);Arc(17,18,6,0,360,2);ink=cameraInk; CutLine(5,11,8,11); break;
            case BistroBuilderEditChromeSymbol.Tools:
                Line(5,29,24,8,5); Poly(19,4,23,1,23,7,28,8,31,4,31,11,26,15,20,11);
                Line(7,7,27,29,5); Poly(1,6,7,1,14,8,9,13); break;
            case BistroBuilderEditChromeSymbol.Home:
                Poly(5,15,16,5,27,15,27,29,19,29,19,20,13,20,13,29,5,29);
                Stroke(2,15,16,2,30,15); Stroke(23,8,23,3,27,3,27,12); break;
            case BistroBuilderEditChromeSymbol.Pencil:
                Poly(4,22,21,5,27,11,10,28,3,30); Poly(23,3,25,1,31,7,29,9);
                CutLine(7,23,10,26); CutLine(10,22,22,10); break;
            case BistroBuilderEditChromeSymbol.Undo:
            case BistroBuilderEditChromeSymbol.Redo:
                bool flip = Symbol == BistroBuilderEditChromeSymbol.Redo;
                for (int i=0;i<20;i++) { float t=i/20f,u=(i+1)/20f; var a=Bezier(t);var b=Bezier(u); Line(flip?32-a.x:a.x,a.y,flip?32-b.x:b.x,b.y); }
                if(flip) Stroke(24,7,30,13,24,19); else Stroke(8,7,2,13,8,19); break;
            case BistroBuilderEditChromeSymbol.Hand:
                Poly(8,29,4,21,1,14,2,12,4,12,8,18,8,6,9,4,11,4,12,6,12,15,13,3,14,1,16,1,17,3,17,15,18,4,19,3,21,3,22,5,22,16,23,8,24,7,26,8,27,10,27,20,24,29,20,31,12,31); break;
            case BistroBuilderEditChromeSymbol.Move:
                Line(16,3,16,29,2.7f);Line(3,16,29,16,2.7f);
                Poly(16,1,10,7,22,7);Poly(16,31,10,25,22,25);Poly(1,16,7,10,7,22);Poly(31,16,25,10,25,22);break;
            case BistroBuilderEditChromeSymbol.Grid:
                for(int i=5;i<=29;i+=8) {Line(i,3,i,31,1);Line(3,i,31,i,1);} break;
            case BistroBuilderEditChromeSymbol.Terrain:
                Stroke(2,27,9,17,14,19,20,9,24,10,30,27,2,27);break;
            case BistroBuilderEditChromeSymbol.Paint:
                Stroke(9,12,13,4,18,2,23,5,23,12); Poly(2,17,15,5,29,18,20,28,11,28);
                CutLine(6,18,25,18); Poly(29,20,26,25,27,28,30,28,31,25);break;
            case BistroBuilderEditChromeSymbol.Sun:
                Disc(16,16,7);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(16+Mathf.Cos(a)*11,16+Mathf.Sin(a)*11,16+Mathf.Cos(a)*15,16+Mathf.Sin(a)*15,1.6f);}break;
            case BistroBuilderEditChromeSymbol.Play: Poly(7,2,29,16,7,30);break;
            case BistroBuilderEditChromeSymbol.Plot:
                Stroke(16,2,29,9,29,24,16,31,3,24,3,9,16,2);
                Line(16,2,16,16,2.2f);Line(3,9,16,16,2.2f);Line(29,9,16,16,2.2f);Line(16,16,16,31,2.2f);break;
            case BistroBuilderEditChromeSymbol.Walls:
                Poly(3,8,16,2,29,8,16,14); Poly(3,10,15,16,15,30,3,24); Poly(17,16,29,10,29,24,17,30);
                if(Symbol==BistroBuilderEditChromeSymbol.Plot){CutLine(5,21,12,18);CutLine(19,10,24,8);}break;
            case BistroBuilderEditChromeSymbol.Chair:
                Stroke(8,3,24,3,24,20,8,20,8,3);
                Line(12,3,12,16,1.6f);Line(16,3,16,16,1.6f);Line(20,3,20,16,1.6f);Line(8,13,24,13,1.6f);
                Line(5,20,27,20,2.5f);Line(8,20,5,30,2.2f);Line(24,20,27,30,2.2f);break;
            case BistroBuilderEditChromeSymbol.Surfaces:
                Poly(16,1,31,19,16,31,1,19);
                CutLine(7,18,16,25);CutLine(16,25,25,18);CutLine(11,14,16,8);CutLine(16,8,21,14);CutLine(11,14,16,19);CutLine(16,19,21,14);break;
            case BistroBuilderEditChromeSymbol.Plant:
                Poly(6,18,26,18,23,30,9,30);Ellipse(10,12,3,6,-35);Ellipse(22,12,3,6,35);Ellipse(16,6,3,5,0);break;
            case BistroBuilderEditChromeSymbol.Bulb:
                Disc(16,11,8);Poly(10,15,22,15,20,23,12,23);Line(12,26,20,26,2);Line(14,29,18,29,2);
                CutLine(12,9,14,6); break;
            case BistroBuilderEditChromeSymbol.Settings:
                for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(16+Mathf.Cos(a)*8,16+Mathf.Sin(a)*8,16+Mathf.Cos(a)*13,16+Mathf.Sin(a)*13,5);}
                Arc(16,16,9,0,360,5);break;
            case BistroBuilderEditChromeSymbol.More: Disc(6,16,3);Disc(16,16,3);Disc(26,16,3);break;
            case BistroBuilderEditChromeSymbol.Delete:
                Stroke(7,9,9,29,23,29,25,9);Line(4,7,28,7);Stroke(12,7,12,3,20,3,20,7);Line(13,12,13,24);Line(19,12,19,24);break;
            case BistroBuilderEditChromeSymbol.Rotate:
                Arc(16,16,10,30,330,3.6f);Poly(18,2,29,3,27,15);break;
            case BistroBuilderEditChromeSymbol.Duplicate:
                Stroke(11,10,28,10,28,29,11,29,11,10);Stroke(7,23,4,23,4,3,21,3,21,6);break;
        }
        mesh = null;
    }
    static Vector2 Bezier(float t) { float u=1-t; return u*u*u*new Vector2(3,13)+3*u*u*t*new Vector2(30,8)+3*u*t*t*new Vector2(32,22)+t*t*t*new Vector2(24,26); }
    Vector2 P(float x,float y) { var r=rectTransform.rect;float s=Mathf.Min(r.width,r.height)/32;return r.center+new Vector2(x-16,16-y)*s; }
    void Vertex(Vector2 p,Color tint) { var v=UIVertex.simpleVert;v.position=p;v.color=tint;mesh.AddVert(v); }
    void Poly(params float[] xy)
    {
        // Ear clipping handles the concave house, pencil and hand without self-overlapping triangles.
        int n=xy.Length/2;var points=new Vector2[n];var indices=new System.Collections.Generic.List<int>(n);
        float area=0;for(int i=0;i<n;i++){points[i]=P(xy[i*2],xy[i*2+1]);indices.Add(i);}
        for(int i=0;i<n;i++) area+=Cross(points[i],points[(i+1)%n]);
        int start=mesh.currentVertCount;for(int i=0;i<n;i++)Vertex(points[i],ink);
        float sign=Mathf.Sign(area);int guard=n*n;
        while(indices.Count>2 && guard-->0){bool clipped=false;for(int i=0;i<indices.Count;i++){
            int a=indices[(i+indices.Count-1)%indices.Count],b=indices[i],c=indices[(i+1)%indices.Count];
            if(Cross(points[b]-points[a],points[c]-points[b])*sign<=0.00001f)continue;
            bool inside=false;foreach(int k in indices){if(k==a||k==b||k==c)continue;var p=points[k];
                if(Cross(points[b]-points[a],p-points[a])*sign>=0 && Cross(points[c]-points[b],p-points[b])*sign>=0 && Cross(points[a]-points[c],p-points[c])*sign>=0){inside=true;break;}}
            if(inside)continue;mesh.AddTriangle(start+a,start+b,start+c);indices.RemoveAt(i);clipped=true;break;}
            if(!clipped)break;
        }
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    void Line(float x,float y,float x2,float y2,float width=1.8f)
    {var a=P(x,y);var b=P(x2,y2);var d=(b-a).normalized;var normal=new Vector2(-d.y,d.x)*width*Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/64;int s=mesh.currentVertCount;
        Vertex(a-normal,ink);Vertex(a+normal,ink);Vertex(b+normal,ink);Vertex(b-normal,ink);mesh.AddTriangle(s,s+1,s+2);mesh.AddTriangle(s,s+2,s+3);}
    void Stroke(params float[] xy){for(int i=0;i<xy.Length-2;i+=2)Line(xy[i],xy[i+1],xy[i+2],xy[i+3]);}
    void CutLine(float x,float y,float xx,float yy){var before=ink;ink=new Color32(250,248,244,255);Line(x,y,xx,yy,1.2f);ink=before;}
    void Arc(float x,float y,float radius,float from,float to,float width)
    {int steps=Mathf.CeilToInt(Mathf.Abs(to-from)/8);for(int i=0;i<steps;i++){float a=Mathf.Lerp(from,to,i/(float)steps)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(i+1f)/steps)*Mathf.Deg2Rad;Line(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius,width);}}
    void Disc(float x,float y,float radius)=>Ellipse(x,y,radius,radius,0);
    void Ellipse(float x,float y,float rx,float ry,float angle)
    {int s=mesh.currentVertCount;Vertex(P(x,y),ink);float a=angle*Mathf.Deg2Rad;for(int i=0;i<=24;i++){float t=i*Mathf.PI/12;float xx=Mathf.Cos(t)*rx,yy=Mathf.Sin(t)*ry;Vertex(P(x+xx*Mathf.Cos(a)-yy*Mathf.Sin(a),y+xx*Mathf.Sin(a)+yy*Mathf.Cos(a)),ink);if(i>0)mesh.AddTriangle(s,s+i,s+i+1);}}
}
