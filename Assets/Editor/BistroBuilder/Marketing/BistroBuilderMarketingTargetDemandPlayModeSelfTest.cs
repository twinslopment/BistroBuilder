using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gate funcional TargetDemand sobre la escena real: plato, carta activa y
/// rechazo pre-Finanzas de objetivos inexistentes.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderMarketingTargetDemandPlayModeSelfTest
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey =
        "BB.Marketing.TargetDemand.Play.Stage";
    private const string SuccessKey =
        "BB.Marketing.TargetDemand.Play.Success";
    private const string ReportPath =
        "TargetDemandPlayModeReport.txt";

    private static BistroBuilderMarketingSnapshot originalMarketing;
    private static BistroBuilderFinanceSnapshot originalFinance;
    private static string originalProgressionStage = string.Empty;
    private static int originalProgressionLevel;

    static BistroBuilderMarketingTargetDemandPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem(
        "Tools/Bistro Builder/Marketing/TargetDemand - PlayMode funcional",
        false,
        7223)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "TargetDemand PlayMode ya está ejecutándose.");

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
        BistroBuilderMarketingService marketing =
            Find<BistroBuilderMarketingService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();
        BistroBuilderMenuSelectionService selection =
            Find<BistroBuilderMenuSelectionService>();
        BistroBuilderMenuOfferService offer = Find<BistroBuilderMenuOfferService>();
        BistroBuilderMenuPortfolioService portfolio =
            Find<BistroBuilderMenuPortfolioService>();
        BistroBuilderMarketingDemandIntegrationService demand =
            Find<BistroBuilderMarketingDemandIntegrationService>();
        BistroBuilderGeneralGameStateService general =
            Find<BistroBuilderGeneralGameStateService>();
        BistroBuilderMarketingMenuSelectionWeightProvider provider =
            Find<BistroBuilderMarketingMenuSelectionWeightProvider>();

        if (marketing == null || finance == null || selection == null ||
            offer == null || portfolio == null || demand == null ||
            general == null || provider == null)
        {
            Finish(false,
                "Faltan autoridades runtime para TargetDemand.",
                commandLine);
            return;
        }

        string marketingError = string.Empty;
        string offerError = string.Empty;
        string demandError = string.Empty;
        string providerError = string.Empty;
        if (!marketing.ValidateConfiguration(out marketingError) ||
            !offer.ValidateConfiguration(out offerError) ||
            !demand.ValidateConfiguration(out demandError) ||
            !provider.ValidateConfiguration(out providerError))
        {
            Finish(false,
                "Configuración runtime inválida. Marketing=" + marketingError +
                " | Oferta=" + offerError +
                " | Demanda=" + demandError +
                " | Proveedor=" + providerError,
                commandLine);
            return;
        }

        originalMarketing = marketing.CreateSnapshot();
        originalFinance = finance.CreateSnapshot();
        originalProgressionStage = general.ProgressionStageId;
        originalProgressionLevel = general.ProgressionLevel;

        if (originalMarketing == null || originalFinance == null)
        {
            Finish(false,
                "No pudieron capturarse snapshots de rollback.",
                commandLine);
            return;
        }

        if (!marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(),
                out string resetError))
        {
            Finish(false, "No pudo limpiarse Marketing: " + resetError,
                commandLine);
            return;
        }

        int testLevel = Math.Max(3, originalProgressionLevel);
        if (!general.TrySetProgression(originalProgressionStage, testLevel))
        {
            Finish(false,
                "No pudo elevarse temporalmente la progresión.",
                commandLine);
            return;
        }

        var candidates = new List<BistroBuilderMenuOfferItemSnapshot>(32);
        if (!offer.TryGetCurrentOffer(
                BistroBuilderServiceMode.TableService,
                false,
                candidates,
                out string offerReadError) || candidates.Count < 2)
        {
            Finish(false,
                "La oferta real no expone al menos 2 platos pedibles: " +
                offerReadError,
                commandLine);
            return;
        }

        string targetDishId = candidates[0].DishId;
        if (!RunDishSelectionTest(
                marketing,
                finance,
                selection,
                offer,
                candidates,
                targetDishId,
                out int baselineDishCount,
                out int promotedDishCount,
                out string dishError))
        {
            Finish(false, dishError, commandLine);
            return;
        }

        if (!RestoreFinanceAndClearMarketing(
                marketing,
                finance,
                out string midRestoreError))
        {
            Finish(false,
                "No pudo limpiarse el escenario de plato: " + midRestoreError,
                commandLine);
            return;
        }

        string activeMenuId = portfolio.ActiveMenuId;
        if (!RunActiveMenuDemandTest(
                marketing,
                demand,
                activeMenuId,
                out int baselineMenuBasisPoints,
                out int promotedMenuBasisPoints,
                out string menuError))
        {
            Finish(false, menuError, commandLine);
            return;
        }

        if (!RestoreFinanceAndClearMarketing(
                marketing,
                finance,
                out midRestoreError))
        {
            Finish(false,
                "No pudo limpiarse el escenario de carta: " + midRestoreError,
                commandLine);
            return;
        }

        if (!RunInvalidTargetFinanceTest(
                marketing,
                finance,
                out string invalidError))
        {
            Finish(false, invalidError, commandLine);
            return;
        }

        Finish(true,
            "PASS — plato real " + targetDishId + " " +
            baselineDishCount + "→" + promotedDishCount +
            " selecciones; carta activa " + activeMenuId +
            " " + baselineMenuBasisPoints + "→" +
            promotedMenuBasisPoints +
            " pb de demanda; objetivo inexistente rechazado sin cargo.",
            commandLine);
    }

    private static bool RunDishSelectionTest(
        BistroBuilderMarketingService marketing,
        BistroBuilderFinanceService finance,
        BistroBuilderMenuSelectionService selection,
        BistroBuilderMenuOfferService offer,
        List<BistroBuilderMenuOfferItemSnapshot> candidates,
        string targetDishId,
        out int baselineCount,
        out int promotedCount,
        out string error)
    {
        const int Samples = 2048;
        promotedCount = 0;
        baselineCount = CountSelections(
            selection,
            offer,
            candidates,
            targetDishId,
            Samples,
            out error);
        if (baselineCount < 0) return false;

        long balanceBefore = finance.CurrentBalanceCents;
        long revisionBefore = finance.Revision;
        int transactionsBefore = finance.TransactionCount;

        if (!marketing.TryStartCampaign(
                "marketing.menu.dish_week",
                targetDishId,
                out BistroBuilderMarketingCampaignRecord started,
                out error))
        {
            error = "La campaña real de plato no pudo iniciarse: " + error;
            return false;
        }

        if (started == null ||
            started.targetId != BistroBuilderMarketingEngine.NormalizeId(
                targetDishId) ||
            finance.TransactionCount != transactionsBefore + 1 ||
            finance.Revision <= revisionBefore ||
            finance.CurrentBalanceCents !=
                balanceBefore - started.paidCostCents)
        {
            error = "La campaña de plato no quedó enlazada a Finanzas/objetivo.";
            return false;
        }

        promotedCount = CountSelections(
            selection,
            offer,
            candidates,
            targetDishId,
            Samples,
            out error);
        if (promotedCount < 0) return false;

        int minimumGain = Math.Max(10, baselineCount / 20);
        if (promotedCount < baselineCount + minimumGain)
        {
            error = "El plato real no ganó selección suficiente: " +
                    baselineCount + "→" + promotedCount + ".";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int CountSelections(
        BistroBuilderMenuSelectionService selection,
        BistroBuilderMenuOfferService offer,
        List<BistroBuilderMenuOfferItemSnapshot> candidates,
        string targetDishId,
        int samples,
        out string error)
    {
        int count = 0;
        for (int sample = 0; sample < samples; sample++)
        {
            var context = new BistroBuilderMenuSelectionContext(
                offer.CurrentMealService,
                BistroBuilderServiceMode.TableService,
                "marketing_target_play_" + sample,
                1,
                sample,
                sample % candidates.Count);
            var random = new BistroBuilderMenuSelectionDeterministicRandom(
                (ulong)sample + 1001UL);

            if (!selection.TrySelectFromCandidates(
                    context,
                    candidates,
                    null,
                    random,
                    out BistroBuilderMenuSelectionResult result,
                    out error))
            {
                error = "La selección real falló en muestra " + sample +
                        ": " + error;
                return -1;
            }

            if (result.DishId == targetDishId) count++;
        }

        error = string.Empty;
        return count;
    }

    private static bool RunActiveMenuDemandTest(
        BistroBuilderMarketingService marketing,
        BistroBuilderMarketingDemandIntegrationService demand,
        string activeMenuId,
        out int baselineBasisPoints,
        out int promotedBasisPoints,
        out string error)
    {
        baselineBasisPoints = 0;
        promotedBasisPoints = 0;

        string normalizedMenuId =
            BistroBuilderMenuIdUtility.NormalizeStableId(activeMenuId);
        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalizedMenuId))
        {
            error = "El portfolio no expone un ActiveMenuId válido.";
            return false;
        }

        if (!demand.TryBuildProjection(
                out BistroBuilderMarketingDemandProjection baseline,
                out error) || baseline == null)
        {
            error = "No pudo calcularse demanda base: " + error;
            return false;
        }
        baselineBasisPoints = baseline.effectiveWalkInBasisPoints;

        if (!marketing.TryStartCampaign(
                "marketing.menu.star_menu",
                normalizedMenuId,
                out BistroBuilderMarketingCampaignRecord started,
                out error))
        {
            error = "La campaña real de carta no pudo iniciarse: " + error;
            return false;
        }
        if (started == null ||
            !string.Equals(
                started.targetId,
                normalizedMenuId,
                StringComparison.Ordinal))
        {
            error = "La campaña de carta no conserva el ActiveMenuId real.";
            return false;
        }

        if (!demand.TryBuildProjection(
                out BistroBuilderMarketingDemandProjection promoted,
                out error) || promoted == null)
        {
            error = "No pudo recalcularse demanda con carta promocionada: " +
                    error;
            return false;
        }

        promotedBasisPoints = promoted.effectiveWalkInBasisPoints;
        if (promotedBasisPoints < baselineBasisPoints + 1800)
        {
            error = "La carta activa no aumentó demanda suficiente: " +
                    baselineBasisPoints + "→" + promotedBasisPoints + " pb.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool RunInvalidTargetFinanceTest(
        BistroBuilderMarketingService marketing,
        BistroBuilderFinanceService finance,
        out string error)
    {
        const string InvalidDishId = "dish_target_does_not_exist";
        long balanceBefore = finance.CurrentBalanceCents;
        long revisionBefore = finance.Revision;
        int transactionCountBefore = finance.TransactionCount;
        long marketingRevisionBefore = marketing.Revision;

        bool started = marketing.TryStartCampaign(
            "marketing.menu.dish_week",
            InvalidDishId,
            out BistroBuilderMarketingCampaignRecord record,
            out string startError);

        if (started || record != null)
        {
            error = "Marketing aceptó un DishId inexistente.";
            return false;
        }

        if (finance.CurrentBalanceCents != balanceBefore ||
            finance.Revision != revisionBefore ||
            finance.TransactionCount != transactionCountBefore ||
            marketing.Revision != marketingRevisionBefore)
        {
            error = "El objetivo inexistente alteró Finanzas o Marketing antes " +
                    "de ser rechazado.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(startError))
        {
            error = "El rechazo del DishId inexistente no explicó la causa.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool RestoreFinanceAndClearMarketing(
        BistroBuilderMarketingService marketing,
        BistroBuilderFinanceService finance,
        out string error)
    {
        if (originalFinance == null)
        {
            error = "No existe snapshot financiero para limpiar el escenario.";
            return false;
        }

        if (!finance.TryRestoreSnapshot(originalFinance, out error))
            return false;

        if (!marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(),
                out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void Finish(
        bool success,
        string message,
        bool commandLine)
    {
        if (!RestoreOriginalState(out string restoreError))
        {
            success = false;
            message += " Rollback funcional falló: " + restoreError;
        }

        string report =
            "=== BISTRO BUILDER — MARKETING / TARGET DEMAND PLAY MODE ===\n" +
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

    private static bool RestoreOriginalState(out string error)
    {
        error = string.Empty;
        BistroBuilderMarketingService marketing =
            Find<BistroBuilderMarketingService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();
        BistroBuilderGeneralGameStateService general =
            Find<BistroBuilderGeneralGameStateService>();

        if (marketing != null && originalMarketing != null &&
            !marketing.TryRestoreSnapshot(originalMarketing, out error))
            return false;

        if (finance != null && originalFinance != null &&
            !finance.TryRestoreSnapshot(originalFinance, out error))
            return false;

        if (general != null &&
            !string.IsNullOrWhiteSpace(originalProgressionStage) &&
            !general.TrySetProgression(
                originalProgressionStage,
                Math.Max(1, originalProgressionLevel)))
        {
            error = "No pudo restaurarse la progresión original.";
            return false;
        }

        originalMarketing = null;
        originalFinance = null;
        originalProgressionStage = string.Empty;
        originalProgressionLevel = 0;
        error = string.Empty;
        return true;
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
