using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa un objeto movil mediante Mobility y Carry Envelopes.
/// No mueve el objeto ni calcula rutas: solo publica y reserva su espacio.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BistroBuilderSpatialSubject))]
public sealed class BistroBuilderMobileSpatialAdapter :
    MonoBehaviour,
    IBistroBuilderSpatialSemanticProvider
{
    [SerializeField] private BistroBuilderSpatialSubject subject;
    [SerializeField] private BistroBuilderMobilitySpatialProfileDefinition profile;
    [SerializeField] private BistroBuilderSpatialInteractionService spatialService;
    [SerializeField] private string ownerId = string.Empty;

    private string mobilityLeaseId = string.Empty;
    private string carryLeaseId = string.Empty;
    private string movementEpisodeId = string.Empty;
    private Vector3 reservedPosition;
    private Quaternion reservedRotation;
    private bool poseInitialized;
    private int loadUnits;
    private int stableTicks;

    public string SpatialSubjectId =>
        subject != null ? subject.SubjectId : string.Empty;
    public string OwnerId => ownerId;
    public string MobilityLeaseId => mobilityLeaseId;
    public string CarryLeaseId => carryLeaseId;
    public int LoadUnits => loadUnits;
    public int RejectedRelocations { get; private set; }
    public BistroBuilderSpatialLeaseDecision LastDecision { get; private set; } =
        new BistroBuilderSpatialLeaseDecision();

    private void Awake()
    {
        CacheReferences();
    }
    private void OnDisable()
    {
        ReleaseSpatialState(BistroBuilderSpatialEpisodeState.Cancelled);
    }

    public void Configure(
        BistroBuilderSpatialSubject spatialSubject,
        BistroBuilderMobilitySpatialProfileDefinition spatialProfile,
        string stableOwnerId)
    {
        subject = spatialSubject;
        profile = spatialProfile;
        ownerId = stableOwnerId ?? string.Empty;
        CacheReferences();
        ConfigureProxy();
        poseInitialized = false;
    }

    public void SetLoadUnits(int value)
    {
        loadUnits = Mathf.Clamp(
            value,
            0,
            profile != null ? profile.MaximumLoadUnits : 1);
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheReferences();
        if (subject == null || spatialService == null ||
            profile == null || string.IsNullOrWhiteSpace(ownerId))
        {
            error = name + ": movilidad BBSIS incompleta.";
            return false;
        }
        if (!profile.ValidateDefinition(out error))
            return false;
        if (subject.Contract == null ||
            !subject.Contract.HasTrait("mobility.cart"))
        {
            error = subject.SubjectId +
                ": falta trait mobility.cart.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TickSpatial(float leaseDurationSeconds)
    {
        CacheReferences();
        if (!isActiveAndEnabled ||
            !ValidateConfiguration(out _))
            return false;

        Vector3 currentPosition = transform.position;
        Quaternion currentRotation = transform.rotation;
        if (!poseInitialized)
        {
            reservedPosition = currentPosition;
            reservedRotation = currentRotation;
            poseInitialized = true;
        }
        float distance = PlanarDistance(
            reservedPosition,
            currentPosition);
        bool moving = distance >= profile.MovementThreshold;
        if (moving)
        {
            stableTicks = 0;
            EnsureMovementEpisode();
        }
        else if (++stableTicks >= 2)
        {
            EndMovementEpisode(
                BistroBuilderSpatialEpisodeState.Completed);
        }

        float duration = Mathf.Max(0.1f, leaseDurationSeconds);
        Vector3 mobilityOrigin = string.IsNullOrEmpty(mobilityLeaseId)
            ? currentPosition
            : reservedPosition;
        BistroBuilderSpatialVolume mobility = BuildEnvelope(
            mobilityOrigin,
            currentPosition,
            currentRotation,
            false);
        if (!TryAcquireLeaseCandidate(
                BistroBuilderSpatialClaimKind.Mobility,
                "mobility.envelope",
                mobility,
                duration,
                out string candidateMobilityLeaseId,
                out BistroBuilderSpatialLeaseDecision decision))
        {
            LastDecision = decision;
            RejectedRelocations++;
            return false;
        }

        if (loadUnits <= 0)
        {
            CommitRelocation(
                candidateMobilityLeaseId,
                string.Empty,
                currentPosition,
                currentRotation);
            LastDecision = decision;
            return true;
        }

        BistroBuilderSpatialVolume carry = BuildEnvelope(
            currentPosition,
            currentPosition,
            currentRotation,
            true);
        if (!TryAcquireLeaseCandidate(
                BistroBuilderSpatialClaimKind.Carry,
                "carry.envelope",
                carry,
                duration,
                out string candidateCarryLeaseId,
                out decision))
        {
            spatialService.ReleaseLease(candidateMobilityLeaseId);
            LastDecision = decision;
            RejectedRelocations++;
            return false;
        }

        CommitRelocation(
            candidateMobilityLeaseId,
            candidateCarryLeaseId,
            currentPosition,
            currentRotation);
        LastDecision = decision;
        return true;
    }
    public int WriteSemanticVolumes(
        List<BistroBuilderSpatialSemanticVolume> results)
    {
        if (results == null || subject == null || profile == null)
            return 0;
        int before = results.Count;
        results.Add(new BistroBuilderSpatialSemanticVolume
        {
            subjectId = subject.SubjectId,
            semanticId = "mobility.envelope",
            role = BistroBuilderSpatialSemanticRole.MobilityEnvelope,
            layer = BistroBuilderSpatialProxyLayer.Dynamic,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BuildEnvelope(
                transform.position,
                transform.position,
                transform.rotation,
                false),
            critical = true
        });
        if (loadUnits > 0)
        {
            results.Add(new BistroBuilderSpatialSemanticVolume
            {
                subjectId = subject.SubjectId,
                semanticId = "carry.envelope",
                role = BistroBuilderSpatialSemanticRole.CarryEnvelope,
                layer = BistroBuilderSpatialProxyLayer.Operational,
                conflictMode = BistroBuilderSpatialConflictMode.Block,
                volume = BuildEnvelope(
                    transform.position,
                    transform.position,
                    transform.rotation,
                    true),
                critical = true
            });
        }
        return results.Count - before;
    }

    public void ReleaseSpatialState(
        BistroBuilderSpatialEpisodeState finalState)
    {
        ReleaseLease(ref mobilityLeaseId);
        ReleaseLease(ref carryLeaseId);
        EndMovementEpisode(finalState);
        poseInitialized = false;
    }
    private bool TryAcquireLeaseCandidate(
        BistroBuilderSpatialClaimKind kind,
        string portId,
        BistroBuilderSpatialVolume volume,
        float duration,
        out string leaseId,
        out BistroBuilderSpatialLeaseDecision decision)
    {
        leaseId = string.Empty;
        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = ownerId,
            subjectId = subject.SubjectId,
            portId = portId,
            kind = kind,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = volume,
            priority = kind == BistroBuilderSpatialClaimKind.Carry
                ? 82
                : 80,
            durationSeconds = duration,
            validateAgainstStaticGeometry = true
        };
        if (!spatialService.TryAcquireLease(
                request,
                out BistroBuilderSpatialLease lease,
                out decision))
            return false;

        leaseId = lease.leaseId;
        return true;
    }

    private void CommitRelocation(
        string newMobilityLeaseId,
        string newCarryLeaseId,
        Vector3 position,
        Quaternion rotation)
    {
        ReleaseLease(ref mobilityLeaseId);
        ReleaseLease(ref carryLeaseId);
        mobilityLeaseId = newMobilityLeaseId ?? string.Empty;
        carryLeaseId = newCarryLeaseId ?? string.Empty;
        reservedPosition = position;
        reservedRotation = rotation;
    }

    private BistroBuilderSpatialVolume BuildEnvelope(
        Vector3 from,
        Vector3 to,
        Quaternion rotation,
        bool carry)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        float distance = delta.magnitude;
        Vector3 forward = distance > 0.001f
            ? delta / distance
            : rotation * Vector3.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f
            ? forward.normalized
            : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float loadRatio = profile.MaximumLoadUnits > 0
            ? Mathf.Clamp01((float)loadUnits / profile.MaximumLoadUnits)
            : 0f;
        float width = profile.BodyWidth +
            profile.MovementMargin * 2f +
            (carry ? profile.CarryWidthExpansion * loadRatio : 0f);
        float depth = profile.BodyDepth +
            profile.MovementMargin * 2f +
            (carry ? profile.CarryDepthExpansion * loadRatio : 0f);
        float trailing = distance > 0.001f
            ? profile.TrailingClearance
            : 0f;
        Vector3 center = (from + to) * 0.5f -
            forward * trailing * 0.5f;
        return BistroBuilderSpatialVolume.Box(
            center,
            right,
            forward,
            new Vector2(
                width * 0.5f,
                depth * 0.5f + distance * 0.5f +
                trailing * 0.5f));
    }

    private void ConfigureProxy()
    {
        if (subject == null || profile == null || subject.Proxy == null)
            return;
        BistroBuilderAdaptiveSpatialProxy proxy = subject.Proxy;
        proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Layered);
        proxy.ClearParts();
        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "cart.body",
            layer = BistroBuilderSpatialProxyLayer.Static,
            shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
            size = new Vector2(profile.BodyWidth, profile.BodyDepth)
        });
        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "cart.mobility",
            layer = BistroBuilderSpatialProxyLayer.Dynamic,
            shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
            size = new Vector2(
                profile.BodyWidth + profile.MovementMargin * 2f,
                profile.BodyDepth + profile.MovementMargin * 2f)
        });
    }
    private void EnsureMovementEpisode()
    {
        if (!string.IsNullOrEmpty(movementEpisodeId) ||
            spatialService == null)
            return;
        if (spatialService.TryBeginEpisode(
                ownerId,
                "logistics.mobility",
                subject.SubjectId,
                out BistroBuilderSpatialEpisode episode))
            movementEpisodeId = episode.episodeId;
    }

    private void EndMovementEpisode(
        BistroBuilderSpatialEpisodeState finalState)
    {
        if (spatialService != null &&
            !string.IsNullOrEmpty(movementEpisodeId))
            spatialService.EndEpisode(movementEpisodeId, finalState);
        movementEpisodeId = string.Empty;
    }

    private void ReleaseLease(ref string leaseId)
    {
        if (spatialService != null &&
            !string.IsNullOrEmpty(leaseId))
            spatialService.ReleaseLease(leaseId);
        leaseId = string.Empty;
    }

    private void CacheReferences()
    {
        if (subject == null)
            subject = GetComponent<BistroBuilderSpatialSubject>();
        if (spatialService == null)
            spatialService = FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
    }

    private static float PlanarDistance(Vector3 first, Vector3 second)
    {
        Vector3 delta = second - first;
        delta.y = 0f;
        return delta.magnitude;
    }
}
