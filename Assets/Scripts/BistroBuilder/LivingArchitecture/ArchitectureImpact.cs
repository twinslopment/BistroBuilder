using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public enum ArchitectureImpactSeverity
    {
        Info = 0,
        Warning = 1,
        Blocking = 2,
        SystemError = 3
    }

    public enum ArchitectureImpactSourceSystem
    {
        Architecture = 0,
        Placeables = 10,
        Seating = 20,
        Circulation = 30,
        External = 100
    }

    [Serializable]
    public sealed class ArchitectureSuggestedDelta
    {
        public double DeltaX;
        public double DeltaY;
        public string Unit = "m";
        public string Explanation;
    }

    [Serializable]
    public sealed class ArchitectureImpactIssue
    {
        public ArchitectureImpactSeverity Severity;
        public ArchitectureImpactSourceSystem SourceSystem;
        public string EntityId;
        public string ReasonCode;
        public string HumanMessage;
        public ArchitectureSuggestedDelta SuggestedDelta;

        public ArchitectureImpactIssue DeepClone()
        {
            return new ArchitectureImpactIssue
            {
                Severity = Severity,
                SourceSystem = SourceSystem,
                EntityId = EntityId,
                ReasonCode = ReasonCode,
                HumanMessage = HumanMessage,
                SuggestedDelta = SuggestedDelta == null
                    ? null
                    : new ArchitectureSuggestedDelta
                    {
                        DeltaX = SuggestedDelta.DeltaX,
                        DeltaY = SuggestedDelta.DeltaY,
                        Unit = SuggestedDelta.Unit,
                        Explanation = SuggestedDelta.Explanation
                    }
            };
        }
    }

    /// <summary>
    /// Contexto read-only LA6. El motor entrega clones aislados a cada adaptador:
    /// ningún sistema externo recibe el snapshot vivo ni la propuesta mutable original.
    /// </summary>
    public sealed class ArchitectureImpactContext
    {
        public ArchitectureOperationDescriptor Operation;
        public ArchitectureSnapshot BaseSnapshot;
        public ArchitectureSnapshot ProposedSnapshot;
    }

    /// <summary>
    /// Contrato universal para consultar una autoridad externa sin transferirle autoridad
    /// sobre Arquitectura Viva. Implementaciones de colocables, seating y circulación
    /// solo describen consecuencias; no ejecutan movimientos ni commits.
    /// </summary>
    public interface IArchitectureImpactAdapter
    {
        ArchitectureImpactSourceSystem SourceSystem { get; }
        int Order { get; }
        void Evaluate(ArchitectureImpactContext context, IList<ArchitectureImpactIssue> issues);
    }

    public interface IArchitecturePlaceablesImpactAdapter : IArchitectureImpactAdapter { }
    public interface IArchitectureSeatingImpactAdapter : IArchitectureImpactAdapter { }
    public interface IArchitectureCirculationImpactAdapter : IArchitectureImpactAdapter { }

    public sealed class ArchitectureImpactReport
    {
        public ArchitectureOperationDescriptor Operation;
        public string BaseFingerprint;
        public string ProposedFingerprint;
        public readonly List<ArchitectureImpactIssue> Issues = new List<ArchitectureImpactIssue>();

        public bool HasBlockingIssues => Issues.Any(x =>
            x != null &&
            (x.Severity == ArchitectureImpactSeverity.Blocking ||
             x.Severity == ArchitectureImpactSeverity.SystemError));

        public int BlockingCount => Issues.Count(x =>
            x != null && x.Severity == ArchitectureImpactSeverity.Blocking);

        public int WarningCount => Issues.Count(x =>
            x != null && x.Severity == ArchitectureImpactSeverity.Warning);
    }

    /// <summary>
    /// LA6 — analiza una propuesta antes del commit. Integra consecuencias arquitectónicas
    /// y adaptadores read-only de Bistro Builder. Es determinista y fail-safe: una excepción
    /// de un adaptador se convierte en incidencia de sistema y nunca muta A/B.
    /// </summary>
    public sealed class ArchitectureImpactService
    {
        private readonly List<IArchitectureImpactAdapter> adapters =
            new List<IArchitectureImpactAdapter>();

        public ArchitectureImpactService(IEnumerable<IArchitectureImpactAdapter> adapters = null)
        {
            if (adapters == null) return;
            this.adapters.AddRange(adapters.Where(x => x != null));
        }

        public ArchitectureImpactReport Analyze(
            ArchitectureSnapshot baseSnapshot,
            ArchitectureOperationProposal proposal)
        {
            var report = new ArchitectureImpactReport
            {
                Operation = proposal?.Operation,
                BaseFingerprint = baseSnapshot?.ComputeFingerprint(),
                ProposedFingerprint = proposal?.ProposedSnapshot?.ComputeFingerprint()
            };

            if (baseSnapshot == null)
            {
                AddSystemIssue(report, "LA6_NULL_BASE", "No existe snapshot base para analizar impacto.");
                return FinalizeReport(report);
            }

            if (proposal == null || !proposal.IsReady || proposal.ProposedSnapshot == null)
            {
                AddSystemIssue(report, "LA6_PROPOSAL_NOT_READY", "La propuesta no está preparada para analizar impacto.");
                return FinalizeReport(report);
            }

            AnalyzeRegionChanges(baseSnapshot, proposal.ProposedSnapshot, report.Issues);

            foreach (var adapter in adapters
                .OrderBy(x => x.Order)
                .ThenBy(x => x.SourceSystem)
                .ThenBy(x => x.GetType().FullName, StringComparer.Ordinal))
            {
                EvaluateAdapterIsolated(adapter, baseSnapshot, proposal, report.Issues);
            }

            return FinalizeReport(report);
        }

        private static void EvaluateAdapterIsolated(
            IArchitectureImpactAdapter adapter,
            ArchitectureSnapshot baseSnapshot,
            ArchitectureOperationProposal proposal,
            IList<ArchitectureImpactIssue> target)
        {
            var isolatedBase = baseSnapshot.DeepClone();
            var isolatedProposed = proposal.ProposedSnapshot.DeepClone();
            var beforeBase = isolatedBase.ComputeFingerprint();
            var beforeProposed = isolatedProposed.ComputeFingerprint();
            var local = new List<ArchitectureImpactIssue>();

            try
            {
                adapter.Evaluate(
                    new ArchitectureImpactContext
                    {
                        Operation = proposal.Operation,
                        BaseSnapshot = isolatedBase,
                        ProposedSnapshot = isolatedProposed
                    },
                    local);
            }
            catch (Exception ex)
            {
                target.Add(new ArchitectureImpactIssue
                {
                    Severity = ArchitectureImpactSeverity.SystemError,
                    SourceSystem = adapter.SourceSystem,
                    ReasonCode = "LA6_ADAPTER_EXCEPTION",
                    HumanMessage = "El adaptador de impacto no pudo completar la consulta: " + ex.Message
                });
                return;
            }

            var mutated =
                !string.Equals(beforeBase, isolatedBase.ComputeFingerprint(), StringComparison.Ordinal) ||
                !string.Equals(beforeProposed, isolatedProposed.ComputeFingerprint(), StringComparison.Ordinal);

            if (mutated)
            {
                target.Add(new ArchitectureImpactIssue
                {
                    Severity = ArchitectureImpactSeverity.SystemError,
                    SourceSystem = adapter.SourceSystem,
                    ReasonCode = "LA6_ADAPTER_MUTATED_READONLY_SNAPSHOT",
                    HumanMessage = "Un adaptador intentó modificar un snapshot de consulta. La mutación fue aislada y descartada."
                });
                return;
            }

            foreach (var issue in local.Where(x => x != null))
            {
                var clone = issue.DeepClone();
                clone.SourceSystem = adapter.SourceSystem;
                target.Add(clone);
            }
        }

        private static void AnalyzeRegionChanges(
            ArchitectureSnapshot before,
            ArchitectureSnapshot after,
            IList<ArchitectureImpactIssue> issues)
        {
            var beforeLevels = before.Building?.Levels ?? new List<ArchitectureLevel>();
            var afterLevels = after.Building?.Levels ?? new List<ArchitectureLevel>();
            var levelIds = beforeLevels.Where(x => x != null).Select(x => x.Id.Value)
                .Concat(afterLevels.Where(x => x != null).Select(x => x.Id.Value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal);

            foreach (var levelIdValue in levelIds)
            {
                var levelId = new LevelId(levelIdValue);
                var beforeLevel = before.FindLevel(levelId);
                var afterLevel = after.FindLevel(levelId);
                var beforeRegions = beforeLevel == null
                    ? new List<ArchitectureRegion>()
                    : ArchitectureRegionEngine.Build(beforeLevel).Regions;
                var afterRegions = afterLevel == null
                    ? new List<ArchitectureRegion>()
                    : ArchitectureRegionEngine.Build(afterLevel).Regions;

                var beforeIds = new HashSet<string>(beforeRegions.Select(x => x.Id.Value), StringComparer.Ordinal);
                var afterIds = new HashSet<string>(afterRegions.Select(x => x.Id.Value), StringComparer.Ordinal);

                foreach (var region in afterRegions.Where(x => !beforeIds.Contains(x.Id.Value)))
                {
                    issues.Add(new ArchitectureImpactIssue
                    {
                        Severity = ArchitectureImpactSeverity.Info,
                        SourceSystem = ArchitectureImpactSourceSystem.Architecture,
                        EntityId = region.Id.Value,
                        ReasonCode = "LA6_REGION_CREATED",
                        HumanMessage = "La reforma crea una región arquitectónica de " +
                            region.Area.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " m²."
                    });
                }

                foreach (var region in beforeRegions.Where(x => !afterIds.Contains(x.Id.Value)))
                {
                    issues.Add(new ArchitectureImpactIssue
                    {
                        Severity = ArchitectureImpactSeverity.Info,
                        SourceSystem = ArchitectureImpactSourceSystem.Architecture,
                        EntityId = region.Id.Value,
                        ReasonCode = "LA6_REGION_REMOVED",
                        HumanMessage = "La reforma elimina o transforma una región arquitectónica existente."
                    });
                }

                if (beforeRegions.Count == 1 && afterRegions.Count > 1)
                {
                    issues.Add(new ArchitectureImpactIssue
                    {
                        Severity = ArchitectureImpactSeverity.Info,
                        SourceSystem = ArchitectureImpactSourceSystem.Architecture,
                        EntityId = levelId.Value,
                        ReasonCode = "LA6_REGION_SPLIT",
                        HumanMessage = "La reforma divide un espacio cerrado en varias regiones."
                    });
                }
                else if (beforeRegions.Count > 1 && afterRegions.Count == 1)
                {
                    issues.Add(new ArchitectureImpactIssue
                    {
                        Severity = ArchitectureImpactSeverity.Info,
                        SourceSystem = ArchitectureImpactSourceSystem.Architecture,
                        EntityId = levelId.Value,
                        ReasonCode = "LA6_REGION_MERGED",
                        HumanMessage = "La reforma fusiona varias regiones en un único espacio."
                    });
                }
            }
        }

        private static void AddSystemIssue(
            ArchitectureImpactReport report,
            string code,
            string message)
        {
            report.Issues.Add(new ArchitectureImpactIssue
            {
                Severity = ArchitectureImpactSeverity.SystemError,
                SourceSystem = ArchitectureImpactSourceSystem.Architecture,
                ReasonCode = code,
                HumanMessage = message
            });
        }

        private static ArchitectureImpactReport FinalizeReport(ArchitectureImpactReport report)
        {
            var unique = report.Issues
                .Where(x => x != null)
                .GroupBy(x => BuildDeduplicationKey(x), StringComparer.Ordinal)
                .Select(x => x.First())
                .OrderByDescending(x => x.Severity)
                .ThenBy(x => x.SourceSystem)
                .ThenBy(x => x.EntityId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.ReasonCode ?? string.Empty, StringComparer.Ordinal)
                .ToList();

            report.Issues.Clear();
            report.Issues.AddRange(unique);
            return report;
        }

        private static string BuildDeduplicationKey(ArchitectureImpactIssue issue)
        {
            return ((int)issue.Severity) + "|" + ((int)issue.SourceSystem) + "|" +
                (issue.EntityId ?? string.Empty) + "|" + (issue.ReasonCode ?? string.Empty) + "|" +
                (issue.HumanMessage ?? string.Empty);
        }
    }

    /// <summary>
    /// Base reutilizable para puentes a autoridades existentes. La función suministrada
    /// debe hacer solo lecturas. Permite integrar colocables/seating/circulación sin acoplar
    /// el Domain a MonoBehaviours ni a clases concretas de escena.
    /// </summary>
    public abstract class ArchitectureImpactAdapterBase : IArchitectureImpactAdapter
    {
        private readonly Action<ArchitectureImpactContext, IList<ArchitectureImpactIssue>> evaluator;

        protected ArchitectureImpactAdapterBase(
            Action<ArchitectureImpactContext, IList<ArchitectureImpactIssue>> evaluator,
            int order)
        {
            this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            Order = order;
        }

        public abstract ArchitectureImpactSourceSystem SourceSystem { get; }
        public int Order { get; }

        public void Evaluate(ArchitectureImpactContext context, IList<ArchitectureImpactIssue> issues)
        {
            evaluator(context, issues);
        }
    }

    public sealed class ArchitecturePlaceablesImpactAdapter : ArchitectureImpactAdapterBase,
        IArchitecturePlaceablesImpactAdapter
    {
        public ArchitecturePlaceablesImpactAdapter(
            Action<ArchitectureImpactContext, IList<ArchitectureImpactIssue>> evaluator,
            int order = 100) : base(evaluator, order) { }
        public override ArchitectureImpactSourceSystem SourceSystem => ArchitectureImpactSourceSystem.Placeables;
    }

    public sealed class ArchitectureSeatingImpactAdapter : ArchitectureImpactAdapterBase,
        IArchitectureSeatingImpactAdapter
    {
        public ArchitectureSeatingImpactAdapter(
            Action<ArchitectureImpactContext, IList<ArchitectureImpactIssue>> evaluator,
            int order = 200) : base(evaluator, order) { }
        public override ArchitectureImpactSourceSystem SourceSystem => ArchitectureImpactSourceSystem.Seating;
    }

    public sealed class ArchitectureCirculationImpactAdapter : ArchitectureImpactAdapterBase,
        IArchitectureCirculationImpactAdapter
    {
        public ArchitectureCirculationImpactAdapter(
            Action<ArchitectureImpactContext, IList<ArchitectureImpactIssue>> evaluator,
            int order = 300) : base(evaluator, order) { }
        public override ArchitectureImpactSourceSystem SourceSystem => ArchitectureImpactSourceSystem.Circulation;
    }
}
