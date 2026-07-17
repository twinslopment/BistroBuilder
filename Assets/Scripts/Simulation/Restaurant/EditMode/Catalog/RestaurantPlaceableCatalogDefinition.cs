using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Define el conjunto de artículos disponibles para un catálogo
/// del modo edición.
///
/// La definición solo contiene datos. La disponibilidad económica,
/// los desbloqueos y otras reglas futuras se resolverán desde
/// servicios externos sin acoplarlas a la interfaz.
/// </summary>
[CreateAssetMenu(
    fileName = "RestaurantPlaceableCatalog_",
    menuName =
        "Bistro Builder/Restaurant/Edit Mode/" +
        "Placeable Catalog"
)]
public sealed class RestaurantPlaceableCatalogDefinition :
    ScriptableObject
{
    [SerializeField]
    private List<RestaurantPlaceableItemDefinition> items =
        new List<RestaurantPlaceableItemDefinition>();

    public IReadOnlyList<RestaurantPlaceableItemDefinition> Items
    {
        get
        {
            return items;
        }
    }

    public int ItemCount
    {
        get
        {
            return items != null
                ? items.Count
                : 0;
        }
    }

    /// <summary>
    /// Busca una definición por su ItemId estable.
    /// </summary>
    public bool TryGetItem(
        string itemId,
        out RestaurantPlaceableItemDefinition definition
    )
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(itemId) ||
            items == null)
        {
            return false;
        }

        string normalizedId =
            itemId.Trim();

        for (int index = 0;
             index < items.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition candidate =
                items[index];

            if (candidate == null)
            {
                continue;
            }

            if (!string.Equals(
                    candidate.ItemId,
                    normalizedId,
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

    private void OnValidate()
    {
        if (items == null)
        {
            items =
                new List<RestaurantPlaceableItemDefinition>();

            return;
        }

        HashSet<string> knownIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        for (int index = items.Count - 1;
             index >= 0;
             index--)
        {
            RestaurantPlaceableItemDefinition item =
                items[index];

            if (item == null)
            {
                items.RemoveAt(index);
                continue;
            }

            string itemId =
                item.ItemId;

            if (string.IsNullOrWhiteSpace(itemId) ||
                !knownIds.Add(itemId))
            {
                items.RemoveAt(index);
            }
        }
    }
}
