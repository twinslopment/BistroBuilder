using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prueba funcional runtime de 2.1G3. Aplica un estado v4 con un plato y una
/// receta creados, valida su captura JSON, simula contenido canónico ausente,
/// comprueba conservación no resuelta y restaura el estado original.
/// </summary>
public sealed class BistroBuilderMenuDishRecipe21G3FunctionalTestWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/2.1G3 Dish Recipe Persistence Functional Test";

    private string report =
        "Entra en Play Mode y ejecuta la prueba funcional 2.1G3.";
    private Vector2 scroll;

    [MenuItem(MenuPath, false, 197)]
    private static void OpenWindow()
    {
        GetWindow<BistroBuilderMenuDishRecipe21G3FunctionalTestWindow>(
            "BB 2.1G3 Test"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "BistroBuilder 2.1G3 — Prueba funcional",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "La prueba modifica solo estado runtime, valida carga/captura " +
            "menu.state v4 y restaura carta, platos y recetas al terminar.",
            MessageType.Info
        );
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);

        if (GUILayout.Button(
                "Ejecutar prueba funcional 2.1G3",
                GUILayout.Height(42f)
            ))
        {
            RunFunctionalTest();
        }

        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RunFunctionalTest()
    {
        StringBuilder builder = new StringBuilder(8192);
        int passed = 0;
        int failed = 0;
        BistroBuilderMenuSaveSectionProvider provider = null;
        BistroBuilderMenuSaveData original = null;

        void Expect(bool condition, string message)
        {
            if (condition)
            {
                passed++;
                builder.AppendLine("- OK: " + message);
            }
            else
            {
                failed++;
                builder.AppendLine("- FALLO: " + message);
            }
        }

        try
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException(
                    "Entra en Play Mode antes de ejecutar la prueba."
                );
            }

            if (!TryResolve(out provider, out string resolveError))
            {
                throw new InvalidOperationException(resolveError);
            }

            BistroBuilderDishRecipePersistenceService persistence =
                provider.DishRecipePersistenceService;
            BistroBuilderDishCatalogService dishes = provider.CatalogService;
            BistroBuilderRecipeCatalogService recipes =
                persistence.RecipeCatalogService;
            BistroBuilderRestaurantMenuCollectionService collection =
                provider.CollectionService;
            BistroBuilderRestaurantMenuService menu = provider.MenuService;

            original = Capture(provider);
            Expect(
                original != null &&
                original.schemaVersion ==
                    BistroBuilderMenuSaveData.CurrentSchemaVersion,
                "Se captura el estado original con la versión actual de menu.state."
            );

            BistroBuilderMenuSaveData testState = Clone(original);
            string suffix = Guid.NewGuid().ToString("N");
            string dishId = "dish_g3_functional_" + suffix;
            string recipeId = "recipe_g3_functional_" + suffix;

            List<BistroBuilderDishCategoryDefinition> categories =
                new List<BistroBuilderDishCategoryDefinition>();
            persistence.CategoryCatalogService.CopyDefinitionsTo(categories);
            List<BistroBuilderIngredientDefinition> ingredients =
                new List<BistroBuilderIngredientDefinition>();
            recipes.CopyIngredientsTo(ingredients);

            BistroBuilderDishCategoryDefinition category = null;
            BistroBuilderIngredientDefinition ingredient = null;

            for (int index = 0; index < categories.Count; index++)
            {
                if (categories[index] != null)
                {
                    category = categories[index];
                    break;
                }
            }

            for (int index = 0; index < ingredients.Count; index++)
            {
                if (ingredients[index] != null)
                {
                    ingredient = ingredients[index];
                    break;
                }
            }

            if (category == null || ingredient == null)
            {
                throw new InvalidOperationException(
                    "Faltan categorías o ingredientes canónicos para la prueba."
                );
            }

            BistroBuilderRestaurantMenuSaveData originalActive =
                FindRestaurant(original, original.activeRestaurantId);
            BistroBuilderDishDefinition canonicalOverride =
                FindCanonicalMenuDishWithoutAuthoredOverride(
                    dishes,
                    original,
                    originalActive
                );

            if (canonicalOverride == null)
            {
                throw new InvalidOperationException(
                    "No existe un plato canónico libre para validar compatibilidad."
                );
            }

            BistroBuilderMenuSaveData invalidIdentityState = Clone(original);
            BistroBuilderDishRecipeSaveData invalidIdentityPair = CreatePair(
                "dish_g3_invalid_identity_" + suffix,
                canonicalOverride.RecipeId,
                category.CategoryId,
                ingredient
            );
            invalidIdentityState.authoredDishRecipes.Add(invalidIdentityPair);
            Expect(
                !provider.ValidateState(invalidIdentityState, out _),
                "Un plato nuevo no puede apropiarse del RecipeId de otro plato canónico."
            );

            BistroBuilderMenuSaveData missingOverrideState = Clone(original);
            BistroBuilderDishRecipeSaveData missingOverride =
                CreatePairFromDish(canonicalOverride, ingredient);
            missingOverride.recipe.ingredients[0].ingredientId =
                "ingredient_missing_g3_override";
            missingOverrideState.authoredDishRecipes.Add(missingOverride);
            Apply(provider, missingOverrideState);
            Expect(
                dishes.IsCanonicalDefinitionSuppressed(
                    canonicalOverride.DishId
                ) &&
                !dishes.TryGetDefinition(canonicalOverride.DishId, out _) &&
                !recipes.TryGetRecipeByDishId(
                    canonicalOverride.DishId,
                    out _
                ) &&
                !recipes.TryGetRecipeByRecipeId(
                    canonicalOverride.RecipeId,
                    out _
                ) &&
                !menu.TryGetItemSnapshot(canonicalOverride.DishId, out _),
                "Una sobrescritura irresoluble no cae silenciosamente en el plato ni la receta canónicos."
            );
            BistroBuilderMenuSaveData missingOverrideCapture =
                Capture(provider);
            Expect(
                FindPair(
                    missingOverrideCapture.unresolvedAuthoredDishRecipes,
                    canonicalOverride.DishId
                ) != null,
                "La sobrescritura canónica irresoluble se conserva para reintento."
            );
            Apply(provider, original);
            Expect(
                !dishes.IsCanonicalDefinitionSuppressed(
                    canonicalOverride.DishId
                ) &&
                dishes.TryGetDefinition(canonicalOverride.DishId, out _) &&
                recipes.TryGetRecipeByDishId(
                    canonicalOverride.DishId,
                    out _
                ) &&
                recipes.TryGetRecipeByRecipeId(
                    canonicalOverride.RecipeId,
                    out _
                ) &&
                menu.TryGetItemSnapshot(canonicalOverride.DishId, out _),
                "Restaurar un estado válido elimina la sombra y recupera plato y receta canónicos."
            );

            BistroBuilderDishRecipeSaveData pair = CreatePair(
                dishId,
                recipeId,
                category.CategoryId,
                ingredient
            );
            testState.authoredDishRecipes.Add(pair);
            BistroBuilderRestaurantMenuSaveData active =
                FindRestaurant(testState, testState.activeRestaurantId);

            if (active == null)
            {
                throw new InvalidOperationException(
                    "No existe la carta activa dentro del snapshot."
                );
            }

            active.items.Add(
                new BistroBuilderMenuItemSaveData
                {
                    dishId = dishId,
                    currentPriceCents = pair.dish.basePriceCents,
                    unlocked = true,
                    enabled = true,
                    manuallySoldOut = false,
                    signatureDish = true,
                    availableServices = pair.dish.defaultAvailability,
                    displayOrder = NextDisplayOrder(active),
                    preparationDifficulty = pair.dish.complexity,
                    basePreparationSeconds =
                        pair.dish.basePreparationSeconds
                }
            );
            active.revision++;

            BistroBuilderRestaurantMenuPortfolioSaveData activePortfolio =
                FindPortfolio(
                    testState,
                    testState.activeRestaurantId
                );
            BistroBuilderNamedMenuSaveData activeNamedMenu =
                FindNamedMenu(
                    activePortfolio,
                    activePortfolio != null
                        ? activePortfolio.activeMenuId
                        : string.Empty
                );
            if (activeNamedMenu == null)
            {
                throw new InvalidOperationException(
                    "No existe la carta nombrada activa del portfolio."
                );
            }
            activeNamedMenu.items.Add(
                CloneMenuItem(active.items[active.items.Count - 1])
            );
            activeNamedMenu.revision++;

            Expect(
                provider.ValidateState(testState, out string stateError),
                string.IsNullOrWhiteSpace(stateError)
                    ? "El estado actual con plato y receta creados es válido."
                    : stateError
            );

            Apply(provider, testState);
            Expect(
                dishes.TryGetDefinition(
                    dishId,
                    out BistroBuilderDishDefinition restoredDish
                ) && restoredDish.DisplayName == pair.dish.displayName,
                "La carga reconstruye la definición runtime del plato."
            );
            Expect(
                recipes.TryGetRecipeByDishId(
                    dishId,
                    out BistroBuilderRecipeDefinition restoredRecipe
                ) && restoredRecipe.RecipeId == recipeId &&
                ReferenceEquals(restoredRecipe.Dish, restoredDish),
                "La carga reconstruye la receta y su enlace al plato."
            );
            Expect(
                menu.TryGetItemSnapshot(
                    dishId,
                    out BistroBuilderMenuItemRuntimeState restoredItem
                ) && restoredItem.CurrentPriceCents ==
                    pair.dish.basePriceCents &&
                restoredItem.BasePreparationSeconds ==
                    pair.dish.basePreparationSeconds,
                "La carta activa resuelve precio y preparación del plato creado."
            );

            BistroBuilderMenuSaveData captured = Capture(provider);
            BistroBuilderDishRecipeSaveData capturedPair =
                FindPair(captured.authoredDishRecipes, dishId);
            Expect(
                capturedPair != null &&
                capturedPair.recipe.ingredients.Count == 1 &&
                capturedPair.recipe.ingredients[0].ingredientId ==
                    ingredient.IngredientId,
                "La captura conserva definición, receta e ingrediente creados."
            );

            string json = JsonUtility.ToJson(captured, true);
            BistroBuilderMenuSaveData jsonRoundTrip =
                JsonUtility.FromJson<BistroBuilderMenuSaveData>(json);
            BistroBuilderDishRecipeSaveData jsonPair =
                FindPair(jsonRoundTrip.authoredDishRecipes, dishId);
            Expect(
                jsonRoundTrip.schemaVersion ==
                    BistroBuilderMenuSaveData.CurrentSchemaVersion &&
                jsonPair != null &&
                Math.Abs(
                    jsonPair.recipe.ingredients[0].amount -
                    pair.recipe.ingredients[0].amount
                ) < 0.001d,
                "El round-trip JSON conserva identidad y cantidades exactas."
            );

            BistroBuilderMenuSaveData unresolvedState = Clone(captured);
            BistroBuilderDishRecipeSaveData unresolvedPair =
                FindPair(unresolvedState.authoredDishRecipes, dishId);
            unresolvedPair.recipe.ingredients[0].ingredientId =
                "ingredient_missing_g3";
            Apply(provider, unresolvedState);
            Expect(
                !dishes.TryGetDefinition(dishId, out _) &&
                !recipes.TryGetRecipeByDishId(dishId, out _) &&
                !dishes.IsCanonicalDefinitionSuppressed(dishId) &&
                persistence.UnresolvedPairCount == 1,
                "El contenido ausente conserva el plato nuevo como no resuelto sin sombra innecesaria."
            );
            Expect(
                persistence.IsDishIdReserved(dishId) &&
                persistence.IsRecipeIdReserved(recipeId),
                "DishId y RecipeId no resueltos permanecen reservados frente a nuevas creaciones."
            );
            Expect(
                !menu.TryGetItemSnapshot(dishId, out _) &&
                collection.UnresolvedItemCount > 0,
                "La carta conserva el DishId sin ofrecer un plato irresoluble."
            );

            BistroBuilderMenuSaveData unresolvedCapture = Capture(provider);
            BistroBuilderDishRecipeSaveData preserved =
                FindPair(
                    unresolvedCapture.unresolvedAuthoredDishRecipes,
                    dishId
                );
            Expect(
                preserved != null &&
                preserved.recipe.ingredients[0].ingredientId ==
                    "ingredient_missing_g3",
                "Una nueva captura conserva íntegramente la autoría no resuelta."
            );

            Apply(provider, captured);
            Expect(
                dishes.TryGetDefinition(dishId, out _) &&
                recipes.TryGetRecipeByDishId(dishId, out _) &&
                persistence.UnresolvedPairCount == 0 &&
                !persistence.IsDishIdReserved(dishId) &&
                !persistence.IsRecipeIdReserved(recipeId) &&
                menu.TryGetItemSnapshot(dishId, out _),
                "Al recuperar el contenido, plato, receta y carta se resuelven de nuevo."
            );

            Expect(
                provider.DishRecipePersistenceService != null &&
                provider.SectionVersion ==
                    BistroBuilderMenuSaveData.CurrentSchemaVersion,
                "La persistencia se mantiene integrada en una única sección menu.state."
            );
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine("- FALLO: Excepción: " + exception.Message);
            Debug.LogException(exception);
        }
        finally
        {
            if (provider != null && original != null)
            {
                try
                {
                    Apply(provider, original);
                    passed++;
                    builder.AppendLine(
                        "- OK: La carta y las capas runtime originales se restauran."
                    );
                }
                catch (Exception restoreException)
                {
                    failed++;
                    builder.AppendLine(
                        "- FALLO: No se pudo restaurar el estado original: " +
                        restoreException.Message
                    );
                    Debug.LogException(restoreException);
                }
            }
        }

        report = (failed == 0
            ? "PRUEBA FUNCIONAL 2.1G3 SUPERADA"
            : "PRUEBA FUNCIONAL 2.1G3 FALLIDA") +
            "\nCorrectos: " + passed +
            "\nFallos: " + failed +
            "\n" + builder.ToString().TrimEnd();

        if (failed == 0)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }
    }

    private static bool TryResolve(
        out BistroBuilderMenuSaveSectionProvider provider,
        out string error
    )
    {
        List<BistroBuilderMenuSaveSectionProvider> providers =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderMenuSaveSectionProvider
            >(SceneManager.GetActiveScene());
        provider = providers.Count == 1 ? providers[0] : null;

        if (provider == null)
        {
            error = "Debe existir un único proveedor menu.state.";
            return false;
        }

        if (provider.DishRecipePersistenceService == null)
        {
            error = "Falta BistroBuilderDishRecipePersistenceService.";
            return false;
        }

        if (!provider.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static BistroBuilderMenuSaveData Capture(
        BistroBuilderMenuSaveSectionProvider provider
    )
    {
        BistroBuilderSaveCaptureContext context =
            new BistroBuilderSaveCaptureContext(210103);
        RunEnumerator(provider.CaptureState(context));

        if (context.HasFailed ||
            !(context.State is BistroBuilderMenuSaveData data))
        {
            throw new InvalidOperationException(
                context.HasFailed
                    ? context.ErrorMessage
                    : "menu.state no devolvió el DTO esperado."
            );
        }

        return Clone(data);
    }

    private static void Apply(
        BistroBuilderMenuSaveSectionProvider provider,
        BistroBuilderMenuSaveData state
    )
    {
        BistroBuilderSaveLoadContext context =
            new BistroBuilderSaveLoadContext(210103, false, 64);
        RunEnumerator(provider.PrepareForLoad(context));

        if (!context.HasFailed)
        {
            RunEnumerator(provider.ApplyState(Clone(state), context));
        }

        provider.FinalizeLoad(context);

        if (context.HasFailed)
        {
            throw new InvalidOperationException(context.ErrorMessage);
        }
    }

    private static BistroBuilderMenuSaveData Clone(
        BistroBuilderMenuSaveData source
    )
    {
        return JsonUtility.FromJson<BistroBuilderMenuSaveData>(
            JsonUtility.ToJson(source, false)
        );
    }

    private static BistroBuilderDishDefinition
        FindCanonicalMenuDishWithoutAuthoredOverride(
            BistroBuilderDishCatalogService dishes,
            BistroBuilderMenuSaveData state,
            BistroBuilderRestaurantMenuSaveData active
        )
    {
        if (dishes == null || dishes.Catalog == null || active == null)
        {
            return null;
        }

        for (int index = 0; index < active.items.Count; index++)
        {
            string dishId = active.items[index].dishId;

            if (FindPair(state.authoredDishRecipes, dishId) == null &&
                FindPair(state.unresolvedAuthoredDishRecipes, dishId) == null &&
                dishes.Catalog.TryGetDefinition(
                    dishId,
                    out BistroBuilderDishDefinition definition
                ) &&
                definition != null)
            {
                return definition;
            }
        }

        return null;
    }

    private static BistroBuilderDishRecipeSaveData CreatePairFromDish(
        BistroBuilderDishDefinition dish,
        BistroBuilderIngredientDefinition ingredient
    )
    {
        BistroBuilderDishRecipeSaveData pair = CreatePair(
            dish.DishId,
            dish.RecipeId,
            dish.CategoryId,
            ingredient
        );
        pair.dish.displayName = dish.DisplayName;
        pair.dish.description = dish.Description;
        pair.dish.course = (int)dish.Course;
        pair.dish.defaultAvailability = (int)dish.DefaultAvailability;
        pair.dish.allowedServiceModes = (int)dish.AllowedServiceModes;
        pair.dish.requiredStation = (int)dish.RequiredStation;
        pair.dish.basePreparationSeconds = dish.BasePreparationSeconds;
        pair.dish.complexity = dish.Complexity;
        pair.dish.basePriceCents = dish.BasePriceCents;
        pair.dish.shareable = dish.Shareable;
        pair.dish.minimumConsumers = dish.MinimumConsumers;
        pair.dish.maximumConsumers = dish.MaximumConsumers;
        return pair;
    }

    private static BistroBuilderDishRecipeSaveData CreatePair(
        string dishId,
        string recipeId,
        string categoryId,
        BistroBuilderIngredientDefinition ingredient
    )
    {
        return new BistroBuilderDishRecipeSaveData
        {
            dish = new BistroBuilderDishDefinitionSaveData
            {
                definitionVersion = 1,
                dishId = dishId,
                displayName = "Plato funcional persistente G3",
                description = "Creado temporalmente para validar guardado y carga.",
                categoryId = categoryId,
                course = (int)BistroBuilderDishCourse.Main,
                defaultAvailability =
                    (int)BistroBuilderMealServiceAvailability.All,
                allowedServiceModes =
                    (int)BistroBuilderDishServiceModeAvailability.All,
                requiredStation =
                    (int)BistroBuilderKitchenStationType.HotKitchen,
                basePreparationSeconds = 493,
                complexity = 7,
                recipeId = recipeId,
                basePriceCents = 2175,
                shareable = false,
                minimumConsumers = 1,
                maximumConsumers = 1
            },
            recipe = new BistroBuilderRecipeDefinitionSaveData
            {
                definitionVersion = 1,
                recipeId = recipeId,
                dishId = dishId,
                yieldPortions = 2,
                wasteBasisPoints = 325,
                notes = "Prueba funcional 2.1G3",
                ingredients = new List<BistroBuilderRecipeIngredientSaveData>
                {
                    new BistroBuilderRecipeIngredientSaveData
                    {
                        ingredientId = ingredient.IngredientId,
                        amount = 2d,
                        unit = (int)ingredient.BaseUnit
                    }
                }
            }
        };
    }

    private static BistroBuilderRestaurantMenuSaveData FindRestaurant(
        BistroBuilderMenuSaveData data,
        string restaurantId
    )
    {
        for (int index = 0; index < data.restaurants.Count; index++)
        {
            if (data.restaurants[index].restaurantId == restaurantId)
            {
                return data.restaurants[index];
            }
        }

        return null;
    }

    private static BistroBuilderDishRecipeSaveData FindPair(
        IList<BistroBuilderDishRecipeSaveData> pairs,
        string dishId
    )
    {
        if (pairs == null)
        {
            return null;
        }

        for (int index = 0; index < pairs.Count; index++)
        {
            BistroBuilderDishRecipeSaveData pair = pairs[index];

            if (pair != null && pair.dish != null &&
                pair.dish.dishId == dishId)
            {
                return pair;
            }
        }

        return null;
    }

    private static int NextDisplayOrder(
        BistroBuilderRestaurantMenuSaveData restaurant
    )
    {
        int maximum = -1;

        for (int index = 0; index < restaurant.items.Count; index++)
        {
            maximum = Math.Max(maximum, restaurant.items[index].displayOrder);
        }

        for (int index = 0; index < restaurant.unresolvedItems.Count; index++)
        {
            maximum = Math.Max(
                maximum,
                restaurant.unresolvedItems[index].displayOrder
            );
        }

        return maximum + 1;
    }

    private static BistroBuilderRestaurantMenuPortfolioSaveData
        FindPortfolio(
            BistroBuilderMenuSaveData data,
            string restaurantId
        )
    {
        if (data == null || data.portfolios == null)
        {
            return null;
        }

        for (int index = 0; index < data.portfolios.Count; index++)
        {
            BistroBuilderRestaurantMenuPortfolioSaveData portfolio =
                data.portfolios[index];
            if (portfolio != null && string.Equals(
                    portfolio.restaurantId,
                    restaurantId,
                    StringComparison.Ordinal
                ))
            {
                return portfolio;
            }
        }

        return null;
    }

    private static BistroBuilderNamedMenuSaveData FindNamedMenu(
        BistroBuilderRestaurantMenuPortfolioSaveData portfolio,
        string menuId
    )
    {
        if (portfolio == null || portfolio.menus == null)
        {
            return null;
        }

        for (int index = 0; index < portfolio.menus.Count; index++)
        {
            BistroBuilderNamedMenuSaveData menu = portfolio.menus[index];
            if (menu != null && string.Equals(
                    menu.menuId,
                    menuId,
                    StringComparison.Ordinal
                ))
            {
                return menu;
            }
        }

        return null;
    }

    private static BistroBuilderMenuItemSaveData CloneMenuItem(
        BistroBuilderMenuItemSaveData source
    )
    {
        return new BistroBuilderMenuItemSaveData
        {
            dishId = source.dishId,
            currentPriceCents = source.currentPriceCents,
            unlocked = source.unlocked,
            enabled = source.enabled,
            manuallySoldOut = source.manuallySoldOut,
            signatureDish = source.signatureDish,
            availableServices = source.availableServices,
            displayOrder = source.displayOrder,
            preparationDifficulty = source.preparationDifficulty,
            basePreparationSeconds = source.basePreparationSeconds
        };
    }

    private static void RunEnumerator(IEnumerator enumerator)
    {
        while (enumerator != null && enumerator.MoveNext())
        {
        }
    }
}
