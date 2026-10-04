using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Native authoring for the demonstrated presentation regressions.
/// No queue/status edits and no generated colour variants.</summary>
public static class BistroBuilderPresentationReviewRepair
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string CatalogPath = "Assets/Data/Restaurant/EditMode/Catalog/RestaurantPlaceableCatalog_Main.asset";

    [MenuItem("Tools/Bistro Builder/Presentation/Prepare Video Review Fixes")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Guarda los cambios de escena y sal de Play Mode antes de reparar contenido.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        var catalog = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableCatalogDefinition>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Falta el catálogo principal.");
        int thumbnails = 0, chairColours = 0;
        foreach (var item in catalog.Items)
        {
            if (item == null) continue;
            if (item.ItemId == "pf_bb_chair_master_001_white" ||
                item.ItemId == "pf_bb_chair_master_001_yellow" ||
                item.ItemId == "pf_bb_chair_master_001_red" ||
                item.ItemId == "pf_bb_chair_master_001_olive")
            {
                if (!item.HasValidPrefab) throw new InvalidOperationException("Referencia de silla inválida: " + item.ItemId);
                chairColours++;
                Debug.Log("[PRESENTATION REVIEW] Existing chair colour: " + item.DisplayName);
            }
            if (item.Prefab == null || item.Prefab.GetComponent<RestaurantTable>() == null) continue;
            bool primitive = false;
            foreach (var filter in item.Prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.sharedMesh.name == "Cube" &&
                    !AssetDatabase.Contains(filter.sharedMesh)) primitive = true;
            // Built-in primitive meshes have a built-in asset path; imported GLBs
            // remain untouched and keep their source previews and proof hashes.
            if (!primitive)
                foreach (var filter in item.Prefab.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null && filter.sharedMesh.name == "Cube" &&
                        AssetDatabase.GetAssetPath(filter.sharedMesh) == "Library/unity default resources") primitive = true;
            if (!primitive) continue;
            var result = BistroBuilderCatalogThumbnailService.GenerateAndAssign(item, false, true);
            if (!result.Succeeded) throw new InvalidOperationException("Miniatura no regenerada: " + item.ItemId);
            if (item.CatalogIcon != null &&
                (item.InspectorPreview == null || BistroBuilderCatalogThumbnailService.IsGeneratedIcon(item.InspectorPreview)))
            {
                var serialized = new SerializedObject(item);
                serialized.FindProperty("inspectorPreview").objectReferenceValue = item.CatalogIcon;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }
            thumbnails++;
        }
        if (chairColours != 4) throw new InvalidOperationException("El catálogo no contiene los cuatro acabados existentes de silla.");
        if (thumbnails < 2) throw new InvalidOperationException("No se comprobaron las dos mesas básicas del catálogo.");
        int removed = BistroBuilderBarServiceInstaller.RetireLegacyBar(scene);
        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("No se guardó la retirada de la barra antigua.");
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/presentation-review-authoring.txt",
            DateTime.UtcNow.ToString("O") + "\nNative authoring completed; runtime acceptance remains required.\n" +
            "legacyRemoved=" + removed + "\nprimitiveTableThumbnails=" + thumbnails + "\nexistingChairColours=" + chairColours + "\n");
        Debug.Log("[PRESENTATION REVIEW] Native authoring completed. Runtime acceptance is still required.");
    }
}
