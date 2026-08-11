#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest no destructivo de 2.3B3.
/// Comprueba proyección, integridad e idempotencia sin publicar assets.
/// </summary>
public sealed class BistroBuilderSuppliers23B3AutotestWindow : EditorWindow
{
    private readonly List<string> lines = new List<string>();
    private Vector2 scroll;
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3B3 - Autotest convergencia canónica", priority = 24)]
    public static void OpenAndRun()
    {
        BistroBuilderSuppliers23B3AutotestWindow window =
            GetWindow<BistroBuilderSuppliers23B3AutotestWindow>(true, "Autotest 2.3B3", true);
        window.minSize = new Vector2(760f, 520f);
        window.Run();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("AUTOTEST 2.3B3 — CONVERGENCIA CANÓNICA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Pruebas superadas: " + passed + " / Pruebas fallidas: " + failed,
            failed == 0 ? MessageType.Info : MessageType.Error);

        if (GUILayout.Button("Repetir autotest", GUILayout.Height(28f)))
        {
            Run();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < lines.Count; index++)
        {
            EditorGUILayout.LabelField(lines[index], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        passed = 0;
        failed = 0;
        lines.Clear();

        BistroBuilderSupplierAuthoringDatabase supplierDb = BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredientDb = BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Autotest ejecutado en Edit Mode.");
        Check(supplierDb != null, "supplier.authoring existe.");
        Check(ingredientDb != null, "ingredient.authoring existe.");

        if (supplierDb == null || ingredientDb == null)
        {
            Finish();
            return;
        }

        int supplierRevisionBefore = supplierDb.ContentRevision;
        int ingredientRevisionBefore = ingredientDb.ContentRevision;

        Check(supplierDb.SchemaVersion == 2, "supplier.authoring usa schema v2.");
        Check(ingredientDb.SchemaVersion == 2, "ingredient.authoring usa schema v2.");
        Check(supplierDb.Suppliers.Count(x => x != null && x.isActive) == 6, "Hay 6 proveedores activos.");
        Check(ingredientDb.Ingredients.Count(x => x != null && x.isActive) >= 22, "Hay al menos 22 ingredientes activos.");
        Check(CountOffers(supplierDb) == 66, "Hay exactamente 66 ofertas base activas.");

        bool contractOk = BistroBuilderSuppliers23B3CanonicalBridge.TryDiscoverContract(
            out BistroBuilderSuppliers23B3CanonicalBridge.Contract contract,
            out string contractError);
        Check(contractOk, "Se descubre el contrato supplier.catalog: " + contractError);

        if (!contractOk)
        {
            Finish();
            return;
        }

        Check(contract.serviceType != null, "Existe el tipo SupplierCatalogService.");
        Check(contract.applyCandidateMethod != null, "Existe el contrato atómico de candidato de 2.3A1.");
        Check(contract.supplierType != null, "Se resuelve el tipo runtime de proveedor.");
        Check(contract.productType != null, "Se resuelve el tipo runtime de producto.");
        Check(contract.ingredientDescriptorType != null, "Se resuelve el descriptor runtime de ingrediente.");
        Check(contract.catalogAsset != null, "Se localiza el asset canónico.");
        Check(contract.catalogAssetPath.StartsWith("Assets/Resources/", StringComparison.Ordinal), "supplier.catalog está en Resources.");

        string assetJsonBefore = EditorJsonUtility.ToJson(contract.catalogAsset);
        string assetFingerprintBefore = BistroBuilderSuppliers23B3CanonicalBridge.BuildFingerprint(contract.catalogAsset, contract);

        bool projectionOk = BistroBuilderSuppliers23B3CanonicalBridge.TryBuildProjection(
            contract,
            out BistroBuilderSuppliers23B3CanonicalBridge.Projection projection,
            out string projectionError);
        Check(projectionOk, "La proyección completa se construye: " + projectionError);

        if (!projectionOk)
        {
            Finish();
            return;
        }

        Check(projection.suppliers.Count == 6, "La proyección contiene 6 proveedores.");
        Check(projection.products.Count == 66, "La proyección contiene 66 productos.");
        Check(projection.ingredients.Count >= 22, "La proyección conserva 22+ ingredientes.");
        Check(projection.supplierIds.Count == 6, "Todos los SupplierId proyectados son únicos.");
        Check(projection.productIds.Count == 66, "Todos los ProductId proyectados son únicos.");
        Check(projection.ingredientIds.Count >= 22, "Todos los IngredientId proyectados son indexables.");

        string[] expectedSuppliers =
        {
            "supplier_mercado_central",
            "supplier_distribuciones_norte",
            "supplier_hosteleria_express",
            "supplier_huerta_clara",
            "supplier_carnes_selectas",
            "supplier_costa_fresca"
        };
        Check(expectedSuppliers.All(projection.supplierIds.Contains), "Los seis SupplierId provisionales están en la proyección.");

        bool fkSupplierOk = true;
        bool fkIngredientOk = true;
        bool priceOk = true;
        bool quantityOk = true;
        bool productIdsReadBack = true;

        foreach (object product in projection.products)
        {
            string productId = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product,
                    "ProductId", "productId", "SupplierOfferId", "supplierOfferId", "Id", "id"));
            string supplierId = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "SupplierId", "supplierId"));
            string ingredientId = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "IngredientId", "ingredientId"));

            if (string.IsNullOrWhiteSpace(productId) || !projection.productIds.Contains(productId)) productIdsReadBack = false;
            if (!projection.supplierIds.Contains(supplierId)) fkSupplierOk = false;
            if (!projection.ingredientIds.Contains(ingredientId)) fkIngredientOk = false;

            if (!BistroBuilderSuppliers23B3CanonicalBridge.TryReadPriceCents(
                    product, out long priceCents, out _) ||
                priceCents <= 0)
            {
                priceOk = false;
            }

            object quantity = BistroBuilderSuppliers23B3CanonicalBridge.ReadMember(product,
                "QuantityMicrounits", "quantityMicrounits", "NetQuantityMicrounits", "netQuantityMicrounits", "PackageQuantityMicrounits", "packageQuantityMicrounits", "QuantityPerPackageMicrounits", "quantityPerPackageMicrounits");
            if (quantity != null && Convert.ToInt64(quantity) <= 0) quantityOk = false;
        }

        Check(productIdsReadBack, "ProductId proyectado se puede leer de los 66 productos.");
        Check(fkSupplierOk, "Los 66 productos tienen SupplierId válido.");
        Check(fkIngredientOk, "Los 66 productos tienen IngredientId válido.");
        Check(priceOk, "Los 66 productos publican un precio positivo.");
        Check(quantityOk, "Las cantidades micro-unitarias disponibles son positivas.");

        Dictionary<string, HashSet<string>> suppliersPerIngredient = BuildSupplierCoverage(projection.products);
        Check(suppliersPerIngredient.Count >= 22, "La cobertura comercial alcanza todos los ingredientes.");
        Check(suppliersPerIngredient.Values.All(set => set.Count >= 2), "Cada ingrediente tiene al menos dos proveedores.");
        Check(CoversAllIngredients(projection.products, "supplier_mercado_central", projection.ingredientIds), "Mercado Central cubre todos los ingredientes.");
        Check(CoversAllIngredients(projection.products, "supplier_hosteleria_express", projection.ingredientIds), "Hostelería Express cubre todos los ingredientes.");

        bool secondProjectionOk = BistroBuilderSuppliers23B3CanonicalBridge.TryBuildProjection(
            contract,
            out BistroBuilderSuppliers23B3CanonicalBridge.Projection projection2,
            out _);
        Check(secondProjectionOk, "Una segunda proyección idéntica se construye.");
        Check(secondProjectionOk && projection2.supplierIds.SetEquals(projection.supplierIds), "La segunda proyección conserva SupplierId.");
        Check(secondProjectionOk && projection2.productIds.SetEquals(projection.productIds), "La segunda proyección conserva ProductId.");
        Check(secondProjectionOk && projection2.products.Count == projection.products.Count, "La segunda proyección conserva cardinalidad.");

        string assetJsonAfter = EditorJsonUtility.ToJson(contract.catalogAsset);
        string assetFingerprintAfter = BistroBuilderSuppliers23B3CanonicalBridge.BuildFingerprint(contract.catalogAsset, contract);

        Check(string.Equals(assetJsonBefore, assetJsonAfter, StringComparison.Ordinal), "El autotest no modifica supplier.catalog.");
        Check(string.Equals(assetFingerprintBefore, assetFingerprintAfter, StringComparison.Ordinal), "El fingerprint canónico no cambia durante el autotest.");
        Check(supplierDb.ContentRevision == supplierRevisionBefore, "El autotest no modifica ContentRevision de supplier.authoring.");
        Check(ingredientDb.ContentRevision == ingredientRevisionBefore, "El autotest no modifica ContentRevision de ingredient.authoring.");

        bool persistedOk = BistroBuilderSuppliers23B3CanonicalBridge.ValidatePersistedProjection(contract, projection, out _);
        Check(persistedOk, "supplier.catalog coincide profundamente con la proyección publicada.");

        Finish();
    }

    private static int CountOffers(BistroBuilderSupplierAuthoringDatabase database)
    {
        return database.Suppliers
            .Where(x => x != null && x.isActive)
            .Sum(x => x.baseOffers == null ? 0 : x.baseOffers.Count(o => o != null && o.isActive));
    }

    private static Dictionary<string, HashSet<string>> BuildSupplierCoverage(IList products)
    {
        Dictionary<string, HashSet<string>> result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        for (int index = 0; index < products.Count; index++)
        {
            object product = products[index];
            string ingredientId = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "IngredientId", "ingredientId"));
            string supplierId = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "SupplierId", "supplierId"));

            if (string.IsNullOrWhiteSpace(ingredientId) || string.IsNullOrWhiteSpace(supplierId)) continue;
            if (!result.TryGetValue(ingredientId, out HashSet<string> set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                result.Add(ingredientId, set);
            }
            set.Add(supplierId);
        }
        return result;
    }

    private static bool CoversAllIngredients(IList products, string supplierId, HashSet<string> ingredients)
    {
        HashSet<string> covered = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < products.Count; index++)
        {
            object product = products[index];
            string currentSupplier = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "SupplierId", "supplierId"));
            if (!string.Equals(currentSupplier, supplierId, StringComparison.Ordinal)) continue;
            covered.Add(BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "IngredientId", "ingredientId")));
        }
        return ingredients.All(covered.Contains);
    }

    private void Check(bool condition, string text)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + text);
        }
        else
        {
            failed++;
            lines.Add("[FALLO] " + text);
        }
    }

    private void Finish()
    {
        string summary = "AUTOTEST 2.3B3 — superadas: " + passed + ", fallidas: " + failed + ".";
        if (failed == 0) Debug.Log(summary); else Debug.LogError(summary);
        Repaint();
    }
}
#endif
