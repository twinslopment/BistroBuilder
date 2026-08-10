using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueba funcional Play Mode de supplier.catalog v2 sobre las autoridades
/// reales del proyecto. Es estrictamente de lectura/rebuild: no crea pedidos,
/// no recibe mercancía, no modifica inventario y no guarda partidas.
/// </summary>
public sealed class BistroBuilderSuppliers23AFunctionalTestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> resultLines = new List<string>();
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Suppliers/2.3A Canonical Suppliers Functional Test")]
    private static void Open()
    {
        var window = GetWindow<BistroBuilderSuppliers23AFunctionalTestWindow>("Prueba 2.3A3");
        window.minSize = new Vector2(680f, 540f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3A3 — Proveedores canónicos · cierre exhaustivo", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Prueba de lectura/reconstrucción sobre supplier.catalog v2 real. " +
            "No crea pedidos, no genera recepciones, no modifica stock y no guarda partidas.",
            MessageType.Info);

        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Entra en Play Mode para ejecutar la prueba.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Ejecutar prueba funcional exhaustiva 2.3A3", GUILayout.Height(32f)))
                RunFunctionalTest();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Resultado: " + passed + " correctos / " + failed + " fallos");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < resultLines.Count; i++)
            EditorGUILayout.LabelField(resultLines[i], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void RunFunctionalTest()
    {
        resultLines.Clear();
        passed = 0;
        failed = 0;

        BistroBuilderSupplierCatalogService service = BistroBuilderSupplierCatalogService.Instance;
        Check(service != null, "Existe SupplierCatalogService en Play Mode.");
        if (service == null) { Finish(); return; }

        int activeServiceCount = 0;
        var allSupplierServices = Resources.FindObjectsOfTypeAll<BistroBuilderSupplierCatalogService>();
        for (int i = 0; i < allSupplierServices.Length; i++)
        {
            var candidate = allSupplierServices[i];
            if (candidate != null && candidate.gameObject != null &&
                candidate.gameObject.scene.IsValid() && candidate.gameObject.activeInHierarchy &&
                candidate.enabled)
                activeServiceCount++;
        }
        Check(activeServiceCount == 1, "Existe exactamente una autoridad activa de proveedores.");

        bool wasInitialized = service.IsInitialized;
        string initialRebuildError = string.Empty;
        bool catalogReady = wasInitialized || service.TryRebuildCatalog(out initialRebuildError);
        Check(catalogReady,
            wasInitialized
                ? "El catálogo ya estaba inicializado correctamente."
                : "El catálogo puede inicializarse desde sus autoridades reales." +
                  (catalogReady ? string.Empty : " " + initialRebuildError));

        Check(service.IsInitialized, "La autoridad 2.3A3 queda inicializada.");
        if (!service.IsInitialized) { Finish(); return; }

        Check(service.Revision == BistroBuilderSupplierCatalogSettings.CurrentSchemaVersion,
            "Runtime exige y publica supplier.catalog schema v2.");
        Check(service.ContentRevision >= 1, "Existe una revisión monotónica de contenido runtime.");
        Check(string.IsNullOrEmpty(service.LastInitializationError), "No queda error residual de inicialización.");
        Check(service.LastValidationWarnings.Count == 0,
            "El catálogo base real no presenta huecos operativos.");

        BistroBuilderSupplierCatalogSettings settings =
            Resources.Load<BistroBuilderSupplierCatalogSettings>(BistroBuilderSupplierCatalogSettings.ResourcesPath);
        Check(settings != null, "Runtime encuentra el asset canónico supplier.catalog en Resources.");
        if (settings == null) { Finish(); return; }
        Check(settings.SchemaVersion == 2, "El asset real está migrado a supplier.catalog v2.");
        Check(settings.Products.Count >= service.IngredientCount * 2,
            "El asset real persiste al menos los dos SKU de semilla por cada ingrediente canónico actual.");

        Check(service.SupplierCount == settings.Suppliers.Count,
            "Runtime conserva exactamente la cardinalidad de proveedores del asset.");
        Check(service.ProductCount == settings.Products.Count,
            "Runtime conserva exactamente la cardinalidad de SKU persistidos.");
        Check(service.SupplierCount >= 4, "Hay al menos los cuatro proveedores base.");
        Check(service.IngredientCount >= 22, "Se enlazan al menos los 22 ingredientes canónicos de la línea base y se admiten ampliaciones.");
        Check(BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode(service.CatalogCurrencyCode),
            "El catálogo real publica una única moneda económica válida.");

        Check(!(service.Suppliers is List<BistroBuilderSupplierDefinition>) &&
              !(service.Products is List<BistroBuilderSupplierProductDefinition>) &&
              !(service.Ingredients is List<BistroBuilderSupplierIngredientDescriptor>) &&
              !(service.LastValidationWarnings is List<string>),
            "La autoridad runtime no expone List mutables mediante cast.");

        List<BistroBuilderSupplierDefinition> assetSuppliers = CloneSuppliers(settings.Suppliers);
        List<BistroBuilderSupplierProductDefinition> assetProducts = CloneProducts(settings.Products);
        SortSuppliers(assetSuppliers);
        SortProducts(assetProducts);
        Check(DeepEqualSuppliers(service.Suppliers, assetSuppliers),
            "Runtime copia profundamente todos los campos de proveedor del asset canónico.");
        Check(DeepEqualProducts(service.Products, assetProducts),
            "Runtime copia profundamente los SKU explícitos del asset; no los rederiva.");

        bool runtimeDoesNotExposeAssetObjects = true;
        for (int i = 0; i < settings.Suppliers.Count; i++)
        {
            if (settings.Suppliers[i] == null) continue;
            runtimeDoesNotExposeAssetObjects &= service.TryGetSupplier(
                settings.Suppliers[i].SupplierId, out var runtimeSupplier) &&
                !ReferenceEquals(runtimeSupplier, settings.Suppliers[i]);
        }
        for (int i = 0; i < settings.Products.Count; i++)
        {
            if (settings.Products[i] == null) continue;
            runtimeDoesNotExposeAssetObjects &= service.TryGetProduct(
                settings.Products[i].ProductId, out var runtimeProduct) &&
                !ReferenceEquals(runtimeProduct, settings.Products[i]);
        }
        Check(runtimeDoesNotExposeAssetObjects,
            "Runtime clona el asset: consumidores no reciben referencias serializadas editables.");

        BistroBuilderRecipeCatalogService recipeService = FindRecipeService();
        Check(recipeService != null && recipeService.IngredientCatalog != null,
            "Existe la autoridad tipada de ingredientes enlazada por RecipeCatalogService.");
        List<BistroBuilderIngredientDefinition> canonicalDefinitions = new List<BistroBuilderIngredientDefinition>();
        if (recipeService != null && recipeService.IngredientCatalog != null)
            recipeService.IngredientCatalog.CopyDefinitionsTo(canonicalDefinitions);
        canonicalDefinitions.Sort((a, b) => string.Compare(
            a != null ? a.IngredientId : string.Empty,
            b != null ? b.IngredientId : string.Empty,
            StringComparison.Ordinal));
        Check(canonicalDefinitions.Count == service.IngredientCount,
            "La vista de ingredientes 2.3A3 coincide uno-a-uno con IngredientCatalog.");

        bool descriptorsDeepMatch = canonicalDefinitions.Count == service.IngredientCount;
        for (int i = 0; i < canonicalDefinitions.Count && i < service.Ingredients.Count; i++)
        {
            var def = canonicalDefinitions[i];
            var descriptor = service.Ingredients[i];
            if (def == null || descriptor == null ||
                !def.TryGetReferencePackCanonicalMilliUnits(out long pack, out _))
            {
                descriptorsDeepMatch = false;
                continue;
            }
            descriptorsDeepMatch &= def.IngredientId == descriptor.IngredientId &&
                def.DisplayName == descriptor.DisplayName && def.BaseUnit == descriptor.BaseUnit &&
                def.Category == descriptor.Category && def.StorageType == descriptor.StorageType &&
                def.Perishable == descriptor.Perishable &&
                pack == descriptor.ReferencePackCanonicalMilliUnits &&
                def.ReferencePackPriceCents == descriptor.ReferencePackPriceCents;
        }
        Check(descriptorsDeepMatch,
            "Identidad, clasificación, unidad, caducidad y referencia económica vienen del dominio canónico.");

        HashSet<string> supplierIds = new HashSet<string>(StringComparer.Ordinal);
        bool suppliersValid = true;
        for (int i = 0; i < service.Suppliers.Count; i++)
        {
            var s = service.Suppliers[i];
            suppliersValid &= s != null && BistroBuilderMenuIdUtility.IsValidStableId(s.SupplierId) &&
                supplierIds.Add(s.SupplierId) && !string.IsNullOrWhiteSpace(s.DisplayName) &&
                s.MinimumOrderCents >= 0 && s.SeedPriceFactorBasisPoints >= 1 &&
                BistroBuilderSupplierCatalogValidator.IsValidCurrencyCode(s.CurrencyCode);
        }
        Check(suppliersValid, "Proveedores runtime mantienen identidad y economía válidas.");
        Check(supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.GeneralSupplierId) &&
              supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.FreshSupplierId) &&
              supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.PantrySupplierId) &&
              supplierIds.Contains(BistroBuilderSupplierCatalogDefaults.PremiumSupplierId),
            "Los cuatro SupplierId base están presentes.");

        Dictionary<string, BistroBuilderSupplierIngredientDescriptor> descriptorById =
            new Dictionary<string, BistroBuilderSupplierIngredientDescriptor>(StringComparer.Ordinal);
        for (int i = 0; i < service.Ingredients.Count; i++)
            descriptorById[service.Ingredients[i].IngredientId] = service.Ingredients[i];

        HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);
        bool productsValid = true;
        bool currencyMatches = true;
        bool baseUnitsMatch = true;
        for (int i = 0; i < service.Products.Count; i++)
        {
            var p = service.Products[i];
            productsValid &= p != null && BistroBuilderMenuIdUtility.IsValidStableId(p.ProductId) &&
                productIds.Add(p.ProductId) && supplierIds.Contains(p.SupplierId) &&
                descriptorById.ContainsKey(p.IngredientId) &&
                p.PackageCanonicalMilliUnits > 0L && p.PackPriceCents >= 1 &&
                p.MinimumPacks >= 1 && p.LeadTimeDays >= 0;
            if (p == null) continue;
            currencyMatches &= service.TryGetSupplier(p.SupplierId, out var supplier) &&
                supplier.CurrencyCode == p.CurrencyCode;
            baseUnitsMatch &= descriptorById.TryGetValue(p.IngredientId, out var ingredient) &&
                p.BaseUnit == ingredient.BaseUnit;
        }
        Check(productsValid, "SKU runtime mantienen IDs, FK, cantidades, precios, mínimos y plazos válidos.");
        Check(currencyMatches, "Cada SKU usa la moneda de su proveedor.");
        Check(baseUnitsMatch, "Cada SKU usa la unidad base del ingrediente canónico.");

        bool coverage = true;
        bool defaultSeedIdsPresent = true;
        bool generalCoversAll = true;
        bool cheapestWorks = true;
        bool offersSortedByCost = true;
        bool productLookupIdentity = true;
        List<BistroBuilderSupplierProductDefinition> expectedSeed =
            BistroBuilderSupplierCatalogBuilder.BuildProducts(CloneSuppliers(service.Suppliers), service.Ingredients);
        for (int i = 0; i < expectedSeed.Count; i++)
        {
            BistroBuilderSupplierProductDefinition expected = expectedSeed[i];
            defaultSeedIdsPresent &= service.TryGetProduct(expected.ProductId, out var actualSeed) &&
                actualSeed != null &&
                actualSeed.SupplierId == expected.SupplierId &&
                actualSeed.IngredientId == expected.IngredientId;
        }

        for (int i = 0; i < service.Ingredients.Count; i++)
        {
            var ingredient = service.Ingredients[i];
            var offers = service.GetOffersForIngredient(ingredient.IngredientId);
            HashSet<string> distinct = new HashSet<string>(StringComparer.Ordinal);
            bool hasGeneral = false;
            for (int j = 0; j < offers.Count; j++)
            {
                distinct.Add(offers[j].SupplierId);
                hasGeneral |= offers[j].SupplierId == BistroBuilderSupplierCatalogDefaults.GeneralSupplierId;
                productLookupIdentity &= service.TryGetProduct(offers[j].ProductId, out var resolved) &&
                    ReferenceEquals(resolved, offers[j]);
                if (j > 0)
                    offersSortedByCost &= BistroBuilderSupplierCatalogBuilder.CompareNormalizedUnitCost(
                        offers[j - 1], offers[j]) <= 0;
            }
            coverage &= distinct.Count >= BistroBuilderSupplierCatalogBuilder.RecommendedDistinctSuppliersPerIngredient;
            generalCoversAll &= hasGeneral;
            cheapestWorks &= service.TryFindLowestUnitCostPurchasableOffer(ingredient.IngredientId, out var cheapest) &&
                cheapest != null && cheapest.IngredientId == ingredient.IngredientId &&
                service.IsOfferPurchasable(cheapest);
        }
        Check(expectedSeed.Count == service.IngredientCount * 2 && defaultSeedIdsPresent,
            "Todos los ProductId de semilla base siguen presentes aunque el modelo acepte SKU adicionales.");
        Check(coverage, "Cada ingrediente real tiene al menos dos proveedores estructurales distintos.");
        Check(generalCoversAll, "El proveedor generalista cubre todos los ingredientes reales.");
        Check(cheapestWorks, "La oferta comprable de menor coste existe para cada ingrediente real.");
        Check(offersSortedByCost, "Ofertas por ingrediente salen ordenadas por coste normalizado estable.");
        Check(productLookupIdentity, "ProductId resuelve la misma instancia canónica runtime.");

        bool supplierLookupIdentity = true;
        int queriedProducts = 0;
        bool indexedViewsReadOnly = true;
        for (int i = 0; i < service.Suppliers.Count; i++)
        {
            var supplier = service.Suppliers[i];
            supplierLookupIdentity &= service.TryGetSupplier(supplier.SupplierId, out var resolved) &&
                ReferenceEquals(resolved, supplier);
            var supplierProducts = service.GetProductsForSupplier(supplier.SupplierId);
            queriedProducts += supplierProducts.Count;
            indexedViewsReadOnly &= !(supplierProducts is List<BistroBuilderSupplierProductDefinition>);
        }
        for (int i = 0; i < service.Ingredients.Count; i++)
            indexedViewsReadOnly &= !(service.GetOffersForIngredient(service.Ingredients[i].IngredientId)
                is List<BistroBuilderSupplierProductDefinition>);
        Check(supplierLookupIdentity, "SupplierId resuelve la misma instancia canónica runtime.");
        Check(queriedProducts == service.ProductCount, "Índice por proveedor cubre los SKU sin duplicados.");
        Check(indexedViewsReadOnly, "Índices por proveedor/ingrediente tampoco exponen List mutables.");

        BistroBuilderSupplierCatalogValidationResult runtimeValidation =
            BistroBuilderSupplierCatalogValidator.Validate(
                service.Suppliers, service.Products, service.Ingredients,
                BistroBuilderSupplierCatalogBuilder.RecommendedDistinctSuppliersPerIngredient,
                reportOperationalGapsAsWarnings: true);
        Check(runtimeValidation.IsValid, "El catálogo runtime supera el mismo validador de dominio.");
        Check(runtimeValidation.WarningCount == 0, "El catálogo runtime base no genera warnings operativos.");

        // Idempotencia del servicio: mismo contenido => misma revisión, referencias y cero evento.
        long revisionBefore = service.ContentRevision;
        var supplierReferenceBefore = service.Suppliers.Count > 0 ? service.Suppliers[0] : null;
        var productReferenceBefore = service.Products.Count > 0 ? service.Products[0] : null;
        int catalogChangedEvents = 0;
        Action handler = () => catalogChangedEvents++;
        service.CatalogChanged += handler;
        bool idempotentRebuild = service.TryRebuildCatalog(out string idempotentError);
        service.CatalogChanged -= handler;
        Check(idempotentRebuild, "Rebuild repetido completa con éxito." +
            (idempotentRebuild ? string.Empty : " " + idempotentError));
        Check(service.ContentRevision == revisionBefore,
            "Rebuild idéntico no incrementa ContentRevision.");
        Check(catalogChangedEvents == 0,
            "Rebuild idéntico no publica CatalogChanged.");
        Check(ReferenceEquals(supplierReferenceBefore, service.Suppliers[0]) &&
              ReferenceEquals(productReferenceBefore, service.Products[0]),
            "Rebuild idéntico conserva referencias canónicas existentes.");

        // Cambio real sintético, exclusivamente en memoria y por el mismo camino
        // atómico que producción. Después se restaura SIEMPRE el asset canónico.
        List<BistroBuilderSupplierDefinition> changedSuppliers = CloneSuppliers(service.Suppliers);
        List<BistroBuilderSupplierProductDefinition> changedProducts = CloneProducts(service.Products);
        List<BistroBuilderSupplierIngredientDescriptor> changedIngredients = CloneIngredients(service.Ingredients);
        BistroBuilderSupplierDefinition firstSupplier = changedSuppliers[0];
        changedSuppliers[0] = new BistroBuilderSupplierDefinition(
            firstSupplier.SupplierId,
            firstSupplier.DisplayName + " [TEST A1]",
            firstSupplier.Description,
            firstSupplier.IsCatalogEnabled,
            firstSupplier.MinimumOrderCents,
            firstSupplier.DefaultLeadTimeDays,
            firstSupplier.CurrencyCode,
            firstSupplier.SeedPriceFactorBasisPoints);

        long revisionBeforeChangedCandidate = service.ContentRevision;
        int changedCandidateEvents = 0;
        int changedCandidateSecondaryEvents = 0;
        Action changedCandidateHandler = () => changedCandidateEvents++;
        Action changedCandidateSecondaryHandler = () => changedCandidateSecondaryEvents++;
        service.CatalogChanged += changedCandidateHandler;
        service.CatalogChanged += changedCandidateSecondaryHandler;
        bool changedCandidateApplied = service.TryApplyCatalogCandidateForEditorTests(
            changedSuppliers, changedProducts, changedIngredients, out string changedCandidateError);
        service.CatalogChanged -= changedCandidateHandler;
        service.CatalogChanged -= changedCandidateSecondaryHandler;
        Check(changedCandidateApplied,
            "Un candidato válido distinto se aplica por el camino atómico de producción." +
            (changedCandidateApplied ? string.Empty : " " + changedCandidateError));
        Check(changedCandidateApplied && service.ContentRevision == revisionBeforeChangedCandidate + 1,
            "Un cambio real incrementa ContentRevision exactamente una vez.");
        Check(changedCandidateEvents == 1,
            "Un cambio real publica CatalogChanged exactamente una vez por suscriptor.");
        Check(changedCandidateSecondaryEvents == 1,
            "CatalogChanged entrega el mismo cambio a varios suscriptores sin omisiones.");
        Check(changedCandidateApplied && service.TryGetSupplier(firstSupplier.SupplierId, out var changedRuntimeSupplier) &&
              changedRuntimeSupplier.DisplayName.EndsWith("[TEST A1]", StringComparison.Ordinal),
            "El cambio válido sustituye el contenido runtime esperado.");

        // Un candidato inválido posterior no debe tocar el último estado válido,
        // ni su revisión, referencias o eventos.
        long revisionBeforeInvalidCandidate = service.ContentRevision;
        var supplierReferenceBeforeInvalid = service.Suppliers[0];
        var productReferenceBeforeInvalid = service.Products[0];
        List<BistroBuilderSupplierProductDefinition> invalidProducts = CloneProducts(service.Products);
        invalidProducts.Add(invalidProducts[0].Clone());
        int invalidCandidateEvents = 0;
        Action invalidCandidateHandler = () => invalidCandidateEvents++;
        service.CatalogChanged += invalidCandidateHandler;
        bool invalidCandidateApplied = service.TryApplyCatalogCandidateForEditorTests(
            CloneSuppliers(service.Suppliers), invalidProducts, CloneIngredients(service.Ingredients),
            out string invalidCandidateError);
        service.CatalogChanged -= invalidCandidateHandler;
        Check(!invalidCandidateApplied && !string.IsNullOrWhiteSpace(invalidCandidateError),
            "Un candidato corrupto se rechaza con error explícito.");
        Check(service.ContentRevision == revisionBeforeInvalidCandidate &&
              ReferenceEquals(supplierReferenceBeforeInvalid, service.Suppliers[0]) &&
              ReferenceEquals(productReferenceBeforeInvalid, service.Products[0]),
            "Un rebuild fallido conserva revisión y referencias del último catálogo válido.");
        Check(invalidCandidateEvents == 0,
            "Un rebuild fallido no publica CatalogChanged.");

        // Restauración obligatoria del contenido real antes de continuar el test.
        int restoreEvents = 0;
        Action restoreHandler = () => restoreEvents++;
        service.CatalogChanged += restoreHandler;
        bool restoredCanonical = service.TryRebuildCatalog(out string restoreError);
        service.CatalogChanged -= restoreHandler;
        Check(restoredCanonical,
            "Tras las pruebas adversariales se restaura supplier.catalog desde Resources." +
            (restoredCanonical ? string.Empty : " " + restoreError));
        Check(restoredCanonical && restoreEvents == 1 &&
              service.ContentRevision == revisionBeforeInvalidCandidate + 1,
            "Restaurar contenido canónico publica un único cambio y una única revisión.");
        Check(restoredCanonical && DeepEqualSuppliers(service.Suppliers, assetSuppliers) &&
              DeepEqualProducts(service.Products, assetProducts) &&
              string.IsNullOrEmpty(service.LastInitializationError),
            "La restauración deja exactamente el catálogo real y limpia el error del candidato inválido.");

        // Validación funcional REAL de inactividad a través de la autoridad runtime.
        // Antes solo se validaba el candidato con el validador de dominio; ahora
        // comprobamos también la aplicación atómica, consultas, comprabilidad,
        // warnings y restauración desde Resources.
        List<BistroBuilderSupplierDefinition> runtimeOneInactive = CloneWithOneInactive(
            service.Suppliers, BistroBuilderSupplierCatalogDefaults.FreshSupplierId);
        long revisionBeforeInactiveRuntime = service.ContentRevision;
        int inactiveRuntimeEvents = 0;
        Action inactiveRuntimeHandler = () => inactiveRuntimeEvents++;
        service.CatalogChanged += inactiveRuntimeHandler;
        bool inactiveRuntimeApplied = service.TryApplyCatalogCandidateForEditorTests(
            runtimeOneInactive,
            CloneProducts(service.Products),
            CloneIngredients(service.Ingredients),
            out string inactiveRuntimeError);
        service.CatalogChanged -= inactiveRuntimeHandler;

        Check(inactiveRuntimeApplied,
            "Un proveedor desactivado se aplica por la autoridad runtime sin corromper el catálogo." +
            (inactiveRuntimeApplied ? string.Empty : " " + inactiveRuntimeError));
        Check(inactiveRuntimeApplied &&
              service.ContentRevision == revisionBeforeInactiveRuntime + 1 &&
              inactiveRuntimeEvents == 1,
            "Desactivar un proveedor produce exactamente una revisión y un CatalogChanged.");

        bool freshSupplierDisabled =
            service.TryGetSupplier(
                BistroBuilderSupplierCatalogDefaults.FreshSupplierId,
                out BistroBuilderSupplierDefinition runtimeFreshSupplier) &&
            runtimeFreshSupplier != null &&
            !runtimeFreshSupplier.IsCatalogEnabled;
        Check(freshSupplierDisabled,
            "La autoridad runtime conserva el proveedor desactivado y publica su estado de catálogo.");

        BistroBuilderSupplierProductDefinition freshRuntimeProduct = null;
        IReadOnlyList<BistroBuilderSupplierProductDefinition> freshProducts =
            service.GetProductsForSupplier(BistroBuilderSupplierCatalogDefaults.FreshSupplierId);
        if (freshProducts.Count > 0)
        {
            freshRuntimeProduct = freshProducts[0];
        }
        Check(freshRuntimeProduct != null &&
              freshRuntimeProduct.IsCatalogAvailable &&
              !service.IsOfferPurchasable(freshRuntimeProduct),
            "Un SKU disponible de un proveedor desactivado permanece en catálogo pero deja de ser comprable.");

        bool unaffectedPurchasableExists = false;
        for (int i = 0; i < service.Products.Count; i++)
        {
            BistroBuilderSupplierProductDefinition candidateOffer = service.Products[i];
            if (candidateOffer != null &&
                candidateOffer.SupplierId != BistroBuilderSupplierCatalogDefaults.FreshSupplierId &&
                service.IsOfferPurchasable(candidateOffer))
            {
                unaffectedPurchasableExists = true;
                break;
            }
        }
        Check(unaffectedPurchasableExists,
            "Desactivar un proveedor no invalida las ofertas comprables de los demás proveedores.");

        long revisionBeforeInactiveRestore = service.ContentRevision;
        int inactiveRestoreEvents = 0;
        Action inactiveRestoreHandler = () => inactiveRestoreEvents++;
        service.CatalogChanged += inactiveRestoreHandler;
        bool restoredAfterInactive = service.TryRebuildCatalog(out string inactiveRestoreError);
        service.CatalogChanged -= inactiveRestoreHandler;
        Check(restoredAfterInactive &&
              service.ContentRevision == revisionBeforeInactiveRestore + 1 &&
              inactiveRestoreEvents == 1 &&
              DeepEqualSuppliers(service.Suppliers, assetSuppliers) &&
              DeepEqualProducts(service.Products, assetProducts) &&
              service.LastValidationWarnings.Count == 0,
            "Tras probar proveedor desactivado se restaura exactamente supplier.catalog real." +
            (restoredAfterInactive ? string.Empty : " " + inactiveRestoreError));

        // Escenario extremo: todos los proveedores desactivados. Debe seguir
        // siendo catálogo estructuralmente válido, informar huecos operativos
        // y no devolver ninguna oferta comprable.
        List<BistroBuilderSupplierDefinition> runtimeAllInactive =
            CloneAllInactive(service.Suppliers);
        long revisionBeforeAllInactive = service.ContentRevision;
        int allInactiveEvents = 0;
        Action allInactiveHandler = () => allInactiveEvents++;
        service.CatalogChanged += allInactiveHandler;
        bool allInactiveApplied = service.TryApplyCatalogCandidateForEditorTests(
            runtimeAllInactive,
            CloneProducts(service.Products),
            CloneIngredients(service.Ingredients),
            out string allInactiveRuntimeError);
        service.CatalogChanged -= allInactiveHandler;

        Check(allInactiveApplied &&
              service.ContentRevision == revisionBeforeAllInactive + 1 &&
              allInactiveEvents == 1,
            "Todos los proveedores pueden desactivarse como estado operativo sin corrupción." +
            (allInactiveApplied ? string.Empty : " " + allInactiveRuntimeError));
        Check(allInactiveApplied &&
              service.LastValidationWarnings.Count >= service.IngredientCount,
            "Con todos los proveedores desactivados la autoridad runtime publica huecos operativos.");

        bool anyPurchasableWhileAllInactive = false;
        for (int i = 0; i < service.Products.Count; i++)
        {
            if (service.IsOfferPurchasable(service.Products[i]))
            {
                anyPurchasableWhileAllInactive = true;
                break;
            }
        }
        Check(allInactiveApplied && !anyPurchasableWhileAllInactive,
            "Con todos los proveedores desactivados ninguna oferta puede comprarse.");

        long revisionBeforeAllInactiveRestore = service.ContentRevision;
        int allInactiveRestoreEvents = 0;
        Action allInactiveRestoreHandler = () => allInactiveRestoreEvents++;
        service.CatalogChanged += allInactiveRestoreHandler;
        bool restoredAfterAllInactive = service.TryRebuildCatalog(out string allInactiveRestoreError);
        service.CatalogChanged -= allInactiveRestoreHandler;
        Check(restoredAfterAllInactive &&
              service.ContentRevision == revisionBeforeAllInactiveRestore + 1 &&
              allInactiveRestoreEvents == 1 &&
              DeepEqualSuppliers(service.Suppliers, assetSuppliers) &&
              DeepEqualProducts(service.Products, assetProducts) &&
              service.LastValidationWarnings.Count == 0,
            "Tras el escenario sin proveedores activos se restaura exactamente el catálogo canónico." +
            (restoredAfterAllInactive ? string.Empty : " " + allInactiveRestoreError));

        // Inactividad es estado operativo; los SKU canónicos no se reescriben.
        List<BistroBuilderSupplierDefinition> oneInactive = CloneWithOneInactive(
            service.Suppliers, BistroBuilderSupplierCatalogDefaults.FreshSupplierId);
        var inactiveValidation = BistroBuilderSupplierCatalogValidator.Validate(
            oneInactive, CloneProducts(service.Products), service.Ingredients,
            2, reportOperationalGapsAsWarnings: true);
        Check(inactiveValidation.IsValid,
            "Desactivar un proveedor no convierte supplier.catalog en datos corruptos.");

        List<BistroBuilderSupplierDefinition> allInactive = CloneAllInactive(service.Suppliers);
        var allInactiveValidation = BistroBuilderSupplierCatalogValidator.Validate(
            allInactive, CloneProducts(service.Products), service.Ingredients,
            2, reportOperationalGapsAsWarnings: true);
        Check(allInactiveValidation.IsValid && allInactiveValidation.WarningCount >= service.IngredientCount,
            "Sin proveedores activos se informan huecos operativos sin romper integridad.");

        var firstRealProduct = service.Products[0];
        var syntheticUnavailable = new BistroBuilderSupplierProductDefinition(
            "product_synthetic_unavailable", firstRealProduct.SupplierId, firstRealProduct.IngredientId,
            "No disponible", firstRealProduct.PackageLabel, firstRealProduct.BaseUnit,
            firstRealProduct.PackageCanonicalMilliUnits, firstRealProduct.PackPriceCents,
            firstRealProduct.MinimumPacks, firstRealProduct.LeadTimeDays, false,
            firstRealProduct.CurrencyCode);
        Check(!service.IsOfferPurchasable(null) && !service.IsOfferPurchasable(syntheticUnavailable),
            "IsOfferPurchasable rechaza null y SKU no disponible sin efectos laterales.");

        var forgedSameProductId = new BistroBuilderSupplierProductDefinition(
            firstRealProduct.ProductId, "supplier_inexistente", firstRealProduct.IngredientId,
            "Clon externo", firstRealProduct.PackageLabel, firstRealProduct.BaseUnit,
            firstRealProduct.PackageCanonicalMilliUnits, 1, 1, 0, false, "USD");
        Check(service.IsOfferPurchasable(forgedSameProductId) ==
              service.IsOfferPurchasable(firstRealProduct),
            "La comprabilidad se resuelve por ProductId contra la autoridad actual; un clon externo no falsifica flags, proveedor ni precio.");

        List<BistroBuilderSupplierProductDefinition> copyBuffer =
            new List<BistroBuilderSupplierProductDefinition> { syntheticUnavailable };
        string firstIngredientId = service.Ingredients[0].IngredientId;
        service.CopyPurchasableOffersForIngredient(firstIngredientId, copyBuffer);
        bool copyBufferValid = copyBuffer.Count > 0;
        for (int i = 0; i < copyBuffer.Count; i++)
            copyBufferValid &= copyBuffer[i].IngredientId == firstIngredientId && service.IsOfferPurchasable(copyBuffer[i]);
        Check(copyBufferValid && !copyBuffer.Contains(syntheticUnavailable),
            "CopyPurchasableOffers limpia el buffer y copia solo ofertas comprables del ingrediente.");

        bool copiedOffersRemainSorted = true;
        for (int i = 1; i < copyBuffer.Count; i++)
        {
            copiedOffersRemainSorted &=
                BistroBuilderSupplierCatalogBuilder.CompareNormalizedUnitCost(
                    copyBuffer[i - 1], copyBuffer[i]) <= 0;
        }
        Check(copiedOffersRemainSorted,
            "CopyPurchasableOffers conserva el orden estable por coste normalizado.");

        bool nullDestinationRejected = false;
        try
        {
            service.CopyPurchasableOffersForIngredient(firstIngredientId, null);
        }
        catch (ArgumentNullException)
        {
            nullDestinationRejected = true;
        }
        Check(nullDestinationRejected,
            "CopyPurchasableOffers rechaza explícitamente un buffer null.");

        BistroBuilderSupplierCatalogSnapshot snapshot = service.CreateSnapshot();
        Check(snapshot != null && snapshot.SchemaVersion == 2 &&
              snapshot.ContentRevision == service.ContentRevision &&
              snapshot.Suppliers.Count == service.SupplierCount &&
              snapshot.Products.Count == service.ProductCount &&
              snapshot.Ingredients.Count == service.IngredientCount,
            "Snapshot incluye schema, revisión y las tres colecciones completas.");
        Check(!ReferenceEquals(snapshot.Suppliers[0], service.Suppliers[0]) &&
              !ReferenceEquals(snapshot.Products[0], service.Products[0]) &&
              !ReferenceEquals(snapshot.Ingredients[0], service.Ingredients[0]),
            "Snapshot es copia profunda y no filtra referencias runtime.");
        Check(!(snapshot.Suppliers is List<BistroBuilderSupplierDefinition>) &&
              !(snapshot.Products is List<BistroBuilderSupplierProductDefinition>) &&
              !(snapshot.Ingredients is List<BistroBuilderSupplierIngredientDescriptor>),
            "Snapshot expone vistas realmente de solo lectura.");
        string json = JsonUtility.ToJson(snapshot);
        var roundTrip = JsonUtility.FromJson<BistroBuilderSupplierCatalogSnapshot>(json);
        Check(DeepEqualSnapshot(snapshot, roundTrip),
            "Round-trip JSON preserva profundamente IDs, céntimos, cantidades, flags y clasificación.");

        Check(service.GetOffersForIngredient("ingredient_inexistente").Count == 0,
            "Ingrediente inexistente devuelve colección vacía.");
        Check(service.GetProductsForSupplier("supplier_inexistente").Count == 0,
            "Proveedor inexistente devuelve colección vacía.");
        Check(!service.TryGetSupplier("supplier_inexistente", out _),
            "SupplierId inexistente se rechaza limpiamente.");
        Check(!service.TryGetProduct("product_inexistente", out _),
            "ProductId inexistente se rechaza limpiamente.");
        Check(!service.TryFindLowestUnitCostPurchasableOffer("ingredient_inexistente", out _),
            "No se inventa oferta para ingrediente inexistente.");
        Check(service.TryGetSupplier("  SUPPLIER_HOSTELERIA_TOTAL  ", out var normalizedSupplier) &&
              normalizedSupplier.SupplierId == BistroBuilderSupplierCatalogDefaults.GeneralSupplierId,
            "Consultas normalizan IDs de entrada sin alterar identidad almacenada.");

        MethodInfo updateMethod = typeof(BistroBuilderSupplierCatalogService).GetMethod(
            "Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);
        Check(updateMethod == null, "SupplierCatalogService no hace polling en Update.");
        FieldInfo[] serviceFields = typeof(BistroBuilderSupplierCatalogService).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        bool noInventoryAuthorityField = true;
        for (int i = 0; i < serviceFields.Length; i++)
        {
            string typeName = serviceFields[i].FieldType.FullName ?? serviceFields[i].FieldType.Name;
            noInventoryAuthorityField &=
                typeName.IndexOf("InventoryService", StringComparison.OrdinalIgnoreCase) < 0 &&
                typeName.IndexOf("GoodsReceiving", StringComparison.OrdinalIgnoreCase) < 0;
        }
        Check(noInventoryAuthorityField,
            "2.3A3 no conserva referencias de escritura a Inventario/Recepciones.");

        Finish();
    }

    private static BistroBuilderRecipeCatalogService FindRecipeService()
    {
        var services = Resources.FindObjectsOfTypeAll<BistroBuilderRecipeCatalogService>();
        for (int i = 0; i < services.Length; i++)
        {
            var s = services[i];
            if (s != null && s.gameObject != null && s.gameObject.scene.IsValid() &&
                s.gameObject.activeInHierarchy && s.enabled && s.IngredientCatalog != null)
                return s;
        }
        return null;
    }

    private static List<BistroBuilderSupplierDefinition> CloneSuppliers(
        IReadOnlyList<BistroBuilderSupplierDefinition> source)
    {
        var result = new List<BistroBuilderSupplierDefinition>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++) if (source[i] != null) result.Add(source[i].Clone());
        return result;
    }

    private static List<BistroBuilderSupplierIngredientDescriptor> CloneIngredients(
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> source)
    {
        var result = new List<BistroBuilderSupplierIngredientDescriptor>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null) result.Add(source[i].Clone());
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

    private static List<BistroBuilderSupplierDefinition> CloneWithOneInactive(
        IReadOnlyList<BistroBuilderSupplierDefinition> source, string inactiveId)
    {
        var result = new List<BistroBuilderSupplierDefinition>();
        for (int i = 0; i < source.Count; i++)
        {
            var s = source[i];
            result.Add(new BistroBuilderSupplierDefinition(
                s.SupplierId, s.DisplayName, s.Description,
                s.SupplierId == inactiveId ? false : s.IsCatalogEnabled,
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

    private static void SortSuppliers(List<BistroBuilderSupplierDefinition> list)
    {
        list.Sort((a, b) => string.Compare(
            a != null ? a.SupplierId : string.Empty,
            b != null ? b.SupplierId : string.Empty,
            StringComparison.Ordinal));
    }

    private static void SortProducts(List<BistroBuilderSupplierProductDefinition> list)
    {
        list.Sort((a, b) => string.Compare(
            a != null ? a.ProductId : string.Empty,
            b != null ? b.ProductId : string.Empty,
            StringComparison.Ordinal));
    }

    private static bool DeepEqualSuppliers(
        IReadOnlyList<BistroBuilderSupplierDefinition> a,
        IReadOnlyList<BistroBuilderSupplierDefinition> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            var x = a[i]; var y = b[i];
            if (x == null || y == null || x.SupplierId != y.SupplierId ||
                x.DisplayName != y.DisplayName || x.Description != y.Description ||
                x.IsCatalogEnabled != y.IsCatalogEnabled || x.MinimumOrderCents != y.MinimumOrderCents ||
                x.DefaultLeadTimeDays != y.DefaultLeadTimeDays ||
                x.CurrencyCode != y.CurrencyCode ||
                x.SeedPriceFactorBasisPoints != y.SeedPriceFactorBasisPoints) return false;
        }
        return true;
    }

    private static bool DeepEqualProducts(
        IReadOnlyList<BistroBuilderSupplierProductDefinition> a,
        IReadOnlyList<BistroBuilderSupplierProductDefinition> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            var x = a[i]; var y = b[i];
            if (x == null || y == null || x.ProductId != y.ProductId ||
                x.SupplierId != y.SupplierId || x.IngredientId != y.IngredientId ||
                x.DisplayName != y.DisplayName || x.PackageLabel != y.PackageLabel ||
                x.BaseUnit != y.BaseUnit ||
                x.PackageCanonicalMilliUnits != y.PackageCanonicalMilliUnits ||
                x.PackPriceCents != y.PackPriceCents || x.MinimumPacks != y.MinimumPacks ||
                x.LeadTimeDays != y.LeadTimeDays || x.IsCatalogAvailable != y.IsCatalogAvailable ||
                x.CurrencyCode != y.CurrencyCode) return false;
        }
        return true;
    }

    private static bool DeepEqualSnapshot(
        BistroBuilderSupplierCatalogSnapshot a,
        BistroBuilderSupplierCatalogSnapshot b)
    {
        if (a == null || b == null || a.SchemaVersion != b.SchemaVersion ||
            a.ContentRevision != b.ContentRevision ||
            !DeepEqualSuppliers(a.Suppliers, b.Suppliers) ||
            !DeepEqualProducts(a.Products, b.Products) ||
            a.Ingredients.Count != b.Ingredients.Count) return false;
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

    private void Check(bool condition, string message)
    {
        if (condition)
        {
            passed++;
            resultLines.Add("OK: " + message);
        }
        else
        {
            failed++;
            resultLines.Add("FALLO: " + message);
        }
    }

    private void Finish()
    {
        string report =
            "PRUEBA FUNCIONAL 2.3A3 " + (failed == 0 ? "SUPERADA" : "FALLIDA") +
            "\nCorrectos: " + passed +
            "\nFallos: " + failed +
            "\n- " + string.Join("\n- ", resultLines);
        if (failed == 0) Debug.Log(report); else Debug.LogError(report);
        Repaint();
    }
}
