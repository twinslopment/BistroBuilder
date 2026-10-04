using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Instalador acumulativo e idempotente de 367H.
/// Conserva las autoridades de servicio y retira la barra provisional obsoleta;
/// cinco artículos rápidos y todas las conexiones runtime necesarias.
/// </summary>
public static class BistroBuilderBarServiceInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Service/" +
        "Install or Repair 367H Bar Service";

    private const string GeneratedRoot =
        "Assets/BistroBuilder/Generated/367H";
    private const string MenuRoot = GeneratedRoot + "/Menu";

    private sealed class AssetFileBackup
    {
        public string AssetPath { get; }
        public byte[] Contents { get; }

        public AssetFileBackup(string assetPath, byte[] contents)
        {
            AssetPath = assetPath ?? string.Empty;
            Contents = contents ?? Array.Empty<byte>();
        }
    }

    private readonly struct DishSeed
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly BistroBuilderDishCategory Category;
        public readonly BistroBuilderDishCourse Course;
        public readonly BistroBuilderKitchenStationType Station;
        public readonly int Seconds;
        public readonly int PriceCents;

        public DishSeed(
            string id,
            string name,
            string description,
            BistroBuilderDishCategory category,
            BistroBuilderDishCourse course,
            BistroBuilderKitchenStationType station,
            int seconds,
            int priceCents
        )
        {
            Id = id;
            Name = name;
            Description = description;
            Category = category;
            Course = course;
            Station = station;
            Seconds = seconds;
            PriceCents = priceCents;
        }
    }

    private static readonly DishSeed[] Seeds =
    {
        new DishSeed(
            "dish_agua_mineral",
            "Agua mineral",
            "Botella de agua mineral servida en barra o mesa.",
            BistroBuilderDishCategory.Beverage,
            BistroBuilderDishCourse.Beverage,
            BistroBuilderKitchenStationType.Bar,
            5,
            220
        ),
        new DishSeed(
            "dish_refresco",
            "Refresco",
            "Refresco frío preparado al momento.",
            BistroBuilderDishCategory.Beverage,
            BistroBuilderDishCourse.Beverage,
            BistroBuilderKitchenStationType.Bar,
            7,
            280
        ),
        new DishSeed(
            "dish_copa_vino",
            "Copa de vino",
            "Copa de vino de la casa.",
            BistroBuilderDishCategory.Beverage,
            BistroBuilderDishCourse.Beverage,
            BistroBuilderKitchenStationType.Bar,
            8,
            350
        ),
        new DishSeed(
            "dish_aceitunas_alinadas",
            "Aceitunas aliñadas",
            "Aperitivo frío de preparación inmediata.",
            BistroBuilderDishCategory.Starter,
            BistroBuilderDishCourse.Welcome,
            BistroBuilderKitchenStationType.ColdPreparation,
            10,
            300
        ),
        new DishSeed(
            "dish_pincho_tortilla",
            "Pincho de tortilla",
            "Ración rápida apta para barra y espera de mesa.",
            BistroBuilderDishCategory.Starter,
            BistroBuilderDishCourse.Starter,
            BistroBuilderKitchenStationType.Bar,
            25,
            450
        )
    };

    [MenuItem(MenuPath, false, 265)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 367H.",
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
                "Abre y guarda Prototype_Restaurant.unity.",
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
        byte[] backup = File.ReadAllBytes(absoluteScenePath);
        List<string> createdAssets = new List<string>();
        List<AssetFileBackup> modifiedAssetBackups =
            new List<AssetFileBackup>();

        try
        {
            GameObject gameSystems =
                BistroBuilderCanonicalOrderIntegrationValidator
                    .FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems."
                );
            }

            ValidatePrerequisites(scene, gameSystems);
            EnsureFolders();

            BistroBuilderDishCatalogService catalogService =
                RequireSingle<BistroBuilderDishCatalogService>(scene);
            BistroBuilderRestaurantMenuService menuService =
                RequireSingle<BistroBuilderRestaurantMenuService>(scene);

            InstallMenuDefinitions(
                catalogService,
                menuService,
                createdAssets,
                modifiedAssetBackups
            );

            RestaurantTable[] existingTables =
                FindSceneObjects<RestaurantTable>(scene);

            if (existingTables.Length < 2)
            {
                throw new InvalidOperationException(
                    "367H necesita al menos las dos mesas base del prototipo."
                );
            }

            RetireLegacyBar(scene);

            BistroBuilderBarServiceRegistry registry =
                GetOrAdd<BistroBuilderBarServiceRegistry>(gameSystems);
            BistroBuilderBarServiceSystem barSystem =
                GetOrAdd<BistroBuilderBarServiceSystem>(gameSystems);

            WireSystems(
                scene,
                registry,
                barSystem,
                catalogService,
                menuService
            );

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 367H."
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderBarServiceValidationResult result =
                BistroBuilderBarServiceValidator.ValidateCurrentScene();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(result.BuildReport());

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Servicio de barra 367H instalado.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount +
                "\n\nEjecuta ahora el autotest 367H.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            try
            {
                for (int index = createdAssets.Count - 1;
                     index >= 0;
                     index--)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(
                            createdAssets[index]
                        ) != null)
                    {
                        AssetDatabase.DeleteAsset(createdAssets[index]);
                    }
                }

                for (int index = modifiedAssetBackups.Count - 1;
                     index >= 0;
                     index--)
                {
                    AssetFileBackup assetBackup =
                        modifiedAssetBackups[index];
                    string absoluteAssetPath = Path.GetFullPath(
                        assetBackup.AssetPath
                    );
                    File.WriteAllBytes(
                        absoluteAssetPath,
                        assetBackup.Contents
                    );
                }

                File.WriteAllBytes(absoluteScenePath, backup);
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
                "La instalación 367H ha fallado y la escena anterior " +
                "se ha restaurado.\n\n" + exception.Message,
                "Aceptar"
            );
        }
    }

    private static void ValidatePrerequisites(Scene scene, GameObject systems)
    {
        BistroBuilderCourseAndSharingService courses =
            systems.GetComponent<BistroBuilderCourseAndSharingService>();
        WaiterTaskCoordinator coordinator =
            RequireSingle<WaiterTaskCoordinator>(scene);

        if (courses == null ||
            !string.Equals(
                BistroBuilderCourseAndSharingService.RuntimeRevision,
                "367F",
                StringComparison.Ordinal
            ))
        {
            throw new InvalidOperationException(
                "367F2 debe estar instalado antes de 367H."
            );
        }

        if (!string.Equals(
                WaiterTaskCoordinator.RuntimeRevision,
                "367H",
                StringComparison.Ordinal
            ) ||
            coordinator.DeliveryRunConsolidationSeconds <= 0f)
        {
            throw new InvalidOperationException(
                "El código acumulativo de 367G1/367H no está activo."
            );
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "BistroBuilder");
        EnsureFolder("Assets/BistroBuilder", "Generated");
        EnsureFolder("Assets/BistroBuilder/Generated", "367H");
        EnsureFolder(GeneratedRoot, "Menu");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void InstallMenuDefinitions(
        BistroBuilderDishCatalogService catalogService,
        BistroBuilderRestaurantMenuService menuService,
        List<string> createdAssets,
        List<AssetFileBackup> modifiedAssetBackups
    )
    {
        BistroBuilderDishCatalog catalog = catalogService.Catalog;

        if (catalog == null)
        {
            throw new InvalidOperationException(
                "El servicio de catálogo no tiene asset asignado."
            );
        }

        BackupAssetFileOnce(
            AssetDatabase.GetAssetPath(catalog),
            modifiedAssetBackups
        );

        SerializedObject catalogSerialized = new SerializedObject(catalog);
        SerializedProperty definitions = RequireProperty(
            catalogSerialized,
            "definitions"
        );

        for (int index = 0; index < Seeds.Length; index++)
        {
            DishSeed seed = Seeds[index];
            string assetPath = MenuRoot + "/" + seed.Id + ".asset";
            BistroBuilderDishDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderDishDefinition
                >(assetPath);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<
                    BistroBuilderDishDefinition
                >();
                AssetDatabase.CreateAsset(definition, assetPath);
                createdAssets.Add(assetPath);
            }
            else
            {
                BackupAssetFileOnce(assetPath, modifiedAssetBackups);
            }

            ConfigureDefinition(definition, seed);

            if (!ContainsReference(definitions, definition))
            {
                definitions.InsertArrayElementAtIndex(
                    definitions.arraySize
                );
                definitions.GetArrayElementAtIndex(
                    definitions.arraySize - 1
                ).objectReferenceValue = definition;
            }
        }

        catalogSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);

        if (!catalog.TryRebuildIndex(out string catalogError))
        {
            throw new InvalidOperationException(catalogError);
        }

        catalogService.RebuildIndex(out _);

        for (int index = 0; index < Seeds.Length; index++)
        {
            if (!menuService.TryGetItemSnapshot(Seeds[index].Id, out _))
            {
                BistroBuilderMenuMutationResult result =
                    menuService.TryAddDish(Seeds[index].Id);

                if (!result.Succeeded &&
                    result.FailureReason !=
                        BistroBuilderMenuMutationFailureReason.NoChange)
                {
                    throw new InvalidOperationException(result.Message);
                }
            }

            menuService.TrySetUnlocked(Seeds[index].Id, true);
            menuService.TrySetEnabled(Seeds[index].Id, true);
            menuService.TrySetPriceCents(
                Seeds[index].Id,
                Seeds[index].PriceCents
            );
        }

        EditorUtility.SetDirty(menuService);
    }

    private static void ConfigureDefinition(
        BistroBuilderDishDefinition definition,
        DishSeed seed
    )
    {
        Undo.RecordObject(definition, "Configurar artículo 367H");
        SerializedObject serialized = new SerializedObject(definition);
        SerializedProperty definitionVersion =
            RequireProperty(serialized, "definitionVersion");
        definitionVersion.intValue = Mathf.Max(
            BistroBuilderDishDefinition.CurrentDefinitionVersion,
            definitionVersion.intValue
        );
        RequireProperty(serialized, "dishId").stringValue = seed.Id;
        RequireProperty(serialized, "displayName").stringValue = seed.Name;
        RequireProperty(serialized, "description").stringValue =
            seed.Description;
        RequireProperty(serialized, "category").enumValueIndex =
            (int)seed.Category;
        RequireProperty(serialized, "categoryId").stringValue =
            BistroBuilderDishCategoryIdUtility.FromLegacyCategory(
                seed.Category
            );
        RequireProperty(serialized, "course").enumValueIndex =
            (int)seed.Course;
        RequireProperty(serialized, "defaultAvailability").intValue =
            (int)BistroBuilderMealServiceAvailability.All;
        RequireProperty(serialized, "allowedServiceModes").intValue =
            (int)BistroBuilderDishServiceModeAvailability.All;
        RequireProperty(serialized, "requiredStation").enumValueIndex =
            (int)seed.Station;
        RequireProperty(serialized, "basePreparationSeconds").intValue =
            seed.Seconds;
        RequireProperty(serialized, "complexity").intValue = 1;
        RequireProperty(serialized, "recipeId").stringValue = string.Empty;
        RequireProperty(serialized, "basePriceCents").intValue =
            seed.PriceCents;
        RequireProperty(serialized, "shareable").boolValue = false;
        RequireProperty(serialized, "minimumConsumers").intValue = 1;
        RequireProperty(serialized, "maximumConsumers").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);

        if (!definition.TryValidate(out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public static int RetireLegacyBar(Scene scene)
    {
        int removed = 0;
        foreach (var fixture in FindSceneObjects<BistroBuilder367HInstalledFixture>(scene))
        {
            if (fixture == null || !fixture.IsRetired) continue;
            Undo.DestroyObjectImmediate(fixture.gameObject);
            removed++;
        }
        return removed;
    }

    private static void EnsureAdditionalTables(
        Scene scene,
        RestaurantTable[] initialTables,
        Vector3 barPosition
    )
    {
        RestaurantTable template = initialTables[0];
        HashSet<int> usedIds = new HashSet<int>();
        List<RestaurantTable> allTables = new List<RestaurantTable>(
            FindSceneObjects<RestaurantTable>(scene)
        );

        for (int index = 0; index < allTables.Count; index++)
        {
            usedIds.Add(allTables[index].TableId);
        }

        for (int fixtureIndex = 0; fixtureIndex < 2; fixtureIndex++)
        {
            string fixtureId =
                "fixture_367h_table_" + (fixtureIndex + 3).ToString("D2");
            BistroBuilder367HInstalledFixture marker =
                FindFixture(scene, fixtureId);

            if (marker != null &&
                marker.TryGetComponent(out RestaurantTable installed))
            {
                allTables.Add(installed);
                usedIds.Add(installed.TableId);
                continue;
            }

            if (!TryFindTablePosition(
                    template,
                    allTables,
                    barPosition,
                    out Vector3 position
                ))
            {
                throw new InvalidOperationException(
                    "No se encontró espacio seguro para la nueva mesa " +
                    (fixtureIndex + 3) + "."
                );
            }

            GameObject clone = Object.Instantiate(
                template.gameObject,
                template.transform.parent
            );
            Undo.RegisterCreatedObjectUndo(clone, "Crear mesa fija 367H");
            clone.name = "BB_367H_FixedTable_" +
                (fixtureIndex + 3).ToString("D2");
            clone.transform.position = position;
            clone.transform.rotation = template.transform.rotation;

            RestaurantTable table = clone.GetComponent<RestaurantTable>();
            int nextId = FindNextTableId(usedIds);
            table.AssignTableId(nextId);
            usedIds.Add(nextId);

            RestaurantPlaceableObject placeable =
                clone.GetComponent<RestaurantPlaceableObject>();
            placeable?.AssignInstanceId(
                "placeable_367h_fixed_table_" + nextId.ToString("D2")
            );

            marker = clone.GetComponent<BistroBuilder367HInstalledFixture>();
            if (marker == null)
            {
                marker = clone.AddComponent<
                    BistroBuilder367HInstalledFixture
                >();
            }
            marker.EditorAssignFixtureId(fixtureId);
            allTables.Add(table);
        }
    }

    private static void WireSystems(
        Scene scene,
        BistroBuilderBarServiceRegistry registry,
        BistroBuilderBarServiceSystem barSystem,
        BistroBuilderDishCatalogService catalogService,
        BistroBuilderRestaurantMenuService menuService
    )
    {
        OrderSystem orderSystem = RequireSingle<OrderSystem>(scene);
        TableAssignmentSystem tableAssignment =
            RequireSingle<TableAssignmentSystem>(scene);
        CustomerWaitingAreaSystem waitingArea =
            RequireSingle<CustomerWaitingAreaSystem>(scene);

        ConfigureObject(
            barSystem,
            new Dictionary<string, Object>
            {
                { "barRegistry", registry },
                { "orderSystem", orderSystem },
                { "catalogService", catalogService },
                { "menuService", menuService },
                { "tableAssignmentSystem", tableAssignment },
                { "waitingAreaSystem", waitingArea }
            }
        );

        SerializedObject barSerialized = new SerializedObject(barSystem);
        RequireProperty(barSerialized, "maximumItemsPerBarOrder")
            .intValue = 4;
        barSerialized.ApplyModifiedPropertiesWithoutUndo();

        ConfigureObject(
            tableAssignment,
            new Dictionary<string, Object>
            {
                { "barServiceSystem", barSystem }
            }
        );

        CustomerGroupSpawner[] spawners =
            FindSceneObjects<CustomerGroupSpawner>(scene);

        for (int index = 0; index < spawners.Length; index++)
        {
            SerializedObject serialized = new SerializedObject(spawners[index]);
            RequireProperty(serialized, "barServiceSystem")
                .objectReferenceValue = barSystem;
            RequireProperty(serialized, "barServiceProbability")
                .floatValue = 0.15f;
            RequireProperty(serialized, "waitingAtBarProbability")
                .floatValue = 0.25f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawners[index]);
        }

        FoodDeliveryServiceFlow[] deliveryFlows =
            FindSceneObjects<FoodDeliveryServiceFlow>(scene);

        for (int index = 0; index < deliveryFlows.Length; index++)
        {
            ConfigureObject(
                deliveryFlows[index],
                new Dictionary<string, Object>
                {
                    { "barServiceSystem", barSystem }
                }
            );
        }

        BillServiceFlow[] billFlows =
            FindSceneObjects<BillServiceFlow>(scene);

        for (int index = 0; index < billFlows.Length; index++)
        {
            ConfigureObject(
                billFlows[index],
                new Dictionary<string, Object>
                {
                    { "barServiceSystem", barSystem }
                }
            );
        }

        EditorUtility.SetDirty(registry);
        EditorUtility.SetDirty(barSystem);
    }

    private static void ConfigureObject(
        Object target,
        Dictionary<string, Object> references
    )
    {
        Undo.RecordObject(target, "Configurar 367H");
        SerializedObject serialized = new SerializedObject(target);

        foreach (KeyValuePair<string, Object> pair in references)
        {
            RequireProperty(serialized, pair.Key).objectReferenceValue =
                pair.Value;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static bool TryResolveDiningArea(
        RestaurantTable[] tables,
        out RestaurantArea area
    )
    {
        area = null;

        for (int index = 0; index < tables.Length; index++)
        {
            RestaurantAreaMember member =
                tables[index].GetComponent<RestaurantAreaMember>();

            if (member != null && member.AssignedArea != null)
            {
                area = member.AssignedArea;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindBarPose(
        RestaurantArea area,
        RestaurantTable[] tables,
        out Vector3 position,
        out Quaternion rotation
    )
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!TryGetAreaBounds(area, out Bounds bounds))
        {
            return false;
        }

        bool alongX = bounds.size.x >= bounds.size.z;
        rotation = alongX
            ? Quaternion.identity
            : Quaternion.Euler(0f, 90f, 0f);

        float y = tables[0].transform.position.y;
        Vector3[] candidates =
        {
            new Vector3(bounds.center.x, y, bounds.max.z - 1.4f),
            new Vector3(bounds.center.x, y, bounds.min.z + 1.4f),
            new Vector3(bounds.max.x - 1.4f, y, bounds.center.z),
            new Vector3(bounds.min.x + 1.4f, y, bounds.center.z)
        };

        for (int index = 0; index < candidates.Length; index++)
        {
            Vector3 candidate = candidates[index];

            if (!IsBarPoseInsideArea(area, candidate, rotation) ||
                IsNearAnyTable(candidate, tables, 2.3f))
            {
                continue;
            }

            position = candidate;
            return true;
        }

        return false;
    }

    private static bool IsBarPoseInsideArea(
        RestaurantArea area,
        Vector3 center,
        Quaternion rotation
    )
    {
        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;
        Vector3[] samples =
        {
            center,
            center + right * 2.45f,
            center - right * 2.45f,
            center + forward * 1.25f,
            center - forward * 1.25f
        };

        for (int index = 0; index < samples.Length; index++)
        {
            if (!area.ContainsPosition(samples[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryFindTablePosition(
        RestaurantTable template,
        List<RestaurantTable> tables,
        Vector3 barPosition,
        out Vector3 position
    )
    {
        position = Vector3.zero;
        RestaurantAreaMember member =
            template.GetComponent<RestaurantAreaMember>();
        RestaurantArea area = member != null ? member.AssignedArea : null;

        if (area == null || !TryGetAreaBounds(area, out Bounds bounds))
        {
            return false;
        }

        float y = template.transform.position.y;
        const float step = 2.4f;

        for (float z = bounds.min.z + 1.2f;
             z <= bounds.max.z - 1.2f;
             z += step)
        {
            for (float x = bounds.min.x + 1.2f;
                 x <= bounds.max.x - 1.2f;
                 x += step)
            {
                Vector3 candidate = new Vector3(x, y, z);

                if (!area.ContainsPosition(candidate) ||
                    Vector3.SqrMagnitude(candidate - barPosition) < 6.25f ||
                    IsNearAnyTable(candidate, tables, 1.9f))
                {
                    continue;
                }

                position = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetAreaBounds(
        RestaurantArea area,
        out Bounds bounds
    )
    {
        bounds = default;
        bool initialized = false;

        if (area == null || area.BoundaryColliders == null)
        {
            return false;
        }

        for (int index = 0; index < area.BoundaryColliders.Count; index++)
        {
            Collider collider = area.BoundaryColliders[index];

            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = collider.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return initialized;
    }

    private static bool IsNearAnyTable(
        Vector3 position,
        IEnumerable<RestaurantTable> tables,
        float minimumDistance
    )
    {
        float minimumSquared = minimumDistance * minimumDistance;

        foreach (RestaurantTable table in tables)
        {
            if (table != null &&
                (table.transform.position - position).sqrMagnitude <
                    minimumSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindNextTableId(HashSet<int> used)
    {
        for (int id = 1; id < int.MaxValue; id++)
        {
            if (!used.Contains(id))
            {
                return id;
            }
        }

        throw new InvalidOperationException(
            "No quedan identificadores funcionales de mesa disponibles."
        );
    }

    private static BistroBuilder367HInstalledFixture FindFixture(
        Scene scene,
        string fixtureId
    )
    {
        BistroBuilder367HInstalledFixture[] fixtures =
            FindSceneObjects<BistroBuilder367HInstalledFixture>(scene);

        for (int index = 0; index < fixtures.Length; index++)
        {
            if (string.Equals(
                    fixtures[index].FixtureId,
                    fixtureId,
                    StringComparison.Ordinal
                ))
            {
                return fixtures[index];
            }
        }

        return null;
    }

    private static T GetOrAdd<T>(GameObject gameObject)
        where T : Component
    {
        T component = gameObject.GetComponent<T>();

        if (component == null)
        {
            component = Undo.AddComponent<T>(gameObject);
        }

        return component;
    }

    private static T RequireSingle<T>(Scene scene)
        where T : Object
    {
        T[] objects = FindSceneObjects<T>(scene);

        if (objects.Length != 1)
        {
            throw new InvalidOperationException(
                "Debe existir exactamente un " + typeof(T).Name +
                " en la escena. Encontrados: " + objects.Length + "."
            );
        }

        return objects[0];
    }

    internal static T[] FindSceneObjects<T>(Scene scene)
        where T : Object
    {
        List<T> results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            results.AddRange(
                roots[index].GetComponentsInChildren<T>(true)
            );
        }

        return results.ToArray();
    }

    private static void BackupAssetFileOnce(
        string assetPath,
        List<AssetFileBackup> backups
    )
    {
        if (backups == null || string.IsNullOrWhiteSpace(assetPath))
        {
            throw new InvalidOperationException(
                "No se puede respaldar un asset sin ruta válida."
            );
        }

        for (int index = 0; index < backups.Count; index++)
        {
            if (string.Equals(
                    backups[index].AssetPath,
                    assetPath,
                    StringComparison.Ordinal
                ))
            {
                return;
            }
        }

        string absolutePath = Path.GetFullPath(assetPath);

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException(
                "No existe el asset que debe respaldarse.",
                absolutePath
            );
        }

        backups.Add(
            new AssetFileBackup(
                assetPath,
                File.ReadAllBytes(absolutePath)
            )
        );
    }

    private static bool ContainsReference(
        SerializedProperty array,
        Object target
    )
    {
        for (int index = 0; index < array.arraySize; index++)
        {
            if (ReferenceEquals(
                    array.GetArrayElementAtIndex(index).objectReferenceValue,
                    target
                ))
            {
                return true;
            }
        }

        return false;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string name
    )
    {
        SerializedProperty property = serialized.FindProperty(name);

        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + name +
                " en " + serialized.targetObject.GetType().Name + "."
            );
        }

        return property;
    }
}
