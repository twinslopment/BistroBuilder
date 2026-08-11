using System;
using System.Collections.Generic;

/// <summary>
/// Contrato de lectura para desacoplar los sistemas 2.3B+ del formato de
/// almacenamiento utilizado por la herramienta de autoría.
///
/// Importante: los adaptadores devuelven copias profundas. Ningún consumidor
/// recibe referencias editables a los ScriptableObject maestros.
/// </summary>
public interface IBistroBuilderSupplierAuthoringRepository
{
    int ContentRevision { get; }
    bool TryGetSupplier(string supplierId, out BistroBuilderSupplierAuthoringRecord supplier);
    int CopySuppliers(List<BistroBuilderSupplierAuthoringRecord> buffer, bool activeOnly = false);
}

public interface IBistroBuilderIngredientAuthoringRepository
{
    int ContentRevision { get; }
    bool TryGetIngredient(string ingredientId, out BistroBuilderIngredientAuthoringRecord ingredient);
    int CopyIngredients(List<BistroBuilderIngredientAuthoringRecord> buffer, bool activeOnly = false);
}

/// <summary>
/// Adaptador de lectura. Nunca modifica el asset y evita que el gameplay tenga
/// que conocer AssetDatabase ni detalles del Editor.
/// </summary>
public sealed class BistroBuilderSupplierAuthoringRepository : IBistroBuilderSupplierAuthoringRepository
{
    private readonly BistroBuilderSupplierAuthoringDatabase database;

    public BistroBuilderSupplierAuthoringRepository(BistroBuilderSupplierAuthoringDatabase database)
    {
        if (database == null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        this.database = database;
    }

    public int ContentRevision => database.ContentRevision;

    public bool TryGetSupplier(string supplierId, out BistroBuilderSupplierAuthoringRecord supplier)
    {
        supplier = null;

        if (!database.TryGetSupplier(
                supplierId,
                out BistroBuilderSupplierAuthoringRecord source) ||
            source == null)
        {
            return false;
        }

        supplier = source.DeepClone(true);
        return true;
    }

    public int CopySuppliers(List<BistroBuilderSupplierAuthoringRecord> buffer, bool activeOnly = false)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        buffer.Clear();
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> source = database.Suppliers;

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = source[index];
            if (supplier == null || (activeOnly && !supplier.isActive))
            {
                continue;
            }

            buffer.Add(supplier.DeepClone(true));
        }

        return buffer.Count;
    }
}

public sealed class BistroBuilderIngredientAuthoringRepository : IBistroBuilderIngredientAuthoringRepository
{
    private readonly BistroBuilderIngredientAuthoringDatabase database;

    public BistroBuilderIngredientAuthoringRepository(BistroBuilderIngredientAuthoringDatabase database)
    {
        if (database == null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        this.database = database;
    }

    public int ContentRevision => database.ContentRevision;

    public bool TryGetIngredient(string ingredientId, out BistroBuilderIngredientAuthoringRecord ingredient)
    {
        ingredient = null;

        if (!database.TryGetIngredient(
                ingredientId,
                out BistroBuilderIngredientAuthoringRecord source) ||
            source == null)
        {
            return false;
        }

        ingredient = source.DeepClone(true);
        return true;
    }

    public int CopyIngredients(List<BistroBuilderIngredientAuthoringRecord> buffer, bool activeOnly = false)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        buffer.Clear();

        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> source = database.Ingredients;
        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = source[index];
            if (ingredient == null || (activeOnly && !ingredient.isActive))
            {
                continue;
            }

            buffer.Add(ingredient.DeepClone(true));
        }

        return buffer.Count;
    }
}
