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

    [SerializeField]
    private RestaurantPlaceableCatalogService playableCatalogService;

    private readonly Dictionary<
        string,
        RestaurantPlaceableItemDefinition
    > definitionByItemId =
        new Dictionary<
            string,
            RestaurantPlaceableItemDefinition
        >(StringComparer.Ordinal);

    private bool indexBuilt;
    private bool catalogSubscribed;
    private bool synchronizing;
    private int synchronizedPlayableCount = -1;

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
        CachePlayableCatalog();
        SynchronizePlayableDefinitions();
        RebuildIndex();
    }

    private void OnEnable()
    {
        CachePlayableCatalog();
        SubscribePlayableCatalog();
        SynchronizePlayableDefinitions();
        RebuildIndex();
    }

    private void Start()
    {
        /*
         * Cubre cualquier orden de Awake entre servicios. El catálogo jugable
         * ya está materializado antes de la primera operación de partida.
         */
        CachePlayableCatalog();
        SubscribePlayableCatalog();
        SynchronizePlayableDefinitions();
        RebuildIndex();
    }

    private void OnDisable()
    {
        UnsubscribePlayableCatalog();
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
        SynchronizePlayableDefinitions();
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
        SynchronizePlayableDefinitions();
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

    private void CachePlayableCatalog()
    {
        if (playableCatalogService == null)
        {
            playableCatalogService =
                FindFirstObjectByType<RestaurantPlaceableCatalogService>();
        }
    }

    private void SubscribePlayableCatalog()
    {
        if (catalogSubscribed ||
            playableCatalogService == null)
        {
            return;
        }

        playableCatalogService.CatalogChanged +=
            HandlePlayableCatalogChanged;

        catalogSubscribed = true;
    }

    private void UnsubscribePlayableCatalog()
    {
        if (!catalogSubscribed ||
            playableCatalogService == null)
        {
            catalogSubscribed = false;
            return;
        }

        playableCatalogService.CatalogChanged -=
            HandlePlayableCatalogChanged;

        catalogSubscribed = false;
    }

    private void HandlePlayableCatalogChanged()
    {
        synchronizedPlayableCount = -1;
        SynchronizePlayableDefinitions();
        RebuildIndex();
    }

    /// <summary>
    /// Adopta en runtime cualquier definición que el catálogo jugable ya
    /// considera canónica. Conserva referencias legacy de guardado para no
    /// romper partidas antiguas y evita hardcodes por asset concreto.
    /// </summary>
    private void SynchronizePlayableDefinitions()
    {
        if (synchronizing)
            return;

        CachePlayableCatalog();
        SubscribePlayableCatalog();

        if (playableCatalogService == null)
            return;

        IReadOnlyList<RestaurantPlaceableItemDefinition> playable =
            playableCatalogService.AvailableItems;

        if (synchronizedPlayableCount ==
            playable.Count)
        {
            return;
        }

        synchronizing = true;

        try
        {
            HashSet<string> knownIds =
                new HashSet<string>(
                    StringComparer.Ordinal);

            for (int index = 0;
                 index < definitions.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition definition =
                    definitions[index];

                if (definition == null ||
                    string.IsNullOrWhiteSpace(
                        definition.ItemId))
                {
                    continue;
                }

                knownIds.Add(
                    NormalizeItemId(
                        definition.ItemId));
            }

            bool changed = false;

            for (int index = 0;
                 index < playable.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition definition =
                    playable[index];

                if (definition == null ||
                    string.IsNullOrWhiteSpace(
                        definition.ItemId))
                {
                    continue;
                }

                string itemId =
                    NormalizeItemId(
                        definition.ItemId);

                if (!knownIds.Add(itemId))
                    continue;

                definitions.Add(definition);
                changed = true;
            }

            if (changed)
                indexBuilt = false;

            synchronizedPlayableCount =
                playable.Count;
        }
        finally
        {
            synchronizing = false;
        }
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
