using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BBPLFSVerticalSliceInstaller
{
    private const string RootName = "BBPLFS_V1";
    private const string DataFolder = "Assets/Data/Restaurant/ProceduralLayout";
    private const string ProfilesFolder = DataFolder + "/Profiles";

    [MenuItem("Bistro Builder/BBPLFS/Install Vertical Slice")]
    public static void Install()
    {
        EnsureFolder("Assets/Data/Restaurant", "ProceduralLayout");
        EnsureFolder(DataFolder, "Profiles");

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Install BBPLFS V1");
        }

        BBPLFSPremisesCaptureService premises = GetOrAdd<BBPLFSPremisesCaptureService>(root);
        BBPLFSAssetLayoutCatalog layoutCatalog = GetOrAdd<BBPLFSAssetLayoutCatalog>(root);
        BBPLFSLayoutGenerator generator = GetOrAdd<BBPLFSLayoutGenerator>(root);
        BBPLFSValidationHub validation = GetOrAdd<BBPLFSValidationHub>(root);
        BBPLFSPreviewService preview = GetOrAdd<BBPLFSPreviewService>(root);
        BBPLFSMaterializationService materialization = GetOrAdd<BBPLFSMaterializationService>(root);
        BBPLFSVerticalSliceController controller = GetOrAdd<BBPLFSVerticalSliceController>(root);

        RestaurantEditModeService editMode = UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>();
        RestaurantPlaceableCatalogService placeableCatalog = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>();
        RestaurantPlaceableCreationService creation = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableCreationService>();
        RestaurantPlacementTransactionService transaction = UnityEngine.Object.FindFirstObjectByType<RestaurantPlacementTransactionService>();
        RestaurantPlaceableDeletionService deletion = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableDeletionService>();
        materialization.EditorConfigure(editMode, placeableCatalog, creation, transaction, deletion);

        List<BBPLFSAssetLayoutProfile> profiles = BuildDefaultProfiles();
        layoutCatalog.EditorSetProfiles(profiles.ToArray());

        RestaurantArea selectedArea = FindPreferredArea();
        controller.EditorConfigure(
            premises,
            layoutCatalog,
            generator,
            validation,
            preview,
            materialization,
            selectedArea);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(layoutCatalog);
        EditorUtility.SetDirty(materialization);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "BBPLFS V1 vertical slice installed. " +
            $"Profiles: {profiles.Count}. Selected area: {(selectedArea != null ? selectedArea.AreaId : "none")}.",
            root);
        Selection.activeGameObject = root;
    }

    private static List<BBPLFSAssetLayoutProfile> BuildDefaultProfiles()
    {
        List<BBPLFSAssetLayoutProfile> profiles = new();
        RestaurantPlaceableItemDefinition[] definitions = LoadAllPlaceableDefinitions();

        RestaurantPlaceableItemDefinition table = FindDefinition(definitions, RestaurantPlaceableItemCategory.Furniture, "table", "mesa");
        RestaurantPlaceableItemDefinition chair = FindDefinition(definitions, RestaurantPlaceableItemCategory.Seating, "chair", "silla");

        if (table != null)
        {
            profiles.Add(GetOrCreateProfile(
                table,
                BBPLFSLayoutRole.DiningTable,
                "dining_table_2",
                new Vector2(2f, 1f),
                2));
        }
        if (chair != null)
        {
            profiles.Add(GetOrCreateProfile(
                chair,
                BBPLFSLayoutRole.DiningSeat,
                "dining_chair",
                new Vector2(0.5f, 0.55f),
                1));
        }

        return profiles;
    }

    private static RestaurantPlaceableItemDefinition[] LoadAllPlaceableDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:RestaurantPlaceableItemDefinition");
        List<RestaurantPlaceableItemDefinition> definitions = new();

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            RestaurantPlaceableItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(path);

            if (definition != null && definition.HasValidPrefab)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
        return definitions.ToArray();
    }

    private static RestaurantPlaceableItemDefinition FindDefinition(
        IReadOnlyList<RestaurantPlaceableItemDefinition> definitions,
        RestaurantPlaceableItemCategory category,
        params string[] tokens)
    {
        for (int index = 0; index < definitions.Count; index++)
        {
            RestaurantPlaceableItemDefinition definition = definitions[index];
            if (definition == null || definition.Category != category)
            {
                continue;
            }
            string haystack = (definition.ItemId + " " + definition.DisplayName).ToLowerInvariant();
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                if (haystack.Contains(tokens[tokenIndex]))
                {
                    return definition;
                }
            }
        }

        return null;
    }

    private static BBPLFSAssetLayoutProfile GetOrCreateProfile(
        RestaurantPlaceableItemDefinition definition,
        BBPLFSLayoutRole role,
        string family,
        Vector2 footprint,
        int capacity)
    {
        string path = ProfilesFolder + "/BBPLFSAssetProfile_" + definition.ItemId + ".asset";
        BBPLFSAssetLayoutProfile profile = AssetDatabase.LoadAssetAtPath<BBPLFSAssetLayoutProfile>(path);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<BBPLFSAssetLayoutProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        profile.EditorConfigure(
            definition,
            role,
            family,
            footprint,
            capacity,
            new[] { "contemporary", "restaurant" });

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static RestaurantArea FindPreferredArea()
    {
        RestaurantArea[] areas = UnityEngine.Object.FindObjectsByType<RestaurantArea>(FindObjectsSortMode.None);
        Array.Sort(areas, (a, b) => string.CompareOrdinal(a != null ? a.AreaId : string.Empty, b != null ? b.AreaId : string.Empty));

        for (int index = 0; index < areas.Length; index++)
        {
            if (areas[index] != null && areas[index].AreaId != null && areas[index].AreaId.ToLowerInvariant().Contains("dining"))
            {
                return areas[index];
            }
        }

        return areas.Length > 0 ? areas[0] : null;
    }
    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        if (root.TryGetComponent(out T component))
        {
            return component;
        }

        return Undo.AddComponent<T>(root);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string fullPath = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
