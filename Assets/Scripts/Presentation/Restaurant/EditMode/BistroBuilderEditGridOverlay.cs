using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Edit Grid Overlay")]
public sealed class BistroBuilderEditGridOverlay : MonoBehaviour
{
    private const float MinorSpacing = 0.25f;
    private const float MajorSpacing = 1f;
    private const float DefaultExtent = 10f;
    private const float SurfaceOffset = 0.0015f;

    [SerializeField] private RestaurantEditModeService editModeService;
    [SerializeField] private Renderer editableFloorRenderer;
    [SerializeField] private MeshFilter editableFloorMeshFilter;
    [SerializeField] private Color minorColor = new Color(0.92f, 0.94f, 0.92f, 0.20f);
    [SerializeField] private Color majorColor = new Color(0.92f, 0.94f, 0.92f, 0.34f);
    [SerializeField, Min(0.002f)] private float minorLineWidth = 0.010f;
    [SerializeField, Min(0.002f)] private float majorLineWidth = 0.016f;

    private GameObject gridRoot;
    private Mesh minorMesh;
    private Mesh majorMesh;
    private Material minorMaterial;
    private Material majorMaterial;
    private int minorLineCount;
    private int majorLineCount;

    public bool IsGridVisible => gridRoot != null && gridRoot.activeSelf;
    public float GridSpacing => MinorSpacing;
    public int MinorLineCount => minorLineCount;
    public int MajorLineCount => majorLineCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeOverlay()
    {
        RestaurantEditModeService service = FindFirstObjectByType<RestaurantEditModeService>();
        if (service == null) return;
        BistroBuilderEditGridOverlay existing = FindFirstObjectByType<BistroBuilderEditGridOverlay>();
        if (existing != null) return;
        service.gameObject.AddComponent<BistroBuilderEditGridOverlay>();
    }

    private void Awake()
    {
        CacheDependencies();
        EnsureGrid();
        SyncVisibility();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
        EnsureGrid();
        SyncVisibility();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (gridRoot != null) gridRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        DestroyRuntimeObject(minorMesh);
        DestroyRuntimeObject(majorMesh);
        DestroyRuntimeObject(minorMaterial);
        DestroyRuntimeObject(majorMaterial);
    }

    private void CacheDependencies()
    {
        if (editModeService == null) editModeService = GetComponent<RestaurantEditModeService>();
        if (editModeService == null) editModeService = FindFirstObjectByType<RestaurantEditModeService>();
        if (editableFloorRenderer == null || editableFloorMeshFilter == null)
        {
            GameObject floor = GameObject.Find("Floor_Test");
            if (floor != null)
            {
                if (editableFloorRenderer == null) editableFloorRenderer = floor.GetComponent<Renderer>();
                if (editableFloorMeshFilter == null) editableFloorMeshFilter = floor.GetComponent<MeshFilter>();
            }
        }
    }

    private void Subscribe()
    {
        if (editModeService == null) return;
        editModeService.EditModeEntered -= HandleEditModeEntered;
        editModeService.EditModeExited -= HandleEditModeExited;
        editModeService.EditModeEntered += HandleEditModeEntered;
        editModeService.EditModeExited += HandleEditModeExited;
    }

    private void Unsubscribe()
    {
        if (editModeService == null) return;
        editModeService.EditModeEntered -= HandleEditModeEntered;
        editModeService.EditModeExited -= HandleEditModeExited;
    }

    private void HandleEditModeEntered()
    {
        CacheDependencies();
        RebuildGrid();
        if (gridRoot != null) gridRoot.SetActive(true);
    }

    private void HandleEditModeExited()
    {
        if (gridRoot != null) gridRoot.SetActive(false);
    }

    private void SyncVisibility()
    {
        if (gridRoot == null) return;
        gridRoot.SetActive(editModeService != null && editModeService.IsEditModeActive);
    }

    private void EnsureGrid()
    {
        if (gridRoot == null)
        {
            gridRoot = new GameObject("BB_EditModeGrid");
            gridRoot.transform.SetParent(transform, false);
            gridRoot.layer = 2;
        }
        if (minorMesh == null || majorMesh == null) RebuildGrid();
    }

    public void RebuildGrid()
    {
        EnsureRootOnly();
        Bounds bounds = ResolveEditableLocalBounds();
        Vector3 lossyScale = editableFloorRenderer != null ? editableFloorRenderer.transform.lossyScale : Vector3.one;
        float horizontalScale = Mathf.Max(0.0001f, (Mathf.Abs(lossyScale.x) + Mathf.Abs(lossyScale.z)) * 0.5f);
        float verticalScale = Mathf.Max(0.0001f, Mathf.Abs(lossyScale.y));
        float localMinorSpacing = MinorSpacing / horizontalScale;
        float localMajorSpacing = MajorSpacing / horizontalScale;
        float y = bounds.max.y + (SurfaceOffset / verticalScale);

        ReplaceMesh(ref minorMesh, BuildGridMesh(bounds, y, localMinorSpacing, minorLineWidth / horizontalScale, localMajorSpacing, false, out minorLineCount), "BB_EditGrid_Minor");
        ReplaceMesh(ref majorMesh, BuildGridMesh(bounds, y + (0.0002f / verticalScale), localMajorSpacing, majorLineWidth / horizontalScale, localMajorSpacing, true, out majorLineCount), "BB_EditGrid_Major");

        ConfigureLayer("Minor", minorMesh, ref minorMaterial, minorColor, -5);
        ConfigureLayer("Major", majorMesh, ref majorMaterial, majorColor, -4);
        SyncVisibility();
    }

    private void EnsureRootOnly()
    {
        Transform parent = editableFloorRenderer != null ? editableFloorRenderer.transform : transform;
        if (gridRoot == null)
        {
            gridRoot = new GameObject("BB_EditModeGrid");
            gridRoot.layer = 2;
        }
        if (gridRoot.transform.parent != parent) gridRoot.transform.SetParent(parent, false);
        gridRoot.transform.localPosition = Vector3.zero;
        gridRoot.transform.localRotation = Quaternion.identity;
        gridRoot.transform.localScale = Vector3.one;
    }

    private Bounds ResolveEditableLocalBounds()
    {
        if (editableFloorMeshFilter != null && editableFloorMeshFilter.sharedMesh != null)
            return editableFloorMeshFilter.sharedMesh.bounds;
        if (editableFloorRenderer != null) return editableFloorRenderer.localBounds;
        return new Bounds(Vector3.zero, new Vector3(DefaultExtent * 2f, 0.1f, DefaultExtent * 2f));
    }

    private Mesh BuildGridMesh(Bounds bounds, float y, float spacing, float width, float majorSpacing, bool includeOnlyMajor, out int lineCount)
    {
        var vertices = new List<Vector3>(1024);
        var triangles = new List<int>(1536);
        lineCount = 0;
        float xMin = bounds.min.x;
        float xMax = bounds.max.x;
        float zMin = bounds.min.z;
        float zMax = bounds.max.z;
        float firstX = Mathf.Ceil(xMin / spacing) * spacing;
        float firstZ = Mathf.Ceil(zMin / spacing) * spacing;

        for (float x = firstX; x <= xMax + 0.0001f; x += spacing)
        {
            if (!includeOnlyMajor && IsMajorCoordinate(x, majorSpacing)) continue;
            AddClippedGridQuad(vertices, triangles, bounds, new Vector3(x, y, zMin), new Vector3(x, y, zMax), width);
            lineCount++;
        }

        for (float z = firstZ; z <= zMax + 0.0001f; z += spacing)
        {
            if (!includeOnlyMajor && IsMajorCoordinate(z, majorSpacing)) continue;
            AddClippedGridQuad(vertices, triangles, bounds, new Vector3(xMin, y, z), new Vector3(xMax, y, z), width);
            lineCount++;
        }

        AddBoundaryGridLines(vertices, triangles, bounds, y, width, spacing, includeOnlyMajor, ref lineCount);

        var mesh = new Mesh { name = includeOnlyMajor ? "BB_EditGridMajorMesh" : "BB_EditGridMinorMesh" };
        if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static bool IsMajorCoordinate(float value, float majorSpacing)
    {
        float nearest = Mathf.Round(value / majorSpacing) * majorSpacing;
        return Mathf.Abs(value - nearest) <= 0.001f;
    }

    private static void AddBoundaryGridLines(
        List<Vector3> vertices,
        List<int> triangles,
        Bounds bounds,
        float y,
        float width,
        float spacing,
        bool includeOnlyMajor,
        ref int lineCount)
    {
        if (includeOnlyMajor) return;

        if (!IsGridCoordinate(bounds.min.x, spacing))
        {
            AddClippedGridQuad(vertices, triangles, bounds,
                new Vector3(bounds.min.x, y, bounds.min.z),
                new Vector3(bounds.min.x, y, bounds.max.z), width);
            lineCount++;
        }

        if (!IsGridCoordinate(bounds.max.x, spacing))
        {
            AddClippedGridQuad(vertices, triangles, bounds,
                new Vector3(bounds.max.x, y, bounds.min.z),
                new Vector3(bounds.max.x, y, bounds.max.z), width);
            lineCount++;
        }

        if (!IsGridCoordinate(bounds.min.z, spacing))
        {
            AddClippedGridQuad(vertices, triangles, bounds,
                new Vector3(bounds.min.x, y, bounds.min.z),
                new Vector3(bounds.max.x, y, bounds.min.z), width);
            lineCount++;
        }

        if (!IsGridCoordinate(bounds.max.z, spacing))
        {
            AddClippedGridQuad(vertices, triangles, bounds,
                new Vector3(bounds.min.x, y, bounds.max.z),
                new Vector3(bounds.max.x, y, bounds.max.z), width);
            lineCount++;
        }
    }

    private static bool IsGridCoordinate(float value, float spacing)
    {
        float nearest = Mathf.Round(value / spacing) * spacing;
        return Mathf.Abs(value - nearest) <= 0.001f;
    }

    private static void AddClippedGridQuad(
        List<Vector3> vertices,
        List<int> triangles,
        Bounds bounds,
        Vector3 a,
        Vector3 b,
        float width)
    {
        Vector3 direction = (b - a).normalized;
        Vector3 side = new Vector3(-direction.z, 0f, direction.x) * (width * 0.5f);

        Vector3 v0 = ClampToBounds(a - side, bounds);
        Vector3 v1 = ClampToBounds(a + side, bounds);
        Vector3 v2 = ClampToBounds(b + side, bounds);
        Vector3 v3 = ClampToBounds(b - side, bounds);

        int start = vertices.Count;
        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private static Vector3 ClampToBounds(Vector3 point, Bounds bounds)
    {
        point.x = Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
        point.z = Mathf.Clamp(point.z, bounds.min.z, bounds.max.z);
        return point;
    }

    private void ConfigureLayer(string name, Mesh mesh, ref Material material, Color color, int sortingOrder)
    {
        Transform child = gridRoot.transform.Find(name);
        GameObject go;
        if (child == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(gridRoot.transform, false);
            go.layer = 2;
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
        }
        else go = child.gameObject;

        MeshFilter filter = go.GetComponent<MeshFilter>();
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        if (material == null) material = CreateGridMaterial("BB_EditGrid_" + name + "_Material", color);
        else SetMaterialColor(material, color);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = sortingOrder;
    }

    private static Material CreateGridMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
        SetMaterialColor(material, color);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
    }

    private static void ReplaceMesh(ref Mesh target, Mesh replacement, string name)
    {
        if (target != null) DestroyRuntimeObject(target);
        target = replacement;
        if (target != null) target.name = name;
    }

    private static void DestroyRuntimeObject(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
