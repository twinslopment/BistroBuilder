#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23IAutotestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> log = new List<string>();
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3I - Autotest progresión y desbloqueos")]
    private static void Open()
    {
        BistroBuilderSuppliers23IAutotestWindow window = GetWindow<BistroBuilderSuppliers23IAutotestWindow>(true, "Autotest 2.3I");
        window.minSize = new Vector2(820f, 480f);
        window.Run();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AUTOTEST 2.3I — Progresión de proveedores", EditorStyles.boldLabel);
        if (GUILayout.Button("Ejecutar autotest", GUILayout.Height(28f))) Run();
        EditorGUILayout.LabelField("Pruebas superadas: " + passed + " / Pruebas fallidas: " + failed, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < log.Count; i++) EditorGUILayout.SelectableLabel(log[i], GUILayout.Height(18f));
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        passed = 0; failed = 0; log.Clear();
        Check(!EditorApplication.isPlaying, "Autotest ejecutado en Edit Mode.");

        BistroBuilderSupplierProgressionSettings settings = BistroBuilderSuppliers23IPaths.LoadSettings();
        BistroBuilderSupplierAuthoringDatabase db = BistroBuilderSuppliers23IPaths.LoadSuppliers();
        Check(settings != null, "supplier.progression.settings existe.");
        Check(db != null, "supplier.authoring existe.");
        if (settings == null || db == null) return;
        Check(settings.SchemaId == BistroBuilderSupplierProgressionSettings.CurrentSchemaId, "schemaId canónico.");
        Check(settings.SchemaVersion == BistroBuilderSupplierProgressionSettings.CurrentSchemaVersion, "schemaVersion canónico.");
        Check(BistroBuilderSupplierProgressionEngine.IsPurchaseVolumeQualifiedStatus(BistroBuilderPurchaseOrderStatus.InDelivery, settings), "InDelivery cualifica volumen de compras.");
        Check(BistroBuilderSupplierProgressionEngine.IsPurchaseVolumeQualifiedStatus(BistroBuilderPurchaseOrderStatus.Delivered, settings), "Delivered cualifica volumen de compras.");
        Check(!BistroBuilderSupplierProgressionEngine.IsPurchaseVolumeQualifiedStatus(BistroBuilderPurchaseOrderStatus.Draft, settings), "Draft no cualifica volumen de compras.");
        Check(!BistroBuilderSupplierProgressionEngine.IsPurchaseVolumeQualifiedStatus(BistroBuilderPurchaseOrderStatus.Confirmed, settings), "Confirmed no cualifica todavía volumen de compras.");
        Check(!BistroBuilderSupplierProgressionEngine.IsPurchaseVolumeQualifiedStatus(BistroBuilderPurchaseOrderStatus.PendingDelivery, settings), "PendingDelivery no cualifica todavía volumen de compras.");
        Check(!BistroBuilderSupplierProgressionEngine.IsPurchaseVolumeQualifiedStatus(BistroBuilderPurchaseOrderStatus.Cancelled, settings), "Cancelled no cualifica volumen de compras.");

        List<BistroBuilderSupplierAuthoringRecord> active = new List<BistroBuilderSupplierAuthoringRecord>();
        db.CopySuppliers(active, true);
        Check(active.Count == 6, "Hay exactamente 6 proveedores activos.");
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < active.Count; i++) Check(active[i] != null && ids.Add(active[i].SupplierId), "SupplierId activo único: " + (active[i] != null ? active[i].SupplierId : "null"));

        BistroBuilderSupplierProgressionFacts day1 = Facts(1, 0, 0L);
        BistroBuilderSupplierProgressionFacts day3 = Facts(4, 3, 0L);
        BistroBuilderSupplierProgressionFacts rich = Facts(12, 11, 100000L);

        BistroBuilderSupplierAuthoringRecord mercado = Get(db, "supplier_mercado_central");
        BistroBuilderSupplierAuthoringRecord express = Get(db, "supplier_hosteleria_express");
        BistroBuilderSupplierAuthoringRecord norte = Get(db, "supplier_distribuciones_norte");
        BistroBuilderSupplierAuthoringRecord huerta = Get(db, "supplier_huerta_clara");
        BistroBuilderSupplierAuthoringRecord carnes = Get(db, "supplier_carnes_selectas");
        BistroBuilderSupplierAuthoringRecord costa = Get(db, "supplier_costa_fresca");
        Check(mercado != null && express != null && norte != null && huerta != null && carnes != null && costa != null, "Los seis proveedores provisionales se localizan.");
        if (mercado == null || express == null || norte == null || huerta == null || carnes == null || costa == null) return;

        Check(BistroBuilderSupplierProgressionEngine.Evaluate(mercado, day1).isUnlocked, "Mercado Central disponible desde el inicio.");
        Check(BistroBuilderSupplierProgressionEngine.Evaluate(express, day1).isUnlocked, "Hostelería Express disponible desde el inicio.");
        Check(!BistroBuilderSupplierProgressionEngine.Evaluate(norte, day1).isUnlocked, "Distribuciones Norte bloqueado sin volumen.");
        Check(!BistroBuilderSupplierProgressionEngine.Evaluate(huerta, day1).isUnlocked, "Huerta Clara bloqueada al inicio.");
        Check(BistroBuilderSupplierProgressionEngine.Evaluate(huerta, day3).isUnlocked, "Huerta Clara se desbloquea al alcanzar 3 días abierto.");
        Check(!BistroBuilderSupplierProgressionEngine.Evaluate(carnes, day3).isUnlocked, "Carnes Selectas exige condiciones AND y sigue bloqueado en día 4.");
        Check(BistroBuilderSupplierProgressionEngine.Evaluate(norte, rich).isUnlocked, "Distribuciones Norte se desbloquea con volumen suficiente.");
        Check(BistroBuilderSupplierProgressionEngine.Evaluate(carnes, rich).isUnlocked, "Carnes Selectas se desbloquea con días + volumen.");
        Check(BistroBuilderSupplierProgressionEngine.Evaluate(costa, rich).isUnlocked, "Costa Fresca se desbloquea con días + volumen.");

        TestAllRuleKinds();

        BistroBuilderSupplierProgressionSnapshot snapshot = BistroBuilderSupplierProgressionEngine.CreateInitialSnapshot(active, 1, 123UL, 456UL);
        Check(snapshot != null, "Snapshot inicial se crea.");
        Check(snapshot.schemaId == BistroBuilderSupplierProgressionSnapshot.CurrentSchemaId, "Snapshot usa schemaId canónico.");
        Check(snapshot.schemaVersion == BistroBuilderSupplierProgressionSnapshot.CurrentSchemaVersion, "Snapshot usa schemaVersion canónico.");
        Check(snapshot.sourceMarketSeed == 123UL && snapshot.sourceCommercialSeed == 456UL, "Snapshot conserva semillas 2.3C/2.3D.");
        Check(snapshot.suppliers.Count == 6, "Snapshot contiene los seis estados de proveedor.");
        int initialUnlocked = 0;
        for (int i = 0; i < snapshot.suppliers.Count; i++) if (snapshot.suppliers[i] != null && snapshot.suppliers[i].unlocked) initialUnlocked++;
        Check(initialUnlocked == 2, "Snapshot inicial desbloquea exactamente dos proveedores.");
        BistroBuilderSupplierProgressionSnapshot clone = snapshot.DeepClone();
        Check(clone != snapshot && clone.suppliers != snapshot.suppliers, "Snapshot clona profundamente la lista de estados.");
        clone.qualifiedPurchaseVolumeCents = 99999L;
        Check(snapshot.qualifiedPurchaseVolumeCents == 0L, "Modificar clon no muta snapshot original.");

        BistroBuilderSupplierProgressionFactBuilder builder = new BistroBuilderSupplierProgressionFactBuilder(day1);
        builder.SetLifetimeRevenueCents(800000L);
        builder.SetReputationPoints(82);
        builder.SetRestaurantCapacitySeats(56);
        builder.AddCuisineCategory("Asturiana");
        builder.AddCuisineCategory("asturiana");
        builder.SetIngredientFamilyConsumptionMicrounits("Pescados y Mariscos", 25000000L);
        BistroBuilderSupplierProgressionFacts built = builder.Build();
        Check(built.hasLifetimeRevenue && built.lifetimeRevenueCents == 800000L, "FactBuilder incorpora facturación sin ser autoridad de Finanzas.");
        Check(built.hasReputation && built.reputationPoints == 82, "FactBuilder incorpora reputación.");
        Check(built.hasRestaurantCapacity && built.restaurantCapacitySeats == 56, "FactBuilder incorpora tamaño/capacidad.");
        Check(built.hasCuisineCategories && built.cuisineCategories.Count == 1, "FactBuilder normaliza/deduplica categoría culinaria.");
        Check(built.ingredientFamilyConsumption.Count == 1 && built.ingredientFamilyConsumption[0].consumedMicrounits == 25000000L, "FactBuilder incorpora consumo de familia.");

        BistroBuilderSupplierProgressionFacts builtClone = built.DeepClone();
        builtClone.cuisineCategories.Add("otra");
        Check(built.cuisineCategories.Count == 1, "Facts realiza clonación defensiva.");
        Check(BistroBuilderSupplierProgressionEngine.NormalizeToken("Pescados y Mariscos") == "pescados_y_mariscos", "Normalización de tokens estable.");

        string supplierJsonBefore = EditorJsonUtility.ToJson(db);
        for (int i = 0; i < 20; i++) BistroBuilderSupplierProgressionEngine.Evaluate(carnes, rich);
        string supplierJsonAfter = EditorJsonUtility.ToJson(db);
        Check(supplierJsonBefore == supplierJsonAfter, "El motor puro no modifica supplier.authoring.");
        Check(BistroBuilderSuppliers23IPaths.LoadSettings() == settings, "El autotest no reemplaza supplier.progression.settings.");
    }

    private void TestAllRuleKinds()
    {
        BistroBuilderSupplierProgressionFacts facts = Facts(15, 14, 60000L);
        facts.hasLifetimeRevenue = true; facts.lifetimeRevenueCents = 1000000L;
        facts.hasReputation = true; facts.reputationPoints = 75;
        facts.hasRestaurantCapacity = true; facts.restaurantCapacitySeats = 48;
        facts.hasCuisineCategories = true; facts.cuisineCategories.Add("asturiana");
        facts.ingredientFamilyConsumption.Add(new BistroBuilderSupplierIngredientFamilyConsumptionFact { familyId = "pescados_y_mariscos", consumedMicrounits = 5000000L });

        Check(Eval(BistroBuilderSupplierUnlockRuleKind.DiasAbierto, 10, null, facts).satisfied, "Regla DiasAbierto evaluable.");
        Check(Eval(BistroBuilderSupplierUnlockRuleKind.VolumenComprasCentimos, 50000, null, facts).satisfied, "Regla VolumenComprasCentimos evaluable.");
        Check(Eval(BistroBuilderSupplierUnlockRuleKind.FacturacionCentimos, 900000, null, facts).satisfied, "Regla FacturacionCentimos evaluable con fuente explícita.");
        Check(Eval(BistroBuilderSupplierUnlockRuleKind.Reputacion, 70, null, facts).satisfied, "Regla Reputacion evaluable con fuente explícita.");
        Check(Eval(BistroBuilderSupplierUnlockRuleKind.TamanoRestaurante, 40, null, facts).satisfied, "Regla TamanoRestaurante evaluable con fuente explícita.");
        Check(Eval(BistroBuilderSupplierUnlockRuleKind.CategoriaCulinaria, 0, "Asturiana", facts).satisfied, "Regla CategoriaCulinaria normaliza texto.");
        Check(Eval(BistroBuilderSupplierUnlockRuleKind.ConsumoFamiliaIngrediente, 4000000, "Pescados y Mariscos", facts).satisfied, "Regla ConsumoFamiliaIngrediente evaluable.");

        BistroBuilderSupplierProgressionFacts missing = Facts(1, 0, 0L);
        BistroBuilderSupplierUnlockConditionResult missingRevenue = Eval(BistroBuilderSupplierUnlockRuleKind.FacturacionCentimos, 1, null, missing);
        Check(!missingRevenue.sourceAvailable && !missingRevenue.satisfied, "Facturación falla cerrada si no existe fuente canónica.");
        BistroBuilderSupplierUnlockConditionResult missingCuisine = Eval(BistroBuilderSupplierUnlockRuleKind.CategoriaCulinaria, 0, "asturiana", missing);
        Check(!missingCuisine.sourceAvailable && !missingCuisine.satisfied, "Categoría culinaria falla cerrada si no existe fuente canónica.");
        BistroBuilderSupplierUnlockConditionResult none = Eval(BistroBuilderSupplierUnlockRuleKind.Ninguna, 0, null, facts);
        Check(!none.sourceAvailable && !none.satisfied, "Regla Ninguna nunca desbloquea accidentalmente.");
        Check(Eval(BistroBuilderSupplierUnlockRuleKind.Reputacion, 90, null, facts).progress01 > 0f && Eval(BistroBuilderSupplierUnlockRuleKind.Reputacion, 90, null, facts).progress01 < 1f, "Progreso parcial numérico queda acotado 0..1.");
    }

    private static BistroBuilderSupplierUnlockConditionResult Eval(BistroBuilderSupplierUnlockRuleKind kind, long numeric, string text, BistroBuilderSupplierProgressionFacts facts)
    {
        return BistroBuilderSupplierProgressionEngine.EvaluateCondition(new BistroBuilderSupplierUnlockConditionAuthoring { kind = kind, numericThreshold = numeric, stringThreshold = text }, facts);
    }

    private static BistroBuilderSupplierProgressionFacts Facts(int day, int daysOpen, long purchase)
    {
        return new BistroBuilderSupplierProgressionFacts { currentGameDay = day, daysOpen = daysOpen, qualifiedPurchaseVolumeCents = purchase };
    }

    private static BistroBuilderSupplierAuthoringRecord Get(BistroBuilderSupplierAuthoringDatabase db, string id)
    {
        BistroBuilderSupplierAuthoringRecord supplier; db.TryGetSupplier(id, out supplier); return supplier;
    }

    private void Check(bool condition, string message)
    {
        if (condition) { passed++; log.Add("[OK] " + message); }
        else { failed++; log.Add("[FALLO] " + message); }
    }
}
#endif
