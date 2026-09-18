using UnityEngine;

/// <summary>
/// Mantiene la topología de circulación sincronizada con el Modo Edición.
/// Reacciona a confirmaciones, altas y bajas; nunca reconstruye cada frame.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderNavigationEditIntegration : MonoBehaviour
{
    [SerializeField] private BistroBuilderNavigationService navigationService;
    [SerializeField] private RestaurantPlacementTransactionService placementTransactions;
    [SerializeField] private RestaurantPlaceableRegistry placeableRegistry;
    [SerializeField, Min(0.02f)] private float rebuildDelaySeconds = 0.08f;

    private bool rebuildPending;
    private float rebuildAt;
    private BistroBuilderSaveGameService saveGameService;

    public int AutomaticTopologyRebuildCount { get; private set; }

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        rebuildPending = false;
    }

    private void Update()
    {
        if (!rebuildPending || Time.unscaledTime < rebuildAt) return;

        // Durante Load pueden llegar altas/bajas parciales de placeables.
        // Esperar evita reconstruir una topología intermedia que será sustituida
        // unos frames después por el estado completo restaurado.
        if (saveGameService != null &&
            saveGameService.IsBusy &&
            saveGameService.ActiveOperation == BistroBuilderSaveOperationKind.Load)
            return;

        RebuildNow();
    }

    public void RequestRebuild()
    {
        rebuildPending = true;
        rebuildAt = Time.unscaledTime + rebuildDelaySeconds;
    }

    public void RebuildNow()
    {
        rebuildPending = false;
        if (navigationService == null)
            CacheDependencies();
        if (navigationService == null) return;
        navigationService.RebuildNavigationTopology();
        AutomaticTopologyRebuildCount++;
    }

    private void HandlePlacementCommitted(
        RestaurantAreaMember member,
        RestaurantPlacementValidationResult result)
    {
        RequestRebuild();
    }

    private void HandlePlaceableChanged(RestaurantPlaceableObject placeable)
    {
        if (placeable != null && placeable.TryGetComponent(out RestaurantSeat seat) &&
            seat.GetComponent<BistroBuilderSeatCirculationEnvelope>() == null)
        {
            seat.gameObject.AddComponent<BistroBuilderSeatCirculationEnvelope>();
        }
        RequestRebuild();
    }

    private void HandleSaveCompleted(BistroBuilderSaveOperationResult result)
    {
        if (result != null &&
            result.OperationKind == BistroBuilderSaveOperationKind.Load &&
            rebuildPending)
        {
            RebuildNow();
        }
    }

    private void Subscribe()
    {
        if (saveGameService != null)
        {
            saveGameService.OperationCompleted -= HandleSaveCompleted;
            saveGameService.OperationCompleted += HandleSaveCompleted;
        }
        if (placementTransactions != null)
        {
            placementTransactions.PlacementCommitted -= HandlePlacementCommitted;
            placementTransactions.PlacementCommitted += HandlePlacementCommitted;
        }
        if (placeableRegistry != null)
        {
            placeableRegistry.PlaceableRegistered -= HandlePlaceableChanged;
            placeableRegistry.PlaceableRegistered += HandlePlaceableChanged;
            placeableRegistry.PlaceableUnregistered -= HandlePlaceableChanged;
            placeableRegistry.PlaceableUnregistered += HandlePlaceableChanged;
        }
    }

    private void Unsubscribe()
    {
        if (saveGameService != null)
            saveGameService.OperationCompleted -= HandleSaveCompleted;
        if (placementTransactions != null)
            placementTransactions.PlacementCommitted -= HandlePlacementCommitted;
        if (placeableRegistry != null)
        {
            placeableRegistry.PlaceableRegistered -= HandlePlaceableChanged;
            placeableRegistry.PlaceableUnregistered -= HandlePlaceableChanged;
        }
    }

    private void CacheDependencies()
    {
        if (saveGameService == null)
            saveGameService = FindFirstObjectByType<BistroBuilderSaveGameService>();
        if (navigationService == null)
            navigationService = FindFirstObjectByType<BistroBuilderNavigationService>();
        if (placementTransactions == null)
            placementTransactions = FindFirstObjectByType<RestaurantPlacementTransactionService>();
        if (placeableRegistry == null)
            placeableRegistry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
    }
}
