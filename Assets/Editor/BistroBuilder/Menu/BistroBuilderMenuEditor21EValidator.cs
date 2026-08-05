using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BistroBuilderMenuEditor21EValidationResult
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
        StringBuilder builder = new StringBuilder(6144);
        builder.AppendLine(
            "BISTRO BUILDER - 2.1E EDITOR JUGABLE DE CARTA"
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
/// Validador no destructivo de la arquitectura runtime 2.1E.
/// </summary>
public static class BistroBuilderMenuEditor21EValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 2.1E Runtime Menu Editor";

    [MenuItem(MenuPath, false, 171)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMenuEditor21EValidationResult result =
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

    public static BistroBuilderMenuEditor21EValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderMenuEditor21EValidationResult result =
            new BistroBuilderMenuEditor21EValidationResult();

        BistroBuilderSignatureDish21DValidationResult prerequisite =
            BistroBuilderSignatureDish21DValidator.ValidateCurrentProject();

        if (prerequisite.ErrorCount > 0)
        {
            result.AddError("2.1D no está validado.");
        }
        else
        {
            result.AddCorrect(
                "2.1A, 2.1B, 2.1C y 2.1D siguen válidos como base."
            );

            if (prerequisite.WarningCount > 0)
            {
                result.AddWarning(
                    "2.1D conserva " + prerequisite.WarningCount +
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

        List<BistroBuilderMenuEditorService> services =
            BistroBuilderMenuEditor21EInstaller
                .FindSceneComponents<BistroBuilderMenuEditorService>(scene);
        List<BistroBuilderMenuEditorRuntimeView> views =
            BistroBuilderMenuEditor21EInstaller
                .FindSceneComponents<BistroBuilderMenuEditorRuntimeView>(
                    scene
                );

        if (services.Count != 1)
        {
            result.AddError(
                "Debe existir un único servicio 2.1E; hay " +
                services.Count + "."
            );
            return result;
        }

        if (views.Count != 1)
        {
            result.AddError(
                "Debe existir una única vista runtime 2.1E; hay " +
                views.Count + "."
            );
            return result;
        }

        BistroBuilderMenuEditorService service = services[0];
        BistroBuilderMenuEditorRuntimeView view = views[0];
        result.AddCorrect("Existe una única autoridad de editor 2.1E.");
        result.AddCorrect("Existe una única vista runtime de carta.");

        if (string.Equals(
                BistroBuilderMenuEditorService.RuntimeRevision,
                "MENU-2.1E",
                StringComparison.Ordinal
            ))
        {
            result.AddCorrect("La revisión runtime del editor es 2.1E.");
        }
        else
        {
            result.AddError("La revisión runtime del editor no es 2.1E.");
        }

        if (string.Equals(
                BistroBuilderMenuEditorRuntimeView.RuntimeRevision,
                "MENU-2.1E-UI",
                StringComparison.Ordinal
            ))
        {
            result.AddCorrect("La revisión runtime de la vista es 2.1E.");
        }
        else
        {
            result.AddError("La revisión runtime de la vista no es 2.1E.");
        }

        if (service.ValidateConfiguration(out string serviceError))
        {
            result.AddCorrect(
                "El servicio de editor comparte las autoridades canónicas."
            );
        }
        else
        {
            result.AddError(serviceError);
        }

        if (view.ValidateConfiguration(out string viewError))
        {
            result.AddCorrect("La vista runtime es estructuralmente válida.");
        }
        else
        {
            result.AddError(viewError);
        }

        if (ReferenceEquals(view.EditorService, service))
        {
            result.AddCorrect("La vista usa el único servicio 2.1E.");
        }
        else
        {
            result.AddError("La vista no usa la autoridad 2.1E canónica.");
        }

        ValidateUiHierarchy(scene, view, result);
        ValidateInputGate(scene, view, result);
        ValidatePublishedContracts(result);
        ValidateNoParallelPersistence(scene, result);
        ValidateCatalogs(service, result);
        return result;
    }

    private static void ValidateUiHierarchy(
        Scene scene,
        BistroBuilderMenuEditorRuntimeView view,
        BistroBuilderMenuEditor21EValidationResult result
    )
    {
        Transform root = view.transform;
        Canvas canvas = view.GetComponentInParent<Canvas>();
        bool correctName = string.Equals(
            root.name,
            BistroBuilderMenuEditor21EInstaller.UiRootName,
            StringComparison.Ordinal
        );

        if (correctName)
        {
            result.AddCorrect("La raíz UI 2.1E tiene identidad estable.");
        }
        else
        {
            result.AddError("La raíz UI 2.1E no tiene el nombre canónico.");
        }

        if (canvas != null &&
            string.Equals(canvas.name, "Canvas", StringComparison.Ordinal) &&
            canvas.transform.parent != null &&
            string.Equals(
                canvas.transform.parent.name,
                "MainHUD",
                StringComparison.Ordinal
            ))
        {
            result.AddCorrect("La vista está integrada en el HUD canónico.");
        }
        else
        {
            result.AddError("La vista no está bajo MainHUD/Canvas.");
        }

        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() != null)
        {
            result.AddCorrect("El Canvas dispone de GraphicRaycaster.");
        }
        else
        {
            result.AddError("El Canvas del editor no recibe eventos uGUI.");
        }

        RectTransform rect = root as RectTransform;

        if (rect != null && rect.anchorMin == Vector2.zero &&
            rect.anchorMax == Vector2.one && rect.offsetMin == Vector2.zero &&
            rect.offsetMax == Vector2.zero)
        {
            result.AddCorrect("La raíz UI está normalizada y ocupa el Canvas.");
        }
        else
        {
            result.AddError("La raíz UI 2.1E no está normalizada.");
        }

        int rootCount = 0;
        List<BistroBuilderMenuEditorRuntimeView> allViews =
            BistroBuilderMenuEditor21EInstaller
                .FindSceneComponents<BistroBuilderMenuEditorRuntimeView>(
                    scene
                );

        for (int index = 0; index < allViews.Count; index++)
        {
            if (string.Equals(
                    allViews[index].name,
                    BistroBuilderMenuEditor21EInstaller.UiRootName,
                    StringComparison.Ordinal
                ))
            {
                rootCount++;
            }
        }

        if (rootCount == 1)
        {
            result.AddCorrect("No existen raíces UI 2.1E duplicadas.");
        }
        else
        {
            result.AddError(
                "Se detectaron " + rootCount + " raíces UI 2.1E."
            );
        }

        List<EventSystem> eventSystems =
            BistroBuilderMenuEditor21EInstaller
                .FindSceneComponents<EventSystem>(scene);

        if (eventSystems.Count == 1)
        {
            result.AddCorrect("Existe un único EventSystem en la escena.");
        }
        else
        {
            result.AddError(
                "La escena debe contener un único EventSystem; hay " +
                eventSystems.Count + "."
            );
        }
    }

    private static void ValidateInputGate(
        Scene scene,
        BistroBuilderMenuEditorRuntimeView view,
        BistroBuilderMenuEditor21EValidationResult result
    )
    {
        List<BistroBuilderProfessionalCameraController> cameras =
            BistroBuilderMenuEditor21EInstaller
                .FindSceneComponents<
                    BistroBuilderProfessionalCameraController
                >(scene);

        if (cameras.Count == 1 &&
            ReferenceEquals(view.CameraController, cameras[0]))
        {
            result.AddCorrect(
                "El editor bloquea y restaura la cámara profesional canónica."
            );
        }
        else
        {
            result.AddError(
                "La vista no comparte la cámara profesional canónica."
            );
        }

        List<RestaurantEditInteractionController> editControllers =
            BistroBuilderMenuEditor21EInstaller
                .FindSceneComponents<RestaurantEditInteractionController>(
                    scene
                );

        if (editControllers.Count == 1 &&
            ReferenceEquals(
                view.EditInteractionController,
                editControllers[0]
            ))
        {
            result.AddCorrect(
                "El editor bloquea y restaura la interacción de edición."
            );
        }
        else if (editControllers.Count == 0 &&
                 view.EditInteractionController == null)
        {
            result.AddWarning(
                "No existe interacción de edición que bloquear en la escena."
            );
        }
        else
        {
            result.AddError(
                "La vista no comparte la interacción de edición canónica."
            );
        }
    }

    private static void ValidatePublishedContracts(
        BistroBuilderMenuEditor21EValidationResult result
    )
    {
        MethodInfo arbitraryAvailability = typeof(
            BistroBuilderDishAvailabilityService
        ).GetMethod(
            "TryEvaluateMenuItem",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[]
            {
                typeof(BistroBuilderMenuItemRuntimeState),
                typeof(BistroBuilderMealServiceAvailability),
                typeof(BistroBuilderDishAvailabilitySnapshot).MakeByRefType(),
                typeof(string).MakeByRefType()
            },
            null
        );

        if (arbitraryAvailability != null)
        {
            result.AddCorrect(
                "La previsualización usa la disponibilidad canónica 368EF."
            );
        }
        else
        {
            result.AddError(
                "Falta el contrato de disponibilidad para borradores."
            );
        }

        MethodInfo economics = typeof(BistroBuilderRecipeEconomics).GetMethod(
            "TryBuildSnapshot",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[]
            {
                typeof(BistroBuilderDishDefinition),
                typeof(BistroBuilderRecipeDefinition),
                typeof(int),
                typeof(BistroBuilderRecipeEconomicsSnapshot).MakeByRefType(),
                typeof(string).MakeByRefType()
            },
            null
        );

        if (economics != null)
        {
            result.AddCorrect(
                "El escandallo admite el precio exacto del borrador."
            );
        }
        else
        {
            result.AddError(
                "Falta el escandallo para precios runtime."
            );
        }

        MethodInfo defaults = typeof(
            BistroBuilderMenuEditSessionService
        ).GetMethod("TryRestoreDishDefaults", new[] { typeof(string) });
        MethodInfo ordering = typeof(
            BistroBuilderMenuEditSessionService
        ).GetMethod(
            "TryMoveDishWithinCategory",
            new[] { typeof(string), typeof(int) }
        );

        if (defaults != null && ordering != null)
        {
            result.AddCorrect(
                "La sesión transaccional publica restauración y orden por categoría."
            );
        }
        else
        {
            result.AddError(
                "La sesión 2.1B no expone todos los contratos de 2.1E."
            );
        }
    }

    private static void ValidateNoParallelPersistence(
        Scene scene,
        BistroBuilderMenuEditor21EValidationResult result
    )
    {
        List<BistroBuilderMenuSaveSectionProvider> providers =
            BistroBuilderMenuEditor21EInstaller
                .FindSceneComponents<BistroBuilderMenuSaveSectionProvider>(
                    scene
                );

        if (providers.Count == 1)
        {
            result.AddCorrect(
                "2.1E conserva menu.state como única persistencia de carta."
            );
        }
        else
        {
            result.AddError(
                "Debe existir un único proveedor menu.state; hay " +
                providers.Count + "."
            );
        }
    }

    private static void ValidateCatalogs(
        BistroBuilderMenuEditorService service,
        BistroBuilderMenuEditor21EValidationResult result
    )
    {
        if (service.CatalogService != null &&
            service.CatalogService.DefinitionCount > 0)
        {
            result.AddCorrect(
                "El editor tiene platos canónicos para presentar."
            );
        }
        else
        {
            result.AddError("El catálogo canónico de platos está vacío.");
        }

        if (service.CategoryCatalogService != null &&
            service.CategoryCatalogService.CategoryCount > 0)
        {
            result.AddCorrect(
                "El editor tiene categorías canónicas para filtrar."
            );
        }
        else
        {
            result.AddError("El catálogo de categorías está vacío.");
        }
    }
}
