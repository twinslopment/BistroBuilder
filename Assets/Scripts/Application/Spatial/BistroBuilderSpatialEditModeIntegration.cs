using System;
using UnityEngine;

/// <summary>
/// Sincroniza BBSIS con el ciclo de vida del Modo Edición.
/// No coloca objetos, no ejecuta Undo/Redo y no reconstruye rutas.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSpatialEditModeIntegration :
    MonoBehaviour
{
    [SerializeField]
    private BistroBuilderSpatialInteractionService spatialService;
    [SerializeField]
    private BistroBuilderSpatialAssessmentService layoutAssessment;
    [SerializeField]
    private BistroBuilderSpatialPlacementAssessmentService placementAssessment;
    [SerializeField]
    private BistroBuilderSpatialRuntimeBinder runtimeBinder;
    [SerializeField]
    private RestaurantPlacementTransactionService transactions;
    [SerializeField]
    private RestaurantPlacementHistoryService history;
    [SerializeField]
    private RestaurantPlaceableRegistry placeableRegistry;
    [SerializeField]
    private RestaurantPlaceableLifecycleService lifecycle;
    [SerializeField, Min(0f)]
    private float refreshDelaySeconds = 0.02f;

    private bool refreshPending;
    private float refreshAt;
    public int RefreshCount { get; private set; }
    public int LastTopologyRevision { get; private set; }
    public bool LastLayoutViable { get; private set; } = true;
    public float LastSpatialQuality { get; private set; } = 1f;

    public event Action<int> SpatialEditStateRebuilt;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
        RequestRefresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        refreshPending = false;
    }

    private void Update()
    {
        if (!refreshPending ||
            Time.unscaledTime < refreshAt)
            return;
        RefreshNow();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        if (spatialService == null ||
            layoutAssessment == null ||
            placementAssessment == null ||
            runtimeBinder == null ||
            transactions == null ||
            history == null ||
            placeableRegistry == null ||
            lifecycle == null)
        {
            error = "La integración BBSIS del Modo Edición está incompleta.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void RequestRefresh()
    {
        refreshPending = true;
        refreshAt = Time.unscaledTime +
            Mathf.Max(0f, refreshDelaySeconds);
    }

    public void RefreshNow()
    {
        ResolveDependencies();
        refreshPending = false;
        spatialService?.RebuildSubjects();
        placementAssessment?.RefreshProviderCache();
        BistroBuilderSpatialQualityResult result =
            layoutAssessment != null
                ? layoutAssessment.EvaluateCurrentLayout()
                : null;
        LastLayoutViable = result == null || result.viable;
        LastSpatialQuality = result != null
            ? result.quality
            : 1f;
        LastTopologyRevision = spatialService != null
            ? spatialService.Revision
            : 0;
        RefreshCount++;
        SpatialEditStateRebuilt?.Invoke(LastTopologyRevision);
    }

    private void HandleProvisionalCreated(
        RestaurantPlaceableObject placeable)
    {
        if (placeable != null)
            runtimeBinder?.TryBindPlaceable(placeable);
        placementAssessment?.RefreshProviderCache();
    }

    private void HandlePlacementStarted(
        RestaurantAreaMember member,
        RestaurantPlacementValidationResult result)
    {
        if (member != null &&
            member.TryGetComponent(
                out RestaurantPlaceableObject placeable))
            runtimeBinder?.TryBindPlaceable(placeable);
        placementAssessment?.RefreshProviderCache();
    }
    private void HandlePlacementFinished(
        RestaurantAreaMember member,
        RestaurantPlacementValidationResult result)
    {
        RequestRefresh();
    }

    private void HandlePlacementCancelled(
        RestaurantAreaMember member)
    {
        RequestRefresh();
    }

    private void HandlePlaceableChanged(
        RestaurantPlaceableObject placeable)
    {
        RequestRefresh();
    }

    private void HandleHistoryChanged(
        RestaurantAreaMember member)
    {
        RequestRefresh();
    }

    private void Subscribe()
    {
        if (lifecycle != null)
        {
            lifecycle.ProvisionalInstanceCreated -=
                HandleProvisionalCreated;
            lifecycle.ProvisionalInstanceCreated +=
                HandleProvisionalCreated;
        }
        if (transactions != null)
        {
            transactions.PlacementStarted -=
                HandlePlacementStarted;
            transactions.PlacementStarted +=
                HandlePlacementStarted;
            transactions.PlacementCommitted -=
                HandlePlacementFinished;
            transactions.PlacementCommitted +=
                HandlePlacementFinished;
            transactions.PlacementCancelled -=
                HandlePlacementCancelled;
            transactions.PlacementCancelled +=
                HandlePlacementCancelled;
        }

        if (placeableRegistry != null)
        {
            placeableRegistry.PlaceableRegistered -=
                HandlePlaceableChanged;
            placeableRegistry.PlaceableRegistered +=
                HandlePlaceableChanged;
            placeableRegistry.PlaceableUnregistered -=
                HandlePlaceableChanged;
            placeableRegistry.PlaceableUnregistered +=
                HandlePlaceableChanged;
        }

        if (history != null)
        {
            history.UndoPerformed -= HandleHistoryChanged;
            history.UndoPerformed += HandleHistoryChanged;
            history.RedoPerformed -= HandleHistoryChanged;
            history.RedoPerformed += HandleHistoryChanged;
        }
    }

    private void Unsubscribe()
    {
        if (lifecycle != null)
            lifecycle.ProvisionalInstanceCreated -=
                HandleProvisionalCreated;
        if (transactions != null)
        {
            transactions.PlacementStarted -=
                HandlePlacementStarted;
            transactions.PlacementCommitted -=
                HandlePlacementFinished;
            transactions.PlacementCancelled -=
                HandlePlacementCancelled;
        }

        if (placeableRegistry != null)
        {
            placeableRegistry.PlaceableRegistered -=
                HandlePlaceableChanged;
            placeableRegistry.PlaceableUnregistered -=
                HandlePlaceableChanged;
        }

        if (history != null)
        {
            history.UndoPerformed -= HandleHistoryChanged;
            history.RedoPerformed -= HandleHistoryChanged;
        }
    }

    private void ResolveDependencies()
    {
        if (spatialService == null)
            spatialService = FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        if (layoutAssessment == null)
            layoutAssessment = FindFirstObjectByType<
                BistroBuilderSpatialAssessmentService>();
        if (placementAssessment == null)
            placementAssessment = FindFirstObjectByType<
                BistroBuilderSpatialPlacementAssessmentService>();
        if (runtimeBinder == null)
            runtimeBinder = FindFirstObjectByType<
                BistroBuilderSpatialRuntimeBinder>();
        if (transactions == null)
            transactions = FindFirstObjectByType<
                RestaurantPlacementTransactionService>();
        if (history == null)
            history = FindFirstObjectByType<
                RestaurantPlacementHistoryService>();
        if (placeableRegistry == null)
            placeableRegistry = FindFirstObjectByType<
                RestaurantPlaceableRegistry>();
        if (lifecycle == null)
            lifecycle = FindFirstObjectByType<
                RestaurantPlaceableLifecycleService>();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderSpatialAssessmentService currentLayout,
        BistroBuilderSpatialPlacementAssessmentService candidateAssessment,
        BistroBuilderSpatialRuntimeBinder binder,
        RestaurantPlacementTransactionService placementTransactions,
        RestaurantPlacementHistoryService placementHistory,
        RestaurantPlaceableRegistry registry,
        RestaurantPlaceableLifecycleService lifecycleService)
    {
        spatialService = spatial;
        layoutAssessment = currentLayout;
        placementAssessment = candidateAssessment;
        runtimeBinder = binder;
        transactions = placementTransactions;
        history = placementHistory;
        placeableRegistry = registry;
        lifecycle = lifecycleService;
    }
#endif
}
