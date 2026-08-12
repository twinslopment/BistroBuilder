#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23EValidationWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> info = new List<string>();

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3E - Validar pedidos de compra")]
    public static void Open()
    {
        BistroBuilderSuppliers23EValidationWindow window =
            GetWindow<BistroBuilderSuppliers23EValidationWindow>("Validación 2.3E");
        window.minSize = new Vector2(900f, 600f);
        window.RunValidation();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.3E — Pedidos de compra y ciclo de estados",
            EditorStyles.boldLabel);
        if (GUILayout.Button("Validar de nuevo", GUILayout.Height(30f)))
        {
            RunValidation();
        }
        EditorGUILayout.LabelField(
            "Errores: " + errors.Count + "  Advertencias: " + warnings.Count + "  Información: " + info.Count,
            EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSection("ERRORES", errors, "Ninguno.");
        DrawSection("ADVERTENCIAS", warnings, "Ninguna.");
        DrawSection("INFORMACIÓN", info, "Sin información adicional.");
        EditorGUILayout.EndScrollView();
    }

    private void RunValidation()
    {
        errors.Clear();
        warnings.Clear();
        info.Clear();

        if (EditorApplication.isPlaying)
        {
            errors.Add("La validación estructural debe ejecutarse fuera de Play Mode.");
            return;
        }

        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSuppliers23EPaths.SupplierDatabasePath);
        BistroBuilderIngredientAuthoringDatabase ingredients =
            AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientAuthoringDatabase>(
                BistroBuilderSuppliers23EPaths.IngredientDatabasePath);
        BistroBuilderSupplierMarketSettings marketSettings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierMarketSettings>(
                BistroBuilderSuppliers23EPaths.MarketSettingsPath);
        BistroBuilderSupplierCommercialIntelligenceSettings commercialSettings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierCommercialIntelligenceSettings>(
                BistroBuilderSuppliers23EPaths.CommercialSettingsPath);
        BistroBuilderSupplierPurchaseOrderSettings orderSettings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierPurchaseOrderSettings>(
                BistroBuilderSuppliers23EPaths.PurchaseOrderSettingsPath);

        if (suppliers == null) errors.Add("Falta supplier.authoring.");
        if (ingredients == null) errors.Add("Falta ingredient.authoring.");
        if (marketSettings == null) errors.Add("Falta supplier.market.settings de 2.3C.");
        if (commercialSettings == null) errors.Add("Falta supplier.commercial.settings de 2.3D.");
        if (orderSettings == null) errors.Add("Falta supplier.orders.settings. Ejecuta el instalador 2.3E.");
        if (errors.Count > 0) return;

        if (orderSettings.SchemaId != BistroBuilderSupplierPurchaseOrderSettings.CurrentSchemaId)
            errors.Add("supplier.orders.settings no usa schemaId canónico.");
        if (orderSettings.SchemaVersion != BistroBuilderSupplierPurchaseOrderSettings.CurrentSchemaVersion)
            errors.Add("supplier.orders.settings no usa schemaVersion canónico.");
        if (marketSettings.ReviewEveryGameDays != 5)
            errors.Add("2.3E espera el ciclo cerrado de 5 días de 2.3C.");
        if (orderSettings.MaximumLinesPerOrder < 1 || orderSettings.MaximumOrdersInSnapshot < 128)
            errors.Add("Los límites defensivos de 2.3E no son válidos.");
        if (string.IsNullOrWhiteSpace(orderSettings.CurrencyCode) ||
            string.IsNullOrWhiteSpace(orderSettings.DisplayCodePrefix))
            errors.Add("Moneda o prefijo visible de PurchaseOrder no configurados.");

        int activeSuppliers = BistroBuilderSuppliers23ETestData.CountActiveSuppliers(suppliers);
        int activeOffers = BistroBuilderSuppliers23ETestData.CountActiveOffers(suppliers);
        int activeIngredients = 0;
        int activePackages = 0;
        for (int ingredientIndex = 0; ingredientIndex < ingredients.Ingredients.Count; ingredientIndex++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients.Ingredients[ingredientIndex];
            if (ingredient == null || !ingredient.isActive) continue;
            activeIngredients++;
            if (ingredient.commercialPackages == null) continue;
            for (int packageIndex = 0; packageIndex < ingredient.commercialPackages.Count; packageIndex++)
            {
                if (ingredient.commercialPackages[packageIndex] != null &&
                    ingredient.commercialPackages[packageIndex].isActive)
                {
                    activePackages++;
                }
            }
        }

        if (activeSuppliers != 6) errors.Add("Se esperaban 6 proveedores activos y hay " + activeSuppliers + ".");
        if (activeOffers != 66) errors.Add("Se esperaban 66 ofertas activas y hay " + activeOffers + ".");
        if (activeIngredients < 22) errors.Add("Se esperaban al menos 22 ingredientes activos y hay " + activeIngredients + ".");
        if (activePackages != 44) errors.Add("Tras 2.3C4 se esperan 44 formatos comerciales activos y hay " + activePackages + ".");

        HashSet<string> offerIds = new HashSet<string>(StringComparer.Ordinal);
        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive) continue;

            if (supplier.minimumOrderValueCents < 0L) errors.Add(supplier.SupplierId + ": pedido mínimo negativo.");
            if (supplier.shippingCostCents < 0L) errors.Add(supplier.SupplierId + ": porte negativo.");
            if (supplier.freeShippingThresholdCents < 0L) errors.Add(supplier.SupplierId + ": umbral de porte gratis negativo.");
            if (supplier.defaultLeadTimeGameHours <= 0f) errors.Add(supplier.SupplierId + ": lead time general no positivo.");
            if (supplier.reliabilityValue < 0f || supplier.reliabilityValue > 1f) errors.Add(supplier.SupplierId + ": fiabilidad fuera de 0..1.");

            if (supplier.deliveryWindows != null)
            {
                for (int windowIndex = 0; windowIndex < supplier.deliveryWindows.Count; windowIndex++)
                {
                    BistroBuilderSupplierDeliveryWindowAuthoring window = supplier.deliveryWindows[windowIndex];
                    if (window == null || window.startMinuteOfDay < 0 ||
                        window.endMinuteOfDay <= window.startMinuteOfDay || window.endMinuteOfDay > 1440)
                    {
                        errors.Add(supplier.SupplierId + ": ventana de entrega inválida #" + windowIndex + ".");
                    }
                }
            }

            if (supplier.baseOffers == null) continue;
            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                if (offer == null || !offer.isActive) continue;
                if (string.IsNullOrWhiteSpace(offer.SupplierOfferId) || !offerIds.Add(offer.SupplierOfferId))
                    errors.Add(supplier.SupplierId + ": SupplierOfferId nulo/duplicado.");
                if (offer.basePriceCents <= 0L) errors.Add(offer.SupplierOfferId + ": precio base no positivo.");
                if (offer.minimumPackageCount < 1 || offer.orderIncrement < 1)
                    errors.Add(offer.SupplierOfferId + ": mínimo/incremento de pedido inválido.");
                BistroBuilderIngredientAuthoringRecord ingredient;
                BistroBuilderCommercialPackageAuthoringRecord package;
                if (!BistroBuilderSupplierPurchaseOrderEngine.TryFindActivePackage(
                        ingredients, offer.ingredientId, offer.packageFormatId, out ingredient, out package))
                {
                    errors.Add(offer.SupplierOfferId + ": FK ingrediente/formato inválida.");
                }
            }
        }

        // Smoke test no destructivo del agregado y su máquina de estados.
        BistroBuilderSupplierAuthoringRecord testSupplier;
        BistroBuilderSupplierBaseOfferAuthoringRecord testOffer;
        if (BistroBuilderSuppliers23ETestData.TryFindSupplierWithActiveOffer(
                suppliers, out testSupplier, out testOffer))
        {
            BistroBuilderSupplierPurchaseOrdersSnapshot snapshot =
                BistroBuilderSupplierPurchaseOrderEngine.CreateInitialSnapshot(1, 2303UL, 2304UL);
            BistroBuilderPurchaseOrderRecord order;
            List<BistroBuilderPurchaseOrderConfirmationLineInput> inputs;
            string error;
            if (!BistroBuilderSuppliers23ETestData.TryBuildValidDraftAndInputs(
                    snapshot, testSupplier, ingredients, orderSettings, 1, out order, out inputs, out error))
            {
                errors.Add("No se pudo construir pedido de prueba: " + error);
            }
            else
            {
                BistroBuilderPurchaseOrderConfirmationPreview preview;
                if (!BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(
                        order, testSupplier, inputs, orderSettings, out preview, out error))
                {
                    errors.Add("No se pudo cotizar pedido de prueba: " + error);
                }
                else if (!preview.canConfirm)
                {
                    errors.Add("La cotización de prueba no es confirmable: " + string.Join(" | ", preview.blockers.ToArray()));
                }
                else
                {
                    BistroBuilderPurchaseOrderConfirmationReceipt receipt;
                    if (!BistroBuilderSupplierPurchaseOrderEngine.TryConfirm(
                            snapshot, order, testSupplier, preview, 1, 1L, 1L, out receipt, out error))
                    {
                        errors.Add("No se pudo confirmar pedido de prueba: " + error);
                    }
                    else
                    {
                        if (order.status != BistroBuilderPurchaseOrderStatus.Confirmed)
                            errors.Add("La confirmación no deja el pedido en Confirmed.");
                        if (order.confirmedLines.Count == 0 || order.draftLines.Count != 0)
                            errors.Add("La confirmación no congela/normaliza correctamente las líneas.");
                        if (order.totalCents != preview.totalCents || order.supplierTerms == null)
                            errors.Add("La confirmación no congela correctamente importes/condiciones.");

                        if (!BistroBuilderSupplierPurchaseOrderEngine.TryMarkPendingDelivery(
                                snapshot, order, "plan_validation_23e", 2, 8 * 60, 12 * 60, 1, out error) ||
                            !BistroBuilderSupplierPurchaseOrderEngine.TryMarkInDelivery(
                                snapshot, order, 2, 0, 2, out error) ||
                            !BistroBuilderSupplierPurchaseOrderEngine.TryMarkDelivered(
                                snapshot, order, "receipt_validation_23e", 2, out error))
                        {
                            errors.Add("La máquina de estados canónica no completa Confirmed→PendingDelivery→InDelivery→Delivered: " + error);
                        }

                        if (!BistroBuilderSupplierPurchaseOrderEngine.ValidateSnapshotAgainstAuthoring(
                                snapshot, suppliers, ingredients, orderSettings, out error))
                        {
                            errors.Add("El snapshot de prueba no valida tras el ciclo completo: " + error);
                        }
                    }
                }
            }
        }
        else
        {
            errors.Add("No existe proveedor/oferta activa para el smoke test.");
        }

        if (errors.Count == 0)
        {
            info.Add("Proveedores activos: " + activeSuppliers + ". Ofertas activas: " + activeOffers + ".");
            info.Add("Ingredientes activos: " + activeIngredients + ". Formatos comerciales activos: " + activePackages + ".");
            info.Add("PurchaseOrder congela precio de mercado, precio efectivo, PromotionId, cantidades, portes, mínimos, lead time y condiciones del proveedor al confirmar.");
            info.Add("Draft es editable; Confirmed y PendingDelivery son cancelables según settings; InDelivery/Delivered no lo son.");
            info.Add("2.3G usará TryMarkPendingDelivery/TryMarkInDelivery; 2.2B cerrará Delivered aportando ReceiptId.");
            info.Add("2.3E no planifica retrasos, no crea entregas físicas y no escribe Inventario ni Recepciones.");
            info.Add("La persistencia integral se conectará en 2.3J; 2.3E ya expone CreateSnapshot/TryRestoreSnapshot.");
        }
    }

    private void DrawSection(string title, List<string> items, string empty)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (items.Count == 0)
        {
            EditorGUILayout.LabelField(empty, EditorStyles.wordWrappedLabel);
            return;
        }
        for (int index = 0; index < items.Count; index++)
        {
            EditorGUILayout.LabelField(items[index], EditorStyles.wordWrappedLabel);
        }
    }
}
#endif
