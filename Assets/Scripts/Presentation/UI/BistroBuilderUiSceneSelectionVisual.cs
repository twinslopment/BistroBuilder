using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BistroBuilderUiSceneSelectionState
{
    Hidden = 0,
    Hover = 1,
    Selected = 2,
    Attention = 3,
    Critical = 4
}

/// <summary>
/// Feedback visual de selección de escena. La mesa seleccionada usa el tratamiento
/// medido de la referencia aprobada: núcleo #DAD1A8 y halo #B7C58C.
/// No selecciona, reserva ni modifica gameplay.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Scene Selection Visual")]
public sealed class BistroBuilderUiSceneSelectionVisual : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private BistroBuilderUiSceneSelectionState state = BistroBuilderUiSceneSelectionState.Hidden;
    [SerializeField, Range(0.01f, 0.08f)] private float coreWidth = 0.021f;
    [SerializeField, Range(0.03f, 0.16f)] private float glowWidth = 0.072f;
    [SerializeField, Range(0f, 0.20f)] private float boundsPadding = 0.085f;
    [SerializeField, Range(0f, 0.20f)] private float heightOffset = 0.035f;

    private LineRenderer coreLine;
    private LineRenderer glowLine;
    private Material coreMaterial;
    private Material glowMaterial;
    private Bounds lastBounds;
    private bool hasBounds;

    private Canvas labelCanvas;
    private TMP_Text labelText;
    private Image labelBorder;

    public BistroBuilderUiSceneSelectionState State => state;
    public float CoreWidth => coreWidth;
    public float GlowWidth => glowWidth;
    public Color CoreColor => BistroBuilderUiTokens.TableSelectionCore;
    public Color GlowColor => BistroBuilderUiTokens.TableSelectionGlow;
    public string LabelText => labelText != null ? labelText.text : string.Empty;

    private void Awake()
    {
        EnsureRenderers();
        EnsureLabel();
        Refresh(true);
    }

    private void OnEnable()
    {
        EnsureRenderers();
        EnsureLabel();
        Refresh(true);
    }

    private void LateUpdate()
    {
        if (state == BistroBuilderUiSceneSelectionState.Hidden || target == null) return;
        if (TryResolveBounds(out Bounds bounds))
        {
            if (!hasBounds || BoundsChanged(bounds, lastBounds))
            {
                lastBounds = bounds;
                hasBounds = true;
                ApplyBounds(bounds);
            }
            UpdateLabel(bounds);
        }
    }

    public void Configure(Transform targetTransform)
    {
        target = targetTransform;
        hasBounds = false;
        EnsureRenderers();
        EnsureLabel();
        Refresh(true);
    }

    public void SetState(BistroBuilderUiSceneSelectionState value)
    {
        if (state == value && coreLine != null && glowLine != null) return;
        state = value;
        Refresh(false);
    }

    private void EnsureRenderers()
    {
        if (glowLine == null)
        {
            Transform found = transform.Find("Glow");
            GameObject glow = found != null ? found.gameObject : new GameObject("Glow");
            glow.transform.SetParent(transform, false);
            glowLine = glow.GetComponent<LineRenderer>();
            if (glowLine == null) glowLine = glow.AddComponent<LineRenderer>();
            ConfigureLine(glowLine, glowWidth, 10);
            glowMaterial = CreateMaterial("BB_UIUX_TableSelectionGlow_Mat");
            if (glowMaterial != null) glowLine.material = glowMaterial;
        }

        if (coreLine == null)
        {
            Transform found = transform.Find("Core");
            GameObject core = found != null ? found.gameObject : new GameObject("Core");
            core.transform.SetParent(transform, false);
            coreLine = core.GetComponent<LineRenderer>();
            if (coreLine == null) coreLine = core.AddComponent<LineRenderer>();
            ConfigureLine(coreLine, coreWidth, 8);
            coreMaterial = CreateMaterial("BB_UIUX_TableSelectionCore_Mat");
            if (coreMaterial != null) coreLine.material = coreMaterial;
        }
    }

    private static void ConfigureLine(LineRenderer line, float width, int corners)
    {
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = 4;
        line.widthMultiplier = width;
        line.numCornerVertices = corners;
        line.numCapVertices = corners;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private static Material CreateMaterial(string name)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        Material material = new Material(shader) { name = name };
        material.hideFlags = HideFlags.DontSave;
        return material;
    }

    private void EnsureLabel()
    {
        if (labelCanvas != null) return;

        Transform existing = transform.Find("TableLabel");
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject("TableLabel", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        labelCanvas = root.GetComponent<Canvas>();
        labelCanvas.renderMode = RenderMode.WorldSpace;
        labelCanvas.sortingOrder = 250;

        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(150f, 46f);
        canvasRect.localScale = Vector3.one * 0.0062f;

        RectTransform border = NewRect("Border", canvasRect);
        Stretch(border);
        labelBorder = border.gameObject.AddComponent<Image>();
        labelBorder.color = BistroBuilderUiTokens.TableSelectionGlow;

        RectTransform background = NewRect("Background", border);
        Stretch(background);
        background.offsetMin = new Vector2(2f, 2f);
        background.offsetMax = new Vector2(-2f, -2f);
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color32(33, 36, 29, 246);

        RectTransform textRect = NewRect("Label", background);
        Stretch(textRect);
        TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = "Mesa";
        text.fontSize = 23f;
        text.fontStyle = FontStyles.Medium;
        text.color = BistroBuilderUiTokens.ContentLight;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        labelText = text;
    }

    private void Refresh(bool rebuildBounds)
    {
        EnsureRenderers();
        EnsureLabel();

        bool visible = state != BistroBuilderUiSceneSelectionState.Hidden && target != null;
        if (coreLine != null) coreLine.enabled = visible;
        if (glowLine != null) glowLine.enabled = visible;
        if (labelCanvas != null) labelCanvas.gameObject.SetActive(visible);
        if (!visible) return;

        bool hover = state == BistroBuilderUiSceneSelectionState.Hover;
        Color core = BistroBuilderUiTokens.TableSelectionCore;
        Color glow = BistroBuilderUiTokens.TableSelectionGlow;
        core.a = hover ? 0.64f : 0.98f;
        glow.a = hover ? 0.18f : 0.34f;

        coreLine.startColor = core;
        coreLine.endColor = core;
        coreLine.widthMultiplier = hover ? coreWidth * 0.82f : coreWidth;
        glowLine.startColor = glow;
        glowLine.endColor = glow;
        glowLine.widthMultiplier = hover ? glowWidth * 0.80f : glowWidth;

        if (labelBorder != null)
        {
            Color border = glow;
            border.a = hover ? 0.65f : 0.94f;
            labelBorder.color = border;
        }

        RestaurantTable table = target.GetComponent<RestaurantTable>();
        if (table != null && labelText != null)
            labelText.text = "Mesa " + table.TableId;

        if ((rebuildBounds || !hasBounds) && TryResolveBounds(out Bounds bounds))
        {
            lastBounds = bounds;
            hasBounds = true;
            ApplyBounds(bounds);
            UpdateLabel(bounds);
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
            if (renderer == null || renderer.transform.IsChildOf(transform)) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found)
        {
            bounds = new Bounds(target.position, new Vector3(1f, 0.8f, 1f));
            found = true;
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
        Vector3 p0 = new Vector3(minX, y, minZ);
        Vector3 p1 = new Vector3(maxX, y, minZ);
        Vector3 p2 = new Vector3(maxX, y, maxZ);
        Vector3 p3 = new Vector3(minX, y, maxZ);
        coreLine.SetPosition(0, p0);
        coreLine.SetPosition(1, p1);
        coreLine.SetPosition(2, p2);
        coreLine.SetPosition(3, p3);
        glowLine.SetPosition(0, p0);
        glowLine.SetPosition(1, p1);
        glowLine.SetPosition(2, p2);
        glowLine.SetPosition(3, p3);
    }

    private void UpdateLabel(Bounds bounds)
    {
        if (labelCanvas == null) return;
        labelCanvas.transform.position = new Vector3(
            bounds.center.x,
            bounds.max.y + 0.32f,
            bounds.center.z);
        Camera camera = Camera.main;
        if (camera == null) camera = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
        if (camera != null) labelCanvas.transform.rotation = camera.transform.rotation;
    }

    private static bool BoundsChanged(Bounds a, Bounds b)
    {
        return (a.center - b.center).sqrMagnitude > 0.0001f ||
               (a.size - b.size).sqrMagnitude > 0.0001f;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(coreMaterial);
        DestroyRuntimeMaterial(glowMaterial);
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material);
        else DestroyImmediate(material);
    }
}
