#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23GValidationWindow : EditorWindow
{
    private readonly List<string> lines = new List<string>();
    private Vector2 scroll;
    private int errors;
    private int warnings;
    private int info;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3G - Validar planificación logística")]
    public static void Open()
    {
        BistroBuilderSuppliers23GValidationWindow window = GetWindow<BistroBuilderSuppliers23GValidationWindow>("Validación 2.3G");
        window.minSize = new Vector2(880f, 560f);
        window.ValidateNow();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3G — Planificación logística, fiabilidad y retrasos", EditorStyles.boldLabel);
        if (GUILayout.Button("Validar de nuevo", GUILayout.Height(28f))) ValidateNow();
        EditorGUILayout.LabelField("Errores: " + errors + "  Advertencias: " + warnings + "  Información: " + info, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < lines.Count; index++) EditorGUILayout.LabelField(lines[index], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void ValidateNow()
    {
        lines.Clear(); errors = 0; warnings = 0; info = 0;
        if (EditorApplication.isPlaying) Error("La validación estructural 2.3G se ejecuta fuera de Play Mode.");
        BistroBuilderSupplierLogisticsPlanningSettings settings = AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierLogisticsPlanningSettings>(BistroBuilderSuppliers23GPaths.LogisticsSettingsPath);
        BistroBuilderSupplierAuthoringDatabase suppliers = AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23GPaths.SupplierDatabasePath);
        if (settings == null) Error("Falta supplier.logistics.settings.");
        else
        {
            if (settings.SchemaId != BistroBuilderSupplierLogisticsPlanningSettings.CurrentSchemaId || settings.SchemaVersion != 1) Error("Schema de supplier.logistics.settings no canónico.");
            else Info("supplier.logistics.settings localizado con schema canónico.");
        }
        if (suppliers == null) Error("Falta supplier.authoring.");
        if (settings == null || suppliers == null) return;

        int active = 0, validWindows = 0, validDelayRanges = 0;
        int excelente = 0, alta = 0, normal = 0, irregular = 0;
        for (int index = 0; index < suppliers.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[index];
            if (supplier == null || !supplier.isActive) continue;
            active++;
            if (supplier.deliveryWindows != null && supplier.deliveryWindows.Count > 0) validWindows++;
            if (supplier.logisticsProfile != null && supplier.logisticsProfile.minimumDelayMinutes >= 0 && supplier.logisticsProfile.maximumDelayMinutes >= supplier.logisticsProfile.minimumDelayMinutes) validDelayRanges++;
            switch (supplier.reliabilityTier)
            {
                case BistroBuilderSupplierReliabilityTier.Excelente: excelente++; break;
                case BistroBuilderSupplierReliabilityTier.Alta: alta++; break;
                case BistroBuilderSupplierReliabilityTier.Normal: normal++; break;
                default: irregular++; break;
            }
            if (supplier.reliabilityValue < 0f || supplier.reliabilityValue > 1f) Error(supplier.SupplierId + ": reliabilityValue fuera de 0..1.");
        }
        Info("Proveedores activos: " + active + ". Con ventanas: " + validWindows + ". Rangos de retraso válidos: " + validDelayRanges + ".");
        Info("Fiabilidad visible: Excelente " + excelente + ", Alta " + alta + ", Normal " + normal + ", Irregular " + irregular + ".");

        BistroBuilderSupplierAuthoringRecord sample = BistroBuilderSuppliers23GTestData.FirstActiveSupplier(suppliers);
        if (sample != null)
        {
            BistroBuilderSupplierLogisticsSnapshot snapshot = BistroBuilderSupplierLogisticsPlanningEngine.CreateInitialSnapshot(1, 123UL, 456UL, settings);
            BistroBuilderPurchaseOrderRecord order = BistroBuilderSuppliers23GTestData.BuildConfirmedOrder(sample, "purchase_order_validation", 1, sample.defaultLeadTimeGameHours, 6, BistroBuilderCommercialPackageLogisticSize.Medio);
            BistroBuilderSupplierLogisticsPlanRecord plan;
            string error;
            if (!BistroBuilderSupplierLogisticsPlanningEngine.TryBuildPlan(snapshot, order, sample, settings, 1, out plan, out error)) Error("No se puede construir plan controlado: " + error);
            else
            {
                Info("Plan controlado: " + plan.logisticsPlanId + ", entrega día " + plan.plannedDeliveryGameDay + " " + FormatTime(plan.windowStartMinuteOfDay) + "–" + FormatTime(plan.windowEndMinuteOfDay) + ", retraso decidido " + plan.decidedDelayGameMinutes + " min.");
                Info("Carga abstracta: " + plan.logisticsLoadUnits + " unidades; vehículo " + plan.resolvedVehicle + "; viajes visuales sugeridos " + plan.suggestedTripCount + ".");
            }
        }
        Info("2.3G nunca elimina pedidos por RNG: solo puede mantener puntualidad o aplicar retraso trazable.");
        Info("2.3G usa 2.3E para PendingDelivery/InDelivery; 2.3H consumirá DispatchTicket y 2.2B seguirá siendo la única autoridad de recepción física.");
    }

    private static string FormatTime(int minute) { return (minute / 60).ToString("D2") + ":" + (minute % 60).ToString("D2"); }
    private void Error(string value) { errors++; lines.Add("[ERROR] " + value); }
    private void Warn(string value) { warnings++; lines.Add("[AVISO] " + value); }
    private void Info(string value) { info++; lines.Add("[INFO] " + value); }
}
#endif
