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
    }
}
