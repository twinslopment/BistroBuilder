using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public sealed class ArchitectureMesherSelfTestResult
    {
        public int Passed;
        public int Failed;
        public readonly List<string> Failures = new List<string>();
        public bool Success => Failed == 0;
    }

    /// <summary>Self-test puro LA7: proyección geométrica determinista y sin autoridad inversa.</summary>
    public static class ArchitectureMesherSelfTest
    {
        public static ArchitectureMesherSelfTestResult Run()
        {
            var result = new ArchitectureMesherSelfTestResult();
            Test(result, "pared simple produce malla", SimpleWallProducesMesh);
            Test(result, "grosor y altura respetados", DimensionsAreRespected);
            Test(result, "elevación de planta aplicada", LevelElevationIsApplied);
            Test(result, "apertura crea hueco", OpeningCreatesHole);
            Test(result, "apertura elevada conserva antepecho", ElevatedOpeningKeepsSill);
            Test(result, "orden de aperturas no altera malla", OpeningOrderDoesNotMatter);
            Test(result, "mesher determinista", MesherIsDeterministic);
            Test(result, "mesher no muta dominio", MesherDoesNotMutateDomain);
            Test(result, "vértice ausente falla seguro", MissingVertexFailsSafely);
            Test(result, "pared degenerada falla seguro", DegenerateWallFailsSafely);
            return result;
        }

        private static void SimpleWallProducesMesh()
        {
            var level = CreateLevel(false);
            var data = ArchitectureWallMesher.Build(level, level.Walls[0]);
            Require(data.Vertices.Count == 8, "Una pared simple debe producir un prisma de 8 vértices.");
            Require(data.Triangles.Count == 36, "Una pared simple debe producir 12 triángulos.");
        }

        private static void DimensionsAreRespected()
        {
            var level = CreateLevel(false);
            var data = ArchitectureWallMesher.Build(level, level.Walls[0]);
            Require(Nearly(data.Vertices.Min(v => v.X), 0d) && Nearly(data.Vertices.Max(v => v.X), 4d), "Longitud incorrecta.");
            Require(Nearly(data.Vertices.Min(v => v.Y), 0d) && Nearly(data.Vertices.Max(v => v.Y), 3d), "Altura incorrecta.");
            Require(Nearly(data.Vertices.Min(v => v.Z), -0.1d) && Nearly(data.Vertices.Max(v => v.Z), 0.1d), "Grosor incorrecto.");
        }

        private static void LevelElevationIsApplied()
        {
            var level = CreateLevel(false);
            level.Elevation = 2.5d;
            var data = ArchitectureWallMesher.Build(level, level.Walls[0]);
            Require(Nearly(data.Vertices.Min(v => v.Y), 2.5d) && Nearly(data.Vertices.Max(v => v.Y), 5.5d), "La elevación no se aplicó.");
        }

        private static void OpeningCreatesHole()
        {
            var level = CreateLevel(true);
            var data = ArchitectureWallMesher.Build(level, level.Walls[0]);
            Require(data.Vertices.Count > 8, "La pared con apertura debe segmentarse.");
            Require(!ContainsSolidCellCenter(data, 2d, 1d), "Existe geometría en el centro de la apertura.");
        }

        private static void ElevatedOpeningKeepsSill()
        {
            var level = CreateLevel(true);
            level.Walls[0].Openings[0].Bottom = 1d;
            level.Walls[0].Openings[0].Height = 1d;
            var data = ArchitectureWallMesher.Build(level, level.Walls[0]);
            Require(data.Vertices.Any(v => Nearly(v.X, 1.5d) && Nearly(v.Y, 0d)), "No se generó el antepecho inferior.");
            Require(data.Vertices.Any(v => Nearly(v.X, 1.5d) && Nearly(v.Y, 1d)), "Antepecho con altura incorrecta.");
        }

        private static void OpeningOrderDoesNotMatter()
        {
            var level = CreateLevel(true);
            level.Walls[0].Openings.Add(new ArchitectureOpening { Id = new OpeningId("op_b"), WallId = level.Walls[0].Id, CenterT = 0.8d, Width = 0.4d, Bottom = 1d, Height = 1d });
            var first = Signature(ArchitectureWallMesher.Build(level, level.Walls[0]));
            level.Walls[0].Openings.Reverse();
            var second = Signature(ArchitectureWallMesher.Build(level, level.Walls[0]));
            Require(first == second, "El orden de aperturas alteró el resultado.");
        }

        private static void MesherIsDeterministic()
        {
            var level = CreateLevel(true);
            Require(Signature(ArchitectureWallMesher.Build(level, level.Walls[0])) == Signature(ArchitectureWallMesher.Build(level, level.Walls[0])), "Resultado no determinista.");
        }

        private static void MesherDoesNotMutateDomain()
        {
            var level = CreateLevel(true);
            var building = new ArchitectureBuilding { Id = new BuildingId("b") };
            building.Levels.Add(level);
            var snapshot = new ArchitectureSnapshot { Building = building };
            var before = snapshot.ComputeFingerprint();
            ArchitectureWallMesher.Build(level, level.Walls[0]);
            Require(before == snapshot.ComputeFingerprint(), "El mesher mutó arquitectura canónica.");
        }

        private static void MissingVertexFailsSafely()
        {
            var level = CreateLevel(false);
            level.Walls[0].EndVertexId = new VertexId("missing");
            ExpectInvalid(() => ArchitectureWallMesher.Build(level, level.Walls[0]), "LA7_WALL_VERTEX_MISSING");
        }

        private static void DegenerateWallFailsSafely()
        {
            var level = CreateLevel(false);
            level.Vertices[1].Position = level.Vertices[0].Position;
            ExpectInvalid(() => ArchitectureWallMesher.Build(level, level.Walls[0]), "LA7_INVALID_WALL_DIMENSIONS");
        }

        private static ArchitectureLevel CreateLevel(bool withOpening)
        {
            var level = new ArchitectureLevel { Id = new LevelId("lvl"), Elevation = 0d };
            var a = new ArchitectureVertex { Id = new VertexId("a"), Position = new ArchitecturePoint(0d, 0d) };
            var b = new ArchitectureVertex { Id = new VertexId("b"), Position = new ArchitecturePoint(4d, 0d) };
            level.Vertices.Add(a); level.Vertices.Add(b);
            var wall = new ArchitectureWall { Id = new WallId("wall"), StartVertexId = a.Id, EndVertexId = b.Id, Thickness = 0.2d, Height = 3d };
            if (withOpening) wall.Openings.Add(new ArchitectureOpening { Id = new OpeningId("op_a"), WallId = wall.Id, CenterT = 0.5d, Width = 1d, Bottom = 0d, Height = 2d });
            level.Walls.Add(wall);
            return level;
        }

        private static string Signature(ArchitectureMeshData data)
        {
            return string.Join(";", data.Vertices.Select(v => $"{v.X:R},{v.Y:R},{v.Z:R}")) + "|" + string.Join(",", data.Triangles);
        }

        // Comprueba que no haya una caja cuyos límites en X/Y contengan estrictamente el punto del hueco.
        private static bool ContainsSolidCellCenter(ArchitectureMeshData data, double x, double y)
        {
            for (var i = 0; i + 7 < data.Vertices.Count; i += 8)
            {
                var minX = data.Vertices.Skip(i).Take(8).Min(v => v.X);
                var maxX = data.Vertices.Skip(i).Take(8).Max(v => v.X);
                var minY = data.Vertices.Skip(i).Take(8).Min(v => v.Y);
                var maxY = data.Vertices.Skip(i).Take(8).Max(v => v.Y);
                if (x > minX + ArchitectureGeometry.Epsilon && x < maxX - ArchitectureGeometry.Epsilon &&
                    y > minY + ArchitectureGeometry.Epsilon && y < maxY - ArchitectureGeometry.Epsilon) return true;
            }
            return false;
        }

        private static void ExpectInvalid(Action action, string token)
        {
            try { action(); }
            catch (InvalidOperationException ex) { Require(ex.Message == token, "Código de fallo inesperado: " + ex.Message); return; }
            throw new InvalidOperationException("Se esperaba InvalidOperationException " + token);
        }

        private static bool Nearly(double a, double b) => Math.Abs(a - b) <= ArchitectureGeometry.Epsilon;
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private static void Test(ArchitectureMesherSelfTestResult result, string name, Action test)
        {
            try { test(); result.Passed++; }
            catch (Exception ex) { result.Failed++; result.Failures.Add(name + ": " + ex.Message); }
        }
    }
}
