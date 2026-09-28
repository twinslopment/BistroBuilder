using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo canónico de recetas indexado por RecipeId y DishId.
/// </summary>
[CreateAssetMenu(
    fileName = "BistroBuilderRecipeCatalog",
    menuName = "Bistro Builder/Recipes/Recipe Catalog",
    order = 211
)]
public sealed class BistroBuilderRecipeCatalog : ScriptableObject
{
    [SerializeField]
    private List<BistroBuilderRecipeDefinition> definitions =
        new List<BistroBuilderRecipeDefinition>();

    [NonSerialized]
    private Dictionary<string, BistroBuilderRecipeDefinition> byRecipeId;

    [NonSerialized]
    private Dictionary<string, BistroBuilderRecipeDefinition> byDishId;

    [NonSerialized]
    private bool indexIsValid;

    public int DefinitionCount => definitions != null
        ? definitions.Count
        : 0;

    public IReadOnlyList<BistroBuilderRecipeDefinition> Definitions =>
        definitions;

    private void OnEnable()
    {
        TryRebuildIndex(out _);
    }

    public bool TryRebuildIndex(out string error)
    {
        if (definitions == null)
        {
            definitions = new List<BistroBuilderRecipeDefinition>();
        }

        if (byRecipeId == null)
        {
            byRecipeId = new Dictionary<string, BistroBuilderRecipeDefinition>(
                StringComparer.Ordinal
            );
            byDishId = new Dictionary<string, BistroBuilderRecipeDefinition>(
                StringComparer.Ordinal
            );
        }
        else
        {
            byRecipeId.Clear();
            byDishId.Clear();
        }

        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderRecipeDefinition definition = definitions[index];

            if (definition == null)
            {
                indexIsValid = false;
                error = "El catálogo de recetas contiene una referencia " +
                        "nula en la posición " + index + ".";
                return false;
            }

            if (!definition.TryValidate(out error))
            {
                indexIsValid = false;
                return false;
            }

            if (byRecipeId.ContainsKey(definition.RecipeId))
            {
                indexIsValid = false;
                error = "El RecipeId " + definition.RecipeId +
                        " está duplicado en el catálogo.";
                return false;
            }

            if (byDishId.ContainsKey(definition.DishId))
            {
                indexIsValid = false;
                error = "El DishId " + definition.DishId +
                        " tiene más de una receta canónica.";
                return false;
            }

            byRecipeId.Add(definition.RecipeId, definition);
            byDishId.Add(definition.DishId, definition);
        }

        indexIsValid = true;
        error = string.Empty;
        return true;
    }

    public bool TryGetByRecipeId(
        string recipeId,
        out BistroBuilderRecipeDefinition definition
    )
    {
        definition = null;

        if (!EnsureIndex())
        {
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(recipeId);

        return !string.IsNullOrWhiteSpace(normalized) &&
               byRecipeId.TryGetValue(normalized, out definition);
    }

    public bool TryGetByDishId(
        string dishId,
        out BistroBuilderRecipeDefinition definition
    )
    {
        definition = null;

        if (!EnsureIndex())
        {
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        return !string.IsNullOrWhiteSpace(normalized) &&
               byDishId.TryGetValue(normalized, out definition);
    }

    public void CopyDefinitionsTo(
        List<BistroBuilderRecipeDefinition> destination
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
            BistroBuilderRecipeDefinition definition = definitions[index];

            if (definition != null)
            {
                destination.Add(definition);
            }
        }
    }

    private bool EnsureIndex()
    {
        return indexIsValid || TryRebuildIndex(out _);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryRebuildIndex(out _);
    }
#endif
}
