using UnityEngine;

/// <summary>
/// Runtime composition fallback for the Construction Authoring V1 playtest.
/// It adds the authoring tool only when Block 18 already exists in the loaded scene.
/// No scene mutation is persisted and no second architecture authority is created.
/// </summary>
public static class BistroBuilderConstructionAuthoringRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>() != null)
            return;

        BistroBuilderEditRuntimeCoordinator coordinator =
            Object.FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
        RestaurantEditModeService editMode =
            Object.FindFirstObjectByType<RestaurantEditModeService>();
        if (coordinator == null || editMode == null)
            return;

        var host = new GameObject("BB18N_ConstructionAuthoringRuntime");
        host.AddComponent<BistroBuilderConstructionAuthoringRuntimeTool>();
        host.AddComponent<BistroBuilderConstructionPlayerPanel>();
    }
}
