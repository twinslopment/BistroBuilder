using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado estructurado de validación 367F.
/// </summary>
public sealed class BistroBuilderSharedCoursesValidationResult
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
        return "BISTRO BUILDER - PLATOS COMPARTIDOS Y PASES 367F\n" +
               "Correctos: " + CorrectCount + "\n" +
               "Advertencias: " + WarningCount + "\n" +
               "Errores: " + ErrorCount + "\n" +
               string.Join("\n", messages);
    }
}

/// <summary>
/// Validador no destructivo de BistroBuilder 367F.
/// </summary>
public static class BistroBuilderSharedCoursesValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Orders/" +
        "Validate 367F Shared Dishes and Courses",
        false,
        241
    )]
    private static void ValidateFromMenu()
    {
        BistroBuilderSharedCoursesValidationResult result =
            ValidateCurrentScene();

        Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderSharedCoursesValidationResult
        ValidateCurrentScene()
    {
        BistroBuilderSharedCoursesValidationResult result =
            new BistroBuilderSharedCoursesValidationResult();

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

        OrderSystem orderSystem = gameSystems.GetComponent<OrderSystem>();
        BistroBuilderCanonicalOrderService canonical =
            gameSystems.GetComponent<BistroBuilderCanonicalOrderService>();
        BistroBuilderCanonicalOrderIntegrationService integration =
            gameSystems.GetComponent<
                BistroBuilderCanonicalOrderIntegrationService
            >();
        BistroBuilderOrderLineExecutionService execution =
            gameSystems.GetComponent<BistroBuilderOrderLineExecutionService>();
        BistroBuilderCustomerDiningService dining =
            gameSystems.GetComponent<BistroBuilderCustomerDiningService>();
        BistroBuilderOrderCompositionService composition =
            gameSystems.GetComponent<BistroBuilderOrderCompositionService>();
        BistroBuilderCourseAndSharingService courses =
            gameSystems.GetComponent<BistroBuilderCourseAndSharingService>();

        CheckComponent(orderSystem, "OrderSystem", result);
        CheckComponent(canonical, "Autoridad canónica", result);
        CheckComponent(integration, "Integración legacy-canónica", result);
        CheckComponent(execution, "Ejecución individual de líneas", result);
        CheckComponent(dining, "Consumo individual", result);
        CheckComponent(composition, "Compositor de comandas", result);
        CheckComponent(courses, "Coordinador de pases", result);

        if (orderSystem != null)
        {
            CheckConfiguration(
                orderSystem.ValidateConfiguration(out string error),
                "OrderSystem validado.",
                error,
                result
            );
        }

        if (canonical != null)
        {
            CheckConfiguration(
                canonical.ValidateConfiguration(out string error),
                "Autoridad canónica validada.",
                error,
                result
            );
        }

        if (execution != null)
        {
            CheckConfiguration(
                execution.ValidateConfiguration(out string error),
                "Ejecución individual validada.",
                error,
                result
            );
        }

        if (dining != null)
        {
            CheckConfiguration(
                dining.ValidateConfiguration(out string error),
                "Consumo individual 367F validado.",
                error,
                result
            );

            if (string.Equals(
                    BistroBuilderCustomerDiningService.RuntimeRevision,
                    "367F",
                    StringComparison.Ordinal
                ))
            {
                result.Correct("La autoridad de consumo declara revisión 367F.");
            }
            else
            {
                result.Error("La autoridad de consumo no declara revisión 367F.");
            }

            if (dining.PerCustomerEatingDurationOffsetSeconds > 0f)
            {
                result.Correct("El progreso parcial compartido es observable.");
            }
            else
            {
                result.Warning(
                    "El desfase de consumo es 0; la lógica es válida, pero " +
                    "los consumidores podrían terminar simultáneamente."
                );
            }
        }

        if (composition != null)
        {
            CheckConfiguration(
                composition.ValidateConfiguration(out string error),
                "Compositor 367F validado.",
                error,
                result
            );

            BistroBuilderOrderCompositionProfile profile =
                composition.CompositionProfile;

            if (profile == null)
            {
                result.Error("El compositor no tiene perfil asignado.");
            }
            else
            {
                result.Correct("Perfil de composición asignado.");
                ValidateProfileCapabilities(profile, result);
            }
        }

        if (courses != null)
        {
            CheckConfiguration(
                courses.ValidateConfiguration(out string error),
                "Coordinación de pases validada.",
                error,
                result
            );

            if (string.Equals(
                    BistroBuilderCourseAndSharingService.RuntimeRevision,
                    "367F",
                    StringComparison.Ordinal
                ))
            {
                result.Correct("El coordinador declara revisión 367F.");
            }
            else
            {
                result.Error("El coordinador no declara revisión 367F.");
            }
        }

        if (integration != null)
        {
            if (integration.IndividualLineExecutionEnabled)
            {
                result.Correct("La ejecución individual 367D permanece activa.");
            }
            else
            {
                result.Error("La ejecución individual 367D está desactivada.");
            }

            if (integration.CourseAndSharingExecutionEnabled)
            {
                result.Correct("La ejecución 367F está activa.");
            }
            else
            {
                result.Error("La ejecución 367F está desactivada.");
            }

            if (ReferenceEquals(
                    integration.OrderCompositionService,
                    composition
                ))
            {
                result.Correct("La integración usa el compositor instalado.");
            }
            else
            {
                result.Error("La integración no usa el compositor instalado.");
            }

            if (ReferenceEquals(
                    integration.CourseAndSharingService,
                    courses
                ))
            {
                result.Correct("La integración usa el coordinador instalado.");
            }
            else
            {
                result.Error("La integración no usa el coordinador instalado.");
            }
        }

        KitchenSystem[] kitchens =
            BistroBuilderIndividualDishFlowValidator
                .FindSceneObjects<KitchenSystem>(scene);

        if (kitchens.Length == 0)
        {
            result.Error("No se encontró ninguna cocina operativa.");
        }
        else
        {
            result.Correct("Cocinas operativas localizadas: " + kitchens.Length + ".");

            bool allRevision = true;
            bool allValid = true;

            for (int index = 0; index < kitchens.Length; index++)
            {
                allRevision &= string.Equals(
                    KitchenSystem.RuntimeRevision,
                    "367F",
                    StringComparison.Ordinal
                );
                allValid &= kitchens[index] != null &&
                            kitchens[index].ValidateConfiguration(out _);
            }

            if (allRevision)
            {
                result.Correct("Las cocinas escuchan liberaciones 367F.");
            }
            else
            {
                result.Error("Alguna cocina no declara revisión 367F.");
            }

            if (allValid)
            {
                result.Correct("Todas las cocinas se validan.");
            }
            else
            {
                result.Error("Alguna cocina no supera su validación.");
            }
        }

        CheckSingleComponent<BistroBuilderOrderCompositionService>(
            gameSystems,
            "No existen compositores duplicados.",
            result
        );
        CheckSingleComponent<BistroBuilderCourseAndSharingService>(
            gameSystems,
            "No existen coordinadores de pases duplicados.",
            result
        );
        CheckSingleComponent<BistroBuilderCustomerDiningService>(
            gameSystems,
            "No existen autoridades de consumo duplicadas.",
            result
        );

        if (typeof(BistroBuilderCanonicalOrderService).GetMethod(
                "TrySubmitOrderAndReleaseCourse"
            ) != null)
        {
            result.Correct("La autoridad canónica expone liberación inicial atómica.");
        }
        else
        {
            result.Error("Falta la liberación inicial atómica.");
        }

        if (typeof(BistroBuilderCanonicalOrderService).GetMethod(
                "TryReleaseSubmittedLines"
            ) != null)
        {
            result.Correct("La autoridad canónica expone liberación selectiva atómica.");
        }
        else
        {
            result.Error("Falta la liberación selectiva atómica.");
        }

        return result;
    }

    private static void ValidateProfileCapabilities(
        BistroBuilderOrderCompositionProfile profile,
        BistroBuilderSharedCoursesValidationResult result
    )
    {
        if (!profile.TryValidate(out string error))
        {
            result.Error(error);
            return;
        }

        result.Correct("Perfil de composición validado.");

        HashSet<int> courses = new HashSet<int>();
        bool hasShared = false;
        bool hasIndividual = false;

        for (int index = 0; index < profile.Rules.Count; index++)
        {
            BistroBuilderCourseCompositionRule rule = profile.Rules[index];

            if (rule == null || !rule.Enabled)
            {
                continue;
            }

            courses.Add(rule.CourseIndex);
            hasShared |= rule.CompositionMode ==
                         BistroBuilderOrderLineCompositionMode
                             .SharedAllCustomers ||
                         rule.CompositionMode ==
                         BistroBuilderOrderLineCompositionMode.SharedGroups;
            hasIndividual |= rule.CompositionMode ==
                             BistroBuilderOrderLineCompositionMode
                                 .IndividualPerCustomer;
        }

        if (courses.Count >= 2)
        {
            result.Correct("El perfil contiene al menos dos pases reales.");
        }
        else
        {
            result.Error("El perfil necesita al menos dos pases para 367F.");
        }

        if (hasShared)
        {
            result.Correct("El perfil genera platos compartidos.");
        }
        else
        {
            result.Error("El perfil no genera platos compartidos.");
        }

        if (hasIndividual)
        {
            result.Correct("El perfil conserva platos individuales.");
        }
        else
        {
            result.Error("El perfil no conserva platos individuales.");
        }
    }

    private static void CheckComponent(
        UnityEngine.Object component,
        string name,
        BistroBuilderSharedCoursesValidationResult result
    )
    {
        if (component != null)
        {
            result.Correct(name + " localizado.");
        }
        else
        {
            result.Error(name + " ausente.");
        }
    }

    private static void CheckConfiguration(
        bool valid,
        string success,
        string error,
        BistroBuilderSharedCoursesValidationResult result
    )
    {
        if (valid)
        {
            result.Correct(success);
        }
        else
        {
            result.Error(string.IsNullOrWhiteSpace(error) ? success : error);
        }
    }

    private static void CheckSingleComponent<T>(
        GameObject gameSystems,
        string success,
        BistroBuilderSharedCoursesValidationResult result
    ) where T : Component
    {
        T[] components = gameSystems.GetComponents<T>();

        if (components.Length == 1)
        {
            result.Correct(success);
        }
        else
        {
            result.Error(
                "Se encontraron " + components.Length + " componentes " +
                typeof(T).Name + "."
            );
        }
    }
}
