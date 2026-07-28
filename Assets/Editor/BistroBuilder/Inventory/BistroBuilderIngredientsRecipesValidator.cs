using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado acumulado de la validación 368A.
/// </summary>
public sealed class BistroBuilderIngredientsRecipesValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;

    public void AddCorrect(string message)
    {
        correct.Add(message);
    }

    public void AddWarning(string message)
    {
        warnings.Add(message);
    }

    public void AddError(string message)
    {
        errors.Add(message);
    }

    public string BuildReport()
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine(
            "BISTRO BUILDER - INGREDIENTES, RECETAS Y SILLAS 368A"
        );
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);
        AppendGroup(builder, "OK", correct);
        AppendGroup(builder, "ADVERTENCIA", warnings);
        AppendGroup(builder, "ERROR", errors);
        return builder.ToString().TrimEnd();
    }

    private static void AppendGroup(
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
/// Validador no destructivo de 368A.
/// </summary>
public static class BistroBuilderIngredientsRecipesValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "Validate 368A Ingredients, Recipes & Visual Chairs";

    [MenuItem(MenuPath, false, 310)]
    private static void ValidateFromMenu()
    {
        BistroBuilderIngredientsRecipesValidationResult result =
            ValidateCurrentProject();

        if (result.ErrorCount > 0)
        {
            Debug.LogError(result.BuildReport());
        }
        else
        {
            Debug.Log(result.BuildReport());
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderIngredientsRecipesValidationResult
        ValidateCurrentProject()
    {
        var result =
            new BistroBuilderIngredientsRecipesValidationResult();
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.AddError("No existe una escena activa válida.");
            return result;
        }

        GameObject gameSystems =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.AddError("No se encontró GameSystems.");
            return result;
        }

        result.AddCorrect("GameSystems localizado.");

        BistroBuilderIngredientCatalog ingredientCatalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientCatalog>(
                BistroBuilderIngredientsRecipesEditorUtility
                    .IngredientCatalogPath
            );
        BistroBuilderRecipeCatalog recipeCatalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderRecipeCatalog>(
                BistroBuilderIngredientsRecipesEditorUtility
                    .RecipeCatalogPath
            );
        BistroBuilderDishCatalog dishCatalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderDishCatalog>(
                BistroBuilderIngredientsRecipesEditorUtility.DishCatalogPath
            );

        ValidateIngredientCatalog(ingredientCatalog, result);
        ValidateRecipeCatalog(recipeCatalog, result);
        ValidateDishRecipeCoverage(
            dishCatalog,
            recipeCatalog,
            result
        );
        ValidateRuntimeService(
            gameSystems,
            ingredientCatalog,
            recipeCatalog,
            result
        );
        ValidateVisualChairs(scene, result);

        return result;
    }

    private static void ValidateIngredientCatalog(
        BistroBuilderIngredientCatalog catalog,
        BistroBuilderIngredientsRecipesValidationResult result
    )
    {
        if (catalog == null)
        {
            result.AddError(
                "No existe el catálogo canónico de ingredientes."
            );
            return;
        }

        if (!catalog.TryRebuildIndex(out string error))
        {
            result.AddError(error);
            return;
        }

        if (catalog.DefinitionCount < 22)
        {
            result.AddError(
                "El catálogo contiene " + catalog.DefinitionCount +
                " ingredientes; 368A necesita al menos los 22 iniciales."
            );
            return;
        }

        result.AddCorrect(
            "Catálogo canónico válido con " +
            catalog.DefinitionCount + " ingrediente(s)."
        );

        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderIngredientDefinition"
        );
        int validCount = 0;

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderIngredientDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderIngredientDefinition
                >(path);

            if (definition == null)
            {
                continue;
            }

            if (!definition.TryValidate(out error))
            {
                result.AddError(path + ": " + error);
                continue;
            }

            if (!catalog.Contains(definition.IngredientId))
            {
                result.AddError(
                    definition.IngredientId +
                    " no está incluido en el catálogo oficial."
                );
                continue;
            }

            validCount++;
        }

        if (validCount == guids.Length)
        {
            result.AddCorrect(
                "Todas las definiciones de ingrediente son válidas y " +
                "están catalogadas."
            );
        }
    }

    private static void ValidateRecipeCatalog(
        BistroBuilderRecipeCatalog catalog,
        BistroBuilderIngredientsRecipesValidationResult result
    )
    {
        if (catalog == null)
        {
            result.AddError("No existe el catálogo canónico de recetas.");
            return;
        }

        if (!catalog.TryRebuildIndex(out string error))
        {
            result.AddError(error);
            return;
        }

        if (catalog.DefinitionCount < 8)
        {
            result.AddError(
                "El catálogo contiene " + catalog.DefinitionCount +
                " recetas; 368A necesita al menos las 8 iniciales."
            );
            return;
        }

        result.AddCorrect(
            "Catálogo canónico válido con " +
            catalog.DefinitionCount + " receta(s)."
        );

        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderRecipeDefinition"
        );
        int validCount = 0;

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderRecipeDefinition recipe =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderRecipeDefinition
                >(path);

            if (recipe == null)
            {
                continue;
            }

            if (!recipe.TryValidate(out error))
            {
                result.AddError(path + ": " + error);
                continue;
            }

            if (!catalog.TryGetByRecipeId(
                    recipe.RecipeId,
                    out BistroBuilderRecipeDefinition catalogued
                ) ||
                catalogued != recipe)
            {
                result.AddError(
                    recipe.RecipeId +
                    " no está incluido correctamente en el catálogo."
                );
                continue;
            }

            if (!BistroBuilderRecipeEconomics.TryBuildSnapshot(
                    recipe.Dish,
                    recipe,
                    out BistroBuilderRecipeEconomicsSnapshot economics,
                    out error
                ))
            {
                result.AddError(recipe.RecipeId + ": " + error);
                continue;
            }

            if (economics.GrossMarginCents < 0)
            {
                result.AddWarning(
                    recipe.Dish.DisplayName +
                    " tiene un coste superior al precio base."
                );
            }

            validCount++;
        }

        if (validCount == guids.Length)
        {
            result.AddCorrect(
                "Todas las recetas calculan coste y margen sin errores."
            );
        }
    }

    private static void ValidateDishRecipeCoverage(
        BistroBuilderDishCatalog dishCatalog,
        BistroBuilderRecipeCatalog recipeCatalog,
        BistroBuilderIngredientsRecipesValidationResult result
    )
    {
        if (dishCatalog == null)
        {
            result.AddError("No existe el catálogo canónico de platos.");
            return;
        }

        if (!dishCatalog.TryRebuildIndex(out string error))
        {
            result.AddError(error);
            return;
        }

        if (recipeCatalog == null)
        {
            return;
        }

        int covered = 0;

        for (int index = 0;
             index < dishCatalog.Definitions.Count;
             index++)
        {
            BistroBuilderDishDefinition dish =
                dishCatalog.Definitions[index];

            if (dish == null || string.IsNullOrWhiteSpace(dish.RecipeId))
            {
                result.AddError(
                    "Existe un plato canónico sin RecipeId."
                );
                continue;
            }

            if (!recipeCatalog.TryGetByRecipeId(
                    dish.RecipeId,
                    out BistroBuilderRecipeDefinition recipe
                ) ||
                recipe == null ||
                recipe.Dish != dish)
            {
                result.AddError(
                    dish.DishId +
                    " no tiene una receta canónica coherente."
                );
                continue;
            }

            covered++;
        }

        if (covered == dishCatalog.DefinitionCount)
        {
            result.AddCorrect(
                "Los " + covered +
                " plato(s) canónico(s) tienen receta enlazada."
            );
        }
    }

    private static void ValidateRuntimeService(
        GameObject gameSystems,
        BistroBuilderIngredientCatalog ingredientCatalog,
        BistroBuilderRecipeCatalog recipeCatalog,
        BistroBuilderIngredientsRecipesValidationResult result
    )
    {
        BistroBuilderRecipeCatalogService service =
            gameSystems.GetComponent<BistroBuilderRecipeCatalogService>();

        if (service == null)
        {
            result.AddError(
                "BistroBuilderRecipeCatalogService no está instalado."
            );
            return;
        }

        if (service.IngredientCatalog != ingredientCatalog ||
            service.RecipeCatalog != recipeCatalog)
        {
            result.AddError(
                "El servicio runtime no referencia los catálogos oficiales."
            );
            return;
        }

        if (!service.ValidateConfiguration(out string error))
        {
            result.AddError(error);
            return;
        }

        result.AddCorrect(
            "Servicio runtime preparado para ingredientes, recetas y " +
            "escandallos."
        );
    }

    private static void ValidateVisualChairs(
        Scene scene,
        BistroBuilderIngredientsRecipesValidationResult result
    )
    {
        RestaurantTable[] tables =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindSceneObjects<RestaurantTable>(scene);
        BistroBuilder368AInstalledChair[] markers =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindSceneObjects<BistroBuilder368AInstalledChair>(scene);

        if (tables.Length < 4)
        {
            result.AddError(
                "La escena debería conservar las cuatro mesas validadas " +
                "en 367H."
            );
            return;
        }

        var byKey =
            new Dictionary<string, BistroBuilder368AInstalledChair>(
                StringComparer.Ordinal
            );

        for (int index = 0; index < markers.Length; index++)
        {
            BistroBuilder368AInstalledChair marker = markers[index];
            string key = BuildChairKey(marker.TableId, marker.SlotIndex);

            if (byKey.ContainsKey(key))
            {
                result.AddError(
                    "Hay más de una silla 368A para " + key + "."
                );
                continue;
            }

            byKey.Add(key, marker);
        }

        int expectedChairCount = 0;
        int validChairCount = 0;
        var slots = new List<RestaurantTableSeatSlot>(8);

        for (int tableIndex = 0; tableIndex < tables.Length; tableIndex++)
        {
            RestaurantTable table = tables[tableIndex];
            RestaurantTableSeatingConfiguration configuration =
                table.GetComponent<RestaurantTableSeatingConfiguration>();

            if (configuration == null)
            {
                result.AddError(
                    table.name +
                    " no contiene configuración universal de plazas."
                );
                continue;
            }

            if (!configuration.ValidateConfiguration(out string error))
            {
                result.AddError(table.name + ": " + error);
                continue;
            }

            int slotCount = configuration.WriteCurrentSlots(slots);
            expectedChairCount += slotCount;

            if (slotCount != table.Capacity)
            {
                result.AddError(
                    table.name + " genera " + slotCount +
                    " plazas para Capacity=" + table.Capacity + "."
                );
                continue;
            }

            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                RestaurantTableSeatSlot slot = slots[slotIndex];
                string key = BuildChairKey(
                    table.TableId,
                    slot.SlotIndex
                );

                if (!byKey.TryGetValue(
                        key,
                        out BistroBuilder368AInstalledChair marker
                    ) ||
                    marker == null)
                {
                    result.AddError(
                        "Falta la silla visual para la mesa " +
                        table.TableId + ", plaza " +
                        slot.SlotIndex + "."
                    );
                    continue;
                }

                if (!marker.ValidateConfiguration(out error))
                {
                    result.AddError(marker.name + ": " + error);
                    continue;
                }

                RestaurantSeat seat =
                    marker.GetComponent<RestaurantSeat>();

                if (!configuration.TryEvaluateSeatAgainstSlot(
                        seat,
                        seat.transform.position,
                        seat.transform.rotation,
                        slot,
                        out RestaurantSeatSlotMatch match
                    ) ||
                    !match.IsValid)
                {
                    result.AddError(
                        marker.name +
                        " no coincide espacialmente con su plaza."
                    );
                    continue;
                }

                Renderer[] renderers =
                    marker.GetComponentsInChildren<Renderer>(true);

                if (renderers.Length == 0)
                {
                    result.AddError(
                        marker.name + " no tiene geometría visible."
                    );
                    continue;
                }

                validChairCount++;
            }
        }

        if (markers.Length != expectedChairCount)
        {
            result.AddError(
                "Se esperaban " + expectedChairCount +
                " sillas instaladas y existen " + markers.Length + "."
            );
            return;
        }

        if (validChairCount == expectedChairCount)
        {
            result.AddCorrect(
                "Las " + validChairCount +
                " sillas son visibles, operativas y están alineadas con " +
                "sus mesas."
            );
            result.AddCorrect(
                "Cada mesa tiene exactamente tantas sillas como su " +
                "capacidad funcional."
            );
        }
    }

    private static string BuildChairKey(int tableId, int slotIndex)
    {
        return tableId + ":" + slotIndex;
    }
}
