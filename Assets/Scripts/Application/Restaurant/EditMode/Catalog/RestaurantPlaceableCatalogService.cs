using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto de acceso de aplicación al catálogo de artículos.
///
/// Responsabilidades:
/// - Exponer definiciones válidas en un orden estable.
/// - Resolver artículos por ItemId.
/// - Publicar cambios de catálogo.
/// - Mantener la UI desacoplada del asset concreto.
///
/// No contiene reglas de presentación ni instancia prefabs.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placeable Catalog Service"
)]
public sealed class RestaurantPlaceableCatalogService :
    MonoBehaviour
{
    [Header("Datos")]

    [SerializeField]
    private RestaurantPlaceableCatalogDefinition
        catalogDefinition;

    [Header("Depuración")]

    [SerializeField]
    private bool logCatalogSummary = true;

    private readonly List<RestaurantPlaceableItemDefinition>
        availableItems =
            new List<RestaurantPlaceableItemDefinition>(64);

    public event Action CatalogChanged;

    public RestaurantPlaceableCatalogDefinition CatalogDefinition
    {
        get
        {
            return catalogDefinition;
        }
    }

    public IReadOnlyList<RestaurantPlaceableItemDefinition>
        AvailableItems
    {
        get
        {
            return availableItems;
        }
    }

    public int AvailableItemCount
    {
        get
        {
            return availableItems.Count;
        }
    }

    private void Awake()
    {
        RebuildCatalog();
    }

    private void Start()
    {
        if (!logCatalogSummary)
        {
            return;
        }

        Debug.Log(
            "RestaurantPlaceableCatalogService ha cargado " +
            availableItems.Count +
            " artículo(s) de catálogo.",
            this
        );
    }

    /// <summary>
    /// Reconstruye la lista runtime descartando referencias inválidas
    /// y duplicados por ItemId.
    /// </summary>
    public void RebuildCatalog()
    {
        availableItems.Clear();

        if (catalogDefinition == null)
        {
            CatalogChanged?.Invoke();
            return;
        }

        HashSet<string> knownItemIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        IReadOnlyList<RestaurantPlaceableItemDefinition> sourceItems =
            catalogDefinition.Items;

        for (int index = 0;
             index < sourceItems.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition item =
                sourceItems[index];

            if (item == null ||
                !item.HasValidPrefab ||
                string.IsNullOrWhiteSpace(item.ItemId) ||
                !knownItemIds.Add(item.ItemId))
            {
                continue;
            }

            availableItems.Add(item);
        }

        availableItems.Sort(
            CompareDefinitions
        );

        CatalogChanged?.Invoke();
    }

    public bool TryGetItem(
        string itemId,
        out RestaurantPlaceableItemDefinition definition
    )
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        for (int index = 0;
             index < availableItems.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition candidate =
                availableItems[index];

            if (!string.Equals(
                    candidate.ItemId,
                    itemId,
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            definition =
                candidate;

            return true;
        }

        return false;
    }

    private static int CompareDefinitions(
        RestaurantPlaceableItemDefinition first,
        RestaurantPlaceableItemDefinition second
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

        int categoryComparison =
            first.Category.CompareTo(
                second.Category
            );

        if (categoryComparison != 0)
        {
            return categoryComparison;
        }

        return string.Compare(
            first.DisplayName,
            second.DisplayName,
            StringComparison.CurrentCultureIgnoreCase
        );
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            RebuildCatalog();
        }
    }
}
