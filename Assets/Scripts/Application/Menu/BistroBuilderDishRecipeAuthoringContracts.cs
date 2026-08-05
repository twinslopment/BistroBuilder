using System;
using System.Collections.Generic;

/// <summary>
/// Línea editable de una receta dentro del borrador 2.1G1/2.
/// Mantiene identidad, cantidad y unidad sin exponer referencias mutables
/// del catálogo canónico de ingredientes.
/// </summary>
[Serializable]
public sealed class BistroBuilderRecipeIngredientDraft
{
    public string IngredientId { get; set; } = string.Empty;
    public double Amount { get; set; } = 1d;
    public BistroBuilderMeasurementUnit Unit { get; set; } =
        BistroBuilderMeasurementUnit.Gram;

    public BistroBuilderRecipeIngredientDraft()
    {
    }

    public BistroBuilderRecipeIngredientDraft(
        string ingredientId,
        double amount,
        BistroBuilderMeasurementUnit unit
    )
    {
        IngredientId = BistroBuilderMenuIdUtility.NormalizeStableId(
            ingredientId
        );
        Amount = amount;
        Unit = unit;
    }

    public BistroBuilderRecipeIngredientDraft Clone()
    {
        return new BistroBuilderRecipeIngredientDraft(
            IngredientId,
            Amount,
            Unit
        );
    }
}

/// <summary>
/// Documento mutable que la vista entrega al servicio de autoría.
/// El servicio normaliza, valida y clona todos sus datos antes de aceptar
/// cualquier cambio en el borrador.
/// </summary>
[Serializable]
public sealed class BistroBuilderDishRecipeAuthoringRequest
{
    public string DishId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } =
        BistroBuilderDishCategoryIdUtility.MainCourse;
    public BistroBuilderDishCourse Course { get; set; } =
        BistroBuilderDishCourse.Main;
    public BistroBuilderKitchenStationType RequiredStation { get; set; } =
        BistroBuilderKitchenStationType.HotKitchen;
    public BistroBuilderMealServiceAvailability DefaultAvailability
    {
        get;
        set;
    } = BistroBuilderMealServiceAvailability.Lunch |
        BistroBuilderMealServiceAvailability.Dinner;
    public BistroBuilderDishServiceModeAvailability AllowedServiceModes
    {
        get;
        set;
    } = BistroBuilderDishServiceModeAvailability.All;
    public int BasePriceCents { get; set; } = 1000;
    public int PreparationDifficulty { get; set; } = 3;
    public int BasePreparationSeconds { get; set; } = 300;
    public int YieldPortions { get; set; } = 1;
    public int WasteBasisPoints { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsPlayerCreated { get; set; }

    public List<BistroBuilderRecipeIngredientDraft> Ingredients { get; } =
        new List<BistroBuilderRecipeIngredientDraft>();

    public BistroBuilderDishRecipeAuthoringRequest Clone()
    {
        BistroBuilderDishRecipeAuthoringRequest clone =
            new BistroBuilderDishRecipeAuthoringRequest
            {
                DishId = DishId,
                DisplayName = DisplayName,
                Description = Description,
                CategoryId = CategoryId,
                Course = Course,
                RequiredStation = RequiredStation,
                DefaultAvailability = DefaultAvailability,
                AllowedServiceModes = AllowedServiceModes,
                BasePriceCents = BasePriceCents,
                PreparationDifficulty = PreparationDifficulty,
                BasePreparationSeconds = BasePreparationSeconds,
                YieldPortions = YieldPortions,
                WasteBasisPoints = WasteBasisPoints,
                Notes = Notes,
                IsPlayerCreated = IsPlayerCreated
            };

        for (int index = 0; index < Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientDraft line = Ingredients[index];

            if (line != null)
            {
                clone.Ingredients.Add(line.Clone());
            }
        }

        return clone;
    }
}

/// <summary>
/// Opción de ingrediente preparada para la interfaz runtime.
/// </summary>
public readonly struct BistroBuilderIngredientOptionSnapshot
{
    public string IngredientId { get; }
    public string DisplayName { get; }
    public BistroBuilderMeasurementUnit BaseUnit { get; }

    public BistroBuilderIngredientOptionSnapshot(
        string ingredientId,
        string displayName,
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        IngredientId = ingredientId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        BaseUnit = baseUnit;
    }
}

/// <summary>
/// Resultado tipado de crear o actualizar un plato y su receta.
/// </summary>
public readonly struct BistroBuilderDishRecipeAuthoringResult
{
    public bool Succeeded { get; }
    public bool Changed { get; }
    public string DishId { get; }
    public string Message { get; }

    public BistroBuilderDishRecipeAuthoringResult(
        bool succeeded,
        bool changed,
        string dishId,
        string message
    )
    {
        Succeeded = succeeded;
        Changed = changed;
        DishId = dishId ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public static BistroBuilderDishRecipeAuthoringResult Failure(
        string message
    )
    {
        return new BistroBuilderDishRecipeAuthoringResult(
            false,
            false,
            string.Empty,
            message
        );
    }
}
