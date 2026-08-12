#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23HRuntimeFunctionalTestWindow : EditorWindow
{
    private readonly List<string> results = new List<string>();
    private readonly List<BistroBuilderSupplierDeliveryPresentationState> observedStates =
        new List<BistroBuilderSupplierDeliveryPresentationState>();
    private Vector2 scroll;
    private int passed;
    private int failed;
    private int runtimeErrors;
    private bool running;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3H - Prueba funcional runtime")]
    public static void Open()
    {
        BistroBuilderSuppliers23HRuntimeFunctionalTestWindow window = GetWindow<BistroBuilderSuppliers23HRuntimeFunctionalTestWindow>("Prueba runtime 2.3H");
        window.minSize = new Vector2(980f, 700f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("PRUEBA FUNCIONAL RUNTIME 2.3H", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Debe ejecutarse en Play Mode. Crea un pedido real, lo lleva por 2.3G hasta ReadyForDispatch, ejecuta la presentación física completa con anclajes temporales, verifica branding lateral/handoff y confirma que 2.3H NO marca Delivered ni escribe recepción.",
            MessageType.Info);
        GUI.enabled = EditorApplication.isPlaying && !running;
        if (GUILayout.Button("Ejecutar prueba completa", GUILayout.Height(34f))) Run();
        GUI.enabled = true;
        EditorGUILayout.LabelField("Correctos: " + passed + "  Fallos: " + failed + "  Errores/Excepciones/Asserts: " + runtimeErrors, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < results.Count; i++) EditorGUILayout.LabelField(results[i], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        results.Clear(); observedStates.Clear(); passed = 0; failed = 0; runtimeErrors = 0; running = true;
        Application.logMessageReceived += HandleLog;

        BistroBuilderSupplierMarketService market = null;
        BistroBuilderSupplierCommercialIntelligenceService commercial = null;
        BistroBuilderSupplierPurchaseOrderService orders = null;
        BistroBuilderSupplierLogisticsService logistics = null;
        BistroBuilderSupplierDeliveryPresentationService presentation = null;
        BistroBuilderSupplierMarketSnapshot backupMarket = null;
        BistroBuilderSupplierCommercialIntelligenceSnapshot backupCommercial = null;
        BistroBuilderSupplierPurchaseOrdersSnapshot backupOrders = null;
        BistroBuilderSupplierLogisticsSnapshot backupLogistics = null;
        BistroBuilderSupplierDeliveryPresentationSnapshot backupPresentation = null;
        BistroBuilderSupplierDeliverySceneAnchors originalAnchors = null;
        GameObject temporaryAnchorsRoot = null;

        try
        {
            BistroBuilderSupplierMarketService[] markets = UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierMarketService>(FindObjectsSortMode.None);
            BistroBuilderSupplierCommercialIntelligenceService[] commercials = UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierCommercialIntelligenceService>(FindObjectsSortMode.None);
            BistroBuilderSupplierPurchaseOrderService[] orderServices = UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierPurchaseOrderService>(FindObjectsSortMode.None);
            BistroBuilderSupplierLogisticsService[] logisticsServices = UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierLogisticsService>(FindObjectsSortMode.None);
            BistroBuilderSupplierDeliveryPresentationService[] presentations = UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierDeliveryPresentationService>(FindObjectsSortMode.None);
            Check(markets.Length == 1, "Existe exactamente una autoridad runtime 2.3C.");
            Check(commercials.Length == 1, "Existe exactamente una autoridad runtime 2.3D.");
            Check(orderServices.Length == 1, "Existe exactamente una autoridad runtime 2.3E.");
            Check(logisticsServices.Length == 1, "Existe exactamente una autoridad runtime 2.3G.");
            Check(presentations.Length == 1, "Existe exactamente una autoridad runtime 2.3H.");
            if (markets.Length != 1 || commercials.Length != 1 || orderServices.Length != 1 || logisticsServices.Length != 1 || presentations.Length != 1) return;

            market = markets[0]; commercial = commercials[0]; orders = orderServices[0]; logistics = logisticsServices[0]; presentation = presentations[0];
            Check(market.IsInitialized, "2.3C está inicializado.");
            Check(commercial.IsInitialized, "2.3D está inicializado.");
            Check(orders.IsInitialized, "2.3E está inicializado.");
            Check(logistics.IsInitialized, "2.3G está inicializado.");
            Check(presentation.IsInitialized, "2.3H está inicializado.");
            Check(string.IsNullOrEmpty(presentation.LastInitializationError), "2.3H no conserva error residual.");

            backupMarket = market.CreateSnapshot();
            backupCommercial = commercial.CreateSnapshot();
            backupOrders = orders.CreateSnapshot();
            backupLogistics = logistics.CreateSnapshot();
            backupPresentation = presentation.CreateSnapshot();
            originalAnchors = presentation.SceneAnchors;
            Check(backupMarket != null, "Snapshot original 2.3C capturado.");
            Check(backupCommercial != null, "Snapshot original 2.3D capturado.");
            Check(backupOrders != null, "Snapshot original 2.3E capturado.");
            Check(backupLogistics != null, "Snapshot original 2.3G capturado.");
            Check(backupPresentation != null, "Snapshot original 2.3H capturado.");
            if (backupMarket == null || backupCommercial == null || backupOrders == null || backupLogistics == null || backupPresentation == null) return;

            BistroBuilderSupplierAuthoringDatabase suppliers = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSupplierDeliveryPresentationService.SupplierAuthoringResourcePath);
            BistroBuilderSupplierDeliveryPresentationSettings settings = Resources.Load<BistroBuilderSupplierDeliveryPresentationSettings>(BistroBuilderSupplierDeliveryPresentationService.SettingsResourcePath);
            Check(suppliers != null, "Runtime localiza supplier.authoring.");
            Check(settings != null, "Runtime localiza supplier.delivery.presentation.settings.");
            if (suppliers == null || settings == null) return;
            Check(settings.SchemaVersion == 1, "Runtime consume schema v1 de supplier.delivery.presentation.settings.");
            Check(settings.RequireBrandingOnBothSides, "Runtime mantiene branding obligatorio en ambos laterales.");

            string error;
            Check(orders.TryInitializeFresh(), "2.3E inicia estado controlado vacío para 2.3H.");
            Check(logistics.TryInitializeFresh(), "2.3G inicia estado controlado vacío para 2.3H.");
            Check(presentation.TryInitializeFresh(), "2.3H inicia estado controlado vacío.");
            Check(orders.OrderCount == 0, "No hay PurchaseOrder ficticios al iniciar la prueba.");
            Check(logistics.PlanCount == 0, "No hay LogisticsPlan ficticios al iniciar la prueba.");
            Check(presentation.PresentationCount == 0, "No hay presentaciones ficticias al iniciar la prueba.");

            temporaryAnchorsRoot = BuildTemporaryAnchors(out BistroBuilderSupplierDeliverySceneAnchors temporaryAnchors);
            presentation.SetSceneAnchors(temporaryAnchors);
            Check(temporaryAnchors != null && temporaryAnchors.IsComplete, "Anclajes temporales completos para la prueba física.");

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
                        commercial.TryGetCommercialQuote(candidateOffer.SupplierOfferId, out candidateQuote) &&
                        candidateQuote != null && candidateQuote.availableForNewOrders)
                    {
                        supplier = candidate; offer = candidateOffer; quote = candidateQuote; break;
                    }
                }
            }
            Check(supplier != null && offer != null && quote != null, "Se localiza oferta real comprable para 2.3H.");
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
            Check(orders.TryBuildConfirmationPreview(draft.purchaseOrderId, out preview, out error), "2.3E construye preview confirmable.");
            Check(preview != null && preview.canConfirm, "Pedido de prueba es confirmable.");
            BistroBuilderPurchaseOrderConfirmationReceipt confirmation;
            Check(orders.TryConfirmOrder(draft.purchaseOrderId, out confirmation, out error), "PurchaseOrder se confirma con cotización real.");

            BistroBuilderSupplierLogisticsPlanRecord plan;
            if (!logistics.TryGetPlanByOrder(draft.purchaseOrderId, out plan))
                Check(logistics.TryCreatePlanForOrder(draft.purchaseOrderId, out plan, out error), "2.3G crea LogisticsPlan real.");
            else Check(true, "2.3G creó LogisticsPlan automáticamente.");
            Check(plan != null && plan.suggestedTripCount >= 1 && plan.suggestedTripCount <= 3, "Plan entrega 1..3 viajes visuales a 2.3H.");
            if (plan == null) return;

            int targetDay = Math.Max(market.CurrentGameDay, plan.basePlannedDeliveryGameDay);
            Check(market.TryAdvanceToGameDay(targetDay, out error), "2.3C avanza al día base de entrega.");
            Check(commercial.TrySynchronizeCurrentMarketState(out error), "2.3D sincroniza después del avance.");
            Check(logistics.TryAdvanceToGameDay(targetDay, out error), "2.3G evalúa fiabilidad/retraso.");
            logistics.TryGetPlanByOrder(draft.purchaseOrderId, out plan);
            if (plan != null && plan.plannedDeliveryGameDay > market.CurrentGameDay)
            {
                targetDay = plan.plannedDeliveryGameDay;
                Check(market.TryAdvanceToGameDay(targetDay, out error), "2.3C avanza a fecha replanificada.");
                Check(commercial.TrySynchronizeCurrentMarketState(out error), "2.3D sincroniza tras retraso.");
                Check(logistics.TryAdvanceToGameDay(targetDay, out error), "2.3G alcanza fecha efectiva.");
                logistics.TryGetPlanByOrder(draft.purchaseOrderId, out plan);
            }
            Check(plan != null && plan.status == BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch, "Plan queda ReadyForDispatch para 2.3H.");

            int handoffCount = 0;
            int completedCount = 0;
            BistroBuilderSupplierReceivingHandoff capturedHandoff = null;
            Action<BistroBuilderSupplierDeliveryPresentationRecord> changedHandler = r => { if (r != null) observedStates.Add(r.state); };
            Action<BistroBuilderSupplierReceivingHandoff> handoffHandler = h => { handoffCount++; capturedHandoff = h != null ? h.DeepClone() : null; };
            Action<BistroBuilderSupplierDeliveryPresentationRecord> completedHandler = r => completedCount++;
            presentation.PresentationChanged += changedHandler;
            presentation.ReceivingHandoffReady += handoffHandler;
            presentation.PresentationCompleted += completedHandler;
            try
            {
                BistroBuilderSupplierDeliveryPresentationRecord started;
                Check(presentation.TryStartDelivery(draft.purchaseOrderId, out started, out error), "2.3H inicia entrega física desde DispatchTicket real.");
                Check(started != null && started.totalTrips >= 1 && started.totalTrips <= 3, "Presentación conserva 1..3 viajes visuales.");
                if (started != null) observedStates.Add(started.state);

                BistroBuilderPurchaseOrderRecord inDelivery;
                Check(orders.TryGetOrder(draft.purchaseOrderId, out inDelivery) && inDelivery.status == BistroBuilderPurchaseOrderStatus.InDelivery, "Iniciar 2.3H mantiene contrato 2.3G: PurchaseOrder pasa a InDelivery.");
                Check(presentation.ActiveController != null && presentation.ActiveController.VehicleObject != null, "Se crea vehículo visual real/fallback.");

                BistroBuilderSupplierDeliveryBrandingView brandingView = presentation.ActiveController != null && presentation.ActiveController.VehicleObject != null
                    ? presentation.ActiveController.VehicleObject.GetComponent<BistroBuilderSupplierDeliveryBrandingView>()
                    : null;
                Check(brandingView != null && brandingView.HasBothSides, "Vehículo lleva branding en ambos laterales.");
                Check(brandingView != null && brandingView.leftName != null && brandingView.rightName != null, "Ambos laterales tienen cartel de nombre.");
                string expectedName = string.IsNullOrWhiteSpace(supplier.displayName) ? supplier.shortName : supplier.displayName;
                Check(brandingView != null && brandingView.leftName.text == expectedName && brandingView.rightName.text == expectedName, "Nombre del proveedor coincide exactamente en ambos laterales.");
                if (supplier.logo != null)
                    Check(brandingView.leftLogo != null && brandingView.leftLogo.sprite == supplier.logo && brandingView.rightLogo != null && brandingView.rightLogo.sprite == supplier.logo, "Logo del proveedor se aplica a ambos laterales cuando existe.");
                else
                    Check(brandingView != null && !string.IsNullOrWhiteSpace(brandingView.leftName.text), "Sin logo, el nombre evita un vehículo anónimo.");

                for (int tick = 0; tick < 4000 && presentation.ActiveController != null; tick++)
                    presentation.ActiveController.ManualTick(0.20f);

                BistroBuilderSupplierDeliveryPresentationRecord finalRecord;
                Check(presentation.TryGetPresentationByOrder(draft.purchaseOrderId, out finalRecord), "PresentationRecord final sigue trazable por PurchaseOrderId.");
                Check(finalRecord != null && finalRecord.state == BistroBuilderSupplierDeliveryPresentationState.Completed, "Secuencia física completa termina en Completed.");
                Check(handoffCount == 1, "ReceivingHandoffReady se emite exactamente una vez.");
                Check(completedCount == 1, "PresentationCompleted se emite exactamente una vez.");
                Check(capturedHandoff != null && capturedHandoff.purchaseOrderId == draft.purchaseOrderId, "Handoff conserva PurchaseOrderId real.");
                Check(capturedHandoff != null && capturedHandoff.logisticsPlanId == plan.logisticsPlanId, "Handoff conserva LogisticsPlanId real.");
                Check(capturedHandoff != null && capturedHandoff.lines.Count == inDelivery.confirmedLines.Count, "Handoff contiene todas las líneas confirmadas.");
                Check(capturedHandoff != null && capturedHandoff.totalPackageCount > 0, "Handoff contiene paquetes físicos positivos.");
                Check(capturedHandoff != null && capturedHandoff.visualTripsCompleted == started.totalTrips, "Handoff registra todos los viajes visuales ejecutados.");

                Check(ContainsState(BistroBuilderSupplierDeliveryPresentationState.VehicleEntering), "Secuencia incluye entrada del vehículo.");
                Check(ContainsState(BistroBuilderSupplierDeliveryPresentationState.OpeningRearDoors), "Secuencia incluye apertura de puertas traseras.");
                Check(ContainsState(BistroBuilderSupplierDeliveryPresentationState.PreparingTrolley), "Secuencia incluye preparación de carretilla/carga.");
                Check(ContainsState(BistroBuilderSupplierDeliveryPresentationState.GoingToWarehouse), "Secuencia incluye desplazamiento del repartidor al almacén.");
                Check(ContainsState(BistroBuilderSupplierDeliveryPresentationState.Unloading), "Secuencia incluye descarga visual.");
                Check(ContainsState(BistroBuilderSupplierDeliveryPresentationState.ReturningToVehicle), "Secuencia incluye retorno del repartidor.");
                Check(ContainsState(BistroBuilderSupplierDeliveryPresentationState.ClosingRearDoors), "Secuencia incluye cierre de puertas.");
                Check(ContainsState(BistroBuilderSupplierDeliveryPresentationState.VehicleExiting), "Secuencia incluye salida del vehículo.");

                BistroBuilderPurchaseOrderRecord afterPresentation;
                Check(orders.TryGetOrder(draft.purchaseOrderId, out afterPresentation) && afterPresentation.status == BistroBuilderPurchaseOrderStatus.InDelivery, "2.3H NO marca Delivered: 2.2B sigue siendo autoridad de recepción.");
                Check(afterPresentation != null && string.IsNullOrWhiteSpace(afterPresentation.deliveryReceiptId), "2.3H no inventa ReceiptId.");

                BistroBuilderSupplierDeliveryPresentationSnapshot runtimeSnapshot = presentation.CreateSnapshot();
                Check(runtimeSnapshot != null && runtimeSnapshot.presentations.Count == 1, "Snapshot 2.3H captura la presentación completada.");
                Check(runtimeSnapshot != null && runtimeSnapshot.sourceLogisticsSeed == logistics.LogisticsSeed, "Snapshot 2.3H queda vinculado a la sesión logística 2.3G.");
                Check(runtimeErrors == 0, "La presentación completa no captura Error/Exception/Assert.");
            }
            finally
            {
                presentation.PresentationChanged -= changedHandler;
                presentation.ReceivingHandoffReady -= handoffHandler;
                presentation.PresentationCompleted -= completedHandler;
            }
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
            if (presentation != null && backupPresentation != null)
            {
                Check(presentation.TryRestoreSnapshot(backupPresentation, out restoreError), "Se restaura snapshot original 2.3H.");
                presentation.SetSceneAnchors(originalAnchors);
            }
            if (temporaryAnchorsRoot != null) UnityEngine.Object.Destroy(temporaryAnchorsRoot);
            Application.logMessageReceived -= HandleLog;
            running = false;
            Repaint();
        }
    }

    private static GameObject BuildTemporaryAnchors(out BistroBuilderSupplierDeliverySceneAnchors anchors)
    {
        GameObject root = new GameObject("__BB_23H_RUNTIME_TEST_ANCHORS__");
        root.transform.position = new Vector3(10000f, 0f, 10000f);
        anchors = root.AddComponent<BistroBuilderSupplierDeliverySceneAnchors>();
        anchors.vehicleEntry = Point(root.transform, "VehicleEntry", new Vector3(-4f, 0f, 0f));
        anchors.vehicleParking = Point(root.transform, "VehicleParking", Vector3.zero);
        anchors.vehicleExit = Point(root.transform, "VehicleExit", new Vector3(4f, 0f, 0f));
        anchors.driverExitPoint = Point(root.transform, "DriverExit", new Vector3(0f, 0f, -0.8f));
        anchors.warehouseDoor = Point(root.transform, "WarehouseDoor", new Vector3(0f, 0f, -2.2f));
        anchors.warehouseDropoff = Point(root.transform, "WarehouseDropoff", new Vector3(0f, 0f, -3.2f));
        return root;
    }

    private static Transform Point(Transform root, string name, Vector3 local)
    {
        GameObject point = new GameObject(name);
        point.transform.SetParent(root, false);
        point.transform.localPosition = local;
        return point.transform;
    }

    private bool ContainsState(BistroBuilderSupplierDeliveryPresentationState state)
    {
        for (int i = 0; i < observedStates.Count; i++) if (observedStates[i] == state) return true;
        return false;
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
