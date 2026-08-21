using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Self-test puro LA9. No requiere escena ni GameObjects.
    /// </summary>
    public static class ArchitectureEditSessionSelfTest
    {
        public static IReadOnlyList<string> Run()
        {
            var failures = new List<string>();
            RunCase(failures, "01_preview_create_is_pure", PreviewCreateIsPure);
            RunCase(failures, "02_confirm_create_changes_current", ConfirmCreateChangesCurrent);
            RunCase(failures, "03_cancel_restores_visible_current", CancelRestoresVisibleCurrent);
            RunCase(failures, "04_move_wall_preview_is_pure", MoveWallPreviewIsPure);
            RunCase(failures, "05_numeric_length_edit", NumericLengthEdit);
            RunCase(failures, "06_delete_selected_wall", DeleteSelectedWall);
            RunCase(failures, "07_undo_redo_semantic", UndoRedoSemantic);
            RunCase(failures, "08_redo_cleared_after_new_commit", RedoClearedAfterNewCommit);
            RunCase(failures, "09_snap_reuses_existing_vertex", SnapReusesExistingVertex);
            RunCase(failures, "10_invalid_length_rejected", InvalidLengthRejected);
            RunCase(failures, "11_selection_is_stable_id", SelectionIsStableId);
            RunCase(failures, "12_visible_snapshot_isolated", VisibleSnapshotIsolated);
            return failures;
        }

        private static void PreviewCreateIsPure()
        {
            var fixture = CreateFixture(false);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            var before = session.CaptureCurrent().ComputeFingerprint();
            var proposal = session.PreviewCreateWall(fixture.LevelId, new ArchitecturePoint(0, 0), new ArchitecturePoint(4, 0), useSnap: false);
            Require(proposal.IsReady, "proposal not ready");
            Require(before == session.CaptureCurrent().ComputeFingerprint(), "preview mutated current");
            Require(session.CaptureVisible().FindWall(session.SelectedWallId) != null, "preview not visible");
        }

        private static void ConfirmCreateChangesCurrent()
        {
            var fixture = CreateFixture(false);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            var before = session.CaptureCurrent().ComputeFingerprint();
            session.PreviewCreateWall(fixture.LevelId, new ArchitecturePoint(0, 0), new ArchitecturePoint(4, 0), useSnap: false);
            string code;
            Require(session.TryConfirm(out code), "confirm failed: " + code);
            Require(before != session.CaptureCurrent().ComputeFingerprint(), "commit did not change current");
            Require(session.CanUndo, "undo not available");
        }

        private static void CancelRestoresVisibleCurrent()
        {
            var fixture = CreateFixture(false);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            var before = session.CaptureCurrent().ComputeFingerprint();
            session.PreviewCreateWall(fixture.LevelId, new ArchitecturePoint(0, 0), new ArchitecturePoint(4, 0), useSnap: false);
            Require(session.CaptureVisible().ComputeFingerprint() != before, "preview not visible");
            session.CancelPreview();
            Require(session.CaptureVisible().ComputeFingerprint() == before, "cancel did not restore visible current");
        }

        private static void MoveWallPreviewIsPure()
        {
            var fixture = CreateFixture(true);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            var before = session.CaptureCurrent().ComputeFingerprint();
            var proposal = session.PreviewMoveWall(fixture.WallId, 1, 2);
            Require(proposal.IsReady, "move proposal rejected");
            Require(before == session.CaptureCurrent().ComputeFingerprint(), "move preview mutated current");
            var visibleWall = session.CaptureVisible().FindWall(fixture.WallId);
            var visibleLevel = session.CaptureVisible().FindLevel(fixture.LevelId);
            var start = visibleLevel.Vertices.First(v => v.Id.Equals(visibleWall.StartVertexId));
            Require(ArchitectureGeometry.NearlyEqual(start.Position.X, 1) && ArchitectureGeometry.NearlyEqual(start.Position.Y, 2), "preview delta wrong");
        }

        private static void NumericLengthEdit()
        {
            var fixture = CreateFixture(true);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            session.SelectWall(fixture.WallId);
            var proposal = session.PreviewSetWallLength(fixture.WallId, 6d);
            Require(proposal.IsReady, "numeric proposal rejected");
            string code;
            Require(session.TryConfirm(out code), "numeric confirm failed: " + code);
            var current = session.CaptureCurrent();
            var wall = current.FindWall(fixture.WallId);
            var level = current.FindLevel(fixture.LevelId);
            var a = level.Vertices.First(v => v.Id.Equals(wall.StartVertexId)).Position;
            var b = level.Vertices.First(v => v.Id.Equals(wall.EndVertexId)).Position;
            Require(ArchitectureGeometry.NearlyEqual(a.DistanceTo(b), 6d), "length not applied");
        }

        private static void DeleteSelectedWall()
        {
            var fixture = CreateFixture(true);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            Require(session.SelectWall(fixture.WallId), "selection failed");
            Require(session.PreviewDeleteSelectedWall().IsReady, "delete preview rejected");
            string code;
            Require(session.TryConfirm(out code), "delete confirm failed: " + code);
            Require(session.CaptureCurrent().FindWall(fixture.WallId) == null, "wall still exists");
        }

        private static void UndoRedoSemantic()
        {
            var fixture = CreateFixture(true);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            var before = session.CaptureCurrent().ComputeFingerprint();
            session.PreviewMoveWall(fixture.WallId, 2, 0);
            string code;
            Require(session.TryConfirm(out code), "commit failed: " + code);
            var after = session.CaptureCurrent().ComputeFingerprint();
            Require(session.TryUndo(out code), "undo failed: " + code);
            Require(session.CaptureCurrent().ComputeFingerprint() == before, "undo fingerprint mismatch");
            Require(session.TryRedo(out code), "redo failed: " + code);
            Require(session.CaptureCurrent().ComputeFingerprint() == after, "redo fingerprint mismatch");
        }

        private static void RedoClearedAfterNewCommit()
        {
            var fixture = CreateFixture(true);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            string code;
            session.PreviewMoveWall(fixture.WallId, 1, 0);
            Require(session.TryConfirm(out code), "first commit failed");
            Require(session.TryUndo(out code), "undo failed");
            Require(session.CanRedo, "redo expected");
            session.PreviewMoveWall(fixture.WallId, 0, 1);
            Require(session.TryConfirm(out code), "second commit failed");
            Require(!session.CanRedo, "redo not cleared by divergent commit");
        }

        private static void SnapReusesExistingVertex()
        {
            var fixture = CreateFixture(true);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            var existingCount = fixture.Snapshot.FindLevel(fixture.LevelId).Vertices.Count;
            var proposal = session.PreviewCreateWall(
                fixture.LevelId,
                new ArchitecturePoint(0.05, 0.02),
                new ArchitecturePoint(2, 2),
                useSnap: true,
                snapDistance: 0.2d);
            Require(proposal.IsReady, "snap create rejected");
            var proposedLevel = proposal.ProposedSnapshot.FindLevel(fixture.LevelId);
            Require(proposedLevel.Vertices.Count == existingCount + 1, "existing snapped vertex was duplicated");
        }

        private static void InvalidLengthRejected()
        {
            var fixture = CreateFixture(true);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            var before = session.CaptureCurrent().ComputeFingerprint();
            var proposal = session.PreviewSetWallLength(fixture.WallId, 0d);
            Require(proposal.Status == ArchitectureProposalStatus.Rejected, "zero length accepted");
            string code;
            Require(!session.TryConfirm(out code), "rejected preview confirmed");
            Require(before == session.CaptureCurrent().ComputeFingerprint(), "invalid edit mutated current");
        }

        private static void SelectionIsStableId()
        {
            var fixture = CreateFixture(true);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            Require(session.SelectWall(fixture.WallId), "wall selection failed");
            Require(session.SelectedWallId.Equals(fixture.WallId), "selection does not preserve WallId");
            session.PreviewMoveWall(fixture.WallId, 1, 0);
            string code;
            Require(session.TryConfirm(out code), "move confirm failed");
            Require(session.SelectedWallId.Equals(fixture.WallId), "selection identity changed after edit");
        }

        private static void VisibleSnapshotIsolated()
        {
            var fixture = CreateFixture(true);
            var session = new ArchitectureEditSession(fixture.Snapshot);
            var before = session.CaptureCurrent().ComputeFingerprint();
            var visible = session.CaptureVisible();
            visible.FindVertex(fixture.StartVertexId).Position = new ArchitecturePoint(99, 99);
            Require(session.CaptureCurrent().ComputeFingerprint() == before, "consumer mutated session through visible snapshot");
        }

        private static Fixture CreateFixture(bool withWall)
        {
            var levelId = new LevelId("lvl_la9_test");
            var startId = new VertexId("vtx_la9_a");
            var endId = new VertexId("vtx_la9_b");
            var wallId = new WallId("wal_la9_main");
            var level = new ArchitectureLevel { Id = levelId, Elevation = 0d };
            if (withWall)
            {
                level.Vertices.Add(new ArchitectureVertex { Id = startId, Position = new ArchitecturePoint(0, 0) });
                level.Vertices.Add(new ArchitectureVertex { Id = endId, Position = new ArchitecturePoint(4, 0) });
                level.Walls.Add(new ArchitectureWall
                {
                    Id = wallId,
                    StartVertexId = startId,
                    EndVertexId = endId,
                    Thickness = 0.15d,
                    Height = 2.8d
                });
            }
            var building = new ArchitectureBuilding { Id = new BuildingId("bld_la9_test") };
            building.Levels.Add(level);
            return new Fixture
            {
                Snapshot = new ArchitectureSnapshot { Building = building },
                LevelId = levelId,
                StartVertexId = startId,
                EndVertexId = endId,
                WallId = wallId
            };
        }

        private static void RunCase(ICollection<string> failures, string name, Action action)
        {
            try { action(); }
            catch (Exception ex) { failures.Add(name + ": " + ex.Message); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Fixture
        {
            public ArchitectureSnapshot Snapshot;
            public LevelId LevelId;
            public VertexId StartVertexId;
            public VertexId EndVertexId;
            public WallId WallId;
        }
    }
}
