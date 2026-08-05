using System;

/// <summary>
/// Indicador simplificado de margen para UI, briefing y finanzas.
/// </summary>
public enum BistroBuilderRecipeMarginBand
{
    Loss = 0,
    Low = 1,
    Correct = 2,
    High = 3,
    Excellent = 4
}

/// <summary>
/// Resultado inmutable de un escandallo por ración.
/// </summary>
public readonly struct BistroBuilderRecipeEconomicsSnapshot
{
    public int SalePriceCents { get; }
    public int CostPerPortionCents { get; }
    public int GrossMarginCents { get; }
    public int GrossMarginBasisPoints { get; }
    public BistroBuilderRecipeMarginBand MarginBand { get; }

    public BistroBuilderRecipeEconomicsSnapshot(
        int salePriceCents,
        int costPerPortionCents,
        int grossMarginCents,
        int grossMarginBasisPoints,
        BistroBuilderRecipeMarginBand marginBand
    )
    {
        SalePriceCents = salePriceCents;
        CostPerPortionCents = costPerPortionCents;
        GrossMarginCents = grossMarginCents;
        GrossMarginBasisPoints = grossMarginBasisPoints;
        MarginBand = marginBand;
    }
}

/// <summary>
/// Cálculos económicos sin estado. El coste de ingrediente se deriva de la
/// receta; el precio de venta procede del plato/carta.
/// </summary>
public static class BistroBuilderRecipeEconomics
{
    public static bool TryBuildSnapshot(
        BistroBuilderDishDefinition dish,
        BistroBuilderRecipeDefinition recipe,
        out BistroBuilderRecipeEconomicsSnapshot snapshot,
        out string error
    )
    {
        return TryBuildSnapshot(
            dish,
            recipe,
            dish != null ? dish.BasePriceCents : 0,
            out snapshot,
            out error
        );
    }

    /// <summary>
    /// Calcula el escandallo con un precio de venta runtime exacto. Permite
    /// que la UI previsualice el margen del borrador sin escribir en la
    /// definición canónica ni duplicar la lógica económica.
    /// </summary>
    public static bool TryBuildSnapshot(
        BistroBuilderDishDefinition dish,
        BistroBuilderRecipeDefinition recipe,
        int salePriceCents,
        out BistroBuilderRecipeEconomicsSnapshot snapshot,
        out string error
    )
    {
        snapshot = default(BistroBuilderRecipeEconomicsSnapshot);
        error = string.Empty;

        if (dish == null)
        {
            error = "Falta la definición de plato.";
            return false;
        }

        if (recipe == null)
        {
            error = "Falta la definición de receta.";
            return false;
        }

        if (salePriceCents < 0 ||
            salePriceCents > BistroBuilderDishDefinition.MaximumPriceCents)
        {
            error = "El precio de venta queda fuera del rango permitido.";
            return false;
        }

        if (!string.Equals(
                dish.DishId,
                recipe.DishId,
                StringComparison.Ordinal
            ))
        {
            error = "El plato y la receta no comparten DishId.";
            return false;
        }

        if (!recipe.TryCalculateCostPerPortionCents(
                out int costCents,
                out error
            ))
        {
            return false;
        }

        int grossMarginCents = salePriceCents - costCents;
        int marginBasisPoints = salePriceCents > 0
            ? (int)decimal.Round(
                (decimal)grossMarginCents * 10000m / salePriceCents,
                0,
                MidpointRounding.AwayFromZero
            )
            : 0;

        snapshot = new BistroBuilderRecipeEconomicsSnapshot(
            salePriceCents,
            costCents,
            grossMarginCents,
            marginBasisPoints,
            ResolveBand(grossMarginCents, marginBasisPoints)
        );

        return true;
    }

    public static BistroBuilderRecipeMarginBand ResolveBand(
        int grossMarginCents,
        int marginBasisPoints
    )
    {
        if (grossMarginCents < 0)
        {
            return BistroBuilderRecipeMarginBand.Loss;
        }

        if (marginBasisPoints < 4500)
        {
            return BistroBuilderRecipeMarginBand.Low;
        }

        if (marginBasisPoints < 6000)
        {
            return BistroBuilderRecipeMarginBand.Correct;
        }

        if (marginBasisPoints < 7500)
        {
            return BistroBuilderRecipeMarginBand.High;
        }

        return BistroBuilderRecipeMarginBand.Excellent;
    }
}
