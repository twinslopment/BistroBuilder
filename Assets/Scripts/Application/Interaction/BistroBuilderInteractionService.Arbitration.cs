using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Arbitraje determinista por epoch. El orden de Update/callbacks no decide ganadores.
/// </summary>
public sealed partial class BistroBuilderInteractionService
{
    public void ResolveArbitrationEpoch()
    {
        if (resolving) return;
        resolving = true;
        try
        {
            arbitrationEpoch = arbitrationEpoch == long.MaxValue ? 1 : arbitrationEpoch + 1;
            DrainDeferredMutations();
            CleanupExpiredUsePermits();
            if (SimulationTime >= nextOrphanAuditAt)
            {
                RunOrphanAudit();
                nextOrphanAuditAt = SimulationTime + orphanAuditIntervalSeconds;
            }

            List<ArbitrationUnit> units = BuildArbitrationUnits();
            units.Sort(CompareArbitrationUnits);
            for (int i = 0; i < units.Count; i++)
                ResolveUnit(units[i]);
        }
        finally
        {
            resolving = false;
        }
    }

    private List<ArbitrationUnit> BuildArbitrationUnits()
    {
        var units = new List<ArbitrationUnit>(pendingRequests.Count + pendingBundles.Count);
        foreach (PendingRequest pending in pendingRequests.Values)
        {
            if (pending?.request == null) continue;
            units.Add(new ArbitrationUnit
            {
                unitId = pending.request.requestId,
                firstEligibleEpoch = pending.firstEligibleEpoch,
                currentEpoch = arbitrationEpoch,
                requests = new List<BistroBuilderInteractionAcquisitionRequest> { pending.request }
            });
        }
        foreach (PendingBundle pending in pendingBundles.Values)
        {
            if (pending?.requests == null || pending.requests.Count == 0) continue;
            units.Add(new ArbitrationUnit
            {
                unitId = pending.bundleId,
                bundleId = pending.bundleId,
                firstEligibleEpoch = pending.firstEligibleEpoch,
                currentEpoch = arbitrationEpoch,
                requests = pending.requests
            });
        }
        return units;
    }
    private static int CompareArbitrationUnits(ArbitrationUnit a, ArbitrationUnit b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        BistroBuilderInteractionAcquisitionRequest ar = StrongestRequest(a.requests);
        BistroBuilderInteractionAcquisitionRequest br = StrongestRequest(b.requests);
        int value = CompareAcquisitionRequests(
            ar, br, a.firstEligibleEpoch, b.firstEligibleEpoch, a.currentEpoch, b.currentEpoch);
        return value != 0 ? value : string.CompareOrdinal(a.unitId, b.unitId);
    }

    private static BistroBuilderInteractionAcquisitionRequest StrongestRequest(
        List<BistroBuilderInteractionAcquisitionRequest> requests)
    {
        if (requests == null || requests.Count == 0) return null;
        return requests.OrderByDescending(r => r != null ? r.taskPriorityClass : int.MinValue)
            .ThenByDescending(r => r != null &&
                (!string.IsNullOrWhiteSpace(r.parentGrantId) || !string.IsNullOrWhiteSpace(r.parentRequestId)))
            .ThenByDescending(r => r?.candidates?.Count > 0
                ? r.candidates.Max(c => c != null ? c.suitability : int.MinValue)
                : int.MinValue)
            .ThenBy(r => r?.requestId ?? string.Empty, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static int CompareAcquisitionRequests(
        BistroBuilderInteractionAcquisitionRequest a,
        BistroBuilderInteractionAcquisitionRequest b,
        long firstA,
        long firstB,
        long epochA,
        long epochB)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int value = b.taskPriorityClass.CompareTo(a.taskPriorityClass);
        if (value != 0) return value;
        bool committedA = !string.IsNullOrWhiteSpace(a.parentGrantId) ||
                          !string.IsNullOrWhiteSpace(a.parentRequestId);
        bool committedB = !string.IsNullOrWhiteSpace(b.parentGrantId) ||
                          !string.IsNullOrWhiteSpace(b.parentRequestId);
        value = committedB.CompareTo(committedA);
        if (value != 0) return value;
        long ageA = Math.Min(10000L, Math.Max(0L, epochA - firstA));
        long ageB = Math.Min(10000L, Math.Max(0L, epochB - firstB));
        value = ageB.CompareTo(ageA);
        if (value != 0) return value;
        int suitabilityA = a.candidates.Count == 0 ? int.MinValue : a.candidates.Max(c => c.suitability);
        int suitabilityB = b.candidates.Count == 0 ? int.MinValue : b.candidates.Max(c => c.suitability);
        value = suitabilityB.CompareTo(suitabilityA);
        if (value != 0) return value;
        int travelA = a.candidates.Count == 0 ? int.MaxValue : a.candidates.Min(c => c.travelCostHint);
        int travelB = b.candidates.Count == 0 ? int.MaxValue : b.candidates.Min(c => c.travelCostHint);
        value = travelA.CompareTo(travelB);
        if (value != 0) return value;
        value = string.CompareOrdinal(a.holderId, b.holderId);
        return value != 0 ? value : string.CompareOrdinal(a.requestId, b.requestId);
    }
    private void ResolveUnit(ArbitrationUnit unit)
    {
        if (unit == null || unit.requests == null || unit.requests.Count == 0) return;
        var staged = new List<BistroBuilderInteractionGrantRecord>();
        var stagedDecisions = new List<BistroBuilderInteractionDecision>();
        var handlesByRequest = new Dictionary<string, BistroBuilderInteractionGrantHandle>(StringComparer.Ordinal);
        var remaining = unit.requests
            .Where(r => r != null)
            .OrderBy(r => r.requestId, StringComparer.Ordinal)
            .ToList();
        BistroBuilderInteractionReasonCode failure = BistroBuilderInteractionReasonCode.None;
        int safety = remaining.Count + 1;

        while (remaining.Count > 0 && safety-- > 0)
        {
            bool progress = false;
            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                BistroBuilderInteractionAcquisitionRequest request = remaining[i];
                if (!string.IsNullOrWhiteSpace(request.parentRequestId) &&
                    !handlesByRequest.ContainsKey(request.parentRequestId))
                    continue;

                if (!TryStageRequest(
                        request,
                        unit.bundleId,
                        staged,
                        handlesByRequest,
                        out BistroBuilderInteractionGrantRecord grant,
                        out BistroBuilderInteractionDecision decision))
                {
                    failure = decision != null
                        ? decision.reason
                        : BistroBuilderInteractionReasonCode.BundleUnavailable;
                    stagedDecisions.Add(decision ?? NewPendingDecision(request, unit.bundleId, failure));
                    RollbackStaged(staged);
                    PublishUnitFailure(unit, stagedDecisions, failure);
                    return;
                }

                staged.Add(grant);
                stagedDecisions.Add(decision);
                handlesByRequest[request.requestId] = grant.Handle;
                remaining.RemoveAt(i);
                progress = true;
            }
            if (!progress) break;
        }

        if (remaining.Count > 0)
        {
            failure = BistroBuilderInteractionReasonCode.DependencyInvalid;
            RollbackStaged(staged);
            PublishUnitFailure(unit, stagedDecisions, failure);
            return;
        }

        staged.Sort((a, b) => a.sequence.CompareTo(b.sequence));
        for (int i = 0; i < staged.Count; i++)
        {
            BistroBuilderInteractionGrantRecord grant = staged[i];
            grants[grant.grantId] = grant;
            NotifyGrantChanged(grant, "GrantCreated");
        }
        for (int i = 0; i < stagedDecisions.Count; i++)
        {
            BistroBuilderInteractionDecision decision = stagedDecisions[i];
            decision.outcome = BistroBuilderInteractionRequestOutcome.Granted;
            decisions[decision.requestId] = decision;
            pendingRequests.Remove(decision.requestId);
            PublishDecision(decision);
        }

        if (!string.IsNullOrWhiteSpace(unit.bundleId))
        {
            pendingBundles.Remove(unit.bundleId);
            var bundleDecision = new BistroBuilderInteractionBundleDecision
            {
                bundleId = unit.bundleId,
                outcome = BistroBuilderInteractionRequestOutcome.Granted,
                reason = BistroBuilderInteractionReasonCode.None,
                arbitrationEpoch = arbitrationEpoch,
                decisions = stagedDecisions
            };
            bundleDecisions[unit.bundleId] = bundleDecision;
            PublishBundleDecision(bundleDecision);
        }
    }
    private bool TryStageRequest(
        BistroBuilderInteractionAcquisitionRequest request,
        string bundleId,
        List<BistroBuilderInteractionGrantRecord> staged,
        Dictionary<string, BistroBuilderInteractionGrantHandle> handlesByRequest,
        out BistroBuilderInteractionGrantRecord grant,
        out BistroBuilderInteractionDecision decision)
    {
        grant = null;
        decision = NewPendingDecision(request, bundleId, BistroBuilderInteractionReasonCode.None);
        if (!ValidateBasicRequest(request, out BistroBuilderInteractionReasonCode reason))
        {
            decision.reason = reason;
            return false;
        }

        BistroBuilderInteractionGrantHandle parentHandle = default;
        if (!string.IsNullOrWhiteSpace(request.parentRequestId))
        {
            if (!handlesByRequest.TryGetValue(request.parentRequestId, out parentHandle))
            {
                decision.reason = BistroBuilderInteractionReasonCode.DependencyInvalid;
                return false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.parentGrantId))
        {
            parentHandle = new BistroBuilderInteractionGrantHandle(
                request.parentGrantId, request.parentGeneration);
            if (!TryGetGrant(parentHandle, out _))
            {
                decision.reason = BistroBuilderInteractionReasonCode.DependencyInvalid;
                return false;
            }
        }

        BistroBuilderInteractionReasonCode lastReason =
            BistroBuilderInteractionReasonCode.TargetUnavailable;
        string blocker = string.Empty;
        List<BistroBuilderInteractionCandidate> candidates = request.candidates
            .OrderBy(c => c, Comparer<BistroBuilderInteractionCandidate>.Create(CompareCandidates))
            .ToList();
        for (int i = 0; i < candidates.Count; i++)
        {
            BistroBuilderInteractionCandidate candidate = candidates[i];
            if (!TryResolveLogicalCandidate(
                    request, candidate, parentHandle, staged,
                    out ResolvedCandidate resolved, out lastReason, out blocker))
                continue;

            grant = CreateGrantRecord(request, resolved, parentHandle);
            if (grant.kind == BistroBuilderInteractionGrantKind.Custody &&
                WouldCreateCustodyCycleIncludingStaged(
                    grant.resourceId, grant.holderKind, grant.holderId, staged))
            {
                grant = null;
                lastReason = BistroBuilderInteractionReasonCode.CustodyCycle;
                continue;
            }

            bool needsSpatial = grant.kind == BistroBuilderInteractionGrantKind.UsePermit &&
                                (request.requiresSpatialAdmission ||
                                 (resolved.binding != null && resolved.binding.RequiresBbsis));
            if (needsSpatial)
            {
                if (resolved.target == null || resolved.binding == null ||
                    !resolved.binding.RequiresBbsis || bbsisBridge == null)
                {
                    grant = null;
                    lastReason = BistroBuilderInteractionReasonCode.SpatialBindingMissing;
                    continue;
                }
                float duration = request.engageWithinSeconds > 0f
                    ? request.engageWithinSeconds
                    : 3f;
                if (!bbsisBridge.TryAcquireSpatialLease(
                        grant, resolved.target, resolved.binding, duration,
                        out string leaseId, out lastReason))
                {
                    grant = null;
                    continue;
                }
                grant.spatialLeaseId = leaseId;
            }

            decision.reason = BistroBuilderInteractionReasonCode.None;
            decision.handle = grant.Handle;
            decision.selectedTargetId = grant.targetId;
            decision.selectedResourceId = grant.resourceId;
            decision.selectedChannelId = grant.channelId;
            decision.selectedSlotId = grant.slotId;
            decision.blockingGrantId = string.Empty;
            return true;
        }
        decision.reason = lastReason;
        decision.blockingGrantId = blocker;
        return false;
    }
    private bool TryResolveLogicalCandidate(
        BistroBuilderInteractionAcquisitionRequest request,
        BistroBuilderInteractionCandidate candidate,
        BistroBuilderInteractionGrantHandle parentHandle,
        List<BistroBuilderInteractionGrantRecord> staged,
        out ResolvedCandidate resolved,
        out BistroBuilderInteractionReasonCode reason,
        out string blocker)
    {
        resolved = default;
        blocker = string.Empty;
        reason = BistroBuilderInteractionReasonCode.None;
        if (candidate == null)
        {
            reason = BistroBuilderInteractionReasonCode.InvalidRequest;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.targetId))
        {
            if (!TryGetTarget(candidate.targetId, out BistroBuilderInteractionTarget target) || target == null)
            {
                reason = BistroBuilderInteractionReasonCode.TargetUnavailable;
                return false;
            }
            if (!target.TryResolve(
                    candidate.channelId,
                    candidate.slotId,
                    request.interactionId,
                    request.holderKind,
                    out BistroBuilderInteractionChannelDefinition channel,
                    out BistroBuilderInteractionSlotDefinition slot,
                    out BistroBuilderInteractionSpatialBindingDefinition binding,
                    out int capacity,
                    out reason))
                return false;

            var conditionContext = new BistroBuilderInteractionConditionContext(
                BistroBuilderInteractionConditionPhase.Commit,
                request.holderId,
                request.holderKind,
                request.interactionId,
                candidate.targetId,
                candidate.channelId,
                candidate.slotId,
                string.IsNullOrWhiteSpace(candidate.resourceId) ? candidate.targetId : candidate.resourceId,
                parentHandle);
            if (!target.EvaluateConditions(in conditionContext, out reason))
                return false;
            string resourceId = string.IsNullOrWhiteSpace(candidate.resourceId)
                ? candidate.targetId
                : candidate.resourceId;
            bool targetExclusive = channel != null && channel.targetExclusive;
            string conflictKey = targetExclusive
                ? "target:" + candidate.targetId
                : slot != null
                    ? "slot:" + candidate.targetId + "|" + candidate.channelId + "|" + candidate.slotId
                    : "channel:" + candidate.targetId + "|" + candidate.channelId;
            resolved = new ResolvedCandidate
            {
                target = target,
                candidate = candidate,
                binding = binding,
                resourceId = resourceId,
                conflictKey = conflictKey,
                logicalCapacity = targetExclusive ? 1 : Math.Max(1, capacity),
                targetExclusive = targetExclusive
            };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(candidate.resourceId))
            {
                reason = BistroBuilderInteractionReasonCode.InvalidRequest;
                return false;
            }
            resolved = new ResolvedCandidate
            {
                candidate = candidate,
                resourceId = candidate.resourceId,
                conflictKey = "resource:" + candidate.resourceId,
                logicalCapacity = 1,
                targetExclusive = false
            };
        }

        if (request.grantKind == BistroBuilderInteractionGrantKind.WaitTicket)
        {
            resolved.logicalCapacity = int.MaxValue;
            return true;
        }
        if (!request.countsCapacity) return true;
        return HasCapacity(request, resolved, parentHandle, staged, out reason, out blocker);
    }

    private bool HasCapacity(
        BistroBuilderInteractionAcquisitionRequest request,
        ResolvedCandidate resolved,
        BistroBuilderInteractionGrantHandle parentHandle,
        List<BistroBuilderInteractionGrantRecord> staged,
        out BistroBuilderInteractionReasonCode reason,
        out string blocker)
    {
        reason = BistroBuilderInteractionReasonCode.None;
        blocker = string.Empty;
        int used = 0;
        foreach (BistroBuilderInteractionGrantRecord existing in grants.Values)
        {
            if (!ConsumesConflictingCapacity(existing, resolved, parentHandle)) continue;
            used += Math.Max(1, existing.capacityUnits);
            if (string.IsNullOrEmpty(blocker)) blocker = existing.grantId;
        }
        for (int i = 0; i < staged.Count; i++)
        {
            BistroBuilderInteractionGrantRecord existing = staged[i];
            if (!ConsumesConflictingCapacity(existing, resolved, parentHandle)) continue;
            used += Math.Max(1, existing.capacityUnits);
            if (string.IsNullOrEmpty(blocker)) blocker = existing.grantId;
        }
        int requested = Math.Max(1, request.capacityUnits);
        if (used + requested <= resolved.logicalCapacity) return true;
        reason = BistroBuilderInteractionReasonCode.CapacityFull;
        return false;
    }
    private bool ConsumesConflictingCapacity(
        BistroBuilderInteractionGrantRecord existing,
        ResolvedCandidate resolved,
        BistroBuilderInteractionGrantHandle parentHandle)
    {
        if (existing == null || existing.IsTerminal || !existing.countsCapacity) return false;
        if (parentHandle.IsValid && IsGrantInParentChain(existing.grantId, parentHandle.grantId))
            return false;

        bool sameTarget = resolved.target != null &&
                          string.Equals(existing.targetId, resolved.target.TargetId, StringComparison.Ordinal);
        if (sameTarget && (resolved.targetExclusive || existing.targetExclusive)) return true;
        return string.Equals(existing.conflictKey, resolved.conflictKey, StringComparison.Ordinal);
    }

    private bool IsGrantInParentChain(string candidateGrantId, string parentGrantId)
    {
        if (string.IsNullOrWhiteSpace(candidateGrantId) || string.IsNullOrWhiteSpace(parentGrantId))
            return false;
        string cursor = parentGrantId;
        for (int i = 0; i <= grants.Count; i++)
        {
            if (string.Equals(candidateGrantId, cursor, StringComparison.Ordinal)) return true;
            if (!grants.TryGetValue(cursor, out BistroBuilderInteractionGrantRecord grant) ||
                grant == null || string.IsNullOrWhiteSpace(grant.parentGrantId))
                return false;
            cursor = grant.parentGrantId;
        }
        return false;
    }

    private BistroBuilderInteractionGrantRecord CreateGrantRecord(
        BistroBuilderInteractionAcquisitionRequest request,
        ResolvedCandidate resolved,
        BistroBuilderInteractionGrantHandle parentHandle)
    {
        if (nextGrantSequence == long.MaxValue)
            throw new InvalidOperationException("Interaction grant sequence exhausted.");
        long sequence = nextGrantSequence++;
        BistroBuilderInteractionGrantState state = InitialStateFor(request.grantKind);
        bool capacity = request.grantKind != BistroBuilderInteractionGrantKind.WaitTicket &&
                        request.countsCapacity;
        float engageWindow = request.engageWithinSeconds > 0f
            ? request.engageWithinSeconds
            : 3f;
        return new BistroBuilderInteractionGrantRecord
        {
            grantId = "grant:" + sequence.ToString("D12"),
            generation = NextGeneration(),
            sequence = sequence,
            grantedEpoch = arbitrationEpoch,
            kind = request.grantKind,
            state = state,
            holderKind = request.holderKind,
            holderId = request.holderId,
            interactionId = request.interactionId,
            targetId = resolved.candidate?.targetId ?? string.Empty,
            resourceId = resolved.resourceId ?? string.Empty,
            channelId = resolved.candidate?.channelId ?? string.Empty,
            slotId = resolved.candidate?.slotId ?? string.Empty,
            sessionId = request.sessionId,
            parentGrantId = parentHandle.IsValid ? parentHandle.grantId : string.Empty,
            parentGeneration = parentHandle.IsValid ? parentHandle.generation : 0L,
            taskPriorityClass = request.taskPriorityClass,
            suitability = resolved.candidate?.suitability ?? 0,
            travelCostHint = resolved.candidate?.travelCostHint ?? 0,
            capacityUnits = capacity ? Math.Max(1, request.capacityUnits) : 0,
            countsCapacity = capacity,
            persistCanonical = request.persistCanonical,
            conflictKey = resolved.conflictKey ?? string.Empty,
            logicalCapacity = Math.Max(1, resolved.logicalCapacity),
            targetExclusive = resolved.targetExclusive,
            engageBySimulationTime = request.grantKind == BistroBuilderInteractionGrantKind.UsePermit
                ? SimulationTime + engageWindow
                : 0d,
            lastReason = BistroBuilderInteractionReasonCode.None
        };
    }

    private static BistroBuilderInteractionGrantState InitialStateFor(
        BistroBuilderInteractionGrantKind kind)
    {
        switch (kind)
        {
            case BistroBuilderInteractionGrantKind.Assignment:
                return BistroBuilderInteractionGrantState.Assigned;
            case BistroBuilderInteractionGrantKind.TaskClaim:
                return BistroBuilderInteractionGrantState.Claimed;
            case BistroBuilderInteractionGrantKind.UsePermit:
                return BistroBuilderInteractionGrantState.Granted;
            case BistroBuilderInteractionGrantKind.Custody:
                return BistroBuilderInteractionGrantState.Held;
            case BistroBuilderInteractionGrantKind.WaitTicket:
                return BistroBuilderInteractionGrantState.Queued;
            default:
                return BistroBuilderInteractionGrantState.Granted;
        }
    }
    private void RollbackStaged(List<BistroBuilderInteractionGrantRecord> staged)
    {
        for (int i = 0; i < staged.Count; i++)
        {
            BistroBuilderInteractionGrantRecord grant = staged[i];
            if (grant == null || string.IsNullOrWhiteSpace(grant.spatialLeaseId)) continue;
            bbsisBridge?.ReleaseSpatialLease(grant.spatialLeaseId);
            grant.spatialLeaseId = string.Empty;
        }
    }

    private void PublishUnitFailure(
        ArbitrationUnit unit,
        List<BistroBuilderInteractionDecision> stagedDecisions,
        BistroBuilderInteractionReasonCode reason)
    {
        for (int i = 0; i < unit.requests.Count; i++)
        {
            BistroBuilderInteractionAcquisitionRequest request = unit.requests[i];
            if (request == null) continue;
            BistroBuilderInteractionDecision decision = stagedDecisions.FirstOrDefault(d =>
                d != null && string.Equals(d.requestId, request.requestId, StringComparison.Ordinal));
            decision ??= NewPendingDecision(request, unit.bundleId, reason);
            decision.handle = default;
            decision.outcome = BistroBuilderInteractionRequestOutcome.Pending;
            if (decision.reason == BistroBuilderInteractionReasonCode.None) decision.reason = reason;
            decisions[request.requestId] = decision;

            bool shouldPublish = true;
            if (pendingRequests.TryGetValue(request.requestId, out PendingRequest pending) && pending != null)
            {
                shouldPublish = pending.lastReason != decision.reason;
                pending.lastReason = decision.reason;
            }
            if (shouldPublish) PublishDecision(decision);
        }
        if (!string.IsNullOrWhiteSpace(unit.bundleId))
        {
            var bundleDecision = new BistroBuilderInteractionBundleDecision
            {
                bundleId = unit.bundleId,
                outcome = BistroBuilderInteractionRequestOutcome.Pending,
                reason = reason,
                arbitrationEpoch = arbitrationEpoch,
                decisions = unit.requests
                    .Where(r => r != null && decisions.ContainsKey(r.requestId))
                    .Select(r => decisions[r.requestId])
                    .ToList()
            };
            bundleDecisions[unit.bundleId] = bundleDecision;
            PublishBundleDecision(bundleDecision);
        }
    }

    private BistroBuilderInteractionDecision NewPendingDecision(
        BistroBuilderInteractionAcquisitionRequest request,
        string bundleId,
        BistroBuilderInteractionReasonCode reason)
    {
        return new BistroBuilderInteractionDecision
        {
            requestId = request?.requestId ?? string.Empty,
            bundleId = bundleId ?? string.Empty,
            outcome = BistroBuilderInteractionRequestOutcome.Pending,
            reason = reason,
            arbitrationEpoch = arbitrationEpoch,
            message = reason == BistroBuilderInteractionReasonCode.None
                ? string.Empty
                : "Esperando adquisición lógica: " + reason + "."
        };
    }

    private void CleanupExpiredUsePermits()
    {
        double now = SimulationTime;
        List<BistroBuilderInteractionGrantHandle> expired = grants.Values
            .Where(g => g != null && g.kind == BistroBuilderInteractionGrantKind.UsePermit &&
                        g.state == BistroBuilderInteractionGrantState.Granted &&
                        g.engageBySimulationTime > 0d && g.engageBySimulationTime <= now)
            .OrderBy(g => g.grantId, StringComparer.Ordinal)
            .Select(g => g.Handle)
            .ToList();
        for (int i = 0; i < expired.Count; i++)
            EndGrantInternal(
                expired[i],
                BistroBuilderInteractionGrantState.Expired,
                BistroBuilderInteractionReasonCode.Expired);
    }
    public int RunOrphanAudit()
    {
        int repaired = 0;
        List<BistroBuilderInteractionGrantHandle> invalid = new List<BistroBuilderInteractionGrantHandle>();
        foreach (BistroBuilderInteractionGrantRecord grant in grants.Values)
        {
            if (grant == null) continue;
            if (!string.IsNullOrWhiteSpace(grant.targetId) && !TryGetTarget(grant.targetId, out _))
            {
                invalid.Add(grant.Handle);
                continue;
            }
            if (!string.IsNullOrWhiteSpace(grant.parentGrantId))
            {
                var parentHandle = new BistroBuilderInteractionGrantHandle(
                    grant.parentGrantId, grant.parentGeneration);
                if (!TryGetGrant(parentHandle, out _)) invalid.Add(grant.Handle);
            }
        }
        invalid = invalid.OrderBy(h => h.grantId, StringComparer.Ordinal).ToList();
        for (int i = 0; i < invalid.Count; i++)
            if (EndGrantInternal(
                    invalid[i],
                    BistroBuilderInteractionGrantState.Invalidated,
                    BistroBuilderInteractionReasonCode.DependencyInvalid))
                repaired++;

        IEnumerable<IGrouping<string, BistroBuilderInteractionGrantRecord>> duplicateCustody = grants.Values
            .Where(g => g != null && g.kind == BistroBuilderInteractionGrantKind.Custody &&
                        (g.state == BistroBuilderInteractionGrantState.Held ||
                         g.state == BistroBuilderInteractionGrantState.Recovery))
            .GroupBy(g => g.resourceId, StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1);
        foreach (IGrouping<string, BistroBuilderInteractionGrantRecord> group in duplicateCustody)
        {
            BistroBuilderInteractionGrantRecord keep = group
                .OrderBy(g => g.sequence)
                .ThenBy(g => g.grantId, StringComparer.Ordinal)
                .First();
            foreach (BistroBuilderInteractionGrantRecord duplicate in group
                         .Where(g => g != keep)
                         .OrderBy(g => g.grantId, StringComparer.Ordinal))
            {
                if (EndGrantInternal(
                        duplicate.Handle,
                        BistroBuilderInteractionGrantState.Invalidated,
                        BistroBuilderInteractionReasonCode.AssignmentConflict))
                    repaired++;
            }
        }
        if (repaired > 0)
            AddTrace(string.Empty, "OrphanAuditRepaired", string.Empty, string.Empty,
                BistroBuilderInteractionReasonCode.DependencyInvalid, repaired.ToString());
        return repaired;
    }

    private bool WouldCreateCustodyCycleIncludingStaged(
        string resourceId,
        BistroBuilderInteractionHolderKind holderKind,
        string holderId,
        List<BistroBuilderInteractionGrantRecord> staged)
    {
        if (holderKind != BistroBuilderInteractionHolderKind.Container) return false;
        string cursor = holderId;
        int limit = grants.Count + (staged?.Count ?? 0) + 1;
        for (int i = 0; i <= limit; i++)
        {
            if (string.Equals(cursor, resourceId, StringComparison.Ordinal)) return true;
            BistroBuilderInteractionGrantRecord parent = staged?.FirstOrDefault(g =>
                g != null && g.kind == BistroBuilderInteractionGrantKind.Custody &&
                string.Equals(g.resourceId, cursor, StringComparison.Ordinal));
            parent ??= grants.Values.FirstOrDefault(g =>
                g != null && g.kind == BistroBuilderInteractionGrantKind.Custody &&
                !g.IsTerminal && string.Equals(g.resourceId, cursor, StringComparison.Ordinal));
            if (parent == null || parent.holderKind != BistroBuilderInteractionHolderKind.Container)
                return false;
            cursor = parent.holderId;
        }
        return true;
    }
    private sealed class ArbitrationUnit
    {
        public string unitId;
        public string bundleId;
        public long firstEligibleEpoch;
        public long currentEpoch;
        public List<BistroBuilderInteractionAcquisitionRequest> requests;
    }

    private struct ResolvedCandidate
    {
        public BistroBuilderInteractionTarget target;
        public BistroBuilderInteractionCandidate candidate;
        public BistroBuilderInteractionSpatialBindingDefinition binding;
        public string resourceId;
        public string conflictKey;
        public int logicalCapacity;
        public bool targetExclusive;
    }
}