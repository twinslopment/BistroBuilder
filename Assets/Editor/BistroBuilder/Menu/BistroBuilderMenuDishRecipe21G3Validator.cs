using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMenuDishRecipe21G3ValidationResult
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
        StringBuilder builder = new StringBuilder(6144);
        builder.AppendLine(
            "BISTRO BUILDER - 2.1G3 PERSISTENCIA DE PLATOS Y RECETAS"
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
/// Validador no destructivo de 2.1G3. Comprueba la única sección menu.state,
/// la cadena completa de migraciones, el cableado de catálogos y los contratos
/// de captura, resolución, conservación y rollback de la autoría runtime.
/// </summary>
public static class BistroBuilderMenuDishRecipe21G3Validator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 2.1G3 Dish Recipe Persistence";

    [MenuItem(MenuPath, false, 195)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMenuDishRecipe21G3ValidationResult result =
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

    public static BistroBuilderMenuDishRecipe21G3ValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderMenuDishRecipe21G3ValidationResult result =
            new BistroBuilderMenuDishRecipe21G3ValidationResult();
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            result.AddError("La escena activa no está cargada o guardada.");
            return result;
        }

        result.AddCorrect("La escena activa está cargada y guardada.");

        BistroBuilderMenuDishRecipe21G12ValidationResult prerequisite =
            BistroBuilderMenuDishRecipe21G12Validator.ValidateCurrentProject();

        if (prerequisite.ErrorCount == 0)
        {
            result.AddCorrect("2.1G1/2 permanece válido como base de autoría.");
        }
        else
        {
            result.AddError(
                "2.1G1/2 presenta regresiones tras integrar 2.1G3."
            );
        }

        List<BistroBuilderDishRecipePersistenceService> persistenceServices =
            Find<BistroBuilderDishRecipePersistenceService>(scene);
        List<BistroBuilderDishRecipeAuthoringService> authoringServices =
            Find<BistroBuilderDishRecipeAuthoringService>(scene);
        List<BistroBuilderMenuSaveSectionProvider> providers =
            Find<BistroBuilderMenuSaveSectionProvider>(scene);
        List<BistroBuilderSaveGameService> saveServices =
            Find<BistroBuilderSaveGameService>(scene);
        List<BistroBuilderActiveServiceSaveSectionProvider> activeServices =
            Find<BistroBuilderActiveServiceSaveSectionProvider>(scene);
        List<BistroBuilderMenuStateV1ToV2Migration> migrations12 =
            Find<BistroBuilderMenuStateV1ToV2Migration>(scene);
        List<BistroBuilderMenuStateV2ToV3Migration> migrations23 =
            Find<BistroBuilderMenuStateV2ToV3Migration>(scene);
        List<BistroBuilderMenuStateV3ToV4Migration> migrations34 =
            Find<BistroBuilderMenuStateV3ToV4Migration>(scene);

        ValidateUnique(
            persistenceServices.Count,
            "servicio de persistencia de platos y recetas",
            result
        );
        ValidateUnique(
            authoringServices.Count,
            "autoridad de autoría de platos y recetas",
            result
        );
        ValidateUnique(providers.Count, "proveedor menu.state", result);
        ValidateUnique(saveServices.Count, "servicio universal de guardado", result);
        ValidateUnique(
            activeServices.Count,
            "proveedor service.runtime",
            result
        );
        ValidateUnique(migrations12.Count, "migración menu.state v1 a v2", result);
        ValidateUnique(migrations23.Count, "migración menu.state v2 a v3", result);
        ValidateUnique(migrations34.Count, "migración menu.state v3 a v4", result);

        if (persistenceServices.Count != 1 || authoringServices.Count != 1 ||
            providers.Count != 1 || saveServices.Count != 1 ||
            activeServices.Count != 1 ||
            migrations12.Count != 1 || migrations23.Count != 1 ||
            migrations34.Count != 1)
        {
            return result;
        }

        BistroBuilderDishRecipePersistenceService persistence =
            persistenceServices[0];
        BistroBuilderDishRecipeAuthoringService authoring =
            authoringServices[0];
        BistroBuilderMenuSaveSectionProvider provider = providers[0];
        BistroBuilderSaveGameService saveService = saveServices[0];
        BistroBuilderActiveServiceSaveSectionProvider activeService =
            activeServices[0];

        if (provider.LoadOrder < activeService.LoadOrder &&
            provider.ApplyOrder < activeService.ApplyOrder)
        {
            result.AddCorrect(
                "menu.state restaura platos y recetas antes de las comandas activas."
            );
        }
        else
        {
            result.AddError(
                "service.runtime se aplica antes de que menu.state restaure " +
                "las definiciones creadas."
            );
        }

        if (provider.SectionId ==
                BistroBuilderMenuSaveSectionProvider.StableSectionId &&
            provider.SectionVersion ==
                BistroBuilderMenuSaveData.CurrentSchemaVersion &&
            provider.SectionVersion >= 4 &&
            provider.StateType == typeof(BistroBuilderMenuSaveData))
        {
            result.AddCorrect(
                "La versión actual de menu.state conserva la autoría introducida en v4."
            );
        }
        else
        {
            result.AddError("El contrato publicado de menu.state no conserva la autoría v4.");
        }

        if (ReferenceEquals(provider.DishRecipePersistenceService, persistence))
        {
            result.AddCorrect(
                "El proveedor menu.state usa la autoridad de persistencia 2.1G3."
            );
        }
        else
        {
            result.AddError(
                "menu.state no está enlazado al servicio de persistencia 2.1G3."
            );
        }

        if (ReferenceEquals(authoring.PersistenceService, persistence))
        {
            result.AddCorrect(
                "La autoría reserva las identidades de pares temporalmente no resueltos."
            );
        }
        else
        {
            result.AddError(
                "La autoría no consulta la reserva persistente de DishId y RecipeId."
            );
        }

        if (string.Equals(
                BistroBuilderDishRecipePersistenceService.RuntimeRevision,
                "MENU-2.1G3",
                StringComparison.Ordinal
            ))
        {
            result.AddCorrect("La revisión runtime corresponde a 2.1G3.");
        }
        else
        {
            result.AddError("La revisión runtime de persistencia es inválida.");
        }

        if (persistence.ValidateConfiguration(out string persistenceError))
        {
            result.AddCorrect(
                "La persistencia comparte catálogos efectivos de plato, receta y categoría."
            );
        }
        else
        {
            result.AddError(persistenceError);
        }

        if (provider.ValidateConfiguration(out string providerError))
        {
            result.AddCorrect("El proveedor menu.state actual está operativo.");
        }
        else
        {
            result.AddError(providerError);
        }

        ValidateCatalogs(persistence, result);
        ValidateMigrationChain(
            migrations12[0],
            migrations23[0],
            migrations34[0],
            result
        );

        saveService.RefreshExtensions();
        if (saveService.ValidateConfiguration(out string saveError))
        {
            result.AddCorrect(
                "El guardado universal registra la cadena histórica hasta la versión actual."
            );
        }
        else
        {
            result.AddError("Guardado universal inválido: " + saveError);
        }

        ValidatePublishedContracts(result);
        ValidateDtoContract(result);
        ValidateSingleSection(scene, result);

        if (persistence.UnresolvedPairCount == 0)
        {
            result.AddCorrect(
                "La escena guardada no contiene autoría no resuelta residual."
            );
        }
        else
        {
            result.AddWarning(
                "Hay " + persistence.UnresolvedPairCount +
                " par(es) de autoría no resueltos en memoria."
            );
        }

        return result;
    }

    private static void ValidateCatalogs(
        BistroBuilderDishRecipePersistenceService persistence,
        BistroBuilderMenuDishRecipe21G3ValidationResult result
    )
    {
        BistroBuilderDishCatalogService dishes =
            persistence.DishCatalogService;
        BistroBuilderRecipeCatalogService recipes =
            persistence.RecipeCatalogService;
        BistroBuilderDishCategoryCatalogService categories =
            persistence.CategoryCatalogService;

        string error = string.Empty;
        bool valid = dishes != null && recipes != null && categories != null &&
            dishes.ValidateConfiguration(out error) &&
            recipes.ValidateConfiguration(out error) &&
            categories.ValidateConfiguration(out error);

        if (valid)
        {
            result.AddCorrect(
                "Los catálogos canónicos necesarios para resolver autoría son válidos."
            );
        }
        else
        {
            result.AddError(
                string.IsNullOrWhiteSpace(error)
                    ? "Falta un catálogo necesario para resolver autoría."
                    : error
            );
        }

        if (dishes != null && recipes != null &&
            dishes.RuntimeDefinitionCount == recipes.RuntimeRecipeCount &&
            dishes.SuppressedCanonicalDefinitionCount ==
                persistence.UnresolvedCanonicalOverrideCount)
        {
            result.AddCorrect(
                "Las capas runtime y las sombras canónicas necesarias permanecen emparejadas."
            );
        }
        else
        {
            result.AddError(
                "Las capas runtime o las sombras canónicas están descompensadas."
            );
        }
    }

    private static void ValidateMigrationChain(
        BistroBuilderMenuStateV1ToV2Migration migration12,
        BistroBuilderMenuStateV2ToV3Migration migration23,
        BistroBuilderMenuStateV3ToV4Migration migration34,
        BistroBuilderMenuDishRecipe21G3ValidationResult result
    )
    {
        bool valid = migration12.FromVersion == 1 &&
            migration12.ToVersion == 2 &&
            migration23.FromVersion == 2 &&
            migration23.ToVersion == 3 &&
            migration34.FromVersion == 3 &&
            migration34.ToVersion == 4 &&
            migration12.SectionId == providerSectionId &&
            migration23.SectionId == providerSectionId &&
            migration34.SectionId == providerSectionId &&
            migration12.ToSerializerId == jsonSerializerId &&
            migration23.ToSerializerId == jsonSerializerId &&
            migration34.ToSerializerId == jsonSerializerId;

        if (valid)
        {
            result.AddCorrect(
                "Las migraciones de menu.state son consecutivas y usan el serializador canónico."
            );
        }
        else
        {
            result.AddError("La cadena de migración menu.state no es consecutiva.");
        }
    }

    private static readonly string providerSectionId =
        BistroBuilderMenuSaveSectionProvider.StableSectionId;
    private static readonly string jsonSerializerId =
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    private static void ValidatePublishedContracts(
        BistroBuilderMenuDishRecipe21G3ValidationResult result
    )
    {
        Type persistence = typeof(BistroBuilderDishRecipePersistenceService);
        bool complete = persistence.GetMethod("TryCapture") != null &&
            persistence.GetMethod("TryValidatePersistentCollections") != null &&
            persistence.GetMethod("IsDishIdReserved") != null &&
            persistence.GetMethod("IsRecipeIdReserved") != null &&
            persistence.GetMethod("TryApply") != null &&
            persistence.GetMethod("Rollback") != null &&
            persistence.GetMethod("CompleteApply") != null &&
            typeof(BistroBuilderDishRecipeSaveDataUtility)
                .GetMethod("TryValidatePairStructure") != null &&
            typeof(BistroBuilderDishRecipeSaveDataUtility)
                .GetMethod("TryValidatePairCollections") != null &&
            typeof(BistroBuilderDishRecipeSaveDataUtility)
                .GetMethod("TryCreateRuntimePair") != null &&
            typeof(BistroBuilderDishRecipeSaveDataUtility)
                .GetMethod("Clone") != null;

        if (complete)
        {
            result.AddCorrect(
                "La persistencia publica captura, resolución, rollback y conservación."
            );
        }
        else
        {
            result.AddError("Faltan contratos públicos de persistencia 2.1G3.");
        }
    }

    private static void ValidateDtoContract(
        BistroBuilderMenuDishRecipe21G3ValidationResult result
    )
    {
        FieldInfo authored = typeof(BistroBuilderMenuSaveData).GetField(
            "authoredDishRecipes"
        );
        FieldInfo unresolved = typeof(BistroBuilderMenuSaveData).GetField(
            "unresolvedAuthoredDishRecipes"
        );
        FieldInfo dish = typeof(BistroBuilderDishRecipeSaveData).GetField("dish");
        FieldInfo recipe = typeof(BistroBuilderDishRecipeSaveData).GetField(
            "recipe"
        );

        if (authored != null && unresolved != null && dish != null &&
            recipe != null &&
            authored.FieldType == typeof(List<BistroBuilderDishRecipeSaveData>) &&
            unresolved.FieldType ==
                typeof(List<BistroBuilderDishRecipeSaveData>))
        {
            result.AddCorrect(
                "El DTO v4 conserva pares atómicos resueltos y no resueltos."
            );
        }
        else
        {
            result.AddError("El DTO persistente v4 está incompleto.");
        }
    }

    private static void ValidateSingleSection(
        Scene scene,
        BistroBuilderMenuDishRecipe21G3ValidationResult result
    )
    {
        List<MonoBehaviour> behaviours = Find<MonoBehaviour>(scene);
        int menuProviders = 0;

        for (int index = 0; index < behaviours.Count; index++)
        {
            if (behaviours[index] is IBistroBuilderSaveSectionProvider provider &&
                string.Equals(
                    provider.SectionId,
                    BistroBuilderMenuSaveSectionProvider.StableSectionId,
                    StringComparison.Ordinal
                ))
            {
                menuProviders++;
            }
        }

        if (menuProviders == 1)
        {
            result.AddCorrect(
                "No existe una sección paralela para platos o recetas creados."
            );
        }
        else
        {
            result.AddError(
                "La sección menu.state está ausente o duplicada: " +
                menuProviders + "."
            );
        }
    }

    private static List<T> Find<T>(Scene scene) where T : Component
    {
        return BistroBuilderMenuEditor21EInstaller.FindSceneComponents<T>(scene);
    }

    private static void ValidateUnique(
        int count,
        string label,
        BistroBuilderMenuDishRecipe21G3ValidationResult result
    )
    {
        if (count == 1)
        {
            result.AddCorrect("Existe un único " + label + ".");
        }
        else
        {
            result.AddError(
                "Debe existir un único " + label + "; hay " + count + "."
            );
        }
    }
}
