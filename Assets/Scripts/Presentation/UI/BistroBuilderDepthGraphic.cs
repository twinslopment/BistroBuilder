using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class BistroBuilderDepthGraphic : MaskableGraphic
{
    public BistroBuilderSurfaceLevel Level;
    public BistroBuilderBorderState State;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();Rect rect=BistroBuilderStateGraphic.Inset(rectTransform.rect,32);int elevation=(int)Level;
        if(elevation>0)
        {
            float blur=elevation*8,offset=elevation==1?2:elevation==2?4:8;
            float alpha=elevation==1?0.08f:elevation==2?0.10f:0.12f;
            for(int i=8;i>=1;i--)
            {
                float spread=blur*i/8*0.5f;Rect shadow=BistroBuilderStateGraphic.Inset(rect,-spread);shadow.y-=offset;
                BistroBuilderStateGraphic.Ring(vh,shadow,blur/16+0.2f,new Color(0,0,0,alpha*(1f-i/9f)));
            }
        }
        Color color=new Color(1,0.91f,0.79f,0.08f);
        switch(State)
        {
            case BistroBuilderBorderState.Hover:color=new Color(1,0.96f,0.89f,0.16f);break;
            case BistroBuilderBorderState.Selected:color=new Color(0.98f,0.73f,0.20f);break;
            case BistroBuilderBorderState.Attention:color=new Color(1,0.46f,0.10f);break;
            case BistroBuilderBorderState.Critical:color=new Color(0.91f,0.21f,0.15f);break;
            case BistroBuilderBorderState.Disabled:color=new Color(1,0.93f,0.83f,0.06f);break;
        }
        if(State==BistroBuilderBorderState.Disabled) Dashed(vh,rect,color);
        else BistroBuilderStateGraphic.Ring(vh,rect,1,color);
    }
    private static void Dashed(VertexHelper vh,Rect rect,Color color)
    {
        for(float x=rect.xMin+7;x<rect.xMax-7;x+=10){Quad(vh,new Rect(x,rect.yMin,Mathf.Min(5,rect.xMax-7-x),1),color);Quad(vh,new Rect(x,rect.yMax-1,Mathf.Min(5,rect.xMax-7-x),1),color);}
        for(float y=rect.yMin+7;y<rect.yMax-7;y+=10){Quad(vh,new Rect(rect.xMin,y,1,Mathf.Min(5,rect.yMax-7-y)),color);Quad(vh,new Rect(rect.xMax-1,y,1,Mathf.Min(5,rect.yMax-7-y)),color);}
    }
    private static void Quad(VertexHelper vh,Rect r,Color color)
    {int i=vh.currentVertCount;vh.AddVert(new Vector2(r.xMin,r.yMin),color,Vector2.zero);vh.AddVert(new Vector2(r.xMin,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMin),color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
}
