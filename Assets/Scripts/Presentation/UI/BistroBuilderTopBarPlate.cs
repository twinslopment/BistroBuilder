using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution independent ivory and brass plate, with a restrained local hover light.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BistroBuilderTopBarPlate : MaskableGraphic
{
    public bool Cell;
    private float light, selected, focus;
    public void State(float hover, float selection, float keyboard)
    {
        if (Mathf.Abs(light-hover)+Mathf.Abs(selected-selection)+Mathf.Abs(focus-keyboard)<.001f) return;
        light=hover; selected=selection; focus=keyboard; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); Rect r=rectTransform.rect;
        if (!Cell)
        {
            Rect shadow=r; shadow.y-=3;
            Shape(mesh,shadow,11,new Color(0,0,0,.18f),new Color(0,0,0,.08f));
            Shape(mesh,r,11,Hex(0x765632),Hex(0xC2A475));
            Shape(mesh,Inset(r,1),10,Hex(0xF9EACF),Hex(0x9D7749));
            Shape(mesh,Inset(r,3),8,Hex(0xB6966D),Hex(0xEBCE9F));
            Shape(mesh,Inset(r,4),7,Hex(0xF5E8D3),Hex(0xDDC7A6));
            Shape(mesh,new Rect(r.x+14,r.yMax-6,r.width-28,1),0,new Color(1,1,1,.65f),new Color(1,1,1,.65f));
        }
        else
        {
            if(selected>0) Shape(mesh,Inset(r,1),6,new Color(.75f,.51f,.20f,.20f*selected),new Color(.81f,.59f,.28f,.10f*selected));
            if(light>0) {
                Shape(mesh,Inset(r,1),6,new Color(1,.90f,.62f,.20f*light),new Color(1,.97f,.86f,.08f*light));
                // Layered radial illumination, never a solid brown rectangle.
                for(int i=6;i>=1;i--) {
                    float w=r.width*.85f*i/6f,h=r.height*.65f*i/6f;
                    Shape(mesh,new Rect(r.center.x-w/2,r.center.y-h/2+8,w,h),Mathf.Min(w,h)*.4f,new Color(1,.96f,.78f,.022f*light),new Color(1,.89f,.55f,.015f*light));
                }
            }
            if(focus>0) {
                Color c=new Color(.52f,.33f,.10f,focus);
                Shape(mesh,new Rect(r.x+4,r.y+2,r.width-8,2),1,c,c);
            }
            float lineWidth=Mathf.Min(32,r.width*.32f);
            Color line=new Color(.58f,.37f,.13f,.25f+.6f*selected+.25f*light);
            Shape(mesh,new Rect(r.center.x-lineWidth/2,r.y+5,lineWidth,2),1,line,line);
        }
    }
    private static Color Hex(int rgb)=>new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);
    private static Rect Inset(Rect r,float d)=>new Rect(r.x+d,r.y+d,r.width-2*d,r.height-2*d);
    private static void Shape(VertexHelper mesh,Rect r,float cut,Color top,Color bottom)
    {
        cut=Mathf.Min(cut,Mathf.Min(r.width,r.height)/2);
        Vector2[] p={new Vector2(r.x+cut,r.y),new Vector2(r.xMax-cut,r.y),new Vector2(r.xMax,r.y+cut),new Vector2(r.xMax,r.yMax-cut),new Vector2(r.xMax-cut,r.yMax),new Vector2(r.x+cut,r.yMax),new Vector2(r.x,r.yMax-cut),new Vector2(r.x,r.y+cut)};
        int start=mesh.currentVertCount;
        mesh.AddVert(r.center,Color.Lerp(bottom,top,.5f),Vector2.zero);
        foreach(var v in p) mesh.AddVert(v,Color.Lerp(bottom,top,(v.y-r.y)/Mathf.Max(1,r.height)),Vector2.zero);
        for(int i=0;i<8;i++)mesh.AddTriangle(start,start+1+i,start+1+(i+1)%8);
    }
}
