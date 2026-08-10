using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adaptador tipado entre 2.3A y el catálogo canónico de ingredientes.
///
/// La única fuente admitida es BistroBuilderRecipeCatalogService ->
/// BistroBuilderIngredientCatalog -> BistroBuilderIngredientDefinition.
/// No existe reflexión por nombres ni fallback a inventario.
/// </summary>
public static class BistroBuilderCanonicalIngredientDiscovery
{
    public static bool TryDiscover(
        out List<BistroBuilderSupplierIngredientDescriptor> ingredients,
        out string error)
    {
        ingredients = new List<BistroBuilderSupplierIngredientDescriptor>();
        error = string.Empty;

        BistroBuilderRecipeCatalogService[] services =
            Resources.FindObjectsOfTypeAll<BistroBuilderRecipeCatalogService>();

        BistroBuilderRecipeCatalogService selected = null;
        BistroBuilderIngredientCatalog selectedCatalog = null;

        for (int i = 0; i < services.Length; i++)
        {
            BistroBuilderRecipeCatalogService candidate = services[i];
            if (candidate == null ||
                candidate.gameObject == null ||
                !candidate.gameObject.scene.IsValid() ||
                !candidate.gameObject.activeInHierarchy ||
                !candidate.enabled ||
                candidate.IngredientCatalog == null)
            {
                continue;
            }

            if (selected == null)
            {
                selected = candidate;
                selectedCatalog = candidate.IngredientCatalog;
                continue;
            }

            if (!ReferenceEquals(selectedCatalog, candidate.IngredientCatalog))
            {
                error =
                    "2.3A detectó más de una autoridad activa de ingredientes " +
                    "con catálogos distintos. Proveedores no elegirá una arbitrariamente.";
                return false;
            }
        }

        if (selected == null || selectedCatalog == null)
        {
            error =
                "2.3A no encuentra un BistroBuilderRecipeCatalogService activo " +
                "con BistroBuilderIngredientCatalog válido.";
            return false;
        }

        return TryCreateDescriptorsFromCatalog(
            selectedCatalog,
            out ingredients,
            out error);
    }

    /// <summary>
    /// Adaptación tipada reutilizable en runtime y herramientas Editor.
    /// </summary>
    public static bool TryCreateDescriptorsFromCatalog(
        BistroBuilderIngredientCatalog catalog,
        out List<BistroBuilderSupplierIngredientDescriptor> ingredients,
        out string error)
    {
        ingredients = new List<BistroBuilderSupplierIngredientDescriptor>();
        error = string.Empty;

        if (catalog == null)
        {
            error = "Falta BistroBuilderIngredientCatalog.";
            return false;
        }

        if (!catalog.TryRebuildIndex(out error))
        {
            error = "El catálogo canónico de ingredientes no es válido: " + error;
            return false;
        }

        List<BistroBuilderIngredientDefinition> definitions =
            new List<BistroBuilderIngredientDefinition>(catalog.DefinitionCount);
        catalog.CopyDefinitionsTo(definitions);

        if (definitions.Count == 0)
        {
            error = "El catálogo canónico de ingredientes está vacío.";
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
        {
            if (!TryCreateDescriptor(
                    definitions[i],
                    out BistroBuilderSupplierIngredientDescriptor descriptor,
                    out error))
            {
                return false;
            }

            if (!ids.Add(descriptor.IngredientId))
            {
                error =
                    "IngredientId duplicado al adaptar Proveedores: " +
                    descriptor.IngredientId + ".";
                return false;
            }

            ingredients.Add(descriptor);
        }

        ingredients.Sort(
            (left, right) => string.Compare(
                left.IngredientId,
                right.IngredientId,
                StringComparison.Ordinal));
        return true;
    }

    public static bool TryCreateDescriptor(
        BistroBuilderIngredientDefinition definition,
        out BistroBuilderSupplierIngredientDescriptor descriptor,
        out string error)
    {
        descriptor = null;
        error = string.Empty;

        if (definition == null)
        {
            error = "Existe una definición canónica de ingrediente nula.";
            return false;
        }

        if (!definition.TryValidate(out error))
        {
            error =
                "El ingrediente " + (definition.IngredientId ?? string.Empty) +
                " no supera su validación canónica: " + error;
            return false;
        }

        if (!definition.TryGetReferencePackCanonicalMilliUnits(
                out long referencePackCanonicalMilliUnits,
                out error))
        {
            return false;
        }

        descriptor = new BistroBuilderSupplierIngredientDescriptor(
            definition.IngredientId,
            definition.DisplayName,
            definition.BaseUnit,
            definition.Category,
            definition.StorageType,
            definition.Perishable,
            referencePackCanonicalMilliUnits,
            definition.ReferencePackPriceCents);
        return true;
    }
}
