using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate puro de AverageTicket: política de cobro, segmentación, franjas,
/// objetivos y regresión de Finanzas 3B/TargetDemand.
/// </summary>
public static class BistroBuilderMarketingAverageTicketSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/AverageTicket - Autotest",
        false,
        7230)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static void RunFromCommandLine()
    {
        if (!Run(out _, out _, out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();

        Check(
            BistroBuilderSalesRevenuePolicy.TryApplyPaymentAdjustment(
                12345L, 0, out long unchanged, out _) &&
            unchanged == 12345L,
            "Sin proveedor, el importe de 3B permanece idéntico.",
            ref passed, ref failed, lines);
        Check(
            BistroBuilderSalesRevenuePolicy.TryApplyPaymentAdjustment(
                10000L, -1800, out long discounted, out _) &&
            discounted == 8200L,
            "Un -18 % convierte 100,00 € en 82,00 €.",
            ref passed, ref failed, lines);
        Check(
            BistroBuilderSalesRevenuePolicy.TryApplyPaymentAdjustment(
                10000L, 1500, out long uplifted, out _) &&
            uplifted == 11500L,
            "Un +15 % convierte 100,00 € en 115,00 €.",
            ref passed, ref failed, lines);
        Check(
            !BistroBuilderSalesRevenuePolicy.TryApplyPaymentAdjustment(
                10000L, -9001, out _, out _) &&
            !BistroBuilderSalesRevenuePolicy.TryApplyPaymentAdjustment(
                10000L, 50001, out _, out _),
            "La política rechaza ajustes fuera del rango seguro.",
            ref passed, ref failed, lines);

        List<BistroBuilderMarketingCampaignDefinition> seed =
            BistroBuilderMarketing7ASeedFactory.CreateSeed();

        RunGlobalDiscountGate(
            seed,
            ref passed,
            ref failed,
            lines);
        RunSegmentAndDayPartGate(
            seed,
            ref passed,
            ref failed,
            lines);
        RunTargetedMenuGate(
            seed,
            ref passed,
            ref failed,
            lines);
        RunStackingGate(
            seed,
            ref passed,
            ref failed,
            lines);

        bool finance3BOk = BistroBuilderFinance3BSelfTest.Run(
            out int financePassed,
            out int financeFailed,
            out _);
        Check(
            finance3BOk && financeFailed == 0 && financePassed > 0,
            "El autotest histórico Finanzas 3B permanece verde.",
            ref passed, ref failed, lines);

        bool targetOk = BistroBuilderMarketingTargetDemandSelfTest.Run(
            out int targetPassed,
            out int targetFailed,
            out _);
        Check(
            targetOk && targetFailed == 0 && targetPassed >= 7,
            "TargetDemand permanece íntegro tras extender los cobros.",
            ref passed, ref failed, lines);

        report =
            "=== BISTRO BUILDER — MARKETING / AVERAGE TICKET ===\n" +
            string.Join("\n", lines) +
            "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static void RunGlobalDiscountGate(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        BistroBuilderMarketingSnapshot snapshot = Activate(
            seed,
            "marketing.promo.weekday",
            3,
            string.Empty,
            "avg_weekday");

        bool ok = TryEvaluate(
            snapshot,
            seed,
            3,
            BistroBuilderMarketingCustomerSegment.Any,
            BistroBuilderMarketingDayPart.Lunch,
            null,
            out int basisPoints,
            out int contributors);

        Check(
            ok && basisPoints == -600 && contributors == 1,
            "Descuento entre semana aporta -6 % global al ticket.",
            ref passed, ref failed, lines);
    }

    private static void RunSegmentAndDayPartGate(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        BistroBuilderMarketingSnapshot snapshot = Activate(
            seed,
            "marketing.promo.menu_day",
            3,
            string.Empty,
            "avg_menu_day");

        bool workersLunch = TryEvaluate(
            snapshot,
            seed,
            3,
            BistroBuilderMarketingCustomerSegment.Workers,
            BistroBuilderMarketingDayPart.Lunch,
            null,
            out int workersLunchBps,
            out _);
        bool workersDinner = TryEvaluate(
            snapshot,
            seed,
            3,
            BistroBuilderMarketingCustomerSegment.Workers,
            BistroBuilderMarketingDayPart.Dinner,
            null,
            out int workersDinnerBps,
            out _);
        bool genericLunch = TryEvaluate(
            snapshot,
            seed,
            3,
            BistroBuilderMarketingCustomerSegment.Any,
            BistroBuilderMarketingDayPart.Lunch,
            null,
            out int genericLunchBps,
            out _);

        Check(
            workersLunch && workersDinner && genericLunch &&
            workersLunchBps == -700 &&
            workersDinnerBps == 0 &&
            genericLunchBps == 0,
            "Menú del día descuenta solo a Workers durante Lunch.",
            ref passed, ref failed, lines);
    }

    private static void RunTargetedMenuGate(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        BistroBuilderMarketingSnapshot snapshot = Activate(
            seed,
            "marketing.event.tasting_menu",
            4,
            "menu_default",
            "avg_tasting");

        var matchingTargets = new HashSet<string>(StringComparer.Ordinal)
        {
            "menu_default"
        };
        var wrongTargets = new HashSet<string>(StringComparer.Ordinal)
        {
            "menu_other"
        };

        bool matching = TryEvaluate(
            snapshot,
            seed,
            4,
            BistroBuilderMarketingCustomerSegment.Foodies,
            BistroBuilderMarketingDayPart.Dinner,
            matchingTargets,
            out int matchingBps,
            out int contributors);
        bool wrongTarget = TryEvaluate(
            snapshot,
            seed,
            4,
            BistroBuilderMarketingCustomerSegment.Foodies,
            BistroBuilderMarketingDayPart.Dinner,
            wrongTargets,
            out int wrongTargetBps,
            out _);
        bool wrongSegment = TryEvaluate(
            snapshot,
            seed,
            4,
            BistroBuilderMarketingCustomerSegment.Workers,
            BistroBuilderMarketingDayPart.Dinner,
            matchingTargets,
            out int wrongSegmentBps,
            out _);

        Check(
            matching && wrongTarget && wrongSegment &&
            matchingBps == 1500 && contributors == 1 &&
            wrongTargetBps == 0 && wrongSegmentBps == 0,
            "Menú degustación +15 % solo afecta al MenuId y segmento correctos.",
            ref passed, ref failed, lines);
    }

    private static void RunStackingGate(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        BistroBuilderMarketingSnapshot snapshot = Activate(
            seed,
            "marketing.promo.celebration_week",
            5,
            string.Empty,
            "avg_celebration");
        snapshot = ActivateFromSnapshot(
            snapshot,
            seed,
            "marketing.event.romantic_dinner",
            5,
            string.Empty,
            "avg_romantic");

        bool ok = TryEvaluate(
            snapshot,
            seed,
            5,
            BistroBuilderMarketingCustomerSegment.Couples,
            BistroBuilderMarketingDayPart.Dinner,
            null,
            out int basisPoints,
            out int contributors);

        Check(
            ok && basisPoints == 0 && contributors == 2,
            "Los modificadores compatibles se agregan de forma determinista (-5 %+5 %=0).",
            ref passed, ref failed, lines);
    }

    private static bool TryEvaluate(
        BistroBuilderMarketingSnapshot snapshot,
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        int dayIndex,
        BistroBuilderMarketingCustomerSegment segment,
        BistroBuilderMarketingDayPart dayPart,
        ISet<string> targets,
        out int basisPoints,
        out int contributors)
    {
        return BistroBuilderMarketingEngine.TryEvaluateAverageTicket(
            snapshot,
            seed,
            dayIndex,
            segment,
            dayPart,
            targets,
            out basisPoints,
            out contributors,
            out _);
    }

    private static BistroBuilderMarketingSnapshot Activate(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        string campaignId,
        int dayIndex,
        string targetId,
        string instanceId)
    {
        return ActivateFromSnapshot(
            BistroBuilderMarketingEngine.CreateEmptySnapshot(),
            seed,
            campaignId,
            dayIndex,
            targetId,
            instanceId);
    }

    private static BistroBuilderMarketingSnapshot ActivateFromSnapshot(
        BistroBuilderMarketingSnapshot source,
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        string campaignId,
        int dayIndex,
        string targetId,
        string instanceId)
    {
        BistroBuilderMarketingCampaignDefinition definition =
            Find(seed, campaignId);
        if (definition == null)
            throw new InvalidOperationException(
                "No existe la campaña de prueba " + campaignId + ".");

        if (!BistroBuilderMarketingEngine.TryCreateCampaign(
                source,
                definition,
                dayIndex,
                targetId,
                instanceId,
                "marketing.expense." + instanceId,
                out BistroBuilderMarketingSnapshot result,
                out string error))
        {
            throw new InvalidOperationException(
                "No pudo activarse " + campaignId + ": " + error);
        }

        return result;
    }

    private static BistroBuilderMarketingCampaignDefinition Find(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        string campaignId)
    {
        string id = BistroBuilderMarketingEngine.NormalizeId(campaignId);
        for (int index = 0; index < seed.Count; index++)
        {
            if (BistroBuilderMarketingEngine.NormalizeId(
                    seed[index].campaignId) == id)
                return seed[index];
        }
        return null;
    }

    private static void Check(
        bool condition,
        string message,
        ref int passed,
        ref int failed,
        List<string> lines)
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
