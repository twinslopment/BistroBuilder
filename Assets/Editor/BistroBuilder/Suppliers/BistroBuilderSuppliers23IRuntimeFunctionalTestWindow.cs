#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23IRuntimeFunctionalTestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> log = new List<string>();
    private int passed;
    private int failed;
    private int capturedErrors;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3I - Prueba funcional runtime")]
    private static void Open()
    {
        BistroBuilderSuppliers23IRuntimeFunctionalTestWindow window = GetWindow<BistroBuilderSuppliers23IRuntimeFunctionalTestWindow>(true, "Prueba runtime 2.3I");
        window.minSize = new Vector2(860f, 520f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("PRUEBA FUNCIONAL RUNTIME 2.3I", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Debe ejecutarse en Play Mode. Usa hechos controlados, verifica desbloqueos permanentes y que 2.3F/creación de Draft respetan 2.3I. Restaura pedidos y progresión al finalizar.", MessageType.Info);
        if (GUILayout.Button("Ejecutar prueba completa", GUILayout.Height(30f))) Run();
        EditorGUILayout.LabelField("Correctos: " + passed + "  Fallos: " + failed + "  Errores/Excepciones/Asserts: " + capturedErrors, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < log.Count; i++) EditorGUILayout.SelectableLabel(log[i], GUILayout.Height(18f));
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        passed = 0; failed = 0; capturedErrors = 0; log.Clear();
        if (!EditorApplication.isPlaying)
        {
            Fail("La prueba debe ejecutarse en Play Mode.");
            return;
        }

        Application.logMessageReceived += CaptureLog;
        BistroBuilderSupplierProgressionSnapshot originalProgression = null;
        BistroBuilderSupplierPurchaseOrdersSnapshot originalOrders = null;
        BistroBuilderSupplierProgressionService progression = null;
        BistroBuilderSupplierPurchaseOrderService orders = null;
        try
        {
            BistroBuilderSupplierMarketService market = BistroBuilderSupplierMarketService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierMarketService>();
            BistroBuilderSupplierCommercialIntelligenceService commercial = BistroBuilderSupplierCommercialIntelligenceService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierCommercialIntelligenceService>();
            orders = BistroBuilderSupplierPurchaseOrderService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
            BistroBuilderSupplierSmartPurchaseService smart = BistroBuilderSupplierSmartPurchaseService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierSmartPurchaseService>();
            progression = BistroBuilderSupplierProgressionService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierProgressionService>();

            Check(market != null && market.IsInitialized, "2.3C runtime disponible e inicializado.");
            Check(commercial != null && commercial.IsInitialized, "2.3D runtime disponible e inicializado.");
            Check(orders != null && orders.IsInitialized, "2.3E runtime disponible e inicializado.");
            Check(smart != null && smart.IsInitialized, "2.3F runtime disponible e inicializado.");
            Check(progression != null && progression.IsInitialized, "Existe exactamente la autoridad funcional 2.3I e inicializa.");
            if (orders == null || progression == null || !orders.IsInitialized || !progression.IsInitialized) return;

            originalProgression = progression.CreateSnapshot();
            originalOrders = orders.CreateSnapshot();
            Check(originalProgression != null, "Snapshot original 2.3I capturado.");
            Check(originalOrders != null, "Snapshot original 2.3E capturado.");
            Check(originalProgression != null && originalProgression.sourceMarketSeed == orders.SourceMarketSeed, "Snapshot 2.3I vinculado a semilla real 2.3C.");
            Check(originalProgression != null && originalProgression.sourceCommercialSeed == orders.SourceCommercialSeed, "Snapshot 2.3I vinculado a semilla real 2.3D.");

            BistroBuilderSupplierProgressionFacts startFacts = new BistroBuilderSupplierProgressionFacts
            {
                currentGameDay = 1,
                daysOpen = 0,
                qualifiedPurchaseVolumeCents = 0L
            };
            Check(progression.EditorInitializeControlledState(startFacts), "2.3I inicia estado controlado día 1 / compras 0.");
            Check(progression.SupplierStateCount == 6, "Estado controlado contiene seis proveedores.");
            Check(progression.IsSupplierUnlocked("supplier_mercado_central"), "Mercado Central disponible desde el inicio.");
            Check(progression.IsSupplierUnlocked("supplier_hosteleria_express"), "Hostelería Express disponible desde el inicio.");
            Check(!progression.IsSupplierUnlocked("supplier_distribuciones_norte"), "Distribuciones Norte bloqueado al inicio.");
            Check(!progression.IsSupplierUnlocked("supplier_huerta_clara"), "Huerta Clara bloqueada al inicio.");
            Check(!progression.IsSupplierUnlocked("supplier_carnes_selectas"), "Carnes Selectas bloqueado al inicio.");
            Check(!progression.IsSupplierUnlocked("supplier_costa_fresca"), "Costa Fresca bloqueada al inicio.");

            int ordersBefore = orders.OrderCount;
            BistroBuilderPurchaseOrderRecord blockedDraft;
            string error;
            Check(!progression.TryCreatePlayerDraft("supplier_distribuciones_norte", out blockedDraft, out error), "Fachada de jugador bloquea Draft de proveedor no desbloqueado.");
            Check(blockedDraft == null, "Bloqueo de Draft no devuelve pedido parcial.");
            Check(orders.OrderCount == ordersBefore, "Bloqueo 2.3I no modifica 2.3E.");

            BistroBuilderSupplierAuthoringDatabase supplierDb = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSupplierProgressionService.SupplierAuthoringResourcePath);
            BistroBuilderSupplierAuthoringRecord norte = null;
            Check(supplierDb != null && supplierDb.TryGetSupplier("supplier_distribuciones_norte", out norte) && norte != null, "Autoría de Distribuciones Norte localizada.");
            BistroBuilderSupplierBaseOfferAuthoringRecord lockedOffer = FirstActiveOffer(norte);
            Check(lockedOffer != null, "Existe oferta activa de proveedor bloqueado para probar 2.3F.");
            if (smart != null && smart.IsInitialized && lockedOffer != null)
            {
                BistroBuilderSmartPurchasePlan lockedPlan = PlanFor(norte, lockedOffer);
                List<string> ids;
                Check(!smart.TryCreateDraftFromPlan(lockedPlan, out ids, out error), "2.3F no puede materializar un plan con proveedor bloqueado por 2.3I.");
                Check(ids != null && ids.Count == 0, "2.3F no deja PurchaseOrder parcial al bloquear progresión.");
                Check(orders.OrderCount == ordersBefore, "Gate 2.3F + 2.3I no modifica pedidos al rechazar.");
            }

            BistroBuilderSupplierProgressionFacts day4 = new BistroBuilderSupplierProgressionFacts
            {
                currentGameDay = 4,
                daysOpen = 3,
                qualifiedPurchaseVolumeCents = 0L
            };
            Check(progression.EditorSetControlledFacts(day4), "Hechos controlados avanzan a 3 días abierto.");
            Check(progression.IsSupplierUnlocked("supplier_huerta_clara"), "Huerta Clara se desbloquea por días.");
            Check(!progression.IsSupplierUnlocked("supplier_distribuciones_norte"), "Distribuciones Norte sigue bloqueado sin compras.");

            BistroBuilderSupplierProgressionFacts day12 = new BistroBuilderSupplierProgressionFacts
            {
                currentGameDay = 12,
                daysOpen = 11,
                qualifiedPurchaseVolumeCents = 100000L
            };
            Check(progression.EditorSetControlledFacts(day12), "Hechos controlados avanzan a día 12 y 1.000 € de volumen.");
            Check(progression.IsSupplierUnlocked("supplier_distribuciones_norte"), "Distribuciones Norte se desbloquea por volumen.");
            Check(progression.IsSupplierUnlocked("supplier_carnes_selectas"), "Carnes Selectas se desbloquea por días + volumen.");
            Check(progression.IsSupplierUnlocked("supplier_costa_fresca"), "Costa Fresca se desbloquea por días + volumen.");

            List<BistroBuilderSupplierAccessEvaluation> access = new List<BistroBuilderSupplierAccessEvaluation>();
            Check(progression.CopySupplierAccess(access, false) == 6, "Tras cumplir requisitos hay seis proveedores jugables.");
            bool allUnlocked = true;
            for (int i = 0; i < access.Count; i++) allUnlocked &= access[i] != null && access[i].isUnlocked;
            Check(allUnlocked, "Todos los accesos copiados como jugables están desbloqueados.");

            BistroBuilderPurchaseOrderRecord allowedDraft;
            Check(progression.TryCreatePlayerDraft("supplier_distribuciones_norte", out allowedDraft, out error), "Fachada de jugador delega a 2.3E cuando el proveedor está desbloqueado.");
            Check(allowedDraft != null && allowedDraft.status == BistroBuilderPurchaseOrderStatus.Draft, "Draft desbloqueado nace en estado Draft de 2.3E.");
            Check(orders.OrderCount == ordersBefore + 1, "2.3E recibe exactamente un Draft permitido.");
            Check(orders.TryRestoreSnapshot(originalOrders, out error), "Se restaura 2.3E tras probar Draft permitido.");
            Check(orders.OrderCount == ordersBefore, "Restauración elimina el Draft de prueba.");

            if (smart != null && smart.IsInitialized && lockedOffer != null)
            {
                BistroBuilderSmartPurchasePlan unlockedPlan = PlanFor(norte, lockedOffer);
                List<string> ids;
                Check(smart.TryCreateDraftFromPlan(unlockedPlan, out ids, out error), "2.3F puede materializar el mismo proveedor tras desbloquearlo.");
                Check(ids != null && ids.Count == 1, "2.3F crea exactamente un Draft para una cesta de un proveedor.");
                Check(orders.TryRestoreSnapshot(originalOrders, out error), "Se restaura 2.3E tras probar integración 2.3F.");
            }

            BistroBuilderSupplierProgressionSnapshot unlockedSnapshot = progression.CreateSnapshot();
            Check(unlockedSnapshot != null, "Snapshot 2.3I captura desbloqueos progresivos.");
            Check(unlockedSnapshot != null && CountUnlocked(unlockedSnapshot) == 6, "Snapshot de progresión conserva seis proveedores desbloqueados.");

            BistroBuilderSupplierProgressionFacts regressedFacts = new BistroBuilderSupplierProgressionFacts
            {
                currentGameDay = 1,
                daysOpen = 0,
                qualifiedPurchaseVolumeCents = 0L
            };
            Check(progression.EditorSetControlledFacts(regressedFacts), "Se reducen hechos controlados para probar permanencia.");
            Check(progression.IsSupplierUnlocked("supplier_costa_fresca"), "Un proveedor ya desbloqueado no vuelve a bloquearse aunque los hechos bajen.");
            Check(progression.IsSupplierUnlocked("supplier_carnes_selectas"), "Permanencia de desbloqueo se conserva en otro proveedor.");

            Check(unlockedSnapshot != null && progression.TryRestoreSnapshot(unlockedSnapshot, out error), "Snapshot 2.3I restaurable.");
            Check(progression.IsSupplierUnlocked("supplier_distribuciones_norte"), "Restauración conserva desbloqueos.");
            Check(progression.ProgressionRevision >= 1L, "ProgressionRevision válida.");
        }
        catch (Exception exception)
        {
            Fail("Excepción de la prueba: " + exception.GetType().Name + " - " + exception.Message);
        }
        finally
        {
            string restoreError;
            if (orders != null && originalOrders != null)
            {
                Check(orders.TryRestoreSnapshot(originalOrders, out restoreError), "Se restaura snapshot original 2.3E.");
            }
            if (progression != null && originalProgression != null)
            {
                progression.EditorClearControlledFacts();
                Check(progression.TryRestoreSnapshot(originalProgression, out restoreError), "Se restaura snapshot original 2.3I.");
            }
            Application.logMessageReceived -= CaptureLog;
            Check(capturedErrors == 0, "La prueba no capturó Error/Exception/Assert.");
        }
    }

    private static BistroBuilderSupplierBaseOfferAuthoringRecord FirstActiveOffer(BistroBuilderSupplierAuthoringRecord supplier)
    {
        if (supplier == null || supplier.baseOffers == null) return null;
        for (int i = 0; i < supplier.baseOffers.Count; i++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[i];
            if (offer != null && offer.isActive) return offer;
        }
        return null;
    }

    private static BistroBuilderSmartPurchasePlan PlanFor(BistroBuilderSupplierAuthoringRecord supplier, BistroBuilderSupplierBaseOfferAuthoringRecord offer)
    {
        BistroBuilderSmartPurchaseCandidate candidate = new BistroBuilderSmartPurchaseCandidate
        {
            supplierId = supplier.SupplierId,
            supplierDisplayName = supplier.displayName,
            supplierOfferId = offer.SupplierOfferId,
            ingredientId = offer.ingredientId,
            packageFormatId = offer.packageFormatId,
            packageCount = Math.Max(1, offer.minimumPackageCount)
        };
        BistroBuilderSmartPurchaseIngredientRecommendation recommendation = new BistroBuilderSmartPurchaseIngredientRecommendation
        {
            ingredientId = offer.ingredientId,
            selected = candidate
        };
        BistroBuilderSmartPurchasePlan plan = new BistroBuilderSmartPurchasePlan
        {
            strategy = BistroBuilderSmartPurchaseStrategy.Equilibrado,
            ingredientsRecommended = 1,
            supplierCount = 1
        };
        plan.ingredients.Add(recommendation);
        return plan;
    }

    private static int CountUnlocked(BistroBuilderSupplierProgressionSnapshot snapshot)
    {
        int count = 0;
        if (snapshot == null || snapshot.suppliers == null) return count;
        for (int i = 0; i < snapshot.suppliers.Count; i++) if (snapshot.suppliers[i] != null && snapshot.suppliers[i].unlocked) count++;
        return count;
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) capturedErrors++;
    }

    private void Check(bool condition, string message)
    {
        if (condition) { passed++; log.Add("[OK] " + message); }
        else Fail(message);
    }

    private void Fail(string message)
    {
        failed++; log.Add("[FALLO] " + message);
    }
}
#endif
