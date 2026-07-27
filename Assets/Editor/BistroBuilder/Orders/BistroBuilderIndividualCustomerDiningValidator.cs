using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Validador de solo lectura para BistroBuilder 367E.
/// </summary>
public static class BistroBuilderIndividualCustomerDiningValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Orders/" +
        "Validate 367E Individual Customer Dining";

    [MenuItem(MenuPath, false, 231)]
    private static void ValidateFromMenu()
    {
        BistroBuilderIndividualCustomerDiningValidationResult result =
            ValidateCurrentScene();

        Debug.Log(
            "BISTRO BUILDER - VALIDACIÓN 367E\n" +
            result.BuildReport()
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderIndividualCustomerDiningValidationResult
        ValidateCurrentScene()
    {
        BistroBuilderIndividualCustomerDiningValidationResult result =
            new BistroBuilderIndividualCustomerDiningValidationResult();

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Error("No existe una escena activa cargada.");
            return result;
        }

        result.Ok("Escena activa cargada."); // 1

        GameObject gameSystems =
            BistroBuilderCanonicalOrderIntegrationValidator
                .FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.Error("No se encontró GameSystems.");
            return result;
        }

        result.Ok("GameSystems localizado."); // 2

        OrderSystem orderSystem = gameSystems.GetComponent<OrderSystem>();
        BistroBuilderCanonicalOrderService canonical =
            gameSystems.GetComponent<BistroBuilderCanonicalOrderService>();
        BistroBuilderCanonicalOrderIntegrationService integration =
            gameSystems.GetComponent<
                BistroBuilderCanonicalOrderIntegrationService
            >();
        BistroBuilderOrderLineExecutionService lineExecution =
            gameSystems.GetComponent<BistroBuilderOrderLineExecutionService>();

        if (!string.Equals(
                KitchenSystem.RuntimeRevision,
                "367D1",
                StringComparison.Ordinal
            ))
        {
            result.Error(
                "367E requiere la revisión de cocina 367D1 validada."
            );
        }
        else
        {
            result.Ok("Base 367D1 de cocina instalada."); // 3
        }

        string orderError = string.Empty;

        if (orderSystem == null)
        {
            result.Error("Falta OrderSystem.");
        }
        else if (!orderSystem.ValidateConfiguration(out orderError))
        {
            result.Error(orderError);
        }
        else
        {
            result.Ok("OrderSystem preparado."); // 4
        }

        string canonicalError = string.Empty;

        if (canonical == null)
        {
            result.Error("Falta BistroBuilderCanonicalOrderService.");
        }
        else if (!canonical.ValidateConfiguration(out canonicalError))
        {
            result.Error(canonicalError);
        }
        else
        {
            result.Ok("Autoridad canónica preparada."); // 5
        }

        string integrationError = string.Empty;

        if (integration == null)
        {
            result.Error("Falta la integración legacy-canónica.");
        }
        else if (!integration.ValidateConfiguration(out integrationError))
        {
            result.Error(integrationError);
        }
        else
        {
            result.Ok("Integración legacy-canónica preparada."); // 6
        }

        if (integration == null || !integration.IndividualLineExecutionEnabled)
        {
            result.Error("La ejecución individual 367D no está activa.");
        }
        else
        {
            result.Ok("Ejecución individual de líneas activa."); // 7
        }

        string executionError = string.Empty;

        if (lineExecution == null)
        {
            result.Error("Falta BistroBuilderOrderLineExecutionService.");
        }
        else if (!lineExecution.ValidateConfiguration(out executionError))
        {
            result.Error(executionError);
        }
        else
        {
            result.Ok("Ejecutor de líneas preparado."); // 8
        }

        MethodInfo consumeMethod = typeof(BistroBuilderCanonicalOrderService)
            .GetMethod(
                "TryConsumeServedLines",
                BindingFlags.Instance | BindingFlags.Public
            );

        if (consumeMethod == null)
        {
            result.Error(
                "La autoridad canónica no expone consumo atómico de líneas."
            );
        }
        else
        {
            result.Ok("Consumo atómico de líneas disponible."); // 9
        }

        BistroBuilderCustomerDiningService[] diningServices =
            gameSystems.GetComponents<BistroBuilderCustomerDiningService>();

        if (diningServices.Length != 1)
        {
            result.Error(
                "Debe existir una única autoridad de consumo individual " +
                "en GameSystems."
            );
        }
        else if (!string.Equals(
                     BistroBuilderCustomerDiningService.RuntimeRevision,
                     "367E",
                     StringComparison.Ordinal
                 ))
        {
            result.Error("La revisión runtime del consumo no es 367E.");
        }
        else
        {
            result.Ok("Autoridad única de consumo individual 367E."); // 10
        }

        BistroBuilderCustomerDiningService dining =
            diningServices.Length == 1 ? diningServices[0] : null;

        if (dining == null ||
            !ReferenceEquals(dining.OrderSystem, orderSystem) ||
            !ReferenceEquals(dining.CanonicalOrderService, canonical) ||
            !ReferenceEquals(dining.LineExecutionService, lineExecution))
        {
            result.Error(
                "El consumo individual no conserva las dependencias " +
                "canónicas correctas."
            );
        }
        else
        {
            result.Ok("Dependencias de consumo asignadas correctamente."); // 11
        }

        string diningError = string.Empty;

        if (dining == null)
        {
            result.Error("Falta BistroBuilderCustomerDiningService.");
        }
        else if (!dining.ValidateConfiguration(out diningError))
        {
            result.Error(diningError);
        }
        else
        {
            result.Ok("Configuración del consumo individual válida."); // 12
        }

        if (dining == null ||
            dining.DefaultEatingDurationSeconds <= 0f ||
            float.IsNaN(dining.DefaultEatingDurationSeconds) ||
            float.IsInfinity(dining.DefaultEatingDurationSeconds))
        {
            result.Error("La duración de consumo individual es inválida.");
        }
        else
        {
            result.Ok("Duración individual positiva y finita."); // 13
        }

        BistroBuilderCustomerDiningRuntimeSnapshot emptySnapshot =
            new BistroBuilderCustomerDiningRuntimeSnapshot(
                new List<BistroBuilderCustomerDiningOrderRuntime>()
            );

        string snapshotError = string.Empty;
        bool snapshotVersionValid = emptySnapshot.SchemaVersion ==
            BistroBuilderCustomerDiningRuntimeSnapshot.CurrentSchemaVersion;
        bool snapshotValid = emptySnapshot.TryValidate(out snapshotError);

        if (!snapshotVersionValid || !snapshotValid)
        {
            result.Error(
                string.IsNullOrWhiteSpace(snapshotError)
                    ? "El snapshot 367E no es válido."
                    : snapshotError
            );
        }
        else
        {
            result.Ok("Snapshot de consumo versionado y válido."); // 14
        }

        if (!Attribute.IsDefined(
                typeof(BistroBuilderCustomerDiningOrderRuntime),
                typeof(SerializableAttribute)
            ))
        {
            result.Error("El runtime de comanda no es serializable.");
        }
        else
        {
            result.Ok("Runtime de comanda serializable."); // 15
        }

        if (!Attribute.IsDefined(
                typeof(BistroBuilderCustomerDiningCustomerRuntime),
                typeof(SerializableAttribute)
            ))
        {
            result.Error("El runtime de cliente no es serializable.");
        }
        else
        {
            result.Ok("Runtime de cliente serializable."); // 16
        }

        FieldInfo oldRoutineField = typeof(CustomerDiningFlow).GetField(
            "activeRoutine",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (oldRoutineField != null)
        {
            result.Error(
                "CustomerDiningFlow todavía contiene el temporizador global."
            );
        }
        else
        {
            result.Ok("Temporizador global de grupo eliminado."); // 17
        }

        MethodInfo oldEatingRoutine = typeof(CustomerDiningFlow).GetMethod(
            "EatingRoutine",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (oldEatingRoutine != null)
        {
            result.Error(
                "CustomerDiningFlow todavía ejecuta una corrutina grupal."
            );
        }
        else
        {
            result.Ok("CustomerDiningFlow queda como adaptador pasivo."); // 18
        }

        FoodDeliveryServiceFlow[] foodFlows =
            BistroBuilderIndividualDishFlowValidator
                .FindSceneObjects<FoodDeliveryServiceFlow>(scene);

        if (foodFlows.Length == 0)
        {
            result.Error("No se encontraron flujos de entrega de comida.");
        }
        else
        {
            result.Ok(
                "Flujos de entrega encontrados: " + foodFlows.Length + "."
            ); // 19
        }

        bool foodReferencesValid = foodFlows.Length > 0;
        bool foodConfigurationValid = foodFlows.Length > 0;

        for (int index = 0; index < foodFlows.Length; index++)
        {
            FoodDeliveryServiceFlow flow = foodFlows[index];

            if (flow == null ||
                !ReferenceEquals(flow.CustomerDiningService, dining))
            {
                foodReferencesValid = false;
            }

            if (flow == null ||
                !flow.ValidateConfiguration(out _))
            {
                foodConfigurationValid = false;
            }
        }

        if (!foodReferencesValid)
        {
            result.Error(
                "Un flujo de entrega apunta a otra autoridad de consumo."
            );
        }
        else
        {
            result.Ok("Entregas enlazadas al consumo individual."); // 20
        }

        if (!foodConfigurationValid)
        {
            result.Error("Existe un flujo de entrega inválido.");
        }
        else
        {
            result.Ok("Flujos de entrega 367E válidos."); // 21
        }

        BillServiceFlow[] billFlows =
            BistroBuilderIndividualDishFlowValidator
                .FindSceneObjects<BillServiceFlow>(scene);

        if (billFlows.Length == 0)
        {
            result.Error("No se encontraron flujos de cuenta y pago.");
        }
        else
        {
            result.Ok(
                "Flujos de cuenta encontrados: " + billFlows.Length + "."
            ); // 22
        }

        bool billReferencesValid = billFlows.Length > 0;
        bool billConfigurationValid = billFlows.Length > 0;

        for (int index = 0; index < billFlows.Length; index++)
        {
            BillServiceFlow flow = billFlows[index];

            if (flow == null ||
                !ReferenceEquals(flow.CustomerDiningService, dining) ||
                !ReferenceEquals(flow.OrderSystem, orderSystem))
            {
                billReferencesValid = false;
            }

            if (flow == null ||
                !flow.ValidateConfiguration(out _))
            {
                billConfigurationValid = false;
            }
        }

        if (!billReferencesValid)
        {
            result.Error(
                "Un flujo de cuenta no está protegido por 367E."
            );
        }
        else
        {
            result.Ok("Cuentas enlazadas a la guardia individual."); // 23
        }

        if (!billConfigurationValid)
        {
            result.Error("Existe un flujo de cuenta inválido.");
        }
        else
        {
            result.Ok("Guardia de cuenta 367E válida."); // 24
        }

        return result;
    }
}

public sealed class BistroBuilderIndividualCustomerDiningValidationResult
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
            "BISTRO BUILDER - CONSUMO INDIVIDUAL 367E"
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
            builder.AppendLine("- ADVERTENCIA: " + warnings[index]);
        }

        for (int index = 0; index < errors.Count; index++)
        {
            builder.AppendLine("- ERROR: " + errors[index]);
        }

        return builder.ToString();
    }
}
