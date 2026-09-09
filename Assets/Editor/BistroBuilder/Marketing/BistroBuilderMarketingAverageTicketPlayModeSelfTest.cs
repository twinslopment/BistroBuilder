using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gate funcional AverageTicket sobre Finanzas real. Comprueba un descuento,
/// una experiencia premium y rollback completo del estado temporal.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderMarketingAverageTicketPlayModeSelfTest
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey =
        "BB.Marketing.AverageTicket.Play.Stage";
    private const string SuccessKey =
        "BB.Marketing.AverageTicket.Play.Success";
    private const string ReportPath =
        "AverageTicketPlayModeReport.txt";

    private static BistroBuilderMarketingSnapshot originalMarketing;
    private static BistroBuilderFinanceSnapshot originalFinance;
    private static string originalProgressionStage = string.Empty;
    private static int originalProgressionLevel;

    static BistroBuilderMarketingAverageTicketPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem(
        "Tools/Bistro Builder/Marketing/AverageTicket - PlayMode funcional",
        false,
        7233)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "AverageTicket PlayMode ya está ejecutándose.");

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
        BistroBuilderMarketingSalesPaymentAdjustmentProvider provider =
            Find<BistroBuilderMarketingSalesPaymentAdjustmentProvider>();
        BistroBuilderMenuPortfolioService portfolio =
            Find<BistroBuilderMenuPortfolioService>();
        BistroBuilderMenuOfferService offer = Find<BistroBuilderMenuOfferService>();
        BistroBuilderGeneralGameStateService general =
            Find<BistroBuilderGeneralGameStateService>();

        if (marketing == null || finance == null || provider == null ||
            portfolio == null || offer == null || general == null)
        {
            Finish(false,
                "Faltan autoridades runtime requeridas para AverageTicket.",
                commandLine);
            return;
        }

        string providerError = string.Empty;
        string offerError = string.Empty;
        if (!provider.ValidateConfiguration(out providerError) ||
            !offer.ValidateConfiguration(out offerError))
        {
            Finish(false,
                "Configuración inválida. Proveedor=" + providerError +
                " | Oferta=" + offerError,
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
                "No pudieron capturarse snapshots para rollback.",
                commandLine);
            return;
        }

        var offerItems = new List<BistroBuilderMenuOfferItemSnapshot>(32);
        if (!offer.TryGetOffer(
                BistroBuilderMealServiceAvailability.Lunch,
                BistroBuilderServiceMode.TableService,
                false,
                offerItems,
                out string readError) || offerItems.Count == 0)
        {
            Finish(false,
                "No existe un plato real pedible para el fixture: " + readError,
                commandLine);
            return;
        }

        string realDishId = offerItems[0].DishId;
        if (!RunDiscountScenario(
                marketing,
                finance,
                provider,
                general,
                realDishId,
                out long discountFinal,
                out string discountError))
        {
            Finish(false, discountError, commandLine);
            return;
        }

        if (!RestoreScenarioState(
                marketing,
                finance,
                general,
                out string resetError))
        {
            Finish(false,
                "No pudo restaurarse entre escenarios: " + resetError,
                commandLine);
            return;
        }

        string activeMenuId = portfolio.ActiveMenuId;
        if (!RunPremiumScenario(
                marketing,
                finance,
                provider,
                general,
                activeMenuId,
                realDishId,
                out long premiumFinal,
                out string premiumError))
        {
            Finish(false, premiumError, commandLine);
            return;
        }

        Finish(true,
            "PASS — venta base 100,00 €: descuento real -6 % = " +
            (discountFinal / 100.0).ToString("F2") +
            " €; experiencia Foodies +15 % = " +
            (premiumFinal / 100.0).ToString("F2") +
            " €; FinanceService registró ambos importes exactos.",
            commandLine);
    }

    private static bool RunDiscountScenario(
        BistroBuilderMarketingService marketing,
        BistroBuilderFinanceService finance,
        BistroBuilderMarketingSalesPaymentAdjustmentProvider provider,
        BistroBuilderGeneralGameStateService general,
        string realDishId,
        out long finalAmount,
        out string error)
    {
        finalAmount = 0L;
        if (!PrepareScenario(marketing, general, 2, out error))
            return false;

        if (!marketing.TryStartCampaign(
                "marketing.promo.weekday",
                out _,
                out error))
        {
            error = "No pudo iniciarse Descuento entre semana: " + error;
            return false;
        }

        BistroBuilderSalesPaymentAdjustmentContext context =
            CreateContext(
                general.DayIndex,
                "general",
                720,
                realDishId);

        if (!provider.TryGetAdjustmentBasisPoints(
                context,
                out int bps,
                out error) || bps != -600)
        {
            error = "El descuento real no devolvió -600 pb. Valor=" + bps +
                    ". " + error;
            return false;
        }

        return PostRealSale(
            finance,
            context,
            bps,
            "order_marketing_avg_discount",
            9400L,
            out finalAmount,
            out error);
    }

    private static bool RunPremiumScenario(
        BistroBuilderMarketingService marketing,
        BistroBuilderFinanceService finance,
        BistroBuilderMarketingSalesPaymentAdjustmentProvider provider,
        BistroBuilderGeneralGameStateService general,
        string activeMenuId,
        string realDishId,
        out long finalAmount,
        out string error)
    {
        finalAmount = 0L;
        if (!PrepareScenario(marketing, general, 4, out error))
            return false;

        if (!marketing.TryStartCampaign(
                "marketing.event.tasting_menu",
                activeMenuId,
                out _,
                out error))
        {
            error = "No pudo iniciarse Menú degustación: " + error;
            return false;
        }

        BistroBuilderSalesPaymentAdjustmentContext foodieContext =
            CreateContext(
                general.DayIndex,
                "foodies",
                1200,
                realDishId);

        if (!provider.TryGetAdjustmentBasisPoints(
                foodieContext,
                out int foodieBps,
                out error) || foodieBps != 1500)
        {
            error = "La experiencia Foodies no devolvió +1500 pb. Valor=" +
                    foodieBps + ". " + error;
            return false;
        }

        BistroBuilderSalesPaymentAdjustmentContext workerContext =
            CreateContext(
                general.DayIndex,
                "workers",
                1200,
                realDishId);
        if (!provider.TryGetAdjustmentBasisPoints(
                workerContext,
                out int workerBps,
                out error) || workerBps != 0)
        {
            error = "La experiencia dirigida a Foodies afectó a Workers: " +
                    workerBps + " pb. " + error;
            return false;
        }

        return PostRealSale(
            finance,
            foodieContext,
            foodieBps,
            "order_marketing_avg_premium",
            11500L,
            out finalAmount,
            out error);
    }

    private static bool PrepareScenario(
        BistroBuilderMarketingService marketing,
        BistroBuilderGeneralGameStateService general,
        int minimumLevel,
        out string error)
    {
        if (!marketing.TryRestoreSnapshot(
                BistroBuilderMarketingEngine.CreateEmptySnapshot(),
                out error))
            return false;

        if (!general.TrySetProgression(
                originalProgressionStage,
                Math.Max(minimumLevel, originalProgressionLevel)))
        {
            error = "No pudo elevarse la progresión temporal.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static BistroBuilderSalesPaymentAdjustmentContext CreateContext(
        int dayIndex,
        string segmentId,
        int minuteOfDay,
        string dishId)
    {
        return new BistroBuilderSalesPaymentAdjustmentContext
        {
            canonicalOrderId = "order_marketing_average_ticket",
            customerGroupReferenceId = "group_marketing_average_ticket",
            acquisitionSegmentId = segmentId,
            serviceMode = BistroBuilderServiceMode.TableService,
            mealService = BistroBuilderMealServiceAvailability.Lunch,
            dayIndex = dayIndex,
            minuteOfDay = minuteOfDay,
            baseAmountCents = 10000L,
            orderedDishIds = new List<string> { dishId }
        };
    }

    private static bool PostRealSale(
        BistroBuilderFinanceService finance,
        BistroBuilderSalesPaymentAdjustmentContext context,
        int basisPoints,
        string orderId,
        long expectedAmount,
        out long finalAmount,
        out string error)
    {
        finalAmount = 0L;
        if (!BistroBuilderSalesRevenuePolicy.TryApplyPaymentAdjustment(
                context.baseAmountCents,
                basisPoints,
                out finalAmount,
                out error))
            return false;

        if (finalAmount != expectedAmount)
        {
            error = "El importe ajustado no coincide. Esperado=" +
                    expectedAmount + ", real=" + finalAmount + ".";
            return false;
        }

        long balanceBefore = finance.CurrentBalanceCents;
        long revisionBefore = finance.Revision;
        int transactionCountBefore = finance.TransactionCount;

        if (!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                orderId,
                context.serviceMode,
                context.mealService,
                finalAmount,
                context.dayIndex,
                context.minuteOfDay,
                out BistroBuilderFinanceTransactionRequest request,
                out error))
            return false;

        if (!finance.TryPostTransaction(
                request,
                out BistroBuilderFinanceTransactionRecord posted,
                out error))
            return false;

        if (posted == null || posted.amountCents != expectedAmount ||
            finance.CurrentBalanceCents != balanceBefore + expectedAmount ||
            finance.Revision != revisionBefore + 1L ||
            finance.TransactionCount != transactionCountBefore + 1)
        {
            error = "FinanceService no registró exactamente el cobro ajustado.";
            return false;
        }

        if (context.baseAmountCents != 10000L)
        {
            error = "El ajuste alteró el importe base histórico del contexto.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool RestoreScenarioState(
        BistroBuilderMarketingService marketing,
        BistroBuilderFinanceService finance,
        BistroBuilderGeneralGameStateService general,
        out string error)
    {
        if (originalMarketing == null || originalFinance == null)
        {
            error = "Los snapshots originales no están disponibles.";
            return false;
        }

        if (!marketing.TryRestoreSnapshot(originalMarketing, out error))
            return false;
        if (!finance.TryRestoreSnapshot(originalFinance, out error))
            return false;

        if (!string.IsNullOrWhiteSpace(originalProgressionStage) &&
            !general.TrySetProgression(
                originalProgressionStage,
                Math.Max(1, originalProgressionLevel)))
        {
            error = "No pudo restaurarse la progresión original.";
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
        BistroBuilderMarketingService marketing = Find<BistroBuilderMarketingService>();
        BistroBuilderFinanceService finance = Find<BistroBuilderFinanceService>();
        BistroBuilderGeneralGameStateService general =
            Find<BistroBuilderGeneralGameStateService>();

        if (marketing != null && finance != null && general != null &&
            originalMarketing != null && originalFinance != null &&
            !RestoreScenarioState(
                marketing,
                finance,
                general,
                out string restoreError))
        {
            success = false;
            message += " Rollback funcional falló: " + restoreError;
        }

        string report =
            "=== BISTRO BUILDER — MARKETING / AVERAGE TICKET PLAY MODE ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        originalMarketing = null;
        originalFinance = null;
        originalProgressionStage = string.Empty;
        originalProgressionLevel = 0;
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(
            StageKey,
            commandLine ? "exit_cli" : "exit_menu");

        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
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
