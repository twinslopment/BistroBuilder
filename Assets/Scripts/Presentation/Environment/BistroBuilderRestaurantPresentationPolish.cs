using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Capa de presentación no autoritativa del restaurante.
///
/// Convierte placeholders visuales heredados en una lectura de producto sin
/// modificar colliders, footprints, BBSIS, navegación, economía ni Save/Load.
/// El gameplay conserva siempre los mismos GameObjects y componentes lógicos.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Presentation/Restaurant Presentation Polish")]
public sealed class BistroBuilderRestaurantPresentationPolish : MonoBehaviour
{
    private const string RuntimeObjectName =
        "BB_RestaurantPresentationPolish";

    private const string WoodResource =
        "BistroBuilder/Construction/Materials/Roble_marcos";

    private const string GraphiteResource =
        "BistroBuilder/Construction/Materials/Metal_grafito";

    private const string PlasterResource =
        "BistroBuilder/Construction/Materials/Enlucido_calido";

    private const string PresentationRootName =
        "BB_PresentationSkin";

    private Material woodMaterial;
    private Material graphiteMaterial;
    private Material plasterMaterial;
    private Mesh unitCubeMesh;

    private RestaurantEditModeService editModeService;
    private RestaurantPlaceableRegistry placeableRegistry;

    private readonly List<MeshRenderer>
        primitiveWaiterRenderers =
            new List<MeshRenderer>(16);

    private static bool bootstrapRegistered;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallRuntime()
    {
        BistroBuilderRestaurantPresentationPolish existing =
            FindFirstObjectByType<
                BistroBuilderRestaurantPresentationPolish>();

        if (existing != null)
            return;

        GameObject root =
            new GameObject(
                RuntimeObjectName);

        DontDestroyOnLoad(root);

        root.AddComponent<
            BistroBuilderRestaurantPresentationPolish>();
    }

    private void Awake()
    {
        if (!bootstrapRegistered)
        {
            SceneManager.sceneLoaded +=
                HandleSceneLoaded;

            bootstrapRegistered = true;
        }

        LoadMaterials();
        EnsureUnitCubeMesh();
    }

    private void Start()
    {
        StartCoroutine(
            ApplyNextFrame());
    }

    private void OnDestroy()
    {
        if (editModeService != null)
        {
            editModeService.EditModeEntered -=
                HandleEditModeEntered;

            editModeService.EditModeExited -=
                HandleEditModeExited;
        }

        if (placeableRegistry != null)
        {
            placeableRegistry.PlaceableRegistered -=
                HandlePlaceableRegistered;
        }

        if (bootstrapRegistered)
        {
            SceneManager.sceneLoaded -=
                HandleSceneLoaded;

            bootstrapRegistered = false;
        }

        if (unitCubeMesh != null)
        {
            Destroy(unitCubeMesh);
            unitCubeMesh = null;
        }
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        StartCoroutine(
            ApplyNextFrame());
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;

        LoadMaterials();
        EnsureUnitCubeMesh();

        ApplyTablePresentation();
        ApplyPrototypePresentationCleanup();
        EnsureSitePlinth();
        BindPlaceableRegistry();
        BindEditModeVisibility();
    }

    private void LoadMaterials()
    {
        if (woodMaterial == null)
            woodMaterial =
                Resources.Load<Material>(
                    WoodResource);

        if (graphiteMaterial == null)
            graphiteMaterial =
                Resources.Load<Material>(
                    GraphiteResource);

        if (plasterMaterial == null)
            plasterMaterial =
                Resources.Load<Material>(
                    PlasterResource);
    }

    /// <summary>
    /// Sustituye únicamente la malla visual cúbica heredada de una mesa por
    /// una composición de tablero y patas. El collider, transform raíz,
    /// RestaurantTable, Placeable, seating y footprint no se modifican.
    /// </summary>
    private void ApplyTablePresentation()
    {
        if (woodMaterial == null ||
            graphiteMaterial == null ||
            unitCubeMesh == null)
        {
            return;
        }

        RestaurantTable[] tables =
            FindObjectsByType<RestaurantTable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0;
             index < tables.Length;
             index++)
        {
            ApplyTablePresentation(
                tables[index]);
        }
    }

    private void ApplyTablePresentation(
        RestaurantTable table)
    {
        if (table == null ||
            woodMaterial == null ||
            graphiteMaterial == null ||
            unitCubeMesh == null)
        {
            return;
        }

        Transform root =
            table.transform;

        if (root.Find(
                PresentationRootName) != null)
        {
            return;
        }

        MeshFilter sourceFilter =
            root.GetComponent<MeshFilter>();

        MeshRenderer sourceRenderer =
            root.GetComponent<MeshRenderer>();

        if (!IsPrimitiveCube(
                sourceFilter,
                sourceRenderer))
        {
            return;
        }

        GameObject skin =
            CreateSkinRoot(root);

        MeshRenderer tabletopRenderer =
            CreateVisualCube(
                skin.transform,
                "Top",
                new Vector3(
                    0f,
                    0.26f,
                    0f),
                new Vector3(
                    0.92f,
                    0.08f,
                    0.90f),
                woodMaterial);

        BistroBuilderPresentationTableAccent accent =
            skin.AddComponent<
                BistroBuilderPresentationTableAccent>();

        accent.Initialize(
            table,
            tabletopRenderer);

        float legX = 0.39f;
        float legZ = 0.34f;
        float legY = -0.14f;

        CreateVisualCube(
            skin.transform,
            "Leg_FL",
            new Vector3(
                -legX,
                legY,
                legZ),
            new Vector3(
                0.045f,
                0.72f,
                0.07f),
            graphiteMaterial);

        CreateVisualCube(
            skin.transform,
            "Leg_FR",
            new Vector3(
                legX,
                legY,
                legZ),
            new Vector3(
                0.045f,
                0.72f,
                0.07f),
            graphiteMaterial);

        CreateVisualCube(
            skin.transform,
            "Leg_BL",
            new Vector3(
                -legX,
                legY,
                -legZ),
            new Vector3(
                0.045f,
                0.72f,
                0.07f),
            graphiteMaterial);

        CreateVisualCube(
            skin.transform,
            "Leg_BR",
            new Vector3(
                legX,
                legY,
                -legZ),
            new Vector3(
                0.045f,
                0.72f,
                0.07f),
            graphiteMaterial);

        sourceRenderer.enabled =
            false;
    }

    /// <summary>
    /// Limpieza deliberadamente restringida a la escena prototipo actual.
    /// Elimina geometría de diagnóstico de la presentación sin tocar su
    /// lógica. Cuando esos placeholders se sustituyan por assets canónicos,
    /// este bloque deja de actuar de forma natural.
    /// </summary>
    private void ApplyPrototypePresentationCleanup()
    {
        if (!string.Equals(
                SceneManager.GetActiveScene().name,
                "Prototype_Restaurant",
                StringComparison.Ordinal))
        {
            return;
        }

        HideStandaloneObstacleVisuals();
        SkinNamedPrototypeBlock(
            "Kitchen_Test",
            isKitchen: true);
        SkinNamedPrototypeBlock(
            "ProvisionalCounter",
            isKitchen: false);
        SkinPrototypeStools();
        CachePrimitiveWaiterRenderers();
    }

    private void EnsureSitePlinth()
    {
        if (!string.Equals(
                SceneManager.GetActiveScene().name,
                "Prototype_Restaurant",
                StringComparison.Ordinal) ||
            graphiteMaterial == null ||
            unitCubeMesh == null)
        {
            return;
        }

        GameObject floor =
            GameObject.Find(
                "Floor_Test");

        if (floor == null)
            return;

        Transform existing =
            floor.transform.Find(
                "BB_PresentationSitePlinth");

        if (existing != null)
            return;

        Renderer floorRenderer =
            floor.GetComponent<Renderer>();

        if (floorRenderer == null)
            return;

        Bounds bounds =
            floorRenderer.bounds;

        GameObject plinth =
            new GameObject(
                "BB_PresentationSitePlinth");

        plinth.layer =
            LayerMask.NameToLayer(
                "Ignore Raycast") >= 0
                ? LayerMask.NameToLayer(
                    "Ignore Raycast")
                : 2;

        plinth.transform.position =
            new Vector3(
                bounds.center.x,
                bounds.min.y - 0.035f,
                bounds.center.z);

        plinth.transform.rotation =
            Quaternion.identity;

        plinth.transform.localScale =
            new Vector3(
                bounds.size.x + 0.12f,
                0.06f,
                bounds.size.z + 0.12f);

        MeshFilter filter =
            plinth.AddComponent<MeshFilter>();

        filter.sharedMesh =
            unitCubeMesh;

        MeshRenderer renderer =
            plinth.AddComponent<MeshRenderer>();

        renderer.sharedMaterial =
            graphiteMaterial;

        renderer.shadowCastingMode =
            ShadowCastingMode.On;

        renderer.receiveShadows =
            true;

        renderer.lightProbeUsage =
            LightProbeUsage.BlendProbes;

        renderer.reflectionProbeUsage =
            ReflectionProbeUsage.BlendProbes;
    }

    private void HideStandaloneObstacleVisuals()
    {
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
                obstacle.GetComponent<
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
                if (renderers[rendererIndex] != null)
                {
                    renderers[rendererIndex].enabled =
                        false;
                }
            }
        }
    }

    private void SkinNamedPrototypeBlock(
        string objectName,
        bool isKitchen)
    {
        GameObject target =
            GameObject.Find(objectName);

        if (target == null ||
            target.transform.Find(
                PresentationRootName) != null)
        {
            return;
        }

        MeshFilter filter =
            target.GetComponent<MeshFilter>();

        MeshRenderer renderer =
            target.GetComponent<MeshRenderer>();

        if (!IsPrimitiveCube(
                filter,
                renderer))
        {
            return;
        }

        if (woodMaterial == null ||
            graphiteMaterial == null ||
            plasterMaterial == null)
        {
            return;
        }

        GameObject skin =
            CreateSkinRoot(
                target.transform);

        if (isKitchen)
        {
            CreateVisualCube(
                skin.transform,
                "Cabinet",
                new Vector3(
                    0f,
                    -0.10f,
                    0f),
                new Vector3(
                    0.94f,
                    0.70f,
                    0.94f),
                plasterMaterial);

            CreateVisualCube(
                skin.transform,
                "Worktop",
                new Vector3(
                    0f,
                    0.28f,
                    0f),
                new Vector3(
                    0.98f,
                    0.06f,
                    0.98f),
                graphiteMaterial);
        }
        else
        {
            CreateVisualCube(
                skin.transform,
                "CounterBody",
                new Vector3(
                    0f,
                    -0.06f,
                    0f),
                new Vector3(
                    0.96f,
                    0.86f,
                    0.92f),
                graphiteMaterial);

            CreateVisualCube(
                skin.transform,
                "CounterTop",
                new Vector3(
                    0f,
                    0.40f,
                    0f),
                new Vector3(
                    1.00f,
                    0.07f,
                    1.04f),
                woodMaterial);
        }

        renderer.enabled =
            false;
    }

    private void SkinPrototypeStools()
    {
        Transform[] all =
            FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0;
             index < all.Length;
             index++)
        {
            Transform root =
                all[index];

            if (root == null ||
                !string.Equals(
                    root.name,
                    "ProvisionalStool",
                    StringComparison.Ordinal) ||
                root.Find(
                    PresentationRootName) != null)
            {
                continue;
            }

            MeshRenderer sourceRenderer =
                root.GetComponent<MeshRenderer>();

            MeshFilter sourceFilter =
                root.GetComponent<MeshFilter>();

            if (sourceRenderer == null ||
                sourceFilter == null ||
                sourceFilter.sharedMesh == null ||
                woodMaterial == null ||
                graphiteMaterial == null)
            {
                continue;
            }

            GameObject skin =
                CreateSkinRoot(root);

            CreateVisualCube(
                skin.transform,
                "Seat",
                new Vector3(
                    0f,
                    0.40f,
                    0f),
                new Vector3(
                    0.88f,
                    0.10f,
                    0.88f),
                woodMaterial);

            const float offset = 0.28f;

            Vector3 legSize =
                new Vector3(
                    0.10f,
                    0.76f,
                    0.10f);

            CreateVisualCube(
                skin.transform,
                "Leg_FL",
                new Vector3(
                    -offset,
                    0f,
                    offset),
                legSize,
                graphiteMaterial);

            CreateVisualCube(
                skin.transform,
                "Leg_FR",
                new Vector3(
                    offset,
                    0f,
                    offset),
                legSize,
                graphiteMaterial);

            CreateVisualCube(
                skin.transform,
                "Leg_BL",
                new Vector3(
                    -offset,
                    0f,
                    -offset),
                legSize,
                graphiteMaterial);

            CreateVisualCube(
                skin.transform,
                "Leg_BR",
                new Vector3(
                    offset,
                    0f,
                    -offset),
                legSize,
                graphiteMaterial);

            sourceRenderer.enabled =
                false;
        }
    }

    private void BindPlaceableRegistry()
    {
        RestaurantPlaceableRegistry found =
            FindFirstObjectByType<
                RestaurantPlaceableRegistry>();

        if (ReferenceEquals(
                placeableRegistry,
                found))
        {
            return;
        }

        if (placeableRegistry != null)
        {
            placeableRegistry.PlaceableRegistered -=
                HandlePlaceableRegistered;
        }

        placeableRegistry =
            found;

        if (placeableRegistry != null)
        {
            placeableRegistry.PlaceableRegistered +=
                HandlePlaceableRegistered;
        }
    }

    private void HandlePlaceableRegistered(
        RestaurantPlaceableObject placeable)
    {
        if (placeable == null)
            return;

        RestaurantTable table =
            placeable.GetComponent<
                RestaurantTable>();

        if (table != null)
        {
            ApplyTablePresentation(
                table);
        }
    }

    private void BindEditModeVisibility()
    {
        RestaurantEditModeService found =
            FindFirstObjectByType<
                RestaurantEditModeService>();

        if (!ReferenceEquals(
                editModeService,
                found))
        {
            if (editModeService != null)
            {
                editModeService.EditModeEntered -=
                    HandleEditModeEntered;

                editModeService.EditModeExited -=
                    HandleEditModeExited;
            }

            editModeService =
                found;

            if (editModeService != null)
            {
                editModeService.EditModeEntered +=
                    HandleEditModeEntered;

                editModeService.EditModeExited +=
                    HandleEditModeExited;
            }
        }

        ApplyPrimitiveWaiterVisibility(
            editModeService != null &&
            editModeService.IsEditModeActive);
    }

    private void CachePrimitiveWaiterRenderers()
    {
        primitiveWaiterRenderers.Clear();

        Waiter[] waiters =
            FindObjectsByType<Waiter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0;
             index < waiters.Length;
             index++)
        {
            Waiter waiter =
                waiters[index];

            if (waiter == null)
                continue;

            MeshFilter filter =
                waiter.GetComponent<MeshFilter>();

            MeshRenderer renderer =
                waiter.GetComponent<MeshRenderer>();

            if (filter == null ||
                renderer == null ||
                filter.sharedMesh == null)
            {
                continue;
            }

            string meshName =
                filter.sharedMesh.name;

            if (!string.Equals(
                    meshName,
                    "Capsule",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            primitiveWaiterRenderers.Add(
                renderer);
        }
    }

    private void HandleEditModeEntered()
    {
        ApplyPrimitiveWaiterVisibility(
            true);
    }

    private void HandleEditModeExited()
    {
        ApplyPrimitiveWaiterVisibility(
            false);
    }

    private void ApplyPrimitiveWaiterVisibility(
        bool editing)
    {
        for (int index = 0;
             index < primitiveWaiterRenderers.Count;
             index++)
        {
            MeshRenderer renderer =
                primitiveWaiterRenderers[index];

            if (renderer != null)
            {
                renderer.enabled =
                    !editing;
            }
        }
    }

    private static bool IsPrimitiveCube(
        MeshFilter filter,
        MeshRenderer renderer)
    {
        return filter != null &&
               renderer != null &&
               filter.sharedMesh != null &&
               string.Equals(
                   filter.sharedMesh.name,
                   "Cube",
                   StringComparison.OrdinalIgnoreCase);
    }

    private GameObject CreateSkinRoot(
        Transform parent)
    {
        GameObject root =
            new GameObject(
                PresentationRootName);

        root.layer =
            parent.gameObject.layer;

        root.transform.SetParent(
            parent,
            false);

        root.transform.localPosition =
            Vector3.zero;

        root.transform.localRotation =
            Quaternion.identity;

        root.transform.localScale =
            Vector3.one;

        return root;
    }

    private MeshRenderer CreateVisualCube(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject go =
            new GameObject(name);

        go.layer =
            parent.gameObject.layer;

        go.transform.SetParent(
            parent,
            false);

        go.transform.localPosition =
            localPosition;

        go.transform.localRotation =
            Quaternion.identity;

        go.transform.localScale =
            localScale;

        MeshFilter filter =
            go.AddComponent<MeshFilter>();

        filter.sharedMesh =
            unitCubeMesh;

        MeshRenderer renderer =
            go.AddComponent<MeshRenderer>();

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

        return renderer;
    }

    private void EnsureUnitCubeMesh()
    {
        if (unitCubeMesh != null)
            return;

        Vector3[] vertices =
        {
            // Front
            new Vector3(-0.5f,-0.5f, 0.5f),
            new Vector3( 0.5f,-0.5f, 0.5f),
            new Vector3( 0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            // Back
            new Vector3( 0.5f,-0.5f,-0.5f),
            new Vector3(-0.5f,-0.5f,-0.5f),
            new Vector3(-0.5f, 0.5f,-0.5f),
            new Vector3( 0.5f, 0.5f,-0.5f),
            // Left
            new Vector3(-0.5f,-0.5f,-0.5f),
            new Vector3(-0.5f,-0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f,-0.5f),
            // Right
            new Vector3( 0.5f,-0.5f, 0.5f),
            new Vector3( 0.5f,-0.5f,-0.5f),
            new Vector3( 0.5f, 0.5f,-0.5f),
            new Vector3( 0.5f, 0.5f, 0.5f),
            // Top
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3( 0.5f, 0.5f, 0.5f),
            new Vector3( 0.5f, 0.5f,-0.5f),
            new Vector3(-0.5f, 0.5f,-0.5f),
            // Bottom
            new Vector3(-0.5f,-0.5f,-0.5f),
            new Vector3( 0.5f,-0.5f,-0.5f),
            new Vector3( 0.5f,-0.5f, 0.5f),
            new Vector3(-0.5f,-0.5f, 0.5f)
        };

        int[] triangles =
        {
             0, 1, 2,  0, 2, 3,
             4, 5, 6,  4, 6, 7,
             8, 9,10,  8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23
        };

        Vector3[] normals =
        {
            Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward,
            Vector3.back,Vector3.back,Vector3.back,Vector3.back,
            Vector3.left,Vector3.left,Vector3.left,Vector3.left,
            Vector3.right,Vector3.right,Vector3.right,Vector3.right,
            Vector3.up,Vector3.up,Vector3.up,Vector3.up,
            Vector3.down,Vector3.down,Vector3.down,Vector3.down
        };

        Vector2[] uv =
        {
            new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1),
            new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1),
            new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1),
            new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1),
            new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1),
            new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)
        };

        unitCubeMesh =
            new Mesh
            {
                name =
                    "BB_PresentationUnitCube",
                hideFlags =
                    HideFlags.HideAndDontSave
            };

        unitCubeMesh.vertices =
            vertices;

        unitCubeMesh.triangles =
            triangles;

        unitCubeMesh.normals =
            normals;

        unitCubeMesh.uv =
            uv;

        unitCubeMesh.RecalculateBounds();
    }
}


/// <summary>
/// Proyección visual de estado para las mesas estilizadas de Presentation.
/// Escucha la autoridad RestaurantTable y aplica un acento muy leve únicamente
/// al tablero. No guarda estado ni participa en reglas de servicio.
/// </summary>
internal sealed class BistroBuilderPresentationTableAccent : MonoBehaviour
{
    private static readonly int BaseColorPropertyId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorPropertyId =
        Shader.PropertyToID("_Color");

    private RestaurantTable table;
    private MeshRenderer targetRenderer;
    private MaterialPropertyBlock block;
    private Color baseColor = Color.white;

    public void Initialize(
        RestaurantTable sourceTable,
        MeshRenderer renderer)
    {
        Unsubscribe();

        table = sourceTable;
        targetRenderer = renderer;

        block ??=
            new MaterialPropertyBlock();

        CaptureBaseColor();
        Subscribe();

        if (table != null)
        {
            ApplyState(
                table.CurrentState);
        }
    }

    private void OnEnable()
    {
        Subscribe();

        if (table != null)
            ApplyState(table.CurrentState);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (table == null)
            return;

        table.StateChanged -=
            HandleStateChanged;

        table.StateChanged +=
            HandleStateChanged;
    }

    private void Unsubscribe()
    {
        if (table != null)
        {
            table.StateChanged -=
                HandleStateChanged;
        }
    }

    private void HandleStateChanged(
        RestaurantTable source,
        TableState state)
    {
        ApplyState(state);
    }

    private void CaptureBaseColor()
    {
        if (targetRenderer == null ||
            targetRenderer.sharedMaterial == null)
        {
            baseColor =
                Color.white;

            return;
        }

        Material material =
            targetRenderer.sharedMaterial;

        if (material.HasProperty(
                BaseColorPropertyId))
        {
            baseColor =
                material.GetColor(
                    BaseColorPropertyId);
        }
        else if (material.HasProperty(
                     ColorPropertyId))
        {
            baseColor =
                material.GetColor(
                    ColorPropertyId);
        }
        else
        {
            baseColor =
                Color.white;
        }
    }

    private void ApplyState(
        TableState state)
    {
        if (targetRenderer == null)
            return;

        Color accent =
            ResolveAccent(
                state);

        float strength =
            state == TableState.Free
                ? 0f
                : 0.10f;

        Color resolved =
            Color.Lerp(
                baseColor,
                accent,
                strength);

        resolved.a =
            baseColor.a;

        block.Clear();

        Material material =
            targetRenderer.sharedMaterial;

        if (material != null &&
            material.HasProperty(
                BaseColorPropertyId))
        {
            block.SetColor(
                BaseColorPropertyId,
                resolved);
        }
        else
        {
            block.SetColor(
                ColorPropertyId,
                resolved);
        }

        targetRenderer.SetPropertyBlock(
            block);
    }

    private static Color ResolveAccent(
        TableState state)
    {
        switch (state)
        {
            case TableState.WaitingForWaiter:
                return new Color(
                    0.82f,
                    0.69f,
                    0.30f,
                    1f);

            case TableState.TakingOrder:
                return new Color(
                    0.84f,
                    0.55f,
                    0.28f,
                    1f);

            case TableState.WaitingForFood:
                return new Color(
                    0.72f,
                    0.36f,
                    0.31f,
                    1f);

            case TableState.Eating:
                return new Color(
                    0.32f,
                    0.58f,
                    0.52f,
                    1f);

            case TableState.WaitingForBill:
                return new Color(
                    0.50f,
                    0.48f,
                    0.68f,
                    1f);

            case TableState.Paying:
                return new Color(
                    0.55f,
                    0.42f,
                    0.66f,
                    1f);

            case TableState.Dirty:
                return new Color(
                    0.42f,
                    0.42f,
                    0.40f,
                    1f);

            default:
                return Color.white;
        }
    }
}
