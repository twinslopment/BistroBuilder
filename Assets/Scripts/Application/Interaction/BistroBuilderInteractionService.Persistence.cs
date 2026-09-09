using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Persistencia canónica y Reservation Reconciliation Barrier.
/// No serializa intents, timers, handles runtime, rutas ni Spatial Leases.
/// </summary>
public sealed partial class BistroBuilderInteractionService
{
    public BistroBuilderInteractionCanonicalSnapshot CaptureCanonicalSnapshot()
    {
        var snapshot = new BistroBuilderInteractionCanonicalSnapshot();
        List<BistroBuilderInteractionGrantRecord> canonical = grants.Values
            .Where(IsCanonicalGrant)
            .OrderBy(g => g.kind)
            .ThenBy(g => g.sequence)
            .ThenBy(g => g.grantId, StringComparer.Ordinal)
            .ToList();
        var keysByGrantId = canonical.ToDictionary(
            g => g.grantId,
            BuildCanonicalKey,
            StringComparer.Ordinal);

        for (int i = 0; i < canonical.Count; i++)
        {
            BistroBuilderInteractionGrantRecord grant = canonical[i];
            snapshot.grants.Add(new BistroBuilderInteractionCanonicalGrant
            {
                kind = grant.kind,
                state = grant.state,
                holderKind = grant.holderKind,
                holderId = grant.holderId,
                interactionId = grant.interactionId,
                targetId = grant.targetId,
                resourceId = grant.resourceId,
                channelId = grant.channelId,
                slotId = grant.slotId,
                sessionId = grant.sessionId,
                parentCanonicalKey = !string.IsNullOrWhiteSpace(grant.parentGrantId) &&
                                     keysByGrantId.TryGetValue(grant.parentGrantId, out string parentKey)
                    ? parentKey
                    : string.Empty,
                queueSequence = grant.kind == BistroBuilderInteractionGrantKind.WaitTicket
                    ? grant.sequence
                    : 0L
            });
        }
        return snapshot;
    }

    public bool ValidateCanonicalSnapshot(
        BistroBuilderInteractionCanonicalSnapshot snapshot,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(snapshot.schemaId, BistroBuilderInteractionCanonicalSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderInteractionCanonicalSnapshot.CurrentSchemaVersion)
        {
            error = "Snapshot Interaction & Reservation incompatible.";
            return false;
        }
        snapshot.grants ??= new List<BistroBuilderInteractionCanonicalGrant>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var custodyResources = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.grants.Count; i++)
        {
            BistroBuilderInteractionCanonicalGrant grant = snapshot.grants[i];
            if (grant == null || string.IsNullOrWhiteSpace(grant.holderId) ||
                string.IsNullOrWhiteSpace(grant.interactionId) ||
                (string.IsNullOrWhiteSpace(grant.targetId) && string.IsNullOrWhiteSpace(grant.resourceId)))
            {
                error = "Snapshot Interaction contiene un grant canónico inválido.";
                return false;
            }
            if (!IsCanonicalKind(grant.kind))
            {
                error = "El snapshot intenta persistir un grant efímero: " + grant.kind + ".";
                return false;
            }
            if (grant.kind == BistroBuilderInteractionGrantKind.Custody)
            {
                if (grant.state == BistroBuilderInteractionGrantState.Recovery)
                {
                    error = "No se puede guardar Custody mientras está pendiente de Recovery.";
                    return false;
                }
                if (!custodyResources.Add(grant.resourceId))
                {
                    error = "Custody duplicada para " + grant.resourceId + ".";
                    return false;
                }
            }
            string key = BuildCanonicalKey(grant);
            if (!keys.Add(key))
            {
                error = "Grant canónico duplicado: " + key + ".";
                return false;
            }
        }
        for (int i = 0; i < snapshot.grants.Count; i++)
        {
            BistroBuilderInteractionCanonicalGrant grant = snapshot.grants[i];
            if (!string.IsNullOrWhiteSpace(grant.parentCanonicalKey) &&
                !keys.Contains(grant.parentCanonicalKey))
            {
                error = "Dependencia canónica ausente: " + grant.parentCanonicalKey + ".";
                return false;
            }
        }
        if (HasCanonicalCustodyCycle(snapshot.grants))
        {
            error = "El snapshot contiene un ciclo de Custody.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryRestoreCanonicalSnapshot(
        BistroBuilderInteractionCanonicalSnapshot snapshot,
        out string error)
    {
        if (!ValidateCanonicalSnapshot(snapshot, out error)) return false;
        ResetTransientRuntimeStateAfterLoad();
        var pending = snapshot.grants
            .OrderBy(g => g.kind == BistroBuilderInteractionGrantKind.WaitTicket ? 1 : 0)
            .ThenBy(g => g.queueSequence)
            .ThenBy(BuildCanonicalKey, StringComparer.Ordinal)
            .ToList();
        var restoredByCanonicalKey =
            new Dictionary<string, BistroBuilderInteractionGrantHandle>(StringComparer.Ordinal);
        int safety = pending.Count + 1;

        while (pending.Count > 0 && safety-- > 0)
        {
            bool progress = false;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                BistroBuilderInteractionCanonicalGrant canonical = pending[i];
                BistroBuilderInteractionGrantHandle parent = default;
                if (!string.IsNullOrWhiteSpace(canonical.parentCanonicalKey) &&
                    !restoredByCanonicalKey.TryGetValue(canonical.parentCanonicalKey, out parent))
                    continue;

                BistroBuilderInteractionAcquisitionRequest request =
                    BuildRestoreRequest(canonical, parent);
                string requestId = SubmitAcquisition(request);
                ResolveArbitrationEpoch();
                if (!TryGetDecision(requestId, out BistroBuilderInteractionDecision decision) ||
                    decision == null || decision.outcome != BistroBuilderInteractionRequestOutcome.Granted ||
                    !decision.handle.IsValid)
                {
                    error = "Reconciliation no pudo restaurar " +
                            BuildCanonicalKey(canonical) + ": " +
                            (decision != null ? decision.reason.ToString() : "sin decisión") + ".";
                    ResetTransientRuntimeStateAfterLoad();
                    return false;
                }

                if (!TryGetGrant(decision.handle, out BistroBuilderInteractionGrantRecord restored))
                {
                    error = "Reconciliation generó un handle inválido.";
                    ResetTransientRuntimeStateAfterLoad();
                    return false;
                }
                restored.persistCanonical = canonical.kind == BistroBuilderInteractionGrantKind.TaskClaim;
                if (canonical.kind == BistroBuilderInteractionGrantKind.TaskClaim &&
                    canonical.state == BistroBuilderInteractionGrantState.Committed)
                    TryCommitTaskClaim(restored.Handle);
                if (canonical.kind == BistroBuilderInteractionGrantKind.WaitTicket &&
                    canonical.state == BistroBuilderInteractionGrantState.Called)
                    restored.state = BistroBuilderInteractionGrantState.Called;

                restoredByCanonicalKey[BuildCanonicalKey(canonical)] = restored.Handle;
                pending.RemoveAt(i);
                progress = true;
            }
            if (!progress) break;
        }

        if (pending.Count > 0)
        {
            error = "Reconciliation detectó dependencias canónicas cíclicas/no resolubles.";
            ResetTransientRuntimeStateAfterLoad();
            return false;
        }
        RunOrphanAudit();
        AddTrace(string.Empty, "LoadReconciled", string.Empty, string.Empty,
            BistroBuilderInteractionReasonCode.LoadReconciled,
            restoredByCanonicalKey.Count.ToString());
        error = string.Empty;
        return true;
    }
    public void ResetTransientRuntimeStateAfterLoad()
    {
        List<BistroBuilderInteractionGrantRecord> active = grants.Values
            .Where(g => g != null)
            .OrderBy(g => g.grantId, StringComparer.Ordinal)
            .ToList();
        for (int i = 0; i < active.Count; i++)
        {
            BistroBuilderInteractionGrantRecord grant = active[i];
            if (!string.IsNullOrWhiteSpace(grant.spatialLeaseId))
                bbsisBridge?.ReleaseSpatialLease(grant.spatialLeaseId);
        }
        grants.Clear();
        pendingRequests.Clear();
        pendingBundles.Clear();
        decisions.Clear();
        bundleDecisions.Clear();
        deferredMutations.Clear();
        nextOrphanAuditAt = SimulationTime + orphanAuditIntervalSeconds;
        AddTrace(string.Empty, "TransientStateReset", string.Empty, string.Empty,
            BistroBuilderInteractionReasonCode.LoadReconciled, active.Count.ToString());
    }

    private BistroBuilderInteractionAcquisitionRequest BuildRestoreRequest(
        BistroBuilderInteractionCanonicalGrant canonical,
        BistroBuilderInteractionGrantHandle parent)
    {
        var candidate = new BistroBuilderInteractionCandidate
        {
            targetId = canonical.targetId,
            resourceId = canonical.resourceId,
            channelId = canonical.channelId,
            slotId = canonical.slotId
        };
        var request = new BistroBuilderInteractionAcquisitionRequest
        {
            requestId = "restore:" + Fnv1a64(BuildCanonicalKey(canonical)).ToString("x16"),
            grantKind = canonical.kind,
            holderKind = canonical.holderKind,
            holderId = canonical.holderId,
            interactionId = canonical.interactionId,
            sessionId = canonical.sessionId,
            parentGrantId = parent.IsValid ? parent.grantId : string.Empty,
            parentGeneration = parent.IsValid ? parent.generation : 0L,
            taskPriorityClass = 0,
            capacityUnits = canonical.kind == BistroBuilderInteractionGrantKind.WaitTicket ? 0 : 1,
            countsCapacity = canonical.kind != BistroBuilderInteractionGrantKind.WaitTicket,
            persistCanonical = canonical.kind == BistroBuilderInteractionGrantKind.TaskClaim,
            requiresSpatialAdmission = false,
            engageWithinSeconds = 0f,
            candidates = new List<BistroBuilderInteractionCandidate> { candidate }
        };
        return request;
    }

    private static bool IsCanonicalGrant(BistroBuilderInteractionGrantRecord grant)
    {
        if (grant == null || grant.IsTerminal) return false;
        if (grant.kind == BistroBuilderInteractionGrantKind.Assignment ||
            grant.kind == BistroBuilderInteractionGrantKind.Custody ||
            grant.kind == BistroBuilderInteractionGrantKind.WaitTicket)
            return true;
        return grant.kind == BistroBuilderInteractionGrantKind.TaskClaim && grant.persistCanonical;
    }

    private static bool IsCanonicalKind(BistroBuilderInteractionGrantKind kind)
    {
        return kind == BistroBuilderInteractionGrantKind.Assignment ||
               kind == BistroBuilderInteractionGrantKind.TaskClaim ||
               kind == BistroBuilderInteractionGrantKind.Custody ||
               kind == BistroBuilderInteractionGrantKind.WaitTicket;
    }
    private static string BuildCanonicalKey(BistroBuilderInteractionGrantRecord grant)
    {
        if (grant == null) return string.Empty;
        string baseKey = ((int)grant.kind) + "|" + ((int)grant.holderKind) + "|" +
                         grant.holderId + "|" + grant.interactionId + "|" +
                         grant.targetId + "|" + grant.resourceId + "|" +
                         grant.channelId + "|" + grant.slotId + "|" + grant.sessionId;
        return grant.kind == BistroBuilderInteractionGrantKind.WaitTicket
            ? baseKey + "|q:" + grant.sequence
            : baseKey;
    }

    private static string BuildCanonicalKey(BistroBuilderInteractionCanonicalGrant grant)
    {
        if (grant == null) return string.Empty;
        string baseKey = ((int)grant.kind) + "|" + ((int)grant.holderKind) + "|" +
                         grant.holderId + "|" + grant.interactionId + "|" +
                         grant.targetId + "|" + grant.resourceId + "|" +
                         grant.channelId + "|" + grant.slotId + "|" + grant.sessionId;
        return grant.kind == BistroBuilderInteractionGrantKind.WaitTicket
            ? baseKey + "|q:" + grant.queueSequence
            : baseKey;
    }

    private static bool HasCanonicalCustodyCycle(
        List<BistroBuilderInteractionCanonicalGrant> canonical)
    {
        var byResource = canonical
            .Where(g => g != null && g.kind == BistroBuilderInteractionGrantKind.Custody)
            .ToDictionary(g => g.resourceId, g => g, StringComparer.Ordinal);
        foreach (string resource in byResource.Keys)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string cursor = resource;
            while (byResource.TryGetValue(cursor, out BistroBuilderInteractionCanonicalGrant grant) &&
                   grant != null && grant.holderKind == BistroBuilderInteractionHolderKind.Container)
            {
                if (!visited.Add(cursor)) return true;
                cursor = grant.holderId;
                if (string.Equals(cursor, resource, StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }
}