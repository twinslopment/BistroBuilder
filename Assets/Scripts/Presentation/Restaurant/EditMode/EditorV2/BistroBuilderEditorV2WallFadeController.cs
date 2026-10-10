using System;
using System.Collections.Generic;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// B13: per-wall, reversible visual fade. Architectural materializer remains
/// the ONLY source of wall renderers; 369A/B/C remain camera authorities.
/// The original shared materials, renderer enable state and collider topology
/// are never mutated by the fade. One bounded ray query per camera change.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEditorV2WallFadeController : MonoBehaviour
{
    [SerializeField] private bool automaticFade = true;
    [SerializeField, Range(0.08f, 0.65f)] private float occludedOpacity = 0.20f;
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.20f;
    [SerializeField, Min(0.05f)] private float queryIntervalSeconds = 0.15f;
    private RestaurantEditModeService editMode;
    private BistroBuilderEditorV2CameraVisibilityContext visibility;
    private BistroBuilderArchitectureRuntimeMaterializer walls;
    private BistroBuilderCameraInspectionService inspection;
    private readonly List<Renderer> occluded = new List<Renderer>(32);
    private readonly Dictionary<Renderer, WallState> faded = new Dictionary<Renderer, WallState>(32);
    private readonly List<Renderer> toRestore = new List<Renderer>(32);
    private float nextQuery;
    private Vector3 lastCamera;
    private Vector3 lastFocus;
    private int lastRevision = -1;
    private bool hasSample;

    private sealed class WallState
    {
        public Material[] original;
        public Material[] runtime;
        public Color[] baseColors;
        public ShadowCastingMode shadowCasting;
        public float opacity = 1f;
        public bool required;
    }

    public int FadedWallCount => faded.Count;
    public bool AutomaticFadeEnabled => automaticFade;

    public void Configure(RestaurantEditModeService mode,
        BistroBuilderEditorV2CameraVisibilityContext cameraVisibility,
        BistroBuilderArchitectureRuntimeMaterializer materializer,
        BistroBuilderCameraInspectionService cameraInspection)
    {
        RestoreAll();
        editMode = mode;
        visibility = cameraVisibility;
        walls = materializer;
        inspection = cameraInspection;
        hasSample = false;
        lastRevision = -1;
        nextQuery = 0f;
    }

    public void SetAutomaticFade(bool enabled)
    {
        if (automaticFade == enabled) return;
        automaticFade = enabled;
        hasSample = false;
        if (!enabled) RestoreAll();
    }

    private bool IsAllowed =>
        isActiveAndEnabled && editMode != null && editMode.IsEditModeActive &&
        visibility != null && visibility.IsEditCameraContext &&
        visibility.HasCameraAuthority && walls != null;

    private void LateUpdate()
    {
        if (!IsAllowed || !automaticFade)
        {
            if (faded.Count > 0) RestoreAll();
            hasSample = false;
            return;
        }
        Camera current = Camera.main;
        if (current == null || inspection == null || inspection.Controller == null)
        {
            RestoreAll();
            hasSample = false;
            return;
        }
        Vector3 focus = inspection.Controller.TargetState.FocusPoint;
        Vector3 origin = current.transform.position;
        int revision = walls.RebuildInvocationCount;
        if (Time.unscaledTime >= nextQuery &&
            (!hasSample || (origin - lastCamera).sqrMagnitude > 0.0025f ||
             (focus - lastFocus).sqrMagnitude > 0.0025f ||
             revision != lastRevision))
        {
            TryRefreshSightline(origin, focus);
            nextQuery = Time.unscaledTime + queryIntervalSeconds;
            lastCamera = origin;
            lastFocus = focus;
            lastRevision = revision;
            hasSample = true;
        }
        Animate(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// On-demand refresh also powers deterministic Play Mode QA and
    /// explicit UI refreshes. A failed query restores, never partially fades.
    /// </summary>
    public bool TryRefreshSightline(Vector3 origin, Vector3 target, bool immediate = false)
    {
        if (!IsAllowed ||
            !visibility.TryFindOccludingArchitectureWalls(origin, target, walls, occluded))
        {
            RestoreAll();
            return false;
        }
        foreach (WallState item in faded.Values) item.required = false;
        for (int i = 0; i < occluded.Count; i++)
        {
            Renderer candidate = occluded[i];
            if (candidate == null) continue;
            if (faded.TryGetValue(candidate, out WallState existing))
            {
                existing.required = true;
                continue;
            }
            if (TryCreateFade(candidate, out WallState entry))
            {
                entry.required = true;
                faded.Add(candidate, entry);
            }
        }
        if (immediate) Animate(100f);
        return true;
    }

    private bool TryCreateFade(Renderer renderer, out WallState entry)
    {
        entry = null;
        if (renderer == null || !renderer.enabled) return false;
        Material[] source = renderer.sharedMaterials;
        if (source == null || source.Length == 0) return false;
        // Fail closed for custom shaders instead of breaking a wall's finish.
        for (int index = 0; index < source.Length; index++)
            if (source[index] == null ||
                !source[index].HasProperty("_Surface") ||
                !source[index].HasProperty("_BaseColor"))
                return false;

        var state = new WallState
        {
            original = source,
            runtime = new Material[source.Length],
            baseColors = new Color[source.Length],
            shadowCasting = renderer.shadowCastingMode
        };
        for (int index = 0; index < source.Length; index++)
        {
            Material clone = new Material(source[index])
            {
                name = source[index].name + " [B13 edit fade]",
                hideFlags = HideFlags.HideAndDontSave
            };
            state.baseColors[index] = source[index].GetColor("_BaseColor");
            clone.SetFloat("_Surface", 1f);
            if (clone.HasProperty("_Blend")) clone.SetFloat("_Blend", 0f);
            if (clone.HasProperty("_SrcBlend")) clone.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (clone.HasProperty("_DstBlend")) clone.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (clone.HasProperty("_ZWrite")) clone.SetInt("_ZWrite", 0);
            if (clone.HasProperty("_AlphaClip")) clone.SetFloat("_AlphaClip", 0f);
            if (clone.HasProperty("_ReceiveShadows")) clone.SetFloat("_ReceiveShadows", 0f);
            clone.SetOverrideTag("RenderType", "Transparent");
            clone.DisableKeyword("_ALPHATEST_ON");
            clone.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            clone.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            clone.EnableKeyword("_ALPHABLEND_ON");
            clone.renderQueue = (int)RenderQueue.Transparent;
            state.runtime[index] = clone;
        }
        renderer.sharedMaterials = state.runtime;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        entry = state;
        return true;
    }

    private void Animate(float dt)
    {
        if (faded.Count == 0) return;
        float step = Mathf.Max(0.005f, dt) / Mathf.Max(0.05f, transitionSeconds);
        toRestore.Clear();
        foreach (var pair in faded)
        {
            Renderer renderer = pair.Key;
            WallState wall = pair.Value;
            if (renderer == null || !renderer.enabled ||
                !ReferenceEquals(renderer.sharedMaterial,
                    wall.runtime.Length > 0 ? wall.runtime[0] : null))
            {
                toRestore.Add(renderer);
                continue;
            }
            float target = wall.required ? occludedOpacity : 1f;
            wall.opacity = Mathf.MoveTowards(wall.opacity, target, step);
            for (int index = 0; index < wall.runtime.Length; index++)
            {
                Color color = wall.baseColors[index];
                color.a *= wall.opacity;
                wall.runtime[index].SetColor("_BaseColor", color);
            }
            if (!wall.required && wall.opacity >= 0.999f)
                toRestore.Add(renderer);
        }
        for (int i = 0; i < toRestore.Count; i++)
            Restore(toRestore[i]);
        toRestore.Clear();
    }

    private void Restore(Renderer renderer)
    {
        if (!faded.TryGetValue(renderer, out WallState wall)) return;
        if (renderer != null)
        {
            // Don't overwrite another system that replaced this renderer's materials.
            if (wall.runtime.Length > 0 &&
                ReferenceEquals(renderer.sharedMaterial, wall.runtime[0]))
            {
                renderer.sharedMaterials = wall.original;
                renderer.shadowCastingMode = wall.shadowCasting;
            }
        }
        foreach (Material material in wall.runtime)
        {
            if (material == null) continue;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
        faded.Remove(renderer);
    }

    public void RestoreAll()
    {
        if (faded.Count == 0) return;
        toRestore.Clear();
        foreach (Renderer renderer in faded.Keys)
            toRestore.Add(renderer);
        for (int i = 0; i < toRestore.Count; i++)
            Restore(toRestore[i]);
        toRestore.Clear();
    }

    private void OnDisable() => RestoreAll();
    private void OnDestroy() => RestoreAll();
}
