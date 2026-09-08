using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Frontera pública hacia BBSIS. Interaction nunca almacena geometría espacial propia.
/// </summary>
public interface IBistroBuilderInteractionSpatialBridge
{
    bool TryAcquireSpatialLease(
        BistroBuilderInteractionGrantRecord grant,
        BistroBuilderInteractionTarget target,
        BistroBuilderInteractionSpatialBindingDefinition binding,
        float durationSeconds,
        out string leaseId,
        out BistroBuilderInteractionReasonCode reason);

    bool RefreshSpatialLease(string leaseId, float durationSeconds);
    bool ReleaseSpatialLease(string leaseId);
}

/// <summary>
/// Adaptador que traduce referencias semánticas Interaction a Claims/Leases BBSIS.
/// Toda posición/volumen procede del Spatial Subject y su Spatial Contract.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderInteractionBbsisBridge :
    MonoBehaviour,
    IBistroBuilderInteractionSpatialBridge
{
    [SerializeField] private BistroBuilderSpatialInteractionService spatialService;
    private readonly Dictionary<string, BistroBuilderInteractionGrantHandle> leases =
        new Dictionary<string, BistroBuilderInteractionGrantHandle>(StringComparer.Ordinal);
    private bool subscribed;

    public event Action<BistroBuilderInteractionGrantHandle> SpatialLeaseInvalidated;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool TryAcquireSpatialLease(
        BistroBuilderInteractionGrantRecord grant,
        BistroBuilderInteractionTarget target,
        BistroBuilderInteractionSpatialBindingDefinition binding,
        float durationSeconds,
        out string leaseId,
        out BistroBuilderInteractionReasonCode reason)
    {
        leaseId = string.Empty;
        reason = BistroBuilderInteractionReasonCode.None;
        CacheDependencies();
        if (grant == null || target == null || binding == null ||
            !binding.RequiresBbsis || spatialService == null || target.SpatialSubject == null)
        {
            reason = BistroBuilderInteractionReasonCode.SpatialBindingMissing;
            return false;
        }
        if (!TryBuildSpatialRequest(
                grant, target, binding, durationSeconds,
                out BistroBuilderSpatialClaimRequest request, out reason))
            return false;

        if (!spatialService.TryAcquireLease(
                request,
                out BistroBuilderSpatialLease lease,
                out BistroBuilderSpatialLeaseDecision decision))
        {
            reason = decision != null &&
                     decision.failure == BistroBuilderSpatialLeaseFailure.SubjectUnavailable
                ? BistroBuilderInteractionReasonCode.SpatialBindingMissing
                : BistroBuilderInteractionReasonCode.SpatialDenied;
            return false;
        }
        leaseId = lease != null ? lease.leaseId : string.Empty;
        return !string.IsNullOrWhiteSpace(leaseId);
    }

    public bool RefreshSpatialLease(string leaseId, float durationSeconds)
    {
        CacheDependencies();
        return spatialService != null &&
               !string.IsNullOrWhiteSpace(leaseId) &&
               spatialService.RefreshLease(leaseId, durationSeconds > 0f ? Mathf.Max(0.05f, durationSeconds) : 0f);
    }

    public bool ReleaseSpatialLease(string leaseId)
    {
        CacheDependencies();
        if (spatialService == null || string.IsNullOrWhiteSpace(leaseId)) return false;
        return spatialService.ReleaseLease(leaseId);
    }

    private bool TryBuildSpatialRequest(
        BistroBuilderInteractionGrantRecord grant,
        BistroBuilderInteractionTarget target,
        BistroBuilderInteractionSpatialBindingDefinition binding,
        float durationSeconds,
        out BistroBuilderSpatialClaimRequest request,
        out BistroBuilderInteractionReasonCode reason)
    {
        request = null;
        reason = BistroBuilderInteractionReasonCode.SpatialBindingMissing;
        BistroBuilderSpatialSubject subject = target.SpatialSubject;
        if (subject == null || subject.Contract == null) return false;

        BistroBuilderSpatialVolume volume;
        BistroBuilderSpatialConflictMode conflictMode;
        if (binding.bindingKind == BistroBuilderInteractionSpatialBindingKind.Port)
        {
            if (!subject.TryGetPortWorld(
                    binding.bindingId, out Vector3 position, out _,
                    out float radius, out conflictMode))
                return false;
            volume = BistroBuilderSpatialVolume.Circle(position, radius);
        }
        else if (binding.bindingKind == BistroBuilderInteractionSpatialBindingKind.WorkEdge)
        {
            if (!TryResolveWorkEdge(subject, binding.bindingId, out volume, out conflictMode))
                return false;
        }
        else
        {
            reason = BistroBuilderInteractionReasonCode.None;
            return true;
        }

        request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "interaction:" + grant.grantId,
            subjectId = subject.SubjectId,
            portId = binding.bindingId,
            kind = binding.claimKind,
            conflictMode = conflictMode,
            volume = volume,
            priority = grant.taskPriorityClass,
            durationSeconds = Mathf.Max(0.05f, durationSeconds),
            validateAgainstStaticGeometry = false
        };
        reason = BistroBuilderInteractionReasonCode.None;
        return true;
    }

    private static bool TryResolveWorkEdge(
        BistroBuilderSpatialSubject subject,
        string edgeId,
        out BistroBuilderSpatialVolume volume,
        out BistroBuilderSpatialConflictMode conflictMode)
    {
        volume = default;
        conflictMode = BistroBuilderSpatialConflictMode.Reservable;
        if (subject == null || subject.Contract == null || string.IsNullOrWhiteSpace(edgeId))
            return false;
        for (int i = 0; i < subject.Contract.WorkEdges.Count; i++)
        {
            BistroBuilderSpatialWorkEdgeDefinition edge = subject.Contract.WorkEdges[i];
            if (edge == null || !string.Equals(edge.edgeId, edgeId, StringComparison.Ordinal))
                continue;
            Vector3 start = subject.transform.TransformPoint(edge.localStart);
            Vector3 end = subject.transform.TransformPoint(edge.localEnd);
            Vector3 right = end - start;
            right.y = 0f;
            float length = Mathf.Max(0.1f, right.magnitude);
            right = right.sqrMagnitude > 0.000001f ? right.normalized : subject.transform.right;
            Vector3 forward = subject.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
            float depth = Mathf.Max(0.05f, edge.serviceDepth);
            Vector3 center = (start + end) * 0.5f + forward * (depth * 0.5f);
            volume = BistroBuilderSpatialVolume.Box(
                center, right, forward, new Vector2(length * 0.5f, depth * 0.5f));
            conflictMode = edge.conflictMode;
            return true;
        }
        return false;
    }
    private void Subscribe()
    {
        if (subscribed || spatialService == null) return;
        spatialService.LeaseReleased += OnSpatialLeaseReleased;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || spatialService == null) return;
        spatialService.LeaseReleased -= OnSpatialLeaseReleased;
        subscribed = false;
    }

    private void OnSpatialLeaseReleased(string leaseId)
    {
        if (string.IsNullOrWhiteSpace(leaseId) ||
            !leases.TryGetValue(leaseId, out BistroBuilderInteractionGrantHandle handle))
            return;
        leases.Remove(leaseId);
        SpatialLeaseInvalidated?.Invoke(handle);
    }
    private void CacheDependencies()
    {
        if (spatialService == null)
            spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderSpatialInteractionService service)
    {
        spatialService = service;
    }
#endif
}