using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adaptador BBSIS para el barrido real de una puerta. La decisiÃ³n de abrir
/// sigue perteneciendo al sistema consumidor; BBSIS solo ofrece el gate espacial.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BistroBuilderNavigableDoor))]
[RequireComponent(typeof(BistroBuilderSpatialSubject))]
public sealed class BistroBuilderDoorSpatialAdapter : MonoBehaviour,
    IBistroBuilderSpatialSemanticProvider
{
    [SerializeField] private BistroBuilderNavigableDoor door;
    [SerializeField] private BistroBuilderSpatialSubject subject;
    [SerializeField] private BistroBuilderDoorCirculationEnvelope envelope;

    private BistroBuilderSpatialInteractionService service;
    private string activeMotionLeaseId = string.Empty;

    public string SpatialSubjectId => subject != null ? subject.SubjectId : string.Empty;
    public bool HasMotionLease => !string.IsNullOrEmpty(activeMotionLeaseId);

    private void Awake() => CacheReferences();

    private void OnDisable()
    {
        ReleaseMotionReservation();
    }

    public int WriteSemanticVolumes(List<BistroBuilderSpatialSemanticVolume> results)
    {
        if (results == null || subject == null || envelope == null) return 0;
        results.Add(new BistroBuilderSpatialSemanticVolume
        {
            subjectId = subject.SubjectId,
            semanticId = "door.sweep",
            role = BistroBuilderSpatialSemanticRole.DynamicSweep,
            layer = BistroBuilderSpatialProxyLayer.Dynamic,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BuildSweepVolume(),
            critical = true
        });
        return 1;
    }

    public bool TryReserveMotion(
        string ownerId,
        float durationSeconds,
        out BistroBuilderSpatialLeaseDecision decision)
    {
        decision = new BistroBuilderSpatialLeaseDecision();
        CacheReferences();
        if (service == null || subject == null || envelope == null ||
            string.IsNullOrWhiteSpace(ownerId))
        {
            decision.failure = BistroBuilderSpatialLeaseFailure.InvalidRequest;
            decision.message = "No existe contexto BBSIS vÃ¡lido para reservar la puerta.";
            return false;
        }
        if (!string.IsNullOrEmpty(activeMotionLeaseId))
        {
            bool refreshed = service.RefreshLease(
                activeMotionLeaseId,
                Mathf.Max(0.01f, durationSeconds));
            decision.granted = refreshed;
            decision.failure = refreshed
                ? BistroBuilderSpatialLeaseFailure.None
                : BistroBuilderSpatialLeaseFailure.InvalidRequest;
            return refreshed;
        }

        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = ownerId.Trim(),
            subjectId = subject.SubjectId,
            kind = BistroBuilderSpatialClaimKind.DynamicSweep,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BuildSweepVolume(),
            durationSeconds = Mathf.Max(0.01f, durationSeconds),
            validateAgainstStaticGeometry = true
        };
        if (!service.TryAcquireLease(
                request,
                out BistroBuilderSpatialLease lease,
                out decision))
            return false;
        activeMotionLeaseId = lease.leaseId;
        return true;
    }

    public void ReleaseMotionReservation()
    {
        if (string.IsNullOrEmpty(activeMotionLeaseId)) return;
        service?.ReleaseLease(activeMotionLeaseId);
        activeMotionLeaseId = string.Empty;
    }

    public void Configure(
        BistroBuilderNavigableDoor sourceDoor,
        BistroBuilderSpatialSubject sourceSubject)
    {
        door = sourceDoor;
        subject = sourceSubject;
        envelope = sourceDoor != null
            ? sourceDoor.GetComponent<BistroBuilderDoorCirculationEnvelope>()
            : null;
        CacheReferences();
    }

    private BistroBuilderSpatialVolume BuildSweepVolume()
    {
        Vector3 center = envelope != null ? envelope.WorldCenter : transform.position;
        Vector2 size = envelope != null ? envelope.WorldSize : Vector2.one;
        return BistroBuilderSpatialVolume.Box(
            center,
            transform.right,
            transform.forward,
            size * 0.5f);
    }

    private void CacheReferences()
    {
        if (door == null) door = GetComponent<BistroBuilderNavigableDoor>();
        if (subject == null) subject = GetComponent<BistroBuilderSpatialSubject>();
        if (envelope == null) envelope = GetComponent<BistroBuilderDoorCirculationEnvelope>();
        if (service == null)
            service = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
    }
}

