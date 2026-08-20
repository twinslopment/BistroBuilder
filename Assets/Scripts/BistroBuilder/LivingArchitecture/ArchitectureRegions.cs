using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Región arquitectónica derivada de la topología de un nivel.
    /// No es autoridad persistente: si cambian paredes/vértices, se recalcula.
    /// </summary>
    public sealed class ArchitectureRegion
    {
        public RegionId Id;
        public LevelId LevelId;
        public List<VertexId> BoundaryVertexIds = new List<VertexId>();
        public List<WallId> BoundaryWallIds = new List<WallId>();
        public List<ArchitecturePoint> Boundary = new List<ArchitecturePoint>();
        public double Area;
        public ArchitecturePoint Centroid;

        /// <summary>
        /// Consulta espacial inclusiva en borde. Útil para saber qué espacio contiene un punto.
        /// </summary>
        public bool Contains(ArchitecturePoint point, double epsilon = ArchitectureGeometry.Epsilon)
        {
            if (Boundary == null || Boundary.Count < 3) return false;

            for (var i = 0; i < Boundary.Count; i++)
            {
                var a = Boundary[i];
                var b = Boundary[(i + 1) % Boundary.Count];
                if (PointOnSegment(point, a, b, epsilon)) return true;
            }

            var inside = false;
            for (var i = 0; i < Boundary.Count; i++)
            {
                var a = Boundary[i];
                var b = Boundary[(i + 1) % Boundary.Count];
                var crosses = (a.Y > point.Y) != (b.Y > point.Y);
                if (!crosses) continue;

                var x = ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X;
                if (point.X < x) inside = !inside;
            }

            return inside;
        }

        private static bool PointOnSegment(ArchitecturePoint p, ArchitecturePoint a, ArchitecturePoint b, double epsilon)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var cross = ((p.X - a.X) * dy) - ((p.Y - a.Y) * dx);
            if (Math.Abs(cross) > epsilon) return false;

            var dot = ((p.X - a.X) * dx) + ((p.Y - a.Y) * dy);
            if (dot < -epsilon) return false;
            var lengthSquared = (dx * dx) + (dy * dy);
            return dot <= lengthSquared + epsilon;
        }
    }

    /// <summary>
    /// Resultado inmutable por convención de una reconstrucción de regiones.
    /// </summary>
    public sealed class ArchitectureRegionSet
    {
        public LevelId LevelId;
        public readonly List<ArchitectureRegion> Regions = new List<ArchitectureRegion>();

        public ArchitectureRegion FindContaining(ArchitecturePoint point)
        {
            // Si existiesen contornos anidados, prima la región física más específica.
            return Regions.Where(x => x != null && x.Contains(point))
                .OrderBy(x => x.Area)
                .ThenBy(x => x.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Motor LA2 de regiones emergentes para grafos planares de paredes rectas.
    /// Recorre medias aristas y obtiene únicamente caras acotadas CCW.
    /// </summary>
    public static class ArchitectureRegionEngine
    {
        private sealed class HalfEdge
        {
            public VertexId From;
            public VertexId To;
            public WallId WallId;
            public string Key => From.Value + ">" + To.Value + "@" + WallId.Value;
        }

        public static ArchitectureRegionSet Build(ArchitectureLevel level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            var result = new ArchitectureRegionSet { LevelId = level.Id };
            var vertices = (level.Vertices ?? new List<ArchitectureVertex>())
                .Where(x => x != null && ArchitectureId.IsValid(x.Id.Value))
                .GroupBy(x => x.Id.Value, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            var outgoing = new Dictionary<string, List<HalfEdge>>(StringComparer.Ordinal);
            var edges = new List<HalfEdge>();

            foreach (var wall in (level.Walls ?? new List<ArchitectureWall>())
                .Where(x => x != null)
                .OrderBy(x => x.Id.Value, StringComparer.Ordinal))
            {
                if (!vertices.ContainsKey(wall.StartVertexId.Value) || !vertices.ContainsKey(wall.EndVertexId.Value)) continue;
                if (wall.StartVertexId.Equals(wall.EndVertexId)) continue;

                AddHalfEdge(new HalfEdge { From = wall.StartVertexId, To = wall.EndVertexId, WallId = wall.Id }, outgoing, edges);
                AddHalfEdge(new HalfEdge { From = wall.EndVertexId, To = wall.StartVertexId, WallId = wall.Id }, outgoing, edges);
            }

            foreach (var pair in outgoing)
            {
                var origin = vertices[pair.Key].Position;
                pair.Value.Sort((a, b) =>
                {
                    var aa = Angle(origin, vertices[a.To.Value].Position);
                    var bb = Angle(origin, vertices[b.To.Value].Position);
                    var cmp = aa.CompareTo(bb);
                    if (cmp != 0) return cmp;
                    cmp = string.CompareOrdinal(a.To.Value, b.To.Value);
                    return cmp != 0 ? cmp : string.CompareOrdinal(a.WallId.Value, b.WallId.Value);
                });
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var start in edges.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (visited.Contains(start.Key)) continue;
                var cycle = WalkFace(start, outgoing, visited);
                if (cycle == null || cycle.Count < 3) continue;

                var points = cycle.Select(x => vertices[x.From.Value].Position).ToList();
                var signedArea = SignedArea(points);
                if (signedArea <= ArchitectureGeometry.Epsilon) continue; // elimina cara exterior/degenerada

                var region = new ArchitectureRegion
                {
                    LevelId = level.Id,
                    Area = signedArea,
                    Centroid = ComputeCentroid(points, signedArea),
                    Boundary = points,
                    BoundaryVertexIds = cycle.Select(x => x.From).ToList(),
                    BoundaryWallIds = cycle.Select(x => x.WallId).ToList()
                };
                region.Id = new RegionId(ComputeStableRegionId(level.Id, region.BoundaryWallIds));
                result.Regions.Add(region);
            }

            result.Regions.Sort((a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
            return result;
        }

        private static void AddHalfEdge(HalfEdge edge, Dictionary<string, List<HalfEdge>> outgoing, List<HalfEdge> edges)
        {
            if (!outgoing.TryGetValue(edge.From.Value, out var list))
            {
                list = new List<HalfEdge>();
                outgoing.Add(edge.From.Value, list);
            }
            list.Add(edge);
            edges.Add(edge);
        }

        private static List<HalfEdge> WalkFace(HalfEdge start, Dictionary<string, List<HalfEdge>> outgoing, HashSet<string> visited)
        {
            var cycle = new List<HalfEdge>();
            var local = new HashSet<string>(StringComparer.Ordinal);
            var current = start;
            var guard = 0;
            var max = Math.Max(16, outgoing.Sum(x => x.Value.Count) + 1);

            while (guard++ < max)
            {
                if (!local.Add(current.Key)) return current.Key == start.Key ? cycle : null;
                visited.Add(current.Key);
                cycle.Add(current);

                if (!outgoing.TryGetValue(current.To.Value, out var candidates) || candidates.Count == 0) return null;
                var reverseIndex = candidates.FindIndex(x => x.To.Equals(current.From) && x.WallId.Equals(current.WallId));
                if (reverseIndex < 0) return null;

                // Candidato inmediatamente horario respecto a la arista inversa: mantiene la cara a la izquierda.
                current = candidates[(reverseIndex - 1 + candidates.Count) % candidates.Count];
                if (current.Key == start.Key) return cycle;
            }

            return null;
        }

        private static double Angle(ArchitecturePoint a, ArchitecturePoint b) => Math.Atan2(b.Y - a.Y, b.X - a.X);

        private static double SignedArea(IReadOnlyList<ArchitecturePoint> points)
        {
            double twiceArea = 0d;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                twiceArea += (a.X * b.Y) - (b.X * a.Y);
            }
            return twiceArea * 0.5d;
        }

        private static ArchitecturePoint ComputeCentroid(IReadOnlyList<ArchitecturePoint> points, double signedArea)
        {
            double x = 0d;
            double y = 0d;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var cross = (a.X * b.Y) - (b.X * a.Y);
                x += (a.X + b.X) * cross;
                y += (a.Y + b.Y) * cross;
            }
            var factor = 1d / (6d * signedArea);
            return new ArchitecturePoint(x * factor, y * factor);
        }

        private static string ComputeStableRegionId(LevelId levelId, IEnumerable<WallId> wallIds)
        {
            // La identidad emerge de las paredes delimitadoras, no de orden/dirección del recorrido.
            var canonical = levelId.Value + "|" + string.Join("|", wallIds.Select(x => x.Value).OrderBy(x => x, StringComparer.Ordinal));
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var hex = new StringBuilder(24);
                for (var i = 0; i < 12; i++) hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return "reg_" + hex;
            }
        }
    }
}
