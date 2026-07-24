using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Índice persistente de definiciones colocables disponibles para carga.
///
/// El instalador lo rellena desde los assets del proyecto. En runtime la
/// resolución por ItemId es O(1) y no utiliza Resources, Find ni búsquedas
/// por rutas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/Save Definition Catalog"
)]
public sealed class BistroBuilderSaveDefinitionCatalog : MonoBehaviour
{
    [SerializeField]
    private List<RestaurantPlaceableItemDefinition> definitions =
        new List<RestaurantPlaceableItemDefinition>();

    private readonly Dictionary<
        string,
        RestaurantPlaceableItemDefinition
    > definitionByItemId =
        new Dictionary<
            string,
            RestaurantPlaceableItemDefinition
        >(StringComparer.Ordinal);

    private bool indexBuilt;

    public IReadOnlyList<RestaurantPlaceableItemDefinition>
        Definitions => definitions;

    public int Count
    {
        get
        {
            EnsureIndex();
            return definitionByItemId.Count;
        }
    }

    private void Awake()
    {
        RebuildIndex();
    }

    public void RebuildIndex()
    {
        definitionByItemId.Clear();

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                definitions[index];

            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.ItemId))
            {
                continue;
            }

            string itemId = NormalizeItemId(definition.ItemId);

            if (!definitionByItemId.ContainsKey(itemId))
            {
                definitionByItemId.Add(itemId, definition);
            }
        }

        indexBuilt = true;
    }

    public bool TryGetDefinition(
        string itemId,
        out RestaurantPlaceableItemDefinition definition
    )
    {
        EnsureIndex();

        definition = null;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        return definitionByItemId.TryGetValue(
            NormalizeItemId(itemId),
            out definition
        );
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (definitions == null || definitions.Count == 0)
        {
            error = "El catálogo de persistencia no contiene definiciones.";
            return false;
        }

        HashSet<string> ids =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                definitions[index];

            if (definition == null)
            {
                error = "El catálogo contiene una referencia nula.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.ItemId))
            {
                error = definition.name + " no tiene ItemId.";
                return false;
            }

            string itemId = NormalizeItemId(definition.ItemId);

            if (!ids.Add(itemId))
            {
                error = "El ItemId " + itemId + " está duplicado.";
                return false;
            }

            if (!definition.HasValidPrefab || definition.Prefab == null)
            {
                error = definition.DisplayName +
                        " no tiene un prefab válido.";
                return false;
            }
        }

        return true;
    }

    private void EnsureIndex()
    {
        if (!indexBuilt)
        {
            RebuildIndex();
        }
    }

    private static string NormalizeItemId(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        indexBuilt = false;
    }
#endif
}
