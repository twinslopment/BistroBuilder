using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Catalogue thumbnails rendered from the construction kit shipped in the game.</summary>
public static class RestaurantArchitectureCatalogAuthoring
{
    [MenuItem("Tools/Bistro Builder/Catalog/Refresh Architecture Previews")]
    public static void RefreshAll()
    {
        var kit = BistroBuilderConstructionAssetKit.Load();
        if (kit == null) throw new System.Exception("Missing construction kit");
        const string folder = "Assets/Resources/BistroBuilder/UI/Architecture";
        Directory.CreateDirectory(folder);
        foreach (var entry in RestaurantArchitectureCatalogPanel.WallEntries.Concat(RestaurantArchitectureCatalogPanel.SurfaceEntries))
        {
            var preview = new PreviewRenderUtility();
            GameObject model = null;
            Texture2D image = null;
            try
            {
                var prefab = entry.Id == "door" ? kit.doorPrefab : entry.Id == "window" ? kit.windowPrefab : null;
                model = prefab != null ? Object.Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.hideFlags = HideFlags.HideAndDontSave;
                if (prefab == null)
                {
                    model.transform.localScale = entry.Role == "floor" ? new Vector3(2, .08f, 2) : new Vector3(2, entry.Height, entry.Thickness);
                    model.GetComponent<Renderer>().sharedMaterial = entry.Role == "floor" ? kit.floorMaterial : kit.wallMaterial;
                }
                preview.AddSingleGO(model);
                var renderers = model.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x, bounds.size.z) * .75f;
                preview.camera.transform.rotation = Quaternion.Euler(22, -32, 0);
                preview.camera.transform.position = bounds.center - preview.camera.transform.forward * 12;
                preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 30;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.945f, .929f, .901f, 1);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40, -30, 0);
                preview.lights[1].intensity = .65f;
                preview.ambientColor = new Color(.65f, .65f, .65f, 1);
                preview.BeginStaticPreview(new Rect(0, 0, 384, 384));
                preview.Render(true);
                image = preview.EndStaticPreview();
                var path = folder + "/" + entry.Id + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            finally
            {
                if (image != null) Object.DestroyImmediate(image);
                preview.Cleanup();
                if (model != null) Object.DestroyImmediate(model);
            }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:RestaurantPlaceableItemDefinition"))
        {
            var definition = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition.Category != RestaurantPlaceableItemCategory.Decoration || !definition.DisplayName.ToLowerInvariant().Contains("planta")) continue;
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("catalogSubcategory").stringValue = "plants";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("BB_ARCHITECTURE_PREVIEWS_PASS");
    }
}
