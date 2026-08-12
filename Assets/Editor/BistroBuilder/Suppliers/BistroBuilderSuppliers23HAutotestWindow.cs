#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23HAutotestWindow : EditorWindow
{
    private readonly List<string> results = new List<string>();
    private Vector2 scroll;
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3H - Autotest entrega física")]
    public static void Open()
    {
        BistroBuilderSuppliers23HAutotestWindow window = GetWindow<BistroBuilderSuppliers23HAutotestWindow>("Autotest 2.3H");
        window.minSize = new Vector2(940f, 640f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AUTOTEST 2.3H — Entrega física y branding", EditorStyles.boldLabel);
        if (GUILayout.Button("Ejecutar autotest", GUILayout.Height(30f))) Run();
        EditorGUILayout.LabelField("Pruebas superadas: " + passed + " / Pruebas fallidas: " + failed, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < results.Count; i++) EditorGUILayout.LabelField(results[i], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        passed = 0; failed = 0; results.Clear();
        Check(!EditorApplication.isPlaying, "Autotest ejecutado en Edit Mode.");

        BistroBuilderSupplierDeliveryPresentationSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierDeliveryPresentationSettings>(BistroBuilderSuppliers23HPaths.DeliveryPresentationSettingsPath);
        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23HPaths.SupplierDatabasePath);
        BistroBuilderSupplierLogisticsPlanningSettings logistics =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierLogisticsPlanningSettings>(BistroBuilderSuppliers23HPaths.LogisticsSettingsPath);

        Check(settings != null, "supplier.delivery.presentation.settings existe.");
        Check(logistics != null, "supplier.logistics.settings 2.3G existe.");
        Check(suppliers != null, "supplier.authoring existe.");
        if (settings == null || suppliers == null) return;
        Check(settings.SchemaId == BistroBuilderSupplierDeliveryPresentationSettings.CurrentSchemaId, "schemaId 2.3H canónico.");
        Check(settings.SchemaVersion == BistroBuilderSupplierDeliveryPresentationSettings.CurrentSchemaVersion, "schemaVersion 2.3H canónico.");
        Check(settings.RequireBrandingOnBothSides, "Branding obligatorio en ambos laterales.");
        Check(settings.MaximumVisibleBoxesPerTrip >= 1, "Límite visual de cajas positivo.");
        Check(settings.VehicleSpeedMetersPerSecond > 0f, "Velocidad de vehículo positiva.");
        Check(settings.DriverSpeedMetersPerSecond > 0f, "Velocidad de repartidor positiva.");

        int active = 0;
        int named = 0;
        for (int i = 0; i < suppliers.Suppliers.Count; i++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[i];
            if (supplier == null || !supplier.isActive) continue;
            active++;
            BistroBuilderSupplierDeliveryBrandingData branding = BistroBuilderSupplierDeliveryVisualFactory.ResolveBranding(supplier);
            Check(branding != null, supplier.SupplierId + ": branding resoluble.");
            if (branding != null && branding.HasReadableIdentity) named++;
        }
        Check(active == 6, "Hay exactamente 6 proveedores activos en el bloque actual.");
        Check(named == active, "Todos los proveedores activos tienen identidad textual para el vehículo.");

        GameObject tempRoot = new GameObject("__BB_23H_AUTOTEST__");
        tempRoot.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            BistroBuilderSupplierAuthoringRecord sample = null;
            for (int i = 0; i < suppliers.Suppliers.Count; i++)
                if (suppliers.Suppliers[i] != null && suppliers.Suppliers[i].isActive) { sample = suppliers.Suppliers[i]; break; }
            Check(sample != null, "Proveedor de muestra disponible.");
            if (sample == null) return;
            BistroBuilderSupplierDeliveryBrandingData branding = BistroBuilderSupplierDeliveryVisualFactory.ResolveBranding(sample);

            GameObject van = BistroBuilderSupplierDeliveryVisualFactory.CreateVehicle(BistroBuilderSupplierVehiclePreference.Furgoneta, settings, tempRoot.transform);
            Check(van != null, "Furgoneta visual puede crearse sin asset obligatorio.");
            string error;
            Check(BistroBuilderSupplierDeliveryVisualFactory.ApplyBranding(van, branding, settings, out error), "Furgoneta acepta branding de proveedor.");
            BistroBuilderSupplierDeliveryBrandingView vanBrand = van.GetComponent<BistroBuilderSupplierDeliveryBrandingView>();
            Check(vanBrand != null && vanBrand.HasBothSides, "Furgoneta publica identidad en ambos laterales.");
            Check(vanBrand != null && vanBrand.leftName != null && !string.IsNullOrWhiteSpace(vanBrand.leftName.text), "Lateral izquierdo muestra nombre legible.");
            Check(vanBrand != null && vanBrand.rightName != null && !string.IsNullOrWhiteSpace(vanBrand.rightName.text), "Lateral derecho muestra nombre legible.");

            GameObject truck = BistroBuilderSupplierDeliveryVisualFactory.CreateVehicle(BistroBuilderSupplierVehiclePreference.CamionLigero, settings, tempRoot.transform);
            Check(truck != null, "Camión ligero visual puede crearse sin asset obligatorio.");
            Check(BistroBuilderSupplierDeliveryVisualFactory.ApplyBranding(truck, branding, settings, out error), "Camión ligero acepta branding de proveedor.");
            BistroBuilderSupplierDeliveryBrandingView truckBrand = truck.GetComponent<BistroBuilderSupplierDeliveryBrandingView>();
            Check(truckBrand != null && truckBrand.HasBothSides, "Camión ligero publica identidad en ambos laterales.");

            GameObject driver = BistroBuilderSupplierDeliveryVisualFactory.CreateDriver(settings, tempRoot.transform);
            GameObject trolley = BistroBuilderSupplierDeliveryVisualFactory.CreateTrolley(settings, tempRoot.transform);
            GameObject box = BistroBuilderSupplierDeliveryVisualFactory.CreateBox(settings, tempRoot.transform, 0);
            Check(driver != null, "Repartidor fallback/prefab creable.");
            Check(trolley != null, "Carretilla fallback/prefab creable.");
            Check(box != null, "Caja visual fallback/prefab creable.");
            BistroBuilderSupplierDeliveryVisualFactory.ApplyDriverBrandColor(driver, branding);
            Check(driver.GetComponentsInChildren<Renderer>(true).Length > 0, "Repartidor tiene representación visual coloreable por proveedor.");
        }
        finally
        {
            Object.DestroyImmediate(tempRoot);
        }

        BistroBuilderSupplierDeliveryPresentationSnapshot snapshot = new BistroBuilderSupplierDeliveryPresentationSnapshot
        {
            currentGameDay = 8,
            sourceLogisticsSeed = 12345UL,
            presentationRevision = 4,
            nextPresentationSequence = 3
        };
        snapshot.presentations.Add(new BistroBuilderSupplierDeliveryPresentationRecord
        {
            presentationId = "delivery_presentation_00000001",
            logisticsPlanId = "logistics_plan_00000001",
            purchaseOrderId = "purchase_order_00000001",
            supplierId = "supplier_mercado_central",
            state = BistroBuilderSupplierDeliveryPresentationState.GoingToWarehouse,
            currentTrip = 2,
            totalTrips = 3,
            vehicle = BistroBuilderSupplierVehiclePreference.Furgoneta,
            visualLoadUnits = 6,
            logisticsLoadUnits = 12
        });
        BistroBuilderSupplierDeliveryPresentationSnapshot clone = snapshot.DeepClone();
        Check(clone != null && clone != snapshot, "Snapshot 2.3H clona defensivamente.");
        Check(clone.presentations.Count == 1 && clone.presentations[0] != snapshot.presentations[0], "PresentationRecord se clona profundamente.");
        Check(clone.sourceLogisticsSeed == snapshot.sourceLogisticsSeed, "Snapshot conserva vínculo a semilla 2.3G.");
        clone.presentations[0].currentTrip = 1;
        Check(snapshot.presentations[0].currentTrip == 2, "Mutar clon no altera snapshot origen.");

        Check(BistroBuilderSupplierDeliveryPresentationController.BuildHandoffId("logistics_plan_123") == "receiving_handoff_logistics_plan_123", "HandoffId deriva deterministamente del LogisticsPlanId.");
        Check(BistroBuilderSupplierDeliveryPresentationController.BuildHandoffId("logistics_plan_123") == BistroBuilderSupplierDeliveryPresentationController.BuildHandoffId("logistics_plan_123"), "Mismo LogisticsPlanId produce mismo HandoffId.");
        Check(BistroBuilderSupplierDeliveryPresentationController.BuildHandoffId("logistics_plan_124") != BistroBuilderSupplierDeliveryPresentationController.BuildHandoffId("logistics_plan_123"), "Otro LogisticsPlanId produce otra identidad de handoff.");
        Check((int)BistroBuilderSupplierDeliveryPresentationState.Completed > (int)BistroBuilderSupplierDeliveryPresentationState.VehicleEntering, "Ciclo visual contiene estado terminal Completed.");
        Check((int)BistroBuilderSupplierDeliveryPresentationState.Unloading > (int)BistroBuilderSupplierDeliveryPresentationState.GoingToWarehouse, "Descarga sucede tras llegar al almacén.");
        Check((int)BistroBuilderSupplierDeliveryPresentationState.VehicleExiting > (int)BistroBuilderSupplierDeliveryPresentationState.ClosingRearDoors, "Vehículo sale después de cerrar puertas.");

        Repaint();
    }

    private void Check(bool condition, string message)
    {
        if (condition) { passed++; results.Add("[OK] " + message); }
        else { failed++; results.Add("[FALLO] " + message); }
    }
}
#endif
