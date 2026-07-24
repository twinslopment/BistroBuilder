using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente de BistroBuilder 367C.
///
/// Añade el puente legacy-canónico y conecta OrderSystem con la autoridad 367B.
/// Conserva una copia binaria de la escena y la restaura ante cualquier fallo.
/// </summary>
public static class BistroBuilderCanonicalOrderIntegrationInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Orders/" +
        "Install or Repair 367C Service Integration";

    [MenuItem(MenuPath, false, 210)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de instalar 367C.",
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
                BistroBuilderCanonicalOrderIntegrationValidator
                    .FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa."
                );
            }

            OrderSystem orderSystem =
                gameSystems.GetComponent<OrderSystem>();
            BistroBuilderCanonicalOrderService canonicalOrders =
                gameSystems.GetComponent<
                    BistroBuilderCanonicalOrderService
                >();

            if (orderSystem == null)
            {
                throw new InvalidOperationException(
                    "No se encontró OrderSystem."
                );
            }

            if (canonicalOrders == null)
            {
                throw new InvalidOperationException(
                    "367B debe estar instalado antes de 367C."
                );
            }

            if (!canonicalOrders.ValidateConfiguration(
                    out string canonicalError
                ))
            {
                throw new InvalidOperationException(canonicalError);
            }

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar integración de comandas BistroBuilder 367C"
            );

            BistroBuilderCanonicalOrderIntegrationService integration =
                gameSystems.GetComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >();

            if (integration == null)
            {
                integration = Undo.AddComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >(gameSystems);
            }

            SerializedObject integrationSerialized =
                new SerializedObject(integration);

            integrationSerialized.FindProperty(
                "canonicalOrderService"
            ).objectReferenceValue = canonicalOrders;
            integrationSerialized.FindProperty(
                "currentMealService"
            ).intValue =
                (int)BistroBuilderMealServiceAvailability.Lunch;
            integrationSerialized.FindProperty(
                "defaultCourseIndex"
            ).intValue = 1;
            integrationSerialized.FindProperty(
                "logSynchronization"
            ).boolValue = true;
            integrationSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject orderSystemSerialized =
                new SerializedObject(orderSystem);

            orderSystemSerialized.FindProperty(
                "canonicalIntegrationService"
            ).objectReferenceValue = integration;
            orderSystemSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (!integration.ValidateConfiguration(
                    out string integrationError
                ))
            {
                throw new InvalidOperationException(integrationError);
            }

            if (!orderSystem.ValidateConfiguration(
                    out string orderSystemError
                ))
            {
                throw new InvalidOperationException(orderSystemError);
            }

            EditorUtility.SetDirty(integration);
            EditorUtility.SetDirty(orderSystem);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderCanonicalOrderIntegrationValidationResult result =
                BistroBuilderCanonicalOrderIntegrationValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(
                "BISTRO BUILDER - SERVICE ORDER INTEGRATION 367C\n" +
                result.BuildReport()
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Integración de comandas 367C instalada.\n\n" +
                "Errores: " + result.ErrorCount +
                "\nAdvertencias: " + result.WarningCount +
                "\n\nEjecuta ahora Validate 367C Service Integration.",
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
