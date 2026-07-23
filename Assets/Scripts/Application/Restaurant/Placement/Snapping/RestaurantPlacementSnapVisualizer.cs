using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visualizador reutilizable de destinos de snapping.
///
/// Mantiene un pool fijo de indicadores sin Instantiate/Destroy por
/// frame. No contiene reglas de sillas, mesas ni otras familias.
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

    [Header("Aspecto")]

    [SerializeField]
    [Min(0f)]
    private float floorOffset = 0.025f;

    [SerializeField]
    [Min(0.005f)]
    private float lineWidth = 0.025f;

    [SerializeField]
    private Color availableColor =
        new Color(0.20f, 0.90f, 0.35f, 0.95f);

    [SerializeField]
    private Color occupiedColor =
        new Color(1.00f, 0.55f, 0.10f, 0.95f);

    [SerializeField]
    private Color blockedColor =
        new Color(0.95f, 0.15f, 0.15f, 0.98f);

    [SerializeField]
    private Color capturedPendingColor =
        new Color(1.00f, 0.85f, 0.10f, 1.00f);

    private const int CircleSegmentCount = 24;

    private readonly List<IndicatorView> pool =
        new List<IndicatorView>(32);

    private Material sharedLineMaterial;

    public int MaximumIndicatorCount =>
        maximumIndicators;

    private void Awake()
    {
        EnsurePool();
        HideAll();
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void OnDestroy()
    {
        if (sharedLineMaterial != null)
        {
            Destroy(sharedLineMaterial);
            sharedLineMaterial = null;
        }
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

            Color color =
                ResolveColor(
                    hint.State,
                    isCaptured,
                    hasCapturedValidation,
                    capturedValidationIsValid
                );

            pool[index].Show(
                hint.WorldPosition +
                Vector3.up * floorOffset,
                hint.FacingDirection,
                hint.Radius,
                lineWidth,
                color
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

    private void EnsurePool()
    {
        int requiredCount =
            Mathf.Clamp(maximumIndicators, 4, 128);

        EnsureSharedMaterial();

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

        LineRenderer circle =
            root.AddComponent<LineRenderer>();

        ConfigureLineRenderer(circle);
        circle.loop = true;
        circle.positionCount = CircleSegmentCount;

        for (int segment = 0;
             segment < CircleSegmentCount;
             segment++)
        {
            float angle =
                segment *
                Mathf.PI * 2f /
                CircleSegmentCount;

            circle.SetPosition(
                segment,
                new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle)
                )
            );
        }

        GameObject arrowObject =
            new GameObject("FacingArrow");

        arrowObject.hideFlags = HideFlags.HideAndDontSave;

        arrowObject.transform.SetParent(
            root.transform,
            false
        );

        LineRenderer arrow =
            arrowObject.AddComponent<LineRenderer>();

        ConfigureLineRenderer(arrow);
        arrow.loop = false;
        arrow.positionCount = 4;

        arrow.SetPosition(0, Vector3.zero);
        arrow.SetPosition(1, new Vector3(0f, 0f, 1.35f));
        arrow.SetPosition(2, new Vector3(-0.28f, 0f, 1.02f));
        arrow.SetPosition(3, new Vector3(0.28f, 0f, 1.02f));

        return new IndicatorView(
            root,
            circle,
            arrow
        );
    }

    private void ConfigureLineRenderer(LineRenderer line)
    {
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = sharedLineMaterial;
    }

    private void EnsureSharedMaterial()
    {
        if (sharedLineMaterial != null)
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
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogError(
                "No se encontró un shader válido para los " +
                "indicadores de snapping.",
                this
            );

            return;
        }

        sharedLineMaterial = new Material(shader)
        {
            name = "BB_Runtime_SnapIndicatorMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private sealed class IndicatorView
    {
        private readonly GameObject root;

        private readonly LineRenderer circle;

        private readonly LineRenderer arrow;

        public IndicatorView(
            GameObject root,
            LineRenderer circle,
            LineRenderer arrow
        )
        {
            this.root = root;
            this.circle = circle;
            this.arrow = arrow;
        }

        public void Show(
            Vector3 worldPosition,
            Vector3 facingDirection,
            float radius,
            float width,
            Color color
        )
        {
            if (root == null)
            {
                return;
            }

            facingDirection.y = 0f;

            Quaternion rotation =
                facingDirection.sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(
                        facingDirection.normalized,
                        Vector3.up
                    )
                    : Quaternion.identity;

            root.transform.SetPositionAndRotation(
                worldPosition,
                rotation
            );

            root.transform.localScale =
                new Vector3(radius, 1f, radius);

            float safeWidth =
                Mathf.Max(0.005f, width) /
                Mathf.Max(0.05f, radius);

            circle.startWidth = safeWidth;
            circle.endWidth = safeWidth;
            arrow.startWidth = safeWidth;
            arrow.endWidth = safeWidth;

            circle.startColor = color;
            circle.endColor = color;
            arrow.startColor = color;
            arrow.endColor = color;

            if (!root.activeSelf)
            {
                root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (root != null &&
                root.activeSelf)
            {
                root.SetActive(false);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maximumIndicators =
            Mathf.Clamp(maximumIndicators, 4, 128);

        floorOffset = Mathf.Max(0f, floorOffset);
        lineWidth = Mathf.Max(0.005f, lineWidth);
    }
#endif
}
