using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Valida la integración 367C sin modificar escenas ni datos runtime.
/// </summary>
public static class BistroBuilderCanonicalOrderIntegrationValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Orders/" +
        "Validate 367C Service Integration";

    [MenuItem(MenuPath, false, 211)]
    private static void ValidateFromMenu()
    {
        BistroBuilderCanonicalOrderIntegrationValidationResult result =
            ValidateCurrentProject();

        Debug.Log(
            "BISTRO BUILDER - VALIDACIÓN 367C\n" +
            result.BuildReport()
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderCanonicalOrderIntegrationValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderCanonicalOrderIntegrationValidationResult result =
            new BistroBuilderCanonicalOrderIntegrationValidationResult();

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Error("No existe una escena activa cargada.");
            return result;
        }

        GameObject gameSystems = FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.Error("No se encontró GameSystems.");
            return result;
        }

        result.Ok("GameSystems localizado.");

        OrderSystem orderSystem =
            gameSystems.GetComponent<OrderSystem>();
        BistroBuilderCanonicalOrderService canonicalOrders =
            gameSystems.GetComponent<
                BistroBuilderCanonicalOrderService
            >();
        BistroBuilderCanonicalOrderIntegrationService integration =
            gameSystems.GetComponent<
                BistroBuilderCanonicalOrderIntegrationService
            >();
        KitchenSystem kitchen =
            gameSystems.GetComponent<KitchenSystem>();

        if (canonicalOrders == null)
        {
            result.Error("Falta BistroBuilderCanonicalOrderService.");
        }
        else if (!canonicalOrders.ValidateConfiguration(
                     out string canonicalError
                 ))
        {
            result.Error(canonicalError);
        }
        else
        {
            result.Ok("Autoridad canónica 367B preparada.");
            result.Ok("Creación y avance atómico disponibles.");
        }

        if (integration == null)
        {
            result.Error(
                "Falta BistroBuilderCanonicalOrderIntegrationService."
            );
        }
        else if (!integration.ValidateConfiguration(
                     out string integrationError
                 ))
        {
            result.Error(integrationError);
        }
        else
        {
            result.Ok("Puente legacy-canónico preparado.");
            result.Ok("Servicio de comida concreto configurado.");
            result.Ok("Generación de clientes por grupo preparada.");
        }

        if (gameSystems.GetComponents<
                BistroBuilderCanonicalOrderIntegrationService
            >().Length != 1)
        {
            result.Error(
                "Debe existir una sola integración canónica en GameSystems."
            );
        }
        else
        {
            result.Ok("No existen puentes de comandas duplicados.");
        }

        if (orderSystem == null)
        {
            result.Error("Falta OrderSystem.");
        }
        else
        {
            SerializedObject serialized =
                new SerializedObject(orderSystem);
            SerializedProperty integrationReference =
                serialized.FindProperty(
                    "canonicalIntegrationService"
                );

            if (integrationReference == null ||
                integrationReference.objectReferenceValue == null)
            {
                result.Error(
                    "OrderSystem no tiene asignada la integración 367C."
                );
            }
            else if (!ReferenceEquals(
                         integrationReference.objectReferenceValue,
                         integration
                     ))
            {
                result.Error(
                    "OrderSystem apunta a otra integración de comandas."
                );
            }
            else if (!orderSystem.ValidateConfiguration(
                         out string orderSystemError
                     ))
            {
                result.Error(orderSystemError);
            }
            else
            {
                result.Ok("OrderSystem crea primero la comanda canónica.");
                result.Ok("Cancelación coordinada preparada.");
            }
        }

        if (integration != null &&
            canonicalOrders != null &&
            !ReferenceEquals(
                integration.CanonicalOrderService,
                canonicalOrders
            ))
        {
            result.Error(
                "La integración apunta a otra autoridad canónica."
            );
        }
        else if (integration != null &&
                 canonicalOrders != null)
        {
            result.Ok("Dependencia canónica asignada correctamente.");
        }

        if (kitchen == null)
        {
            result.Error("Falta KitchenSystem.");
        }
        else
        {
            SerializedObject kitchenSerialized =
                new SerializedObject(kitchen);
            SerializedProperty kitchenOrderSystem =
                kitchenSerialized.FindProperty("orderSystem");

            if (kitchenOrderSystem == null ||
                !ReferenceEquals(
                    kitchenOrderSystem.objectReferenceValue,
                    orderSystem
                ))
            {
                result.Error(
                    "KitchenSystem no utiliza el OrderSystem integrado."
                );
            }
            else
            {
                result.Ok("KitchenSystem conserva el flujo integrado.");
            }
        }

        WaiterTableServiceFlow[] waiterFlows =
            UnityEngine.Object.FindObjectsByType<
                WaiterTableServiceFlow
            >(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        int sceneFlowCount = 0;
        bool invalidFlow = false;

        for (int index = 0; index < waiterFlows.Length; index++)
        {
            WaiterTableServiceFlow flow = waiterFlows[index];

            if (flow == null ||
                flow.gameObject.scene != scene)
            {
                continue;
            }

            sceneFlowCount++;

            SerializedObject flowSerialized =
                new SerializedObject(flow);
            SerializedProperty flowOrderSystem =
                flowSerialized.FindProperty("orderSystem");

            if (flowOrderSystem == null ||
                !ReferenceEquals(
                    flowOrderSystem.objectReferenceValue,
                    orderSystem
                ))
            {
                invalidFlow = true;
                break;
            }
        }

        if (sceneFlowCount == 0)
        {
            result.Warning(
                "No se encontraron flujos de toma de pedido en la escena."
            );
        }
        else if (invalidFlow)
        {
            result.Error(
                "Algún WaiterTableServiceFlow utiliza otro OrderSystem."
            );
        }
        else
        {
            result.Ok(
                "Todos los flujos de toma de pedido usan la integración."
            );
        }

        if (result.ErrorCount == 0)
        {
            result.Ok(
                "367C conecta el servicio real con las comandas " +
                "canónicas sin sustituir aún cocina y entrega por línea."
            );
        }

        return result;
    }

    public static GameObject FindGameSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            if (string.Equals(
                    roots[index].name,
                    "GameSystems",
                    StringComparison.Ordinal
                ))
            {
                return roots[index];
            }
        }

        for (int index = 0; index < roots.Length; index++)
        {
            if (roots[index].GetComponent<OrderSystem>() != null)
            {
                return roots[index];
            }
        }

        return null;
    }
}

public sealed class
    BistroBuilderCanonicalOrderIntegrationValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;

    public void Ok(string message)
    {
        correct.Add(message);
    }

    public void Warning(string message)
    {
        warnings.Add(message);
    }

    public void Error(string message)
    {
        errors.Add(message);
    }

    public string BuildReport()
    {
        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        builder.AppendLine(
            "BISTRO BUILDER - INTEGRACIÓN DE COMANDAS 367C"
        );
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);

        for (int index = 0; index < correct.Count; index++)
        {
            builder.AppendLine("- OK: " + correct[index]);
        }

        for (int index = 0; index < warnings.Count; index++)
        {
            builder.AppendLine(
                "- ADVERTENCIA: " + warnings[index]
            );
        }

        for (int index = 0; index < errors.Count; index++)
        {
            builder.AppendLine("- ERROR: " + errors[index]);
        }

        return builder.ToString();
    }
}
