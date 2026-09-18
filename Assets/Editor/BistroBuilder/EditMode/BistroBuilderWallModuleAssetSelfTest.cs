using System;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderWallModuleAssetSelfTest
{
    private const string ResourcePath = "BistroBuilder/Architecture/BB_Wall_Module_Master_001";

    [MenuItem("Tools/Bistro Builder/Edit Mode/18N - Wall Module Asset Self-Test")]
    public static void RunFromMenu() => Run(false);

    public static void RunBatch() => Run(true);

    private static void Run(bool batch)
    {
        GameObject instance = null;
        GameObject host = null;
        try
        {
            AssetDatabase.Refresh();
            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            Require(prefab != null, "wall module resource is missing");
            instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "BB_Wall_Module_AssetProbe";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);
            Require(filters.Length > 0, "wall module has no mesh filter");
            int vertexCount = 0;
            for (int i = 0; i < filters.Length; i++)
                if (filters[i].sharedMesh != null) vertexCount += filters[i].sharedMesh.vertexCount;
            Require(vertexCount >= 9000, "wall module mesh is unexpectedly simplified or empty");

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length > 0, "wall module has no renderer");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 size = bounds.size;
            Require(size.x > 0.45f && size.x < 0.55f, "unexpected wall module width: " + size.x);
            Require(size.y > 1.80f && size.y < 2.00f, "unexpected wall module height: " + size.y);
            Require(size.z > 0.02f && size.z < 0.06f, "unexpected wall module thickness: " + size.z);

            UnityEngine.Object.DestroyImmediate(instance);
            instance = null;
            host = new GameObject("BB_Wall_Module_MaterializerProbe");
            var materializer = host.AddComponent<BistroBuilderArchitectureRuntimeMaterializer>();
            var document = new BistroBuilderEditDocument();
            var wall = new BistroBuilderWallRecord
            {
                wallId = BistroBuilderEditId.NewId(),
                buildPlaneId = "default",
                axisStart = Vector2.zero,
                axisEnd = new Vector2(3f, 0f),
                baseElevation = 0f,
                height = 2.8f,
                thickness = 0.12f,
                wallDefinitionId = "wall.default"
            };
            document.walls.Add(wall);
            document.openings.Add(new BistroBuilderOpeningRecord
            {
                openingId = BistroBuilderEditId.NewId(),
                hostWallId = wall.wallId,
                axisPosition01 = 0.5f,
                width = 0.9f,
                bottomElevation = 0f,
                height = 2.1f,
                openingType = "door",
                fillDefinitionId = "door"
            });
            BistroBuilderArchitectureMaterializationSummary summary = materializer.Rebuild(document);
            Require(summary.wallObjects == 1, "materializer did not create the wall root");
            Require(materializer.HasWallVisualModule, "materializer did not resolve the wall module resource");

            Transform generated = host.transform.Find("BB18_GeneratedArchitecture");
            Require(generated != null && generated.childCount == 1, "generated architecture root is invalid");
            Transform wallRoot = generated.GetChild(0);
            int visualCount = 0;
            bool doorBlocked = false;
            Renderer[] wallRenderers = wallRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < wallRenderers.Length; i++)
            {
                Renderer renderer = wallRenderers[i];
                if (renderer.gameObject.name.StartsWith("WallVisualModule_")) visualCount++;
                if (renderer.enabled && renderer.bounds.Contains(new Vector3(1.5f, 1f, 0f))) doorBlocked = true;
            }
            Require(visualCount >= 5, "wall visual modules were not tiled across the wall");
            Require(!doorBlocked, "wall visual modules are covering the door opening");
            Require(materializer.SetWallVisualVisibility(wall.wallId, false), "could not hide committed wall visuals");
            Renderer[] hiddenRenderers = wallRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < hiddenRenderers.Length; i++)
                Require(!hiddenRenderers[i].enabled, "hidden wall still has an enabled renderer");
            Require(materializer.SetWallVisualVisibility(wall.wallId, true), "could not restore committed wall visuals");
            Renderer rootRenderer = wallRoot.GetComponent<Renderer>();
            Require(rootRenderer != null && !rootRenderer.enabled,
                "restoring wall visuals incorrectly re-enabled the fallback wall mesh");
            bool restoredModule = false;
            for (int i = 0; i < hiddenRenderers.Length; i++)
                if (hiddenRenderers[i].gameObject.name.StartsWith("WallVisualModule_") && hiddenRenderers[i].enabled)
                    { restoredModule = true; break; }
            Require(restoredModule, "wall modules were not restored after draft visibility handoff");

            var previewHost = new GameObject("BB_Wall_Module_DraftPreviewProbe");
            previewHost.transform.SetParent(host.transform, false);
            Require(materializer.TryCreateWallVisualPreview(previewHost.transform, wall, document.openings),
                "draft preview did not create the wall module");
            Require(previewHost.GetComponentsInChildren<Renderer>(true).Length > 0,
                "draft preview has no visible wall renderer");

            Debug.Log("BB_WALL_MODULE_SELFTEST|PASS|VERTICES=" + vertexCount +
                "|BOUNDS=" + size.ToString("F4") + "|MODULES=" + visualCount);
        }
        catch (Exception exception)
        {
            Debug.LogError("BB_WALL_MODULE_SELFTEST|FAIL|" + exception.Message);
            if (batch) EditorApplication.Exit(1);
            throw;
        }
        finally
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
        }
        if (batch) EditorApplication.Exit(0);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
