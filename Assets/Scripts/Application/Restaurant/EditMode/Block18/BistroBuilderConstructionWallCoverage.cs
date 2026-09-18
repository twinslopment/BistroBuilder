using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.ConstructionAuthoring
{
    /// <summary>Reuse shared boundaries, including a shorter room against a long wall.</summary>
    public static class ConstructionWallCoverage
    {
        public static void AppendUncovered(ArchitectureQueryCache queries, BistroBuilderWallRecord desired,
            List<IBistroBuilderEditCommand> commands)
        {
            var remaining = new List<Vector2> { new Vector2(0, desired.Length) };
            Vector2 axis = (desired.axisEnd-desired.axisStart).normalized;
            float tolerance = ConstructionGeometry.Tolerance;
            foreach (var existing in queries.Walls)
            {
                if (existing.buildPlaneId != desired.buildPlaneId || Mathf.Abs(existing.baseElevation-desired.baseElevation)>tolerance) continue;
                Vector2 a=existing.axisStart-desired.axisStart,b=existing.axisEnd-desired.axisStart;
                if(Mathf.Abs(a.x*axis.y-a.y*axis.x)>tolerance || Mathf.Abs(b.x*axis.y-b.y*axis.x)>tolerance) continue;
                float start=Mathf.Min(Vector2.Dot(a,axis),Vector2.Dot(b,axis));
                float end=Mathf.Max(Vector2.Dot(a,axis),Vector2.Dot(b,axis));
                for(int i=remaining.Count-1;i>=0;i--)
                {
                    Vector2 span=remaining[i]; if(end<=span.x+tolerance || start>=span.y-tolerance) continue;
                    remaining.RemoveAt(i);
                    if(start>span.x+tolerance) remaining.Add(new Vector2(span.x,start));
                    if(end<span.y-tolerance) remaining.Add(new Vector2(end,span.y));
                }
            }
            remaining.Sort((a,b)=>a.x.CompareTo(b.x));
            foreach(var span in remaining)
            {
                var wall=desired.DeepClone(); wall.wallId=BistroBuilderEditId.NewId();
                wall.axisStart=desired.axisStart+axis*span.x; wall.axisEnd=desired.axisStart+axis*span.y;
                commands.Add(new BistroBuilderCreateWallCommand(wall));
            }
        }
    }
}
