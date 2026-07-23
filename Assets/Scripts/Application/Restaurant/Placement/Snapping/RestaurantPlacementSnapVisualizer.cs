using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Visualizador universal, plano y reutilizable de destinos de snapping.
///
/// Sustituye los LineRenderer orientados a cámara por mallas planas
/// apoyadas sobre la superficie. El resultado no cambia de grosor ni
/// se deforma con la cámara isométrica.
///
/// Mantiene un pool fijo y materiales compartidos. No ejecuta Update,
/// Instantiate ni Destroy durante el movimiento del puntero.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placement Snap Visualizer"
)]
public sealed class RestaurantPlacementSnapVisualizer :
    MonoBehaviour
{
    [Header("Pool")]

    [SerializeField]
    [Range(4, 128)]
    private int maximumIndicators = 32;

    [Header("Geometría")]

    [SerializeField]
    [Min(0f)]
    private float surfaceOffset = 0.012f;

    [SerializeField]
    [Range(0.04f, 0.30f)]
    private float outlineRelativeThickness = 0.11f;

    [SerializeField]
    [Range(0.10f, 1f)]
    private float inactiveOpacity = 0.70f;

    [SerializeField]
    [Range(0.05f, 0.60f)]
    private float capturedFillOpacity = 0.20f;

    [SerializeField]
    [Range(0.25f, 1.50f)]
    private float capturedScaleMultiplier = 1.08f;

    [Header("Estados")]

    [SerializeField]
    private Color availableColor =
        new Color(0.18f, 0.88f, 0.36f, 1f);

    [SerializeField]
    private Color occupiedColor =
        new Color(1.00f, 0.58f, 0.10f, 1f);

    [SerializeField]
    private Color blockedColor =
        new Color(0.95f, 0.18f, 0.18f, 1f);

    [SerializeField]
    private Color capturedPendingColor =
        new Color(1.00f, 0.82f, 0.12f, 1f);

    private const int CircleSegmentCount = 32;

    private readonly List<IndicatorView> pool =
        new List<IndicatorView>(32);

    private Material sharedMaterial;

    private Mesh circularOutlineMesh;

    private Mesh rectangularOutlineMesh;

    private Mesh linearOutlineMesh;

    private Mesh circularFillMesh;

    private Mesh rectangularFillMesh;

    private Mesh facingArrowMesh;

    public int MaximumIndicatorCount =>
        maximumIndicators;

    public float SurfaceOffset =>
        surfaceOffset;

    private void Awake()
    {
        EnsureResources();
        EnsurePool();
        HideAll();
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void OnDestroy()
    {
        DestroyRuntimeResource(sharedMaterial);
        DestroyRuntimeResource(circularOutlineMesh);
        DestroyRuntimeResource(rectangularOutlineMesh);
        DestroyRuntimeResource(linearOutlineMesh);
        DestroyRuntimeResource(circularFillMesh);
        DestroyRuntimeResource(rectangularFillMesh);
        DestroyRuntimeResource(facingArrowMesh);

        sharedMaterial = null;
        circularOutlineMesh = null;
        rectangularOutlineMesh = null;
        linearOutlineMesh = null;
        circularFillMesh = null;
        rectangularFillMesh = null;
        facingArrowMesh = null;
    }

    /// <summary>
    /// Representa los destinos recibidos y oculta el resto del pool.
    /// </summary>
    public void Render(
        List<RestaurantPlacementSnapHint> hints,
        bool hasCapturedTarget,
        RestaurantPlacementSnapTargetKey capturedTarget,
        bool hasCapturedValidation,
        bool capturedValidationIsValid
    )
    {
        EnsureResources();
        EnsurePool();

        int visibleCount =
            hints != null
                ? Mathf.Min(hints.Count, pool.Count)
                : 0;

        for (int index = 0;
             index < visibleCount;
             index++)
        {
            RestaurantPlacementSnapHint hint =
                hints[index];

            bool isCaptured =
                hasCapturedTarget &&
                hint.TargetKey == capturedTarget;

            Color stateColor =
                ResolveColor(
                    hint.State,
                    isCaptured,
                    hasCapturedValidation,
                    capturedValidationIsValid
                );

            float scaleMultiplier =
                isCaptured
                    ? capturedScaleMultiplier
                    : 1f;

            pool[index].Show(
                hint,
                ResolveOutlineMesh(hint.Geometry),
                ResolveFillMesh(hint.Geometry),
                facingArrowMesh,
                stateColor,
                isCaptured,
                surfaceOffset,
                inactiveOpacity,
                capturedFillOpacity,
                scaleMultiplier
            );
        }

        for (int index = visibleCount;
             index < pool.Count;
             index++)
        {
            pool[index].Hide();
        }
    }

    public void HideAll()
    {
        for (int index = 0;
             index < pool.Count;
             index++)
        {
            pool[index].Hide();
        }
    }

    private Color ResolveColor(
        RestaurantPlacementSnapHintState state,
        bool isCaptured,
        bool hasCapturedValidation,
        bool capturedValidationIsValid
    )
    {
        if (isCaptured)
        {
            if (!hasCapturedValidation)
            {
                return capturedPendingColor;
            }

            return capturedValidationIsValid
                ? availableColor
                : blockedColor;
        }

        switch (state)
        {
            case RestaurantPlacementSnapHintState.Occupied:
                return occupiedColor;

            case RestaurantPlacementSnapHintState.Blocked:
                return blockedColor;

            default:
                return availableColor;
        }
    }

    private void EnsureResources()
    {
        EnsureSharedMaterial();

        if (circularOutlineMesh == null)
        {
            circularOutlineMesh =
                CreateRingMesh(
                    "BB_Snap_CircularOutline",
                    CircleSegmentCount,
                    Mathf.Clamp01(
                        1f - outlineRelativeThickness
                    )
                );
        }

        if (rectangularOutlineMesh == null)
        {
            rectangularOutlineMesh =
                CreateRectangleBorderMesh(
                    "BB_Snap_RectangularOutline",
                    outlineRelativeThickness
                );
        }

        if (linearOutlineMesh == null)
        {
            linearOutlineMesh =
                CreateRectangleBorderMesh(
                    "BB_Snap_LinearOutline",
                    Mathf.Max(
                        0.12f,
                        outlineRelativeThickness
                    )
                );
        }

        if (circularFillMesh == null)
        {
            circularFillMesh =
                CreateDiskMesh(
                    "BB_Snap_CircularFill",
                    CircleSegmentCount,
                    0.78f
                );
        }

        if (rectangularFillMesh == null)
        {
            rectangularFillMesh =
                CreateRectangleFillMesh(
                    "BB_Snap_RectangularFill",
                    0.80f
                );
        }

        if (facingArrowMesh == null)
        {
            facingArrowMesh =
                CreateFacingArrowMesh();
        }
    }

    private void EnsurePool()
    {
        int requiredCount =
            Mathf.Clamp(maximumIndicators, 4, 128);

        while (pool.Count < requiredCount)
        {
            pool.Add(
                CreateIndicator(pool.Count)
            );
        }

        for (int index = requiredCount;
             index < pool.Count;
             index++)
        {
            pool[index].Hide();
        }
    }

    private IndicatorView CreateIndicator(int index)
    {
        GameObject root =
            new GameObject(
                "SnapIndicator_" +
                index.ToString("00")
            );

        root.transform.SetParent(transform, false);
        root.hideFlags = HideFlags.HideAndDontSave;

        MeshRenderer outlineRenderer;
        MeshFilter outlineFilter =
            CreateMeshChild(
                root.transform,
                "Outline",
                32000,
                out outlineRenderer
            );

        MeshRenderer fillRenderer;
        MeshFilter fillFilter =
            CreateMeshChild(
                root.transform,
                "Fill",
                31999,
                out fillRenderer
            );

        MeshRenderer arrowRenderer;
        MeshFilter arrowFilter =
            CreateMeshChild(
                root.transform,
                "Facing",
                32001,
                out arrowRenderer
            );

        return new IndicatorView(
            root,
            outlineFilter,
            outlineRenderer,
            fillFilter,
            fillRenderer,
            arrowFilter,
            arrowRenderer
        );
    }

    private MeshFilter CreateMeshChild(
        Transform parent,
        string childName,
        int sortingOrder,
        out MeshRenderer meshRenderer
    )
    {
        GameObject child =
            new GameObject(childName);

        child.transform.SetParent(parent, false);
        child.hideFlags = HideFlags.HideAndDontSave;

        MeshFilter filter =
            child.AddComponent<MeshFilter>();

        meshRenderer =
            child.AddComponent<MeshRenderer>();

        meshRenderer.sharedMaterial = sharedMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.sortingOrder = sortingOrder;

        return filter;
    }

    private void EnsureSharedMaterial()
    {
        if (sharedMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find(
                "Universal Render Pipeline/Unlit"
            );
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            Debug.LogError(
                "No se encontró un shader transparente para los " +
                "indicadores universales de snapping.",
                this
            );

            return;
        }

        sharedMaterial = new Material(shader)
        {
            name = "BB_Runtime_SnapIndicatorMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (sharedMaterial.HasProperty("_ZWrite"))
        {
            sharedMaterial.SetFloat("_ZWrite", 0f);
        }

        if (sharedMaterial.HasProperty("_Surface"))
        {
            sharedMaterial.SetFloat("_Surface", 1f);
        }

        sharedMaterial.renderQueue =
            (int)RenderQueue.Transparent + 20;
    }

    private Mesh ResolveOutlineMesh(
        RestaurantPlacementSnapHintGeometry geometry
    )
    {
        switch (geometry)
        {
            case RestaurantPlacementSnapHintGeometry
                .RectangularFootprint:

                return rectangularOutlineMesh;

            case RestaurantPlacementSnapHintGeometry
                .LinearSocket:

                return linearOutlineMesh;

            default:
                return circularOutlineMesh;
        }
    }

    private Mesh ResolveFillMesh(
        RestaurantPlacementSnapHintGeometry geometry
    )
    {
        return geometry ==
               RestaurantPlacementSnapHintGeometry.CircularAnchor
            ? circularFillMesh
            : rectangularFillMesh;
    }

    private static Mesh CreateRingMesh(
        string meshName,
        int segmentCount,
        float innerRadius
    )
    {
        int safeSegments =
            Mathf.Max(8, segmentCount);

        float safeInner =
            Mathf.Clamp(innerRadius, 0.40f, 0.95f) * 0.5f;

        const float outerRadius = 0.5f;

        Vector3[] vertices =
            new Vector3[safeSegments * 2];

        int[] triangles =
            new int[safeSegments * 6];

        for (int index = 0;
             index < safeSegments;
             index++)
        {
            float angle =
                index * Mathf.PI * 2f / safeSegments;

            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);

            vertices[index * 2] =
                new Vector3(
                    cosine * outerRadius,
                    sine * outerRadius,
                    0f
                );

            vertices[index * 2 + 1] =
                new Vector3(
                    cosine * safeInner,
                    sine * safeInner,
                    0f
                );

            int next =
                (index + 1) % safeSegments;

            int triangleIndex = index * 6;
            int outerCurrent = index * 2;
            int innerCurrent = outerCurrent + 1;
            int outerNext = next * 2;
            int innerNext = outerNext + 1;

            triangles[triangleIndex] = outerCurrent;
            triangles[triangleIndex + 1] = outerNext;
            triangles[triangleIndex + 2] = innerCurrent;
            triangles[triangleIndex + 3] = innerCurrent;
            triangles[triangleIndex + 4] = outerNext;
            triangles[triangleIndex + 5] = innerNext;
        }

        return CreateMesh(
            meshName,
            vertices,
            triangles
        );
    }

    private static Mesh CreateDiskMesh(
        string meshName,
        int segmentCount,
        float diameter
    )
    {
        int safeSegments =
            Mathf.Max(8, segmentCount);

        float radius =
            Mathf.Clamp(diameter, 0.10f, 1f) * 0.5f;

        Vector3[] vertices =
            new Vector3[safeSegments + 1];

        int[] triangles =
            new int[safeSegments * 3];

        vertices[0] = Vector3.zero;

        for (int index = 0;
             index < safeSegments;
             index++)
        {
            float angle =
                index * Mathf.PI * 2f / safeSegments;

            vertices[index + 1] =
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                );

            int next =
                (index + 1) % safeSegments;

            triangles[index * 3] = 0;
            triangles[index * 3 + 1] = index + 1;
            triangles[index * 3 + 2] = next + 1;
        }

        return CreateMesh(
            meshName,
            vertices,
            triangles
        );
    }

    private static Mesh CreateRectangleBorderMesh(
        string meshName,
        float relativeThickness
    )
    {
        float thickness =
            Mathf.Clamp(relativeThickness, 0.04f, 0.35f);

        float outer = 0.5f;
        float inner = outer - thickness;

        Vector3[] vertices =
        {
            new Vector3(-outer, -outer, 0f),
            new Vector3( outer, -outer, 0f),
            new Vector3( outer,  outer, 0f),
            new Vector3(-outer,  outer, 0f),
            new Vector3(-inner, -inner, 0f),
            new Vector3( inner, -inner, 0f),
            new Vector3( inner,  inner, 0f),
            new Vector3(-inner,  inner, 0f)
        };

        int[] triangles =
        {
            0, 1, 4, 4, 1, 5,
            1, 2, 5, 5, 2, 6,
            2, 3, 6, 6, 3, 7,
            3, 0, 7, 7, 0, 4
        };

        return CreateMesh(
            meshName,
            vertices,
            triangles
        );
    }

    private static Mesh CreateRectangleFillMesh(
        string meshName,
        float relativeSize
    )
    {
        float halfSize =
            Mathf.Clamp(relativeSize, 0.10f, 1f) * 0.5f;

        Vector3[] vertices =
        {
            new Vector3(-halfSize, -halfSize, 0f),
            new Vector3( halfSize, -halfSize, 0f),
            new Vector3( halfSize,  halfSize, 0f),
            new Vector3(-halfSize,  halfSize, 0f)
        };

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 3
        };

        return CreateMesh(
            meshName,
            vertices,
            triangles
        );
    }

    private static Mesh CreateFacingArrowMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(-0.070f, -0.08f, 0f),
            new Vector3( 0.070f, -0.08f, 0f),
            new Vector3( 0.070f,  0.16f, 0f),
            new Vector3(-0.070f,  0.16f, 0f),
            new Vector3(-0.19f,  0.13f, 0f),
            new Vector3( 0.19f,  0.13f, 0f),
            new Vector3( 0.00f,  0.43f, 0f)
        };

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 3,
            4, 5, 6
        };

        return CreateMesh(
            "BB_Snap_FacingArrow",
            vertices,
            triangles
        );
    }

    private static Mesh CreateMesh(
        string meshName,
        Vector3[] vertices,
        int[] triangles
    )
    {
        Mesh mesh = new Mesh
        {
            name = meshName,
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.UploadMeshData(true);

        return mesh;
    }

    private static void DestroyRuntimeResource(
        Object resource
    )
    {
        if (resource == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(resource);
        }
        else
        {
            DestroyImmediate(resource);
        }
    }

    private sealed class IndicatorView
    {
        private static readonly int ColorProperty =
            Shader.PropertyToID("_Color");

        private static readonly int BaseColorProperty =
            Shader.PropertyToID("_BaseColor");

        private readonly GameObject root;

        private readonly MeshFilter outlineFilter;

        private readonly MeshRenderer outlineRenderer;

        private readonly MeshFilter fillFilter;

        private readonly MeshRenderer fillRenderer;

        private readonly MeshFilter arrowFilter;

        private readonly MeshRenderer arrowRenderer;

        private readonly MaterialPropertyBlock outlineBlock =
            new MaterialPropertyBlock();

        private readonly MaterialPropertyBlock fillBlock =
            new MaterialPropertyBlock();

        private readonly MaterialPropertyBlock arrowBlock =
            new MaterialPropertyBlock();

        public IndicatorView(
            GameObject root,
            MeshFilter outlineFilter,
            MeshRenderer outlineRenderer,
            MeshFilter fillFilter,
            MeshRenderer fillRenderer,
            MeshFilter arrowFilter,
            MeshRenderer arrowRenderer
        )
        {
            this.root = root;
            this.outlineFilter = outlineFilter;
            this.outlineRenderer = outlineRenderer;
            this.fillFilter = fillFilter;
            this.fillRenderer = fillRenderer;
            this.arrowFilter = arrowFilter;
            this.arrowRenderer = arrowRenderer;
        }

        public void Show(
            RestaurantPlacementSnapHint hint,
            Mesh outlineMesh,
            Mesh fillMesh,
            Mesh arrowMesh,
            Color stateColor,
            bool isCaptured,
            float surfaceOffset,
            float inactiveOpacity,
            float capturedFillOpacity,
            float scaleMultiplier
        )
        {
            if (root == null)
            {
                return;
            }

            Vector3 surfaceNormal =
                hint.SurfaceNormal.sqrMagnitude > 0.000001f
                    ? hint.SurfaceNormal.normalized
                    : Vector3.up;

            Vector3 facingDirection =
                Vector3.ProjectOnPlane(
                    hint.FacingDirection,
                    surfaceNormal
                );

            if (facingDirection.sqrMagnitude <= 0.000001f)
            {
                facingDirection =
                    Vector3.ProjectOnPlane(
                        Vector3.forward,
                        surfaceNormal
                    );
            }

            if (facingDirection.sqrMagnitude <= 0.000001f)
            {
                facingDirection = Vector3.right;
            }

            root.transform.SetPositionAndRotation(
                hint.WorldPosition +
                surfaceNormal * Mathf.Max(0f, surfaceOffset),
                Quaternion.LookRotation(
                    surfaceNormal,
                    facingDirection.normalized
                )
            );

            root.transform.localScale =
                new Vector3(
                    hint.Size.x * scaleMultiplier,
                    hint.Size.y * scaleMultiplier,
                    1f
                );

            outlineFilter.sharedMesh = outlineMesh;
            fillFilter.sharedMesh = fillMesh;
            arrowFilter.sharedMesh = arrowMesh;

            Color outlineColor = stateColor;
            outlineColor.a *=
                isCaptured
                    ? 1f
                    : Mathf.Clamp01(inactiveOpacity);

            SetRendererColor(
                outlineRenderer,
                outlineBlock,
                outlineColor
            );

            Color fillColor = stateColor;
            fillColor.a =
                Mathf.Clamp01(capturedFillOpacity);

            SetRendererColor(
                fillRenderer,
                fillBlock,
                fillColor
            );

            Color arrowColor = outlineColor;

            SetRendererColor(
                arrowRenderer,
                arrowBlock,
                arrowColor
            );

            fillRenderer.enabled = isCaptured;
            arrowRenderer.enabled =
                hint.ShowFacingDirection;

            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private static void SetRendererColor(
            MeshRenderer renderer,
            MaterialPropertyBlock block,
            Color color
        )
        {
            block.Clear();
            block.SetColor(ColorProperty, color);
            block.SetColor(BaseColorProperty, color);
            renderer.SetPropertyBlock(block);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maximumIndicators =
            Mathf.Clamp(maximumIndicators, 4, 128);

        surfaceOffset =
            Mathf.Max(0f, surfaceOffset);

        outlineRelativeThickness =
            Mathf.Clamp(
                outlineRelativeThickness,
                0.04f,
                0.30f
            );

        inactiveOpacity =
            Mathf.Clamp01(inactiveOpacity);

        capturedFillOpacity =
            Mathf.Clamp01(capturedFillOpacity);

        capturedScaleMultiplier =
            Mathf.Clamp(
                capturedScaleMultiplier,
                0.25f,
                1.50f
            );
    }
#endif
}
