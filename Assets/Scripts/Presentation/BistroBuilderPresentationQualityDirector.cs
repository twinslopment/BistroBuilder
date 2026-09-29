using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquesta únicamente la capa visual de calidad del escenario.
///
/// No modifica colliders, footprints, BBSIS, Navigation ni estados de gameplay.
/// Su trabajo es sustituir la lectura de whitebox por materiales/proxies de
/// presentación cuando el objeto funcional todavía usa primitivas de Unity.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Presentation/Presentation Quality Director")]
public sealed class BistroBuilderPresentationQualityDirector : MonoBehaviour
{
    private const string WoodMaterialPath =
        "BistroBuilder/Construction/Materials/Roble_marcos";

    private const string PlasterMaterialPath =
        "BistroBuilder/Construction/Materials/Enlucido_calido";

    private const string GraphiteMaterialPath =
        "BistroBuilder/Construction/Materials/Metal_grafito";

    private static bool runtimeHookInstalled;

    private readonly Dictionary<Renderer, Material[]>
        originalMaterials =
            new Dictionary<Renderer, Material[]>();

    private readonly Dictionary<Renderer, bool>
        actorRendererStates =
            new Dictionary<Renderer, bool>();

    private RestaurantEditModeService editModeService;
    private Material woodMaterial;
    private Material plasterMaterial;
    private Material graphiteMaterial;
    private bool sceneSkinned;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallRuntimeHook()
    {
        if (runtimeHookInstalled)
            return;

        runtimeHookInstalled = true;

        SceneManager.sceneLoaded -=
            HandleSceneLoaded;

        SceneManager.sceneLoaded +=
            HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        if (!scene.IsValid() ||
            !scene.isLoaded)
        {
            return;
        }

        BistroBuilderPresentationQualityDirector existing =
            FindFirstObjectByType<
                BistroBuilderPresentationQualityDirector>();

        if (existing != null)
            return;

        GameObject host =
            new GameObject(
                "BB_PresentationQuality");

        host.AddComponent<
            BistroBuilderPresentationQualityDirector>();
    }

    private void Awake()
    {
        LoadMaterials();
        ResolveDependencies();
    }

    private void Start()
    {
        ApplyScenePresentation();
        RefreshActorVisibility();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RestoreActorRenderers();
    }

    private void OnDestroy()
    {
        RestoreActorRenderers();
    }

    private void ResolveDependencies()
    {
        if (editModeService == null)
        {
            editModeService =
                FindFirstObjectByType<
                    RestaurantEditModeService>();
        }
    }

    private void LoadMaterials()
    {
        if (woodMaterial == null)
        {
            woodMaterial =
                Resources.Load<Material>(
                    WoodMaterialPath);
        }

        if (plasterMaterial == null)
        {
            plasterMaterial =
                Resources.Load<Material>(
                    PlasterMaterialPath);
        }

        if (graphiteMaterial == null)
        {
            graphiteMaterial =
                Resources.Load<Material>(
                    GraphiteMaterialPath);
        }
    }

    private void Subscribe()
    {
        if (editModeService == null)
            return;

        editModeService.EditModeEntered -=
            HandleEditModeChanged;

        editModeService.EditModeExited -=
            HandleEditModeChanged;

        editModeService.EditModeEntered +=
            HandleEditModeChanged;

        editModeService.EditModeExited +=
            HandleEditModeChanged;
    }

    private void Unsubscribe()
    {
        if (editModeService == null)
            return;

        editModeService.EditModeEntered -=
            HandleEditModeChanged;

        editModeService.EditModeExited -=
            HandleEditModeChanged;
    }

    private void HandleEditModeChanged()
    {
        ApplyScenePresentation();
        RefreshActorVisibility();
    }

    private void ApplyScenePresentation()
    {
        LoadMaterials();

        if (!sceneSkinned)
        {
            SkinPrimitivePlaceables();
            SkinPrimitiveObstacles();
            InstallPrimitiveTableProxies();
            sceneSkinned = true;
        }
        else
        {
            /*
             * Mobiliario nuevo puede aparecer durante una sesión de edición.
             * El pase es idempotente y solo adopta primitivas todavía sin skin.
             */
            SkinPrimitivePlaceables();
            InstallPrimitiveTableProxies();
        }
    }

    private void SkinPrimitivePlaceables()
    {
        RestaurantPlaceableObject[] placeables =
            FindObjectsByType<
                RestaurantPlaceableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0;
             index < placeables.Length;
             index++)
        {
            RestaurantPlaceableObject placeable =
                placeables[index];

            if (placeable == null)
                continue;

            Material target =
                ResolvePlaceableMaterial(placeable);

            if (target == null)
                continue;

            MeshRenderer[] renderers =
                placeable.GetComponentsInChildren<
                    MeshRenderer>(true);

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                MeshRenderer renderer =
                    renderers[rendererIndex];

                if (!IsPrimitiveRenderer(renderer))
                    continue;

                ApplyMaterial(
                    renderer,
                    target);
            }
        }
    }

    private void SkinPrimitiveObstacles()
    {
        if (plasterMaterial == null)
            return;

        RestaurantPlacementObstacle[] obstacles =
            FindObjectsByType<
                RestaurantPlacementObstacle>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0;
             index < obstacles.Length;
             index++)
        {
            RestaurantPlacementObstacle obstacle =
                obstacles[index];

            if (obstacle == null ||
                obstacle.GetComponentInParent<
                    RestaurantPlaceableObject>() != null)
            {
                continue;
            }

            MeshRenderer[] renderers =
                obstacle.GetComponentsInChildren<
                    MeshRenderer>(true);

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                MeshRenderer renderer =
                    renderers[rendererIndex];

                if (IsPrimitiveRenderer(renderer))
                {
                    ApplyMaterial(
                        renderer,
                        plasterMaterial);
                }
            }
        }
    }

    private void InstallPrimitiveTableProxies()
    {
        if (woodMaterial == null)
            return;

        RestaurantTable[] tables =
            FindObjectsByType<
                RestaurantTable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0;
             index < tables.Length;
             index++)
        {
            RestaurantTable table =
                tables[index];

            if (table == null ||
                table.GetComponent<
                    BistroBuilderPrimitiveTablePresentationProxy>() != null)
            {
                continue;
            }

            MeshRenderer source =
                FindTablePrimitiveRenderer(table);

            if (source == null)
                continue;

            BistroBuilderPrimitiveTablePresentationProxy proxy =
                table.gameObject.AddComponent<
                    BistroBuilderPrimitiveTablePresentationProxy>();

            proxy.Configure(
                source,
                woodMaterial);
        }
    }

    private void RefreshActorVisibility()
    {
        RestoreActorRenderers();

        if (editModeService == null ||
            !editModeService.IsEditModeActive)
        {
            return;
        }

        HidePrimitiveRenderers(
            FindObjectsByType<
                CustomerMovementView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None));

        HidePrimitiveRenderers(
            FindObjectsByType<
                WaiterMovementView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None));
    }

    private void HidePrimitiveRenderers<T>(
        T[] owners)
        where T : Component
    {
        if (owners == null)
            return;

        for (int ownerIndex = 0;
             ownerIndex < owners.Length;
             ownerIndex++)
        {
            T owner =
                owners[ownerIndex];

            if (owner == null)
                continue;

            MeshRenderer[] renderers =
                owner.GetComponentsInChildren<
                    MeshRenderer>(true);

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                MeshRenderer renderer =
                    renderers[rendererIndex];

                if (!IsPrimitiveRenderer(renderer) ||
                    actorRendererStates.ContainsKey(renderer))
                {
                    continue;
                }

                actorRendererStates.Add(
                    renderer,
                    renderer.enabled);

                renderer.enabled = false;
            }
        }
    }

    private void RestoreActorRenderers()
    {
        foreach (
            KeyValuePair<Renderer, bool> pair
            in actorRendererStates)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }

        actorRendererStates.Clear();
    }

    private Material ResolvePlaceableMaterial(
        RestaurantPlaceableObject placeable)
    {
        if (placeable == null ||
            placeable.ItemDefinition == null)
        {
            return woodMaterial;
        }

        switch (placeable.ItemDefinition.Category)
        {
            case RestaurantPlaceableItemCategory.KitchenEquipment:
            case RestaurantPlaceableItemCategory.ServiceEquipment:
            case RestaurantPlaceableItemCategory.Lighting:
                return graphiteMaterial != null
                    ? graphiteMaterial
                    : woodMaterial;

            case RestaurantPlaceableItemCategory.Structural:
                return plasterMaterial != null
                    ? plasterMaterial
                    : woodMaterial;

            default:
                return woodMaterial;
        }
    }

    private void ApplyMaterial(
        Renderer renderer,
        Material material)
    {
        if (renderer == null ||
            material == null)
        {
            return;
        }

        if (!originalMaterials.ContainsKey(renderer))
        {
            originalMaterials.Add(
                renderer,
                renderer.sharedMaterials);
        }

        Material[] materials =
            renderer.sharedMaterials;

        if (materials == null ||
            materials.Length == 0)
        {
            renderer.sharedMaterial =
                material;
            return;
        }

        Material[] replacement =
            new Material[materials.Length];

        for (int index = 0;
             index < replacement.Length;
             index++)
        {
            replacement[index] =
                material;
        }

        renderer.sharedMaterials =
            replacement;
    }

    private static MeshRenderer FindTablePrimitiveRenderer(
        RestaurantTable table)
    {
        MeshRenderer[] renderers =
            table.GetComponentsInChildren<
                MeshRenderer>(true);

        MeshRenderer best = null;
        float bestVolume = 0f;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            MeshRenderer renderer =
                renderers[index];

            if (!IsPrimitiveRenderer(renderer))
                continue;

            Bounds bounds =
                renderer.bounds;

            float volume =
                bounds.size.x *
                bounds.size.y *
                bounds.size.z;

            if (volume <= bestVolume)
                continue;

            best =
                renderer;

            bestVolume =
                volume;
        }

        return best;
    }

    private static bool IsPrimitiveRenderer(
        MeshRenderer renderer)
    {
        if (renderer == null)
            return false;

        MeshFilter filter =
            renderer.GetComponent<MeshFilter>();

        if (filter == null ||
            filter.sharedMesh == null)
        {
            return false;
        }

        string meshName =
            filter.sharedMesh.name;

        return meshName == "Cube" ||
               meshName == "Sphere" ||
               meshName == "Capsule" ||
               meshName == "Cylinder" ||
               meshName == "Plane" ||
               meshName == "Quad";
    }
}
