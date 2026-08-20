using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Resultado compacto del gate puro LA2. No requiere escena, MonoBehaviour ni GameObject.
    /// </summary>
    public sealed class ArchitectureRegionSelfTestResult
    {
        public int Passed;
        public int Failed;
        public readonly List<string> Failures = new List<string>();
        public bool Success => Failed == 0;
    }

    /// <summary>
    /// Autotest determinista de regiones: detección, partición, identidad, área y consultas espaciales.
    /// Unity deberá ejecutarlo posteriormente como parte del cierre real del hito.
    /// </summary>
    public static class ArchitectureRegionSelfTest
    {
        public static ArchitectureRegionSelfTestResult Run()
        {
            var result = new ArchitectureRegionSelfTestResult();
            Test(result, "rectángulo genera una región", RectangleProducesOneRegion);
            Test(result, "área y centroide son correctos", AreaAndCentroidAreCorrect);
            Test(result, "punto interior localiza región", InteriorPointFindsRegion);
            Test(result, "punto exterior no localiza región", ExteriorPointFindsNothing);
            Test(result, "borde pertenece a la región", BoundaryPointIsContained);
            Test(result, "pared divisoria produce dos regiones", DividerProducesTwoRegions);
            Test(result, "identidad no depende del orden de listas", IdentityIsOrderIndependent);
            Test(result, "grafo abierto no inventa habitaciones", OpenGraphProducesNoRegions);
            Test(result, "dos recintos desconectados se detectan", DisconnectedRoomsAreDetected);
            return result;
        }

        private static void RectangleProducesOneRegion()
        {
            var regions = ArchitectureRegionEngine.Build(CreateRectangleLevel());
            Require(regions.Regions.Count == 1, "Se esperaba exactamente 1 región.");
        }

        private static void AreaAndCentroidAreCorrect()
        {
            var region = ArchitectureRegionEngine.Build(CreateRectangleLevel()).Regions.Single();
            Require(ArchitectureGeometry.NearlyEqual(region.Area, 24d), "Área esperada: 24 m².");
            Require(ArchitectureGeometry.NearlyEqual(region.Centroid.X, 3d), "Centroide X esperado: 3.");
            Require(ArchitectureGeometry.NearlyEqual(region.Centroid.Y, 2d), "Centroide Y esperado: 2.");
        }

        private static void InteriorPointFindsRegion()
        {
            var regions = ArchitectureRegionEngine.Build(CreateRectangleLevel());
            Require(regions.FindContaining(new ArchitecturePoint(2d, 2d)) != null, "El punto interior debe resolver una región.");
        }

        private static void ExteriorPointFindsNothing()
        {
            var regions = ArchitectureRegionEngine.Build(CreateRectangleLevel());
            Require(regions.FindContaining(new ArchitecturePoint(8d, 8d)) == null, "El punto exterior no debe resolver región.");
        }

        private static void BoundaryPointIsContained()
        {
            var region = ArchitectureRegionEngine.Build(CreateRectangleLevel()).Regions.Single();
            Require(region.Contains(new ArchitecturePoint(0d, 2d)), "El borde debe considerarse contenido para consultas arquitectónicas.");
        }

        private static void DividerProducesTwoRegions()
        {
            var level = CreateRectangleLevel();
            level.Vertices.Add(Vertex("v_mid_bottom", 3d, 0d));
            level.Vertices.Add(Vertex("v_mid_top", 3d, 4d));

            // Sustituye los tramos horizontal inferior/superior por segmentos conectados al divisor.
            level.Walls.RemoveAll(x => x.Id.Value == "w_bottom" || x.Id.Value == "w_top");
            level.Walls.Add(Wall("w_bottom_l", "v_a", "v_mid_bottom"));
            level.Walls.Add(Wall("w_bottom_r", "v_mid_bottom", "v_b"));
            level.Walls.Add(Wall("w_top_r", "v_c", "v_mid_top"));
            level.Walls.Add(Wall("w_top_l", "v_mid_top", "v_d"));
            level.Walls.Add(Wall("w_divider", "v_mid_bottom", "v_mid_top"));

            var regions = ArchitectureRegionEngine.Build(level);
            Require(regions.Regions.Count == 2, "El divisor debe producir exactamente 2 regiones.");
            Require(regions.Regions.All(x => ArchitectureGeometry.NearlyEqual(x.Area, 12d)), "Ambas regiones deben medir 12 m².");
        }

        private static void IdentityIsOrderIndependent()
        {
            var levelA = CreateRectangleLevel();
            var levelB = CreateRectangleLevel();
            levelB.Vertices.Reverse();
            levelB.Walls.Reverse();

            var idA = ArchitectureRegionEngine.Build(levelA).Regions.Single().Id.Value;
            var idB = ArchitectureRegionEngine.Build(levelB).Regions.Single().Id.Value;
            Require(string.Equals(idA, idB, StringComparison.Ordinal), "RegionId debe ser estable ante reordenación de colecciones.");
        }

        private static void OpenGraphProducesNoRegions()
        {
            var level = new ArchitectureLevel { Id = new LevelId("lvl_open") };
            level.Vertices.Add(Vertex("ov1", 0d, 0d));
            level.Vertices.Add(Vertex("ov2", 2d, 0d));
            level.Vertices.Add(Vertex("ov3", 2d, 2d));
            level.Walls.Add(Wall("ow1", "ov1", "ov2"));
            level.Walls.Add(Wall("ow2", "ov2", "ov3"));
            Require(ArchitectureRegionEngine.Build(level).Regions.Count == 0, "Un grafo abierto no debe producir regiones.");
        }

        private static void DisconnectedRoomsAreDetected()
        {
            var level = CreateRectangleLevel();
            AddRectangle(level, "b", 10d, 0d, 2d, 2d);
            var regions = ArchitectureRegionEngine.Build(level);
            Require(regions.Regions.Count == 2, "Se esperaban 2 recintos desconectados.");
        }

        private static ArchitectureLevel CreateRectangleLevel()
        {
            var level = new ArchitectureLevel { Id = new LevelId("lvl_test"), Elevation = 0d };
            level.Vertices.Add(Vertex("v_a", 0d, 0d));
            level.Vertices.Add(Vertex("v_b", 6d, 0d));
            level.Vertices.Add(Vertex("v_c", 6d, 4d));
            level.Vertices.Add(Vertex("v_d", 0d, 4d));
            level.Walls.Add(Wall("w_bottom", "v_a", "v_b"));
            level.Walls.Add(Wall("w_right", "v_b", "v_c"));
            level.Walls.Add(Wall("w_top", "v_c", "v_d"));
            level.Walls.Add(Wall("w_left", "v_d", "v_a"));
            return level;
        }

        private static void AddRectangle(ArchitectureLevel level, string prefix, double x, double y, double width, double height)
        {
            var a = prefix + "_a"; var b = prefix + "_b"; var c = prefix + "_c"; var d = prefix + "_d";
            level.Vertices.Add(Vertex(a, x, y));
            level.Vertices.Add(Vertex(b, x + width, y));
            level.Vertices.Add(Vertex(c, x + width, y + height));
            level.Vertices.Add(Vertex(d, x, y + height));
            level.Walls.Add(Wall(prefix + "_w1", a, b));
            level.Walls.Add(Wall(prefix + "_w2", b, c));
            level.Walls.Add(Wall(prefix + "_w3", c, d));
            level.Walls.Add(Wall(prefix + "_w4", d, a));
        }

        private static ArchitectureVertex Vertex(string id, double x, double y)
        {
            return new ArchitectureVertex { Id = new VertexId(id), Position = new ArchitecturePoint(x, y) };
        }

        private static ArchitectureWall Wall(string id, string start, string end)
        {
            return new ArchitectureWall
            {
                Id = new WallId(id), StartVertexId = new VertexId(start), EndVertexId = new VertexId(end), Thickness = 0.15d, Height = 3d
            };
        }

        private static void Test(ArchitectureRegionSelfTestResult result, string name, Action action)
        {
            try { action(); result.Passed++; }
            catch (Exception ex) { result.Failed++; result.Failures.Add(name + ": " + ex.Message); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
