using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo canónico de ingredientes. El índice runtime se reconstruye una
/// sola vez y permite búsquedas O(1) por IngredientId.
/// </summary>
[CreateAssetMenu(
    fileName = "BistroBuilderIngredientCatalog",
    menuName = "Bistro Builder/Inventory/Ingredient Catalog",
    order = 201
)]
public sealed class BistroBuilderIngredientCatalog : ScriptableObject
{
    [SerializeField]
    private List<BistroBuilderIngredientDefinition> definitions =
        new List<BistroBuilderIngredientDefinition>();

    [NonSerialized]
    private Dictionary<string, BistroBuilderIngredientDefinition> byId;

    [NonSerialized]
    private bool indexIsValid;

    public int DefinitionCount => definitions != null
        ? definitions.Count
        : 0;

    public IReadOnlyList<BistroBuilderIngredientDefinition> Definitions =>
        definitions;

    private void OnEnable()
    {
        TryRebuildIndex(out _);
    }

    public bool TryRebuildIndex(out string error)
    {
        if (definitions == null)
        {
            definitions = new List<BistroBuilderIngredientDefinition>();
        }

        if (byId == null)
        {
            byId = new Dictionary<string, BistroBuilderIngredientDefinition>(
                StringComparer.Ordinal
            );
        }
        else
        {
            byId.Clear();
        }

        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderIngredientDefinition definition =
                definitions[index];

            if (definition == null)
            {
                indexIsValid = false;
                error = "El catálogo de ingredientes contiene una " +
                        "referencia nula en la posición " + index + ".";
                return false;
            }

            if (!definition.TryValidate(out error))
            {
                indexIsValid = false;
                return false;
            }

            if (byId.ContainsKey(definition.IngredientId))
            {
                indexIsValid = false;
                error = "El IngredientId " + definition.IngredientId +
                        " está duplicado en el catálogo.";
                return false;
            }

            byId.Add(definition.IngredientId, definition);
        }

        indexIsValid = true;
        error = string.Empty;
        return true;
    }

    public bool TryGetDefinition(
        string ingredientId,
        out BistroBuilderIngredientDefinition definition
    )
    {
        definition = null;

        if (!indexIsValid && !TryRebuildIndex(out _))
        {
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(ingredientId);

        return !string.IsNullOrWhiteSpace(normalized) &&
               byId.TryGetValue(normalized, out definition);
    }

    public bool Contains(string ingredientId)
    {
        return TryGetDefinition(ingredientId, out _);
    }

    public void CopyDefinitionsTo(
        List<BistroBuilderIngredientDefinition> destination
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
            BistroBuilderIngredientDefinition definition =
                definitions[index];

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
