using UnityEngine;

public enum BistroBuilderUiSceneSelectionState
{
    Hidden = 0,
    Hover = 1,
    Selected = 2,
    Attention = 3,
    Critical = 4
}

/// <summary>
/// Feedback de selección de escena puramente visual. Dibuja un contorno bajo y discreto
/// alrededor del footprint renderizado; no selecciona, no reserva y no modifica gameplay.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Scene Selection Visual")]
public sealed class BistroBuilderUiSceneSelectionVisual : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private BistroBuilderUiSceneSelectionState state = BistroBuilderUiSceneSelectionState.Hidden;
    [SerializeField, Range(0.01f, 0.08f)] private float lineWidth = 0.026f;
    [SerializeField, Range(0f, 0.15f)] private float boundsPadding = 0.045f;
    [SerializeField, Range(0f, 0.20f)] private float heightOffset = 0.035f;

    private LineRenderer line;
    private Material material;
    private Bounds lastBounds;
    private bool hasBounds;

    public BistroBuilderUiSceneSelectionState State => state;

    private void Awake()
    {
        EnsureRenderer();
        Refresh(true);
    }

    private void OnEnable()
    {
        EnsureRenderer();
        Refresh(true);
    }

    private void LateUpdate()
    {
        if (state == BistroBuilderUiSceneSelectionState.Hidden || target == null) return;
        if (TryResolveBounds(out Bounds bounds) && (!hasBounds || BoundsChanged(bounds, lastBounds)))
        {
            lastBounds = bounds;
            hasBounds = true;
            ApplyBounds(bounds);
        }
    }

    public void Configure(Transform targetTransform)
    {
        target = targetTransform;
        hasBounds = false;
        Refresh(true);
    }

    public void SetState(BistroBuilderUiSceneSelectionState value)
    {
        if (state == value && line != null) return;
        state = value;
        Refresh(false);
    }

    private void EnsureRenderer()
    {
        if (line != null) return;
        line = GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = 4;
        line.widthMultiplier = lineWidth;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            material = new Material(shader) { name = "BB_UIUX_SceneSelection_Mat" };
            material.hideFlags = HideFlags.DontSave;
            line.material = material;
        }
    }

    private void Refresh(bool rebuildBounds)
    {
        EnsureRenderer();
        if (line == null) return;
        bool visible = state != BistroBuilderUiSceneSelectionState.Hidden && target != null;
        line.enabled = visible;
        if (!visible) return;

        Color color = ResolveColor(state);
        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = state == BistroBuilderUiSceneSelectionState.Hover
            ? lineWidth * 0.82f : lineWidth;

        if ((rebuildBounds || !hasBounds) && TryResolveBounds(out Bounds bounds))
        {
            lastBounds = bounds;
            hasBounds = true;
            ApplyBounds(bounds);
        }
    }

    private bool TryResolveBounds(out Bounds bounds)
    {
        bounds = default;
        if (target == null) return false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer == line) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    private void ApplyBounds(Bounds bounds)
    {
        float minX = bounds.min.x - boundsPadding;
        float maxX = bounds.max.x + boundsPadding;
        float minZ = bounds.min.z - boundsPadding;
        float maxZ = bounds.max.z + boundsPadding;
        float y = bounds.min.y + heightOffset;
        line.SetPosition(0, new Vector3(minX, y, minZ));
        line.SetPosition(1, new Vector3(maxX, y, minZ));
        line.SetPosition(2, new Vector3(maxX, y, maxZ));
        line.SetPosition(3, new Vector3(minX, y, maxZ));
    }

    private static Color ResolveColor(BistroBuilderUiSceneSelectionState value)
    {
        Color color;
        switch (value)
        {
            case BistroBuilderUiSceneSelectionState.Hover:
                color = BistroBuilderUiTokens.Brand; color.a = 0.52f; return color;
            case BistroBuilderUiSceneSelectionState.Selected:
                color = BistroBuilderUiTokens.Primary; color.a = 0.82f; return color;
            case BistroBuilderUiSceneSelectionState.Attention:
                color = BistroBuilderUiTokens.Attention; color.a = 0.82f; return color;
            case BistroBuilderUiSceneSelectionState.Critical:
                color = BistroBuilderUiTokens.Critical; color.a = 0.86f; return color;
            default:
                return Color.clear;
        }
    }

    private static bool BoundsChanged(Bounds a, Bounds b)
    {
        return (a.center - b.center).sqrMagnitude > 0.0001f ||
            (a.size - b.size).sqrMagnitude > 0.0001f;
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}
