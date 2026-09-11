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

    private readonly List<BistroBuilderOpeningRecord> hostedOpenings =
        new List<BistroBuilderOpeningRecord>(8);
    private readonly List<Vector2> blockedIntervals = new List<Vector2>(8);
    private readonly List<Vector2> passageIntervals = new List<Vector2>(8);
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
        Mesh mesh = BistroBuilderWallGeometryBuilder.Build(wall, openings);
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
        if (wallMaterial != null) renderer.sharedMaterial = wallMaterial;
        if (createMeshColliders)
        {
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

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

    private void EnsureRoot()
    {
        if (generatedRoot != null) return;
        var root = new GameObject("BB18_GeneratedArchitecture");
        generatedRoot = root.transform;
        generatedRoot.SetParent(transform, false);
    }

    private static void DestroyGeneratedObject(GameObject go)
    {
        if (go == null) return;
        MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            filters[i].sharedMesh = null;
            if (mesh == null) continue;
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
