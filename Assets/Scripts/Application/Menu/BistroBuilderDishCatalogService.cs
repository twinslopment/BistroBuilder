using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puerta runtime de solo lectura al catálogo canónico de platos.
/// Centraliza validación e indexación y evita búsquedas por AssetDatabase
/// o nombres de objetos durante la simulación.
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

    public BistroBuilderDishCatalog Catalog => catalog;

    public int DefinitionCount => catalog != null
        ? catalog.DefinitionCount
        : 0;

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
                DefinitionCount + " plato(s) canónico(s).",
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

        error = string.Empty;
        return true;
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

        return catalog != null &&
               catalog.TryGetDefinition(dishId, out definition);
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

        if (catalog == null)
        {
            destination.Clear();
            return;
        }

        catalog.CopyDefinitionsTo(destination);
    }
}
