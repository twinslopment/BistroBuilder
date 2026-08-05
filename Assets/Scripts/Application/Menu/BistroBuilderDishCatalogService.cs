using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puerta runtime al catálogo de platos.
///
/// Conserva los assets canónicos como base inmutable y permite aplicar una
/// capa runtime de sobrescrituras y platos creados por el jugador. La capa
/// runtime no se persiste todavía; 2.1G3 añadirá su captura y restauración.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Dish Catalog Service")]
public sealed class BistroBuilderDishCatalogService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderDishCatalog catalog;

    [Header("Depuración")]

    [SerializeField]
    private bool logInitialization = true;

    private readonly List<BistroBuilderDishDefinition> runtimeDefinitions =
        new List<BistroBuilderDishDefinition>(16);

    private readonly Dictionary<string, BistroBuilderDishDefinition>
        runtimeByDishId =
            new Dictionary<string, BistroBuilderDishDefinition>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderDishDefinition> canonicalBuffer =
        new List<BistroBuilderDishDefinition>(32);

    public event Action CatalogChanged;

    public BistroBuilderDishCatalog Catalog => catalog;

    public int CanonicalDefinitionCount => catalog != null
        ? catalog.DefinitionCount
        : 0;

    public int RuntimeDefinitionCount => runtimeDefinitions.Count;

    public int DefinitionCount
    {
        get
        {
            int count = CanonicalDefinitionCount;

            for (int index = 0; index < runtimeDefinitions.Count; index++)
            {
                BistroBuilderDishDefinition definition =
                    runtimeDefinitions[index];

                if (definition != null &&
                    (catalog == null ||
                     !catalog.Contains(definition.DishId)))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int Revision { get; private set; }

    private void Awake()
    {
        if (!RebuildIndex(out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        if (logInitialization)
        {
            Debug.Log(
                "BistroBuilderDishCatalogService ha cargado " +
                CanonicalDefinitionCount + " plato(s) canónico(s) y " +
                RuntimeDefinitionCount + " definición(es) runtime.",
                this
            );
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        if (catalog == null)
        {
            error = "Falta BistroBuilderDishCatalog.";
            return false;
        }

        if (!catalog.TryRebuildIndex(out error))
        {
            return false;
        }

        if (catalog.DefinitionCount == 0)
        {
            error = "El catálogo canónico de platos está vacío.";
            return false;
        }

        return TryRebuildRuntimeIndex(out error);
    }

    public bool RebuildIndex(out string error)
    {
        return ValidateConfiguration(out error);
    }

    public bool TryGetDefinition(
        string dishId,
        out BistroBuilderDishDefinition definition
    )
    {
        definition = null;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            dishId
        );

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (runtimeByDishId.TryGetValue(normalized, out definition))
        {
            return definition != null;
        }

        return catalog != null &&
               catalog.TryGetDefinition(normalized, out definition);
    }

    public bool Contains(string dishId)
    {
        return TryGetDefinition(dishId, out _);
    }

    public void CopyDefinitionsTo(
        List<BistroBuilderDishDefinition> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        canonicalBuffer.Clear();

        if (catalog != null)
        {
            catalog.CopyDefinitionsTo(canonicalBuffer);
        }

        HashSet<string> copied = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < canonicalBuffer.Count; index++)
        {
            BistroBuilderDishDefinition canonical = canonicalBuffer[index];

            if (canonical == null)
            {
                continue;
            }

            BistroBuilderDishDefinition effective =
                runtimeByDishId.TryGetValue(
                    canonical.DishId,
                    out BistroBuilderDishDefinition runtime
                )
                    ? runtime
                    : canonical;

            if (effective != null && copied.Add(effective.DishId))
            {
                destination.Add(effective);
            }
        }

        for (int index = 0; index < runtimeDefinitions.Count; index++)
        {
            BistroBuilderDishDefinition runtime = runtimeDefinitions[index];

            if (runtime != null && copied.Add(runtime.DishId))
            {
                destination.Add(runtime);
            }
        }
    }

    public void CopyRuntimeDefinitionsTo(
        List<BistroBuilderDishDefinition> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();

        for (int index = 0; index < runtimeDefinitions.Count; index++)
        {
            BistroBuilderDishDefinition definition =
                runtimeDefinitions[index];

            if (definition != null)
            {
                destination.Add(definition);
            }
        }
    }

    /// <summary>
    /// Sustituye atómicamente la capa runtime completa.
    /// </summary>
    public bool TryReplaceRuntimeDefinitions(
        IList<BistroBuilderDishDefinition> definitions,
        out string error,
        bool publishChange = true
    )
    {
        Dictionary<string, BistroBuilderDishDefinition> next =
            new Dictionary<string, BistroBuilderDishDefinition>(
                StringComparer.Ordinal
            );
        List<BistroBuilderDishDefinition> nextList =
            new List<BistroBuilderDishDefinition>(
                definitions != null ? definitions.Count : 0
            );

        if (definitions != null)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                BistroBuilderDishDefinition definition = definitions[index];

                if (definition == null)
                {
                    error = "La capa runtime contiene una definición nula.";
                    return false;
                }

                if (!definition.TryValidate(out error))
                {
                    return false;
                }

                if (next.ContainsKey(definition.DishId))
                {
                    error = "La capa runtime repite el DishId " +
                            definition.DishId + ".";
                    return false;
                }

                next.Add(definition.DishId, definition);
                nextList.Add(definition);
            }
        }

        runtimeDefinitions.Clear();
        runtimeDefinitions.AddRange(nextList);
        runtimeByDishId.Clear();

        foreach (KeyValuePair<string, BistroBuilderDishDefinition> pair in next)
        {
            runtimeByDishId.Add(pair.Key, pair.Value);
        }

        if (publishChange)
        {
            PublishChanged();
        }

        error = string.Empty;
        return true;
    }

    public void PublishChanged()
    {
        Revision++;
        CatalogChanged?.Invoke();
    }

    private bool TryRebuildRuntimeIndex(out string error)
    {
        runtimeByDishId.Clear();

        for (int index = 0; index < runtimeDefinitions.Count; index++)
        {
            BistroBuilderDishDefinition definition =
                runtimeDefinitions[index];

            if (definition == null)
            {
                error = "La capa runtime contiene una definición nula.";
                return false;
            }

            if (!definition.TryValidate(out error))
            {
                return false;
            }

            if (runtimeByDishId.ContainsKey(definition.DishId))
            {
                error = "La capa runtime repite el DishId " +
                        definition.DishId + ".";
                return false;
            }

            runtimeByDishId.Add(definition.DishId, definition);
        }

        error = string.Empty;
        return true;
    }
}
