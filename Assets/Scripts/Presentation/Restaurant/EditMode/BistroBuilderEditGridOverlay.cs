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
    private const float SurfaceOffset = 0f;

    [SerializeField] private RestaurantEditModeService editModeService;
    [SerializeField] private Renderer editableFloorRenderer;
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
        if (editableFloorRenderer == null)
        {
            GameObject floor = GameObject.Find("Floor_Test");
            if (floor != null) editableFloorRenderer = floor.GetComponent<Renderer>();
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
        Bounds bounds = ResolveEditableBounds();
        float y = bounds.max.y + SurfaceOffset;

        ReplaceMesh(ref minorMesh, BuildGridMesh(bounds, y, MinorSpacing, minorLineWidth, false, out minorLineCount), "BB_EditGrid_Minor");
        ReplaceMesh(ref majorMesh, BuildGridMesh(bounds, y, MajorSpacing, majorLineWidth, true, out majorLineCount), "BB_EditGrid_Major");

        ConfigureLayer("Minor", minorMesh, ref minorMaterial, minorColor, -5);
        ConfigureLayer("Major", majorMesh, ref majorMaterial, majorColor, -4);
        SyncVisibility();
    }

    private void EnsureRootOnly()
    {
        if (gridRoot != null) return;
        gridRoot = new GameObject("BB_EditModeGrid");
        gridRoot.transform.SetParent(transform, false);
        gridRoot.layer = 2;
    }

    private Bounds ResolveEditableBounds()
    {
        if (editableFloorRenderer != null) return editableFloorRenderer.bounds;
        return new Bounds(Vector3.zero, new Vector3(DefaultExtent * 2f, 0f, DefaultExtent * 2f));
    }

    private Mesh BuildGridMesh(Bounds bounds, float y, float spacing, float width, bool includeOnlyMajor, out int lineCount)
    {
        var vertices = new List<Vector3>(1024);
        var triangles = new List<int>(1536);
        lineCount = 0;
        float xMin = Mathf.Ceil(bounds.min.x / spacing) * spacing;
        float xMax = Mathf.Floor(bounds.max.x / spacing) * spacing;
        float zMin = Mathf.Ceil(bounds.min.z / spacing) * spacing;
        float zMax = Mathf.Floor(bounds.max.z / spacing) * spacing;

        for (float x = xMin; x <= xMax + 0.0001f; x += spacing)
        {
            if (!includeOnlyMajor && IsMajorCoordinate(x)) continue;
            AddQuad(vertices, triangles, new Vector3(x, y, bounds.min.z), new Vector3(x, y, bounds.max.z), width);
            lineCount++;
        }
        for (float z = zMin; z <= zMax + 0.0001f; z += spacing)
        {
            if (!includeOnlyMajor && IsMajorCoordinate(z)) continue;
            AddQuad(vertices, triangles, new Vector3(bounds.min.x, y, z), new Vector3(bounds.max.x, y, z), width);
            lineCount++;
        }

        var mesh = new Mesh { name = includeOnlyMajor ? "BB_EditGridMajorMesh" : "BB_EditGridMinorMesh" };
        if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static bool IsMajorCoordinate(float value)
    {
        float nearest = Mathf.Round(value / MajorSpacing) * MajorSpacing;
        return Mathf.Abs(value - nearest) <= 0.001f;
    }

    private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, float width)
    {
        Vector3 direction = (b - a).normalized;
        Vector3 side = new Vector3(-direction.z, 0f, direction.x) * (width * 0.5f);
        int start = vertices.Count;
        vertices.Add(a - side);
        vertices.Add(a + side);
        vertices.Add(b + side);
        vertices.Add(b - side);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
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
