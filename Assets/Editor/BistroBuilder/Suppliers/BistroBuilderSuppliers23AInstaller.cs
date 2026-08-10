using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Instalador/migrador idempotente de 2.3A1.
///
/// Migra supplier.catalog v1 -> v2, convierte economía float a enteros y
/// persiste el catálogo inicial de productos. No modifica escenas, inventario
/// ni partidas guardadas.
/// </summary>
public static class BistroBuilderSuppliers23AInstaller
{
    private const string RootFolder = "Assets/Resources/BistroBuilder/Suppliers";
    private const string SettingsAssetPath =
        RootFolder + "/BistroBuilderSupplierCatalogSettings.asset";
    private const string IngredientCatalogPath =
        "Assets/Data/BistroBuilder/Ingredients/BistroBuilderIngredientCatalog.asset";

    [MenuItem("Tools/Bistro Builder/Suppliers/Install 2.3A Canonical Suppliers")]
    public static void Install()
    {
        int correct = 0;
        int warnings = 0;
        int errors = 0;
        List<string> lines = new List<string>();

        try
        {
            EnsureFolder("Assets", "Resources");
            Pass("Existe Assets/Resources.", ref correct, lines);
            EnsureFolder("Assets/Resources", "BistroBuilder");
            Pass("Existe Resources/BistroBuilder.", ref correct, lines);
            EnsureFolder("Assets/Resources/BistroBuilder", "Suppliers");
            Pass("Existe Resources/BistroBuilder/Suppliers.", ref correct, lines);

            BistroBuilderIngredientCatalog ingredientCatalog =
                AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientCatalog>(
                    IngredientCatalogPath);
            Check(ingredientCatalog != null,
                "Existe el IngredientCatalog canónico en su ruta estable.",
                ref correct, ref errors, lines);

            List<BistroBuilderSupplierIngredientDescriptor> ingredients = null;
            string ingredientError = string.Empty;
            bool ingredientReadOk = ingredientCatalog != null &&
                BistroBuilderCanonicalIngredientDiscovery.TryCreateDescriptorsFromCatalog(
                    ingredientCatalog,
                    out ingredients,
                    out ingredientError);
            Check(ingredientReadOk,
                "Los ingredientes canónicos se adaptan de forma tipada." +
                (ingredientReadOk ? string.Empty : " " + ingredientError),
                ref correct, ref errors, lines);

            if (!ingredientReadOk)
            {
                Finish(correct, warnings, errors, lines);
                return;
            }

            Check(ingredients.Count >= 22,
                "Se detectan al menos los 22 ingredientes canónicos de la línea base; el instalador admite ampliaciones futuras.",
                ref correct, ref errors, lines);

            BistroBuilderSupplierCatalogSettings settings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierCatalogSettings>(
                    SettingsAssetPath);

            bool created = false;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BistroBuilderSupplierCatalogSettings>();
                if (!settings.ResetToCanonicalDefaults(ingredients, out string resetError))
                {
                    Fail("No se pudo crear supplier.catalog v2: " + resetError,
                        ref errors, lines);
                    Finish(correct, warnings, errors, lines);
                    return;
                }
                AssetDatabase.CreateAsset(settings, SettingsAssetPath);
                created = true;
                Pass("Creado supplier.catalog v2 con proveedores y productos base.",
                    ref correct, lines);
            }
            else
            {
                Pass("Localizado el supplier.catalog existente.", ref correct, lines);
            }

            int schemaBefore = settings.SchemaVersion;
            bool ensureOk = settings.TryEnsureCanonicalDefaults(
                ingredients,
                out bool changed,
                out string migrationError);
            Check(ensureOk,
                "Migración/completado de supplier.catalog ejecutado." +
                (ensureOk ? string.Empty : " " + migrationError),
                ref correct, ref errors, lines);

            if (ensureOk && (changed || created || schemaBefore != settings.SchemaVersion))
            {
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Pass("AssetDatabase guardada y refrescada.", ref correct, lines);

            Check(
                settings.SchemaVersion == BistroBuilderSupplierCatalogSettings.CurrentSchemaVersion,
                "supplier.catalog usa schema v2.",
                ref correct, ref errors, lines);
            Check(settings.Suppliers != null && settings.Suppliers.Count >= 4,
                "Hay al menos cuatro proveedores configurados.",
                ref correct, ref errors, lines);
            Check(settings.Products != null && settings.Products.Count >= ingredients.Count * 2,
                "Hay al menos dos SKU iniciales por ingrediente.",
                ref correct, ref errors, lines);

            HashSet<string> supplierIds = new HashSet<string>(StringComparer.Ordinal);
            bool supplierIdsValid = true;
            bool supplierEconomyValid = true;
            for (int i = 0; i < settings.Suppliers.Count; i++)
            {
                var supplier = settings.Suppliers[i];
                supplierIdsValid &= supplier != null &&
                                    BistroBuilderMenuIdUtility.IsValidStableId(supplier.SupplierId) &&
                                    supplierIds.Add(supplier.SupplierId);
                if (supplier == null) continue;
                supplierEconomyValid &= supplier.MinimumOrderCents >= 0 &&
                    supplier.DefaultLeadTimeDays >= 0 &&
                    supplier.SeedPriceFactorBasisPoints >= 1 &&
                    BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode(
                        supplier.CurrencyCode);
            }
            Check(supplierIdsValid,
                "SupplierId válidos, normalizados y únicos.",
                ref correct, ref errors, lines);
            Check(supplierEconomyValid,
                "Economía de proveedores usa enteros y rangos válidos.",
                ref correct, ref errors, lines);

            HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);
            bool productIdsValid = true;
            bool productQuantitiesValid = true;
            bool productPricesValid = true;
            for (int i = 0; i < settings.Products.Count; i++)
            {
                var product = settings.Products[i];
                productIdsValid &= product != null &&
                    BistroBuilderMenuIdUtility.IsValidStableId(product.ProductId) &&
                    productIds.Add(product.ProductId);
                if (product == null) continue;
                productQuantitiesValid &= product.PackageCanonicalMilliUnits > 0L &&
                    product.PackageCanonicalMilliUnits <=
                        BistroBuilderSupplierProductDefinition.MaximumPackageCanonicalMilliUnits;
                productPricesValid &= product.PackPriceCents >= 1;
            }
            Check(productIdsValid,
                "ProductId persistidos son válidos y únicos.",
                ref correct, ref errors, lines);
            Check(productQuantitiesValid,
                "Cantidades de envase persistidas usan milli-units canónicas.",
                ref correct, ref errors, lines);
            Check(productPricesValid,
                "Precios de producto persistidos usan céntimos positivos.",
                ref correct, ref errors, lines);

            Check(
                supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.GeneralSupplierId) &&
                supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.FreshSupplierId) &&
                supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.PantrySupplierId) &&
                supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.PremiumSupplierId),
                "Están presentes los cuatro SupplierId base.",
                ref correct, ref errors, lines);

            List<BistroBuilderSupplierProductDefinition> expectedSeeds =
                BistroBuilderSupplierCatalogBuilder.BuildProducts(
                    CloneSuppliers(settings.Suppliers), ingredients);
            bool seedMappingsValid = expectedSeeds.Count == ingredients.Count * 2;
            for (int i = 0; i < expectedSeeds.Count; i++)
            {
                BistroBuilderSupplierProductDefinition expected = expectedSeeds[i];
                BistroBuilderSupplierProductDefinition actual = FindProduct(
                    settings.Products, expected.ProductId);
                seedMappingsValid &= actual != null &&
                    actual.SupplierId == expected.SupplierId &&
                    actual.IngredientId == expected.IngredientId;
            }
            Check(seedMappingsValid,
                "Todos los ProductId de semilla base conservan su mapeo SupplierId -> IngredientId.",
                ref correct, ref errors, lines);

            Check(HasRecommendedCoverage(settings.Products, ingredients),
                "El contenido inicial mantiene al menos dos proveedores distintos por ingrediente.",
                ref correct, ref errors, lines);

            BistroBuilderSupplierCatalogValidationResult validation =
                BistroBuilderSupplierCatalogValidator.Validate(
                    settings.Suppliers,
                    settings.Products,
                    ingredients,
                    BistroBuilderSupplierCatalogBuilder.RecommendedDistinctSuppliersPerIngredient,
                    reportOperationalGapsAsWarnings: true);
            Check(validation.IsValid,
                "supplier.catalog persistido supera el validador compartido." +
                (validation.IsValid || validation.Errors.Count == 0
                    ? string.Empty
                    : " Primer error: " + validation.Errors[0]),
                ref correct, ref errors, lines);
            warnings += validation.WarningCount;
            for (int i = 0; i < validation.Warnings.Count; i++)
                lines.Add("ADVERTENCIA CATÁLOGO: " + validation.Warnings[i]);

            bool secondEnsureOk = settings.TryEnsureCanonicalDefaults(
                ingredients,
                out bool secondChanged,
                out string secondError);
            Check(secondEnsureOk && !secondChanged,
                "Segunda instalación es realmente idempotente." +
                (secondEnsureOk ? string.Empty : " " + secondError),
                ref correct, ref errors, lines);

            string[] settingsGuids = AssetDatabase.FindAssets(
                "t:BistroBuilderSupplierCatalogSettings");
            if (settingsGuids.Length > 1)
            {
                warnings++;
                lines.Add(
                    "ADVERTENCIA: existen " + settingsGuids.Length +
                    " assets SupplierCatalogSettings. Solo ResourcesPath es autoridad.");
            }
            else
            {
                Pass("No se detectan settings alternativos de Proveedores.",
                    ref correct, lines);
            }

            Pass("El instalador no modifica escenas.", ref correct, lines);
            Pass("El instalador no modifica inventario ni recepciones.", ref correct, lines);
            Pass("El instalador no crea pedidos 2.3B.", ref correct, lines);
        }
        catch (Exception exception)
        {
            errors++;
            lines.Add("ERROR: excepción no controlada: " + exception.Message);
            Debug.LogException(exception);
        }

        Finish(correct, warnings, errors, lines);
    }

    private static List<BistroBuilderSupplierDefinition> CloneSuppliers(
        IReadOnlyList<BistroBuilderSupplierDefinition> source)
    {
        var result = new List<BistroBuilderSupplierDefinition>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null) result.Add(source[i].Clone());
        return result;
    }

    private static BistroBuilderSupplierProductDefinition FindProduct(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> source, string productId)
    {
        if (source == null) return null;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null && source[i].ProductId == productId) return source[i];
        return null;
    }

    private static bool HasRecommendedCoverage(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> products,
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> ingredients)
    {
        if (products == null || ingredients == null) return false;
        for (int i = 0; i < ingredients.Count; i++)
        {
            HashSet<string> suppliers = new HashSet<string>(StringComparer.Ordinal);
            for (int j = 0; j < products.Count; j++)
            {
                BistroBuilderSupplierProductDefinition product = products[j];
                if (product != null && product.IngredientId == ingredients[i].IngredientId)
                    suppliers.Add(product.SupplierId);
            }
            if (suppliers.Count < BistroBuilderSupplierCatalogBuilder.RecommendedDistinctSuppliersPerIngredient)
                return false;
        }
        return true;
    }

    private static void Finish(
        int correct,
        int warnings,
        int errors,
        List<string> lines)
    {
        string report =
            "BISTRO BUILDER — INSTALACIÓN / MIGRACIÓN 2.3A1\n\n" +
            "Correctos: " + correct + "\n" +
            "Advertencias: " + warnings + "\n" +
            "Errores: " + errors + "\n\n" +
            string.Join("\n", lines);

        if (errors == 0) Debug.Log(report); else Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 2.3A1",
            "Instalación/migración completada.\n\n" +
            "Correctos: " + correct + "\n" +
            "Advertencias: " + warnings + "\n" +
            "Errores: " + errors,
            "Aceptar");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string fullPath = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void Pass(string message, ref int correct, List<string> lines)
    {
        correct++;
        lines.Add("OK: " + message);
    }

    private static void Fail(string message, ref int errors, List<string> lines)
    {
        errors++;
        lines.Add("ERROR: " + message);
    }

    private static void Check(
        bool condition,
        string message,
        ref int correct,
        ref int errors,
        List<string> lines)
    {
        if (condition) Pass(message, ref correct, lines);
        else Fail(message, ref errors, lines);
    }
}
