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
    [SerializeField, Min(0.004f)] private float candidateWidth = 0.024f;
    [SerializeField, Min(0.004f)] private float ghostWidth = 0.012f;
    [SerializeField, Min(0.004f)] private float conflictWidth = 0.032f;

    [Header("Presencia en suelo")]
    [SerializeField] private bool showContactFill = true;
    [SerializeField, Range(0.01f, 0.20f)] private float validFillAlpha = 0.055f;
    [SerializeField, Range(0.01f, 0.20f)] private float invalidFillAlpha = 0.045f;
    [SerializeField, Range(0.01f, 0.20f)] private float neutralFillAlpha = 0.035f;
    [SerializeField, Min(0f)] private float contactFillDrop = 0.008f;

    [Header("Escala adaptativa")]
    [SerializeField, Min(0.1f)] private float referenceOrthographicSize = 7f;
    [SerializeField, Min(0.1f)] private float referencePerspectiveDistance = 12f;
    [SerializeField, Range(0.25f, 1f)] private float minimumWidthScale = 0.72f;
    [SerializeField, Range(1f, 3f)] private float maximumWidthScale = 1.75f;

    [Header("Microanimación")]
    [SerializeField, Min(0.05f)] private float conflictPulseDuration = 0.72f;
    [SerializeField, Range(0f, 1f)] private float conflictPulseStrength = 0.34f;
    [SerializeField, Min(0.05f)] private float snapHaloDuration = 0.34f;
    [SerializeField, Min(0.05f)] private float rotationCueDuration = 0.34f;

    [SerializeField] private Color neutralColor = new Color(0.90f, 0.89f, 0.84f, 0.70f);
    [SerializeField] private Color validColor = new Color(0.36f, 0.72f, 0.64f, 0.92f);
    [SerializeField] private Color invalidColor = new Color(0.88f, 0.43f, 0.36f, 0.92f);
    [SerializeField] private Color ghostColor = new Color(0.66f, 0.73f, 0.79f, 0.22f);
    [SerializeField] private Color conflictColor = new Color(0.94f, 0.48f, 0.36f, 0.86f);
    [SerializeField] private Color snapColor = new Color(0.46f, 0.69f, 0.90f, 0.90f);
    [SerializeField] private Color rotationCueColor = new Color(0.82f, 0.77f, 0.62f, 0.82f);

    private readonly List<LineRenderer> candidateLines = new List<LineRenderer>(16);
    private readonly List<LineRenderer> ghostLines = new List<LineRenderer>(16);
    private readonly List<LineRenderer> conflictLines = new List<LineRenderer>(8);
    private readonly List<MeshRenderer> volumeRenderers = new List<MeshRenderer>(8);
    private Transform visualRoot;
    private Material lineMaterial;
    private Material volumeMaterial;
    private Mesh volumeMesh;
    private Mesh contactFillMesh;
    private MeshFilter contactFillFilter;
    private MeshRenderer contactFillRenderer;
    private MaterialPropertyBlock contactFillPropertyBlock;

    private static readonly int ColorPropertyId =
        Shader.PropertyToID("_Color");

    private static readonly int BaseColorPropertyId =
        Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock volumePropertyBlock;
    private LineRenderer snapLine;
    private LineRenderer snapHaloLine;
    private LineRenderer rotationCueLine;
    private float snapPulseStartedAt = -1f;
    private float rotationCueStartedAt = -1f;
    private bool hadSnapPoint;
    private Vector3 lastSnapPoint;

    private float conflictPulseStartedAt = -1f;
    private Object lastConflictObject;
    private bool wasInvalid;
    private BistroBuilderUniversalPreviewState renderedState;
    private Camera cachedCamera;
    private float nextCameraResolveAt;

    private bool hasLastCandidateRotation;
    private Quaternion lastCandidateRotation =
        Quaternion.identity;
    private float rotationCueFromYaw;
    private float rotationCueDeltaYaw;
    private Vector3 rotationCueCenter;
    private float rotationCueRadius;

    private void Awake()
    {
        ResolveService();
        EnsureVisualRoot();
        volumePropertyBlock = new MaterialPropertyBlock();
        contactFillPropertyBlock = new MaterialPropertyBlock();

        if (lineMaterial == null ||
            volumeMaterial == null)
        {
            Debug.LogError(
                "BB Universal Preview necesita el shader " +
                "'Sprites/Default' para representar sus guías.",
                this
            );
        }
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

        if (volumeMaterial != null)
        {
            if (Application.isPlaying) Destroy(volumeMaterial);
            else DestroyImmediate(volumeMaterial);
        }

        if (volumeMesh != null)
        {
            if (Application.isPlaying) Destroy(volumeMesh);
            else DestroyImmediate(volumeMesh);
        }

        if (contactFillMesh != null)
        {
            if (Application.isPlaying) Destroy(contactFillMesh);
            else DestroyImmediate(contactFillMesh);
        }
    }

    private void Update()
    {
        TickSnapPulse();
        TickConflictPulse();
        TickRotationCue();
        ApplyAdaptivePresentation();
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
        previewService.PreviewChanged += HandlePreviewChanged;
    }

    private void Unsubscribe()
    {
        if (previewService == null) return;
        previewService.PreviewChanged -= HandlePreviewChanged;
    }

    private void HandlePreviewChanged(BistroBuilderUniversalPreviewState state)
    {
        Render(state);
    }

    private void Render(BistroBuilderUniversalPreviewState state)
    {
        EnsureVisualRoot();
        renderedState = state;

        if (state == null || !state.IsVisible)
        {
            HideAll();
            return;
        }

        bool isInvalid =
            state.Validity ==
                BistroBuilderPreviewValidity.Invalid;

        if (isInvalid &&
            (!wasInvalid ||
             !ReferenceEquals(
                 lastConflictObject,
                 state.ConflictObject)))
        {
            conflictPulseStartedAt =
                Time.unscaledTime;
        }

        wasInvalid =
            isInvalid;

        lastConflictObject =
            state.ConflictObject;

        UpdateRotationCueState(state);

        Color candidateColor = ResolveCandidateColor(state.Validity);
        RenderSegments(state.CandidateSegments, candidateLines, candidateColor, candidateWidth, "Candidate");
        RenderSegments(state.GhostSegments, ghostLines, ghostColor, ghostWidth, "Ghost");
        RenderSegments(state.ConflictSegments, conflictLines, conflictColor, conflictWidth, "Conflict");
        RenderVolumes(state.Volumes, state.Validity);
        RenderContactFill(state);

        if (state.HasSnapPoint)
        {
            bool snapChanged =
                !hadSnapPoint ||
                (state.SnapPoint - lastSnapPoint).sqrMagnitude >
                    0.0004f;

            EnsureSnapLines();
            DrawSnapDiamond(
                state.SnapPoint,
                0.11f,
                snapColor,
                0.026f);

            if (snapChanged)
            {
                snapPulseStartedAt =
                    Time.unscaledTime;

                DrawSnapHalo(
                    state.SnapPoint,
                    0.11f,
                    snapColor,
                    0.018f);
            }

            hadSnapPoint = true;
            lastSnapPoint = state.SnapPoint;
        }
        else
        {
            if (snapLine != null)
                snapLine.enabled = false;

            if (snapHaloLine != null)
                snapHaloLine.enabled = false;

            snapPulseStartedAt = -1f;
            hadSnapPoint = false;
            lastSnapPoint = default;
        }
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

        lineMaterial =
            CreateRuntimeMaterial(
                "BB_UniversalPreview_Line");

        volumeMaterial =
            CreateRuntimeMaterial(
                "BB_UniversalPreview_Volume");
    }

    private void RenderVolumes(
        IReadOnlyList<BistroBuilderPreviewBox> volumes,
        BistroBuilderPreviewValidity validity)
    {
        int count = volumes != null ? volumes.Count : 0;

        if (volumeMaterial == null)
        {
            for (int i = 0; i < volumeRenderers.Count; i++)
                if (volumeRenderers[i] != null)
                    volumeRenderers[i].enabled = false;

            return;
        }

        EnsureVolumePool(count);

        Color color = ResolveVolumeColor(validity);

        for (int i = 0; i < count; i++)
        {
            BistroBuilderPreviewBox box = volumes[i];
            MeshRenderer renderer = volumeRenderers[i];
            renderer.enabled = true;
            renderer.transform.SetPositionAndRotation(box.Center, box.Rotation);
            renderer.transform.localScale = box.Size;

            if (volumePropertyBlock == null)
                volumePropertyBlock = new MaterialPropertyBlock();

            volumePropertyBlock.Clear();
            volumePropertyBlock.SetColor(
                ColorPropertyId,
                color);
            volumePropertyBlock.SetColor(
                BaseColorPropertyId,
                color);
            renderer.SetPropertyBlock(volumePropertyBlock);
        }

        for (int i = count; i < volumeRenderers.Count; i++)
            if (volumeRenderers[i] != null)
                volumeRenderers[i].enabled = false;
    }

    private Color ResolveVolumeColor(BistroBuilderPreviewValidity validity)
    {
        Color baseColor = ResolveCandidateColor(validity);
        baseColor.a =
            validity == BistroBuilderPreviewValidity.Invalid
                ? 0.09f
                : 0.11f;
        return baseColor;
    }

    private void EnsureVolumePool(int count)
    {
        EnsureVisualRoot();

        while (volumeRenderers.Count < count)
        {
            GameObject go =
                new GameObject(
                    "Volume_" +
                    volumeRenderers.Count.ToString("D2"));

            go.transform.SetParent(
                visualRoot,
                false);

            go.layer = 2;

            MeshFilter filter =
                go.AddComponent<MeshFilter>();

            filter.sharedMesh =
                GetOrCreateVolumeMesh();

            MeshRenderer renderer =
                go.AddComponent<MeshRenderer>();

            renderer.shadowCastingMode =
                ShadowCastingMode.Off;

            renderer.receiveShadows =
                false;

            renderer.lightProbeUsage =
                LightProbeUsage.Off;

            renderer.reflectionProbeUsage =
                ReflectionProbeUsage.Off;

            if (volumeMaterial != null)
                renderer.sharedMaterial =
                    volumeMaterial;

            renderer.enabled =
                false;

            volumeRenderers.Add(
                renderer);
        }
    }

    private Mesh GetOrCreateVolumeMesh()
    {
        if (volumeMesh != null)
            return volumeMesh;

        volumeMesh =
            new Mesh
            {
                name =
                    "BB_UniversalPreview_UnitCube",
                hideFlags =
                    HideFlags.HideAndDontSave
            };

        volumeMesh.vertices =
            new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3(-0.5f, -0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f)
            };

        volumeMesh.triangles =
            new[]
            {
                // Bottom (-Y)
                0, 1, 2, 0, 2, 3,

                // Top (+Y)
                4, 6, 5, 4, 7, 6,

                // Front (-Z)
                0, 4, 5, 0, 5, 1,

                // Right (+X)
                1, 5, 6, 1, 6, 2,

                // Back (+Z)
                2, 6, 7, 2, 7, 3,

                // Left (-X)
                3, 7, 4, 3, 4, 0
            };

        volumeMesh.RecalculateNormals();
        volumeMesh.RecalculateBounds();

        return volumeMesh;
    }

    private static Material CreateRuntimeMaterial(
        string name)
    {
        Shader shader =
            Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material material =
            new Material(shader)
            {
                name = name,
                hideFlags =
                    HideFlags.HideAndDontSave,
                renderQueue =
                    (int)RenderQueue.Transparent
            };

        material.SetColor(
            ColorPropertyId,
            Color.white);

        return material;
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

    private void EnsureSnapLines()
    {
        if (snapLine == null)
            snapLine = CreateLine("Snap");

        if (snapHaloLine == null)
            snapHaloLine = CreateLine("SnapHalo");
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

    private void DrawSnapHalo(
        Vector3 center,
        float radius,
        Color color,
        float width)
    {
        if (snapHaloLine == null)
            return;

        center.y += 0.044f;

        const int points = 16;

        snapHaloLine.enabled = true;
        snapHaloLine.loop = true;
        snapHaloLine.positionCount = points;
        snapHaloLine.startWidth =
            snapHaloLine.endWidth =
                width;
        snapHaloLine.startColor =
            snapHaloLine.endColor =
                color;

        for (int i = 0; i < points; i++)
        {
            float angle =
                (Mathf.PI * 2f * i) /
                points;

            snapHaloLine.SetPosition(
                i,
                center +
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
        }
    }

    private void TickSnapPulse()
    {
        if (snapLine == null ||
            !snapLine.enabled ||
            snapPulseStartedAt < 0f)
        {
            return;
        }

        float t =
            Mathf.Clamp01(
                (Time.unscaledTime -
                 snapPulseStartedAt) /
                Mathf.Max(
                    0.05f,
                    snapHaloDuration));

        Color pulse =
            snapColor;

        pulse.a *=
            0.58f +
            (1f - t) *
            0.42f;

        float widthScale =
            GetAdaptiveWidthScale();

        float width =
            Mathf.Lerp(
                0.048f,
                0.026f,
                t) *
            widthScale;

        snapLine.startWidth =
            snapLine.endWidth =
                width;

        snapLine.startColor =
            snapLine.endColor =
                pulse;

        if (snapHaloLine != null &&
            snapHaloLine.enabled)
        {
            float radius =
                Mathf.Lerp(
                    0.11f,
                    0.28f,
                    t);

            Color halo =
                snapColor;

            halo.a *=
                (1f - t) *
                0.52f;

            DrawSnapHalo(
                lastSnapPoint,
                radius,
                halo,
                0.016f *
                widthScale);
        }

        if (t >= 1f)
        {
            snapPulseStartedAt = -1f;

            if (snapHaloLine != null)
                snapHaloLine.enabled = false;
        }
    }

    private void HideAll()
    {
        HidePool(candidateLines);
        HidePool(ghostLines);
        HidePool(conflictLines);

        for (int i = 0; i < volumeRenderers.Count; i++)
            if (volumeRenderers[i] != null)
                volumeRenderers[i].enabled = false;

        if (snapLine != null) snapLine.enabled = false;
        if (snapHaloLine != null) snapHaloLine.enabled = false;
        if (contactFillRenderer != null) contactFillRenderer.enabled = false;

        snapPulseStartedAt = -1f;
        conflictPulseStartedAt = -1f;
        rotationCueStartedAt = -1f;
        hadSnapPoint = false;
        wasInvalid = false;
        hasLastCandidateRotation = false;
        lastCandidateRotation = Quaternion.identity;
        lastConflictObject = null;
        renderedState = null;
        lastSnapPoint = default;

        if (rotationCueLine != null)
            rotationCueLine.enabled = false;
    }

    private void RenderContactFill(
        BistroBuilderUniversalPreviewState state)
    {
        if (!showContactFill ||
            state == null ||
            state.CandidateSegments == null ||
            state.CandidateSegments.Count != 8)
        {
            if (contactFillRenderer != null)
                contactFillRenderer.enabled = false;

            return;
        }

        EnsureContactFill();

        if (contactFillRenderer == null ||
            contactFillMesh == null)
        {
            return;
        }

        Vector3 a =
            state.CandidateSegments[0];

        Vector3 b =
            state.CandidateSegments[2];

        Vector3 c =
            state.CandidateSegments[4];

        Vector3 d =
            state.CandidateSegments[6];

        a.y -= contactFillDrop;
        b.y -= contactFillDrop;
        c.y -= contactFillDrop;
        d.y -= contactFillDrop;

        Transform fillTransform =
            contactFillRenderer.transform;

        a = fillTransform.InverseTransformPoint(a);
        b = fillTransform.InverseTransformPoint(b);
        c = fillTransform.InverseTransformPoint(c);
        d = fillTransform.InverseTransformPoint(d);

        contactFillMesh.Clear();
        contactFillMesh.vertices =
            new[] { a, b, c, d };

        contactFillMesh.triangles =
            new[]
            {
                0, 1, 2,
                0, 2, 3
            };

        contactFillMesh.RecalculateBounds();

        Color color =
            ResolveCandidateColor(
                state.Validity);

        switch (state.Validity)
        {
            case BistroBuilderPreviewValidity.Valid:
                color.a = validFillAlpha;
                break;

            case BistroBuilderPreviewValidity.Invalid:
                color.a = invalidFillAlpha;
                break;

            default:
                color.a = neutralFillAlpha;
                break;
        }

        contactFillPropertyBlock.Clear();
        contactFillPropertyBlock.SetColor(
            ColorPropertyId,
            color);
        contactFillPropertyBlock.SetColor(
            BaseColorPropertyId,
            color);

        contactFillRenderer.SetPropertyBlock(
            contactFillPropertyBlock);

        contactFillRenderer.enabled = true;
    }

    private void EnsureContactFill()
    {
        EnsureVisualRoot();

        if (contactFillRenderer != null)
            return;

        GameObject go =
            new GameObject(
                "ContactFill");

        go.transform.SetParent(
            visualRoot,
            false);

        go.layer = 2;

        contactFillFilter =
            go.AddComponent<MeshFilter>();

        contactFillRenderer =
            go.AddComponent<MeshRenderer>();

        contactFillRenderer.shadowCastingMode =
            ShadowCastingMode.Off;

        contactFillRenderer.receiveShadows =
            false;

        contactFillRenderer.lightProbeUsage =
            LightProbeUsage.Off;

        contactFillRenderer.reflectionProbeUsage =
            ReflectionProbeUsage.Off;

        if (volumeMaterial != null)
        {
            contactFillRenderer.sharedMaterial =
                volumeMaterial;
        }

        contactFillMesh =
            new Mesh
            {
                name =
                    "BB_UniversalPreview_ContactFill",
                hideFlags =
                    HideFlags.HideAndDontSave
            };

        contactFillMesh.MarkDynamic();

        contactFillFilter.sharedMesh =
            contactFillMesh;

        contactFillRenderer.enabled =
            false;
    }

    private void UpdateRotationCueState(
        BistroBuilderUniversalPreviewState state)
    {
        if (state == null ||
            !state.HasCandidatePose)
        {
            hasLastCandidateRotation = false;

            if (rotationCueLine != null)
                rotationCueLine.enabled = false;

            rotationCueStartedAt = -1f;
            return;
        }

        Quaternion current =
            state.CandidateRotation;

        if (!hasLastCandidateRotation)
        {
            lastCandidateRotation =
                current;

            hasLastCandidateRotation =
                true;

            return;
        }

        float previousYaw =
            lastCandidateRotation.eulerAngles.y;

        float currentYaw =
            current.eulerAngles.y;

        float delta =
            Mathf.DeltaAngle(
                previousYaw,
                currentYaw);

        lastCandidateRotation =
            current;

        if (Mathf.Abs(delta) < 1f)
            return;

        rotationCueFromYaw =
            previousYaw;

        rotationCueDeltaYaw =
            Mathf.Clamp(
                delta,
                -180f,
                180f);

        rotationCueCenter =
            state.CandidatePosition +
            Vector3.up *
            0.055f;

        rotationCueRadius =
            ResolveRotationCueRadius(state);

        rotationCueStartedAt =
            Time.unscaledTime;

        EnsureRotationCueLine();
        DrawRotationCue(
            0f);
    }

    private float ResolveRotationCueRadius(
        BistroBuilderUniversalPreviewState state)
    {
        float radius =
            0.38f;

        if (state.CandidateSegments != null)
        {
            for (int i = 0;
                 i < state.CandidateSegments.Count;
                 i++)
            {
                Vector3 delta =
                    state.CandidateSegments[i] -
                    state.CandidatePosition;

                delta.y = 0f;

                radius =
                    Mathf.Max(
                        radius,
                        delta.magnitude *
                        0.72f);
            }
        }

        return Mathf.Clamp(
            radius,
            0.34f,
            1.10f);
    }

    private void EnsureRotationCueLine()
    {
        if (rotationCueLine == null)
        {
            rotationCueLine =
                CreateLine(
                    "RotationCue");
        }
    }

    private void TickRotationCue()
    {
        if (rotationCueLine == null ||
            !rotationCueLine.enabled ||
            rotationCueStartedAt < 0f)
        {
            return;
        }

        float t =
            Mathf.Clamp01(
                (Time.unscaledTime -
                 rotationCueStartedAt) /
                Mathf.Max(
                    0.05f,
                    rotationCueDuration));

        DrawRotationCue(t);

        if (t >= 1f)
        {
            rotationCueLine.enabled =
                false;

            rotationCueStartedAt =
                -1f;
        }
    }

    private void DrawRotationCue(
        float normalizedTime)
    {
        EnsureRotationCueLine();

        const int pointCount = 20;

        rotationCueLine.enabled = true;
        rotationCueLine.loop = false;
        rotationCueLine.positionCount =
            pointCount;

        float reveal =
            1f -
            Mathf.Pow(
                1f - normalizedTime,
                3f);

        float visibleDelta =
            Mathf.Lerp(
                rotationCueDeltaYaw *
                0.32f,
                rotationCueDeltaYaw,
                reveal);

        for (int i = 0;
             i < pointCount;
             i++)
        {
            float ratio =
                i /
                (float)(pointCount - 1);

            float yaw =
                rotationCueFromYaw +
                visibleDelta *
                ratio;

            float radians =
                yaw *
                Mathf.Deg2Rad;

            Vector3 direction =
                new Vector3(
                    Mathf.Sin(radians),
                    0f,
                    Mathf.Cos(radians));

            rotationCueLine.SetPosition(
                i,
                rotationCueCenter +
                direction *
                rotationCueRadius);
        }

        Color color =
            rotationCueColor;

        color.a *=
            1f -
            Mathf.SmoothStep(
                0f,
                1f,
                normalizedTime);

        float width =
            0.018f *
            GetAdaptiveWidthScale();

        rotationCueLine.startWidth =
            rotationCueLine.endWidth =
                width;

        rotationCueLine.startColor =
            rotationCueLine.endColor =
                color;
    }

    private void TickConflictPulse()
    {
        if (conflictLines.Count == 0)
            return;

        float widthScale =
            GetAdaptiveWidthScale();

        if (conflictPulseStartedAt < 0f)
        {
            ApplyConflictPresentation(
                1f,
                conflictColor,
                widthScale);

            return;
        }

        float t =
            Mathf.Clamp01(
                (Time.unscaledTime -
                 conflictPulseStartedAt) /
                Mathf.Max(
                    0.05f,
                    conflictPulseDuration));

        float envelope =
            1f - t;

        float wave =
            0.5f +
            0.5f *
            Mathf.Sin(
                t *
                Mathf.PI *
                4f);

        float pulse =
            1f +
            wave *
            envelope *
            conflictPulseStrength;

        Color color =
            conflictColor;

        color.a *=
            Mathf.Lerp(
                0.78f,
                1f,
                pulse - 1f);

        ApplyConflictPresentation(
            pulse,
            color,
            widthScale);

        if (t >= 1f)
            conflictPulseStartedAt = -1f;
    }

    private void ApplyConflictPresentation(
        float pulse,
        Color color,
        float widthScale)
    {
        float width =
            conflictWidth *
            widthScale *
            pulse;

        for (int i = 0;
             i < conflictLines.Count;
             i++)
        {
            LineRenderer line =
                conflictLines[i];

            if (line == null ||
                !line.enabled)
            {
                continue;
            }

            line.startWidth =
                line.endWidth =
                    width;

            line.startColor =
                line.endColor =
                    color;
        }
    }

    private void ApplyAdaptivePresentation()
    {
        float widthScale =
            GetAdaptiveWidthScale();

        ApplyPoolWidth(
            candidateLines,
            candidateWidth *
            widthScale);

        ApplyPoolWidth(
            ghostLines,
            ghostWidth *
            widthScale);

        if (conflictPulseStartedAt < 0f)
        {
            ApplyPoolWidth(
                conflictLines,
                conflictWidth *
                widthScale);
        }
    }

    private static void ApplyPoolWidth(
        List<LineRenderer> pool,
        float width)
    {
        for (int i = 0;
             i < pool.Count;
             i++)
        {
            LineRenderer line =
                pool[i];

            if (line == null ||
                !line.enabled)
            {
                continue;
            }

            line.startWidth =
                line.endWidth =
                    width;
        }
    }

    private float GetAdaptiveWidthScale()
    {
        Camera camera =
            ResolveCamera();

        if (camera == null)
            return 1f;

        float scale;

        if (camera.orthographic)
        {
            scale =
                camera.orthographicSize /
                Mathf.Max(
                    0.1f,
                    referenceOrthographicSize);
        }
        else
        {
            Vector3 focus =
                ResolvePreviewFocus();

            float distance =
                Vector3.Distance(
                    camera.transform.position,
                    focus);

            scale =
                Mathf.Sqrt(
                    distance /
                    Mathf.Max(
                        0.1f,
                        referencePerspectiveDistance));
        }

        return Mathf.Clamp(
            scale,
            minimumWidthScale,
            maximumWidthScale);
    }

    private Camera ResolveCamera()
    {
        if (cachedCamera != null &&
            cachedCamera.isActiveAndEnabled)
        {
            return cachedCamera;
        }

        if (Time.unscaledTime <
            nextCameraResolveAt)
        {
            return null;
        }

        nextCameraResolveAt =
            Time.unscaledTime + 1f;

        Camera main =
            Camera.main;

        cachedCamera =
            main != null
                ? main
                : FindFirstObjectByType<Camera>();

        return cachedCamera;
    }

    private Vector3 ResolvePreviewFocus()
    {
        if (renderedState != null &&
            renderedState.HasCandidatePose)
        {
            return renderedState.CandidatePosition;
        }

        if (renderedState != null &&
            renderedState.CandidateSegments != null &&
            renderedState.CandidateSegments.Count > 0)
        {
            Vector3 sum =
                Vector3.zero;

            int count =
                renderedState.CandidateSegments.Count;

            for (int i = 0; i < count; i++)
            {
                sum +=
                    renderedState.CandidateSegments[i];
            }

            return sum /
                   Mathf.Max(
                       1,
                       count);
        }

        return transform.position;
    }

    private static void HidePool(List<LineRenderer> pool)
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null) pool[i].enabled = false;
    }
}
