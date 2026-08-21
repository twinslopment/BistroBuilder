using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public sealed class ArchitectureImpactSelfTestResult
    {
        public int Passed;
        public int Failed;
        public readonly List<string> Failures = new List<string>();
        public bool Success => Failed == 0;
    }

    /// <summary>Self-test puro LA6: reporte previo, aislamiento read-only y determinismo.</summary>
    public static class ArchitectureImpactSelfTest
    {
        public static ArchitectureImpactSelfTestResult Run()
        {
            var result = new ArchitectureImpactSelfTestResult();
            Test(result, "propuesta sin impactos externos", EmptyExternalImpact);
            Test(result, "impacto de colocables", PlaceablesImpact);
            Test(result, "impacto de seating", SeatingImpact);
            Test(result, "impacto de circulación", CirculationImpact);
            Test(result, "delta mínimo sugerido", SuggestedDeltaIsPreserved);
            Test(result, "orden determinista", DeterministicOrder);
            Test(result, "deduplicación estable", DuplicateIssuesAreCollapsed);
            Test(result, "excepción de adaptador aislada", AdapterExceptionIsolated);
            Test(result, "mutación de adaptador aislada", AdapterMutationIsolated);
            Test(result, "detección de región creada", RegionCreationDetected);
            Test(result, "blocking gate", BlockingGate);
            Test(result, "fingerprints A/B preservados", FingerprintsPreserved);
            return result;
        }

        private static void EmptyExternalImpact()
        {
            var before = CreateOpenLevelSnapshot();
            var proposal = Ready(before, before.DeepClone());
            var report = new ArchitectureImpactService().Analyze(before, proposal);
            Require(!report.HasBlockingIssues, "Un análisis vacío quedó bloqueado.");
            Require(report.Issues.Count == 0, "Aparecieron impactos inesperados.");
        }

        private static void PlaceablesImpact()
        {
            var before = CreateOpenLevelSnapshot();
            var adapter = new ArchitecturePlaceablesImpactAdapter((context, issues) =>
                issues.Add(Issue(ArchitectureImpactSeverity.Warning, "table_14", "PLACEABLE_OVERLAP", "La mesa 14 quedaría solapada.")));
            var report = Analyze(before, adapter);
            Require(report.Issues.Any(x => x.SourceSystem == ArchitectureImpactSourceSystem.Placeables && x.EntityId == "table_14"),
                "No se propagó el impacto de colocables.");
        }

        private static void SeatingImpact()
        {
            var before = CreateOpenLevelSnapshot();
            var adapter = new ArchitectureSeatingImpactAdapter((context, issues) =>
                issues.Add(Issue(ArchitectureImpactSeverity.Warning, "seat_group_2", "SEATING_AFFECTED", "Se afecta un grupo de asientos.")));
            var report = Analyze(before, adapter);
            Require(report.Issues.Any(x => x.SourceSystem == ArchitectureImpactSourceSystem.Seating),
                "No se propagó el impacto de seating.");
        }

        private static void CirculationImpact()
        {
            var before = CreateOpenLevelSnapshot();
            var adapter = new ArchitectureCirculationImpactAdapter((context, issues) =>
                issues.Add(Issue(ArchitectureImpactSeverity.Blocking, "route_kitchen_room", "CIRCULATION_BLOCKED", "Se bloquearía el recorrido Cocina → Sala.")));
            var report = Analyze(before, adapter);
            Require(report.HasBlockingIssues, "La circulación bloqueada no activó el gate.");
            Require(report.Issues.Any(x => x.SourceSystem == ArchitectureImpactSourceSystem.Circulation),
                "No se propagó el impacto de circulación.");
        }

        private static void SuggestedDeltaIsPreserved()
        {
            var before = CreateOpenLevelSnapshot();
            var adapter = new ArchitectureCirculationImpactAdapter((context, issues) =>
            {
                var issue = Issue(ArchitectureImpactSeverity.Warning, "aisle_1", "AISLE_NARROW", "El pasillo queda 0,12 m por debajo del mínimo.");
                issue.SuggestedDelta = new ArchitectureSuggestedDelta
                {
                    DeltaX = 0.12d,
                    DeltaY = 0d,
                    Explanation = "Desplazar el borde 12 cm resolvería el conflicto."
                };
                issues.Add(issue);
            });
            var report = Analyze(before, adapter);
            var found = report.Issues.First(x => x.EntityId == "aisle_1");
            Require(found.SuggestedDelta != null && Math.Abs(found.SuggestedDelta.DeltaX - 0.12d) < 0.00001d,
                "Se perdió la corrección mínima sugerida.");
        }

        private static void DeterministicOrder()
        {
            var before = CreateOpenLevelSnapshot();
            var adapters = new IArchitectureImpactAdapter[]
            {
                new ArchitectureCirculationImpactAdapter((context, issues) => issues.Add(Issue(ArchitectureImpactSeverity.Warning, "c", "C", "C"))),
                new ArchitecturePlaceablesImpactAdapter((context, issues) => issues.Add(Issue(ArchitectureImpactSeverity.Blocking, "p", "P", "P"))),
                new ArchitectureSeatingImpactAdapter((context, issues) => issues.Add(Issue(ArchitectureImpactSeverity.Warning, "s", "S", "S")))
            };
            var first = new ArchitectureImpactService(adapters).Analyze(before, Ready(before, before.DeepClone()));
            var second = new ArchitectureImpactService(adapters.Reverse()).Analyze(before, Ready(before, before.DeepClone()));
            Require(first.Issues.Count == second.Issues.Count, "Cantidad no determinista.");
            for (var i = 0; i < first.Issues.Count; i++)
            {
                Require(first.Issues[i].SourceSystem == second.Issues[i].SourceSystem, "Fuente no determinista.");
                Require(first.Issues[i].ReasonCode == second.Issues[i].ReasonCode, "Código no determinista.");
            }
        }

        private static void DuplicateIssuesAreCollapsed()
        {
            var before = CreateOpenLevelSnapshot();
            var adapter = new ArchitecturePlaceablesImpactAdapter((context, issues) =>
            {
                issues.Add(Issue(ArchitectureImpactSeverity.Warning, "table", "OVERLAP", "Solapamiento."));
                issues.Add(Issue(ArchitectureImpactSeverity.Warning, "table", "OVERLAP", "Solapamiento."));
            });
            var report = Analyze(before, adapter);
            Require(report.Issues.Count(x => x.ReasonCode == "OVERLAP") == 1, "No se deduplicó el impacto.");
        }

        private static void AdapterExceptionIsolated()
        {
            var before = CreateOpenLevelSnapshot();
            var adapter = new ArchitecturePlaceablesImpactAdapter((context, issues) =>
            {
                throw new InvalidOperationException("boom");
            });
            var report = Analyze(before, adapter);
            Require(report.Issues.Any(x => x.ReasonCode == "LA6_ADAPTER_EXCEPTION" && x.Severity == ArchitectureImpactSeverity.SystemError),
                "La excepción no se aisló como incidencia de sistema.");
            Require(before.ComputeFingerprint() == Ready(before, before.DeepClone()).BaseFingerprint,
                "El snapshot base cambió tras la excepción.");
        }

        private static void AdapterMutationIsolated()
        {
            var before = CreateOpenLevelSnapshot();
            var original = before.ComputeFingerprint();
            var proposed = before.DeepClone();
            var proposedOriginal = proposed.ComputeFingerprint();
            var adapter = new ArchitectureSeatingImpactAdapter((context, issues) =>
            {
                context.ProposedSnapshot.Building.Levels[0].Vertices[0].Position = new ArchitecturePoint(999d, 999d);
            });
            var proposal = Ready(before, proposed);
            var report = new ArchitectureImpactService(new[] { adapter }).Analyze(before, proposal);
            Require(report.Issues.Any(x => x.ReasonCode == "LA6_ADAPTER_MUTATED_READONLY_SNAPSHOT"),
                "No se detectó el intento de mutación.");
            Require(before.ComputeFingerprint() == original, "El adaptador mutó A.");
            Require(proposal.ProposedSnapshot.ComputeFingerprint() == proposedOriginal, "El adaptador mutó B.");
        }

        private static void RegionCreationDetected()
        {
            var before = CreateOpenLevelSnapshot();
            var after = before.DeepClone();
            var level = after.Building.Levels[0];
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_d"), Position = new ArchitecturePoint(0d, 4d) });
            level.Walls.Add(Wall("w_right", "v_b", "v_c"));
            level.Walls.Add(Wall("w_top", "v_c", "v_d"));
            level.Walls.Add(Wall("w_left", "v_d", "v_a"));
            var report = new ArchitectureImpactService().Analyze(before, Ready(before, after));
            Require(report.Issues.Any(x => x.ReasonCode == "LA6_REGION_CREATED"),
                "No se detectó la región creada.");
        }

        private static void BlockingGate()
        {
            var before = CreateOpenLevelSnapshot();
            var warning = new ArchitecturePlaceablesImpactAdapter((context, issues) =>
                issues.Add(Issue(ArchitectureImpactSeverity.Warning, "p", "WARN", "Aviso.")));
            Require(!Analyze(before, warning).HasBlockingIssues, "Un warning bloqueó la propuesta.");

            var blocking = new ArchitecturePlaceablesImpactAdapter((context, issues) =>
                issues.Add(Issue(ArchitectureImpactSeverity.Blocking, "p", "BLOCK", "Bloqueo.")));
            Require(Analyze(before, blocking).HasBlockingIssues, "Un blocking no bloqueó la propuesta.");
        }

        private static void FingerprintsPreserved()
        {
            var before = CreateOpenLevelSnapshot();
            var after = before.DeepClone();
            after.Building.Levels[0].Vertices[1].Position = new ArchitecturePoint(6d, 0d);
            var baseFp = before.ComputeFingerprint();
            var proposedFp = after.ComputeFingerprint();
            var report = new ArchitectureImpactService().Analyze(before, Ready(before, after));
            Require(report.BaseFingerprint == baseFp, "Fingerprint A incorrecto.");
            Require(report.ProposedFingerprint == proposedFp, "Fingerprint B incorrecto.");
            Require(before.ComputeFingerprint() == baseFp && after.ComputeFingerprint() == proposedFp,
                "El análisis mutó A/B.");
        }

        private static ArchitectureImpactReport Analyze(ArchitectureSnapshot before, IArchitectureImpactAdapter adapter)
        {
            return new ArchitectureImpactService(new[] { adapter })
                .Analyze(before, Ready(before, before.DeepClone()));
        }

        private static ArchitectureOperationProposal Ready(ArchitectureSnapshot before, ArchitectureSnapshot after)
        {
            return new ArchitectureOperationProposal
            {
                Operation = new ArchitectureOperationDescriptor
                {
                    Id = new ArchitectureOperationId("op_la6_test"),
                    Kind = ArchitectureOperationKind.Composite,
                    Label = "LA6 test"
                },
                BaseFingerprint = before.ComputeFingerprint(),
                ProposedFingerprint = after.ComputeFingerprint(),
                ProposedSnapshot = after,
                Status = ArchitectureProposalStatus.Ready,
                Validation = ArchitectureValidator.Validate(after)
            };
        }

        private static ArchitectureSnapshot CreateOpenLevelSnapshot()
        {
            var snapshot = new ArchitectureSnapshot
            {
                Building = new ArchitectureBuilding { Id = new BuildingId("building") }
            };
            var level = new ArchitectureLevel { Id = new LevelId("level"), Elevation = 0d };
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_a"), Position = new ArchitecturePoint(0d, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_b"), Position = new ArchitecturePoint(5d, 0d) });
            level.Vertices.Add(new ArchitectureVertex { Id = new VertexId("v_c"), Position = new ArchitecturePoint(5d, 4d) });
            level.Walls.Add(Wall("w_bottom", "v_a", "v_b"));
            snapshot.Building.Levels.Add(level);
            return snapshot;
        }

        private static ArchitectureWall Wall(string id, string start, string end)
        {
            return new ArchitectureWall
            {
                Id = new WallId(id),
                StartVertexId = new VertexId(start),
                EndVertexId = new VertexId(end),
                Thickness = 0.15d,
                Height = 3d
            };
        }

        private static ArchitectureImpactIssue Issue(
            ArchitectureImpactSeverity severity,
            string entityId,
            string code,
            string message)
        {
            return new ArchitectureImpactIssue
            {
                Severity = severity,
                EntityId = entityId,
                ReasonCode = code,
                HumanMessage = message
            };
        }

        private static void Test(ArchitectureImpactSelfTestResult result, string name, Action action)
        {
            try { action(); result.Passed++; }
            catch (Exception ex) { result.Failed++; result.Failures.Add(name + ": " + ex.Message); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "Fallo LA6." : message);
        }
    }
}
