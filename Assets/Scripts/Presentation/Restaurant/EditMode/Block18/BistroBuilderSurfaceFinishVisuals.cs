using UnityEngine;

/// <summary>Render-only projection of saved and draft finish patches; no colliders or spatial authority.</summary>
public static class BistroBuilderSurfaceFinishVisuals
{
    public static void Build(Transform parent, BistroBuilderEditDocument document)
    {
        var kit = BistroBuilderConstructionAssetKit.Load();
        if (document == null || kit == null || kit.floorMaterial == null) return;
        foreach (var patch in document.surfaces)
        {
            if (patch == null || patch.surfaceRole != "floor" || patch.finishDefinitionId != "finish.floor.default" ||
                patch.fallbackBoundary.Count < 3) continue;
            var go = new GameObject("SurfaceFloor_" + patch.surfacePatchId.Value);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = BistroBuilderPlanarGeometryBuilder.BuildHorizontalPolygon(patch.fallbackBoundary, .018f);
            go.AddComponent<MeshRenderer>().sharedMaterial = kit.floorMaterial;
        }
    }
}
