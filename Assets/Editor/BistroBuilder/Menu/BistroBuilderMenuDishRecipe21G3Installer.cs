using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo, idempotente y transaccional de 2.1G3.
/// Registra la persistencia de autoría dentro de menu.state v4 y la migración
/// consecutiva v3 -> v4 sin crear una segunda fuente de guardado.
/// </summary>
public static class BistroBuilderMenuDishRecipe21G3Installer
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 2.1G3 Dish Recipe Persistence";

    [MenuItem(MenuPath, false, 194)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.1G3.",
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

        AssetDatabase.SaveAssets();
        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);

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

            BistroBuilderSaveGameService saveGameService =
                RequireComponent<BistroBuilderSaveGameService>(gameSystems);
            BistroBuilderMenuSaveSectionProvider provider =
                RequireComponent<BistroBuilderMenuSaveSectionProvider>(
                    gameSystems
                );
            BistroBuilderDishCatalogService dishCatalog =
                RequireComponent<BistroBuilderDishCatalogService>(gameSystems);
            BistroBuilderRecipeCatalogService recipeCatalog =
                RequireComponent<BistroBuilderRecipeCatalogService>(gameSystems);
            BistroBuilderDishCategoryCatalogService categoryCatalog =
                RequireComponent<BistroBuilderDishCategoryCatalogService>(
                    gameSystems
                );
            BistroBuilderDishRecipeAuthoringService authoring =
                RequireComponent<BistroBuilderDishRecipeAuthoringService>(
                    gameSystems
                );
            RequireComponent<BistroBuilderMenuStateV1ToV2Migration>(
                gameSystems
            );
            RequireComponent<BistroBuilderMenuStateV2ToV3Migration>(
                gameSystems
            );

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar persistencia de platos y recetas 2.1G3"
            );

            BistroBuilderDishRecipePersistenceService persistence =
                GetOrAddComponent<BistroBuilderDishRecipePersistenceService>(
                    gameSystems
                );
            BistroBuilderMenuStateV3ToV4Migration migration =
                GetOrAddComponent<BistroBuilderMenuStateV3ToV4Migration>(
                    gameSystems
                );

            BistroBuilderMenuDishRecipe21G12Installer.SetReference(
                persistence,
                "dishCatalogService",
                dishCatalog
            );
            BistroBuilderMenuDishRecipe21G12Installer.SetReference(
                persistence,
                "recipeCatalogService",
                recipeCatalog
            );
            BistroBuilderMenuDishRecipe21G12Installer.SetReference(
                persistence,
                "categoryCatalogService",
                categoryCatalog
            );
            BistroBuilderMenuDishRecipe21G12Installer.SetReference(
                provider,
                "dishRecipePersistenceService",
                persistence
            );
            BistroBuilderMenuDishRecipe21G12Installer.SetReference(
                authoring,
                "persistenceService",
                persistence
            );

            EditorUtility.SetDirty(persistence);
            EditorUtility.SetDirty(migration);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(authoring);
            EditorUtility.SetDirty(saveGameService);
            EditorSceneManager.MarkSceneDirty(scene);

            saveGameService.RefreshExtensions();

            if (!persistence.ValidateConfiguration(out string error) ||
                !authoring.ValidateConfiguration(out error) ||
                !provider.ValidateConfiguration(out error) ||
                !saveGameService.ValidateConfiguration(out error))
            {
                throw new InvalidOperationException(error);
            }

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            saveGameService.RefreshExtensions();

            BistroBuilderMenuDishRecipe21G3ValidationResult result =
                BistroBuilderMenuDishRecipe21G3Validator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(
                "BISTRO BUILDER - 2.1G3 INSTALADO\n" +
                result.BuildReport()
            );
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1G3 instalado correctamente.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount,
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación de 2.1G3 falló y la escena fue " +
                "restaurada.\n\n" + exception.Message,
                "Aceptar"
            );
        }
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
        catch (Exception restoreException)
        {
            Debug.LogException(restoreException);
        }
    }
}
