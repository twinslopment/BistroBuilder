using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor V2 Halo de Huella. The geometry is derived from the same canonical
/// RestaurantPlacementFootprint used for collision/snapping validation.
/// It never modifies item transforms, placement state or economics.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(900)]
public sealed class BistroBuilderEditorV2FootprintHalo : MonoBehaviour
{
    [SerializeField] private float floorLift = 0.032f;
    [SerializeField] private float entranceSeconds = .24f;
    [SerializeField] private float exitSeconds = .12f;
    [SerializeField] private float coreWidth = .026f;
    [SerializeField] private float glowWidth = .095f;

    private static readonly Color HoneyCore = new Color(1f, .73f, .23f, .96f);
    private static readonly Color HoneyGlow = new Color(1f, .62f, .15f, .24f);
    private BistroBuilderEditorV2SelectionCoordinator selection;
    private RestaurantPlaceableRegistry registry;
    private RestaurantEditModeService editMode;
    private readonly List<LinePair> pairs = new List<LinePair>(8);
    private readonly List<LineRenderer> groupSegments = new List<LineRenderer>(56);
    private readonly List<BistroBuilderEditorV2Selection> scratch =
        new List<BistroBuilderEditorV2Selection>(16);
    private Material material;
    private float opacity;
    private float lastRealtimeTick = -1f;
    private long lastRevision = long.MinValue;
    private int visibleCount;
    private bool hasGroup;

    public int VisibleMemberCount => visibleCount;
    public bool HasGroupOutline => hasGroup;
    public float Opacity => opacity;

    private sealed class LinePair
    {
        public LineRenderer glow;
        public LineRenderer core;
    }

    private void Awake()
    {
        Resolve();
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            material = new Material(shader)
            {
                name = "BB_EditorV2_Halo_Material_Runtime",
                hideFlags = HideFlags.DontSave
            };
        }
    }

    private void Resolve()
    {
        if (selection == null)
            selection = FindFirstObjectByType<
                BistroBuilderEditorV2SelectionCoordinator>(FindObjectsInactive.Include);
        if (registry == null)
            registry = FindFirstObjectByType<RestaurantPlaceableRegistry>(
                FindObjectsInactive.Include);
        if (editMode == null)
            editMode = FindFirstObjectByType<RestaurantEditModeService>(
                FindObjectsInactive.Include);
    }

    private void LateUpdate()
    {
        Resolve();
        bool visible = editMode != null && editMode.IsEditModeActive &&
            selection != null && selection.SelectionCount > 0 && registry != null;
        float duration = visible ? Mathf.Max(.01f, entranceSeconds) :
            Mathf.Max(.01f, exitSeconds);
        // Time-based transition, not a frame-count dependency. A headless
        // editor test may render few frames while realtime moves normally.
        float now = Time.realtimeSinceStartup;
        float elapsed = lastRealtimeTick < 0f ? Time.unscaledDeltaTime :
            Mathf.Max(Time.unscaledDeltaTime, now - lastRealtimeTick);
        lastRealtimeTick = now;
        opacity = Mathf.MoveTowards(opacity, visible ? 1f : 0f,
            elapsed / duration);

        if (!visible && opacity <= .001f)
        {
            HideAll();
            lastRevision = long.MinValue;
            return;
        }

        if (visible)
        {
            selection.CopySelectionSet(scratch, true);
            visibleCount = 0;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;
            float meanY = 0f;
            for (int i = 0; i < scratch.Count; i++)
            {
                var item = scratch[i];
                if (item.kind != BistroBuilderEditorV2SelectionKind.Furniture ||
                    !registry.TryGetByInstanceId(item.stableId, out var obj) ||
                    obj == null || !obj.gameObject.activeInHierarchy)
                    continue;

                var footprint = obj.GetComponent<RestaurantPlacementFootprint>();
                if (footprint == null) continue;
                RestaurantPlacementShape shape = footprint.BuildCurrentShape();
                bool round = IsRoundFurniture(obj, shape);
                EnsurePair(visibleCount);
                RenderShape(pairs[visibleCount], shape, round, opacity);
                visibleCount++;
                // Group bounds follow each independently rotated footprint,
                // never the screen-space UI or approximate item pivots.
                for (int k = 0; k < 4; k++)
                {
                    var corner = shape.GetCorner(k == 0 || k == 3 ? -1 : 1,
                        k < 2 ? -1 : 1);
                    minX = Mathf.Min(minX, corner.x);
                    maxX = Mathf.Max(maxX, corner.x);
                    minZ = Mathf.Min(minZ, corner.z);
                    maxZ = Mathf.Max(maxZ, corner.z);
                }
                meanY += shape.Center.y;
            }
            for (int i = visibleCount; i < pairs.Count; i++)
                SetPairActive(pairs[i], false);
            hasGroup = visibleCount > 1;
            if (hasGroup)
                RenderGroup(minX, maxX, minZ, maxZ, meanY / visibleCount, opacity);
            else
                HideGroup();
            lastRevision = selection.SelectionSetRevision;
        }
        else
        {
            foreach (var p in pairs)
            {
                if (p.glow == null || p.core == null) continue;
                Tint(p.glow, HoneyGlow, opacity);
                Tint(p.core, HoneyCore, opacity);
            }
            foreach (var line in groupSegments)
                if (line != null) Tint(line, HoneyCore, opacity * .60f);
        }
    }

    private static bool IsRoundFurniture(RestaurantPlaceableObject obj,
        RestaurantPlacementShape shape)
    {
        if (Mathf.Abs(shape.HalfWidth - shape.HalfDepth) >
            Mathf.Max(shape.HalfWidth, shape.HalfDepth) * .14f)
            return false;
        string name = obj.DisplayName ?? string.Empty;
        // Only authored round/circular objects receive a circular halo; a
        // square chair must stay rectangular even if width equals depth.
        return name.IndexOf("redond", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("round", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("circular", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RenderShape(LinePair pair,
        RestaurantPlacementShape shape, bool round, float alpha)
    {
        SetPairActive(pair, true);
        int segments = round ? 48 : 4;
        pair.glow.positionCount = segments + 1;
        pair.core.positionCount = segments + 1;
        Vector3 center = shape.Center;
        float y = center.y + floorLift;
        for (int i = 0; i <= segments; i++)
        {
            Vector3 point;
            if (round)
            {
                float a = i * Mathf.PI * 2f / segments;
                point = center +
                    shape.RightAxis * (Mathf.Cos(a) * shape.HalfWidth) +
                    shape.ForwardAxis * (Mathf.Sin(a) * shape.HalfDepth);
            }
            else
            {
                int j = i % 4;
                point = shape.GetCorner(j == 0 || j == 3 ? -1f : 1f,
                    j < 2 ? -1f : 1f);
            }
            point.y = y;
            pair.glow.SetPosition(i, point);
            pair.core.SetPosition(i, point);
        }
        Tint(pair.glow, HoneyGlow, alpha);
        Tint(pair.core, HoneyCore, alpha);
    }

    private void RenderGroup(float minX, float maxX, float minZ,
        float maxZ, float y, float alpha)
    {
        // Dashed enclosing footprint; each member keeps its own exact halo.
        float pad = .11f;
        minX -= pad; maxX += pad; minZ -= pad; maxZ += pad;
        var corners = new[]
        {
            new Vector3(minX, y + floorLift * 1.5f, minZ),
            new Vector3(maxX, y + floorLift * 1.5f, minZ),
            new Vector3(maxX, y + floorLift * 1.5f, maxZ),
            new Vector3(minX, y + floorLift * 1.5f, maxZ)
        };
        int used = 0;
        for (int edge = 0; edge < 4; edge++)
        {
            Vector3 a = corners[edge];
            Vector3 b = corners[(edge + 1) % 4];
            float distance = Vector3.Distance(a, b);
            int slices = Mathf.Clamp(Mathf.CeilToInt(distance / .22f), 2, 24);
            for (int s = 0; s < slices; s += 2)
            {
                EnsureGroupSegment(used);
                var line = groupSegments[used++];
                line.gameObject.SetActive(true);
                line.positionCount = 2;
                line.SetPosition(0, Vector3.Lerp(a, b, s / (float)slices));
                line.SetPosition(1, Vector3.Lerp(a, b,
                    Mathf.Min(s + 1, slices) / (float)slices));
                Tint(line, HoneyCore, alpha * .65f);
            }
        }
        for (int i = used; i < groupSegments.Count; i++)
            groupSegments[i].gameObject.SetActive(false);
    }

    private void EnsurePair(int index)
    {
        while (pairs.Count <= index)
        {
            var p = new LinePair
            {
                glow = NewLine("HaloGlow_" + pairs.Count, glowWidth),
                core = NewLine("HaloCore_" + pairs.Count, coreWidth)
            };
            pairs.Add(p);
        }
    }

    private void EnsureGroupSegment(int index)
    {
        while (groupSegments.Count <= index)
            groupSegments.Add(NewLine("GroupDash_" + groupSegments.Count,
                coreWidth * .70f));
    }

    private LineRenderer NewLine(string name, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.positionCount = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (material != null) line.sharedMaterial = material;
        line.enabled = false;
        return line;
    }

    private static void Tint(LineRenderer line, Color baseColor, float alpha)
    {
        if (line == null) return;
        baseColor.a *= Mathf.Clamp01(alpha);
        line.startColor = line.endColor = baseColor;
        line.enabled = alpha > .005f;
    }

    private static void SetPairActive(LinePair pair, bool active)
    {
        if (pair.glow != null) pair.glow.enabled = active;
        if (pair.core != null) pair.core.enabled = active;
    }

    private void HideGroup()
    {
        foreach (var line in groupSegments)
            if (line != null) line.enabled = false;
    }

    private void HideAll()
    {
        visibleCount = 0;
        hasGroup = false;
        foreach (var p in pairs) SetPairActive(p, false);
        HideGroup();
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
