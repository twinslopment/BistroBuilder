using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente y con rollback de BistroBuilder 367A.
///
/// Crea únicamente las definiciones iniciales que faltan, recompone el
/// catálogo con todas las definiciones del proyecto y añade los componentes
/// runtime a GameSystems. No reemplaza los sistemas actuales de comandas.
/// </summary>
public static class BistroBuilderMenuFoundationInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 367A Dish & Menu Foundation";

    private const string DefinitionsFolder =
        "Assets/Data/BistroBuilder/Menu/Definitions";

    private const string FabadaPath =
        DefinitionsFolder + "/dish_fabada_asturiana.asset";

    private const string MerluzaPath =
        DefinitionsFolder + "/dish_merluza_plancha.asset";

    private const string TartaPath =
        DefinitionsFolder + "/dish_tarta_queso.asset";

    [MenuItem(MenuPath, false, 100)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de instalar BistroBuilder 367A.",
                "Aceptar"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant.unity antes de ejecutar el instalador.",
                "Aceptar"
            );
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el instalador.",
                "Aceptar"
            );
            return;
        }

        string absoluteScenePath = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);
        List<AssetBackupRecord> assetBackups =
            CreateAssetBackups(
                FabadaPath,
                MerluzaPath,
                TartaPath,
                BistroBuilderMenuFoundationValidator.CatalogAssetPath
            );

        try
        {
            EnsureFolder(DefinitionsFolder);

            EnsureDishDefinition(
                FabadaPath,
                "dish_fabada_asturiana",
                "Fabada asturiana",
                "Plato principal de cocción lenta basado en fabes y compango.",
                BistroBuilderDishCategory.MainCourse,
                BistroBuilderDishCourse.Main,
                BistroBuilderMealServiceAvailability.Lunch |
                    BistroBuilderMealServiceAvailability.Dinner,
                BistroBuilderKitchenStationType.HotKitchen,
                840,
                5,
                "recipe_fabada_asturiana",
                1850
            );

            EnsureDishDefinition(
                MerluzaPath,
                "dish_merluza_plancha",
                "Merluza a la plancha",
                "Merluza preparada a la plancha y servida como principal.",
                BistroBuilderDishCategory.MainCourse,
                BistroBuilderDishCourse.Main,
                BistroBuilderMealServiceAvailability.Lunch |
                    BistroBuilderMealServiceAvailability.Dinner,
                BistroBuilderKitchenStationType.Grill,
                720,
                4,
                "recipe_merluza_plancha",
                2100
            );

            EnsureDishDefinition(
                TartaPath,
                "dish_tarta_queso",
                "Tarta de queso",
                "Postre de tarta de queso preparado para servicio en porciones.",
                BistroBuilderDishCategory.Dessert,
                BistroBuilderDishCourse.Dessert,
                BistroBuilderMealServiceAvailability.Lunch |
                    BistroBuilderMealServiceAvailability.Dinner,
                BistroBuilderKitchenStationType.Pastry,
                180,
                2,
                "recipe_tarta_queso",
                750
            );

            BistroBuilderDishCatalog catalog = EnsureCatalog();
            ConfigureCatalog(catalog);

            GameObject gameSystems =
                BistroBuilderMenuFoundationValidator.FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa."
                );
            }

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar BistroBuilder 367A"
            );

            BistroBuilderDishCatalogService catalogService =
                GetOrAddComponent<BistroBuilderDishCatalogService>(
                    gameSystems
                );
            BistroBuilderRestaurantMenuService menuService =
                GetOrAddComponent<BistroBuilderRestaurantMenuService>(
                    gameSystems
                );
            BistroBuilderMenuSaveSectionProvider menuProvider =
                GetOrAddComponent<BistroBuilderMenuSaveSectionProvider>(
                    gameSystems
                );
            BistroBuilderSaveGameService saveGameService =
                RequireComponent<BistroBuilderSaveGameService>(gameSystems);

            ConfigureCatalogService(catalogService, catalog);
            ConfigureMenuService(menuService, catalogService);
            ConfigureSaveProvider(
                menuProvider,
                saveGameService,
                menuService,
                catalogService
            );

            saveGameService.RefreshExtensions();

            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(catalogService);
            EditorUtility.SetDirty(menuService);
            EditorUtility.SetDirty(menuProvider);
            EditorUtility.SetDirty(saveGameService);
            EditorSceneManager.MarkSceneDirty(scene);

            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.Refresh();

            BistroBuilderMenuValidationResult result =
                BistroBuilderMenuFoundationValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(
                "BISTRO BUILDER - 367A INSTALADO\n" +
                result.BuildReport()
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "BistroBuilder 367A instalado correctamente.\n\n" +
                "Errores: " + result.ErrorCount +
                "\nAdvertencias: " + result.WarningCount +
                "\n\nEjecuta ahora Validate 367A Dish & Menu Foundation.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            try
            {
                RestoreAssets(assetBackups);
                File.WriteAllBytes(absoluteScenePath, sceneBackup);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(
                    scene.path,
                    OpenSceneMode.Single
                );
            }
            catch (Exception rollbackException)
            {
                Debug.LogException(rollbackException);
            }

            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación de 367A ha fallado y se ha restaurado el estado anterior.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static void EnsureDishDefinition(
        string path,
        string dishId,
        string displayName,
        string description,
        BistroBuilderDishCategory category,
        BistroBuilderDishCourse course,
        BistroBuilderMealServiceAvailability availability,
        BistroBuilderKitchenStationType station,
        int preparationSeconds,
        int complexity,
        string recipeId,
        int basePriceCents
    )
    {
        BistroBuilderDishDefinition definition =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderDishDefinition
            >(path);
        bool created = false;

        if (definition == null)
        {
            if (File.Exists(Path.GetFullPath(path)))
            {
                throw new InvalidOperationException(
                    "Ya existe un asset incompatible en " + path + "."
                );
            }

            definition =
                ScriptableObject.CreateInstance<
                    BistroBuilderDishDefinition
                >();
            AssetDatabase.CreateAsset(definition, path);
            created = true;
        }

        SerializedObject serialized = new SerializedObject(definition);
        SerializedProperty idProperty =
            RequireProperty(serialized, "dishId");
        string currentId = idProperty.stringValue;

        if (!string.IsNullOrWhiteSpace(currentId) &&
            !string.Equals(currentId, dishId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                path + " contiene el DishId " + currentId +
                " y no puede repararse como " + dishId + "."
            );
        }

        if (created || string.IsNullOrWhiteSpace(currentId))
        {
            idProperty.stringValue = dishId;
        }

        SetStringIfCreatedOrEmpty(
            serialized,
            "displayName",
            displayName,
            created
        );
        SetStringIfCreatedOrEmpty(
            serialized,
            "description",
            description,
            created
        );
        SetStringIfCreatedOrEmpty(
            serialized,
            "recipeId",
            recipeId,
            created
        );

        if (created)
        {
            RequireProperty(serialized, "category").enumValueIndex =
                (int)category;
            RequireProperty(serialized, "course").enumValueIndex =
                (int)course;
            RequireProperty(serialized, "defaultAvailability").intValue =
                (int)availability;
            RequireProperty(serialized, "requiredStation").enumValueIndex =
                (int)station;
            RequireProperty(serialized, "basePreparationSeconds").intValue =
                preparationSeconds;
            RequireProperty(serialized, "complexity").intValue = complexity;
            RequireProperty(serialized, "basePriceCents").intValue =
                basePriceCents;
            RequireProperty(serialized, "shareable").boolValue = false;
            RequireProperty(serialized, "minimumConsumers").intValue = 1;
            RequireProperty(serialized, "maximumConsumers").intValue = 1;
        }
        else
        {
            SerializedProperty availabilityProperty =
                RequireProperty(serialized, "defaultAvailability");

            if (availabilityProperty.intValue == 0)
            {
                availabilityProperty.intValue = (int)availability;
            }

            SerializedProperty preparationProperty =
                RequireProperty(serialized, "basePreparationSeconds");

            if (preparationProperty.intValue < 1)
            {
                preparationProperty.intValue = preparationSeconds;
            }

            SerializedProperty complexityProperty =
                RequireProperty(serialized, "complexity");

            if (complexityProperty.intValue < 1)
            {
                complexityProperty.intValue = complexity;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);

        if (!definition.TryValidate(out string error))
        {
            throw new InvalidOperationException(path + ": " + error);
        }
    }

    private static BistroBuilderDishCatalog EnsureCatalog()
    {
        string path =
            BistroBuilderMenuFoundationValidator.CatalogAssetPath;
        BistroBuilderDishCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderDishCatalog>(path);

        if (catalog != null)
        {
            return catalog;
        }

        if (File.Exists(Path.GetFullPath(path)))
        {
            throw new InvalidOperationException(
                "Ya existe un asset incompatible en " + path + "."
            );
        }

        catalog = ScriptableObject.CreateInstance<BistroBuilderDishCatalog>();
        AssetDatabase.CreateAsset(catalog, path);
        return catalog;
    }

    private static void ConfigureCatalog(BistroBuilderDishCatalog catalog)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderDishDefinition"
        );
        List<BistroBuilderDishDefinition> definitions =
            new List<BistroBuilderDishDefinition>(guids.Length);

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderDishDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderDishDefinition
                >(path);

            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort(
            (first, second) => string.Compare(
                first != null ? first.DishId : string.Empty,
                second != null ? second.DishId : string.Empty,
                StringComparison.Ordinal
            )
        );

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty list = RequireProperty(serialized, "definitions");
        list.arraySize = definitions.Count;

        for (int index = 0; index < definitions.Count; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (!catalog.TryRebuildIndex(out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static void ConfigureCatalogService(
        BistroBuilderDishCatalogService service,
        BistroBuilderDishCatalog catalog
    )
    {
        SerializedObject serialized = new SerializedObject(service);
        RequireProperty(serialized, "catalog").objectReferenceValue = catalog;
        RequireProperty(serialized, "logInitialization").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureMenuService(
        BistroBuilderRestaurantMenuService service,
        BistroBuilderDishCatalogService catalogService
    )
    {
        SerializedObject serialized = new SerializedObject(service);
        RequireProperty(serialized, "catalogService").objectReferenceValue =
            catalogService;
        RequireProperty(
            serialized,
            "initializeCatalogDishesWhenEmpty"
        ).boolValue = true;
        RequireProperty(serialized, "defaultDishEnabled").boolValue = true;
        RequireProperty(serialized, "defaultDishUnlocked").boolValue = true;
        RequireProperty(serialized, "logChanges").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSaveProvider(
        BistroBuilderMenuSaveSectionProvider provider,
        BistroBuilderSaveGameService saveGameService,
        BistroBuilderRestaurantMenuService menuService,
        BistroBuilderDishCatalogService catalogService
    )
    {
        SerializedObject serialized = new SerializedObject(provider);
        RequireProperty(serialized, "saveGameService").objectReferenceValue =
            saveGameService;
        RequireProperty(serialized, "menuService").objectReferenceValue =
            menuService;
        RequireProperty(serialized, "catalogService").objectReferenceValue =
            catalogService;
        RequireProperty(serialized, "captureItemsPerFrame").intValue = 64;
        RequireProperty(serialized, "logLoadSummary").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();

        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static T RequireComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
        {
            throw new InvalidOperationException(
                "GameSystems necesita " + typeof(T).Name + "."
            );
        }

        return component;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName
    )
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                serialized.targetObject.GetType().Name +
                " no contiene la propiedad " + propertyName + "."
            );
        }

        return property;
    }

    private static void SetStringIfCreatedOrEmpty(
        SerializedObject serialized,
        string propertyName,
        string value,
        bool created
    )
    {
        SerializedProperty property =
            RequireProperty(serialized, propertyName);

        if (created || string.IsNullOrWhiteSpace(property.stringValue))
        {
            property.stringValue = value ?? string.Empty;
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        string normalized = folderPath.Replace('\\', '/').TrimEnd('/');

        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        string parent = Path.GetDirectoryName(normalized)
            ?.Replace('\\', '/');
        string name = Path.GetFileName(normalized);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException(
                "Ruta de carpeta inválida: " + folderPath + "."
            );
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static List<AssetBackupRecord> CreateAssetBackups(
        params string[] assetPaths
    )
    {
        List<AssetBackupRecord> records =
            new List<AssetBackupRecord>(assetPaths.Length);

        for (int index = 0; index < assetPaths.Length; index++)
        {
            string assetPath = assetPaths[index];
            string absolutePath = Path.GetFullPath(assetPath);
            string metaPath = absolutePath + ".meta";

            records.Add(
                new AssetBackupRecord
                {
                    AssetPath = assetPath,
                    Existed = File.Exists(absolutePath),
                    AssetBytes = File.Exists(absolutePath)
                        ? File.ReadAllBytes(absolutePath)
                        : null,
                    MetaExisted = File.Exists(metaPath),
                    MetaBytes = File.Exists(metaPath)
                        ? File.ReadAllBytes(metaPath)
                        : null
                }
            );
        }

        return records;
    }

    private static void RestoreAssets(List<AssetBackupRecord> records)
    {
        for (int index = 0; index < records.Count; index++)
        {
            AssetBackupRecord record = records[index];
            string absolutePath = Path.GetFullPath(record.AssetPath);
            string metaPath = absolutePath + ".meta";

            if (!record.Existed)
            {
                AssetDatabase.DeleteAsset(record.AssetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, record.AssetBytes);

            if (record.MetaExisted)
            {
                File.WriteAllBytes(metaPath, record.MetaBytes);
            }
            else if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
    }

    private sealed class AssetBackupRecord
    {
        public string AssetPath;
        public bool Existed;
        public byte[] AssetBytes;
        public bool MetaExisted;
        public byte[] MetaBytes;
    }
}
