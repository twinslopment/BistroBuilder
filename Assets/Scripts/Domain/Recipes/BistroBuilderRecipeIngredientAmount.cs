using System;
using UnityEngine;

/// <summary>
/// Una línea de ingrediente dentro de una receta.
/// La cantidad visible conserva la unidad elegida por autoría; el runtime
/// la convierte a milésimas de unidad base antes de operar.
/// </summary>
[Serializable]
public sealed class BistroBuilderRecipeIngredientAmount
{
    [SerializeField]
    private BistroBuilderIngredientDefinition ingredient;

    [SerializeField]
    [Min(0.001f)]
    private double amount = 1d;

    [SerializeField]
    private BistroBuilderMeasurementUnit unit =
        BistroBuilderMeasurementUnit.Gram;

    public BistroBuilderIngredientDefinition Ingredient => ingredient;

    public double Amount => amount;

    public BistroBuilderMeasurementUnit Unit => unit;

    public BistroBuilderRecipeIngredientAmount()
    {
    }

    public BistroBuilderRecipeIngredientAmount(
        BistroBuilderIngredientDefinition ingredient,
        double amount,
        BistroBuilderMeasurementUnit unit
    )
    {
        this.ingredient = ingredient;
        this.amount = amount;
        this.unit = unit;
    }

    public BistroBuilderRecipeIngredientAmount Clone()
    {
        return new BistroBuilderRecipeIngredientAmount(
            ingredient,
            amount,
            unit
        );
    }

    public void InitializeRuntime(
        BistroBuilderIngredientDefinition runtimeIngredient,
        double runtimeAmount,
        BistroBuilderMeasurementUnit runtimeUnit
    )
    {
        ingredient = runtimeIngredient;
        amount = runtimeAmount;
        unit = runtimeUnit;
    }

    public bool TryGetCanonicalMilliUnits(
        out long canonicalMilliUnits,
        out string error
    )
    {
        canonicalMilliUnits = 0L;
        error = string.Empty;

        if (ingredient == null)
        {
            error = "La línea de receta no tiene ingrediente.";
            return false;
        }

        if (!ingredient.TryValidate(out error))
        {
            return false;
        }

        if (!BistroBuilderMeasurementUtility.AreCompatible(
                ingredient.BaseUnit,
                unit
            ))
        {
            error = "La cantidad de " + ingredient.IngredientId +
                    " usa una unidad incompatible con su unidad base.";
            return false;
        }

        return BistroBuilderMeasurementUtility
            .TryConvertToCanonicalMilliUnits(
                amount,
                unit,
                out canonicalMilliUnits,
                out error
            );
    }

    public bool TryValidate(out string error)
    {
        return TryGetCanonicalMilliUnits(out _, out error);
    }
}
