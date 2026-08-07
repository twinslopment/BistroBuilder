using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo e idempotente de 2.1H/I.
/// Añade portfolios multicarte, contexto de activación, migración v4->v5 y
/// vista runtime sin crear una segunda sección de guardado.
/// </summary>
public static class BistroBuilderMenuPortfolio21HIInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 2.1H-I Multiple Menus and Rules";

    [MenuItem(MenuPath, false, 198)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.1H/I.",
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

            BistroBuilderRestaurantMenuCollectionService collection =
                Require<BistroBuilderRestaurantMenuCollectionService>(gameSystems);
            BistroBuilderRestaurantMenuService menuService =
                Require<BistroBuilderRestaurantMenuService>(gameSystems);
            BistroBuilderDishCatalogService catalog =
                Require<BistroBuilderDishCatalogService>(gameSystems);
            BistroBuilderMenuOfferService offer =
                Require<BistroBuilderMenuOfferService>(gameSystems);
            BistroBuilderMenuEditSessionService editSession =
                Require<BistroBuilderMenuEditSessionService>(gameSystems);
            BistroBuilderGeneralGameStateService generalState =
                Require<BistroBuilderGeneralGameStateService>(gameSystems);
            GameClock clock = Require<GameClock>(gameSystems);
            BistroBuilderMenuSaveSectionProvider provider =
                Require<BistroBuilderMenuSaveSectionProvider>(gameSystems);
            BistroBuilderSaveGameService saveGameService =
                Require<BistroBuilderSaveGameService>(gameSystems);
            Require<BistroBuilderMenuStateV1ToV2Migration>(gameSystems);
            Require<BistroBuilderMenuStateV2ToV3Migration>(gameSystems);
            Require<BistroBuilderMenuStateV3ToV4Migration>(gameSystems);

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar múltiples cartas y reglas 2.1H/I"
            );

            BistroBuilderMenuActivationContextService contextService =
                GetOrAdd<BistroBuilderMenuActivationContextService>(gameSystems);
            BistroBuilderMenuPortfolioService portfolioService =
                GetOrAdd<BistroBuilderMenuPortfolioService>(gameSystems);
            BistroBuilderMenuStateV4ToV5Migration migration =
                GetOrAdd<BistroBuilderMenuStateV4ToV5Migration>(gameSystems);

            SetReference(contextService, "generalGameStateService", generalState);
            SetReference(contextService, "gameClock", clock);
            SetReference(contextService, "offerService", offer);

            SetReference(portfolioService, "collectionService", collection);
            SetReference(portfolioService, "menuService", menuService);
            SetReference(portfolioService, "catalogService", catalog);
            SetReference(portfolioService, "contextService", contextService);
            SetReference(portfolioService, "editSessionService", editSession);

            SetReference(provider, "portfolioService", portfolioService);
            SetReference(provider, "contextService", contextService);

            BistroBuilderMenuEditorRuntimeView editorView =
                FindSingle<BistroBuilderMenuEditorRuntimeView>(scene);
            if (editorView == null)
            {
                throw new InvalidOperationException(
                    "No se encontró la vista runtime única del editor 2.1E."
                );
            }

            BistroBuilderMenuPortfolioRuntimeView portfolioView =
                GetOrAdd<BistroBuilderMenuPortfolioRuntimeView>(
                    editorView.gameObject
                );
            SetReference(portfolioView, "portfolioService", portfolioService);
            SetReference(portfolioView, "menuEditorView", editorView);

            if (!portfolioService.RebuildRuntimeIndexAndEnsureDefaults(
                    out string error
                ) ||
                !contextService.ValidateConfiguration(out error) ||
                !portfolioService.ValidateConfiguration(out error) ||
                !portfolioView.ValidateConfiguration(out error))
            {
                throw new InvalidOperationException(error);
            }

            EditorUtility.SetDirty(contextService);
            EditorUtility.SetDirty(portfolioService);
            EditorUtility.SetDirty(migration);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(portfolioView);
            EditorUtility.SetDirty(saveGameService);
            EditorSceneManager.MarkSceneDirty(scene);

            saveGameService.RefreshExtensions();
            if (!provider.ValidateConfiguration(out error) ||
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

            BistroBuilderMenuPortfolio21HIValidationResult result =
                BistroBuilderMenuPortfolio21HIValidator.ValidateCurrentProject();
            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log("BISTRO BUILDER - 2.1H/I INSTALADO\n" + result.BuildReport());
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1H/I instalado correctamente.\n\n" +
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
                "La instalación de 2.1H/I falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    internal static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + propertyName + "."
            );
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T Require<T>(GameObject target) where T : Component
    {
        T value = target.GetComponent<T>();
        if (value == null)
        {
            throw new InvalidOperationException(
                "GameSystems necesita " + typeof(T).Name + "."
            );
        }
        return value;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T value = target.GetComponent<T>();
        return value != null ? value : Undo.AddComponent<T>(target);
    }

    private static T FindSingle<T>(Scene scene) where T : Component
    {
        var values = BistroBuilderMenuEditor21EInstaller.FindSceneComponents<T>(scene);
        return values.Count == 1 ? values[0] : null;
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
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception restoreException)
        {
            Debug.LogException(restoreException);
        }
    }
}
