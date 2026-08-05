using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo, idempotente y transaccional de 2.1F.
/// Añade exclusivamente la migración consecutiva menu.state v2 -> v3;
/// los servicios y la UI existentes se amplían mediante código.
/// </summary>
public static class BistroBuilderMenuPreparation21FInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 2.1F Preparation Settings";

    [MenuItem(MenuPath, false, 180)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.1F.",
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
        byte[] backup = File.ReadAllBytes(absoluteScenePath);

        try
        {
            BistroBuilderMenuEditor21EValidationResult prerequisite =
                BistroBuilderMenuEditor21EValidator.ValidateCurrentProject();

            if (prerequisite.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    "2.1E no está validado.\n\n" + prerequisite.BuildReport()
                );
            }

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
            RequireComponent<BistroBuilderMenuSaveSectionProvider>(gameSystems);
            RequireComponent<BistroBuilderMenuEditorService>(gameSystems);
            RequireComponent<BistroBuilderOrderLineExecutionService>(
                gameSystems
            );

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar preparación configurable 2.1F"
            );
            BistroBuilderMenuStateV2ToV3Migration migration =
                GetOrAddComponent<BistroBuilderMenuStateV2ToV3Migration>(
                    gameSystems
                );
            saveService.RefreshExtensions();

            if (!saveService.ValidateConfiguration(out string saveError))
            {
                throw new InvalidOperationException(saveError);
            }

            EditorUtility.SetDirty(gameSystems);
            EditorUtility.SetDirty(migration);
            EditorUtility.SetDirty(saveService);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.Refresh();
            BistroBuilderMenuPreparation21FValidationResult result =
                BistroBuilderMenuPreparation21FValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log("BISTRO BUILDER - 2.1F INSTALADO\n" + result.BuildReport());
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1F instalado correctamente.\n\n" +
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
                "La instalación de 2.1F falló y la escena fue restaurada.\n\n" +
                exception.Message,
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
