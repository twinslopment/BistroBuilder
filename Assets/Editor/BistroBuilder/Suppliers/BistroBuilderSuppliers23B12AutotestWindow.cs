#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23B12AutotestWindow : EditorWindow
{
    private readonly List<string> lines = new List<string>();
    private Vector2 scroll;
    private int passed;
    private int failed;

    [MenuItem(
        "Tools/Bistro Builder/Proveedores/2.3B1+B2 - Autotest formatos y catálogo",
        priority = 4)]
    public static void OpenWindow()
    {
        BistroBuilderSuppliers23B12AutotestWindow window =
            GetWindow<BistroBuilderSuppliers23B12AutotestWindow>();
        window.titleContent = new GUIContent("Autotest 2.3B1+B2");
        window.minSize = new Vector2(760f, 520f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.3B1+B2 — Autotest de formatos, catálogo y ofertas base",
            EditorStyles.boldLabel);

        if (GUILayout.Button("Ejecutar autotest", GUILayout.Height(30f)))
        {
            RunAutotest();
        }

        EditorGUILayout.HelpBox(
            "Superadas: " + passed + "   |   Fallidas: " + failed,
            failed > 0 ? MessageType.Error : MessageType.Info);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < lines.Count; index++)
        {
            EditorGUILayout.LabelField(lines[index], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunAutotest()
    {
        lines.Clear();
        passed = 0;
        failed = 0;

        BistroBuilderSupplierAuthoringDatabase suppliers =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredients =
            BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        Check(suppliers != null, "Existe supplier.authoring.");
        Check(ingredients != null, "Existe ingredient.authoring.");
        if (suppliers == null || ingredients == null)
        {
            Finish();
            return;
        }

        Check(suppliers.SchemaVersion == 2, "supplier.authoring está migrado a schema v2.");
        Check(ingredients.SchemaVersion == 2, "ingredient.authoring está migrado a schema v2.");
        Check(suppliers.Suppliers.Count >= 6, "Existen al menos seis proveedores de autoría.");
        Check(ingredients.Ingredients.Count >= 22, "Se conservan al menos 22 ingredientes canónicos.");

        int activeIngredientCount = 0;
        int ingredientsWithPackages = 0;
        int ingredientsWithTwoPackages = 0;
        int totalPackages = 0;
        HashSet<string> packageIds = new HashSet<string>(StringComparer.Ordinal);
        bool packageIdsUnique = true;
        bool packageQuantitiesValid = true;
        bool packageNamesValid = true;

        for (int i = 0; i < ingredients.Ingredients.Count; i++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients.Ingredients[i];
            if (ingredient == null || !ingredient.isActive)
            {
                continue;
            }

            activeIngredientCount++;
            int active = 0;
            if (ingredient.commercialPackages != null)
            {
                for (int p = 0; p < ingredient.commercialPackages.Count; p++)
                {
                    BistroBuilderCommercialPackageAuthoringRecord package = ingredient.commercialPackages[p];
                    if (package == null)
                    {
                        packageIdsUnique = false;
                        packageQuantitiesValid = false;
                        continue;
                    }

                    totalPackages++;
                    if (!packageIds.Add(package.PackageFormatId)) packageIdsUnique = false;
                    if (package.netQuantityMicrounits <= 0) packageQuantitiesValid = false;
                    if (string.IsNullOrWhiteSpace(package.displayName) || string.IsNullOrWhiteSpace(package.packageType)) packageNamesValid = false;
                    if (package.isActive) active++;
                }
            }

            if (active > 0) ingredientsWithPackages++;
            if (active >= 2) ingredientsWithTwoPackages++;
        }

        Check(activeIngredientCount >= 22, "La línea base conserva al menos 22 ingredientes activos.");
        Check(ingredientsWithPackages == activeIngredientCount, "Todos los ingredientes activos tienen al menos un formato comercial activo.");
        Check(ingredientsWithTwoPackages == activeIngredientCount, "La semilla actual ofrece al menos dos formatos por ingrediente activo.");
        Check(totalPackages >= activeIngredientCount * 2, "Existen suficientes formatos para la línea base activa actual.");
        Check(packageIdsUnique, "Todos los PackageFormatId son únicos.");
        Check(packageQuantitiesValid, "Todos los formatos tienen cantidad exacta mayor que cero.");
        Check(packageNamesValid, "Todos los formatos tienen nombre y tipo de envase.");

        int totalOffers = 0;
        HashSet<string> offerIds = new HashSet<string>(StringComparer.Ordinal);
        bool offerIdsUnique = true;
        bool pricesValid = true;
        bool orderRulesValid = true;
        bool offerReferencesValid = true;
        bool offerIngredientMatches = true;
        bool marketRangesValid = true;
        bool everySupplierHasOffer = true;

        Dictionary<string, BistroBuilderIngredientAuthoringRecord> packageOwner =
            new Dictionary<string, BistroBuilderIngredientAuthoringRecord>(StringComparer.Ordinal);
        for (int i = 0; i < ingredients.Ingredients.Count; i++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients.Ingredients[i];
            if (ingredient == null || ingredient.commercialPackages == null) continue;
            for (int p = 0; p < ingredient.commercialPackages.Count; p++)
            {
                BistroBuilderCommercialPackageAuthoringRecord package = ingredient.commercialPackages[p];
                if (package != null && !string.IsNullOrWhiteSpace(package.PackageFormatId))
                {
                    packageOwner[package.PackageFormatId] = ingredient;
                }
            }
        }

        for (int s = 0; s < suppliers.Suppliers.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[s];
            if (supplier == null || !supplier.isActive) continue;
            int activeOffers = 0;
            HashSet<string> packagePerSupplier = new HashSet<string>(StringComparer.Ordinal);
            if (supplier.baseOffers != null)
            {
                for (int o = 0; o < supplier.baseOffers.Count; o++)
                {
                    BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[o];
                    if (offer == null)
                    {
                        offerReferencesValid = false;
                        continue;
                    }

                    totalOffers++;
                    if (!offerIds.Add(offer.SupplierOfferId)) offerIdsUnique = false;
                    if (offer.basePriceCents <= 0) pricesValid = false;
                    if (offer.minimumPackageCount <= 0 || offer.orderIncrement <= 0) orderRulesValid = false;
                    if (!packageOwner.TryGetValue(offer.packageFormatId, out BistroBuilderIngredientAuthoringRecord owner))
                    {
                        offerReferencesValid = false;
                    }
                    else if (!string.Equals(owner.IngredientId, offer.ingredientId, StringComparison.Ordinal))
                    {
                        offerIngredientMatches = false;
                    }
                    if (!packagePerSupplier.Add(offer.packageFormatId)) offerReferencesValid = false;
                    if (offer.minimumMarketVariationPercent > 0f || offer.maximumMarketVariationPercent < 0f || offer.minimumMarketVariationPercent > offer.maximumMarketVariationPercent) marketRangesValid = false;
                    if (offer.isActive) activeOffers++;
                }
            }
            if (activeOffers == 0) everySupplierHasOffer = false;
        }

        Check(totalOffers >= activeIngredientCount * 2, "Existen al menos dos ofertas estructurales por ingrediente activo en conjunto.");
        Check(offerIdsUnique, "Todos los SupplierOfferId son únicos.");
        Check(pricesValid, "Todos los precios base son mayores que cero.");
        Check(orderRulesValid, "Mínimos e incrementos de pedido son válidos.");
        Check(offerReferencesValid, "Todas las ofertas resuelven un formato existente y no duplican formato por proveedor.");
        Check(offerIngredientMatches, "IngredientId de cada oferta coincide con el propietario del formato.");
        Check(marketRangesValid, "Todos los rangos de evolución futura contienen el precio base.");
        Check(everySupplierHasOffer, "Todos los proveedores activos tienen catálogo base.");

        bool everyIngredientHasCompetition = true;
        bool marketCentralCoversAll = true;
        bool expressCoversAll = true;
        bool hasThirdOptionForAll = true;
        for (int i = 0; i < ingredients.Ingredients.Count; i++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients.Ingredients[i];
            if (ingredient == null || !ingredient.isActive) continue;
            int count = BistroBuilderSuppliers23B12ContentSeed.CountActiveOffersForIngredient(suppliers, ingredient.IngredientId);
            if (count < 2) everyIngredientHasCompetition = false;
            if (count < 3) hasThirdOptionForAll = false;
            if (!SupplierHasIngredient(suppliers, "supplier_mercado_central", ingredient.IngredientId)) marketCentralCoversAll = false;
            if (!SupplierHasIngredient(suppliers, "supplier_hosteleria_express", ingredient.IngredientId)) expressCoversAll = false;
        }

        Check(everyIngredientHasCompetition, "Todo ingrediente activo tiene al menos dos proveedores distintos.");
        Check(marketCentralCoversAll, "Mercado Central cubre todos los ingredientes actuales.");
        Check(expressCoversAll, "Hostelería Express cubre todos los ingredientes actuales.");
        Check(hasThirdOptionForAll, "La semilla proporciona una tercera alternativa comercial a cada ingrediente actual.");

        Check(SupplierExists(suppliers, "supplier_distribuciones_norte"), "Existe Distribuciones Norte.");
        Check(SupplierExists(suppliers, "supplier_huerta_clara"), "Existe Huerta Clara.");
        Check(SupplierExists(suppliers, "supplier_carnes_selectas"), "Existe Carnes Selectas.");
        Check(SupplierExists(suppliers, "supplier_costa_fresca"), "Existe Costa Fresca.");

        Check(VerifyWholesaleUsesLargeFormats(suppliers, ingredients), "Distribuciones Norte prioriza el formato mayor disponible.");
        Check(VerifyExpressUsesSmallFormats(suppliers, ingredients), "Hostelería Express prioriza el formato menor disponible.");
        Check(VerifyPricePositioning(suppliers, ingredients), "La semilla mantiene Express por encima del mayorista cuando ambos compiten.");

        BistroBuilderSupplierAuthoringRecord supplierClone = suppliers.Suppliers[0].DeepClone(true);
        Check(supplierClone != suppliers.Suppliers[0], "DeepClone de proveedor crea una instancia distinta.");
        Check(supplierClone.baseOffers.Count == suppliers.Suppliers[0].baseOffers.Count, "DeepClone conserva cardinalidad de ofertas base.");
        Check(supplierClone.baseOffers.Count == 0 || supplierClone.baseOffers[0] != suppliers.Suppliers[0].baseOffers[0], "DeepClone no comparte referencias de oferta.");

        BistroBuilderIngredientAuthoringRecord ingredientClone = ingredients.Ingredients[0].DeepClone(true);
        Check(ingredientClone != ingredients.Ingredients[0], "DeepClone de ingrediente crea una instancia distinta.");
        Check(ingredientClone.commercialPackages.Count == ingredients.Ingredients[0].commercialPackages.Count, "DeepClone conserva formatos comerciales.");
        Check(ingredientClone.commercialPackages.Count == 0 || ingredientClone.commercialPackages[0] != ingredients.Ingredients[0].commercialPackages[0], "DeepClone no comparte referencias de formato.");

        BistroBuilderIngredientAuthoringRecord synthetic = new BistroBuilderIngredientAuthoringRecord();
        synthetic.AssignStableIdOnce("ingredient_test_23b");
        synthetic.RefreshCanonicalSnapshot("Ingrediente prueba", "Gram", "Produce");
        int firstSeed = BistroBuilderSuppliers23B12ContentSeed.EnsureFormatsForIngredient(synthetic);
        int secondSeed = BistroBuilderSuppliers23B12ContentSeed.EnsureFormatsForIngredient(synthetic);
        Check(firstSeed >= 2, "La semilla crea formatos en un ingrediente vacío.");
        Check(secondSeed == 0, "La semilla de formatos es idempotente.");

        BistroBuilderSuppliers23B12ValidationReport validation =
            BistroBuilderSuppliers23B12Validator.Validate(suppliers, ingredients);
        Check(validation.ErrorCount == 0, "El validador estructural B1+B2 devuelve 0 errores.");

        Finish();
    }

    private static bool SupplierExists(BistroBuilderSupplierAuthoringDatabase database, string id)
    {
        return database.TryGetSupplier(id, out BistroBuilderSupplierAuthoringRecord supplier) && supplier != null;
    }

    private static bool SupplierHasIngredient(BistroBuilderSupplierAuthoringDatabase database, string supplierId, string ingredientId)
    {
        if (!database.TryGetSupplier(supplierId, out BistroBuilderSupplierAuthoringRecord supplier) || supplier.baseOffers == null) return false;
        for (int i = 0; i < supplier.baseOffers.Count; i++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[i];
            if (offer != null && offer.isActive && string.Equals(offer.ingredientId, ingredientId, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static bool VerifyWholesaleUsesLargeFormats(BistroBuilderSupplierAuthoringDatabase suppliers, BistroBuilderIngredientAuthoringDatabase ingredients)
    {
        if (!suppliers.TryGetSupplier("supplier_distribuciones_norte", out BistroBuilderSupplierAuthoringRecord wholesale)) return false;
        if (wholesale.baseOffers == null || wholesale.baseOffers.Count == 0) return false;
        for (int o = 0; o < wholesale.baseOffers.Count; o++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = wholesale.baseOffers[o];
            if (!ingredients.TryGetIngredient(offer.ingredientId, out BistroBuilderIngredientAuthoringRecord ingredient)) return false;
            BistroBuilderCommercialPackageAuthoringRecord selected = BistroBuilderSuppliers23B12ContentSeed.SelectPackageForSupplier(wholesale.SupplierId, ingredient);
            if (selected == null || !string.Equals(selected.PackageFormatId, offer.packageFormatId, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool VerifyExpressUsesSmallFormats(BistroBuilderSupplierAuthoringDatabase suppliers, BistroBuilderIngredientAuthoringDatabase ingredients)
    {
        if (!suppliers.TryGetSupplier("supplier_hosteleria_express", out BistroBuilderSupplierAuthoringRecord express)) return false;
        if (express.baseOffers == null || express.baseOffers.Count == 0) return false;
        for (int o = 0; o < express.baseOffers.Count; o++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = express.baseOffers[o];
            if (!ingredients.TryGetIngredient(offer.ingredientId, out BistroBuilderIngredientAuthoringRecord ingredient)) return false;
            BistroBuilderCommercialPackageAuthoringRecord selected = BistroBuilderSuppliers23B12ContentSeed.SelectPackageForSupplier(express.SupplierId, ingredient);
            if (selected == null || !string.Equals(selected.PackageFormatId, offer.packageFormatId, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool VerifyPricePositioning(BistroBuilderSupplierAuthoringDatabase suppliers, BistroBuilderIngredientAuthoringDatabase ingredients)
    {
        if (!suppliers.TryGetSupplier("supplier_hosteleria_express", out BistroBuilderSupplierAuthoringRecord express) ||
            !suppliers.TryGetSupplier("supplier_distribuciones_norte", out BistroBuilderSupplierAuthoringRecord wholesale)) return false;

        for (int i = 0; i < ingredients.Ingredients.Count; i++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients.Ingredients[i];
            BistroBuilderSupplierBaseOfferAuthoringRecord e = FindOffer(express, ingredient.IngredientId);
            BistroBuilderSupplierBaseOfferAuthoringRecord w = FindOffer(wholesale, ingredient.IngredientId);
            if (e == null || w == null) continue;
            BistroBuilderCommercialPackageAuthoringRecord ep = FindPackage(ingredient, e.packageFormatId);
            BistroBuilderCommercialPackageAuthoringRecord wp = FindPackage(ingredient, w.packageFormatId);
            if (ep == null || wp == null) return false;
            double en = e.basePriceCents / ep.NetQuantityInBaseUnits;
            double wn = w.basePriceCents / wp.NetQuantityInBaseUnits;
            if (en <= wn) return false;
        }
        return true;
    }

    private static BistroBuilderSupplierBaseOfferAuthoringRecord FindOffer(BistroBuilderSupplierAuthoringRecord supplier, string ingredientId)
    {
        if (supplier == null || supplier.baseOffers == null) return null;
        for (int i = 0; i < supplier.baseOffers.Count; i++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[i];
            if (offer != null && offer.isActive && string.Equals(offer.ingredientId, ingredientId, StringComparison.Ordinal)) return offer;
        }
        return null;
    }

    private static BistroBuilderCommercialPackageAuthoringRecord FindPackage(BistroBuilderIngredientAuthoringRecord ingredient, string id)
    {
        if (ingredient == null || ingredient.commercialPackages == null) return null;
        for (int i = 0; i < ingredient.commercialPackages.Count; i++)
        {
            BistroBuilderCommercialPackageAuthoringRecord package = ingredient.commercialPackages[i];
            if (package != null && string.Equals(package.PackageFormatId, id, StringComparison.Ordinal)) return package;
        }
        return null;
    }

    private void Check(bool condition, string message)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + message);
        }
        else
        {
            failed++;
            lines.Add("[FALLO] " + message);
        }
    }

    private void Finish()
    {
        Debug.Log(
            "AUTOTEST 2.3B1+B2 — Superadas: " + passed +
            ", fallidas: " + failed + ".");
        Repaint();
    }
}
#endif
