using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Projects the immutable premises envelope into Block 18 as logical walls.
/// These walls participate in room closure/snapping but are never persisted,
/// costed, selected or materialized as player-authored architecture.
/// </summary>
public static class BistroBuilderPremisesBoundaryRuntimeProvider
{
    public const string BoundaryDefinitionId = "wall.premises-boundary";
    private const string BoundaryIdPrefix = "premises-boundary-";

    public static bool TryResolve(Scene scene, List<BistroBuilderWallRecord> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        output.Clear();
        Renderer source = FindBoundarySource(scene);
        if (source == null) return false;

        Bounds bounds = source.bounds;
        if (bounds.size.x < 0.1f || bounds.size.z < 0.1f) return false;
        Vector2 southWest = new Vector2(bounds.min.x, bounds.min.z);
        Vector2 southEast = new Vector2(bounds.max.x, bounds.min.z);
        Vector2 northEast = new Vector2(bounds.max.x, bounds.max.z);
        Vector2 northWest = new Vector2(bounds.min.x, bounds.max.z);
        float elevation = bounds.min.y;

        output.Add(Create("south", southWest, southEast, elevation));
        output.Add(Create("east", southEast, northEast, elevation));
        output.Add(Create("north", northEast, northWest, elevation));
        output.Add(Create("west", northWest, southWest, elevation));
        return true;
    }

    public static bool IsBoundaryWall(BistroBuilderEditId id)
    {
        return id.IsValid && id.Value.StartsWith(BoundaryIdPrefix, StringComparison.Ordinal);
    }

    private static BistroBuilderWallRecord Create(
        string side, Vector2 start, Vector2 end, float elevation)
    {
        return new BistroBuilderWallRecord
        {
            wallId = new BistroBuilderEditId(BoundaryIdPrefix + side),
            buildPlaneId = "default",
            axisStart = start,
            axisEnd = end,
            baseElevation = elevation,
            height = 2.8f,
            thickness = 0.12f,
            wallDefinitionId = BoundaryDefinitionId
        };
    }

    private static Renderer FindBoundarySource(Scene scene)
    {
        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Renderer best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.gameObject.scene != scene) continue;
            string name = renderer.gameObject.name ?? string.Empty;
            if (name.StartsWith("DraftFloor", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("BB_RuntimeRoomFloor", StringComparison.OrdinalIgnoreCase)) continue;

            if (string.Equals(name, "Floor_Test", StringComparison.OrdinalIgnoreCase))
                return renderer;

            Bounds bounds = renderer.bounds;
            if (bounds.size.x < 0.1f || bounds.size.z < 0.1f) continue;
            bool floorNamed = name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              name.IndexOf("suelo", StringComparison.OrdinalIgnoreCase) >= 0;
            float area = bounds.size.x * bounds.size.z;
            float score = area + (floorNamed ? 100000f : 0f) +
                          (renderer.gameObject.layer == 3 ? 50000f : 0f);
            if (score <= bestScore) continue;
            best = renderer;
            bestScore = score;
        }
        return best;
    }
}
