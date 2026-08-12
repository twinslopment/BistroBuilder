#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23HValidationWindow : EditorWindow
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> info = new List<string>();
    private Vector2 scroll;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3H - Validar entrega física")]
    public static void Open()
    {
        BistroBuilderSuppliers23HValidationWindow window = GetWindow<BistroBuilderSuppliers23HValidationWindow>("Validación 2.3H");
        window.minSize = new Vector2(900f, 560f);
        window.ValidateNow();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3H — Entrega física, vehículo, repartidor y carretilla", EditorStyles.boldLabel);
        if (GUILayout.Button("Validar de nuevo", GUILayout.Height(28f))) ValidateNow();
        EditorGUILayout.LabelField("Errores: " + errors.Count + "  Advertencias: " + warnings.Count + "  Información: " + info.Count, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Draw("ERRORES", errors);
        Draw("ADVERTENCIAS", warnings);
        Draw("INFORMACIÓN", info);
        EditorGUILayout.EndScrollView();
    }

    private void ValidateNow()
    {
        errors.Clear(); warnings.Clear(); info.Clear();
        if (EditorApplication.isPlaying) errors.Add("La validación estructural debe ejecutarse fuera de Play Mode.");

        BistroBuilderSupplierDeliveryPresentationSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierDeliveryPresentationSettings>(BistroBuilderSuppliers23HPaths.DeliveryPresentationSettingsPath);
        BistroBuilderSupplierLogisticsPlanningSettings logistics =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierLogisticsPlanningSettings>(BistroBuilderSuppliers23HPaths.LogisticsSettingsPath);
        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23HPaths.SupplierDatabasePath);

        if (settings == null) errors.Add("Falta supplier.delivery.presentation.settings. Ejecuta el instalador 2.3H.");
        else
        {
            if (settings.SchemaId != BistroBuilderSupplierDeliveryPresentationSettings.CurrentSchemaId ||
                settings.SchemaVersion != BistroBuilderSupplierDeliveryPresentationSettings.CurrentSchemaVersion)
                errors.Add("supplier.delivery.presentation.settings usa un schema incompatible.");
            if (!settings.RequireBrandingOnBothSides) errors.Add("El branding en ambos laterales debe permanecer obligatorio en 2.3H.");
            if (settings.MaximumVisibleBoxesPerTrip < 1) errors.Add("Máximo visual de cajas por viaje inválido.");
            info.Add("Branding obligatorio: nombre y/o logo en ambos laterales; el nombre es fallback si falta logo.");
            info.Add("Prefabs 3D son opcionales: si faltan, 2.3H genera vehículo/repartidor/carretilla/cajas fallback para mantener el flujo funcional.");
        }

        if (logistics == null) errors.Add("Falta supplier.logistics.settings de 2.3G.");
        else info.Add("2.3H consume Vehicle/TripCount/LoadUnits/PresentationProfile del DispatchTicket 2.3G.");

        int active = 0, names = 0, logos = 0;
        if (suppliers == null) errors.Add("Falta supplier.authoring.");
        else
        {
            for (int i = 0; i < suppliers.Suppliers.Count; i++)
            {
                BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[i];
                if (supplier == null || !supplier.isActive) continue;
                active++;
                if (!string.IsNullOrWhiteSpace(supplier.displayName) || !string.IsNullOrWhiteSpace(supplier.shortName)) names++;
                else errors.Add((supplier.SupplierId ?? "supplier_sin_id") + ": sin nombre para el cartel lateral obligatorio.");
                if (supplier.logo != null) logos++;
            }
        }
        info.Add("Proveedores activos: " + active + ". Con identidad textual válida: " + names + ". Logos asignados actualmente: " + logos + ".");

        BistroBuilderSupplierDeliverySceneAnchors anchors = Object.FindFirstObjectByType<BistroBuilderSupplierDeliverySceneAnchors>();
        if (anchors == null)
            warnings.Add("La escena actual aún no tiene BB_SupplierDeliveryAnchors. Ejecuta '2.3H - Crear/actualizar anclajes de escena' antes de la prueba visual/runtime.");
        else if (!anchors.IsComplete)
            warnings.Add("Los anclajes 2.3H existen pero están incompletos.");
        else
            info.Add("Anclajes de escena completos: entrada, parking, salida, puerta y punto de descarga de almacén.");

        info.Add("El repartidor usa NavMeshAgent si el prefab lo aporta y existe NavMesh válido; en caso contrario usa waypoints deterministas.");
        info.Add("2.3H emite ReceivingHandoffReady una única vez tras la descarga visual y NO escribe Inventario ni crea ReceiptId.");
        info.Add("El vehículo abandona la escena tras el handoff visual; PurchaseOrder permanece InDelivery hasta que 2.2B/2.3L confirme la recepción canónica.");
        Repaint();
    }

    private static void Draw(string title, List<string> lines)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (lines.Count == 0) EditorGUILayout.LabelField("Ninguno.");
        for (int i = 0; i < lines.Count; i++) EditorGUILayout.LabelField(lines[i], EditorStyles.wordWrappedLabel);
    }
}
#endif
