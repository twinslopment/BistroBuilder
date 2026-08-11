#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prueba funcional runtime de 2.3B3.
/// Rebuild real de BistroBuilderSupplierCatalogService desde Resources y
/// comparación contra la autoría publicada. No crea pedidos ni toca stock.
/// </summary>
public sealed class BistroBuilderSuppliers23B3RuntimeFunctionalTestWindow : EditorWindow
{
    private readonly List<string> lines = new List<string>();
    private Vector2 scroll;
    private int passed;
    private int failed;
    private int runtimeErrors;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3B3 - Prueba funcional runtime", priority = 25)]
    public static void OpenWindow()
    {
        BistroBuilderSuppliers23B3RuntimeFunctionalTestWindow window =
            GetWindow<BistroBuilderSuppliers23B3RuntimeFunctionalTestWindow>(true, "Prueba runtime 2.3B3", true);
        window.minSize = new Vector2(780f, 560f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("PRUEBA FUNCIONAL RUNTIME 2.3B3", EditorStyles.boldLabel);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Entra en Play Mode y pulsa Ejecutar prueba.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "La prueba fuerza un rebuild REAL del SupplierCatalogService desde supplier.catalog y verifica que runtime publica 6 proveedores/66 productos sin tocar Inventario.",
                MessageType.Info);
        }

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
        if (GUILayout.Button("Ejecutar prueba funcional 2.3B3", GUILayout.Height(34f)))
        {
            Run();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Correctos: " + passed + " · Fallos: " + failed + " · Errores/Exception/Assert capturados: " + runtimeErrors,
            EditorStyles.boldLabel);

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
        runtimeErrors = 0;
        lines.Clear();

        if (!EditorApplication.isPlaying)
        {
            Fail("La prueba necesita Play Mode.");
            return;
        }

        Application.logMessageReceived += HandleLog;
        try
        {
            ExecuteCore();
        }
        catch (Exception exception)
        {
            Fail("Excepción de la prueba: " + exception.GetType().Name + " — " + exception.Message);
        }
        finally
        {
            Application.logMessageReceived -= HandleLog;
        }

        Check(runtimeErrors == 0, "La ejecución no capturó Error, Exception ni Assert.");

        string summary =
            (failed == 0 && runtimeErrors == 0 ? "PRUEBA RUNTIME 2.3B3 SUPERADA" : "PRUEBA RUNTIME 2.3B3 FALLIDA") +
            "\nCorrectos: " + passed +
            "\nFallos: " + failed +
            "\nErrores/Excepciones/Asserts capturados: " + runtimeErrors + ".";

        if (failed == 0 && runtimeErrors == 0) Debug.Log(summary); else Debug.LogError(summary);
        Repaint();
    }

    private void ExecuteCore()
    {
        bool contractOk = BistroBuilderSuppliers23B3CanonicalBridge.TryDiscoverContract(
            out BistroBuilderSuppliers23B3CanonicalBridge.Contract contract,
            out string contractError);
        Check(contractOk, "Contrato 2.3A1/supplier.catalog localizado en Play Mode: " + contractError);
        if (!contractOk) return;

        bool projectionOk = BistroBuilderSuppliers23B3CanonicalBridge.TryBuildProjection(
            contract,
            out BistroBuilderSuppliers23B3CanonicalBridge.Projection projection,
            out string projectionError);
        Check(projectionOk, "Proyección de autoría reconstruida en Play Mode: " + projectionError);
        if (!projectionOk) return;

        UnityEngine.Object[] services = Resources.FindObjectsOfTypeAll(contract.serviceType)
            .Where(IsLiveSceneComponent)
            .ToArray();

        Check(services.Length == 1, "Existe exactamente una autoridad SupplierCatalogService activa.");
        if (services.Length != 1) return;

        object service = services[0];
        Check(service != null, "SupplierCatalogService real localizado.");

        string inventoryBefore = CaptureLiveComponentJson("BistroBuilderInventoryService");

        int revisionBefore = ReadInt(service, "ContentRevision", "contentRevision", "Revision", "revision");
        Check(revisionBefore >= 0, "ContentRevision runtime es legible.");

        bool rebuild1 = InvokeTryRebuild(service, out string rebuildError1);
        Check(rebuild1, "Rebuild real desde supplier.catalog aceptado: " + rebuildError1);

        int revisionAfterFirst = ReadInt(service, "ContentRevision", "contentRevision", "Revision", "revision");
        Check(revisionAfterFirst >= revisionBefore, "ContentRevision no retrocede tras rebuild.");

        IList suppliers = ReadList(service, "Suppliers", "suppliers", "SupplierDefinitions", "supplierDefinitions");
        IList products = ReadList(service, "Products", "products", "SupplierProducts", "supplierProducts", "ProductDefinitions", "productDefinitions");
        IList ingredients = ReadList(service, "Ingredients", "ingredients", "IngredientDescriptors", "ingredientDescriptors");

        Check(suppliers != null, "Runtime expone colección de proveedores.");
        Check(products != null, "Runtime expone colección de productos.");
        Check(ingredients != null, "Runtime expone colección de ingredientes.");
        if (suppliers == null || products == null || ingredients == null) return;

        Check(suppliers.Count == 6, "Runtime publica exactamente 6 proveedores.");
        Check(products.Count == 66, "Runtime publica exactamente 66 productos/ofertas base.");
        Check(ingredients.Count >= 22, "Runtime conserva al menos 22 ingredientes canónicos.");

        HashSet<string> supplierIds = BistroBuilderSuppliers23B3CanonicalBridge.ExtractIds(
            suppliers, "SupplierId", "supplierId", "Id", "id");
        HashSet<string> productIds = BistroBuilderSuppliers23B3CanonicalBridge.ExtractIds(
            products, "ProductId", "productId", "SupplierOfferId", "supplierOfferId", "Id", "id");
        HashSet<string> ingredientIds = BistroBuilderSuppliers23B3CanonicalBridge.ExtractIds(
            ingredients, "IngredientId", "ingredientId", "Id", "id");

        Check(supplierIds.SetEquals(projection.supplierIds), "SupplierId runtime coinciden exactamente con autoría.");
        Check(productIds.SetEquals(projection.productIds), "ProductId runtime coinciden exactamente con las 66 ofertas de autoría.");
        Check(ingredientIds.IsSupersetOf(projection.ingredientIds), "IngredientId runtime cubren la proyección de autoría.");

        string[] expectedSuppliers =
        {
            "supplier_mercado_central",
            "supplier_distribuciones_norte",
            "supplier_hosteleria_express",
            "supplier_huerta_clara",
            "supplier_carnes_selectas",
            "supplier_costa_fresca"
        };
        Check(expectedSuppliers.All(supplierIds.Contains), "Los seis proveedores provisionales están activos en runtime.");

        bool allFkSupplier = true;
        bool allFkIngredient = true;
        bool allPricePositive = true;
        bool allQuantityPositive = true;
        Dictionary<string, HashSet<string>> coverage = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        for (int index = 0; index < products.Count; index++)
        {
            object product = products[index];
            string supplierId = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "SupplierId", "supplierId"));
            string ingredientId = BistroBuilderSuppliers23B3CanonicalBridge.NormalizeId(
                BistroBuilderSuppliers23B3CanonicalBridge.ReadString(product, "IngredientId", "ingredientId"));

            if (!supplierIds.Contains(supplierId)) allFkSupplier = false;
            if (!ingredientIds.Contains(ingredientId)) allFkIngredient = false;

            if (!BistroBuilderSuppliers23B3CanonicalBridge.TryReadPriceCents(
                    product, out long priceCents, out _) ||
                priceCents <= 0)
            {
                allPricePositive = false;
            }

            object quantity = BistroBuilderSuppliers23B3CanonicalBridge.ReadMember(product,
                "QuantityMicrounits", "quantityMicrounits", "NetQuantityMicrounits", "netQuantityMicrounits", "PackageQuantityMicrounits", "packageQuantityMicrounits", "QuantityPerPackageMicrounits", "quantityPerPackageMicrounits");
            if (quantity != null && Convert.ToInt64(quantity) <= 0) allQuantityPositive = false;

            if (!coverage.TryGetValue(ingredientId, out HashSet<string> set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                coverage.Add(ingredientId, set);
            }
            set.Add(supplierId);
        }

        Check(allFkSupplier, "Todos los productos runtime resuelven SupplierId válido.");
        Check(allFkIngredient, "Todos los productos runtime resuelven IngredientId canónico.");
        Check(allPricePositive, "Los 66 productos runtime tienen precio positivo.");
        Check(allQuantityPositive, "Las cantidades runtime disponibles son positivas.");
        Check(coverage.Count >= 22, "Runtime ofrece cobertura comercial para todos los ingredientes.");
        Check(coverage.Values.All(set => set.Count >= 2), "Cada ingrediente runtime dispone de al menos dos proveedores.");
        Check(CoversSupplier(products, "supplier_mercado_central", projection.ingredientIds), "Mercado Central cubre todos los ingredientes runtime.");
        Check(CoversSupplier(products, "supplier_hosteleria_express", projection.ingredientIds), "Hostelería Express cubre todos los ingredientes runtime.");

        bool rebuild2 = InvokeTryRebuild(service, out string rebuildError2);
        Check(rebuild2, "Segundo rebuild idéntico aceptado: " + rebuildError2);
        int revisionAfterSecond = ReadInt(service, "ContentRevision", "contentRevision", "Revision", "revision");
        Check(revisionAfterSecond == revisionAfterFirst, "Rebuild idéntico no incrementa ContentRevision.");

        IList productsAfter = ReadList(service, "Products", "products", "SupplierProducts", "supplierProducts", "ProductDefinitions", "productDefinitions");
        HashSet<string> productIdsAfter = BistroBuilderSuppliers23B3CanonicalBridge.ExtractIds(
            productsAfter, "ProductId", "productId", "SupplierOfferId", "supplierOfferId", "Id", "id");
        Check(productIdsAfter.SetEquals(productIds), "Rebuild idéntico conserva exactamente los 66 ProductId.");

        string schema = BistroBuilderSuppliers23B3CanonicalBridge.ReadString(service, "SchemaId", "schemaId");
        Check(string.IsNullOrWhiteSpace(schema) || string.Equals(schema, "supplier.catalog", StringComparison.OrdinalIgnoreCase),
            "Runtime publica schema supplier.catalog.");

        string inventoryAfter = CaptureLiveComponentJson("BistroBuilderInventoryService");
        Check(string.IsNullOrEmpty(inventoryBefore) || string.Equals(inventoryBefore, inventoryAfter, StringComparison.Ordinal),
            "La convergencia de proveedores no modifica el estado serializado de InventoryService.");
    }

    private static bool IsLiveSceneComponent(UnityEngine.Object obj)
    {
        Component component = obj as Component;
        if (component == null || component.gameObject == null) return false;
        Scene scene = component.gameObject.scene;
        return scene.IsValid() && component.gameObject.activeInHierarchy;
    }

    private static bool InvokeTryRebuild(object service, out string error)
    {
        error = string.Empty;
        if (service == null) return false;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo method = service.GetType().GetMethod("TryRebuildCatalog", flags);
        if (method == null)
        {
            error = "No existe TryRebuildCatalog.";
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        object[] args = parameters.Length == 0 ? Array.Empty<object>() : new object[parameters.Length];
        for (int index = 0; index < parameters.Length; index++)
        {
            args[index] = parameters[index].ParameterType.IsByRef ? null : GetDefault(parameters[index].ParameterType);
        }

        object returned = method.Invoke(service, args);
        for (int index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].ParameterType.IsByRef && args[index] is string text)
            {
                error = text;
            }
        }

        return returned is bool boolean && boolean;
    }

    private static object GetDefault(Type type)
    {
        return type != null && type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static IList ReadList(object target, params string[] names)
    {
        object value = BistroBuilderSuppliers23B3CanonicalBridge.ReadMember(target, names);
        if (value is IList direct) return direct;
        if (value is IEnumerable enumerable)
        {
            ArrayList list = new ArrayList();
            foreach (object item in enumerable) list.Add(item);
            return list;
        }
        return null;
    }

    private static int ReadInt(object target, params string[] names)
    {
        object value = BistroBuilderSuppliers23B3CanonicalBridge.ReadMember(target, names);
        if (value == null) return -1;
        try { return Convert.ToInt32(value); } catch { return -1; }
    }

    private static string CaptureLiveComponentJson(string simpleTypeName)
    {
        Type type = FindType(simpleTypeName);
        if (type == null) return string.Empty;

        UnityEngine.Object candidate = Resources.FindObjectsOfTypeAll(type).FirstOrDefault(IsLiveSceneComponent);
        return candidate == null ? string.Empty : EditorJsonUtility.ToJson(candidate);
    }

    private static Type FindType(string simpleName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { types = exception.Types.Where(x => x != null).ToArray(); }
            Type type = types.FirstOrDefault(x => x != null && string.Equals(x.Name, simpleName, StringComparison.Ordinal));
            if (type != null) return type;
        }
        return null;
    }

    private static bool CoversSupplier(IList products, string supplierId, HashSet<string> expectedIngredients)
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
        return expectedIngredients.All(covered.Contains);
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            runtimeErrors++;
            lines.Add("[RUNTIME " + type + "] " + condition);
        }
    }

    private void Check(bool condition, string text)
    {
        if (condition) Pass(text); else Fail(text);
    }

    private void Pass(string text)
    {
        passed++;
        lines.Add("[OK] " + text);
    }

    private void Fail(string text)
    {
        failed++;
        lines.Add("[FALLO] " + text);
    }
}
#endif
