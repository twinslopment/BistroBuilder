using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Expone cliente, servicio y transferencia de una plaza real de barra.
/// La ocupación funcional sigue perteneciendo al sistema de barra.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderBarSpatialAdapter :
    MonoBehaviour,
    IBistroBuilderSpatialSemanticProvider
{
    public const string CustomerPortId = "customer";
    public const string ServicePortId = "waiter.service";
    public const string TransferPortId = "bar.transfer";

    [SerializeField] private BistroBuilderBarServiceSpot barSpot;
    [SerializeField] private BistroBuilderSpatialSubject subject;

    private string customerLeaseId = string.Empty;
    private string customerOwnerId = string.Empty;
    private BistroBuilderSpatialInteractionService spatialService;

    public string SpatialSubjectId =>
        subject != null ? subject.SubjectId : string.Empty;
    public BistroBuilderBarServiceSpot BarSpot => barSpot;
    public BistroBuilderSpatialSubject Subject => subject;
    public bool HasCustomerLease =>
        !string.IsNullOrWhiteSpace(customerLeaseId);

    private void OnDisable()
    {
        ReleaseCustomerLease();
    }

    public void Configure(
        BistroBuilderBarServiceSpot spot,
        BistroBuilderSpatialSubject spatialSubject)
    {
        barSpot = spot;
        subject = spatialSubject;
        spatialService = FindFirstObjectByType<
            BistroBuilderSpatialInteractionService>();
        EnsureAnchors();
    }

    public bool ValidateConfiguration(out string error)
    {
        if (barSpot == null || subject == null ||
            subject.Contract == null || subject.Proxy == null)
        {
            error = "El adaptador espacial de barra está incompleto.";
            return false;
        }

        if (!TryGetPortVolume(
                CustomerPortId, out _, out _) ||
            !TryGetPortVolume(
                ServicePortId, out _, out _) ||
            !TryGetPortVolume(
                TransferPortId, out _, out _))
        {
            error = barSpot.BarSpotId +
                ": faltan puertos espaciales de barra.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryAcquireCustomerLease(
        CustomerGroup group,
        out string rejectionReason)
    {
        rejectionReason = string.Empty;
        if (group == null)
        {
            rejectionReason = "Grupo de barra nulo.";
            return false;
        }

        string ownerId = "bbsis.bar.customer." +
            group.GroupId + "." + barSpot.BarSpotId;
        if (HasCustomerLease &&
            string.Equals(
                customerOwnerId, ownerId, StringComparison.Ordinal))
            return true;

        ReleaseCustomerLease();
        ResolveService();
        if (spatialService == null ||
            !TryGetPortVolume(
                CustomerPortId,
                out BistroBuilderSpatialVolume volume,
                out BistroBuilderSpatialConflictMode mode))
        {
            rejectionReason =
                "BBSIS no puede resolver la plaza de barra.";
            return false;
        }

        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = ownerId,
            subjectId = subject.SubjectId,
            portId = CustomerPortId,
            kind = BistroBuilderSpatialClaimKind.Seat,
            conflictMode = mode,
            volume = volume,
            priority = 80,
            durationSeconds = 0f,
            validateAgainstStaticGeometry = true
        };

        if (!spatialService.TryAcquireLease(
                request,
                out BistroBuilderSpatialLease lease,
                out BistroBuilderSpatialLeaseDecision decision))
        {
            rejectionReason = decision.message;
            return false;
        }

        customerOwnerId = ownerId;
        customerLeaseId = lease.leaseId;
        return true;
    }

    public void ReleaseCustomerLease()
    {
        ResolveService();
        if (spatialService != null &&
            !string.IsNullOrWhiteSpace(customerLeaseId))
            spatialService.ReleaseLease(customerLeaseId);
        customerLeaseId = string.Empty;
        customerOwnerId = string.Empty;
    }

    public bool TryGetPortVolume(
        string portId,
        out BistroBuilderSpatialVolume volume,
        out BistroBuilderSpatialConflictMode conflictMode)
    {
        volume = default;
        conflictMode = BistroBuilderSpatialConflictMode.Block;
        if (subject == null ||
            !subject.TryGetPortWorld(
                portId,
                out Vector3 position,
                out _,
                out float radius,
                out conflictMode))
            return false;
        volume = BistroBuilderSpatialVolume.Circle(
            position, Mathf.Max(0.18f, radius));
        return true;
    }

    public int WriteSemanticVolumes(
        List<BistroBuilderSpatialSemanticVolume> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        if (subject == null || subject.Contract == null)
            return 0;

        int before = results.Count;
        AddSemantic(
            results,
            CustomerPortId,
            BistroBuilderSpatialSemanticRole.SeatBay,
            true);
        AddSemantic(
            results,
            ServicePortId,
            BistroBuilderSpatialSemanticRole.WorkZone,
            true);
        AddSemantic(
            results,
            TransferPortId,
            BistroBuilderSpatialSemanticRole.TransferZone,
            true);
        return results.Count - before;
    }

    private void AddSemantic(
        List<BistroBuilderSpatialSemanticVolume> results,
        string portId,
        BistroBuilderSpatialSemanticRole role,
        bool critical)
    {
        if (!TryGetPortVolume(
                portId,
                out BistroBuilderSpatialVolume volume,
                out BistroBuilderSpatialConflictMode mode))
            return;
        results.Add(new BistroBuilderSpatialSemanticVolume
        {
            subjectId = subject.SubjectId,
            semanticId = portId,
            role = role,
            layer = BistroBuilderSpatialProxyLayer.Operational,
            conflictMode = mode,
            volume = volume,
            critical = critical
        });
    }

    private void EnsureAnchors()
    {
        if (barSpot == null || subject == null)
            return;
        BistroBuilderSpatialPortAnchors anchors =
            subject.GetComponent<BistroBuilderSpatialPortAnchors>();
        if (anchors == null)
            anchors = subject.gameObject.AddComponent<
                BistroBuilderSpatialPortAnchors>();
        anchors.ClearBindings();
        anchors.AddBinding(CustomerPortId, barSpot.CustomerPoint);
        anchors.AddBinding(ServicePortId, barSpot.WaiterServicePoint);
        anchors.AddBinding(TransferPortId, barSpot.WaiterServicePoint);
    }

    private void ResolveService()
    {
        if (spatialService == null)
            spatialService = FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
    }
}
