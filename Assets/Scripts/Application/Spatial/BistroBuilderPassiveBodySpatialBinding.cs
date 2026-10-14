using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Static geometry of a confirmed passive placeable, using the existing
/// placeable and BBSIS authorities. It creates no service, capacity or gameplay.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantPlaceableObject), typeof(BistroBuilderSpatialSubject), typeof(BistroBuilderSpatialPhysicalFootprintAdapter))]
public sealed class BistroBuilderPassiveBodySpatialBinding : MonoBehaviour,
    IBistroBuilderSpatialLifecycleOwner, IRestaurantPlaceableActivationParticipant, IRestaurantPlaceableLifecycleGuard
{
    private RestaurantPlaceableRegistry placeables;
    private BistroBuilderSpatialInteractionService spatial;
    private BistroBuilderSpatialPlacementAssessmentService assessment;
    private RestaurantPlaceableObject placeable;
    private BistroBuilderSpatialSubject subject;
    private bool activationCommitted;
    private readonly List<RestaurantPlacementShape> shapes = new List<RestaurantPlacementShape>(4);
    private readonly List<BistroBuilderSpatialVolume> volumes = new List<BistroBuilderSpatialVolume>(4);

    public string SpatialSubjectId => subject != null ? subject.SubjectId : string.Empty;
    public bool IsSpatialLifecycleActive
    { get { Resolve(); return activationCommitted && isActiveAndEnabled && placeables != null && placeable != null && placeables.ContainsPlaceable(placeable); } }
    public static string BuildBodyId(string instanceId) => "spatial.passive.placeable." + instanceId + ".body";

    private void OnEnable()
    {
        Resolve(); Subscribe(); PrepareIdentity();
        if (placeables != null && placeable != null && placeables.ContainsPlaceable(placeable) && !TryCompleteActivation(out string error))
            Debug.LogError(error, this);
    }
    private void Start() { if (IsSpatialLifecycleActive) TryCompleteActivation(out _); }
    private void OnDisable() { Unsubscribe(); RollbackActivation(); }

    public bool ValidateConfiguration(out string error)
    {
        Resolve(); error = "Passive geometry requires a valid root-owned BBSIS body and floor placement envelope.";
        if (placeable == null || subject == null || subject.Contract == null || subject.Contract.FamilyId != "generic" ||
            !subject.Contract.ValidateDefinition(out _) || subject.Contract.Ports.Count != 0 ||
            subject.Contract.WorkEdges.Count != 0 || subject.Contract.Gates.Count != 0 ||
            subject.Proxy == null || subject.Proxy != GetComponent<BistroBuilderAdaptiveSpatialProxy>() ||
            !GetComponent<BistroBuilderSpatialPhysicalFootprintAdapter>().enabled ||
            !BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(GetComponent<RestaurantPlacementFootprint>(),
                transform.position, transform.rotation, shapes, out _)) return false;
        error = string.Empty; return true;
    }
    public bool CanActivate(out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        if (placeables == null || spatial == null || assessment == null)
        { error = "Passive geometry requires the placeable registry and native spatial services."; return false; }
        string id = placeable.HasInstanceId ? BuildBodyId(placeable.InstanceId) : string.Empty;
        if (!string.IsNullOrEmpty(id) && spatial.TryGetSubject(id, out var other) && other != subject)
        { error = "The passive body identity belongs to another instance."; return false; }
        volumes.Clear(); subject.Proxy.BuildWorldVolumes(BistroBuilderSpatialProxyLayer.Static, volumes);
        foreach (var body in volumes)
            if (spatial.TryFindStaticGeometryConflict(body, id, "", out _) ||
                spatial.TryFindBlockingLease(body, out _))
            { error = "The passive body intersects registered geometry or an active spatial claim."; return false; }
        var preflight = assessment.EvaluateCandidate(subject, transform.position, transform.rotation,
            GetComponent<RestaurantAreaMember>()?.AssignedArea, null);
        if (!preflight.IsValid) { error = preflight.TechnicalMessage; return false; }
        error = string.Empty; return true;
    }
    public bool CanDeactivate(out string error)
    {
        Resolve(); volumes.Clear(); subject?.Proxy?.BuildWorldVolumes(BistroBuilderSpatialProxyLayer.Static, volumes);
        foreach (var body in volumes)
            if (spatial != null && spatial.TryFindBlockingLease(body, out _))
            { error = "The passive body still intersects an active spatial claim."; return false; }
        error = string.Empty; return true;
    }
    public bool TryCompleteActivation(out string error)
    {
        Resolve();
        if (!isActiveAndEnabled || placeables == null || placeable == null || !placeables.ContainsPlaceable(placeable) || !placeable.HasInstanceId)
        { error = "A provisional passive body cannot register."; return false; }
        if (!CanActivate(out error)) return false;
        activationCommitted = true; PrepareIdentity();
        if (!spatial.RegisterSubject(subject)) { RollbackActivation(); error = "Passive body registration failed."; return false; }
        error = string.Empty; return true;
    }
    public void RollbackActivation()
    {
        activationCommitted = false;
        if (subject == null) return;
        spatial?.UnregisterSubject(subject);
        subject.Configure(string.Empty, subject.Contract, subject.Proxy);
    }
    private void PrepareIdentity()
    {
        if (subject == null) return;
        subject.ConfigureLifecycleOwner(this);
        if (placeable != null && placeable.HasInstanceId)
            subject.Configure(BuildBodyId(placeable.InstanceId), subject.Contract, subject.Proxy);
    }
    private void OnUnregistered(RestaurantPlaceableObject changed) { if (changed == placeable) RollbackActivation(); }
    private void OnIdentityChanged(RestaurantPlaceableObject changed, string previous, string next)
    { RollbackActivation(); PrepareIdentity(); }
    private void Subscribe()
    {
        Unsubscribe();
        if (placeable != null) placeable.InstanceIdChanged += OnIdentityChanged;
        if (placeables != null) placeables.PlaceableUnregistered += OnUnregistered;
    }
    private void Unsubscribe()
    {
        if (placeable != null) placeable.InstanceIdChanged -= OnIdentityChanged;
        if (placeables != null) placeables.PlaceableUnregistered -= OnUnregistered;
    }
    private void Resolve()
    {
        if (placeable == null) placeable = GetComponent<RestaurantPlaceableObject>();
        if (subject == null) subject = GetComponent<BistroBuilderSpatialSubject>();
        if (placeables == null) placeables = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        if (spatial == null) spatial = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        if (assessment == null) assessment = FindFirstObjectByType<BistroBuilderSpatialPlacementAssessmentService>();
    }
#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderSpatialContractDefinition contract,
        RestaurantPlaceableRegistry registry = null, BistroBuilderSpatialInteractionService service = null,
        BistroBuilderSpatialPlacementAssessmentService preflight = null)
    {
        Unsubscribe(); placeables = registry; spatial = service; assessment = preflight; Resolve();
        subject.ConfigureLifecycleOwner(this);
        subject.Configure(string.Empty, contract, GetComponent<BistroBuilderAdaptiveSpatialProxy>());
        Subscribe(); PrepareIdentity();
    }
#endif
}
