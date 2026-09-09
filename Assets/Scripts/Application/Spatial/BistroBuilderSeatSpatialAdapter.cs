using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adaptador BBSIS para una silla real. Solo expone semÃ¡ntica espacial;
/// no decide cuÃ¡ndo el sistema de seating debe iniciar una acciÃ³n.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantSeat))]
[RequireComponent(typeof(BistroBuilderSpatialSubject))]
public sealed class BistroBuilderSeatSpatialAdapter : MonoBehaviour,
    IBistroBuilderSpatialSemanticProvider
{
    [SerializeField] private RestaurantSeat seat;
    [SerializeField] private BistroBuilderSpatialSubject subject;

    private BistroBuilderSpatialInteractionService service;
    private readonly List<BistroBuilderSpatialVolume> scratchVolumes =
        new List<BistroBuilderSpatialVolume>(4);
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
        if (results == null || seat == null || subject == null || subject.Proxy == null)
            return 0;
        int before = results.Count;
        string relatedTableId = ResolveAssociatedTableSubjectId();

        if (seat.CustomerApproachPoint != null && seat.UseProfile != null)
        {
            results.Add(new BistroBuilderSpatialSemanticVolume
            {
                subjectId = subject.SubjectId,
                semanticId = "chair.approach",
                relatedSubjectId = relatedTableId,
                role = BistroBuilderSpatialSemanticRole.Approach,
                layer = BistroBuilderSpatialProxyLayer.Operational,
                conflictMode = BistroBuilderSpatialConflictMode.Degrade,
                volume = BistroBuilderSpatialVolume.Circle(
                    seat.CustomerApproachPoint.position,
                    seat.UseProfile.CustomerApproachRadius),
                critical = false
            });
        }

        scratchVolumes.Clear();
        subject.Proxy.BuildWorldVolumes(BistroBuilderSpatialProxyLayer.Dynamic, scratchVolumes);
        for (int i = 0; i < scratchVolumes.Count; i++)
        {
            results.Add(new BistroBuilderSpatialSemanticVolume
            {
                subjectId = subject.SubjectId,
                semanticId = "chair.sweep." + i,
                relatedSubjectId = relatedTableId,
                role = BistroBuilderSpatialSemanticRole.DynamicSweep,
                layer = BistroBuilderSpatialProxyLayer.Dynamic,
                conflictMode = BistroBuilderSpatialConflictMode.Block,
                volume = scratchVolumes[i],
                critical = true
            });
        }
        return results.Count - before;
    }

    public bool TryReserveMotion(
        string ownerId,
        float durationSeconds,
        out BistroBuilderSpatialLeaseDecision decision)
    {
        decision = new BistroBuilderSpatialLeaseDecision();
        CacheReferences();
        if (service == null || subject == null || subject.Proxy == null ||
            string.IsNullOrWhiteSpace(ownerId))
        {
            decision.failure = BistroBuilderSpatialLeaseFailure.InvalidRequest;
            decision.message = "No existe contexto BBSIS vÃ¡lido para reservar la silla.";
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

        scratchVolumes.Clear();
        subject.Proxy.BuildWorldVolumes(BistroBuilderSpatialProxyLayer.Dynamic, scratchVolumes);
        if (scratchVolumes.Count == 0)
        {
            decision.failure = BistroBuilderSpatialLeaseFailure.InvalidRequest;
            decision.message = "La silla no tiene Dynamic Sweep configurado.";
            return false;
        }

        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = ownerId.Trim(),
            subjectId = subject.SubjectId,
            kind = BistroBuilderSpatialClaimKind.DynamicSweep,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = scratchVolumes[0],
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

    public void Configure(RestaurantSeat sourceSeat, BistroBuilderSpatialSubject sourceSubject)
    {
        seat = sourceSeat;
        subject = sourceSubject;
        CacheReferences();
    }

    private string ResolveAssociatedTableSubjectId()
    {
        if (seat == null || seat.AssociatedTable == null) return string.Empty;
        BistroBuilderSpatialSubject tableSubject =
            seat.AssociatedTable.GetComponent<BistroBuilderSpatialSubject>();
        return tableSubject != null ? tableSubject.SubjectId : string.Empty;
    }

    private void CacheReferences()
    {
        if (seat == null) seat = GetComponent<RestaurantSeat>();
        if (subject == null) subject = GetComponent<BistroBuilderSpatialSubject>();
        if (service == null)
            service = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
    }
}
