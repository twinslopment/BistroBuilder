using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado estructurado de validación 367G1.
/// </summary>
public sealed class BistroBuilderSmartDeliveryRunsValidationResult
{
    private readonly List<string> messages = new List<string>();

    public int CorrectCount { get; private set; }
    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }

    public void Correct(string message)
    {
        CorrectCount++;
        messages.Add("- OK: " + message);
    }

    public void Warning(string message)
    {
        WarningCount++;
        messages.Add("- AVISO: " + message);
    }

    public void Error(string message)
    {
        ErrorCount++;
        messages.Add("- ERROR: " + message);
    }

    public string BuildReport()
    {
        return "BISTRO BUILDER - RONDAS INTELIGENTES 367G1\n" +
               "Correctos: " + CorrectCount + "\n" +
               "Advertencias: " + WarningCount + "\n" +
               "Errores: " + ErrorCount + "\n" +
               string.Join("\n", messages);
    }
}

/// <summary>
/// Validador no destructivo de las rondas multimesa 367G1.
/// </summary>
public static class BistroBuilderSmartDeliveryRunsValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Service/" +
        "Validate 367G1 Smart Delivery Runs",
        false,
        261
    )]
    private static void ValidateFromMenu()
    {
        BistroBuilderSmartDeliveryRunsValidationResult result =
            ValidateCurrentScene();

        Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderSmartDeliveryRunsValidationResult
        ValidateCurrentScene()
    {
        BistroBuilderSmartDeliveryRunsValidationResult result =
            new BistroBuilderSmartDeliveryRunsValidationResult();

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Error("No existe una escena activa cargada.");
            return result;
        }

        result.Correct("Escena activa cargada.");

        GameObject gameSystems =
            BistroBuilderCanonicalOrderIntegrationValidator
                .FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.Error("No se encontró GameSystems.");
            return result;
        }

        result.Correct("GameSystems localizado.");

        BistroBuilderCourseAndSharingService courses =
            gameSystems.GetComponent<BistroBuilderCourseAndSharingService>();
        BistroBuilderOrderLineExecutionService execution =
            gameSystems.GetComponent<
                BistroBuilderOrderLineExecutionService
            >();
        BistroBuilderCustomerDiningService dining =
            gameSystems.GetComponent<BistroBuilderCustomerDiningService>();

        CheckComponent(courses, "Platos compartidos y pases 367F", result);
        CheckComponent(execution, "Ejecución individual de líneas", result);
        CheckComponent(dining, "Consumo individual", result);

        if (courses != null)
        {
            Check(
                string.Equals(
                    BistroBuilderCourseAndSharingService.RuntimeRevision,
                    "367F",
                    StringComparison.Ordinal
                ),
                "La base acumulativa 367F2 permanece instalada.",
                "La revisión de pases no coincide con 367F.",
                result
            );

            CheckConfiguration(
                courses.ValidateConfiguration(out string error),
                "La coordinación de pases continúa válida.",
                error,
                result
            );
        }

        if (execution != null)
        {
            CheckConfiguration(
                execution.ValidateConfiguration(out string error),
                "La autoridad individual de líneas está válida.",
                error,
                result
            );
        }

        WaiterTaskCoordinator[] coordinators =
            BistroBuilderIndividualDishFlowValidator
                .FindSceneObjects<WaiterTaskCoordinator>(scene);

        if (coordinators.Length != 1)
        {
            result.Error(
                "Debe existir un único WaiterTaskCoordinator; encontrados: " +
                coordinators.Length + "."
            );
        }
        else
        {
            WaiterTaskCoordinator coordinator = coordinators[0];
            result.Correct("Existe un único coordinador de tareas.");

            Check(
                IsCompatibleCoordinatorRevision(
                    WaiterTaskCoordinator.RuntimeRevision
                ),
                "El coordinador declara 367G1 o una revisión acumulativa posterior.",
                "El coordinador no es compatible con 367G1.",
                result
            );
            Check(
                coordinator.ManagesFoodDeliveryTasks,
                "El coordinador gestiona el reparto de comida.",
                "El reparto central está desactivado.",
                result
            );
            Check(
                coordinator.MultiTableDeliveryRunsEnabled,
                "La agrupación multimesa está activada.",
                "La agrupación multimesa está desactivada.",
                result
            );
            Check(
                coordinator.MaximumDeliveryRunSize >= 2,
                "El límite global admite agrupación real.",
                "El límite global solo permite una línea por viaje.",
                result
            );
            Check(
                coordinator.PreferCompletingTables,
                "La planificación prioriza completar mesas.",
                "La preferencia por completar mesas está desactivada.",
                result
            );
            Check(
                coordinator.RestrictsRunsToSameResponsibleWaiter,
                "Las rondas respetan al camarero responsable de las mesas.",
                "Las rondas pueden mezclar mesas de responsables distintos.",
                result
            );
            Check(
                coordinator.DeliveryRunConsolidationSeconds > 0f &&
                coordinator.DeliveryRunConsolidationSeconds <= 3f,
                "La ventana de consolidación breve 367G1 está activa.",
                "La ventana de consolidación está desactivada o es excesiva.",
                result
            );
            CheckConfiguration(
                coordinator.ValidateIndividualDishFlowConfiguration(
                    out string error
                ),
                "El coordinador valida toda su configuración.",
                error,
                result
            );
        }

        KitchenSystem[] kitchens =
            BistroBuilderIndividualDishFlowValidator
                .FindSceneObjects<KitchenSystem>(scene);

        if (kitchens.Length == 0)
        {
            result.Error("No existe ninguna cocina operativa.");
        }
        else
        {
            result.Correct(
                "Cocinas operativas detectadas: " + kitchens.Length + "."
            );

            for (int index = 0; index < kitchens.Length; index++)
            {
                KitchenSystem kitchen = kitchens[index];

                if (kitchen == null || kitchen.PickupPoint == null)
                {
                    result.Error(
                        "Una cocina no tiene punto de recogida válido."
                    );
                    continue;
                }

                result.Correct(
                    "La cocina " + (index + 1) +
                    " tiene punto de recogida."
                );
            }
        }

        Waiter[] waiters =
            BistroBuilderIndividualDishFlowValidator
                .FindSceneObjects<Waiter>(scene);

        if (waiters.Length == 0)
        {
            result.Error("No existen camareros en la escena.");
        }
        else
        {
            result.Correct(
                "Camareros operativos detectados: " + waiters.Length + "."
            );

            for (int index = 0; index < waiters.Length; index++)
            {
                Waiter waiter = waiters[index];

                Check(
                    waiter != null &&
                    IsCompatibleWaiterRevision(Waiter.RuntimeRevision),
                    "El camarero " + (waiter != null ? waiter.WaiterId : 0) +
                    " usa 367G o una revisión acumulativa posterior.",
                    "Un camarero no es compatible con 367G.",
                    result
                );

                if (waiter == null)
                    continue;

                if (waiter.FoodDeliveryCapacity >= 2)
                {
                    result.Correct(
                        "Camarero " + waiter.WaiterId +
                        " puede transportar " +
                        waiter.FoodDeliveryCapacity + " platos."
                    );
                }
                else
                {
                    result.Warning(
                        "Camarero " + waiter.WaiterId +
                        " tiene capacidad 1 y no agrupará platos."
                    );
                }
            }
        }

        FoodDeliveryServiceFlow[] flows =
            BistroBuilderIndividualDishFlowValidator
                .FindSceneObjects<FoodDeliveryServiceFlow>(scene);

        if (flows.Length == 0)
        {
            result.Error("No existen flujos de entrega de comida.");
        }
        else
        {
            result.Correct(
                "Flujos de entrega detectados: " + flows.Length + "."
            );

            for (int index = 0; index < flows.Length; index++)
            {
                FoodDeliveryServiceFlow flow = flows[index];

                Check(
                    IsCompatibleCoordinatorRevision(
                        FoodDeliveryServiceFlow.RuntimeRevision
                    ),
                    "El flujo " + (index + 1) +
                    " declara 367G1 o una revisión acumulativa posterior.",
                    "Un flujo de reparto no es compatible con 367G1.",
                    result
                );

                if (flow == null)
                {
                    result.Error("El flujo " + (index + 1) + " es nulo.");
                }
                else
                {
                    bool valid = flow.ValidateConfiguration(
                        out string flowError
                    );

                    CheckConfiguration(
                        valid,
                        "Flujo de reparto " + (index + 1) + " validado.",
                        flowError,
                        result
                    );
                }

                if (flow != null &&
                    flow.AdditionalPickupDurationPerLine >= 0f)
                {
                    result.Correct(
                        "El tiempo incremental de recogida es válido."
                    );
                }
                else
                {
                    result.Error(
                        "El tiempo incremental de recogida es inválido."
                    );
                }
            }
        }

        FoodDeliveryAssignmentSystem[] legacy =
            BistroBuilderIndividualDishFlowValidator
                .FindSceneObjects<FoodDeliveryAssignmentSystem>(scene);

        int enabledLegacyCount = 0;

        for (int index = 0; index < legacy.Length; index++)
        {
            if (legacy[index] != null && legacy[index].enabled)
                enabledLegacyCount++;
        }

        Check(
            enabledLegacyCount == 0,
            "La autoridad legacy de reparto permanece desactivada.",
            "Hay " + enabledLegacyCount +
            " FoodDeliveryAssignmentSystem activos.",
            result
        );

        if (waiters.Length == 1)
        {
            result.Warning(
                "Solo hay un camarero en la escena; la agrupación se validará, " +
                "pero no habrá competencia entre camareros."
            );
        }

        return result;
    }

    private static void CheckComponent(
        UnityEngine.Object component,
        string name,
        BistroBuilderSmartDeliveryRunsValidationResult result
    )
    {
        if (component == null)
            result.Error("Falta " + name + ".");
        else
            result.Correct(name + " localizado.");
    }

    private static void CheckConfiguration(
        bool succeeded,
        string successMessage,
        string error,
        BistroBuilderSmartDeliveryRunsValidationResult result
    )
    {
        if (succeeded)
            result.Correct(successMessage);
        else
            result.Error(string.IsNullOrWhiteSpace(error)
                ? "La configuración no es válida."
                : error);
    }

    private static bool IsCompatibleCoordinatorRevision(string revision)
    {
        return string.Equals(revision, "367G1", StringComparison.Ordinal) ||
               string.Equals(revision, "367H", StringComparison.Ordinal);
    }

    private static bool IsCompatibleWaiterRevision(string revision)
    {
        return string.Equals(revision, "367G", StringComparison.Ordinal) ||
               string.Equals(revision, "367H", StringComparison.Ordinal);
    }

    private static void Check(
        bool condition,
        string successMessage,
        string errorMessage,
        BistroBuilderSmartDeliveryRunsValidationResult result
    )
    {
        if (condition)
            result.Correct(successMessage);
        else
            result.Error(errorMessage);
    }
}
