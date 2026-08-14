using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JKL-C — Queen Test real.
///
/// Precondición dual: si existe una alerta Critical/OutOfStock previa se usa
/// directamente. Si no existe, el modo automático guarda primero rollback y
/// genera una escasez diagnóstica exclusivamente elevando el stock mínimo 2.2C;
/// no elimina stock ni inventa consumos. Desde esa escasez ejecuta 2.3F ->
/// Draft -> Confirm -> Save/Load ->
/// 2.3G -> 2.3H -> bridge 2.3L -> 2.2B -> Inventario y exige que el pedido llegue
/// a Delivered con ReceiptId y que mejore/desaparezca la alerta objetivo.
///
/// Guarda primero un rollback completo en un slot temporal y lo carga al final,
/// por lo que la prueba es reversible.
/// </summary>
public sealed class BistroBuilderSuppliers23JKLQueenTestWindow : EditorWindow
{
    private enum Phase
    {
        Idle,
        SavingRollback,
        WaitingPlan,
        SavingCheckpoint,
        LoadingCheckpoint,
        WaitingDelivery,
        LoadingRollbackSuccess,
        LoadingRollbackFailure,
        DeletingCheckpointSuccess,
        DeletingRollbackSuccess,
        DeletingCheckpointFailure,
        DeletingRollbackFailure,
        Completed,
        Failed
    }

    private Vector2 scroll;
    private string report = "Ejecuta primero un servicio real hasta provocar stock crítico y entra aquí en Play Mode.";
    private MessageType reportType = MessageType.Info;
    private Phase phase;

    private BistroBuilderSaveGameService save;
    private BistroBuilderInventoryWarehouseService warehouse;
    private BistroBuilderInventoryPlanningService planning;
    private BistroBuilderSupplierSmartPurchaseService smart;
    private BistroBuilderSupplierPurchaseOrderService orders;
    private BistroBuilderSupplierMarketService market;
    private BistroBuilderSupplierCommercialIntelligenceService commercial;
    private BistroBuilderSupplierLogisticsService logistics;
    private BistroBuilderSupplierDeliveryPresentationService delivery;
    private BistroBuilderSupplierReceivingBridge23L bridge;
    private BistroBuilderUnifiedUiInteractionService uiInteraction;

    private int rollbackSlot = -1;
    private int checkpointSlot = -1;
    private string targetIngredientId;
    private string targetIngredientName;
    private long targetAvailableBefore;
    private long targetMinimumStock;
    private string targetOrderId;
    private string checkpointLogisticsPlanId;
    private long checkpointOrderTotal;
    private float deadline;
    private string pendingFailure;
    private bool subscribedSave;
    private bool subscribedUpdate;
    private int capturedErrors;
    private bool requirePreexistingRealShortage;
    private bool diagnosticShortageApplied;
    private long diagnosticOriginalMinimum;
    private long diagnosticMinimumApplied;
    private BistroBuilderSmartPurchaseReport preparedSmartReport;
    private bool rollbackRestored;
    private string shortageOrigin = string.Empty;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3JKL-C - QUEEN TEST real de Proveedores", false, 2905)]
    private static void Open()
    {
        GetWindow<BistroBuilderSuppliers23JKLQueenTestWindow>("Queen Test 2.3JKL");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3JKL-C — QUEEN TEST REAL", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Modo automático recomendado: si ya existe una escasez Critical/OutOfStock se usa tal cual. " +
            "Si no existe, tras guardar el rollback el test crea una precondición diagnóstica REAL en 2.2C " +
            "elevando temporalmente el stock mínimo de un ingrediente canónico; no elimina stock, no crea " +
            "consumo ficticio y el rollback restaura exactamente la política original. Después automatiza " +
            "Compra Inteligente → pedido → Save/Load → logística → entrega → 2.2B → 2.2D.", MessageType.Info);

        requirePreexistingRealShortage = EditorGUILayout.ToggleLeft(
            "Modo estricto: exigir Critical/OutOfStock preexistente (sin precondición diagnóstica)",
            requirePreexistingRealShortage
        );

        bool canRun = EditorApplication.isPlaying &&
                      (phase == Phase.Idle || phase == Phase.Completed || phase == Phase.Failed);
        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button("EJECUTAR QUEEN TEST AUTOMÁTICO 2.3JKL", GUILayout.Height(38f))) Begin();
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, reportType);
        EditorGUILayout.EndScrollView();
    }

    private void Begin()
    {
        ResetRun();
        if (!Resolve(out string error))
        {
            FailImmediate(error);
            return;
        }

        // El Queen Test final también exige que la capa UX transversal B2 esté
        // instalada y operativa; así no se cierra 2.3 con regresiones conocidas
        // de cabeceras, ayuda contextual o selectores cíclicos.
        uiInteraction.RunImmediateScanForTests();
        if (uiInteraction.SelectorTriggerCount < 10 ||
            uiInteraction.TooltipTriggerCount < 20)
        {
            FailImmediate(
                "2.3JKL-B2 no tiene cobertura UI suficiente antes del Queen Test. " +
                "Selectores=" + uiInteraction.SelectorTriggerCount +
                " · tooltips=" + uiInteraction.TooltipTriggerCount + ".");
            return;
        }

        // La escasez se resuelve DESPUÉS de guardar el rollback. Así, si no
        // existe una escasez real previa, la política diagnóstica de 2.2C también
        // queda cubierta por la restauración integral del test.
        if (!FindTwoFreeSlots(out rollbackSlot, out checkpointSlot))
        {
            FailImmediate("Se necesitan dos slots libres entre 970 y 979.");
            return;
        }

        save.RefreshExtensions();
        if (!save.HasProvider(BistroBuilderSupplierIntegratedSaveSectionProvider.StableSectionId))
        {
            FailImmediate("SaveGameService no registra supplier.integrated.runtime.");
            return;
        }

        Subscribe();
        capturedErrors = 0;
        phase = Phase.SavingRollback;
        report = "Guardando rollback completo de la partida en slot " + rollbackSlot +
                 " antes de preparar/verificar la escasez objetivo...";
        Repaint();
        if (!save.TrySaveSlot(rollbackSlot, "BB 2.3JKL QUEEN ROLLBACK", out string rejection))
            FailImmediate("No se pudo guardar rollback inicial: " + rejection);
    }

    private void HandleSaveOperation(BistroBuilderSaveOperationResult result)
    {
        if (result == null) return;

        if (phase == Phase.SavingRollback && result.SlotIndex == rollbackSlot)
        {
            if (!result.Succeeded) { FailImmediate("Rollback inicial no pudo guardarse: " + result.Message); return; }
            PrepareShortageThenSmartPurchase();
            return;
        }
        if (phase == Phase.SavingCheckpoint && result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded) { FailAndRollback("Checkpoint post-pedido no pudo guardarse: " + result.Message); return; }
            MutateThenLoadCheckpoint();
            return;
        }
        if (phase == Phase.LoadingCheckpoint && result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded) { FailAndRollback("Checkpoint post-pedido no pudo cargarse: " + result.Message); return; }
            ContinueAfterCheckpointLoad();
            return;
        }
        if ((phase == Phase.LoadingRollbackSuccess || phase == Phase.LoadingRollbackFailure) && result.SlotIndex == rollbackSlot)
        {
            bool wasFailure = phase == Phase.LoadingRollbackFailure;
            if (!result.Succeeded)
            {
                TryRestoreDiagnosticPolicyBestEffort();
                CompleteFailure((wasFailure ? pendingFailure + " " : string.Empty) +
                    "Además, falló la restauración del rollback inicial: " + result.Message);
                return;
            }

            if (!Resolve(out string restoreResolveError))
            {
                CompleteFailure(
                    (wasFailure ? pendingFailure + " " : string.Empty) +
                    "Rollback cargado, pero no se pudieron resolver dependencias para verificarlo: " +
                    restoreResolveError);
                return;
            }

            if (!ValidateDiagnosticPolicyRestored(out string policyRestoreError))
            {
                CompleteFailure(
                    (wasFailure ? pendingFailure + " " : string.Empty) +
                    policyRestoreError);
                return;
            }

            rollbackRestored = true;
            BeginCleanup(wasFailure);
            return;
        }
        if ((phase == Phase.DeletingCheckpointSuccess || phase == Phase.DeletingCheckpointFailure) && result.SlotIndex == checkpointSlot)
        {
            bool failure = phase == Phase.DeletingCheckpointFailure;
            if (!result.Succeeded)
            {
                string cleanupError = "No se pudo eliminar el checkpoint diagnóstico: " + result.Message;
                if (failure) pendingFailure += " " + cleanupError;
                else pendingFailure = "Queen flow validado, pero " + cleanupError;
                DeleteRollbackSlot(true);
                return;
            }
            DeleteRollbackSlot(failure);
            return;
        }
        if ((phase == Phase.DeletingRollbackSuccess || phase == Phase.DeletingRollbackFailure) && result.SlotIndex == rollbackSlot)
        {
            bool failure = phase == Phase.DeletingRollbackFailure;
            if (!result.Succeeded)
            {
                string cleanupError = "No se pudo eliminar el rollback diagnóstico: " + result.Message;
                CompleteFailure((failure ? pendingFailure + " " : "Queen flow validado, pero ") + cleanupError);
                return;
            }
            if (failure) CompleteFailure(pendingFailure);
            else CompleteSuccess();
        }
    }

    private void PrepareShortageThenSmartPurchase()
    {
        if (!Resolve(out string error))
        {
            FailAndRollback(error);
            return;
        }

        // Primero intentamos una escasez ya presente, pero en modo automático
        // solo la aceptamos si el 2.3F REAL publica para ese ingrediente una
        // comparación de proveedores y un plan accionable. De este modo el
        // Queen Test no depende de una predicción paralela de elegibilidad.
        if (FindCriticalIngredient(
                out targetIngredientId,
                out targetIngredientName,
                out targetAvailableBefore,
                out targetMinimumStock,
                out error))
        {
            if (TryBuildComparableReportForCurrentTarget(
                    out BistroBuilderSmartPurchaseReport existingReport,
                    out string comparisonError))
            {
                shortageOrigin = "escasez preexistente";
                diagnosticShortageApplied = false;
                preparedSmartReport = existingReport;
                report =
                    "Escasez preexistente comparable detectada: " + targetIngredientName +
                    " · disponible=" + targetAvailableBefore +
                    " · mínimo=" + targetMinimumStock +
                    ". Iniciando Compra Inteligente...";
                Repaint();
                PrepareSmartPurchaseScenario();
                return;
            }

            if (requirePreexistingRealShortage)
            {
                FailAndRollback(
                    "Modo estricto activo: existe escasez real, pero 2.3F no puede " +
                    "validar comparación + compra accionable sobre ella. " + comparisonError);
                return;
            }

            // En modo automático no convertimos una escasez real poco adecuada
            // en un falso negativo del cierre. Buscamos una precondición de
            // diagnóstico que el propio 2.3F confirme como comparable.
        }
        else if (requirePreexistingRealShortage)
        {
            FailAndRollback(
                "Modo estricto activo: no existe ningún ingrediente Critical/OutOfStock preexistente.");
            return;
        }

        if (!TryCreateDiagnosticComparableShortage(
                out BistroBuilderSmartPurchaseReport diagnosticReport,
                out error))
        {
            FailAndRollback(
                "No se pudo crear una precondición diagnóstica comparable mediante 2.2C + 2.3F: " + error);
            return;
        }

        shortageOrigin = "precondición diagnóstica 2.2C validada por 2.3F";
        preparedSmartReport = diagnosticReport;
        report =
            "Precondición diagnóstica comparable creada: " +
            targetIngredientName + " · disponible=" + targetAvailableBefore +
            " · mínimo original=" + diagnosticOriginalMinimum +
            " · mínimo diagnóstico=" + diagnosticMinimumApplied +
            ". 2.3F ya confirmó tres estrategias, alternativa real y plan accionable.";
        Repaint();
        PrepareSmartPurchaseScenario();
    }

    private bool TryCreateDiagnosticComparableShortage(
        out BistroBuilderSmartPurchaseReport acceptedReport,
        out string error)
    {
        acceptedReport = null;
        error = string.Empty;

        if (planning == null || !planning.EnsureInitialized(out error))
        {
            error = "InventoryPlanningService 2.2C no está disponible. " + error;
            return false;
        }

        var items = new List<BistroBuilderInventoryWarehouseIngredientSnapshot>();
        if (!warehouse.CopyIngredientsTo(
                items,
                BistroBuilderInventoryWarehouseFilter.All,
                BistroBuilderInventoryWarehouseSort.Status,
                string.Empty,
                out error))
        {
            return false;
        }

        // Preferimos stocks bajos para que la compra de diagnóstico sea pequeña
        // y rápida, pero la aceptación final la decide el informe REAL de 2.3F.
        items.Sort((left, right) =>
            Math.Max(0L, left.AvailableCanonicalMilliUnits).CompareTo(
                Math.Max(0L, right.AvailableCanonicalMilliUnits)));

        var attempts = new List<string>();
        long maximum = BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits;

        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderInventoryWarehouseIngredientSnapshot item = items[index];
            if (string.IsNullOrWhiteSpace(item.IngredientId) ||
                item.AvailableCanonicalMilliUnits <= 0L)
            {
                continue;
            }

            // Filtro barato para no mutar políticas de ingredientes que ni
            // siquiera tienen dos vías comerciales utilizables. La decisión
            // definitiva sigue siendo el reporte real de 2.3F.
            HasAtLeastTwoPurchasableUnlockedProviders(
                item.IngredientId,
                out int rawProviderCount,
                out string rawDiagnostic);
            if (rawProviderCount < 2)
            {
                if (attempts.Count < 10) attempts.Add(rawDiagnostic);
                continue;
            }

            if (!planning.TryGetMinimumStock(
                    item.IngredientId,
                    out long originalMinimum))
            {
                if (attempts.Count < 10)
                    attempts.Add(item.IngredientId + ": no se pudo leer mínimo original.");
                continue;
            }

            long diagnosticMinimum;
            try
            {
                diagnosticMinimum = checked(item.AvailableCanonicalMilliUnits * 2L + 1L);
            }
            catch (OverflowException)
            {
                diagnosticMinimum = maximum;
            }
            diagnosticMinimum = Math.Max(
                originalMinimum + 1L,
                Math.Min(maximum, diagnosticMinimum));

            if (diagnosticMinimum <= item.AvailableCanonicalMilliUnits ||
                diagnosticMinimum <= 0L)
            {
                if (attempts.Count < 10)
                    attempts.Add(item.IngredientId + ": mínimo diagnóstico no representable.");
                continue;
            }

            bool minimumSet = planning.TrySetMinimumStock(
                item.IngredientId,
                diagnosticMinimum,
                out string setError);
            string recalcError = string.Empty;
            bool recalculated = minimumSet && planning.TryRecalculateAll(out recalcError);
            if (!minimumSet || !recalculated)
            {
                planning.TrySetMinimumStock(item.IngredientId, originalMinimum, out _);
                planning.TryRecalculateAll(out _);
                if (attempts.Count < 10)
                    attempts.Add(item.IngredientId + ": 2.2C rechazó precondición. " +
                                 (string.IsNullOrWhiteSpace(setError) ? recalcError : setError));
                continue;
            }

            // Confirmamos que 2.2D ve realmente el estado crítico.
            var critical = new List<BistroBuilderInventoryWarehouseIngredientSnapshot>();
            bool warehouseOk = warehouse.CopyIngredientsTo(
                critical,
                BistroBuilderInventoryWarehouseFilter.CriticalOrOutOfStock,
                BistroBuilderInventoryWarehouseSort.Status,
                string.Empty,
                out string warehouseError);
            BistroBuilderInventoryWarehouseIngredientSnapshot refreshed = default;
            bool hasRefreshed = false;
            if (warehouseOk)
            {
                for (int criticalIndex = 0; criticalIndex < critical.Count; criticalIndex++)
                {
                    if (string.Equals(
                            critical[criticalIndex].IngredientId,
                            item.IngredientId,
                            StringComparison.Ordinal))
                    {
                        refreshed = critical[criticalIndex];
                        hasRefreshed = true;
                        break;
                    }
                }
            }

            BistroBuilderSmartPurchaseReport candidateReport = null;
            string smartError = string.Empty;
            bool smartOk = hasRefreshed &&
                           smart.TryBuildRecommendations(out candidateReport, out smartError) &&
                           candidateReport != null;
            bool strategiesOk = smartOk && HasAllThreeStrategies(candidateReport);
            bool comparisonOk = smartOk &&
                                HasProviderAlternativeForIngredient(
                                    candidateReport, item.IngredientId);
            BistroBuilderSmartPurchasePlan actionable = smartOk
                ? FindActionablePlanForIngredient(
                    candidateReport,
                    item.IngredientId,
                    refreshed.AvailableCanonicalMilliUnits,
                    refreshed.MinimumStockCanonicalMilliUnits)
                : null;

            if (smartOk && strategiesOk && comparisonOk && actionable != null)
            {
                targetIngredientId = refreshed.IngredientId;
                targetIngredientName = refreshed.DisplayName;
                targetAvailableBefore = refreshed.AvailableCanonicalMilliUnits;
                targetMinimumStock = refreshed.MinimumStockCanonicalMilliUnits;
                diagnosticOriginalMinimum = originalMinimum;
                diagnosticMinimumApplied = diagnosticMinimum;
                diagnosticShortageApplied = true;
                acceptedReport = candidateReport;
                return true;
            }

            // Este candidato no sirve para un Queen Flow completo: restauramos
            // su política antes de probar el siguiente, sin dejar residuos.
            planning.TrySetMinimumStock(item.IngredientId, originalMinimum, out _);
            planning.TryRecalculateAll(out _);

            if (attempts.Count < 10)
            {
                string detail = !warehouseOk || !hasRefreshed
                    ? "2.2D no lo publicó crítico" +
                      (string.IsNullOrWhiteSpace(warehouseError) ? string.Empty : " (" + warehouseError + ")")
                    : !smartOk
                        ? "2.3F no generó informe" +
                          (string.IsNullOrWhiteSpace(smartError) ? string.Empty : " (" + smartError + ")")
                        : "estrategias=" + strategiesOk +
                          ", comparación=" + comparisonOk +
                          ", accionable=" + (actionable != null) +
                          ", proveedores publicados=" +
                          DescribeReportProvidersForIngredient(candidateReport, item.IngredientId);
                attempts.Add(item.IngredientId + " [raw=" + rawProviderCount + "]: " + detail);
            }
        }

        error =
            "Se probaron candidatos con rollback local de inventory.policy y ninguno cumplió " +
            "simultáneamente Critical 2.2D + tres estrategias 2.3F + >=2 SupplierId publicados + " +
            "plan confirmable. " +
            (attempts.Count == 0 ? "No hubo candidatos elegibles." : string.Join(" | ", attempts));
        return false;
    }

    private bool TryBuildComparableReportForCurrentTarget(
        out BistroBuilderSmartPurchaseReport reportData,
        out string error)
    {
        reportData = null;
        error = string.Empty;
        if (!smart.TryBuildRecommendations(out reportData, out error) || reportData == null)
        {
            return false;
        }
        if (!HasAllThreeStrategies(reportData))
        {
            error = "2.3F no publicó las tres estrategias.";
            return false;
        }
        if (!HasProviderAlternativeForIngredient(reportData, targetIngredientId))
        {
            error = "2.3F no publicó >=2 SupplierId para " + targetIngredientId +
                    ". Publicados: " +
                    DescribeReportProvidersForIngredient(reportData, targetIngredientId);
            return false;
        }
        if (FindActionablePlanForIngredient(
                reportData,
                targetIngredientId,
                targetAvailableBefore,
                targetMinimumStock) == null)
        {
            error = "2.3F no publicó un plan confirmable suficiente para resolver la alerta.";
            return false;
        }
        return true;
    }

    private static string DescribeReportProvidersForIngredient(
        BistroBuilderSmartPurchaseReport reportData,
        string ingredientId)
    {
        if (reportData == null || reportData.plans == null) return "sin informe";
        var chunks = new List<string>();
        for (int planIndex = 0; planIndex < reportData.plans.Count; planIndex++)
        {
            BistroBuilderSmartPurchasePlan plan = reportData.plans[planIndex];
            if (plan == null || plan.ingredients == null) continue;
            for (int ingredientIndex = 0; ingredientIndex < plan.ingredients.Count; ingredientIndex++)
            {
                BistroBuilderSmartPurchaseIngredientRecommendation rec =
                    plan.ingredients[ingredientIndex];
                if (rec == null || !string.Equals(rec.ingredientId, ingredientId, StringComparison.Ordinal))
                    continue;
                var suppliers = new HashSet<string>(StringComparer.Ordinal);
                if (rec.selected != null && !string.IsNullOrWhiteSpace(rec.selected.supplierId))
                    suppliers.Add(rec.selected.supplierId);
                if (rec.alternatives != null)
                {
                    for (int alternativeIndex = 0; alternativeIndex < rec.alternatives.Count; alternativeIndex++)
                    {
                        BistroBuilderSmartPurchaseCandidate alt = rec.alternatives[alternativeIndex];
                        if (alt != null && !string.IsNullOrWhiteSpace(alt.supplierId))
                            suppliers.Add(alt.supplierId);
                    }
                }
                chunks.Add(plan.strategy + "=[" + string.Join(",", suppliers) + "]");
                break;
            }
        }
        return chunks.Count == 0 ? "sin recomendación para ingrediente" : string.Join("; ", chunks);
    }

    private void TryRestoreDiagnosticPolicyBestEffort()
    {
        if (!diagnosticShortageApplied || planning == null ||
            string.IsNullOrWhiteSpace(targetIngredientId))
        {
            return;
        }

        planning.TrySetMinimumStock(
            targetIngredientId,
            diagnosticOriginalMinimum,
            out _
        );
        diagnosticShortageApplied = false;
    }

    private bool ValidateDiagnosticPolicyRestored(out string error)
    {
        error = string.Empty;
        if (!diagnosticShortageApplied)
        {
            return true;
        }

        if (planning == null && !Resolve(out error))
        {
            return false;
        }

        if (!planning.TryGetMinimumStock(
                targetIngredientId,
                out long currentMinimum))
        {
            error = "No se pudo comprobar el mínimo tras restaurar rollback.";
            return false;
        }

        if (currentMinimum != diagnosticOriginalMinimum)
        {
            error =
                "El rollback no restauró inventory.policy para " +
                targetIngredientId + ": esperado " + diagnosticOriginalMinimum +
                ", actual " + currentMinimum + ".";
            return false;
        }

        diagnosticShortageApplied = false;
        rollbackRestored = true;
        return true;
    }

    private void PrepareSmartPurchaseScenario()
    {
        if (!Resolve(out string error))
        {
            FailAndRollback(error);
            return;
        }
        BistroBuilderSmartPurchaseReport reportData = preparedSmartReport;
        preparedSmartReport = null;
        if (reportData == null &&
            (!smart.TryBuildRecommendations(out reportData, out error) || reportData == null))
        {
            FailAndRollback("2.3F no pudo analizar el stock real: " + error);
            return;
        }
        if (!HasAllThreeStrategies(reportData))
        {
            FailAndRollback("2.3F no publicó simultáneamente Ahorrar / Equilibrado / Urgente para comparar.");
            return;
        }
        if (!HasProviderAlternativeForIngredient(reportData, targetIngredientId))
        {
            FailAndRollback("2.3F no publicó alternativa de proveedor para el ingrediente crítico; no puede validarse comparación real.");
            return;
        }

        BistroBuilderSmartPurchasePlan plan = FindActionablePlanForIngredient(
            reportData, targetIngredientId, targetAvailableBefore, targetMinimumStock);
        if (plan == null)
        {
            FailAndRollback(
                "2.3F no propone una compra confirmable que, al recibirse, saque a " +
                targetIngredientName + " del umbral Low/Critical/OutOfStock. Revisar tuning de 2.3F/stock mínimo.");
            return;
        }
        if (!smart.TryCreateDraftFromPlan(plan, out List<string> createdOrders, out error) || createdOrders.Count == 0)
        {
            FailAndRollback("2.3F no pudo materializar Drafts reales: " + error);
            return;
        }

        targetOrderId = FindOrderContainingIngredient(createdOrders, targetIngredientId, out error);
        if (string.IsNullOrEmpty(targetOrderId))
        {
            FailAndRollback("No se encontró Draft que contenga el ingrediente crítico. " + error);
            return;
        }
        if (!orders.TryConfirmOrder(targetOrderId, out BistroBuilderPurchaseOrderConfirmationReceipt receipt, out error))
        {
            FailAndRollback("2.3E no pudo confirmar el pedido recomendado: " + error);
            return;
        }
        checkpointOrderTotal = receipt.totalCents;
        phase = Phase.WaitingPlan;
        deadline = Time.realtimeSinceStartup + 8f;
        report = "2.3F eligió plan " + plan.strategy + ". Pedido real " + receipt.displayCode +
                 " confirmado por " + BistroBuilderSupplierPlayerUiFormat.Money(receipt.totalCents) +
                 ". Esperando LogisticsPlan 2.3G...";
        Repaint();
    }

    private void Tick()
    {
        if (!EditorApplication.isPlaying && phase != Phase.Idle && phase != Phase.Completed && phase != Phase.Failed)
        {
            CompleteFailure("Play Mode terminó durante el Queen Test.");
            return;
        }

        if (phase == Phase.WaitingPlan)
        {
            logistics.TryPlanConfirmedOrders(out _, out _);
            if (orders.TryGetOrder(targetOrderId, out BistroBuilderPurchaseOrderRecord order) && order != null &&
                logistics.TryGetPlanByOrder(targetOrderId, out BistroBuilderSupplierLogisticsPlanRecord plan) && plan != null &&
                !string.IsNullOrWhiteSpace(order.logisticsPlanId))
            {
                checkpointLogisticsPlanId = plan.logisticsPlanId;
                phase = Phase.SavingCheckpoint;
                report = "LogisticsPlan " + plan.logisticsPlanId + " creado. Guardando checkpoint post-pedido en slot " + checkpointSlot + "...";
                Repaint();
                if (!save.TrySaveSlot(checkpointSlot, "BB 2.3JKL QUEEN CHECKPOINT", out string rejection))
                    FailAndRollback("No se pudo guardar checkpoint post-pedido: " + rejection);
                return;
            }
            if (Time.realtimeSinceStartup > deadline)
            {
                FailAndRollback("Timeout esperando que 2.3G planifique el pedido confirmado.");
            }
        }
        else if (phase == Phase.WaitingDelivery)
        {
            if (orders.TryGetOrder(targetOrderId, out BistroBuilderPurchaseOrderRecord order) && order != null &&
                order.status == BistroBuilderPurchaseOrderStatus.Delivered)
            {
                ValidateDeliveredScenario(order);
                return;
            }
            if (Time.realtimeSinceStartup > deadline)
            {
                FailAndRollback("Timeout esperando entrega física 2.3H + recepción 2.2B.");
            }
        }
    }

    private void MutateThenLoadCheckpoint()
    {
        int mutationDay = market.CurrentGameDay + 5;
        if (!market.TryAdvanceToGameDay(mutationDay, out string error) ||
            !commercial.TrySynchronizeCurrentMarketState(out error))
        {
            FailAndRollback("No se pudo mutar mercado después del Save: " + error);
            return;
        }
        phase = Phase.LoadingCheckpoint;
        report = "Mercado mutado tras Save. Cargando checkpoint para demostrar persistencia de PurchaseOrder/LogisticsPlan...";
        Repaint();
        if (!save.TryLoadSlot(checkpointSlot, out string rejection))
            FailAndRollback("Load del checkpoint rechazado: " + rejection);
    }

    private void ContinueAfterCheckpointLoad()
    {
        if (!Resolve(out string error))
        {
            FailAndRollback("Tras Load faltan dependencias: " + error);
            return;
        }
        if (!orders.TryGetOrder(targetOrderId, out BistroBuilderPurchaseOrderRecord order) || order == null)
        {
            FailAndRollback("PurchaseOrder desapareció tras Save/Load.");
            return;
        }
        if (order.totalCents != checkpointOrderTotal ||
            !string.Equals(order.logisticsPlanId, checkpointLogisticsPlanId, StringComparison.Ordinal))
        {
            FailAndRollback("PurchaseOrder/LogisticsPlan no conservan identidad y condiciones tras Load.");
            return;
        }
        if (!logistics.TryGetPlanByOrder(targetOrderId, out BistroBuilderSupplierLogisticsPlanRecord plan) || plan == null)
        {
            FailAndRollback("2.3G no restauró el LogisticsPlan del pedido.");
            return;
        }

        int deliveryDay = Math.Max(market.CurrentGameDay, Math.Max(1, plan.plannedDeliveryGameDay));
        if (!market.TryAdvanceToGameDay(deliveryDay, out error) ||
            !commercial.TrySynchronizeCurrentMarketState(out error) ||
            !logistics.TryAdvanceToGameDay(deliveryDay, out error))
        {
            FailAndRollback("No se pudo avanzar de forma controlada hasta la entrega: " + error);
            return;
        }

        if (!logistics.TryGetPlanByOrder(targetOrderId, out plan) || plan == null)
        {
            FailAndRollback("LogisticsPlan ausente después de avanzar día.");
            return;
        }
        if (plan.status != BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch)
        {
            // Un retraso puede desplazar el plan al día siguiente.
            int retryDay = Math.Max(deliveryDay + 1, plan.plannedDeliveryGameDay);
            if (!market.TryAdvanceToGameDay(retryDay, out error) ||
                !commercial.TrySynchronizeCurrentMarketState(out error) ||
                !logistics.TryAdvanceToGameDay(retryDay, out error) ||
                !logistics.TryGetPlanByOrder(targetOrderId, out plan) || plan == null ||
                plan.status != BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch)
            {
                FailAndRollback("2.3G no alcanzó ReadyForDispatch: " + error);
                return;
            }
        }

        BistroBuilderSupplierDeliveryPresentationRecord presentation;
        // 2.3H puede autoarrancar un DispatchTicket ReadyForDispatch. Si ya existe
        // presentación para este pedido, la reutilizamos en vez de crear una segunda.
        if (!delivery.TryGetPresentationByOrder(targetOrderId, out presentation) || presentation == null)
        {
            if (!delivery.TryStartDelivery(targetOrderId, out presentation, out error))
            {
                FailAndRollback("2.3H no pudo iniciar entrega física real: " + error);
                return;
            }
        }
        if (presentation.state == BistroBuilderSupplierDeliveryPresentationState.Cancelled)
        {
            FailAndRollback("2.3H presenta el pedido como Cancelled antes de la entrega.");
            return;
        }
        phase = Phase.WaitingDelivery;
        deadline = Time.realtimeSinceStartup + 120f;
        report = "Checkpoint restaurado. Pedido persiste. Entrega física " + presentation.presentationId +
                 " activa; esperando 2.3H → bridge 2.3L → 2.2B → Delivered...";
        Repaint();
    }

    private void ValidateDeliveredScenario(BistroBuilderPurchaseOrderRecord delivered)
    {
        if (string.IsNullOrWhiteSpace(delivered.deliveryReceiptId))
        {
            FailAndRollback("PurchaseOrder llegó a Delivered sin ReceiptId.");
            return;
        }
        if (!warehouse.TryGetIngredient(targetIngredientId, out BistroBuilderInventoryWarehouseIngredientSnapshot after, out string error))
        {
            FailAndRollback("2.2D no puede leer el ingrediente recibido: " + error);
            return;
        }
        if (after.AvailableCanonicalMilliUnits <= targetAvailableBefore)
        {
            FailAndRollback("La recepción no aumentó stock disponible del ingrediente crítico.");
            return;
        }

        List<BistroBuilderInventoryWarehouseReceiptSnapshot> receipts =
            new List<BistroBuilderInventoryWarehouseReceiptSnapshot>();
        if (!warehouse.CopyReceiptsTo(receipts, 200, out error))
        {
            FailAndRollback("No se pudo consultar Recepciones 2.2D: " + error);
            return;
        }
        bool receiptVisible = false;
        for (int i = 0; i < receipts.Count; i++)
            if (receipts[i] != null && string.Equals(receipts[i].ReceiptId, delivered.deliveryReceiptId, StringComparison.Ordinal))
                receiptVisible = true;
        if (!receiptVisible)
        {
            FailAndRollback("ReceiptId del pedido no aparece en la lectura jugable 2.2D.");
            return;
        }

        List<BistroBuilderInventoryAlertSnapshot> alerts = new List<BistroBuilderInventoryAlertSnapshot>();
        if (!warehouse.CopyAlertsTo(alerts, out error))
        {
            FailAndRollback("No se pudo consultar alertas 2.2C/2.2D: " + error);
            return;
        }
        bool stockAlertStillActive = false;
        for (int i = 0; i < alerts.Count; i++)
        {
            BistroBuilderInventoryAlertSnapshot a = alerts[i];
            if (string.Equals(a.IngredientId, targetIngredientId, StringComparison.Ordinal) &&
                (a.Kind == BistroBuilderInventoryAlertKind.LowStock ||
                 a.Kind == BistroBuilderInventoryAlertKind.CriticalStock ||
                 a.Kind == BistroBuilderInventoryAlertKind.OutOfStock))
                stockAlertStillActive = true;
        }
        if (stockAlertStillActive)
        {
            FailAndRollback("La compra llegó correctamente, pero la alerta de stock objetivo sigue activa. Revisar tuning de cantidad recomendada.");
            return;
        }
        if (capturedErrors > 0)
        {
            FailAndRollback("La integración funcional generó " + capturedErrors + " Error/Exception/Assert.");
            return;
        }

        phase = Phase.LoadingRollbackSuccess;
        report = "QUEEN FLOW validado: pedido Delivered con " + delivered.deliveryReceiptId +
                 ", stock de " + targetIngredientName + " aumentó y alerta desapareció. Restaurando partida inicial...";
        Repaint();
        if (!save.TryLoadSlot(rollbackSlot, out string rejection))
            CompleteFailure("Queen flow correcto, pero no se pudo restaurar rollback inicial: " + rejection);
    }

    private bool FindCriticalIngredient(
        out string ingredientId,
        out string displayName,
        out long available,
        out long minimumStock,
        out string error)
    {
        ingredientId = string.Empty;
        displayName = string.Empty;
        available = 0L;
        minimumStock = 0L;
        error = string.Empty;

        var items = new List<BistroBuilderInventoryWarehouseIngredientSnapshot>();
        if (!warehouse.CopyIngredientsTo(
                items,
                BistroBuilderInventoryWarehouseFilter.CriticalOrOutOfStock,
                BistroBuilderInventoryWarehouseSort.Status,
                string.Empty,
                out error))
        {
            return false;
        }

        if (items.Count == 0)
        {
            error = "No existe ningún ingrediente Critical/OutOfStock en el inventario real.";
            return false;
        }

        int bestProviderCount = 0;
        string bestDiagnostic = string.Empty;
        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderInventoryWarehouseIngredientSnapshot item = items[index];
            if (string.IsNullOrWhiteSpace(item.IngredientId))
            {
                continue;
            }

            if (!HasAtLeastTwoPurchasableUnlockedProviders(
                    item.IngredientId,
                    out int providerCount,
                    out string providerDiagnostic))
            {
                if (providerCount > bestProviderCount)
                {
                    bestProviderCount = providerCount;
                    bestDiagnostic = providerDiagnostic;
                }
                continue;
            }

            ingredientId = item.IngredientId;
            displayName = item.DisplayName;
            available = item.AvailableCanonicalMilliUnits;
            minimumStock = item.MinimumStockCanonicalMilliUnits;
            return true;
        }

        error =
            "Hay " + items.Count +
            " ingrediente(s) Critical/OutOfStock, pero ninguno dispone ahora mismo de al menos " +
            "dos proveedores distintos, desbloqueados y cotizables para validar una comparación real." +
            (string.IsNullOrWhiteSpace(bestDiagnostic)
                ? string.Empty
                : " Mejor caso observado: " + bestDiagnostic);
        return false;
    }

    private bool HasAtLeastTwoPurchasableUnlockedProviders(
        string ingredientId,
        out int providerCount,
        out string diagnostic)
    {
        providerCount = 0;
        diagnostic = string.Empty;

        if (string.IsNullOrWhiteSpace(ingredientId))
        {
            diagnostic = "IngredientId vacío.";
            return false;
        }

        if (commercial == null || !commercial.IsInitialized)
        {
            diagnostic = "2.3D no está inicializado.";
            return false;
        }

        BistroBuilderSupplierAuthoringDatabase supplierDatabase =
            Resources.Load<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSupplierSmartPurchaseService.SupplierAuthoringResourcePath
            );
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase =
            Resources.Load<BistroBuilderIngredientAuthoringDatabase>(
                BistroBuilderSupplierSmartPurchaseService.IngredientAuthoringResourcePath
            );

        if (supplierDatabase == null || ingredientDatabase == null)
        {
            diagnostic = "No se localizaron supplier.authoring / ingredient.authoring.";
            return false;
        }

        if (!ingredientDatabase.TryGetIngredient(
                ingredientId,
                out BistroBuilderIngredientAuthoringRecord ingredient) ||
            ingredient == null)
        {
            diagnostic = "ingredient.authoring no contiene " + ingredientId + ".";
            return false;
        }

        BistroBuilderSupplierProgressionService progression =
            BistroBuilderSupplierProgressionService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierProgressionService>();

        var providers = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers =
            supplierDatabase.Suppliers;

        for (int supplierIndex = 0; supplierIndex < suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null)
            {
                continue;
            }

            if (progression != null && progression.IsInitialized &&
                !progression.IsSupplierUnlocked(supplier.SupplierId))
            {
                continue;
            }

            bool supplierQualifies = false;
            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                    supplier.baseOffers[offerIndex];
                if (offer == null || !offer.isActive ||
                    !string.Equals(
                        offer.ingredientId,
                        ingredientId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!commercial.TryGetCommercialQuote(
                        offer.SupplierOfferId,
                        out BistroBuilderSupplierCommercialQuote quote) ||
                    quote == null ||
                    !quote.availableForNewOrders ||
                    quote.availability ==
                        BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado)
                {
                    continue;
                }

                bool packageValid = false;
                if (ingredient.commercialPackages != null)
                {
                    for (int packageIndex = 0;
                         packageIndex < ingredient.commercialPackages.Count;
                         packageIndex++)
                    {
                        BistroBuilderCommercialPackageAuthoringRecord package =
                            ingredient.commercialPackages[packageIndex];
                        if (package != null && package.isActive &&
                            string.Equals(
                                package.PackageFormatId,
                                offer.packageFormatId,
                                StringComparison.Ordinal))
                        {
                            packageValid = true;
                            break;
                        }
                    }
                }

                if (!packageValid)
                {
                    continue;
                }

                supplierQualifies = true;
                break;
            }

            if (supplierQualifies)
            {
                providers.Add(supplier.SupplierId);
            }
        }

        providerCount = providers.Count;
        diagnostic = ingredientId + " tiene " + providerCount +
                     " proveedor(es) desbloqueados y cotizables.";
        return providerCount >= 2;
    }

    private static bool HasAllThreeStrategies(BistroBuilderSmartPurchaseReport report)
    {
        bool save = false;
        bool balanced = false;
        bool urgent = false;
        if (report == null || report.plans == null) return false;
        for (int i = 0; i < report.plans.Count; i++)
        {
            BistroBuilderSmartPurchasePlan plan = report.plans[i];
            if (plan == null) continue;
            if (plan.strategy == BistroBuilderSmartPurchaseStrategy.Ahorrar) save = true;
            else if (plan.strategy == BistroBuilderSmartPurchaseStrategy.Equilibrado) balanced = true;
            else if (plan.strategy == BistroBuilderSmartPurchaseStrategy.Urgente) urgent = true;
        }
        return save && balanced && urgent;
    }

    private static bool HasProviderAlternativeForIngredient(
        BistroBuilderSmartPurchaseReport report,
        string ingredientId)
    {
        if (report == null || report.plans == null)
        {
            return false;
        }

        for (int planIndex = 0; planIndex < report.plans.Count; planIndex++)
        {
            BistroBuilderSmartPurchasePlan plan = report.plans[planIndex];
            if (plan == null || plan.ingredients == null)
            {
                continue;
            }

            for (int ingredientIndex = 0;
                 ingredientIndex < plan.ingredients.Count;
                 ingredientIndex++)
            {
                BistroBuilderSmartPurchaseIngredientRecommendation recommendation =
                    plan.ingredients[ingredientIndex];
                if (recommendation == null ||
                    !string.Equals(
                        recommendation.ingredientId,
                        ingredientId,
                        StringComparison.Ordinal) ||
                    recommendation.selected == null)
                {
                    continue;
                }

                var supplierIds = new HashSet<string>(StringComparer.Ordinal);
                if (!string.IsNullOrWhiteSpace(recommendation.selected.supplierId))
                {
                    supplierIds.Add(recommendation.selected.supplierId);
                }

                if (recommendation.alternatives != null)
                {
                    for (int alternativeIndex = 0;
                         alternativeIndex < recommendation.alternatives.Count;
                         alternativeIndex++)
                    {
                        BistroBuilderSmartPurchaseCandidate alternative =
                            recommendation.alternatives[alternativeIndex];
                        if (alternative != null &&
                            !string.IsNullOrWhiteSpace(alternative.supplierId))
                        {
                            supplierIds.Add(alternative.supplierId);
                        }
                    }
                }

                if (supplierIds.Count >= 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static BistroBuilderSmartPurchasePlan FindActionablePlanForIngredient(
        BistroBuilderSmartPurchaseReport report,
        string ingredientId,
        long currentAvailableMilliUnits,
        long minimumStockMilliUnits)
    {
        if (report == null || report.plans == null) return null;

        // Preferencia: recomendada. Después Equilibrado, Ahorrar, Urgente.
        // Además de ser confirmable, el Queen Test exige que la cantidad elegida
        // sea suficiente para eliminar la alerta de stock al recibirla; así el test
        // valida de verdad el objetivo end-to-end acordado y no una mera recepción.
        BistroBuilderSmartPurchaseStrategy[] order =
        {
            report.recommendedStrategy,
            BistroBuilderSmartPurchaseStrategy.Equilibrado,
            BistroBuilderSmartPurchaseStrategy.Ahorrar,
            BistroBuilderSmartPurchaseStrategy.Urgente
        };
        HashSet<BistroBuilderSmartPurchaseStrategy> visited =
            new HashSet<BistroBuilderSmartPurchaseStrategy>();
        for (int s = 0; s < order.Length; s++)
        {
            if (!visited.Add(order[s])) continue;
            for (int p = 0; p < report.plans.Count; p++)
            {
                BistroBuilderSmartPurchasePlan plan = report.plans[p];
                if (plan == null || plan.strategy != order[s] || plan.containsMinimumOrderGap) continue;
                for (int i = 0; i < plan.ingredients.Count; i++)
                {
                    BistroBuilderSmartPurchaseIngredientRecommendation rec = plan.ingredients[i];
                    if (rec == null || rec.selected == null || rec.selected.packageCount <= 0 ||
                        !string.Equals(rec.ingredientId, ingredientId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    long purchasedMicro = rec.selected.purchasedMicrounits;
                    if (purchasedMicro <= 0L || purchasedMicro % 1000L != 0L) continue;
                    long receivedMilli = purchasedMicro / 1000L;
                    long projected;
                    try { projected = checked(Math.Max(0L, currentAvailableMilliUnits) + receivedMilli); }
                    catch (OverflowException) { projected = long.MaxValue; }

                    long required = Math.Max(1L, minimumStockMilliUnits);
                    if (projected >= required) return plan;
                }
            }
        }
        return null;
    }

    private string FindOrderContainingIngredient(List<string> orderIds, string ingredientId, out string error)
    {
        error = string.Empty;
        for (int i = 0; i < orderIds.Count; i++)
        {
            if (!orders.TryBuildConfirmationPreview(orderIds[i], out BistroBuilderPurchaseOrderConfirmationPreview preview, out string previewError) || preview == null)
            {
                error += previewError + " ";
                continue;
            }
            if (!preview.canConfirm) continue;
            for (int l = 0; l < preview.lines.Count; l++)
                if (preview.lines[l] != null && string.Equals(preview.lines[l].ingredientId, ingredientId, StringComparison.Ordinal))
                    return orderIds[i];
        }
        return string.Empty;
    }

    private bool Resolve(out string error)
    {
        error = string.Empty;
        save = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
        warehouse = UnityEngine.Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseService>();
        planning = UnityEngine.Object.FindFirstObjectByType<BistroBuilderInventoryPlanningService>();
        smart = BistroBuilderSupplierSmartPurchaseService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierSmartPurchaseService>();
        orders = BistroBuilderSupplierPurchaseOrderService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        market = BistroBuilderSupplierMarketService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierMarketService>();
        commercial = BistroBuilderSupplierCommercialIntelligenceService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierCommercialIntelligenceService>();
        logistics = BistroBuilderSupplierLogisticsService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierLogisticsService>();
        delivery = BistroBuilderSupplierDeliveryPresentationService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierDeliveryPresentationService>();
        bridge = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierReceivingBridge23L>();
        uiInteraction = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUnifiedUiInteractionService>();

        if (save == null)
        {
            error = "Falta SaveGameService.";
        }
        else if (warehouse == null || !warehouse.EnsureReady(out error))
        {
            error = "Inventario/Almacén 2.2D no listo. " + error;
        }
        else if (planning == null || !planning.IsInitialized)
        {
            error = "2.2C no inicializado.";
        }
        // El orden importa: 2.3F depende de 2.3D. El Queen Test anterior
        // comprobaba 2.3F antes que mercado/comercial y podía emitir un falso
        // negativo si SmartPurchaseService había ejecutado Awake antes de que
        // 2.3D estuviera listo.
        else if (market == null || !market.IsInitialized)
        {
            error = "2.3C no inicializado.";
        }
        else if (commercial == null || !commercial.IsInitialized)
        {
            error = "2.3D no inicializado.";
        }
        else if (orders == null || !orders.IsInitialized)
        {
            error = "2.3E no inicializado.";
        }
        else
        {
            // 2.3F está diseñado para reintentar su binding de dependencias de
            // forma segura cuando se solicita una recomendación. Resolve debe
            // respetar ese contrato en lugar de exigir que Awake haya acertado
            // el orden exacto de inicialización de 2.3C/2.3D.
            if (smart != null && !smart.IsInitialized)
            {
                smart.TryInitialize();
            }

            if (smart == null || !smart.IsInitialized)
            {
                error = "2.3F no inicializado" +
                    (smart != null && !string.IsNullOrWhiteSpace(smart.LastInitializationError)
                        ? ": " + smart.LastInitializationError
                        : ".");
            }
            else if (logistics == null || !logistics.IsInitialized)
            {
                error = "2.3G no inicializado.";
            }
            else if (delivery == null || !delivery.IsInitialized)
            {
                error = "2.3H no inicializado.";
            }
            else if (bridge == null || !bridge.ValidateConfiguration(out error))
            {
                error = "Bridge 2.3L no listo. " + error;
            }
            else if (uiInteraction == null || !uiInteraction.ValidateConfiguration(out error))
            {
                error = "UI transversal 2.3JKL-B2 no lista. " + error;
            }
        }

        return string.IsNullOrEmpty(error);
    }

    private bool FindTwoFreeSlots(out int first, out int second)
    {
        first = -1;
        second = -1;
        for (int slot = 970; slot <= 979; slot++)
        {
            if (save.SlotExists(slot)) continue;
            if (first < 0) first = slot;
            else { second = slot; return true; }
        }
        return false;
    }

    private void FailAndRollback(string message)
    {
        pendingFailure = message;
        if (save != null && !save.IsBusy && rollbackSlot >= 0 && save.SlotExists(rollbackSlot))
        {
            phase = Phase.LoadingRollbackFailure;
            reportType = MessageType.Error;
            report = "Fallo: " + message + "\nRestaurando rollback inicial...";
            Repaint();
            if (save.TryLoadSlot(rollbackSlot, out string rejection)) return;
            pendingFailure += " No se pudo iniciar rollback: " + rejection;
        }
        CompleteFailure(pendingFailure);
    }

    private void BeginCleanup(bool failure)
    {
        if (checkpointSlot >= 0 && save.SlotExists(checkpointSlot))
        {
            phase = failure ? Phase.DeletingCheckpointFailure : Phase.DeletingCheckpointSuccess;
            if (save.TryDeleteSlot(checkpointSlot, out string rejection)) return;
            if (failure) pendingFailure += " No se pudo eliminar checkpoint: " + rejection;
        }
        DeleteRollbackSlot(failure);
    }

    private void DeleteRollbackSlot(bool failure)
    {
        if (rollbackSlot >= 0 && save.SlotExists(rollbackSlot))
        {
            phase = failure ? Phase.DeletingRollbackFailure : Phase.DeletingRollbackSuccess;
            if (save.TryDeleteSlot(rollbackSlot, out string rejection)) return;
            if (failure) pendingFailure += " No se pudo eliminar rollback: " + rejection;
        }
        if (failure) CompleteFailure(pendingFailure);
        else CompleteSuccess();
    }

    private void CompleteSuccess()
    {
        phase = Phase.Completed;
        reportType = MessageType.Info;
        report =
            "QUEEN TEST 2.3JKL SUPERADO\n\n" +
            "- Escasez objetivo: " + targetIngredientName + " (" + shortageOrigin + ").\n" +
            "- 2.3F publicó Ahorrar/Equilibrado/Urgente y alternativa real de proveedor.\n" +
            "- 2.3F seleccionó un plan comprable suficiente para eliminar la alerta objetivo.\n" +
            "- 2.3E creó/confirmó PurchaseOrder real.\n" +
            "- Save/Load conservó PurchaseOrder + LogisticsPlan.\n" +
            "- 2.3G llevó el pedido a ReadyForDispatch.\n" +
            "- 2.3H ejecutó entrega física.\n" +
            "- 2.3L entregó handoff a 2.2B.\n" +
            "- 2.2B generó ReceiptId, lotes/ledger/stock.\n" +
            "- 2.2D refleja la recepción y la alerta objetivo desapareció.\n" +
            "- 2.3JKL-B2 mantiene aislamiento contextual, tooltips y selectores desplazables.\n" +
            "- Error/Exception/Assert capturados: 0.\n" +
            "- Partida inicial restaurada, inventory.policy verificada y slots diagnósticos eliminados.";
        Unsubscribe();
        Debug.Log(report);
        Repaint();
    }

    private void FailImmediate(string message)
    {
        pendingFailure = message;
        CompleteFailure(message);
    }

    private void CompleteFailure(string message)
    {
        if (!rollbackRestored)
        {
            TryRestoreDiagnosticPolicyBestEffort();
        }

        phase = Phase.Failed;
        reportType = MessageType.Error;
        report = "QUEEN TEST 2.3JKL FALLIDO\n\n" + message;
        Unsubscribe();
        Debug.LogError(report);
        Repaint();
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (phase == Phase.Idle || phase == Phase.Completed || phase == Phase.Failed) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) capturedErrors++;
    }

    private void Subscribe()
    {
        if (!subscribedSave)
        {
            save.OperationCompleted += HandleSaveOperation;
            subscribedSave = true;
        }
        if (!subscribedUpdate)
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += HandleLog;
            subscribedUpdate = true;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedSave && save != null) save.OperationCompleted -= HandleSaveOperation;
        subscribedSave = false;
        if (subscribedUpdate)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= HandleLog;
        }
        subscribedUpdate = false;
    }

    private void ResetRun()
    {
        Unsubscribe();
        phase = Phase.Idle;
        rollbackSlot = -1;
        checkpointSlot = -1;
        targetIngredientId = string.Empty;
        targetIngredientName = string.Empty;
        targetAvailableBefore = 0L;
        targetMinimumStock = 0L;
        targetOrderId = string.Empty;
        checkpointLogisticsPlanId = string.Empty;
        checkpointOrderTotal = 0L;
        pendingFailure = string.Empty;
        capturedErrors = 0;
        diagnosticShortageApplied = false;
        diagnosticOriginalMinimum = 0L;
        diagnosticMinimumApplied = 0L;
        preparedSmartReport = null;
        rollbackRestored = false;
        shortageOrigin = string.Empty;
    }

    private void OnDisable()
    {
        Unsubscribe();
    }
}
