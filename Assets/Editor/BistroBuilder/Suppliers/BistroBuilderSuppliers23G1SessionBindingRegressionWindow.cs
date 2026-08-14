using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23G1SessionBindingRegressionWindow : EditorWindow
{
    private readonly List<string> lines = new List<string>();
    private int passed;
    private int failed;
    private int capturedErrors;
    private bool running;
    private int waitFrames;
    private BistroBuilderSupplierLogisticsService logistics;
    private BistroBuilderSupplierPurchaseOrderService orders;
    private BistroBuilderSupplierLogisticsSnapshot original;
    private BistroBuilderSupplierPurchaseOrdersSnapshot originalOrders;
    private string syntheticOrderId;
    private string syntheticPlanId;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3G1 - Prueba regresión binding persistente")]
    public static void Open()
    {
        GetWindow<BistroBuilderSuppliers23G1SessionBindingRegressionWindow>(
            "Regresión 2.3G1"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.3G1 — Persistencia de LogisticsPlan con sesión inicialmente unbound",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "Prueba no destructiva en Play Mode. Captura 2.3G, restaura una copia con " +
            "source seeds=0 y un LogisticsPlan sintético válido. Si 2.3E está unbound, " +
            "la prueba lo vincula de forma temporal mediante su flujo público de cotización; " +
            "espera varios frames y verifica que Update NO reinicializa 2.3G ni pierde " +
            "el plan. Al final restaura exactamente el snapshot original.",
            MessageType.Info
        );

        GUI.enabled = !running;
        if (GUILayout.Button("Ejecutar regresión 2.3G1"))
        {
            Run();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Correctos: " + passed + "  Fallos: " + failed +
            "  Errores/Exceptions/Asserts: " + capturedErrors,
            EditorStyles.boldLabel
        );
        for (int i = 0; i < lines.Count; i++)
        {
            EditorGUILayout.LabelField(lines[i], EditorStyles.wordWrappedLabel);
        }
    }

    private void Run()
    {
        ResetState();
        if (!EditorApplication.isPlaying)
        {
            Fail("Debe ejecutarse en Play Mode.");
            return;
        }

        logistics = BistroBuilderSupplierLogisticsService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSupplierLogisticsService>();
        orders = BistroBuilderSupplierPurchaseOrderService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSupplierPurchaseOrderService>();

        Check(logistics != null, "Existe autoridad runtime 2.3G.");
        Check(orders != null, "Existe autoridad runtime 2.3E.");
        if (logistics == null || orders == null) return;

        Check(logistics.IsInitialized, "2.3G está inicializado.");
        Check(orders.IsInitialized, "2.3E está inicializado.");
        if (!logistics.IsInitialized || !orders.IsInitialized) return;

        original = logistics.CreateSnapshot();
        Check(original != null, "Snapshot original 2.3G capturado.");
        if (original == null) return;

        originalOrders = orders.CreateSnapshot();
        Check(originalOrders != null, "Snapshot original 2.3E capturado.");
        if (originalOrders == null) return;

        // 2.3E puede estar legítimamente inicializado con seeds=0 si Awake ocurrió
        // antes de que 2.3C/2.3D estuvieran listos. No es un fallo de 2.3E: la sesión
        // se vincula en el primer flujo de cotización real. La regresión debe crear
        // esa precondición por la API pública, no exigirla de antemano.
        if (orders.SourceMarketSeed == 0UL || orders.SourceCommercialSeed == 0UL)
        {
            BistroBuilderPurchaseOrderRecord temporaryDraft;
            string bindError;
            if (!orders.TryCreateDraft(
                    "supplier_mercado_central",
                    out temporaryDraft,
                    out bindError) ||
                temporaryDraft == null)
            {
                FinishFailure(
                    "No se pudo crear Draft temporal para vincular 2.3E: " +
                    bindError);
                return;
            }

            BistroBuilderPurchaseOrderConfirmationPreview ignoredPreview;
            if (!orders.TryBuildConfirmationPreview(
                    temporaryDraft.purchaseOrderId,
                    out ignoredPreview,
                    out bindError))
            {
                FinishFailure(
                    "2.3E no pudo vincular su sesión mediante cotización pública: " +
                    bindError);
                return;
            }

            Check(
                orders.SourceMarketSeed != 0UL,
                "2.3E enlaza MarketSeed mediante su flujo público de cotización."
            );
            Check(
                orders.SourceCommercialSeed != 0UL,
                "2.3E enlaza CommercialSeed mediante su flujo público de cotización."
            );
        }
        else
        {
            Check(true, "2.3E ya tenía MarketSeed real vinculada.");
            Check(true, "2.3E ya tenía CommercialSeed real vinculada.");
        }

        ulong orderMarket = orders.SourceMarketSeed;
        ulong orderCommercial = orders.SourceCommercialSeed;
        if (orderMarket == 0UL || orderCommercial == 0UL)
        {
            FinishFailure(
                "2.3E sigue sin seeds reales después del binding controlado.");
            return;
        }

        BistroBuilderSupplierLogisticsSnapshot candidate = original.DeepClone();
        candidate.currentGameDay = Math.Max(1, orders.CurrentGameDay);
        candidate.sourceMarketSeed = 0UL;
        candidate.sourceCommercialSeed = 0UL;
        candidate.nextPlanSequence = Math.Max(1L, candidate.nextPlanSequence);
        if (candidate.logisticsSeed == 0UL) candidate.logisticsSeed = 1UL;
        if (candidate.plans == null)
            candidate.plans = new List<BistroBuilderSupplierLogisticsPlanRecord>();

        syntheticOrderId = "po_regression_23g1_" + DateTime.UtcNow.Ticks;
        syntheticPlanId = "logistics_plan_regression_23g1_" + DateTime.UtcNow.Ticks;
        int deliveryDay = Math.Max(candidate.currentGameDay + 1, 2);
        candidate.plans.Add(new BistroBuilderSupplierLogisticsPlanRecord
        {
            logisticsPlanId = syntheticPlanId,
            purchaseOrderId = syntheticOrderId,
            orderDisplayCode = "PO-REG-23G1",
            supplierId = "supplier_mercado_central",
            supplierDisplayName = "Mercado Central",
            status = BistroBuilderSupplierLogisticsPlanStatus.Planned,
            createdGameDay = candidate.currentGameDay,
            stateRevision = 1,
            sourceOrderStateRevision = 1,
            basePlannedDeliveryGameDay = deliveryDay,
            baseWindowStartMinuteOfDay = 480,
            baseWindowEndMinuteOfDay = 720,
            plannedDeliveryGameDay = deliveryDay,
            windowStartMinuteOfDay = 480,
            windowEndMinuteOfDay = 720,
            reliabilityTier = BistroBuilderSupplierReliabilityTier.Alta,
            reliabilityValue = 0.95f,
            delayProbabilityBasisPoints = 0,
            deterministicDelayRollBasisPoints = 0,
            decidedDelayGameMinutes = 0,
            delayApplied = false,
            delayAppliedGameDay = 0,
            logisticsLoadUnits = 1,
            visualLoadUnits = 1,
            suggestedTripCount = 1,
            resolvedVehicle = BistroBuilderSupplierVehiclePreference.Furgoneta,
            vehiclePresentationProfileId = "vehicle_van_default",
            driverPresentationProfileId = "driver_default",
            reasonCode = "regression_session_binding",
            reasonText = "Plan sintético temporal para probar que el binding no borra snapshots restaurados."
        });

        Application.logMessageReceived += OnLog;
        running = true;

        if (!logistics.TryRestoreSnapshot(candidate, out string restoreError))
        {
            FinishFailure("2.3G rechazó snapshot unbound controlado: " + restoreError);
            return;
        }
        Check(
            logistics.TryGetPlanByOrder(
                syntheticOrderId,
                out BistroBuilderSupplierLogisticsPlanRecord beforeBind
            ) && beforeBind != null,
            "El LogisticsPlan sintético existe inmediatamente tras Restore."
        );

        if (!logistics.TrySynchronizeSessionBinding(out string bindingError))
        {
            FinishFailure("No se pudo enlazar la sesión unbound: " + bindingError);
            return;
        }

        Check(
            logistics.SourceMarketSeed == orderMarket,
            "2.3G enlaza MarketSeed 0 -> seed real de 2.3E sin reiniciar."
        );
        Check(
            logistics.SourceCommercialSeed == orderCommercial,
            "2.3G enlaza CommercialSeed 0 -> seed real de 2.3E sin reiniciar."
        );
        Check(
            logistics.TryGetPlanByOrder(
                syntheticOrderId,
                out BistroBuilderSupplierLogisticsPlanRecord afterBind
            ) && afterBind != null,
            "El LogisticsPlan sobrevive al binding explícito."
        );

        waitFrames = 0;
        EditorApplication.update += WaitForRuntimeUpdates;
        Repaint();
    }

    private void WaitForRuntimeUpdates()
    {
        if (!running) return;
        if (!EditorApplication.isPlaying)
        {
            FinishFailure("Play Mode terminó durante la regresión.");
            return;
        }

        waitFrames++;
        EditorApplication.QueuePlayerLoopUpdate();
        if (waitFrames < 4) return;

        EditorApplication.update -= WaitForRuntimeUpdates;
        Check(
            logistics != null && logistics.IsInitialized,
            "2.3G sigue inicializado después de varios Update."
        );
        Check(
            logistics != null &&
            logistics.TryGetPlanByOrder(
                syntheticOrderId,
                out BistroBuilderSupplierLogisticsPlanRecord persisted
            ) && persisted != null && persisted.logisticsPlanId == syntheticPlanId,
            "Update no reinicializa 2.3G ni elimina el LogisticsPlan restaurado."
        );
        Check(
            logistics != null &&
            logistics.SourceMarketSeed == orders.SourceMarketSeed &&
            logistics.SourceCommercialSeed == orders.SourceCommercialSeed,
            "El binding permanece alineado con 2.3E."
        );

        RestoreOriginalAndFinish();
    }

    private void RestoreOriginalAndFinish()
    {
        // Restaura primero 2.3E. Si originalmente estaba unbound, 2.3G puede
        // recuperar después exactamente su snapshot original sin que C1.5 lo
        // normalice contra las seeds temporales usadas por esta regresión.
        if (orders != null && originalOrders != null)
        {
            if (orders.TryRestoreSnapshot(
                    originalOrders.DeepClone(),
                    out string ordersError))
            {
                Check(true, "Snapshot original 2.3E restaurado al finalizar.");
            }
            else
            {
                Fail("No se pudo restaurar snapshot original 2.3E: " + ordersError);
            }
        }

        if (logistics != null && original != null)
        {
            if (logistics.TryRestoreSnapshot(
                    original.DeepClone(),
                    out string logisticsError))
            {
                Check(true, "Snapshot original 2.3G restaurado al finalizar.");
            }
            else
            {
                Fail("No se pudo restaurar snapshot original 2.3G: " + logisticsError);
            }
        }

        StopCapture();
        running = false;
        Repaint();
    }

    private void FinishFailure(string message)
    {
        Fail(message);

        if (orders != null && originalOrders != null)
        {
            orders.TryRestoreSnapshot(originalOrders.DeepClone(), out _);
        }

        if (logistics != null && original != null)
        {
            logistics.TryRestoreSnapshot(original.DeepClone(), out _);
        }

        StopCapture();
        running = false;
        Repaint();
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (!running) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            capturedErrors++;
    }

    private void StopCapture()
    {
        EditorApplication.update -= WaitForRuntimeUpdates;
        Application.logMessageReceived -= OnLog;
    }

    private void ResetState()
    {
        StopCapture();
        lines.Clear();
        passed = 0;
        failed = 0;
        capturedErrors = 0;
        running = false;
        waitFrames = 0;
        logistics = null;
        orders = null;
        original = null;
        originalOrders = null;
        syntheticOrderId = string.Empty;
        syntheticPlanId = string.Empty;
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

    private void Fail(string message)
    {
        failed++;
        lines.Add("[FALLO] " + message);
    }

    private void OnDisable()
    {
        if (running)
        {
            FinishFailure("La ventana se cerró durante la regresión.");
        }
        else
        {
            StopCapture();
        }
    }
}
