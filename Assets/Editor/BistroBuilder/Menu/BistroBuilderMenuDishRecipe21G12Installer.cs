using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo, idempotente y transaccional de 2.1G1/2.
/// Añade una única autoridad de autoría runtime y la integra en el editor
/// 2.1E sin crear persistencia paralela. La persistencia llegará en 2.1G3.
/// </summary>
public static class BistroBuilderMenuDishRecipe21G12Installer
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 2.1G1-2 Dish and Recipe Authoring";

    [MenuItem(MenuPath, false, 190)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.1G1/2.",
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

            BistroBuilderDishCatalogService dishCatalog =
                RequireComponent<BistroBuilderDishCatalogService>(gameSystems);
            BistroBuilderRecipeCatalogService recipeCatalog =
                RequireComponent<BistroBuilderRecipeCatalogService>(gameSystems);
            BistroBuilderDishCategoryCatalogService categoryCatalog =
                RequireComponent<BistroBuilderDishCategoryCatalogService>(
                    gameSystems
                );
            BistroBuilderMenuEditSessionService editSession =
                RequireComponent<BistroBuilderMenuEditSessionService>(
                    gameSystems
                );
            BistroBuilderMenuEditorService editorService =
                RequireComponent<BistroBuilderMenuEditorService>(gameSystems);

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar autoría de platos y recetas 2.1G1/2"
            );

            BistroBuilderDishRecipeAuthoringService authoringService =
                GetOrAddComponent<BistroBuilderDishRecipeAuthoringService>(
                    gameSystems
                );
            SetReference(
                authoringService,
                "dishCatalogService",
                dishCatalog
            );
            SetReference(
                authoringService,
                "recipeCatalogService",
                recipeCatalog
            );
            SetReference(
                authoringService,
                "categoryCatalogService",
                categoryCatalog
            );
            SetReference(
                authoringService,
                "editSessionService",
                editSession
            );
            SetReference(editSession, "authoringService", authoringService);
            SetReference(editorService, "authoringService", authoringService);

            BistroBuilderMenuEditorRuntimeView editorView =
                FindSingle<BistroBuilderMenuEditorRuntimeView>(scene);

            if (editorView == null)
            {
                throw new InvalidOperationException(
                    "No se encontró la vista runtime única del editor 2.1E."
                );
            }

            BistroBuilderDishRecipeAuthoringRuntimeView authoringView =
                GetOrAddComponent<
                    BistroBuilderDishRecipeAuthoringRuntimeView
                >(editorView.gameObject);
            SetReference(authoringView, "editorService", editorService);
            SetReference(editorView, "authoringView", authoringView);

            EditorUtility.SetDirty(authoringService);
            EditorUtility.SetDirty(editSession);
            EditorUtility.SetDirty(editorService);
            EditorUtility.SetDirty(authoringView);
            EditorUtility.SetDirty(editorView);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!authoringService.ValidateConfiguration(out string error) ||
                !editorService.ValidateConfiguration(out error) ||
                !authoringView.ValidateConfiguration(out error) ||
                !editorView.ValidateConfiguration(out error))
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

            BistroBuilderMenuDishRecipe21G12ValidationResult result =
                BistroBuilderMenuDishRecipe21G12Validator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(
                "BISTRO BUILDER - 2.1G1/2 INSTALADO\n" +
                result.BuildReport()
            );
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1G1/2 instalado correctamente.\n\n" +
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
                "La instalación de 2.1G1/2 falló y la escena fue " +
                "restaurada.\n\n" + exception.Message,
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

    private static T FindSingle<T>(Scene scene) where T : Component
    {
        var values = BistroBuilderMenuEditor21EInstaller
            .FindSceneComponents<T>(scene);
        return values.Count == 1 ? values[0] : null;
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
