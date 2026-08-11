#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2.3B3E — Puente de convergencia adaptativo entre las bases de autoría 2.3A/B
/// y la única autoridad runtime supplier.catalog existente.
///
/// Principios:
/// - no crea un segundo catálogo operativo;
/// - descubre el contrato runtime ya validado en 2.3A1;
/// - proyecta supplier.authoring + ingredient.authoring hacia ese contrato;
/// - persiste de forma atómica sobre el asset supplier.catalog existente;
/// - restaura automáticamente el asset si la publicación no supera la
///   comprobación post-escritura;
/// - nunca escribe en Inventario ni en Recepciones.
///
/// Se usa reflexión de forma deliberada porque 2.3B3 debe extender el
/// contrato ya existente sin duplicar sus tipos ni acoplar esta entrega a
/// nombres de campos privados que pueden haber cambiado durante 2.3A1.
/// </summary>
internal static class BistroBuilderSuppliers23B3CanonicalBridge
{
    internal const int ExpectedSupplierCount = 6;
    internal const int ExpectedOfferCount = 66;
    internal const int ExpectedIngredientCount = 22;

    internal sealed class Contract
    {
        public Type serviceType;
        public MethodInfo applyCandidateMethod;
        public Type supplierType;
        public Type productType;
        public Type ingredientDescriptorType;
        public Type catalogAssetType;
        public FieldInfo supplierListField;
        public FieldInfo productListField;
        public FieldInfo ingredientListField;
        public string catalogAssetPath;
        public ScriptableObject catalogAsset;

        public bool IngredientsStoredInCatalogAsset => ingredientListField != null;

        public bool IsComplete =>
            serviceType != null &&
            supplierType != null &&
            productType != null &&
            ingredientDescriptorType != null &&
            catalogAssetType != null &&
            supplierListField != null &&
            productListField != null &&
            catalogAsset != null &&
            !string.IsNullOrWhiteSpace(catalogAssetPath);
    }

    internal sealed class Projection
    {
        public IList suppliers;
        public IList products;
        public IList ingredients;
        public HashSet<string> supplierIds = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> ingredientIds = new HashSet<string>(StringComparer.Ordinal);
        public Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord> offerByProductId =
            new Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord>(StringComparer.Ordinal);
    }

    internal sealed class PublicationResult
    {
        public bool success;
        public bool changed;
        public string message;
        public int suppliers;
        public int products;
        public int ingredients;
        public string assetPath;
    }

    internal static bool TryDiscoverContract(out Contract contract, out string error)
    {
        contract = new Contract();
        error = string.Empty;

        Type serviceType = FindType("BistroBuilderSupplierCatalogService");
        if (serviceType == null)
        {
            error = "No se encontró BistroBuilderSupplierCatalogService.";
            return false;
        }

        contract.serviceType = serviceType;
        contract.applyCandidateMethod = FindCandidateMethod(serviceType);
        if (contract.applyCandidateMethod == null)
        {
            error = "El SupplierCatalogService no expone el contrato de candidato de 2.3A1.";
            return false;
        }

        ParameterInfo[] parameters = contract.applyCandidateMethod.GetParameters();
        if (parameters.Length < 3)
        {
            error = "El contrato de candidato de SupplierCatalogService no contiene las tres colecciones esperadas.";
            return false;
        }

        contract.supplierType = GetEnumerableElementType(parameters[0].ParameterType);
        contract.productType = GetEnumerableElementType(parameters[1].ParameterType);
        contract.ingredientDescriptorType = GetEnumerableElementType(parameters[2].ParameterType);

        if (contract.supplierType == null || contract.productType == null || contract.ingredientDescriptorType == null)
        {
            error = "No se pudieron resolver los tipos runtime de proveedores, productos e ingredientes.";
            return false;
        }

        // 2.3B3B confirmó el contrato real: el recurso canónico es
        // BistroBuilderSupplierCatalogSettings. El asset serializa proveedores y
        // productos; los descriptores de ingrediente pertenecen al catálogo
        // canónico de recetas y se reconstruyen en runtime. Por tanto NO exigimos
        // una tercera lista serializada dentro de supplier.catalog.
        contract.catalogAssetType = FindType("BistroBuilderSupplierCatalogSettings");
        if (contract.catalogAssetType == null || !typeof(ScriptableObject).IsAssignableFrom(contract.catalogAssetType))
        {
            contract.catalogAssetType = FindCatalogAssetType(serviceType);
        }

        if (contract.catalogAssetType == null)
        {
            error = "No se pudo resolver BistroBuilderSupplierCatalogSettings, tipo real de supplier.catalog.";
            return false;
        }

        contract.supplierListField = FindCollectionField(contract.catalogAssetType, contract.supplierType, "suppliers");
        contract.productListField = FindCollectionField(contract.catalogAssetType, contract.productType, "products");
        contract.ingredientListField = FindCollectionField(contract.catalogAssetType, contract.ingredientDescriptorType, "ingredients");

        if (contract.supplierListField == null || contract.productListField == null)
        {
            error =
                "BistroBuilderSupplierCatalogSettings no expone las colecciones serializadas suppliers/products " +
                "del contrato runtime real.";
            return false;
        }

        if (!TryLocateCanonicalAsset(contract.catalogAssetType, out ScriptableObject asset, out string path))
        {
            error = "No se encontró el asset supplier.catalog canónico dentro de Assets/Resources.";
            return false;
        }

        contract.catalogAsset = asset;
        contract.catalogAssetPath = path;

        return true;
    }

    internal static bool TryBuildProjection(
        Contract contract,
        out Projection projection,
        out string error)
    {
        projection = null;
        error = string.Empty;

        if (contract == null || !contract.IsComplete)
        {
            error = "El contrato canónico no está completo.";
            return false;
        }

        BistroBuilderSupplierAuthoringDatabase supplierDb =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredientDb =
            BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        if (supplierDb == null || ingredientDb == null)
        {
            error = "No están disponibles supplier.authoring e ingredient.authoring.";
            return false;
        }

        Projection built = new Projection
        {
            suppliers = CreateGenericList(contract.supplierType),
            products = CreateGenericList(contract.productType),
            // supplier.catalog no serializa descriptores de ingrediente.
            // Conservamos aquí solo las identidades canónicas como referencias
            // de proyección para FK/autotest; los descriptores reales los
            // reconstruye BistroBuilderSupplierCatalogService en runtime.
            ingredients = new List<string>()
        };

        Dictionary<string, BistroBuilderIngredientAuthoringRecord> ingredientsById =
            ingredientDb.Ingredients
                .Where(item => item != null && item.isActive && !string.IsNullOrWhiteSpace(item.IngredientId))
                .ToDictionary(item => item.IngredientId, item => item, StringComparer.Ordinal);

        Dictionary<string, BistroBuilderCommercialPackageAuthoringRecord> packagesById =
            new Dictionary<string, BistroBuilderCommercialPackageAuthoringRecord>(StringComparer.Ordinal);

        foreach (BistroBuilderIngredientAuthoringRecord ingredient in ingredientsById.Values)
        {
            if (ingredient.commercialPackages == null)
            {
                continue;
            }

            for (int index = 0; index < ingredient.commercialPackages.Count; index++)
            {
                BistroBuilderCommercialPackageAuthoringRecord package = ingredient.commercialPackages[index];
                if (package != null && package.isActive && !string.IsNullOrWhiteSpace(package.PackageFormatId))
                {
                    packagesById[package.PackageFormatId] = package;
                }
            }
        }

        // Ingredientes: 2.3B3B confirmó que supplier.catalog NO serializa
        // BistroBuilderSupplierIngredientDescriptor. No intentamos construir
        // aquí objetos runtime inmutables/privados que pertenecen al dominio
        // de RecipeCatalog. La proyección conserva exclusivamente los
        // IngredientId canónicos; TryRebuildCatalog reconstruirá sus
        // descriptores reales desde la autoridad de ingredientes en Play Mode.
        foreach (BistroBuilderIngredientAuthoringRecord ingredient in
                 ingredientsById.Values.OrderBy(x => x.IngredientId))
        {
            string ingredientId = NormalizeId(ingredient.IngredientId);
            if (string.IsNullOrWhiteSpace(ingredientId))
            {
                error = "ingredient.authoring contiene un IngredientId vacío.";
                return false;
            }

            built.ingredients.Add(ingredient.IngredientId);
            built.ingredientIds.Add(ingredientId);
        }

        // Tomamos prototipos del supplier.catalog ya validado en 2.3A1. Esto
        // permite crear nuevas instancias incluso si los modelos runtime son
        // inmutables o no exponen constructor vacío. El clon se sobrescribe
        // después campo a campo con la autoría 2.3B.
        IList persistedSupplierTemplates = GetOrCreateCollection(contract.catalogAsset, contract.supplierListField);
        IList persistedProductTemplates = GetOrCreateCollection(contract.catalogAsset, contract.productListField);
        object supplierPrototype = FirstNonNull(persistedSupplierTemplates);
        object productPrototype = FirstNonNull(persistedProductTemplates);

        foreach (BistroBuilderSupplierAuthoringRecord supplier in
                 supplierDb.Suppliers.Where(x => x != null && x.isActive).OrderBy(x => x.SupplierId))
        {
            object runtimeSupplier = CreateInstance(contract.supplierType, supplierPrototype);
            if (runtimeSupplier == null)
            {
                error = "No se pudo crear " + contract.supplierType.Name + ".";
                return false;
            }

            if (!PopulateSupplier(runtimeSupplier, supplier, out string supplierError))
            {
                error = supplier.SupplierId + ": " + supplierError;
                return false;
            }

            built.suppliers.Add(runtimeSupplier);
            if (!built.supplierIds.Add(supplier.SupplierId))
            {
                error = "SupplierId duplicado durante la proyección: " + supplier.SupplierId + ".";
                return false;
            }

            if (supplier.baseOffers == null)
            {
                continue;
            }

            foreach (BistroBuilderSupplierBaseOfferAuthoringRecord offer in
                     supplier.baseOffers.Where(x => x != null && x.isActive).OrderBy(x => x.sortOrder).ThenBy(x => x.SupplierOfferId))
            {
                if (!ingredientsById.TryGetValue(offer.ingredientId ?? string.Empty, out BistroBuilderIngredientAuthoringRecord ingredient))
                {
                    error = "La oferta " + offer.SupplierOfferId + " referencia un ingrediente inexistente.";
                    return false;
                }

                if (!packagesById.TryGetValue(offer.packageFormatId ?? string.Empty, out BistroBuilderCommercialPackageAuthoringRecord package))
                {
                    error = "La oferta " + offer.SupplierOfferId + " referencia un formato inexistente.";
                    return false;
                }

                object runtimeProduct = CreateInstance(contract.productType, productPrototype);
                if (runtimeProduct == null)
                {
                    error = "No se pudo crear " + contract.productType.Name + ".";
                    return false;
                }

                if (!PopulateProduct(runtimeProduct, supplier, ingredient, package, offer, out string productError))
                {
                    error = offer.SupplierOfferId + ": " + productError;
                    return false;
                }

                built.products.Add(runtimeProduct);
                string productId = offer.SupplierOfferId;
                if (!built.productIds.Add(productId))
                {
                    error = "ProductId/SupplierOfferId duplicado: " + productId + ".";
                    return false;
                }

                built.offerByProductId[productId] = offer;
            }
        }

        if (built.suppliers.Count != ExpectedSupplierCount)
        {
            error = "La proyección contiene " + built.suppliers.Count + " proveedores; se esperaban " + ExpectedSupplierCount + ".";
            return false;
        }

        if (built.products.Count != ExpectedOfferCount)
        {
            error = "La proyección contiene " + built.products.Count + " productos; se esperaban " + ExpectedOfferCount + ".";
            return false;
        }

        if (built.ingredients.Count < ExpectedIngredientCount)
        {
            error = "La proyección referencia solo " + built.ingredients.Count + " ingredientes canónicos.";
            return false;
        }

        // Integridad FK antes de tocar el asset canónico.
        foreach (object product in built.products)
        {
            string supplierId = NormalizeId(ReadString(product, "SupplierId", "supplierId"));
            string ingredientId = NormalizeId(ReadString(product, "IngredientId", "ingredientId"));

            if (!built.supplierIds.Contains(supplierId))
            {
                error = "Producto proyectado con SupplierId inexistente: " + supplierId + ".";
                return false;
            }

            if (!built.ingredientIds.Contains(ingredientId))
            {
                error = "Producto proyectado con IngredientId inexistente: " + ingredientId + ".";
                return false;
            }
        }

        projection = built;
        return true;
    }

    internal static bool TryValidateWithExistingDomain(
        Contract contract,
        Projection projection,
        out bool validatorFound,
        out string error)
    {
        validatorFound = false;
        error = string.Empty;

        if (contract == null || projection == null)
        {
            error = "Contrato/proyección no disponibles para validar.";
            return false;
        }

        // En el contrato real de Bistro Builder los ingredientes NO viven en
        // supplier.catalog. Cualquier validador que exija una lista de
        // BistroBuilderSupplierIngredientDescriptor requiere objetos que solo
        // RecipeCatalogService debe construir. Forzarlos desde Editor fue la
        // causa de B3C. Por tanto, en este storage la validación previa es la
        // integridad estructural/FK de 2.3B3; la validación de dominio real se
        // ejecuta después mediante TryRebuildCatalog en Play Mode.
        if (!contract.IngredientsStoredInCatalogAsset)
        {
            validatorFound = false;
            return true;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { types = exception.Types.Where(x => x != null).ToArray(); }

            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                Type type = types[typeIndex];
                if (type == null ||
                    type.Name.IndexOf("Supplier", StringComparison.OrdinalIgnoreCase) < 0 ||
                    type.Name.IndexOf("Catalog", StringComparison.OrdinalIgnoreCase) < 0 ||
                    (type.Name.IndexOf("Valid", StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                {
                    MethodInfo method = methods[methodIndex];
                    if (method.ReturnType != typeof(bool) ||
                        method.Name.IndexOf("Valid", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length < 3)
                    {
                        continue;
                    }

                    Type p0 = GetEnumerableElementType(parameters[0].ParameterType);
                    Type p1 = GetEnumerableElementType(parameters[1].ParameterType);
                    Type p2 = GetEnumerableElementType(parameters[2].ParameterType);
                    if (p0 != contract.supplierType || p1 != contract.productType || p2 != contract.ingredientDescriptorType)
                    {
                        continue;
                    }

                    object[] args = new object[parameters.Length];
                    args[0] = projection.suppliers;
                    args[1] = projection.products;
                    args[2] = projection.ingredients;
                    for (int index = 3; index < parameters.Length; index++)
                    {
                        Type parameterType = parameters[index].ParameterType;
                        Type effective = parameterType.IsByRef ? parameterType.GetElementType() : parameterType;
                        args[index] = effective != null && effective.IsValueType
                            ? Activator.CreateInstance(effective)
                            : null;
                    }

                    validatorFound = true;
                    try
                    {
                        bool valid = (bool)method.Invoke(null, args);
                        for (int index = 3; index < parameters.Length; index++)
                        {
                            if (parameters[index].ParameterType.IsByRef && args[index] is string text && !string.IsNullOrWhiteSpace(text))
                            {
                                error = text;
                            }
                        }

                        if (!valid && string.IsNullOrWhiteSpace(error))
                        {
                            error = type.Name + "." + method.Name + " rechazó la proyección.";
                        }

                        return valid;
                    }
                    catch (Exception exception)
                    {
                        error = "El validador existente lanzó " + exception.GetBaseException().Message;
                        return false;
                    }
                }
            }
        }

        return true;
    }

    internal static PublicationResult Publish()
    {
        PublicationResult result = new PublicationResult();

        if (!TryDiscoverContract(out Contract contract, out string contractError))
        {
            result.message = contractError;
            return result;
        }

        if (!TryBuildProjection(contract, out Projection projection, out string projectionError))
        {
            result.message = projectionError;
            return result;
        }

        if (!TryValidateWithExistingDomain(contract, projection, out bool validatorFound, out string domainError))
        {
            result.message =
                "La proyección fue rechazada antes de tocar supplier.catalog" +
                (validatorFound ? " por el validador de dominio existente: " : ": ") +
                domainError;
            return result;
        }

        result.assetPath = contract.catalogAssetPath;
        result.suppliers = projection.suppliers.Count;
        result.products = projection.products.Count;
        result.ingredients = projection.ingredients.Count;

        string beforeFingerprint = BuildFingerprint(contract.catalogAsset, contract);
        string backupJson = EditorJsonUtility.ToJson(contract.catalogAsset, true);

        try
        {
            Undo.RecordObject(contract.catalogAsset, "2.3B3 publicar supplier.catalog");

            ReplaceCollection(contract.catalogAsset, contract.supplierListField, projection.suppliers);
            ReplaceCollection(contract.catalogAsset, contract.productListField, projection.products);
            if (contract.ingredientListField != null)
            {
                ReplaceCollection(contract.catalogAsset, contract.ingredientListField, projection.ingredients);
            }

            SetOptional(contract.catalogAsset, "supplier.catalog", "SchemaId", "schemaId");
            SetOptional(contract.catalogAsset, 2, "SchemaVersion", "schemaVersion");

            EditorUtility.SetDirty(contract.catalogAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(contract.catalogAssetPath, ImportAssetOptions.ForceUpdate);

            ScriptableObject reloaded =
                AssetDatabase.LoadAssetAtPath(contract.catalogAssetPath, contract.catalogAssetType) as ScriptableObject;

            if (reloaded == null)
            {
                throw new InvalidOperationException("No se pudo recargar supplier.catalog después de guardarlo.");
            }

            contract.catalogAsset = reloaded;

            if (!ValidatePersistedProjection(contract, projection, out string validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            string afterFingerprint = BuildFingerprint(reloaded, contract);
            result.changed = !string.Equals(beforeFingerprint, afterFingerprint, StringComparison.Ordinal);
            result.success = true;
            result.message =
                "supplier.catalog publicado correctamente: " +
                result.suppliers + " proveedores, " +
                result.products + " productos y " +
                result.ingredients + " ingredientes canónicos referenciados. " +
                (result.changed ? "El asset canónico cambió." : "El contenido ya era idéntico (idempotente).") +
                " Ruta: " + result.assetPath + ".";

            return result;
        }
        catch (Exception exception)
        {
            try
            {
                ScriptableObject target =
                    AssetDatabase.LoadAssetAtPath(contract.catalogAssetPath, contract.catalogAssetType) as ScriptableObject;

                if (target != null && !string.IsNullOrWhiteSpace(backupJson))
                {
                    EditorJsonUtility.FromJsonOverwrite(backupJson, target);
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(contract.catalogAssetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            catch (Exception rollbackException)
            {
                result.message =
                    "La publicación falló y también falló el rollback. Publicación: " +
                    exception.Message + " | Rollback: " + rollbackException.Message;
                return result;
            }

            result.message = "Publicación cancelada y supplier.catalog restaurado: " + exception.Message;
            return result;
        }
    }

    internal static bool ValidatePersistedProjection(Contract contract, Projection projection, out string error)
    {
        error = string.Empty;
        if (contract == null || contract.catalogAsset == null || projection == null)
        {
            error = "No hay contrato/proyección para validar.";
            return false;
        }

        IList suppliers = GetOrCreateCollection(contract.catalogAsset, contract.supplierListField);
        IList products = GetOrCreateCollection(contract.catalogAsset, contract.productListField);
        IList ingredients = contract.ingredientListField != null
            ? GetOrCreateCollection(contract.catalogAsset, contract.ingredientListField)
            : null;

        if (suppliers == null || products == null)
        {
            error = "supplier.catalog no conserva sus colecciones suppliers/products después de la publicación.";
            return false;
        }

        if (suppliers.Count != projection.suppliers.Count || products.Count != projection.products.Count)
        {
            error =
                "Cardinalidad persistida inesperada. Supplier.catalog=" +
                suppliers.Count + "/" + products.Count +
                ", proyección=" + projection.suppliers.Count + "/" + projection.products.Count + ".";
            return false;
        }

        if (ingredients != null && ingredients.Count != projection.ingredients.Count)
        {
            error =
                "La versión del asset que serializa ingredientes conserva una cardinalidad inesperada: " +
                ingredients.Count + " frente a " + projection.ingredients.Count + ".";
            return false;
        }

        HashSet<string> supplierIds = ExtractIds(suppliers, "SupplierId", "supplierId", "Id", "id");
        HashSet<string> productIds = ExtractIds(products, "ProductId", "productId", "SupplierOfferId", "supplierOfferId", "Id", "id");

        if (!supplierIds.SetEquals(projection.supplierIds))
        {
            error = "Los SupplierId persistidos no coinciden con supplier.authoring.";
            return false;
        }

        if (!productIds.SetEquals(projection.productIds))
        {
            error = "Los ProductId persistidos no coinciden con las 66 ofertas base.";
            return false;
        }

        if (!CompareCollectionsById(
                suppliers, projection.suppliers,
                new[] { "SupplierId", "supplierId", "Id", "id" },
                "proveedor", out string supplierDifference))
        {
            error = supplierDifference;
            return false;
        }

        if (!CompareCollectionsById(
                products, projection.products,
                new[] { "ProductId", "productId", "SupplierOfferId", "supplierOfferId", "Id", "id" },
                "producto/oferta", out string productDifference))
        {
            error = productDifference;
            return false;
        }

        return true;
    }

    private static bool CompareCollectionsById(
        IList persisted,
        IList projected,
        string[] idMembers,
        string label,
        out string error)
    {
        error = string.Empty;

        Dictionary<string, object> projectedById = new Dictionary<string, object>(StringComparer.Ordinal);
        for (int index = 0; index < projected.Count; index++)
        {
            object item = projected[index];
            string id = NormalizeId(ReadString(item, idMembers));
            if (!string.IsNullOrWhiteSpace(id))
            {
                projectedById[id] = item;
            }
        }

        for (int index = 0; index < persisted.Count; index++)
        {
            object current = persisted[index];
            string id = NormalizeId(ReadString(current, idMembers));
            if (string.IsNullOrWhiteSpace(id) || !projectedById.TryGetValue(id, out object expected))
            {
                error = "No se pudo emparejar " + label + " persistido: " + id + ".";
                return false;
            }

            string currentJson = JsonUtility.ToJson(current, false);
            string expectedJson = JsonUtility.ToJson(expected, false);

            // Los modelos de supplier.catalog son serializables en el asset.
            // Comparar su representación serializada garantiza que no solo
            // coincidan los IDs: también precio, cantidad, mínimos, plazos,
            // flags y cualquier otro campo persistente del contrato vigente.
            if (!string.Equals(currentJson, expectedJson, StringComparison.Ordinal))
            {
                error = "El " + label + " " + id + " difiere entre autoría proyectada y supplier.catalog persistido.";
                return false;
            }
        }

        return true;
    }

    internal static string BuildFingerprint(ScriptableObject asset, Contract contract)
    {
        if (asset == null || contract == null)
        {
            return string.Empty;
        }

        // El JSON de Editor recoge todos los campos serializados relevantes
        // (incluidos precios y cantidades), no solo las identidades. Esto hace
        // que una republicación con el mismo ProductId pero precio distinto no
        // pueda confundirse con una operación idempotente.
        string serialized = EditorJsonUtility.ToJson(asset, false);

        IList suppliers = GetOrCreateCollection(asset, contract.supplierListField);
        IList products = GetOrCreateCollection(asset, contract.productListField);
        IList ingredients = contract.ingredientListField != null
            ? GetOrCreateCollection(asset, contract.ingredientListField)
            : null;

        List<string> supplierIds = ExtractIds(suppliers, "SupplierId", "supplierId", "Id", "id").OrderBy(x => x).ToList();
        List<string> productIds = ExtractIds(products, "ProductId", "productId", "SupplierOfferId", "supplierOfferId", "Id", "id").OrderBy(x => x).ToList();
        List<string> ingredientIds = ExtractIds(ingredients, "IngredientId", "ingredientId", "Id", "id").OrderBy(x => x).ToList();

        return serialized + "\n#SUPPLIERS=" + string.Join("|", supplierIds) +
               "\n#PRODUCTS=" + string.Join("|", productIds) +
               "\n#INGREDIENTS_STORAGE=" +
               (contract.ingredientListField != null ? string.Join("|", ingredientIds) : "EXTERNAL_RECIPE_CATALOG");
    }

    internal static string DescribeContract(Contract contract)
    {
        if (contract == null)
        {
            return "Contrato no disponible.";
        }

        return
            "Service: " + (contract.serviceType != null ? contract.serviceType.FullName : "-") + "\n" +
            "Supplier: " + (contract.supplierType != null ? contract.supplierType.FullName : "-") + "\n" +
            "Product: " + (contract.productType != null ? contract.productType.FullName : "-") + "\n" +
            "Ingredient descriptor: " + (contract.ingredientDescriptorType != null ? contract.ingredientDescriptorType.FullName : "-") + "\n" +
            "Asset: " + (contract.catalogAssetType != null ? contract.catalogAssetType.FullName : "-") + "\n" +
            "Ruta: " + (contract.catalogAssetPath ?? "-") + "\n" +
            "Suppliers field: " + (contract.supplierListField != null ? contract.supplierListField.Name : "-") + "\n" +
            "Products field: " + (contract.productListField != null ? contract.productListField.Name : "-") + "\n" +
            "Ingredients field: " + (contract.ingredientListField != null
                ? contract.ingredientListField.Name
                : "- (externos; reconstruidos por BistroBuilderSupplierCatalogService desde el catálogo canónico de ingredientes)");
    }

    private static bool PopulateSupplier(
        object target,
        BistroBuilderSupplierAuthoringRecord source,
        out string error)
    {
        error = string.Empty;

        if (!SetRequired(target, source.SupplierId, "SupplierId", "supplierId", "Id", "id"))
        {
            error = "el contrato runtime no permite asignar SupplierId.";
            return false;
        }

        if (!SetRequired(target, source.displayName, "DisplayName", "displayName", "Name", "name"))
        {
            error = "el contrato runtime no permite asignar DisplayName.";
            return false;
        }

        SetOptional(target, source.shortName, "ShortName", "shortName");
        SetOptional(target, source.description, "Description", "description");
        SetOptional(target, "EUR", "CurrencyCode", "currencyCode", "Currency", "currency");
        SetOptional(target, source.minimumOrderValueCents,
            "MinimumOrderValueCents", "minimumOrderValueCents", "MinimumOrderCents", "minimumOrderCents");
        SetOptional(target, source.shippingCostCents,
            "ShippingCostCents", "shippingCostCents", "DeliveryCostCents", "deliveryCostCents");
        SetOptional(target, source.freeShippingEnabled,
            "FreeShippingEnabled", "freeShippingEnabled");
        SetOptional(target, source.freeShippingThresholdCents,
            "FreeShippingThresholdCents", "freeShippingThresholdCents");
        SetDuration(target, source.defaultLeadTimeGameHours,
            new[] { "DefaultLeadTimeGameHours", "defaultLeadTimeGameHours", "LeadTimeGameHours", "leadTimeGameHours", "LeadTimeHours", "leadTimeHours" },
            new[] { "DefaultLeadTimeMinutes", "defaultLeadTimeMinutes", "LeadTimeMinutes", "leadTimeMinutes" });
        SetOptional(target, source.reliabilityValue,
            "ReliabilityValue", "reliabilityValue", "Reliability", "reliability");
        SetOptionalEnumOrString(target, source.reliabilityTier.ToString(),
            "ReliabilityTier", "reliabilityTier");
        SetOptional(target, source.isActive,
            "IsActive", "isActive", "Enabled", "enabled");

        string[] classification = ResolveSupplierClassificationNames(source);
        SetOptionalEnumFromCandidates(target, classification,
            "Classification", "classification", "SupplierType", "supplierType", "CommercialModel", "commercialModel");

        return true;
    }

    private static bool PopulateProduct(
        object target,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderIngredientAuthoringRecord ingredient,
        BistroBuilderCommercialPackageAuthoringRecord package,
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        out string error)
    {
        error = string.Empty;

        if (!SetRequired(target, offer.SupplierOfferId,
                "ProductId", "productId", "SupplierOfferId", "supplierOfferId", "Id", "id"))
        {
            error = "el contrato runtime no permite asignar ProductId.";
            return false;
        }

        if (!SetRequired(target, supplier.SupplierId, "SupplierId", "supplierId"))
        {
            error = "el contrato runtime no permite asignar SupplierId.";
            return false;
        }

        if (!SetRequired(target, ingredient.IngredientId, "IngredientId", "ingredientId"))
        {
            error = "el contrato runtime no permite asignar IngredientId.";
            return false;
        }

        SetOptional(target, package.displayName,
            "DisplayName", "displayName", "ProductName", "productName", "PackageDisplayName", "packageDisplayName");
        SetOptional(target, package.PackageFormatId,
            "PackageFormatId", "packageFormatId", "FormatId", "formatId");
        SetOptionalEnumOrString(target, ingredient.canonicalUnitSnapshot,
            "Unit", "unit", "BaseUnit", "baseUnit", "IngredientUnit", "ingredientUnit");

        bool quantitySet =
            SetOptional(target, package.netQuantityMicrounits,
                "QuantityMicrounits", "quantityMicrounits", "NetQuantityMicrounits", "netQuantityMicrounits", "PackageQuantityMicrounits", "packageQuantityMicrounits", "QuantityPerPackageMicrounits", "quantityPerPackageMicrounits");

        if (!quantitySet)
        {
            SetOptional(target, package.NetQuantityInBaseUnits,
                "Quantity", "quantity", "NetQuantity", "netQuantity", "PackageQuantity", "packageQuantity", "QuantityPerPackage", "quantityPerPackage");
        }

        // 2.3B3E: el contrato 2.3A1 ya demostró mediante round-trip JSON
        // que el SKU runtime persiste el precio en céntimos. No exigimos un
        // nombre concreto del miembro: primero probamos los alias conocidos y
        // después resolvemos semánticamente el miembro monetario real.
        if (!TrySetPriceCents(target, offer.basePriceCents, out string priceMember))
        {
            error =
                "el contrato runtime persiste céntimos, pero no se pudo resolver " +
                "de forma inequívoca el miembro monetario de " + target.GetType().Name +
                ". Miembros numéricos detectados: " + DescribeNumericMembers(target) + ".";
            return false;
        }

        SetOptional(target, offer.minimumPackageCount,
            "MinimumOrderQuantity", "minimumOrderQuantity", "MinimumPackageCount", "minimumPackageCount", "MinimumQuantity", "minimumQuantity", "MinimumOrderPackages", "minimumOrderPackages");
        SetOptional(target, offer.orderIncrement,
            "OrderIncrement", "orderIncrement", "QuantityIncrement", "quantityIncrement");

        float leadTime = offer.overrideLeadTime
            ? offer.leadTimeOverrideGameHours
            : supplier.defaultLeadTimeGameHours;

        SetDuration(target, leadTime,
            new[] { "LeadTimeGameHours", "leadTimeGameHours", "LeadTimeHours", "leadTimeHours" },
            new[] { "LeadTimeMinutes", "leadTimeMinutes" });

        bool available = offer.initialAvailability != BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado;
        SetOptional(target, available,
            "IsAvailable", "isAvailable", "Available", "available", "Purchasable", "purchasable");
        SetOptionalEnumOrString(target, offer.initialAvailability.ToString(),
            "Availability", "availability", "AvailabilityState", "availabilityState");
        SetOptional(target, offer.promotionEligible,
            "PromotionEligible", "promotionEligible");
        SetOptional(target, offer.minimumMarketVariationPercent,
            "MinimumMarketVariationPercent", "minimumMarketVariationPercent");
        SetOptional(target, offer.maximumMarketVariationPercent,
            "MaximumMarketVariationPercent", "maximumMarketVariationPercent");
        SetOptional(target, offer.isActive,
            "IsActive", "isActive", "Enabled", "enabled");
        SetOptional(target, "EUR", "CurrencyCode", "currencyCode", "Currency", "currency");

        return true;
    }

    private static string[] ResolveSupplierClassificationNames(BistroBuilderSupplierAuthoringRecord source)
    {
        if ((source.commercialModelFlags & BistroBuilderSupplierCommercialModelFlags.Express) != 0)
        {
            return new[] { "Express", "Urgent", "Urgente" };
        }

        if ((source.commercialModelFlags & BistroBuilderSupplierCommercialModelFlags.Mayorista) != 0)
        {
            return new[] { "Wholesale", "Wholesaler", "Mayorista", "Economic", "Economico" };
        }

        if ((source.commercialModelFlags & BistroBuilderSupplierCommercialModelFlags.ProductorLocal) != 0)
        {
            return new[] { "LocalProducer", "Producer", "ProductorLocal", "Local" };
        }

        if ((source.commercialModelFlags & BistroBuilderSupplierCommercialModelFlags.Especialista) != 0)
        {
            return new[] { "Specialist", "Especialista", "Premium" };
        }

        return new[] { "Generalist", "Generalista", "Balanced", "Equilibrado" };
    }

    private static MethodInfo FindCandidateMethod(Type serviceType)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo exact = serviceType.GetMethod("TryApplyCatalogCandidateForEditorTests", flags);
        if (exact != null)
        {
            return exact;
        }

        MethodInfo[] methods = serviceType.GetMethods(flags);
        for (int index = 0; index < methods.Length; index++)
        {
            MethodInfo method = methods[index];
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name.IndexOf("Apply", StringComparison.OrdinalIgnoreCase) >= 0 &&
                method.Name.IndexOf("Candidate", StringComparison.OrdinalIgnoreCase) >= 0 &&
                parameters.Length >= 3 &&
                GetEnumerableElementType(parameters[0].ParameterType) != null &&
                GetEnumerableElementType(parameters[1].ParameterType) != null &&
                GetEnumerableElementType(parameters[2].ParameterType) != null)
            {
                return method;
            }
        }

        return null;
    }

    private static Type FindCatalogAssetType(Type serviceType)
    {
        Type exact = FindType("BistroBuilderSupplierCatalogSettings");
        if (exact != null && typeof(ScriptableObject).IsAssignableFrom(exact))
        {
            return exact;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (FieldInfo field in serviceType.GetFields(flags))
        {
            Type type = field.FieldType;
            if (typeof(ScriptableObject).IsAssignableFrom(type) &&
                type.Name.IndexOf("Supplier", StringComparison.OrdinalIgnoreCase) >= 0 &&
                type.Name.IndexOf("Catalog", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return type;
            }
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(x => x != null).ToArray();
            }

            for (int index = 0; index < types.Length; index++)
            {
                Type type = types[index];
                if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type))
                {
                    continue;
                }

                if (type.Name.IndexOf("Supplier", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    type.Name.IndexOf("Catalog", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (type.Name.IndexOf("Settings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.Name.IndexOf("Asset", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static bool TryLocateCanonicalAsset(Type assetType, out ScriptableObject asset, out string path)
    {
        asset = null;
        path = string.Empty;

        // Ruta real confirmada por 2.3B3B. Se intenta primero para evitar
        // cualquier ambigüedad con assets de autoría que también viven en Resources.
        const string ExactPath =
            "Assets/Resources/BistroBuilder/Suppliers/BistroBuilderSupplierCatalogSettings.asset";

        ScriptableObject exact = AssetDatabase.LoadAssetAtPath(ExactPath, assetType) as ScriptableObject;
        if (exact != null)
        {
            string exactSchema = ReadString(exact, "SchemaId", "schemaId");
            if (string.IsNullOrWhiteSpace(exactSchema) ||
                string.Equals(exactSchema.Trim(), "supplier.catalog", StringComparison.OrdinalIgnoreCase))
            {
                asset = exact;
                path = ExactPath;
                return true;
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:" + assetType.Name, new[] { "Assets/Resources" });
        for (int index = 0; index < guids.Length; index++)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(guids[index]);
            ScriptableObject candidate = AssetDatabase.LoadAssetAtPath(candidatePath, assetType) as ScriptableObject;
            if (candidate == null)
            {
                continue;
            }

            string schema = ReadString(candidate, "SchemaId", "schemaId");
            if (string.IsNullOrWhiteSpace(schema) ||
                string.Equals(schema.Trim(), "supplier.catalog", StringComparison.OrdinalIgnoreCase))
            {
                asset = candidate;
                path = candidatePath;
                return true;
            }
        }

        return false;
    }

    private static FieldInfo FindCollectionField(Type ownerType, Type elementType, params string[] preferredNames)
    {
        if (ownerType == null || elementType == null)
        {
            return null;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo[] fields = ownerType.GetFields(flags);

        if (preferredNames != null)
        {
            for (int nameIndex = 0; nameIndex < preferredNames.Length; nameIndex++)
            {
                string preferred = preferredNames[nameIndex];
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    if (!string.Equals(field.Name, preferred, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (GetEnumerableElementType(field.FieldType) == elementType && typeof(IList).IsAssignableFrom(field.FieldType))
                    {
                        return field;
                    }
                }
            }
        }

        // Fallback estructural: solo colecciones IList mutables. Esto excluye
        // suppliersView/productsView (ReadOnlyCollection) detectados por 2.3B3B.
        for (int index = 0; index < fields.Length; index++)
        {
            FieldInfo field = fields[index];
            Type found = GetEnumerableElementType(field.FieldType);
            if (found == elementType && typeof(IList).IsAssignableFrom(field.FieldType))
            {
                return field;
            }
        }

        return null;
    }

    private static Type GetEnumerableElementType(Type type)
    {
        if (type == null)
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType)
        {
            Type[] args = type.GetGenericArguments();
            if (args.Length == 1 &&
                (typeof(IEnumerable).IsAssignableFrom(type) ||
                 type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))))
            {
                return args[0];
            }
        }

        Type enumerable = type.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable != null ? enumerable.GetGenericArguments()[0] : null;
    }

    private static IList CreateGenericList(Type elementType)
    {
        Type listType = typeof(List<>).MakeGenericType(elementType);
        return (IList)Activator.CreateInstance(listType);
    }

    private static object CreateInstance(Type type, object prototype = null)
    {
        if (type == null)
        {
            return null;
        }

        // 1) Constructor vacío, si el modelo lo expone.
        try
        {
            return Activator.CreateInstance(type, true);
        }
        catch
        {
            // Continuamos.
        }

        // 2) El supplier.catalog existente ya contiene instancias válidas de
        // estos modelos. Intentamos primero una clonación por el serializador
        // de Unity: conserva campos serializados sin compartir colecciones.
        if (prototype != null && type.IsInstanceOfType(prototype))
        {
            try
            {
                string json = JsonUtility.ToJson(prototype, false);
                object serializedClone = JsonUtility.FromJson(json, type);
                if (serializedClone != null)
                {
                    return serializedClone;
                }
            }
            catch
            {
                // Algunos POCO del dominio pueden no admitir FromJson(Type).
            }

            // 3) Fallback de clonación CLR. No ejecuta constructores y mantiene
            // la inicialización del prototipo validado. Populate* sobrescribe
            // después todos los campos relevantes de 2.3B.
            try
            {
                MethodInfo memberwiseClone = typeof(object).GetMethod(
                    "MemberwiseClone",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (memberwiseClone != null)
                {
                    object clone = memberwiseClone.Invoke(prototype, null);
                    if (clone != null)
                    {
                        return clone;
                    }
                }
            }
            catch
            {
                // Continuamos con el último fallback.
            }
        }

        // 4) Último recurso para POCO/Serializable sin ctor vacío. Nunca se usa
        // para UnityEngine.Object. Los campos requeridos se escriben y verifican
        // inmediatamente después por PopulateSupplier/PopulateProduct.
        if (!typeof(UnityEngine.Object).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
        {
            try
            {
                return FormatterServices.GetUninitializedObject(type);
            }
            catch
            {
                // No existe una forma segura de crear este modelo.
            }
        }

        return null;
    }

    private static object FirstNonNull(IList list)
    {
        if (list == null)
        {
            return null;
        }

        for (int index = 0; index < list.Count; index++)
        {
            if (list[index] != null)
            {
                return list[index];
            }
        }

        return null;
    }

    private static IList GetOrCreateCollection(object owner, FieldInfo field)
    {
        if (owner == null || field == null)
        {
            return null;
        }

        object value = field.GetValue(owner);
        if (value is IList list)
        {
            return list;
        }

        Type element = GetEnumerableElementType(field.FieldType);
        if (element == null)
        {
            return null;
        }

        IList created = CreateGenericList(element);
        try
        {
            field.SetValue(owner, created);
            return created;
        }
        catch
        {
            return null;
        }
    }

    private static void ReplaceCollection(object owner, FieldInfo field, IList source)
    {
        IList target = GetOrCreateCollection(owner, field);
        if (target == null)
        {
            throw new InvalidOperationException("No se puede escribir la colección " + field.Name + ".");
        }

        target.Clear();
        for (int index = 0; index < source.Count; index++)
        {
            target.Add(source[index]);
        }
    }

    internal static HashSet<string> ExtractIds(IList list, params string[] memberNames)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
        if (list == null)
        {
            return result;
        }

        for (int index = 0; index < list.Count; index++)
        {
            object item = list[index];
            string id = NormalizeId(ReadString(item, memberNames));
            if (!string.IsNullOrWhiteSpace(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    internal static string ReadString(object target, params string[] names)
    {
        object value = ReadMember(target, names);
        return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    internal static object ReadMember(object target, params string[] names)
    {
        if (target == null)
        {
            return null;
        }

        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int index = 0; index < names.Length; index++)
        {
            FieldInfo field = type.GetField(names[index], flags);
            if (field != null)
            {
                return field.GetValue(target);
            }

            PropertyInfo property = type.GetProperty(names[index], flags);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(target, null);
                }
                catch
                {
                    // Nunca abortamos por un getter de diagnóstico.
                }
            }
        }

        string[] normalizedNames = names.Select(NormalizeMemberName).ToArray();
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (normalizedNames.Contains(NormalizeMemberName(field.Name)))
            {
                try { return field.GetValue(target); } catch { }
            }
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (property.CanRead && property.GetIndexParameters().Length == 0 &&
                normalizedNames.Contains(NormalizeMemberName(property.Name)))
            {
                try { return property.GetValue(target, null); } catch { }
            }
        }

        return null;
    }

    /// <summary>
    /// Resuelve el campo monetario del SKU sin depender de un nombre privado
    /// concreto del contrato 2.3A1. El dominio ya garantiza que el valor se
    /// expresa/persiste en céntimos; esta función solo descubre dónde vive.
    /// </summary>
    private static bool TrySetPriceCents(object target, long cents, out string resolvedMember)
    {
        resolvedMember = string.Empty;

        if (target == null)
        {
            return false;
        }

        string[] aliases =
        {
            "PriceCents", "priceCents",
            "BasePriceCents", "basePriceCents",
            "UnitPriceCents", "unitPriceCents",
            "PackagePriceCents", "packagePriceCents",
            "PurchasePriceCents", "purchasePriceCents",
            "PriceMinorUnits", "priceMinorUnits",
            "BasePriceMinorUnits", "basePriceMinorUnits",
            "UnitPriceMinorUnits", "unitPriceMinorUnits",
            "PackagePriceMinorUnits", "packagePriceMinorUnits",
            "PurchasePriceMinorUnits", "purchasePriceMinorUnits",
            "PriceInCents", "priceInCents",
            "UnitPriceInCents", "unitPriceInCents",
            "CostCents", "costCents",
            "UnitCostCents", "unitCostCents",
            "PurchaseCostCents", "purchaseCostCents",
            "CostMinorUnits", "costMinorUnits",
            "UnitCostMinorUnits", "unitCostMinorUnits",
            "PurchaseCostMinorUnits", "purchaseCostMinorUnits"
        };

        if (TrySetNamedMemberWithResolvedName(target, cents, aliases, out resolvedMember))
        {
            return true;
        }

        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        List<FieldInfo> fieldCandidates = type.GetFields(flags)
            .Where(field => IsWritableNumericLike(field.FieldType) && ScorePriceMember(field.Name) > 0)
            .OrderByDescending(field => ScorePriceMember(field.Name))
            .ThenBy(field => field.Name, StringComparer.Ordinal)
            .ToList();

        for (int index = 0; index < fieldCandidates.Count; index++)
        {
            FieldInfo field = fieldCandidates[index];
            if (!TryConvertMoneyCents(cents, field.FieldType, out object converted))
            {
                continue;
            }

            try
            {
                field.SetValue(target, converted);
                resolvedMember = field.Name;
                return true;
            }
            catch
            {
                // Probamos el siguiente candidato semántico.
            }
        }

        List<PropertyInfo> propertyCandidates = type.GetProperties(flags)
            .Where(property =>
                property.CanWrite &&
                property.GetIndexParameters().Length == 0 &&
                IsWritableNumericLike(property.PropertyType) &&
                ScorePriceMember(property.Name) > 0)
            .OrderByDescending(property => ScorePriceMember(property.Name))
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .ToList();

        for (int index = 0; index < propertyCandidates.Count; index++)
        {
            PropertyInfo property = propertyCandidates[index];
            if (!TryConvertMoneyCents(cents, property.PropertyType, out object converted))
            {
                continue;
            }

            try
            {
                property.SetValue(target, converted, null);
                resolvedMember = property.Name;
                return true;
            }
            catch
            {
                // Probamos el siguiente candidato semántico.
            }
        }

        return false;
    }

    /// <summary>
    /// Lee el precio runtime usando exactamente la misma resolución adaptativa
    /// que B3E emplea al publicarlo. Esto evita que validadores/autotests
    /// dependan de un alias concreto del contrato 2.3A1.
    /// </summary>
    internal static bool TryReadPriceCents(object target, out long cents, out string resolvedMember)
    {
        cents = 0L;
        resolvedMember = string.Empty;
        if (target == null)
        {
            return false;
        }

        string[] aliases =
        {
            "PriceCents", "priceCents",
            "BasePriceCents", "basePriceCents",
            "UnitPriceCents", "unitPriceCents",
            "PackagePriceCents", "packagePriceCents",
            "PurchasePriceCents", "purchasePriceCents",
            "PriceMinorUnits", "priceMinorUnits",
            "BasePriceMinorUnits", "basePriceMinorUnits",
            "UnitPriceMinorUnits", "unitPriceMinorUnits",
            "PackagePriceMinorUnits", "packagePriceMinorUnits",
            "PurchasePriceMinorUnits", "purchasePriceMinorUnits",
            "PriceInCents", "priceInCents",
            "UnitPriceInCents", "unitPriceInCents",
            "CostCents", "costCents",
            "UnitCostCents", "unitCostCents",
            "PurchaseCostCents", "purchaseCostCents",
            "CostMinorUnits", "costMinorUnits",
            "UnitCostMinorUnits", "unitCostMinorUnits",
            "PurchaseCostMinorUnits", "purchaseCostMinorUnits"
        };

        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Primero los alias explícitos, exactamente igual que en escritura.
        for (int index = 0; index < aliases.Length; index++)
        {
            FieldInfo field = type.GetField(aliases[index], flags);
            if (field != null && TryExtractMoneyCents(field.GetValue(target), field.FieldType, out cents))
            {
                resolvedMember = field.Name;
                return true;
            }

            PropertyInfo property = type.GetProperty(aliases[index], flags);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    object value = property.GetValue(target, null);
                    if (TryExtractMoneyCents(value, property.PropertyType, out cents))
                    {
                        resolvedMember = property.Name;
                        return true;
                    }
                }
                catch
                {
                    // Continuamos con el siguiente candidato.
                }
            }
        }

        // Después resolvemos semánticamente el mismo miembro que el writer B3E.
        List<FieldInfo> fieldCandidates = type.GetFields(flags)
            .Where(field => IsWritableNumericLike(field.FieldType) && ScorePriceMember(field.Name) > 0)
            .OrderByDescending(field => ScorePriceMember(field.Name))
            .ThenBy(field => field.Name, StringComparer.Ordinal)
            .ToList();

        for (int index = 0; index < fieldCandidates.Count; index++)
        {
            FieldInfo field = fieldCandidates[index];
            if (TryExtractMoneyCents(field.GetValue(target), field.FieldType, out cents))
            {
                resolvedMember = field.Name;
                return true;
            }
        }

        List<PropertyInfo> propertyCandidates = type.GetProperties(flags)
            .Where(property =>
                property.CanRead &&
                property.GetIndexParameters().Length == 0 &&
                IsWritableNumericLike(property.PropertyType) &&
                ScorePriceMember(property.Name) > 0)
            .OrderByDescending(property => ScorePriceMember(property.Name))
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .ToList();

        for (int index = 0; index < propertyCandidates.Count; index++)
        {
            PropertyInfo property = propertyCandidates[index];
            try
            {
                object value = property.GetValue(target, null);
                if (TryExtractMoneyCents(value, property.PropertyType, out cents))
                {
                    resolvedMember = property.Name;
                    return true;
                }
            }
            catch
            {
                // Continuamos con el siguiente candidato.
            }
        }

        return false;
    }

    private static bool TryExtractMoneyCents(object value, Type declaredType, out long cents)
    {
        cents = 0L;
        if (value == null || declaredType == null)
        {
            return false;
        }

        Type effective = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (effective == typeof(byte) || effective == typeof(short) || effective == typeof(ushort) ||
            effective == typeof(int) || effective == typeof(uint) || effective == typeof(long) ||
            effective == typeof(ulong) || effective == typeof(float) || effective == typeof(double) ||
            effective == typeof(decimal))
        {
            try
            {
                cents = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Fallback para value objects monetarios. No inspeccionamos objetos
        // Unity y no invocamos getters arbitrarios fuera del propio value object.
        if (value is UnityEngine.Object)
        {
            return false;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type valueType = value.GetType();

        string[] nestedAliases =
        {
            "Cents", "cents", "MinorUnits", "minorUnits", "ValueCents", "valueCents",
            "AmountCents", "amountCents", "ValueMinorUnits", "valueMinorUnits",
            "AmountMinorUnits", "amountMinorUnits", "RawValue", "rawValue", "Value", "value"
        };

        for (int index = 0; index < nestedAliases.Length; index++)
        {
            FieldInfo field = valueType.GetField(nestedAliases[index], flags);
            if (field != null && TryExtractPrimitiveLong(field.GetValue(value), out cents))
            {
                return true;
            }

            PropertyInfo property = valueType.GetProperty(nestedAliases[index], flags);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    if (TryExtractPrimitiveLong(property.GetValue(value, null), out cents))
                    {
                        return true;
                    }
                }
                catch { }
            }
        }

        FieldInfo semanticField = valueType.GetFields(flags)
            .Where(field => TryScoreNestedMoneyMember(field.Name) > 0)
            .OrderByDescending(field => TryScoreNestedMoneyMember(field.Name))
            .ThenBy(field => field.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (semanticField != null && TryExtractPrimitiveLong(semanticField.GetValue(value), out cents))
        {
            return true;
        }

        return false;
    }

    private static bool TryExtractPrimitiveLong(object value, out long result)
    {
        result = 0L;
        if (value == null)
        {
            return false;
        }

        Type type = value.GetType();
        if (type != typeof(byte) && type != typeof(short) && type != typeof(ushort) &&
            type != typeof(int) && type != typeof(uint) && type != typeof(long) &&
            type != typeof(ulong) && type != typeof(float) && type != typeof(double) &&
            type != typeof(decimal))
        {
            return false;
        }

        try
        {
            result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int TryScoreNestedMoneyMember(string memberName)
    {
        string name = NormalizeMemberName(memberName);
        if (string.IsNullOrWhiteSpace(name)) return 0;

        int score = 0;
        if (name.Contains("cent")) score += 100;
        if (name.Contains("minor")) score += 90;
        if (name.Contains("amount")) score += 20;
        if (name.Contains("value")) score += 10;
        return score;
    }

    private static bool TrySetNamedMemberWithResolvedName(
        object target,
        object value,
        string[] names,
        out string resolvedMember)
    {
        resolvedMember = string.Empty;
        if (target == null || names == null)
        {
            return false;
        }

        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int index = 0; index < names.Length; index++)
        {
            FieldInfo field = type.GetField(names[index], flags);
            if (field != null && TryConvertMoneyCents(Convert.ToInt64(value, CultureInfo.InvariantCulture), field.FieldType, out object convertedField))
            {
                try
                {
                    field.SetValue(target, convertedField);
                    resolvedMember = field.Name;
                    return true;
                }
                catch { }
            }

            PropertyInfo property = type.GetProperty(names[index], flags);
            if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0 &&
                TryConvertMoneyCents(Convert.ToInt64(value, CultureInfo.InvariantCulture), property.PropertyType, out object convertedProperty))
            {
                try
                {
                    property.SetValue(target, convertedProperty, null);
                    resolvedMember = property.Name;
                    return true;
                }
                catch { }
            }
        }

        string[] normalized = names.Select(NormalizeMemberName).ToArray();
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (!normalized.Contains(NormalizeMemberName(field.Name)) ||
                !TryConvertMoneyCents(Convert.ToInt64(value, CultureInfo.InvariantCulture), field.FieldType, out object converted))
            {
                continue;
            }

            try
            {
                field.SetValue(target, converted);
                resolvedMember = field.Name;
                return true;
            }
            catch { }
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length != 0 ||
                !normalized.Contains(NormalizeMemberName(property.Name)) ||
                !TryConvertMoneyCents(Convert.ToInt64(value, CultureInfo.InvariantCulture), property.PropertyType, out object converted))
            {
                continue;
            }

            try
            {
                property.SetValue(target, converted, null);
                resolvedMember = property.Name;
                return true;
            }
            catch { }
        }

        return false;
    }

    private static int ScorePriceMember(string memberName)
    {
        string name = NormalizeMemberName(memberName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        int score = 0;
        if (name.Contains("price")) score += 100;
        if (name.Contains("cost")) score += 90;
        if (name.Contains("cent")) score += 55;
        if (name.Contains("minor")) score += 50;
        if (name.Contains("purchase")) score += 12;
        if (name.Contains("unit")) score += 8;
        if (name.Contains("package")) score += 5;

        // Nunca confundimos el precio del SKU con límites, descuentos,
        // transporte, mínimos de pedido o parámetros de mercado.
        if (name.Contains("variation")) score -= 250;
        if (name.Contains("discount")) score -= 250;
        if (name.Contains("shipping")) score -= 250;
        if (name.Contains("delivery")) score -= 200;
        if (name.Contains("threshold")) score -= 200;
        if (name.Contains("minimum") || name.Contains("maximum") ||
            name.StartsWith("min", StringComparison.Ordinal) ||
            name.StartsWith("max", StringComparison.Ordinal)) score -= 180;
        if (name.Contains("quantity") || name.Contains("increment") ||
            name.Contains("leadtime") || name.Contains("duration")) score -= 200;

        return score;
    }

    private static bool IsWritableNumericLike(Type type)
    {
        if (type == null)
        {
            return false;
        }

        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        return effective == typeof(byte) ||
               effective == typeof(short) ||
               effective == typeof(ushort) ||
               effective == typeof(int) ||
               effective == typeof(uint) ||
               effective == typeof(long) ||
               effective == typeof(ulong) ||
               effective == typeof(float) ||
               effective == typeof(double) ||
               effective == typeof(decimal) ||
               ScoreMoneyType(effective) > 0;
    }

    private static int ScoreMoneyType(Type type)
    {
        if (type == null)
        {
            return 0;
        }

        string name = NormalizeToken(type.Name);
        int score = 0;
        if (name.Contains("money")) score += 20;
        if (name.Contains("price")) score += 20;
        if (name.Contains("cost")) score += 20;
        if (name.Contains("cent")) score += 30;
        if (name.Contains("minor")) score += 30;
        return score;
    }

    private static bool TryConvertMoneyCents(long cents, Type targetType, out object converted)
    {
        converted = null;
        if (targetType == null)
        {
            return false;
        }

        Type effective = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (TryConvert(cents, effective, out converted))
        {
            return true;
        }

        // Soporte defensivo para value objects monetarios que expongan una
        // fábrica explícita en céntimos/minor units. No interpretamos como
        // euros un constructor numérico ambiguo.
        BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        string[] factoryNames =
        {
            "FromCents", "FromMinorUnits", "CreateFromCents", "CreateFromMinorUnits"
        };

        for (int index = 0; index < factoryNames.Length; index++)
        {
            MethodInfo[] factories = effective.GetMethods(staticFlags)
                .Where(method => string.Equals(method.Name, factoryNames[index], StringComparison.Ordinal) &&
                                 effective.IsAssignableFrom(method.ReturnType))
                .ToArray();

            for (int factoryIndex = 0; factoryIndex < factories.Length; factoryIndex++)
            {
                ParameterInfo[] parameters = factories[factoryIndex].GetParameters();
                if (parameters.Length != 1 ||
                    !TryConvert(cents, parameters[0].ParameterType, out object argument))
                {
                    continue;
                }

                try
                {
                    converted = factories[factoryIndex].Invoke(null, new[] { argument });
                    return converted != null;
                }
                catch { }
            }
        }

        return false;
    }

    private static string DescribeNumericMembers(object target)
    {
        if (target == null)
        {
            return "-";
        }

        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        List<string> descriptions = new List<string>();

        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (IsWritableNumericLike(field.FieldType))
            {
                descriptions.Add("field " + field.Name + ":" + field.FieldType.Name);
            }
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (property.GetIndexParameters().Length == 0 && IsWritableNumericLike(property.PropertyType))
            {
                descriptions.Add("property " + property.Name + ":" + property.PropertyType.Name +
                                 (property.CanWrite ? "[set]" : "[readonly]"));
            }
        }

        return descriptions.Count == 0
            ? "ninguno"
            : string.Join(", ", descriptions.OrderBy(text => text, StringComparer.Ordinal).ToArray());
    }

    private static bool SetRequired(object target, object value, params string[] names)
    {
        return SetMember(target, value, true, names);
    }

    private static bool SetOptional(object target, object value, params string[] names)
    {
        return SetMember(target, value, false, names);
    }

    private static bool SetMember(object target, object value, bool required, params string[] names)
    {
        if (target == null)
        {
            return false;
        }

        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int index = 0; index < names.Length; index++)
        {
            FieldInfo field = type.GetField(names[index], flags);
            if (field != null && TryConvert(value, field.FieldType, out object convertedField))
            {
                try
                {
                    field.SetValue(target, convertedField);
                    return true;
                }
                catch
                {
                    // Continuamos con otras variantes del contrato.
                }
            }

            PropertyInfo property = type.GetProperty(names[index], flags);
            if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0 &&
                TryConvert(value, property.PropertyType, out object convertedProperty))
            {
                try
                {
                    property.SetValue(target, convertedProperty, null);
                    return true;
                }
                catch
                {
                    // Continuamos con otras variantes del contrato.
                }
            }
        }

        string[] normalizedNames = names.Select(NormalizeMemberName).ToArray();
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (!normalizedNames.Contains(NormalizeMemberName(field.Name)) ||
                !TryConvert(value, field.FieldType, out object converted))
            {
                continue;
            }

            try
            {
                field.SetValue(target, converted);
                return true;
            }
            catch { }
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length != 0 ||
                !normalizedNames.Contains(NormalizeMemberName(property.Name)) ||
                !TryConvert(value, property.PropertyType, out object converted))
            {
                continue;
            }

            try
            {
                property.SetValue(target, converted, null);
                return true;
            }
            catch { }
        }

        return false;
    }

    private static bool SetOptionalEnumOrString(object target, string source, params string[] names)
    {
        if (target == null)
        {
            return false;
        }

        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int index = 0; index < names.Length; index++)
        {
            FieldInfo field = type.GetField(names[index], flags);
            if (field != null && TryConvertEnumOrString(source, field.FieldType, out object convertedField))
            {
                try
                {
                    field.SetValue(target, convertedField);
                    return true;
                }
                catch { }
            }

            PropertyInfo property = type.GetProperty(names[index], flags);
            if (property != null && property.CanWrite && TryConvertEnumOrString(source, property.PropertyType, out object convertedProperty))
            {
                try
                {
                    property.SetValue(target, convertedProperty, null);
                    return true;
                }
                catch { }
            }
        }

        return false;
    }

    private static bool SetOptionalEnumFromCandidates(object target, string[] candidates, params string[] names)
    {
        if (target == null || candidates == null)
        {
            return false;
        }

        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            FieldInfo field = type.GetField(names[nameIndex], flags);
            if (field != null)
            {
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    if (TryConvertEnumOrString(candidates[candidateIndex], field.FieldType, out object converted))
                    {
                        try
                        {
                            field.SetValue(target, converted);
                            return true;
                        }
                        catch { }
                    }
                }
            }

            PropertyInfo property = type.GetProperty(names[nameIndex], flags);
            if (property != null && property.CanWrite)
            {
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    if (TryConvertEnumOrString(candidates[candidateIndex], property.PropertyType, out object converted))
                    {
                        try
                        {
                            property.SetValue(target, converted, null);
                            return true;
                        }
                        catch { }
                    }
                }
            }
        }

        return false;
    }

    private static void SetDuration(object target, float hours, string[] hourMembers, string[] minuteMembers)
    {
        if (SetMember(target, hours, false, hourMembers))
        {
            return;
        }

        int minutes = Mathf.Max(1, Mathf.RoundToInt(hours * 60f));
        SetMember(target, minutes, false, minuteMembers);
    }

    private static bool TryConvert(object value, Type targetType, out object converted)
    {
        converted = null;
        if (targetType == null)
        {
            return false;
        }

        Type nullable = Nullable.GetUnderlyingType(targetType);
        if (nullable != null)
        {
            targetType = nullable;
        }

        if (value == null)
        {
            if (!targetType.IsValueType)
            {
                converted = null;
                return true;
            }

            return false;
        }

        if (targetType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        if (targetType == typeof(string))
        {
            converted = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return true;
        }

        if (targetType.IsEnum)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return TryParseEnumLoose(targetType, text, out converted);
        }

        try
        {
            if (targetType == typeof(bool))
            {
                converted = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType == typeof(int))
            {
                converted = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType == typeof(long))
            {
                converted = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType == typeof(float))
            {
                converted = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType == typeof(double))
            {
                converted = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType == typeof(decimal))
            {
                converted = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryConvertEnumOrString(string source, Type targetType, out object converted)
    {
        converted = null;
        if (targetType == typeof(string))
        {
            converted = source ?? string.Empty;
            return true;
        }

        if (targetType != null && targetType.IsEnum)
        {
            return TryParseEnumLoose(targetType, source, out converted);
        }

        return false;
    }

    private static bool TryParseEnumLoose(Type enumType, string source, out object value)
    {
        value = null;
        if (enumType == null || !enumType.IsEnum)
        {
            return false;
        }

        string normalized = NormalizeToken(source);
        string[] names = Enum.GetNames(enumType);
        for (int index = 0; index < names.Length; index++)
        {
            if (string.Equals(NormalizeToken(names[index]), normalized, StringComparison.Ordinal))
            {
                value = Enum.Parse(enumType, names[index]);
                return true;
            }
        }

        // Alias de unidades habituales del dominio actual.
        Dictionary<string, string[]> aliases = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "gram", new[] { "gram", "grams", "g" } },
            { "milliliter", new[] { "milliliter", "millilitre", "milliliters", "ml" } },
            { "unit", new[] { "unit", "units", "piece", "pieces", "unidad", "unidades" } },
            { "stocklimitado", new[] { "stocklimitado", "limited", "limitedstock" } },
            { "temporalmenteagotado", new[] { "temporalmenteagotado", "unavailable", "outofstock", "temporarilyunavailable" } },
            { "disponible", new[] { "disponible", "available" } }
        };

        foreach (KeyValuePair<string, string[]> pair in aliases)
        {
            if (!pair.Value.Contains(normalized))
            {
                continue;
            }

            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                if (pair.Value.Contains(NormalizeToken(names[nameIndex])))
                {
                    value = Enum.Parse(enumType, names[nameIndex]);
                    return true;
                }
            }
        }

        return false;
    }

    private static string NormalizeMemberName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string cleaned = value.Replace("k__BackingField", string.Empty)
            .Replace("<", string.Empty)
            .Replace(">", string.Empty);
        return NormalizeToken(cleaned);
    }

    internal static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] buffer = value.Trim().ToLowerInvariant().ToCharArray();
        return new string(buffer.Where(char.IsLetterOrDigit).ToArray());
    }

    private static Type FindType(string simpleName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(x => x != null).ToArray();
            }

            Type type = types.FirstOrDefault(
                x => x != null && string.Equals(x.Name, simpleName, StringComparison.Ordinal));

            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
#endif
