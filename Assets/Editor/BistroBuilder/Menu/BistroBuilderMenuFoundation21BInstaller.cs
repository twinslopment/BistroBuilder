using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo, idempotente y con rollback de 2.1B.
///
/// Crea las categorías canónicas, migra las definiciones existentes a
/// CategoryId estable, instala la política comercial y prepara la edición
/// transaccional sin alterar comandas, inventario ni persistencia v2.
/// </summary>
public static class BistroBuilderMenuFoundation21BInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 2.1B Categories and Transactional Editing";

    private sealed class CategorySpec
    {
        public readonly BistroBuilderDishCategory LegacyCategory;
        public readonly string CategoryId;
        public readonly string DisplayName;
        public readonly int DisplayOrder;

        public CategorySpec(
            BistroBuilderDishCategory legacyCategory,
            string categoryId,
            string displayName,
            int displayOrder
        )
        {
            LegacyCategory = legacyCategory;
            CategoryId = categoryId;
            DisplayName = displayName;
            DisplayOrder = displayOrder;
        }
    }

    private sealed class AssetBackup
    {
        public string AssetPath;
        public bool Existed;
        public byte[] Bytes;
    }

    private static readonly CategorySpec[] CategorySpecs =
    {
        new CategorySpec(
            BistroBuilderDishCategory.Starter,
            BistroBuilderDishCategoryIdUtility.Starter,
            "Entrantes",
            0
        ),
        new CategorySpec(
            BistroBuilderDishCategory.MainCourse,
            BistroBuilderDishCategoryIdUtility.MainCourse,
            "Platos principales",
            10
        ),
        new CategorySpec(
            BistroBuilderDishCategory.SharedDish,
            BistroBuilderDishCategoryIdUtility.SharedDish,
            "Platos para compartir",
            20
        ),
        new CategorySpec(
            BistroBuilderDishCategory.SideDish,
            BistroBuilderDishCategoryIdUtility.SideDish,
            "Guarniciones",
            30
        ),
        new CategorySpec(
            BistroBuilderDishCategory.Dessert,
            BistroBuilderDishCategoryIdUtility.Dessert,
            "Postres",
            40
        ),
        new CategorySpec(
            BistroBuilderDishCategory.Beverage,
            BistroBuilderDishCategoryIdUtility.Beverage,
            "Bebidas",
            50
        ),
        new CategorySpec(
            BistroBuilderDishCategory.TastingItem,
            BistroBuilderDishCategoryIdUtility.TastingItem,
            "Pases de degustación",
            60
        )
    };

    [MenuItem(MenuPath, false, 140)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.1B.",
                "Aceptar"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant.unity antes de instalar.",
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

        BistroBuilderMenuState21AValidationResult prerequisite =
            BistroBuilderMenuState21AValidator.ValidateCurrentProject();

        if (prerequisite.ErrorCount > 0)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1A debe estar validado antes de instalar 2.1B.\n\n" +
                prerequisite.BuildReport(),
                "Aceptar"
            );
            return;
        }

        AssetDatabase.SaveAssets();

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);
        List<string> dishPaths = FindDishDefinitionPaths();
        List<string> backupPaths = BuildBackupPathList(dishPaths);
        List<AssetBackup> assetBackups = CreateBackups(backupPaths);
        const string categoriesFolder =
            "Assets/Data/BistroBuilder/Menu/Categories";
        const string definitionsFolder =
            "Assets/Data/BistroBuilder/Menu/Categories/Definitions";
        bool categoriesFolderExisted =
            AssetDatabase.IsValidFolder(categoriesFolder);
        bool definitionsFolderExisted =
            AssetDatabase.IsValidFolder(definitionsFolder);

        try
        {
            EnsureFolder(categoriesFolder);
            EnsureFolder(definitionsFolder);

            List<BistroBuilderDishCategoryDefinition> categoryDefinitions =
                new List<BistroBuilderDishCategoryDefinition>(
                    CategorySpecs.Length
                );

            for (int index = 0; index < CategorySpecs.Length; index++)
            {
                categoryDefinitions.Add(
                    EnsureCategoryDefinition(CategorySpecs[index])
                );
            }

            BistroBuilderDishCategoryCatalog categoryCatalog =
                EnsureCategoryCatalog();
            ConfigureCategoryCatalog(
                categoryCatalog,
                categoryDefinitions
            );

            BistroBuilderMenuCommercialPolicy policy =
                EnsureCommercialPolicy(out bool policyCreated);
            ConfigureCommercialPolicy(policy, policyCreated);

            MigrateDishDefinitions(
                dishPaths,
                categoryCatalog
            );

            GameObject gameSystems =
                BistroBuilderMenuFoundationValidator.FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa."
                );
            }

            BistroBuilderDishCatalogService dishCatalogService =
                RequireComponent<BistroBuilderDishCatalogService>(gameSystems);
            BistroBuilderRestaurantMenuService menuService =
                RequireComponent<BistroBuilderRestaurantMenuService>(
                    gameSystems
                );
            BistroBuilderRestaurantMenuCollectionService collectionService =
                RequireComponent<
                    BistroBuilderRestaurantMenuCollectionService
                >(gameSystems);

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar Bistro Builder 2.1B"
            );

            BistroBuilderDishCategoryCatalogService categoryService =
                GetOrAddComponent<
                    BistroBuilderDishCategoryCatalogService
                >(gameSystems);
            BistroBuilderMenuEditSessionService editSessionService =
                GetOrAddComponent<BistroBuilderMenuEditSessionService>(
                    gameSystems
                );

            ConfigureCategoryService(
                categoryService,
                categoryCatalog
            );
            ConfigureMenuService(menuService, policy);
            ConfigureEditSessionService(
                editSessionService,
                menuService,
                collectionService,
                dishCatalogService,
                categoryService,
                policy
            );

            if (!menuService.RebuildRuntimeIndexAndEnsureDefaults(
                    out string menuError
                ))
            {
                throw new InvalidOperationException(menuError);
            }

            if (!collectionService
                    .RebuildRuntimeIndexAndEnsurePrimaryRestaurant(
                        out string collectionError
                    ))
            {
                throw new InvalidOperationException(collectionError);
            }

            if (!editSessionService.ValidateConfiguration(
                    out string sessionError
                ))
            {
                throw new InvalidOperationException(sessionError);
            }

            EditorUtility.SetDirty(categoryCatalog);
            EditorUtility.SetDirty(policy);
            EditorUtility.SetDirty(categoryService);
            EditorUtility.SetDirty(menuService);
            EditorUtility.SetDirty(collectionService);
            EditorUtility.SetDirty(editSessionService);
            EditorSceneManager.MarkSceneDirty(scene);

            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.Refresh();

            BistroBuilderMenuFoundation21BValidationResult result =
                BistroBuilderMenuFoundation21BValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            string report = result.BuildReport();
            Debug.Log("BISTRO BUILDER - 2.1B INSTALADO\n" + report);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1B instalado correctamente.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount,
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            RestoreAssets(assetBackups);
            DeleteFolderCreatedByInstallation(
                definitionsFolder,
                definitionsFolderExisted
            );
            DeleteFolderCreatedByInstallation(
                categoriesFolder,
                categoriesFolderExisted
            );
            RestoreScene(
                scenePath,
                absoluteScenePath,
                sceneBackup
            );
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 2.1B se ha revertido.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static BistroBuilderDishCategoryDefinition
        EnsureCategoryDefinition(CategorySpec spec)
    {
        string path = GetCategoryDefinitionPath(spec.CategoryId);
        BistroBuilderDishCategoryDefinition definition =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderDishCategoryDefinition
            >(path);

        if (definition == null)
        {
            if (File.Exists(Path.GetFullPath(path)))
            {
                throw new InvalidOperationException(
                    "Ya existe un asset incompatible en " + path + "."
                );
            }

            definition = ScriptableObject.CreateInstance<
                BistroBuilderDishCategoryDefinition
            >();
            AssetDatabase.CreateAsset(definition, path);
        }

        SerializedObject serialized = new SerializedObject(definition);
        RequireProperty(serialized, "categoryId").stringValue =
            spec.CategoryId;
        RequireProperty(serialized, "displayName").stringValue =
            spec.DisplayName;
        RequireProperty(serialized, "hasLegacyMapping").boolValue = true;
        RequireProperty(serialized, "legacyCategory").enumValueIndex =
            (int)spec.LegacyCategory;
        RequireProperty(serialized, "displayOrder").intValue =
            spec.DisplayOrder;
        RequireProperty(serialized, "visible").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);

        if (!definition.TryValidate(out string error))
        {
            throw new InvalidOperationException(path + ": " + error);
        }

        return definition;
    }

    private static BistroBuilderDishCategoryCatalog EnsureCategoryCatalog()
    {
        string path =
            BistroBuilderMenuFoundation21BValidator.CategoryCatalogAssetPath;
        BistroBuilderDishCategoryCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderDishCategoryCatalog
            >(path);

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

        catalog = ScriptableObject.CreateInstance<
            BistroBuilderDishCategoryCatalog
        >();
        AssetDatabase.CreateAsset(catalog, path);
        return catalog;
    }

    private static void ConfigureCategoryCatalog(
        BistroBuilderDishCategoryCatalog catalog,
        List<BistroBuilderDishCategoryDefinition> definitions
    )
    {
        List<BistroBuilderDishCategoryDefinition> existing =
            new List<BistroBuilderDishCategoryDefinition>();
        catalog.CopyDefinitionsTo(existing);
        HashSet<string> knownIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < definitions.Count; index++)
        {
            knownIds.Add(definitions[index].CategoryId);
        }

        for (int index = 0; index < existing.Count; index++)
        {
            BistroBuilderDishCategoryDefinition definition = existing[index];

            if (definition == null || knownIds.Contains(definition.CategoryId))
            {
                continue;
            }

            if (!definition.TryValidate(out string existingError))
            {
                throw new InvalidOperationException(existingError);
            }

            definitions.Add(definition);
            knownIds.Add(definition.CategoryId);
        }

        definitions.Sort(
            (first, second) =>
            {
                int order = first.DisplayOrder.CompareTo(second.DisplayOrder);
                return order != 0
                    ? order
                    : string.Compare(
                        first.CategoryId,
                        second.CategoryId,
                        StringComparison.Ordinal
                    );
            }
        );

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty list =
            RequireProperty(serialized, "definitions");
        list.arraySize = definitions.Count;

        for (int index = 0; index < definitions.Count; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);

        if (!catalog.TryRebuildIndex(out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static BistroBuilderMenuCommercialPolicy
        EnsureCommercialPolicy(out bool created)
    {
        created = false;
        string path =
            BistroBuilderMenuFoundation21BValidator.CommercialPolicyAssetPath;
        BistroBuilderMenuCommercialPolicy policy =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderMenuCommercialPolicy
            >(path);

        if (policy != null)
        {
            return policy;
        }

        if (File.Exists(Path.GetFullPath(path)))
        {
            throw new InvalidOperationException(
                "Ya existe un asset incompatible en " + path + "."
            );
        }

        policy = ScriptableObject.CreateInstance<
            BistroBuilderMenuCommercialPolicy
        >();
        AssetDatabase.CreateAsset(policy, path);
        created = true;
        return policy;
    }

    private static void ConfigureCommercialPolicy(
        BistroBuilderMenuCommercialPolicy policy,
        bool created
    )
    {
        bool requiresDefaults = created || !policy.TryValidate(out _);
        SerializedObject serialized = new SerializedObject(policy);

        if (requiresDefaults)
        {
            RequireProperty(serialized, "minimumPriceCents").intValue =
                BistroBuilderMenuCommercialPolicy.DefaultMinimumPriceCents;
            RequireProperty(serialized, "maximumPriceCents").intValue =
                BistroBuilderDishDefinition.MaximumPriceCents;
            RequireProperty(serialized, "maximumMenuItems").intValue =
                BistroBuilderMenuCommercialPolicy.DefaultMaximumMenuItems;
            RequireProperty(serialized, "maximumSignatureDishes").intValue =
                BistroBuilderMenuCommercialPolicy
                    .DefaultMaximumSignatureDishes;
            RequireProperty(
                serialized,
                "signatureSelectionWeightBasisPoints"
            ).intValue = BistroBuilderMenuCommercialPolicy
                .DefaultSignatureSelectionWeightBasisPoints;
        }

        // Estas tres reglas son invariantes del diseño de Bistro Builder,
        // no preferencias comerciales editables por restaurante.
        RequireProperty(
            serialized,
            "requireSignatureDishEnabled"
        ).boolValue = true;
        RequireProperty(
            serialized,
            "requireSignatureDishUnlocked"
        ).boolValue = true;
        RequireProperty(
            serialized,
            "requireSignatureDishServiceAvailability"
        ).boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(policy);

        if (!policy.TryValidate(out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static void MigrateDishDefinitions(
        List<string> paths,
        BistroBuilderDishCategoryCatalog categoryCatalog
    )
    {
        for (int index = 0; index < paths.Count; index++)
        {
            string path = paths[index];
            BistroBuilderDishDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderDishDefinition
                >(path);

            if (definition == null)
            {
                throw new InvalidOperationException(
                    "No se pudo cargar la definición en " + path + "."
                );
            }

            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty legacyCategory =
                RequireProperty(serialized, "category");
            SerializedProperty categoryId =
                RequireProperty(serialized, "categoryId");
            SerializedProperty definitionVersion =
                RequireProperty(serialized, "definitionVersion");

            string normalized =
                BistroBuilderMenuIdUtility.NormalizeStableId(
                    categoryId.stringValue
                );

            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized =
                    BistroBuilderDishCategoryIdUtility.FromLegacyCategory(
                        (BistroBuilderDishCategory)
                            legacyCategory.enumValueIndex
                    );
            }

            if (!categoryCatalog.TryGetDefinition(normalized, out _))
            {
                throw new InvalidOperationException(
                    path + " referencia el CategoryId no registrado " +
                    normalized + "."
                );
            }

            if (definitionVersion.intValue >
                BistroBuilderDishDefinition.CurrentDefinitionVersion)
            {
                throw new InvalidOperationException(
                    path + " usa una versión futura de definición (" +
                    definitionVersion.intValue + ") que 2.1B no puede " +
                    "reinterpretar de forma segura."
                );
            }

            categoryId.stringValue = normalized;
            definitionVersion.intValue =
                BistroBuilderDishDefinition.CurrentDefinitionVersion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            if (!definition.TryValidate(out string error))
            {
                throw new InvalidOperationException(path + ": " + error);
            }
        }
    }

    private static void ConfigureCategoryService(
        BistroBuilderDishCategoryCatalogService service,
        BistroBuilderDishCategoryCatalog catalog
    )
    {
        SerializedObject serialized = new SerializedObject(service);
        RequireProperty(serialized, "catalog").objectReferenceValue = catalog;
        RequireProperty(serialized, "logInitialization").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureMenuService(
        BistroBuilderRestaurantMenuService service,
        BistroBuilderMenuCommercialPolicy policy
    )
    {
        SerializedObject serialized = new SerializedObject(service);
        RequireProperty(serialized, "commercialPolicy").objectReferenceValue =
            policy;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureEditSessionService(
        BistroBuilderMenuEditSessionService service,
        BistroBuilderRestaurantMenuService menuService,
        BistroBuilderRestaurantMenuCollectionService collectionService,
        BistroBuilderDishCatalogService catalogService,
        BistroBuilderDishCategoryCatalogService categoryService,
        BistroBuilderMenuCommercialPolicy policy
    )
    {
        SerializedObject serialized = new SerializedObject(service);
        RequireProperty(serialized, "menuService").objectReferenceValue =
            menuService;
        RequireProperty(serialized, "collectionService").objectReferenceValue =
            collectionService;
        RequireProperty(serialized, "catalogService").objectReferenceValue =
            catalogService;
        RequireProperty(
            serialized,
            "categoryCatalogService"
        ).objectReferenceValue = categoryService;
        RequireProperty(serialized, "commercialPolicy").objectReferenceValue =
            policy;
        RequireProperty(serialized, "logChanges").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<string> FindDishDefinitionPaths()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderDishDefinition"
        );
        List<string> paths = new List<string>(guids.Length);

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);

            if (!string.IsNullOrWhiteSpace(path) &&
                path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static List<string> BuildBackupPathList(
        List<string> dishPaths
    )
    {
        List<string> paths = new List<string>(dishPaths);
        paths.Add(
            BistroBuilderMenuFoundation21BValidator.CategoryCatalogAssetPath
        );
        paths.Add(
            BistroBuilderMenuFoundation21BValidator.CommercialPolicyAssetPath
        );

        for (int index = 0; index < CategorySpecs.Length; index++)
        {
            paths.Add(
                GetCategoryDefinitionPath(CategorySpecs[index].CategoryId)
            );
        }

        return paths;
    }

    private static List<AssetBackup> CreateBackups(
        List<string> assetPaths
    )
    {
        List<AssetBackup> backups =
            new List<AssetBackup>(assetPaths.Count);

        for (int index = 0; index < assetPaths.Count; index++)
        {
            string path = assetPaths[index];
            string absolute = Path.GetFullPath(path);
            bool existed = File.Exists(absolute);
            backups.Add(
                new AssetBackup
                {
                    AssetPath = path,
                    Existed = existed,
                    Bytes = existed ? File.ReadAllBytes(absolute) : null
                }
            );
        }

        return backups;
    }

    private static void RestoreAssets(List<AssetBackup> backups)
    {
        try
        {
            for (int index = backups.Count - 1; index >= 0; index--)
            {
                AssetBackup backup = backups[index];

                if (!backup.Existed)
                {
                    AssetDatabase.DeleteAsset(backup.AssetPath);
                    continue;
                }

                string absolute = Path.GetFullPath(backup.AssetPath);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(absolute)
                );
                File.WriteAllBytes(absolute, backup.Bytes);
            }

            AssetDatabase.Refresh();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void DeleteFolderCreatedByInstallation(
        string folderPath,
        bool existedBeforeInstallation
    )
    {
        if (existedBeforeInstallation ||
            !AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string absoluteFolderPath = Path.GetFullPath(folderPath);

        if (Directory.Exists(absoluteFolderPath) &&
            Directory.GetFileSystemEntries(absoluteFolderPath).Length == 0)
        {
            AssetDatabase.DeleteAsset(folderPath);
        }
    }

    private static void RestoreScene(
        string scenePath,
        string absoluteScenePath,
        byte[] backup
    )
    {
        try
        {
            File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.ImportAsset(
                scenePath,
                ImportAssetOptions.ForceUpdate
            );
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static string GetCategoryDefinitionPath(string categoryId)
    {
        return "Assets/Data/BistroBuilder/Menu/Categories/Definitions/" +
               categoryId + ".asset";
    }

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace('\\', '/').TrimEnd('/');

        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        int separator = normalized.LastIndexOf('/');

        if (separator <= 0)
        {
            throw new InvalidOperationException(
                "Ruta de carpeta inválida: " + normalized + "."
            );
        }

        string parent = normalized.Substring(0, separator);
        string name = normalized.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
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
}
