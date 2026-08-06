using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puerta runtime al catálogo de platos.
///
/// Conserva los assets canónicos como base inmutable y permite aplicar una
/// capa runtime de sobrescrituras y platos creados por el jugador. La capa
/// runtime se captura y restaura dentro de menu.state desde 2.1G3.
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

    // Un plato canónico queda suprimido cuando existe una sobrescritura
    // persistente que no puede reconstruirse temporalmente. Así evitamos que
    // una receta editada vuelva silenciosamente a su definición canónica.
    private readonly HashSet<string> suppressedCanonicalDishIds =
        new HashSet<string>(StringComparer.Ordinal);

    public event Action CatalogChanged;

    public BistroBuilderDishCatalog Catalog => catalog;

    public int CanonicalDefinitionCount => catalog != null
        ? catalog.DefinitionCount
        : 0;

    public int RuntimeDefinitionCount => runtimeDefinitions.Count;

    public int SuppressedCanonicalDefinitionCount =>
        suppressedCanonicalDishIds.Count;

    public int DefinitionCount
    {
        get
        {
            int count = CanonicalDefinitionCount;

            if (catalog != null)
            {
                foreach (string suppressedId in suppressedCanonicalDishIds)
                {
                    if (!runtimeByDishId.ContainsKey(suppressedId) &&
                        catalog.Contains(suppressedId))
                    {
                        count--;
                    }
                }
            }

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

        if (suppressedCanonicalDishIds.Contains(normalized))
        {
            definition = null;
            return false;
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

            bool hasRuntime = runtimeByDishId.TryGetValue(
                canonical.DishId,
                out BistroBuilderDishDefinition runtime
            );
            BistroBuilderDishDefinition effective = hasRuntime
                ? runtime
                : suppressedCanonicalDishIds.Contains(canonical.DishId)
                    ? null
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

    public bool IsCanonicalDefinitionSuppressed(string dishId)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            dishId
        );
        return !string.IsNullOrWhiteSpace(normalized) &&
               suppressedCanonicalDishIds.Contains(normalized);
    }

    public void CopySuppressedCanonicalDishIdsTo(List<string> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        destination.AddRange(suppressedCanonicalDishIds);
        destination.Sort(StringComparer.Ordinal);
    }

    public bool TryReplaceSuppressedCanonicalDishIds(
        IList<string> dishIds,
        out string error,
        bool publishChange = true
    )
    {
        HashSet<string> next = new HashSet<string>(StringComparer.Ordinal);

        if (dishIds != null)
        {
            for (int index = 0; index < dishIds.Count; index++)
            {
                string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
                    dishIds[index]
                );

                if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized) ||
                    !string.Equals(
                        normalized,
                        dishIds[index],
                        StringComparison.Ordinal
                    ))
                {
                    error = "La supresión canónica contiene un DishId inválido.";
                    return false;
                }

                if (catalog == null || !catalog.Contains(normalized))
                {
                    error = "La supresión canónica referencia un plato que no " +
                            "existe en el catálogo base: " + normalized + ".";
                    return false;
                }

                if (!next.Add(normalized))
                {
                    error = "La supresión canónica repite el DishId " +
                            normalized + ".";
                    return false;
                }
            }
        }

        suppressedCanonicalDishIds.Clear();

        foreach (string dishId in next)
        {
            suppressedCanonicalDishIds.Add(dishId);
        }

        if (publishChange)
        {
            PublishChanged();
        }

        error = string.Empty;
        return true;
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
