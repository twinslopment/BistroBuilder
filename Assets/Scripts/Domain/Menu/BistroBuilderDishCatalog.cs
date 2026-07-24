using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo de todas las definiciones de plato instaladas en el proyecto.
///
/// La lista se serializa como asset. El índice de ejecución se reconstruye
/// una sola vez y permite búsquedas O(1) por DishId.
/// </summary>
[CreateAssetMenu(
    fileName = "BistroBuilderDishCatalog",
    menuName = "Bistro Builder/Menu/Dish Catalog",
    order = 101
)]
public sealed class BistroBuilderDishCatalog : ScriptableObject
{
    [SerializeField]
    private List<BistroBuilderDishDefinition> definitions =
        new List<BistroBuilderDishDefinition>();

    [NonSerialized]
    private Dictionary<string, BistroBuilderDishDefinition> byId;

    [NonSerialized]
    private bool indexIsValid;

    public int DefinitionCount => definitions != null
        ? definitions.Count
        : 0;

    public IReadOnlyList<BistroBuilderDishDefinition> Definitions =>
        definitions;

    private void OnEnable()
    {
        TryRebuildIndex(out _);
    }

    /// <summary>
    /// Reconstruye el índice y valida IDs, referencias y duplicados.
    /// </summary>
    public bool TryRebuildIndex(out string error)
    {
        if (definitions == null)
        {
            definitions = new List<BistroBuilderDishDefinition>();
        }

        if (byId == null)
        {
            byId = new Dictionary<string, BistroBuilderDishDefinition>(
                StringComparer.Ordinal
            );
        }
        else
        {
            byId.Clear();
        }

        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderDishDefinition definition = definitions[index];

            if (definition == null)
            {
                indexIsValid = false;
                error = "El catálogo contiene una definición nula en la posición " +
                        index + ".";
                return false;
            }

            if (!definition.TryValidate(out error))
            {
                indexIsValid = false;
                return false;
            }

            if (byId.ContainsKey(definition.DishId))
            {
                indexIsValid = false;
                error = "El DishId " + definition.DishId +
                        " está duplicado en el catálogo.";
                return false;
            }

            byId.Add(definition.DishId, definition);
        }

        indexIsValid = true;
        error = string.Empty;
        return true;
    }

    public bool TryGetDefinition(
        string dishId,
        out BistroBuilderDishDefinition definition
    )
    {
        definition = null;

        if (!indexIsValid && !TryRebuildIndex(out _))
        {
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        return !string.IsNullOrEmpty(normalized) &&
               byId.TryGetValue(normalized, out definition);
    }

    public bool Contains(string dishId)
    {
        return TryGetDefinition(dishId, out _);
    }

    /// <summary>
    /// Copia las referencias del catálogo sin exponer la lista serializada
    /// a modificaciones externas.
    /// </summary>
    public void CopyDefinitionsTo(
        List<BistroBuilderDishDefinition> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();

        if (definitions == null)
        {
            return;
        }

        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderDishDefinition definition = definitions[index];

            if (definition != null)
            {
                destination.Add(definition);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryRebuildIndex(out _);
    }
#endif
}
