using System;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Punto 2D del plano arquitectónico. Evita acoplar el dominio a Transform/GameObject.
    /// </summary>
    [Serializable]
    public readonly struct ArchitecturePoint : IEquatable<ArchitecturePoint>
    {
        public readonly double X;
        public readonly double Y;

        public ArchitecturePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceTo(ArchitecturePoint other)
        {
            var dx = other.X - X;
            var dy = other.Y - Y;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        public bool Equals(ArchitecturePoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is ArchitecturePoint other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return (X.GetHashCode() * 397) ^ Y.GetHashCode(); }
        }
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }

    public static class ArchitectureGeometry
    {
        /// <summary>
        /// Epsilon canónico V1 para degeneración topológica, expresado en metros.
        /// </summary>
        public const double Epsilon = 0.0001d;

        public static bool NearlyEqual(double a, double b, double epsilon = Epsilon)
        {
            return Math.Abs(a - b) <= epsilon;
        }
    }
}
