using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente y con rollback de BistroBuilder 367G1.
/// </summary>
public static class BistroBuilderSmartDeliveryRunsInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Service/" +
        "Install or Repair 367G1 Smart Delivery Runs";

    [MenuItem(MenuPath, false, 260)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 367G1.",
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

        try
        {
            GameObject gameSystems =
                BistroBuilderCanonicalOrderIntegrationValidator
                    .FindGameSystems(scene);

            if (gameSystems == null)
                throw new InvalidOperationException(
                    "No se encontró GameSystems."
                );

            BistroBuilderCourseAndSharingService courses =
                gameSystems.GetComponent<
                    BistroBuilderCourseAndSharingService
                >();

            if (courses == null ||
                !string.Equals(
                    BistroBuilderCourseAndSharingService.RuntimeRevision,
                    "367F",
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    "367F2 debe estar instalado antes de 367G1."
                );
            }

            WaiterTaskCoordinator[] coordinators =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<WaiterTaskCoordinator>(scene);

            if (coordinators.Length != 1)
                throw new InvalidOperationException(
                    "Debe existir un único WaiterTaskCoordinator."
                );

            WaiterTaskCoordinator coordinator = coordinators[0];
            Undo.RecordObject(coordinator, "Instalar rondas 367G1");

            SerializedObject coordinatorSerialized =
                new SerializedObject(coordinator);
            RequireProperty(
                coordinatorSerialized,
                "manageFoodDeliveryTasks"
            ).boolValue = true;
            RequireProperty(
                coordinatorSerialized,
                "enableMultiTableDeliveryRuns"
            ).boolValue = true;

            SerializedProperty maxRunProperty = RequireProperty(
                coordinatorSerialized,
                "maxDeliveryRunSize"
            );

            if (maxRunProperty.intValue < 1)
                maxRunProperty.intValue = 3;

            SerializedProperty consolidationProperty = RequireProperty(
                coordinatorSerialized,
                "deliveryRunConsolidationSeconds"
            );

            if (consolidationProperty.floatValue <= 0f ||
                float.IsNaN(consolidationProperty.floatValue) ||
                float.IsInfinity(consolidationProperty.floatValue))
            {
                consolidationProperty.floatValue = 0.8f;
            }

            RequireProperty(
                coordinatorSerialized,
                "preferCompletingTables"
            ).boolValue = true;
            RequireProperty(
                coordinatorSerialized,
                "restrictRunsToSameResponsibleWaiter"
            ).boolValue = true;
            RequireProperty(
                coordinatorSerialized,
                "logDeliveryRuns"
            ).boolValue = true;
            coordinatorSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coordinator);

            Waiter[] waiters =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<Waiter>(scene);

            if (waiters.Length == 0)
                throw new InvalidOperationException(
                    "No se encontraron camareros en la escena."
                );

            for (int index = 0; index < waiters.Length; index++)
            {
                Waiter waiter = waiters[index];
                Undo.RecordObject(waiter, "Configurar capacidad 367G1");
                SerializedObject serialized = new SerializedObject(waiter);
                SerializedProperty capacityProperty = RequireProperty(
                    serialized,
                    "foodDeliveryCapacity"
                );

                if (capacityProperty.intValue < 1)
                    capacityProperty.intValue = 3;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(waiter);
            }

            FoodDeliveryServiceFlow[] flows =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<FoodDeliveryServiceFlow>(scene);

            if (flows.Length == 0)
                throw new InvalidOperationException(
                    "No se encontraron FoodDeliveryServiceFlow."
                );

            for (int index = 0; index < flows.Length; index++)
            {
                FoodDeliveryServiceFlow flow = flows[index];
                Undo.RecordObject(flow, "Configurar flujo 367G1");
                SerializedObject serialized = new SerializedObject(flow);
                RequireProperty(serialized, "taskCoordinator")
                    .objectReferenceValue = coordinator;

                SerializedProperty extraPickupProperty = RequireProperty(
                    serialized,
                    "additionalPickupDurationPerLine"
                );

                if (extraPickupProperty.floatValue < 0f)
                    extraPickupProperty.floatValue = 0.2f;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(flow);
            }

            FoodDeliveryAssignmentSystem[] legacy =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<FoodDeliveryAssignmentSystem>(scene);

            for (int index = 0; index < legacy.Length; index++)
            {
                if (legacy[index] == null || !legacy[index].enabled)
                    continue;

                Undo.RecordObject(
                    legacy[index],
                    "Desactivar reparto legacy 367G1"
                );
                legacy[index].enabled = false;
                EditorUtility.SetDirty(legacy[index]);
            }

            if (!coordinator.ValidateIndividualDishFlowConfiguration(
                    out string coordinatorError
                ))
            {
                throw new InvalidOperationException(coordinatorError);
            }

            for (int index = 0; index < flows.Length; index++)
            {
                if (!flows[index].ValidateConfiguration(out string flowError))
                    throw new InvalidOperationException(flowError);
            }

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena."
                );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderSmartDeliveryRunsValidationResult result =
                BistroBuilderSmartDeliveryRunsValidator
                    .ValidateCurrentScene();

            if (result.ErrorCount > 0)
                throw new InvalidOperationException(result.BuildReport());

            Debug.Log(result.BuildReport());

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Rondas inteligentes 367G1 instaladas.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount +
                "\n\nEjecuta ahora el autotest 367G1.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            try
            {
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
                "La instalación ha fallado y la escena anterior se ha " +
                "restaurado.\n\n" + exception.Message,
                "Aceptar"
            );
        }
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string name
    )
    {
        SerializedProperty property = serialized.FindProperty(name);

        if (property == null)
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + name + "."
            );

        return property;
    }
}
