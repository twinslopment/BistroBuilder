using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Vértice 3D independiente de Unity. X/Z proceden del plano X/Y y Y es altura.
    /// </summary>
    public readonly struct ArchitectureMeshVertex : IEquatable<ArchitectureMeshVertex>
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public ArchitectureMeshVertex(double x, double y, double z) { X = x; Y = y; Z = z; }
        public bool Equals(ArchitectureMeshVertex other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is ArchitectureMeshVertex other && Equals(other);
        public override int GetHashCode() { unchecked { return ((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode(); } }
    }

    /// <summary>
    /// Datos de malla reconstruibles. Nunca son autoridad arquitectónica.
    /// </summary>
    public sealed class ArchitectureMeshData
    {
        public readonly List<ArchitectureMeshVertex> Vertices = new List<ArchitectureMeshVertex>();
        public readonly List<int> Triangles = new List<int>();

        public bool IsEmpty => Vertices.Count == 0;
    }

    /// <summary>
    /// Mesher determinista V1 para paredes rectas con grosor, altura y aperturas.
    /// Divide la pared en celdas sólidas; las aperturas son huecos reales en la malla.
    /// </summary>
    public static class ArchitectureWallMesher
    {
        public static ArchitectureMeshData Build(ArchitectureLevel level, ArchitectureWall wall)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (wall == null) throw new ArgumentNullException(nameof(wall));

            var start = FindVertex(level, wall.StartVertexId);
            var end = FindVertex(level, wall.EndVertexId);
            if (start == null || end == null) throw new InvalidOperationException("LA7_WALL_VERTEX_MISSING");

            var length = start.Position.DistanceTo(end.Position);
            if (length <= ArchitectureGeometry.Epsilon || wall.Thickness <= 0d || wall.Height <= 0d)
                throw new InvalidOperationException("LA7_INVALID_WALL_DIMENSIONS");

            var dx = (end.Position.X - start.Position.X) / length;
            var dz = (end.Position.Y - start.Position.Y) / length;
            var px = -dz;
            var pz = dx;
            var half = wall.Thickness * 0.5d;

            var openings = (wall.Openings ?? new List<ArchitectureOpening>())
                .Where(o => o != null)
                .OrderBy(o => o.Id.Value, StringComparer.Ordinal)
                .ToList();

            var xCuts = new List<double> { 0d, length };
            var yCuts = new List<double> { 0d, wall.Height };
            foreach (var opening in openings)
            {
                var center = opening.CenterT * length;
                xCuts.Add(Clamp(center - (opening.Width * 0.5d), 0d, length));
                xCuts.Add(Clamp(center + (opening.Width * 0.5d), 0d, length));
                yCuts.Add(Clamp(opening.Bottom, 0d, wall.Height));
                yCuts.Add(Clamp(opening.Bottom + opening.Height, 0d, wall.Height));
            }

            xCuts = DistinctSorted(xCuts);
            yCuts = DistinctSorted(yCuts);
            var data = new ArchitectureMeshData();

            for (var xi = 0; xi < xCuts.Count - 1; xi++)
            {
                var x0 = xCuts[xi];
                var x1 = xCuts[xi + 1];
                if ((x1 - x0) <= ArchitectureGeometry.Epsilon) continue;
                var xm = (x0 + x1) * 0.5d;

                for (var yi = 0; yi < yCuts.Count - 1; yi++)
                {
                    var y0 = yCuts[yi];
                    var y1 = yCuts[yi + 1];
                    if ((y1 - y0) <= ArchitectureGeometry.Epsilon) continue;
                    var ym = (y0 + y1) * 0.5d;
                    if (IsInsideOpening(openings, length, xm, ym)) continue;
                    AddBox(data, start.Position, dx, dz, px, pz, half, x0, x1, y0, y1, level.Elevation);
                }
            }

            return data;
        }

        private static ArchitectureVertex FindVertex(ArchitectureLevel level, VertexId id)
        {
            for (var i = 0; i < level.Vertices.Count; i++)
                if (level.Vertices[i] != null && level.Vertices[i].Id.Equals(id)) return level.Vertices[i];
            return null;
        }

        private static bool IsInsideOpening(List<ArchitectureOpening> openings, double length, double x, double y)
        {
            foreach (var opening in openings)
            {
                var center = opening.CenterT * length;
                var minX = center - opening.Width * 0.5d;
                var maxX = center + opening.Width * 0.5d;
                var minY = opening.Bottom;
                var maxY = opening.Bottom + opening.Height;
                if (x > minX + ArchitectureGeometry.Epsilon && x < maxX - ArchitectureGeometry.Epsilon &&
                    y > minY + ArchitectureGeometry.Epsilon && y < maxY - ArchitectureGeometry.Epsilon)
                    return true;
            }
            return false;
        }

        private static List<double> DistinctSorted(List<double> values)
        {
            values.Sort();
            var result = new List<double>();
            foreach (var value in values)
                if (result.Count == 0 || !ArchitectureGeometry.NearlyEqual(result[result.Count - 1], value)) result.Add(value);
            return result;
        }

        private static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;

        private static void AddBox(ArchitectureMeshData data, ArchitecturePoint origin, double dx, double dz, double px, double pz,
            double half, double x0, double x1, double y0, double y1, double elevation)
        {
            var baseIndex = data.Vertices.Count;
            Add(data, origin, dx, dz, px, pz, x0, -half, y0 + elevation);
            Add(data, origin, dx, dz, px, pz, x1, -half, y0 + elevation);
            Add(data, origin, dx, dz, px, pz, x1, half, y0 + elevation);
            Add(data, origin, dx, dz, px, pz, x0, half, y0 + elevation);
            Add(data, origin, dx, dz, px, pz, x0, -half, y1 + elevation);
            Add(data, origin, dx, dz, px, pz, x1, -half, y1 + elevation);
            Add(data, origin, dx, dz, px, pz, x1, half, y1 + elevation);
            Add(data, origin, dx, dz, px, pz, x0, half, y1 + elevation);

            var t = data.Triangles;
            AddQuad(t, baseIndex + 0, baseIndex + 1, baseIndex + 2, baseIndex + 3);
            AddQuad(t, baseIndex + 4, baseIndex + 7, baseIndex + 6, baseIndex + 5);
            AddQuad(t, baseIndex + 0, baseIndex + 4, baseIndex + 5, baseIndex + 1);
            AddQuad(t, baseIndex + 1, baseIndex + 5, baseIndex + 6, baseIndex + 2);
            AddQuad(t, baseIndex + 2, baseIndex + 6, baseIndex + 7, baseIndex + 3);
            AddQuad(t, baseIndex + 3, baseIndex + 7, baseIndex + 4, baseIndex + 0);
        }

        private static void Add(ArchitectureMeshData data, ArchitecturePoint origin, double dx, double dz, double px, double pz,
            double distance, double lateral, double height)
        {
            data.Vertices.Add(new ArchitectureMeshVertex(
                origin.X + dx * distance + px * lateral,
                height,
                origin.Y + dz * distance + pz * lateral));
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }
    }
}
