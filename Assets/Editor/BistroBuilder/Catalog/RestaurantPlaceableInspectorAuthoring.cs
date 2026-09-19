using System;
using System.Collections.Generic;
using System.Linq;
using BistroBuilder.FurnitureFinishes;
using UnityEditor;
using UnityEngine;

public static class RestaurantPlaceableInspectorAuthoring
{
    private const string MenuRoot =
        "Tools/Bistro Builder/Catalog/";

    [MenuItem(MenuRoot + "Refresh Inspector Metadata")]
    public static void RefreshAll()
    {
        string[] guids =
            AssetDatabase.FindAssets("t:RestaurantPlaceableItemDefinition");

        int changedCount = 0;
        int validCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RestaurantPlaceableItemDefinition definition =
                    AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(path);

                if (definition == null)
                    continue;

                if (RefreshDefinition(definition, true))
                {
                    changedCount++;
                    EditorUtility.SetDirty(definition);
                }

                if (IsInspectorReady(definition))
                    validCount++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"BB_CATALOG_INSPECTOR_AUTHORING|PASS|items={guids.Length}|" +
            $"updated={changedCount}|ready={validCount}");
    }

    [MenuItem(MenuRoot + "Validate Inspector Metadata")]
    public static void ValidateAll()
    {
        string[] guids =
            AssetDatabase.FindAssets("t:RestaurantPlaceableItemDefinition");

        int errors = 0;
        int warnings = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RestaurantPlaceableItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(path);

            if (definition == null)
                continue;

            ValidateDefinition(definition, ref errors, ref warnings);
        }

        string verdict = errors == 0 ? "PASS" : "FAIL";
        Debug.Log(
            $"BB_CATALOG_INSPECTOR_VALIDATE|{verdict}|items={guids.Length}|" +
            $"errors={errors}|warnings={warnings}");
    }

    public static bool RefreshDefinition(
        RestaurantPlaceableItemDefinition definition,
        bool preserveManualValues)
    {
        if (definition == null)
            return false;

        Sprite preview = definition.CatalogIcon;
        Vector3 dimensions = CalculateDimensionsCentimeters(definition);
        FurnitureFinishProfile profile = FindFinishProfile(definition);
        RestaurantPlaceableInspectorRuleFlags rules =
            InferPlacementRules(definition);

        return definition.EditorApplyInspectorMetadata(
            preview,
            dimensions,
            profile,
            rules,
            preserveManualValues);
    }

    public static bool IsInspectorReady(
        RestaurantPlaceableItemDefinition definition)
    {
        return definition != null &&
            definition.Prefab != null &&
            definition.InspectorPreview != null &&
            definition.DimensionsCentimeters.sqrMagnitude > 0.001f;
    }

    private static void ValidateDefinition(
        RestaurantPlaceableItemDefinition definition,
        ref int errors,
        ref int warnings)
    {
        string assetPath = AssetDatabase.GetAssetPath(definition);

        if (definition.Prefab == null)
        {
            errors++;
            Debug.LogError(
                $"BB inspector: '{definition.DisplayName}' no tiene prefab. " +
                assetPath,
                definition);
            return;
        }

        if (definition.InspectorPreview == null)
        {
            warnings++;
            Debug.LogWarning(
                $"BB inspector: '{definition.DisplayName}' no tiene preview. " +
                assetPath,
                definition);
        }

        if (definition.DimensionsCentimeters.sqrMagnitude <= 0.001f)
        {
            warnings++;
            Debug.LogWarning(
                $"BB inspector: '{definition.DisplayName}' no tiene dimensiones. " +
                assetPath,
                definition);
        }

        if (string.IsNullOrWhiteSpace(definition.Description))
        {
            warnings++;
            Debug.LogWarning(
                $"BB inspector: '{definition.DisplayName}' no tiene descripción.",
                definition);
        }

        if ((definition.Category == RestaurantPlaceableItemCategory.Furniture ||
             definition.Category == RestaurantPlaceableItemCategory.Seating) &&
            (definition.InspectorRules &
             RestaurantPlaceableInspectorRuleFlags.FloorSurface) == 0)
        {
            warnings++;
            Debug.LogWarning(
                $"BB inspector: '{definition.DisplayName}' no declara colocación " +
                "sobre suelo aunque pertenece a mobiliario/asientos.",
                definition);
        }
    }

    private static Vector3 CalculateDimensionsCentimeters(
        RestaurantPlaceableItemDefinition definition)
    {
        if (definition == null || definition.Prefab == null)
            return Vector3.zero;

        string prefabPath =
            AssetDatabase.GetAssetPath(definition.Prefab.gameObject);

        if (string.IsNullOrWhiteSpace(prefabPath))
            return Vector3.zero;

        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                return Vector3.zero;

            if (!TryCalculateBounds(root, out Bounds bounds))
                return Vector3.zero;

            Vector3 size = bounds.size;
            return new Vector3(
                RoundCentimeters(size.x * 100f),
                RoundCentimeters(size.y * 100f),
                RoundCentimeters(size.z * 100f));
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"BB inspector: no se pudieron calcular dimensiones para " +
                $"'{definition.DisplayName}': {exception.Message}",
                definition);
            return Vector3.zero;
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool TryCalculateBounds(
        GameObject root,
        out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
            return true;

        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private static float RoundCentimeters(float value)
    {
        return Mathf.Round(Mathf.Max(0f, value) * 10f) / 10f;
    }

    private static RestaurantPlaceableInspectorRuleFlags InferPlacementRules(
        RestaurantPlaceableItemDefinition definition)
    {
        RestaurantPlaceableInspectorRuleFlags rules =
            RestaurantPlaceableInspectorRuleFlags.None;

        if (definition.Prefab != null &&
            definition.Prefab.GetComponent<RestaurantPlacementFootprint>() != null)
        {
            rules |=
                RestaurantPlaceableInspectorRuleFlags.RequiresClearance;
        }

        switch (definition.Category)
        {
            case RestaurantPlaceableItemCategory.Furniture:
            case RestaurantPlaceableItemCategory.Seating:
            case RestaurantPlaceableItemCategory.KitchenEquipment:
            case RestaurantPlaceableItemCategory.ServiceEquipment:
                rules |=
                    RestaurantPlaceableInspectorRuleFlags.FloorSurface;
                break;
        }

        return rules;
    }

    private static FurnitureFinishProfile FindFinishProfile(
        RestaurantPlaceableItemDefinition definition)
    {
        string[] guids =
            AssetDatabase.FindAssets("t:FurnitureFinishProfile");

        if (guids.Length == 0)
            return null;

        FurnitureFinishProfile best = null;
        int bestScore = 0;
        bool ambiguous = false;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            FurnitureFinishProfile profile =
                AssetDatabase.LoadAssetAtPath<FurnitureFinishProfile>(path);

            if (profile == null)
                continue;

            int score = ScoreProfile(definition, profile);
            if (score <= 0)
                continue;

            if (score > bestScore)
            {
                best = profile;
                bestScore = score;
                ambiguous = false;
            }
            else if (score == bestScore)
            {
                ambiguous = true;
            }
        }

        return ambiguous ? null : best;
    }

    private static int ScoreProfile(
        RestaurantPlaceableItemDefinition definition,
        FurnitureFinishProfile profile)
    {
        int score = 0;

        if (definition.Prefab != null &&
            profile.SourceAsset == definition.Prefab.gameObject)
        {
            score += 1000;
        }

        if (!string.IsNullOrWhiteSpace(profile.FurnitureId) &&
            Normalize(profile.FurnitureId) == definition.ItemId)
        {
            score += 500;
        }

        if (!string.IsNullOrWhiteSpace(profile.DisplayName) &&
            Normalize(profile.DisplayName) ==
            Normalize(definition.DisplayName))
        {
            score += 100;
        }

        return score;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(
                value.Trim()
                    .ToLowerInvariant()
                    .Where(char.IsLetterOrDigit)
                    .ToArray());
    }
}

[CustomEditor(typeof(RestaurantPlaceableItemDefinition))]
public sealed class RestaurantPlaceableItemDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RestaurantPlaceableItemDefinition definition =
            (RestaurantPlaceableItemDefinition)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(
            "Inspector contextual",
            EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                "Preview efectiva",
                definition.InspectorPreview,
                typeof(Sprite),
                false);

            Vector3 dimensions = definition.DimensionsCentimeters;
            EditorGUILayout.Vector3Field(
                "Dimensiones cm (A/H/F)",
                dimensions);

            EditorGUILayout.ObjectField(
                "Perfil de acabados",
                definition.FinishProfile,
                typeof(FurnitureFinishProfile),
                false);
        }

        if (GUILayout.Button("Actualizar metadatos automáticamente"))
        {
            Undo.RecordObject(
                definition,
                "Refresh Placeable Inspector Metadata");

            bool changed =
                RestaurantPlaceableInspectorAuthoring.RefreshDefinition(
                    definition,
                    false);

            if (changed)
            {
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }
        }

        if (!RestaurantPlaceableInspectorAuthoring.IsInspectorReady(definition))
        {
            EditorGUILayout.HelpBox(
                "La ficha contextual aún no está completa. Ejecuta la " +
                "actualización automática y revisa los campos pendientes.",
                MessageType.Warning);
        }
    }
}
