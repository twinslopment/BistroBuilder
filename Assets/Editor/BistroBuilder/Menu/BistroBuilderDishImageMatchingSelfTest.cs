using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Real-catalog photo test: reads the 29 source images, verifies deterministic
/// ID and name resolution, checks false positives, and leaves gameplay intact.
/// </summary>
public static class BistroBuilderDishImageMatchingSelfTest
{
    private sealed class Report
    {
        internal int Passed;
        internal int Failed;
        internal readonly List<string> Lines = new List<string>();

        internal void Check(bool ok, string message)
        {
            if (ok) Passed++; else Failed++;
            Lines.Add((ok ? "PASS " : "FAIL ") + message);
        }
    }

    [MenuItem("Tools/Bistro Builder/Menu/Run Dish Photo Matching Self-Test")]
    public static void RunFromMenu()
    {
        RunTests();
    }

    public static void RunBatch()
    {
        RunTests();
    }

    private static void RunTests()
    {
        Report report = new Report();

        try
        {
            BistroBuilderDishImageCatalogImporter.Rebuild();
            BistroBuilderDishImageCatalog catalog =
                BistroBuilderDishImageCatalog.LoadDefault();

            report.Check(catalog != null, "Default Resources catalog loads");
            if (catalog == null)
                throw new InvalidOperationException("Missing DishImageCatalog");

            string folder = BistroBuilderDishImageCatalogImporter.ImageFolder +
                "/Inbox";
            string[] guids = AssetDatabase.FindAssets("t:Texture2D",
                new[] { folder });
            Dictionary<string, Sprite> photos =
                new Dictionary<string, Sprite>(StringComparer.Ordinal);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string ext = Path.GetExtension(path);
                if (!string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".webp", StringComparison.OrdinalIgnoreCase))
                    continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                report.Check(sprite != null, "Imported Sprite: " + stem);
                if (sprite == null) continue;

                string canonical =
                    BistroBuilderDishImageKeyUtility.CanonicalKey(stem);
                report.Check(!photos.ContainsKey(canonical),
                    "No duplicate canonical photo key: " + canonical);
                photos[canonical] = sprite;

                report.Check(
                    catalog.GetImageOrNull("dish_" + canonical) == sprite,
                    "Stable prefixed ID resolves: " + canonical);
                report.Check(
                    catalog.GetImageOrNull("user_custom_dish_id",
                        stem.Replace('_', ' ')) == sprite,
                    "Display name fallback resolves: " + canonical);
            }

            report.Check(photos.Count == 29,
                "All 29 supplied photos imported and indexed");
            report.Check(catalog.Entries.Count == photos.Count,
                "No image entry omitted or duplicated");

            report.Check(
                catalog.GetImageOrNull("dish_fabada_asturiana") ==
                    photos["fabada_asturiana"],
                "Existing canonical prefixed DishId remains unchanged");
            report.Check(
                catalog.GetImageOrNull("random_id", "Tortilla de patatas") ==
                    photos["tortilla_patatas"],
                "Supported historical alternate name");
            report.Check(
                catalog.GetImageOrNull("random_id", "Pimientos de Padrón") ==
                    photos["pimientos_padron"],
                "Accent-insensitive and alias-aware resolution");
            report.Check(
                catalog.GetImageOrNull("random_id", "Pan tumaca") ==
                    photos["pan_con_tomate"],
                "Legacy alias resolution");
            report.Check(
                catalog.GetImageOrNull("random_id", "Tarta de queso") ==
                    photos["tarta_queso"],
                "Natural displayed dessert name resolves");
            report.Check(
                catalog.GetImageOrNull("random_id",
                    "Tabla de embutidos ibéricos") ==
                    photos["tabla_embutidos_ibericos"],
                "Natural displayed shared-board name resolves");
            report.Check(
                catalog.GetImageOrNull("random_id", "Croquetas") == null &&
                catalog.GetImageOrNull("random_id", "Pincho de tortilla") == null,
                "No photo invented for unmatched items");

            // Exact stable ID must win even if a display name refers to another dish.
            report.Check(
                catalog.GetImageOrNull("dish_fabada_asturiana",
                    "Tarta de queso") == photos["fabada_asturiana"],
                "Exact DishId wins over display-name fallback");

            VerifyAmbiguousKey(report, photos);

            // The definition registry is authoritative: do not manufacture 26
            // recipes/prices just to make staged images appear as playable dishes.
            int known = AssetDatabase.FindAssets("t:BistroBuilderDishDefinition").Length;
            report.Check(known >= 3,
                "Existing dish definitions remain available; test adds none");
        }
        catch (Exception ex)
        {
            report.Check(false, "Unhandled exception: " + ex);
        }

        string root = Directory.GetParent(Application.dataPath).FullName;
        string reportPath = Path.Combine(root, "Logs",
            "DishPhotoMatching_20261027.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath,
            "BB_DISH_PHOTO_MATCHING|PASS=" + report.Passed +
            "|FAIL=" + report.Failed + Environment.NewLine +
            string.Join(Environment.NewLine, report.Lines));

        if (report.Failed != 0)
            throw new InvalidOperationException(
                "Dish photo matching FAILED (" + report.Failed +
                "). See " + reportPath);

        Debug.Log("BB_DISH_PHOTO_MATCHING_PASS|PASS=" + report.Passed +
            "|FAIL=0|REPORT=" + reportPath);
    }

    private static void VerifyAmbiguousKey(
        Report report, Dictionary<string, Sprite> photos)
    {
        BistroBuilderDishImageCatalog temp =
            ScriptableObject.CreateInstance<BistroBuilderDishImageCatalog>();

        try
        {
            SerializedObject serialized = new SerializedObject(temp);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = 2;

            SerializedProperty first = entries.GetArrayElementAtIndex(0);
            first.FindPropertyRelative("dishId").stringValue = "dish_same";
            first.FindPropertyRelative("image").objectReferenceValue =
                photos["anchoas"];

            SerializedProperty second = entries.GetArrayElementAtIndex(1);
            second.FindPropertyRelative("dishId").stringValue = "same";
            second.FindPropertyRelative("image").objectReferenceValue =
                photos["fabada_asturiana"];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            report.Check(temp.GetImageOrNull("customer_custom_id",
                "same") == null, "Ambiguous name does not pick arbitrary photo");
            report.Check(temp.GetImageOrNull("dish_same") == photos["anchoas"],
                "Exact ID still works for ambiguous photo keys");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temp);
        }
    }
}
