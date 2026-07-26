using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente y con rollback de BistroBuilder 367D.
/// </summary>
public static class BistroBuilderIndividualDishFlowInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Orders/" +
        "Install or Repair 367D1 Individual Dish Flow";

    [MenuItem(MenuPath, false, 220)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 367D1.",
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

            OrderSystem orderSystem =
                gameSystems.GetComponent<OrderSystem>();
            BistroBuilderCanonicalOrderService canonical =
                gameSystems.GetComponent<
                    BistroBuilderCanonicalOrderService
                >();
            BistroBuilderCanonicalOrderIntegrationService integration =
                gameSystems.GetComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >();

            if (orderSystem == null || canonical == null || integration == null)
            {
                throw new InvalidOperationException(
                    "367B y 367C deben estar instalados antes de 367D."
                );
            }

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar flujo individual BistroBuilder 367D"
            );

            BistroBuilderOrderLineExecutionService lineExecution =
                gameSystems.GetComponent<
                    BistroBuilderOrderLineExecutionService
                >();

            if (lineExecution == null)
            {
                lineExecution = Undo.AddComponent<
                    BistroBuilderOrderLineExecutionService
                >(gameSystems);
            }

            SerializedObject lineSerialized =
                new SerializedObject(lineExecution);
            RequireProperty(lineSerialized, "canonicalOrderService")
                .objectReferenceValue = canonical;
            RequireProperty(lineSerialized, "integrationService")
                .objectReferenceValue = integration;
            RequireProperty(lineSerialized, "logTransitions")
                .boolValue = true;
            lineSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject integrationSerialized =
                new SerializedObject(integration);
            RequireProperty(
                integrationSerialized,
                "individualLineExecutionEnabled"
            ).boolValue = true;
            integrationSerialized.ApplyModifiedPropertiesWithoutUndo();

            KitchenSystem[] kitchens =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<KitchenSystem>(scene);

            if (kitchens.Length != 1)
                throw new InvalidOperationException(
                    "367D requiere exactamente una cocina operativa " +
                    "provisional en esta fase."
                );

            HashSet<string> usedKitchenIds =
                new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < kitchens.Length; index++)
            {
                KitchenSystem kitchen = kitchens[index];
                Undo.RecordObject(kitchen, "Configurar cocina 367D");
                SerializedObject serialized = new SerializedObject(kitchen);

                RequireProperty(serialized, "orderSystem")
                    .objectReferenceValue = orderSystem;
                RequireProperty(serialized, "lineExecutionService")
                    .objectReferenceValue = lineExecution;

                SerializedProperty idProperty =
                    RequireProperty(serialized, "kitchenId");
                string normalizedId =
                    BistroBuilderOrderIdUtility.Normalize(
                        idProperty.stringValue
                    );

                if (!BistroBuilderOrderIdUtility.IsValid(normalizedId) ||
                    !usedKitchenIds.Add(normalizedId))
                {
                    normalizedId = "kitchen_" +
                        Guid.NewGuid().ToString("N");
                    usedKitchenIds.Add(normalizedId);
                }

                idProperty.stringValue = normalizedId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(kitchen);
            }

            WaiterTaskCoordinator[] coordinators =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<WaiterTaskCoordinator>(scene);

            if (coordinators.Length != 1)
                throw new InvalidOperationException(
                    "Debe existir un único WaiterTaskCoordinator."
                );

            WaiterTaskCoordinator coordinator = coordinators[0];
            Undo.RecordObject(coordinator, "Configurar coordinador 367D");
            SerializedObject coordinatorSerialized =
                new SerializedObject(coordinator);
            RequireProperty(coordinatorSerialized, "manageFoodDeliveryTasks")
                .boolValue = true;
            RequireProperty(coordinatorSerialized, "lineExecutionService")
                .objectReferenceValue = lineExecution;
            coordinatorSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coordinator);

            FoodDeliveryServiceFlow[] flows =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<FoodDeliveryServiceFlow>(scene);

            if (flows.Length == 0)
                throw new InvalidOperationException(
                    "No se encontraron FoodDeliveryServiceFlow."
                );

            KitchenSystem operationalKitchen = kitchens[0];

            for (int index = 0; index < flows.Length; index++)
            {
                FoodDeliveryServiceFlow flow = flows[index];
                Undo.RecordObject(flow, "Configurar entrega 367D");
                SerializedObject serialized = new SerializedObject(flow);
                RequireProperty(serialized, "taskCoordinator")
                    .objectReferenceValue = coordinator;
                RequireProperty(serialized, "lineExecutionService")
                    .objectReferenceValue = lineExecution;

                SerializedProperty movementProperty =
                    RequireProperty(serialized, "waiterMovementView");
                WaiterMovementView movementView =
                    movementProperty.objectReferenceValue as WaiterMovementView;

                if (movementView == null)
                {
                    movementView = flow.GetComponent<WaiterMovementView>();
                    movementProperty.objectReferenceValue = movementView;
                }

                if (movementView == null)
                {
                    throw new InvalidOperationException(
                        "Un FoodDeliveryServiceFlow no tiene " +
                        "WaiterMovementView en su GameObject."
                    );
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(flow);

                Undo.RecordObject(
                    movementView,
                    "Configurar destino de cocina 367D"
                );
                SerializedObject movementSerialized =
                    new SerializedObject(movementView);
                RequireProperty(movementSerialized, "kitchenSystem")
                    .objectReferenceValue = operationalKitchen;
                movementSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(movementView);
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
                    "Desactivar autoridad legacy de reparto"
                );
                legacy[index].enabled = false;
                EditorUtility.SetDirty(legacy[index]);
            }

            if (!lineExecution.ValidateConfiguration(out string lineError))
                throw new InvalidOperationException(lineError);

            for (int index = 0; index < kitchens.Length; index++)
            {
                if (!kitchens[index].ValidateConfiguration(
                        out string kitchenError
                    ))
                {
                    throw new InvalidOperationException(kitchenError);
                }
            }

            if (!coordinator.ValidateIndividualDishFlowConfiguration(
                    out string coordinatorError
                ))
            {
                throw new InvalidOperationException(coordinatorError);
            }

            for (int index = 0; index < flows.Length; index++)
            {
                if (!flows[index].ValidateConfiguration(
                        out string flowError
                    ))
                {
                    throw new InvalidOperationException(flowError);
                }
            }

            EditorUtility.SetDirty(lineExecution);
            EditorUtility.SetDirty(integration);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena."
                );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderIndividualDishFlowValidationResult result =
                BistroBuilderIndividualDishFlowValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
                throw new InvalidOperationException(result.BuildReport());

            Debug.Log(
                "BISTRO BUILDER - INDIVIDUAL DISH FLOW 367D1\n" +
                result.BuildReport()
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Flujo individual 367D1 instalado.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount +
                "\n\nEjecuta ahora Validate 367D1 Individual Dish Flow.",
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
