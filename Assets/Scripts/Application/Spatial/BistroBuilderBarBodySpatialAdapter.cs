using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Root body/ports of a compound placeable bar. Provisional ports can be assessed,
/// while scene geometry and scene semantics require the existing placeable registry.
/// Native child bar adapters retain service/reservation authority.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BistroBuilderSpatialSubject), typeof(BistroBuilderSpatialPhysicalFootprintAdapter))]
public sealed class BistroBuilderBarBodySpatialAdapter : MonoBehaviour,
    IBistroBuilderSpatialSemanticProvider, IBistroBuilderSpatialLifecycleOwner, IRestaurantPlaceableLifecycleGuard
{
    [SerializeField] private BistroBuilderBarPlaceableBinding binding;
    private RestaurantPlaceableRegistry placeableRegistry;
    private BistroBuilderSpatialInteractionService spatialService;
    private BistroBuilderSpatialPlacementAssessmentService placementAssessment;
    private RestaurantPlaceableObject placeable;
    private BistroBuilderSpatialSubject subject;
    private readonly List<BistroBuilderSpatialVolume> physicalVolumes = new List<BistroBuilderSpatialVolume>(32);
    private readonly List<RestaurantPlacementShape> checkedShapes = new List<RestaurantPlacementShape>(32);

    public string SpatialSubjectId => subject != null ? subject.SubjectId : string.Empty;
    public bool IsSpatialLifecycleActive
    {
        get
        {
            Resolve();
            return isActiveAndEnabled && placeableRegistry != null && placeable != null &&
                placeableRegistry.ContainsPlaceable(placeable);
        }
    }

    private void OnEnable() { Resolve(); Subscribe(); PrepareIdentity(); }
    private void OnDisable() { Unsubscribe(); ReleaseRoot(); }

    public static string BuildBodyId(string instanceId) => "spatial.bar.placeable." + instanceId + ".body";

    public bool ValidateConfiguration(out string error)
    {
        Resolve();
        error = "El cuerpo de barra requiere colocable, plazas propias, contrato y geometría BBSIS válidos.";
        if (placeable == null || binding == null || subject == null || subject.Contract == null ||
            subject.Proxy == null || subject.Proxy != GetComponent<BistroBuilderAdaptiveSpatialProxy>() ||
            !GetComponent<BistroBuilderSpatialPhysicalFootprintAdapter>().enabled ||
            subject.Contract.FamilyId != "work.bar" || !subject.Contract.ValidateDefinition(out _) ||
            !binding.ValidateConfiguration(out _) ||
            !BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(GetComponent<RestaurantPlacementFootprint>(),
                transform.position, transform.rotation, checkedShapes, out _)) return false;
        physicalVolumes.Clear();
        subject.Proxy.BuildWorldVolumes(BistroBuilderSpatialProxyLayer.Static, physicalVolumes);
        if (physicalVolumes.Count == 0) return false;
        foreach (BistroBuilderBarServiceSpot spot in binding.Spots)
        {
            if (!PortClear(spot.CustomerPoint.position, PortRadius(BistroBuilderBarSpatialAdapter.CustomerPortId)) ||
                !PortClear(spot.WaiterServicePoint.position, PortRadius(BistroBuilderBarSpatialAdapter.ServicePortId)))
            { error = "Un punto de cliente o camarero invade el cuerpo físico de la barra."; return false; }
        }
        error = string.Empty;
        return true;
    }

    public bool CanActivate(out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        if (placeableRegistry == null || spatialService == null || placementAssessment == null)
        { error = "Faltan las autoridades de colocables o BBSIS para activar la barra."; return false; }
        PrepareIdentity();
        if (placeable.HasInstanceId && spatialService.TryGetSubject(BuildBodyId(placeable.InstanceId), out BistroBuilderSpatialSubject other) && other != subject)
        { error = "La identidad espacial del cuerpo pertenece a otra barra."; return false; }
        error = string.Empty;
        return true;
    }

    public bool CanDeactivate(out string error)
    {
        Resolve();
        if (binding != null) return binding.CanDeactivate(out error);
        error = string.Empty;
        return true;
    }

    public bool TryRegisterRoot(out string error)
    {
        if (!CanActivate(out error)) return false;
        if (!IsSpatialLifecycleActive || !placeable.HasInstanceId)
        { error = "Un cuerpo de barra provisional no puede registrarse en BBSIS."; return false; }
        PrepareIdentity();
        if (!spatialService.RegisterSubject(subject))
        { error = "No se pudo registrar la identidad canónica del cuerpo de barra."; return false; }
        placementAssessment.RegisterProvider(this);
        error = string.Empty;
        return true;
    }

    public void ReleaseRoot()
    {
        if (subject == null) return;
        spatialService?.UnregisterSubject(subject);
        placementAssessment?.UnregisterProvider(this);
        subject.Configure(string.Empty, subject.Contract, subject.Proxy);
    }

    public void PrepareSpot(BistroBuilderBarServiceSpot spot)
    {
        BistroBuilderSpatialSubject child = spot.GetComponent<BistroBuilderSpatialSubject>();
        if (child == null) child = spot.gameObject.AddComponent<BistroBuilderSpatialSubject>();
        child.ConfigureLifecycleOwner(this);
    }

    public int WriteSemanticVolumes(List<BistroBuilderSpatialSemanticVolume> results)
    {
        if (results == null) throw new ArgumentNullException(nameof(results));
        Resolve();
        if (binding == null || subject == null || subject.Contract == null) return 0;
        int before = results.Count;
        for (int index = 0; index < binding.Spots.Count; index++)
        {
            BistroBuilderBarServiceSpot spot = binding.Spots[index];
            if (spot == null) continue;
            string relatedSeat = spot.AttachedSeat != null && spot.AttachedSeat.ValidateRuntimeAssociation(out _) ?
                spot.AttachedSeat.SpatialSubjectId : string.Empty;
            Add(results, index, BistroBuilderBarSpatialAdapter.CustomerPortId, spot.CustomerPoint.position,
                BistroBuilderSpatialSemanticRole.SeatBay, relatedSeat);
            Add(results, index, BistroBuilderBarSpatialAdapter.ServicePortId, spot.WaiterServicePoint.position,
                BistroBuilderSpatialSemanticRole.WorkZone);
            Add(results, index, BistroBuilderBarSpatialAdapter.TransferPortId, spot.WaiterServicePoint.position,
                BistroBuilderSpatialSemanticRole.TransferZone);
        }
        return results.Count - before;
    }

    private void Add(List<BistroBuilderSpatialSemanticVolume> results, int index, string portId, Vector3 position,
        BistroBuilderSpatialSemanticRole role, string relatedSubjectId = "")
    {
        foreach (BistroBuilderSpatialPortDefinition port in subject.Contract.Ports)
            if (port.portId == portId)
            {
                results.Add(new BistroBuilderSpatialSemanticVolume {
                    subjectId = subject.SubjectId, relatedSubjectId = relatedSubjectId,
                    semanticId = "slot_" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "." + portId,
                    role = role, layer = BistroBuilderSpatialProxyLayer.Operational, conflictMode = port.conflictMode,
                    volume = BistroBuilderSpatialVolume.Circle(position, Mathf.Max(0.18f, port.radius)), critical = true });
                return;
            }
    }
    private float PortRadius(string id)
    {
        foreach (BistroBuilderSpatialPortDefinition port in subject.Contract.Ports)
            if (port.portId == id) return Mathf.Max(0.18f, port.radius);
        return 0.3f;
    }
    private bool PortClear(Vector3 position, float radius)
    {
        if (!Finite(position)) return false;
        BistroBuilderSpatialVolume port = BistroBuilderSpatialVolume.Circle(position, radius);
        foreach (BistroBuilderSpatialVolume volume in physicalVolumes) if (volume.Overlaps(port)) return false;
        return true;
    }
    private void PrepareIdentity()
    {
        Resolve();
        if (subject == null) return;
        subject.ConfigureLifecycleOwner(this);
        if (placeable != null && placeable.HasInstanceId)
            subject.Configure(BuildBodyId(placeable.InstanceId), subject.Contract, subject.Proxy);
    }
    private void OnIdentityChanged(RestaurantPlaceableObject changed, string previous, string next)
    {
        if (!CanDeactivate(out _)) return;
        ReleaseRoot(); PrepareIdentity();
    }
    private void Subscribe()
    {
        Unsubscribe();
        if (placeable != null) placeable.InstanceIdChanged += OnIdentityChanged;
    }
    private void Unsubscribe()
    { if (placeable != null) placeable.InstanceIdChanged -= OnIdentityChanged; }
    private void Resolve()
    {
        if (placeable == null) placeable = GetComponent<RestaurantPlaceableObject>();
        if (binding == null) binding = GetComponent<BistroBuilderBarPlaceableBinding>();
        if (subject == null) subject = GetComponent<BistroBuilderSpatialSubject>();
        if (placeableRegistry == null) placeableRegistry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        if (spatialService == null) spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        if (placementAssessment == null) placementAssessment = FindFirstObjectByType<BistroBuilderSpatialPlacementAssessmentService>();
    }
    private static bool Finite(Vector3 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
        !float.IsNaN(point.y) && !float.IsInfinity(point.y) && !float.IsNaN(point.z) && !float.IsInfinity(point.z);
#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderBarPlaceableBinding barBinding,
        BistroBuilderSpatialContractDefinition contract, RestaurantPlaceableRegistry placeables = null,
        BistroBuilderSpatialInteractionService spatial = null, BistroBuilderSpatialPlacementAssessmentService assessment = null)
    {
        Unsubscribe(); binding = barBinding; placeableRegistry = placeables; spatialService = spatial; placementAssessment = assessment;
        Resolve();
        subject.ConfigureLifecycleOwner(this);
        subject.Configure(string.Empty, contract, GetComponent<BistroBuilderAdaptiveSpatialProxy>());
        foreach (BistroBuilderBarServiceSpot spot in binding.Spots) if (spot != null) PrepareSpot(spot);
        Subscribe(); PrepareIdentity();
    }
#endif
}
