using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional physical seat for one existing native bar spot. It does not create capacity,
/// allocate customers or own occupancy. The spot and BBSIS retain those authorities.
/// A floor approach and an elevated seat frame are distinct presentation contracts.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantPlaceableObject), typeof(BistroBuilderSpatialSubject))]
public sealed class BistroBuilderBarSeatBinding : MonoBehaviour, IRestaurantPlaceableLifecycleGuard,
    IBistroBuilderSpatialLifecycleOwner, IRestaurantPlaceableActivationParticipant,
    IBistroBuilderSpatialSemanticProvider, IBistroBuilderSpatialCandidateSemanticProvider
{
    public const string SpatialFamilyId = "seating.bar";
    public static string BuildSpatialId(string instanceId) => "spatial.bar.seat." + instanceId + ".body";
    [SerializeField] private Transform seatFrame;
    [SerializeField] private Transform approachFrame;
    [SerializeField] private float minimumCounterGap = 0.2f;
    [SerializeField] private float maximumCounterGap = 0.4f;
    [SerializeField] private float positionTolerance = 0.08f;
    [SerializeField] private float maximumFacingAngle = 10f;
    [SerializeField] private float approachRadius = 0.32f;
    [SerializeField] private bool automaticAssociation = true;
    [SerializeField] private bool requirePersistedBar;

    private RestaurantPlaceableRegistry placeables;
    private BistroBuilderBarServiceRegistry bars;
    private BistroBuilderSpatialInteractionService spatial;
    private RestaurantPlaceableObject placeable;
    private BistroBuilderSpatialSubject subject;
    private BistroBuilderBarServiceSpot attachedSpot;
    private BistroBuilderSpatialPlacementAssessmentService assessment;
    private readonly List<BistroBuilderSpatialVolume> body = new List<BistroBuilderSpatialVolume>();
    private readonly List<RestaurantPlacementShape> physicalShapes = new List<RestaurantPlacementShape>();

    public Transform SeatFrame => seatFrame;
    public bool RequiresPersistedBar => requirePersistedBar;
    public Transform ApproachFrame => approachFrame;
    public BistroBuilderBarServiceSpot AttachedSpot => attachedSpot;
    public string SpatialSubjectId { get { Resolve(); return subject != null ? subject.SubjectId : string.Empty; } }
    public CustomerGroup Occupant => attachedSpot != null ? attachedSpot.AssignedCustomerGroup : null;
    public bool IsSpatialLifecycleActive
    {
        get { Resolve(); return isActiveAndEnabled && placeable != null && placeable.HasInstanceId &&
            placeables != null && placeables.ContainsPlaceable(placeable); }
    }

    private void Awake() { Resolve(); subject?.ConfigureLifecycleOwner(this); }
    private void OnEnable()
    {
        Resolve(); Subscribe();
        if (automaticAssociation && Application.isPlaying)
        {
            var constraints = FindFirstObjectByType<RestaurantPlacementConstraintService>();
            if (constraints != null && constraints.GetComponent<BistroBuilderBarSeatPlacementConstraintRule>() == null)
            {
                constraints.gameObject.AddComponent<BistroBuilderBarSeatPlacementConstraintRule>();
                constraints.RefreshRules();
            }
        }
    }
    private void OnDisable() { Unsubscribe(); RollbackActivation(); }

    public void ConfigureRuntime(Transform seat, Transform approach, float minGap, float maxGap,
        float tolerance, float facingAngle, float mobilityRadius)
    {
        if (attachedSpot != null) throw new InvalidOperationException("Detach the native bar seat before changing its authoring.");
        seatFrame = seat; approachFrame = approach; minimumCounterGap = minGap; maximumCounterGap = maxGap;
        positionTolerance = tolerance; maximumFacingAngle = facingAngle; approachRadius = mobilityRadius;
        Resolve(); subject?.ConfigureLifecycleOwner(this);
    }

    public bool ValidateConfiguration(out string error)
    {
        Resolve();
        error = "The bar seat needs its own valid BBSIS body, seat frame, floor approach and physical profile.";
        BistroBuilderSpatialPhysicalFootprintAdapter physical = GetComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
        if (subject == null || subject.transform != transform || !subject.ValidateSubject(out _) ||
            subject.Contract.FamilyId != SpatialFamilyId || physical == null || !physical.enabled ||
            seatFrame == null || approachFrame == null || seatFrame == approachFrame ||
            !seatFrame.IsChildOf(transform) || !approachFrame.IsChildOf(transform) ||
            !Finite(seatFrame.position) || !Finite(approachFrame.position) ||
            !Finite(seatFrame.forward) || !Finite(seatFrame.up) || !Finite(approachFrame.up) ||
            Vector3.Angle(seatFrame.up, Vector3.up) > 0.1f || Vector3.Angle(approachFrame.up, Vector3.up) > 0.1f ||
            !Positive(minimumCounterGap) || !Positive(maximumCounterGap) || maximumCounterGap < minimumCounterGap ||
            !Positive(positionTolerance) || positionTolerance > 0.15f || !Positive(maximumFacingAngle) ||
            maximumFacingAngle > 30f || !Positive(approachRadius) || approachRadius > 0.6f ||
            !Finite(transform.lossyScale) || Vector3.Distance(transform.lossyScale, Vector3.one) > 0.0001f ||
            Vector3.Angle(transform.up, Vector3.up) > 0.1f) return false;
        physicalShapes.Clear();
        if (!physical.TryWriteShapes(transform.position, transform.rotation, physicalShapes, out error)) return false;
        body.Clear(); subject.Proxy.BuildWorldVolumes(BistroBuilderSpatialProxyLayer.Static, body);
        bool seatOverBody = false;
        foreach (BistroBuilderSpatialVolume volume in body)
        {
            seatOverBody |= volume.ContainsPoint(seatFrame.position);
            if (volume.Overlaps(BistroBuilderSpatialVolume.Circle(approachFrame.position, approachRadius)))
            { error = "The customer's floor approach intersects the stool body."; return false; }
        }
        if (!seatOverBody || seatFrame.position.y <= approachFrame.position.y)
            return false;
        error = string.Empty; return true;
    }

    public bool ValidateForSpot(BistroBuilderBarServiceSpot spot, out string error)
        => ValidateForSpotAtPose(spot, transform.position, transform.rotation, out error);

    public bool ValidateForSpotAtPose(BistroBuilderBarServiceSpot spot, Vector3 position,
        Quaternion rotation, out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        error = "The stool does not match a single native bar spot with an authored counter surface.";
        if (!Finite(position) || !ValidRotation(rotation) || spot == null || spot.Capacity != 1 ||
            spot.CustomerPoint == null || spot.CounterSurfacePoint == null ||
            !spot.CounterSurfacePoint.IsChildOf(spot.transform)) return false;
        Vector3 seat = PointAtPose(seatFrame, position, rotation), customer = spot.CustomerPoint.position;
        Vector3 approach = PointAtPose(approachFrame, position, rotation);
        Vector3 facing = rotation * transform.InverseTransformDirection(seatFrame.forward); facing.y = 0f;
        Vector3 customerFacing = spot.CustomerPoint.forward; customerFacing.y = 0f;
        return ValidateFrames(seat, approach, facing, customer, customerFacing, spot.CounterSurfacePoint.position, out error);
    }

    public bool ValidateSavedAssociation(BistroBuilderBarServiceSpot spot, Transform barRoot,
        Vector3 seatRootPosition, Quaternion seatRootRotation, Vector3 barRootPosition,
        Quaternion barRootRotation, out string error)
    {
        error = "The saved native bar/seat relationship has invalid authoring or pose.";
        if (!ValidateConfiguration(out error) || spot == null || spot.Capacity != 1 || barRoot == null ||
            spot.CustomerPoint == null || spot.CounterSurfacePoint == null ||
            !spot.transform.IsChildOf(barRoot) || !spot.CounterSurfacePoint.IsChildOf(spot.transform) ||
            !Finite(seatRootPosition) || !Finite(barRootPosition) ||
            !ValidRotation(seatRootRotation) || !ValidRotation(barRootRotation)) return false;
        Vector3 seat = PointAtPose(seatFrame, seatRootPosition, seatRootRotation);
        Vector3 approach = PointAtPose(approachFrame, seatRootPosition, seatRootRotation);
        Vector3 facing = seatRootRotation * transform.InverseTransformDirection(seatFrame.forward);
        Vector3 customer = barRootPosition + barRootRotation * barRoot.InverseTransformPoint(spot.CustomerPoint.position);
        Vector3 customerFacing = barRootRotation * barRoot.InverseTransformDirection(spot.CustomerPoint.forward);
        Vector3 surface = barRootPosition + barRootRotation * barRoot.InverseTransformPoint(spot.CounterSurfacePoint.position);
        return ValidateFrames(seat, approach, facing, customer, customerFacing, surface, out error);
    }

    private bool ValidateFrames(Vector3 seat, Vector3 approach, Vector3 facing,
        Vector3 customer, Vector3 customerFacing, Vector3 surface, out string error)
    {
        error = "The native bar/seat height, facing, floor approach or alignment is incompatible.";
        Vector3 delta = seat - customer; delta.y = 0f;
        float gap = surface.y - seat.y;
        facing.y = 0f; customerFacing.y = 0f;
        Vector3 approachDelta = approach - seat; approachDelta.y = 0f;
        if (!Finite(surface) || !Finite(customer) || delta.magnitude > positionTolerance ||
            Mathf.Abs(approach.y - customer.y) > 0.01f ||
            gap < minimumCounterGap - 0.0001f || gap > maximumCounterGap + 0.0001f ||
            facing.sqrMagnitude < 0.9f || customerFacing.sqrMagnitude < 0.9f ||
            Vector3.Angle(facing, customerFacing) > maximumFacingAngle ||
            Vector3.Dot(approachDelta, facing.normalized) >= -approachRadius) return false;
        error = string.Empty; return true;
    }

    public bool TryResolveSpotAtPose(Vector3 position, Quaternion rotation,
        out BistroBuilderBarServiceSpot spot, out string error)
    {
        Resolve(); spot = null;
        error = "Place this stool at one free, compatible native bar place with an authored counter surface.";
        if (bars == null || spatial == null || !ValidateConfiguration(out error)) return false;
        foreach (BistroBuilderBarServiceSpot candidate in bars.RegisteredSpots)
        {
            if (candidate == null || !HasConfirmedBarSubject(candidate) ||
                (requirePersistedBar && !HasPersistedBar(candidate)) ||
                (candidate.AttachedSeat != null && candidate.AttachedSeat != this) ||
                (candidate != attachedSpot && (!candidate.IsFree ||
                    candidate.GetComponent<BistroBuilderBarSpatialAdapter>()?.HasCustomerLease == true)) ||
                !ValidateForSpotAtPose(candidate, position, rotation, out _)) continue;
            if (spot != null)
            { spot = null; error = "The proposed stool pose matches multiple bar places; the association is ambiguous."; return false; }
            spot = candidate;
        }
        if (spot == null)
        { error = "No unique free native bar place matches the proposed seat height, position and facing."; return false; }
        if (attachedSpot != null && spot != attachedSpot && !CanDeactivate(out error))
        { spot = null; return false; }
        error = string.Empty; return true;
    }

    public void RequirePersistedBarRuntime(bool required) => requirePersistedBar = required;
    private bool HasPersistedBar(BistroBuilderBarServiceSpot spot)
    {
        var bar = spot != null ? spot.GetComponentInParent<BistroBuilderBarPlaceableBinding>() : null;
        var root = bar != null ? bar.GetComponent<RestaurantPlaceableObject>() : null;
        if (root == null || !root.HasInstanceId || placeables == null || !placeables.ContainsPlaceable(root)) return false;
        foreach (var owned in bar.Spots) if (owned == spot) return true;
        return false;
    }

    public bool TryCompleteActivation(out string error)
    {
        Resolve();
        error = "The stool's native activation requires confirmed placeable/BBSIS dependencies.";
        if (!IsSpatialLifecycleActive || spatial == null || !ValidateConfiguration(out error)) return false;
        if (attachedSpot != null && ValidateRuntimeAssociation(out error)) return true;
        BistroBuilderBarServiceSpot target = null;
        if (automaticAssociation && !TryResolveSpotAtPose(transform.position, transform.rotation, out target, out error)) return false;
        string identity = BuildSpatialId(placeable.InstanceId);
        if (spatial.TryGetSubject(identity, out var existing) && existing != subject)
        { error = "Another stool owns this persisted spatial identity."; return false; }
        subject.Configure(identity, subject.Contract, subject.Proxy);
        if (!spatial.RegisterSubject(subject))
        { error = "The confirmed stool body could not register in BBSIS."; return false; }
        if (automaticAssociation)
        {
            if (assessment == null)
            { error = "The stool needs the canonical BBSIS placement assessment."; return false; }
            var result = assessment.EvaluateCandidate(subject, transform.position, transform.rotation,
                GetComponent<RestaurantAreaMember>()?.AssignedArea,
                FindFirstObjectByType<RestaurantPlacementObstacleRegistry>());
            if (!result.IsValid) { error = result.TechnicalMessage; return false; }
            if (!TryAttach(target, out error)) return false;
        }
        assessment?.RegisterProvider(this);
        error = string.Empty; return true;
    }

    public void RollbackActivation()
    {
        Resolve();
        if (!TryDetach(out _)) return;
        assessment?.UnregisterProvider(this);
        if (subject != null) spatial?.UnregisterSubject(subject);
    }

    public int WriteSemanticVolumes(List<BistroBuilderSpatialSemanticVolume> results)
        => WriteCandidateSemanticVolumes(transform.position, transform.rotation, results);

    public int WriteCandidateSemanticVolumes(Vector3 position, Quaternion rotation,
        List<BistroBuilderSpatialSemanticVolume> results)
    {
        if (results == null || !ValidateConfiguration(out _) || !Finite(position) || !ValidRotation(rotation)) return 0;
        string relatedSubject = string.Empty, relatedSemantic = string.Empty;
        if (TryResolveSpotAtPose(position, rotation, out var target, out _))
            GetCustomerSemanticIdentity(target, out relatedSubject, out relatedSemantic);
        results.Add(new BistroBuilderSpatialSemanticVolume {
            subjectId = SpatialSubjectId, semanticId = "bar.seat.approach",
            relatedSemanticSubjectId = relatedSubject, relatedSemanticId = relatedSemantic,
            role = BistroBuilderSpatialSemanticRole.Approach, critical = true,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Circle(PointAtPose(approachFrame, position, rotation), approachRadius) });
        // This relationship marker authorizes only the compatible customer's
        // seat bay to coexist with the stool body. Work/transfer ports stay protected.
        results.Add(new BistroBuilderSpatialSemanticVolume {
            subjectId = SpatialSubjectId, semanticId = "bar.seat",
            relatedSemanticSubjectId = relatedSubject, relatedSemanticId = relatedSemantic,
            role = BistroBuilderSpatialSemanticRole.SeatBay, critical = false,
            conflictMode = BistroBuilderSpatialConflictMode.Reservable,
            volume = BistroBuilderSpatialVolume.Circle(PointAtPose(seatFrame, position, rotation), 0.18f) });
        return 2;
    }

    private static void GetCustomerSemanticIdentity(BistroBuilderBarServiceSpot spot,
        out string relatedSubject, out string relatedSemantic)
    {
        var barBody = spot.GetComponentInParent<BistroBuilderBarBodySpatialAdapter>();
        var barBinding = spot.GetComponentInParent<BistroBuilderBarPlaceableBinding>();
        if (barBody != null && barBinding != null)
            for (int index = 0; index < barBinding.Spots.Count; index++)
                if (barBinding.Spots[index] == spot)
                {
                    relatedSubject = barBody.SpatialSubjectId;
                    relatedSemantic = "slot_" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "." +
                        BistroBuilderBarSpatialAdapter.CustomerPortId;
                    return;
                }
        relatedSubject = spot.GetComponent<BistroBuilderBarSpatialAdapter>()?.SpatialSubjectId ?? string.Empty;
        relatedSemantic = BistroBuilderBarSpatialAdapter.CustomerPortId;
    }

    private Vector3 PointAtPose(Transform frame, Vector3 position, Quaternion rotation)
        => position + rotation * transform.InverseTransformPoint(frame.position);
    private static bool ValidRotation(Quaternion rotation)
    {
        if (!Finite(new Vector3(rotation.x, rotation.y, rotation.z)) || float.IsNaN(rotation.w) || float.IsInfinity(rotation.w)) return false;
        float magnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        return Mathf.Abs(magnitude - 1f) < 0.001f && Vector3.Angle(rotation * Vector3.up, Vector3.up) < 0.1f;
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (placeables != null) placeables.PlaceableUnregistered += OnUnregistered;
    }
    private void Unsubscribe()
    { if (placeables != null) placeables.PlaceableUnregistered -= OnUnregistered; }
    private void OnUnregistered(RestaurantPlaceableObject candidate)
    { if (candidate == placeable) RollbackActivation(); }

    public bool ValidateRuntimeAssociation(out string error)
    {
        Resolve();
        error = "The native seat association is not confirmed in the placeable, bar and BBSIS registries.";
        if (!IsSpatialLifecycleActive || SpatialSubjectId != BuildSpatialId(placeable.InstanceId) ||
            attachedSpot == null || attachedSpot.AttachedSeat != this ||
            bars == null || !bars.TryGetSpot(attachedSpot.BarSpotId, out BistroBuilderBarServiceSpot actualSpot) || actualSpot != attachedSpot ||
            spatial == null || !spatial.TryGetSubject(SpatialSubjectId, out BistroBuilderSpatialSubject actualSubject) || actualSubject != subject ||
            !HasConfirmedBarSubject(attachedSpot))
            return false;
        return ValidateForSpot(attachedSpot, out error);
    }

    public bool TryAttach(BistroBuilderBarServiceSpot spot, out string error)
    {
        Resolve();
        if (attachedSpot == spot && spot != null && spot.AttachedSeat == this) return ValidateRuntimeAssociation(out error);
        error = "Only a confirmed stool and a free registered native bar spot may be associated.";
        if (attachedSpot != null || !IsSpatialLifecycleActive || SpatialSubjectId != BuildSpatialId(placeable.InstanceId) || spot == null || bars == null ||
            !bars.TryGetSpot(spot.BarSpotId, out BistroBuilderBarServiceSpot actual) || actual != spot ||
            spatial == null || !spatial.TryGetSubject(SpatialSubjectId, out BistroBuilderSpatialSubject actualSubject) || actualSubject != subject ||
            !HasConfirmedBarSubject(spot) || !ValidateForSpot(spot, out error) || !spot.TryAttachSeat(this, out error)) return false;
        attachedSpot = spot;
        error = string.Empty; return true;
    }

    public bool TryDetach(out string error)
    {
        error = string.Empty;
        if (attachedSpot == null) return true;
        if (!attachedSpot.TryDetachSeat(this, out error)) return false;
        attachedSpot = null; return true;
    }

    public bool CanActivate(out string error) => ValidateConfiguration(out error);
    public bool CanDeactivate(out string error)
    {
        error = "The stool belongs to an occupied or spatially reserved native bar spot.";
        if (attachedSpot != null && (!attachedSpot.IsFree || attachedSpot.GetComponent<BistroBuilderBarSpatialAdapter>()?.HasCustomerLease == true))
            return false;
        error = string.Empty; return true;
    }
    private void Resolve()
    {
        if (placeable == null) placeable = GetComponent<RestaurantPlaceableObject>();
        if (subject == null) subject = GetComponent<BistroBuilderSpatialSubject>();
        if (placeables == null) placeables = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        if (bars == null) bars = FindFirstObjectByType<BistroBuilderBarServiceRegistry>();
        if (spatial == null) spatial = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        if (assessment == null) assessment = FindFirstObjectByType<BistroBuilderSpatialPlacementAssessmentService>();
    }
    private bool HasConfirmedBarSubject(BistroBuilderBarServiceSpot spot)
    {
        BistroBuilderBarSpatialAdapter adapter = spot != null ? spot.GetComponent<BistroBuilderBarSpatialAdapter>() : null;
        return adapter != null && adapter.Subject != null && adapter.Subject.IsRegistrationEligible &&
            spatial != null && spatial.TryGetSubject(adapter.SpatialSubjectId, out BistroBuilderSpatialSubject actual) && actual == adapter.Subject;
    }
    private static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
#if UNITY_EDITOR
    public void ConfigureForEditor(Transform seat, Transform approach, RestaurantPlaceableRegistry registry,
        BistroBuilderBarServiceRegistry barRegistry, BistroBuilderSpatialInteractionService spatialService)
    {
        Unsubscribe(); placeables = registry; bars = barRegistry; spatial = spatialService;
        automaticAssociation = false;
        ConfigureRuntime(seat, approach, 0.2f, 0.4f, 0.08f, 10f, 0.32f);
        Subscribe();
    }
    public void EnableAutomaticAssociationForEditor(BistroBuilderSpatialPlacementAssessmentService service)
    { automaticAssociation = true; assessment = service; }
    public void ConfigureDependenciesForEditor(RestaurantPlaceableRegistry registry,
        BistroBuilderBarServiceRegistry barRegistry, BistroBuilderSpatialInteractionService spatialService,
        BistroBuilderSpatialPlacementAssessmentService placementAssessment)
    {
        Unsubscribe(); placeables = registry; bars = barRegistry; spatial = spatialService; assessment = placementAssessment;
        Resolve(); Subscribe();
    }
#endif
}
