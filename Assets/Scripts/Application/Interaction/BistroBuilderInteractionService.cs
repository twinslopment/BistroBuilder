using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Autoridad única de coordinación lógica de recursos de Bistro Builder.
/// No decide tareas, navegación, animación ni espacio BBSIS.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1200)]
public sealed partial class BistroBuilderInteractionService : MonoBehaviour
{
    [SerializeField] private BistroBuilderInteractionBbsisBridge bbsisBridge;
    [SerializeField, Min(0.1f)] private float orphanAuditIntervalSeconds = 2f;
    [SerializeField, Min(64)] private int maxTraceRecords = 512;
    [SerializeField] private bool autoResolveInLateUpdate = true;

    private readonly Dictionary<string, BistroBuilderInteractionTarget> targets =
        new Dictionary<string, BistroBuilderInteractionTarget>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderInteractionGrantRecord> grants =
        new Dictionary<string, BistroBuilderInteractionGrantRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingRequest> pendingRequests =
        new Dictionary<string, PendingRequest>(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingBundle> pendingBundles =
        new Dictionary<string, PendingBundle>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderInteractionDecision> decisions =
        new Dictionary<string, BistroBuilderInteractionDecision>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderInteractionBundleDecision> bundleDecisions =
        new Dictionary<string, BistroBuilderInteractionBundleDecision>(StringComparer.Ordinal);
    private readonly Queue<Action> deferredMutations = new Queue<Action>();
    private readonly List<BistroBuilderInteractionTraceRecord> trace =
        new List<BistroBuilderInteractionTraceRecord>(512);

    private long arbitrationEpoch;
    private long nextGrantSequence = 1;
    private long nextGeneration = 1;
    private double nextOrphanAuditAt;
    private bool resolving;
    private bool publishing;
    private bool bridgeSubscribed;
#if UNITY_EDITOR
    private double? editorSimulationTimeOverride;
#endif
    public event Action<BistroBuilderInteractionGrantRecord> GrantChanged;
    public event Action<BistroBuilderInteractionDecision> DecisionPublished;
    public event Action<BistroBuilderInteractionBundleDecision> BundleDecisionPublished;
    public event Action<BistroBuilderInteractionTraceRecord> TraceAdded;

    public long ArbitrationEpoch => arbitrationEpoch;
    public int ActiveGrantCount => grants.Count;
    public int PendingRequestCount => pendingRequests.Count;
    public int PendingBundleCount => pendingBundles.Count;
    public int RegisteredTargetCount => targets.Count;
    public int AssignmentCount => CountGrants(BistroBuilderInteractionGrantKind.Assignment);
    public int TaskClaimCount => CountGrants(BistroBuilderInteractionGrantKind.TaskClaim);
    public int UsePermitCount => CountGrants(BistroBuilderInteractionGrantKind.UsePermit);
    public int CustodyCount => CountGrants(BistroBuilderInteractionGrantKind.Custody);
    public int WaitTicketCount => CountGrants(BistroBuilderInteractionGrantKind.WaitTicket);

    private double SimulationTime
    {
        get
        {
#if UNITY_EDITOR
            if (editorSimulationTimeOverride.HasValue)
                return editorSimulationTimeOverride.Value;
#endif
            return Time.timeAsDouble;
        }
    }

    private void Awake()
    {
        CacheDependencies();
        SubscribeBridge();
        RebuildTargets();
    }

    private void OnDestroy()
    {
        UnsubscribeBridge();
    }

    private void LateUpdate()
    {
        if (autoResolveInLateUpdate)
            ResolveArbitrationEpoch();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (orphanAuditIntervalSeconds <= 0f || maxTraceRecords < 64)
        {
            error = "Configuración base Interaction & Reservation inválida.";
            return false;
        }
        CacheDependencies();
        foreach (BistroBuilderInteractionTarget target in targets.Values)
        {
            if (target == null || !target.ValidateTarget(out error)) return false;
        }
        error = string.Empty;
        return true;
    }
    public void RebuildTargets()
    {
        targets.Clear();
        BistroBuilderInteractionTarget[] found =
            FindObjectsByType<BistroBuilderInteractionTarget>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Array.Sort(found, (a, b) => string.CompareOrdinal(
            a != null ? a.TargetId : string.Empty,
            b != null ? b.TargetId : string.Empty));
        for (int i = 0; i < found.Length; i++)
            RegisterTarget(found[i]);
    }

    public bool RegisterTarget(BistroBuilderInteractionTarget target)
    {
        if (target == null || string.IsNullOrWhiteSpace(target.TargetId)) return false;
        if (targets.TryGetValue(target.TargetId, out BistroBuilderInteractionTarget existing) &&
            existing != null && existing != target)
            return false;
        targets[target.TargetId] = target;
        AddTrace(target.TargetId, "TargetRegistered", string.Empty, string.Empty,
            BistroBuilderInteractionReasonCode.None, target.name);
        return true;
    }

    public void UnregisterTarget(
        BistroBuilderInteractionTarget target,
        BistroBuilderInteractionReasonCode reason)
    {
        if (target == null || string.IsNullOrWhiteSpace(target.TargetId)) return;
        string id = target.TargetId;
        if (!targets.TryGetValue(id, out BistroBuilderInteractionTarget current) || current != target)
            return;
        targets.Remove(id);
        QueueOrRunMutation(() => InvalidateTargetInternal(id, reason));
    }

    public void NotifyTargetAdmissionChanged(BistroBuilderInteractionTarget target)
    {
        if (target == null) return;
        AddTrace(target.TargetId, "AdmissionChanged", string.Empty, string.Empty,
            BistroBuilderInteractionReasonCode.None, target.AdmissionState.ToString());
    }

    public bool TryGetTarget(string targetId, out BistroBuilderInteractionTarget target)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(targetId)) return false;
        if (!targets.TryGetValue(targetId, out target) || target == null)
        {
            targets.Remove(targetId);
            target = null;
            return false;
        }
        return true;
    }
    /// <summary>
    /// Registra una intención. No consume capacidad hasta ResolveArbitrationEpoch.
    /// </summary>
    public string SubmitAcquisition(BistroBuilderInteractionAcquisitionRequest source)
    {
        if (source == null) return string.Empty;
        BistroBuilderInteractionAcquisitionRequest request = source.Clone();
        NormalizeRequest(request);
        if (!ValidateBasicRequest(request, out BistroBuilderInteractionReasonCode reason))
        {
            PublishRejectedRequest(request, reason, "Solicitud lógica inválida.");
            return request.requestId;
        }
        if (pendingRequests.ContainsKey(request.requestId)) return request.requestId;
        if (decisions.TryGetValue(request.requestId, out BistroBuilderInteractionDecision old) &&
            old != null && old.outcome == BistroBuilderInteractionRequestOutcome.Granted)
            return request.requestId;

        pendingRequests[request.requestId] = new PendingRequest
        {
            request = request,
            firstEligibleEpoch = arbitrationEpoch + 1,
            lastReason = BistroBuilderInteractionReasonCode.None
        };
        decisions[request.requestId] = new BistroBuilderInteractionDecision
        {
            requestId = request.requestId,
            outcome = BistroBuilderInteractionRequestOutcome.Pending,
            reason = BistroBuilderInteractionReasonCode.None,
            arbitrationEpoch = arbitrationEpoch
        };
        AddTrace(request.holderId, "RequestSubmitted", string.Empty, request.requestId,
            BistroBuilderInteractionReasonCode.None, request.interactionId);
        return request.requestId;
    }

    public string SubmitBundle(BistroBuilderInteractionBundleRequest source)
    {
        if (source == null || source.requests == null || source.requests.Count == 0)
            return string.Empty;
        var requests = new List<BistroBuilderInteractionAcquisitionRequest>();
        for (int i = 0; i < source.requests.Count; i++)
        {
            if (source.requests[i] == null) continue;
            BistroBuilderInteractionAcquisitionRequest request = source.requests[i].Clone();
            NormalizeRequest(request);
            if (!ValidateBasicRequest(request, out _)) return string.Empty;
            requests.Add(request);
        }
        if (requests.Count == 0) return string.Empty;
        string bundleId = string.IsNullOrWhiteSpace(source.bundleId)
            ? BuildStableBundleId(requests)
            : source.bundleId.Trim();
        if (pendingBundles.ContainsKey(bundleId)) return bundleId;
        pendingBundles[bundleId] = new PendingBundle
        {
            bundleId = bundleId,
            requests = requests,
            firstEligibleEpoch = arbitrationEpoch + 1
        };
        bundleDecisions[bundleId] = new BistroBuilderInteractionBundleDecision
        {
            bundleId = bundleId,
            outcome = BistroBuilderInteractionRequestOutcome.Pending,
            arbitrationEpoch = arbitrationEpoch
        };
        return bundleId;
    }
    public bool TryGetDecision(
        string requestId,
        out BistroBuilderInteractionDecision decision)
    {
        decision = null;
        return !string.IsNullOrWhiteSpace(requestId) &&
               decisions.TryGetValue(requestId, out decision) && decision != null;
    }

    public bool TryGetBundleDecision(
        string bundleId,
        out BistroBuilderInteractionBundleDecision decision)
    {
        decision = null;
        return !string.IsNullOrWhiteSpace(bundleId) &&
               bundleDecisions.TryGetValue(bundleId, out decision) && decision != null;
    }

    public bool CancelRequest(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId) || !pendingRequests.Remove(requestId))
            return false;
        var decision = new BistroBuilderInteractionDecision
        {
            requestId = requestId,
            outcome = BistroBuilderInteractionRequestOutcome.Cancelled,
            reason = BistroBuilderInteractionReasonCode.TaskCancelled,
            arbitrationEpoch = arbitrationEpoch,
            message = "Intención lógica cancelada antes de la concesión."
        };
        decisions[requestId] = decision;
        PublishDecision(decision);
        return true;
    }

    public bool CancelBundle(string bundleId)
    {
        if (string.IsNullOrWhiteSpace(bundleId) || !pendingBundles.Remove(bundleId))
            return false;
        var decision = new BistroBuilderInteractionBundleDecision
        {
            bundleId = bundleId,
            outcome = BistroBuilderInteractionRequestOutcome.Cancelled,
            reason = BistroBuilderInteractionReasonCode.TaskCancelled,
            arbitrationEpoch = arbitrationEpoch
        };
        bundleDecisions[bundleId] = decision;
        PublishBundleDecision(decision);
        return true;
    }

    public bool ValidateCurrentGrant(BistroBuilderInteractionGrantHandle handle)
    {
        return TryGetGrant(handle, out _);
    }

    public bool TryGetGrant(
        BistroBuilderInteractionGrantHandle handle,
        out BistroBuilderInteractionGrantRecord grant)
    {
        grant = null;
        return handle.IsValid && grants.TryGetValue(handle.grantId, out grant) &&
               grant != null && grant.generation == handle.generation && !grant.IsTerminal;
    }
    public bool TryCommitTaskClaim(BistroBuilderInteractionGrantHandle handle)
    {
        if (!TryGetGrant(handle, out BistroBuilderInteractionGrantRecord grant) ||
            grant.kind != BistroBuilderInteractionGrantKind.TaskClaim)
            return false;
        if (grant.state == BistroBuilderInteractionGrantState.Committed) return true;
        if (grant.state != BistroBuilderInteractionGrantState.Claimed) return false;
        grant.state = BistroBuilderInteractionGrantState.Committed;
        grant.lastReason = BistroBuilderInteractionReasonCode.None;
        NotifyGrantChanged(grant, "TaskCommitted");
        return true;
    }

    public bool TryEngageUsePermit(BistroBuilderInteractionGrantHandle handle)
    {
        if (!TryGetGrant(handle, out BistroBuilderInteractionGrantRecord grant) ||
            grant.kind != BistroBuilderInteractionGrantKind.UsePermit)
            return false;
        if (grant.state == BistroBuilderInteractionGrantState.Engaged) return true;
        if (grant.state != BistroBuilderInteractionGrantState.Granted) return false;
        if (!RevalidateGrantTarget(grant, out BistroBuilderInteractionReasonCode reason))
        {
            EndGrantInternal(grant.Handle, BistroBuilderInteractionGrantState.Invalidated, reason);
            return false;
        }
        if (!string.IsNullOrWhiteSpace(grant.targetId) &&
            TryGetTarget(grant.targetId, out BistroBuilderInteractionTarget conditionTarget))
        {
            var context = new BistroBuilderInteractionConditionContext(
                BistroBuilderInteractionConditionPhase.Engage,
                grant.holderId,
                grant.holderKind,
                grant.interactionId,
                grant.targetId,
                grant.channelId,
                grant.slotId,
                grant.resourceId,
                grant.Handle);
            if (!conditionTarget.EvaluateConditions(in context, out reason))
            {
                EndGrantInternal(grant.Handle, BistroBuilderInteractionGrantState.Invalidated, reason);
                return false;
            }
        }        if (!string.IsNullOrWhiteSpace(grant.spatialLeaseId) &&
            (bbsisBridge == null || !bbsisBridge.RefreshSpatialLease(grant.spatialLeaseId, 0f)))
        {
            EndGrantInternal(grant.Handle, BistroBuilderInteractionGrantState.Invalidated,
                BistroBuilderInteractionReasonCode.SpatialDenied);
            return false;
        }
        grant.state = BistroBuilderInteractionGrantState.Engaged;
        grant.engageBySimulationTime = 0d;
        grant.lastReason = BistroBuilderInteractionReasonCode.None;
        NotifyGrantChanged(grant, "UseEngaged");
        return true;
    }

    public bool ReportApproachProgress(
        BistroBuilderInteractionGrantHandle handle,
        float extensionSeconds)
    {
        if (!TryGetGrant(handle, out BistroBuilderInteractionGrantRecord grant) ||
            grant.kind != BistroBuilderInteractionGrantKind.UsePermit ||
            grant.state != BistroBuilderInteractionGrantState.Granted)
            return false;
        double extension = Math.Max(0.05d, extensionSeconds);
        grant.engageBySimulationTime = Math.Max(
            grant.engageBySimulationTime, SimulationTime + extension);
        if (!string.IsNullOrWhiteSpace(grant.spatialLeaseId))
            bbsisBridge?.RefreshSpatialLease(grant.spatialLeaseId, (float)extension);
        AddTrace(grant.holderId, "ApproachProgress", grant.grantId, string.Empty,
            BistroBuilderInteractionReasonCode.None, extension.ToString("0.###"));
        return true;
    }
    public bool CompleteGrant(BistroBuilderInteractionGrantHandle handle)
    {
        return QueueOrEndGrant(
            handle,
            BistroBuilderInteractionGrantState.Completed,
            BistroBuilderInteractionReasonCode.Completed);
    }

    public bool CancelGrant(
        BistroBuilderInteractionGrantHandle handle,
        BistroBuilderInteractionReasonCode reason = BistroBuilderInteractionReasonCode.TaskCancelled)
    {
        return QueueOrEndGrant(handle, BistroBuilderInteractionGrantState.Cancelled, reason);
    }

    public bool InvalidateGrant(
        BistroBuilderInteractionGrantHandle handle,
        BistroBuilderInteractionReasonCode reason)
    {
        return QueueOrEndGrant(handle, BistroBuilderInteractionGrantState.Invalidated, reason);
    }

    public bool ReleaseGrant(BistroBuilderInteractionGrantHandle handle)
    {
        return QueueOrEndGrant(
            handle,
            BistroBuilderInteractionGrantState.Released,
            BistroBuilderInteractionReasonCode.Released);
    }

    public int InvalidateHolder(
        string holderId,
        BistroBuilderInteractionReasonCode reason = BistroBuilderInteractionReasonCode.HolderInvalidated)
    {
        if (string.IsNullOrWhiteSpace(holderId)) return 0;
        List<string> ids = grants.Values
            .Where(g => g != null && string.Equals(g.holderId, holderId, StringComparison.Ordinal))
            .Select(g => g.grantId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        int affected = 0;
        for (int i = 0; i < ids.Count; i++)
        {
            if (!grants.TryGetValue(ids[i], out BistroBuilderInteractionGrantRecord grant) || grant == null)
                continue;
            if (grant.kind == BistroBuilderInteractionGrantKind.Custody &&
                grant.state == BistroBuilderInteractionGrantState.Held)
            {
                BeginCustodyRecoveryInternal(grant, reason);
                affected++;
            }
            else if (EndGrantInternal(
                         grant.Handle,
                         BistroBuilderInteractionGrantState.Invalidated,
                         reason))
                affected++;
        }
        return affected;
    }

    public int InvalidateSession(
        string sessionId,
        BistroBuilderInteractionReasonCode reason = BistroBuilderInteractionReasonCode.SessionEnded)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return 0;
        List<BistroBuilderInteractionGrantHandle> handles = grants.Values
            .Where(g => g != null && string.Equals(g.sessionId, sessionId, StringComparison.Ordinal))
            .OrderBy(g => g.grantId, StringComparer.Ordinal)
            .Select(g => g.Handle)
            .ToList();
        int count = 0;
        for (int i = 0; i < handles.Count; i++)
            if (EndGrantInternal(handles[i], BistroBuilderInteractionGrantState.Invalidated, reason)) count++;
        return count;
    }
    public bool TryTransferCustody(
        BistroBuilderInteractionGrantHandle handle,
        BistroBuilderInteractionHolderKind newHolderKind,
        string newHolderId,
        out BistroBuilderInteractionGrantHandle newHandle)
    {
        newHandle = default;
        if (resolving || publishing || string.IsNullOrWhiteSpace(newHolderId))
            return false;
        if (!TryGetGrant(handle, out BistroBuilderInteractionGrantRecord grant) ||
            grant.kind != BistroBuilderInteractionGrantKind.Custody ||
            (grant.state != BistroBuilderInteractionGrantState.Held &&
             grant.state != BistroBuilderInteractionGrantState.Recovery))
            return false;
        if (WouldCreateCustodyCycle(grant.resourceId, newHolderKind, newHolderId))
        {
            AddTrace(grant.holderId, "CustodyTransferRejected", grant.grantId, string.Empty,
                BistroBuilderInteractionReasonCode.CustodyCycle, newHolderId);
            return false;
        }

        string oldHolder = grant.holderId;
        grant.holderKind = newHolderKind;
        grant.holderId = newHolderId.Trim();
        grant.state = BistroBuilderInteractionGrantState.Held;
        grant.generation = NextGeneration();
        grant.lastReason = BistroBuilderInteractionReasonCode.None;
        newHandle = grant.Handle;
        AddTrace(newHolderId, "CustodyTransferred", grant.grantId, string.Empty,
            BistroBuilderInteractionReasonCode.None, oldHolder + " -> " + newHolderId);
        NotifyGrantChanged(grant, "CustodyTransferred");
        return true;
    }

    public bool ResolveCustodyRecovery(
        BistroBuilderInteractionGrantHandle handle,
        BistroBuilderInteractionHolderKind newHolderKind,
        string newHolderId,
        out BistroBuilderInteractionGrantHandle newHandle)
    {
        return TryTransferCustody(handle, newHolderKind, newHolderId, out newHandle);
    }

    public bool TryCallNextWaitTicket(
        string resourceId,
        out BistroBuilderInteractionGrantHandle handle)
    {
        handle = default;
        if (string.IsNullOrWhiteSpace(resourceId)) return false;
        BistroBuilderInteractionGrantRecord ticket = grants.Values
            .Where(g => g != null && g.kind == BistroBuilderInteractionGrantKind.WaitTicket &&
                        g.state == BistroBuilderInteractionGrantState.Queued &&
                        string.Equals(g.resourceId, resourceId, StringComparison.Ordinal))
            .OrderBy(g => g.sequence)
            .ThenBy(g => g.grantId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (ticket == null) return false;
        ticket.state = BistroBuilderInteractionGrantState.Called;
        ticket.lastReason = BistroBuilderInteractionReasonCode.None;
        handle = ticket.Handle;
        NotifyGrantChanged(ticket, "WaitTicketCalled");
        return true;
    }

    public IReadOnlyList<BistroBuilderInteractionGrantRecord> GetActiveGrantSnapshot()
    {
        return grants.Values
            .Where(g => g != null && !g.IsTerminal)
            .OrderBy(g => g.grantId, StringComparer.Ordinal)
            .Select(CloneGrant)
            .ToList();
    }
    private bool QueueOrEndGrant(
        BistroBuilderInteractionGrantHandle handle,
        BistroBuilderInteractionGrantState state,
        BistroBuilderInteractionReasonCode reason)
    {
        if (!TryGetGrant(handle, out _)) return false;
        if (resolving || publishing)
        {
            deferredMutations.Enqueue(() => EndGrantInternal(handle, state, reason));
            AddTrace(string.Empty, "MutationDeferred", handle.grantId, string.Empty,
                BistroBuilderInteractionReasonCode.ReentrantMutationDeferred, state.ToString());
            return true;
        }
        return EndGrantInternal(handle, state, reason);
    }

    private bool EndGrantInternal(
        BistroBuilderInteractionGrantHandle handle,
        BistroBuilderInteractionGrantState finalState,
        BistroBuilderInteractionReasonCode reason)
    {
        if (!TryGetGrant(handle, out BistroBuilderInteractionGrantRecord grant)) return false;
        List<BistroBuilderInteractionGrantHandle> children = grants.Values
            .Where(g => g != null && string.Equals(g.parentGrantId, grant.grantId, StringComparison.Ordinal))
            .OrderBy(g => g.grantId, StringComparer.Ordinal)
            .Select(g => g.Handle)
            .ToList();
        for (int i = 0; i < children.Count; i++)
            EndGrantInternal(
                children[i],
                BistroBuilderInteractionGrantState.Invalidated,
                BistroBuilderInteractionReasonCode.DependencyInvalid);

        if (!string.IsNullOrWhiteSpace(grant.spatialLeaseId))
        {
            bbsisBridge?.ReleaseSpatialLease(grant.spatialLeaseId);
            grant.spatialLeaseId = string.Empty;
        }
        grant.state = finalState;
        grant.lastReason = reason;
        AddTrace(grant.holderId, "GrantEnded", grant.grantId, string.Empty, reason, finalState.ToString());
        NotifyGrantChanged(grant, "GrantEnded");
        grants.Remove(grant.grantId);
        return true;
    }

    private void BeginCustodyRecoveryInternal(
        BistroBuilderInteractionGrantRecord grant,
        BistroBuilderInteractionReasonCode reason)
    {
        if (grant == null || grant.kind != BistroBuilderInteractionGrantKind.Custody) return;
        grant.state = BistroBuilderInteractionGrantState.Recovery;
        grant.generation = NextGeneration();
        grant.lastReason = reason;
        AddTrace(grant.holderId, "CustodyRecovery", grant.grantId, string.Empty, reason, grant.resourceId);
        NotifyGrantChanged(grant, "CustodyRecovery");
    }

    private void InvalidateTargetInternal(
        string targetId,
        BistroBuilderInteractionReasonCode reason)
    {
        List<BistroBuilderInteractionGrantHandle> handles = grants.Values
            .Where(g => g != null && string.Equals(g.targetId, targetId, StringComparison.Ordinal))
            .OrderBy(g => g.grantId, StringComparer.Ordinal)
            .Select(g => g.Handle)
            .ToList();
        for (int i = 0; i < handles.Count; i++)
            EndGrantInternal(handles[i], BistroBuilderInteractionGrantState.Invalidated, reason);
    }
    private bool RevalidateGrantTarget(
        BistroBuilderInteractionGrantRecord grant,
        out BistroBuilderInteractionReasonCode reason)
    {
        reason = BistroBuilderInteractionReasonCode.None;
        if (grant == null)
        {
            reason = BistroBuilderInteractionReasonCode.InvalidRequest;
            return false;
        }
        if (string.IsNullOrWhiteSpace(grant.targetId)) return true;
        if (!TryGetTarget(grant.targetId, out BistroBuilderInteractionTarget target) || target == null)
        {
            reason = BistroBuilderInteractionReasonCode.TargetUnavailable;
            return false;
        }
        return target.TryResolve(
            grant.channelId,
            grant.slotId,
            grant.interactionId,
            grant.holderKind,
            out _, out _, out _, out _, out reason);
    }

    private int CountGrants(BistroBuilderInteractionGrantKind kind)
    {
        int count = 0;
        foreach (BistroBuilderInteractionGrantRecord grant in grants.Values)
            if (grant != null && !grant.IsTerminal && grant.kind == kind) count++;
        return count;
    }
    private bool WouldCreateCustodyCycle(
        string resourceId,
        BistroBuilderInteractionHolderKind holderKind,
        string holderId)
    {
        if (holderKind != BistroBuilderInteractionHolderKind.Container ||
            string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(holderId))
            return false;
        string cursor = holderId;
        for (int i = 0; i <= grants.Count; i++)
        {
            if (string.Equals(cursor, resourceId, StringComparison.Ordinal)) return true;
            BistroBuilderInteractionGrantRecord parent = grants.Values.FirstOrDefault(g =>
                g != null && g.kind == BistroBuilderInteractionGrantKind.Custody &&
                (g.state == BistroBuilderInteractionGrantState.Held ||
                 g.state == BistroBuilderInteractionGrantState.Recovery) &&
                string.Equals(g.resourceId, cursor, StringComparison.Ordinal));
            if (parent == null || parent.holderKind != BistroBuilderInteractionHolderKind.Container)
                return false;
            cursor = parent.holderId;
        }
        return true;
    }

    private void QueueOrRunMutation(Action action)
    {
        if (action == null) return;
        if (resolving || publishing) deferredMutations.Enqueue(action);
        else action();
    }

    private void DrainDeferredMutations()
    {
        int safety = 0;
        while (deferredMutations.Count > 0 && safety++ < 10000)
            deferredMutations.Dequeue()?.Invoke();
    }

    private long NextGeneration()
    {
        if (nextGeneration == long.MaxValue)
            throw new InvalidOperationException("Interaction generation space exhausted.");
        return nextGeneration++;
    }
    private void NormalizeRequest(BistroBuilderInteractionAcquisitionRequest request)
    {
        request.requestId = (request.requestId ?? string.Empty).Trim();
        request.holderId = (request.holderId ?? string.Empty).Trim();
        request.interactionId = (request.interactionId ?? string.Empty).Trim();
        request.sessionId = (request.sessionId ?? string.Empty).Trim();
        request.parentGrantId = (request.parentGrantId ?? string.Empty).Trim();
        request.parentRequestId = (request.parentRequestId ?? string.Empty).Trim();
        request.capacityUnits = request.countsCapacity ? Math.Max(1, request.capacityUnits) : 0;
        request.engageWithinSeconds = Mathf.Max(0f, request.engageWithinSeconds);
        request.candidates ??= new List<BistroBuilderInteractionCandidate>();

        var normalized = new Dictionary<string, BistroBuilderInteractionCandidate>(StringComparer.Ordinal);
        for (int i = 0; i < request.candidates.Count; i++)
        {
            BistroBuilderInteractionCandidate candidate = request.candidates[i];
            if (candidate == null) continue;
            candidate.targetId = (candidate.targetId ?? string.Empty).Trim();
            candidate.resourceId = (candidate.resourceId ?? string.Empty).Trim();
            candidate.channelId = (candidate.channelId ?? string.Empty).Trim();
            candidate.slotId = (candidate.slotId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(candidate.targetId) &&
                string.IsNullOrWhiteSpace(candidate.resourceId))
                continue;
            string key = CandidateKey(candidate);
            if (!normalized.TryGetValue(key, out BistroBuilderInteractionCandidate existing) ||
                CompareCandidates(candidate, existing) < 0)
                normalized[key] = candidate;
        }
        request.candidates = normalized.Values
            .OrderBy(c => c, Comparer<BistroBuilderInteractionCandidate>.Create(CompareCandidates))
            .ToList();
        if (string.IsNullOrWhiteSpace(request.requestId))
            request.requestId = BuildStableRequestId(request);
    }

    private static bool ValidateBasicRequest(
        BistroBuilderInteractionAcquisitionRequest request,
        out BistroBuilderInteractionReasonCode reason)
    {
        reason = BistroBuilderInteractionReasonCode.InvalidRequest;
        if (request == null || string.IsNullOrWhiteSpace(request.requestId) ||
            string.IsNullOrWhiteSpace(request.holderId) ||
            string.IsNullOrWhiteSpace(request.interactionId) ||
            request.candidates == null || request.candidates.Count == 0)
            return false;
        if (request.countsCapacity && request.capacityUnits < 1) return false;
        if (!string.IsNullOrWhiteSpace(request.parentGrantId) && request.parentGeneration < 1)
            return false;
        reason = BistroBuilderInteractionReasonCode.None;
        return true;
    }
    private static string CandidateKey(BistroBuilderInteractionCandidate candidate)
    {
        return (candidate.targetId ?? string.Empty) + "|" +
               (candidate.resourceId ?? string.Empty) + "|" +
               (candidate.channelId ?? string.Empty) + "|" +
               (candidate.slotId ?? string.Empty);
    }

    private static int CompareCandidates(
        BistroBuilderInteractionCandidate a,
        BistroBuilderInteractionCandidate b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int value = b.suitability.CompareTo(a.suitability);
        if (value != 0) return value;
        value = a.travelCostHint.CompareTo(b.travelCostHint);
        if (value != 0) return value;
        return string.CompareOrdinal(CandidateKey(a), CandidateKey(b));
    }

    private static string BuildStableRequestId(BistroBuilderInteractionAcquisitionRequest request)
    {
        string semantic = ((int)request.grantKind) + "|" + ((int)request.holderKind) + "|" +
                          request.holderId + "|" + request.interactionId + "|" +
                          request.sessionId + "|" + request.parentGrantId + "|" +
                          request.parentRequestId + "|" +
                          string.Join(";", request.candidates.Select(CandidateKey));
        return "ireq:" + Fnv1a64(semantic).ToString("x16");
    }

    private static string BuildStableBundleId(
        IEnumerable<BistroBuilderInteractionAcquisitionRequest> requests)
    {
        string semantic = string.Join(";", requests
            .Where(r => r != null)
            .Select(r => r.requestId)
            .OrderBy(id => id, StringComparer.Ordinal));
        return "ibundle:" + Fnv1a64(semantic).ToString("x16");
    }

    private static ulong Fnv1a64(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        string text = value ?? string.Empty;
        for (int i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= prime;
        }
        return hash;
    }
    private void PublishRejectedRequest(
        BistroBuilderInteractionAcquisitionRequest request,
        BistroBuilderInteractionReasonCode reason,
        string message)
    {
        var decision = new BistroBuilderInteractionDecision
        {
            requestId = request != null ? request.requestId : string.Empty,
            outcome = BistroBuilderInteractionRequestOutcome.Rejected,
            reason = reason,
            arbitrationEpoch = arbitrationEpoch,
            message = message ?? string.Empty
        };
        if (!string.IsNullOrWhiteSpace(decision.requestId))
            decisions[decision.requestId] = decision;
        PublishDecision(decision);
    }

    private void PublishDecision(BistroBuilderInteractionDecision decision)
    {
        if (decision == null) return;
        publishing = true;
        try { DecisionPublished?.Invoke(decision); }
        finally { publishing = false; }
    }

    private void PublishBundleDecision(BistroBuilderInteractionBundleDecision decision)
    {
        if (decision == null) return;
        publishing = true;
        try { BundleDecisionPublished?.Invoke(decision); }
        finally { publishing = false; }
    }

    private void NotifyGrantChanged(BistroBuilderInteractionGrantRecord grant, string action)
    {
        if (grant == null) return;
        AddTrace(grant.holderId, action, grant.grantId, string.Empty, grant.lastReason, grant.resourceId);
        publishing = true;
        try { GrantChanged?.Invoke(grant); }
        finally { publishing = false; }
    }

    private void AddTrace(
        string subjectId,
        string action,
        string grantId,
        string requestId,
        BistroBuilderInteractionReasonCode reason,
        string detail)
    {
        var record = new BistroBuilderInteractionTraceRecord
        {
            epoch = arbitrationEpoch,
            simulationTime = SimulationTime,
            subjectId = subjectId ?? string.Empty,
            action = action ?? string.Empty,
            grantId = grantId ?? string.Empty,
            requestId = requestId ?? string.Empty,
            reason = reason,
            detail = detail ?? string.Empty
        };
        trace.Add(record);
        while (trace.Count > maxTraceRecords) trace.RemoveAt(0);
        TraceAdded?.Invoke(record);
    }
    public bool CanDiscoverCandidate(
        BistroBuilderInteractionAcquisitionRequest request,
        BistroBuilderInteractionCandidate candidate,
        out BistroBuilderInteractionReasonCode reason)
    {
        reason = BistroBuilderInteractionReasonCode.None;
        if (request == null || candidate == null)
        {
            reason = BistroBuilderInteractionReasonCode.InvalidRequest;
            return false;
        }
        if (string.IsNullOrWhiteSpace(candidate.targetId))
            return !string.IsNullOrWhiteSpace(candidate.resourceId);
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
                out _, out _, out _, out _, out reason))
            return false;
        var context = new BistroBuilderInteractionConditionContext(
            BistroBuilderInteractionConditionPhase.Discovery,
            request.holderId,
            request.holderKind,
            request.interactionId,
            candidate.targetId,
            candidate.channelId,
            candidate.slotId,
            string.IsNullOrWhiteSpace(candidate.resourceId) ? candidate.targetId : candidate.resourceId,
            default);
        return target.EvaluateConditions(in context, out reason);
    }
    private void SubscribeBridge()
    {
        if (bridgeSubscribed || bbsisBridge == null) return;
        bbsisBridge.SpatialLeaseInvalidated += OnBridgeSpatialLeaseInvalidated;
        bridgeSubscribed = true;
    }

    private void UnsubscribeBridge()
    {
        if (!bridgeSubscribed || bbsisBridge == null) return;
        bbsisBridge.SpatialLeaseInvalidated -= OnBridgeSpatialLeaseInvalidated;
        bridgeSubscribed = false;
    }

    private void OnBridgeSpatialLeaseInvalidated(BistroBuilderInteractionGrantHandle handle)
    {
        if (!ValidateCurrentGrant(handle)) return;
        QueueOrRunMutation(() => EndGrantInternal(
            handle,
            BistroBuilderInteractionGrantState.Invalidated,
            BistroBuilderInteractionReasonCode.SpatialDenied));
    }
    private void CacheDependencies()
    {
        if (bbsisBridge == null)
            bbsisBridge = GetComponent<BistroBuilderInteractionBbsisBridge>();
        if (bbsisBridge == null)
            bbsisBridge = FindFirstObjectByType<BistroBuilderInteractionBbsisBridge>();
    }

    private static BistroBuilderInteractionGrantRecord CloneGrant(
        BistroBuilderInteractionGrantRecord source)
    {
        if (source == null) return null;
        return new BistroBuilderInteractionGrantRecord
        {
            grantId = source.grantId,
            generation = source.generation,
            sequence = source.sequence,
            grantedEpoch = source.grantedEpoch,
            kind = source.kind,
            state = source.state,
            holderKind = source.holderKind,
            holderId = source.holderId,
            interactionId = source.interactionId,
            targetId = source.targetId,
            resourceId = source.resourceId,
            channelId = source.channelId,
            slotId = source.slotId,
            sessionId = source.sessionId,
            parentGrantId = source.parentGrantId,
            parentGeneration = source.parentGeneration,
            taskPriorityClass = source.taskPriorityClass,
            suitability = source.suitability,
            travelCostHint = source.travelCostHint,
            capacityUnits = source.capacityUnits,
            countsCapacity = source.countsCapacity,
            persistCanonical = source.persistCanonical,
            conflictKey = source.conflictKey,
            logicalCapacity = source.logicalCapacity,
            targetExclusive = source.targetExclusive,
            engageBySimulationTime = source.engageBySimulationTime,
            spatialLeaseId = source.spatialLeaseId,
            lastReason = source.lastReason
        };
    }

    private sealed class PendingRequest
    {
        public BistroBuilderInteractionAcquisitionRequest request;
        public long firstEligibleEpoch;
        public BistroBuilderInteractionReasonCode lastReason;
    }

    private sealed class PendingBundle
    {
        public string bundleId;
        public List<BistroBuilderInteractionAcquisitionRequest> requests;
        public long firstEligibleEpoch;
    }

#if UNITY_EDITOR
    public void SetEditorSimulationTime(double value)
    {
        editorSimulationTimeOverride = value;
    }

    public void ClearEditorSimulationTimeOverride()
    {
        editorSimulationTimeOverride = null;
    }

    public void ConfigureForEditor(BistroBuilderInteractionBbsisBridge bridge)
    {
        bbsisBridge = bridge;
    }
#endif
}