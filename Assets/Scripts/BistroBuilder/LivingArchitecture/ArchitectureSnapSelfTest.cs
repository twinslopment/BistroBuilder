using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public sealed class ArchitectureSnapSelfTestResult
    {
        public int Passed;
        public int Failed;
        public readonly List<string> Failures = new List<string>();
        public bool Success => Failed == 0;
    }

    /// <summary>Self-test puro LA5: candidatos de snap, confianza y determinismo.</summary>
    public static class ArchitectureSnapSelfTest
    {
        public static ArchitectureSnapSelfTestResult Run()
        {
            var result = new ArchitectureSnapSelfTestResult();
            Test(result, "snap a vértice cercano", VertexSnap);
            Test(result, "proyección a pared", WallProjectionSnap);
            Test(result, "snap paralelo", ParallelSnap);
            Test(result, "snap perpendicular", PerpendicularSnap);
            Test(result, "igual longitud", EqualLengthSnap);
            Test(result, "continuidad", ContinuitySnap);
            Test(result, "pared excluida no propone", ExcludedWallIsIgnored);
            Test(result, "fuera de tolerancia no propone", OutsideToleranceIsIgnored);
            Test(result, "orden determinista", CandidateOrderIsDeterministic);
            Test(result, "servicio no muta nivel", ServiceDoesNotMutateLevel);
            return result;
        }

        private static void VertexSnap()
        {
            var level = CreateLevel();
            var candidates = Run(level, new ArchitecturePoint(0.05d, 0.02d), false, default(ArchitecturePoint));
            var c = candidates.FirstOrDefault(x => x.Type == ArchitectureSnapType.Vertex && x.SourceEntityId == "v_a");
            Require(c != null, "No apareció candidato de vértice.");
            Require(c.Confidence == ArchitectureSnapConfidence.High, "Confianza inesperada para vértice muy cercano.");
            Require(c.SnappedPoint.Equals(new ArchitecturePoint(0d, 0d)), "Punto de snap incorrecto.");
        }

        private static void WallProjectionSnap()
        {
            var level = CreateLevel();
            var candidates = Run(level, new ArchitecturePoint(2d, 0.1d), false, default(ArchitecturePoint));
            var c = candidates.FirstOrDefault(x => x.Type == ArchitectureSnapType.WallProjection && x.SourceEntityId == "w_bottom");
            Require(c != null, "No apareció proyección a pared.");
            Require(Math.Abs(c.SnappedPoint.X - 2d) < 0.0001d && Math.Abs(c.SnappedPoint.Y) < 0.0001d, "Proyección incorrecta.");
        }

        private static void ParallelSnap()
        {
            var level = CreateLevel();
            var candidates = Run(level, new ArchitecturePoint(3d, 0.15d), true, new ArchitecturePoint(0d, 0d));
            var c = candidates.FirstOrDefault(x => x.Type == ArchitectureSnapType.Parallel && x.SourceEntityId == "w_bottom");
            Require(c != null, "No apareció paralelo.");
            Require(Math.Abs(c.SnappedPoint.Y) < 0.0001d, "Paralelo no corrigió el ángulo.");
        }

        private static void PerpendicularSnap()
        {
            var level = CreateLevel();
            var candidates = Run(level, new ArchitecturePoint(0.1d, 2.5d), true, new ArchitecturePoint(0d, 0d));
            var c = candidates.FirstOrDefault(x => x.Type == ArchitectureSnapType.Perpendicular && x.SourceEntityId == "w_bottom");
            Require(c != null, "No apareció perpendicular.");
            Require(Math.Abs(c.SnappedPoint.X) < 0.0001d, "Perpendicular no corrigió el ángulo.");
        }

        private static void EqualLengthSnap()
        {
            var level = CreateLevel();
            var candidates = Run(level, new ArchitecturePoint(4.85d, 1d), true, new ArchitecturePoint(0d, 1d));
            var c = candidates.FirstOrDefault(x => x.Type == ArchitectureSnapType.EqualLength && x.SourceEntityId == "w_bottom");
            Require(c != null, "No apareció igualdad de longitud.");
            Require(c.TargetLength.HasValue && Math.Abs(c.TargetLength.Value - 5d) < 0.0001d, "Longitud objetivo incorrecta.");
        }

        private static void ContinuitySnap()
        {
            var level = CreateLevel();
            var candidates = Run(level, new ArchitecturePoint(7d, 0.1d), true, new ArchitecturePoint(5d, 0d));
            var c = candidates.FirstOrDefault(x => x.Type == ArchitectureSnapType.Continuity && x.SourceEntityId == "w_bottom");
            Require(c != null, "No apareció continuidad.");
            Require(Math.Abs(c.SnappedPoint.Y) < 0.0001d && c.SnappedPoint.X > 5d, "Continuidad incorrecta.");
        }

        private static void ExcludedWallIsIgnored()
        {
            var level = CreateLevel();
            var service = new ArchitectureSnapService();
            var candidates = service.GenerateCandidates(new ArchitectureSnapRequest
            {
                Level = level,
                Cursor = new ArchitecturePoint(2d, 0.05d),
                ExcludedWallId = new WallId("w_bottom")
            });
            Require(!candidates.Any(x => x.SourceEntityId == "w_bottom" && x.Type == ArchitectureSnapType.WallProjection), "La pared excluida produjo candidato.");
        }

        private static void OutsideToleranceIsIgnored()
        {
            var level = CreateLevel();
            var service = new ArchitectureSnapService();
            var candidates = service.GenerateCandidates(new ArchitectureSnapRequest
            {
                Level = level,
                Cursor = new ArchitecturePoint(20d, 20d),
                MaxDistance = 0.10d
            });
            Require(!candidates.Any(x => x.Type == ArchitectureSnapType.Vertex || x.Type == ArchitectureSnapType.WallProjection), "Se propuso snap geométrico fuera de tolerancia.");
        }

        private static void CandidateOrderIsDeterministic()
        {
            var level = CreateLevel();
            var first = Run(level, new ArchitecturePoint(3d, 0.12d), true, new ArchitecturePoint(0d, 0d));
            var second = Run(level, new ArchitecturePoint(3d, 0.12d), true, new ArchitecturePoint(0d, 0d));
            Require(first.Count == second.Count, "Cantidad de candidatos no determinista.");
            for (var i = 0; i < first.Count; i++)
            {
                Require(first[i].Type == second[i].Type, "Tipo no determinista.");
                Require(first[i].SourceEntityId == second[i].SourceEntityId, "Fuente no determinista.");
                Require(first[i].SnappedPoint.Equals(second[i].SnappedPoint), "Punto no determinista.");
            }
        }

        private static void ServiceDoesNotMutateLevel()
        {
            var level = CreateLevel();
            var before = level.Vertices.Select(v => v.Position).ToArray();
            Run(level, new ArchitecturePoint(3d, 0.1d), true, new ArchitecturePoint(0d, 0d));
            for (var i = 0; i < before.Length; i++)
                Require(before[i].Equals(level.Vertices[i].Position), "El servicio mutó un vértice del nivel.");
        }

        private static IReadOnlyList<ArchitectureSnapCandidate> Run(ArchitectureLevel level, ArchitecturePoint cursor, bool hasAnchor, ArchitecturePoint anchor)
        {
            return new ArchitectureSnapService().GenerateCandidates(new ArchitectureSnapRequest
            {
                Level = level,
                Cursor = cursor,
                HasAnchor = hasAnchor,
                Anchor = anchor,
                MaxDistance = 0.35d,
                AngleToleranceDegrees = 5d,
                EqualLengthTolerance = 0.25d
            });
        }

        private static ArchitectureLevel CreateLevel()
        {
            var level = new ArchitectureLevel { Id = new LevelId("level"), Elevation = 0d };
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_a"), Position = new ArchitecturePoint(0d, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_b"), Position = new ArchitecturePoint(5d, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_c"), Position = new ArchitecturePoint(5d, 4d) });
            level.Walls.Add(new ArchitectureWall { Id = new WallId("w_bottom"), StartVertexId = new VertexId("v_a"), EndVertexId = new VertexId("v_b"), Thickness = 0.15d, Height = 3d });
            level.Walls.Add(new ArchitectureWall { Id = new WallId("w_right"), StartVertexId = new VertexId("v_b"), EndVertexId = new VertexId("v_c"), Thickness = 0.15d, Height = 3d });
            return level;
        }

        private static void Test(ArchitectureSnapSelfTestResult result, string name, Action action)
        {
            try { action(); result.Passed++; }
            catch (Exception ex) { result.Failed++; result.Failures.Add(name + ": " + ex.Message); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "Fallo LA5." : message);
        }
    }
}
