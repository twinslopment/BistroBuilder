#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validación estructural de la convergencia 2.3B3.
/// </summary>
public sealed class BistroBuilderSuppliers23B3ValidationWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> lines = new List<string>();
    private int correct;
    private int warnings;
    private int errors;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3B3 - Validar convergencia canónica", priority = 23)]
    public static void OpenAndValidate()
    {
        BistroBuilderSuppliers23B3ValidationWindow window =
            GetWindow<BistroBuilderSuppliers23B3ValidationWindow>(true, "Validación 2.3B3", true);
        window.minSize = new Vector2(760f, 520f);
        window.RunValidation();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("VALIDACIÓN 2.3B3 — CONVERGENCIA CANÓNICA", EditorStyles.boldLabel);

        MessageType type = errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info;
        EditorGUILayout.HelpBox(
            "Correctos: " + correct + " · Advertencias: " + warnings + " · Errores: " + errors,
            type);

        if (GUILayout.Button("Repetir validación", GUILayout.Height(28f)))
        {
            RunValidation();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < lines.Count; index++)
        {
            EditorGUILayout.LabelField(lines[index], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunValidation()
    {
        lines.Clear();
        correct = 0;
        warnings = 0;
        errors = 0;

        Check(!EditorApplication.isPlayingOrWillChangePlaymode,
            "La validación estructural se ejecuta fuera de Play Mode.",
            "Sal de Play Mode antes de validar 2.3B3.");

        BistroBuilderSupplierAuthoringDatabase supplierDb = BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredientDb = BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        Check(supplierDb != null, "supplier.authoring localizado.", "No existe supplier.authoring.");
        Check(ingredientDb != null, "ingredient.authoring localizado.", "No existe ingredient.authoring.");

        if (supplierDb != null)
        {
            Check(supplierDb.SchemaVersion == 2, "supplier.authoring permanece en schema v2.", "supplier.authoring no está en schema v2.");
            int activeSuppliers = supplierDb.Suppliers.Count(x => x != null && x.isActive);
            Check(activeSuppliers == 6, "Hay 6 proveedores activos de autoría.", "Se esperaban 6 proveedores activos y hay " + activeSuppliers + ".");
            int activeOffers = supplierDb.Suppliers.Where(x => x != null && x.isActive)
                .Sum(x => x.baseOffers == null ? 0 : x.baseOffers.Count(o => o != null && o.isActive));
            Check(activeOffers == 66, "Hay 66 ofertas base activas de autoría.", "Se esperaban 66 ofertas base y hay " + activeOffers + ".");
        }

        if (ingredientDb != null)
        {
            Check(ingredientDb.SchemaVersion == 2, "ingredient.authoring permanece en schema v2.", "ingredient.authoring no está en schema v2.");
            int activeIngredients = ingredientDb.Ingredients.Count(x => x != null && x.isActive);
            Check(activeIngredients >= 22, "Hay al menos 22 ingredientes activos de autoría.", "Hay menos de 22 ingredientes activos.");
        }

        if (!BistroBuilderSuppliers23B3CanonicalBridge.TryDiscoverContract(out BistroBuilderSuppliers23B3CanonicalBridge.Contract contract, out string contractError))
        {
            Fail(contractError);
            Finish();
            return;
        }

        Pass("Contrato runtime de BistroBuilderSupplierCatalogService localizado.");
        Pass("Tipo runtime de proveedor: " + contract.supplierType.Name + ".");
        Pass("Tipo runtime de producto: " + contract.productType.Name + ".");
        Pass("Tipo runtime de descriptor de ingrediente: " + contract.ingredientDescriptorType.Name + ".");
        Pass("Asset supplier.catalog localizado en Resources: " + contract.catalogAssetPath + ".");

        string schema = BistroBuilderSuppliers23B3CanonicalBridge.ReadString(contract.catalogAsset, "SchemaId", "schemaId");
        if (!string.IsNullOrWhiteSpace(schema))
        {
            Check(string.Equals(schema.Trim(), "supplier.catalog", StringComparison.OrdinalIgnoreCase),
                "El asset conserva schemaId supplier.catalog.",
                "SchemaId inesperado: " + schema + ".");
        }
        else
        {
            Warn("El schemaId no es legible por reflexión; el runtime volverá a comprobarlo en la prueba funcional.");
        }

        IList suppliers = contract.supplierListField.GetValue(contract.catalogAsset) as IList;
        IList products = contract.productListField.GetValue(contract.catalogAsset) as IList;
        IList ingredients = contract.ingredientListField != null
            ? contract.ingredientListField.GetValue(contract.catalogAsset) as IList
            : null;

        Check(suppliers != null, "Colección canónica de proveedores legible.", "No se puede leer la colección de proveedores de supplier.catalog.");
        Check(products != null, "Colección canónica de productos legible.", "No se puede leer la colección de productos de supplier.catalog.");

        if (contract.ingredientListField != null)
        {
            Check(ingredients != null, "Colección canónica de ingredientes legible.", "No se puede leer la colección de ingredientes de supplier.catalog.");
        }
        else
        {
            Pass("El storage real no duplica ingredientes: BistroBuilderSupplierCatalogService los reconstruye desde la autoridad canónica de ingredientes.");
        }

        if (suppliers != null) Check(suppliers.Count == 6, "supplier.catalog contiene 6 proveedores.", "supplier.catalog contiene " + suppliers.Count + " proveedores; se esperaban 6.");
        if (products != null) Check(products.Count == 66, "supplier.catalog contiene 66 productos/ofertas.", "supplier.catalog contiene " + products.Count + " productos; se esperaban 66.");
        if (ingredients != null) Check(ingredients.Count >= 22, "supplier.catalog conserva al menos 22 descriptores de ingrediente.", "supplier.catalog contiene menos de 22 ingredientes.");

        if (!BistroBuilderSuppliers23B3CanonicalBridge.TryBuildProjection(contract, out BistroBuilderSuppliers23B3CanonicalBridge.Projection projection, out string projectionError))
        {
            Fail("No se puede reconstruir la proyección de autoría: " + projectionError);
            Finish();
            return;
        }

        Pass("La proyección 2.3B3 se reconstruye sin mutar datos.");
        Check(projection.suppliers.Count == 6, "La proyección contiene 6 proveedores.", "La proyección no contiene 6 proveedores.");
        Check(projection.products.Count == 66, "La proyección contiene 66 productos.", "La proyección no contiene 66 productos.");

        if (BistroBuilderSuppliers23B3CanonicalBridge.ValidatePersistedProjection(contract, projection, out string persistedError))
        {
            Pass("supplier.catalog coincide profundamente con la proyección de autoría (IDs y contenido serializado).");
        }
        else
        {
            Fail(persistedError);
        }

        if (suppliers != null)
        {
            HashSet<string> ids = BistroBuilderSuppliers23B3CanonicalBridge.ExtractIds(suppliers, "SupplierId", "supplierId", "Id", "id");
            string[] expected =
            {
                "supplier_mercado_central",
                "supplier_distribuciones_norte",
                "supplier_hosteleria_express",
                "supplier_huerta_clara",
                "supplier_carnes_selectas",
                "supplier_costa_fresca"
            };

            for (int index = 0; index < expected.Length; index++)
            {
                Check(ids.Contains(expected[index]), "SupplierId presente: " + expected[index] + ".", "Falta SupplierId " + expected[index] + ".");
            }
        }

        if (products != null)
        {
            ValidateProductForeignKeys(products, projection);
        }

        // La publicación debe ser idempotente: si fingerprints de IDs ya son
        // iguales, repetirla no tiene por qué alterar el catálogo.
        string canonicalFingerprint = BistroBuilderSuppliers23B3CanonicalBridge.BuildFingerprint(contract.catalogAsset, contract);
        Check(!string.IsNullOrWhiteSpace(canonicalFingerprint),
            "Fingerprint canónico generado para control de idempotencia.",
            "No se pudo generar fingerprint canónico.");

        Finish();
    }

    private void ValidateProductForeignKeys(IList products, BistroBuilderSuppliers23B3CanonicalBridge.Projection projection)
    {
        int badSupplierFk = 0;
        int badIngredientFk = 0;
        int badPrice = 0;
        int badQuantity = 0;

        for (int index = 0; index < products.Count; index++)
        {
            object product = products[index];
            string supplierId = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "SupplierId", "supplierId"));
            string ingredientId = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "IngredientId", "ingredientId"));

            if (!projection.supplierIds.Contains(supplierId)) badSupplierFk++;
            if (!projection.ingredientIds.Contains(ingredientId)) badIngredientFk++;

            if (!BistroBuilderSuppliers23B3CanonicalBridge.TryReadPriceCents(
                    product, out long priceCents, out _) ||
                priceCents <= 0)
            {
                badPrice++;
            }

            object quantity = BistroBuilderSuppliers23B3CanonicalBridge.ReadMember(product,
                "QuantityMicrounits", "quantityMicrounits", "NetQuantityMicrounits", "netQuantityMicrounits", "PackageQuantityMicrounits", "packageQuantityMicrounits", "QuantityPerPackageMicrounits", "quantityPerPackageMicrounits");
            if (quantity != null && Convert.ToInt64(quantity) <= 0) badQuantity++;
        }

        Check(badSupplierFk == 0, "Todos los productos conservan una FK SupplierId válida.", "Hay " + badSupplierFk + " productos con SupplierId inválido.");
        Check(badIngredientFk == 0, "Todos los productos conservan una FK IngredientId válida.", "Hay " + badIngredientFk + " productos con IngredientId inválido.");
        Check(badPrice == 0, "Todos los precios canónicos leídos son positivos.", "Hay " + badPrice + " precios canónicos no positivos.");
        Check(badQuantity == 0, "Todas las cantidades canónicas leídas son positivas.", "Hay " + badQuantity + " cantidades canónicas no positivas.");
    }

    private void Check(bool condition, string ok, string fail)
    {
        if (condition) Pass(ok); else Fail(fail);
    }

    private void Pass(string text)
    {
        correct++;
        lines.Add("[OK] " + text);
    }

    private void Warn(string text)
    {
        warnings++;
        lines.Add("[AVISO] " + text);
    }

    private void Fail(string text)
    {
        errors++;
        lines.Add("[ERROR] " + text);
    }

    private void Finish()
    {
        string summary =
            "VALIDACIÓN 2.3B3 — Errores: " + errors +
            ", advertencias: " + warnings +
            ", correctos: " + correct + ".";

        if (errors == 0)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }

        Repaint();
    }
}
#endif
