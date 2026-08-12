#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Demo VISUAL real de 2.3H. A diferencia del simulador no destructivo,
/// este botón instancia temporalmente el vehículo/repartidor/carretilla/cajas
/// en Play Mode y ejecuta la animación completa sobre los anclajes de escena.
/// No crea PurchaseOrder canónico, no escribe Inventario/Recepciones y no
/// modifica assets ni la escena guardada.
/// </summary>
public sealed class BistroBuilderSuppliers23HVisualDemoWindow : EditorWindow
{
    private const string SupplierDatabasePath =
        "Assets/Resources/BistroBuilder/Suppliers/Authoring/BistroBuilderSupplierAuthoringDatabase.asset";
    private const string PresentationSettingsPath =
        "Assets/Resources/BistroBuilder/Suppliers/BistroBuilderSupplierDeliveryPresentationSettings.asset";
    private const string DemoRootName = "BB_2_3H_VISUAL_DEMO_RUNTIME";

    private readonly List<BistroBuilderSupplierAuthoringRecord> activeSuppliers =
        new List<BistroBuilderSupplierAuthoringRecord>();
    private string[] supplierLabels = Array.Empty<string>();
    private int supplierIndex;
    private int trips = 2;
    private VehicleMode vehicleMode = VehicleMode.Automatico;
    private BistroBuilderSupplierDeliveryPresentationController controller;
    private GameObject demoRoot;
    private string lastMessage = "Listo para iniciar un demo visual real en Play Mode.";
    private bool handoffSeen;

    private enum VehicleMode
    {
        Automatico = 0,
        Furgoneta = 1,
        CamionLigero = 2
    }

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3H5 - Demo visual REAL de entrega física")]
    public static void Open()
    {
        BistroBuilderSuppliers23HVisualDemoWindow window =
            GetWindow<BistroBuilderSuppliers23HVisualDemoWindow>("Demo visual 2.3H5");
        window.minSize = new Vector2(620f, 430f);
        window.RefreshSuppliers();
    }

    private void OnEnable()
    {
        RefreshSuppliers();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            controller = null;
            demoRoot = null;
            handoffSeen = false;
            lastMessage = "Listo para iniciar un demo visual real en Play Mode.";
            Repaint();
        }
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3H5 — Demo visual REAL de entrega física", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Este demo sí instancia y mueve físicamente el fallback/prefab de vehículo, repartidor, carretilla y cajas sobre los anclajes 2.3H. " +
            "La cámara temporal sigue la acción para que pueda verse en Game View. No crea pedidos canónicos ni escribe Inventario/Recepciones.",
            MessageType.Info);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Entra en Play Mode antes de iniciar el demo.", MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(activeSuppliers.Count == 0 || IsDemoRunning()))
        {
            supplierIndex = EditorGUILayout.Popup("Proveedor", Mathf.Clamp(supplierIndex, 0, Mathf.Max(0, supplierLabels.Length - 1)), supplierLabels);
            trips = EditorGUILayout.IntSlider("Viajes visuales", trips, 1, 3);
            vehicleMode = (VehicleMode)EditorGUILayout.EnumPopup("Vehículo", vehicleMode);
        }

        GUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || activeSuppliers.Count == 0 || IsDemoRunning()))
        {
            if (GUILayout.Button("INICIAR DEMO VISUAL REAL", GUILayout.Height(38f))) StartDemo();
        }

        using (new EditorGUI.DisabledScope(!IsDemoRunning()))
        {
            if (GUILayout.Button("Detener y limpiar demo", GUILayout.Height(28f))) StopDemo();
        }

        GUILayout.Space(10f);
        DrawStatus();

        GUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Qué mirar: entrada y orientación del vehículo; nombre/logo en ambos laterales; escala del repartidor/carretilla/cajas; ruta a WarehouseDoor/Dropoff; " +
            "número de viajes; apertura/cierre de puertas y salida. El evento ReceivingHandoff se intercepta solo para el demo y NO se entrega a 2.2B.",
            MessageType.None);
    }

    private void DrawStatus()
    {
        EditorGUILayout.LabelField("Estado", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(lastMessage, EditorStyles.wordWrappedLabel);
        if (controller != null)
        {
            BistroBuilderSupplierDeliveryPresentationRecord record = controller.Record;
            if (record != null)
            {
                EditorGUILayout.LabelField("Fase", record.state.ToString());
                EditorGUILayout.LabelField("Viaje", record.currentTrip + " / " + record.totalTrips);
                EditorGUILayout.LabelField("Handoff visual", handoffSeen ? "Emitido (interceptado; sin recepción)" : "Aún no emitido");
            }
        }
    }

    private void RefreshSuppliers()
    {
        activeSuppliers.Clear();
        BistroBuilderSupplierAuthoringDatabase database =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(SupplierDatabasePath);
        if (database != null)
        {
            for (int i = 0; i < database.Suppliers.Count; i++)
            {
                BistroBuilderSupplierAuthoringRecord supplier = database.Suppliers[i];
                if (supplier != null && supplier.isActive) activeSuppliers.Add(supplier);
            }
        }

        supplierLabels = new string[activeSuppliers.Count];
        for (int i = 0; i < activeSuppliers.Count; i++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = activeSuppliers[i];
            string name = string.IsNullOrWhiteSpace(supplier.displayName) ? supplier.shortName : supplier.displayName;
            supplierLabels[i] = name + "  [" + supplier.SupplierId + "]";
        }
        supplierIndex = Mathf.Clamp(supplierIndex, 0, Mathf.Max(0, activeSuppliers.Count - 1));
    }

    private bool IsDemoRunning()
    {
        return demoRoot != null && controller != null && !controller.IsCompleted;
    }

    private void StartDemo()
    {
        if (!Application.isPlaying)
        {
            lastMessage = "El demo requiere Play Mode.";
            return;
        }

        StopDemo();
        RefreshSuppliers();
        if (activeSuppliers.Count == 0)
        {
            lastMessage = "No hay proveedores activos en supplier.authoring.";
            return;
        }

        BistroBuilderSupplierDeliveryPresentationSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierDeliveryPresentationSettings>(PresentationSettingsPath);
        if (settings == null)
        {
            lastMessage = "Falta supplier.delivery.presentation.settings. Ejecuta el instalador 2.3H.";
            return;
        }

        BistroBuilderSupplierDeliverySceneAnchors anchors =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierDeliverySceneAnchors>();
        if (anchors == null || !anchors.IsComplete)
        {
            lastMessage = "No se han localizado anclajes completos 2.3H en la escena.";
            return;
        }

        BistroBuilderSupplierAuthoringRecord supplier = activeSuppliers[Mathf.Clamp(supplierIndex, 0, activeSuppliers.Count - 1)];
        BistroBuilderSupplierDeliveryBrandingData branding =
            BistroBuilderSupplierDeliveryVisualFactory.ResolveBranding(supplier);
        if (branding == null || !branding.HasReadableIdentity)
        {
            lastMessage = "El proveedor seleccionado no tiene identidad visual resoluble.";
            return;
        }

        BistroBuilderSupplierVehiclePreference vehicle = ResolveVehicle(supplier);
        int tripCount = Mathf.Clamp(trips, 1, 3);
        int visualUnits = tripCount * 3;
        int logisticsUnits = tripCount * 12;

        BistroBuilderSupplierDispatchTicket ticket = new BistroBuilderSupplierDispatchTicket
        {
            logisticsPlanId = "logistics_plan_visual_demo_23h5",
            purchaseOrderId = "purchase_order_visual_demo_23h5",
            orderDisplayCode = "PO-DEMO-H5",
            supplierId = supplier.SupplierId,
            plannedDeliveryGameDay = 1,
            windowStartMinuteOfDay = 8 * 60,
            windowEndMinuteOfDay = 12 * 60,
            appliedDelayGameMinutes = 0,
            logisticsLoadUnits = logisticsUnits,
            visualLoadUnits = visualUnits,
            suggestedTripCount = tripCount,
            vehicle = vehicle,
            vehiclePresentationProfileId = supplier.logisticsProfile != null ? supplier.logisticsProfile.vehiclePresentationProfileId : "vehicle_supplier_default",
            driverPresentationProfileId = supplier.logisticsProfile != null ? supplier.logisticsProfile.driverPresentationProfileId : "driver_supplier_default"
        };

        string supplierName = string.IsNullOrWhiteSpace(supplier.displayName) ? supplier.shortName : supplier.displayName;
        BistroBuilderPurchaseOrderRecord order = BuildSyntheticOrder(supplier, supplierName, logisticsUnits, tripCount);
        BistroBuilderSupplierDeliveryPresentationRecord record = new BistroBuilderSupplierDeliveryPresentationRecord
        {
            presentationId = "delivery_presentation_visual_demo_23h5",
            logisticsPlanId = ticket.logisticsPlanId,
            purchaseOrderId = ticket.purchaseOrderId,
            orderDisplayCode = ticket.orderDisplayCode,
            supplierId = supplier.SupplierId,
            state = BistroBuilderSupplierDeliveryPresentationState.Queued,
            currentTrip = 1,
            totalTrips = tripCount,
            startedGameDay = 1,
            vehicle = vehicle,
            visualLoadUnits = visualUnits,
            logisticsLoadUnits = logisticsUnits,
            appliedDelayGameMinutes = 0,
            vehiclePresentationProfileId = ticket.vehiclePresentationProfileId,
            driverPresentationProfileId = ticket.driverPresentationProfileId
        };

        demoRoot = new GameObject(DemoRootName);
        controller = demoRoot.AddComponent<BistroBuilderSupplierDeliveryPresentationController>();
        handoffSeen = false;

        string error;
        bool initialized = controller.Initialize(
            ticket,
            order,
            settings,
            anchors,
            branding,
            record,
            OnDemoStateChanged,
            OnDemoHandoff,
            OnDemoCompleted,
            out error);

        if (!initialized)
        {
            lastMessage = "No se pudo iniciar el demo: " + error;
            StopDemo();
            return;
        }

        CreateDemoCamera();
        lastMessage = "Demo iniciado: " + supplierName + " · " + HumanizeVehicle(vehicle) + " · " + tripCount + " viaje(s). Mira la Game View.";
        Repaint();
    }

    private BistroBuilderSupplierVehiclePreference ResolveVehicle(BistroBuilderSupplierAuthoringRecord supplier)
    {
        if (vehicleMode == VehicleMode.Furgoneta) return BistroBuilderSupplierVehiclePreference.Furgoneta;
        if (vehicleMode == VehicleMode.CamionLigero) return BistroBuilderSupplierVehiclePreference.CamionLigero;

        BistroBuilderSupplierVehiclePreference preferred =
            supplier.logisticsProfile != null ? supplier.logisticsProfile.preferredVehicle : BistroBuilderSupplierVehiclePreference.Automatico;
        if (preferred != BistroBuilderSupplierVehiclePreference.Automatico) return preferred;
        return trips >= 2 ? BistroBuilderSupplierVehiclePreference.CamionLigero : BistroBuilderSupplierVehiclePreference.Furgoneta;
    }

    private static BistroBuilderPurchaseOrderRecord BuildSyntheticOrder(
        BistroBuilderSupplierAuthoringRecord supplier,
        string supplierName,
        int logisticsUnits,
        int tripCount)
    {
        int packageCount = Mathf.Max(1, tripCount * 6);
        long packageNet = 1000000L;
        BistroBuilderPurchaseOrderRecord order = new BistroBuilderPurchaseOrderRecord
        {
            purchaseOrderId = "purchase_order_visual_demo_23h5",
            displayCode = "PO-DEMO-H5",
            supplierId = supplier.SupplierId,
            status = BistroBuilderPurchaseOrderStatus.InDelivery,
            createdGameDay = 1,
            confirmedGameDay = 1,
            pendingDeliveryGameDay = 1,
            inDeliveryGameDay = 1,
            actualDeliveryStartGameDay = 1,
            logisticsPlanId = "logistics_plan_visual_demo_23h5",
            supplierTerms = new BistroBuilderPurchaseOrderSupplierTermsSnapshot
            {
                supplierId = supplier.SupplierId,
                supplierDisplayName = supplierName,
                reliabilityTier = supplier.reliabilityTier,
                reliabilityValue = supplier.reliabilityValue,
                preferredVehicle = supplier.logisticsProfile != null ? supplier.logisticsProfile.preferredVehicle : BistroBuilderSupplierVehiclePreference.Automatico,
                vehiclePresentationProfileId = supplier.logisticsProfile != null ? supplier.logisticsProfile.vehiclePresentationProfileId : "vehicle_supplier_default",
                driverPresentationProfileId = supplier.logisticsProfile != null ? supplier.logisticsProfile.driverPresentationProfileId : "driver_supplier_default"
            }
        };
        order.confirmedLines.Add(new BistroBuilderPurchaseOrderConfirmedLineSnapshot
        {
            purchaseOrderLineId = "purchase_order_line_visual_demo_001",
            supplierOfferId = "supplier_offer_visual_demo",
            supplierId = supplier.SupplierId,
            ingredientId = "ingredient_visual_demo",
            ingredientDisplayName = "Carga visual de demostración",
            canonicalUnit = "Unit",
            packageFormatId = "package_visual_demo",
            packageDisplayName = "Caja demo",
            packageType = "Caja",
            logisticSize = logisticsUnits >= 24 ? BistroBuilderCommercialPackageLogisticSize.Grande : BistroBuilderCommercialPackageLogisticSize.Medio,
            packageNetQuantityMicrounits = packageNet,
            packageCount = packageCount,
            totalNetQuantityMicrounits = packageNet * packageCount,
            minimumPackageCount = 1,
            orderIncrement = 1,
            basePriceCents = 1000,
            marketPriceCents = 1000,
            effectiveUnitPriceCents = 1000,
            lineSubtotalCents = 1000L * packageCount,
            availabilityAtConfirmation = BistroBuilderSupplierOfferAvailability.Disponible,
            quotedLeadTimeGameHours = Mathf.Max(1f, supplier.defaultLeadTimeGameHours)
        });
        return order;
    }

    private void CreateDemoCamera()
    {
        GameObject cameraObject = new GameObject("BB_2_3H_VISUAL_DEMO_CAMERA");
        cameraObject.transform.SetParent(demoRoot.transform, true);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.depth = 1000f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 54f;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 1200f;
        camera.cullingMask = ~0;
        BistroBuilderSupplierDeliveryVisualDemoCamera rig =
            cameraObject.AddComponent<BistroBuilderSupplierDeliveryVisualDemoCamera>();
        rig.Initialize(controller);
    }

    private void OnDemoStateChanged(
        BistroBuilderSupplierDeliveryPresentationController source,
        BistroBuilderSupplierDeliveryPresentationRecord record)
    {
        if (record == null) return;
        lastMessage = "Fase visual: " + record.state + " · viaje " + record.currentTrip + "/" + record.totalTrips + ".";
        Repaint();
    }

    private void OnDemoHandoff(
        BistroBuilderSupplierDeliveryPresentationController source,
        BistroBuilderSupplierReceivingHandoff handoff)
    {
        handoffSeen = true;
        lastMessage = "Descarga visual terminada. ReceivingHandoff interceptado para demo (NO enviado a Inventario/Recepciones).";
        Repaint();
    }

    private void OnDemoCompleted(
        BistroBuilderSupplierDeliveryPresentationController source,
        BistroBuilderSupplierDeliveryPresentationRecord record)
    {
        lastMessage = "Demo COMPLETADO. El vehículo ha salido de la ruta. Pulsa 'Detener y limpiar demo' o inicia otro tras limpiar.";
        Repaint();
    }

    private void StopDemo()
    {
        if (controller != null)
        {
            controller.DisposeVisuals();
            controller = null;
        }

        GameObject existing = GameObject.Find(DemoRootName);
        if (existing != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(existing);
            else UnityEngine.Object.DestroyImmediate(existing);
        }
        demoRoot = null;
        handoffSeen = false;
    }

    private static string HumanizeVehicle(BistroBuilderSupplierVehiclePreference vehicle)
    {
        return vehicle == BistroBuilderSupplierVehiclePreference.CamionLigero ? "Camión ligero" : "Furgoneta";
    }
}
#endif
