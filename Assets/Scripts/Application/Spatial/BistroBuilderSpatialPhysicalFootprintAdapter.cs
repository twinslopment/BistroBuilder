using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opt-in projection of root-owned BBSIS static boxes into placement/navigation.
/// The proxy remains the geometry authority; the footprint is its area envelope.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantPlacementFootprint), typeof(BistroBuilderAdaptiveSpatialProxy))]
public sealed class BistroBuilderSpatialPhysicalFootprintAdapter : MonoBehaviour
{
    public const int MaximumStaticParts = 128;

    public bool TryWriteShapes(Vector3 position, Quaternion rotation,
        List<RestaurantPlacementShape> output, out string error)
    {
        error = string.Empty;
        RestaurantPlacementFootprint footprint = GetComponent<RestaurantPlacementFootprint>();
        BistroBuilderAdaptiveSpatialProxy proxy = GetComponent<BistroBuilderAdaptiveSpatialProxy>();
        Vector3 scale = transform.lossyScale;
        if (footprint == null || proxy == null || !proxy.ValidateProxy(out error) ||
            !Finite(position) || !Finite(rotation) || !Finite(scale) ||
            scale.x <= 0f || scale.y <= 0f || scale.z <= 0f ||
            Mathf.Abs(Quaternion.Dot(rotation, rotation) - 1f) > 0.001f ||
            Vector3.Dot(rotation * Vector3.up, Vector3.up) < 0.9999f ||
            !Finite(footprint.LocalCenter) || !Finite(footprint.Size) ||
            footprint.Size.x <= 0f || footprint.Size.y <= 0f ||
            !Finite(footprint.MinimumClearance) || footprint.MinimumClearance < 0f)
        {
            if (string.IsNullOrEmpty(error)) error = "Compound physical envelope or pose is invalid.";
            return false;
        }

        int count = 0;
        for (int i = 0; i < proxy.Parts.Count; i++)
        {
            BistroBuilderSpatialProxyPart part = proxy.Parts[i];
            if (part == null || !part.enabled || part.layer != BistroBuilderSpatialProxyLayer.Static) continue;
            if (++count > MaximumStaticParts ||
                part.shapeKind != BistroBuilderSpatialShapeKind.OrientedBox ||
                (part.anchor != null && part.anchor != transform) ||
                !Finite(part.localCenter) || !Finite(part.size) ||
                part.size.x <= 0f || part.size.y <= 0f)
            {
                error = "Physical projection requires bounded, finite, root-owned static boxes.";
                return false;
            }
            Vector3 offset = part.localCenter - footprint.LocalCenter;
            Vector2 half = WorldHalfExtents(part, scale);
            Vector3 center = position + rotation * Vector3.Scale(part.localCenter, scale);
            if (!Finite(half) || !Finite(center) ||
                Mathf.Abs(offset.x * scale.x) + half.x > footprint.Size.x * scale.x * 0.5f + 0.0001f ||
                Mathf.Abs(offset.z * scale.z) + half.y > footprint.Size.y * scale.z * 0.5f + 0.0001f)
            {
                error = "A BBSIS static box exceeds its placement envelope.";
                return false;
            }
        }
        if (count == 0)
        {
            error = "Compound physical projection has no static body.";
            return false;
        }

        // Only write after validating all parts: never expose a partially checked body.
        for (int i = 0; i < proxy.Parts.Count; i++)
        {
            BistroBuilderSpatialProxyPart part = proxy.Parts[i];
            if (part == null || !part.enabled || part.layer != BistroBuilderSpatialProxyLayer.Static) continue;
            output.Add(new RestaurantPlacementShape(
                position + rotation * Vector3.Scale(part.localCenter, scale),
                rotation * Vector3.right, rotation * Vector3.forward,
                WorldHalfExtents(part, scale),
                footprint.MinimumClearance,
                part.hasVerticalExtent ? BistroBuilderSpatialHeightRange.Between(
                    position.y + part.localCenter.y * scale.y - part.height * scale.y * 0.5f,
                    position.y + part.localCenter.y * scale.y + part.height * scale.y * 0.5f) : default));
        }
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    // Same minimum extents as BBSIS BuildWorldVolume, including very small source units.
    private static Vector2 WorldHalfExtents(BistroBuilderSpatialProxyPart part, Vector3 scale) =>
        new Vector2(Mathf.Max(0.01f, part.size.x * scale.x * 0.5f),
            Mathf.Max(0.01f, part.size.y * scale.z * 0.5f));
    private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
}

/// <summary>Shared physical projection, with a conservative envelope for invalid opt-in data.</summary>
public static class BistroBuilderPhysicalPlacementGeometry
{
    public static bool TryWriteShapes(RestaurantPlacementFootprint footprint,
        Vector3 position, Quaternion rotation, List<RestaurantPlacementShape> output, out string error)
    {
        output.Clear();
        error = string.Empty;
        if (footprint == null) { error = "Missing physical footprint."; return false; }
        BistroBuilderSpatialPhysicalFootprintAdapter adapter =
            footprint.GetComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
        if (adapter != null && adapter.enabled)
        {
            if (adapter.TryWriteShapes(position, rotation, output, out error)) return true;
            output.Clear();
            RestaurantPlacementShape envelope = footprint.BuildShapeAtPose(position, rotation);
            output.Add(new RestaurantPlacementShape(envelope.Center, envelope.RightAxis, envelope.ForwardAxis,
                envelope.HalfExtents, envelope.MinimumClearance, default, true));
            return false;
        }
        output.Add(footprint.BuildShapeAtPose(position, rotation));
        return true;
    }

    public static RestaurantPlacementConflictType EvaluateConflict(
        IReadOnlyList<RestaurantPlacementShape> first, IReadOnlyList<RestaurantPlacementShape> second)
    {
        RestaurantPlacementConflictType result = RestaurantPlacementConflictType.None;
        for (int i = 0; i < first.Count; i++)
        for (int j = 0; j < second.Count; j++)
        {
            RestaurantPlacementConflictType conflict =
                RestaurantPlacementCollisionUtility.EvaluateConflict(first[i], second[j]);
            if (conflict == RestaurantPlacementConflictType.PhysicalOverlap) return conflict;
            if (conflict != RestaurantPlacementConflictType.None) result = conflict;
        }
        return result;
    }
}
