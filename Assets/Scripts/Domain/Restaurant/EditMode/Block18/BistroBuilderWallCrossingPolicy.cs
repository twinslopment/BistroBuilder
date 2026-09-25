using System.Collections.Generic;
using UnityEngine;

/// <summary>Construction rule: walls may meet at endpoints/T junctions, but never pass through each other.</summary>
public static class BistroBuilderWallCrossingPolicy
{
    public const string Code = "ARCH_WALL_CROSSING";
    public const string Message = "La pared atraviesa otra pared. Termina el tramo en la uniÃ³n.";
    static readonly float Tolerance = BistroBuilderArchitectureGeometryPolicy.Default.pointTolerance;
    static float Cross(Vector2 a, Vector2 b) => a.x*b.y-a.y*b.x;

    public static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var u=b-a; var v=d-c;
        float lu=u.magnitude, lv=v.magnitude;
        if(lu<=Tolerance||lv<=Tolerance)return false;
        float denominator=Cross(u,v);
        if(Mathf.Abs(denominator)<1e-7f*lu*lv)return false;
        float t=Cross(c-a,v)/denominator, s=Cross(c-a,u)/denominator;
        return t*lu>Tolerance && (1-t)*lu>Tolerance && s*lv>Tolerance && (1-s)*lv>Tolerance;
    }

    public static bool SameVolume(BistroBuilderWallRecord a, BistroBuilderWallRecord b) =>
        a!=null && b!=null && a.buildPlaneId==b.buildPlaneId &&
        Mathf.Min(a.baseElevation+a.height,b.baseElevation+b.height)-Mathf.Max(a.baseElevation,b.baseElevation)>Tolerance;

    public static bool Crosses(BistroBuilderWallRecord a, BistroBuilderWallRecord b) =>
        SameVolume(a,b)&&Crosses(a.axisStart,a.axisEnd,b.axisStart,b.axisEnd);

    static string PairKey(BistroBuilderWallRecord a, BistroBuilderWallRecord b) =>
        string.CompareOrdinal(a.wallId.Value,b.wallId.Value)<0?GeometryKey(a)+"/"+GeometryKey(b):GeometryKey(b)+"/"+GeometryKey(a);

    static string GeometryKey(BistroBuilderWallRecord wall) => string.Format(System.Globalization.CultureInfo.InvariantCulture,
        "{0}:{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7}", wall.wallId.Value,wall.axisStart.x,wall.axisStart.y,
        wall.axisEnd.x,wall.axisEnd.y,wall.baseElevation,wall.height,wall.buildPlaneId);

    public static HashSet<string> CaptureExisting(BistroBuilderEditDocument document)
    {
        var result=new HashSet<string>();
        for(int i=0;i<document.walls.Count;i++)for(int j=i+1;j<document.walls.Count;j++)
            if(Crosses(document.walls[i],document.walls[j]))result.Add(PairKey(document.walls[i],document.walls[j]));
        return result;
    }

    public static bool HasNewCrossing(BistroBuilderEditDocument document, HashSet<string> inherited)
    {
        for(int i=0;i<document.walls.Count;i++)for(int j=i+1;j<document.walls.Count;j++)
        {
            var a=document.walls[i];var b=document.walls[j];
            if(Crosses(a,b)&&(inherited==null||!inherited.Contains(PairKey(a,b))))return true;
        }
        return false;
    }

    public static void Validate(BistroBuilderEditDocument document,long revision,List<BistroBuilderEditDiagnostic> diagnostics)
    {
        for(int i=0;i<document.walls.Count;i++)for(int j=i+1;j<document.walls.Count;j++)
        {
            var a=document.walls[i];var b=document.walls[j];
            if(Crosses(a,b))diagnostics.Add(new BistroBuilderEditDiagnostic { sourceSystem="EditMode.Architecture",code=Code,
                severity=BistroBuilderEditDiagnosticSeverity.Blocking,targetId=a.wallId,location=(a.axisStart+a.axisEnd)*.5f,
                message=Message,draftRevision=revision });
        }
    }
}
