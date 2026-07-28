using UnityEngine;

/// <summary>
/// Puerta runtime de solo lectura a ingredientes, recetas y escandallos.
/// Centraliza los catálogos y evita búsquedas por AssetDatabase durante la
/// simulación.
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

    public BistroBuilderIngredientCatalog IngredientCatalog =>
        ingredientCatalog;

    public BistroBuilderRecipeCatalog RecipeCatalog => recipeCatalog;

    public BistroBuilderDishCatalogService DishCatalogService =>
        dishCatalogService;

    public int IngredientCount => ingredientCatalog != null
        ? ingredientCatalog.DefinitionCount
        : 0;

    public int RecipeCount => recipeCatalog != null
        ? recipeCatalog.DefinitionCount
        : 0;

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
                " ingrediente(s) y " + RecipeCount +
                " receta(s) canónica(s).",
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

        if (!dishCatalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        var dishes = dishCatalogService.Catalog.Definitions;

        for (int index = 0; index < dishes.Count; index++)
        {
            BistroBuilderDishDefinition dish = dishes[index];

            if (dish == null || string.IsNullOrWhiteSpace(dish.RecipeId))
            {
                error = "El catálogo contiene un plato sin RecipeId.";
                return false;
            }

            if (!recipeCatalog.TryGetByRecipeId(
                    dish.RecipeId,
                    out BistroBuilderRecipeDefinition recipe
                ) ||
                recipe == null ||
                recipe.Dish != dish)
            {
                error = "No existe una receta canónica coherente para " +
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

    public bool TryGetRecipeByDishId(
        string dishId,
        out BistroBuilderRecipeDefinition recipe
    )
    {
        recipe = null;

        return recipeCatalog != null &&
               recipeCatalog.TryGetByDishId(dishId, out recipe);
    }

    public bool TryGetEconomics(
        string dishId,
        out BistroBuilderRecipeEconomicsSnapshot snapshot,
        out string error
    )
    {
        snapshot = default;
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
