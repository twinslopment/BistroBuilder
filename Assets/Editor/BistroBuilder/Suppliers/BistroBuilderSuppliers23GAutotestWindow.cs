#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23GAutotestWindow : EditorWindow
{
    private readonly List<string> results = new List<string>();
    private Vector2 scroll;
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3G - Autotest planificación logística")]
    public static void Open()
    {
        BistroBuilderSuppliers23GAutotestWindow window = GetWindow<BistroBuilderSuppliers23GAutotestWindow>("Autotest 2.3G");
        window.minSize = new Vector2(900f, 620f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AUTOTEST 2.3G — Planificación logística", EditorStyles.boldLabel);
        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("Ejecutar autotest", GUILayout.Height(30f))) Run();
        GUI.enabled = true;
        EditorGUILayout.LabelField("Pruebas superadas: " + passed + " / Pruebas fallidas: " + failed, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < results.Count; index++) EditorGUILayout.LabelField(results[index], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        results.Clear(); passed = 0; failed = 0;
        Check(!EditorApplication.isPlaying, "Autotest ejecutado en Edit Mode.");
        BistroBuilderSupplierLogisticsPlanningSettings settings = AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierLogisticsPlanningSettings>(BistroBuilderSuppliers23GPaths.LogisticsSettingsPath);
        BistroBuilderSupplierAuthoringDatabase suppliers = AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23GPaths.SupplierDatabasePath);
        Check(settings != null, "supplier.logistics.settings existe.");
        Check(suppliers != null, "supplier.authoring existe.");
        if (settings == null || suppliers == null) return;
        Check(settings.SchemaId == BistroBuilderSupplierLogisticsPlanningSettings.CurrentSchemaId, "schemaId canónico.");
        Check(settings.SchemaVersion == 1, "schemaVersion canónico.");
        Check(settings.MaximumSuggestedTrips >= 1 && settings.MaximumSuggestedTrips <= 3, "Máximo de viajes visuales acotado a 1..3.");
        Check(settings.FallbackWindowEndMinuteOfDay > settings.FallbackWindowStartMinuteOfDay, "Ventana fallback válida.");

        int active = 0;
        for (int index = 0; index < suppliers.Suppliers.Count; index++) if (suppliers.Suppliers[index] != null && suppliers.Suppliers[index].isActive) active++;
        Check(active == 6, "Hay exactamente 6 proveedores activos.");

        BistroBuilderSupplierAuthoringRecord sample = BistroBuilderSuppliers23GTestData.FirstActiveSupplier(suppliers);
        Check(sample != null, "Existe proveedor activo para pruebas.");
        if (sample == null) return;

        BistroBuilderSupplierLogisticsSnapshot snapshotA = BistroBuilderSupplierLogisticsPlanningEngine.CreateInitialSnapshot(1, 101UL, 202UL, settings);
        BistroBuilderSupplierLogisticsSnapshot snapshotB = BistroBuilderSupplierLogisticsPlanningEngine.CreateInitialSnapshot(1, 101UL, 202UL, settings);
        Check(snapshotA.logisticsSeed == snapshotB.logisticsSeed && snapshotA.logisticsSeed != 0UL, "Misma sesión produce la misma semilla logística no nula.");
        Check(snapshotA.schemaId == BistroBuilderSupplierLogisticsSnapshot.CurrentSchemaId, "Snapshot runtime usa schema canónico.");

        BistroBuilderPurchaseOrderRecord orderA = BistroBuilderSuppliers23GTestData.BuildConfirmedOrder(sample, "purchase_order_autotest", 1, 24f, 10, BistroBuilderCommercialPackageLogisticSize.Medio);
        BistroBuilderPurchaseOrderRecord orderB = orderA.DeepClone();
        BistroBuilderSupplierLogisticsPlanRecord planA, planB;
        string error;
        bool builtA = BistroBuilderSupplierLogisticsPlanningEngine.TryBuildPlan(snapshotA, orderA, sample, settings, 1, out planA, out error);
        bool builtB = BistroBuilderSupplierLogisticsPlanningEngine.TryBuildPlan(snapshotB, orderB, sample, settings, 1, out planB, out error);
        Check(builtA && builtB, "Dos planes controlados se construyen sin error.");
        if (!builtA || !builtB) return;
        Check(planA.plannedDeliveryGameDay >= 1, "Fecha planificada nunca queda antes de día 1.");
        Check(planA.windowStartMinuteOfDay >= 0 && planA.windowEndMinuteOfDay > planA.windowStartMinuteOfDay && planA.windowEndMinuteOfDay <= 1440, "Ventana planificada válida.");
        Check(planA.delayProbabilityBasisPoints > 0 && planA.delayProbabilityBasisPoints <= settings.MaximumDelayChanceBasisPoints, "Probabilidad de retraso acotada.");
        Check(planA.deterministicDelayRollBasisPoints >= 0 && planA.deterministicDelayRollBasisPoints < 10000, "Roll determinista acotado.");
        Check(planA.deterministicDelayRollBasisPoints == planB.deterministicDelayRollBasisPoints, "Misma semilla + mismo pedido = mismo roll.");
        Check(planA.decidedDelayGameMinutes == planB.decidedDelayGameMinutes, "Misma semilla + mismo pedido = mismo retraso decidido.");
        Check(planA.logisticsLoadUnits == planB.logisticsLoadUnits, "Carga logística determinista.");
        Check(planA.visualLoadUnits >= 1, "Carga visual abstracta positiva.");
        Check(planA.suggestedTripCount >= 1 && planA.suggestedTripCount <= 3, "Viajes visuales sugeridos 1..3.");
        Check(planA.resolvedVehicle == BistroBuilderSupplierVehiclePreference.Furgoneta || planA.resolvedVehicle == BistroBuilderSupplierVehiclePreference.CamionLigero, "Vehículo automático resuelve a furgoneta o camión ligero.");
        Check(!string.IsNullOrWhiteSpace(planA.reasonCode) && !string.IsNullOrWhiteSpace(planA.reasonText), "Plan publica razón explicable.");

        BistroBuilderPurchaseOrderRecord express = BistroBuilderSuppliers23GTestData.BuildConfirmedOrder(sample, "purchase_order_express", 1, 6f, 1, BistroBuilderCommercialPackageLogisticSize.Pequeno);
        BistroBuilderSupplierLogisticsPlanRecord expressPlan;
        Check(BistroBuilderSupplierLogisticsPlanningEngine.TryBuildPlan(snapshotA, express, sample, settings, 1, out expressPlan, out error), "Lead time 6 h se puede planificar.");
        Check(expressPlan.plannedDeliveryGameDay <= planA.plannedDeliveryGameDay, "Lead time corto no llega después que lead time 24 h en el mismo proveedor.");

        BistroBuilderPurchaseOrderRecord heavy = BistroBuilderSuppliers23GTestData.BuildConfirmedOrder(sample, "purchase_order_heavy", 1, 24f, 20, BistroBuilderCommercialPackageLogisticSize.Grande);
        BistroBuilderSupplierLogisticsPlanRecord heavyPlan;
        Check(BistroBuilderSupplierLogisticsPlanningEngine.TryBuildPlan(snapshotA, heavy, sample, settings, 1, out heavyPlan, out error), "Carga grande se planifica.");
        Check(heavyPlan.logisticsLoadUnits > planA.logisticsLoadUnits, "Más paquetes grandes generan más carga logística.");
        if (sample.logisticsProfile == null || sample.logisticsProfile.preferredVehicle == BistroBuilderSupplierVehiclePreference.Automatico)
            Check(heavyPlan.resolvedVehicle == BistroBuilderSupplierVehiclePreference.CamionLigero, "Carga pesada automática selecciona camión ligero.");

        BistroBuilderSupplierLogisticsPlanRecord delayedClone = planA.DeepClone();
        delayedClone.decidedDelayGameMinutes = Math.Max(30, delayedClone.decidedDelayGameMinutes);
        bool changed;
        Check(BistroBuilderSupplierLogisticsPlanningEngine.TryApplyDelay(delayedClone, delayedClone.basePlannedDeliveryGameDay - 1, out changed, out error) && !changed, "El retraso no se aplica antes del día previsto.");
        Check(BistroBuilderSupplierLogisticsPlanningEngine.TryApplyDelay(delayedClone, delayedClone.basePlannedDeliveryGameDay, out changed, out error) && changed, "El retraso se aplica al llegar el día previsto.");
        int delayedDay = delayedClone.plannedDeliveryGameDay;
        int delayedStart = delayedClone.windowStartMinuteOfDay;
        Check(BistroBuilderSupplierLogisticsPlanningEngine.TryApplyDelay(delayedClone, delayedClone.basePlannedDeliveryGameDay, out changed, out error) && !changed, "Aplicar el mismo retraso es idempotente.");
        Check(delayedClone.plannedDeliveryGameDay == delayedDay && delayedClone.windowStartMinuteOfDay == delayedStart, "Idempotencia conserva fecha/ventana.");
        Check(delayedClone.delayApplied, "Plan registra delayApplied.");
        Check(delayedClone.reasonCode == "reliability_delay_applied", "Retraso aplicado publica razón trazable.");

        snapshotA.plans.Add(planA.DeepClone());
        snapshotA.nextPlanSequence = 2;
        Check(BistroBuilderSupplierLogisticsPlanningEngine.ValidateSnapshot(snapshotA, out error), "Snapshot logístico válido supera validación.");
        BistroBuilderSupplierLogisticsSnapshot clone = snapshotA.DeepClone();
        Check(!ReferenceEquals(snapshotA, clone) && !ReferenceEquals(snapshotA.plans, clone.plans), "DeepClone no comparte colecciones.");
        Check(clone.plans.Count == snapshotA.plans.Count && clone.plans[0].logisticsPlanId == snapshotA.plans[0].logisticsPlanId, "DeepClone conserva contenido.");

        // Comprobar que mayor fiabilidad nunca produce mayor probabilidad con el mismo resto de parámetros.
        BistroBuilderPurchaseOrderRecord reliabilityOrder = orderA.DeepClone();
        reliabilityOrder.purchaseOrderId = "purchase_order_reliability";
        reliabilityOrder.supplierTerms.reliabilityTier = BistroBuilderSupplierReliabilityTier.Excelente;
        reliabilityOrder.supplierTerms.reliabilityValue = 0.99f;
        BistroBuilderSupplierLogisticsPlanRecord excellentPlan;
        BistroBuilderSupplierLogisticsPlanningEngine.TryBuildPlan(snapshotA, reliabilityOrder, sample, settings, 1, out excellentPlan, out error);
        reliabilityOrder.supplierTerms.reliabilityTier = BistroBuilderSupplierReliabilityTier.Irregular;
        reliabilityOrder.supplierTerms.reliabilityValue = 0.75f;
        BistroBuilderSupplierLogisticsPlanRecord irregularPlan;
        BistroBuilderSupplierLogisticsPlanningEngine.TryBuildPlan(snapshotA, reliabilityOrder, sample, settings, 1, out irregularPlan, out error);
        Check(excellentPlan.delayProbabilityBasisPoints < irregularPlan.delayProbabilityBasisPoints, "Excelente tiene menor probabilidad de retraso que Irregular.");
        Check(planA.decidedDelayGameMinutes == 0 || planA.decidedDelayGameMinutes >= Math.Max(0, sample.logisticsProfile != null ? sample.logisticsProfile.minimumDelayMinutes : settings.FallbackMinimumDelayMinutes), "Retraso decidido respeta mínimo configurado.");
        Check(planA.decidedDelayGameMinutes == 0 || planA.decidedDelayGameMinutes <= Math.Max(settings.FallbackMaximumDelayMinutes, sample.logisticsProfile != null ? sample.logisticsProfile.maximumDelayMinutes : 0), "Retraso decidido respeta máximo configurado.");
        Check(planA.status == BistroBuilderSupplierLogisticsPlanStatus.Planned, "Un plan nuevo nace Planned.");
        Check(planA.purchaseOrderId == orderA.purchaseOrderId && planA.supplierId == orderA.supplierId, "Plan conserva FK a PurchaseOrder y Supplier.");
        Check(planA.basePlannedDeliveryGameDay == planA.plannedDeliveryGameDay, "Antes de retraso, fecha base y actual coinciden.");
        Check(planA.baseWindowStartMinuteOfDay == planA.windowStartMinuteOfDay && planA.baseWindowEndMinuteOfDay == planA.windowEndMinuteOfDay, "Antes de retraso, ventana base y actual coinciden.");
    }

    private void Check(bool condition, string message)
    {
        if (condition) { passed++; results.Add("[OK] " + message); }
        else { failed++; results.Add("[FALLO] " + message); }
    }
}
#endif
