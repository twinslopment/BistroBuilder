#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23HSimulatorWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Pulsa Simular presentación.";

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3H - Simulador de presentación física")]
    public static void Open()
    {
        BistroBuilderSuppliers23HSimulatorWindow window = GetWindow<BistroBuilderSuppliers23HSimulatorWindow>("Simulador 2.3H");
        window.minSize = new Vector2(940f, 640f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Simulador no destructivo de entrega física 2.3H", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Revisa branding, vehículo, carga visual y 1/2/3 viajes con los seis proveedores. No crea PurchaseOrder runtime, no modifica escena ni escribe Inventario/Recepciones.",
            MessageType.Info);
        if (GUILayout.Button("Simular presentación", GUILayout.Height(30f))) Simulate();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Simulate()
    {
        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23HPaths.SupplierDatabasePath);
        BistroBuilderSupplierDeliveryPresentationSettings presentation =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierDeliveryPresentationSettings>(BistroBuilderSuppliers23HPaths.DeliveryPresentationSettingsPath);
        BistroBuilderSupplierLogisticsPlanningSettings logistics =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierLogisticsPlanningSettings>(BistroBuilderSuppliers23HPaths.LogisticsSettingsPath);
        if (suppliers == null || presentation == null || logistics == null)
        {
            report = "Faltan assets 2.3G/2.3H. Ejecuta los instaladores antes de simular.";
            return;
        }

        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("SIMULACIÓN 2.3H — PRESENTACIÓN FÍSICA Y BRANDING");
        sb.AppendLine("No se ha modificado ningún asset, escena, PurchaseOrder, Inventario ni Recepciones.");
        sb.AppendLine();

        int sampleIndex = 0;
        int[] loads = { 7, 14, 22, 33, 10, 28 };
        for (int i = 0; i < suppliers.Suppliers.Count; i++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[i];
            if (supplier == null || !supplier.isActive) continue;
            int loadUnits = loads[sampleIndex % loads.Length];
            sampleIndex++;
            int visualUnits = Mathf.Max(1, (loadUnits + logistics.VisualLoadUnitCapacity - 1) / logistics.VisualLoadUnitCapacity);
            int trips = Mathf.Clamp((loadUnits + logistics.TripCapacityLoadUnits - 1) / logistics.TripCapacityLoadUnits, 1, logistics.MaximumSuggestedTrips);
            BistroBuilderSupplierVehiclePreference preferred = supplier.logisticsProfile != null
                ? supplier.logisticsProfile.preferredVehicle
                : BistroBuilderSupplierVehiclePreference.Automatico;
            BistroBuilderSupplierVehiclePreference resolved = preferred;
            if (resolved == BistroBuilderSupplierVehiclePreference.Automatico)
                resolved = loadUnits >= logistics.LightTruckThresholdLoadUnits
                    ? BistroBuilderSupplierVehiclePreference.CamionLigero
                    : BistroBuilderSupplierVehiclePreference.Furgoneta;

            string name = string.IsNullOrWhiteSpace(supplier.displayName) ? supplier.shortName : supplier.displayName;
            sb.AppendLine(name + " [" + supplier.SupplierId + "]");
            sb.AppendLine("  Identidad lateral: " + (supplier.logo != null ? "LOGO + NOMBRE" : "NOMBRE (fallback obligatorio)"));
            sb.AppendLine("  Color principal: #" + ColorUtility.ToHtmlStringRGB(supplier.primaryBrandColor));
            sb.AppendLine("  Vehículo: " + Humanize(resolved) + " | carga abstracta: " + loadUnits + " | unidades visuales: " + visualUnits + " | viajes: " + trips);
            sb.AppendLine("  Secuencia: entra → aparca → repartidor baja → puertas → carretilla → almacén → descarga → vuelve → cierra → sale.");
            sb.AppendLine();
        }

        sb.AppendLine("REGLAS");
        sb.AppendLine("- Todo vehículo muestra proveedor en ambos laterales.");
        sb.AppendLine("- Si existe logo: se usa logo y nombre. Si falta: nombre + colores; nunca vehículo anónimo.");
        sb.AppendLine("- El repartidor hereda color principal como identidad mínima.");
        sb.AppendLine("- 1..3 viajes visuales; no se simulan decenas de cajas individualmente.");
        sb.AppendLine("- NavMeshAgent se usa cuando el prefab/escena lo permiten; existe fallback de waypoints.");
        sb.AppendLine("- Al terminar la descarga se emite ReceivingHandoffReady una vez. 2.3H NO añade stock.");
        report = sb.ToString();
    }

    private static string Humanize(BistroBuilderSupplierVehiclePreference value)
    {
        return value == BistroBuilderSupplierVehiclePreference.CamionLigero ? "Camión ligero" :
               value == BistroBuilderSupplierVehiclePreference.Furgoneta ? "Furgoneta" : value.ToString();
    }
}
#endif
