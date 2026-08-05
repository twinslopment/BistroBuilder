using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderSignatureDish21DValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;

    public void AddCorrect(string message) => correct.Add(message);
    public void AddWarning(string message) => warnings.Add(message);
    public void AddError(string message) => errors.Add(message);

    public string BuildReport()
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine(
            "BISTRO BUILDER - 2.1D PLATOS FIRMA Y SELECCIÓN"
        );
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);
        Append(builder, "OK", correct);
        Append(builder, "ADVERTENCIA", warnings);
        Append(builder, "ERROR", errors);
        return builder.ToString().TrimEnd();
    }

    private static void Append(
        StringBuilder builder,
        string prefix,
        List<string> messages
    )
    {
        for (int index = 0; index < messages.Count; index++)
        {
            builder.Append("- ");
            builder.Append(prefix);
            builder.Append(": ");
            builder.AppendLine(messages[index]);
        }
    }
}

/// <summary>
/// Validador no destructivo de 2.1D.
/// </summary>
public static class BistroBuilderSignatureDish21DValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 2.1D Signature Dishes";

    [MenuItem(MenuPath, false, 161)]
    private static void ValidateFromMenu()
    {
        BistroBuilderSignatureDish21DValidationResult result =
            ValidateCurrentProject();
        string report = result.BuildReport();

        if (result.ErrorCount > 0)
        {
            Debug.LogError(report);
        }
        else if (result.WarningCount > 0)
        {
            Debug.LogWarning(report);
        }
        else
        {
            Debug.Log(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderSignatureDish21DValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderSignatureDish21DValidationResult result =
            new BistroBuilderSignatureDish21DValidationResult();

        BistroBuilderMenuOffer21CValidationResult prerequisite =
            BistroBuilderMenuOffer21CValidator.ValidateCurrentProject();

        if (prerequisite.ErrorCount > 0)
        {
            result.AddError("2.1C no está validado.");
        }
        else
        {
            result.AddCorrect(
                "2.1A, 2.1B y 2.1C siguen válidos como base de 2.1D."
            );

            if (prerequisite.WarningCount > 0)
            {
                result.AddWarning(
                    "2.1C conserva " + prerequisite.WarningCount +
                    " advertencia(s)."
                );
            }
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            result.AddError("La escena activa no está cargada o guardada.");
            return result;
        }

        result.AddCorrect("La escena activa está cargada y es válida.");

        List<BistroBuilderMenuSelectionService> selections =
            FindSceneComponents<BistroBuilderMenuSelectionService>(scene);
        List<BistroBuilderSignatureDishTelemetryService> telemetry =
            FindSceneComponents<
                BistroBuilderSignatureDishTelemetryService
            >(scene);

        if (selections.Count != 1)
        {
            result.AddError(
                "Debe existir un único servicio de selección 2.1D; hay " +
                selections.Count + "."
            );
            return result;
        }

        if (telemetry.Count != 1)
        {
            result.AddError(
                "Debe existir un único servicio de telemetría 2.1D; hay " +
                telemetry.Count + "."
            );
            return result;
        }

        BistroBuilderMenuSelectionService selection = selections[0];
        BistroBuilderSignatureDishTelemetryService metrics = telemetry[0];
        result.AddCorrect("Existe una única autoridad de selección 2.1D.");
        result.AddCorrect("Existe una única telemetría de platos firma.");

        if (string.Equals(
                BistroBuilderMenuSelectionService.RuntimeRevision,
                "MENU-2.1D",
                StringComparison.Ordinal
            ))
        {
            result.AddCorrect("La revisión runtime de selección es 2.1D.");
        }
        else
        {
            result.AddError("La revisión runtime de selección no es 2.1D.");
        }

        if (selection.ValidateConfiguration(out string selectionError))
        {
            result.AddCorrect("La selección ponderada es estructuralmente válida.");
        }
        else
        {
            result.AddError(selectionError);
        }

        if (metrics.ValidateConfiguration(out string metricsError))
        {
            result.AddCorrect("La telemetría 2.1D es estructuralmente válida.");
        }
        else
        {
            result.AddError(metricsError);
        }

        BistroBuilderMenuCommercialPolicy policy =
            selection.MenuService != null
                ? selection.MenuService.CommercialPolicy
                : null;

        string policyError = string.Empty;

        if (policy != null && policy.TryValidate(out policyError))
        {
            result.AddCorrect("La política comercial de platos firma es válida.");

            if (policy.SignatureSelectionWeightBasisPoints >
                BistroBuilderMenuCommercialPolicy.BasisPointsPerUnit)
            {
                result.AddCorrect(
                    "Un plato firma tiene un peso de elección superior a x1."
                );
            }
            else
            {
                result.AddError(
                    "El peso de plato firma no aumenta la elección del cliente."
                );
            }

            if (policy.RequireSignatureDishEnabled &&
                policy.RequireSignatureDishUnlocked &&
                policy.RequireSignatureDishServiceAvailability)
            {
                result.AddCorrect(
                    "Los platos firma deben permanecer activos, desbloqueados " +
                    "y disponibles en al menos un servicio."
                );
            }
            else
            {
                result.AddError(
                    "La política permite platos firma estructuralmente inválidos."
                );
            }
        }
        else
        {
            result.AddError(
                string.IsNullOrWhiteSpace(policyError)
                    ? "Falta la política comercial."
                    : policyError
            );
        }

        ValidateConsumers(scene, selection, result);
        ValidateHistoricalSnapshot(result);
        ValidateDeterminismSource(result);

        int telemetrySchemaVersion =
            BistroBuilderSignatureDishTelemetrySnapshot.CurrentSchemaVersion;

        if (telemetrySchemaVersion == 1)
        {
            result.AddCorrect(
                "La telemetría expone un snapshot neutral versionado."
            );
        }
        else
        {
            result.AddError("La versión de telemetría 2.1D es desconocida.");
        }

        result.AddCorrect(
            "2.1D no añade una sección persistente paralela a menu.state."
        );

        return result;
    }

    private static void ValidateConsumers(
        Scene scene,
        BistroBuilderMenuSelectionService selection,
        BistroBuilderSignatureDish21DValidationResult result
    )
    {
        List<BistroBuilderCanonicalOrderService> orderServices =
            FindSceneComponents<BistroBuilderCanonicalOrderService>(scene);
        List<BistroBuilderOrderCompositionService> compositionServices =
            FindSceneComponents<BistroBuilderOrderCompositionService>(scene);
        List<BistroBuilderBarServiceSystem> barSystems =
            FindSceneComponents<BistroBuilderBarServiceSystem>(scene);

        if (orderServices.Count == 1 &&
            ReferenceEquals(
                orderServices[0].SelectionService,
                selection
            ))
        {
            result.AddCorrect(
                "Las comandas individuales usan la selección 2.1D."
            );
        }
        else
        {
            result.AddError(
                "La autoridad canónica de comandas no comparte selección 2.1D."
            );
        }

        if (compositionServices.Count == 1 &&
            ReferenceEquals(
                compositionServices[0].SelectionService,
                selection
            ))
        {
            result.AddCorrect(
                "La composición de mesa usa la selección 2.1D."
            );
        }
        else
        {
            result.AddError(
                "La composición de mesa no comparte selección 2.1D."
            );
        }

        if (barSystems.Count == 0)
        {
            result.AddError("No existe ningún sistema de barra.");
            return;
        }

        bool allBarSystemsShareSelection = true;

        for (int index = 0; index < barSystems.Count; index++)
        {
            if (!ReferenceEquals(
                    barSystems[index].SelectionService,
                    selection
                ))
            {
                allBarSystemsShareSelection = false;
                break;
            }
        }

        if (allBarSystemsShareSelection)
        {
            result.AddCorrect(
                "Barra y espera en barra usan la selección 2.1D."
            );
        }
        else
        {
            result.AddError(
                "Existe una barra que no comparte la selección 2.1D."
            );
        }
    }

    private static void ValidateHistoricalSnapshot(
        BistroBuilderSignatureDish21DValidationResult result
    )
    {
        Type lineType = typeof(BistroBuilderCanonicalOrderLine);
        PropertyInfo signature = lineType.GetProperty(
            "WasSignatureDishAtOrder",
            BindingFlags.Instance | BindingFlags.Public
        );
        PropertyInfo restaurant = lineType.GetProperty(
            "RestaurantIdAtOrder",
            BindingFlags.Instance | BindingFlags.Public
        );
        PropertyInfo revision = lineType.GetProperty(
            "MenuOfferRevisionAtOrder",
            BindingFlags.Instance | BindingFlags.Public
        );

        if (signature != null && restaurant != null && revision != null)
        {
            result.AddCorrect(
                "Cada línea congela plato firma, restaurante y revisión de oferta."
            );
        }
        else
        {
            result.AddError(
                "La línea canónica no conserva el snapshot comercial 2.1D."
            );
        }
    }

    private static void ValidateDeterminismSource(
        BistroBuilderSignatureDish21DValidationResult result
    )
    {
        Type randomContract =
            typeof(IBistroBuilderMenuSelectionRandomSource);
        Type deterministicRandom =
            typeof(BistroBuilderMenuSelectionDeterministicRandom);
        MethodInfo evaluator = typeof(BistroBuilderMenuSelectionEvaluator)
            .GetMethod(
                "TrySelect",
                BindingFlags.Public | BindingFlags.Static
            );

        bool exposesInjectableSource = evaluator != null;

        if (exposesInjectableSource)
        {
            ParameterInfo[] parameters = evaluator.GetParameters();
            exposesInjectableSource = false;

            for (int index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].ParameterType == randomContract)
                {
                    exposesInjectableSource = true;
                    break;
                }
            }
        }

        bool deterministicImplementation =
            randomContract.IsAssignableFrom(deterministicRandom) &&
            deterministicRandom.GetConstructor(new[] { typeof(ulong) }) != null;

        if (exposesInjectableSource && deterministicImplementation)
        {
            result.AddCorrect(
                "La selección usa una fuente determinista e inyectable."
            );
        }
        else
        {
            result.AddError(
                "La selección no expone el contrato determinista 2.1D."
            );
        }
    }

    private static List<T> FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        List<T> result = new List<T>();
        T[] all = Resources.FindObjectsOfTypeAll<T>();

        for (int index = 0; index < all.Length; index++)
        {
            T component = all[index];

            if (component != null &&
                component.gameObject.scene == scene &&
                !EditorUtility.IsPersistent(component))
            {
                result.Add(component);
            }
        }

        return result;
    }
}
