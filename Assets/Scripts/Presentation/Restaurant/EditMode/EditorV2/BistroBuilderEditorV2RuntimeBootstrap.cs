using UnityEngine;

/// <summary>
/// Runtime composition for Editor V2 B2/B3.
///
/// Installs coordination plus the read-only common-selection projection.
/// Existing Placement, Construction, Surfaces, Finance, BBSIS, Navigation
/// and Save/Load remain the authorities. Composition is scene-scoped
/// and idempotent.
/// </summary>
public static class BistroBuilderEditorV2RuntimeBootstrap
{
    private const string HostName = "BB_EditorV2Coordinator";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        RestaurantEditModeService editMode =
            Object.FindFirstObjectByType<RestaurantEditModeService>(
                FindObjectsInactive.Include);
        if (editMode == null)
            return;

        BistroBuilderEditorV2Coordinator coordinator =
            Object.FindFirstObjectByType<BistroBuilderEditorV2Coordinator>(
                FindObjectsInactive.Include);

        BistroBuilderEditorV2FurnitureAdapter furniture =
            Object.FindFirstObjectByType<BistroBuilderEditorV2FurnitureAdapter>(
                FindObjectsInactive.Include);
        BistroBuilderEditorV2ConstructionAdapter construction =
            Object.FindFirstObjectByType<BistroBuilderEditorV2ConstructionAdapter>(
                FindObjectsInactive.Include);
        BistroBuilderEditorV2SurfacesAdapter surfaces =
            Object.FindFirstObjectByType<BistroBuilderEditorV2SurfacesAdapter>(
                FindObjectsInactive.Include);

        GameObject host = coordinator != null
            ? coordinator.gameObject
            : new GameObject(HostName);

        if (furniture == null)
            furniture = host.AddComponent<BistroBuilderEditorV2FurnitureAdapter>();
        if (construction == null)
            construction = host.AddComponent<BistroBuilderEditorV2ConstructionAdapter>();
        if (surfaces == null)
            surfaces = host.AddComponent<BistroBuilderEditorV2SurfacesAdapter>();

        if (coordinator == null)
        {
            coordinator = host.AddComponent<BistroBuilderEditorV2Coordinator>();
            coordinator.Configure(
                editMode,
                furniture,
                construction,
                surfaces);
        }

        BistroBuilderEditorV2SelectionCoordinator selection =
            Object.FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>(
                FindObjectsInactive.Include);
        if (selection == null)
            selection = host.AddComponent<BistroBuilderEditorV2SelectionCoordinator>();

        selection.Configure(
            editMode,
            coordinator,
            furniture,
            construction,
            surfaces);

        BistroBuilderEditorV2GroupOperationService groupOperations =
            Object.FindFirstObjectByType<
                BistroBuilderEditorV2GroupOperationService>(
                    FindObjectsInactive.Include);

        if (groupOperations == null)
        {
            groupOperations =
                host.AddComponent<
                    BistroBuilderEditorV2GroupOperationService>();
        }

        groupOperations.Configure(
            selection,
            Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
                FindObjectsInactive.Include),
            Object.FindFirstObjectByType<
                RestaurantEditInteractionController>(
                    FindObjectsInactive.Include),
            Object.FindFirstObjectByType<
                RestaurantPlaceableCreationService>(
                    FindObjectsInactive.Include),
            Object.FindFirstObjectByType<
                RestaurantPlaceableDeletionService>(
                    FindObjectsInactive.Include));

        BistroBuilderEditorV2GlobalHistory globalHistory =
            Object.FindFirstObjectByType<BistroBuilderEditorV2GlobalHistory>(
                FindObjectsInactive.Include);
        if (globalHistory == null)
            globalHistory = host.AddComponent<BistroBuilderEditorV2GlobalHistory>();

        RestaurantPlacementHistoryService placementHistory =
            Object.FindFirstObjectByType<RestaurantPlacementHistoryService>(
                FindObjectsInactive.Include);
        BistroBuilderEditRuntimeCoordinator architectureRuntime =
            Object.FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>(
                FindObjectsInactive.Include);

        globalHistory.Configure(placementHistory, architectureRuntime);

        BistroBuilderEditorV2RenovationSession renovation =
            Object.FindFirstObjectByType<BistroBuilderEditorV2RenovationSession>(
                FindObjectsInactive.Include);
        if (renovation == null)
            renovation = host.AddComponent<BistroBuilderEditorV2RenovationSession>();

        renovation.Configure(
            editMode,
            coordinator,
            globalHistory,
            placementHistory,
            architectureRuntime,
            Object.FindFirstObjectByType<BistroBuilderFinanceService>(
                FindObjectsInactive.Include));

        RestaurantPlacementLinkedGroupService linkedGroups =
            Object.FindFirstObjectByType<RestaurantPlacementLinkedGroupService>(
                FindObjectsInactive.Include);
        if (linkedGroups != null)
        {
            BistroBuilderEditorV2ExplicitSelectionLinkedGroupProvider explicitSelection =
                linkedGroups.GetComponent<
                    BistroBuilderEditorV2ExplicitSelectionLinkedGroupProvider>();

            if (explicitSelection == null)
            {
                explicitSelection =
                    linkedGroups.gameObject.AddComponent<
                        BistroBuilderEditorV2ExplicitSelectionLinkedGroupProvider>();
            }

            explicitSelection.Configure(
                selection,
                Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
                    FindObjectsInactive.Include));

            linkedGroups.RefreshProviders();
        }

        // B11 diagnosis is installed once and stays dormant until a player
        // asks to scan. Existing BBSIS/Navigation/Placement are authorities.
        BistroBuilderEditorV2DiagnosisService diagnosis =
            Object.FindFirstObjectByType<BistroBuilderEditorV2DiagnosisService>(
                FindObjectsInactive.Include);
        if (diagnosis == null)
            diagnosis = host.AddComponent<BistroBuilderEditorV2DiagnosisService>();
        diagnosis.Configure(
            editMode,
            Object.FindFirstObjectByType<BistroBuilderSpatialAssessmentService>(
                FindObjectsInactive.Include),
            Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>(
                FindObjectsInactive.Include),
            Object.FindFirstObjectByType<BistroBuilderNavigationService>(
                FindObjectsInactive.Include),
            Object.FindFirstObjectByType<RestaurantPlacementRegistry>(
                FindObjectsInactive.Include),
            Object.FindFirstObjectByType<RestaurantPlacementValidationService>(
                FindObjectsInactive.Include),
            Object.FindFirstObjectByType<RestaurantSeatRegistry>(
                FindObjectsInactive.Include),
            Object.FindFirstObjectByType<RestaurantSeatingTopologyService>(
                FindObjectsInactive.Include),
            Object.FindFirstObjectByType<BistroBuilderEditNavigationValidationProvider>(
                FindObjectsInactive.Include),
            Object.FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>(
                FindObjectsInactive.Include));

        BistroBuilderEditorV2DiagnosisOverlay diagnosisOverlay =
            Object.FindFirstObjectByType<BistroBuilderEditorV2DiagnosisOverlay>(
                FindObjectsInactive.Include);
        if (diagnosisOverlay == null)
            diagnosisOverlay =
                host.AddComponent<BistroBuilderEditorV2DiagnosisOverlay>();
        diagnosisOverlay.Configure(diagnosis, editMode);

        RestaurantPlacementSnapService placementSnap =
            Object.FindFirstObjectByType<RestaurantPlacementSnapService>(
                FindObjectsInactive.Include);
        if (placementSnap != null)
        {
            RestaurantContextualPlacementSnapProvider contextual =
                placementSnap.GetComponent<RestaurantContextualPlacementSnapProvider>();
            if (contextual == null)
                contextual = placementSnap.gameObject.AddComponent<RestaurantContextualPlacementSnapProvider>();

            placementSnap.RefreshProviders();
        }
    }
}
