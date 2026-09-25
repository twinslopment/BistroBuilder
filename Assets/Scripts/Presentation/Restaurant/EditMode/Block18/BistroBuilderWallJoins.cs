using System.Collections.Generic;
using UnityEngine;

/// <summary>Visual/collider end cuts derived from neighbouring canonical walls; never changes wall IDs or opening positions.</summary>
public static class BistroBuilderWallJoins
{
    public static void Apply(BistroBuilderWallRecord wall,IReadOnlyList<BistroBuilderWallRecord> neighbours,List<Vector3> vertices)
    {
        if(neighbours==null)return;
        Cut(wall,neighbours,true,out float startSlope,out float startOffset);
        Cut(wall,neighbours,false,out float endSlope,out float endOffset);
        for(int i=0;i<vertices.Count;i++)
        {
            var p=vertices[i];
            if(Mathf.Abs(p.x)<.00001f)p.x+=startOffset+startSlope*p.z;
            else if(Mathf.Abs(p.x-wall.Length)<.00001f)p.x+=endOffset+endSlope*p.z;
            vertices[i]=p;
        }
    }
    static void Cut(BistroBuilderWallRecord wall,IReadOnlyList<BistroBuilderWallRecord> walls,bool start,out float slope,out float offset)
    {
        slope=offset=0;var point=start?wall.axisStart:wall.axisEnd;
        var u=(wall.axisEnd-wall.axisStart).normalized;var away=start?u:-u;
        const float eps=.005f;BistroBuilderWallRecord corner=null;Vector2 otherDirection=default;
        bool host=false;float hostHalf=0;
        foreach(var other in walls)
        {
            if(other==null||other.wallId==wall.wallId||other.buildPlaneId!=wall.buildPlaneId||
                Mathf.Abs(other.baseElevation-wall.baseElevation)>eps||Mathf.Abs(other.height-wall.height)>eps)continue;
            var axis=other.axisEnd-other.axisStart;float sq=axis.sqrMagnitude;if(sq<eps*eps)continue;
            float t=Vector2.Dot(point-other.axisStart,axis)/sq;
            if(t<0||t>1||(point-(other.axisStart+t*axis)).sqrMagnitude>eps*eps)continue;
            bool atStart=Vector2.Distance(point,other.axisStart)<=eps,atEnd=Vector2.Distance(point,other.axisEnd)<=eps;
            if(!atStart&&!atEnd){host=true;hostHalf=Mathf.Max(hostHalf,other.thickness*.5f);continue;}
            var v=(atStart?axis:-axis).normalized;
            // A straight continuation wins over any branch at a T junction.
            if(Vector2.Dot(away,v)<-.999f)return;
            if(corner!=null&&Vector2.Dot(otherDirection,v)<-.999f){host=true;hostHalf=Mathf.Max(hostHalf,other.thickness*.5f);}
            corner=other;otherDirection=v;
        }
        if(host){offset=(start?1:-1)*Mathf.Min(hostHalf,wall.Length*.25f);return;}
        if(corner==null)return;
        // Equal sections meet on a shared mitre, closing both inner and outer corners.
        if(Mathf.Abs(corner.thickness-wall.thickness)>eps)return;
        var normal=u+(start?-otherDirection:otherDirection);float denominator=Vector2.Dot(normal,u);
        if(Mathf.Abs(denominator)<.001f)return;
        float value=-Vector2.Dot(normal,new Vector2(-u.y,u.x))/denominator;
        if(Mathf.Abs(value)*wall.thickness*.5f<wall.Length*.4f)slope=value;
    }
}
