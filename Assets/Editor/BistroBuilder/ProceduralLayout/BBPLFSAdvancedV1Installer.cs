using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BBPLFSAdvancedV1Installer
{
    private const string RootName = "BBPLFS_V1";
    private const string DataFolder = "Assets/Data/Restaurant/ProceduralLayout";
    private const string ProfilesFolder = DataFolder + "/Profiles";
    private const string SetsFolder = DataFolder + "/FurnishingSets";

    public static void InstallPrototypeFromCommandLine()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
        Install();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("BBPLFS_ADVANCED_INSTALL_PASS");
    }

    public static void SmokeTestFromCommandLine()
    {
        string scenePath = "Assets/Scenes/Prototype_Restaurant.unity";
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        BBPLFSAutoFurnishService service = UnityEngine.Object.FindFirstObjectByType<BBPLFSAutoFurnishService>();
        if (service == null || service.SelectedArea == null)
            throw new InvalidOperationException("BBPLFS Auto Furnish no está correctamente instalado.");

        PrimeSceneRegistriesForEditorSmokeTest();

        RestaurantEditModeService editMode = UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>();
        if (editMode == null) throw new InvalidOperationException("Modo Edición no está disponible para el smoke test.");
        if (!editMode.IsEditModeActive && !editMode.TryEnterEditMode(out _, out string editError))
            throw new InvalidOperationException("No se puede activar Modo Edición: " + editError);

        RestaurantArea targetArea = service.SelectedArea;
        RestaurantAreaMember[] members = UnityEngine.Object.FindObjectsByType<RestaurantAreaMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        int evacuated = 0;
        for (int i = 0; i < members.Length; i++)
        {
            RestaurantAreaMember member = members[i];
            if (member == null || member.AssignedArea != targetArea || !member.TryGetComponent(out RestaurantPlaceableObject _)) continue;
            member.SetArea(null);
            member.transform.position += Vector3.right * 1000f;
            evacuated++;
        }
        UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>()?.RebuildNavigationTopology();

        if (!service.Generate(out string generateMessage))
            throw new InvalidOperationException("BBPLFS Generate FAIL: " + generateMessage);
        if (service.Candidates.Count == 0)
            throw new InvalidOperationException("BBPLFS Generate no devolvió candidatos válidos.");
        if (!service.Preview(0, out string previewMessage))
            throw new InvalidOperationException("BBPLFS Preview FAIL: " + previewMessage);

        int before = UnityEngine.Object.FindObjectsByType<RestaurantPlaceableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        if (!service.Accept(out string acceptMessage))
            throw new InvalidOperationException("BBPLFS Accept FAIL: " + acceptMessage);
        int after = UnityEngine.Object.FindObjectsByType<RestaurantPlaceableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        if (after <= before)
            throw new InvalidOperationException("BBPLFS Accept no materializó nuevos objetos editables.");

        Debug.Log("BBPLFS_ADVANCED_SMOKE_PASS|evacuated=" + evacuated + "|candidates=" + service.Candidates.Count + "|materialized=" + (after - before));
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }

    private static void PrimeSceneRegistriesForEditorSmokeTest()
    {
        RestaurantAreaRegistry areaRegistry = UnityEngine.Object.FindFirstObjectByType<RestaurantAreaRegistry>();
        if (areaRegistry == null)
            throw new InvalidOperationException("RestaurantAreaRegistry no está disponible.");

        RestaurantArea[] areas = UnityEngine.Object.FindObjectsByType<RestaurantArea>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < areas.Length; i++)
        {
            if (areas[i] != null && !areaRegistry.ContainsArea(areas[i]))
                areaRegistry.RegisterArea(areas[i]);
        }

        RestaurantPlaceableCatalogService placeableCatalog =
            UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>();
        if (placeableCatalog == null)
            throw new InvalidOperationException("RestaurantPlaceableCatalogService no está disponible.");
        placeableCatalog.RebuildCatalog();
        if (placeableCatalog.AvailableItemCount == 0)
            throw new InvalidOperationException("El catálogo clásico de colocables está vacío.");

        Physics.SyncTransforms();
    }

    [MenuItem("Bistro Builder/BBPLFS/Install Advanced V1")]
    public static void Install()
    {
        BBPLFSVerticalSliceInstaller.Install();
        EnsureFolder(DataFolder, "FurnishingSets");
        GameObject root = GameObject.Find(RootName);
        if (root == null) throw new InvalidOperationException("No se ha podido crear BBPLFS_V1.");
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

        BBPLFSPremisesCaptureService premises = GetOrAdd<BBPLFSPremisesCaptureService>(root);
        BBPLFSAssetLayoutCatalog assetCatalog = GetOrAdd<BBPLFSAssetLayoutCatalog>(root);
        BBPLFSFurnishingSetCatalog setCatalog = GetOrAdd<BBPLFSFurnishingSetCatalog>(root);
        BBPLFSAdvancedLayoutGenerator generator = GetOrAdd<BBPLFSAdvancedLayoutGenerator>(root);
        BBPLFSCandidateValidationService validation = GetOrAdd<BBPLFSCandidateValidationService>(root);
        BBPLFSPreviewService preview = GetOrAdd<BBPLFSPreviewService>(root);
        BBPLFSMaterializationService materialization = GetOrAdd<BBPLFSMaterializationService>(root);
        BBPLFSAutoFurnishService autoFurnish = GetOrAdd<BBPLFSAutoFurnishService>(root);

        List<BBPLFSAssetLayoutProfile> profiles = BuildProfiles();
        assetCatalog.EditorSetProfiles(profiles.ToArray());
        List<BBPLFSFurnishingSet> sets = BuildSets();
        setCatalog.EditorSetSets(sets.ToArray());
        RestaurantArea selectedArea = FindPreferredArea();
        autoFurnish.EditorConfigure(premises, assetCatalog, setCatalog, generator, validation,
            preview, materialization, selectedArea);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(assetCatalog);
        EditorUtility.SetDirty(setCatalog);
        EditorUtility.SetDirty(autoFurnish);
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("BBPLFS Advanced V1 instalado: " + profiles.Count + " perfiles, " + sets.Count + " sets.", root);
    }

    private static List<BBPLFSAssetLayoutProfile> BuildProfiles()
    {
        string[] guids = AssetDatabase.FindAssets("t:RestaurantPlaceableItemDefinition");
        var result = new List<BBPLFSAssetLayoutProfile>();
        for (int i = 0; i < guids.Length; i++)
        {
            RestaurantPlaceableItemDefinition definition = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (definition == null || !definition.HasValidPrefab) continue;
            BBPLFSLayoutRole role = Classify(definition);
            if (role == BBPLFSLayoutRole.Other && definition.Category == RestaurantPlaceableItemCategory.Decoration) continue;
            Vector2 footprint = definition.Prefab.TryGetComponent(out RestaurantPlacementFootprint fp) ? fp.Size : Vector2.one;
            int capacity = 1;
            if (definition.Prefab.TryGetComponent(out RestaurantTable table)) capacity = Mathf.Max(1, table.Capacity);
            string path = ProfilesFolder + "/BBPLFSAssetProfile_" + definition.ItemId + ".asset";
            BBPLFSAssetLayoutProfile profile = AssetDatabase.LoadAssetAtPath<BBPLFSAssetLayoutProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BBPLFSAssetLayoutProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.EditorConfigure(definition, role, Family(role, capacity), footprint, capacity,
                new[] { "restaurant", definition.Category.ToString().ToLowerInvariant() });
            EditorUtility.SetDirty(profile);
            result.Add(profile);
        }
        result.Sort((a, b) => string.CompareOrdinal(a.ItemDefinition.ItemId, b.ItemDefinition.ItemId));
        return result;
    }

    private static BBPLFSLayoutRole Classify(RestaurantPlaceableItemDefinition definition)
    {
        RestaurantPlaceableObject prefab = definition.Prefab;
        if (prefab.TryGetComponent(out RestaurantTable _)) return BBPLFSLayoutRole.DiningTable;
        if (prefab.TryGetComponent(out RestaurantSeat _)) return BBPLFSLayoutRole.DiningSeat;
        if (prefab.TryGetComponent(out KitchenSystem _)) return BBPLFSLayoutRole.KitchenEquipment;
        string n = (definition.ItemId + " " + definition.DisplayName).ToLowerInvariant();
        if (definition.Category == RestaurantPlaceableItemCategory.KitchenEquipment) return BBPLFSLayoutRole.KitchenEquipment;
        if (n.Contains("bar") || n.Contains("counter") || n.Contains("barra") || n.Contains("mostrador")) return BBPLFSLayoutRole.Counter;
        if (n.Contains("storage") || n.Contains("shelf") || n.Contains("cabinet") || n.Contains("almacen") || n.Contains("estanter")) return BBPLFSLayoutRole.Storage;
        if (definition.Category == RestaurantPlaceableItemCategory.ServiceEquipment) return BBPLFSLayoutRole.Counter;
        return BBPLFSLayoutRole.Other;
    }

    private static string Family(BBPLFSLayoutRole role, int capacity) =>
        role == BBPLFSLayoutRole.DiningTable ? "dining_table_" + capacity : role.ToString().ToLowerInvariant();

    private static List<BBPLFSFurnishingSet> BuildSets()
    {
        var result = new List<BBPLFSFurnishingSet>();
        result.Add(GetOrCreateDiningSet(2));
        result.Add(GetOrCreateDiningSet(4));
        result.Add(GetOrCreateDiningSet(6));
        result.Add(GetOrCreateSet("bar_module", BBPLFSSpaceFunction.Bar, 0,
            new[] { BBPLFSPlacementPattern.WallBand, BBPLFSPlacementPattern.Perimeter },
            new[] { new BBPLFSFurnishingSlot(BBPLFSLayoutRole.Counter, 1), new BBPLFSFurnishingSlot(BBPLFSLayoutRole.Storage, 1, false) }));
        result.Add(GetOrCreateSet("kitchen_station", BBPLFSSpaceFunction.Kitchen, 0,
            new[] { BBPLFSPlacementPattern.WallBand, BBPLFSPlacementPattern.InteriorGrid },
            new[] { new BBPLFSFurnishingSlot(BBPLFSLayoutRole.KitchenEquipment, 1), new BBPLFSFurnishingSlot(BBPLFSLayoutRole.Storage, 1, false) }));
        result.Add(GetOrCreateSet("support_station", BBPLFSSpaceFunction.Support, 0,
            new[] { BBPLFSPlacementPattern.WallBand, BBPLFSPlacementPattern.Perimeter },
            new[] { new BBPLFSFurnishingSlot(BBPLFSLayoutRole.Storage, 1) }));
        return result;
    }

    private static BBPLFSFurnishingSet GetOrCreateDiningSet(int capacity)
    {
        return GetOrCreateSet("dining_set_" + capacity, BBPLFSSpaceFunction.Dining, capacity,
            new[] { BBPLFSPlacementPattern.InteriorGrid, BBPLFSPlacementPattern.Staggered,
                BBPLFSPlacementPattern.CenterAxis, BBPLFSPlacementPattern.Perimeter, BBPLFSPlacementPattern.WallBand },
            new[] { new BBPLFSFurnishingSlot(BBPLFSLayoutRole.DiningTable, 1),
                new BBPLFSFurnishingSlot(BBPLFSLayoutRole.DiningSeat, capacity) });
    }

    private static BBPLFSFurnishingSet GetOrCreateSet(string id, BBPLFSSpaceFunction function, int capacity,
        BBPLFSPlacementPattern[] patterns, BBPLFSFurnishingSlot[] slots)
    {
        string path = SetsFolder + "/BBPLFSFurnishingSet_" + id + ".asset";
        BBPLFSFurnishingSet set = AssetDatabase.LoadAssetAtPath<BBPLFSFurnishingSet>(path);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<BBPLFSFurnishingSet>();
            AssetDatabase.CreateAsset(set, path);
        }
        set.EditorConfigure(id, function, capacity, patterns, slots, new[] { "restaurant" });
        EditorUtility.SetDirty(set);
        return set;
    }

    private static RestaurantArea FindPreferredArea()
    {
        RestaurantArea[] areas = UnityEngine.Object.FindObjectsByType<RestaurantArea>(FindObjectsSortMode.None);
        Array.Sort(areas, (a, b) => string.CompareOrdinal(a != null ? a.AreaId : string.Empty, b != null ? b.AreaId : string.Empty));
        for (int i = 0; i < areas.Length; i++)
            if (areas[i] != null && areas[i].AreaId != null && areas[i].AreaId.ToLowerInvariant().Contains("dining")) return areas[i];
        return areas.Length > 0 ? areas[0] : null;
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        return root.TryGetComponent(out T component) ? component : Undo.AddComponent<T>(root);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
