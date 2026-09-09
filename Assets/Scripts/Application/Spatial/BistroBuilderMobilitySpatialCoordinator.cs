using System;
using UnityEngine;

/// <summary>
/// Integra objetos logisticos moviles con BBSIS.
/// Consume eventos de logistica, sin decidir rutas, tareas ni movimiento.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderMobilitySpatialCoordinator :
    MonoBehaviour
{
    [SerializeField] private BistroBuilderSpatialInteractionService spatialService;
    [SerializeField] private BistroBuilderSupplierDeliveryPresentationService presentationService;
    [SerializeField] private BistroBuilderSpatialContractDefinition cartContract;
    [SerializeField] private BistroBuilderMobilitySpatialProfileDefinition cartProfile;
    [SerializeField, Min(0.05f)] private float reconciliationInterval = 0.1f;
    [SerializeField, Min(0.1f)] private float leaseDuration = 0.4f;

    private BistroBuilderSupplierDeliveryPresentationController boundController;
    private GameObject boundTrolley;
    private BistroBuilderMobileSpatialAdapter boundAdapter;
    private BistroBuilderSupplierDeliveryPresentationService subscribedPresentationService;
    private float nextReconciliationAt;

    public BistroBuilderMobileSpatialAdapter BoundAdapter => boundAdapter;
    public bool HasPresentationAuthority => presentationService != null;
    public int BoundObjectCount => boundAdapter != null ? 1 : 0;
    public int RebindCount { get; private set; }

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
        ReconcileNow();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReleaseBoundObject();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextReconciliationAt)
            return;
        nextReconciliationAt = Time.unscaledTime +
            Mathf.Max(0.05f, reconciliationInterval);
        ReconcileNow();
    }
    public void ConfigureForEditor(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderSupplierDeliveryPresentationService presentations,
        BistroBuilderSpatialContractDefinition contract,
        BistroBuilderMobilitySpatialProfileDefinition profile)
    {
        spatialService = spatial;
        presentationService = presentations;
        cartContract = contract;
        cartProfile = profile;
        ResolveDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        if (spatialService == null ||
            cartContract == null || cartProfile == null)
        {
            error = "Coordinador de movilidad BBSIS incompleto.";
            return false;
        }
        if (!cartContract.ValidateDefinition(out error) ||
            !cartContract.HasTrait("mobility.cart"))
            return false;
        return cartProfile.ValidateDefinition(out error);
    }

    public void ReconcileNow()
    {
        ResolveDependencies();
        Subscribe();
        if (presentationService == null ||
            !ValidateConfiguration(out _))
            return;

        BistroBuilderSupplierDeliveryPresentationController controller =
            presentationService.ActiveController;
        GameObject trolley = controller != null
            ? controller.TrolleyObject
            : null;
        if (controller == null || trolley == null)
        {
            ReleaseBoundObject();
            return;
        }

        if (!ReferenceEquals(controller, boundController) ||
            !ReferenceEquals(trolley, boundTrolley))
            Bind(controller, trolley);

        if (boundAdapter == null || !boundTrolley.activeInHierarchy)
            return;

        BistroBuilderSupplierDeliveryPresentationRecord record =
            controller.Record;
        boundAdapter.SetLoadUnits(ResolveLoadUnits(record));
        boundAdapter.TickSpatial(leaseDuration);
    }
    private void Bind(
        BistroBuilderSupplierDeliveryPresentationController controller,
        GameObject trolley)
    {
        ReleaseBoundObject();
        BistroBuilderSupplierDeliveryPresentationRecord record =
            controller.Record;
        string presentationId = record != null
            ? BistroBuilderOrderIdUtility.Normalize(record.presentationId)
            : string.Empty;
        if (string.IsNullOrWhiteSpace(presentationId))
            return;

        BistroBuilderAdaptiveSpatialProxy proxy =
            GetOrAdd<BistroBuilderAdaptiveSpatialProxy>(trolley);
        BistroBuilderSpatialSubject subject =
            GetOrAdd<BistroBuilderSpatialSubject>(trolley);
        subject.Configure(
            "spatial.logistics.cart." + presentationId,
            cartContract,
            proxy);
        BistroBuilderMobileSpatialAdapter adapter =
            GetOrAdd<BistroBuilderMobileSpatialAdapter>(trolley);
        adapter.Configure(
            subject,
            cartProfile,
            "bbsis.logistics.cart." + presentationId);

        boundController = controller;
        boundTrolley = trolley;
        boundAdapter = adapter;
        RebindCount++;
        if (trolley.activeInHierarchy)
            spatialService.RegisterSubject(subject);
    }

    private void ReleaseBoundObject()
    {
        if (boundAdapter != null)
            boundAdapter.ReleaseSpatialState(
                BistroBuilderSpatialEpisodeState.Cancelled);
        boundController = null;
        boundTrolley = null;
        boundAdapter = null;
    }

    private static int ResolveLoadUnits(
        BistroBuilderSupplierDeliveryPresentationRecord record)
    {
        if (record == null)
            return 0;
        bool loaded =
            record.state ==
                BistroBuilderSupplierDeliveryPresentationState.PreparingTrolley ||
            record.state ==
                BistroBuilderSupplierDeliveryPresentationState.GoingToWarehouse ||
            record.state ==
                BistroBuilderSupplierDeliveryPresentationState.Unloading;
        if (!loaded)
            return 0;
        int trips = Mathf.Max(1, record.totalTrips);
        return Mathf.Max(1, Mathf.CeilToInt(
            (float)Mathf.Max(1, record.visualLoadUnits) / trips));
    }
    private void Subscribe()
    {
        if (ReferenceEquals(
                subscribedPresentationService,
                presentationService))
            return;
        Unsubscribe();
        if (presentationService == null)
            return;
        subscribedPresentationService = presentationService;
        subscribedPresentationService.PresentationStarted += OnPresentationChanged;
        subscribedPresentationService.PresentationChanged += OnPresentationChanged;
        subscribedPresentationService.PresentationCompleted += OnPresentationChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedPresentationService == null)
            return;
        subscribedPresentationService.PresentationStarted -= OnPresentationChanged;
        subscribedPresentationService.PresentationChanged -= OnPresentationChanged;
        subscribedPresentationService.PresentationCompleted -= OnPresentationChanged;
        subscribedPresentationService = null;
    }

    private void OnPresentationChanged(
        BistroBuilderSupplierDeliveryPresentationRecord _)
    {
        ReconcileNow();
    }

    private void ResolveDependencies()
    {
        if (spatialService == null)
            spatialService = FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        if (presentationService == null)
            presentationService = FindFirstObjectByType<
                BistroBuilderSupplierDeliveryPresentationService>();
    }

    private static T GetOrAdd<T>(GameObject source)
        where T : Component
    {
        T component = source.GetComponent<T>();
        return component != null
            ? component
            : source.AddComponent<T>();
    }
}
