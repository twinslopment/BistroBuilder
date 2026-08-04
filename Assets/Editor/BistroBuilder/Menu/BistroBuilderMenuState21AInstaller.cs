using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo, idempotente y con rollback de 2.1A.
/// No modifica definiciones, muebles, áreas, seating ni placement.
/// </summary>
public static class BistroBuilderMenuState21AInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 2.1A Restaurant Menu State";

    [MenuItem(MenuPath, false, 130)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.1A.",
                "Aceptar"
            );
            return;
        }

        if (AssetDatabase.IsValidFolder(
                BistroBuilderMenuState21AValidator.AccidentalCopyFolder
            ))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Elimina primero Assets/Scripts/Application/Menu - copia. " +
                "Contiene scripts y GUID duplicados.",
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

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);

        try
        {
            GameObject gameSystems =
                BistroBuilderMenuFoundationValidator.FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa."
                );
            }

            BistroBuilderSaveGameService saveService =
                RequireComponent<BistroBuilderSaveGameService>(gameSystems);
            BistroBuilderDishCatalogService catalogService =
                RequireComponent<BistroBuilderDishCatalogService>(gameSystems);
            BistroBuilderRestaurantMenuService menuService =
                RequireComponent<BistroBuilderRestaurantMenuService>(
                    gameSystems
                );

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar Bistro Builder 2.1A"
            );

            BistroBuilderRestaurantMenuCollectionService collectionService =
                GetOrAddComponent<
                    BistroBuilderRestaurantMenuCollectionService
                >(gameSystems);
            BistroBuilderMenuSaveSectionProvider provider =
                GetOrAddComponent<BistroBuilderMenuSaveSectionProvider>(
                    gameSystems
                );
            BistroBuilderMenuStateV1ToV2Migration migration =
                GetOrAddComponent<BistroBuilderMenuStateV1ToV2Migration>(
                    gameSystems
                );

            ConfigureCollection(
                collectionService,
                menuService,
                catalogService
            );
            ConfigureProvider(
                provider,
                saveService,
                menuService,
                catalogService,
                collectionService
            );
            ConfigureMigration(migration);

            if (!collectionService
                    .RebuildRuntimeIndexAndEnsurePrimaryRestaurant(
                        out string collectionError
                    ))
            {
                throw new InvalidOperationException(collectionError);
            }

            saveService.RefreshExtensions();

            if (!saveService.ValidateConfiguration(out string saveError))
            {
                throw new InvalidOperationException(saveError);
            }

            EditorUtility.SetDirty(collectionService);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(migration);
            EditorUtility.SetDirty(saveService);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderMenuState21AValidationResult result =
                BistroBuilderMenuState21AValidator.ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(
                "BISTRO BUILDER - 2.1A INSTALADO\n" + result.BuildReport()
            );
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1A instalado correctamente.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount,
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            RestoreScene(scenePath, absoluteScenePath, sceneBackup);
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 2.1A se ha revertido.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static void ConfigureCollection(
        BistroBuilderRestaurantMenuCollectionService service,
        BistroBuilderRestaurantMenuService menuService,
        BistroBuilderDishCatalogService catalogService
    )
    {
        SerializedObject serialized = new SerializedObject(service);
        RequireProperty(serialized, "menuService").objectReferenceValue =
            menuService;
        RequireProperty(serialized, "catalogService").objectReferenceValue =
            catalogService;
        RequireProperty(serialized, "initialRestaurantId").stringValue =
            BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;

        SerializedProperty active =
            RequireProperty(serialized, "activeRestaurantId");

        if (!BistroBuilderMenuIdUtility.IsValidStableId(active.stringValue))
        {
            active.stringValue =
                BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;
        }

        RequireProperty(serialized, "logChanges").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureProvider(
        BistroBuilderMenuSaveSectionProvider provider,
        BistroBuilderSaveGameService saveService,
        BistroBuilderRestaurantMenuService menuService,
        BistroBuilderDishCatalogService catalogService,
        BistroBuilderRestaurantMenuCollectionService collectionService
    )
    {
        SerializedObject serialized = new SerializedObject(provider);
        RequireProperty(serialized, "saveGameService").objectReferenceValue =
            saveService;
        RequireProperty(serialized, "menuService").objectReferenceValue =
            menuService;
        RequireProperty(serialized, "catalogService").objectReferenceValue =
            catalogService;
        RequireProperty(serialized, "collectionService").objectReferenceValue =
            collectionService;
        RequireProperty(serialized, "captureItemsPerFrame").intValue = 64;
        RequireProperty(serialized, "logLoadSummary").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureMigration(
        BistroBuilderMenuStateV1ToV2Migration migration
    )
    {
        SerializedObject serialized = new SerializedObject(migration);
        RequireProperty(serialized, "defaultRestaurantId").stringValue =
            BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;
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

    private static void RestoreScene(
        string scenePath,
        string absoluteScenePath,
        byte[] sceneBackup
    )
    {
        try
        {
            File.WriteAllBytes(absoluteScenePath, sceneBackup);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception rollbackException)
        {
            Debug.LogError(
                "Falló el rollback de escena 2.1A: " +
                rollbackException.Message
            );
        }
    }
}
