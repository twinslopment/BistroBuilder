using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Gate geométrico de LA2. Un cruce de paredes solo es topológicamente válido si existe
    /// un vértice compartido explícito; tocarse por coordenadas sin compartir ID es igualmente inválido.
    /// </summary>
    public static class ArchitecturePlanarity
    {
        public static void EnsureValid(ArchitectureLevel level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            var vertices = (level.Vertices ?? new List<ArchitectureVertex>())
                .Where(x => x != null && ArchitectureId.IsValid(x.Id.Value))
                .GroupBy(x => x.Id.Value, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            var walls = (level.Walls ?? new List<ArchitectureWall>())
                .Where(x => x != null && vertices.ContainsKey(x.StartVertexId.Value) && vertices.ContainsKey(x.EndVertexId.Value))
                .OrderBy(x => x.Id.Value, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < walls.Count; i++)
            {
                for (var j = i + 1; j < walls.Count; j++)
                {
                    var a = walls[i];
                    var b = walls[j];
                    if (SharesVertex(a, b)) continue;

                    var a0 = vertices[a.StartVertexId.Value].Position;
                    var a1 = vertices[a.EndVertexId.Value].Position;
                    var b0 = vertices[b.StartVertexId.Value].Position;
                    var b1 = vertices[b.EndVertexId.Value].Position;

                    if (SegmentsIntersectOrTouch(a0, a1, b0, b1))
                    {
                        throw new InvalidOperationException(
                            "LA2_NON_PLANAR_CROSSING: las paredes '" + a.Id.Value + "' y '" + b.Id.Value +
                            "' se cruzan/tocan sin compartir un VertexId. La intersección debe materializarse como vértice topológico antes de reconstruir regiones.");
                    }
                }
            }
        }

        private static bool SharesVertex(ArchitectureWall a, ArchitectureWall b)
        {
            return a.StartVertexId.Equals(b.StartVertexId) || a.StartVertexId.Equals(b.EndVertexId) ||
                   a.EndVertexId.Equals(b.StartVertexId) || a.EndVertexId.Equals(b.EndVertexId);
        }

        private static bool SegmentsIntersectOrTouch(ArchitecturePoint a, ArchitecturePoint b, ArchitecturePoint c, ArchitecturePoint d)
        {
            var o1 = Orientation(a, b, c);
            var o2 = Orientation(a, b, d);
            var o3 = Orientation(c, d, a);
            var o4 = Orientation(c, d, b);

            if (Opposite(o1, o2) && Opposite(o3, o4)) return true;
            if (o1 == 0 && OnSegment(a, b, c)) return true;
            if (o2 == 0 && OnSegment(a, b, d)) return true;
            if (o3 == 0 && OnSegment(c, d, a)) return true;
            if (o4 == 0 && OnSegment(c, d, b)) return true;
            return false;
        }

        private static int Orientation(ArchitecturePoint a, ArchitecturePoint b, ArchitecturePoint c)
        {
            var cross = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
            if (Math.Abs(cross) <= ArchitectureGeometry.Epsilon) return 0;
            return cross > 0d ? 1 : -1;
        }

        private static bool OnSegment(ArchitecturePoint a, ArchitecturePoint b, ArchitecturePoint p)
        {
            var e = ArchitectureGeometry.Epsilon;
            return p.X >= Math.Min(a.X, b.X) - e && p.X <= Math.Max(a.X, b.X) + e &&
                   p.Y >= Math.Min(a.Y, b.Y) - e && p.Y <= Math.Max(a.Y, b.Y) + e;
        }

        private static bool Opposite(int a, int b) => (a > 0 && b < 0) || (a < 0 && b > 0);
    }
}
