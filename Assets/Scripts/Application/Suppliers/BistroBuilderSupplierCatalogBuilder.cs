using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generador determinista de SEMILLAS iniciales de productos de proveedor.
///
/// No es la autoridad runtime: desde supplier.catalog v2 los SKU quedan
/// persistidos explícitamente en BistroBuilderSupplierCatalogSettings. Este
/// builder solo crea los productos base al instalar/migrar o en tests.
///
/// Invariantes de la semilla A1:
/// - Cada ingrediente obtiene dos SKU base.
/// - Un SKU pertenece al generalista y otro a un especialista distinto.
/// - La especialización se decide por categoría/almacenamiento canónicos,
///   nunca por palabras del nombre visible.
/// - El envase usa la cantidad de referencia ya autorada en el ingrediente.
/// - Todo dinero se calcula en céntimos mediante decimal + puntos básicos.
/// - IsCatalogAvailable pertenece al SKU y NO copia IsCatalogEnabled del proveedor.
/// </summary>
public static class BistroBuilderSupplierCatalogBuilder
{
    public const int RecommendedDistinctSuppliersPerIngredient = 2;

    [Obsolete("Usar RecommendedDistinctSuppliersPerIngredient. La cobertura es política de contenido, no integridad estructural.")]
    public const int MinimumDistinctSuppliersPerIngredient = RecommendedDistinctSuppliersPerIngredient;

    [Obsolete("Usar RecommendedDistinctSuppliersPerIngredient. El catálogo v2 permite SKU adicionales.")]
    public const int ExpectedOffersPerIngredient = RecommendedDistinctSuppliersPerIngredient;
    public const int GeneralOfferAdjustmentBasisPoints = 10000;
    public const int SpecialistOfferAdjustmentBasisPoints = 9700;

    public static List<BistroBuilderSupplierProductDefinition> BuildProducts(
        IReadOnlyList<BistroBuilderSupplierDefinition> suppliers,
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> ingredients)
    {
        List<BistroBuilderSupplierProductDefinition> products =
            new List<BistroBuilderSupplierProductDefinition>();

        if (suppliers == null || ingredients == null)
        {
            return products;
        }

        Dictionary<string, BistroBuilderSupplierDefinition> supplierById =
            new Dictionary<string, BistroBuilderSupplierDefinition>(StringComparer.Ordinal);

        for (int i = 0; i < suppliers.Count; i++)
        {
            BistroBuilderSupplierDefinition supplier = suppliers[i];
            if (supplier != null &&
                BistroBuilderMenuIdUtility.IsValidStableId(supplier.SupplierId) &&
                !supplierById.ContainsKey(supplier.SupplierId))
            {
                supplierById.Add(supplier.SupplierId, supplier);
            }
        }

        if (!supplierById.TryGetValue(
                BistroBuilderSupplierCatalogDefaults.GeneralSupplierId,
                out BistroBuilderSupplierDefinition generalSupplier))
        {
            return products;
        }

        for (int i = 0; i < ingredients.Count; i++)
        {
            BistroBuilderSupplierIngredientDescriptor ingredient = ingredients[i];
            if (ingredient == null ||
                !BistroBuilderMenuIdUtility.IsValidStableId(ingredient.IngredientId))
            {
                continue;
            }

            AddProduct(
                products,
                generalSupplier,
                ingredient,
                GeneralOfferAdjustmentBasisPoints);

            string specialistId = ChooseSpecialistId(ingredient);
            if (supplierById.TryGetValue(
                    specialistId,
                    out BistroBuilderSupplierDefinition specialist) &&
                !string.Equals(
                    specialist.SupplierId,
                    generalSupplier.SupplierId,
                    StringComparison.Ordinal))
            {
                AddProduct(
                    products,
                    specialist,
                    ingredient,
                    SpecialistOfferAdjustmentBasisPoints);
            }
        }

        products.Sort(
            (left, right) => string.Compare(
                left.ProductId,
                right.ProductId,
                StringComparison.Ordinal));

        return products;
    }

    private static void AddProduct(
        List<BistroBuilderSupplierProductDefinition> products,
        BistroBuilderSupplierDefinition supplier,
        BistroBuilderSupplierIngredientDescriptor ingredient,
        int offerAdjustmentBasisPoints)
    {
        long packageCanonicalMilliUnits = ingredient.ReferencePackCanonicalMilliUnits;
        if (packageCanonicalMilliUnits <= 0L)
        {
            return;
        }

        int packPriceCents = CalculateAdjustedPackPriceCents(
            ingredient.ReferencePackPriceCents,
            supplier.SeedPriceFactorBasisPoints,
            offerAdjustmentBasisPoints);

        string packageLabel = BuildPackageLabel(
            packageCanonicalMilliUnits,
            ingredient.BaseUnit);

        string productId = BuildProductId(
            supplier.SupplierId,
            ingredient.IngredientId);

        products.Add(
            new BistroBuilderSupplierProductDefinition(
                productId,
                supplier.SupplierId,
                ingredient.IngredientId,
                ingredient.DisplayName + " · " + packageLabel,
                packageLabel,
                ingredient.BaseUnit,
                packageCanonicalMilliUnits,
                packPriceCents,
                1,
                supplier.DefaultLeadTimeDays,
                true,
                supplier.CurrencyCode));
    }

    /// <summary>
    /// Convierte el coste de referencia en precio de proveedor. El mínimo de
    /// 1 céntimo evita productos comprables a coste cero si un ingrediente de
    /// autoría tiene referencia 0; el dato original no se modifica.
    /// </summary>
    public static int CalculateAdjustedPackPriceCents(
        int referencePackPriceCents,
        int supplierFactorBasisPoints,
        int offerAdjustmentBasisPoints)
    {
        int reference = Math.Max(0, referencePackPriceCents);
        int supplierFactor = Math.Max(1, supplierFactorBasisPoints);
        int offerFactor = Math.Max(1, offerAdjustmentBasisPoints);

        decimal raw =
            (decimal)reference *
            supplierFactor *
            offerFactor /
            BistroBuilderSupplierDefinition.BasisPointsPerOne /
            BistroBuilderSupplierDefinition.BasisPointsPerOne;

        decimal rounded = decimal.Round(
            raw,
            0,
            MidpointRounding.AwayFromZero);

        if (rounded < 1m)
        {
            return 1;
        }

        if (rounded > BistroBuilderSupplierProductDefinition.MaximumPackPriceCents)
        {
            return BistroBuilderSupplierProductDefinition.MaximumPackPriceCents;
        }

        return (int)rounded;
    }

    /// <summary>
    /// Compara dos ofertas por coste normalizado por unidad canónica. No
    /// compara precio de envase, porque dos proveedores futuros podrán vender
    /// formatos diferentes. En empate usa ProductId para resultado estable.
    /// </summary>
    public static int CompareNormalizedUnitCost(
        BistroBuilderSupplierProductDefinition left,
        BistroBuilderSupplierProductDefinition right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        if (left.PackageCanonicalMilliUnits <= 0L &&
            right.PackageCanonicalMilliUnits <= 0L)
        {
            return string.Compare(left.ProductId, right.ProductId, StringComparison.Ordinal);
        }

        if (left.PackageCanonicalMilliUnits <= 0L)
        {
            return 1;
        }

        if (right.PackageCanonicalMilliUnits <= 0L)
        {
            return -1;
        }

        decimal leftCost =
            (decimal)left.PackPriceCents / left.PackageCanonicalMilliUnits;
        decimal rightCost =
            (decimal)right.PackPriceCents / right.PackageCanonicalMilliUnits;

        int priceComparison = leftCost.CompareTo(rightCost);
        if (priceComparison != 0)
        {
            return priceComparison;
        }

        return string.Compare(left.ProductId, right.ProductId, StringComparison.Ordinal);
    }

    public static string ChooseSpecialistId(
        BistroBuilderSupplierIngredientDescriptor ingredient)
    {
        if (ingredient == null)
        {
            return BistroBuilderSupplierCatalogDefaults.PantrySupplierId;
        }

        switch (ingredient.Category)
        {
            case BistroBuilderIngredientCategory.Produce:
            case BistroBuilderIngredientCategory.DairyAndEggs:
                return BistroBuilderSupplierCatalogDefaults.FreshSupplierId;

            case BistroBuilderIngredientCategory.Meat:
            case BistroBuilderIngredientCategory.FishAndSeafood:
                return BistroBuilderSupplierCatalogDefaults.PremiumSupplierId;

            case BistroBuilderIngredientCategory.DryGoods:
            case BistroBuilderIngredientCategory.Condiment:
            case BistroBuilderIngredientCategory.Beverage:
                return BistroBuilderSupplierCatalogDefaults.PantrySupplierId;

            case BistroBuilderIngredientCategory.PreparedProduct:
                return ingredient.Perishable ||
                       ingredient.StorageType == BistroBuilderIngredientStorageType.Refrigerated ||
                       ingredient.StorageType == BistroBuilderIngredientStorageType.Frozen
                    ? BistroBuilderSupplierCatalogDefaults.FreshSupplierId
                    : BistroBuilderSupplierCatalogDefaults.PantrySupplierId;

            default:
                uint hash = StableHash(ingredient.IngredientId);
                switch (hash % 3u)
                {
                    case 0u:
                        return BistroBuilderSupplierCatalogDefaults.FreshSupplierId;
                    case 1u:
                        return BistroBuilderSupplierCatalogDefaults.PantrySupplierId;
                    default:
                        return BistroBuilderSupplierCatalogDefaults.PremiumSupplierId;
                }
        }
    }

    public static string BuildPackageLabel(
        long canonicalMilliUnits,
        BistroBuilderMeasurementUnit baseUnit)
    {
        if (canonicalMilliUnits <= 0L)
        {
            return "Formato inválido";
        }

        double baseAmount =
            canonicalMilliUnits /
            (double)BistroBuilderMeasurementUtility.MilliUnitsPerCanonicalUnit;

        switch (baseUnit)
        {
            case BistroBuilderMeasurementUnit.Gram:
                if (baseAmount >= 1000d)
                {
                    return "Envase " + FormatAmount(baseAmount / 1000d) + " kg";
                }
                return "Envase " + FormatAmount(baseAmount) + " g";

            case BistroBuilderMeasurementUnit.Milliliter:
                if (baseAmount >= 1000d)
                {
                    return "Envase " + FormatAmount(baseAmount / 1000d) + " L";
                }
                return "Envase " + FormatAmount(baseAmount) + " ml";

            case BistroBuilderMeasurementUnit.Unit:
                return "Caja " + FormatAmount(baseAmount) + " ud";

            case BistroBuilderMeasurementUnit.Portion:
                return "Pack " + FormatAmount(baseAmount) + " ración(es)";

            default:
                return "Formato profesional";
        }
    }

    public static string BuildProductId(string supplierId, string ingredientId)
    {
        string supplier = BistroBuilderMenuIdUtility.NormalizeStableId(supplierId);
        string ingredient = BistroBuilderMenuIdUtility.NormalizeStableId(ingredientId);
        string readable = supplier + "_" + ingredient;

        if (readable.Length <= 96 && BistroBuilderMenuIdUtility.IsValidStableId(readable))
        {
            return readable;
        }

        ulong hash = StableHash64(supplier + "|" + ingredient);
        return "product_" + hash.ToString("x16");
    }

    /// <summary>
    /// Genera un ProductId estable para un formato adicional del mismo
    /// proveedor/ingrediente. El ProductId base permanece intacto para no
    /// romper referencias ya instaladas; las variantes añaden una clave de
    /// SKU explícita y normalizada.
    /// </summary>
    public static string BuildVariantProductId(
        string supplierId,
        string ingredientId,
        string variantKey)
    {
        string supplier = BistroBuilderMenuIdUtility.NormalizeStableId(supplierId);
        string ingredient = BistroBuilderMenuIdUtility.NormalizeStableId(ingredientId);
        string variant = BistroBuilderMenuIdUtility.NormalizeStableId(variantKey);

        if (string.IsNullOrWhiteSpace(variant))
        {
            return BuildProductId(supplier, ingredient);
        }

        string readable = supplier + "_" + ingredient + "_" + variant;
        if (readable.Length <= 96 && BistroBuilderMenuIdUtility.IsValidStableId(readable))
        {
            return readable;
        }

        ulong hash = StableHash64(supplier + "|" + ingredient + "|" + variant);
        return "product_" + hash.ToString("x16");
    }

    public static uint StableHash(string value)
    {
        unchecked
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            string safe = value ?? string.Empty;

            for (int i = 0; i < safe.Length; i++)
            {
                hash ^= safe[i];
                hash *= prime;
            }

            return hash;
        }
    }

    public static ulong StableHash64(string value)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            string safe = value ?? string.Empty;

            for (int i = 0; i < safe.Length; i++)
            {
                hash ^= safe[i];
                hash *= prime;
            }

            return hash;
        }
    }

    private static string FormatAmount(double amount)
    {
        double rounded = Math.Round(amount, 3, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
