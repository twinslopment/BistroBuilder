using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Receta canónica enlazada a un DishId mediante su definición de plato.
///
/// La receta define ingredientes, cantidades, rendimiento y merma. El
/// precio de venta sigue perteneciendo al plato/carta; el coste se deriva
/// de ingredientes y en el futuro podrá usar precios reales de proveedor.
/// </summary>
[CreateAssetMenu(
    fileName = "RecipeDefinition",
    menuName = "Bistro Builder/Recipes/Recipe Definition",
    order = 210
)]
public sealed class BistroBuilderRecipeDefinition : ScriptableObject
{
    public const int MaximumYieldPortions = 10000;
    public const int MaximumWasteBasisPoints = 10000;
    public const int BasisPointsScale = 10000;

    [Header("Identidad estable")]

    [SerializeField]
    private string recipeId = string.Empty;

    [SerializeField]
    private BistroBuilderDishDefinition dish;

    [Header("Rendimiento")]

    [SerializeField]
    [Min(1)]
    private int yieldPortions = 1;

    [Tooltip(
        "Merma en puntos básicos. 500 equivale a 5 %. Se aplica al coste " +
        "de ingredientes antes de dividir entre raciones."
    )]
    [SerializeField]
    [Range(0, MaximumWasteBasisPoints)]
    private int wasteBasisPoints;

    [Header("Ingredientes")]

    [SerializeField]
    private List<BistroBuilderRecipeIngredientAmount> ingredients =
        new List<BistroBuilderRecipeIngredientAmount>();

    [Header("Autoría")]

    [TextArea(2, 5)]
    [SerializeField]
    private string notes = string.Empty;

    public string RecipeId => recipeId;

    public BistroBuilderDishDefinition Dish => dish;

    public string DishId => dish != null ? dish.DishId : string.Empty;

    public int YieldPortions => yieldPortions;

    public int WasteBasisPoints => wasteBasisPoints;

    public IReadOnlyList<BistroBuilderRecipeIngredientAmount> Ingredients =>
        ingredients;

    public string Notes => notes;

    /// <summary>
    /// Crea una receta runtime sin alterar los assets canónicos.
    /// </summary>
    public static BistroBuilderRecipeDefinition CreateRuntime(
        string recipeId,
        BistroBuilderDishDefinition dish,
        int yieldPortions,
        int wasteBasisPoints,
        IList<BistroBuilderRecipeIngredientAmount> ingredients,
        string notes
    )
    {
        BistroBuilderRecipeDefinition recipe =
            CreateInstance<BistroBuilderRecipeDefinition>();
        recipe.hideFlags = HideFlags.DontSave;
        recipe.InitializeRuntime(
            recipeId,
            dish,
            yieldPortions,
            wasteBasisPoints,
            ingredients,
            notes
        );
        return recipe;
    }

    public void InitializeRuntime(
        string runtimeRecipeId,
        BistroBuilderDishDefinition runtimeDish,
        int runtimeYieldPortions,
        int runtimeWasteBasisPoints,
        IList<BistroBuilderRecipeIngredientAmount> runtimeIngredients,
        string runtimeNotes
    )
    {
        recipeId = BistroBuilderMenuIdUtility.NormalizeStableId(
            runtimeRecipeId
        );
        dish = runtimeDish;
        yieldPortions = runtimeYieldPortions;
        wasteBasisPoints = runtimeWasteBasisPoints;
        notes = runtimeNotes != null ? runtimeNotes.Trim() : string.Empty;
        ingredients = new List<BistroBuilderRecipeIngredientAmount>();

        if (runtimeIngredients == null)
        {
            return;
        }

        for (int index = 0; index < runtimeIngredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount line =
                runtimeIngredients[index];

            if (line != null)
            {
                ingredients.Add(line.Clone());
            }
        }
    }

    public BistroBuilderRecipeDefinition CloneRuntime(
        BistroBuilderDishDefinition runtimeDish
    )
    {
        return CreateRuntime(
            RecipeId,
            runtimeDish,
            YieldPortions,
            WasteBasisPoints,
            ingredients,
            Notes
        );
    }

    public bool TryCalculateCostPerPortionMicroCents(
        out long costPerPortionMicroCents,
        out string error
    )
    {
        costPerPortionMicroCents = 0L;
        error = string.Empty;

        if (yieldPortions < 1 || yieldPortions > MaximumYieldPortions)
        {
            error = "El rendimiento de " + recipeId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (wasteBasisPoints < 0 ||
            wasteBasisPoints > MaximumWasteBasisPoints)
        {
            error = "La merma de " + recipeId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (ingredients == null || ingredients.Count == 0)
        {
            error = "La receta " + recipeId +
                    " no contiene ingredientes.";
            return false;
        }

        decimal totalMicroCents = 0m;

        for (int index = 0; index < ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount line = ingredients[index];

            if (line == null)
            {
                error = "La receta " + recipeId +
                        " contiene una línea nula en la posición " +
                        index + ".";
                return false;
            }

            if (!line.TryGetCanonicalMilliUnits(
                    out long canonicalMilliUnits,
                    out error
                ))
            {
                error = "Receta " + recipeId + ": " + error;
                return false;
            }

            if (!line.Ingredient.TryCalculateCostMicroCents(
                    canonicalMilliUnits,
                    out long lineCostMicroCents,
                    out error
                ))
            {
                error = "Receta " + recipeId + ": " + error;
                return false;
            }

            totalMicroCents += lineCostMicroCents;
        }

        decimal withWaste =
            totalMicroCents *
            (BasisPointsScale + wasteBasisPoints) /
            BasisPointsScale;

        decimal perPortion = withWaste / yieldPortions;
        decimal rounded = decimal.Round(
            perPortion,
            0,
            MidpointRounding.AwayFromZero
        );

        if (rounded < 0m || rounded > long.MaxValue)
        {
            error = "El coste de " + recipeId +
                    " queda fuera del rango permitido.";
            return false;
        }

        costPerPortionMicroCents = (long)rounded;
        return true;
    }

    public bool TryCalculateCostPerPortionCents(
        out int costPerPortionCents,
        out string error
    )
    {
        costPerPortionCents = 0;

        if (!TryCalculateCostPerPortionMicroCents(
                out long microCents,
                out error
            ))
        {
            return false;
        }

        decimal cents =
            (decimal)microCents /
            BistroBuilderIngredientDefinition.MicroCentsPerCent;

        decimal rounded = decimal.Round(
            cents,
            0,
            MidpointRounding.AwayFromZero
        );

        if (rounded < 0m || rounded > int.MaxValue)
        {
            error = "El coste en céntimos de " + recipeId +
                    " queda fuera de rango.";
            return false;
        }

        costPerPortionCents = (int)rounded;
        return true;
    }

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(recipeId))
        {
            error = "El RecipeId '" + recipeId +
                    "' no es estable o válido.";
            return false;
        }

        if (dish == null)
        {
            error = "La receta " + recipeId +
                    " no referencia un plato canónico.";
            return false;
        }

        if (!dish.TryValidate(out error))
        {
            return false;
        }

        if (!string.Equals(
                dish.RecipeId,
                recipeId,
                StringComparison.Ordinal
            ))
        {
            error = "La receta " + recipeId +
                    " no coincide con el RecipeId declarado por " +
                    dish.DishId + ".";
            return false;
        }

        if (yieldPortions < 1 || yieldPortions > MaximumYieldPortions)
        {
            error = "El rendimiento de " + recipeId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (wasteBasisPoints < 0 ||
            wasteBasisPoints > MaximumWasteBasisPoints)
        {
            error = "La merma de " + recipeId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (ingredients == null || ingredients.Count == 0)
        {
            error = "La receta " + recipeId +
                    " debe contener al menos un ingrediente.";
            return false;
        }

        HashSet<string> ingredientIds = new HashSet<string>(
            StringComparer.Ordinal
        );

        for (int index = 0; index < ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount line = ingredients[index];

            if (line == null)
            {
                error = "La receta " + recipeId +
                        " contiene una línea nula en la posición " +
                        index + ".";
                return false;
            }

            if (!line.TryValidate(out error))
            {
                error = "Receta " + recipeId + ": " + error;
                return false;
            }

            string ingredientId = line.Ingredient.IngredientId;

            if (!ingredientIds.Add(ingredientId))
            {
                error = "La receta " + recipeId +
                        " repite el ingrediente " + ingredientId +
                        ". Debe agruparse en una única línea.";
                return false;
            }
        }

        if (!TryCalculateCostPerPortionMicroCents(out _, out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        recipeId = BistroBuilderMenuIdUtility.NormalizeStableId(recipeId);
        yieldPortions = Mathf.Clamp(
            yieldPortions,
            1,
            MaximumYieldPortions
        );
        wasteBasisPoints = Mathf.Clamp(
            wasteBasisPoints,
            0,
            MaximumWasteBasisPoints
        );
        notes = notes != null ? notes.Trim() : string.Empty;

        if (ingredients == null)
        {
            ingredients = new List<BistroBuilderRecipeIngredientAmount>();
        }
    }
#endif
}
