using System;
using System.Collections.Generic;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public sealed class ArchitectureOperationSelfTestResult
    {
        public int Passed;
        public int Failed;
        public readonly List<string> Failures = new List<string>();
        public bool Success => Failed == 0;
    }

    /// <summary>Self-test puro LA3; no depende de escena, GameObjects ni Presentation.</summary>
    public static class ArchitectureOperationSelfTest
    {
        public static ArchitectureOperationSelfTestResult Run()
        {
            var result = new ArchitectureOperationSelfTestResult();
            Test(result, "propuesta no muta base", ProposalDoesNotMutateBase);
            Test(result, "move vertex commit atómico", MoveVertexCommitsAtomically);
            Test(result, "operación inválida hace rollback", InvalidProposalRollsBack);
            Test(result, "stale proposal se rechaza", StaleProposalIsRejected);
            Test(result, "undo restaura fingerprint A", UndoRestoresA);
            Test(result, "redo restaura fingerprint B", RedoRestoresB);
            Test(result, "split preserva WallId original", SplitPreservesOriginalWallIdentity);
            Test(result, "split remapea apertura", SplitRemapsOpening);
            Test(result, "split que corta apertura se rechaza", SplitAcrossOpeningIsRejected);
            Test(result, "delete wall es reversible", DeleteWallIsReversible);
            Test(result, "create wall respeta invariantes", CreateWallRespectsInvariants);
            Test(result, "move wall conserva conectividad compartida", MoveWallKeepsSharedConnectivity);
            return result;
        }

        private static void ProposalDoesNotMutateBase()
        {
            var state = CreateRectangle();
            var before = state.ComputeFingerprint();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.MoveVertex, "move", s =>
                ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(7d, 0d)));
            Require(proposal.IsReady, proposal.DiagnosticCode);
            Require(state.ComputeFingerprint() == before, "La propuesta alteró A.");
            Require(proposal.ProposedFingerprint != before, "B debe diferir de A.");
        }

        private static void MoveVertexCommitsAtomically()
        {
            var state = CreateRectangle();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.MoveVertex, "move", s =>
                ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(7d, 0d)));
            Require(ArchitectureTransactionEngine.TryCommit(ref state, proposal, out var record, out var code), code);
            Require(state.FindVertex(new VertexId("v_b")).Position.Equals(new ArchitecturePoint(7d, 0d)), "Commit incompleto.");
            Require(record.AfterFingerprint == state.ComputeFingerprint(), "Registro B incorrecto.");
        }

        private static void InvalidProposalRollsBack()
        {
            var state = CreateRectangle();
            var before = state.ComputeFingerprint();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.MoveVertex, "degenerate", s =>
                ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(0d, 0d)));
            Require(!proposal.IsReady, "La propuesta degenerada debía rechazarse.");
            Require(state.ComputeFingerprint() == before, "A cambió tras rechazo.");
        }

        private static void StaleProposalIsRejected()
        {
            var state = CreateRectangle();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.MoveVertex, "stale", s =>
                ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(7d, 0d)));
            state.FindVertex(new VertexId("v_a")).Position = new ArchitecturePoint(-1d, 0d);
            var live = state.ComputeFingerprint();
            Require(!ArchitectureTransactionEngine.TryCommit(ref state, proposal, out _, out var code), "Commit stale no rechazado.");
            Require(code == "LA3_STALE_PROPOSAL", "Código stale incorrecto.");
            Require(state.ComputeFingerprint() == live, "Conflicto alteró estado live.");
        }

        private static void UndoRestoresA()
        {
            var state = CreateRectangle();
            var a = state.ComputeFingerprint();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.MoveVertex, "move", s =>
                ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(7d, 0d)));
            Require(ArchitectureTransactionEngine.TryCommit(ref state, proposal, out var record, out var code), code);
            Require(ArchitectureTransactionEngine.TryUndo(ref state, record, out code), code);
            Require(state.ComputeFingerprint() == a, "Undo no restauró exactamente A.");
        }

        private static void RedoRestoresB()
        {
            var state = CreateRectangle();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.MoveVertex, "move", s =>
                ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(7d, 0d)));
            Require(ArchitectureTransactionEngine.TryCommit(ref state, proposal, out var record, out var code), code);
            var b = state.ComputeFingerprint();
            Require(ArchitectureTransactionEngine.TryUndo(ref state, record, out code), code);
            Require(ArchitectureTransactionEngine.TryRedo(ref state, record, out code), code);
            Require(state.ComputeFingerprint() == b, "Redo no restauró exactamente B.");
        }

        private static void SplitPreservesOriginalWallIdentity()
        {
            var state = CreateRectangle();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.SplitWall, "split", s =>
                ArchitectureMutations.SplitWall(s, new WallId("w_bottom"), 0.5d, new VertexId("v_mid"), new WallId("w_bottom_2")));
            Require(proposal.IsReady, proposal.DiagnosticMessage);
            Require(proposal.ProposedSnapshot.FindWall(new WallId("w_bottom")) != null, "WallId original perdido.");
            Require(proposal.ProposedSnapshot.FindWall(new WallId("w_bottom_2")) != null, "Segundo tramo inexistente.");
        }

        private static void SplitRemapsOpening()
        {
            var state = CreateRectangle();
            var wall = state.FindWall(new WallId("w_bottom"));
            wall.Openings.Add(new ArchitectureOpening { Id = new OpeningId("door"), WallId = wall.Id, CenterT = 0.75d, Width = 1d, Bottom = 0d, Height = 2d });
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.SplitWall, "split", s =>
                ArchitectureMutations.SplitWall(s, wall.Id, 0.5d, new VertexId("v_mid"), new WallId("w_bottom_2")));
            Require(proposal.IsReady, proposal.DiagnosticMessage);
            var moved = proposal.ProposedSnapshot.FindWall(new WallId("w_bottom_2")).Openings[0];
            Require(moved.WallId.Equals(new WallId("w_bottom_2")), "Opening WallId no remapeada.");
            Require(ArchitectureGeometry.NearlyEqual(moved.CenterT, 0.5d), "CenterT remapeado incorrecto.");
        }

        private static void SplitAcrossOpeningIsRejected()
        {
            var state = CreateRectangle();
            var wall = state.FindWall(new WallId("w_bottom"));
            wall.Openings.Add(new ArchitectureOpening { Id = new OpeningId("door"), WallId = wall.Id, CenterT = 0.5d, Width = 1d, Bottom = 0d, Height = 2d });
            var before = state.ComputeFingerprint();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.SplitWall, "split", s =>
                ArchitectureMutations.SplitWall(s, wall.Id, 0.5d, new VertexId("v_mid"), new WallId("w_bottom_2")));
            Require(!proposal.IsReady, "Debía rechazarse split sobre apertura.");
            Require(state.ComputeFingerprint() == before, "Rechazo modificó A.");
        }

        private static void DeleteWallIsReversible()
        {
            var state = CreateRectangle();
            var a = state.ComputeFingerprint();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.DeleteWall, "delete", s =>
                ArchitectureMutations.DeleteWall(s, new WallId("w_top")));
            Require(ArchitectureTransactionEngine.TryCommit(ref state, proposal, out var record, out var code), code);
            Require(state.FindWall(new WallId("w_top")) == null, "Pared no eliminada.");
            Require(ArchitectureTransactionEngine.TryUndo(ref state, record, out code), code);
            Require(state.ComputeFingerprint() == a, "Undo delete no restauró A.");
        }

        private static void CreateWallRespectsInvariants()
        {
            var state = CreateOpenShape();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.CreateWall, "close", s =>
                ArchitectureMutations.CreateWall(s, new LevelId("lvl"), new VertexId("v_d"), new VertexId("v_a"), new WallId("w_left"), 0.2d, 3d));
            Require(proposal.IsReady, proposal.DiagnosticMessage);
            Require(ArchitectureRegionEngine.Build(proposal.ProposedSnapshot.FindLevel(new LevelId("lvl"))).Regions.Count == 1, "Cerrar pared debe crear una región.");
        }

        private static void MoveWallKeepsSharedConnectivity()
        {
            var state = CreateOpenShape();
            var proposal = ArchitectureTransactionEngine.Propose(state, ArchitectureOperationKind.MoveWall, "translate", s =>
                ArchitectureMutations.MoveWall(s, new WallId("w_right"), 1d, 0d));
            Require(proposal.IsReady, proposal.DiagnosticMessage);
            var right = proposal.ProposedSnapshot.FindWall(new WallId("w_right"));
            var top = proposal.ProposedSnapshot.FindWall(new WallId("w_top"));
            Require(right.EndVertexId.Equals(top.StartVertexId), "Se rompió el vértice compartido.");
        }

        private static ArchitectureSnapshot CreateRectangle()
        {
            var state = CreateOpenShape();
            ArchitectureMutations.CreateWall(state, new LevelId("lvl"), new VertexId("v_d"), new VertexId("v_a"), new WallId("w_left"), 0.2d, 3d);
            return state;
        }

        private static ArchitectureSnapshot CreateOpenShape()
        {
            var level = new ArchitectureLevel { Id = new LevelId("lvl"), Elevation = 0d };
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_a"), Position = new ArchitecturePoint(0d, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_b"), Position = new ArchitecturePoint(6d, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_c"), Position = new ArchitecturePoint(6d, 4d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_d"), Position = new ArchitecturePoint(0d, 4d) });
            level.Walls.Add(Wall("w_bottom", "v_a", "v_b"));
            level.Walls.Add(Wall("w_right", "v_b", "v_c"));
            level.Walls.Add(Wall("w_top", "v_c", "v_d"));
            return new ArchitectureSnapshot { Building = new ArchitectureBuilding { Id = new BuildingId("bld") , Levels = new List<ArchitectureLevel> { level } } };
        }

        private static ArchitectureWall Wall(string id, string start, string end)
        {
            return new ArchitectureWall { Id = new WallId(id), StartVertexId = new VertexId(start), EndVertexId = new VertexId(end), Thickness = 0.2d, Height = 3d };
        }

        private static void Test(ArchitectureOperationSelfTestResult result, string name, Action test)
        {
            try { test(); result.Passed++; }
            catch (Exception ex) { result.Failed++; result.Failures.Add(name + ": " + ex.Message); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message ?? "Condición LA3 no satisfecha.");
        }
    }
}
