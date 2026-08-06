using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Conversión y validación pura del bloque de autoría persistente 2.1G3.
/// Centraliza el contrato para que captura, carga, migraciones y pruebas no
/// mantengan reglas duplicadas ni divergentes.
/// </summary>
public static class BistroBuilderDishRecipeSaveDataUtility
{
    public static bool TryValidatePairStructure(
        BistroBuilderDishRecipeSaveData pair,
        out string error
    )
    {
        if (pair == null || pair.dish == null || pair.recipe == null)
        {
            error = "menu.state contiene un par plato/receta nulo.";
            return false;
        }

        BistroBuilderDishDefinitionSaveData dish = pair.dish;
        BistroBuilderRecipeDefinitionSaveData recipe = pair.recipe;

        if (dish.definitionVersion < 1 ||
            dish.definitionVersion >
                BistroBuilderDishDefinition.CurrentDefinitionVersion)
        {
            error = "La definición persistente de " + dish.dishId +
                    " usa una versión no soportada.";
            return false;
        }

        if (!IsNormalizedStableId(dish.dishId) ||
            string.IsNullOrWhiteSpace(dish.displayName) ||
            !IsNormalizedStableId(dish.categoryId) ||
            !IsNormalizedStableId(dish.recipeId))
        {
            error = "La definición persistente de plato tiene identidad o " +
                    "nombre inválidos.";
            return false;
        }

        if (!Enum.IsDefined(typeof(BistroBuilderDishCourse), dish.course) ||
            !Enum.IsDefined(
                typeof(BistroBuilderKitchenStationType),
                dish.requiredStation
            ) ||
            !BistroBuilderMenuIdUtility.IsValidServiceMask(
                (BistroBuilderMealServiceAvailability)
                    dish.defaultAvailability,
                false
            ) ||
            !BistroBuilderServiceModeUtility.IsValidAvailabilityMask(
                (BistroBuilderDishServiceModeAvailability)
                    dish.allowedServiceModes,
                false
            ))
        {
            error = "La definición persistente de " + dish.dishId +
                    " contiene clasificación o servicio inválidos.";
            return false;
        }

        if (dish.basePreparationSeconds <
                BistroBuilderDishDefinition.MinimumPreparationSeconds ||
            dish.basePreparationSeconds >
                BistroBuilderDishDefinition.MaximumPreparationSeconds ||
            dish.complexity <
                BistroBuilderDishDefinition.MinimumPreparationDifficulty ||
            dish.complexity >
                BistroBuilderDishDefinition.MaximumPreparationDifficulty ||
            dish.basePriceCents < 0 ||
            dish.basePriceCents >
                BistroBuilderDishDefinition.MaximumPriceCents)
        {
            error = "La definición persistente de " + dish.dishId +
                    " contiene precio o preparación fuera de rango.";
            return false;
        }

        if ((!dish.shareable &&
             (dish.minimumConsumers != 1 || dish.maximumConsumers != 1)) ||
            (dish.shareable &&
             (dish.minimumConsumers < 2 ||
              dish.maximumConsumers < dish.minimumConsumers)))
        {
            error = "La definición persistente de " + dish.dishId +
                    " contiene consumidores incompatibles.";
            return false;
        }

        if (recipe.definitionVersion < 1 ||
            recipe.definitionVersion >
                BistroBuilderRecipeDefinition.CurrentDefinitionVersion ||
            !IsNormalizedStableId(recipe.recipeId) ||
            !IsNormalizedStableId(recipe.dishId) ||
            !string.Equals(
                recipe.recipeId,
                dish.recipeId,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                recipe.dishId,
                dish.dishId,
                StringComparison.Ordinal
            ))
        {
            error = "La receta persistente no coincide con el plato " +
                    dish.dishId + ".";
            return false;
        }

        if (recipe.yieldPortions < 1 ||
            recipe.yieldPortions >
                BistroBuilderRecipeDefinition.MaximumYieldPortions ||
            recipe.wasteBasisPoints < 0 ||
            recipe.wasteBasisPoints >
                BistroBuilderRecipeDefinition.MaximumWasteBasisPoints ||
            recipe.ingredients == null || recipe.ingredients.Count == 0)
        {
            error = "La receta persistente de " + dish.dishId +
                    " contiene rendimiento, merma o ingredientes inválidos.";
            return false;
        }

        HashSet<string> ingredientIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < recipe.ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientSaveData line =
                recipe.ingredients[index];

            if (line == null ||
                !IsNormalizedStableId(line.ingredientId) ||
                !ingredientIds.Add(line.ingredientId) ||
                double.IsNaN(line.amount) ||
                double.IsInfinity(line.amount) ||
                line.amount <= 0d ||
                !Enum.IsDefined(
                    typeof(BistroBuilderMeasurementUnit),
                    line.unit
                ))
            {
                error = "La receta persistente de " + dish.dishId +
                        " contiene una línea de ingrediente inválida.";
                return false;
            }

            if (!BistroBuilderMeasurementUtility
                    .TryConvertToCanonicalMilliUnits(
                        line.amount,
                        (BistroBuilderMeasurementUnit)line.unit,
                        out _,
                        out _
                    ))
            {
                error = "La receta persistente de " + dish.dishId +
                        " contiene una cantidad fuera del rango operativo.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Valida conjuntamente las dos colecciones persistentes. Además del
    /// contrato individual exige DishId y RecipeId únicos entre pares
    /// resueltos y no resueltos para impedir identidades ambiguas al reintentar.
    /// </summary>
    public static bool TryValidatePairCollections(
        IList<BistroBuilderDishRecipeSaveData> resolved,
        IList<BistroBuilderDishRecipeSaveData> unresolved,
        out string error
    )
    {
        if (resolved == null || unresolved == null)
        {
            error = "Las colecciones persistentes de autoría son nulas.";
            return false;
        }

        HashSet<string> dishIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> recipeIds =
            new HashSet<string>(StringComparer.Ordinal);

        return TryValidatePairCollection(
                   resolved,
                   dishIds,
                   recipeIds,
                   out error
               ) &&
               TryValidatePairCollection(
                   unresolved,
                   dishIds,
                   recipeIds,
                   out error
               );
    }

    public static BistroBuilderDishRecipeSaveData FromRuntime(
        BistroBuilderDishDefinition dish,
        BistroBuilderRecipeDefinition recipe
    )
    {
        if (dish == null)
        {
            throw new ArgumentNullException(nameof(dish));
        }

        if (recipe == null)
        {
            throw new ArgumentNullException(nameof(recipe));
        }

        BistroBuilderDishRecipeSaveData pair =
            new BistroBuilderDishRecipeSaveData
            {
                dish = new BistroBuilderDishDefinitionSaveData
                {
                    definitionVersion = dish.DefinitionVersion,
                    dishId = dish.DishId,
                    displayName = dish.DisplayName,
                    description = dish.Description,
                    categoryId = dish.CategoryId,
                    course = (int)dish.Course,
                    defaultAvailability = (int)dish.DefaultAvailability,
                    allowedServiceModes = (int)dish.AllowedServiceModes,
                    requiredStation = (int)dish.RequiredStation,
                    basePreparationSeconds = dish.BasePreparationSeconds,
                    complexity = dish.Complexity,
                    recipeId = dish.RecipeId,
                    basePriceCents = dish.BasePriceCents,
                    shareable = dish.Shareable,
                    minimumConsumers = dish.MinimumConsumers,
                    maximumConsumers = dish.MaximumConsumers
                },
                recipe = new BistroBuilderRecipeDefinitionSaveData
                {
                    definitionVersion =
                        BistroBuilderRecipeDefinition.CurrentDefinitionVersion,
                    recipeId = recipe.RecipeId,
                    dishId = recipe.DishId,
                    yieldPortions = recipe.YieldPortions,
                    wasteBasisPoints = recipe.WasteBasisPoints,
                    notes = recipe.Notes,
                    ingredients =
                        new List<BistroBuilderRecipeIngredientSaveData>(
                            recipe.Ingredients.Count
                        )
                }
            };

        for (int index = 0; index < recipe.Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount line =
                recipe.Ingredients[index];
            pair.recipe.ingredients.Add(
                new BistroBuilderRecipeIngredientSaveData
                {
                    ingredientId = line.Ingredient.IngredientId,
                    amount = line.Amount,
                    unit = (int)line.Unit
                }
            );
        }

        return pair;
    }

    public static bool TryCreateRuntimePair(
        BistroBuilderDishRecipeSaveData pair,
        BistroBuilderDishCategoryCatalogService categoryCatalogService,
        BistroBuilderRecipeCatalogService recipeCatalogService,
        out BistroBuilderDishDefinition dish,
        out BistroBuilderRecipeDefinition recipe,
        out string unresolvedReason
    )
    {
        dish = null;
        recipe = null;

        if (!TryValidatePairStructure(pair, out unresolvedReason))
        {
            return false;
        }

        if (categoryCatalogService == null ||
            !categoryCatalogService.TryGetDefinition(
                pair.dish.categoryId,
                out _
            ))
        {
            unresolvedReason = "No existe la categoría " +
                pair.dish.categoryId + ".";
            return false;
        }

        List<BistroBuilderRecipeIngredientAmount> lines =
            new List<BistroBuilderRecipeIngredientAmount>(
                pair.recipe.ingredients.Count
            );

        for (int index = 0; index < pair.recipe.ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientSaveData source =
                pair.recipe.ingredients[index];

            if (recipeCatalogService == null ||
                !recipeCatalogService.TryGetIngredient(
                    source.ingredientId,
                    out BistroBuilderIngredientDefinition ingredient
                ) ||
                ingredient == null)
            {
                unresolvedReason = "No existe el ingrediente " +
                    source.ingredientId + ".";
                return false;
            }

            BistroBuilderMeasurementUnit unit =
                (BistroBuilderMeasurementUnit)source.unit;

            if (!BistroBuilderMeasurementUtility.AreCompatible(
                    ingredient.BaseUnit,
                    unit
                ))
            {
                unresolvedReason = "La unidad persistida de " +
                    source.ingredientId +
                    " ya no es compatible con su unidad base.";
                return false;
            }

            if (!BistroBuilderMeasurementUtility
                    .TryConvertToCanonicalMilliUnits(
                        source.amount,
                        unit,
                        out _,
                        out unresolvedReason
                    ))
            {
                return false;
            }

            lines.Add(
                new BistroBuilderRecipeIngredientAmount(
                    ingredient,
                    source.amount,
                    unit
                )
            );
        }

        BistroBuilderDishDefinitionSaveData savedDish = pair.dish;
        dish = BistroBuilderDishDefinition.CreateRuntime(
            savedDish.dishId,
            savedDish.displayName,
            savedDish.description,
            savedDish.categoryId,
            (BistroBuilderDishCourse)savedDish.course,
            (BistroBuilderMealServiceAvailability)
                savedDish.defaultAvailability,
            (BistroBuilderDishServiceModeAvailability)
                savedDish.allowedServiceModes,
            (BistroBuilderKitchenStationType)savedDish.requiredStation,
            savedDish.basePreparationSeconds,
            savedDish.complexity,
            savedDish.recipeId,
            savedDish.basePriceCents,
            savedDish.shareable,
            savedDish.minimumConsumers,
            savedDish.maximumConsumers
        );

        if (!dish.TryValidate(out unresolvedReason))
        {
            DestroyRuntimeObject(dish);
            dish = null;
            return false;
        }

        recipe = BistroBuilderRecipeDefinition.CreateRuntime(
            pair.recipe.recipeId,
            dish,
            pair.recipe.yieldPortions,
            pair.recipe.wasteBasisPoints,
            lines,
            pair.recipe.notes
        );

        if (!recipe.TryValidate(out unresolvedReason))
        {
            DestroyRuntimeObject(recipe);
            DestroyRuntimeObject(dish);
            recipe = null;
            dish = null;
            return false;
        }

        unresolvedReason = string.Empty;
        return true;
    }

    public static BistroBuilderDishRecipeSaveData Clone(
        BistroBuilderDishRecipeSaveData source
    )
    {
        if (source == null)
        {
            return null;
        }

        BistroBuilderDishRecipeSaveData result =
            new BistroBuilderDishRecipeSaveData
            {
                dish = source.dish == null
                    ? null
                    : new BistroBuilderDishDefinitionSaveData
                    {
                        definitionVersion = source.dish.definitionVersion,
                        dishId = source.dish.dishId,
                        displayName = source.dish.displayName,
                        description = source.dish.description,
                        categoryId = source.dish.categoryId,
                        course = source.dish.course,
                        defaultAvailability =
                            source.dish.defaultAvailability,
                        allowedServiceModes =
                            source.dish.allowedServiceModes,
                        requiredStation = source.dish.requiredStation,
                        basePreparationSeconds =
                            source.dish.basePreparationSeconds,
                        complexity = source.dish.complexity,
                        recipeId = source.dish.recipeId,
                        basePriceCents = source.dish.basePriceCents,
                        shareable = source.dish.shareable,
                        minimumConsumers = source.dish.minimumConsumers,
                        maximumConsumers = source.dish.maximumConsumers
                    },
                recipe = source.recipe == null
                    ? null
                    : new BistroBuilderRecipeDefinitionSaveData
                    {
                        definitionVersion = source.recipe.definitionVersion,
                        recipeId = source.recipe.recipeId,
                        dishId = source.recipe.dishId,
                        yieldPortions = source.recipe.yieldPortions,
                        wasteBasisPoints = source.recipe.wasteBasisPoints,
                        notes = source.recipe.notes,
                        ingredients =
                            new List<BistroBuilderRecipeIngredientSaveData>()
                    }
            };

        if (source.recipe != null && source.recipe.ingredients != null)
        {
            for (int index = 0;
                 index < source.recipe.ingredients.Count;
                 index++)
            {
                BistroBuilderRecipeIngredientSaveData line =
                    source.recipe.ingredients[index];
                result.recipe.ingredients.Add(
                    line == null
                        ? null
                        : new BistroBuilderRecipeIngredientSaveData
                        {
                            ingredientId = line.ingredientId,
                            amount = line.amount,
                            unit = line.unit
                        }
                );
            }
        }

        return result;
    }

    public static void DestroyRuntimeObject(UnityEngine.Object value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(value);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(value);
        }
    }

    private static bool TryValidatePairCollection(
        IList<BistroBuilderDishRecipeSaveData> pairs,
        HashSet<string> dishIds,
        HashSet<string> recipeIds,
        out string error
    )
    {
        for (int index = 0; index < pairs.Count; index++)
        {
            BistroBuilderDishRecipeSaveData pair = pairs[index];

            if (!TryValidatePairStructure(pair, out error))
            {
                return false;
            }

            if (!dishIds.Add(pair.dish.dishId))
            {
                error = "La autoría persistente repite el DishId " +
                        pair.dish.dishId + ".";
                return false;
            }

            if (!recipeIds.Add(pair.recipe.recipeId))
            {
                error = "La autoría persistente repite el RecipeId " +
                        pair.recipe.recipeId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool IsNormalizedStableId(string value)
    {
        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(value);
        return BistroBuilderMenuIdUtility.IsValidStableId(normalized) &&
               string.Equals(value, normalized, StringComparison.Ordinal);
    }
}
