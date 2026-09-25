using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class BistroBuilderPremisesIcon : MaskableGraphic
{
    public BistroBuilderStartingPremisesProfile Profile;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        void Box(float x,float y,float w,float h)
        {
            var r=GetPixelAdjustedRect();int i=vh.currentVertCount;
            Vector3 P(float px,float py)=>new Vector3(r.x+px*r.width/48f,r.y+(48-py)*r.height/48f);
            vh.AddVert(P(x,y),color,Vector2.zero);vh.AddVert(P(x+w,y),color,Vector2.zero);
            vh.AddVert(P(x+w,y+h),color,Vector2.zero);vh.AddVert(P(x,y+h),color,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        void Frame(float x,float y,float w,float h){Box(x,y,w,2);Box(x,y,2,h);Box(x+w-2,y,2,h);Box(x,y+h-2,w,2);}
        void Table(float x,float y){Box(x,y,7,6);Box(x+2,y-3,3,2);Box(x+2,y+7,3,2);}
        if(Profile==BistroBuilderStartingPremisesProfile.Empty)
        { Frame(5,5,38,38);Box(23,17,2,14);Box(17,23,14,2); }
        else if(Profile==BistroBuilderStartingPremisesProfile.Compact)
        {Frame(11,4,26,40);Box(11,14,26,2);Table(17,23);Box(28,22,2,14);}
        else if(Profile==BistroBuilderStartingPremisesProfile.Balanced)
        {Frame(4,5,40,38);Box(4,16,40,2);Box(28,5,2,12);Table(11,26);Table(29,26);}
        else
        {Frame(2,3,44,42);Box(2,14,44,2);Box(29,3,2,11);Table(7,22);Table(24,22);Table(7,35);Table(24,35);Box(38,22,2,17);}
    }
}
