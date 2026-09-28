using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renderizador único de guías de preview. Mantiene pools de líneas y no instancia
/// materiales ni GameObjects por movimiento del cursor.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Universal Preview Renderer")]
public sealed class BistroBuilderUniversalPreviewRenderer : MonoBehaviour
{
    [SerializeField] private BistroBuilderUniversalPreviewService previewService;
    [SerializeField, Min(0.004f)] private float candidateWidth = 0.028f;
    [SerializeField, Min(0.004f)] private float ghostWidth = 0.016f;
    [SerializeField, Min(0.004f)] private float conflictWidth = 0.042f;

    [SerializeField] private Color neutralColor = new Color(0.94f, 0.92f, 0.86f, 0.78f);
    [SerializeField] private Color validColor = new Color(0.34f, 0.78f, 0.70f, 0.96f);
    [SerializeField] private Color invalidColor = new Color(0.90f, 0.38f, 0.32f, 0.96f);
    [SerializeField] private Color ghostColor = new Color(0.68f, 0.76f, 0.84f, 0.30f);
    [SerializeField] private Color conflictColor = new Color(0.96f, 0.48f, 0.34f, 0.92f);
    [SerializeField] private Color snapColor = new Color(0.42f, 0.76f, 1.00f, 0.95f);

    private readonly List<LineRenderer> candidateLines = new List<LineRenderer>(16);
    private readonly List<LineRenderer> ghostLines = new List<LineRenderer>(16);
    private readonly List<LineRenderer> conflictLines = new List<LineRenderer>(8);
    private Transform visualRoot;
    private Material lineMaterial;
    private LineRenderer snapLine;
    private float snapPulseStartedAt = -1f;
    private int lastRevision = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeRenderer()
    {
        BistroBuilderUniversalPreviewService service =
            BistroBuilderUniversalPreviewService.GetOrCreate();
        if (service == null) return;
        if (service.GetComponent<BistroBuilderUniversalPreviewRenderer>() == null)
            service.gameObject.AddComponent<BistroBuilderUniversalPreviewRenderer>();
    }

    private void Awake()
    {
        ResolveService();
        EnsureVisualRoot();
    }

    private void OnEnable()
    {
        ResolveService();
        Subscribe();
        EnsureVisualRoot();
        if (previewService != null)
            Render(previewService.Current);
    }

    private void OnDisable()
    {
        Unsubscribe();
        HideAll();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (lineMaterial != null)
        {
            if (Application.isPlaying) Destroy(lineMaterial);
            else DestroyImmediate(lineMaterial);
        }
    }

    private void Update()
    {
        TickSnapPulse();
    }

    private void ResolveService()
    {
        if (previewService == null)
            previewService = GetComponent<BistroBuilderUniversalPreviewService>();
        if (previewService == null)
            previewService = FindFirstObjectByType<BistroBuilderUniversalPreviewService>();
    }

    private void Subscribe()
    {
        if (previewService == null) return;
        previewService.PreviewChanged -= HandlePreviewChanged;
        previewService.PreviewCleared -= HandlePreviewCleared;
        previewService.PreviewChanged += HandlePreviewChanged;
        previewService.PreviewCleared += HandlePreviewCleared;
    }

    private void Unsubscribe()
    {
        if (previewService == null) return;
        previewService.PreviewChanged -= HandlePreviewChanged;
        previewService.PreviewCleared -= HandlePreviewCleared;
    }

    private void HandlePreviewChanged(BistroBuilderUniversalPreviewState state)
    {
        Render(state);
    }

    private void HandlePreviewCleared()
    {
        HideAll();
    }

    private void Render(BistroBuilderUniversalPreviewState state)
    {
        EnsureVisualRoot();
        if (state == null || !state.IsVisible)
        {
            HideAll();
            return;
        }

        Color candidateColor = ResolveCandidateColor(state.Validity);
        RenderSegments(state.CandidateSegments, candidateLines, candidateColor, candidateWidth, "Candidate");
        RenderSegments(state.GhostSegments, ghostLines, ghostColor, ghostWidth, "Ghost");
        RenderSegments(state.ConflictSegments, conflictLines, conflictColor, conflictWidth, "Conflict");

        if (state.HasSnapPoint)
        {
            EnsureSnapLine();
            DrawSnapDiamond(state.SnapPoint, 0.11f, snapColor, 0.026f);
            if (lastRevision != state.Revision)
                snapPulseStartedAt = Time.unscaledTime;
        }
        else if (snapLine != null)
        {
            snapLine.enabled = false;
            snapPulseStartedAt = -1f;
        }

        lastRevision = state.Revision;
    }

    private Color ResolveCandidateColor(BistroBuilderPreviewValidity validity)
    {
        switch (validity)
        {
            case BistroBuilderPreviewValidity.Valid: return validColor;
            case BistroBuilderPreviewValidity.Invalid: return invalidColor;
            default: return neutralColor;
        }
    }

    private void RenderSegments(
        IReadOnlyList<Vector3> segments,
        List<LineRenderer> pool,
        Color color,
        float width,
        string prefix)
    {
        int segmentCount = segments != null ? segments.Count / 2 : 0;
        EnsureLinePool(pool, segmentCount, prefix);
        for (int i = 0; i < segmentCount; i++)
        {
            LineRenderer line = pool[i];
            line.enabled = true;
            line.loop = false;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.SetPosition(0, segments[i * 2]);
            line.SetPosition(1, segments[i * 2 + 1]);
        }
        for (int i = segmentCount; i < pool.Count; i++)
            pool[i].enabled = false;
    }

    private void EnsureLinePool(List<LineRenderer> pool, int count, string prefix)
    {
        EnsureVisualRoot();
        while (pool.Count < count)
            pool.Add(CreateLine(prefix + "_" + pool.Count.ToString("D2")));
    }

    private void EnsureVisualRoot()
    {
        if (visualRoot != null) return;
        GameObject root = new GameObject("BB_UniversalPreviewVisuals");
        root.transform.SetParent(transform, false);
        root.layer = 2;
        visualRoot = root.transform;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            lineMaterial = new Material(shader)
            {
                name = "BB_UniversalPreview_Line",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private LineRenderer CreateLine(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(visualRoot, false);
        go.layer = 2;
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        if (lineMaterial != null) line.sharedMaterial = lineMaterial;
        line.enabled = false;
        return line;
    }

    private void EnsureSnapLine()
    {
        if (snapLine == null)
            snapLine = CreateLine("Snap");
    }

    private void DrawSnapDiamond(Vector3 center, float radius, Color color, float width)
    {
        center.y += 0.045f;
        snapLine.enabled = true;
        snapLine.loop = true;
        snapLine.positionCount = 4;
        snapLine.startWidth = snapLine.endWidth = width;
        snapLine.startColor = snapLine.endColor = color;
        snapLine.SetPosition(0, center + new Vector3(-radius, 0f, 0f));
        snapLine.SetPosition(1, center + new Vector3(0f, 0f, radius));
        snapLine.SetPosition(2, center + new Vector3(radius, 0f, 0f));
        snapLine.SetPosition(3, center + new Vector3(0f, 0f, -radius));
    }

    private void TickSnapPulse()
    {
        if (snapLine == null || !snapLine.enabled || snapPulseStartedAt < 0f)
            return;

        const float duration = 0.28f;
        float t = Mathf.Clamp01((Time.unscaledTime - snapPulseStartedAt) / duration);
        if (t >= 1f)
        {
            snapPulseStartedAt = -1f;
            return;
        }

        Color pulse = snapColor;
        pulse.a *= 0.55f + (1f - t) * 0.45f;
        float width = Mathf.Lerp(0.052f, 0.026f, t);
        snapLine.startWidth = snapLine.endWidth = width;
        snapLine.startColor = snapLine.endColor = pulse;
    }

    private void HideAll()
    {
        HidePool(candidateLines);
        HidePool(ghostLines);
        HidePool(conflictLines);
        if (snapLine != null) snapLine.enabled = false;
        snapPulseStartedAt = -1f;
    }

    private static void HidePool(List<LineRenderer> pool)
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null) pool[i].enabled = false;
    }
}
