using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Capa única de presentación para contenido whitebox.
///
/// No altera colliders, footprints, BBSIS, Seat Bays, navegación, economía
/// ni Save/Load. Solo reemplaza o calma la representación visual de
/// placeholders inequívocos mientras sigan siendo necesarios.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Presentation/Prototype Presentation Service")]
public sealed class BistroBuilderPrototypePresentationService :
    MonoBehaviour
{
    private const string TableVisualRootName =
        "BB_Presentation_TableVisual";

    private const string FloorPlinthName =
        "BB_Presentation_FloorPlinth";

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntimeInstallation()
    {
        SceneManager.sceneLoaded -=
            HandleRuntimeSceneLoaded;

        SceneManager.sceneLoaded +=
            HandleRuntimeSceneLoaded;
    }

    private static void HandleRuntimeSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        EnsureRuntimeInstallation();
    }

    private static void EnsureRuntimeInstallation()
    {
        if (FindFirstObjectByType<
                BistroBuilderPrototypePresentationService>() != null)
        {
            return;
        }

        RestaurantPlaceableCreationService creation =
            FindFirstObjectByType<
                RestaurantPlaceableCreationService>(
                FindObjectsInactive.Include);

        BistroBuilderNewGameOpeningService opening =
            FindFirstObjectByType<
                BistroBuilderNewGameOpeningService>(
                FindObjectsInactive.Include);

        GameObject host =
            creation != null
                ? creation.gameObject
                : opening != null
                    ? opening.gameObject
                    : null;

        if (host == null)
            return;

        host.AddComponent<
            BistroBuilderPrototypePresentationService>();
    }

    [Header("Ámbito")]
    [SerializeField] private bool skinPrimitiveTables = true;
    [SerializeField] private bool skinPrimitivePlaceables = true;
    [SerializeField] private bool calmExplicitTestGeometry = true;
    [SerializeField] private bool skinPrimitiveWaiters = true;
    [SerializeField] private bool suppressPrimitiveActorsDuringEdit = true;

    [Header("Materiales canónicos")]
    [SerializeField] private string tableMaterialResource =
        "BistroBuilder/Construction/Materials/Roble_marcos";

    [SerializeField] private string serviceMaterialResource =
        "BistroBuilder/Construction/Materials/Metal_grafito";

    [SerializeField] private string architectureMaterialResource =
        "BistroBuilder/Construction/Materials/Enlucido_calido";

    [SerializeField] private RestaurantPlaceableCreationService
        creationService;

    [SerializeField] private BistroBuilderNewGameOpeningService
        openingService;

    [SerializeField] private RestaurantEditModeService
        editModeService;

    private readonly Dictionary<int, TablePresentationBinding>
        tableBindings =
            new Dictionary<int, TablePresentationBinding>();

    private readonly Dictionary<Renderer, bool>
        actorRendererStates =
            new Dictionary<Renderer, bool>();

    private Material tableMaterial;
    private Material serviceMaterial;
    private Material architectureMaterial;
    private MaterialPropertyBlock sourceBlock;

    private void Awake()
    {
        sourceBlock =
            new MaterialPropertyBlock();

        CacheDependencies();
        LoadMaterials();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();

        if (tableBindings.Count > 0)
            ReactivateTableBindings();
    }

    private void Start()
    {
        ApplyScenePresentation();
    }

    private void LateUpdate()
    {
        SynchronizeTableVisualState();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RestoreActorRenderers();
        RestoreTableSources();
    }

    private void OnDestroy()
    {
        RestoreActorRenderers();
        RestoreTableSources();
    }

    public void ApplyScenePresentation()
    {
        LoadMaterials();

        if (skinPrimitiveTables)
        {
            RestaurantTable[] tables =
                FindObjectsByType<RestaurantTable>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int index = 0;
                 index < tables.Length;
                 index++)
            {
                TrySkinPrimitiveTable(
                    tables[index]);
            }
        }

        if (skinPrimitivePlaceables)
        {
            SkinPrimitivePlaceables();
            SkinPrimitiveObstacles();
        }

        if (skinPrimitiveWaiters)
        {
            Waiter[] waiters =
                FindObjectsByType<Waiter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int index = 0;
                 index < waiters.Length;
                 index++)
            {
                StylePrimitiveActor(
                    waiters[index] != null
                        ? waiters[index].gameObject
                        : null);
            }
        }

        if (calmExplicitTestGeometry)
            CalmExplicitPlaceholders();

        EnsureFloorPlinth();
        RefreshActorVisibility();
    }

    private void HandleCreationCommitted(
        RestaurantPlaceableObject placeable)
    {
        if (placeable == null)
            return;

        RestaurantTable table =
            placeable.GetComponent<RestaurantTable>();

        if (table != null)
        {
            TrySkinPrimitiveTable(
                table);
        }

        if (skinPrimitivePlaceables)
        {
            SkinPrimitivePlaceable(
                placeable);
        }
    }

    private void TrySkinPrimitiveTable(RestaurantTable table)
    {
        if (table == null || tableBindings.ContainsKey(table.GetInstanceID())) return;
        TablePresentationBinding binding = CreateTablePresentation(table, tableMaterial);
        if (binding != null) tableBindings.Add(table.GetInstanceID(), binding);
    }

    // The catalogue renderer uses the same presentation geometry as placement.
    // This creates visual children only, with no subscription or scene authority.
    public static GameObject ApplyTablePresentationForPreview(RestaurantTable table)
    {
        var material = Resources.Load<Material>(
            "BistroBuilder/Construction/Materials/Roble_marcos");
        return CreateTablePresentation(table, material)?.Root;
    }

    private static TablePresentationBinding CreateTablePresentation(
        RestaurantTable table, Material material)
    {
        if (table == null) return null;
        MeshRenderer sourceRenderer = FindPrimitiveTableSource(table);
        MeshFilter sourceFilter = sourceRenderer != null
            ? sourceRenderer.GetComponent<MeshFilter>() : null;
        if (sourceFilter == null || sourceFilter.sharedMesh == null) return null;
        if (material == null) material = sourceRenderer.sharedMaterial;
        if (material == null) return null;

        Transform existing =
            table.transform.Find(
                TableVisualRootName);

        if (existing != null)
        {
            if (Application.isPlaying) { existing.gameObject.SetActive(false); Destroy(existing.gameObject); }
            else DestroyImmediate(existing.gameObject);
        }

        GameObject root =
            new GameObject(
                TableVisualRootName);

        root.layer =
            table.gameObject.layer;

        root.transform.SetParent(
            table.transform,
            false);

        // Work in the table frame: world AABB dimensions would swap/widen
        // the visual when the root is rotated, unlike its physical footprint.
        Bounds meshBounds = sourceFilter.sharedMesh.bounds;
        Matrix4x4 sourceToTable = table.transform.worldToLocalMatrix *
            sourceFilter.transform.localToWorldMatrix;
        Bounds localBounds = new Bounds(sourceToTable.MultiplyPoint3x4(meshBounds.min), Vector3.zero);
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = new Vector3(
                (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
            localBounds.Encapsulate(sourceToTable.MultiplyPoint3x4(point));
        }
        Vector3 localSize = localBounds.size;
        Vector3 localCenter = localBounds.center;

        float topThickness =
            Mathf.Clamp(
                localSize.y * 0.105f,
                0.055f,
                0.085f);

        float legHeight =
            Mathf.Max(
                0.46f,
                localSize.y -
                topThickness);

        float shortestSide =
            Mathf.Min(
                localSize.x,
                localSize.z);

        float legWidth =
            Mathf.Clamp(
                shortestSide * 0.065f,
                0.045f,
                0.075f);

        float insetX =
            Mathf.Clamp(
                localSize.x * 0.105f,
                0.08f,
                0.14f);

        float insetZ =
            Mathf.Clamp(
                localSize.z * 0.105f,
                0.08f,
                0.14f);

        float topY =
            localCenter.y +
            localSize.y * 0.5f -
            topThickness * 0.5f;

        var generated =
            new List<MeshRenderer>(6);

        AddTablePart(
            root.transform,
            "Top",
            sourceFilter.sharedMesh,
            material,
            new Vector3(
                localCenter.x,
                topY,
                localCenter.z),
            new Vector3(
                localSize.x,
                topThickness,
                localSize.z),
            generated);

        float legCenterY =
            topY -
            topThickness * 0.5f -
            legHeight * 0.5f;

        float halfX =
            Mathf.Max(
                0.12f,
                localSize.x * 0.5f -
                insetX);

        float halfZ =
            Mathf.Max(
                0.12f,
                localSize.z * 0.5f -
                insetZ);

        AddLeg(
            root.transform,
            sourceFilter.sharedMesh,
            material,
            localCenter,
            -halfX,
            legCenterY,
            -halfZ,
            legWidth,
            legHeight,
            generated);

        AddLeg(
            root.transform,
            sourceFilter.sharedMesh,
            material,
            localCenter,
            halfX,
            legCenterY,
            -halfZ,
            legWidth,
            legHeight,
            generated);

        AddLeg(
            root.transform,
            sourceFilter.sharedMesh,
            material,
            localCenter,
            -halfX,
            legCenterY,
            halfZ,
            legWidth,
            legHeight,
            generated);

        AddLeg(
            root.transform,
            sourceFilter.sharedMesh,
            material,
            localCenter,
            halfX,
            legCenterY,
            halfZ,
            legWidth,
            legHeight,
            generated);

        float apronHeight =
            Mathf.Clamp(
                localSize.y * 0.075f,
                0.045f,
                0.065f);

        AddTablePart(
            root.transform,
            "Apron",
            sourceFilter.sharedMesh,
            material,
            new Vector3(
                localCenter.x,
                topY -
                topThickness * 0.5f -
                apronHeight * 0.5f,
                localCenter.z),
            new Vector3(
                Mathf.Max(
                    0.20f,
                    localSize.x -
                    insetX * 1.05f),
                apronHeight,
                Mathf.Max(
                    0.20f,
                    localSize.z -
                    insetZ * 1.05f)),
            generated);

        bool sourceWasEnabled =
            sourceRenderer.enabled;

        sourceRenderer.enabled =
            false;

        return new TablePresentationBinding(
            sourceRenderer, sourceWasEnabled, root, generated);
    }

    private static void AddLeg(
        Transform parent,
        Mesh mesh,
        Material material,
        Vector3 localCenter,
        float offsetX,
        float centerY,
        float offsetZ,
        float width,
        float height,
        List<MeshRenderer> generated)
    {
        AddTablePart(
            parent,
            "Leg",
            mesh,
            material,
            new Vector3(
                localCenter.x + offsetX,
                centerY,
                localCenter.z + offsetZ),
            new Vector3(
                width,
                height,
                width),
            generated);
    }

    private static void AddTablePart(
        Transform parent,
        string name,
        Mesh mesh,
        Material material,
        Vector3 localPosition,
        Vector3 localScale,
        List<MeshRenderer> generated)
    {
        GameObject part =
            new GameObject(
                name,
                typeof(MeshFilter),
                typeof(MeshRenderer));

        part.layer =
            parent.gameObject.layer;

        part.transform.SetParent(
            parent,
            false);

        part.transform.localPosition =
            localPosition;

        part.transform.localRotation =
            Quaternion.identity;

        part.transform.localScale =
            localScale;

        MeshFilter filter =
            part.GetComponent<MeshFilter>();

        filter.sharedMesh =
            mesh;

        MeshRenderer renderer =
            part.GetComponent<MeshRenderer>();

        renderer.sharedMaterial =
            material;

        renderer.shadowCastingMode =
            ShadowCastingMode.On;

        renderer.receiveShadows =
            true;

        renderer.lightProbeUsage =
            LightProbeUsage.BlendProbes;

        renderer.reflectionProbeUsage =
            ReflectionProbeUsage.BlendProbes;

        generated?.Add(
            renderer);

        /*
         * No se añade Collider: toda la autoridad física permanece
         * en el placeholder funcional original.
         */
    }

    private void SynchronizeTableVisualState()
    {
        if (sourceBlock == null ||
            tableBindings.Count == 0)
        {
            return;
        }

        foreach (
            KeyValuePair<int, TablePresentationBinding>
                pair in tableBindings)
        {
            TablePresentationBinding binding =
                pair.Value;

            if (binding == null ||
                binding.Source == null)
            {
                continue;
            }

            sourceBlock.Clear();

            binding.Source.GetPropertyBlock(
                sourceBlock);

            for (int index = 0;
                 index < binding.Generated.Count;
                 index++)
            {
                MeshRenderer renderer =
                    binding.Generated[index];

                if (renderer != null)
                {
                    renderer.SetPropertyBlock(
                        sourceBlock);
                }
            }
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
            SkinPrimitivePlaceable(
                placeables[index]);
        }
    }

    private void SkinPrimitivePlaceable(
        RestaurantPlaceableObject placeable)
    {
        if (placeable == null)
            return;

        Material target =
            ResolvePlaceableMaterial(
                placeable);

        if (target == null)
            return;

        MeshRenderer[] renderers =
            placeable.GetComponentsInChildren<
                MeshRenderer>(true);

        Transform tableVisualRoot =
            placeable.transform.Find(
                TableVisualRootName);

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            MeshRenderer renderer =
                renderers[index];

            if (!IsPrimitiveRenderer(renderer) ||
                (tableVisualRoot != null &&
                 renderer.transform.IsChildOf(
                     tableVisualRoot)))
            {
                continue;
            }

            renderer.sharedMaterial =
                target;
        }
    }

    private void SkinPrimitiveObstacles()
    {
        if (architectureMaterial == null)
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
                    renderer.sharedMaterial =
                        architectureMaterial;
                }
            }
        }
    }

    private Material ResolvePlaceableMaterial(
        RestaurantPlaceableObject placeable)
    {
        if (placeable == null ||
            placeable.ItemDefinition == null)
        {
            return tableMaterial;
        }

        switch (placeable.ItemDefinition.Category)
        {
            case RestaurantPlaceableItemCategory.KitchenEquipment:
            case RestaurantPlaceableItemCategory.ServiceEquipment:
            case RestaurantPlaceableItemCategory.Lighting:
                return serviceMaterial != null
                    ? serviceMaterial
                    : tableMaterial;

            case RestaurantPlaceableItemCategory.Structural:
                return architectureMaterial != null
                    ? architectureMaterial
                    : tableMaterial;

            default:
                return tableMaterial;
        }
    }

    private void StylePrimitiveActor(
        GameObject actor)
    {
        if (actor == null ||
            serviceMaterial == null)
        {
            return;
        }

        MeshRenderer[] renderers =
            actor.GetComponentsInChildren<
                MeshRenderer>(true);

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            MeshRenderer renderer =
                renderers[index];

            if (IsPrimitiveRenderer(renderer))
            {
                renderer.sharedMaterial =
                    serviceMaterial;
            }
        }
    }

    private void EnsureFloorPlinth()
    {
        GameObject floor =
            GameObject.Find(
                "Floor_Test");

        if (floor == null)
            return;

        Transform existing =
            floor.transform.Find(
                FloorPlinthName);

        bool fullPremisesPresentation =
            GameObject.Find(
                BistroBuilderPremisesPresentationRuntime.RootName) != null;

        if (fullPremisesPresentation)
        {
            if (existing != null)
                existing.gameObject.SetActive(false);

            return;
        }

        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        if (serviceMaterial == null)
            return;

        MeshRenderer floorRenderer =
            floor.GetComponent<MeshRenderer>();

        Mesh cubeMesh =
            FindReusablePrimitiveMesh(
                "Cube");

        if (floorRenderer == null ||
            cubeMesh == null)
        {
            return;
        }

        Bounds bounds =
            floorRenderer.bounds;

        const float worldThickness =
            0.10f;

        Vector3 worldCenter =
            bounds.center;

        worldCenter.y =
            bounds.min.y -
            worldThickness * 0.5f;

        Vector3 lossyScale =
            floor.transform.lossyScale;

        float safeX =
            Mathf.Max(
                0.0001f,
                Mathf.Abs(lossyScale.x));

        float safeY =
            Mathf.Max(
                0.0001f,
                Mathf.Abs(lossyScale.y));

        float safeZ =
            Mathf.Max(
                0.0001f,
                Mathf.Abs(lossyScale.z));

        GameObject plinth =
            new GameObject(
                FloorPlinthName,
                typeof(MeshFilter),
                typeof(MeshRenderer));

        int ignoreRaycast =
            LayerMask.NameToLayer(
                "Ignore Raycast");

        plinth.layer =
            ignoreRaycast >= 0
                ? ignoreRaycast
                : floor.layer;

        plinth.transform.SetParent(
            floor.transform,
            false);

        plinth.transform.localPosition =
            floor.transform.InverseTransformPoint(
                worldCenter);

        plinth.transform.localRotation =
            Quaternion.identity;

        plinth.transform.localScale =
            new Vector3(
                bounds.size.x * 1.012f /
                    safeX,
                worldThickness /
                    safeY,
                bounds.size.z * 1.012f /
                    safeZ);

        MeshFilter filter =
            plinth.GetComponent<MeshFilter>();

        filter.sharedMesh =
            cubeMesh;

        MeshRenderer renderer =
            plinth.GetComponent<MeshRenderer>();

        ConfigurePresentationRenderer(
            renderer,
            serviceMaterial);
    }

    private void CalmExplicitPlaceholders()
    {
        HideRendererOnly(
            GameObject.Find(
                "PlacementObstacle_Test"));

        /*
         * Cuando la presentación completa del local está activa, ella es la
         * única autoridad visual de cocina/bar/envolvente. El fallback no
         * dibuja ni modifica una segunda versión de esos elementos.
         */
        if (GameObject.Find(
                BistroBuilderPremisesPresentationRuntime.RootName) != null)
        {
            return;
        }

        HideRendererOnly(
            GameObject.Find(
                "Kitchen_Test"));

        ApplyMaterialToNamedObject(
            "ProvisionalCounter",
            tableMaterial);

        GameObject[] all =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0;
             index < all.Length;
             index++)
        {
            GameObject candidate =
                all[index];

            if (candidate == null ||
                !candidate.name.StartsWith(
                    "ProvisionalStool",
                    StringComparison.Ordinal))
            {
                continue;
            }

            ApplyMaterial(
                candidate,
                serviceMaterial != null
                    ? serviceMaterial
                    : tableMaterial);
        }
    }

    private static void ConfigurePresentationRenderer(
        MeshRenderer renderer,
        Material material)
    {
        if (renderer == null)
            return;

        renderer.sharedMaterial =
            material;

        renderer.shadowCastingMode =
            ShadowCastingMode.On;

        renderer.receiveShadows =
            true;

        renderer.lightProbeUsage =
            LightProbeUsage.BlendProbes;

        renderer.reflectionProbeUsage =
            ReflectionProbeUsage.BlendProbes;
    }

    private static Mesh FindReusablePrimitiveMesh(
        string meshName)
    {
        MeshFilter[] filters =
            FindObjectsByType<MeshFilter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0;
             index < filters.Length;
             index++)
        {
            MeshFilter filter =
                filters[index];

            if (filter != null &&
                filter.sharedMesh != null &&
                string.Equals(
                    filter.sharedMesh.name,
                    meshName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return filter.sharedMesh;
            }
        }

        return null;
    }

    private void RefreshActorVisibility()
    {
        RestoreActorRenderers();

        bool initialSetup =
            openingService != null &&
            (openingService.Phase ==
                 BistroBuilderNewGamePhase.StartMenu ||
             openingService.Phase ==
                 BistroBuilderNewGamePhase.InitialSetup);

        bool editing =
            suppressPrimitiveActorsDuringEdit &&
            editModeService != null &&
            editModeService.IsEditModeActive;

        if (!initialSetup &&
            !editing)
        {
            return;
        }

        HidePrimitiveActorRenderers(
            FindObjectsByType<
                WaiterMovementView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None));

        HidePrimitiveActorRenderers(
            FindObjectsByType<
                CustomerMovementView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None));
    }

    private void HidePrimitiveActorRenderers<T>(
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
                    actorRendererStates.ContainsKey(
                        renderer))
                {
                    continue;
                }

                actorRendererStates.Add(
                    renderer,
                    renderer.enabled);

                renderer.enabled =
                    false;
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
            {
                pair.Key.enabled =
                    pair.Value;
            }
        }

        actorRendererStates.Clear();
    }

    private void RestoreTableSources()
    {
        foreach (
            KeyValuePair<int, TablePresentationBinding>
                pair in tableBindings)
        {
            TablePresentationBinding binding =
                pair.Value;

            if (binding == null)
                continue;

            if (binding.Source != null)
            {
                binding.Source.enabled =
                    binding.SourceWasEnabled;
            }

            if (binding.Root != null)
            {
                binding.Root.SetActive(
                    false);
            }
        }
    }

    private void ReactivateTableBindings()
    {
        foreach (
            KeyValuePair<int, TablePresentationBinding>
                pair in tableBindings)
        {
            TablePresentationBinding binding =
                pair.Value;

            if (binding == null ||
                binding.Source == null ||
                binding.Root == null)
            {
                continue;
            }

            binding.Source.enabled =
                false;

            binding.Root.SetActive(
                true);
        }
    }

    private static MeshRenderer FindPrimitiveTableSource(
        RestaurantTable table)
    {
        if (table == null)
            return null;

        MeshRenderer[] renderers =
            table.GetComponentsInChildren<
                MeshRenderer>(true);

        MeshRenderer best =
            null;

        float bestVolume =
            0f;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            MeshRenderer renderer =
                renderers[index];

            if (!IsCubeRenderer(renderer))
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

    private void HandleOpeningStateChanged()
    {
        ApplyScenePresentation();
    }

    private void HandleEditModeChanged()
    {
        if (!isActiveAndEnabled || editModeService == null || !editModeService.isActiveAndEnabled) return;
        ApplyScenePresentation();
    }

    private static void HideRendererOnly(
        GameObject target)
    {
        if (target == null)
            return;

        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(
                true);

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            if (renderers[index] != null)
                renderers[index].enabled = false;
        }
    }

    private static void ApplyMaterialToNamedObject(
        string objectName,
        Material material)
    {
        if (string.IsNullOrWhiteSpace(objectName) ||
            material == null)
        {
            return;
        }

        ApplyMaterial(
            GameObject.Find(objectName),
            material);
    }

    private static void ApplyMaterial(
        GameObject target,
        Material material)
    {
        if (target == null ||
            material == null)
        {
            return;
        }

        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(
                true);

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer =
                renderers[index];

            if (renderer != null)
                renderer.sharedMaterial =
                    material;
        }
    }

    private static bool IsCubeRenderer(
        MeshRenderer renderer)
    {
        if (renderer == null)
            return false;

        MeshFilter filter =
            renderer.GetComponent<MeshFilter>();

        return filter != null &&
               filter.sharedMesh != null &&
               string.Equals(
                   filter.sharedMesh.name,
                   "Cube",
                   StringComparison.OrdinalIgnoreCase);
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

        string name =
            filter.sharedMesh.name;

        return string.Equals(
                   name,
                   "Cube",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   name,
                   "Sphere",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   name,
                   "Capsule",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   name,
                   "Cylinder",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   name,
                   "Plane",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   name,
                   "Quad",
                   StringComparison.OrdinalIgnoreCase);
    }

    private void CacheDependencies()
    {
        if (creationService == null)
        {
            creationService =
                GetComponent<
                    RestaurantPlaceableCreationService>();
        }

        if (creationService == null)
        {
            creationService =
                FindFirstObjectByType<
                    RestaurantPlaceableCreationService>(
                    FindObjectsInactive.Include);
        }

        if (openingService == null)
        {
            openingService =
                FindFirstObjectByType<
                    BistroBuilderNewGameOpeningService>(
                    FindObjectsInactive.Include);
        }

        if (editModeService == null)
        {
            editModeService =
                FindFirstObjectByType<
                    RestaurantEditModeService>(
                    FindObjectsInactive.Include);
        }
    }

    private void Subscribe()
    {
        if (creationService != null)
        {
            creationService.CreationStarted -= HandleCreationCommitted;
            creationService.CreationCommitted -=
                HandleCreationCommitted;

            creationService.CreationStarted += HandleCreationCommitted;
            creationService.CreationCommitted +=
                HandleCreationCommitted;
        }

        if (openingService != null)
        {
            openingService.StateChanged -=
                HandleOpeningStateChanged;

            openingService.StateChanged +=
                HandleOpeningStateChanged;
        }

        if (editModeService != null)
        {
            editModeService.EditModeEntered -=
                HandleEditModeChanged;

            editModeService.EditModeExited -=
                HandleEditModeChanged;

            editModeService.EditModeEntered +=
                HandleEditModeChanged;

            editModeService.EditModeExited +=
                HandleEditModeChanged;
        }
    }

    private void Unsubscribe()
    {
        if (creationService != null)
        {
            creationService.CreationStarted -= HandleCreationCommitted;
            creationService.CreationCommitted -=
                HandleCreationCommitted;
        }

        if (openingService != null)
        {
            openingService.StateChanged -=
                HandleOpeningStateChanged;
        }

        if (editModeService != null)
        {
            editModeService.EditModeEntered -=
                HandleEditModeChanged;

            editModeService.EditModeExited -=
                HandleEditModeChanged;
        }
    }

    private void LoadMaterials()
    {
        if (tableMaterial == null)
        {
            tableMaterial =
                Resources.Load<Material>(
                    tableMaterialResource);
        }

        if (serviceMaterial == null)
        {
            serviceMaterial =
                Resources.Load<Material>(
                    serviceMaterialResource);
        }

        if (architectureMaterial == null)
        {
            architectureMaterial =
                Resources.Load<Material>(
                    architectureMaterialResource);
        }
    }

    private sealed class TablePresentationBinding
    {
        public MeshRenderer Source { get; }
        public bool SourceWasEnabled { get; }
        public GameObject Root { get; }
        public List<MeshRenderer> Generated { get; }

        public TablePresentationBinding(
            MeshRenderer source,
            bool sourceWasEnabled,
            GameObject root,
            List<MeshRenderer> generated)
        {
            Source = source;
            SourceWasEnabled = sourceWasEnabled;
            Root = root;
            Generated = generated ??
                new List<MeshRenderer>();
        }
    }
}
