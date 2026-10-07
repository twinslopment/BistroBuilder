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
