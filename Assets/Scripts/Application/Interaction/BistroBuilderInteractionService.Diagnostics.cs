using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Observabilidad, invariantes y diagnóstico causal del sistema lógico.
/// </summary>
public sealed partial class BistroBuilderInteractionService
{
    public IReadOnlyList<BistroBuilderInteractionTraceRecord> GetTraceSnapshot()
    {
        return trace.Select(CloneTrace).ToList();
    }

    public IReadOnlyList<BistroBuilderInteractionGrantRecord> GetGrantsForHolder(string holderId)
    {
        return grants.Values
            .Where(g => g != null && string.Equals(g.holderId, holderId, StringComparison.Ordinal))
            .OrderBy(g => g.grantId, StringComparer.Ordinal)
            .Select(CloneGrant)
            .ToList();
    }

    public IReadOnlyList<BistroBuilderInteractionGrantRecord> GetGrantsForResource(string resourceId)
    {
        return grants.Values
            .Where(g => g != null && string.Equals(g.resourceId, resourceId, StringComparison.Ordinal))
            .OrderBy(g => g.grantId, StringComparer.Ordinal)
            .Select(CloneGrant)
            .ToList();
    }

    public IReadOnlyList<BistroBuilderInteractionDecision> GetDecisionSnapshot()
    {
        return decisions.Values
            .Where(d => d != null)
            .OrderBy(d => d.requestId, StringComparer.Ordinal)
            .ToList();
    }

    public bool CanCaptureCanonicalState(out string error)
    {
        if (grants.Values.Any(g => g != null &&
            g.kind == BistroBuilderInteractionGrantKind.Custody &&
            g.state == BistroBuilderInteractionGrantState.Recovery))
        {
            error = "Existe Custody Recovery pendiente; el destino físico todavía no es canónico.";
            return false;
        }
        if (!ValidateRuntimeInvariants(out error)) return false;
        return ValidateCanonicalSnapshot(CaptureCanonicalSnapshot(), out error);
    }

    public bool ValidateRuntimeInvariants(out string error)
    {
        foreach (BistroBuilderInteractionGrantRecord grant in grants.Values)
        {
            if (grant == null || grant.IsTerminal) continue;
            if (string.IsNullOrWhiteSpace(grant.grantId) || grant.generation <= 0 ||
                string.IsNullOrWhiteSpace(grant.holderId) || string.IsNullOrWhiteSpace(grant.resourceId))
            {
                error = "Grant activo estructuralmente inválido.";
                return false;
            }
            if (grant.kind != BistroBuilderInteractionGrantKind.UsePermit &&
                !string.IsNullOrWhiteSpace(grant.spatialLeaseId))
            {
                error = "Un grant no-UsePermit contiene Spatial Lease: " + grant.grantId + ".";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(grant.parentGrantId))
            {
                var parent = new BistroBuilderInteractionGrantHandle(
                    grant.parentGrantId, grant.parentGeneration);
                if (!TryGetGrant(parent, out _))
                {
                    error = "Grant huérfano: " + grant.grantId + ".";
                    return false;
                }
            }
        }
        foreach (IGrouping<string, BistroBuilderInteractionGrantRecord> group in grants.Values
                     .Where(g => g != null && !g.IsTerminal && g.countsCapacity &&
                                 !string.IsNullOrWhiteSpace(g.conflictKey))
                     .GroupBy(g => g.conflictKey, StringComparer.Ordinal))
        {
            int used = group.Sum(g => Math.Max(1, g.capacityUnits));
            int capacity = group.Min(g => Math.Max(1, g.logicalCapacity));
            if (used > capacity)
            {
                error = "Logical capacity excedida en " + group.Key +
                        ": " + used + "/" + capacity + ".";
                return false;
            }
        }

        foreach (IGrouping<string, BistroBuilderInteractionGrantRecord> targetGroup in grants.Values
                     .Where(g => g != null && !g.IsTerminal && g.countsCapacity &&
                                 !string.IsNullOrWhiteSpace(g.targetId))
                     .GroupBy(g => g.targetId, StringComparer.Ordinal))
        {
            if (!targetGroup.Any(g => g.targetExclusive)) continue;
            BistroBuilderInteractionGrantRecord exclusive = targetGroup.First(g => g.targetExclusive);
            foreach (BistroBuilderInteractionGrantRecord other in targetGroup)
            {
                if (ReferenceEquals(other, exclusive)) continue;
                if (IsGrantInParentChain(exclusive.grantId, other.grantId) ||
                    IsGrantInParentChain(other.grantId, exclusive.grantId))
                    continue;
                error = "TargetExclusive en conflicto: " + targetGroup.Key + ".";
                return false;
            }
        }

        foreach (IGrouping<string, BistroBuilderInteractionGrantRecord> custody in grants.Values
                     .Where(g => g != null && !g.IsTerminal &&
                                 g.kind == BistroBuilderInteractionGrantKind.Custody)
                     .GroupBy(g => g.resourceId, StringComparer.Ordinal))
        {
            if (custody.Count() <= 1) continue;
            error = "Custody duplicada para " + custody.Key + ".";
            return false;
        }
        if (HasRuntimeCustodyCycle())
        {
            error = "Ciclo runtime de Custody detectado.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public string BuildDiagnosticReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("BB INTERACTION & RESERVATION v1");
        builder.AppendLine("Epoch: " + arbitrationEpoch);
        builder.AppendLine("Targets: " + RegisteredTargetCount);
        builder.AppendLine("Grants: " + ActiveGrantCount +
                           " (A=" + AssignmentCount +
                           ", T=" + TaskClaimCount +
                           ", U=" + UsePermitCount +
                           ", C=" + CustodyCount +
                           ", Q=" + WaitTicketCount + ")");
        builder.AppendLine("Pending: " + PendingRequestCount + " / Bundles: " + PendingBundleCount);
        builder.AppendLine(ValidateRuntimeInvariants(out string error)
            ? "Invariants: PASS"
            : "Invariants: FAIL - " + error);
        foreach (BistroBuilderInteractionGrantRecord grant in grants.Values
                     .Where(g => g != null)
                     .OrderBy(g => g.grantId, StringComparer.Ordinal))
        {
            builder.AppendLine(
                grant.grantId + "@" + grant.generation + " " + grant.kind + "/" + grant.state +
                " holder=" + grant.holderId + " resource=" + grant.resourceId +
                " scope=" + grant.conflictKey +
                (grant.engageBySimulationTime > 0d
                    ? " engageBy=" + grant.engageBySimulationTime.ToString("0.###")
                    : string.Empty));
        }
        return builder.ToString();
    }
    private bool HasRuntimeCustodyCycle()
    {
        var byResource = grants.Values
            .Where(g => g != null && !g.IsTerminal &&
                        g.kind == BistroBuilderInteractionGrantKind.Custody)
            .ToDictionary(g => g.resourceId, g => g, StringComparer.Ordinal);
        foreach (string resource in byResource.Keys)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string cursor = resource;
            while (byResource.TryGetValue(cursor, out BistroBuilderInteractionGrantRecord grant) &&
                   grant != null && grant.holderKind == BistroBuilderInteractionHolderKind.Container)
            {
                if (!visited.Add(cursor)) return true;
                cursor = grant.holderId;
                if (string.Equals(cursor, resource, StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

    private static BistroBuilderInteractionTraceRecord CloneTrace(
        BistroBuilderInteractionTraceRecord source)
    {
        if (source == null) return null;
        return new BistroBuilderInteractionTraceRecord
        {
            epoch = source.epoch,
            simulationTime = source.simulationTime,
            subjectId = source.subjectId,
            action = source.action,
            grantId = source.grantId,
            requestId = source.requestId,
            reason = source.reason,
            detail = source.detail
        };
    }
}