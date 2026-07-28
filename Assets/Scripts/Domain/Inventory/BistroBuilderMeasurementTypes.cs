using System;

/// <summary>
/// Dimensión física utilizada para impedir conversiones incoherentes.
/// </summary>
public enum BistroBuilderMeasurementDimension
{
    Mass = 0,
    Volume = 1,
    Count = 2,
    Portion = 3
}

/// <summary>
/// Unidades admitidas por recetas, inventario y proveedores.
///
/// El runtime normaliza todas las cantidades a una unidad canónica de alta
/// precisión:
/// - masa: miligramos lógicos (milésimas de gramo),
/// - volumen: microlitros lógicos (milésimas de mililitro),
/// - conteo: milésimas de unidad,
/// - porciones: milésimas de porción.
///
/// No se usa float para el estado autoritativo de inventario.
/// </summary>
public enum BistroBuilderMeasurementUnit
{
    Gram = 0,
    Kilogram = 1,
    Milliliter = 2,
    Liter = 3,
    Unit = 4,
    Portion = 5
}

/// <summary>
/// Conversión determinista de unidades de autoría a cantidades canónicas.
/// </summary>
public static class BistroBuilderMeasurementUtility
{
    public const long MilliUnitsPerCanonicalUnit = 1000L;

    public const long MaximumCanonicalMilliUnits =
        9000000000000000L;

    public static bool IsDefined(BistroBuilderMeasurementUnit unit)
    {
        switch (unit)
        {
            case BistroBuilderMeasurementUnit.Gram:
            case BistroBuilderMeasurementUnit.Kilogram:
            case BistroBuilderMeasurementUnit.Milliliter:
            case BistroBuilderMeasurementUnit.Liter:
            case BistroBuilderMeasurementUnit.Unit:
            case BistroBuilderMeasurementUnit.Portion:
                return true;

            default:
                return false;
        }
    }

    public static bool TryGetDimension(
        BistroBuilderMeasurementUnit unit,
        out BistroBuilderMeasurementDimension dimension
    )
    {
        switch (unit)
        {
            case BistroBuilderMeasurementUnit.Gram:
            case BistroBuilderMeasurementUnit.Kilogram:
                dimension = BistroBuilderMeasurementDimension.Mass;
                return true;

            case BistroBuilderMeasurementUnit.Milliliter:
            case BistroBuilderMeasurementUnit.Liter:
                dimension = BistroBuilderMeasurementDimension.Volume;
                return true;

            case BistroBuilderMeasurementUnit.Unit:
                dimension = BistroBuilderMeasurementDimension.Count;
                return true;

            case BistroBuilderMeasurementUnit.Portion:
                dimension = BistroBuilderMeasurementDimension.Portion;
                return true;

            default:
                dimension = default;
                return false;
        }
    }

    public static bool IsCanonicalBaseUnit(
        BistroBuilderMeasurementUnit unit
    )
    {
        return unit == BistroBuilderMeasurementUnit.Gram ||
               unit == BistroBuilderMeasurementUnit.Milliliter ||
               unit == BistroBuilderMeasurementUnit.Unit ||
               unit == BistroBuilderMeasurementUnit.Portion;
    }

    public static bool AreCompatible(
        BistroBuilderMeasurementUnit first,
        BistroBuilderMeasurementUnit second
    )
    {
        return TryGetDimension(first, out var firstDimension) &&
               TryGetDimension(second, out var secondDimension) &&
               firstDimension == secondDimension;
    }

    /// <summary>
    /// Convierte una cantidad visible a milésimas de la unidad base de su
    /// dimensión. Rechaza NaN, infinito, cero y desbordamientos.
    /// </summary>
    public static bool TryConvertToCanonicalMilliUnits(
        double amount,
        BistroBuilderMeasurementUnit unit,
        out long canonicalMilliUnits,
        out string error
    )
    {
        canonicalMilliUnits = 0L;
        error = string.Empty;

        if (!IsDefined(unit))
        {
            error = "La unidad de medida no está definida.";
            return false;
        }

        if (double.IsNaN(amount) ||
            double.IsInfinity(amount) ||
            amount <= 0d)
        {
            error = "La cantidad debe ser un número finito mayor que cero.";
            return false;
        }

        decimal unitMultiplier;

        switch (unit)
        {
            case BistroBuilderMeasurementUnit.Kilogram:
            case BistroBuilderMeasurementUnit.Liter:
                unitMultiplier = 1000m;
                break;

            default:
                unitMultiplier = 1m;
                break;
        }

        decimal raw;

        try
        {
            raw = (decimal)amount *
                  unitMultiplier *
                  MilliUnitsPerCanonicalUnit;
        }
        catch (OverflowException)
        {
            error = "La cantidad excede el rango permitido.";
            return false;
        }

        decimal rounded = decimal.Round(
            raw,
            0,
            MidpointRounding.AwayFromZero
        );

        if (rounded < 1m ||
            rounded > MaximumCanonicalMilliUnits)
        {
            error = "La cantidad queda fuera del rango permitido.";
            return false;
        }

        canonicalMilliUnits = (long)rounded;
        return true;
    }

    public static double ConvertCanonicalMilliUnitsToDisplayAmount(
        long canonicalMilliUnits,
        BistroBuilderMeasurementUnit unit
    )
    {
        if (canonicalMilliUnits <= 0L || !IsDefined(unit))
        {
            return 0d;
        }

        decimal amount =
            (decimal)canonicalMilliUnits /
            MilliUnitsPerCanonicalUnit;

        if (unit == BistroBuilderMeasurementUnit.Kilogram ||
            unit == BistroBuilderMeasurementUnit.Liter)
        {
            amount /= 1000m;
        }

        return (double)amount;
    }

    public static string GetSymbol(BistroBuilderMeasurementUnit unit)
    {
        switch (unit)
        {
            case BistroBuilderMeasurementUnit.Gram:
                return "g";
            case BistroBuilderMeasurementUnit.Kilogram:
                return "kg";
            case BistroBuilderMeasurementUnit.Milliliter:
                return "ml";
            case BistroBuilderMeasurementUnit.Liter:
                return "l";
            case BistroBuilderMeasurementUnit.Unit:
                return "ud";
            case BistroBuilderMeasurementUnit.Portion:
                return "ración";
            default:
                return "?";
        }
    }
}
