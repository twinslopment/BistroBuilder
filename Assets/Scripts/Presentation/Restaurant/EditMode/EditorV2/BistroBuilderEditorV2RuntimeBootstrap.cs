using UnityEngine;

/// <summary>
/// Runtime composition for Editor V2 B2.
///
/// Installs only the coordination layer. Existing Placement, Construction,
/// Surfaces, Finance, BBSIS, Navigation and Save/Load remain the authorities.
/// The composition is scene-scoped and idempotent.
/// </summary>
public static class BistroBuilderEditorV2RuntimeBootstrap
{
    private const string HostName = "BB_EditorV2Coordinator";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Object.FindFirstObjectByType<BistroBuilderEditorV2Coordinator>(
                FindObjectsInactive.Include) != null)
            return;

        RestaurantEditModeService editMode =
            Object.FindFirstObjectByType<RestaurantEditModeService>(
                FindObjectsInactive.Include);
        if (editMode == null)
            return;

        var host = new GameObject(HostName);
        var furniture = host.AddComponent<BistroBuilderEditorV2FurnitureAdapter>();
        var construction = host.AddComponent<BistroBuilderEditorV2ConstructionAdapter>();
        var surfaces = host.AddComponent<BistroBuilderEditorV2SurfacesAdapter>();
        var coordinator = host.AddComponent<BistroBuilderEditorV2Coordinator>();

        coordinator.Configure(
            editMode,
            furniture,
            construction,
            surfaces);
    }
}
