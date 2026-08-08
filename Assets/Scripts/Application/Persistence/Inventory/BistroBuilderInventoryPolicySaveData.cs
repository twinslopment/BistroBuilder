using System;
using System.Collections.Generic;

/// <summary>
/// Política persistente de inventario 2.2C.
///
/// No contiene stock, lotes, reservas ni consumos: esos datos permanecen en
/// inventory.canonical. Esta sección solo conserva decisiones configurables
/// del jugador para un único almacén genérico por restaurante.
/// </summary>
[Serializable]
public sealed class BistroBuilderInventoryPolicySaveData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string restaurantId =
        BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;
    public long policyRevision;
    public List<BistroBuilderInventoryMinimumStockSaveRecord> minimumStocks =
        new List<BistroBuilderInventoryMinimumStockSaveRecord>();

    public bool TryValidateBasic(out string error)
    {
        error = string.Empty;

        if (schemaVersion != CurrentSchemaVersion)
        {
            error = "La versión de inventory.policy no es compatible.";
            return false;
        }

        restaurantId = BistroBuilderMenuIdUtility.NormalizeStableId(
            restaurantId
        );
        if (!BistroBuilderMenuIdUtility.IsValidStableId(restaurantId))
        {
            error = "inventory.policy contiene un RestaurantId inválido.";
            return false;
        }

        if (policyRevision < 0L || minimumStocks == null)
        {
            error = "inventory.policy contiene una revisión o colección inválida.";
            return false;
        }

        var ingredientIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < minimumStocks.Count; index++)
        {
            BistroBuilderInventoryMinimumStockSaveRecord record =
                minimumStocks[index];

            if (record == null || !record.TryValidate(out error))
            {
                return false;
            }

            if (!ingredientIds.Add(record.ingredientId))
            {
                error = "inventory.policy repite el ingrediente " +
                        record.ingredientId + ".";
                return false;
            }
        }

        return true;
    }
}

[Serializable]
public sealed class BistroBuilderInventoryMinimumStockSaveRecord
{
    public string ingredientId = string.Empty;
    public long minimumCanonicalMilliUnits;

    public bool TryValidate(out string error)
    {
        ingredientId = BistroBuilderMenuIdUtility.NormalizeStableId(
            ingredientId
        );

        if (!BistroBuilderMenuIdUtility.IsValidStableId(ingredientId))
        {
            error = "La política contiene un IngredientId inválido.";
            return false;
        }

        if (minimumCanonicalMilliUnits < 0L ||
            minimumCanonicalMilliUnits >
                BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
        {
            error = "El stock mínimo de " + ingredientId +
                    " queda fuera de rango.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
