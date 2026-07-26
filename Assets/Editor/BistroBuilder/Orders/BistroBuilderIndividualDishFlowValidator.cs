using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Validador no destructivo de BistroBuilder 367D.
/// </summary>
public static class BistroBuilderIndividualDishFlowValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Orders/" +
        "Validate 367D1 Individual Dish Flow";

    [MenuItem(MenuPath, false, 221)]
    private static void ValidateFromMenu()
    {
        BistroBuilderIndividualDishFlowValidationResult result =
            ValidateCurrentProject();

        Debug.Log(
            "BISTRO BUILDER - VALIDACIÓN 367D1\n" +
            result.BuildReport()
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderIndividualDishFlowValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderIndividualDishFlowValidationResult result =
            new BistroBuilderIndividualDishFlowValidationResult();

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Error("No existe una escena activa cargada.");
            return result;
        }

        GameObject gameSystems =
            BistroBuilderCanonicalOrderIntegrationValidator
                .FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.Error("No se encontró GameSystems.");
            return result;
        }

        result.Ok("GameSystems localizado.");

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
        OrderSystem orderSystem = gameSystems.GetComponent<OrderSystem>();

        if (canonical == null)
        {
            result.Error("Falta BistroBuilderCanonicalOrderService.");
        }
        else if (!canonical.ValidateConfiguration(out string canonicalError))
        {
            result.Error(canonicalError);
        }
        else
        {
            result.Ok("Autoridad canónica de líneas preparada.");
        }

        if (integration == null)
        {
            result.Error("Falta la integración legacy-canónica 367C.");
        }
        else if (!integration.ValidateConfiguration(out string integrationError))
        {
            result.Error(integrationError);
        }
        else if (!integration.IndividualLineExecutionEnabled)
        {
            result.Error(
                "La ejecución individual de líneas no está activada."
            );
        }
        else
        {
            result.Ok("367C opera en modo individual 367D.");
        }

        int lineExecutionCount = gameSystems.GetComponents<
            BistroBuilderOrderLineExecutionService
        >().Length;

        if (lineExecutionCount != 1)
        {
            result.Error(
                "GameSystems debe contener un único " +
                "BistroBuilderOrderLineExecutionService."
            );
        }
        else if (!lineExecution.ValidateConfiguration(out string lineExecutionError))
        {
            result.Error(lineExecutionError);
        }
        else
        {
            result.Ok("Orquestador de platos físicos preparado.");
            result.Ok("Rollback de cocina y reparto disponible.");
        }

        if (orderSystem == null)
        {
            result.Error("Falta OrderSystem.");
        }
        else if (!orderSystem.ValidateConfiguration(out string orderSystemError))
        {
            result.Error(orderSystemError);
        }
        else
        {
            result.Ok("OrderSystem conserva la creación canónica primero.");
        }

        if (!string.Equals(
                KitchenSystem.RuntimeRevision,
                "367D1",
                StringComparison.Ordinal
            ))
        {
            result.Error(
                "No está instalada la guardia anti-reentrada 367D1."
            );
        }
        else
        {
            result.Ok(
                "Guardia anti-reentrada de cocina 367D1 instalada."
            );
        }

        KitchenSystem[] kitchens = FindSceneObjects<KitchenSystem>(scene);
        HashSet<string> kitchenIds =
            new HashSet<string>(StringComparer.Ordinal);
        bool invalidKitchen = false;

        if (kitchens.Length != 1)
        {
            result.Error(
                "367D requiere exactamente una cocina operativa provisional. " +
                "El enrutamiento entre varias estaciones se incorporará " +
                "sobre estos mismos contratos en un hito posterior."
            );
        }

        for (int index = 0; index < kitchens.Length; index++)
        {
            KitchenSystem kitchen = kitchens[index];

            if (kitchen == null)
            {
                invalidKitchen = true;
                result.Error("La colección contiene una cocina nula.");
                break;
            }

            if (!kitchen.ValidateConfiguration(out string kitchenError))
            {
                invalidKitchen = true;
                result.Error(kitchenError);
                break;
            }

            if (!ReferenceEquals(kitchen.LineExecutionService, lineExecution))
            {
                invalidKitchen = true;
                result.Error(
                    "Una cocina apunta a otro ejecutor de líneas."
                );
                break;
            }

            if (!kitchenIds.Add(kitchen.KitchenId))
            {
                invalidKitchen = true;
                result.Error("Existe un KitchenId duplicado.");
                break;
            }
        }

        if (!invalidKitchen && kitchens.Length == 1)
        {
            result.Ok("Cocina individual provisional válida.");
            result.Ok("KitchenId únicos y persistibles.");
            result.Ok("Duración de preparación resuelta por DishId.");
            result.Ok("Snapshot de cola y tiempo de cocina preparado.");
        }

        WaiterTaskCoordinator[] coordinators =
            FindSceneObjects<WaiterTaskCoordinator>(scene);

        if (coordinators.Length != 1)
        {
            result.Error(
                "Debe existir un único WaiterTaskCoordinator en la escena."
            );
        }
        else if (!coordinators[0]
                     .ValidateIndividualDishFlowConfiguration(
                         out string coordinatorError
                     ))
        {
            result.Error(coordinatorError);
        }
        else if (!ReferenceEquals(
                     coordinators[0].LineExecutionService,
                     lineExecution
                 ))
        {
            result.Error(
                "El coordinador apunta a otro ejecutor de líneas."
            );
        }
        else
        {
            result.Ok("Coordinador de tareas por LineId preparado.");
            result.Ok("Varias líneas de una comanda pueden repartirse.");
        }

        FoodDeliveryServiceFlow[] deliveryFlows =
            FindSceneObjects<FoodDeliveryServiceFlow>(scene);
        bool invalidFlow = deliveryFlows.Length == 0;

        for (int index = 0; index < deliveryFlows.Length; index++)
        {
            FoodDeliveryServiceFlow flow = deliveryFlows[index];

            if (flow == null)
            {
                invalidFlow = true;
                result.Error("La colección contiene un flujo de entrega nulo.");
                break;
            }

            if (!flow.ValidateConfiguration(out string flowError))
            {
                invalidFlow = true;
                result.Error(flowError);
                break;
            }

            if (!ReferenceEquals(flow.LineExecutionService, lineExecution) ||
                coordinators.Length != 1 ||
                !ReferenceEquals(flow.TaskCoordinator, coordinators[0]))
            {
                invalidFlow = true;
                result.Error(
                    "Un flujo de entrega tiene dependencias distintas."
                );
                break;
            }

            if (flow.MovementView == null)
            {
                invalidFlow = true;
                result.Error(
                    "Un flujo de entrega no tiene WaiterMovementView."
                );
                break;
            }

            SerializedObject movementSerialized =
                new SerializedObject(flow.MovementView);
            SerializedProperty kitchenProperty =
                movementSerialized.FindProperty("kitchenSystem");

            if (kitchenProperty == null ||
                kitchens.Length != 1 ||
                !ReferenceEquals(
                    kitchenProperty.objectReferenceValue,
                    kitchens[0]
                ))
            {
                invalidFlow = true;
                result.Error(
                    "Un camarero se desplaza hacia una cocina distinta " +
                    "de la cocina operativa de 367D."
                );
                break;
            }
        }

        if (invalidFlow)
        {
            if (deliveryFlows.Length == 0)
            {
                result.Error(
                    "No se encontraron flujos de entrega de comida."
                );
            }
        }
        else
        {
            result.Ok(
                "Flujos de entrega individual válidos: " +
                deliveryFlows.Length + "."
            );
            result.Ok("Recogida, transporte y servicio se validan por línea.");
            result.Ok("Todos los camareros comparten el pase operativo válido.");
        }

        FoodDeliveryAssignmentSystem[] legacyAssignments =
            FindSceneObjects<FoodDeliveryAssignmentSystem>(scene);
        bool legacyAuthorityActive = false;

        for (int index = 0; index < legacyAssignments.Length; index++)
        {
            if (legacyAssignments[index] != null &&
                legacyAssignments[index].enabled)
            {
                legacyAuthorityActive = true;
                break;
            }
        }

        if (legacyAuthorityActive)
        {
            result.Error(
                "FoodDeliveryAssignmentSystem sigue activo y duplicaría " +
                "la autoridad del coordinador."
            );
        }
        else
        {
            result.Ok("No existe autoridad de reparto duplicada.");
        }

        if (typeof(WaiterTask).GetProperty("OrderLineId") == null ||
            typeof(Waiter).GetProperty("AssignedOrderLineId") == null)
        {
            result.Error("Los contratos de camarero no exponen LineId.");
        }
        else
        {
            result.Ok("Tareas y camareros conservan OrderLineId.");
        }

        BistroBuilderKitchenRuntimeSnapshot emptySnapshot =
            new BistroBuilderKitchenRuntimeSnapshot
            {
                kitchenId = "kitchen_validation",
                nextSequence = 0
            };

        if (!emptySnapshot.TryValidate(out string snapshotError))
        {
            result.Error(snapshotError);
        }
        else
        {
            result.Ok("Contrato de persistencia de cocina versionado.");
        }

        if (result.ErrorCount == 0)
        {
            result.Ok(
                "367D prepara, recoge y sirve cada plato físico de forma " +
                "independiente sin duplicar la autoridad canónica."
            );
        }

        return result;
    }

    public static T[] FindSceneObjects<T>(Scene scene)
        where T : UnityEngine.Object
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        List<T> sceneItems = new List<T>();

        for (int index = 0; index < all.Length; index++)
        {
            Component component = all[index] as Component;

            if (component != null && component.gameObject.scene == scene)
            {
                sceneItems.Add(all[index]);
            }
        }

        return sceneItems.ToArray();
    }
}

public sealed class BistroBuilderIndividualDishFlowValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;

    public void Ok(string message) => correct.Add(message);
    public void Warning(string message) => warnings.Add(message);
    public void Error(string message) => errors.Add(message);

    public string BuildReport()
    {
        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        builder.AppendLine(
            "BISTRO BUILDER - FLUJO INDIVIDUAL DE PLATOS 367D"
        );
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);

        for (int index = 0; index < correct.Count; index++)
            builder.AppendLine("- OK: " + correct[index]);
        for (int index = 0; index < warnings.Count; index++)
            builder.AppendLine("- ADVERTENCIA: " + warnings[index]);
        for (int index = 0; index < errors.Count; index++)
            builder.AppendLine("- ERROR: " + errors[index]);

        return builder.ToString();
    }
}
