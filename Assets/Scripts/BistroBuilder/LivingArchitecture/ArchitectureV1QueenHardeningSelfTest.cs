using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// LA11 — hardening complementario del Queen Test V1.
    /// Cierra dos fronteras de autoridad que el flujo Queen principal no ejercita directamente:
    /// feedback LA10 como presentación pura y meshing LA7 como derivado reconstruible tras Save/Load.
    /// </summary>
    public static class ArchitectureV1QueenHardeningSelfTest
    {
        public const int CaseCount = 2;

        private static readonly BuildingId Building = new BuildingId("queen_hardening_building");
        private static readonly LevelId Level = new LevelId("queen_hardening_level");
        private static readonly VertexId V1 = new VertexId("queen_hardening_v1");
        private static readonly VertexId V2 = new VertexId("queen_hardening_v2");
        private static readonly WallId W1 = new WallId("queen_hardening_w1");
        private static readonly OpeningId O1 = new OpeningId("queen_hardening_o1");

        public static IReadOnlyList<string> Run()
        {
            var failures = new List<string>();
            RunCase(failures, "13_feedback_is_presentation_only", FeedbackIsPresentationOnly);
            RunCase(failures, "14_mesh_rebuild_after_persistence_is_derived", MeshRebuildAfterPersistenceIsDerived);
            return failures;
        }

        private static void FeedbackIsPresentationOnly()
        {
            var current = CreateWallFixture();
            var baseFingerprint = current.ComputeFingerprint();

            var proposal = ArchitectureTransactionEngine.Propose(
                current,
                ArchitectureOperationKind.MoveVertex,
                "Queen feedback authority",
                snapshot => ArchitectureMutations.MoveVertex(snapshot, V2, new ArchitecturePoint(6d, 0d)));

            Require(proposal.IsReady, "valid feedback proposal rejected: " + proposal.DiagnosticCode);
            var proposalFingerprint = proposal.ProposedSnapshot.ComputeFingerprint();

            var impactService = new ArchitectureImpactService(new IArchitectureImpactAdapter[]
            {
                new ArchitectureCirculationImpactAdapter((context, issues) => issues.Add(new ArchitectureImpactIssue
                {
                    Severity = ArchitectureImpactSeverity.Warning,
                    EntityId = "queen_feedback_route",
                    ReasonCode = "QUEEN_FEEDBACK_WARNING",
                    HumanMessage = "Aviso simulado para comprobar que LA10 solo presenta decisiones ya tomadas.",
                    SuggestedDelta = new ArchitectureSuggestedDelta
                    {
                        DeltaX = 0.1d,
                        DeltaY = 0d,
                        Unit = "m",
                        Explanation = "Corrección sugerida únicamente informativa."
                    }
                }))
            });

            var impact = impactService.Analyze(current, proposal);
            var feedback = new ArchitectureEditFeedbackService().Build(proposal, impact, null);

            Require(feedback.State == ArchitectureFeedbackState.Warning, "warning impact was not represented as Warning");
            Require(feedback.CanConfirm, "warning feedback must remain confirmable");
            Require(feedback.ProposedFingerprint == proposal.ProposedFingerprint, "feedback changed proposal identity");
            Require(feedback.Cues.Any(x => x != null && x.Kind == ArchitectureFeedbackCueKind.CorrectionHint), "correction hint was not exposed");
            Require(current.ComputeFingerprint() == baseFingerprint, "feedback pipeline mutated canonical A");
            Require(proposal.ProposedSnapshot.ComputeFingerprint() == proposalFingerprint, "feedback pipeline mutated proposal B");
        }

        private static void MeshRebuildAfterPersistenceIsDerived()
        {
            var current = CreateWallFixture();
            var canonicalFingerprint = current.ComputeFingerprint();
            var saved = ArchitecturePersistence.Capture(current.Building);

            Require(ArchitecturePersistence.TryRestore(saved, out var restoredBuilding, out var restoreError), "restore failed: " + restoreError);
            var restored = new ArchitectureSnapshot
            {
                SchemaVersion = current.SchemaVersion,
                Building = restoredBuilding
            };

            Require(restored.ComputeFingerprint() == canonicalFingerprint, "restored topology fingerprint mismatch");
            var level = restored.FindLevel(Level);
            var wall = restored.FindWall(W1);
            Require(level != null && wall != null, "restored wall fixture missing");

            var beforeMeshing = restored.ComputeFingerprint();
            var first = ArchitectureWallMesher.Build(level, wall);
            var second = ArchitectureWallMesher.Build(level, wall);

            Require(!first.IsEmpty, "mesher produced empty geometry");
            Require(first.Vertices.SequenceEqual(second.Vertices), "mesh vertices are not deterministic");
            Require(first.Triangles.SequenceEqual(second.Triangles), "mesh triangles are not deterministic");
            Require(restored.ComputeFingerprint() == beforeMeshing, "meshing mutated restored canonical snapshot");

            first.Vertices.Clear();
            first.Triangles.Clear();
            Require(restored.ComputeFingerprint() == beforeMeshing, "mutating derived MeshData leaked into canonical snapshot");
            Require(!ArchitectureWallMesher.Build(level, wall).IsEmpty, "canonical snapshot could not rebuild mesh after derived data was discarded");
        }

        private static ArchitectureSnapshot CreateWallFixture()
        {
            var a = new ArchitectureVertex
            {
                Id = V1,
                Position = new ArchitecturePoint(0d, 0d)
            };
            var b = new ArchitectureVertex
            {
                Id = V2,
                Position = new ArchitecturePoint(5d, 0d)
            };
            var wall = new ArchitectureWall
            {
                Id = W1,
                StartVertexId = V1,
                EndVertexId = V2,
                Thickness = 0.15d,
                Height = 3d
            };
            wall.Openings.Add(new ArchitectureOpening
            {
                Id = O1,
                WallId = W1,
                CenterT = 0.5d,
                Width = 1.2d,
                Bottom = 0d,
                Height = 2.1d
            });

            var level = new ArchitectureLevel
            {
                Id = Level,
                Elevation = 0d
            };
            level.Vertices.Add(a);
            level.Vertices.Add(b);
            level.Walls.Add(wall);

            var building = new ArchitectureBuilding { Id = Building };
            building.Levels.Add(level);
            var snapshot = new ArchitectureSnapshot { Building = building };
            Require(ArchitectureValidator.Validate(snapshot).IsValid, "hardening fixture invalid");
            return snapshot;
        }

        private static void RunCase(ICollection<string> failures, string name, Action test)
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failures.Add(name + ": " + ex.Message);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
