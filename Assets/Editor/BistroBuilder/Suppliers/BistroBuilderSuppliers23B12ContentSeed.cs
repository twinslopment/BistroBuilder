#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Semilla editorial de 2.3B1+B2.
///
/// Objetivos:
/// - Añadir formatos comerciales coherentes a todos los ingredientes canónicos
///   sin reescribir formatos ya existentes.
/// - Crear un catálogo base competitivo para los seis proveedores provisionales.
/// - Mantener precios y nombres como contenido de balance editable, no como
///   reglas de gameplay ocultas.
///
/// La semilla es idempotente: únicamente añade IDs que todavía no existen.
/// No modifica Inventario, Recepciones ni supplier.catalog runtime; esa
/// publicación canónica se resolverá en 2.3B3.
/// </summary>
internal static class BistroBuilderSuppliers23B12ContentSeed
{
    internal sealed class PackageSeed
    {
        public string code;
        public string displayName;
        public string packageType;
        public long netQuantityMicrounits;
        public BistroBuilderCommercialPackageLogisticSize logisticSize;
    }

    private const long OneBaseUnitMicrounits = 1000000L;

    public static int EnsureFormatsForIngredient(
        BistroBuilderIngredientAuthoringRecord ingredient)
    {
        if (ingredient == null ||
            string.IsNullOrWhiteSpace(ingredient.IngredientId))
        {
            return 0;
        }

        if (ingredient.commercialPackages == null)
        {
            ingredient.commercialPackages =
                new List<BistroBuilderCommercialPackageAuthoringRecord>();
        }

        List<PackageSeed> seeds = CreatePackageSeeds(ingredient);
        int added = 0;

        for (int index = 0; index < seeds.Count; index++)
        {
            PackageSeed seed = seeds[index];
            string packageId = BuildPackageId(ingredient.IngredientId, seed.code);

            if (ContainsPackageId(ingredient.commercialPackages, packageId))
            {
                continue;
            }

            BistroBuilderCommercialPackageAuthoringRecord package =
                new BistroBuilderCommercialPackageAuthoringRecord
                {
                    displayName = seed.displayName,
                    packageType = seed.packageType,
                    netQuantityMicrounits = seed.netQuantityMicrounits,
                    logisticSize = seed.logisticSize,
                    isActive = true
                };

            package.AssignStableIdOnce(packageId);
            ingredient.commercialPackages.Add(package);
            added++;
        }

        return added;
    }

    public static int EnsureBaseOffers(
        BistroBuilderSupplierAuthoringDatabase supplierDatabase,
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase)
    {
        if (supplierDatabase == null || ingredientDatabase == null)
        {
            return 0;
        }

        int added = 0;
        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredients =
            ingredientDatabase.Ingredients;

        for (int supplierIndex = 0;
             supplierIndex < supplierDatabase.EditorSuppliers.Count;
             supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier =
                supplierDatabase.EditorSuppliers[supplierIndex];

            if (supplier == null || !supplier.isActive)
            {
                continue;
            }

            if (supplier.baseOffers == null)
            {
                supplier.baseOffers =
                    new List<BistroBuilderSupplierBaseOfferAuthoringRecord>();
            }

            for (int ingredientIndex = 0;
                 ingredientIndex < ingredients.Count;
                 ingredientIndex++)
            {
                BistroBuilderIngredientAuthoringRecord ingredient =
                    ingredients[ingredientIndex];

                if (ingredient == null || !ingredient.isActive ||
                    ingredient.commercialPackages == null ||
                    ingredient.commercialPackages.Count == 0 ||
                    !ShouldSupplierSell(supplier.SupplierId, ingredient))
                {
                    continue;
                }

                BistroBuilderCommercialPackageAuthoringRecord package =
                    SelectPackageForSupplier(supplier.SupplierId, ingredient);

                if (package == null ||
                    string.IsNullOrWhiteSpace(package.PackageFormatId) ||
                    ContainsOfferForPackage(supplier.baseOffers, package.PackageFormatId))
                {
                    continue;
                }

                BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                    CreateBaseOffer(supplier, ingredient, package);

                supplier.baseOffers.Add(offer);
                added++;
            }
        }

        return added;
    }

    public static BistroBuilderSupplierBaseOfferAuthoringRecord CreateBaseOffer(
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderIngredientAuthoringRecord ingredient,
        BistroBuilderCommercialPackageAuthoringRecord package)
    {
        if (supplier == null)
        {
            throw new ArgumentNullException(nameof(supplier));
        }

        if (ingredient == null)
        {
            throw new ArgumentNullException(nameof(ingredient));
        }

        if (package == null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        BistroBuilderSupplierBaseOfferAuthoringRecord offer =
            new BistroBuilderSupplierBaseOfferAuthoringRecord
            {
                ingredientId = ingredient.IngredientId,
                packageFormatId = package.PackageFormatId,
                basePriceCents = EstimateBasePriceCents(
                    supplier.SupplierId,
                    ingredient,
                    package),
                minimumPackageCount = ResolveMinimumPackageCount(
                    supplier.SupplierId,
                    ingredient,
                    package),
                orderIncrement = 1,
                initialAvailability =
                    BistroBuilderSupplierOfferAvailability.Disponible,
                promotionEligible = true,
                overrideLeadTime = false,
                leadTimeOverrideGameHours =
                    Math.Max(0.1f, supplier.defaultLeadTimeGameHours),
                minimumMarketVariationPercent =
                    supplier.priceEvolutionProfile != null
                        ? supplier.priceEvolutionProfile.minimumVariationPercent
                        : -10f,
                maximumMarketVariationPercent =
                    supplier.priceEvolutionProfile != null
                        ? supplier.priceEvolutionProfile.maximumVariationPercent
                        : 15f,
                sortOrder = supplier.baseOffers != null
                    ? supplier.baseOffers.Count
                    : 0,
                isActive = true
            };

        offer.AssignStableIdOnce(
            supplier.SupplierId + "_" + package.PackageFormatId);

        return offer;
    }

    public static long EstimateBasePriceCents(
        string supplierId,
        BistroBuilderIngredientAuthoringRecord ingredient,
        BistroBuilderCommercialPackageAuthoringRecord package)
    {
        if (ingredient == null || package == null)
        {
            return 1;
        }

        decimal referencePerLargeUnit =
            ResolveReferencePricePerLargeUnitCents(ingredient);

        decimal quantityFactor =
            ResolveLargeUnitQuantity(
                ingredient.canonicalUnitSnapshot,
                package.NetQuantityInBaseUnits);

        decimal supplierMultiplier =
            ResolveSupplierPriceMultiplier(supplierId);

        decimal packageMultiplier =
            package.logisticSize == BistroBuilderCommercialPackageLogisticSize.Grande
                ? 0.95m
                : package.logisticSize == BistroBuilderCommercialPackageLogisticSize.Pequeno
                    ? 1.03m
                    : 1.00m;

        decimal raw =
            referencePerLargeUnit *
            quantityFactor *
            supplierMultiplier *
            packageMultiplier;

        long rounded = (long)Math.Ceiling(raw / 5m) * 5L;
        return Math.Max(5L, rounded);
    }

    public static bool ShouldSupplierSell(
        string supplierId,
        BistroBuilderIngredientAuthoringRecord ingredient)
    {
        if (ingredient == null || string.IsNullOrWhiteSpace(supplierId))
        {
            return false;
        }

        string id = supplierId.Trim().ToLowerInvariant();
        string category = NormalizeToken(ingredient.categorySnapshot);

        // Dos alternativas estructurales para todo ingrediente comprable.
        if (id == "supplier_mercado_central" ||
            id == "supplier_hosteleria_express")
        {
            return true;
        }

        if (id == "supplier_huerta_clara")
        {
            return category == "produce";
        }

        if (id == "supplier_carnes_selectas")
        {
            return category == "meat" || category == "preparedproduct";
        }

        if (id == "supplier_costa_fresca")
        {
            return category == "fishandseafood";
        }

        if (id == "supplier_distribuciones_norte")
        {
            // Mayorista amplio, pero deja los frescos especialistas como
            // tercera vía preferente en la semilla inicial.
            return category == "drygoods" ||
                   category == "dairyandeggs" ||
                   category == "condiment" ||
                   category == "beverage" ||
                   category == "bakery" ||
                   category == "other" ||
                   string.IsNullOrWhiteSpace(category);
        }

        return false;
    }

    public static BistroBuilderCommercialPackageAuthoringRecord SelectPackageForSupplier(
        string supplierId,
        BistroBuilderIngredientAuthoringRecord ingredient)
    {
        if (ingredient == null || ingredient.commercialPackages == null)
        {
            return null;
        }

        List<BistroBuilderCommercialPackageAuthoringRecord> active =
            new List<BistroBuilderCommercialPackageAuthoringRecord>();

        for (int index = 0;
             index < ingredient.commercialPackages.Count;
             index++)
        {
            BistroBuilderCommercialPackageAuthoringRecord package =
                ingredient.commercialPackages[index];

            if (package != null && package.isActive &&
                package.netQuantityMicrounits > 0)
            {
                active.Add(package);
            }
        }

        if (active.Count == 0)
        {
            return null;
        }

        active.Sort((left, right) =>
            left.netQuantityMicrounits.CompareTo(right.netQuantityMicrounits));

        string normalizedSupplier =
            (supplierId ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedSupplier == "supplier_distribuciones_norte")
        {
            return active[active.Count - 1];
        }

        if (normalizedSupplier == "supplier_hosteleria_express")
        {
            return active[0];
        }

        if (active.Count == 1)
        {
            return active[0];
        }

        // Generalista y especialistas usan el formato contenido/medio.
        return active[0];
    }

    public static List<PackageSeed> CreatePackageSeeds(
        BistroBuilderIngredientAuthoringRecord ingredient)
    {
        List<PackageSeed> result = new List<PackageSeed>();
        if (ingredient == null)
        {
            return result;
        }

        string name = NormalizeText(
            ingredient.displayNameSnapshot + " " + ingredient.IngredientId);
        string category = NormalizeToken(ingredient.categorySnapshot);
        string unit = NormalizeToken(ingredient.canonicalUnitSnapshot);

        if (unit == "unit" || unit == "units" || unit == "piece" || unit == "pieces")
        {
            if (name.Contains("huevo"))
            {
                AddSeed(result, "box_12u", "Caja 12 uds.", "Caja", 12d, BistroBuilderCommercialPackageLogisticSize.Pequeno);
                AddSeed(result, "box_30u", "Caja 30 uds.", "Caja", 30d, BistroBuilderCommercialPackageLogisticSize.Medio);
            }
            else
            {
                AddSeed(result, "pack_6u", "Pack 6 uds.", "Paquete", 6d, BistroBuilderCommercialPackageLogisticSize.Pequeno);
                AddSeed(result, "box_24u", "Caja 24 uds.", "Caja", 24d, BistroBuilderCommercialPackageLogisticSize.Medio);
            }

            return result;
        }

        if (unit == "milliliter" || unit == "milliliters" || unit == "ml")
        {
            if (name.Contains("aceite"))
            {
                AddSeed(result, "bottle_1l", "Botella 1 L", "Botella", 1000d, BistroBuilderCommercialPackageLogisticSize.Pequeno);
                AddSeed(result, "jug_5l", "Garrafa 5 L", "Garrafa", 5000d, BistroBuilderCommercialPackageLogisticSize.Medio);
            }
            else if (name.Contains("agua mineral") || name.Contains("botella de agua"))
            {
                AddSeed(result, "case_6x15l", "Caja 6 × 1,5 L", "Caja", 9000d, BistroBuilderCommercialPackageLogisticSize.Medio);
                AddSeed(result, "case_12x15l", "Caja 12 × 1,5 L", "Caja", 18000d, BistroBuilderCommercialPackageLogisticSize.Grande);
            }
            else if (name.Contains("agua"))
            {
                AddSeed(result, "jug_5l", "Garrafa 5 L", "Garrafa", 5000d, BistroBuilderCommercialPackageLogisticSize.Medio);
                AddSeed(result, "jug_10l", "Garrafa 10 L", "Garrafa", 10000d, BistroBuilderCommercialPackageLogisticSize.Grande);
            }
            else
            {
                AddSeed(result, "bottle_1l", "Botella 1 L", "Botella", 1000d, BistroBuilderCommercialPackageLogisticSize.Pequeno);
                AddSeed(result, "jug_5l", "Garrafa 5 L", "Garrafa", 5000d, BistroBuilderCommercialPackageLogisticSize.Medio);
            }

            return result;
        }

        if (unit == "liter" || unit == "liters" || unit == "litre" || unit == "litres" || unit == "l")
        {
            AddSeed(result, "bottle_1l", "Botella 1 L", "Botella", 1d, BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "jug_5l", "Garrafa 5 L", "Garrafa", 5d, BistroBuilderCommercialPackageLogisticSize.Medio);
            return result;
        }

        // El catálogo actual trabaja mayoritariamente en gramos.
        if (name.Contains("ajo"))
        {
            AddSeed(result, "mesh_1kg", "Malla 1 kg", "Malla", QuantityForWeightUnit(unit, 1d), BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "box_3kg", "Caja 3 kg", "Caja", QuantityForWeightUnit(unit, 3d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }
        else if (name.Contains("cebolla"))
        {
            AddSeed(result, "mesh_5kg", "Malla 5 kg", "Malla", QuantityForWeightUnit(unit, 5d), BistroBuilderCommercialPackageLogisticSize.Medio);
            AddSeed(result, "sack_10kg", "Saco 10 kg", "Saco", QuantityForWeightUnit(unit, 10d), BistroBuilderCommercialPackageLogisticSize.Grande);
        }
        else if (name.Contains("limon"))
        {
            AddSeed(result, "mesh_2kg", "Malla 2 kg", "Malla", QuantityForWeightUnit(unit, 2d), BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "box_5kg", "Caja 5 kg", "Caja", QuantityForWeightUnit(unit, 5d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }
        else if (name.Contains("galleta"))
        {
            AddSeed(result, "box_1kg", "Caja 1 kg", "Caja", QuantityForWeightUnit(unit, 1d), BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "box_3kg", "Caja 3 kg", "Caja", QuantityForWeightUnit(unit, 3d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }
        else if (name.Contains("azucar") || name.Contains("fabes") || name.Contains("harina") || category == "drygoods")
        {
            AddSeed(result, "pack_1kg", "Paquete 1 kg", "Paquete", QuantityForWeightUnit(unit, 1d), BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "sack_5kg", "Saco 5 kg", "Saco", QuantityForWeightUnit(unit, 5d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }
        else if (name.Contains("mantequilla"))
        {
            AddSeed(result, "block_1kg", "Bloque 1 kg", "Bloque", QuantityForWeightUnit(unit, 1d), BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "case_5kg", "Caja 5 kg", "Caja", QuantityForWeightUnit(unit, 5d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }
        else if (category == "meat")
        {
            AddSeed(result, "tray_2kg", "Bandeja 2 kg", "Bandeja", QuantityForWeightUnit(unit, 2d), BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "box_5kg", "Caja 5 kg", "Caja", QuantityForWeightUnit(unit, 5d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }
        else if (category == "fishandseafood")
        {
            AddSeed(result, "box_3kg", "Caja 3 kg", "Caja", QuantityForWeightUnit(unit, 3d), BistroBuilderCommercialPackageLogisticSize.Medio);
            AddSeed(result, "box_6kg", "Caja 6 kg", "Caja", QuantityForWeightUnit(unit, 6d), BistroBuilderCommercialPackageLogisticSize.Grande);
        }
        else if (category == "produce")
        {
            AddSeed(result, "box_3kg", "Caja 3 kg", "Caja", QuantityForWeightUnit(unit, 3d), BistroBuilderCommercialPackageLogisticSize.Medio);
            AddSeed(result, "box_5kg", "Caja 5 kg", "Caja", QuantityForWeightUnit(unit, 5d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }
        else if (category == "preparedproduct")
        {
            AddSeed(result, "tray_1kg", "Bandeja 1 kg", "Bandeja", QuantityForWeightUnit(unit, 1d), BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "box_3kg", "Caja 3 kg", "Caja", QuantityForWeightUnit(unit, 3d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }
        else if (category == "condiment")
        {
            AddSeed(result, "jar_500g", "Bote 500 g", "Bote", QuantityForWeightUnit(unit, 0.5d), BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "case_2kg", "Caja 2 kg", "Caja", QuantityForWeightUnit(unit, 2d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }
        else
        {
            AddSeed(result, "pack_1kg", "Paquete 1 kg", "Paquete", QuantityForWeightUnit(unit, 1d), BistroBuilderCommercialPackageLogisticSize.Pequeno);
            AddSeed(result, "case_5kg", "Caja 5 kg", "Caja", QuantityForWeightUnit(unit, 5d), BistroBuilderCommercialPackageLogisticSize.Medio);
        }

        return result;
    }

    public static int CountActiveOffersForIngredient(
        BistroBuilderSupplierAuthoringDatabase database,
        string ingredientId)
    {
        if (database == null || string.IsNullOrWhiteSpace(ingredientId))
        {
            return 0;
        }

        int count = 0;
        for (int supplierIndex = 0; supplierIndex < database.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = database.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null)
            {
                continue;
            }

            bool supplierCounted = false;
            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                if (offer != null && offer.isActive &&
                    string.Equals(offer.ingredientId, ingredientId, StringComparison.Ordinal))
                {
                    supplierCounted = true;
                    break;
                }
            }

            if (supplierCounted)
            {
                count++;
            }
        }

        return count;
    }

    public static int CountReferencesToPackage(
        BistroBuilderSupplierAuthoringDatabase database,
        string packageFormatId)
    {
        if (database == null || string.IsNullOrWhiteSpace(packageFormatId))
        {
            return 0;
        }

        int count = 0;
        for (int supplierIndex = 0; supplierIndex < database.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = database.Suppliers[supplierIndex];
            if (supplier == null || supplier.baseOffers == null)
            {
                continue;
            }

            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                if (offer != null &&
                    string.Equals(offer.packageFormatId, packageFormatId, StringComparison.Ordinal))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int ResolveMinimumPackageCount(
        string supplierId,
        BistroBuilderIngredientAuthoringRecord ingredient,
        BistroBuilderCommercialPackageAuthoringRecord package)
    {
        string normalized = (supplierId ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized == "supplier_distribuciones_norte" &&
            package.logisticSize != BistroBuilderCommercialPackageLogisticSize.Grande)
        {
            return 2;
        }

        return 1;
    }

    private static bool ContainsPackageId(
        List<BistroBuilderCommercialPackageAuthoringRecord> packages,
        string packageId)
    {
        for (int index = 0; index < packages.Count; index++)
        {
            BistroBuilderCommercialPackageAuthoringRecord package = packages[index];
            if (package != null &&
                string.Equals(package.PackageFormatId, packageId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsOfferForPackage(
        List<BistroBuilderSupplierBaseOfferAuthoringRecord> offers,
        string packageFormatId)
    {
        for (int index = 0; index < offers.Count; index++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = offers[index];
            if (offer != null &&
                string.Equals(offer.packageFormatId, packageFormatId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildPackageId(string ingredientId, string code)
    {
        return BistroBuilderSupplierAuthoringRecord.NormalizeId(
            ingredientId + "_" + code,
            "package");
    }

    private static void AddSeed(
        List<PackageSeed> result,
        string code,
        string displayName,
        string packageType,
        double baseUnitQuantity,
        BistroBuilderCommercialPackageLogisticSize size)
    {
        long microunits = (long)Math.Round(
            baseUnitQuantity * OneBaseUnitMicrounits,
            MidpointRounding.AwayFromZero);

        result.Add(new PackageSeed
        {
            code = code,
            displayName = displayName,
            packageType = packageType,
            netQuantityMicrounits = Math.Max(1L, microunits),
            logisticSize = size
        });
    }

    private static double QuantityForWeightUnit(string normalizedUnit, double kilograms)
    {
        string unit = NormalizeToken(normalizedUnit);
        if (unit == "kilogram" || unit == "kilograms" || unit == "kg")
        {
            return kilograms;
        }

        // Gram es la unidad canónica actual de sólidos.
        return kilograms * 1000d;
    }

    private static decimal ResolveLargeUnitQuantity(string rawUnit, double baseUnits)
    {
        string unit = NormalizeToken(rawUnit);
        if (unit == "gram" || unit == "grams" || unit == "g")
        {
            return (decimal)baseUnits / 1000m;
        }

        if (unit == "milliliter" || unit == "milliliters" || unit == "ml")
        {
            return (decimal)baseUnits / 1000m;
        }

        return (decimal)baseUnits;
    }

    private static decimal ResolveReferencePricePerLargeUnitCents(
        BistroBuilderIngredientAuthoringRecord ingredient)
    {
        string name = NormalizeText(
            ingredient.displayNameSnapshot + " " + ingredient.IngredientId);
        string category = NormalizeToken(ingredient.categorySnapshot);
        string unit = NormalizeToken(ingredient.canonicalUnitSnapshot);

        if (name.Contains("aceite")) return 700m;       // €/L 7,00
        if (name.Contains("aceituna")) return 500m;     // €/kg 5,00
        if (name.Contains("agua mineral") || name.Contains("botella de agua")) return 60m;
        if (name.Contains("agua")) return 30m;
        if (name.Contains("ajo")) return 450m;
        if (name.Contains("azucar")) return 120m;
        if (name.Contains("cebolla")) return 140m;
        if (name.Contains("chorizo")) return 900m;
        if (name.Contains("fabes")) return 600m;
        if (name.Contains("galleta")) return 700m;
        if (name.Contains("huevo")) return 25m;         // céntimos/ud.
        if (name.Contains("limon")) return 280m;
        if (name.Contains("mantequilla")) return 900m;
        if (name.Contains("merluza")) return 1150m;

        if (unit == "unit" || unit == "units" || unit == "piece" || unit == "pieces")
        {
            return 45m;
        }

        if (category == "fishandseafood") return 1200m;
        if (category == "meat") return 950m;
        if (category == "produce") return 250m;
        if (category == "dairyandeggs") return 600m;
        if (category == "drygoods") return 240m;
        if (category == "preparedproduct") return 650m;
        if (category == "condiment") return 800m;
        if (category == "beverage") return 120m;

        return 400m;
    }

    private static decimal ResolveSupplierPriceMultiplier(string supplierId)
    {
        string id = (supplierId ?? string.Empty).Trim().ToLowerInvariant();
        switch (id)
        {
            case "supplier_distribuciones_norte":
                return 0.88m;
            case "supplier_hosteleria_express":
                return 1.28m;
            case "supplier_huerta_clara":
                return 0.94m;
            case "supplier_carnes_selectas":
                return 1.08m;
            case "supplier_costa_fresca":
                return 1.05m;
            default:
                return 1.00m;
        }
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace(" ", string.Empty).ToLowerInvariant();
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(decomposed.Length);

        for (int index = 0; index < decomposed.Length; index++)
        {
            char character = decomposed[index];
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
#endif
