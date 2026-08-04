using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fotografía mutable serializable de la carta de un restaurante concreto.
///
/// Las entradas resueltas contienen DishId presentes en el catálogo actual.
/// Las entradas no resueltas conservan datos de partidas antiguas o de
/// contenido temporalmente ausente para poder reconciliarlos posteriormente.
/// </summary>
[Serializable]
public sealed class BistroBuilderRestaurantMenuRuntimeState
{
    [SerializeField]
    private string restaurantId = string.Empty;

    [SerializeField]
    private int revision;

    [SerializeField]
    private List<BistroBuilderMenuItemRuntimeState> items =
        new List<BistroBuilderMenuItemRuntimeState>();

    [SerializeField]
    private List<BistroBuilderMenuItemRuntimeState> unresolvedItems =
        new List<BistroBuilderMenuItemRuntimeState>();

    public string RestaurantId => restaurantId;

    public int Revision => revision;

    public IReadOnlyList<BistroBuilderMenuItemRuntimeState> Items => items;

    public IReadOnlyList<BistroBuilderMenuItemRuntimeState> UnresolvedItems =>
        unresolvedItems;

    public int ItemCount => items != null ? items.Count : 0;

    public int UnresolvedItemCount =>
        unresolvedItems != null ? unresolvedItems.Count : 0;

    public BistroBuilderRestaurantMenuRuntimeState()
    {
    }

    public BistroBuilderRestaurantMenuRuntimeState(
        string restaurantId,
        int revision,
        IList<BistroBuilderMenuItemRuntimeState> items,
        IList<BistroBuilderMenuItemRuntimeState> unresolvedItems
    )
    {
        this.restaurantId =
            BistroBuilderMenuIdUtility.NormalizeStableId(restaurantId);
        this.revision = Math.Max(0, revision);
        CopyItems(items, this.items);
        CopyItems(unresolvedItems, this.unresolvedItems);
    }

    public BistroBuilderRestaurantMenuRuntimeState Clone()
    {
        return new BistroBuilderRestaurantMenuRuntimeState(
            restaurantId,
            revision,
            items,
            unresolvedItems
        );
    }

    /// <summary>
    /// Valida estructura, duplicados y clasificación conocida/no resuelta.
    /// No modifica el estado recibido.
    /// </summary>
    public bool TryValidate(
        BistroBuilderDishCatalogService catalogService,
        out string error
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(restaurantId))
        {
            error = "La carta contiene un RestaurantId inválido.";
            return false;
        }

        if (revision < 0)
        {
            error = "La revisión de la carta " + restaurantId +
                    " no puede ser negativa.";
            return false;
        }

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (items == null || unresolvedItems == null)
        {
            error = "La carta " + restaurantId +
                    " contiene colecciones nulas.";
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = items[index];

            if (item == null)
            {
                error = "La carta contiene una entrada resuelta nula.";
                return false;
            }

            if (!item.TryValidateStructure(out error))
            {
                return false;
            }

            if (!ids.Add(item.DishId))
            {
                error = "La carta " + restaurantId +
                        " contiene el DishId duplicado " +
                        item.DishId + ".";
                return false;
            }

            if (!catalogService.TryGetDefinition(item.DishId, out _))
            {
                error = "La entrada resuelta " + item.DishId +
                        " no existe en el catálogo actual.";
                return false;
            }
        }

        for (int index = 0; index < unresolvedItems.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = unresolvedItems[index];

            if (item == null)
            {
                error = "La carta contiene una entrada no resuelta nula.";
                return false;
            }

            if (!item.TryValidateStructure(out error))
            {
                return false;
            }

            if (!ids.Add(item.DishId))
            {
                error = "La carta " + restaurantId +
                        " contiene el DishId duplicado " +
                        item.DishId + ".";
                return false;
            }

            if (catalogService.TryGetDefinition(item.DishId, out _))
            {
                error = "La entrada " + item.DishId +
                        " figura como no resuelta aunque ya existe en el catálogo.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    internal void ReplaceResolvedItems(
        IList<BistroBuilderMenuItemRuntimeState> source,
        int nextRevision
    )
    {
        CopyItems(source, items);
        revision = Math.Max(0, nextRevision);
    }

    internal void ReplaceAll(
        IList<BistroBuilderMenuItemRuntimeState> resolved,
        IList<BistroBuilderMenuItemRuntimeState> unresolved,
        int nextRevision
    )
    {
        CopyItems(resolved, items);
        CopyItems(unresolved, unresolvedItems);
        revision = Math.Max(0, nextRevision);
    }

    private static void CopyItems(
        IList<BistroBuilderMenuItemRuntimeState> source,
        List<BistroBuilderMenuItemRuntimeState> destination
    )
    {
        destination.Clear();

        if (source == null)
        {
            return;
        }

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = source[index];
            destination.Add(item != null ? item.Clone() : null);
        }
    }
}
