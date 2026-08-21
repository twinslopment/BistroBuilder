using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public sealed class ArchitectureIntentSelfTestResult
    {
        public int Passed;
        public int Failed;
        public readonly List<string> Failures = new List<string>();
        public bool Success => Failed == 0;
    }

    /// <summary>Self-test puro LA4: intención y restricciones, sin escena ni Presentation.</summary>
    public static class ArchitectureIntentSelfTest
    {
        public static ArchitectureIntentSelfTestResult Run()
        {
            var result = new ArchitectureIntentSelfTestResult();
            Test(result, "longitud exacta determinista", ExactLengthIsPreserved);
            Test(result, "ángulo exacto determinista", ExactAngleIsPreserved);
            Test(result, "vértice fijado prevalece", FixedVertexIsPreserved);
            Test(result, "apertura centrada se conserva", CenteredOpeningIsPreserved);
            Test(result, "offset de apertura se conserva", OpeningOffsetIsPreserved);
            Test(result, "área compatible se acepta", CompatibleAreaIsAccepted);
            Test(result, "área incompatible se rechaza sin mutar A", IncompatibleAreaRejectsWithoutMutation);
            Test(result, "restricción advisory informa sin bloquear", AdvisoryDoesNotBlock);
            Test(result, "mismo input produce mismo B", SameInputIsDeterministic);
            return result;
        }

        private static void ExactLengthIsPreserved()
        {
            var state = CreateRectangle();
            var intent = Intent(new ArchitectureConstraint { Kind = ArchitectureConstraintKind.WallLength, EntityId = "w_bottom", TargetValue = 5d });
            var r = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.MoveVertex, intent,
                s => ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(8d, 0d)));
            Require(r.IsReady, r.Proposal?.DiagnosticCode);
            var wall = r.Proposal.ProposedSnapshot.FindWall(new WallId("w_bottom"));
            var a = r.Proposal.ProposedSnapshot.FindVertex(wall.StartVertexId);
            var b = r.Proposal.ProposedSnapshot.FindVertex(wall.EndVertexId);
            Require(ArchitectureGeometry.NearlyEqual(a.Position.DistanceTo(b.Position), 5d), "Longitud no preservada.");
        }

        private static void ExactAngleIsPreserved()
        {
            var state = CreateRectangle();
            var intent = Intent(new ArchitectureConstraint { Kind = ArchitectureConstraintKind.WallAngle, EntityId = "w_bottom", TargetValue = 90d, Tolerance = 0.0001d });
            var r = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.MoveVertex, intent,
                s => ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(6d, 2d)));
            Require(r.IsReady, r.Proposal?.DiagnosticCode);
            var p = r.Proposal.ProposedSnapshot.FindVertex(new VertexId("v_b")).Position;
            Require(Math.Abs(p.X) <= 0.001d && p.Y > 0d, "Ángulo no preservado.");
        }

        private static void FixedVertexIsPreserved()
        {
            var state = CreateRectangle();
            var fixedPoint = state.FindVertex(new VertexId("v_b")).Position;
            var intent = Intent(new ArchitectureConstraint { Kind = ArchitectureConstraintKind.FixedVertex, EntityId = "v_b", TargetPoint = fixedPoint });
            var r = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.MoveVertex, intent,
                s => ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(8d, 1d)));
            Require(r.IsReady, r.Proposal?.DiagnosticCode);
            Require(r.Proposal.ProposedSnapshot.FindVertex(new VertexId("v_b")).Position.Equals(fixedPoint), "Ancla desplazada.");
        }

        private static void CenteredOpeningIsPreserved()
        {
            var state = CreateRectangleWithDoor(0.5d);
            var intent = Intent(new ArchitectureConstraint { Kind = ArchitectureConstraintKind.OpeningCentered, EntityId = "door" });
            var r = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.MoveVertex, intent,
                s => ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(8d, 0d)));
            Require(r.IsReady, r.Proposal?.DiagnosticCode);
            Require(ArchitectureGeometry.NearlyEqual(r.Proposal.ProposedSnapshot.FindWall(new WallId("w_bottom")).Openings[0].CenterT, 0.5d), "Apertura dejó de estar centrada.");
        }

        private static void OpeningOffsetIsPreserved()
        {
            var state = CreateRectangleWithDoor(0.4d); // 2 m desde el inicio sobre 5 m.
            var intent = Intent(new ArchitectureConstraint { Kind = ArchitectureConstraintKind.OpeningOffsetFromStart, EntityId = "door", TargetValue = 2d });
            var r = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.MoveVertex, intent,
                s => ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(8d, 0d)));
            Require(r.IsReady, r.Proposal?.DiagnosticCode);
            var opening = r.Proposal.ProposedSnapshot.FindWall(new WallId("w_bottom")).Openings[0];
            Require(ArchitectureGeometry.NearlyEqual(opening.CenterT * 8d, 2d), "Offset físico no preservado.");
        }

        private static void CompatibleAreaIsAccepted()
        {
            var state = CreateRectangle();
            var region = ArchitectureRegionEngine.Build(state.FindLevel(new LevelId("level"))).Regions.Single();
            var intent = Intent(new ArchitectureConstraint { Kind = ArchitectureConstraintKind.RegionArea, RegionId = region.Id.Value, TargetValue = region.Area });
            var r = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.Composite, intent, s => { });
            Require(r.IsReady, r.Proposal?.DiagnosticCode);
        }

        private static void IncompatibleAreaRejectsWithoutMutation()
        {
            var state = CreateRectangle();
            var before = state.ComputeFingerprint();
            var region = ArchitectureRegionEngine.Build(state.FindLevel(new LevelId("level"))).Regions.Single();
            var intent = Intent(new ArchitectureConstraint { Kind = ArchitectureConstraintKind.RegionArea, RegionId = region.Id.Value, TargetValue = region.Area });
            var r = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.MoveVertex, intent,
                s => ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(8d, 0d)));
            Require(!r.IsReady, "Área incompatible debía bloquear commit.");
            Require(state.ComputeFingerprint() == before, "A cambió al rechazar área.");
        }

        private static void AdvisoryDoesNotBlock()
        {
            var state = CreateRectangle();
            var intent = Intent(new ArchitectureConstraint { Kind = ArchitectureConstraintKind.RegionArea, RegionId = "missing", TargetValue = 99d, Severity = ArchitectureConstraintSeverity.Advisory });
            var r = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.Composite, intent, s => { });
            Require(r.IsReady, r.Proposal?.DiagnosticCode);
            Require(r.Evaluations.Count == 1 && !r.Evaluations[0].Satisfied, "Advisory debía informar incumplimiento.");
        }

        private static void SameInputIsDeterministic()
        {
            var state = CreateRectangle();
            var intent = Intent(new ArchitectureConstraint { Kind = ArchitectureConstraintKind.WallLength, EntityId = "w_bottom", TargetValue = 7d });
            var r1 = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.MoveVertex, intent, s => ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(9d, 1d)));
            var r2 = ArchitectureIntentEngine.Propose(state, ArchitectureOperationKind.MoveVertex, intent, s => ArchitectureMutations.MoveVertex(s, new VertexId("v_b"), new ArchitecturePoint(9d, 1d)));
            Require(r1.IsReady && r2.IsReady, "Propuestas no listas.");
            Require(r1.Proposal.ProposedFingerprint == r2.Proposal.ProposedFingerprint, "Resultado no determinista.");
        }

        private static ArchitectureIntent Intent(ArchitectureConstraint constraint)
        {
            var intent = new ArchitectureIntent { Label = "LA4 self-test" };
            intent.Constraints.Add(constraint);
            return intent;
        }

        private static ArchitectureSnapshot CreateRectangleWithDoor(double centerT)
        {
            var state = CreateRectangle();
            state.FindWall(new WallId("w_bottom")).Openings.Add(new ArchitectureOpening
            {
                Id = new OpeningId("door"), WallId = new WallId("w_bottom"), CenterT = centerT, Width = 0.8d, Bottom = 0d, Height = 2d
            });
            return state;
        }

        private static ArchitectureSnapshot CreateRectangle()
        {
            var level = new ArchitectureLevel { Id = new LevelId("level"), Elevation = 0d };
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_a"), Position = new ArchitecturePoint(0d, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_b"), Position = new ArchitecturePoint(5d, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_c"), Position = new ArchitecturePoint(5d, 4d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_d"), Position = new ArchitecturePoint(0d, 4d) });
            level.Walls.Add(Wall("w_bottom", "v_a", "v_b"));
            level.Walls.Add(Wall("w_right", "v_b", "v_c"));
            level.Walls.Add(Wall("w_top", "v_c", "v_d"));
            level.Walls.Add(Wall("w_left", "v_d", "v_a"));
            var building = new ArchitectureBuilding { Id = new BuildingId("building") };
            building.Levels.Add(level);
            return new ArchitectureSnapshot { Building = building };
        }

        private static ArchitectureWall Wall(string id, string a, string b)
        {
            return new ArchitectureWall { Id = new WallId(id), StartVertexId = new VertexId(a), EndVertexId = new VertexId(b), Thickness = 0.15d, Height = 3d };
        }

        private static void Test(ArchitectureIntentSelfTestResult result, string name, Action action)
        {
            try { action(); result.Passed++; }
            catch (Exception ex) { result.Failed++; result.Failures.Add(name + ": " + ex.Message); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "Fallo LA4." : message);
        }
    }
}
