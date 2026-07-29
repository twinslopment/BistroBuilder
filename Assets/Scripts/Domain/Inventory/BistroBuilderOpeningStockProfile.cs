using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Línea de stock inicial de autoría.
///
/// La cantidad visible conserva una unidad cómoda para diseño. El runtime
/// la convierte a la unidad base canónica antes de construir el inventario.
/// </summary>
[Serializable]
public sealed class BistroBuilderOpeningStockLine
{
    [SerializeField]
    private BistroBuilderIngredientDefinition ingredient;

    [SerializeField]
    [Min(0.001f)]
    private double amount = 1d;

    [SerializeField]
    private BistroBuilderMeasurementUnit unit =
        BistroBuilderMeasurementUnit.Gram;

    [SerializeField]
    private string storageLocationId = string.Empty;

    public BistroBuilderIngredientDefinition Ingredient => ingredient;

    public double Amount => amount;

    public BistroBuilderMeasurementUnit Unit => unit;

    public string StorageLocationId => storageLocationId;

    public BistroBuilderOpeningStockLine()
    {
    }

    public BistroBuilderOpeningStockLine(
        BistroBuilderIngredientDefinition ingredient,
        double amount,
        BistroBuilderMeasurementUnit unit,
        string storageLocationId
    )
    {
        this.ingredient = ingredient;
        this.amount = amount;
        this.unit = unit;
        this.storageLocationId = storageLocationId != null
            ? storageLocationId.Trim()
            : string.Empty;
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
            error = "La línea de stock inicial no tiene ingrediente.";
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
            error = "El stock inicial de " + ingredient.IngredientId +
                    " usa una unidad incompatible con su unidad base.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(storageLocationId))
        {
            error = "El stock inicial de " + ingredient.IngredientId +
                    " no tiene StorageLocationId.";
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidStableId(storageLocationId))
        {
            error = "El StorageLocationId '" + storageLocationId +
                    "' no es estable o válido.";
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

/// <summary>
/// Perfil de existencias con el que comienza una partida nueva.
///
/// No contiene estado runtime ni movimientos. Es únicamente una definición
/// de datos sustituible por dificultad, escenario o tipo de restaurante.
/// Los ingredientes no incluidos siguen existiendo con balance cero.
/// </summary>
[CreateAssetMenu(
    fileName = "OpeningStockProfile",
    menuName = "Bistro Builder/Inventory/Opening Stock Profile",
    order = 210
)]
public sealed class BistroBuilderOpeningStockProfile : ScriptableObject
{
    [SerializeField]
    private string profileId = "opening_stock_default";

    [SerializeField]
    private List<BistroBuilderOpeningStockLine> lines =
        new List<BistroBuilderOpeningStockLine>();

    public string ProfileId => profileId;

    public IReadOnlyList<BistroBuilderOpeningStockLine> Lines => lines;

    public int LineCount => lines != null ? lines.Count : 0;

    public bool TryValidate(out string error)
    {
        error = string.Empty;

        if (!BistroBuilderMenuIdUtility.IsValidStableId(profileId))
        {
            error = "El ProfileId '" + profileId +
                    "' no es estable o válido.";
            return false;
        }

        if (lines == null)
        {
            error = "El perfil de stock inicial no contiene una lista.";
            return false;
        }

        var ingredientIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < lines.Count; index++)
        {
            BistroBuilderOpeningStockLine line = lines[index];

            if (line == null)
            {
                error = "El perfil contiene una línea nula en la posición " +
                        index + ".";
                return false;
            }

            if (!line.TryValidate(out error))
            {
                error = "Stock inicial " + profileId + ": " + error;
                return false;
            }

            string ingredientId = line.Ingredient.IngredientId;

            if (!ingredientIds.Add(ingredientId))
            {
                error = "El perfil " + profileId +
                        " repite el ingrediente " + ingredientId + ".";
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        profileId = BistroBuilderMenuIdUtility.NormalizeStableId(profileId);

        if (lines == null)
        {
            lines = new List<BistroBuilderOpeningStockLine>();
        }
    }
#endif
}
