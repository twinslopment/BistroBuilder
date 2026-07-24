using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente de BistroBuilder 367B.
///
/// Solo añade la autoridad canónica de comandas sobre la carta validada 367A.
/// Conserva una copia binaria de la escena y la restaura ante cualquier fallo.
/// </summary>
public static class BistroBuilderCanonicalOrderFoundationInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Orders/" +
        "Install or Repair 367B Canonical Orders Foundation";

    [MenuItem(MenuPath, false, 200)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de instalar 367B.",
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
                "Abre y guarda Prototype_Restaurant.unity antes de " +
                "ejecutar el instalador.",
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

        try
        {
            GameObject gameSystems =
                BistroBuilderCanonicalOrderFoundationValidator
                    .FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa."
                );
            }

            BistroBuilderRestaurantMenuService menu =
                gameSystems.GetComponent<
                    BistroBuilderRestaurantMenuService
                >();

            if (menu == null)
            {
                throw new InvalidOperationException(
                    "367A debe estar validado antes de instalar 367B. " +
                    "No se encontró BistroBuilderRestaurantMenuService."
                );
            }

            if (!menu.ValidateConfiguration(out string menuError))
            {
                throw new InvalidOperationException(
                    "367A debe estar validado antes de instalar 367B. " +
                    menuError
                );
            }

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar comandas canónicas BistroBuilder 367B"
            );

            BistroBuilderCanonicalOrderService orderService =
                gameSystems.GetComponent<
                    BistroBuilderCanonicalOrderService
                >();

            if (orderService == null)
            {
                orderService = Undo.AddComponent<
                    BistroBuilderCanonicalOrderService
                >(gameSystems);
            }

            SerializedObject serialized =
                new SerializedObject(orderService);

            serialized.FindProperty("menuService").objectReferenceValue =
                menu;
            serialized.FindProperty("logChanges").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!orderService.RebuildRuntimeIndex(out string error))
            {
                throw new InvalidOperationException(error);
            }

            EditorUtility.SetDirty(orderService);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderCanonicalOrderValidationResult result =
                BistroBuilderCanonicalOrderFoundationValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(
                "BISTRO BUILDER - CANONICAL ORDERS FOUNDATION 367B\n" +
                result.BuildReport()
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Comandas canónicas 367B instaladas.\n\n" +
                "Errores: " + result.ErrorCount +
                "\nAdvertencias: " + result.WarningCount +
                "\n\nEjecuta ahora Validate 367B Canonical Orders " +
                "Foundation.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            try
            {
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
                "La instalación ha fallado y se ha restaurado la escena " +
                "anterior.\n\n" + exception.Message,
                "Aceptar"
            );
        }
    }
}
