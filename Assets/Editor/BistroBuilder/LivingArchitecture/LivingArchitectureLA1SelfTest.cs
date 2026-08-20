#if UNITY_EDITOR
using System;
using BistroBuilder.LivingArchitecture.Domain;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.LivingArchitecture.Editor
{
    /// <summary>
    /// Autotest estático LA1. No requiere escena ni GameObjects.
    /// Comprueba DeepClone, fingerprint e invariantes esenciales del kernel.
    /// </summary>
    public static class LivingArchitectureLA1SelfTest
    {
        [MenuItem("Bistro Builder/Living Architecture/LA1/Run Self Test")]
        public static void Run()
        {
            var snapshot = BuildFixture();
            Assert(ArchitectureValidator.Validate(snapshot).IsValid, "Fixture válida rechazada.");

            var originalFingerprint = snapshot.ComputeFingerprint();
            var clone = snapshot.DeepClone();
            Assert(originalFingerprint == clone.ComputeFingerprint(), "DeepClone debe conservar fingerprint.");

            var movedVertex = clone.Building.Levels[0].Vertices[1];
            movedVertex.Position = new ArchitecturePoint(6d, 0d);
            Assert(originalFingerprint != clone.ComputeFingerprint(), "Mutar clone debe cambiar solo su fingerprint.");
            Assert(originalFingerprint == snapshot.ComputeFingerprint(), "DeepClone no puede aliasar el snapshot original.");

            var duplicate = snapshot.DeepClone();
            duplicate.Building.Levels[0].Vertices.Add(duplicate.Building.Levels[0].Vertices[0].DeepClone());
            Assert(!ArchitectureValidator.Validate(duplicate).IsValid, "VertexId duplicado debe ser inválido.");

            var missingVertex = snapshot.DeepClone();
            missingVertex.Building.Levels[0].Walls[0].EndVertexId = new VertexId("vtx_missing");
            Assert(!ArchitectureValidator.Validate(missingVertex).IsValid, "Pared con vértice inexistente debe ser inválida.");

            var invalidOpening = snapshot.DeepClone();
            invalidOpening.Building.Levels[0].Walls[0].Openings[0].Width = 20d;
            Assert(!ArchitectureValidator.Validate(invalidOpening).IsValid, "Apertura fuera del dominio debe ser inválida.");

            Debug.Log("[BB Living Architecture][LA1] SELF TEST PASS — kernel, DeepClone, fingerprint e invariantes básicos correctos. Pendiente compilación/ejecución real en Unity.");
        }

        private static ArchitectureSnapshot BuildFixture()
        {
            var buildingId = new BuildingId("bld_la1_fixture");
            var levelId = new LevelId("lvl_la1_fixture");
            var a = new ArchitectureVertex { Id = new VertexId("vtx_a"), Position = new ArchitecturePoint(0d, 0d) };
            var b = new ArchitectureVertex { Id = new VertexId("vtx_b"), Position = new ArchitecturePoint(5d, 0d) };
            var wallId = new WallId("wal_ab");
            var wall = new ArchitectureWall
            {
                Id = wallId,
                StartVertexId = a.Id,
                EndVertexId = b.Id,
                Thickness = 0.15d,
                Height = 3d
            };
            wall.Openings.Add(new ArchitectureOpening
            {
                Id = new OpeningId("opn_door"),
                WallId = wallId,
                CenterT = 0.5d,
                Width = 0.9d,
                Bottom = 0d,
                Height = 2.1d
            });

            var level = new ArchitectureLevel { Id = levelId, Elevation = 0d };
            level.Vertices.Add(a);
            level.Vertices.Add(b);
            level.Walls.Add(wall);

            var building = new ArchitectureBuilding { Id = buildingId };
            building.Levels.Add(level);
            return new ArchitectureSnapshot { Building = building };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BB Living Architecture][LA1] " + message);
        }
    }
}
#endif
