using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validador estructural exhaustivo de 2.3A1 en Edit Mode.
///
/// supplier.catalog v2 persiste proveedores Y SKU. El validador comprueba el
/// asset real contra IngredientCatalog, pero no modifica escenas, inventario,
/// partidas ni el propio catálogo.
/// </summary>
public static class BistroBuilderSuppliers23AValidator
{
    private const string SettingsAssetPath =
        "Assets/Resources/BistroBuilder/Suppliers/BistroBuilderSupplierCatalogSettings.asset";
    private const string IngredientCatalogPath =
        "Assets/Data/BistroBuilder/Ingredients/BistroBuilderIngredientCatalog.asset";

    [MenuItem("Tools/Bistro Builder/Suppliers/Validate 2.3A Canonical Suppliers")]
    public static void Validate()
    {
        int correct = 0;
        int warnings = 0;
        int errors = 0;
        List<string> lines = new List<string>();

        BistroBuilderSupplierCatalogSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierCatalogSettings>(SettingsAssetPath);
        Check(settings != null,
            "Existe el asset canónico supplier.catalog.",
            ref correct, ref errors, lines);
        if (settings == null)
        {
            Finish(correct, warnings, errors, lines);
            return;
        }

        Check(settings.SchemaVersion == BistroBuilderSupplierCatalogSettings.CurrentSchemaVersion,
            "supplier.catalog usa schema v2.", ref correct, ref errors, lines);
        Check(settings.Suppliers != null && settings.Suppliers.Count >= 4,
            "supplier.catalog contiene al menos los cuatro proveedores base.",
            ref correct, ref errors, lines);
        Check(settings.Products != null && settings.Products.Count > 0,
            "supplier.catalog v2 persiste SKU de proveedor explícitos.",
            ref correct, ref errors, lines);
        Check(!(settings.Suppliers is List<BistroBuilderSupplierDefinition>) &&
              !(settings.Products is List<BistroBuilderSupplierProductDefinition>),
            "El asset no expone sus listas serializadas mediante cast mutable.",
            ref correct, ref errors, lines);

        BistroBuilderIngredientCatalog ingredientCatalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientCatalog>(IngredientCatalogPath);
        Check(ingredientCatalog != null,
            "Existe IngredientCatalog en su ruta canónica estable.",
            ref correct, ref errors, lines);
        if (ingredientCatalog == null)
        {
            Finish(correct, warnings, errors, lines);
            return;
        }

        string[] ingredientCatalogGuids = AssetDatabase.FindAssets("t:BistroBuilderIngredientCatalog");
        if (ingredientCatalogGuids.Length == 1)
        {
            Pass("No hay ambigüedad de assets IngredientCatalog.", ref correct, lines);
        }
        else
        {
            warnings++;
            lines.Add("ADVERTENCIA: existen " + ingredientCatalogGuids.Length +
                " assets BistroBuilderIngredientCatalog; 2.3A usa la ruta canónica estable.");
        }

        Check(ingredientCatalog.TryRebuildIndex(out string indexError),
            "El índice de IngredientCatalog es válido." +
            (string.IsNullOrWhiteSpace(indexError) ? string.Empty : " " + indexError),
            ref correct, ref errors, lines);

        bool descriptorsOk =
            BistroBuilderCanonicalIngredientDiscovery.TryCreateDescriptorsFromCatalog(
                ingredientCatalog,
                out List<BistroBuilderSupplierIngredientDescriptor> ingredients,
                out string descriptorError);
        Check(descriptorsOk,
            "Los ingredientes se adaptan mediante API tipada, sin reflexión." +
            (descriptorsOk ? string.Empty : " " + descriptorError),
            ref correct, ref errors, lines);
        if (!descriptorsOk)
        {
            Finish(correct, warnings, errors, lines);
            return;
        }

        Check(ingredients.Count >= 22,
            "Se enlazan al menos los 22 ingredientes canónicos de la línea base y se permiten ampliaciones futuras.",
            ref correct, ref errors, lines);

        HashSet<string> ingredientIds = new HashSet<string>(StringComparer.Ordinal);
        bool ingredientDescriptorsValid = true;
        for (int i = 0; i < ingredients.Count; i++)
        {
            BistroBuilderSupplierIngredientDescriptor ingredient = ingredients[i];
            ingredientDescriptorsValid &= ingredient != null &&
                BistroBuilderMenuIdUtility.IsValidStableId(ingredient.IngredientId) &&
                ingredientIds.Add(ingredient.IngredientId) &&
                BistroBuilderMeasurementUtility.IsCanonicalBaseUnit(ingredient.BaseUnit) &&
                ingredient.ReferencePackCanonicalMilliUnits > 0L &&
                ingredient.ReferencePackCanonicalMilliUnits <=
                    BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits &&
                ingredient.ReferencePackPriceCents >= 0;
        }
        Check(ingredientDescriptorsValid,
            "Los descriptores conservan identidad, unidad y magnitud canónicas válidas.",
            ref correct, ref errors, lines);

        BistroBuilderSupplierCatalogValidationResult validation =
            BistroBuilderSupplierCatalogValidator.Validate(
                settings.Suppliers,
                settings.Products,
                ingredients,
                BistroBuilderSupplierCatalogBuilder.RecommendedDistinctSuppliersPerIngredient,
                reportOperationalGapsAsWarnings: true);
        Check(validation.IsValid,
            "supplier.catalog completo supera el validador compartido." +
            (validation.IsValid || validation.Errors.Count == 0
                ? string.Empty
                : " Primer error: " + validation.Errors[0]),
            ref correct, ref errors, lines);
        warnings += validation.WarningCount;
        for (int i = 0; i < validation.Warnings.Count; i++)
            lines.Add("ADVERTENCIA CATÁLOGO: " + validation.Warnings[i]);

        HashSet<string> supplierIds = new HashSet<string>(StringComparer.Ordinal);
        bool supplierIdentity = true;
        bool supplierCommercialData = true;
        for (int i = 0; i < settings.Suppliers.Count; i++)
        {
            BistroBuilderSupplierDefinition supplier = settings.Suppliers[i];
            supplierIdentity &= supplier != null &&
                BistroBuilderMenuIdUtility.IsValidStableId(supplier.SupplierId) &&
                supplierIds.Add(supplier.SupplierId) &&
                !string.IsNullOrWhiteSpace(supplier.DisplayName);
            if (supplier == null) continue;
            supplierCommercialData &= supplier.MinimumOrderCents >= 0 &&
                supplier.MinimumOrderCents <= BistroBuilderSupplierDefinition.MaximumMinimumOrderCents &&
                supplier.SeedPriceFactorBasisPoints >= 1 &&
                supplier.SeedPriceFactorBasisPoints <= BistroBuilderSupplierDefinition.MaximumPriceFactorBasisPoints &&
                supplier.DefaultLeadTimeDays >= 0 &&
                supplier.DefaultLeadTimeDays <= BistroBuilderSupplierDefinition.MaximumLeadTimeDays &&
                BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode(supplier.CurrencyCode);
        }
        Check(supplierIdentity,
            "SupplierId y nombres son válidos, estables y únicos.",
            ref correct, ref errors, lines);
        Check(supplierCommercialData,
            "Economía/plazos de proveedor usan enteros y límites defensivos.",
            ref correct, ref errors, lines);
        Check(supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.GeneralSupplierId) &&
              supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.FreshSupplierId) &&
              supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.PantrySupplierId) &&
              supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.PremiumSupplierId),
            "Los cuatro SupplierId base están presentes.",
            ref correct, ref errors, lines);

        Dictionary<string, BistroBuilderSupplierDefinition> supplierById =
            new Dictionary<string, BistroBuilderSupplierDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < settings.Suppliers.Count; i++)
            if (settings.Suppliers[i] != null && !supplierById.ContainsKey(settings.Suppliers[i].SupplierId))
                supplierById.Add(settings.Suppliers[i].SupplierId, settings.Suppliers[i]);

        Dictionary<string, BistroBuilderSupplierIngredientDescriptor> ingredientById =
            new Dictionary<string, BistroBuilderSupplierIngredientDescriptor>(StringComparer.Ordinal);
        for (int i = 0; i < ingredients.Count; i++)
            ingredientById[ingredients[i].IngredientId] = ingredients[i];

        HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> distinctSuppliersByIngredient =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        bool productIdentity = true;
        bool productForeignKeys = true;
        bool productCommercialData = true;
        bool productUnitsCompatible = true;
        bool productCurrencyMatches = true;
        bool productOrderDeterministic = true;
        string previousProductId = string.Empty;

        for (int i = 0; i < settings.Products.Count; i++)
        {
            BistroBuilderSupplierProductDefinition product = settings.Products[i];
            productIdentity &= product != null &&
                BistroBuilderMenuIdUtility.IsValidStableId(product.ProductId) &&
                productIds.Add(product.ProductId);
            if (product == null) continue;

            productOrderDeterministic &= i == 0 ||
                string.Compare(previousProductId, product.ProductId, StringComparison.Ordinal) <= 0;
            previousProductId = product.ProductId;

            bool supplierExists = supplierById.TryGetValue(product.SupplierId, out var supplier);
            bool ingredientExists = ingredientById.TryGetValue(product.IngredientId, out var ingredient);
            productForeignKeys &= supplierExists && ingredientExists;
            productCommercialData &= !string.IsNullOrWhiteSpace(product.DisplayName) &&
                !string.IsNullOrWhiteSpace(product.PackageLabel) &&
                product.PackageCanonicalMilliUnits > 0L &&
                product.PackageCanonicalMilliUnits <= BistroBuilderSupplierProductDefinition.MaximumPackageCanonicalMilliUnits &&
                product.PackPriceCents >= 1 &&
                product.PackPriceCents <= BistroBuilderSupplierProductDefinition.MaximumPackPriceCents &&
                product.MinimumPacks >= 1 &&
                product.MinimumPacks <= BistroBuilderSupplierProductDefinition.MaximumMinimumPacks &&
                product.LeadTimeDays >= 0 &&
                product.LeadTimeDays <= BistroBuilderSupplierProductDefinition.MaximumLeadTimeDays;
            productUnitsCompatible &= ingredientExists &&
                BistroBuilderMeasurementUtility.IsCanonicalBaseUnit(product.BaseUnit) &&
                product.BaseUnit == ingredient.BaseUnit;
            productCurrencyMatches &= supplierExists &&
                string.Equals(product.CurrencyCode, supplier.CurrencyCode, StringComparison.Ordinal);

            if (!distinctSuppliersByIngredient.TryGetValue(product.IngredientId, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                distinctSuppliersByIngredient.Add(product.IngredientId, set);
            }
            set.Add(product.SupplierId);
        }

        Check(productIdentity,
            "ProductId persistidos son válidos y únicos.", ref correct, ref errors, lines);
        Check(productForeignKeys,
            "Todos los SKU enlazan SupplierId e IngredientId existentes.", ref correct, ref errors, lines);
        Check(productCommercialData,
            "Todos los SKU tienen formato, cantidad, precio, mínimos y plazo válidos.",
            ref correct, ref errors, lines);
        Check(productUnitsCompatible,
            "Cada SKU usa la unidad base del ingrediente canónico enlazado.",
            ref correct, ref errors, lines);
        Check(productCurrencyMatches,
            "Cada SKU usa la misma moneda que su proveedor.", ref correct, ref errors, lines);
        Check(productOrderDeterministic,
            "Los SKU persistidos están ordenados determinísticamente por ProductId.",
            ref correct, ref errors, lines);

        bool coverage = true;
        bool generalistCoverage = true;
        for (int i = 0; i < ingredients.Count; i++)
        {
            string ingredientId = ingredients[i].IngredientId;
            coverage &= distinctSuppliersByIngredient.TryGetValue(ingredientId, out var providers) &&
                providers.Count >= BistroBuilderSupplierCatalogBuilder.RecommendedDistinctSuppliersPerIngredient;
            generalistCoverage &= ContainsProduct(
                settings.Products, BistroBuilderSupplierCatalogDefaults.GeneralSupplierId, ingredientId);
        }
        Check(coverage,
            "Cada ingrediente tiene al menos dos proveedores estructurales distintos.",
            ref correct, ref errors, lines);
        Check(generalistCoverage,
            "El SKU base generalista está presente para todos los ingredientes.",
            ref correct, ref errors, lines);

        List<BistroBuilderSupplierProductDefinition> defaultSeeds =
            BistroBuilderSupplierCatalogBuilder.BuildProducts(CloneSuppliers(settings.Suppliers), ingredients);
        bool allSeedMappingsValid = defaultSeeds.Count == ingredients.Count * 2;
        for (int i = 0; i < defaultSeeds.Count; i++)
        {
            BistroBuilderSupplierProductDefinition expected = defaultSeeds[i];
            BistroBuilderSupplierProductDefinition actual = FindProduct(
                settings.Products, expected.ProductId);
            allSeedMappingsValid &= actual != null &&
                actual.SupplierId == expected.SupplierId &&
                actual.IngredientId == expected.IngredientId;
        }
        Check(allSeedMappingsValid,
            "Todos los ProductId de semilla base conservan su mapeo SupplierId -> IngredientId sin impedir SKU adicionales.",
            ref correct, ref errors, lines);

        Check(settings.Products.Count >= ingredients.Count * 2,
            "El catálogo persistido tiene como mínimo dos SKU base por ingrediente.",
            ref correct, ref errors, lines);

        Check(
            typeof(BistroBuilderSupplierCatalogService).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly) == null,
            "SupplierCatalogService no implementa polling por Update.",
            ref correct, ref errors, lines);

        FieldInfo[] serviceFields = typeof(BistroBuilderSupplierCatalogService).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        bool noInventoryWriteDependency = true;
        for (int i = 0; i < serviceFields.Length; i++)
        {
            string name = serviceFields[i].FieldType.FullName ?? serviceFields[i].FieldType.Name;
            if (name.IndexOf("InventoryService", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("GoodsReceiving", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                noInventoryWriteDependency = false;
                break;
            }
        }
        Check(noInventoryWriteDependency,
            "2.3A1 no conserva dependencias de escritura a Inventario/Recepciones.",
            ref correct, ref errors, lines);

        Check(typeof(BistroBuilderSupplierCatalogSettings).GetField(
                "products", BindingFlags.Instance | BindingFlags.NonPublic) != null,
            "supplier.catalog v2 mantiene los SKU como datos serializados canónicos.",
            ref correct, ref errors, lines);

        Finish(correct, warnings, errors, lines);
    }


    private static BistroBuilderSupplierProductDefinition FindProduct(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> products,
        string productId)
    {
        if (products == null) return null;
        for (int i = 0; i < products.Count; i++)
        {
            BistroBuilderSupplierProductDefinition product = products[i];
            if (product != null && product.ProductId == productId) return product;
        }
        return null;
    }

    private static bool ContainsProduct(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> products,
        string supplierId,
        string ingredientId)
    {
        if (products == null) return false;
        for (int i = 0; i < products.Count; i++)
        {
            BistroBuilderSupplierProductDefinition product = products[i];
            if (product != null && product.SupplierId == supplierId && product.IngredientId == ingredientId)
                return true;
        }
        return false;
    }

    private static List<BistroBuilderSupplierDefinition> CloneSuppliers(
        IReadOnlyList<BistroBuilderSupplierDefinition> source)
    {
        List<BistroBuilderSupplierDefinition> result = new List<BistroBuilderSupplierDefinition>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null) result.Add(source[i].Clone());
        result.Sort((a, b) => string.Compare(a.SupplierId, b.SupplierId, StringComparison.Ordinal));
        return result;
    }

    private static void Check(
        bool condition,
        string message,
        ref int correct,
        ref int errors,
        List<string> lines)
    {
        if (condition) Pass(message, ref correct, lines);
        else
        {
            errors++;
            lines.Add("ERROR: " + message);
        }
    }

    private static void Pass(string message, ref int correct, List<string> lines)
    {
        correct++;
        lines.Add("OK: " + message);
    }

    private static void Finish(
        int correct,
        int warnings,
        int errors,
        List<string> lines)
    {
        string report =
            "BISTRO BUILDER — VALIDACIÓN 2.3A1\n\n" +
            "Correctos: " + correct + "\n" +
            "Advertencias: " + warnings + "\n" +
            "Errores: " + errors + "\n\n" +
            string.Join("\n", lines);

        if (errors == 0) Debug.Log(report); else Debug.LogError(report);
    }
}
