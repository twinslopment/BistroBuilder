#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23GSimulatorWindow : EditorWindow
{
    private int finalDay = 180;
    private Vector2 scroll;
    private string report = "";

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3G - Simulador de fiabilidad y retrasos")]
    public static void Open()
    {
        BistroBuilderSuppliers23GSimulatorWindow window = GetWindow<BistroBuilderSuppliers23GSimulatorWindow>("Simulador 2.3G");
        window.minSize = new Vector2(920f, 660f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Simulador no destructivo de planificación logística 2.3G", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Genera pedidos sintéticos con las condiciones reales de los seis proveedores. No crea PurchaseOrder runtime ni modifica assets.", MessageType.Info);
        finalDay = EditorGUILayout.IntSlider("Día final", finalDay, 30, 365);
        if (GUILayout.Button("Simular", GUILayout.Height(30f))) Simulate();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Simulate()
    {
        BistroBuilderSupplierLogisticsPlanningSettings settings = AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierLogisticsPlanningSettings>(BistroBuilderSuppliers23GPaths.LogisticsSettingsPath);
        BistroBuilderSupplierAuthoringDatabase suppliers = AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23GPaths.SupplierDatabasePath);
        if (settings == null || suppliers == null)
        {
            report = "Falta supplier.logistics.settings o supplier.authoring.";
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("SIMULACIÓN 2.3G — DÍA " + finalDay);
        sb.AppendLine("No se ha modificado ningún asset ni estado runtime.");
        sb.AppendLine();
        int totalPlans = 0, totalDelayed = 0, totalDelayMinutes = 0, vans = 0, trucks = 0;
        Dictionary<string, int> plansBySupplier = new Dictionary<string, int>();
        Dictionary<string, int> delayedBySupplier = new Dictionary<string, int>();
        List<string> samples = new List<string>();
        BistroBuilderSupplierLogisticsSnapshot snapshot = BistroBuilderSupplierLogisticsPlanningEngine.CreateInitialSnapshot(1, 777UL, 888UL, settings);

        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive) continue;
            int localPlans = 0, localDelayed = 0, localDelayMinutes = 0;
            for (int day = 1; day <= finalDay; day += 5)
            {
                int packages = 1 + ((day + supplierIndex * 3) % 18);
                BistroBuilderCommercialPackageLogisticSize size = (BistroBuilderCommercialPackageLogisticSize)((day / 5 + supplierIndex) % 3);
                float lead = Math.Max(0.1f, supplier.defaultLeadTimeGameHours);
                BistroBuilderPurchaseOrderRecord order = BistroBuilderSuppliers23GTestData.BuildConfirmedOrder(
                    supplier,
                    "sim_order_" + supplierIndex + "_" + day,
                    day,
                    lead,
                    packages,
                    size);
                BistroBuilderSupplierLogisticsPlanRecord plan;
                string error;
                if (!BistroBuilderSupplierLogisticsPlanningEngine.TryBuildPlan(snapshot, order, supplier, settings, day, out plan, out error))
                {
                    sb.AppendLine("[ERROR] " + supplier.displayName + " día " + day + ": " + error);
                    continue;
                }
                snapshot.nextPlanSequence++;
                totalPlans++; localPlans++;
                if (plan.resolvedVehicle == BistroBuilderSupplierVehiclePreference.CamionLigero) trucks++; else vans++;
                if (plan.decidedDelayGameMinutes > 0)
                {
                    totalDelayed++; localDelayed++;
                    totalDelayMinutes += plan.decidedDelayGameMinutes;
                    localDelayMinutes += plan.decidedDelayGameMinutes;
                    bool changed;
                    BistroBuilderSupplierLogisticsPlanningEngine.TryApplyDelay(plan, plan.basePlannedDeliveryGameDay, out changed, out error);
                }
                if (samples.Count < 18 && (plan.decidedDelayGameMinutes > 0 || day % 25 == 1))
                {
                    samples.Add(
                        plan.orderDisplayCode + " | " + supplier.displayName +
                        " | día " + plan.basePlannedDeliveryGameDay + " " + FormatTime(plan.baseWindowStartMinuteOfDay) + "–" + FormatTime(plan.baseWindowEndMinuteOfDay) +
                        (plan.decidedDelayGameMinutes > 0 ? " | retraso " + plan.decidedDelayGameMinutes + " min → día " + plan.plannedDeliveryGameDay + " " + FormatTime(plan.windowStartMinuteOfDay) + "–" + FormatTime(plan.windowEndMinuteOfDay) : " | puntual") +
                        " | " + plan.resolvedVehicle + " | viajes " + plan.suggestedTripCount);
                }
            }
            plansBySupplier[supplier.SupplierId] = localPlans;
            delayedBySupplier[supplier.SupplierId] = localDelayed;
            float pct = localPlans > 0 ? localDelayed * 100f / localPlans : 0f;
            float avg = localDelayed > 0 ? localDelayMinutes / (float)localDelayed : 0f;
            sb.AppendLine(supplier.displayName + " [" + supplier.reliabilityTier + " " + supplier.reliabilityValue.ToString("0.000") + "] → " + localDelayed + "/" + localPlans + " retrasados (" + pct.ToString("0.0") + " %), retraso medio " + avg.ToString("0") + " min.");
        }

        sb.AppendLine();
        sb.AppendLine("RESUMEN");
        sb.AppendLine("Planes: " + totalPlans);
        sb.AppendLine("Retrasados: " + totalDelayed + " (" + (totalPlans > 0 ? totalDelayed * 100f / totalPlans : 0f).ToString("0.0") + " %)");
        sb.AppendLine("Retraso medio cuando ocurre: " + (totalDelayed > 0 ? totalDelayMinutes / (float)totalDelayed : 0f).ToString("0") + " min");
        sb.AppendLine("Vehículos: furgoneta " + vans + " / camión ligero " + trucks);
        sb.AppendLine("Regla: ningún pedido desaparece por fiabilidad; el resultado siempre es puntual o retrasado.");
        sb.AppendLine();
        sb.AppendLine("MUESTRAS");
        for (int index = 0; index < samples.Count; index++) sb.AppendLine("- " + samples[index]);
        report = sb.ToString();
    }

    private static string FormatTime(int minute) { return (minute / 60).ToString("D2") + ":" + (minute % 60).ToString("D2"); }
}
#endif
