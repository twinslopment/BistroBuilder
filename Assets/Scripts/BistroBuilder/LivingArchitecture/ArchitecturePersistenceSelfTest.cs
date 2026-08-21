using System;
using System.Collections.Generic;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>Self-test puro LA8: round-trip, IDs, migración y rechazo seguro.</summary>
    public static class ArchitecturePersistenceSelfTest
    {
        public static IReadOnlyList<string> Run()
        {
            var failures = new List<string>();
            RunCase(failures, "round_trip_fingerprint", TestRoundTripFingerprint);
            RunCase(failures, "ids_preserved", TestIdsPreserved);
            RunCase(failures, "capture_deterministic_order", TestCaptureDeterministicOrder);
            RunCase(failures, "opening_preserved", TestOpeningPreserved);
            RunCase(failures, "elevation_preserved", TestElevationPreserved);
            RunCase(failures, "legacy_v0_migrates", TestLegacyMigration);
            RunCase(failures, "future_schema_rejected", TestFutureSchemaRejected);
            RunCase(failures, "invalid_topology_rejected", TestInvalidTopologyRejected);
            RunCase(failures, "capture_does_not_mutate", TestCaptureDoesNotMutate);
            RunCase(failures, "restore_independent_clone", TestRestoreIndependentClone);
            return failures;
        }

        private static ArchitectureBuilding Fixture()
        {
            var b = new ArchitectureBuilding { Id = new BuildingId("bld_la8") };
            var l = new ArchitectureLevel { Id = new LevelId("lvl_la8"), Elevation = 1.25d };
            var a = new ArchitectureVertex { Id = new VertexId("vtx_a"), Position = new ArchitecturePoint(0d, 0d) };
            var c = new ArchitectureVertex { Id = new VertexId("vtx_c"), Position = new ArchitecturePoint(4d, 0d) };
            l.Vertices.Add(c); l.Vertices.Add(a);
            var w = new ArchitectureWall { Id = new WallId("wal_a"), StartVertexId = a.Id, EndVertexId = c.Id, Thickness = 0.18d, Height = 3.2d };
            w.Openings.Add(new ArchitectureOpening { Id = new OpeningId("opn_a"), WallId = w.Id, CenterT = 0.5d, Width = 1d, Bottom = 0d, Height = 2.1d });
            l.Walls.Add(w); b.Levels.Add(l); return b;
        }

        private static void TestRoundTripFingerprint()
        {
            var b = Fixture();
            var before = new ArchitectureSnapshot { Building = b }.ComputeFingerprint();
            if (!ArchitecturePersistence.TryRestore(ArchitecturePersistence.Capture(b), out var restored, out var error)) throw new Exception(error);
            var after = new ArchitectureSnapshot { Building = restored }.ComputeFingerprint();
            if (!string.Equals(before, after, StringComparison.Ordinal)) throw new Exception("fingerprint mismatch");
        }

        private static void TestIdsPreserved()
        {
            ArchitecturePersistence.TryRestore(ArchitecturePersistence.Capture(Fixture()), out var b, out var error);
            if (b == null) throw new Exception(error);
            if (b.Id.Value != "bld_la8" || b.Levels[0].Id.Value != "lvl_la8" || b.Levels[0].Walls[0].Id.Value != "wal_a") throw new Exception("IDs not preserved");
        }

        private static void TestCaptureDeterministicOrder()
        {
            var b = Fixture();
            var state = ArchitecturePersistence.Capture(b);
            if (state.levels[0].vertices[0].vertexId != "vtx_a") throw new Exception("vertices not ordered by ID");
        }

        private static void TestOpeningPreserved()
        {
            ArchitecturePersistence.TryRestore(ArchitecturePersistence.Capture(Fixture()), out var b, out var error);
            if (b == null) throw new Exception(error);
            var o = b.Levels[0].Walls[0].Openings[0];
            if (o.Id.Value != "opn_a" || o.WallId.Value != "wal_a" || !ArchitectureGeometry.NearlyEqual(o.Width, 1d)) throw new Exception("opening mismatch");
        }

        private static void TestElevationPreserved()
        {
            ArchitecturePersistence.TryRestore(ArchitecturePersistence.Capture(Fixture()), out var b, out var error);
            if (b == null || !ArchitectureGeometry.NearlyEqual(b.Levels[0].Elevation, 1.25d)) throw new Exception(error ?? "elevation mismatch");
        }

        private static void TestLegacyMigration()
        {
            var state = ArchitecturePersistence.Capture(Fixture());
            state.schemaVersion = 0;
            state.levels[0].walls[0].thickness = 0d;
            state.levels[0].walls[0].height = 0d;
            state.levels[0].walls[0].openings[0].wallId = string.Empty;
            if (!ArchitecturePersistence.TryMigrate(state, out var migrated, out var error)) throw new Exception(error);
            var wall = migrated.levels[0].walls[0];
            if (migrated.schemaVersion != 1 || wall.thickness <= 0d || wall.height <= 0d || wall.openings[0].wallId != wall.wallId) throw new Exception("legacy migration incomplete");
        }

        private static void TestFutureSchemaRejected()
        {
            var state = ArchitecturePersistence.Capture(Fixture()); state.schemaVersion = ArchitecturePersistence.CurrentSchemaVersion + 1;
            if (ArchitecturePersistence.TryRestore(state, out _, out _)) throw new Exception("future schema accepted");
        }

        private static void TestInvalidTopologyRejected()
        {
            var state = ArchitecturePersistence.Capture(Fixture()); state.levels[0].walls[0].endVertexId = "missing";
            if (ArchitecturePersistence.TryRestore(state, out _, out _)) throw new Exception("invalid topology accepted");
        }

        private static void TestCaptureDoesNotMutate()
        {
            var b = Fixture(); var before = new ArchitectureSnapshot { Building = b }.ComputeFingerprint();
            ArchitecturePersistence.Capture(b);
            var after = new ArchitectureSnapshot { Building = b }.ComputeFingerprint();
            if (before != after) throw new Exception("capture mutated domain");
        }

        private static void TestRestoreIndependentClone()
        {
            var state = ArchitecturePersistence.Capture(Fixture());
            ArchitecturePersistence.TryRestore(state, out var b, out var error);
            if (b == null) throw new Exception(error);
            state.levels[0].vertices[0].x = 99d;
            if (ArchitectureGeometry.NearlyEqual(b.Levels[0].Vertices[0].Position.X, 99d)) throw new Exception("restored graph aliases DTO");
        }

        private static void RunCase(List<string> failures, string name, Action test)
        {
            try { test(); }
            catch (Exception ex) { failures.Add(name + ": " + ex.Message); }
        }
    }
}
