using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoría transaccional de platos y recetas para 2.1G1/2.
///
/// Mantiene una capa de previsualización aislada mientras el editor está
/// abierto. Al aplicar, sustituye atómicamente las capas runtime de platos y
/// recetas; si el commit de carta falla, el editor puede restaurar el estado
/// anterior mediante RollbackState.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Menu/Dish Recipe Authoring Service"
)]
public sealed class BistroBuilderDishRecipeAuthoringService : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1G12";

    [SerializeField]
    private BistroBuilderDishCatalogService dishCatalogService;

    [SerializeField]
    private BistroBuilderRecipeCatalogService recipeCatalogService;

    [SerializeField]
    private BistroBuilderDishCategoryCatalogService categoryCatalogService;

    [SerializeField]
    private BistroBuilderMenuEditSessionService editSessionService;

    [Header("Depuración")]

    [SerializeField]
    private bool logChanges = true;

    private readonly Dictionary<string, BistroBuilderDishRecipeAuthoringRequest>
        draftByDishId =
            new Dictionary<string, BistroBuilderDishRecipeAuthoringRequest>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, BistroBuilderDishDefinition>
        previewDishById =
            new Dictionary<string, BistroBuilderDishDefinition>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, BistroBuilderRecipeDefinition>
        previewRecipeByDishId =
            new Dictionary<string, BistroBuilderRecipeDefinition>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderDishDefinition> dishBuffer =
        new List<BistroBuilderDishDefinition>(48);

    private readonly List<BistroBuilderDishDefinition> runtimeDishBuffer =
        new List<BistroBuilderDishDefinition>(24);

    private readonly List<BistroBuilderRecipeDefinition> runtimeRecipeBuffer =
        new List<BistroBuilderRecipeDefinition>(24);

    private readonly List<BistroBuilderIngredientDefinition> ingredientBuffer =
        new List<BistroBuilderIngredientDefinition>(32);

    private bool sessionOpen;
    private bool runtimeChangesAwaitingPublish;
    private int draftChangeCount;

    public event Action<string> DraftChanged;

    public BistroBuilderDishCatalogService DishCatalogService =>
        dishCatalogService;

    public BistroBuilderRecipeCatalogService RecipeCatalogService =>
        recipeCatalogService;

    public BistroBuilderDishCategoryCatalogService CategoryCatalogService =>
        categoryCatalogService;

    public BistroBuilderMenuEditSessionService EditSessionService =>
        editSessionService;

    public bool HasOpenSession => sessionOpen;

    public bool HasPendingChanges => sessionOpen && draftByDishId.Count > 0;

    public int DraftChangeCount => draftChangeCount;

    public int ModifiedDishCount => draftByDishId.Count;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnDisable()
    {
        DiscardSession();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (dishCatalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (recipeCatalogService == null)
        {
            error = "Falta BistroBuilderRecipeCatalogService.";
            return false;
        }

        if (categoryCatalogService == null)
        {
            error = "Falta BistroBuilderDishCategoryCatalogService.";
            return false;
        }

        if (editSessionService == null)
        {
            error = "Falta BistroBuilderMenuEditSessionService.";
            return false;
        }

        if (!dishCatalogService.ValidateConfiguration(out error) ||
            !recipeCatalogService.ValidateConfiguration(out error) ||
            !categoryCatalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!ReferenceEquals(
                recipeCatalogService.DishCatalogService,
                dishCatalogService
            ))
        {
            error = "La autoría no comparte el catálogo de platos y recetas.";
            return false;
        }

        if (!ReferenceEquals(
                editSessionService.CatalogService,
                dishCatalogService
            ) ||
            !ReferenceEquals(
                editSessionService.CategoryCatalogService,
                categoryCatalogService
            ))
        {
            error = "La autoría no comparte la sesión canónica de carta.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryBeginSession(out string error)
    {
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!editSessionService.HasOpenSession)
        {
            error = "La autoría necesita una sesión de carta abierta.";
            return false;
        }

        ClearDraftObjects();
        sessionOpen = true;
        runtimeChangesAwaitingPublish = false;
        draftChangeCount = 0;
        error = string.Empty;
        return true;
    }

    public void DiscardSession()
    {
        ClearDraftObjects();
        sessionOpen = false;
        runtimeChangesAwaitingPublish = false;
        draftChangeCount = 0;
    }

    public bool IsDishModified(string dishId)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            dishId
        );
        return sessionOpen && draftByDishId.ContainsKey(normalized);
    }

    public bool TryGetAuthoringRequest(
        string dishId,
        out BistroBuilderDishRecipeAuthoringRequest request,
        out string error
    )
    {
        request = null;

        if (!EnsureSession(out error))
        {
            return false;
        }

        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            dishId
        );

        if (draftByDishId.TryGetValue(normalized, out var existingDraft))
        {
            request = existingDraft.Clone();
            error = string.Empty;
            return true;
        }

        if (!dishCatalogService.TryGetDefinition(
                normalized,
                out BistroBuilderDishDefinition definition
            ) ||
            definition == null)
        {
            error = "No existe el plato " + normalized + ".";
            return false;
        }

        if (!recipeCatalogService.TryGetRecipeByDishId(
                normalized,
                out BistroBuilderRecipeDefinition recipe
            ) ||
            recipe == null)
        {
            error = "No existe una receta válida para " + normalized + ".";
            return false;
        }

        request = BuildRequest(definition, recipe);
        error = string.Empty;
        return true;
    }

    public BistroBuilderDishRecipeAuthoringRequest CreateNewRequest()
    {
        BistroBuilderDishRecipeAuthoringRequest request =
            new BistroBuilderDishRecipeAuthoringRequest();

        if (recipeCatalogService != null)
        {
            recipeCatalogService.CopyIngredientsTo(ingredientBuffer);

            if (ingredientBuffer.Count > 0 && ingredientBuffer[0] != null)
            {
                BistroBuilderIngredientDefinition ingredient =
                    ingredientBuffer[0];
                request.Ingredients.Add(
                    new BistroBuilderRecipeIngredientDraft(
                        ingredient.IngredientId,
                        100d,
                        ingredient.BaseUnit
                    )
                );
            }
        }

        request.IsPlayerCreated = true;
        return request;
    }

    public BistroBuilderDishRecipeAuthoringResult TryCreateOrUpdate(
        BistroBuilderDishRecipeAuthoringRequest source
    )
    {
        if (!EnsureSession(out string error))
        {
            return BistroBuilderDishRecipeAuthoringResult.Failure(error);
        }

        if (source == null)
        {
            return BistroBuilderDishRecipeAuthoringResult.Failure(
                "El formulario de plato es nulo."
            );
        }

        BistroBuilderDishRecipeAuthoringRequest request = source.Clone();
        bool creating = string.IsNullOrWhiteSpace(request.DishId);

        if (creating)
        {
            request.DishId = GenerateUniqueDishId(request.DisplayName);
            request.IsPlayerCreated = true;
        }
        else
        {
            request.DishId = BistroBuilderMenuIdUtility.NormalizeStableId(
                request.DishId
            );
        }

        if (!TryBuildPreview(
                request,
                out BistroBuilderDishDefinition previewDish,
                out BistroBuilderRecipeDefinition previewRecipe,
                out error
            ))
        {
            return BistroBuilderDishRecipeAuthoringResult.Failure(error);
        }

        if (creating)
        {
            BistroBuilderMenuMutationResult addResult =
                editSessionService.TryAddDishDefinition(previewDish);

            if (!addResult.Succeeded)
            {
                DestroyRuntimeObject(previewRecipe);
                DestroyRuntimeObject(previewDish);
                return BistroBuilderDishRecipeAuthoringResult.Failure(
                    addResult.Message
                );
            }
        }
        else if (editSessionService.TryGetDraftItem(
                     request.DishId,
                     out _
                 ))
        {
            BistroBuilderMenuMutationResult updateResult =
                editSessionService.TrySetCommercialAndPreparation(
                    request.DishId,
                    request.BasePriceCents,
                    request.PreparationDifficulty,
                    request.BasePreparationSeconds
                );

            if (!updateResult.Succeeded &&
                updateResult.FailureReason !=
                    BistroBuilderMenuMutationFailureReason.NoChange)
            {
                DestroyRuntimeObject(previewRecipe);
                DestroyRuntimeObject(previewDish);
                return BistroBuilderDishRecipeAuthoringResult.Failure(
                    updateResult.Message
                );
            }
        }

        ReplaceDraft(
            request.DishId,
            request,
            previewDish,
            previewRecipe
        );
        draftChangeCount++;
        DraftChanged?.Invoke(request.DishId);

        if (logChanges)
        {
            Debug.Log(
                (creating ? "Plato creado" : "Plato actualizado") +
                " en borrador: " + request.DishId + ".",
                this
            );
        }

        return new BistroBuilderDishRecipeAuthoringResult(
            true,
            true,
            request.DishId,
            creating
                ? "Plato y receta creados en el borrador."
                : "Plato y receta actualizados en el borrador."
        );
    }

    public bool TryResolveDraftDefinition(
        string dishId,
        out BistroBuilderDishDefinition definition
    )
    {
        definition = null;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            dishId
        );

        if (sessionOpen &&
            previewDishById.TryGetValue(normalized, out definition))
        {
            return definition != null;
        }

        return dishCatalogService != null &&
               dishCatalogService.TryGetDefinition(normalized, out definition);
    }

    public bool TryResolveDraftRecipe(
        string dishId,
        out BistroBuilderRecipeDefinition recipe
    )
    {
        recipe = null;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            dishId
        );

        if (sessionOpen &&
            previewRecipeByDishId.TryGetValue(normalized, out recipe))
        {
            return recipe != null;
        }

        return recipeCatalogService != null &&
               recipeCatalogService.TryGetRecipeByDishId(
                   normalized,
                   out recipe
               );
    }

    public void CopyDraftDefinitionsTo(
        List<BistroBuilderDishDefinition> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        dishCatalogService.CopyDefinitionsTo(destination);
        HashSet<string> copied = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < destination.Count; index++)
        {
            BistroBuilderDishDefinition definition = destination[index];

            if (definition == null)
            {
                continue;
            }

            copied.Add(definition.DishId);

            if (sessionOpen &&
                previewDishById.TryGetValue(
                    definition.DishId,
                    out BistroBuilderDishDefinition preview
                ))
            {
                destination[index] = preview;
            }
        }

        if (!sessionOpen)
        {
            return;
        }

        foreach (KeyValuePair<string, BistroBuilderDishDefinition> pair in
                 previewDishById)
        {
            if (pair.Value != null && copied.Add(pair.Key))
            {
                destination.Add(pair.Value);
            }
        }
    }

    public void CopyIngredientOptionsTo(
        List<BistroBuilderIngredientOptionSnapshot> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        recipeCatalogService.CopyIngredientsTo(ingredientBuffer);
        ingredientBuffer.Sort(CompareIngredients);

        for (int index = 0; index < ingredientBuffer.Count; index++)
        {
            BistroBuilderIngredientDefinition ingredient =
                ingredientBuffer[index];

            if (ingredient != null)
            {
                destination.Add(
                    new BistroBuilderIngredientOptionSnapshot(
                        ingredient.IngredientId,
                        ingredient.DisplayName,
                        ingredient.BaseUnit
                    )
                );
            }
        }
    }

    public bool TryApplyRuntime(
        out RollbackState rollback,
        out string error
    )
    {
        rollback = null;

        if (!EnsureSession(out error))
        {
            return false;
        }

        if (!HasPendingChanges)
        {
            rollback = new RollbackState();
            runtimeChangesAwaitingPublish = false;
            error = string.Empty;
            return true;
        }

        rollback = new RollbackState();
        dishCatalogService.CopyRuntimeDefinitionsTo(
            rollback.PreviousDishes
        );
        recipeCatalogService.CopyRuntimeRecipesTo(
            rollback.PreviousRecipes
        );

        runtimeDishBuffer.Clear();
        runtimeDishBuffer.AddRange(rollback.PreviousDishes);
        runtimeRecipeBuffer.Clear();
        runtimeRecipeBuffer.AddRange(rollback.PreviousRecipes);

        foreach (KeyValuePair<string, BistroBuilderDishDefinition> pair in
                 previewDishById)
        {
            ReplaceByDishId(runtimeDishBuffer, pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, BistroBuilderRecipeDefinition> pair in
                 previewRecipeByDishId)
        {
            ReplaceRecipeByDishId(runtimeRecipeBuffer, pair.Key, pair.Value);
        }

        if (!dishCatalogService.TryReplaceRuntimeDefinitions(
                runtimeDishBuffer,
                out error,
                false
            ))
        {
            return false;
        }

        if (!recipeCatalogService.TryReplaceRuntimeRecipes(
                runtimeRecipeBuffer,
                out error,
                false
            ))
        {
            dishCatalogService.TryReplaceRuntimeDefinitions(
                rollback.PreviousDishes,
                out _,
                false
            );
            return false;
        }

        rollback.Applied = true;
        runtimeChangesAwaitingPublish = true;
        error = string.Empty;
        return true;
    }

    public void Rollback(RollbackState rollback)
    {
        if (rollback == null || !rollback.Applied)
        {
            return;
        }

        dishCatalogService.TryReplaceRuntimeDefinitions(
            rollback.PreviousDishes,
            out _,
            false
        );
        recipeCatalogService.TryReplaceRuntimeRecipes(
            rollback.PreviousRecipes,
            out _,
            false
        );
        runtimeChangesAwaitingPublish = false;
        rollback.Applied = false;
    }

    public void CompleteCommit()
    {
        if (runtimeChangesAwaitingPublish)
        {
            dishCatalogService.PublishChanged();
            recipeCatalogService.PublishChanged();
        }

        runtimeChangesAwaitingPublish = false;
        ClearDraftObjects(false);
        sessionOpen = false;
        draftChangeCount = 0;
    }

    private bool TryBuildPreview(
        BistroBuilderDishRecipeAuthoringRequest request,
        out BistroBuilderDishDefinition dish,
        out BistroBuilderRecipeDefinition recipe,
        out string error
    )
    {
        dish = null;
        recipe = null;

        request.DisplayName = request.DisplayName != null
            ? request.DisplayName.Trim()
            : string.Empty;
        request.Description = request.Description != null
            ? request.Description.Trim()
            : string.Empty;
        request.CategoryId = BistroBuilderMenuIdUtility.NormalizeStableId(
            request.CategoryId
        );

        if (!BistroBuilderMenuIdUtility.IsValidStableId(request.DishId))
        {
            error = "No se pudo generar un DishId estable.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            error = "El plato necesita un nombre visible.";
            return false;
        }

        if (!categoryCatalogService.TryGetDefinition(
                request.CategoryId,
                out _
            ))
        {
            error = "La categoría seleccionada no está registrada.";
            return false;
        }

        if (!BistroBuilderMenuPolicyEvaluator.TryValidatePrice(
                request.BasePriceCents,
                editSessionService.CommercialPolicy,
                out error
            ))
        {
            return false;
        }

        if (!Enum.IsDefined(typeof(BistroBuilderDishCourse), request.Course) ||
            !Enum.IsDefined(
                typeof(BistroBuilderKitchenStationType),
                request.RequiredStation
            ))
        {
            error = "El plato contiene una clasificación no válida.";
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                request.DefaultAvailability,
                false
            ) ||
            !BistroBuilderServiceModeUtility.IsValidAvailabilityMask(
                request.AllowedServiceModes,
                false
            ))
        {
            error = "El plato debe ofrecer al menos un servicio y una " +
                    "modalidad operativa.";
            return false;
        }

        string recipeId = ResolveRecipeId(request.DishId);
        bool shareable = false;
        int minimumConsumers = 1;
        int maximumConsumers = 1;

        if (dishCatalogService.TryGetDefinition(
                request.DishId,
                out BistroBuilderDishDefinition current
            ) &&
            current != null)
        {
            recipeId = current.RecipeId;
            shareable = current.Shareable;
            minimumConsumers = current.MinimumConsumers;
            maximumConsumers = current.MaximumConsumers;
        }

        dish = BistroBuilderDishDefinition.CreateRuntime(
            request.DishId,
            request.DisplayName,
            request.Description,
            request.CategoryId,
            request.Course,
            request.DefaultAvailability,
            request.AllowedServiceModes,
            request.RequiredStation,
            request.BasePreparationSeconds,
            request.PreparationDifficulty,
            recipeId,
            request.BasePriceCents,
            shareable,
            minimumConsumers,
            maximumConsumers
        );

        if (!dish.TryValidate(out error))
        {
            DestroyRuntimeObject(dish);
            dish = null;
            return false;
        }

        if (request.YieldPortions < 1 ||
            request.YieldPortions >
                BistroBuilderRecipeDefinition.MaximumYieldPortions)
        {
            error = "El rendimiento debe estar entre 1 y " +
                    BistroBuilderRecipeDefinition.MaximumYieldPortions + ".";
            DestroyRuntimeObject(dish);
            dish = null;
            return false;
        }

        if (request.WasteBasisPoints < 0 ||
            request.WasteBasisPoints >
                BistroBuilderRecipeDefinition.MaximumWasteBasisPoints)
        {
            error = "La merma debe estar entre 0 y 100 %.";
            DestroyRuntimeObject(dish);
            dish = null;
            return false;
        }

        if (request.Ingredients == null || request.Ingredients.Count == 0)
        {
            error = "La receta necesita al menos un ingrediente.";
            DestroyRuntimeObject(dish);
            dish = null;
            return false;
        }

        List<BistroBuilderRecipeIngredientAmount> lines =
            new List<BistroBuilderRecipeIngredientAmount>(
                request.Ingredients.Count
            );
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < request.Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientDraft line =
                request.Ingredients[index];

            if (line == null)
            {
                error = "La receta contiene una línea nula.";
                DestroyRuntimeObject(dish);
                dish = null;
                return false;
            }

            string ingredientId =
                BistroBuilderMenuIdUtility.NormalizeStableId(
                    line.IngredientId
                );

            if (!ids.Add(ingredientId))
            {
                error = "El ingrediente " + ingredientId +
                        " está repetido. Agrupa su cantidad en una línea.";
                DestroyRuntimeObject(dish);
                dish = null;
                return false;
            }

            if (!recipeCatalogService.TryGetIngredient(
                    ingredientId,
                    out BistroBuilderIngredientDefinition ingredient
                ) ||
                ingredient == null)
            {
                error = "No existe el ingrediente " + ingredientId + ".";
                DestroyRuntimeObject(dish);
                dish = null;
                return false;
            }

            if (!BistroBuilderMeasurementUtility.AreCompatible(
                    ingredient.BaseUnit,
                    line.Unit
                ) ||
                !BistroBuilderMeasurementUtility.TryConvertToCanonicalMilliUnits(
                    line.Amount,
                    line.Unit,
                    out _,
                    out error
                ))
            {
                DestroyRuntimeObject(dish);
                dish = null;
                return false;
            }

            lines.Add(
                new BistroBuilderRecipeIngredientAmount(
                    ingredient,
                    line.Amount,
                    line.Unit
                )
            );
        }

        recipe = BistroBuilderRecipeDefinition.CreateRuntime(
            recipeId,
            dish,
            request.YieldPortions,
            request.WasteBasisPoints,
            lines,
            request.Notes
        );

        if (!recipe.TryValidate(out error))
        {
            DestroyRuntimeObject(recipe);
            DestroyRuntimeObject(dish);
            recipe = null;
            dish = null;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private BistroBuilderDishRecipeAuthoringRequest BuildRequest(
        BistroBuilderDishDefinition definition,
        BistroBuilderRecipeDefinition recipe
    )
    {
        BistroBuilderDishRecipeAuthoringRequest request =
            new BistroBuilderDishRecipeAuthoringRequest
            {
                DishId = definition.DishId,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                CategoryId = definition.CategoryId,
                Course = definition.Course,
                RequiredStation = definition.RequiredStation,
                DefaultAvailability = definition.DefaultAvailability,
                AllowedServiceModes = definition.AllowedServiceModes,
                BasePriceCents = definition.BasePriceCents,
                PreparationDifficulty = definition.Complexity,
                BasePreparationSeconds = definition.BasePreparationSeconds,
                YieldPortions = recipe.YieldPortions,
                WasteBasisPoints = recipe.WasteBasisPoints,
                Notes = recipe.Notes,
                IsPlayerCreated = dishCatalogService.Catalog == null ||
                    !dishCatalogService.Catalog.Contains(definition.DishId)
            };

        for (int index = 0; index < recipe.Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount line =
                recipe.Ingredients[index];

            if (line != null && line.Ingredient != null)
            {
                request.Ingredients.Add(
                    new BistroBuilderRecipeIngredientDraft(
                        line.Ingredient.IngredientId,
                        line.Amount,
                        line.Unit
                    )
                );
            }
        }

        return request;
    }

    private void ReplaceDraft(
        string dishId,
        BistroBuilderDishRecipeAuthoringRequest request,
        BistroBuilderDishDefinition dish,
        BistroBuilderRecipeDefinition recipe
    )
    {
        if (previewRecipeByDishId.TryGetValue(
                dishId,
                out BistroBuilderRecipeDefinition oldRecipe
            ))
        {
            DestroyRuntimeObject(oldRecipe);
        }

        if (previewDishById.TryGetValue(
                dishId,
                out BistroBuilderDishDefinition oldDish
            ))
        {
            DestroyRuntimeObject(oldDish);
        }

        draftByDishId[dishId] = request.Clone();
        previewDishById[dishId] = dish;
        previewRecipeByDishId[dishId] = recipe;
    }

    private string GenerateUniqueDishId(string displayName)
    {
        string slug = BistroBuilderMenuIdUtility.NormalizeStableId(
            displayName
        );

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "nuevo_plato";
        }

        string baseId = slug.StartsWith("dish_", StringComparison.Ordinal)
            ? slug
            : "dish_" + slug;
        string candidate = baseId;
        int suffix = 2;

        while (dishCatalogService.Contains(candidate) ||
               draftByDishId.ContainsKey(candidate))
        {
            candidate = baseId + "_" + suffix;
            suffix++;
        }

        return candidate;
    }

    private static string ResolveRecipeId(string dishId)
    {
        string suffix = dishId.StartsWith("dish_", StringComparison.Ordinal)
            ? dishId.Substring(5)
            : dishId;
        return BistroBuilderMenuIdUtility.NormalizeStableId(
            "recipe_" + suffix
        );
    }

    private bool EnsureSession(out string error)
    {
        if (sessionOpen && editSessionService != null &&
            editSessionService.HasOpenSession)
        {
            error = string.Empty;
            return true;
        }

        error = "No existe una sesión de autoría abierta.";
        return false;
    }

    private void ClearDraftObjects(bool destroyObjects = true)
    {
        if (destroyObjects)
        {
            foreach (BistroBuilderRecipeDefinition recipe in
                     previewRecipeByDishId.Values)
            {
                DestroyRuntimeObject(recipe);
            }

            foreach (BistroBuilderDishDefinition dish in
                     previewDishById.Values)
            {
                DestroyRuntimeObject(dish);
            }
        }

        draftByDishId.Clear();
        previewDishById.Clear();
        previewRecipeByDishId.Clear();
    }

    private void ResolveDependencies()
    {
        if (dishCatalogService == null)
        {
            TryGetComponent(out dishCatalogService);
        }

        if (recipeCatalogService == null)
        {
            TryGetComponent(out recipeCatalogService);
        }

        if (categoryCatalogService == null)
        {
            TryGetComponent(out categoryCatalogService);
        }

        if (editSessionService == null)
        {
            TryGetComponent(out editSessionService);
        }
    }

    private static void ReplaceByDishId(
        List<BistroBuilderDishDefinition> target,
        string dishId,
        BistroBuilderDishDefinition replacement
    )
    {
        for (int index = 0; index < target.Count; index++)
        {
            if (target[index] != null &&
                string.Equals(
                    target[index].DishId,
                    dishId,
                    StringComparison.Ordinal
                ))
            {
                target[index] = replacement;
                return;
            }
        }

        target.Add(replacement);
    }

    private static void ReplaceRecipeByDishId(
        List<BistroBuilderRecipeDefinition> target,
        string dishId,
        BistroBuilderRecipeDefinition replacement
    )
    {
        for (int index = 0; index < target.Count; index++)
        {
            if (target[index] != null &&
                string.Equals(
                    target[index].DishId,
                    dishId,
                    StringComparison.Ordinal
                ))
            {
                target[index] = replacement;
                return;
            }
        }

        target.Add(replacement);
    }

    private static int CompareIngredients(
        BistroBuilderIngredientDefinition first,
        BistroBuilderIngredientDefinition second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        return string.Compare(
            first.DisplayName,
            second.DisplayName,
            StringComparison.CurrentCultureIgnoreCase
        );
    }

    private static void DestroyRuntimeObject(UnityEngine.Object value)
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

    public sealed class RollbackState
    {
        internal readonly List<BistroBuilderDishDefinition> PreviousDishes =
            new List<BistroBuilderDishDefinition>(16);
        internal readonly List<BistroBuilderRecipeDefinition> PreviousRecipes =
            new List<BistroBuilderRecipeDefinition>(16);
        internal bool Applied;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }
#endif
}
