using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only premises pass for the starting restaurant.
/// Uses approved construction materials/assets and keeps gameplay authorities intact.
/// It never creates placeables or mutates SAVIC/BBSIS ownership.
/// </summary>
public static class BistroBuilderPremisesPresentationRuntime
{
    public const string RootName = "BB_PremisesPresentation";

    private const float FullWallHeight = 2.8f;
    private const float CutawayWallHeight = 0.9f;
    private const float WallThickness = 0.12f;

    private static Material originalFloorMaterial;
    private static bool originalFloorMaterialCaptured;
    private static Mesh cachedCubeMesh;
    private static Mesh cachedCylinderMesh;

    public static bool Apply(
        BistroBuilderStartingPremisesProfile profile,
        out string error)
    {
        error = string.Empty;

        GameObject floor = GameObject.Find("Floor_Test");
        Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
        if (floorRenderer == null)
        {
            error = "No se encontró el suelo canónico del local para presentar la nueva partida.";
            return false;
        }

        BistroBuilderConstructionAssetKit kit = BistroBuilderConstructionAssetKit.Load();
        if (kit == null || kit.wallMaterial == null || kit.floorMaterial == null)
        {
            error = "Falta el kit visual de construcción aprobado.";
            return false;
        }

        Clear();

        originalFloorMaterial = floorRenderer.sharedMaterial;
        originalFloorMaterialCaptured = true;

        floorRenderer.enabled = true;
        floorRenderer.sharedMaterial = kit.floorMaterial;

        var root = new GameObject(RootName);
        BuildEnvelope(root.transform, floorRenderer.bounds, kit);

        // El obstáculo técnico sigue activo y participando en validación.
        // Solo se elimina su cubo de depuración visible.
        SetTechnicalRendererVisible(
            "PlacementObstacle_Test",
            false);

        if (profile != BistroBuilderStartingPremisesProfile.Empty)
        {
            UpgradeKitchenAuthorityVisual(kit);
            UpgradeLegacyBarVisuals(kit);
        }

        ReconcilePrototypeFallback();
        return true;
    }

    public static void Clear()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            DestroyPresentationObject(existing);

        RemovePresentationChild("Kitchen_Test", "BB_PresentationKitchen");
        RemoveBarPresentationChildren();

        SetTechnicalRendererVisible("PlacementObstacle_Test", true);
        SetTechnicalRendererVisible("Kitchen_Test", true);
        SetLegacyPrimitiveVisible("ProvisionalCounter", true);
        SetLegacyPrimitiveVisible("ProvisionalStool", true);

        if (originalFloorMaterialCaptured)
        {
            GameObject floor = GameObject.Find("Floor_Test");
            Renderer renderer = floor != null ? floor.GetComponent<Renderer>() : null;
            if (renderer != null)
                renderer.sharedMaterial = originalFloorMaterial;

            originalFloorMaterial = null;
            originalFloorMaterialCaptured = false;
        }

        ReconcilePrototypeFallback();
    }

    private static void ReconcilePrototypeFallback()
    {
        BistroBuilderPrototypePresentationService fallback =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderPrototypePresentationService>(
                FindObjectsInactive.Include);

        if (fallback != null)
        {
            fallback.ApplyScenePresentation();
        }
    }

    private static void BuildEnvelope(
        Transform root,
        Bounds bounds,
        BistroBuilderConstructionAssetKit kit)
    {
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minZ = bounds.min.z;
        float maxZ = bounds.max.z;
        float baseY = bounds.max.y;

        GameObject entranceObject = GameObject.Find("RestaurantEntrancePoint");
        float entranceX = entranceObject != null
            ? Mathf.Clamp(entranceObject.transform.position.x, minX + 1.2f, maxX - 1.2f)
            : Mathf.Lerp(minX, maxX, 0.30f);

        Material exterior = Resources.Load<Material>(
            "BistroBuilder/Construction/Materials/Metal_grafito");
        CreateBox(
            root,
            "ExteriorApron",
            new Vector3(bounds.center.x, bounds.min.y - 0.09f, bounds.center.z),
            new Vector3(bounds.size.x + 3.0f, 0.12f, bounds.size.z + 3.0f),
            exterior != null ? exterior : kit.wallMaterial);

        CreateBox(
            root,
            "EntranceApron",
            new Vector3(entranceX, bounds.min.y - 0.025f, minZ - 1.35f),
            new Vector3(3.0f, 0.07f, 2.7f),
            kit.floorMaterial);

        CreateWall(
            root,
            "NorthWall",
            new Vector2(minX, maxZ),
            new Vector2(maxX, maxZ),
            baseY,
            FullWallHeight,
            kit.wallMaterial,
            Windows("north", 0.22f, 0.50f, 0.78f));

        // Mantiene el lateral oeste en cutaway: la envolvente existe, pero
        // no tapa el comedor desde la cámara isométrica principal.
        CreateWall(
            root,
            "WestCutaway",
            new Vector2(minX, minZ),
            new Vector2(minX, maxZ),
            baseY,
            CutawayWallHeight,
            kit.wallMaterial,
            null);

        CreateWall(
            root,
            "EastWall",
            new Vector2(maxX, maxZ),
            new Vector2(maxX, minZ),
            baseY,
            FullWallHeight,
            kit.wallMaterial,
            Windows("east", 0.30f, 0.64f));

        float halfPortal = 0.92f;
        if (entranceX - halfPortal > minX + 0.15f)
        {
            CreateWall(
                root,
                "SouthCutawayLeft",
                new Vector2(minX, minZ),
                new Vector2(entranceX - halfPortal, minZ),
                baseY,
                CutawayWallHeight,
                kit.wallMaterial,
                null);
        }

        if (entranceX + halfPortal < maxX - 0.15f)
        {
            CreateWall(
                root,
                "SouthCutawayRight",
                new Vector2(entranceX + halfPortal, minZ),
                new Vector2(maxX, minZ),
                baseY,
                CutawayWallHeight,
                kit.wallMaterial,
                null);
        }

        var portalOpening = new List<BistroBuilderOpeningRecord>
        {
            new BistroBuilderOpeningRecord
            {
                openingId = new BistroBuilderEditId("presentation-entrance-door"),
                hostWallId = new BistroBuilderEditId("presentation-south-portal"),
                axisPosition01 = 0.5f,
                width = 1.25f,
                bottomElevation = 0f,
                height = 2.15f,
                openingType = "door",
                fillDefinitionId = "door.oak"
            }
        };

        CreateWall(
            root,
            "SouthEntrancePortal",
            new Vector2(entranceX - halfPortal, minZ),
            new Vector2(entranceX + halfPortal, minZ),
            baseY,
            FullWallHeight,
            kit.wallMaterial,
            portalOpening,
            new BistroBuilderEditId("presentation-south-portal"));
    }

    private static List<BistroBuilderOpeningRecord> Windows(
        string side,
        params float[] positions)
    {
        var openings = new List<BistroBuilderOpeningRecord>();
        var host = new BistroBuilderEditId("presentation-" + side);
        for (int i = 0; i < positions.Length; i++)
        {
            openings.Add(new BistroBuilderOpeningRecord
            {
                openingId = new BistroBuilderEditId(
                    "presentation-" + side + "-window-" + i.ToString("D2")),
                hostWallId = host,
                axisPosition01 = positions[i],
                width = 1.65f,
                bottomElevation = 0.82f,
                height = 1.28f,
                openingType = "window",
                fillDefinitionId = "window.graphite"
            });
        }
        return openings;
    }

    private static void CreateWall(
        Transform parent,
        string name,
        Vector2 start,
        Vector2 end,
        float baseElevation,
        float height,
        Material material,
        IReadOnlyList<BistroBuilderOpeningRecord> openings,
        BistroBuilderEditId explicitId = default)
    {
        BistroBuilderEditId wallId = explicitId.IsValid
            ? explicitId
            : new BistroBuilderEditId(
                "presentation-" + name.Replace(" ", string.Empty).ToLowerInvariant());

        if (openings is List<BistroBuilderOpeningRecord> list)
        {
            for (int i = 0; i < list.Count; i++)
                list[i].hostWallId = wallId;
        }

        var wall = new BistroBuilderWallRecord
        {
            wallId = wallId,
            buildPlaneId = "default",
            axisStart = start,
            axisEnd = end,
            baseElevation = baseElevation,
            height = height,
            thickness = WallThickness,
            wallDefinitionId = "wall.premises-presentation"
        };

        Mesh mesh = BistroBuilderWallGeometryBuilder.Build(wall, openings);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Vector2 axis = end - start;
        Vector3 direction = new Vector3(axis.x, 0f, axis.y).normalized;
        go.transform.position = new Vector3(start.x, baseElevation, start.y);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;

        if (openings != null && openings.Count > 0)
        {
            BistroBuilderOpeningVisuals.Build(go.transform, wall, openings, material);
            DisablePresentationColliders(go.transform);
        }
    }

    private static void SetTechnicalRendererVisible(
        string objectName,
        bool visible)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null) return;

        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].enabled = visible;
    }

    private static void UpgradeKitchenAuthorityVisual(
        BistroBuilderConstructionAssetKit kit)
    {
        GameObject kitchen = GameObject.Find("Kitchen_Test");
        if (kitchen == null) return;

        MeshRenderer legacyRenderer = kitchen.GetComponent<MeshRenderer>();
        if (legacyRenderer != null) legacyRenderer.enabled = false;

        Transform old = kitchen.transform.Find("BB_PresentationKitchen");
        if (old != null) DestroyPresentationObject(old.gameObject);

        var visual = new GameObject("BB_PresentationKitchen");
        visual.transform.SetParent(kitchen.transform, false);

        // Kitchen_Test is scaled legacy geometry. Neutralise that inherited scale so
        // the replacement is authored in real world metres instead of becoming 3x wider.
        Vector3 inherited = kitchen.transform.lossyScale;
        visual.transform.localScale = new Vector3(
            SafeInverse(inherited.x),
            SafeInverse(inherited.y),
            SafeInverse(inherited.z));

        Material metal = Resources.Load<Material>(
            "BistroBuilder/Construction/Materials/Metal_grafito");

        CreateBox(
            visual.transform,
            "KitchenBase",
            new Vector3(0f, 0.38f, 0f),
            new Vector3(3.00f, 0.76f, 1.42f),
            metal != null ? metal : kit.wallMaterial);

        CreateBox(
            visual.transform,
            "KitchenWorktop",
            new Vector3(0f, 0.81f, 0f),
            new Vector3(3.10f, 0.10f, 1.52f),
            kit.floorMaterial);

        CreateBox(
            visual.transform,
            "KitchenFrontRail",
            new Vector3(0f, 0.38f, -0.73f),
            new Vector3(3.02f, 0.13f, 0.08f),
            kit.trimMaterial != null ? kit.trimMaterial : kit.wallMaterial);
    }

    private static void UpgradeLegacyBarVisuals(
        BistroBuilderConstructionAssetKit kit)
    {
        BistroBuilder367HInstalledFixture[] fixtures =
            UnityEngine.Object.FindObjectsByType<BistroBuilder367HInstalledFixture>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.InstanceID);

        BistroBuilder367HInstalledFixture bar = null;
        for (int i = 0; i < fixtures.Length; i++)
        {
            if (fixtures[i] != null &&
                string.Equals(
                    fixtures[i].FixtureId,
                    "fixture_367h_bar",
                    StringComparison.Ordinal))
            {
                bar = fixtures[i];
                break;
            }
        }

        if (bar == null) return;

        HideLegacyPrimitive(bar.transform, "ProvisionalCounter");
        HideLegacyPrimitive(bar.transform, "ProvisionalStool");

        Transform previous = bar.transform.Find("BB_PresentationBar");
        if (previous != null) DestroyPresentationObject(previous.gameObject);

        var visual = new GameObject("BB_PresentationBar");
        visual.transform.SetParent(bar.transform, false);

        Material metal = Resources.Load<Material>(
            "BistroBuilder/Construction/Materials/Metal_grafito");
        Material timber = kit.trimMaterial != null ? kit.trimMaterial : kit.wallMaterial;

        CreateBox(
            visual.transform,
            "CounterBase",
            new Vector3(0f, 0.38f, 0f),
            new Vector3(5.20f, 0.72f, 0.78f),
            timber);

        CreateBox(
            visual.transform,
            "CounterTop",
            new Vector3(0f, 0.80f, 0f),
            new Vector3(5.45f, 0.12f, 1.02f),
            kit.floorMaterial);

        Material accent = metal != null ? metal : kit.wallMaterial;
        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.Lerp(-2.25f, 2.25f, i / 5f);
            CreateBox(
                visual.transform,
                "FrontSlat_" + i.ToString("D2"),
                new Vector3(x, 0.38f, -0.43f),
                new Vector3(0.055f, 0.62f, 0.06f),
                accent);
        }

        BistroBuilderBarServiceSpot[] spots =
            bar.GetComponentsInChildren<BistroBuilderBarServiceSpot>(true);
        for (int i = 0; i < spots.Length; i++)
        {
            if (spots[i] == null) continue;
            Transform oldStool = spots[i].transform.Find("BB_PresentationStool");
            if (oldStool != null) DestroyPresentationObject(oldStool.gameObject);

            var stool = new GameObject("BB_PresentationStool");
            stool.transform.SetParent(spots[i].transform, false);

            CreateCylinder(
                stool.transform,
                "Base",
                new Vector3(0f, 0.055f, -1f),
                new Vector3(0.28f, 0.055f, 0.28f),
                accent);

            CreateCylinder(
                stool.transform,
                "Stem",
                new Vector3(0f, 0.37f, -1f),
                new Vector3(0.065f, 0.31f, 0.065f),
                accent);

            CreateCylinder(
                stool.transform,
                "Seat",
                new Vector3(0f, 0.72f, -1f),
                new Vector3(0.34f, 0.075f, 0.34f),
                timber);
        }
    }

    private static void HideLegacyPrimitive(
        Transform root,
        string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform child = all[i];
            if (child == null ||
                !string.Equals(child.name, name, StringComparison.Ordinal))
                continue;

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
        }
    }

    private static void SetLegacyPrimitiveVisible(
        string name,
        bool visible)
    {
        GameObject[] all =
            UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go == null ||
                !string.Equals(go.name, name, StringComparison.Ordinal))
                continue;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    private static void RemovePresentationChild(
        string parentName,
        string childName)
    {
        GameObject parent = GameObject.Find(parentName);
        if (parent == null) return;

        Transform child = parent.transform.Find(childName);
        if (child != null)
            DestroyPresentationObject(child.gameObject);
    }

    private static void RemoveBarPresentationChildren()
    {
        BistroBuilder367HInstalledFixture[] fixtures =
            UnityEngine.Object.FindObjectsByType<BistroBuilder367HInstalledFixture>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);

        for (int i = 0; i < fixtures.Length; i++)
        {
            BistroBuilder367HInstalledFixture fixture = fixtures[i];
            if (fixture == null) continue;

            Transform barVisual =
                fixture.transform.Find("BB_PresentationBar");
            if (barVisual != null)
                DestroyPresentationObject(barVisual.gameObject);

            Transform[] children =
                fixture.GetComponentsInChildren<Transform>(true);
            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                Transform child = children[childIndex];
                if (child != null &&
                    string.Equals(
                        child.name,
                        "BB_PresentationStool",
                        StringComparison.Ordinal))
                    DestroyPresentationObject(child.gameObject);
            }
        }
    }

    private static void DisablePresentationColliders(
        Transform root)
    {
        if (root == null) return;

        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null)
                colliders[i].enabled = false;
    }

    private static void CreateBox(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        CreatePrimitiveVisual(
            parent,
            name,
            "Cube",
            localPosition,
            localScale,
            material);
    }

    private static void CreateCylinder(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        Mesh cylinder =
            ResolvePrimitiveMesh(
                "Cylinder");

        if (cylinder == null)
        {
            CreatePrimitiveVisual(
                parent,
                name,
                "Cube",
                localPosition,
                localScale,
                material);

            return;
        }

        CreatePrimitiveVisual(
            parent,
            name,
            cylinder,
            localPosition,
            localScale,
            material);
    }

    private static void CreatePrimitiveVisual(
        Transform parent,
        string name,
        string meshName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        Mesh mesh =
            ResolvePrimitiveMesh(
                meshName);

        if (mesh == null)
            return;

        CreatePrimitiveVisual(
            parent,
            name,
            mesh,
            localPosition,
            localScale,
            material);
    }

    private static void CreatePrimitiveVisual(
        Transform parent,
        string name,
        Mesh mesh,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        if (parent == null ||
            mesh == null)
        {
            return;
        }

        GameObject go =
            new GameObject(
                name,
                typeof(MeshFilter),
                typeof(MeshRenderer));

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
            go.GetComponent<MeshFilter>();

        filter.sharedMesh =
            mesh;

        MeshRenderer renderer =
            go.GetComponent<MeshRenderer>();

        renderer.sharedMaterial =
            material;

        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.On;

        renderer.receiveShadows =
            true;

        renderer.lightProbeUsage =
            UnityEngine.Rendering.LightProbeUsage.BlendProbes;

        renderer.reflectionProbeUsage =
            UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;

        /*
         * Deliberadamente no existe Collider: esta capa es solo Presentation.
         */
    }

    private static Mesh ResolvePrimitiveMesh(
        string meshName)
    {
        if (string.Equals(
                meshName,
                "Cube",
                StringComparison.OrdinalIgnoreCase) &&
            cachedCubeMesh != null)
        {
            return cachedCubeMesh;
        }

        if (string.Equals(
                meshName,
                "Cylinder",
                StringComparison.OrdinalIgnoreCase) &&
            cachedCylinderMesh != null)
        {
            return cachedCylinderMesh;
        }

        MeshFilter[] filters =
            UnityEngine.Object.FindObjectsByType<MeshFilter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0;
             index < filters.Length;
             index++)
        {
            MeshFilter filter =
                filters[index];

            if (filter == null ||
                filter.sharedMesh == null ||
                !string.Equals(
                    filter.sharedMesh.name,
                    meshName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Mesh resolved =
                filter.sharedMesh;

            if (string.Equals(
                    meshName,
                    "Cube",
                    StringComparison.OrdinalIgnoreCase))
            {
                cachedCubeMesh =
                    resolved;
            }
            else if (string.Equals(
                         meshName,
                         "Cylinder",
                         StringComparison.OrdinalIgnoreCase))
            {
                cachedCylinderMesh =
                    resolved;
            }

            return resolved;
        }

        return null;
    }

    private static float SafeInverse(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }

    private static void DestroyPresentationObject(GameObject go)
    {
        if (go == null) return;

        MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i] != null ? filters[i].sharedMesh : null;
            if (mesh == null || !mesh.name.StartsWith("BB_WallMesh_", StringComparison.Ordinal))
                continue;

            filters[i].sharedMesh = null;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(mesh);
            else
                UnityEngine.Object.DestroyImmediate(mesh);
        }

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(go);
        else
            UnityEngine.Object.DestroyImmediate(go);
    }
}
