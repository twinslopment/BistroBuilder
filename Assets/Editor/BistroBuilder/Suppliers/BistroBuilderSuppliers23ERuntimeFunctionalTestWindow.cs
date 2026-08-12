#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23ERuntimeFunctionalTestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;
    private int runtimeErrors;
    private bool running;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3E - Prueba funcional runtime")]
    public static void Open()
    {
        BistroBuilderSuppliers23ERuntimeFunctionalTestWindow window =
            GetWindow<BistroBuilderSuppliers23ERuntimeFunctionalTestWindow>("Prueba runtime 2.3E");
        window.minSize = new Vector2(920f, 640f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("PRUEBA FUNCIONAL RUNTIME 2.3E", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Debe ejecutarse en Play Mode. Respalda 2.3C + 2.3D + 2.3E, crea pedidos controlados, " +
            "confirma con cotización real, verifica freeze comercial y ciclo de estados, y restaura todo al finalizar.",
            MessageType.Info);
        GUI.enabled = EditorApplication.isPlaying && !running;
        if (GUILayout.Button("Ejecutar prueba completa", GUILayout.Height(34f))) RunTest();
        GUI.enabled = true;
        EditorGUILayout.LabelField(
            "Correctos: " + passed + "  Fallos: " + failed +
            "  Errores/Excepciones/Asserts: " + runtimeErrors,
            EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < results.Count; index++)
            EditorGUILayout.LabelField(results[index], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void RunTest()
    {
        results.Clear();
        passed = 0;
        failed = 0;
        runtimeErrors = 0;
        running = true;
        Application.logMessageReceived += HandleLog;

        BistroBuilderSupplierMarketService marketService = null;
        BistroBuilderSupplierCommercialIntelligenceService commercialService = null;
        BistroBuilderSupplierPurchaseOrderService orderService = null;
        BistroBuilderSupplierMarketSnapshot originalMarket = null;
        BistroBuilderSupplierCommercialIntelligenceSnapshot originalCommercial = null;
        BistroBuilderSupplierPurchaseOrdersSnapshot originalOrders = null;
        Action<BistroBuilderPurchaseOrderRecord> createdHandler = null;
        Action<BistroBuilderPurchaseOrderRecord> changedHandler = null;
        Action<BistroBuilderPurchaseOrderConfirmationReceipt> confirmedHandler = null;
        Action<BistroBuilderPurchaseOrderRecord> stateHandler = null;
        Action<BistroBuilderPurchaseOrderRecord> cancelledHandler = null;

        try
        {
            BistroBuilderSupplierMarketService[] markets =
                UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierMarketService>(FindObjectsSortMode.None);
            BistroBuilderSupplierCommercialIntelligenceService[] commercials =
                UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierCommercialIntelligenceService>(FindObjectsSortMode.None);
            BistroBuilderSupplierPurchaseOrderService[] orders =
                UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierPurchaseOrderService>(FindObjectsSortMode.None);

            Check(markets.Length == 1, "Existe exactamente una autoridad runtime de mercado 2.3C.");
            Check(commercials.Length == 1, "Existe exactamente una autoridad runtime comercial 2.3D.");
            Check(orders.Length == 1, "Existe exactamente una autoridad runtime PurchaseOrder 2.3E.");
            if (markets.Length != 1 || commercials.Length != 1 || orders.Length != 1) return;

            marketService = markets[0];
            commercialService = commercials[0];
            orderService = orders[0];
            Check(marketService.IsInitialized, "2.3C está inicializado.");
            Check(commercialService.IsInitialized, "2.3D está inicializado.");
            Check(orderService.IsInitialized, "2.3E está inicializado.");
            Check(string.IsNullOrEmpty(orderService.LastInitializationError), "2.3E no conserva error residual de inicialización.");

            originalMarket = marketService.CreateSnapshot();
            originalCommercial = commercialService.CreateSnapshot();
            originalOrders = orderService.CreateSnapshot();
            Check(originalMarket != null, "Se captura snapshot original de 2.3C.");
            Check(originalCommercial != null, "Se captura snapshot original de 2.3D.");
            Check(originalOrders != null, "Se captura snapshot original de 2.3E.");
            if (originalMarket == null || originalCommercial == null || originalOrders == null) return;

            BistroBuilderSupplierAuthoringDatabase suppliers =
                Resources.Load<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSupplierPurchaseOrderService.SupplierAuthoringResourcePath);
            BistroBuilderIngredientAuthoringDatabase ingredients =
                Resources.Load<BistroBuilderIngredientAuthoringDatabase>(BistroBuilderSupplierPurchaseOrderService.IngredientAuthoringResourcePath);
            BistroBuilderSupplierMarketSettings marketSettings =
                Resources.Load<BistroBuilderSupplierMarketSettings>(BistroBuilderSupplierMarketService.MarketSettingsResourcePath);
            BistroBuilderSupplierCommercialIntelligenceSettings commercialSettings =
                Resources.Load<BistroBuilderSupplierCommercialIntelligenceSettings>(BistroBuilderSupplierCommercialIntelligenceService.SettingsResourcePath);
            BistroBuilderSupplierPurchaseOrderSettings orderSettings =
                Resources.Load<BistroBuilderSupplierPurchaseOrderSettings>(BistroBuilderSupplierPurchaseOrderService.SettingsResourcePath);

            Check(suppliers != null, "Runtime localiza supplier.authoring.");
            Check(ingredients != null, "Runtime localiza ingredient.authoring.");
            Check(marketSettings != null, "Runtime localiza supplier.market.settings.");
            Check(commercialSettings != null, "Runtime localiza supplier.commercial.settings.");
            Check(orderSettings != null, "Runtime localiza supplier.orders.settings.");
            if (suppliers == null || ingredients == null || marketSettings == null || commercialSettings == null || orderSettings == null) return;
            Check(orderSettings.SchemaVersion == 1, "Runtime consume schema v1 de supplier.orders.settings.");
            Check(marketSettings.ReviewEveryGameDays == 5, "2.3E cotiza sobre el ciclo cerrado de cinco días.");

            int supplierRevisionBefore = suppliers.ContentRevision;
            int ingredientRevisionBefore = ingredients.ContentRevision;
            ulong marketSeed = BistroBuilderSupplierMarketEngine.StableSeedFromText("runtime-functional-23e-market", marketSettings.DeterministicSalt);
            ulong commercialSeed = BistroBuilderSupplierMarketEngine.StableSeedFromText("runtime-functional-23e-commercial", commercialSettings.DeterministicSalt);
            string error;
            Check(marketService.TryInitializeFresh(marketSeed), "2.3C acepta semilla controlada para 2.3E.");
            Check(commercialService.TryInitializeFresh(commercialSeed), "2.3D acepta semilla comercial controlada para 2.3E.");
            Check(orderService.TryInitializeFresh(), "2.3E inicia un estado de pedidos controlado y vacío.");
            Check(orderService.OrderCount == 0, "El estado controlado empieza sin pedidos ficticios.");
            Check(orderService.SourceMarketSeed == marketSeed, "2.3E queda vinculado a la semilla real de mercado 2.3C.");
            Check(orderService.SourceCommercialSeed == commercialSeed, "2.3E queda vinculado a la semilla real del Motor Comercial 2.3D.");

            int createdEvents = 0;
            int changedEvents = 0;
            int confirmedEvents = 0;
            int stateEvents = 0;
            int cancelledEvents = 0;
            createdHandler = delegate { createdEvents++; };
            changedHandler = delegate { changedEvents++; };
            confirmedHandler = delegate { confirmedEvents++; };
            stateHandler = delegate { stateEvents++; };
            cancelledHandler = delegate { cancelledEvents++; };
            orderService.OrderCreated += createdHandler;
            orderService.OrderChanged += changedHandler;
            orderService.OrderConfirmed += confirmedHandler;
            orderService.OrderStateChanged += stateHandler;
            orderService.OrderCancelled += cancelledHandler;

            // Buscar una promoción real para comprobar freeze de precio/PromotionId.
            BistroBuilderSupplierPromotionRecord promotion = null;
            bool marketAdvanceOk = true;
            bool commercialSyncOk = true;
            for (int day = 2; day <= 120 && promotion == null; day++)
            {
                if (!marketService.TryAdvanceToGameDay(day, out error))
                {
                    marketAdvanceOk = false;
                    break;
                }
                if (!commercialService.TrySynchronizeCurrentMarketState(out error))
                {
                    commercialSyncOk = false;
                    break;
                }
                if (commercialService.ActivePromotionCount > 0)
                {
                    List<BistroBuilderSupplierPromotionRecord> active = new List<BistroBuilderSupplierPromotionRecord>();
                    commercialService.CopyActivePromotions(active);
                    for (int index = 0; index < active.Count; index++)
                    {
                        BistroBuilderSupplierCommercialQuote activeQuote;
                        if (active[index] != null && commercialService.TryGetCommercialQuote(active[index].supplierOfferId, out activeQuote) &&
                            activeQuote != null && activeQuote.availableForNewOrders)
                        {
                            promotion = active[index];
                            break;
                        }
                    }
                }
            }
            Check(marketAdvanceOk, "2.3C avanza sin error durante la búsqueda controlada de promoción.");
            Check(commercialSyncOk, "2.3D sincroniza sin error durante la búsqueda controlada de promoción.");
            Check(promotion != null, "La simulación controlada encuentra una promoción real disponible para nuevos pedidos.");
            if (promotion == null) return;

            BistroBuilderSupplierAuthoringRecord supplier;
            Check(suppliers.TryGetSupplier(promotion.supplierId, out supplier) && supplier != null, "PromotionId resuelve proveedor de autoría.");
            BistroBuilderSupplierBaseOfferAuthoringRecord promotedOffer;
            Check(BistroBuilderSupplierPurchaseOrderEngine.TryFindActiveOffer(supplier, promotion.supplierOfferId, out promotedOffer), "PromotionId resuelve oferta base activa.");
            BistroBuilderSupplierCommercialQuote quote;
            Check(commercialService.TryGetCommercialQuote(promotion.supplierOfferId, out quote) && quote != null, "2.3D entrega cotización comercial real de la promoción.");
            Check(quote.hasActivePromotion && quote.promotionId == promotion.promotionId, "Cotización real conserva PromotionId activo.");
            Check(quote.effectivePriceCents <= quote.marketPriceCents && quote.effectivePriceCents > 0L, "Precio efectivo promocional es válido.");

            BistroBuilderPurchaseOrderRecord draft;
            Check(orderService.TryCreateDraft(supplier.SupplierId, out draft, out error), "2.3E crea Draft para el proveedor promocionado.");
            Check(draft.status == BistroBuilderPurchaseOrderStatus.Draft, "Pedido runtime nace en Draft.");
            Check(createdEvents == 1, "Crear Draft publica un único evento OrderCreated.");

            int minPackages = Math.Max(1, promotedOffer.minimumPackageCount);
            int increment = Math.Max(1, promotedOffer.orderIncrement);
            long target = Math.Max(1L, supplier.minimumOrderValueCents);
            long raw = (target + quote.effectivePriceCents - 1L) / quote.effectivePriceCents;
            int packages = raw > int.MaxValue ? int.MaxValue : (int)Math.Max(minPackages, raw);
            if (packages > minPackages)
            {
                int remainder = (packages - minPackages) % increment;
                if (remainder != 0) packages += increment - remainder;
            }

            BistroBuilderPurchaseOrderRecord edited;
            Check(orderService.TrySetDraftLine(draft.purchaseOrderId, promotedOffer.SupplierOfferId, packages, out edited, out error), "Se añade línea promocionada respetando mínimo/incremento.");
            Check(edited.draftLines.Count == 1, "El Draft contiene exactamente una línea.");
            Check(changedEvents == 1, "Editar Draft publica un evento OrderChanged.");

            BistroBuilderPurchaseOrderConfirmationPreview preview;
            Check(orderService.TryBuildConfirmationPreview(draft.purchaseOrderId, out preview, out error), "2.3E construye preview usando 2.3D real.");
            Check(preview.canConfirm, "El preview real es confirmable.");
            Check(preview.minimumOrderSatisfied, "El pedido real satisface el mínimo económico.");
            Check(preview.lines.Count == 1 && preview.lines[0].hadActivePromotion, "Preview conserva la promoción activa.");
            Check(preview.lines[0].promotionId == promotion.promotionId, "Preview conserva PromotionId exacto.");
            Check(preview.lines[0].effectiveUnitPriceCents == quote.effectivePriceCents, "Preview usa precio efectivo de 2.3D.");
            Check(preview.totalCents == preview.subtotalCents + preview.shippingCostCents, "Preview calcula total exacto con portes.");

            BistroBuilderPurchaseOrderConfirmationReceipt receipt;
            Check(orderService.TryConfirmOrder(draft.purchaseOrderId, out receipt, out error), "2.3E confirma el pedido con cotización real.");
            Check(receipt != null && receipt.status == BistroBuilderPurchaseOrderStatus.Confirmed, "Confirmación devuelve receipt compacto en estado Confirmed.");
            Check(confirmedEvents == 1, "Confirmar publica un único OrderConfirmed.");
            Check(stateEvents == 1, "Confirmar publica una transición de estado.");

            BistroBuilderPurchaseOrderRecord confirmed;
            Check(orderService.TryGetOrder(draft.purchaseOrderId, out confirmed), "El PurchaseOrder confirmado se consulta por ID.");
            Check(confirmed.confirmedLines.Count == 1 && confirmed.draftLines.Count == 0, "Pedido confirmado solo conserva snapshot comercial congelado.");
            long frozenPrice = confirmed.confirmedLines[0].effectiveUnitPriceCents;
            string frozenPromotion = confirmed.confirmedLines[0].promotionId;
            long frozenTotal = confirmed.totalCents;
            Check(frozenPrice == quote.effectivePriceCents, "Precio confirmado coincide con cotización efectiva.");
            Check(frozenPromotion == promotion.promotionId, "PromotionId confirmado queda trazable.");
            Check(confirmed.confirmedLines[0].promotionStartGameDay == promotion.startGameDay && confirmed.confirmedLines[0].promotionEndGameDayExclusive == promotion.endGameDayExclusive, "Ventana temporal promocional queda congelada en el pedido.");
            Check(confirmed.supplierTerms != null && confirmed.supplierTerms.supplierId == supplier.SupplierId, "Condiciones del proveedor quedan congeladas.");
            Check(confirmed.sourceMarketRevision > 0L && confirmed.sourceCommercialRevision > 0L, "Pedido registra revisiones origen de mercado/comercial.");
            long revisionAfterConfirm = orderService.OrdersRevision;
            BistroBuilderPurchaseOrderConfirmationReceipt repeatedReceipt;
            Check(orderService.TryConfirmOrder(draft.purchaseOrderId, out repeatedReceipt, out error), "Reconfirmar el mismo PurchaseOrder es idempotente en el servicio runtime.");
            Check(orderService.OrdersRevision == revisionAfterConfirm, "Reconfirmación runtime no incrementa OrdersRevision.");
            Check(confirmedEvents == 1 && repeatedReceipt != null && repeatedReceipt.totalCents == receipt.totalCents, "Reconfirmación no duplica evento y devuelve el mismo compromiso económico.");

            // Disponibilidad: TemporalmenteAgotado bloquea pedidos NUEVOS, nunca invalida el ya confirmado.
            BistroBuilderPurchaseOrderRecord outageDraft;
            Check(orderService.TryCreateDraft(supplier.SupplierId, out outageDraft, out error), "Se crea Draft independiente para probar TemporalmenteAgotado.");
            BistroBuilderPurchaseOrderRecord outageEdited;
            Check(orderService.TrySetDraftLine(outageDraft.purchaseOrderId, promotedOffer.SupplierOfferId, packages, out outageEdited, out error), "El Draft de disponibilidad acepta la misma oferta antes del corte.");
            BistroBuilderSupplierMarketSnapshot beforeOutageMarket = marketService.CreateSnapshot();
            BistroBuilderSupplierCommercialIntelligenceSnapshot beforeOutageCommercial = commercialService.CreateSnapshot();
            BistroBuilderSupplierMarketSnapshot outageMarket = beforeOutageMarket != null ? beforeOutageMarket.DeepClone() : null;
            bool outageStateFound = false;
            if (outageMarket != null && outageMarket.offerStates != null)
            {
                for (int stateIndex = 0; stateIndex < outageMarket.offerStates.Count; stateIndex++)
                {
                    BistroBuilderSupplierMarketOfferState offerState = outageMarket.offerStates[stateIndex];
                    if (offerState != null && string.Equals(offerState.supplierOfferId, promotedOffer.SupplierOfferId, StringComparison.Ordinal))
                    {
                        offerState.availability = BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado;
                        outageStateFound = true;
                        break;
                    }
                }
            }
            Check(outageStateFound, "La prueba localiza el estado de mercado de la oferta para simular agotamiento.");
            Check(outageStateFound && marketService.TryRestoreSnapshot(outageMarket, out error), "2.3C acepta snapshot controlado con TemporalmenteAgotado.");
            BistroBuilderPurchaseOrderConfirmationPreview outagePreview;
            Check(orderService.TryBuildConfirmationPreview(outageDraft.purchaseOrderId, out outagePreview, out error), "2.3E construye preview aunque la oferta esté temporalmente agotada.");
            Check(outagePreview != null && !outagePreview.canConfirm, "TemporalmenteAgotado bloquea la confirmación de un pedido nuevo.");
            BistroBuilderPurchaseOrderConfirmationReceipt outageReceipt;
            Check(!orderService.TryConfirmOrder(outageDraft.purchaseOrderId, out outageReceipt, out error), "TryConfirmOrder rechaza el nuevo pedido agotado.");
            BistroBuilderPurchaseOrderRecord stillConfirmed;
            Check(orderService.TryGetOrder(draft.purchaseOrderId, out stillConfirmed) && stillConfirmed.status == BistroBuilderPurchaseOrderStatus.Confirmed && stillConfirmed.totalCents == frozenTotal, "El agotamiento posterior no invalida ni recalcula el pedido ya confirmado.");
            Check(beforeOutageMarket != null && marketService.TryRestoreSnapshot(beforeOutageMarket, out error), "Se restaura el mercado tras la prueba de agotamiento.");
            Check(beforeOutageCommercial != null && commercialService.TryRestoreSnapshot(beforeOutageCommercial, out error), "Se restaura 2.3D tras la prueba de agotamiento.");
            BistroBuilderPurchaseOrderRecord outageCancelled;
            Check(orderService.TryCancelOrder(outageDraft.purchaseOrderId, "Fin prueba de disponibilidad", out outageCancelled, out error), "El Draft de disponibilidad se cancela limpiamente.");

            // El mercado y la promo pueden cambiar; el pedido confirmado no.
            int advanceTo = Math.Max(marketService.CurrentGameDay + 10, promotion.endGameDayExclusive + 5);
            Check(marketService.TryAdvanceToGameDay(advanceTo, out error), "Mercado avanza después de confirmar el pedido.");
            Check(commercialService.TrySynchronizeCurrentMarketState(out error), "2.3D sincroniza después de cambiar mercado/promociones.");
            BistroBuilderPurchaseOrderRecord confirmedAfterMarket;
            Check(orderService.TryGetOrder(draft.purchaseOrderId, out confirmedAfterMarket), "Pedido sigue consultable tras cambios comerciales.");
            Check(confirmedAfterMarket.confirmedLines[0].effectiveUnitPriceCents == frozenPrice, "Precio confirmado no cambia al evolucionar el mercado.");
            Check(confirmedAfterMarket.confirmedLines[0].promotionId == frozenPromotion, "PromotionId confirmado no cambia al caducar/evolucionar la promoción.");
            Check(confirmedAfterMarket.totalCents == frozenTotal, "Total confirmado queda congelado.");

            // Contrato de estados para 2.3G/2.3H/2.2B.
            BistroBuilderPurchaseOrderRecord pending;
            Check(orderService.TryMarkPendingDelivery(draft.purchaseOrderId, "plan_runtime_23e", advanceTo + 1, 480, 720, out pending, out error), "Confirmed → PendingDelivery mediante contrato 2.3G.");
            Check(pending.status == BistroBuilderPurchaseOrderStatus.PendingDelivery, "Estado PendingDelivery runtime correcto.");
            int stateEventsAfterPending = stateEvents;
            BistroBuilderPurchaseOrderRecord repeatedPending;
            Check(orderService.TryMarkPendingDelivery(draft.purchaseOrderId, "plan_runtime_23e", advanceTo + 1, 480, 720, out repeatedPending, out error), "Repetir la misma transición PendingDelivery es idempotente.");
            Check(stateEvents == stateEventsAfterPending, "La transición PendingDelivery idempotente no duplica evento de estado.");
            BistroBuilderPurchaseOrderRecord conflictingPending;
            Check(!orderService.TryMarkPendingDelivery(draft.purchaseOrderId, "plan_conflictivo", advanceTo + 1, 480, 720, out conflictingPending, out error), "PendingDelivery no acepta sustituir silenciosamente LogisticsPlanId.");
            Check(stateEvents == stateEventsAfterPending, "Un plan PendingDelivery conflictivo no publica evento de estado.");
            BistroBuilderPurchaseOrderRecord replanned;
            Check(orderService.TryUpdatePendingDeliveryPlan(draft.purchaseOrderId, "plan_runtime_23e", advanceTo + 2, 540, 780, 60, out replanned, out error), "2.3G puede replanificar PendingDelivery antes de expedición.");
            Check(replanned.plannedDeliveryGameDay == advanceTo + 2 && replanned.plannedDelayGameMinutes == 60, "Replanificación runtime conserva retraso previsto.");
            Check(marketService.TryAdvanceToGameDay(advanceTo + 2, out error), "El reloj de mercado alcanza el día real de expedición.");
            Check(commercialService.TrySynchronizeCurrentMarketState(out error), "2.3D permanece sincronizado al iniciar la entrega.");
            BistroBuilderPurchaseOrderRecord inDelivery;
            Check(orderService.TryMarkInDelivery(draft.purchaseOrderId, advanceTo + 2, 60, out inDelivery, out error), "PendingDelivery → InDelivery mediante contrato logístico.");
            Check(inDelivery.status == BistroBuilderPurchaseOrderStatus.InDelivery, "Estado InDelivery runtime correcto.");
            int stateEventsAfterInDelivery = stateEvents;
            BistroBuilderPurchaseOrderRecord repeatedInDelivery;
            Check(orderService.TryMarkInDelivery(draft.purchaseOrderId, advanceTo + 2, 60, out repeatedInDelivery, out error), "Repetir InDelivery con los mismos datos es idempotente.");
            Check(stateEvents == stateEventsAfterInDelivery, "InDelivery idempotente no duplica evento de estado.");
            BistroBuilderPurchaseOrderRecord conflictingInDelivery;
            Check(!orderService.TryMarkInDelivery(draft.purchaseOrderId, advanceTo + 2, 90, out conflictingInDelivery, out error), "InDelivery no permite reescribir el retraso aplicado.");
            BistroBuilderPurchaseOrderRecord cancelledImpossible;
            Check(!orderService.TryCancelOrder(draft.purchaseOrderId, "Demasiado tarde", out cancelledImpossible, out error), "InDelivery bloquea cancelación real.");
            BistroBuilderPurchaseOrderRecord delivered;
            Check(orderService.TryMarkDelivered(draft.purchaseOrderId, "receipt_runtime_23e", advanceTo + 2, out delivered, out error), "InDelivery → Delivered exige ReceiptId 2.2B.");
            Check(delivered.status == BistroBuilderPurchaseOrderStatus.Delivered, "Estado Delivered runtime correcto.");
            Check(delivered.deliveryReceiptId == "receipt_runtime_23e", "ReceiptId de 2.2B queda trazable sin escribir inventario desde 2.3E.");
            int stateEventsAfterDelivered = stateEvents;
            BistroBuilderPurchaseOrderRecord repeatedDelivered;
            Check(orderService.TryMarkDelivered(draft.purchaseOrderId, "receipt_runtime_23e", advanceTo + 2, out repeatedDelivered, out error), "Repetir Delivered con el mismo ReceiptId es idempotente.");
            Check(stateEvents == stateEventsAfterDelivered, "Delivered idempotente no duplica evento de estado.");
            BistroBuilderPurchaseOrderRecord conflictingDelivered;
            Check(!orderService.TryMarkDelivered(draft.purchaseOrderId, "receipt_conflictivo", advanceTo + 2, out conflictingDelivered, out error), "Delivered no permite sustituir el ReceiptId de 2.2B.");

            // Cancelación legal antes de expedición.
            BistroBuilderPurchaseOrderRecord cancelDraft;
            Check(orderService.TryCreateDraft(supplier.SupplierId, out cancelDraft, out error), "Se crea segundo Draft runtime para cancelación.");
            BistroBuilderPurchaseOrderRecord cancelled;
            Check(orderService.TryCancelOrder(cancelDraft.purchaseOrderId, "Cancelación funcional 2.3E", out cancelled, out error), "Draft puede cancelarse antes de expedición.");
            Check(cancelled.status == BistroBuilderPurchaseOrderStatus.Cancelled, "Pedido cancelado entra en estado terminal Cancelled.");
            Check(cancelledEvents == 2, "Las dos cancelaciones válidas publican exactamente dos OrderCancelled.");
            BistroBuilderPurchaseOrderRecord repeatedCancelled;
            Check(orderService.TryCancelOrder(cancelDraft.purchaseOrderId, "Repetida", out repeatedCancelled, out error), "Repetir cancelación sobre Cancelled es idempotente.");
            Check(cancelledEvents == 2, "Cancelación idempotente no duplica OrderCancelled.");

            // Copias defensivas y snapshot.
            BistroBuilderPurchaseOrderRecord defensive;
            Check(orderService.TryGetOrder(draft.purchaseOrderId, out defensive), "TryGetOrder devuelve copia del pedido.");
            defensive.totalCents++;
            BistroBuilderPurchaseOrderRecord queriedAgain;
            Check(orderService.TryGetOrder(draft.purchaseOrderId, out queriedAgain) && queriedAgain.totalCents == frozenTotal, "TryGetOrder devuelve copia defensiva, no estado mutable.");
            BistroBuilderSupplierPurchaseOrdersSnapshot finalSnapshot = orderService.CreateSnapshot();
            Check(finalSnapshot != null && finalSnapshot.orders.Count == 3, "Snapshot runtime contiene exactamente los tres pedidos de prueba.");
            Check(finalSnapshot != null && finalSnapshot.sourceMarketSeed == marketSeed, "Snapshot 2.3E conserva la identidad de sesión 2.3C.");
            Check(finalSnapshot != null && finalSnapshot.sourceCommercialSeed == commercialSeed, "Snapshot 2.3E conserva la identidad de sesión 2.3D.");
            Check(BistroBuilderSupplierPurchaseOrderEngine.ValidateSnapshotAgainstAuthoring(finalSnapshot, suppliers, ingredients, orderSettings, out error), "Snapshot runtime converge con supplier/ingredient.authoring.");

            BistroBuilderSupplierPurchaseOrdersSnapshot wrongMarketSession = finalSnapshot.DeepClone();
            wrongMarketSession.sourceMarketSeed = marketSeed == ulong.MaxValue ? marketSeed - 1UL : marketSeed + 1UL;
            Check(!orderService.TryRestoreSnapshot(wrongMarketSession, out error), "2.3E rechaza restaurar pedidos sobre otra sesión de mercado.");
            Check(!string.IsNullOrWhiteSpace(error) && error.Contains("otra sesión"), "El rechazo por sesión de mercado explica la causa.");
            BistroBuilderSupplierPurchaseOrdersSnapshot wrongCommercialSession = finalSnapshot.DeepClone();
            wrongCommercialSession.sourceCommercialSeed = commercialSeed == ulong.MaxValue ? commercialSeed - 1UL : commercialSeed + 1UL;
            Check(!orderService.TryRestoreSnapshot(wrongCommercialSession, out error), "2.3E rechaza restaurar pedidos sobre otra sesión comercial.");
            Check(!string.IsNullOrWhiteSpace(error) && error.Contains("otra sesión"), "El rechazo por sesión comercial explica la causa.");
            BistroBuilderSupplierPurchaseOrdersSnapshot wrongMarketDay = finalSnapshot.DeepClone();
            wrongMarketDay.currentGameDay = finalSnapshot.currentGameDay + 1;
            Check(!orderService.TryRestoreSnapshot(wrongMarketDay, out error), "2.3E rechaza restaurar pedidos de otro día dentro de la misma sesión.");
            Check(!string.IsNullOrWhiteSpace(error) && error.Contains("otro día"), "El rechazo por día de sesión explica la causa.");

            string fingerprint = BistroBuilderSupplierPurchaseOrderEngine.BuildFingerprint(finalSnapshot);
            Check(!string.IsNullOrWhiteSpace(fingerprint) && fingerprint != "NULL", "Snapshot runtime genera fingerprint canónico.");
            Check(suppliers.ContentRevision == supplierRevisionBefore, "2.3E runtime no modifica supplier.authoring.");
            Check(ingredients.ContentRevision == ingredientRevisionBefore, "2.3E runtime no modifica ingredient.authoring.");

        }
        catch (Exception exception)
        {
            failed++;
            results.Add("[FALLO] Excepción de la prueba: " + exception);
        }
        finally
        {
            if (orderService != null)
            {
                if (createdHandler != null) orderService.OrderCreated -= createdHandler;
                if (changedHandler != null) orderService.OrderChanged -= changedHandler;
                if (confirmedHandler != null) orderService.OrderConfirmed -= confirmedHandler;
                if (stateHandler != null) orderService.OrderStateChanged -= stateHandler;
                if (cancelledHandler != null) orderService.OrderCancelled -= cancelledHandler;
            }
            if (marketService != null && originalMarket != null)
            {
                string restoreError;
                bool restored = marketService.TryRestoreSnapshot(originalMarket, out restoreError);
                Check(restored, "La prueba restaura el snapshot original de 2.3C.");
                if (!restored) results.Add("[FALLO] Restore 2.3C: " + restoreError);
            }
            if (commercialService != null && originalCommercial != null)
            {
                string restoreError;
                bool restored = commercialService.TryRestoreSnapshot(originalCommercial, out restoreError);
                Check(restored, "La prueba restaura el snapshot original de 2.3D.");
                if (!restored) results.Add("[FALLO] Restore 2.3D: " + restoreError);
            }
            if (orderService != null && originalOrders != null)
            {
                string restoreError;
                bool restored = orderService.TryRestoreSnapshot(originalOrders, out restoreError);
                Check(restored, "La prueba restaura el snapshot original de 2.3E.");
                if (!restored) results.Add("[FALLO] Restore 2.3E: " + restoreError);
            }
            Application.logMessageReceived -= HandleLog;
            Check(runtimeErrors == 0, "La ejecución termina sin Error, Exception ni Assert capturados por 2.3E.");
            running = false;
            Debug.Log(
                "PRUEBA FUNCIONAL RUNTIME 2.3E — " + passed + " superadas / " + failed +
                " fallidas / " + runtimeErrors + " Error-Exception-Assert.");
            Repaint();
        }
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (!running) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            runtimeErrors++;
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
