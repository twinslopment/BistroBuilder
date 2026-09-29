using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Capa de presentación para contenido whitebox que todavía conserva
/// geometría primitiva.
///
/// Principios:
/// - Nunca altera colliders, huellas, BBSIS, Seat Bays, navegación ni Save/Load.
/// - Solo actúa sobre placeholders inequívocos (malla Cube + RestaurantTable
///   o nombres explícitos *_Test / Provisional*).
/// - Cuando un asset posee geometría autorada real, no interviene.
/// - Todo visual generado cuelga del objeto lógico y desaparece con él.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Presentation/Prototype Presentation Service")]
public sealed class BistroBuilderPrototypePresentationService :
    MonoBehaviour
{
    private const string TableVisualRootName =
        "BB_Presentation_TableVisual";

    [Header("Ámbito")]
    [SerializeField] private bool skinPrimitiveTables = true;
    [SerializeField] private bool calmExplicitTestGeometry = true;
    [SerializeField] private bool skinPrimitiveWaiters = true;

    [Header("Materiales canónicos")]
    [SerializeField] private string tableMaterialResource =
        "BistroBuilder/Construction/Materials/Roble_marcos";

    [SerializeField] private string serviceMaterialResource =
        "BistroBuilder/Construction/Materials/Metal_grafito";

    [SerializeField] private RestaurantPlaceableCreationService
        creationService;

    [SerializeField] private BistroBuilderNewGameOpeningService
        openingService;

    private readonly HashSet<int> skinnedTableIds =
        new HashSet<int>();

    private Material tableMaterial;
    private Material serviceMaterial;

    private void Awake()
    {
        CacheDependencies();
        LoadMaterials();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void Start()
    {
        ApplyScenePresentation();
    }

    private void OnDisable()
    {
        Unsubscribe();
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

            for (int index = 0; index < tables.Length; index++)
            {
                TrySkinPrimitiveTable(tables[index]);
            }
        }

        if (skinPrimitiveWaiters)
        {
            Waiter[] waiters =
                FindObjectsByType<Waiter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int index = 0; index < waiters.Length; index++)
            {
                StylePrimitiveActor(
                    waiters[index] != null
                        ? waiters[index].gameObject
                        : null);
            }
        }

        if (calmExplicitTestGeometry)
            CalmExplicitPlaceholders();

        ApplyOpeningPhasePresentation();
    }

    private void HandleCreationCommitted(
        RestaurantPlaceableObject placeable)
    {
        if (placeable == null)
            return;

        RestaurantTable table =
            placeable.GetComponent<RestaurantTable>();

        if (table != null)
            TrySkinPrimitiveTable(table);
    }

    private void TrySkinPrimitiveTable(
        RestaurantTable table)
    {
        if (table == null)
            return;

        int instanceId =
            table.GetInstanceID();

        if (skinnedTableIds.Contains(instanceId))
            return;

        Transform existing =
            table.transform.Find(TableVisualRootName);

        if (existing != null)
        {
            skinnedTableIds.Add(instanceId);
            return;
        }

        MeshFilter sourceFilter =
            table.GetComponent<MeshFilter>();

        MeshRenderer sourceRenderer =
            table.GetComponent<MeshRenderer>();

        if (sourceFilter == null ||
            sourceRenderer == null ||
            sourceFilter.sharedMesh == null ||
            !string.Equals(
                sourceFilter.sharedMesh.name,
                "Cube",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Material material =
            tableMaterial != null
                ? tableMaterial
                : sourceRenderer.sharedMaterial;

        if (material == null)
            return;

        GameObject root =
            new GameObject(TableVisualRootName);

        root.layer =
            table.gameObject.layer;

        root.transform.SetParent(
            table.transform,
            false);

        AddTablePart(
            root.transform,
            "Top",
            sourceFilter.sharedMesh,
            material,
            new Vector3(0f, 0.28f, 0f),
            new Vector3(0.96f, 0.085f, 0.90f));

        AddTablePart(
            root.transform,
            "Leg_FL",
            sourceFilter.sharedMesh,
            material,
            new Vector3(-0.40f, -0.09f, 0.35f),
            new Vector3(0.055f, 0.66f, 0.11f));

        AddTablePart(
            root.transform,
            "Leg_FR",
            sourceFilter.sharedMesh,
            material,
            new Vector3(0.40f, -0.09f, 0.35f),
            new Vector3(0.055f, 0.66f, 0.11f));

        AddTablePart(
            root.transform,
            "Leg_BL",
            sourceFilter.sharedMesh,
            material,
            new Vector3(-0.40f, -0.09f, -0.35f),
            new Vector3(0.055f, 0.66f, 0.11f));

        AddTablePart(
            root.transform,
            "Leg_BR",
            sourceFilter.sharedMesh,
            material,
            new Vector3(0.40f, -0.09f, -0.35f),
            new Vector3(0.055f, 0.66f, 0.11f));

        /*
         * El collider y el MeshFilter originales siguen intactos porque son
         * parte del contrato lógico del placeholder. Solo se oculta su cubo
         * visual monolítico.
         */
        sourceRenderer.enabled = false;

        skinnedTableIds.Add(instanceId);
    }

    private static void AddTablePart(
        Transform parent,
        string name,
        Mesh mesh,
        Material material,
        Vector3 localPosition,
        Vector3 localScale)
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
    }

    private void StylePrimitiveActor(
        GameObject actor)
    {
        if (actor == null ||
            serviceMaterial == null)
        {
            return;
        }

        MeshFilter filter =
            actor.GetComponent<MeshFilter>();

        MeshRenderer renderer =
            actor.GetComponent<MeshRenderer>();

        if (filter == null ||
            renderer == null ||
            filter.sharedMesh == null ||
            !string.Equals(
                filter.sharedMesh.name,
                "Capsule",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        renderer.sharedMaterial =
            serviceMaterial;
    }

    private void CalmExplicitPlaceholders()
    {
        HideRendererOnly(
            GameObject.Find("PlacementObstacle_Test"));

        HideRendererOnly(
            GameObject.Find("Kitchen_Test"));

        ApplyMaterialToNamedObject(
            "ProvisionalCounter",
            tableMaterial);

        GameObject[] all =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0; index < all.Length; index++)
        {
            GameObject candidate =
                all[index];

            if (candidate == null)
                continue;

            if (candidate.name.StartsWith(
                    "ProvisionalStool",
                    StringComparison.Ordinal))
            {
                ApplyMaterial(
                    candidate,
                    serviceMaterial != null
                        ? serviceMaterial
                        : tableMaterial);
            }
        }
    }

    private void ApplyOpeningPhasePresentation()
    {
        if (openingService == null)
            return;

        bool showWaiterVisuals =
            openingService.Phase !=
                BistroBuilderNewGamePhase.StartMenu &&
            openingService.Phase !=
                BistroBuilderNewGamePhase.InitialSetup;

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

            Renderer[] renderers =
                waiter.GetComponentsInChildren<Renderer>(
                    true);

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer =
                    renderers[rendererIndex];

                if (renderer != null)
                    renderer.enabled =
                        showWaiterVisuals;
            }
        }
    }

    private void HandleOpeningStateChanged()
    {
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

        for (int index = 0; index < renderers.Length; index++)
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

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer =
                renderers[index];

            if (renderer != null)
                renderer.sharedMaterial = material;
        }
    }

    private void CacheDependencies()
    {
        if (creationService == null)
        {
            creationService =
                GetComponent<RestaurantPlaceableCreationService>();
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
    }

    private void Subscribe()
    {
        if (creationService != null)
        {
            creationService.CreationCommitted -=
                HandleCreationCommitted;

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
    }

    private void Unsubscribe()
    {
        if (creationService != null)
        {
            creationService.CreationCommitted -=
                HandleCreationCommitted;
        }

        if (openingService != null)
        {
            openingService.StateChanged -=
                HandleOpeningStateChanged;
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
    }
}
