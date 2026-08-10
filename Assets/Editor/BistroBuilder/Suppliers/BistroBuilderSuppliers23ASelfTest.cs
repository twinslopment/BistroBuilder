using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Self-Test adversarial de supplier.catalog v2.
///
/// No depende de Play Mode ni modifica assets del proyecto. Construye datos
/// temporales e intenta romper identidad, cobertura, FK, economía, formatos,
/// disponibilidad, migración, encapsulación y serialización.
/// </summary>
public static class BistroBuilderSuppliers23ASelfTest
{
    [MenuItem("Tools/Bistro Builder/Suppliers/Run 2.3A Canonical Suppliers Self-Test")]
    public static void Run()
    {
        int passed = 0;
        int failed = 0;
        List<string> lines = new List<string>();

        List<BistroBuilderSupplierDefinition> suppliers =
            BistroBuilderSupplierCatalogDefaults.CreateDefaultSuppliers();
        List<BistroBuilderSupplierIngredientDescriptor> ingredients = CreateFakeIngredients(22);
        List<BistroBuilderSupplierProductDefinition> products =
            BistroBuilderSupplierCatalogBuilder.BuildProducts(suppliers, ingredients);

        Test(suppliers.Count == 4, "La semilla crea cuatro proveedores base.", ref passed, ref failed, lines);
        Test(ingredients.Count == 22, "El escenario adversarial reproduce 22 ingredientes.", ref passed, ref failed, lines);
        Test(products.Count == 44, "La semilla inicial crea 44 SKU base.", ref passed, ref failed, lines);

        HashSet<string> supplierIds = new HashSet<string>(StringComparer.Ordinal);
        bool supplierIdentity = true;
        bool supplierEconomy = true;
        for (int i = 0; i < suppliers.Count; i++)
        {
            var s = suppliers[i];
            supplierIdentity &= s != null && BistroBuilderMenuIdUtility.IsValidStableId(s.SupplierId) &&
                supplierIds.Add(s.SupplierId) && !string.IsNullOrWhiteSpace(s.DisplayName);
            supplierEconomy &= s != null && s.MinimumOrderCents >= 0 &&
                s.MinimumOrderCents <= BistroBuilderSupplierDefinition.MaximumMinimumOrderCents &&
                s.SeedPriceFactorBasisPoints >= 1 &&
                s.SeedPriceFactorBasisPoints <= BistroBuilderSupplierDefinition.MaximumPriceFactorBasisPoints &&
                s.DefaultLeadTimeDays >= 0 &&
                s.DefaultLeadTimeDays <= BistroBuilderSupplierDefinition.MaximumLeadTimeDays &&
                BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode(s.CurrencyCode);
        }
        Test(supplierIdentity, "SupplierId/nombres base son válidos y únicos.", ref passed, ref failed, lines);
        Test(supplierEconomy, "Dinero, factores, plazos y moneda base respetan límites.", ref passed, ref failed, lines);

        HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> supplierCoverage =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        bool productsValid = true;
        bool initialAvailability = true;
        for (int i = 0; i < products.Count; i++)
        {
            var p = products[i];
            productsValid &= p != null && BistroBuilderMenuIdUtility.IsValidStableId(p.ProductId) &&
                productIds.Add(p.ProductId) && supplierIds.Contains(p.SupplierId) &&
                p.PackageCanonicalMilliUnits > 0L && p.PackPriceCents >= 1 &&
                p.MinimumPacks >= 1 && p.LeadTimeDays >= 0 &&
                BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode(p.CurrencyCode);
            initialAvailability &= p != null && p.IsCatalogAvailable;
            if (p == null) continue;
            if (!supplierCoverage.TryGetValue(p.IngredientId, out var providers))
            {
                providers = new HashSet<string>(StringComparer.Ordinal);
                supplierCoverage.Add(p.IngredientId, providers);
            }
            providers.Add(p.SupplierId);
        }
        Test(productsValid, "Los 44 SKU base tienen identidad/FK/economía válidas.", ref passed, ref failed, lines);
        Test(initialAvailability, "IsCatalogAvailable nace independiente de IsCatalogEnabled del proveedor.", ref passed, ref failed, lines);

        bool twoDistinct = supplierCoverage.Count == ingredients.Count;
        bool generalistCoversAll = true;
        for (int i = 0; i < ingredients.Count; i++)
        {
            string id = ingredients[i].IngredientId;
            twoDistinct &= supplierCoverage.TryGetValue(id, out var providers) && providers.Count == 2;
            generalistCoversAll &= products.Exists(p => p.IngredientId == id &&
                p.SupplierId == BistroBuilderSupplierCatalogDefaults.GeneralSupplierId);
        }
        Test(twoDistinct, "La semilla da exactamente dos proveedores distintos por ingrediente.", ref passed, ref failed, lines);
        Test(generalistCoversAll, "La semilla generalista cubre todos los ingredientes.", ref passed, ref failed, lines);

        bool seedPackageMatchesReference = true;
        for (int i = 0; i < products.Count; i++)
        {
            var ingredient = ingredients.Find(x => x.IngredientId == products[i].IngredientId);
            seedPackageMatchesReference &= ingredient != null &&
                products[i].BaseUnit == ingredient.BaseUnit &&
                products[i].PackageCanonicalMilliUnits == ingredient.ReferencePackCanonicalMilliUnits;
        }
        Test(seedPackageMatchesReference,
            "Los SKU sembrados parten del envase de referencia sin impedir formatos futuros.",
            ref passed, ref failed, lines);

        Test(BistroBuilderSupplierCatalogBuilder.ChooseSpecialistId(
                FindByCategory(ingredients, BistroBuilderIngredientCategory.Produce)) ==
                BistroBuilderSupplierCatalogDefaults.FreshSupplierId,
            "Produce se especializa por categoría, no por texto.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogBuilder.ChooseSpecialistId(
                FindByCategory(ingredients, BistroBuilderIngredientCategory.Meat)) ==
                BistroBuilderSupplierCatalogDefaults.PremiumSupplierId,
            "Carne se especializa por categoría, no por texto.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogBuilder.ChooseSpecialistId(
                FindByCategory(ingredients, BistroBuilderIngredientCategory.DryGoods)) ==
                BistroBuilderSupplierCatalogDefaults.PantrySupplierId,
            "Secos se especializan por categoría, no por texto.", ref passed, ref failed, lines);

        Test(BistroBuilderSupplierCatalogBuilder.CalculateAdjustedPackPriceCents(1000, 9400, 9700) == 912,
            "El precio usa decimal/puntos básicos y redondeo determinista.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogBuilder.CalculateAdjustedPackPriceCents(0, 10000, 10000) == 1,
            "Una referencia a 0 nunca crea un SKU comprable a 0 céntimos.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogBuilder.CalculateAdjustedPackPriceCents(
                int.MaxValue, int.MaxValue, int.MaxValue) ==
                BistroBuilderSupplierProductDefinition.MaximumPackPriceCents,
            "El cálculo de precio satura al máximo autoritativo sin overflow.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierProductDefinition.MaximumPackageCanonicalMilliUnits *
                (long)BistroBuilderSupplierProductDefinition.MaximumPacksPerOrderLine <=
                BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits,
            "El contrato 2.3A garantiza multiplicación segura cantidadPorPack × packs para 2.3B.",
            ref passed, ref failed, lines);

        Test((long)BistroBuilderSupplierProductDefinition.MaximumPackPriceCents *
                BistroBuilderSupplierProductDefinition.MaximumPacksPerOrderLine <= long.MaxValue,
            "El contrato 2.3A permite calcular precioPorPack × packs en long cents sin overflow.",
            ref passed, ref failed, lines);

        var expensiveSmall = NewProduct("product_small", "supplier_a", "ingredient_x", 1000L, 100, "EUR");
        var cheaperLarge = NewProduct("product_large", "supplier_b", "ingredient_x", 2000L, 150, "EUR");
        Test(BistroBuilderSupplierCatalogBuilder.CompareNormalizedUnitCost(cheaperLarge, expensiveSmall) < 0,
            "La comparación de ofertas usa coste normalizado, no precio bruto.", ref passed, ref failed, lines);
        var equalA = NewProduct("product_a", "supplier_a", "ingredient_x", 1000L, 100, "EUR");
        var equalB = NewProduct("product_b", "supplier_b", "ingredient_x", 2000L, 200, "EUR");
        Test(BistroBuilderSupplierCatalogBuilder.CompareNormalizedUnitCost(equalA, equalB) < 0,
            "Empates de coste se resuelven por ProductId estable.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> secondBuild =
            BistroBuilderSupplierCatalogBuilder.BuildProducts(suppliers, ingredients);
        Test(DeepEqualProducts(products, secondBuild),
            "Dos semillas idénticas son profundamente deterministas.", ref passed, ref failed, lines);
        List<BistroBuilderSupplierDefinition> reverseSuppliers = CloneSuppliers(suppliers);
        reverseSuppliers.Reverse();
        List<BistroBuilderSupplierIngredientDescriptor> reverseIngredients =
            new List<BistroBuilderSupplierIngredientDescriptor>(ingredients);
        reverseIngredients.Reverse();
        Test(DeepEqualProducts(products,
                BistroBuilderSupplierCatalogBuilder.BuildProducts(reverseSuppliers, reverseIngredients)),
            "Reordenar entradas no altera IDs, contenido ni orden de la semilla.", ref passed, ref failed, lines);

        BistroBuilderSupplierCatalogValidationResult valid = Validate(suppliers, products, ingredients);
        Test(valid.IsValid && valid.WarningCount == 0,
            "El catálogo base supera validación estructural y operativa.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogValidator.Validate(
                suppliers, products, ingredients, 0, true).IsValid,
            "Cobertura recomendada 0 desactiva la política sin afectar integridad.", ref passed, ref failed, lines);
        Test(!BistroBuilderSupplierCatalogValidator.Validate(
                suppliers, products, ingredients, -1, true).IsValid,
            "Cobertura recomendada negativa se rechaza como contrato inválido.", ref passed, ref failed, lines);
        Test(!BistroBuilderSupplierCatalogValidator.Validate(
                null, products, ingredients, 2, true).IsValid,
            "Colección de proveedores nula se rechaza.", ref passed, ref failed, lines);
        Test(!BistroBuilderSupplierCatalogValidator.Validate(
                suppliers, null, ingredients, 2, true).IsValid,
            "Colección de productos nula se rechaza.", ref passed, ref failed, lines);
        Test(!BistroBuilderSupplierCatalogValidator.Validate(
                suppliers, products, null, 2, true).IsValid,
            "Colección de ingredientes nula se rechaza.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierDefinition> oneInactive = CloneSuppliersWithActiveOverride(
            suppliers, BistroBuilderSupplierCatalogDefaults.FreshSupplierId, false);
        BistroBuilderSupplierCatalogValidationResult inactiveValidation =
            Validate(oneInactive, CloneProducts(products), ingredients);
        Test(inactiveValidation.IsValid,
            "Desactivar un proveedor NO corrompe supplier.catalog.", ref passed, ref failed, lines);
        Test(products.TrueForAll(p => p.IsCatalogAvailable),
            "Desactivar proveedor no muta IsAvailable de sus SKU.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierDefinition> allInactive = CloneAllInactive(suppliers);
        BistroBuilderSupplierCatalogValidationResult allInactiveValidation =
            Validate(allInactive, CloneProducts(products), ingredients);
        Test(allInactiveValidation.IsValid && allInactiveValidation.WarningCount >= ingredients.Count,
            "Cero proveedores activos genera huecos operativos, no corrupción estructural.",
            ref passed, ref failed, lines);

        // Un producto temporalmente no disponible sigue formando parte de la topología estructural.
        var source = products[0];
        List<BistroBuilderSupplierProductDefinition> oneUnavailable = CloneProducts(products);
        oneUnavailable[0] = CopyProduct(source, source.ProductId, available: false);
        Test(Validate(suppliers, oneUnavailable, ingredients).IsValid,
            "Un SKU no disponible sigue siendo estructura válida.", ref passed, ref failed, lines);

        // Formatos adicionales del mismo proveedor están permitidos.
        List<BistroBuilderSupplierProductDefinition> extraFormat = CloneProducts(products);
        extraFormat.Add(new BistroBuilderSupplierProductDefinition(
            "product_extra_format", source.SupplierId, source.IngredientId,
            "Formato adicional", "Envase alternativo", source.BaseUnit,
            source.PackageCanonicalMilliUnits * 2L,
            Math.Min(BistroBuilderSupplierProductDefinition.MaximumPackPriceCents, source.PackPriceCents * 2 + 1),
            1, source.LeadTimeDays, true, source.CurrencyCode));
        Test(Validate(suppliers, extraFormat, ingredients).IsValid,
            "Varios formatos del mismo ingrediente/proveedor están permitidos.", ref passed, ref failed, lines);

        // Pero varios SKU del mismo proveedor NO sustituyen la cobertura de proveedores distintos.
        string targetIngredient = source.IngredientId;
        string retainedSupplier = source.SupplierId;
        List<BistroBuilderSupplierProductDefinition> oneProviderOnly = CloneProducts(products);
        oneProviderOnly.RemoveAll(p => p.IngredientId == targetIngredient && p.SupplierId != retainedSupplier);
        oneProviderOnly.Add(new BistroBuilderSupplierProductDefinition(
            "product_same_provider_second_format", retainedSupplier, targetIngredient,
            "Segundo formato", "Caja alternativa", source.BaseUnit,
            source.PackageCanonicalMilliUnits * 3L,
            Math.Min(BistroBuilderSupplierProductDefinition.MaximumPackPriceCents, source.PackPriceCents * 3 + 7),
            1, source.LeadTimeDays, true, source.CurrencyCode));
        var oneProviderValidation = Validate(suppliers, oneProviderOnly, ingredients);
        Test(oneProviderValidation.IsValid && oneProviderValidation.WarningCount > 0,
            "Dos formatos del mismo proveedor no cuentan como dos proveedores: se avisa sin corromper el dominio.",
            ref passed, ref failed, lines);

        // Duplicado comercial exacto con otro ProductId: inválido incluso si cambia disponibilidad.
        List<BistroBuilderSupplierProductDefinition> exactDuplicate = CloneProducts(products);
        exactDuplicate.Add(CopyProduct(source, "product_commercial_duplicate", available: !source.IsCatalogAvailable));
        Test(!Validate(suppliers, exactDuplicate, ingredients).IsValid,
            "Dos ProductId comercialmente idénticos se rechazan aunque difiera disponibilidad.",
            ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> missingStructuralOffer = CloneProducts(products);
        string specialistForTarget = products.Find(p => p.IngredientId == targetIngredient &&
            p.SupplierId != retainedSupplier).SupplierId;
        missingStructuralOffer.RemoveAll(p => p.IngredientId == targetIngredient &&
            p.SupplierId == specialistForTarget);
        var degradedCoverageValidation = Validate(suppliers, missingStructuralOffer, ingredients);
        Test(degradedCoverageValidation.IsValid && degradedCoverageValidation.WarningCount > 0,
            "Un ingrediente exclusivo de un proveedor sigue siendo válido y genera warning de cobertura.",
            ref passed, ref failed, lines);

        List<BistroBuilderSupplierDefinition> duplicateSupplierId = CloneSuppliers(suppliers);
        duplicateSupplierId.Add(suppliers[0].Clone());
        Test(!Validate(duplicateSupplierId, products, ingredients).IsValid,
            "SupplierId duplicado se rechaza.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierDefinition> duplicateSupplierName = CloneSuppliers(suppliers);
        var d0 = duplicateSupplierName[0];
        var d1 = duplicateSupplierName[1];
        duplicateSupplierName[1] = new BistroBuilderSupplierDefinition(
            d1.SupplierId, d0.DisplayName, d1.Description, d1.IsCatalogEnabled,
            d1.MinimumOrderCents, d1.DefaultLeadTimeDays, d1.CurrencyCode, d1.SeedPriceFactorBasisPoints);
        var duplicateNameValidation = Validate(duplicateSupplierName, products, ingredients);
        Test(duplicateNameValidation.IsValid && duplicateNameValidation.WarningCount > 0,
            "Nombres visibles duplicados avisan sin romper identidad estable.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierDefinition> mixedCurrencies = CloneSuppliers(suppliers);
        var mc = mixedCurrencies[0];
        mixedCurrencies[0] = new BistroBuilderSupplierDefinition(
            mc.SupplierId, mc.DisplayName, mc.Description, mc.IsCatalogEnabled,
            mc.MinimumOrderCents, mc.DefaultLeadTimeDays, "USD", mc.SeedPriceFactorBasisPoints);
        Test(!Validate(mixedCurrencies, products, ingredients).IsValid,
            "Mezclar monedas se rechaza para impedir comparaciones sin sistema FX.",
            ref passed, ref failed, lines);

        List<BistroBuilderSupplierDefinition> badSupplierRange = CloneSuppliers(suppliers);
        var bs = badSupplierRange[0];
        badSupplierRange[0] = new BistroBuilderSupplierDefinition(
            bs.SupplierId, bs.DisplayName, bs.Description, bs.IsCatalogEnabled,
            BistroBuilderSupplierDefinition.MaximumMinimumOrderCents + 1,
            BistroBuilderSupplierDefinition.MaximumLeadTimeDays + 1,
            bs.CurrencyCode,
            BistroBuilderSupplierDefinition.MaximumPriceFactorBasisPoints + 1);
        Test(!Validate(badSupplierRange, products, ingredients).IsValid,
            "Límites máximos de economía/plazo de proveedor se aplican.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> badSupplierFk = ReplaceFirst(products,
            NewLike(source, "product_bad_supplier", "supplier_missing", source.IngredientId,
                source.BaseUnit, source.PackageCanonicalMilliUnits, source.PackPriceCents,
                source.MinimumPacks, source.LeadTimeDays, true, source.CurrencyCode));
        Test(!Validate(suppliers, badSupplierFk, ingredients).IsValid,
            "SupplierId foráneo de SKU se rechaza.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> badIngredientFk = ReplaceFirst(products,
            NewLike(source, "product_bad_ingredient", source.SupplierId, "ingredient_missing",
                source.BaseUnit, source.PackageCanonicalMilliUnits, source.PackPriceCents,
                source.MinimumPacks, source.LeadTimeDays, true, source.CurrencyCode));
        Test(!Validate(suppliers, badIngredientFk, ingredients).IsValid,
            "IngredientId foráneo de SKU se rechaza.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> badProductCurrency = ReplaceFirst(products,
            NewLike(source, "product_bad_currency", source.SupplierId, source.IngredientId,
                source.BaseUnit, source.PackageCanonicalMilliUnits, source.PackPriceCents,
                source.MinimumPacks, source.LeadTimeDays, true, "USD"));
        Test(!Validate(suppliers, badProductCurrency, ingredients).IsValid,
            "Moneda de SKU distinta al proveedor se rechaza.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> zeroQuantity = ReplaceFirst(products,
            NewLike(source, "product_zero_quantity", source.SupplierId, source.IngredientId,
                source.BaseUnit, 0L, source.PackPriceCents, source.MinimumPacks,
                source.LeadTimeDays, true, source.CurrencyCode));
        Test(!Validate(suppliers, zeroQuantity, ingredients).IsValid,
            "Cantidad de envase cero se rechaza.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> zeroPrice = ReplaceFirst(products,
            NewLike(source, "product_zero_price", source.SupplierId, source.IngredientId,
                source.BaseUnit, source.PackageCanonicalMilliUnits, 0,
                source.MinimumPacks, source.LeadTimeDays, true, source.CurrencyCode));
        Test(!Validate(suppliers, zeroPrice, ingredients).IsValid,
            "Precio de envase cero se rechaza.", ref passed, ref failed, lines);

        // Clone no debe sanear corrupción serializada antes de validarla.
        var corruptCloneSource = products[0].Clone();
        var packPriceFieldForClone = typeof(BistroBuilderSupplierProductDefinition).GetField(
            "packPriceCents", BindingFlags.Instance | BindingFlags.NonPublic);
        packPriceFieldForClone.SetValue(corruptCloneSource, -7);
        var corruptClone = corruptCloneSource.Clone();
        Test(corruptClone.PackPriceCents == -7 &&
             !Validate(suppliers, ReplaceFirst(products, corruptClone), ingredients).IsValid,
            "Clone conserva datos corruptos para que el validador los rechace; no repara silenciosamente.",
            ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> badUnit = ReplaceFirst(products,
            NewLike(source, "product_bad_unit", source.SupplierId, source.IngredientId,
                DifferentUnit(source.BaseUnit), source.PackageCanonicalMilliUnits,
                source.PackPriceCents, source.MinimumPacks, source.LeadTimeDays,
                true, source.CurrencyCode));
        Test(!Validate(suppliers, badUnit, ingredients).IsValid,
            "Unidad base distinta al ingrediente se rechaza.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> overProductLimits = ReplaceFirst(products,
            NewLike(source, "product_over_limits", source.SupplierId, source.IngredientId,
                source.BaseUnit, BistroBuilderSupplierProductDefinition.MaximumPackageCanonicalMilliUnits + 1L,
                BistroBuilderSupplierProductDefinition.MaximumPackPriceCents + 1,
                BistroBuilderSupplierProductDefinition.MaximumMinimumPacks + 1,
                BistroBuilderSupplierProductDefinition.MaximumLeadTimeDays + 1,
                true, source.CurrencyCode));
        Test(!Validate(suppliers, overProductLimits, ingredients).IsValid,
            "Máximos de cantidad/precio/packs/plazo de SKU se aplican.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierProductDefinition> duplicateProductId = CloneProducts(products);
        duplicateProductId.Add(source.Clone());
        Test(!Validate(suppliers, duplicateProductId, ingredients).IsValid,
            "ProductId duplicado se rechaza.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierIngredientDescriptor> duplicateIngredient =
            CloneIngredients(ingredients);
        duplicateIngredient.Add(ingredients[0].Clone());
        Test(!Validate(suppliers, products, duplicateIngredient).IsValid,
            "IngredientId duplicado en la vista de dominio se rechaza.", ref passed, ref failed, lines);

        List<BistroBuilderSupplierIngredientDescriptor> badIngredientClassification =
            CloneIngredients(ingredients);
        var bi = badIngredientClassification[0];
        badIngredientClassification[0] = new BistroBuilderSupplierIngredientDescriptor(
            bi.IngredientId, bi.DisplayName, bi.BaseUnit,
            (BistroBuilderIngredientCategory)999,
            bi.StorageType, bi.Perishable,
            bi.ReferencePackCanonicalMilliUnits, bi.ReferencePackPriceCents);
        Test(!Validate(suppliers, products, badIngredientClassification).IsValid,
            "Clasificación canónica desconocida se rechaza.", ref passed, ref failed, lines);

        Test(!BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode("eur") &&
             !BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode("EU") &&
             !BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode("€€€") &&
              BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode("EUR"),
            "Moneda exige exactamente tres letras ASCII mayúsculas.", ref passed, ref failed, lines);

        Test(BistroBuilderSupplierCatalogBuilder.BuildPackageLabel(
                1000000L, BistroBuilderMeasurementUnit.Gram) == "Envase 1 kg",
            "Formato 1 kg se representa determinísticamente.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogBuilder.BuildPackageLabel(
                750000L, BistroBuilderMeasurementUnit.Milliliter) == "Envase 750 ml",
            "Formato 750 ml conserva precisión.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogBuilder.BuildPackageLabel(
                24000L, BistroBuilderMeasurementUnit.Unit) == "Caja 24 ud",
            "Formato discreto usa unidades.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogBuilder.BuildPackageLabel(
                0L, BistroBuilderMeasurementUnit.Gram) == "Formato inválido",
            "Formato no positivo no se presenta como envase válido.", ref passed, ref failed, lines);

        string longSupplier = "supplier_" + new string('a', 80);
        string longIngredient = "ingredient_" + new string('b', 80);
        string fallbackId = BistroBuilderSupplierCatalogBuilder.BuildProductId(longSupplier, longIngredient);
        Test(fallbackId.StartsWith("product_") && BistroBuilderMenuIdUtility.IsValidStableId(fallbackId) &&
             fallbackId.Length <= 96,
            "ProductId demasiado largo usa hash estable dentro del contrato de IDs.", ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogBuilder.BuildProductId(
                BistroBuilderSupplierCatalogDefaults.GeneralSupplierId, "ingredient_test_00") ==
                BistroBuilderSupplierCatalogBuilder.BuildProductId(
                    BistroBuilderSupplierCatalogDefaults.GeneralSupplierId, "ingredient_test_00"),
            "BuildProductId es determinista.", ref passed, ref failed, lines);
        string variantA = BistroBuilderSupplierCatalogBuilder.BuildVariantProductId(
            BistroBuilderSupplierCatalogDefaults.GeneralSupplierId, "ingredient_test_00", "caja_5kg");
        string variantB = BistroBuilderSupplierCatalogBuilder.BuildVariantProductId(
            BistroBuilderSupplierCatalogDefaults.GeneralSupplierId, "ingredient_test_00", "caja_10kg");
        Test(variantA != variantB && BistroBuilderMenuIdUtility.IsValidStableId(variantA) &&
             BistroBuilderMenuIdUtility.IsValidStableId(variantB),
            "Formatos adicionales disponen de ProductId de variante estables y distintos.",
            ref passed, ref failed, lines);
        Test(BistroBuilderSupplierCatalogBuilder.StableHash64("abc") ==
             BistroBuilderSupplierCatalogBuilder.StableHash64("abc"),
            "Hash64 de fallback es determinista.", ref passed, ref failed, lines);

        BistroBuilderSupplierCatalogSnapshot snapshot =
            new BistroBuilderSupplierCatalogSnapshot(2, 7, suppliers, products, ingredients);
        Test(snapshot.SchemaVersion == 2 && snapshot.ContentRevision == 7 &&
             snapshot.Suppliers.Count == 4 && snapshot.Products.Count == 44 &&
             snapshot.Ingredients.Count == 22,
            "Snapshot conserva schema, revisión y cardinalidades.", ref passed, ref failed, lines);
        Test(!(snapshot.Suppliers is List<BistroBuilderSupplierDefinition>) &&
             !(snapshot.Products is List<BistroBuilderSupplierProductDefinition>) &&
             !(snapshot.Ingredients is List<BistroBuilderSupplierIngredientDescriptor>),
            "Snapshot no expone colecciones mutables mediante cast.", ref passed, ref failed, lines);
        Test(!ReferenceEquals(snapshot.Suppliers[0], suppliers[0]) &&
             !ReferenceEquals(snapshot.Products[0], products[0]) &&
             !ReferenceEquals(snapshot.Ingredients[0], ingredients[0]),
            "Snapshot realiza copia profunda de las tres colecciones.", ref passed, ref failed, lines);

        string json = JsonUtility.ToJson(snapshot);
        BistroBuilderSupplierCatalogSnapshot roundTrip =
            JsonUtility.FromJson<BistroBuilderSupplierCatalogSnapshot>(json);
        Test(DeepEqualSnapshot(snapshot, roundTrip),
            "Round-trip JSON profundo preserva todos los campos autoritativos.", ref passed, ref failed, lines);

        BistroBuilderSupplierCatalogSnapshot partialSnapshot =
            JsonUtility.FromJson<BistroBuilderSupplierCatalogSnapshot>("{\"schemaVersion\":2}");
        bool partialSnapshotSafe = false;
        try
        {
            partialSnapshotSafe = partialSnapshot != null &&
                partialSnapshot.Suppliers.Count == 0 &&
                partialSnapshot.Products.Count == 0 &&
                partialSnapshot.Ingredients.Count == 0;
        }
        catch
        {
            partialSnapshotSafe = false;
        }
        Test(partialSnapshotSafe,
            "Snapshot JSON parcial/antiguo materializa colecciones vacías sin NullReference.",
            ref passed, ref failed, lines);

        BistroBuilderSupplierCatalogSettings tempSettings =
            ScriptableObject.CreateInstance<BistroBuilderSupplierCatalogSettings>();
        bool resetOk = tempSettings.ResetToCanonicalDefaults(ingredients, out string resetError);
        Test(resetOk && tempSettings.SchemaVersion == 2 &&
             tempSettings.Suppliers.Count == 4 && tempSettings.Products.Count == 44,
            "Settings temporal puede sembrar supplier.catalog v2 completo." +
            (resetOk ? string.Empty : " " + resetError), ref passed, ref failed, lines);
        Test(!(tempSettings.Suppliers is List<BistroBuilderSupplierDefinition>) &&
             !(tempSettings.Products is List<BistroBuilderSupplierProductDefinition>),
            "Settings no filtra sus List serializadas a consumidores.", ref passed, ref failed, lines);
        Test(IsSupplierOrderCanonical(tempSettings.Suppliers),
            "ResetToCanonicalDefaults entrega proveedores en orden canónico estable.",
            ref passed, ref failed, lines);
        Test(IsProductOrderCanonical(tempSettings.Products),
            "ResetToCanonicalDefaults entrega productos en orden canónico estable.",
            ref passed, ref failed, lines);

        bool immediateEnsureOk = tempSettings.TryEnsureCanonicalDefaults(
            ingredients, out bool immediateChanged, out string immediateEnsureError);
        Test(immediateEnsureOk && !immediateChanged,
            "Reset -> EnsureDefaults inmediato es estrictamente idempotente." +
            (immediateEnsureOk ? string.Empty : " " + immediateEnsureError),
            ref passed, ref failed, lines);

        // Personalización legítima debe sobrevivir a EnsureDefaults.
        var productsField = typeof(BistroBuilderSupplierCatalogSettings).GetField(
            "products", BindingFlags.Instance | BindingFlags.NonPublic);
        var internalProducts = productsField.GetValue(tempSettings) as List<BistroBuilderSupplierProductDefinition>;
        var customized = internalProducts[0];
        internalProducts[0] = new BistroBuilderSupplierProductDefinition(
            customized.ProductId, customized.SupplierId, customized.IngredientId,
            customized.DisplayName + " personalizado", "Caja personalizada",
            customized.BaseUnit, customized.PackageCanonicalMilliUnits * 2L,
            customized.PackPriceCents + 123, 2, customized.LeadTimeDays + 1,
            customized.IsCatalogAvailable, customized.CurrencyCode);
        bool ensureCustomOk = tempSettings.TryEnsureCanonicalDefaults(
            ingredients, out bool customChanged, out string customError);
        var persistedCustom = FindProduct(tempSettings.Products, customized.ProductId);
        Test(ensureCustomOk && persistedCustom != null &&
             persistedCustom.PackageLabel == "Caja personalizada" &&
             persistedCustom.MinimumPacks == 2 && persistedCustom.PackPriceCents == customized.PackPriceCents + 123,
            "EnsureDefaults no sobrescribe precio/formato/mínimos personalizados de SKU existente." +
            (ensureCustomOk ? string.Empty : " " + customError), ref passed, ref failed, lines);
        Test(ensureCustomOk && !customChanged,
            "EnsureDefaults sobre catálogo v2 completo/personalizado es idempotente.", ref passed, ref failed, lines);

        // Migración realista v1 -> v2: economía float + catálogo de productos inexistente.
        var schemaField = typeof(BistroBuilderSupplierCatalogSettings).GetField(
            "schemaVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        var minCentsField = typeof(BistroBuilderSupplierDefinition).GetField(
            "minimumOrderCents", BindingFlags.Instance | BindingFlags.NonPublic);
        var factorBpField = typeof(BistroBuilderSupplierDefinition).GetField(
            "priceFactorBasisPoints", BindingFlags.Instance | BindingFlags.NonPublic);
        var legacyMinField = typeof(BistroBuilderSupplierDefinition).GetField(
            "legacyMinimumOrderValue", BindingFlags.Instance | BindingFlags.NonPublic);
        var legacyFactorField = typeof(BistroBuilderSupplierDefinition).GetField(
            "legacyPriceFactor", BindingFlags.Instance | BindingFlags.NonPublic);

        var migratingSupplier = tempSettings.Suppliers[0];
        schemaField.SetValue(tempSettings, 1);
        minCentsField.SetValue(migratingSupplier, 0);
        factorBpField.SetValue(migratingSupplier, 1);
        legacyMinField.SetValue(migratingSupplier, 77.25f);
        legacyFactorField.SetValue(migratingSupplier, 1.23f);
        internalProducts.Clear();

        bool migratedOk = tempSettings.TryEnsureCanonicalDefaults(
            ingredients, out bool migrationChanged, out string migrationError);
        Test(migratedOk && migrationChanged && tempSettings.SchemaVersion == 2 &&
             migratingSupplier.MinimumOrderCents == 7725 &&
             migratingSupplier.SeedPriceFactorBasisPoints == 12300,
            "Migración v1->v2 convierte economía float a céntimos/puntos básicos." +
            (migratedOk ? string.Empty : " " + migrationError), ref passed, ref failed, lines);
        Test(migratedOk && tempSettings.Products.Count == 44 &&
             Validate(tempSettings.Suppliers, tempSettings.Products, ingredients).IsValid,
            "Migración v1->v2 siembra SKU persistidos y deja catálogo estructuralmente válido.",
            ref passed, ref failed, lines);

        var legacyMinAttributes = legacyMinField.GetCustomAttributes(typeof(FormerlySerializedAsAttribute), false);
        var legacyFactorAttributes = legacyFactorField.GetCustomAttributes(typeof(FormerlySerializedAsAttribute), false);
        Test(legacyMinAttributes.Length == 1 && legacyFactorAttributes.Length == 1,
            "Campos legacy conservan FormerlySerializedAs para leer el asset 2.3A v1.",
            ref passed, ref failed, lines);

        schemaField.SetValue(tempSettings, BistroBuilderSupplierCatalogSettings.CurrentSchemaVersion + 1);
        Test(!tempSettings.TryEnsureCanonicalDefaults(ingredients, out _, out _),
            "Un schema futuro desconocido se rechaza; no se degrada silenciosamente.",
            ref passed, ref failed, lines);

        UnityEngine.Object.DestroyImmediate(tempSettings);

        string report =
            "BISTRO BUILDER — SELF-TEST 2.3A3\n\n" +
            "Superadas: " + passed + "\n" +
            "Fallidas: " + failed + "\n\n" +
            string.Join("\n", lines);
        if (failed == 0) Debug.Log(report); else Debug.LogError(report);
    }

    private static BistroBuilderSupplierCatalogValidationResult Validate(
        IReadOnlyList<BistroBuilderSupplierDefinition> suppliers,
        IReadOnlyList<BistroBuilderSupplierProductDefinition> products,
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> ingredients)
    {
        return BistroBuilderSupplierCatalogValidator.Validate(
            suppliers, products, ingredients,
            BistroBuilderSupplierCatalogBuilder.RecommendedDistinctSuppliersPerIngredient,
            reportOperationalGapsAsWarnings: true);
    }

    private static BistroBuilderSupplierProductDefinition NewProduct(
        string id, string supplier, string ingredient, long quantity, int cents, string currency)
    {
        return new BistroBuilderSupplierProductDefinition(
            id, supplier, ingredient, id, "Envase",
            BistroBuilderMeasurementUnit.Gram, quantity, cents, 1, 1, true, currency);
    }

    private static BistroBuilderSupplierProductDefinition NewLike(
        BistroBuilderSupplierProductDefinition source,
        string id, string supplierId, string ingredientId,
        BistroBuilderMeasurementUnit unit, long quantity, int cents,
        int minimumPacks, int leadTimeDays, bool available, string currency)
    {
        return new BistroBuilderSupplierProductDefinition(
            id, supplierId, ingredientId, "Producto prueba", "Envase prueba",
            unit, quantity, cents, minimumPacks, leadTimeDays, available, currency);
    }

    private static BistroBuilderSupplierProductDefinition CopyProduct(
        BistroBuilderSupplierProductDefinition source, string productId, bool available)
    {
        return new BistroBuilderSupplierProductDefinition(
            productId, source.SupplierId, source.IngredientId,
            source.DisplayName, source.PackageLabel, source.BaseUnit,
            source.PackageCanonicalMilliUnits, source.PackPriceCents,
            source.MinimumPacks, source.LeadTimeDays, available, source.CurrencyCode);
    }

    private static List<BistroBuilderSupplierProductDefinition> ReplaceFirst(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> source,
        BistroBuilderSupplierProductDefinition replacement)
    {
        List<BistroBuilderSupplierProductDefinition> result = CloneProducts(source);
        result[0] = replacement;
        return result;
    }

    private static List<BistroBuilderSupplierDefinition> CloneSuppliers(
        IReadOnlyList<BistroBuilderSupplierDefinition> source)
    {
        var result = new List<BistroBuilderSupplierDefinition>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++) if (source[i] != null) result.Add(source[i].Clone());
        return result;
    }

    private static List<BistroBuilderSupplierProductDefinition> CloneProducts(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> source)
    {
        var result = new List<BistroBuilderSupplierProductDefinition>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++) if (source[i] != null) result.Add(source[i].Clone());
        return result;
    }

    private static List<BistroBuilderSupplierIngredientDescriptor> CloneIngredients(
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> source)
    {
        var result = new List<BistroBuilderSupplierIngredientDescriptor>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++) if (source[i] != null) result.Add(source[i].Clone());
        return result;
    }

    private static List<BistroBuilderSupplierDefinition> CloneSuppliersWithActiveOverride(
        IReadOnlyList<BistroBuilderSupplierDefinition> source, string targetId, bool active)
    {
        var result = new List<BistroBuilderSupplierDefinition>();
        for (int i = 0; i < source.Count; i++)
        {
            var s = source[i];
            result.Add(new BistroBuilderSupplierDefinition(
                s.SupplierId, s.DisplayName, s.Description,
                s.SupplierId == targetId ? active : s.IsCatalogEnabled,
                s.MinimumOrderCents, s.DefaultLeadTimeDays,
                s.CurrencyCode, s.SeedPriceFactorBasisPoints));
        }
        return result;
    }

    private static List<BistroBuilderSupplierDefinition> CloneAllInactive(
        IReadOnlyList<BistroBuilderSupplierDefinition> source)
    {
        var result = new List<BistroBuilderSupplierDefinition>();
        for (int i = 0; i < source.Count; i++)
        {
            var s = source[i];
            result.Add(new BistroBuilderSupplierDefinition(
                s.SupplierId, s.DisplayName, s.Description, false,
                s.MinimumOrderCents, s.DefaultLeadTimeDays,
                s.CurrencyCode, s.SeedPriceFactorBasisPoints));
        }
        return result;
    }

    private static BistroBuilderSupplierIngredientDescriptor FindByCategory(
        List<BistroBuilderSupplierIngredientDescriptor> ingredients,
        BistroBuilderIngredientCategory category)
    {
        return ingredients.Find(i => i.Category == category);
    }

    private static BistroBuilderMeasurementUnit DifferentUnit(BistroBuilderMeasurementUnit current)
    {
        return current == BistroBuilderMeasurementUnit.Gram
            ? BistroBuilderMeasurementUnit.Milliliter
            : BistroBuilderMeasurementUnit.Gram;
    }

    private static BistroBuilderSupplierProductDefinition FindProduct(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> source, string productId)
    {
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null && source[i].ProductId == productId) return source[i];
        return null;
    }

    private static bool IsSupplierOrderCanonical(
        IReadOnlyList<BistroBuilderSupplierDefinition> source)
    {
        if (source == null) return false;
        for (int i = 1; i < source.Count; i++)
        {
            string previous = source[i - 1] != null ? source[i - 1].SupplierId : string.Empty;
            string current = source[i] != null ? source[i].SupplierId : string.Empty;
            if (string.Compare(previous, current, StringComparison.Ordinal) > 0) return false;
        }
        return true;
    }

    private static bool IsProductOrderCanonical(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> source)
    {
        if (source == null) return false;
        for (int i = 1; i < source.Count; i++)
        {
            string previous = source[i - 1] != null ? source[i - 1].ProductId : string.Empty;
            string current = source[i] != null ? source[i].ProductId : string.Empty;
            if (string.Compare(previous, current, StringComparison.Ordinal) > 0) return false;
        }
        return true;
    }

    private static List<BistroBuilderSupplierIngredientDescriptor> CreateFakeIngredients(int count)
    {
        var result = new List<BistroBuilderSupplierIngredientDescriptor>();
        BistroBuilderIngredientCategory[] categories =
        {
            BistroBuilderIngredientCategory.Produce,
            BistroBuilderIngredientCategory.Meat,
            BistroBuilderIngredientCategory.FishAndSeafood,
            BistroBuilderIngredientCategory.DairyAndEggs,
            BistroBuilderIngredientCategory.DryGoods,
            BistroBuilderIngredientCategory.Condiment,
            BistroBuilderIngredientCategory.Beverage,
            BistroBuilderIngredientCategory.PreparedProduct,
            BistroBuilderIngredientCategory.Other
        };

        for (int i = 0; i < count; i++)
        {
            BistroBuilderMeasurementUnit unit = i % 4 == 0
                ? BistroBuilderMeasurementUnit.Gram
                : i % 4 == 1
                    ? BistroBuilderMeasurementUnit.Milliliter
                    : i % 4 == 2
                        ? BistroBuilderMeasurementUnit.Unit
                        : BistroBuilderMeasurementUnit.Portion;
            long package = unit == BistroBuilderMeasurementUnit.Unit
                ? 12000L
                : unit == BistroBuilderMeasurementUnit.Portion
                    ? 10000L
                    : 1000000L;
            BistroBuilderIngredientCategory category = categories[i % categories.Length];
            bool perishable = category == BistroBuilderIngredientCategory.Produce ||
                              category == BistroBuilderIngredientCategory.Meat ||
                              category == BistroBuilderIngredientCategory.FishAndSeafood ||
                              category == BistroBuilderIngredientCategory.DairyAndEggs;
            result.Add(new BistroBuilderSupplierIngredientDescriptor(
                "ingredient_test_" + i.ToString("00"),
                "Ingrediente " + (i + 1),
                unit,
                category,
                perishable
                    ? BistroBuilderIngredientStorageType.Refrigerated
                    : BistroBuilderIngredientStorageType.DryStorage,
                perishable,
                package,
                100 + i * 17));
        }
        return result;
    }

    private static bool DeepEqualProducts(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> a,
        IReadOnlyList<BistroBuilderSupplierProductDefinition> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (!EqualProduct(a[i], b[i])) return false;
        return true;
    }

    private static bool DeepEqualSnapshot(
        BistroBuilderSupplierCatalogSnapshot a,
        BistroBuilderSupplierCatalogSnapshot b)
    {
        if (a == null || b == null || a.SchemaVersion != b.SchemaVersion ||
            a.ContentRevision != b.ContentRevision ||
            a.Suppliers.Count != b.Suppliers.Count ||
            a.Products.Count != b.Products.Count ||
            a.Ingredients.Count != b.Ingredients.Count) return false;
        for (int i = 0; i < a.Suppliers.Count; i++)
        {
            var x = a.Suppliers[i]; var y = b.Suppliers[i];
            if (x == null || y == null || x.SupplierId != y.SupplierId ||
                x.DisplayName != y.DisplayName || x.Description != y.Description ||
                x.IsCatalogEnabled != y.IsCatalogEnabled || x.MinimumOrderCents != y.MinimumOrderCents ||
                x.DefaultLeadTimeDays != y.DefaultLeadTimeDays ||
                x.CurrencyCode != y.CurrencyCode ||
                x.SeedPriceFactorBasisPoints != y.SeedPriceFactorBasisPoints) return false;
        }
        for (int i = 0; i < a.Products.Count; i++) if (!EqualProduct(a.Products[i], b.Products[i])) return false;
        for (int i = 0; i < a.Ingredients.Count; i++)
        {
            var x = a.Ingredients[i]; var y = b.Ingredients[i];
            if (x == null || y == null || x.IngredientId != y.IngredientId ||
                x.DisplayName != y.DisplayName || x.BaseUnit != y.BaseUnit ||
                x.Category != y.Category || x.StorageType != y.StorageType ||
                x.Perishable != y.Perishable ||
                x.ReferencePackCanonicalMilliUnits != y.ReferencePackCanonicalMilliUnits ||
                x.ReferencePackPriceCents != y.ReferencePackPriceCents) return false;
        }
        return true;
    }

    private static bool EqualProduct(
        BistroBuilderSupplierProductDefinition x,
        BistroBuilderSupplierProductDefinition y)
    {
        return x != null && y != null &&
            x.ProductId == y.ProductId && x.SupplierId == y.SupplierId &&
            x.IngredientId == y.IngredientId && x.DisplayName == y.DisplayName &&
            x.PackageLabel == y.PackageLabel && x.BaseUnit == y.BaseUnit &&
            x.PackageCanonicalMilliUnits == y.PackageCanonicalMilliUnits &&
            x.PackPriceCents == y.PackPriceCents && x.MinimumPacks == y.MinimumPacks &&
            x.LeadTimeDays == y.LeadTimeDays && x.IsCatalogAvailable == y.IsCatalogAvailable &&
            x.CurrencyCode == y.CurrencyCode;
    }

    private static void Test(
        bool condition, string message,
        ref int passed, ref int failed, List<string> lines)
    {
        if (condition)
        {
            passed++;
            lines.Add("OK: " + message);
        }
        else
        {
            failed++;
            lines.Add("FALLO: " + message);
        }
    }
}
