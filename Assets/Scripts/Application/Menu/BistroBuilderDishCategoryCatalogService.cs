using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto único de acceso runtime al catálogo canónico de categorías.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Dish Category Catalog Service")]
public sealed class BistroBuilderDishCategoryCatalogService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderDishCategoryCatalog catalog;

    [SerializeField]
    private bool logInitialization = true;

    public BistroBuilderDishCategoryCatalog Catalog => catalog;

    public int CategoryCount => catalog != null ? catalog.Count : 0;

    private void Awake()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        if (logInitialization)
        {
            Debug.Log(
                "Catálogo de categorías preparado con " +
                catalog.Count + " categoría(s).",
                this
            );
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        if (catalog == null)
        {
            error = "Falta BistroBuilderDishCategoryCatalog.";
            return false;
        }

        return catalog.TryRebuildIndex(out error);
    }

    public bool TryGetDefinition(
        string categoryId,
        out BistroBuilderDishCategoryDefinition definition
    )
    {
        definition = null;
        return catalog != null &&
               catalog.TryGetDefinition(categoryId, out definition);
    }

    public bool TryGetDefinition(
        BistroBuilderDishCategory legacyCategory,
        out BistroBuilderDishCategoryDefinition definition
    )
    {
        definition = null;
        return catalog != null &&
               catalog.TryGetDefinition(legacyCategory, out definition);
    }

    public void CopyDefinitionsTo(
        List<BistroBuilderDishCategoryDefinition> destination
    )
    {
        if (catalog == null)
        {
            destination?.Clear();
            return;
        }

        catalog.CopyDefinitionsTo(destination);
    }
}
