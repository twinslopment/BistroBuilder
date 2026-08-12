#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23GRuntimeFunctionalTestWindow : EditorWindow
{
    private readonly List<string> results = new List<string>();
    private Vector2 scroll;
    private int passed;
    private int failed;
    private int runtimeErrors;
    private bool running;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3G - Prueba funcional runtime")]
    public static void Open()
    {
        BistroBuilderSuppliers23GRuntimeFunctionalTestWindow window = GetWindow<BistroBuilderSuppliers23GRuntimeFunctionalTestWindow>("Prueba runtime 2.3G");
        window.minSize = new Vector2(940f, 680f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("PRUEBA FUNCIONAL RUNTIME 2.3G", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Debe ejecutarse en Play Mode. Respalda 2.3C/2.3D/2.3E/2.3G, crea y confirma un PurchaseOrder real, verifica planificación, retraso/fiabilidad, DispatchTicket e InDelivery y restaura todo al finalizar.",
            MessageType.Info);
        GUI.enabled = EditorApplication.isPlaying && !running;
        if (GUILayout.Button("Ejecutar prueba completa", GUILayout.Height(34f))) Run();
        GUI.enabled = true;
        EditorGUILayout.LabelField("Correctos: " + passed + "  Fallos: " + failed + "  Errores/Excepciones/Asserts: " + runtimeErrors, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < results.Count; index++) EditorGUILayout.LabelField(results[index], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        results.Clear(); passed = 0; failed = 0; runtimeErrors = 0; running = true;
        Application.logMessageReceived += HandleLog;

        BistroBuilderSupplierMarketService market = null;
        BistroBuilderSupplierCommercialIntelligenceService commercial = null;
        BistroBuilderSupplierPurchaseOrderService orders = null;
        BistroBuilderSupplierLogisticsService logistics = null;
        BistroBuilderSupplierMarketSnapshot backupMarket = null;
        BistroBuilderSupplierCommercialIntelligenceSnapshot backupCommercial = null;
        BistroBuilderSupplierPurchaseOrdersSnapshot backupOrders = null;
        BistroBuilderSupplierLogisticsSnapshot backupLogistics = null;

        try
        {
            BistroBuilderSupplierMarketService[] marketServices = UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierMarketService>(FindObjectsSortMode.None);
            BistroBuilderSupplierCommercialIntelligenceService[] commercialServices = UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierCommercialIntelligenceService>(FindObjectsSortMode.None);
            BistroBuilderSupplierPurchaseOrderService[] orderServices = UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierPurchaseOrderService>(FindObjectsSortMode.None);
            BistroBuilderSupplierLogisticsService[] logisticsServices = UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierLogisticsService>(FindObjectsSortMode.None);
            Check(marketServices.Length == 1, "Existe exactamente una autoridad runtime 2.3C.");
            Check(commercialServices.Length == 1, "Existe exactamente una autoridad runtime 2.3D.");
            Check(orderServices.Length == 1, "Existe exactamente una autoridad runtime 2.3E.");
            Check(logisticsServices.Length == 1, "Existe exactamente una autoridad runtime 2.3G.");
            if (marketServices.Length != 1 || commercialServices.Length != 1 || orderServices.Length != 1 || logisticsServices.Length != 1) return;
            market = marketServices[0]; commercial = commercialServices[0]; orders = orderServices[0]; logistics = logisticsServices[0];
            Check(market.IsInitialized, "2.3C está inicializado.");
            Check(commercial.IsInitialized, "2.3D está inicializado.");
            Check(orders.IsInitialized, "2.3E está inicializado.");
            Check(logistics.IsInitialized, "2.3G está inicializado.");
            Check(string.IsNullOrEmpty(logistics.LastInitializationError), "2.3G no conserva error residual.");

            backupMarket = market.CreateSnapshot();
            backupCommercial = commercial.CreateSnapshot();
            backupOrders = orders.CreateSnapshot();
            backupLogistics = logistics.CreateSnapshot();
            Check(backupMarket != null, "Snapshot original 2.3C capturado.");
            Check(backupCommercial != null, "Snapshot original 2.3D capturado.");
            Check(backupOrders != null, "Snapshot original 2.3E capturado.");
            Check(backupLogistics != null, "Snapshot original 2.3G capturado.");
            if (backupMarket == null || backupCommercial == null || backupOrders == null || backupLogistics == null) return;

            BistroBuilderSupplierAuthoringDatabase suppliers = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSupplierLogisticsService.SupplierAuthoringResourcePath);
            BistroBuilderSupplierLogisticsPlanningSettings settings = Resources.Load<BistroBuilderSupplierLogisticsPlanningSettings>(BistroBuilderSupplierLogisticsService.SettingsResourcePath);
            Check(suppliers != null, "Runtime localiza supplier.authoring.");
            Check(settings != null, "Runtime localiza supplier.logistics.settings.");
            if (suppliers == null || settings == null) return;
            Check(settings.SchemaVersion == 1, "Runtime consume schema v1 de supplier.logistics.settings.");

            string error;
            Check(orders.TryInitializeFresh(), "2.3E inicia estado controlado vacío para 2.3G.");
            Check(logistics.TryInitializeFresh(), "2.3G inicia estado controlado vacío.");
            Check(orders.OrderCount == 0, "No hay PurchaseOrder ficticios al iniciar la prueba.");
            Check(logistics.PlanCount == 0, "No hay LogisticsPlan ficticios al iniciar la prueba.");
            Check(logistics.LogisticsSeed != 0UL, "2.3G genera semilla logística determinista no nula.");

            BistroBuilderSupplierAuthoringRecord supplier = null;
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = null;
            BistroBuilderSupplierCommercialQuote quote = null;
            for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count && supplier == null; supplierIndex++)
            {
                BistroBuilderSupplierAuthoringRecord candidate = suppliers.Suppliers[supplierIndex];
                if (candidate == null || !candidate.isActive || candidate.baseOffers == null) continue;
                for (int offerIndex = 0; offerIndex < candidate.baseOffers.Count; offerIndex++)
                {
                    BistroBuilderSupplierBaseOfferAuthoringRecord candidateOffer = candidate.baseOffers[offerIndex];
                    BistroBuilderSupplierCommercialQuote candidateQuote;
                    if (candidateOffer != null && candidateOffer.isActive &&
                        commercial.TryGetCommercialQuote(candidateOffer.SupplierOfferId, out candidateQuote) && candidateQuote != null && candidateQuote.availableForNewOrders)
                    {
                        supplier = candidate;
                        offer = candidateOffer;
                        quote = candidateQuote;
                        break;
                    }
                }
            }
            Check(supplier != null && offer != null && quote != null, "Se localiza una oferta real comprable para la prueba 2.3G.");
            if (supplier == null || offer == null || quote == null) return;

            BistroBuilderPurchaseOrderRecord draft;
            Check(orders.TryCreateDraft(supplier.SupplierId, out draft, out error), "Se crea PurchaseOrder Draft real.");
            int minimum = Math.Max(1, offer.minimumPackageCount);
            int increment = Math.Max(1, offer.orderIncrement);
            long target = Math.Max(1L, supplier.minimumOrderValueCents);
            long raw = (target + quote.effectivePriceCents - 1L) / quote.effectivePriceCents;
            int packages = raw > int.MaxValue ? int.MaxValue : (int)Math.Max(minimum, raw);
            if (packages > minimum)
            {
                int remainder = (packages - minimum) % increment;
                if (remainder != 0) packages += increment - remainder;
            }
            BistroBuilderPurchaseOrderRecord edited;
            Check(orders.TrySetDraftLine(draft.purchaseOrderId, offer.SupplierOfferId, packages, out edited, out error), "Se añade línea real respetando mínimo/incremento.");
            BistroBuilderPurchaseOrderConfirmationPreview preview;
            Check(orders.TryBuildConfirmationPreview(draft.purchaseOrderId, out preview, out error), "2.3E construye preview real antes de logística.");
            Check(preview != null && preview.canConfirm, "El pedido de prueba es confirmable.");
            BistroBuilderPurchaseOrderConfirmationReceipt receipt;
            Check(orders.TryConfirmOrder(draft.purchaseOrderId, out receipt, out error), "PurchaseOrder se confirma con cotización real.");
            Check(receipt != null && receipt.purchaseOrderId == draft.purchaseOrderId, "Receipt de confirmación conserva PurchaseOrderId.");

            BistroBuilderSupplierLogisticsPlanRecord plan;
            if (!logistics.TryGetPlanByOrder(draft.purchaseOrderId, out plan))
            {
                Check(logistics.TryCreatePlanForOrder(draft.purchaseOrderId, out plan, out error), "2.3G crea plan si el evento automático no lo hizo.");
            }
            else Check(true, "2.3G crea automáticamente el plan al confirmar.");
            Check(plan != null, "Plan logístico real disponible.");
            if (plan == null) return;
            Check(!string.IsNullOrWhiteSpace(plan.logisticsPlanId), "LogisticsPlanId estable no vacío.");
            Check(plan.purchaseOrderId == draft.purchaseOrderId, "Plan conserva FK al PurchaseOrder real.");
            Check(plan.plannedDeliveryGameDay >= orders.CurrentGameDay, "Fecha planificada no queda en el pasado.");
            Check(plan.windowStartMinuteOfDay >= 0 && plan.windowEndMinuteOfDay > plan.windowStartMinuteOfDay, "Ventana runtime válida.");
            Check(plan.reliabilityValue >= 0f && plan.reliabilityValue <= 1f, "Fiabilidad congelada acotada 0..1.");
            Check(plan.delayProbabilityBasisPoints > 0 && plan.delayProbabilityBasisPoints <= 10000, "Probabilidad de retraso runtime válida.");
            Check(plan.logisticsLoadUnits > 0 && plan.visualLoadUnits > 0, "Carga logística runtime positiva.");
            Check(plan.suggestedTripCount >= 1 && plan.suggestedTripCount <= 3, "2.3H recibirá 1..3 viajes visuales.");

            BistroBuilderPurchaseOrderRecord pending;
            Check(orders.TryGetOrder(draft.purchaseOrderId, out pending) && pending.status == BistroBuilderPurchaseOrderStatus.PendingDelivery, "Confirmado + plan pasa realmente a PendingDelivery.");
            Check(pending.logisticsPlanId == plan.logisticsPlanId, "PurchaseOrder conserva LogisticsPlanId exacto.");

            int targetDay = Math.Max(market.CurrentGameDay, plan.basePlannedDeliveryGameDay);
            Check(market.TryAdvanceToGameDay(targetDay, out error), "2.3C avanza al día base de entrega sin error.");
            Check(commercial.TrySynchronizeCurrentMarketState(out error), "2.3D sincroniza tras el avance temporal.");
            Check(logistics.TryAdvanceToGameDay(targetDay, out error), "2.3G evalúa fiabilidad/retraso al llegar el día previsto.");
            Check(logistics.TryGetPlanByOrder(draft.purchaseOrderId, out plan), "Plan sigue existiendo tras evaluar fiabilidad.");
            Check(plan != null && (plan.decidedDelayGameMinutes == 0 || plan.delayApplied), "Un retraso decidido se aplica; un plan puntual permanece sin retraso.");
            Check(plan != null && !plan.IsTerminal, "La fiabilidad nunca hace desaparecer el pedido.");

            if (plan != null && plan.plannedDeliveryGameDay > market.CurrentGameDay)
            {
                targetDay = plan.plannedDeliveryGameDay;
                Check(market.TryAdvanceToGameDay(targetDay, out error), "2.3C avanza hasta la fecha replanificada.");
                Check(commercial.TrySynchronizeCurrentMarketState(out error), "2.3D sincroniza tras replanificación.");
                Check(logistics.TryAdvanceToGameDay(targetDay, out error), "2.3G alcanza ReadyForDispatch tras la replanificación.");
                logistics.TryGetPlanByOrder(draft.purchaseOrderId, out plan);
            }
            Check(plan != null && plan.status == BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch, "Plan queda ReadyForDispatch en la fecha efectiva.");

            BistroBuilderSupplierDispatchTicket ticket;
            Check(logistics.TryBuildDispatchTicket(draft.purchaseOrderId, out ticket, out error), "2.3G genera DispatchTicket para 2.3H.");
            Check(ticket != null && ticket.logisticsPlanId == plan.logisticsPlanId, "DispatchTicket conserva LogisticsPlanId.");
            Check(ticket != null && ticket.suggestedTripCount >= 1 && ticket.suggestedTripCount <= 3, "DispatchTicket conserva viajes visuales 1..3.");
            Check(ticket != null && (ticket.vehicle == BistroBuilderSupplierVehiclePreference.Furgoneta || ticket.vehicle == BistroBuilderSupplierVehiclePreference.CamionLigero), "DispatchTicket resuelve vehículo físico futuro.");

            BistroBuilderSupplierDispatchTicket dispatchedTicket;
            Check(logistics.TryDispatch(draft.purchaseOrderId, out dispatchedTicket, out error), "2.3G inicia reparto mediante el contrato que consumirá 2.3H.");
            BistroBuilderPurchaseOrderRecord inDelivery;
            Check(orders.TryGetOrder(draft.purchaseOrderId, out inDelivery) && inDelivery.status == BistroBuilderPurchaseOrderStatus.InDelivery, "Dispatch real transiciona PurchaseOrder a InDelivery.");
            Check(inDelivery.appliedDelayGameMinutes == (plan.delayApplied ? plan.decidedDelayGameMinutes : 0), "InDelivery congela el retraso realmente aplicado.");
            BistroBuilderPurchaseOrderRecord cancelled;
            Check(!orders.TryCancelOrder(draft.purchaseOrderId, "No debe cancelarse", out cancelled, out error), "Un pedido InDelivery no puede cancelarse.");
            Check(logistics.TryGetPlanByOrder(draft.purchaseOrderId, out plan) && plan.status == BistroBuilderSupplierLogisticsPlanStatus.Dispatched, "Plan runtime queda Dispatched.");

            BistroBuilderSupplierLogisticsSnapshot testSnapshot = logistics.CreateSnapshot();
            Check(testSnapshot != null && testSnapshot.plans.Count == 1, "Snapshot 2.3G captura el plan real.");
            Check(BistroBuilderSupplierLogisticsPlanningEngine.ValidateSnapshot(testSnapshot, out error), "Snapshot runtime 2.3G supera validación de dominio.");
            Check(runtimeErrors == 0, "La ejecución no ha capturado Error/Exception/Assert hasta este punto.");
        }
        catch (Exception exception)
        {
            failed++;
            results.Add("[EXCEPCIÓN TEST] " + exception.GetType().Name + ": " + exception.Message);
        }
        finally
        {
            string restoreError;
            if (market != null && backupMarket != null) Check(market.TryRestoreSnapshot(backupMarket, out restoreError), "Se restaura snapshot original 2.3C.");
            if (commercial != null && backupCommercial != null) Check(commercial.TryRestoreSnapshot(backupCommercial, out restoreError), "Se restaura snapshot original 2.3D.");
            if (orders != null && backupOrders != null) Check(orders.TryRestoreSnapshot(backupOrders, out restoreError), "Se restaura snapshot original 2.3E.");
            if (logistics != null && backupLogistics != null) Check(logistics.TryRestoreSnapshot(backupLogistics, out restoreError), "Se restaura snapshot original 2.3G.");
            Application.logMessageReceived -= HandleLog;
            running = false;
            Repaint();
        }
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors++;
    }

    private void Check(bool condition, string message)
    {
        if (condition) { passed++; results.Add("[OK] " + message); }
        else { failed++; results.Add("[FALLO] " + message); }
    }
}
#endif
