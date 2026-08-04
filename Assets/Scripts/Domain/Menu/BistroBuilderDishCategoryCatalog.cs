using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo canónico e indexado de categorías de carta.
///
/// Mantiene índices por CategoryId y por el enum histórico para que los
/// sistemas nuevos usen identidades estables sin romper consumidores
/// anteriores.
/// </summary>
[CreateAssetMenu(
    fileName = "BistroBuilderDishCategoryCatalog",
    menuName = "Bistro Builder/Menu/Dish Category Catalog",
    order = 91
)]
public sealed class BistroBuilderDishCategoryCatalog : ScriptableObject
{
    [SerializeField]
    private List<BistroBuilderDishCategoryDefinition> definitions =
        new List<BistroBuilderDishCategoryDefinition>();

    private readonly Dictionary<string, BistroBuilderDishCategoryDefinition>
        byCategoryId =
            new Dictionary<string, BistroBuilderDishCategoryDefinition>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<BistroBuilderDishCategory,
        BistroBuilderDishCategoryDefinition> byLegacyCategory =
            new Dictionary<BistroBuilderDishCategory,
                BistroBuilderDishCategoryDefinition>();

    private bool indexReady;

    public int Count => definitions != null ? definitions.Count : 0;

    private void OnEnable()
    {
        TryRebuildIndex(out _);
    }

    public bool TryRebuildIndex(out string error)
    {
        indexReady = false;

        Dictionary<string, BistroBuilderDishCategoryDefinition>
            candidateByCategoryId =
                new Dictionary<string, BistroBuilderDishCategoryDefinition>(
                    StringComparer.Ordinal
                );
        Dictionary<BistroBuilderDishCategory,
            BistroBuilderDishCategoryDefinition> candidateByLegacyCategory =
                new Dictionary<BistroBuilderDishCategory,
                    BistroBuilderDishCategoryDefinition>();

        if (definitions == null)
        {
            ClearRuntimeIndex();
            error = "La lista de categorías es nula.";
            return false;
        }

        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderDishCategoryDefinition definition =
                definitions[index];

            if (definition == null)
            {
                ClearRuntimeIndex();
                error = "El catálogo contiene una categoría nula en la " +
                        "posición " + index + ".";
                return false;
            }

            if (!definition.TryValidate(out error))
            {
                ClearRuntimeIndex();
                return false;
            }

            if (candidateByCategoryId.ContainsKey(definition.CategoryId))
            {
                ClearRuntimeIndex();
                error = "El CategoryId " + definition.CategoryId +
                        " está duplicado.";
                return false;
            }

            if (definition.HasLegacyMapping &&
                candidateByLegacyCategory.ContainsKey(
                    definition.LegacyCategory
                ))
            {
                ClearRuntimeIndex();
                error = "La categoría histórica " +
                        definition.LegacyCategory + " está duplicada.";
                return false;
            }

            candidateByCategoryId.Add(
                definition.CategoryId,
                definition
            );

            if (definition.HasLegacyMapping)
            {
                candidateByLegacyCategory.Add(
                    definition.LegacyCategory,
                    definition
                );
            }
        }

        ClearRuntimeIndex();

        foreach (KeyValuePair<string, BistroBuilderDishCategoryDefinition>
                 pair in candidateByCategoryId)
        {
            byCategoryId.Add(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<BistroBuilderDishCategory,
                     BistroBuilderDishCategoryDefinition> pair
                 in candidateByLegacyCategory)
        {
            byLegacyCategory.Add(pair.Key, pair.Value);
        }

        indexReady = true;
        error = string.Empty;
        return true;
    }

    public bool TryGetDefinition(
        string categoryId,
        out BistroBuilderDishCategoryDefinition definition
    )
    {
        if (!EnsureIndex())
        {
            definition = null;
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(categoryId);
        return byCategoryId.TryGetValue(normalized, out definition);
    }

    public bool TryGetDefinition(
        BistroBuilderDishCategory legacyCategory,
        out BistroBuilderDishCategoryDefinition definition
    )
    {
        if (!EnsureIndex())
        {
            definition = null;
            return false;
        }

        return byLegacyCategory.TryGetValue(
            legacyCategory,
            out definition
        );
    }

    public bool Contains(string categoryId)
    {
        return TryGetDefinition(categoryId, out _);
    }

    public void CopyDefinitionsTo(
        List<BistroBuilderDishCategoryDefinition> destination
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
            destination.Add(definitions[index]);
        }

        destination.Sort(CompareDefinitions);
    }

    private bool EnsureIndex()
    {
        return indexReady || TryRebuildIndex(out _);
    }

    private void ClearRuntimeIndex()
    {
        byCategoryId.Clear();
        byLegacyCategory.Clear();
        indexReady = false;
    }

    private static int CompareDefinitions(
        BistroBuilderDishCategoryDefinition first,
        BistroBuilderDishCategoryDefinition second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int order = first.DisplayOrder.CompareTo(second.DisplayOrder);
        return order != 0
            ? order
            : string.Compare(
                first.CategoryId,
                second.CategoryId,
                StringComparison.Ordinal
            );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryRebuildIndex(out _);
    }
#endif
}
