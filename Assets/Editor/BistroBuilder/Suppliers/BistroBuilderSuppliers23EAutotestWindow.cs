#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23EAutotestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3E - Autotest pedidos de compra")]
    public static void Open()
    {
        BistroBuilderSuppliers23EAutotestWindow window =
            GetWindow<BistroBuilderSuppliers23EAutotestWindow>("Autotest 2.3E");
        window.minSize = new Vector2(900f, 600f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AUTOTEST 2.3E — PurchaseOrder canónico", EditorStyles.boldLabel);
        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("Ejecutar autotest", GUILayout.Height(32f))) Run();
        GUI.enabled = true;
        EditorGUILayout.LabelField(
            "Pruebas superadas: " + passed + " / Pruebas fallidas: " + failed,
            EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < results.Count; index++)
            EditorGUILayout.LabelField(results[index], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        results.Clear();
        passed = 0;
        failed = 0;
        try
        {
            Check(!EditorApplication.isPlaying, "Autotest ejecutado en Edit Mode.");
            BistroBuilderSupplierAuthoringDatabase suppliers =
                AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23EPaths.SupplierDatabasePath);
            BistroBuilderIngredientAuthoringDatabase ingredients =
                AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientAuthoringDatabase>(BistroBuilderSuppliers23EPaths.IngredientDatabasePath);
            BistroBuilderSupplierMarketSettings marketSettings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierMarketSettings>(BistroBuilderSuppliers23EPaths.MarketSettingsPath);
            BistroBuilderSupplierCommercialIntelligenceSettings commercialSettings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierCommercialIntelligenceSettings>(BistroBuilderSuppliers23EPaths.CommercialSettingsPath);
            BistroBuilderSupplierPurchaseOrderSettings settings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierPurchaseOrderSettings>(BistroBuilderSuppliers23EPaths.PurchaseOrderSettingsPath);

            Check(suppliers != null, "supplier.authoring existe.");
            Check(ingredients != null, "ingredient.authoring existe.");
            Check(marketSettings != null, "supplier.market.settings de 2.3C existe.");
            Check(commercialSettings != null, "supplier.commercial.settings de 2.3D existe.");
            Check(settings != null, "supplier.orders.settings de 2.3E existe.");
            if (suppliers == null || ingredients == null || marketSettings == null || commercialSettings == null || settings == null) return;

            Check(settings.SchemaId == BistroBuilderSupplierPurchaseOrderSettings.CurrentSchemaId, "supplier.orders.settings usa schemaId canónico.");
            Check(settings.SchemaVersion == BistroBuilderSupplierPurchaseOrderSettings.CurrentSchemaVersion, "supplier.orders.settings usa schemaVersion canónico.");
            Check(settings.CurrencyCode == "EUR", "2.3E trabaja en EUR para la V1.");
            Check(!string.IsNullOrWhiteSpace(settings.DisplayCodePrefix), "PurchaseOrder tiene prefijo visible configurado.");
            Check(settings.MaximumLinesPerOrder >= 1, "Límite de líneas por pedido válido.");
            Check(settings.MaximumOrdersInSnapshot >= 128, "Límite de histórico de pedidos del snapshot válido.");
            Check(marketSettings.ReviewEveryGameDays == 5, "2.3E consume el ciclo cerrado de cinco días de 2.3C.");
            Check(BistroBuilderSuppliers23ETestData.CountActiveSuppliers(suppliers) == 6, "Hay exactamente 6 proveedores activos.");
            Check(BistroBuilderSuppliers23ETestData.CountActiveOffers(suppliers) == 66, "Hay exactamente 66 ofertas base activas.");

            int ingredientRevisionBefore = ingredients.ContentRevision;
            int supplierRevisionBefore = suppliers.ContentRevision;
            BistroBuilderSupplierAuthoringRecord supplier;
            BistroBuilderSupplierBaseOfferAuthoringRecord firstOffer;
            Check(BistroBuilderSuppliers23ETestData.TryFindSupplierWithActiveOffer(suppliers, out supplier, out firstOffer), "Existe proveedor con oferta activa para probar 2.3E.");
            if (supplier == null || firstOffer == null) return;

            BistroBuilderSupplierPurchaseOrdersSnapshot snapshot = BistroBuilderSupplierPurchaseOrderEngine.CreateInitialSnapshot(1, 2303UL, 2304UL);
            Check(snapshot != null, "Se crea supplier.orders.runtime.");
            Check(snapshot.schemaId == BistroBuilderSupplierPurchaseOrdersSnapshot.CurrentSchemaId, "Snapshot de pedidos usa schemaId canónico.");
            Check(snapshot.schemaVersion == BistroBuilderSupplierPurchaseOrdersSnapshot.CurrentSchemaVersion, "Snapshot de pedidos usa schemaVersion canónico.");
            Check(snapshot.orders.Count == 0, "Una partida nueva no inventa pedidos.");
            Check(snapshot.nextOrderSequence == 1, "Secuencia de PurchaseOrder comienza en 1.");
            Check(snapshot.sourceMarketSeed == 2303UL, "Snapshot de pedidos queda vinculado a la sesión de mercado indicada.");
            Check(snapshot.sourceCommercialSeed == 2304UL, "Snapshot de pedidos queda vinculado a la sesión comercial indicada.");

            BistroBuilderPurchaseOrderRecord order;
            List<BistroBuilderPurchaseOrderConfirmationLineInput> inputs;
            string error;
            Check(BistroBuilderSuppliers23ETestData.TryBuildValidDraftAndInputs(snapshot, supplier, ingredients, settings, 1, out order, out inputs, out error), "Se construye un Draft válido de prueba.");
            Check(order != null && order.status == BistroBuilderPurchaseOrderStatus.Draft, "El nuevo pedido nace en Draft.");
            Check(order != null && !string.IsNullOrWhiteSpace(order.purchaseOrderId), "PurchaseOrderId estable asignado.");
            Check(order != null && order.displayCode.StartsWith(settings.DisplayCodePrefix + "-", StringComparison.Ordinal), "Código visible PO asignado.");
            Check(order != null && order.draftLines.Count > 0, "Draft contiene líneas comerciales.");
            Check(snapshot.orders.Count == 1, "El agregado registra el pedido una sola vez.");
            Check(snapshot.nextOrderSequence == 2, "La secuencia avanza tras crear el pedido.");

            BistroBuilderPurchaseOrderDraftLine firstDraftLine = order.draftLines[0];
            BistroBuilderSupplierBaseOfferAuthoringRecord resolvedOffer;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryFindActiveOffer(supplier, firstDraftLine.supplierOfferId, out resolvedOffer), "La línea Draft resuelve su oferta activa.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.IsPackageCountValid(resolvedOffer, firstDraftLine.packageCount), "La línea respeta mínimo e incremento.");
            Check(!BistroBuilderSupplierPurchaseOrderEngine.IsPackageCountValid(resolvedOffer, 0), "Cantidad cero es rechazada.");

            int lineCountBeforeUpdate = order.draftLines.Count;
            int validUpdatedCount = firstDraftLine.packageCount + Math.Max(1, resolvedOffer.orderIncrement);
            Check(BistroBuilderSupplierPurchaseOrderEngine.TrySetDraftLine(snapshot, order, resolvedOffer, validUpdatedCount, 1, settings, out error), "Actualizar una línea Draft existente es válido.");
            Check(order.draftLines.Count == lineCountBeforeUpdate, "Actualizar una oferta no duplica la línea.");
            Check(order.draftLines[0].packageCount == validUpdatedCount, "La cantidad actualizada queda aplicada.");
            // Sincronizar el input de prueba con la cantidad actualizada.
            inputs[0].packageCount = validUpdatedCount;

            BistroBuilderPurchaseOrderConfirmationPreview preview;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(order, supplier, inputs, settings, out preview, out error), "Se construye preview de confirmación.");
            Check(preview != null && preview.canConfirm, "El pedido válido puede confirmarse.");
            Check(preview.lineCount == inputs.Count, "Preview conserva cardinalidad de líneas.");
            Check(preview.subtotalCents > 0L, "Subtotal cotizado es positivo.");
            Check(preview.totalCents == preview.subtotalCents + preview.shippingCostCents, "Total = subtotal + portes.");
            Check(preview.minimumOrderSatisfied, "El pedido satisface el mínimo económico del proveedor.");
            Check(preview.quotedLeadTimeGameHours > 0f, "El pedido congela un lead time positivo.");
            BistroBuilderSupplierPurchaseOrdersSnapshot unboundSnapshot = snapshot.DeepClone();
            unboundSnapshot.sourceMarketSeed = 0UL;
            unboundSnapshot.sourceCommercialSeed = 0UL;
            BistroBuilderPurchaseOrderConfirmationReceipt unboundReceipt;
            Check(!BistroBuilderSupplierPurchaseOrderEngine.TryConfirm(
                unboundSnapshot, unboundSnapshot.orders[0], supplier, preview, 1, 10L, 20L, out unboundReceipt, out error),
                "Confirmar exige identidad de sesión 2.3C/2.3D y rechaza un snapshot no vinculado.");

            BistroBuilderSupplierAuthoringRecord freeShippingSupplier = supplier.DeepClone(true);
            freeShippingSupplier.freeShippingEnabled = true;
            freeShippingSupplier.freeShippingThresholdCents = 0L;
            freeShippingSupplier.shippingCostCents = 999L;
            BistroBuilderPurchaseOrderConfirmationPreview freeShippingPreview;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(order, freeShippingSupplier, inputs, settings, out freeShippingPreview, out error), "Preview calcula escenario de porte gratuito.");
            Check(freeShippingPreview.shippingCostCents == 0L, "Umbral de porte gratis satisfecho aplica 0 céntimos.");
            BistroBuilderSupplierAuthoringRecord paidShippingSupplier = supplier.DeepClone(true);
            paidShippingSupplier.freeShippingEnabled = false;
            paidShippingSupplier.shippingCostCents = 777L;
            BistroBuilderPurchaseOrderConfirmationPreview paidShippingPreview;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(order, paidShippingSupplier, inputs, settings, out paidShippingPreview, out error), "Preview calcula escenario de porte de pago.");
            Check(paidShippingPreview.shippingCostCents == 777L, "Porte configurado se aplica exactamente cuando no hay gratuidad.");

            BistroBuilderSupplierAuthoringRecord impossibleMinimumSupplier = supplier.DeepClone(true);
            impossibleMinimumSupplier.minimumOrderValueCents = long.MaxValue / 4L;
            BistroBuilderPurchaseOrderConfirmationPreview blockedMinimum;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(order, impossibleMinimumSupplier, inputs, settings, out blockedMinimum, out error), "Preview puede explicar un mínimo económico incumplido.");
            Check(!blockedMinimum.canConfirm && !blockedMinimum.minimumOrderSatisfied, "Pedido bajo mínimo queda bloqueado.");

            List<BistroBuilderPurchaseOrderConfirmationLineInput> unavailableInputs = CloneInputs(inputs);
            unavailableInputs[0].availability = BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado;
            unavailableInputs[0].availableForNewOrders = false;
            BistroBuilderPurchaseOrderConfirmationPreview unavailablePreview;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(order, supplier, unavailableInputs, settings, out unavailablePreview, out error), "Preview procesa disponibilidad comercial agotada.");
            Check(!unavailablePreview.canConfirm, "TemporalmenteAgotado bloquea nuevos pedidos.");
            List<BistroBuilderPurchaseOrderConfirmationLineInput> limitedInputs = CloneInputs(inputs);
            limitedInputs[0].availability = BistroBuilderSupplierOfferAvailability.StockLimitado;
            limitedInputs[0].availableForNewOrders = true;
            BistroBuilderPurchaseOrderConfirmationPreview limitedPreview;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(order, supplier, limitedInputs, settings, out limitedPreview, out error), "Preview procesa StockLimitado como disponibilidad cualitativa.");
            Check(limitedPreview.canConfirm, "StockLimitado sigue permitiendo un pedido nuevo en 2.3E.");

            // Simular una promoción para comprobar el freeze del snapshot comercial.
            inputs[0].hasActivePromotion = true;
            inputs[0].promotionId = "promotion_autotest_23e";
            inputs[0].promotionStartGameDay = 1;
            inputs[0].promotionEndGameDayExclusive = 7;
            inputs[0].discountBasisPoints = 1000;
            inputs[0].promotionReasonCode = "autotest";
            inputs[0].promotionReasonText = "Promoción controlada del autotest.";
            inputs[0].marketPriceCents = Math.Max(inputs[0].basePriceCents, 100L);
            inputs[0].effectiveUnitPriceCents = Math.Max(1L, inputs[0].marketPriceCents * 9L / 10L);
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(order, supplier, inputs, settings, out preview, out error), "Preview acepta una cotización promocional de 2.3D.");
            Check(preview.canConfirm, "La promoción no rompe la confirmación.");
            BistroBuilderSupplierPurchaseOrdersSnapshot tamperedCommitSnapshot = snapshot.DeepClone();
            BistroBuilderPurchaseOrderConfirmationPreview tamperedCommitPreview = preview.DeepClone();
            tamperedCommitPreview.totalCents++;
            BistroBuilderPurchaseOrderConfirmationReceipt tamperedCommitReceipt;
            Check(!BistroBuilderSupplierPurchaseOrderEngine.TryConfirm(
                tamperedCommitSnapshot, tamperedCommitSnapshot.orders[0], supplier, tamperedCommitPreview, 1, 10L, 20L, out tamperedCommitReceipt, out error),
                "TryConfirm rechaza una cotización manipulada entre preview y commit.");

            long frozenUnitPrice = preview.lines[0].effectiveUnitPriceCents;
            long ordersRevisionBeforeConfirm = snapshot.ordersRevision;
            BistroBuilderPurchaseOrderConfirmationReceipt receipt;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryConfirm(snapshot, order, supplier, preview, 1, 10L, 20L, out receipt, out error), "Draft confirma correctamente.");
            Check(order.status == BistroBuilderPurchaseOrderStatus.Confirmed, "Estado tras confirmar: Confirmed.");
            Check(order.draftLines.Count == 0, "Las líneas editables se eliminan al congelar el pedido.");
            Check(order.confirmedLines.Count == preview.lineCount, "El pedido conserva líneas confirmadas inmutables.");
            Check(order.confirmedLines[0].effectiveUnitPriceCents == frozenUnitPrice, "Precio efectivo queda congelado.");
            Check(order.confirmedLines[0].promotionId == "promotion_autotest_23e", "PromotionId queda congelado y trazable.");
            Check(order.confirmedLines[0].promotionStartGameDay == 1 && order.confirmedLines[0].promotionEndGameDayExclusive == 7, "Ventana temporal de la promoción queda congelada.");
            Check(order.sourceMarketRevision == 10L && order.sourceCommercialRevision == 20L, "Revisiones de mercado/comercial quedan congeladas.");
            Check(order.supplierTerms != null && order.supplierTerms.supplierId == supplier.SupplierId, "Condiciones del proveedor quedan congeladas.");
            Check(order.totalCents == preview.totalCents, "Total confirmado coincide con la cotización.");
            Check(receipt != null && receipt.purchaseOrderId == order.purchaseOrderId, "Confirmación genera receipt compacto para UI.");
            Check(snapshot.ordersRevision == ordersRevisionBeforeConfirm + 1, "Confirmar incrementa OrdersRevision una vez.");

            long revisionAfterConfirm = snapshot.ordersRevision;
            BistroBuilderPurchaseOrderConfirmationReceipt secondReceipt;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryConfirm(snapshot, order, supplier, preview, 1, 10L, 20L, out secondReceipt, out error), "Reconfirmar Confirmed es idempotente.");
            Check(snapshot.ordersRevision == revisionAfterConfirm, "Reconfirmación idempotente no incrementa revisión.");
            Check(secondReceipt != null && secondReceipt.totalCents == receipt.totalCents, "Reconfirmación devuelve el mismo resumen económico.");
            Check(!BistroBuilderSupplierPurchaseOrderEngine.TrySetDraftLine(snapshot, order, resolvedOffer, validUpdatedCount, 1, settings, out error), "Un pedido confirmado ya no puede editar líneas.");

            long priceBeforeExternalChange = order.confirmedLines[0].effectiveUnitPriceCents;
            inputs[0].effectiveUnitPriceCents += 9999L;
            Check(order.confirmedLines[0].effectiveUnitPriceCents == priceBeforeExternalChange, "Cambios posteriores de mercado no alteran el precio confirmado.");

            // Flujo logístico canónico reservado a 2.3G/H/2.2B.
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryMarkPendingDelivery(snapshot, order, "plan_autotest_23e", 2, 480, 720, 1, out error), "Confirmed → PendingDelivery válido con plan 2.3G.");
            Check(order.status == BistroBuilderPurchaseOrderStatus.PendingDelivery, "Estado PendingDelivery aplicado.");
            Check(order.logisticsPlanId == "plan_autotest_23e", "LogisticsPlanId queda trazable.");
            long pendingRevision = order.stateRevision;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryMarkPendingDelivery(snapshot, order, "plan_autotest_23e", 2, 480, 720, 1, out error), "Repetir exactamente PendingDelivery es idempotente.");
            Check(order.stateRevision == pendingRevision, "PendingDelivery idempotente no incrementa StateRevision.");
            Check(!BistroBuilderSupplierPurchaseOrderEngine.TryMarkPendingDelivery(snapshot, order, "otro_plan", 2, 480, 720, 1, out error), "PendingDelivery rechaza sobrescribir silenciosamente otro plan.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryUpdatePendingDeliveryPlan(snapshot, order, "plan_autotest_23e", 3, 540, 780, 60, 2, out error), "2.3G puede replanificar un PendingDelivery conservando LogisticsPlanId.");
            Check(order.plannedDeliveryGameDay == 3 && order.plannedDelayGameMinutes == 60, "La replanificación conserva retraso previsto y nueva ventana.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryMarkInDelivery(snapshot, order, 3, 60, 3, out error), "PendingDelivery → InDelivery válido.");
            Check(order.status == BistroBuilderPurchaseOrderStatus.InDelivery, "Estado InDelivery aplicado.");
            Check(order.appliedDelayGameMinutes == 60, "El contrato puede recibir retraso calculado por 2.3G.");
            long inDeliveryRevision = order.stateRevision;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryMarkInDelivery(snapshot, order, 3, 60, 4, out error), "Repetir exactamente InDelivery es idempotente.");
            Check(order.stateRevision == inDeliveryRevision, "InDelivery idempotente no incrementa StateRevision.");
            Check(!BistroBuilderSupplierPurchaseOrderEngine.TryMarkInDelivery(snapshot, order, 3, 90, 4, out error), "InDelivery rechaza reescribir el retraso ya aplicado.");
            Check(!BistroBuilderSupplierPurchaseOrderEngine.TryCancel(snapshot, order, "No permitido", 2, settings, out error), "InDelivery no puede cancelarse.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryMarkDelivered(snapshot, order, "receipt_autotest_23e", 3, out error), "InDelivery → Delivered válido con ReceiptId 2.2B.");
            Check(order.status == BistroBuilderPurchaseOrderStatus.Delivered, "Estado Delivered aplicado.");
            Check(order.deliveryReceiptId == "receipt_autotest_23e", "Delivered conserva ReceiptId de recepción.");
            long deliveredRevision = order.stateRevision;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryMarkDelivered(snapshot, order, "receipt_autotest_23e", 3, out error), "Repetir exactamente Delivered es idempotente.");
            Check(order.stateRevision == deliveredRevision, "Delivered idempotente no incrementa StateRevision.");
            Check(!BistroBuilderSupplierPurchaseOrderEngine.TryMarkDelivered(snapshot, order, "otro_receipt", 3, out error), "Delivered rechaza sustituir el ReceiptId congelado.");
            Check(!BistroBuilderSupplierPurchaseOrderEngine.TryCancel(snapshot, order, "No permitido", 2, settings, out error), "Delivered no puede cancelarse.");

            // Segundo pedido para cancelar antes de expedición.
            BistroBuilderPurchaseOrderRecord cancelDraft;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryCreateDraft(snapshot, supplier.SupplierId, 2, settings, out cancelDraft, out error), "Se crea segundo Draft para probar cancelación.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryCancel(snapshot, cancelDraft, "Cambio de decisión", 2, settings, out error), "Draft puede cancelarse.");
            Check(cancelDraft.status == BistroBuilderPurchaseOrderStatus.Cancelled, "Cancelación deja estado terminal Cancelled.");
            Check(cancelDraft.cancellationReason == "Cambio de decisión", "Motivo de cancelación queda trazable.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryCancel(snapshot, cancelDraft, "Otra vez", 2, settings, out error), "Cancelar dos veces es idempotente.");

            // Cancelación de pedidos ya confirmados o planificados, antes de InDelivery.
            BistroBuilderPurchaseOrderRecord confirmedCancelOrder;
            List<BistroBuilderPurchaseOrderConfirmationLineInput> confirmedCancelInputs;
            Check(BistroBuilderSuppliers23ETestData.TryBuildValidDraftAndInputs(snapshot, supplier, ingredients, settings, 2, out confirmedCancelOrder, out confirmedCancelInputs, out error), "Se crea Draft para probar cancelación desde Confirmed.");
            BistroBuilderPurchaseOrderConfirmationPreview confirmedCancelPreview;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(confirmedCancelOrder, supplier, confirmedCancelInputs, settings, out confirmedCancelPreview, out error) && confirmedCancelPreview.canConfirm, "Pedido de cancelación Confirmed es cotizable.");
            BistroBuilderPurchaseOrderConfirmationReceipt confirmedCancelReceipt;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryConfirm(snapshot, confirmedCancelOrder, supplier, confirmedCancelPreview, 2, 2L, 2L, out confirmedCancelReceipt, out error), "Pedido de prueba alcanza Confirmed.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryCancel(snapshot, confirmedCancelOrder, "Cancelado antes de planificación", 2, settings, out error), "Confirmed puede cancelarse según settings.");
            Check(confirmedCancelOrder.status == BistroBuilderPurchaseOrderStatus.Cancelled, "Cancelación desde Confirmed termina en Cancelled.");

            BistroBuilderPurchaseOrderRecord pendingCancelOrder;
            List<BistroBuilderPurchaseOrderConfirmationLineInput> pendingCancelInputs;
            Check(BistroBuilderSuppliers23ETestData.TryBuildValidDraftAndInputs(snapshot, supplier, ingredients, settings, 2, out pendingCancelOrder, out pendingCancelInputs, out error), "Se crea Draft para probar cancelación desde PendingDelivery.");
            BistroBuilderPurchaseOrderConfirmationPreview pendingCancelPreview;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(pendingCancelOrder, supplier, pendingCancelInputs, settings, out pendingCancelPreview, out error) && pendingCancelPreview.canConfirm, "Pedido de cancelación PendingDelivery es cotizable.");
            BistroBuilderPurchaseOrderConfirmationReceipt pendingCancelReceipt;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryConfirm(snapshot, pendingCancelOrder, supplier, pendingCancelPreview, 2, 2L, 2L, out pendingCancelReceipt, out error), "Segundo pedido de prueba alcanza Confirmed.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryMarkPendingDelivery(snapshot, pendingCancelOrder, "plan_cancel_23e", 3, 480, 720, 2, out error), "Segundo pedido alcanza PendingDelivery.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryCancel(snapshot, pendingCancelOrder, "Cancelado antes de expedición", 2, settings, out error), "PendingDelivery puede cancelarse según settings.");
            Check(pendingCancelOrder.status == BistroBuilderPurchaseOrderStatus.Cancelled, "Cancelación desde PendingDelivery termina en Cancelled.");

            Check(BistroBuilderSupplierPurchaseOrderEngine.ValidateSnapshotAgainstAuthoring(snapshot, suppliers, ingredients, settings, out error), "Snapshot completo converge con autoría.");
            string fingerprint = BistroBuilderSupplierPurchaseOrderEngine.BuildFingerprint(snapshot);
            Check(!string.IsNullOrWhiteSpace(fingerprint) && fingerprint != "NULL", "Fingerprint canónico de pedidos generado.");
            BistroBuilderSupplierPurchaseOrdersSnapshot clone = snapshot.DeepClone();
            Check(BistroBuilderSupplierPurchaseOrderEngine.BuildFingerprint(clone) == fingerprint, "DeepClone conserva exactamente el fingerprint.");
            BistroBuilderSupplierPurchaseOrdersSnapshot seedClone = snapshot.DeepClone();
            seedClone.sourceMarketSeed++;
            Check(BistroBuilderSupplierPurchaseOrderEngine.BuildFingerprint(seedClone) != fingerprint, "El fingerprint detecta cambio de sesión de mercado.");
            clone.orders[0].totalCents++;
            Check(snapshot.orders[0].totalCents != clone.orders[0].totalCents, "DeepClone es defensivo y no comparte estado mutable.");
            Check(!BistroBuilderSupplierPurchaseOrderEngine.ValidateSnapshotAgainstAuthoring(clone, suppliers, ingredients, settings, out error), "El validador detecta manipulación de la aritmética de un pedido confirmado.");
            Check(suppliers.ContentRevision == supplierRevisionBefore, "Autotest no modifica supplier.authoring.");
            Check(ingredients.ContentRevision == ingredientRevisionBefore, "Autotest no modifica ingredient.authoring.");
        }
        catch (Exception exception)
        {
            failed++;
            results.Add("[FALLO] Excepción del autotest: " + exception);
        }
        Repaint();
    }

    private static List<BistroBuilderPurchaseOrderConfirmationLineInput> CloneInputs(
        List<BistroBuilderPurchaseOrderConfirmationLineInput> source)
    {
        List<BistroBuilderPurchaseOrderConfirmationLineInput> clone =
            new List<BistroBuilderPurchaseOrderConfirmationLineInput>();
        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderPurchaseOrderConfirmationLineInput input = source[index];
            clone.Add(new BistroBuilderPurchaseOrderConfirmationLineInput
            {
                purchaseOrderLineId = input.purchaseOrderLineId,
                supplierOfferId = input.supplierOfferId,
                supplierId = input.supplierId,
                ingredientId = input.ingredientId,
                ingredientDisplayName = input.ingredientDisplayName,
                canonicalUnit = input.canonicalUnit,
                packageFormatId = input.packageFormatId,
                packageDisplayName = input.packageDisplayName,
                packageType = input.packageType,
                logisticSize = input.logisticSize,
                packageNetQuantityMicrounits = input.packageNetQuantityMicrounits,
                packageCount = input.packageCount,
                minimumPackageCount = input.minimumPackageCount,
                orderIncrement = input.orderIncrement,
                basePriceCents = input.basePriceCents,
                marketPriceCents = input.marketPriceCents,
                effectiveUnitPriceCents = input.effectiveUnitPriceCents,
                availability = input.availability,
                availableForNewOrders = input.availableForNewOrders,
                hasActivePromotion = input.hasActivePromotion,
                promotionId = input.promotionId,
                promotionStartGameDay = input.promotionStartGameDay,
                promotionEndGameDayExclusive = input.promotionEndGameDayExclusive,
                discountBasisPoints = input.discountBasisPoints,
                promotionReasonCode = input.promotionReasonCode,
                promotionReasonText = input.promotionReasonText,
                quotedLeadTimeGameHours = input.quotedLeadTimeGameHours,
                sourceMarketRevision = input.sourceMarketRevision,
                sourceCommercialRevision = input.sourceCommercialRevision
            });
        }
        return clone;
    }

    private void Check(bool condition, string description)
    {
        if (condition)
        {
            passed++;
            results.Add("[OK] " + description);
        }
        else
        {
            failed++;
            results.Add("[FALLO] " + description);
        }
    }
}
#endif
