using System;
using System.Collections.Generic;

/// <summary>
/// Reglas estructurales compartidas de supplier.catalog v2.
///
/// Integridad estructural y disponibilidad operativa son conceptos distintos:
/// un proveedor inactivo o un producto temporalmente no disponible no corrompe
/// el catálogo; solo puede dejar huecos operativos que se reportan como warning.
///
/// El modelo permite múltiples formatos del mismo ingrediente por proveedor.
/// La cobertura se mide por proveedores DISTINTOS, no por número bruto de SKU.
/// </summary>
public static class BistroBuilderSupplierCatalogValidator
{
    public static BistroBuilderSupplierCatalogValidationResult Validate(
        IReadOnlyList<BistroBuilderSupplierDefinition> suppliers,
        IReadOnlyList<BistroBuilderSupplierProductDefinition> products,
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> ingredients,
        int recommendedDistinctSuppliersPerIngredient,
        bool reportOperationalGapsAsWarnings)
    {
        BistroBuilderSupplierCatalogValidationResult result =
            new BistroBuilderSupplierCatalogValidationResult();

        if (recommendedDistinctSuppliersPerIngredient < 0)
        {
            result.AddError(
                "La cobertura recomendada no puede ser negativa.");
            return result;
        }

        if (suppliers == null || suppliers.Count == 0)
        {
            result.AddError("No existe ningún proveedor canónico.");
            return result;
        }

        Dictionary<string, BistroBuilderSupplierDefinition> supplierById =
            new Dictionary<string, BistroBuilderSupplierDefinition>(StringComparer.Ordinal);
        HashSet<string> supplierNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> supplierCurrencies =
            new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < suppliers.Count; i++)
        {
            BistroBuilderSupplierDefinition supplier = suppliers[i];
            if (supplier == null)
            {
                result.AddError("Existe una definición de proveedor nula.");
                continue;
            }

            if (!BistroBuilderMenuIdUtility.IsValidStableId(supplier.SupplierId))
            {
                result.AddError(
                    "SupplierId inválido o no normalizado: " +
                    (supplier.SupplierId ?? string.Empty) + ".");
                continue;
            }

            if (supplierById.ContainsKey(supplier.SupplierId))
            {
                result.AddError("SupplierId duplicado: " + supplier.SupplierId + ".");
            }
            else
            {
                supplierById.Add(supplier.SupplierId, supplier);
            }

            if (string.IsNullOrWhiteSpace(supplier.DisplayName))
            {
                result.AddError("El proveedor " + supplier.SupplierId + " no tiene nombre.");
            }
            else if (!supplierNames.Add(supplier.DisplayName.Trim()))
            {
                result.AddWarning(
                    "Hay proveedores con el mismo nombre visible: " +
                    supplier.DisplayName.Trim() + ".");
            }

            if (supplier.MinimumOrderCents < 0 ||
                supplier.MinimumOrderCents >
                    BistroBuilderSupplierDefinition.MaximumMinimumOrderCents)
            {
                result.AddError(
                    "Pedido mínimo fuera de rango en " + supplier.SupplierId + ".");
            }

            if (supplier.DefaultLeadTimeDays < 0 ||
                supplier.DefaultLeadTimeDays >
                    BistroBuilderSupplierDefinition.MaximumLeadTimeDays)
            {
                result.AddError(
                    "Plazo estándar fuera de rango en " + supplier.SupplierId + ".");
            }

            if (!IsValidCurrencyCode(supplier.CurrencyCode))
            {
                result.AddError(
                    "Código de moneda inválido en " + supplier.SupplierId + ".");
            }
            else
            {
                supplierCurrencies.Add(supplier.CurrencyCode);
            }

            if (supplier.SeedPriceFactorBasisPoints < 1 ||
                supplier.SeedPriceFactorBasisPoints >
                    BistroBuilderSupplierDefinition.MaximumPriceFactorBasisPoints)
            {
                result.AddError(
                    "Factor de precio fuera de rango en " + supplier.SupplierId + ".");
            }
        }

        if (supplierCurrencies.Count > 1)
        {
            result.AddError(
                "supplier.catalog mezcla monedas de proveedor. 2.3A exige una moneda " +
                "económica única; comparar precios sin un sistema FX sería incorrecto.");
        }

        Dictionary<string, BistroBuilderSupplierIngredientDescriptor> ingredientById =
            new Dictionary<string, BistroBuilderSupplierIngredientDescriptor>(StringComparer.Ordinal);

        if (ingredients == null || ingredients.Count == 0)
        {
            result.AddError(
                "2.3A no dispone de ingredientes canónicos para enlazar productos.");
        }
        else
        {
            for (int i = 0; i < ingredients.Count; i++)
            {
                BistroBuilderSupplierIngredientDescriptor ingredient = ingredients[i];
                if (ingredient == null)
                {
                    result.AddError("Existe un descriptor de ingrediente nulo.");
                    continue;
                }

                if (!BistroBuilderMenuIdUtility.IsValidStableId(ingredient.IngredientId))
                {
                    result.AddError(
                        "IngredientId inválido en la lectura 2.3A: " +
                        (ingredient.IngredientId ?? string.Empty) + ".");
                    continue;
                }

                if (ingredientById.ContainsKey(ingredient.IngredientId))
                {
                    result.AddError(
                        "IngredientId duplicado en 2.3A: " +
                        ingredient.IngredientId + ".");
                }
                else
                {
                    ingredientById.Add(ingredient.IngredientId, ingredient);
                }

                if (string.IsNullOrWhiteSpace(ingredient.DisplayName))
                {
                    result.AddError(
                        "El ingrediente " + ingredient.IngredientId +
                        " no tiene nombre visible.");
                }

                if (!BistroBuilderMeasurementUtility.IsCanonicalBaseUnit(
                        ingredient.BaseUnit))
                {
                    result.AddError(
                        "El ingrediente " + ingredient.IngredientId +
                        " no usa una unidad base canónica.");
                }

                if (!Enum.IsDefined(
                        typeof(BistroBuilderIngredientCategory),
                        ingredient.Category) ||
                    !Enum.IsDefined(
                        typeof(BistroBuilderIngredientStorageType),
                        ingredient.StorageType))
                {
                    result.AddError(
                        "El ingrediente " + ingredient.IngredientId +
                        " contiene clasificación desconocida.");
                }

                if (ingredient.ReferencePackCanonicalMilliUnits <= 0L ||
                    ingredient.ReferencePackCanonicalMilliUnits >
                        BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
                {
                    result.AddError(
                        "El ingrediente " + ingredient.IngredientId +
                        " tiene un envase de referencia fuera de rango.");
                }

                if (ingredient.ReferencePackPriceCents < 0 ||
                    ingredient.ReferencePackPriceCents >
                        BistroBuilderIngredientDefinition.MaximumReferencePackPriceCents)
                {
                    result.AddError(
                        "El ingrediente " + ingredient.IngredientId +
                        " tiene un precio de referencia fuera de rango.");
                }
            }
        }

        HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> commercialSignatures =
            new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, List<BistroBuilderSupplierProductDefinition>> offersByIngredient =
            new Dictionary<string, List<BistroBuilderSupplierProductDefinition>>(StringComparer.Ordinal);
        Dictionary<string, int> productCountBySupplier =
            new Dictionary<string, int>(StringComparer.Ordinal);

        if (products == null || products.Count == 0)
        {
            result.AddError("El catálogo de productos de proveedor está vacío.");
        }
        else
        {
            for (int i = 0; i < products.Count; i++)
            {
                BistroBuilderSupplierProductDefinition product = products[i];
                if (product == null)
                {
                    result.AddError("Existe un producto de proveedor nulo.");
                    continue;
                }

                if (!BistroBuilderMenuIdUtility.IsValidStableId(product.ProductId))
                {
                    result.AddError(
                        "ProductId inválido o no normalizado: " +
                        (product.ProductId ?? string.Empty) + ".");
                }
                else if (!productIds.Add(product.ProductId))
                {
                    result.AddError("ProductId duplicado: " + product.ProductId + ".");
                }

                BistroBuilderSupplierDefinition supplier = null;
                if (!supplierById.TryGetValue(product.SupplierId, out supplier))
                {
                    result.AddError(
                        "El producto " + product.ProductId +
                        " referencia SupplierId inexistente: " +
                        product.SupplierId + ".");
                }
                else
                {
                    productCountBySupplier.TryGetValue(
                        product.SupplierId,
                        out int count);
                    productCountBySupplier[product.SupplierId] = count + 1;

                    if (!string.Equals(
                            product.CurrencyCode,
                            supplier.CurrencyCode,
                            StringComparison.Ordinal))
                    {
                        result.AddError(
                            "El producto " + product.ProductId +
                            " usa una moneda distinta a su proveedor.");
                    }
                }

                BistroBuilderSupplierIngredientDescriptor ingredient = null;
                if (!ingredientById.TryGetValue(product.IngredientId, out ingredient))
                {
                    result.AddError(
                        "El producto " + product.ProductId +
                        " referencia IngredientId no canónico: " +
                        product.IngredientId + ".");
                }
                else
                {
                    if (product.BaseUnit != ingredient.BaseUnit)
                    {
                        result.AddError(
                            "El producto " + product.ProductId +
                            " usa una unidad base distinta al ingrediente.");
                    }

                    if (!offersByIngredient.TryGetValue(
                            product.IngredientId,
                            out List<BistroBuilderSupplierProductDefinition> offers))
                    {
                        offers = new List<BistroBuilderSupplierProductDefinition>();
                        offersByIngredient.Add(product.IngredientId, offers);
                    }
                    offers.Add(product);
                }

                if (string.IsNullOrWhiteSpace(product.DisplayName))
                {
                    result.AddError(
                        "El producto " + product.ProductId +
                        " no tiene nombre visible.");
                }

                if (string.IsNullOrWhiteSpace(product.PackageLabel))
                {
                    result.AddError(
                        "El producto " + product.ProductId +
                        " no tiene formato visible.");
                }

                if (!BistroBuilderMeasurementUtility.IsCanonicalBaseUnit(product.BaseUnit))
                {
                    result.AddError(
                        "El producto " + product.ProductId +
                        " no usa una unidad base canónica.");
                }

                if (product.PackageCanonicalMilliUnits <= 0L ||
                    product.PackageCanonicalMilliUnits >
                        BistroBuilderSupplierProductDefinition.MaximumPackageCanonicalMilliUnits)
                {
                    result.AddError(
                        "Cantidad de envase fuera de rango seguro para pedidos en " +
                        product.ProductId + ".");
                }

                if (product.PackPriceCents < 1 ||
                    product.PackPriceCents >
                        BistroBuilderSupplierProductDefinition.MaximumPackPriceCents)
                {
                    result.AddError(
                        "Precio de envase fuera de rango en " +
                        product.ProductId + ".");
                }

                if (product.MinimumPacks < 1 ||
                    product.MinimumPacks >
                        BistroBuilderSupplierProductDefinition.MaximumMinimumPacks)
                {
                    result.AddError(
                        "Mínimo de packs fuera de rango en " +
                        product.ProductId + ".");
                }

                if (product.LeadTimeDays < 0 ||
                    product.LeadTimeDays >
                        BistroBuilderSupplierProductDefinition.MaximumLeadTimeDays)
                {
                    result.AddError(
                        "Plazo de producto fuera de rango en " +
                        product.ProductId + ".");
                }

                if (!IsValidCurrencyCode(product.CurrencyCode))
                {
                    result.AddError(
                        "Código de moneda inválido en " + product.ProductId + ".");
                }

                /*
                 * Se permiten varios formatos por proveedor/ingrediente, pero
                 * dos SKU comercialmente idénticos son ambigüedad sin valor.
                 */
                string commercialSignature =
                    product.SupplierId + "|" +
                    product.IngredientId + "|" +
                    product.PackageCanonicalMilliUnits + "|" +
                    product.PackPriceCents + "|" +
                    product.MinimumPacks + "|" +
                    product.LeadTimeDays + "|" +
                    product.CurrencyCode;

                if (!commercialSignatures.Add(commercialSignature))
                {
                    result.AddError(
                        "Hay dos ProductId comercialmente idénticos para " +
                        product.SupplierId + " / " + product.IngredientId + ".");
                }
            }
        }

        foreach (KeyValuePair<string, BistroBuilderSupplierIngredientDescriptor> pair
                 in ingredientById)
        {
            string ingredientId = pair.Key;
            offersByIngredient.TryGetValue(
                ingredientId,
                out List<BistroBuilderSupplierProductDefinition> offers);

            HashSet<string> distinctSuppliers =
                new HashSet<string>(StringComparer.Ordinal);
            if (offers != null)
            {
                for (int i = 0; i < offers.Count; i++)
                {
                    if (offers[i] != null &&
                        supplierById.ContainsKey(offers[i].SupplierId))
                    {
                        distinctSuppliers.Add(offers[i].SupplierId);
                    }
                }
            }

            if (reportOperationalGapsAsWarnings &&
                distinctSuppliers.Count < recommendedDistinctSuppliersPerIngredient)
            {
                result.AddWarning(
                    "El ingrediente " + ingredientId + " solo tiene " +
                    distinctSuppliers.Count + " proveedor(es) estructural(es) distinto(s); " +
                    "la política de contenido recomienda " +
                    recommendedDistinctSuppliersPerIngredient + ".");
            }

            if (reportOperationalGapsAsWarnings)
            {
                int purchasableCount = 0;
                if (offers != null)
                {
                    for (int i = 0; i < offers.Count; i++)
                    {
                        BistroBuilderSupplierProductDefinition offer = offers[i];
                        if (offer == null || !offer.IsCatalogAvailable)
                        {
                            continue;
                        }

                        if (supplierById.TryGetValue(
                                offer.SupplierId,
                                out BistroBuilderSupplierDefinition supplier) &&
                            supplier.IsCatalogEnabled)
                        {
                            purchasableCount++;
                        }
                    }
                }

                if (purchasableCount == 0)
                {
                    result.AddWarning(
                        "El ingrediente " + ingredientId +
                        " no tiene ninguna oferta comprable en este momento.");
                }
            }
        }

        foreach (KeyValuePair<string, BistroBuilderSupplierDefinition> pair in supplierById)
        {
            if (!pair.Value.IsCatalogEnabled)
            {
                continue;
            }

            productCountBySupplier.TryGetValue(pair.Key, out int count);
            if (count == 0)
            {
                result.AddWarning(
                    "El proveedor activo " + pair.Key +
                    " no tiene productos asignados.");
            }
        }

        return result;
    }

    /// <summary>
    /// Compatibilidad temporal con 2.3A v1. La cobertura deja de ser criterio
    /// de corrupción: se reporta como política/warning, permitiendo ingredientes
    /// exclusivos sin rediseñar el dominio.
    /// </summary>
    [Obsolete("Usar la sobrecarga con cobertura de proveedores explícita.")]
    public static BistroBuilderSupplierCatalogValidationResult Validate(
        IReadOnlyList<BistroBuilderSupplierDefinition> suppliers,
        IReadOnlyList<BistroBuilderSupplierProductDefinition> products,
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> ingredients,
        bool requireTwoOffersPerIngredient)
    {
        return Validate(
            suppliers,
            products,
            ingredients,
            requireTwoOffersPerIngredient ? 2 : 0,
            reportOperationalGapsAsWarnings: true);
    }

    public static bool IsValidCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
        {
            return false;
        }

        for (int i = 0; i < currencyCode.Length; i++)
        {
            char c = currencyCode[i];
            if (c < 'A' || c > 'Z')
            {
                return false;
            }
        }

        return true;
    }
}
