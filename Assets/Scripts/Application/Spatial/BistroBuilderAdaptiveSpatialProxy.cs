using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pieza semántica de un Adaptive Spatial Proxy.
/// Puede estar anclada a una articulación distinta del objeto raíz.
/// </summary>
[Serializable]
public sealed class BistroBuilderSpatialProxyPart
{
    public string partId = "part";
    public BistroBuilderSpatialProxyLayer layer = BistroBuilderSpatialProxyLayer.Static;
    public BistroBuilderSpatialShapeKind shapeKind = BistroBuilderSpatialShapeKind.OrientedBox;
    public Transform anchor;
    public Vector3 localCenter = Vector3.zero;
    public Vector2 size = Vector2.one;
    [Min(0.01f)] public float radius = 0.25f;
    [Min(0.01f)] public float capsuleLength = 0.5f;
    public Vector3 localForward = Vector3.forward;
    public List<Vector2> convexHull = new List<Vector2>();
    public bool enabled = true;

    public BistroBuilderSpatialVolume BuildWorldVolume(Transform root)
    {
        Transform basis = anchor != null ? anchor : root;
        Vector3 scale = basis != null ? basis.lossyScale : Vector3.one;
        Vector3 center = basis != null ? basis.TransformPoint(localCenter) : localCenter;
        Vector3 right = basis != null ? basis.right : Vector3.right;
        Vector3 forward = basis != null ? basis.forward : Vector3.forward;

        if (shapeKind == BistroBuilderSpatialShapeKind.Circle)
        {
            float worldRadius = radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return BistroBuilderSpatialVolume.Circle(center, worldRadius);
        }

        Vector2 half = GetApproximateHalfExtents(scale);
        return BistroBuilderSpatialVolume.Box(center, right, forward, half);
    }

    public bool ContainsPoint(Transform root, Vector3 worldPoint, float expansion)
    {
        if (!enabled) return false;
        Transform basis = anchor != null ? anchor : root;
        if (basis == null) return false;
        Vector3 local = basis.InverseTransformPoint(worldPoint) - localCenter;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(basis.lossyScale.x));
        float sz = Mathf.Max(0.0001f, Mathf.Abs(basis.lossyScale.z));
        float ex = Mathf.Max(0f, expansion) / sx;
        float ez = Mathf.Max(0f, expansion) / sz;

        switch (shapeKind)
        {
            case BistroBuilderSpatialShapeKind.Circle:
                return new Vector2(local.x, local.z).sqrMagnitude <=
                       Mathf.Pow(radius + Mathf.Max(ex, ez), 2f);
            case BistroBuilderSpatialShapeKind.Capsule:
                return ContainsCapsule(local, ex, ez);
            case BistroBuilderSpatialShapeKind.ConvexHull:
                return ContainsConvex(local, ex, ez);
            default:
                return Mathf.Abs(local.x) <= Mathf.Max(0.01f, size.x * 0.5f) + ex &&
                       Mathf.Abs(local.z) <= Mathf.Max(0.01f, size.y * 0.5f) + ez;
        }
    }

    private bool ContainsCapsule(Vector3 local, float ex, float ez)
    {
        Vector2 direction = new Vector2(localForward.x, localForward.z);
        if (direction.sqrMagnitude <= 0.000001f) direction = Vector2.up;
        direction.Normalize();
        float halfSegment = Mathf.Max(0f, capsuleLength * 0.5f);
        Vector2 a = -direction * halfSegment;
        Vector2 b = direction * halfSegment;
        Vector2 p = new Vector2(local.x, local.z);
        Vector2 ab = b - a;
        float denominator = Mathf.Max(0.000001f, ab.sqrMagnitude);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denominator);
        Vector2 closest = a + ab * t;
        float expanded = radius + Mathf.Max(ex, ez);
        return (p - closest).sqrMagnitude <= expanded * expanded;
    }

    private bool ContainsConvex(Vector3 local, float ex, float ez)
    {
        if (convexHull == null || convexHull.Count < 3)
            return Mathf.Abs(local.x) <= Mathf.Max(0.01f, size.x * 0.5f) + ex &&
                   Mathf.Abs(local.z) <= Mathf.Max(0.01f, size.y * 0.5f) + ez;

        Vector2 p = new Vector2(local.x, local.z);
        bool inside = false;
        for (int i = 0, j = convexHull.Count - 1; i < convexHull.Count; j = i++)
        {
            Vector2 pi = convexHull[i];
            Vector2 pj = convexHull[j];
            bool crosses = ((pi.y > p.y) != (pj.y > p.y)) &&
                p.x < (pj.x - pi.x) * (p.y - pi.y) /
                Mathf.Max(0.000001f, pj.y - pi.y) + pi.x;
            if (crosses) inside = !inside;
        }
        if (inside) return true;
        if (ex <= 0f && ez <= 0f) return false;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        for (int i = 0; i < convexHull.Count; i++)
        {
            minX = Mathf.Min(minX, convexHull[i].x);
            maxX = Mathf.Max(maxX, convexHull[i].x);
            minZ = Mathf.Min(minZ, convexHull[i].y);
            maxZ = Mathf.Max(maxZ, convexHull[i].y);
        }
        return p.x >= minX - ex && p.x <= maxX + ex &&
               p.y >= minZ - ez && p.y <= maxZ + ez;
    }

    private Vector2 GetApproximateHalfExtents(Vector3 worldScale)
    {
        float sx = Mathf.Abs(worldScale.x);
        float sz = Mathf.Abs(worldScale.z);
        if (shapeKind == BistroBuilderSpatialShapeKind.Capsule)
        {
            float diameter = radius * 2f;
            Vector2 direction = new Vector2(localForward.x, localForward.z);
            if (direction.sqrMagnitude <= 0.000001f) direction = Vector2.up;
            direction.Normalize();
            float width = diameter + Mathf.Abs(direction.x) * capsuleLength;
            float depth = diameter + Mathf.Abs(direction.y) * capsuleLength;
            return new Vector2(
                Mathf.Max(0.01f, width * sx * 0.5f),
                Mathf.Max(0.01f, depth * sz * 0.5f));
        }
        if (shapeKind == BistroBuilderSpatialShapeKind.ConvexHull &&
            convexHull != null && convexHull.Count >= 3)
        {
            float maxX = 0.01f;
            float maxZ = 0.01f;
            for (int i = 0; i < convexHull.Count; i++)
            {
                maxX = Mathf.Max(maxX, Mathf.Abs(convexHull[i].x));
                maxZ = Mathf.Max(maxZ, Mathf.Abs(convexHull[i].y));
            }
            return new Vector2(maxX * sx, maxZ * sz);
        }
        return new Vector2(
            Mathf.Max(0.01f, size.x * sx * 0.5f),
            Mathf.Max(0.01f, size.y * sz * 0.5f));
    }
}

/// <summary>
/// Proxy espacial adaptativo de BBSIS. Admite geometría simple, compuesta,
/// por capas y articulada sin depender de mallas de render ni Rigidbody.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdaptiveSpatialProxy : MonoBehaviour
{
    [SerializeField] private BistroBuilderAdaptiveSpatialProxyMode mode =
        BistroBuilderAdaptiveSpatialProxyMode.Simple;
    [SerializeField] private List<BistroBuilderSpatialProxyPart> parts =
        new List<BistroBuilderSpatialProxyPart>();

    public BistroBuilderAdaptiveSpatialProxyMode Mode => mode;
    public IReadOnlyList<BistroBuilderSpatialProxyPart> Parts => parts;

    public bool ContainsPoint(
        Vector3 worldPoint,
        BistroBuilderSpatialProxyLayer layer,
        float expansion = 0f)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            BistroBuilderSpatialProxyPart part = parts[i];
            if (part != null && part.enabled && part.layer == layer &&
                part.ContainsPoint(transform, worldPoint, expansion))
                return true;
        }
        return false;
    }

    public int BuildWorldVolumes(
        BistroBuilderSpatialProxyLayer layer,
        List<BistroBuilderSpatialVolume> results)
    {
        if (results == null) throw new ArgumentNullException(nameof(results));
        int before = results.Count;
        for (int i = 0; i < parts.Count; i++)
        {
            BistroBuilderSpatialProxyPart part = parts[i];
            if (part == null || !part.enabled || part.layer != layer) continue;
            results.Add(part.BuildWorldVolume(transform));
        }
        return results.Count - before;
    }

    public bool ValidateProxy(out string error)
    {
        int enabledCount = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var layers = new HashSet<BistroBuilderSpatialProxyLayer>();
        bool articulatedAnchor = false;

        for (int i = 0; i < parts.Count; i++)
        {
            BistroBuilderSpatialProxyPart part = parts[i];
            if (part == null || !part.enabled) continue;
            enabledCount++;
            layers.Add(part.layer);
            if (string.IsNullOrWhiteSpace(part.partId) || !ids.Add(part.partId))
            {
                error = "Adaptive Spatial Proxy contiene partId vacío o duplicado.";
                return false;
            }
            articulatedAnchor |= part.anchor != null && part.anchor != transform;
            if (part.shapeKind == BistroBuilderSpatialShapeKind.ConvexHull &&
                part.convexHull != null && part.convexHull.Count > 0 &&
                part.convexHull.Count < 3)
            {
                error = part.partId + ": ConvexHull necesita al menos 3 puntos.";
                return false;
            }
        }

        if (enabledCount == 0)
        {
            error = "Adaptive Spatial Proxy sin geometría espacial activa.";
            return false;
        }
        if (mode == BistroBuilderAdaptiveSpatialProxyMode.Simple && enabledCount != 1)
        {
            error = "Proxy Simple debe contener exactamente una pieza activa.";
            return false;
        }
        if (mode == BistroBuilderAdaptiveSpatialProxyMode.Compound && enabledCount < 2)
        {
            error = "Proxy Compound necesita al menos dos piezas activas.";
            return false;
        }
        if (mode == BistroBuilderAdaptiveSpatialProxyMode.Layered && layers.Count < 2)
        {
            error = "Proxy Layered necesita al menos dos capas espaciales.";
            return false;
        }
        if (mode == BistroBuilderAdaptiveSpatialProxyMode.Articulated && !articulatedAnchor)
        {
            error = "Proxy Articulated necesita al menos una pieza anclada a una articulación.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void Configure(BistroBuilderAdaptiveSpatialProxyMode proxyMode)
    {
        mode = proxyMode;
    }

    public void ClearParts()
    {
        parts.Clear();
    }

    public void AddPart(BistroBuilderSpatialProxyPart part)
    {
        if (part != null) parts.Add(part);
    }
#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderAdaptiveSpatialProxyMode proxyMode)
    {
        Configure(proxyMode);
    }

    public void ClearPartsForEditor()
    {
        ClearParts();
    }

    public void AddPartForEditor(BistroBuilderSpatialProxyPart part)
    {
        AddPart(part);
    }
#endif
}
