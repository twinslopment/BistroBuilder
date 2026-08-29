using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gate funcional de OperationalPressure sobre 367D real.
/// Crea una comanda temporal, mide tiempo base y presión activa y restaura todo.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderMarketingOperationalPressurePlayModeSelfTest
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey =
        "BB.Marketing.OperationalPressure.Play.Stage";
    private const string SuccessKey =
        "BB.Marketing.OperationalPressure.Play.Success";
    private const string ReportPath =
        "OperationalPressurePlayModeReport.txt";

    private static BistroBuilderMarketingSnapshot originalMarketing;
    private static BistroBuilderFinanceSnapshot originalFinance;
    private static BistroBuilderCanonicalOrderRuntimeSnapshot originalOrders;
    private static RestaurantOrder temporaryLegacyOrder;
    private static GameObject temporaryTableObject;
    private static GameObject temporaryGroupObject;
    private static GameObject temporaryWaiterObject;

    static BistroBuilderMarketingOperationalPressurePlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem(
        "Tools/Bistro Builder/Marketing/OperationalPressure - PlayMode funcional",
        false,
        7243)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "OperationalPressure PlayMode ya está ejecutándose.");

        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(
            StageKey,
            commandLine ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stage)) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(
                StageKey,
                cli ? "run_cli" : "run_menu");
        }

        if (state == PlayModeStateChange.EnteredEditMode)
            FinishEditor(stage.Contains("cli", StringComparison.Ordinal));
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 4) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;

        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        SessionState.SetString(
            StageKey,
            cli ? "executing_cli" : "executing_menu");
        RunScenario(cli);
    }

    private static void RunScenario(bool commandLine)
    {
        BistroBuilderMarketingService marketing = Find<BistroBuilderMarketingService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();
        BistroBuilderCanonicalOrderService canonical =
            Find<BistroBuilderCanonicalOrderService>();
        BistroBuilderCanonicalOrderIntegrationService integration =
            Find<BistroBuilderCanonicalOrderIntegrationService>();
        BistroBuilderOrderLineExecutionService execution =
            Find<BistroBuilderOrderLineExecutionService>();
        BistroBuilderMarketingPreparationDurationAdjustmentProvider provider =
            Find<BistroBuilderMarketingPreparationDurationAdjustmentProvider>();
        KitchenSystem kitchen = Find<KitchenSystem>();

        if (marketing == null || finance == null || canonical == null ||
            integration == null || execution == null || provider == null ||
            kitchen == null)
        {
            Finish(false,
                "Faltan autoridades runtime para OperationalPressure.",
                commandLine);
            return;
        }

        string providerError = string.Empty;
        string executionError = string.Empty;
        string kitchenError = string.Empty;
        bool providerValid = provider.ValidateConfiguration(out providerError);
        bool executionValid = execution.ValidateConfiguration(out executionError);
        bool kitchenValid = kitchen.ValidateConfiguration(out kitchenError);
        if (!providerValid || !executionValid || !kitchenValid)
        {
            Finish(false,
                "Configuración inválida. Proveedor=" + providerError +
                " | 367D=" + executionError + " | Cocina=" + kitchenError,
                commandLine);
            return;
        }

        originalMarketing = marketing.CreateSnapshot();
        originalFinance = finance.CreateSnapshot();
        if (!canonical.TryCaptureRuntimeSnapshot(
                out originalOrders,
                out string snapshotError))
        {
            Finish(false,
                "No pudo capturarse el runtime de comandas: " + snapshotError,
                commandLine);
            return;
        }

        if (originalMarketing == null || originalFinance == null ||
            originalOrders == null)
        {
            Finish(false,
                "No pudieron capturarse snapshots para rollback.",
                commandLine);
            return;
        }

        if (!marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(),
                out string clearError))
        {
            Finish(false,
                "No pudo limpiarse Marketing para la línea base: " + clearError,
                commandLine);
            return;
        }

        if (!TryCreateTemporaryLinkedOrder(
                canonical,
                integration,
                out BistroBuilderCanonicalOrder orderSnapshot,
                out BistroBuilderCanonicalOrderLine line,
                out string fixtureError))
        {
            Finish(false, fixtureError, commandLine);
            return;
        }

        string baseError = string.Empty;
        string baselineError = string.Empty;
        bool baseResolved = execution.TryResolveDishPreparationDurationSeconds(
            line.DishId,
            0.01f,
            0.1f,
            30f,
            out float historicalBase,
            out baseError);
        bool baselineResolved = execution.TryResolvePreparationDurationSeconds(
            temporaryLegacyOrder,
            line.LineId,
            0.01f,
            0.1f,
            30f,
            out float baselineDuration,
            out baselineError);
        if (!baseResolved || !baselineResolved)
        {
            Finish(false,
                "No pudo resolverse la línea base. Base=" + baseError +
                " | 367D=" + baselineError,
                commandLine);
            return;
        }

        if (Mathf.Abs(historicalBase - baselineDuration) > 0.0001f ||
            execution.LastPreparationDurationAdjustmentBasisPoints != 0)
        {
            Finish(false,
                "Sin campaña, 367D alteró la duración histórica.",
                commandLine);
            return;
        }

        if (!marketing.TryStartCampaign(
                "marketing.local.flyers",
                out BistroBuilderMarketingCampaignRecord started,
                out string campaignError))
        {
            Finish(false,
                "No pudo iniciarse la campaña real: " + campaignError,
                commandLine);
            return;
        }

        if (started == null ||
            !string.Equals(
                started.campaignId,
                "marketing.local.flyers",
                StringComparison.Ordinal))
        {
            Finish(false,
                "Marketing no devolvió la campaña real contratada.",
                commandLine);
            return;
        }

        if (!execution.TryResolvePreparationDurationSeconds(
                temporaryLegacyOrder,
                line.LineId,
                0.01f,
                0.1f,
                30f,
                out float pressuredDuration,
                out string pressureError))
        {
            Finish(false,
                "367D no pudo resolver la duración promocionada: " +
                pressureError,
                commandLine);
            return;
        }

        if (!BistroBuilderPreparationDurationAdjustmentPolicy.TryApply(
                historicalBase,
                0.1f,
                30f,
                200,
                out float expectedDuration,
                out string expectedError))
        {
            Finish(false,
                "No pudo calcularse la expectativa de presión: " + expectedError,
                commandLine);
            return;
        }

        if (Mathf.Abs(pressuredDuration - expectedDuration) > 0.0001f ||
            execution.LastPreparationDurationAdjustmentBasisPoints != 200 ||
            !string.Equals(
                execution.LastAdjustedPreparationLineId,
                line.LineId,
                StringComparison.Ordinal) ||
            provider.LastAdjustmentBasisPoints != 200 ||
            provider.LastContributingCampaigns != 1)
        {
            Finish(false,
                "La presión real no llegó íntegra a 367D. Base=" +
                baselineDuration.ToString("0.000") + " s, esperado=" +
                expectedDuration.ToString("0.000") + " s, real=" +
                pressuredDuration.ToString("0.000") + " s.",
                commandLine);
            return;
        }

        if (!execution.TryResolveDishPreparationDurationSeconds(
                line.DishId,
                0.01f,
                0.1f,
                30f,
                out float baseAfterCampaign,
                out string baseAfterError) ||
            Mathf.Abs(baseAfterCampaign - historicalBase) > 0.0001f)
        {
            Finish(false,
                "Marketing alteró la receta/tiempo base histórico: " +
                baseAfterError,
                commandLine);
            return;
        }

        Finish(
            true,
            "PASS — plato real " + line.DishId + ": base " +
            baselineDuration.ToString("0.000") + " s → presión +2 % = " +
            pressuredDuration.ToString("0.000") +
            " s; receta base intacta y KitchenSystem mantiene 367D real.",
            commandLine);
    }

    private static bool TryCreateTemporaryLinkedOrder(
        BistroBuilderCanonicalOrderService canonical,
        BistroBuilderCanonicalOrderIntegrationService integration,
        out BistroBuilderCanonicalOrder orderSnapshot,
        out BistroBuilderCanonicalOrderLine line,
        out string error)
    {
        orderSnapshot = null;
        line = null;
        const int legacyOrderId = 987654;
        const int tableId = 87654;
        const int groupId = 76543;

        temporaryTableObject = new GameObject("__BB_PRESSURE_TABLE__");
        RestaurantTable table =
            temporaryTableObject.AddComponent<RestaurantTable>();
        SetInteger(table, "capacity", 1);
        table.AssignTableId(tableId);

        temporaryGroupObject = new GameObject("__BB_PRESSURE_GROUP__");
        CustomerGroup group =
            temporaryGroupObject.AddComponent<CustomerGroup>();
        if (!group.Initialize(groupId, 1) || !group.AssignTable(table))
        {
            error = "No pudo inicializarse el grupo temporal de Play Mode.";
            return false;
        }

        temporaryWaiterObject = new GameObject("__BB_PRESSURE_WAITER__");
        Waiter waiter = temporaryWaiterObject.AddComponent<Waiter>();
        SetInteger(waiter, "waiterId", 65432);

        var customerIds = new List<string>(1);
        if (!BistroBuilderServiceOrderIdentityUtility.TryBuildCustomerReferences(
                groupId,
                1,
                customerIds,
                out error))
        {
            return false;
        }

        string externalReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildLegacyOrderReference(legacyOrderId);
        string tableReferenceId =
            BistroBuilderServiceOrderIdentityUtility.BuildTableReference(tableId);
        string groupReferenceId =
            BistroBuilderServiceOrderIdentityUtility.BuildGroupReference(groupId);

        BistroBuilderCanonicalOrderOperationResult createResult =
            canonical.TryCreateIndividualOrder(
                externalReferenceId,
                tableReferenceId,
                groupReferenceId,
                customerIds,
                BistroBuilderMealServiceAvailability.Lunch,
                1,
                out orderSnapshot);

        if (!createResult.Succeeded || orderSnapshot == null ||
            orderSnapshot.Lines.Count == 0)
        {
            error = "No pudo crearse una comanda canónica temporal: " +
                    createResult.Message;
            return false;
        }

        temporaryLegacyOrder = new RestaurantOrder(
            legacyOrderId,
            table,
            group,
            waiter,
            orderSnapshot.OrderId,
            integration);

        if (!integration.TryRegisterLegacyOrder(
                temporaryLegacyOrder,
                out error))
        {
            return false;
        }

        line = orderSnapshot.Lines[0];
        if (line == null)
        {
            error = "La comanda temporal no contiene una línea utilizable.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool RestoreRuntimeState(out string error)
    {
        error = string.Empty;
        BistroBuilderMarketingService marketing = Find<BistroBuilderMarketingService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();
        BistroBuilderCanonicalOrderService canonical =
            Find<BistroBuilderCanonicalOrderService>();
        BistroBuilderCanonicalOrderIntegrationService integration =
            Find<BistroBuilderCanonicalOrderIntegrationService>();

        if (integration != null && temporaryLegacyOrder != null)
            integration.NotifyLegacyOrderRemoved(temporaryLegacyOrder);

        if (canonical != null && originalOrders != null &&
            !canonical.TryReplaceFromRuntimeSnapshot(
                originalOrders,
                false,
                out error))
        {
            return false;
        }

        if (marketing != null && originalMarketing != null &&
            !marketing.TryRestoreSnapshot(originalMarketing, out error))
        {
            return false;
        }

        if (finance != null && originalFinance != null &&
            !finance.TryRestoreSnapshot(originalFinance, out error))
        {
            return false;
        }

        DestroyTemporaryObjects();
        temporaryLegacyOrder = null;
        originalMarketing = null;
        originalFinance = null;
        originalOrders = null;
        error = string.Empty;
        return true;
    }

    private static void DestroyTemporaryObjects()
    {
        DestroySafe(temporaryWaiterObject);
        DestroySafe(temporaryGroupObject);
        DestroySafe(temporaryTableObject);
        temporaryWaiterObject = null;
        temporaryGroupObject = null;
        temporaryTableObject = null;
    }

    private static void DestroySafe(GameObject value)
    {
        if (value != null)
            UnityEngine.Object.Destroy(value);
    }

    private static void Finish(
        bool success,
        string message,
        bool commandLine)
    {
        if ((originalMarketing != null || originalFinance != null ||
             originalOrders != null || temporaryLegacyOrder != null) &&
            !RestoreRuntimeState(out string restoreError))
        {
            success = false;
            message += " Rollback funcional falló: " + restoreError;
            DestroyTemporaryObjects();
        }

        string report =
            "=== BISTRO BUILDER — MARKETING / OPERATIONAL PRESSURE PLAY MODE ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(
            StageKey,
            commandLine ? "exit_cli" : "exit_menu");

        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static void SetInteger(
        UnityEngine.Object target,
        string fieldName,
        int value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException(
                target.GetType().Name + " no expone " + fieldName + ".");
        field.SetValue(target, value);
    }

    private static T Find<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(
            FindObjectsInactive.Include);
    }

    private static void FinishEditor(bool commandLine)
    {
        bool success = SessionState.GetBool(SuccessKey, false);
        SessionState.EraseString(StageKey);
        if (commandLine)
            EditorApplication.Exit(success ? 0 : 1);
    }
}
