using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puerta runtime a ingredientes, recetas y escandallos.
///
/// Los ingredientes continúan siendo canónicos en 2.1G1/2. Las recetas
/// admiten una capa runtime para sobrescribir recetas existentes y registrar
/// las de platos creados por el jugador. Su persistencia llegará en 2.1G3.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Inventory/Recipe Catalog Service"
)]
public sealed class BistroBuilderRecipeCatalogService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderIngredientCatalog ingredientCatalog;

    [SerializeField]
    private BistroBuilderRecipeCatalog recipeCatalog;

    [SerializeField]
    private BistroBuilderDishCatalogService dishCatalogService;

    [Header("Depuración")]

    [SerializeField]
    private bool logInitialization = true;

    private readonly List<BistroBuilderRecipeDefinition> runtimeRecipes =
        new List<BistroBuilderRecipeDefinition>(16);

    private readonly Dictionary<string, BistroBuilderRecipeDefinition>
        runtimeByDishId =
            new Dictionary<string, BistroBuilderRecipeDefinition>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, BistroBuilderRecipeDefinition>
        runtimeByRecipeId =
            new Dictionary<string, BistroBuilderRecipeDefinition>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderDishDefinition> dishBuffer =
        new List<BistroBuilderDishDefinition>(48);

    public event Action CatalogChanged;

    public BistroBuilderIngredientCatalog IngredientCatalog =>
        ingredientCatalog;

    public BistroBuilderRecipeCatalog RecipeCatalog => recipeCatalog;

    public BistroBuilderDishCatalogService DishCatalogService =>
        dishCatalogService;

    public int IngredientCount => ingredientCatalog != null
        ? ingredientCatalog.DefinitionCount
        : 0;

    public int CanonicalRecipeCount => recipeCatalog != null
        ? recipeCatalog.DefinitionCount
        : 0;

    public int RuntimeRecipeCount => runtimeRecipes.Count;

    public int RecipeCount
    {
        get
        {
            int count = CanonicalRecipeCount;

            for (int index = 0; index < runtimeRecipes.Count; index++)
            {
                BistroBuilderRecipeDefinition recipe = runtimeRecipes[index];

                if (recipe != null &&
                    (recipeCatalog == null ||
                     !recipeCatalog.TryGetByDishId(recipe.DishId, out _)))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int Revision { get; private set; }

    private void Awake()
    {
        CacheDependenciesIfNeeded();

        if (!RebuildIndexes(out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        if (logInitialization)
        {
            Debug.Log(
                nameof(BistroBuilderRecipeCatalogService) +
                " ha cargado " + IngredientCount +
                " ingrediente(s), " + CanonicalRecipeCount +
                " receta(s) canónica(s) y " + RuntimeRecipeCount +
                " receta(s) runtime.",
                this
            );
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (ingredientCatalog == null)
        {
            error = "Falta BistroBuilderIngredientCatalog.";
            return false;
        }

        if (!ingredientCatalog.TryRebuildIndex(out error))
        {
            return false;
        }

        if (ingredientCatalog.DefinitionCount == 0)
        {
            error = "El catálogo canónico de ingredientes está vacío.";
            return false;
        }

        if (recipeCatalog == null)
        {
            error = "Falta BistroBuilderRecipeCatalog.";
            return false;
        }

        if (!recipeCatalog.TryRebuildIndex(out error))
        {
            return false;
        }

        if (recipeCatalog.DefinitionCount == 0)
        {
            error = "El catálogo canónico de recetas está vacío.";
            return false;
        }

        if (dishCatalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (!dishCatalogService.ValidateConfiguration(out error) ||
            !TryRebuildRuntimeIndexes(out error))
        {
            return false;
        }

        dishCatalogService.CopyDefinitionsTo(dishBuffer);

        for (int index = 0; index < dishBuffer.Count; index++)
        {
            BistroBuilderDishDefinition dish = dishBuffer[index];

            if (dish == null || string.IsNullOrWhiteSpace(dish.RecipeId))
            {
                error = "El catálogo contiene un plato sin RecipeId.";
                return false;
            }

            if (!TryGetRecipeByDishId(
                    dish.DishId,
                    out BistroBuilderRecipeDefinition recipe
                ) ||
                recipe == null ||
                !string.Equals(
                    recipe.RecipeId,
                    dish.RecipeId,
                    StringComparison.Ordinal
                ) ||
                !ReferenceEquals(recipe.Dish, dish))
            {
                error = "No existe una receta coherente para " +
                        dish.DishId + ".";
                return false;
            }
        }

        return true;
    }

    public bool RebuildIndexes(out string error)
    {
        return ValidateConfiguration(out error);
    }

    public bool TryGetIngredient(
        string ingredientId,
        out BistroBuilderIngredientDefinition ingredient
    )
    {
        ingredient = null;

        return ingredientCatalog != null &&
               ingredientCatalog.TryGetDefinition(
                   ingredientId,
                   out ingredient
               );
    }

    public void CopyIngredientsTo(
        List<BistroBuilderIngredientDefinition> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (ingredientCatalog == null)
        {
            destination.Clear();
            return;
        }

        ingredientCatalog.CopyDefinitionsTo(destination);
    }

    public bool TryGetRecipeByDishId(
        string dishId,
        out BistroBuilderRecipeDefinition recipe
    )
    {
        recipe = null;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            dishId
        );

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (runtimeByDishId.TryGetValue(normalized, out recipe))
        {
            return recipe != null;
        }

        return recipeCatalog != null &&
               recipeCatalog.TryGetByDishId(normalized, out recipe);
    }

    public bool TryGetRecipeByRecipeId(
        string recipeId,
        out BistroBuilderRecipeDefinition recipe
    )
    {
        recipe = null;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            recipeId
        );

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (runtimeByRecipeId.TryGetValue(normalized, out recipe))
        {
            return recipe != null;
        }

        return recipeCatalog != null &&
               recipeCatalog.TryGetByRecipeId(normalized, out recipe);
    }

    public void CopyRuntimeRecipesTo(
        List<BistroBuilderRecipeDefinition> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();

        for (int index = 0; index < runtimeRecipes.Count; index++)
        {
            BistroBuilderRecipeDefinition recipe = runtimeRecipes[index];

            if (recipe != null)
            {
                destination.Add(recipe);
            }
        }
    }

    public bool TryReplaceRuntimeRecipes(
        IList<BistroBuilderRecipeDefinition> recipes,
        out string error,
        bool publishChange = true
    )
    {
        Dictionary<string, BistroBuilderRecipeDefinition> nextByDish =
            new Dictionary<string, BistroBuilderRecipeDefinition>(
                StringComparer.Ordinal
            );
        Dictionary<string, BistroBuilderRecipeDefinition> nextByRecipe =
            new Dictionary<string, BistroBuilderRecipeDefinition>(
                StringComparer.Ordinal
            );
        List<BistroBuilderRecipeDefinition> nextList =
            new List<BistroBuilderRecipeDefinition>(
                recipes != null ? recipes.Count : 0
            );

        if (recipes != null)
        {
            for (int index = 0; index < recipes.Count; index++)
            {
                BistroBuilderRecipeDefinition recipe = recipes[index];

                if (recipe == null)
                {
                    error = "La capa runtime contiene una receta nula.";
                    return false;
                }

                if (!recipe.TryValidate(out error) ||
                    !TryValidateCanonicalIngredients(recipe, out error))
                {
                    return false;
                }

                if (!dishCatalogService.TryGetDefinition(
                        recipe.DishId,
                        out BistroBuilderDishDefinition effectiveDish
                    ) ||
                    !ReferenceEquals(effectiveDish, recipe.Dish))
                {
                    error = "La receta runtime " + recipe.RecipeId +
                            " no referencia la definición efectiva de " +
                            recipe.DishId + ".";
                    return false;
                }

                if (nextByDish.ContainsKey(recipe.DishId))
                {
                    error = "La capa runtime repite la receta de " +
                            recipe.DishId + ".";
                    return false;
                }

                if (nextByRecipe.ContainsKey(recipe.RecipeId))
                {
                    error = "La capa runtime repite el RecipeId " +
                            recipe.RecipeId + ".";
                    return false;
                }

                nextByDish.Add(recipe.DishId, recipe);
                nextByRecipe.Add(recipe.RecipeId, recipe);
                nextList.Add(recipe);
            }
        }

        runtimeRecipes.Clear();
        runtimeRecipes.AddRange(nextList);
        runtimeByDishId.Clear();
        runtimeByRecipeId.Clear();

        foreach (KeyValuePair<string, BistroBuilderRecipeDefinition> pair in nextByDish)
        {
            runtimeByDishId.Add(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, BistroBuilderRecipeDefinition> pair in nextByRecipe)
        {
            runtimeByRecipeId.Add(pair.Key, pair.Value);
        }

        if (publishChange)
        {
            PublishChanged();
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetEconomics(
        string dishId,
        out BistroBuilderRecipeEconomicsSnapshot snapshot,
        out string error
    )
    {
        snapshot = default(BistroBuilderRecipeEconomicsSnapshot);
        error = string.Empty;

        if (dishCatalogService == null ||
            !dishCatalogService.TryGetDefinition(
                dishId,
                out BistroBuilderDishDefinition dish
            ))
        {
            error = "No existe el plato " + dishId + ".";
            return false;
        }

        if (!TryGetRecipeByDishId(
                dishId,
                out BistroBuilderRecipeDefinition recipe
            ))
        {
            error = "No existe receta para " + dishId + ".";
            return false;
        }

        return BistroBuilderRecipeEconomics.TryBuildSnapshot(
            dish,
            recipe,
            out snapshot,
            out error
        );
    }

    public void PublishChanged()
    {
        Revision++;
        CatalogChanged?.Invoke();
    }

    private bool TryRebuildRuntimeIndexes(out string error)
    {
        error = string.Empty;
        runtimeByDishId.Clear();
        runtimeByRecipeId.Clear();

        for (int index = 0; index < runtimeRecipes.Count; index++)
        {
            BistroBuilderRecipeDefinition recipe = runtimeRecipes[index];

            if (recipe == null)
            {
                error = "La capa runtime contiene una receta nula.";
                return false;
            }

            if (!recipe.TryValidate(out error))
            {
                return false;
            }

            if (!TryValidateCanonicalIngredients(recipe, out error))
            {
                return false;
            }

            if (runtimeByDishId.ContainsKey(recipe.DishId) ||
                runtimeByRecipeId.ContainsKey(recipe.RecipeId))
            {
                error = "La capa runtime contiene identidades de receta " +
                        "duplicadas.";
                return false;
            }

            runtimeByDishId.Add(recipe.DishId, recipe);
            runtimeByRecipeId.Add(recipe.RecipeId, recipe);
        }

        error = string.Empty;
        return true;
    }


    private bool TryValidateCanonicalIngredients(
        BistroBuilderRecipeDefinition recipe,
        out string error
    )
    {
        if (recipe == null)
        {
            error = "No puede validarse una receta nula.";
            return false;
        }

        for (int index = 0; index < recipe.Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount line =
                recipe.Ingredients[index];

            if (line == null || line.Ingredient == null ||
                !TryGetIngredient(
                    line.Ingredient.IngredientId,
                    out BistroBuilderIngredientDefinition canonical
                ) ||
                !ReferenceEquals(canonical, line.Ingredient))
            {
                error = "La receta runtime " + recipe.RecipeId +
                        " contiene un ingrediente ajeno al catálogo canónico.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (dishCatalogService == null)
        {
            TryGetComponent(out dishCatalogService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
