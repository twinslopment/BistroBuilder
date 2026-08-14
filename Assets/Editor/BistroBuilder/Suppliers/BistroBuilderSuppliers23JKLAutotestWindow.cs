using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderSuppliers23JKLAutotestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> lines = new List<string>();
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3JKL - Autotest persistencia e integración", false, 2902)]
    private static void Open()
    {
        GetWindow<BistroBuilderSuppliers23JKLAutotestWindow>("Autotest 2.3JKL");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AUTOTEST 2.3JKL — Persistencia + UI + bridge", EditorStyles.boldLabel);
        if (GUILayout.Button("Ejecutar autotest", GUILayout.Height(30f))) Run();
        EditorGUILayout.LabelField("Pruebas superadas: " + passed + " / Pruebas fallidas: " + failed, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < lines.Count; i++) EditorGUILayout.LabelField(lines[i], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        passed = 0;
        failed = 0;
        lines.Clear();

        BistroBuilderSupplierIntegratedSaveState state = BuildSyntheticState();
        Check(state != null, "Se construye snapshot integrado sintético.");
        Check(state.TryValidateBasic(out string error), "Snapshot integrado coherente valida. " + error);
        Check(state.schemaId == BistroBuilderSupplierIntegratedSaveState.CurrentSchemaId, "schemaId integrado canónico.");
        Check(state.schemaVersion == 1, "schemaVersion integrado canónico.");
        Check(state.market.marketSeed == 111UL, "MarketSeed preservada.");
        Check(state.commercial.sourceMarketSeed == 111UL, "2.3D enlazado a 2.3C.");
        Check(state.commercial.commercialSeed == 222UL, "CommercialSeed preservada.");
        Check(state.orders.sourceMarketSeed == 111UL, "2.3E enlazado a 2.3C.");
        Check(state.orders.sourceCommercialSeed == 222UL, "2.3E enlazado a 2.3D.");
        Check(state.logistics.logisticsSeed == 333UL, "LogisticsSeed preservada.");
        Check(state.logistics.sourceMarketSeed == 111UL, "2.3G enlazado a 2.3C.");
        Check(state.logistics.sourceCommercialSeed == 222UL, "2.3G enlazado a 2.3D.");
        Check(state.deliveryPresentation.sourceLogisticsSeed == 333UL, "2.3H enlazado a 2.3G.");
        Check(state.progression.sourceMarketSeed == 111UL, "2.3I enlazado a 2.3C.");
        Check(state.progression.sourceCommercialSeed == 222UL, "2.3I enlazado a 2.3D.");
        Check(state.market.currentGameDay == 12 && state.commercial.currentGameDay == 12 &&
              state.orders.currentGameDay == 12 && state.logistics.currentGameDay == 12 &&
              state.deliveryPresentation.currentGameDay == 12 && state.progression.currentGameDay == 12,
              "Todas las subsecciones comparten día.");

        string fingerprint = state.BuildFingerprint();
        Check(!string.IsNullOrWhiteSpace(fingerprint) && fingerprint.Length == 16, "Fingerprint diagnóstico de 16 hex generado.");
        BistroBuilderSupplierIntegratedSaveState clone = state.DeepClone();
        Check(clone != state, "DeepClone crea raíz independiente.");
        Check(clone.market != state.market && clone.commercial != state.commercial && clone.orders != state.orders,
              "DeepClone no comparte snapshots C/D/E.");
        Check(clone.logistics != state.logistics && clone.deliveryPresentation != state.deliveryPresentation && clone.progression != state.progression,
              "DeepClone no comparte snapshots G/H/I.");
        Check(clone.BuildFingerprint() == fingerprint, "DeepClone conserva fingerprint.");
        clone.market.marketRevision++;
        Check(clone.BuildFingerprint() != fingerprint, "Cambiar mercado cambia fingerprint.");
        Check(state.market.marketRevision != clone.market.marketRevision, "Mutar clon no muta original.");

        string json = JsonUtility.ToJson(state, false);
        Check(!string.IsNullOrWhiteSpace(json) && json.Contains("supplier.integrated.runtime"), "JsonUtility serializa la sección integrada.");
        BistroBuilderSupplierIntegratedSaveState roundtrip = JsonUtility.FromJson<BistroBuilderSupplierIntegratedSaveState>(json);
        Check(roundtrip != null, "JsonUtility reconstruye la sección integrada.");
        Check(roundtrip.TryValidateBasic(out error), "Round-trip JSON vuelve a validar. " + error);
        Check(roundtrip.BuildFingerprint() == fingerprint, "Round-trip JSON conserva fingerprint.");

        BistroBuilderSupplierIntegratedSaveState broken = state.DeepClone();
        broken.market = null;
        Check(!broken.TryValidateBasic(out _), "Snapshot sin 2.3C se rechaza.");
        broken = state.DeepClone(); broken.commercial.sourceMarketSeed = 999UL;
        Check(!broken.TryValidateBasic(out _), "Seed 2.3D incompatible se rechaza.");
        broken = state.DeepClone(); broken.orders.currentGameDay++;
        Check(!broken.TryValidateBasic(out _), "Día 2.3E divergente se rechaza.");
        broken = state.DeepClone(); broken.logistics.logisticsSeed = 0UL;
        Check(!broken.TryValidateBasic(out _), "LogisticsSeed cero se rechaza.");
        broken = state.DeepClone(); broken.deliveryPresentation.sourceLogisticsSeed = 999UL;
        Check(!broken.TryValidateBasic(out _), "2.3H con otra sesión logística se rechaza.");
        broken = state.DeepClone(); broken.progression.sourceCommercialSeed = 999UL;
        Check(!broken.TryValidateBasic(out _), "2.3I con otra sesión comercial se rechaza.");
        broken = state.DeepClone(); broken.orders.sourceMarketSeed = 0UL; broken.orders.sourceCommercialSeed = 0UL;
        Check(broken.TryValidateBasic(out _), "2.3E todavía sin vínculo explícito puede persistirse si no contradice sesión.");

        BistroBuilderSupplierReceivingHandoff handoff = new BistroBuilderSupplierReceivingHandoff
        {
            purchaseOrderId = "purchase_order_00000001",
            logisticsPlanId = "logistics_plan_00000001",
            supplierId = "supplier_mercado_central",
            lines = new List<BistroBuilderSupplierDeliveryManifestLine>
            {
                new BistroBuilderSupplierDeliveryManifestLine { ingredientId="ingredient_a", packageCount=1, totalNetQuantityMicrounits=1000000L },
                new BistroBuilderSupplierDeliveryManifestLine { ingredientId="ingredient_a", packageCount=2, totalNetQuantityMicrounits=2000000L },
                new BistroBuilderSupplierDeliveryManifestLine { ingredientId="ingredient_b", packageCount=1, totalNetQuantityMicrounits=500000L }
            }
        };
        Check(BistroBuilderSupplierReceivingBridge23L.TryConvertHandoffLines(handoff, out List<BistroBuilderInventoryQuantityLine> converted, out error),
              "Handoff válido convierte a 2.2B. " + error);
        Check(converted.Count == 2, "Líneas duplicadas por ingrediente se agregan.");
        long a = FindQuantity(converted, "ingredient_a");
        long b = FindQuantity(converted, "ingredient_b");
        Check(a == 3000L, "3.000.000 micro → 3.000 milli exactos.");
        Check(b == 500L, "500.000 micro → 500 milli exactos.");
        Check(BistroBuilderSupplierReceivingBridge23L.BuildReceiptId("purchase_order_00000001") ==
              "receipt_supplier_purchase_order_00000001", "ReceiptId determinista por PurchaseOrder.");
        BistroBuilderSupplierReceivingHandoff invalidPrecision = handoff.DeepClone();
        invalidPrecision.lines[0].totalNetQuantityMicrounits = 1000001L;
        Check(!BistroBuilderSupplierReceivingBridge23L.TryConvertHandoffLines(invalidPrecision, out _, out _),
              "Conversión con pérdida de precisión se rechaza.");
        BistroBuilderSupplierReceivingHandoff empty = new BistroBuilderSupplierReceivingHandoff();
        Check(!BistroBuilderSupplierReceivingBridge23L.TryConvertHandoffLines(empty, out _, out _), "Handoff vacío se rechaza.");

        Check(BistroBuilderSupplierPlayerUiFormat.Money(12345L).Contains("123"), "UI formatea dinero.");
        Check(BistroBuilderSupplierPlayerUiFormat.NormalizedPrice(250L, 1000000000L, "Gram").Contains("€/kg"),
              "UI publica precio normalizado legible para formatos por peso.");
        Check(BistroBuilderSupplierPlayerUiFormat.Availability(BistroBuilderSupplierOfferAvailability.StockLimitado) == "Stock limitado", "UI humaniza StockLimitado.");
        Check(BistroBuilderSupplierPlayerUiFormat.OrderStatus(BistroBuilderPurchaseOrderStatus.PendingDelivery) == "Pendiente de entrega", "UI humaniza estado de pedido.");
        Check(BistroBuilderSupplierPlayerUiFormat.Strategy(BistroBuilderSmartPurchaseStrategy.Ahorrar) == "Ahorrar", "UI humaniza estrategia Ahorrar.");
        Check(BistroBuilderSupplierPlayerUiFormat.Strategy(BistroBuilderSmartPurchaseStrategy.Equilibrado) == "Equilibrado", "UI humaniza estrategia Equilibrado.");
        Check(BistroBuilderSupplierPlayerUiFormat.Strategy(BistroBuilderSmartPurchaseStrategy.Urgente) == "Urgente", "UI humaniza estrategia Urgente.");
        Check(BistroBuilderSupplierPlayerUiFormat.Risk(BistroBuilderSmartPurchaseRisk.Critico) == "Crítico", "UI humaniza riesgo crítico.");

        Scene scene = SceneManager.GetActiveScene();
        GameObject gs = BistroBuilderSuppliers23JKLInstaller.FindGameSystems(scene);
        Check(gs != null, "GameSystems localizado.");
        if (gs != null)
        {
            BistroBuilderSaveGameService save = gs.GetComponent<BistroBuilderSaveGameService>();
            BistroBuilderSupplierIntegratedSaveSectionProvider provider = gs.GetComponent<BistroBuilderSupplierIntegratedSaveSectionProvider>();
            BistroBuilderSupplierReceivingBridge23L bridge = gs.GetComponent<BistroBuilderSupplierReceivingBridge23L>();
            Check(save != null, "SaveGameService existente.");
            Check(provider != null, "Provider integrado instalado.");
            Check(bridge != null, "Bridge 2.3L instalado.");
            if (save != null)
            {
                save.RefreshExtensions();
                Check(save.HasProvider(BistroBuilderSupplierIntegratedSaveSectionProvider.StableSectionId), "SaveGameService registra provider integrado.");
            }
            if (provider != null)
            {
                Check(provider.SectionId == "supplier.integrated.runtime", "SectionId estable.");
                Check(provider.SectionVersion == 1, "SectionVersion estable.");
                Check(provider.SerializerId == BistroBuilderJsonSaveSerializer.StableSerializerId, "Serializer JSON canónico.");
                Check(!provider.IsRequired, "Sección opcional permite cargar partidas antiguas.");
            }
        }

        BistroBuilderSuppliers23JKLValidationResult validation =
            BistroBuilderSuppliers23JKLValidationWindow.ValidateCurrentProject();
        Check(validation.ErrorCount == 0, "Validación estructural 2.3JKL sin errores (errores=" + validation.ErrorCount + ").");

        Repaint();
    }

    private static BistroBuilderSupplierIntegratedSaveState BuildSyntheticState()
    {
        return new BistroBuilderSupplierIntegratedSaveState
        {
            market = new BistroBuilderSupplierMarketSnapshot
            {
                marketSeed = 111UL,
                currentGameDay = 12,
                lastReviewGameDay = 10,
                nextReviewGameDay = 15,
                marketRevision = 3
            },
            commercial = new BistroBuilderSupplierCommercialIntelligenceSnapshot
            {
                sourceMarketSeed = 111UL,
                commercialSeed = 222UL,
                currentGameDay = 12,
                commercialRevision = 4
            },
            orders = new BistroBuilderSupplierPurchaseOrdersSnapshot
            {
                sourceMarketSeed = 111UL,
                sourceCommercialSeed = 222UL,
                currentGameDay = 12,
                ordersRevision = 5
            },
            logistics = new BistroBuilderSupplierLogisticsSnapshot
            {
                logisticsSeed = 333UL,
                sourceMarketSeed = 111UL,
                sourceCommercialSeed = 222UL,
                currentGameDay = 12,
                logisticsRevision = 6
            },
            deliveryPresentation = new BistroBuilderSupplierDeliveryPresentationSnapshot
            {
                sourceLogisticsSeed = 333UL,
                currentGameDay = 12,
                presentationRevision = 7
            },
            progression = new BistroBuilderSupplierProgressionSnapshot
            {
                sourceMarketSeed = 111UL,
                sourceCommercialSeed = 222UL,
                currentGameDay = 12,
                progressionRevision = 8
            }
        };
    }

    private static long FindQuantity(List<BistroBuilderInventoryQuantityLine> lines, string ingredientId)
    {
        for (int i = 0; i < lines.Count; i++)
            if (string.Equals(lines[i].IngredientId, ingredientId, StringComparison.Ordinal))
                return lines[i].CanonicalMilliUnits;
        return -1L;
    }

    private void Check(bool condition, string message)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + message);
        }
        else
        {
            failed++;
            lines.Add("[FALLO] " + message);
        }
    }
}
