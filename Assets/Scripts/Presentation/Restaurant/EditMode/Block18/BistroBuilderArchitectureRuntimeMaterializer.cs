using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Block 18/Architecture Runtime Materializer")]
public sealed class BistroBuilderArchitectureRuntimeMaterializer : MonoBehaviour
{
    private const string DefaultWallContractResource =
        "BistroBuilder/Spatial/Contracts/BB_SpatialContract_Architecture_Wall";

    [SerializeField] private Transform generatedRoot;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private bool createMeshColliders = true;
    [SerializeField] private bool materializeDetectedRooms = true;
    [SerializeField] private float floorElevation;

    [Header("Spatial / Navigation projection")]
    [SerializeField] private bool createNavigationFootprints = true;
    [SerializeField] private bool createSpatialSubjects = true;
    [SerializeField] private BistroBuilderSpatialContractDefinition wallSpatialContract;
    [SerializeField, Min(0f)] private float passableOpeningBottomTolerance = 0.15f;
    [SerializeField, Min(0.5f)] private float passableOpeningMinimumHeight = 1.8f;
    [SerializeField, Min(0.01f)] private float minimumObstacleSegmentLength = 0.03f;

    [Header("Wall visual module")]
    [SerializeField] private bool useWallVisualModule = true;
    [SerializeField] private string wallVisualModuleResource =
        "BistroBuilder/Architecture/BB_Wall_Module_Master_001";
    [SerializeField, Min(0.05f)] private float wallVisualModuleWidth = 0.49141f;
    [SerializeField, Min(0.05f)] private float wallVisualModuleHeight = 1.89958f;
    [SerializeField, Min(0.005f)] private float wallVisualModuleThickness = 0.03436f;

    private readonly List<BistroBuilderOpeningRecord> hostedOpenings =
        new List<BistroBuilderOpeningRecord>(8);
    private readonly List<Vector2> blockedIntervals = new List<Vector2>(8);
    private readonly List<Vector2> passageIntervals = new List<Vector2>(8);
    private readonly List<float> visualXBreaks = new List<float>(16);
    private readonly List<Vector2> visualYIntervals = new List<Vector2>(8);
    private readonly List<Vector2> visualOpeningIntervals = new List<Vector2>(8);
    private GameObject wallVisualModulePrefab;
    private Material fallbackWallMaterial;
    private BistroBuilderEditDocument lastDocument;

    public BistroBuilderEditDocument LastDocument =>
        lastDocument != null ? lastDocument.DeepClone() : null;

    public BistroBuilderArchitectureMaterializationSummary Rebuild(
        BistroBuilderEditDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        EnsureRoot();
        ClearGenerated();
        lastDocument = document.DeepClone();
        ResolveWallSpatialContract();
        ResolveWallVisualModule();

        int wallCount = 0;
        int floorCount = 0;
        int obstacleCount = 0;
        for (int i = 0; i < document.walls.Count; i++)
        {
            BistroBuilderWallRecord wall = document.walls[i];
            if (wall == null || !wall.wallId.IsValid || wall.Length <= 0.0001f)
                continue;

            hostedOpenings.Clear();
            for (int o = 0; o < document.openings.Count; o++)
            {
                BistroBuilderOpeningRecord opening = document.openings[o];
                if (opening != null && opening.hostWallId == wall.wallId)
                    hostedOpenings.Add(opening);
            }

            obstacleCount += CreateWallObject(wall, hostedOpenings);
            wallCount++;
        }

        if (materializeDetectedRooms)
        {
            var topology = new BistroBuilderWallTopologyBuilder().Build(
                document.walls,
                document.revision);
            if (!topology.HasBlockingDiagnostics)
            {
                List<BistroBuilderEnclosedFaceCandidate> faces =
                    new BistroBuilderRoomFaceDetector().Detect(topology);
                for (int i = 0; i < faces.Count; i++)
                {
                    if (faces[i] == null || faces[i].boundary.Count < 3)
                        continue;
                    CreateFloorObject(faces[i], i);
                    floorCount++;
                }
            }
        }

        BistroBuilderSurfaceFinishVisuals.Build(generatedRoot, document);

        return new BistroBuilderArchitectureMaterializationSummary(
            wallCount,
            floorCount,
            generatedRoot.childCount,
            obstacleCount);
    }

    public void ClearGenerated()
    {
        EnsureRoot();
        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
            DestroyGeneratedObject(generatedRoot.GetChild(i).gameObject);
    }

    private int CreateWallObject(
        BistroBuilderWallRecord wall,
        IReadOnlyList<BistroBuilderOpeningRecord> openings)
    {
        Mesh mesh = BistroBuilderWallGeometryBuilder.Build(wall, openings, lastDocument?.walls);
        var go = new GameObject("Wall_" + wall.wallId.Value);
        go.transform.SetParent(generatedRoot, false);

        Vector2 axis = wall.axisEnd - wall.axisStart;
        Vector3 direction = new Vector3(axis.x, 0f, axis.y).normalized;
        go.transform.position = new Vector3(
            wall.axisStart.x,
            wall.baseElevation,
            wall.axisStart.y);
        go.transform.rotation = Quaternion.FromToRotation(
            Vector3.right,
            direction);

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ContinuousWallMaterial();
        if (createMeshColliders)
        {
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        BistroBuilderOpeningVisuals.Build(go.transform, wall, openings, renderer.sharedMaterial);

        BuildBlockedFloorIntervals(wall, openings, blockedIntervals);
        for (int i = 0; i < blockedIntervals.Count; i++)
        {
            Vector2 interval = blockedIntervals[i];
            CreateWallObstacleSegment(go.transform, wall, interval, i);
        }
        return blockedIntervals.Count;
    }

    private void CreateWallObstacleSegment(
        Transform wallRoot,
        BistroBuilderWallRecord wall,
        Vector2 interval,
        int index)
    {
        float length = interval.y - interval.x;
        if (length < minimumObstacleSegmentLength) return;

        var obstacle = new GameObject(
            "ObstacleSegment_" + index.ToString("D3"));
        obstacle.transform.SetParent(wallRoot, false);
        obstacle.transform.localPosition = new Vector3(
            (interval.x + interval.y) * 0.5f,
            0f,
            0f);

        if (createNavigationFootprints)
        {
            var footprint = obstacle.AddComponent<RestaurantPlacementFootprint>();
            footprint.ConfigureRuntime(
                Vector3.zero,
                new Vector2(length, Mathf.Max(0.05f, wall.thickness)),
                0f,
                true,
                0.005f);
        }

        if (createSpatialSubjects && wallSpatialContract != null)
        {
            var proxy = obstacle.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Simple);
            proxy.AddPart(new BistroBuilderSpatialProxyPart
            {
                partId = "static",
                layer = BistroBuilderSpatialProxyLayer.Static,
                shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
                localCenter = Vector3.zero,
                size = new Vector2(length, Mathf.Max(0.05f, wall.thickness))
            });

            var subject = obstacle.AddComponent<BistroBuilderSpatialSubject>();
            subject.Configure(
                "architecture.wall." + wall.wallId.Value +
                ".segment." + index.ToString("D3"),
                wallSpatialContract,
                proxy);
        }
    }

    private bool CreateWallVisualModules(
        Transform wallRoot,
        BistroBuilderWallRecord wall,
        IReadOnlyList<BistroBuilderOpeningRecord> openings)
    {
        if (!useWallVisualModule || wallVisualModulePrefab == null || wallRoot == null) return false;
        float length = wall.Length;
        float height = Mathf.Max(0.05f, wall.height);
        if (length <= 0.0001f) return false;

        visualXBreaks.Clear();
        visualXBreaks.Add(0f);
        visualXBreaks.Add(length);
        if (openings != null)
        {
            for (int i = 0; i < openings.Count; i++)
            {
                BistroBuilderOpeningRecord opening = openings[i];
                if (opening == null || opening.width <= 0f || opening.height <= 0f) continue;
                float center = Mathf.Clamp01(opening.axisPosition01) * length;
                float half = opening.width * 0.5f;
                visualXBreaks.Add(Mathf.Clamp(center - half, 0f, length));
                visualXBreaks.Add(Mathf.Clamp(center + half, 0f, length));
            }
        }
        SortUniqueVisualBreaks();

        int created = 0;
        for (int i = 0; i < visualXBreaks.Count - 1; i++)
        {
            float x0 = visualXBreaks[i];
            float x1 = visualXBreaks[i + 1];
            if (x1 - x0 <= 0.002f) continue;
            BuildSolidVisualYIntervals((x0 + x1) * 0.5f, length, height, openings);
            for (int y = 0; y < visualYIntervals.Count; y++)
                created += CreateWallVisualStrip(wallRoot, x0, x1, visualYIntervals[y].x,
                    visualYIntervals[y].y, Mathf.Max(0.005f, wall.thickness), created);
        }
        return created > 0;
    }

    private void SortUniqueVisualBreaks()
    {
        visualXBreaks.Sort();
        for (int i = visualXBreaks.Count - 1; i > 0; i--)
            if (Mathf.Abs(visualXBreaks[i] - visualXBreaks[i - 1]) <= 0.0001f)
                visualXBreaks.RemoveAt(i);
    }

    private void BuildSolidVisualYIntervals(
        float xMid,
        float wallLength,
        float wallHeight,
        IReadOnlyList<BistroBuilderOpeningRecord> openings)
    {
        visualYIntervals.Clear();
        visualOpeningIntervals.Clear();
        if (openings != null)
        {
            for (int i = 0; i < openings.Count; i++)
            {
                BistroBuilderOpeningRecord opening = openings[i];
                if (opening == null || opening.width <= 0f || opening.height <= 0f) continue;
                float center = Mathf.Clamp01(opening.axisPosition01) * wallLength;
                float x0 = Mathf.Clamp(center - opening.width * 0.5f, 0f, wallLength);
                float x1 = Mathf.Clamp(center + opening.width * 0.5f, 0f, wallLength);
                if (xMid <= x0 + 0.0001f || xMid >= x1 - 0.0001f) continue;
                float y0 = Mathf.Clamp(opening.bottomElevation, 0f, wallHeight);
                float y1 = Mathf.Clamp(opening.bottomElevation + opening.height, 0f, wallHeight);
                if (y1 - y0 > 0.002f) visualOpeningIntervals.Add(new Vector2(y0, y1));
            }
        }
        visualOpeningIntervals.Sort((a, b) => a.x.CompareTo(b.x));
        float cursor = 0f;
        for (int i = 0; i < visualOpeningIntervals.Count; i++)
        {
            Vector2 opening = visualOpeningIntervals[i];
            if (opening.x - cursor > 0.002f) visualYIntervals.Add(new Vector2(cursor, opening.x));
            cursor = Mathf.Max(cursor, opening.y);
        }
        if (wallHeight - cursor > 0.002f) visualYIntervals.Add(new Vector2(cursor, wallHeight));
    }

    private int CreateWallVisualStrip(
        Transform wallRoot, float x0, float x1, float y0, float y1,
        float thickness, int startIndex)
    {
        float width = x1 - x0;
        float height = y1 - y0;
        if (width <= 0.002f || height <= 0.002f) return 0;
        int tiles = Mathf.Max(1, Mathf.CeilToInt(width / Mathf.Max(0.05f, wallVisualModuleWidth)));
        float tileWidth = width / tiles;
        Material visualMaterial = ResolveWallVisualMaterial();
        for (int i = 0; i < tiles; i++)
        {
            GameObject visual = Instantiate(wallVisualModulePrefab, wallRoot, false);
            visual.name = "WallVisualModule_" + (startIndex + i).ToString("D3");
            visual.transform.localPosition = new Vector3(
                x0 + tileWidth * (i + 0.5f),
                y0 + height * 0.5f,
                0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(
                tileWidth / Mathf.Max(0.05f, wallVisualModuleWidth),
                height / Mathf.Max(0.05f, wallVisualModuleHeight),
                thickness / Mathf.Max(0.005f, wallVisualModuleThickness));
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
                renderers[r].sharedMaterial = visualMaterial;
        }
        return tiles;
    }

    private Material ResolveWallVisualMaterial()
    {
        if (wallMaterial != null) return wallMaterial;
        if (fallbackWallMaterial != null) return fallbackWallMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return null;
        fallbackWallMaterial = new Material(shader)
        {
            name = "BB_Wall_Module_RuntimeMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        Color neutral = new Color(0.72f, 0.68f, 0.62f, 1f);
        if (fallbackWallMaterial.HasProperty("_BaseColor")) fallbackWallMaterial.SetColor("_BaseColor", neutral);
        if (fallbackWallMaterial.HasProperty("_Color")) fallbackWallMaterial.SetColor("_Color", neutral);
        if (fallbackWallMaterial.HasProperty("_Smoothness")) fallbackWallMaterial.SetFloat("_Smoothness", 0.28f);
        return fallbackWallMaterial;
    }

    private void ResolveWallVisualModule()
    {
        if (!useWallVisualModule) { wallVisualModulePrefab = null; return; }
        if (wallVisualModulePrefab == null && !string.IsNullOrWhiteSpace(wallVisualModuleResource))
            wallVisualModulePrefab = Resources.Load<GameObject>(wallVisualModuleResource);
    }

    public bool HasWallVisualModule => wallVisualModulePrefab != null;

    public bool TryCreateWallVisualPreview(
        Transform parent,
        BistroBuilderWallRecord wall,
        IReadOnlyList<BistroBuilderOpeningRecord> openings, IReadOnlyList<BistroBuilderWallRecord> neighbours = null)
    {
        if (parent == null || wall == null || !wall.wallId.IsValid) return false;
        parent.gameObject.AddComponent<MeshFilter>().sharedMesh = BistroBuilderWallGeometryBuilder.Build(wall, openings, neighbours);
        var material = ContinuousWallMaterial();
        parent.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        BistroBuilderOpeningVisuals.Build(parent, wall, openings, material);
        return true;
    }

    private Material ContinuousWallMaterial()
    {
        if (wallMaterial != null) return wallMaterial;
        var kit = BistroBuilderConstructionAssetKit.Load();
        if (kit != null && kit.wallMaterial != null) return kit.wallMaterial;
        if (fallbackWallMaterial == null)
        {
            fallbackWallMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            fallbackWallMaterial.color = new Color(.73f,.69f,.60f);
        }
        return fallbackWallMaterial;
    }

    public bool SetWallVisualVisibility(BistroBuilderEditId wallId, bool visible)
    {
        EnsureRoot();
        Transform wallRoot = generatedRoot.Find("Wall_" + wallId.Value);
        if (wallRoot == null) return false;
        Renderer rootRenderer = wallRoot.GetComponent<Renderer>();
        Renderer[] renderers = wallRoot.GetComponentsInChildren<Renderer>(true);
        bool hasVisualModules = false;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].gameObject.name.StartsWith("WallVisualModule_"))
                { hasVisualModules = true; break; }
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            renderer.enabled = visible && (renderer != rootRenderer || !hasVisualModules);
        }
        return true;
    }

    public void ConfigureSpatialProjectionRuntime(
        BistroBuilderSpatialContractDefinition contract,
        bool navigationFootprints = true,
        bool spatialSubjects = true)
    {
        wallSpatialContract = contract;
        createNavigationFootprints = navigationFootprints;
        createSpatialSubjects = spatialSubjects;
    }
    private void BuildBlockedFloorIntervals(
        BistroBuilderWallRecord wall,
        IReadOnlyList<BistroBuilderOpeningRecord> openings,
        List<Vector2> results)
    {
        results.Clear();
        passageIntervals.Clear();
        float wallLength = wall.Length;

        if (openings != null)
        {
            for (int i = 0; i < openings.Count; i++)
            {
                BistroBuilderOpeningRecord opening = openings[i];
                if (opening == null ||
                    opening.bottomElevation > passableOpeningBottomTolerance ||
                    opening.height < passableOpeningMinimumHeight)
                    continue;

                float center = Mathf.Clamp01(opening.axisPosition01) * wallLength;
                float half = Mathf.Max(0f, opening.width) * 0.5f;
                float start = Mathf.Clamp(center - half, 0f, wallLength);
                float end = Mathf.Clamp(center + half, 0f, wallLength);
                if (end - start >= minimumObstacleSegmentLength)
                    passageIntervals.Add(new Vector2(start, end));
            }
        }

        passageIntervals.Sort((a, b) => a.x.CompareTo(b.x));
        float cursor = 0f;
        for (int i = 0; i < passageIntervals.Count; i++)
        {
            Vector2 passage = passageIntervals[i];
            if (passage.x - cursor >= minimumObstacleSegmentLength)
                results.Add(new Vector2(cursor, passage.x));
            cursor = Mathf.Max(cursor, passage.y);
        }

        if (wallLength - cursor >= minimumObstacleSegmentLength)
            results.Add(new Vector2(cursor, wallLength));
    }

    private void CreateFloorObject(
        BistroBuilderEnclosedFaceCandidate face,
        int index)
    {
        Mesh mesh = BistroBuilderPlanarGeometryBuilder.BuildHorizontalPolygon(
            face.boundary,
            floorElevation);
        var go = new GameObject("RoomFloor_" + index.ToString("D3"));
        go.transform.SetParent(generatedRoot, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        if (floorMaterial != null) renderer.sharedMaterial = floorMaterial;
    }

    private void ResolveWallSpatialContract()
    {
        if (wallSpatialContract != null || !createSpatialSubjects) return;
        wallSpatialContract = Resources.Load<BistroBuilderSpatialContractDefinition>(
            DefaultWallContractResource);
    }

    private void OnDestroy()
    {
        if (fallbackWallMaterial == null) return;
        if (Application.isPlaying) Destroy(fallbackWallMaterial);
        else DestroyImmediate(fallbackWallMaterial);
        fallbackWallMaterial = null;
    }

    private void EnsureRoot()
    {
        if (generatedRoot != null) return;
        var root = new GameObject("BB18_GeneratedArchitecture");
        generatedRoot = root.transform;
        generatedRoot.SetParent(transform, false);
    }

    private static bool IsRuntimeGeneratedMesh(Mesh mesh)
    {
        if (mesh == null || string.IsNullOrEmpty(mesh.name)) return false;
        return mesh.name.StartsWith("BB_WallMesh_") || mesh.name == "BB_PlanarPolygon";
    }

    private static void DestroyGeneratedObject(GameObject go)
    {
        if (go == null) return;
        // Unregister BBSIS/Navigation projections immediately; Destroy is deferred in PlayMode.
        if (go.activeSelf) go.SetActive(false);
        MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            filters[i].sharedMesh = null;
            if (!IsRuntimeGeneratedMesh(mesh)) continue;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}

public readonly struct BistroBuilderArchitectureMaterializationSummary
{
    public readonly int wallObjects;
    public readonly int roomFloorObjects;
    public readonly int generatedObjectCount;
    public readonly int obstacleSegments;

    public BistroBuilderArchitectureMaterializationSummary(
        int wallObjects,
        int roomFloorObjects,
        int generatedObjectCount)
        : this(wallObjects, roomFloorObjects, generatedObjectCount, 0)
    {
    }

    public BistroBuilderArchitectureMaterializationSummary(
        int wallObjects,
        int roomFloorObjects,
        int generatedObjectCount,
        int obstacleSegments)
    {
        this.wallObjects = wallObjects;
        this.roomFloorObjects = roomFloorObjects;
        this.generatedObjectCount = generatedObjectCount;
        this.obstacleSegments = obstacleSegments;
    }
}
