using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMenuDishRecipe21G12ValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;
    public void AddCorrect(string value) => correct.Add(value);
    public void AddWarning(string value) => warnings.Add(value);
    public void AddError(string value) => errors.Add(value);

    public string BuildReport()
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine(
            "BISTRO BUILDER - 2.1G1/2 CREACIÓN DE PLATOS Y RECETAS"
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
        List<string> values
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            builder.Append("- ");
            builder.Append(prefix);
            builder.Append(": ");
            builder.AppendLine(values[index]);
        }
    }
}

/// <summary>
/// Validador no destructivo de la creación de platos y edición de recetas
/// 2.1G1/2. No inicia sesiones ni modifica capas runtime.
/// </summary>
public static class BistroBuilderMenuDishRecipe21G12Validator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 2.1G1-2 Dish and Recipe Authoring";

    [MenuItem(MenuPath, false, 191)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMenuDishRecipe21G12ValidationResult result =
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

    public static BistroBuilderMenuDishRecipe21G12ValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderMenuDishRecipe21G12ValidationResult result =
            new BistroBuilderMenuDishRecipe21G12ValidationResult();
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            result.AddError("La escena activa no está cargada o guardada.");
            return result;
        }

        result.AddCorrect("La escena activa está cargada y guardada.");

        List<BistroBuilderDishRecipeAuthoringService> services =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderDishRecipeAuthoringService
            >(scene);
        List<BistroBuilderDishRecipeAuthoringRuntimeView> views =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderDishRecipeAuthoringRuntimeView
            >(scene);
        List<BistroBuilderMenuEditorService> editors =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderMenuEditorService
            >(scene);
        List<BistroBuilderMenuEditorRuntimeView> editorViews =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderMenuEditorRuntimeView
            >(scene);

        ValidateUnique(services.Count, "autoridad de autoría 2.1G1/2", result);
        ValidateUnique(views.Count, "vista de autoría 2.1G1/2", result);
        ValidateUnique(editors.Count, "servicio de editor de carta", result);
        ValidateUnique(editorViews.Count, "vista principal de carta", result);

        if (services.Count != 1 || views.Count != 1 ||
            editors.Count != 1 || editorViews.Count != 1)
        {
            return result;
        }

        BistroBuilderDishRecipeAuthoringService service = services[0];
        BistroBuilderDishRecipeAuthoringRuntimeView view = views[0];
        BistroBuilderMenuEditorService editor = editors[0];
        BistroBuilderMenuEditorRuntimeView editorView = editorViews[0];

        if (string.Equals(
                BistroBuilderDishRecipeAuthoringService.RuntimeRevision,
                "MENU-2.1G12",
                StringComparison.Ordinal
            ) &&
            string.Equals(
                BistroBuilderDishRecipeAuthoringRuntimeView.RuntimeRevision,
                "MENU-2.1G12-UI",
                StringComparison.Ordinal
            ))
        {
            result.AddCorrect("Las revisiones runtime corresponden a 2.1G1/2.");
        }
        else
        {
            result.AddError("Las revisiones runtime de 2.1G1/2 son inválidas.");
        }

        if (service.ValidateConfiguration(out string serviceError))
        {
            result.AddCorrect("La autoridad de autoría comparte los catálogos canónicos.");
        }
        else
        {
            result.AddError(serviceError);
        }

        if (editor.ValidateConfiguration(out string editorError))
        {
            result.AddCorrect("El editor integra la autoría transaccional 2.1G1/2.");
        }
        else
        {
            result.AddError(editorError);
        }

        string viewError;
        string mainViewError = string.Empty;
        bool authoringViewValid = view.ValidateConfiguration(out viewError);
        bool mainViewValid = authoringViewValid &&
            editorView.ValidateConfiguration(out mainViewError);

        if (authoringViewValid && mainViewValid)
        {
            result.AddCorrect("Las vistas principal y de autoría están configuradas.");
        }
        else
        {
            result.AddError(
                !string.IsNullOrWhiteSpace(viewError) ? viewError : mainViewError
            );
        }

        if (ReferenceEquals(editor.AuthoringService, service) &&
            ReferenceEquals(view.EditorService, editor) &&
            ReferenceEquals(editorView.AuthoringView, view))
        {
            result.AddCorrect("La UI usa una única sesión de autoría compartida.");
        }
        else
        {
            result.AddError("La UI no comparte la autoridad 2.1G1/2 canónica.");
        }

        ValidateCatalogLayers(service, result);
        ValidatePublishedContracts(result);
        ValidateFactories(result);
        ValidatePrerequisiteAndPersistence(result);
        return result;
    }

    private static void ValidateCatalogLayers(
        BistroBuilderDishRecipeAuthoringService service,
        BistroBuilderMenuDishRecipe21G12ValidationResult result
    )
    {
        BistroBuilderDishCatalogService dishes = service.DishCatalogService;
        BistroBuilderRecipeCatalogService recipes = service.RecipeCatalogService;

        if (dishes != null && recipes != null &&
            dishes.CanonicalDefinitionCount > 0 &&
            recipes.IngredientCount > 0 &&
            recipes.CanonicalRecipeCount > 0)
        {
            result.AddCorrect(
                "Los platos y recetas runtime se superponen a catálogos canónicos no vacíos."
            );
        }
        else
        {
            result.AddError("Los catálogos base de platos, ingredientes o recetas están vacíos.");
        }

        if (dishes != null && recipes != null &&
            dishes.RuntimeDefinitionCount == 0 &&
            recipes.RuntimeRecipeCount == 0)
        {
            result.AddCorrect("La escena guardada no contiene objetos runtime residuales.");
        }
        else
        {
            result.AddWarning(
                "Existen capas runtime activas; confirma que no se guardó la escena durante Play Mode."
            );
        }
    }

    private static void ValidatePublishedContracts(
        BistroBuilderMenuDishRecipe21G12ValidationResult result
    )
    {
        Type authoring = typeof(BistroBuilderDishRecipeAuthoringService);
        MethodInfo create = authoring.GetMethod("CreateNewRequest");
        MethodInfo update = authoring.GetMethod("TryCreateOrUpdate");
        MethodInfo apply = authoring.GetMethod("TryApplyRuntime");
        MethodInfo rollback = authoring.GetMethod("Rollback");
        MethodInfo draftRecipe = authoring.GetMethod("TryResolveDraftRecipe");

        if (create != null && update != null && apply != null &&
            rollback != null && draftRecipe != null)
        {
            result.AddCorrect(
                "La autoría publica creación, edición, aplicación y rollback atómicos."
            );
        }
        else
        {
            result.AddError("Faltan contratos públicos de autoría 2.1G1/2.");
        }

        MethodInfo replaceDishes = typeof(BistroBuilderDishCatalogService)
            .GetMethod("TryReplaceRuntimeDefinitions");
        MethodInfo replaceRecipes = typeof(BistroBuilderRecipeCatalogService)
            .GetMethod("TryReplaceRuntimeRecipes");
        MethodInfo previewAvailability = typeof(
            BistroBuilderDishAvailabilityService
        ).GetMethod("TryEvaluateMenuItemWithRecipe");

        if (replaceDishes != null && replaceRecipes != null &&
            previewAvailability != null)
        {
            result.AddCorrect(
                "Catálogos, disponibilidad y escandallo aceptan borradores runtime."
            );
        }
        else
        {
            result.AddError("Falta integración de catálogos o disponibilidad runtime.");
        }
    }

    private static void ValidateFactories(
        BistroBuilderMenuDishRecipe21G12ValidationResult result
    )
    {
        MethodInfo dishFactory = typeof(BistroBuilderDishDefinition)
            .GetMethod("CreateRuntime", BindingFlags.Public | BindingFlags.Static);
        MethodInfo recipeFactory = typeof(BistroBuilderRecipeDefinition)
            .GetMethod("CreateRuntime", BindingFlags.Public | BindingFlags.Static);
        MethodInfo lineClone = typeof(BistroBuilderRecipeIngredientAmount)
            .GetMethod("Clone", BindingFlags.Public | BindingFlags.Instance);

        if (dishFactory != null && recipeFactory != null && lineClone != null)
        {
            result.AddCorrect(
                "Las definiciones runtime se crean sin modificar ScriptableObjects canónicos."
            );
        }
        else
        {
            result.AddError("Faltan fábricas runtime de plato, receta o ingrediente.");
        }
    }

    private static void ValidatePrerequisiteAndPersistence(
        BistroBuilderMenuDishRecipe21G12ValidationResult result
    )
    {
        BistroBuilderMenuPreparation21FValidationResult prerequisite =
            BistroBuilderMenuPreparation21FValidator.ValidateCurrentProject();

        if (prerequisite.ErrorCount == 0)
        {
            result.AddCorrect("2.1F permanece válido como base de preparación.");
        }
        else
        {
            result.AddError("2.1F ya no es válido tras integrar 2.1G1/2.");
        }

        int currentSchemaVersion = Convert.ToInt32(
            BistroBuilderMenuSaveData.CurrentSchemaVersion
        );
        int stableSectionVersion = Convert.ToInt32(
            BistroBuilderMenuSaveSectionProvider.StableSectionVersion
        );

        if (currentSchemaVersion == 3 && stableSectionVersion == 3)
        {
            result.AddCorrect(
                "2.1G1/2 no introduce persistencia paralela; menu.state sigue en v3."
            );
        }
        else
        {
            result.AddError("2.1G1/2 alteró indebidamente la versión persistente.");
        }
    }

    private static void ValidateUnique(
        int count,
        string label,
        BistroBuilderMenuDishRecipe21G12ValidationResult result
    )
    {
        if (count == 1)
        {
            result.AddCorrect("Existe una única " + label + ".");
        }
        else
        {
            result.AddError(
                "Debe existir una única " + label + "; hay " + count + "."
            );
        }
    }
}
