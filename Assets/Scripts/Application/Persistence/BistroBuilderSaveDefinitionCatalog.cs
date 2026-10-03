using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Índice persistente de definiciones colocables disponibles para carga.
///
/// La lista legacy de definiciones explícitas se conserva por compatibilidad,
/// pero el catálogo puede consumir además uno o más catálogos canónicos del
/// modo edición. De este modo, añadir contenido a un catálogo canónico hace
/// que Save/Load pueda resolverlo sin reescribir la escena por cada asset.
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

    [SerializeField]
    private List<RestaurantPlaceableCatalogDefinition> sourceCatalogs =
        new List<RestaurantPlaceableCatalogDefinition>();

    private readonly Dictionary<
        string,
        RestaurantPlaceableItemDefinition
    > definitionByItemId =
        new Dictionary<
            string,
            RestaurantPlaceableItemDefinition
        >(StringComparer.Ordinal);

    private readonly List<RestaurantPlaceableItemDefinition>
        resolvedDefinitions =
            new List<RestaurantPlaceableItemDefinition>(128);

    private bool indexBuilt;
    private bool catalogSubscribed;
    private bool synchronizing;
    private int synchronizedPlayableCount = -1;

    public IReadOnlyList<RestaurantPlaceableItemDefinition>
        Definitions
    {
        get
        {
            EnsureIndex();
            return resolvedDefinitions;
        }
    }

    public IReadOnlyList<RestaurantPlaceableCatalogDefinition>
        SourceCatalogs => sourceCatalogs;

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
        resolvedDefinitions.Clear();

        AddDefinitions(
            definitions,
            throwOnConflict: false);

        if (sourceCatalogs != null)
        {
            for (int catalogIndex = 0;
                 catalogIndex < sourceCatalogs.Count;
                 catalogIndex++)
            {
                RestaurantPlaceableCatalogDefinition source =
                    sourceCatalogs[catalogIndex];

                if (source == null)
                    continue;

                AddDefinitions(
                    source.Items,
                    throwOnConflict: false);
            }
        }

        resolvedDefinitions.Sort(
            CompareDefinitions);

        indexBuilt = true;
    }

    public bool TryGetDefinition(
        string itemId,
        out RestaurantPlaceableItemDefinition definition
    )
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        string normalizedItemId =
            NormalizeItemId(itemId);

        SynchronizePlayableDefinitions();
        EnsureIndex();

        if (definitionByItemId.TryGetValue(
                normalizedItemId,
                out definition))
        {
            return true;
        }

        /*
         * Defensa ante catálogos mutados durante la misma sesión:
         * una igualdad de count no garantiza que el contenido sea el mismo.
         * Antes de rechazar un ItemId persistente reconstruimos desde la
         * autoridad jugable actual, sin hardcodes por asset concreto.
         */
        synchronizedPlayableCount = -1;
        SynchronizePlayableDefinitions();
        RebuildIndex();

        if (definitionByItemId.TryGetValue(
                normalizedItemId,
                out definition))
        {
            return true;
        }

        if (playableCatalogService != null &&
            playableCatalogService.TryGetItem(
                normalizedItemId,
                out RestaurantPlaceableItemDefinition playableDefinition) &&
            playableDefinition != null)
        {
            definitions.Add(playableDefinition);
            RebuildIndex();

            return definitionByItemId.TryGetValue(
                normalizedItemId,
                out definition);
        }

        return false;
    }

    public bool ValidateConfiguration(out string error)
    {
        SynchronizePlayableDefinitions();
        error = string.Empty;

        RebuildIndex();

        if (definitionByItemId.Count == 0)
        {
            error =
                "El catálogo de persistencia no contiene definiciones.";
            return false;
        }

        Dictionary<
            string,
            RestaurantPlaceableItemDefinition
        > seen =
            new Dictionary<
                string,
                RestaurantPlaceableItemDefinition
            >(StringComparer.Ordinal);

        if (!ValidateDefinitionSequence(
                definitions,
                "definiciones explícitas",
                seen,
                out error))
        {
            return false;
        }

        if (sourceCatalogs != null)
        {
            HashSet<RestaurantPlaceableCatalogDefinition>
                uniqueCatalogs =
                    new HashSet<
                        RestaurantPlaceableCatalogDefinition>();

            for (int catalogIndex = 0;
                 catalogIndex < sourceCatalogs.Count;
                 catalogIndex++)
            {
                RestaurantPlaceableCatalogDefinition source =
                    sourceCatalogs[catalogIndex];

                if (source == null)
                {
                    error =
                        "El catálogo de persistencia contiene " +
                        "una fuente de catálogo nula.";
                    return false;
                }

                if (!uniqueCatalogs.Add(source))
                {
                    error =
                        "El catálogo de persistencia contiene " +
                        "una fuente de catálogo duplicada: " +
                        source.name + ".";
                    return false;
                }

                if (!ValidateDefinitionSequence(
                        source.Items,
                        "catálogo " + source.name,
                        seen,
                        out error))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void AddDefinitions(
        IReadOnlyList<RestaurantPlaceableItemDefinition> source,
        bool throwOnConflict)
    {
        if (source == null)
            return;

        for (int index = 0;
             index < source.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                source[index];

            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.ItemId))
            {
                continue;
            }

            string itemId =
                NormalizeItemId(definition.ItemId);

            if (definitionByItemId.TryGetValue(
                    itemId,
                    out RestaurantPlaceableItemDefinition existing))
            {
                if (ReferenceEquals(
                        existing,
                        definition))
                {
                    continue;
                }

                if (throwOnConflict)
                {
                    throw new InvalidOperationException(
                        "El ItemId " +
                        itemId +
                        " resuelve a más de una definición.");
                }

                continue;
            }

            definitionByItemId.Add(
                itemId,
                definition);

            resolvedDefinitions.Add(
                definition);
        }
    }

    private static bool ValidateDefinitionSequence(
        IReadOnlyList<RestaurantPlaceableItemDefinition> source,
        string sourceLabel,
        IDictionary<string, RestaurantPlaceableItemDefinition> seen,
        out string error)
    {
        error = string.Empty;

        if (source == null)
            return true;

        for (int index = 0;
             index < source.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                source[index];

            if (definition == null)
            {
                error =
                    sourceLabel +
                    " contiene una referencia nula.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.ItemId))
            {
                error =
                    definition.name +
                    " no tiene ItemId.";
                return false;
            }

            if (!definition.HasValidPrefab ||
                definition.Prefab == null)
            {
                error =
                    definition.DisplayName +
                    " no tiene un prefab válido.";
                return false;
            }

            string itemId =
                NormalizeItemId(definition.ItemId);

            if (seen.TryGetValue(
                    itemId,
                    out RestaurantPlaceableItemDefinition existing))
            {
                if (!ReferenceEquals(
                        existing,
                        definition))
                {
                    error =
                        "El ItemId " +
                        itemId +
                        " resuelve a definiciones distintas entre " +
                        "las fuentes de persistencia.";
                    return false;
                }

                continue;
            }

            seen.Add(
                itemId,
                definition);
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

    private static int CompareDefinitions(
        RestaurantPlaceableItemDefinition first,
        RestaurantPlaceableItemDefinition second)
    {
        return string.Compare(
            first != null
                ? first.ItemId
                : string.Empty,
            second != null
                ? second.ItemId
                : string.Empty,
            StringComparison.Ordinal);
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
