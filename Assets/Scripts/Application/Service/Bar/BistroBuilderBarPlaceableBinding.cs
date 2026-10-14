using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adapta plazas de una barra colocable al registro y BBSIS existentes.
/// La identidad funcional procede de la instancia persistida, nunca del nombre del prefab.
/// Las instancias provisionales no se registran como destinos de servicio.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantPlaceableObject))]
public sealed class BistroBuilderBarPlaceableBinding : MonoBehaviour, IRestaurantPlaceableLifecycleGuard,
    IRestaurantPlaceableActivationParticipant
{
    [SerializeField] private BistroBuilderBarServiceSpot[] spots = Array.Empty<BistroBuilderBarServiceSpot>();
    [SerializeField] private BistroBuilderSpatialContractDefinition spatialContract;
    private RestaurantPlaceableRegistry placeableRegistry;
    private BistroBuilderBarServiceRegistry barRegistry;
    private BistroBuilderSpatialInteractionService spatialService;
    private BistroBuilderSpatialPlacementAssessmentService placementAssessment;

    private RestaurantPlaceableObject placeable;
    private readonly List<BistroBuilderBarServiceSpot> registered = new List<BistroBuilderBarServiceSpot>();
    private string boundInstanceId = string.Empty;
    public IReadOnlyList<BistroBuilderBarServiceSpot> Spots => spots;
    public bool IsRuntimeRegistered => spots != null && registered.Count == spots.Length && registered.Count > 0;

    private void OnEnable() { ResolveDependencies(); Subscribe(); }
    private void Start() { ResolveDependencies(); Subscribe(); RegisterIfActivated(); }
    private void OnDisable() { Unsubscribe(); ReleaseRuntimeRegistration(); }

    public bool ValidateConfiguration(out string error)
    {
        error = "La barra colocable necesita plazas y un contrato espacial canónico válido.";
        if (spots == null || spots.Length == 0 || spatialContract == null ||
            spatialContract.FamilyId != "work.bar" || !spatialContract.ValidateDefinition(out _)) return false;
        HashSet<string> ports = new HashSet<string>(StringComparer.Ordinal);
        foreach (BistroBuilderSpatialPortDefinition port in spatialContract.Ports) ports.Add(port.portId);
        if (!ports.Contains(BistroBuilderBarSpatialAdapter.CustomerPortId) ||
            !ports.Contains(BistroBuilderBarSpatialAdapter.ServicePortId) ||
            !ports.Contains(BistroBuilderBarSpatialAdapter.TransferPortId)) return false;
        HashSet<BistroBuilderBarServiceSpot> unique = new HashSet<BistroBuilderBarServiceSpot>();
        foreach (BistroBuilderBarServiceSpot spot in spots)
        {
            if (spot == null || !unique.Add(spot) || !spot.transform.IsChildOf(transform) ||
                !spot.ValidateConfiguration(out error) || !spot.CustomerPoint.IsChildOf(transform) ||
                !spot.WaiterServicePoint.IsChildOf(transform) ||
                !Finite(spot.CustomerPoint.localPosition) || !Finite(spot.WaiterServicePoint.localPosition) ||
                Vector3.Distance(spot.CustomerPoint.position, spot.WaiterServicePoint.position) < 0.3f)
            { error = "La barra contiene plazas duplicadas o puntos operativos inválidos."; return false; }
        }
        error = string.Empty;
        return true;
    }

    public bool CanActivate(out string rejectionMessage)
    {
        ResolveDependencies();
        if (!ValidateConfiguration(out rejectionMessage)) return false;
        if (placeable == null || placeableRegistry == null || barRegistry == null || spatialService == null || placementAssessment == null)
        { rejectionMessage = "No están disponibles los registros de colocables/barra y los servicios BBSIS."; return false; }
        if (placeable.HasInstanceId)
        {
            for (int index = 0; index < spots.Length; index++)
                if (barRegistry.TryGetSpot(BuildSpotId(placeable.InstanceId, index), out BistroBuilderBarServiceSpot existing) &&
                    existing != spots[index])
                { rejectionMessage = "La identidad de plaza de esta barra pertenece a otra instancia."; return false; }
            for (int index = 0; index < spots.Length; index++)
                if (spatialService.TryGetSubject("spatial." + BuildSpotId(placeable.InstanceId, index), out BistroBuilderSpatialSubject existingSubject) &&
                    existingSubject != spots[index].GetComponent<BistroBuilderSpatialSubject>())
                { rejectionMessage = "La identidad espacial de plaza pertenece a otra instancia."; return false; }
        }
        BistroBuilderBarBodySpatialAdapter body = GetComponent<BistroBuilderBarBodySpatialAdapter>();
        if (body != null && !body.CanActivate(out rejectionMessage)) return false;
        rejectionMessage = string.Empty;
        return true;
    }

    public bool CanDeactivate(out string rejectionMessage)
    {
        foreach (BistroBuilderBarServiceSpot spot in spots ?? Array.Empty<BistroBuilderBarServiceSpot>())
            if (spot != null && spot.AttachedSeat != null)
            { rejectionMessage = "Retira primero los taburetes asociados a las plazas de esta barra."; return false; }
        foreach (BistroBuilderBarServiceSpot spot in spots ?? Array.Empty<BistroBuilderBarServiceSpot>())
            if (spot != null && (!spot.IsFree || spot.GetComponent<BistroBuilderBarSpatialAdapter>()?.HasCustomerLease == true))
            { rejectionMessage = "La barra tiene clientes o una reserva espacial activa."; return false; }
        rejectionMessage = string.Empty;
        return true;
    }

    public bool TryRegisterRuntime(out string error)
    {
        if (!CanActivate(out error)) return false;
        if (!placeableRegistry.ContainsPlaceable(placeable) || !placeable.HasInstanceId || !isActiveAndEnabled)
        { error = "Una barra provisional o inactiva no puede registrarse para servicio."; return false; }
        if (boundInstanceId != placeable.InstanceId && registered.Count > 0)
        {
            if (!CanDeactivate(out error)) return false;
            ReleaseRuntimeRegistration();
        }

        List<BistroBuilderBarServiceSpot> added = new List<BistroBuilderBarServiceSpot>();
        BistroBuilderBarBodySpatialAdapter body = GetComponent<BistroBuilderBarBodySpatialAdapter>();
        if (body != null && !body.TryRegisterRoot(out error)) return false;
        for (int index = 0; index < spots.Length; index++)
        {
            BistroBuilderBarServiceSpot spot = spots[index];
            string id = BuildSpotId(placeable.InstanceId, index);
            if (body != null) body.PrepareSpot(spot);
            bool configured = spot.TryConfigure(id, spot.CustomerPoint, spot.WaiterServicePoint, spot.Capacity, spot.AllowsStandingService);
            bool bound = configured && BistroBuilderOperationalSpatialBindingUtility.BindBarSpot(spot, spatialContract, "spatial." + id);
            BistroBuilderBarSpatialAdapter adapter = spot.GetComponent<BistroBuilderBarSpatialAdapter>();
            if (!bound || adapter == null || !adapter.ValidateConfiguration(out error))
            { UnregisterSpatial(spot); Rollback(added); error = "No se pudo vincular la plaza de barra con su contrato BBSIS."; return false; }
            adapter.Configure(spot, adapter.Subject, spatialService);
            if (!spatialService.RegisterSubject(adapter.Subject))
            { UnregisterSpatial(spot); Rollback(added); error = "La identidad espacial de la plaza pertenece a otra instancia."; return false; }
            bool alreadyRegistered = barRegistry.TryGetSpot(id, out BistroBuilderBarServiceSpot current) && current == spot;
            if (!alreadyRegistered && !barRegistry.TryRegisterSpot(spot, out error))
            { UnregisterSpatial(spot); Rollback(added); return false; }
            if (!alreadyRegistered) added.Add(spot);
            if (!registered.Contains(spot)) registered.Add(spot);
            if (body == null) placementAssessment.RegisterProviders(spot.gameObject);
        }
        boundInstanceId = placeable.InstanceId;
        error = string.Empty;
        return true;
    }

    public bool TryCompleteActivation(out string error) => TryRegisterRuntime(out error);
    public void RollbackActivation() => ReleaseRuntimeRegistration();

    public void ReleaseRuntimeRegistration()
    {
        foreach (BistroBuilderBarServiceSpot spot in registered)
            if (spot != null) { barRegistry?.UnregisterSpot(spot); UnregisterSpatial(spot); }
        registered.Clear();
        boundInstanceId = string.Empty;
        GetComponent<BistroBuilderBarBodySpatialAdapter>()?.ReleaseRoot();
    }

    public static string BuildSpotId(string instanceId, int index) =>
        "bar.placeable." + instanceId + ".slot_" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);

    private void Rollback(List<BistroBuilderBarServiceSpot> added)
    {
        foreach (BistroBuilderBarServiceSpot spot in added)
        { barRegistry.UnregisterSpot(spot); registered.Remove(spot); UnregisterSpatial(spot); }
        if (registered.Count == 0) GetComponent<BistroBuilderBarBodySpatialAdapter>()?.ReleaseRoot();
    }
    private void UnregisterSpatial(BistroBuilderBarServiceSpot spot)
    {
        BistroBuilderBarSpatialAdapter adapter = spot.GetComponent<BistroBuilderBarSpatialAdapter>();
        if (adapter != null) { adapter.ReleaseCustomerLease(); placementAssessment?.UnregisterProvider(adapter); }
        BistroBuilderSpatialSubject subject = spot.GetComponent<BistroBuilderSpatialSubject>();
        if (subject != null)
        {
            spatialService?.UnregisterSubject(subject);
            subject.Configure(string.Empty, subject.Contract, subject.Proxy);
        }
    }
    private void RegisterIfActivated()
    {
        if (isActiveAndEnabled && placeableRegistry != null && placeableRegistry.ContainsPlaceable(placeable) && !TryRegisterRuntime(out string error))
            Debug.LogError(error, this);
    }
    private void OnRegistered(RestaurantPlaceableObject candidate) { if (candidate == placeable) RegisterIfActivated(); }
    private void OnUnregistered(RestaurantPlaceableObject candidate) { if (candidate == placeable) ReleaseRuntimeRegistration(); }
    private void OnIdentityChanged(RestaurantPlaceableObject candidate, string previous, string next) => RegisterIfActivated();
    private void Subscribe()
    {
        Unsubscribe();
        if (placeableRegistry != null)
        { placeableRegistry.PlaceableRegistered += OnRegistered; placeableRegistry.PlaceableUnregistered += OnUnregistered; }
        if (placeable != null) placeable.InstanceIdChanged += OnIdentityChanged;
    }
    private void Unsubscribe()
    {
        if (placeableRegistry != null)
        { placeableRegistry.PlaceableRegistered -= OnRegistered; placeableRegistry.PlaceableUnregistered -= OnUnregistered; }
        if (placeable != null) placeable.InstanceIdChanged -= OnIdentityChanged;
    }
    private void ResolveDependencies()
    {
        if (placeable == null) placeable = GetComponent<RestaurantPlaceableObject>();
        if (placeableRegistry == null) placeableRegistry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        if (barRegistry == null) barRegistry = FindFirstObjectByType<BistroBuilderBarServiceRegistry>();
        if (spatialService == null) spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        if (placementAssessment == null) placementAssessment = FindFirstObjectByType<BistroBuilderSpatialPlacementAssessmentService>();
    }
    private static bool Finite(Vector3 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
        !float.IsNaN(point.y) && !float.IsInfinity(point.y) && !float.IsNaN(point.z) && !float.IsInfinity(point.z);

#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderBarServiceSpot[] authoredSpots, BistroBuilderSpatialContractDefinition contract,
        RestaurantPlaceableRegistry placeables = null, BistroBuilderBarServiceRegistry bars = null,
        BistroBuilderSpatialInteractionService spatial = null, BistroBuilderSpatialPlacementAssessmentService assessment = null)
    {
        Unsubscribe();
        spots = authoredSpots ?? Array.Empty<BistroBuilderBarServiceSpot>(); spatialContract = contract;
        placeableRegistry = placeables; barRegistry = bars; spatialService = spatial; placementAssessment = assessment;
        ResolveDependencies(); Subscribe();
    }
#endif
}
