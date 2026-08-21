using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// LA11 — Queen Test puro de Arquitectura Viva V1.
    /// Recorre el flujo completo sin escena ni GameObjects y comprueba reversibilidad,
    /// determinismo, persistencia e aislamiento de autoridades antes de Unity real.
    /// </summary>
    public static class ArchitectureV1QueenSelfTest
    {
        public const int CaseCount = 12;

        private static readonly BuildingId Building = new BuildingId("queen_building");
        private static readonly LevelId Level = new LevelId("queen_level");
        private static readonly VertexId V1 = new VertexId("queen_v1");
        private static readonly VertexId V2 = new VertexId("queen_v2");
        private static readonly VertexId V3 = new VertexId("queen_v3");
        private static readonly VertexId V4 = new VertexId("queen_v4");
        private static readonly WallId W1 = new WallId("queen_w1");
        private static readonly WallId W2 = new WallId("queen_w2");
        private static readonly WallId W3 = new WallId("queen_w3");
        private static readonly WallId W4 = new WallId("queen_w4");
        private static readonly OpeningId O1 = new OpeningId("queen_o1");

        public static IReadOnlyList<string> Run()
        {
            var failures = new List<string>();
            RunCase(failures, "01_full_queen_pipeline", FullQueenPipeline);
            RunCase(failures, "02_construction_creates_emergent_region", ConstructionCreatesEmergentRegion);
            RunCase(failures, "03_opening_belongs_to_wall", OpeningBelongsToWall);
            RunCase(failures, "04_intent_preserves_centered_opening", IntentPreservesCenteredOpening);
            RunCase(failures, "05_impact_reports_without_mutation", ImpactReportsWithoutMutation);
            RunCase(failures, "06_save_load_roundtrip_exact", SaveLoadRoundTripExact);
            RunCase(failures, "07_undo_redo_exact_after_restore", UndoRedoExactAfterRestore);
            RunCase(failures, "08_rejected_operation_is_integral_rollback", RejectedOperationIsIntegralRollback);
            RunCase(failures, "09_stale_proposal_cannot_commit", StaleProposalCannotCommit);
            RunCase(failures, "10_mutating_adapter_is_isolated", MutatingAdapterIsIsolated);
            RunCase(failures, "11_same_operation_is_deterministic", SameOperationIsDeterministic);
            RunCase(failures, "12_basic_topology_property_sweep", BasicTopologyPropertySweep);
            return failures;
        }

        private static void FullQueenPipeline()
        {
            var current = CreateScaffold(6d, 4d);
            var scaffoldFingerprint = current.ComputeFingerprint();

            var build = ProposeRoom(current);
            Require(build.IsReady, "room construction proposal rejected: " + build.DiagnosticCode);
            Require(current.ComputeFingerprint() == scaffoldFingerprint, "construction preview mutated A");
            Require(ArchitectureTransactionEngine.TryCommit(ref current, build, out _, out var buildCode), "room commit failed: " + buildCode);

            var regions = ArchitectureRegionEngine.Build(current.FindLevel(Level));
            Require(regions.Regions.Count == 1, "constructed room must yield exactly one region");
            Require(regions.FindContaining(new ArchitecturePoint(3d, 2d)) != null, "room spatial query failed");

            var openingProposal = ProposeOpening(current);
            Require(openingProposal.IsReady, "opening proposal rejected: " + openingProposal.DiagnosticCode);
            Require(ArchitectureTransactionEngine.TryCommit(ref current, openingProposal, out _, out var openingCode), "opening commit failed: " + openingCode);
            Require(FindOpening(current, O1) != null, "opening missing after commit");

            var beforeReformFingerprint = current.ComputeFingerprint();
            var intentResult = ProposeReformPreservingOpening(current, 7d);
            Require(intentResult.IsReady, "intent reform rejected: " + intentResult.Proposal?.DiagnosticCode);
            Require(current.ComputeFingerprint() == beforeReformFingerprint, "intent preview mutated A");

            var impact = CreateWarningImpactService().Analyze(current, intentResult.Proposal);
            Require(impact.WarningCount >= 1, "expected external warning impact");
            Require(!impact.HasBlockingIssues, "warning-only impact must remain confirmable");
            Require(impact.Issues.Any(x => x.SuggestedDelta != null), "minimal correction suggestion missing");

            Require(ArchitectureTransactionEngine.TryCommit(ref current, intentResult.Proposal, out var reform, out var reformCode), "reform commit failed: " + reformCode);
            var afterReformFingerprint = current.ComputeFingerprint();
            Require(afterReformFingerprint != beforeReformFingerprint, "reform did not change topology fingerprint");
            Require(Math.Abs(FindOpening(current, O1).CenterT - 0.5d) <= ArchitectureGeometry.Epsilon, "opening centering intent lost");

            var saved = ArchitecturePersistence.Capture(current.Building);
            Require(ArchitecturePersistence.TryRestore(saved, out var restoredBuilding, out var restoreError), "restore failed: " + restoreError);
            var restored = new ArchitectureSnapshot { SchemaVersion = current.SchemaVersion, Building = restoredBuilding };
            Require(restored.ComputeFingerprint() == afterReformFingerprint, "Save/Load changed topology fingerprint");

            current = restored;
            Require(ArchitectureTransactionEngine.TryUndo(ref current, reform, out var undoCode), "Undo after restore failed: " + undoCode);
            Require(current.ComputeFingerprint() == beforeReformFingerprint, "Undo did not restore exact A");
            Require(ArchitectureTransactionEngine.TryRedo(ref current, reform, out var redoCode), "Redo after restore failed: " + redoCode);
            Require(current.ComputeFingerprint() == afterReformFingerprint, "Redo did not restore exact B");

            var liveBeforeReject = current.ComputeFingerprint();
            var invalid = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Queen invalid collapse", snapshot =>
                ArchitectureMutations.MoveVertex(snapshot, V3, snapshot.FindVertex(V2).Position));
            Require(!invalid.IsReady, "degenerate reform should be rejected");
            Require(current.ComputeFingerprint() == liveBeforeReject, "rejected operation changed live architecture");
        }

        private static void ConstructionCreatesEmergentRegion()
        {
            var current = CreateScaffold(8d, 5d);
            Require(ArchitectureRegionEngine.Build(current.FindLevel(Level)).Regions.Count == 0, "scaffold must not contain a region");
            var proposal = ProposeRoom(current);
            Require(proposal.IsReady, "room proposal rejected");
            Require(ArchitectureRegionEngine.Build(proposal.ProposedSnapshot.FindLevel(Level)).Regions.Count == 1, "preview must derive one region");
            Require(ArchitectureRegionEngine.Build(current.FindLevel(Level)).Regions.Count == 0, "preview leaked into base snapshot");
        }

        private static void OpeningBelongsToWall()
        {
            var current = BuildCommittedRoom(6d, 4d);
            var proposal = ProposeOpening(current);
            Require(proposal.IsReady, "opening proposal rejected");
            var opening = FindOpening(proposal.ProposedSnapshot, O1);
            Require(opening != null, "opening not present in proposal");
            Require(opening.WallId.Equals(W3), "opening owner WallId changed");
            Require(proposal.ProposedSnapshot.FindWall(W3).Openings.Any(x => x != null && x.Id.Equals(O1)), "opening is not stored by containing wall");
        }

        private static void IntentPreservesCenteredOpening()
        {
            var current = BuildRoomWithOpening();
            var result = ProposeReformPreservingOpening(current, 8d);
            Require(result.IsReady, "intent proposal rejected");
            var opening = FindOpening(result.Proposal.ProposedSnapshot, O1);
            Require(opening != null && Math.Abs(opening.CenterT - 0.5d) <= ArchitectureGeometry.Epsilon, "centered opening was not preserved");
            Require(result.Evaluations.Any(x => x.Satisfied && x.Constraint != null && x.Constraint.Kind == ArchitectureConstraintKind.OpeningCentered), "intent evaluation missing");
        }

        private static void ImpactReportsWithoutMutation()
        {
            var current = BuildRoomWithOpening();
            var result = ProposeReformPreservingOpening(current, 7d);
            var before = current.ComputeFingerprint();
            var proposedBefore = result.Proposal.ProposedSnapshot.ComputeFingerprint();
            var report = CreateWarningImpactService().Analyze(current, result.Proposal);
            Require(report.WarningCount == 1, "expected one deterministic warning");
            Require(current.ComputeFingerprint() == before, "impact analysis mutated base snapshot");
            Require(result.Proposal.ProposedSnapshot.ComputeFingerprint() == proposedBefore, "impact analysis mutated proposal");
        }

        private static void SaveLoadRoundTripExact()
        {
            var current = BuildRoomWithOpening();
            var before = current.ComputeFingerprint();
            var a = ArchitecturePersistence.Capture(current.Building);
            var b = ArchitecturePersistence.Capture(current.Building);
            Require(PersistenceSignature(a) == PersistenceSignature(b), "persistence capture is not deterministic");
            Require(ArchitecturePersistence.TryRestore(a, out var restored, out var error), "restore failed: " + error);
            var roundTrip = new ArchitectureSnapshot { SchemaVersion = current.SchemaVersion, Building = restored };
            Require(roundTrip.ComputeFingerprint() == before, "round-trip fingerprint mismatch");
            Require(FindOpening(roundTrip, O1) != null, "opening identity lost in round-trip");
        }

        private static void UndoRedoExactAfterRestore()
        {
            var current = BuildRoomWithOpening();
            var before = current.ComputeFingerprint();
            var reform = ProposeReformPreservingOpening(current, 7.25d);
            Require(reform.IsReady, "reform rejected");
            Require(ArchitectureTransactionEngine.TryCommit(ref current, reform.Proposal, out var committed, out var commitCode), "commit failed: " + commitCode);
            var after = current.ComputeFingerprint();
            var saved = ArchitecturePersistence.Capture(current.Building);
            Require(ArchitecturePersistence.TryRestore(saved, out var restoredBuilding, out var error), "restore failed: " + error);
            current = new ArchitectureSnapshot { SchemaVersion = current.SchemaVersion, Building = restoredBuilding };
            Require(ArchitectureTransactionEngine.TryUndo(ref current, committed, out var undoCode), "undo failed: " + undoCode);
            Require(current.ComputeFingerprint() == before, "undo fingerprint mismatch");
            Require(ArchitectureTransactionEngine.TryRedo(ref current, committed, out var redoCode), "redo failed: " + redoCode);
            Require(current.ComputeFingerprint() == after, "redo fingerprint mismatch");
        }

        private static void RejectedOperationIsIntegralRollback()
        {
            var current = BuildCommittedRoom(6d, 4d);
            var before = current.ComputeFingerprint();
            var proposal = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Collapse", snapshot =>
                ArchitectureMutations.MoveVertex(snapshot, V3, snapshot.FindVertex(V2).Position));
            Require(!proposal.IsReady, "invalid topology was accepted");
            Require(current.ComputeFingerprint() == before, "rejected proposal changed base fingerprint");
        }

        private static void StaleProposalCannotCommit()
        {
            var current = BuildCommittedRoom(6d, 4d);
            var stale = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Stale", snapshot =>
                ArchitectureMutations.MoveVertex(snapshot, V3, new ArchitecturePoint(6.5d, 4d)));
            Require(stale.IsReady, "stale candidate should initially be ready");

            var intervening = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Intervening", snapshot =>
                ArchitectureMutations.MoveVertex(snapshot, V4, new ArchitecturePoint(-0.25d, 4d)));
            Require(intervening.IsReady, "intervening proposal rejected");
            Require(ArchitectureTransactionEngine.TryCommit(ref current, intervening, out _, out var code), "intervening commit failed: " + code);
            var beforeStaleCommit = current.ComputeFingerprint();
            Require(!ArchitectureTransactionEngine.TryCommit(ref current, stale, out _, out var staleCode), "stale proposal committed");
            Require(staleCode == "LA3_STALE_PROPOSAL", "wrong stale diagnostic: " + staleCode);
            Require(current.ComputeFingerprint() == beforeStaleCommit, "stale commit changed current state");
        }

        private static void MutatingAdapterIsIsolated()
        {
            var current = BuildCommittedRoom(6d, 4d);
            var proposal = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Impact isolation", snapshot =>
                ArchitectureMutations.MoveVertex(snapshot, V3, new ArchitecturePoint(6.5d, 4d)));
            Require(proposal.IsReady, "proposal rejected");
            var baseBefore = current.ComputeFingerprint();
            var proposalBefore = proposal.ProposedSnapshot.ComputeFingerprint();
            var adapter = new ArchitecturePlaceablesImpactAdapter((context, issues) =>
            {
                context.BaseSnapshot.FindVertex(V1).Position = new ArchitecturePoint(999d, 999d);
            });
            var report = new ArchitectureImpactService(new IArchitectureImpactAdapter[] { adapter }).Analyze(current, proposal);
            Require(report.Issues.Any(x => x.ReasonCode == "LA6_ADAPTER_MUTATED_READONLY_SNAPSHOT" && x.Severity == ArchitectureImpactSeverity.SystemError), "mutating adapter was not quarantined");
            Require(current.ComputeFingerprint() == baseBefore, "adapter mutated live base");
            Require(proposal.ProposedSnapshot.ComputeFingerprint() == proposalBefore, "adapter mutated live proposal");
        }

        private static void SameOperationIsDeterministic()
        {
            var current = BuildRoomWithOpening();
            var before = current.ComputeFingerprint();
            var a = ProposeReformPreservingOpening(current, 7.5d);
            var b = ProposeReformPreservingOpening(current, 7.5d);
            Require(a.IsReady && b.IsReady, "deterministic proposals rejected");
            Require(a.Proposal.ProposedFingerprint == b.Proposal.ProposedFingerprint, "same snapshot + operation produced different result");
            Require(current.ComputeFingerprint() == before, "determinism check mutated base");
        }

        private static void BasicTopologyPropertySweep()
        {
            for (var i = 1; i <= 64; i++)
            {
                var width = 3d + (i * 0.137d);
                var depth = 2d + ((i % 11) * 0.173d);
                var current = BuildCommittedRoom(width, depth);
                var before = current.ComputeFingerprint();
                var target = new ArchitecturePoint(width + 0.15d + ((i % 7) * 0.03d), depth);
                var a = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Sweep", snapshot => ArchitectureMutations.MoveVertex(snapshot, V3, target));
                var b = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Sweep", snapshot => ArchitectureMutations.MoveVertex(snapshot, V3, target));
                Require(a.IsReady && b.IsReady, "property sweep legal mutation rejected at case " + i);
                Require(a.ProposedFingerprint == b.ProposedFingerprint, "property sweep nondeterminism at case " + i);
                Require(current.ComputeFingerprint() == before, "property sweep preview mutated A at case " + i);
                Require(ArchitectureRegionEngine.Build(a.ProposedSnapshot.FindLevel(Level)).Regions.Count == 1, "property sweep lost region at case " + i);

                var invalid = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Sweep invalid", snapshot => ArchitectureMutations.MoveVertex(snapshot, V3, snapshot.FindVertex(V2).Position));
                Require(!invalid.IsReady, "property sweep accepted degenerate wall at case " + i);
                Require(current.ComputeFingerprint() == before, "property sweep rejection mutated A at case " + i);
            }
        }

        private static ArchitectureSnapshot CreateScaffold(double width, double depth)
        {
            var level = new ArchitectureLevel { Id = Level, Elevation = 0d };
            level.Vertices.Add(new ArchitectureVertex { Id = V1, Position = new ArchitecturePoint(0d, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = V2, Position = new ArchitecturePoint(width, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = V3, Position = new ArchitecturePoint(width, depth) });
            level.Vertices.Add(new ArchitectureVertex { Id = V4, Position = new ArchitecturePoint(0d, depth) });
            var building = new ArchitectureBuilding { Id = Building };
            building.Levels.Add(level);
            var snapshot = new ArchitectureSnapshot { Building = building };
            Require(ArchitectureValidator.Validate(snapshot).IsValid, "queen scaffold invalid");
            return snapshot;
        }

        private static ArchitectureOperationProposal ProposeRoom(ArchitectureSnapshot current)
        {
            return ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.Composite, "Queen construir recinto", snapshot =>
            {
                ArchitectureMutations.CreateWall(snapshot, Level, V1, V2, W1, 0.15d, 3d);
                ArchitectureMutations.CreateWall(snapshot, Level, V2, V3, W2, 0.15d, 3d);
                ArchitectureMutations.CreateWall(snapshot, Level, V3, V4, W3, 0.15d, 3d);
                ArchitectureMutations.CreateWall(snapshot, Level, V4, V1, W4, 0.15d, 3d);
            });
        }

        private static ArchitectureSnapshot BuildCommittedRoom(double width, double depth)
        {
            var current = CreateScaffold(width, depth);
            var proposal = ProposeRoom(current);
            Require(proposal.IsReady, "room proposal rejected: " + proposal.DiagnosticCode);
            Require(ArchitectureTransactionEngine.TryCommit(ref current, proposal, out _, out var code), "room commit failed: " + code);
            return current;
        }

        private static ArchitectureSnapshot BuildRoomWithOpening()
        {
            var current = BuildCommittedRoom(6d, 4d);
            var proposal = ProposeOpening(current);
            Require(proposal.IsReady, "opening proposal rejected: " + proposal.DiagnosticCode);
            Require(ArchitectureTransactionEngine.TryCommit(ref current, proposal, out _, out var code), "opening commit failed: " + code);
            return current;
        }

        private static ArchitectureOperationProposal ProposeOpening(ArchitectureSnapshot current)
        {
            return ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.Composite, "Queen añadir apertura", snapshot =>
            {
                var wall = snapshot.FindWall(W3);
                if (wall == null) throw new InvalidOperationException("QUEEN_WALL_NOT_FOUND");
                wall.Openings.Add(new ArchitectureOpening
                {
                    Id = O1,
                    WallId = W3,
                    CenterT = 0.5d,
                    Width = 1.2d,
                    Bottom = 0d,
                    Height = 2.1d
                });
            });
        }

        private static ArchitectureIntentResult ProposeReformPreservingOpening(ArchitectureSnapshot current, double newRightX)
        {
            var intent = new ArchitectureIntent { Label = "Queen ampliar comedor preservando apertura" };
            intent.Constraints.Add(new ArchitectureConstraint
            {
                Kind = ArchitectureConstraintKind.OpeningCentered,
                Severity = ArchitectureConstraintSeverity.Hard,
                EntityId = O1.Value,
                Tolerance = ArchitectureGeometry.Epsilon
            });
            var v3 = current.FindVertex(V3);
            if (v3 == null) throw new InvalidOperationException("QUEEN_V3_NOT_FOUND");
            return ArchitectureIntentEngine.Propose(current, ArchitectureOperationKind.MoveVertex, intent, snapshot =>
                ArchitectureMutations.MoveVertex(snapshot, V3, new ArchitecturePoint(newRightX, v3.Position.Y)));
        }

        private static ArchitectureImpactService CreateWarningImpactService()
        {
            return new ArchitectureImpactService(new IArchitectureImpactAdapter[]
            {
                new ArchitectureCirculationImpactAdapter((context, issues) => issues.Add(new ArchitectureImpactIssue
                {
                    Severity = ArchitectureImpactSeverity.Warning,
                    EntityId = "queen_route",
                    ReasonCode = "QUEEN_ROUTE_RECHECK",
                    HumanMessage = "La reforma modifica un borde del comedor; conviene revisar la holgura de circulación.",
                    SuggestedDelta = new ArchitectureSuggestedDelta
                    {
                        DeltaX = 0.10d,
                        DeltaY = 0d,
                        Unit = "m",
                        Explanation = "+10 cm de holgura resolverían el conflicto simulado del Queen Test."
                    }
                }))
            });
        }

        private static ArchitectureOpening FindOpening(ArchitectureSnapshot snapshot, OpeningId id)
        {
            return snapshot?.Building?.Levels?
                .Where(x => x != null)
                .SelectMany(x => x.Walls ?? new List<ArchitectureWall>())
                .Where(x => x != null)
                .SelectMany(x => x.Openings ?? new List<ArchitectureOpening>())
                .FirstOrDefault(x => x != null && x.Id.Equals(id));
        }

        private static string PersistenceSignature(ArchitecturePersistenceState state)
        {
            if (state == null) return string.Empty;
            var parts = new List<string> { state.schemaVersion.ToString(), state.buildingId ?? string.Empty };
            foreach (var level in state.levels ?? new List<ArchitectureLevelPersistenceData>())
            {
                if (level == null) continue;
                parts.Add(level.levelId + "@" + level.elevation.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                parts.AddRange((level.vertices ?? new List<ArchitectureVertexPersistenceData>()).Where(x => x != null).Select(x => x.vertexId + "@" + x.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + x.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                foreach (var wall in level.walls ?? new List<ArchitectureWallPersistenceData>())
                {
                    if (wall == null) continue;
                    parts.Add(wall.wallId + ":" + wall.startVertexId + ">" + wall.endVertexId);
                    parts.AddRange((wall.openings ?? new List<ArchitectureOpeningPersistenceData>()).Where(x => x != null).Select(x => x.openingId + "@" + x.wallId + ":" + x.centerT.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
            return string.Join("|", parts.ToArray());
        }

        private static void RunCase(ICollection<string> failures, string name, Action test)
        {
            try { test(); }
            catch (Exception ex) { failures.Add(name + ": " + ex.Message); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
