using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente, acumulativo y con rollback para BistroBuilder 367E.
/// </summary>
public static class BistroBuilderIndividualCustomerDiningInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Orders/" +
        "Install or Repair 367E Individual Customer Dining";

    [MenuItem(MenuPath, false, 230)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 367E.",
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
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems."
                );
            }

            OrderSystem orderSystem =
                gameSystems.GetComponent<OrderSystem>();
            BistroBuilderCanonicalOrderService canonical =
                gameSystems.GetComponent<BistroBuilderCanonicalOrderService>();
            BistroBuilderCanonicalOrderIntegrationService integration =
                gameSystems.GetComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >();
            BistroBuilderOrderLineExecutionService lineExecution =
                gameSystems.GetComponent<
                    BistroBuilderOrderLineExecutionService
                >();

            if (orderSystem == null ||
                canonical == null ||
                integration == null ||
                lineExecution == null ||
                !integration.IndividualLineExecutionEnabled ||
                !string.Equals(
                    KitchenSystem.RuntimeRevision,
                    "367D1",
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    "367E requiere 367D1 completamente instalado y validado."
                );
            }

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar consumo individual BistroBuilder 367E"
            );

            BistroBuilderCustomerDiningService dining =
                gameSystems.GetComponent<
                    BistroBuilderCustomerDiningService
                >();
            bool diningCreated = dining == null;

            if (diningCreated)
            {
                dining = Undo.AddComponent<
                    BistroBuilderCustomerDiningService
                >(gameSystems);
            }

            SerializedObject diningSerialized =
                new SerializedObject(dining);
            RequireProperty(diningSerialized, "orderSystem")
                .objectReferenceValue = orderSystem;
            RequireProperty(diningSerialized, "canonicalOrderService")
                .objectReferenceValue = canonical;
            RequireProperty(diningSerialized, "lineExecutionService")
                .objectReferenceValue = lineExecution;

            SerializedProperty durationProperty = RequireProperty(
                diningSerialized,
                "defaultEatingDurationSeconds"
            );

            if (diningCreated ||
                float.IsNaN(durationProperty.floatValue) ||
                float.IsInfinity(durationProperty.floatValue) ||
                durationProperty.floatValue <= 0f)
            {
                durationProperty.floatValue = 6f;
            }

            if (diningCreated)
            {
                RequireProperty(diningSerialized, "logTransitions")
                    .boolValue = true;
            }

            diningSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dining);

            FoodDeliveryServiceFlow[] foodFlows =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<FoodDeliveryServiceFlow>(scene);

            if (foodFlows.Length == 0)
            {
                throw new InvalidOperationException(
                    "No se encontraron FoodDeliveryServiceFlow."
                );
            }

            for (int index = 0; index < foodFlows.Length; index++)
            {
                FoodDeliveryServiceFlow flow = foodFlows[index];
                Undo.RecordObject(flow, "Enlazar entrega con 367E");
                SerializedObject serialized = new SerializedObject(flow);
                RequireProperty(serialized, "customerDiningService")
                    .objectReferenceValue = dining;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(flow);
            }

            BillServiceFlow[] billFlows =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<BillServiceFlow>(scene);

            if (billFlows.Length == 0)
            {
                throw new InvalidOperationException(
                    "No se encontraron BillServiceFlow."
                );
            }

            for (int index = 0; index < billFlows.Length; index++)
            {
                BillServiceFlow flow = billFlows[index];
                Undo.RecordObject(flow, "Proteger cuenta con 367E");
                SerializedObject serialized = new SerializedObject(flow);
                RequireProperty(serialized, "orderSystem")
                    .objectReferenceValue = orderSystem;
                RequireProperty(serialized, "customerDiningService")
                    .objectReferenceValue = dining;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(flow);
            }

            string rebuildError = string.Empty;
            string diningError = string.Empty;

            if (!dining.RebuildRuntimeIndex(out rebuildError))
            {
                throw new InvalidOperationException(rebuildError);
            }

            if (!dining.ValidateConfiguration(out diningError))
            {
                throw new InvalidOperationException(diningError);
            }

            for (int index = 0; index < foodFlows.Length; index++)
            {
                if (!foodFlows[index].ValidateConfiguration(
                        out string flowError
                    ))
                {
                    throw new InvalidOperationException(flowError);
                }
            }

            for (int index = 0; index < billFlows.Length; index++)
            {
                if (!billFlows[index].ValidateConfiguration(
                        out string flowError
                    ))
                {
                    throw new InvalidOperationException(flowError);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new IOException(
                    "Unity no pudo guardar la escena tras instalar 367E."
                );
            }

            BistroBuilderIndividualCustomerDiningValidationResult result =
                BistroBuilderIndividualCustomerDiningValidator
                    .ValidateCurrentScene();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "BISTRO BUILDER - INSTALACIÓN 367E\n" +
                result.BuildReport()
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                result.BuildReport(),
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            try
            {
                File.WriteAllBytes(absoluteScenePath, backup);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackException)
            {
                Debug.LogException(rollbackException);
            }

            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 367E falló y se restauró la escena.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serializedObject,
        string propertyName
    )
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new MissingFieldException(
                serializedObject.targetObject.GetType().Name,
                propertyName
            );
        }

        return property;
    }
}
