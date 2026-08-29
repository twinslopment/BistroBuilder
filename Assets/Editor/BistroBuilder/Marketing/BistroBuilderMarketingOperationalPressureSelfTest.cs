using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate puro de OperationalPressure: política temporal, objetivos y regresión
/// de los efectos Marketing ya materializados.
/// </summary>
public static class BistroBuilderMarketingOperationalPressureSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/OperationalPressure - Autotest",
        false,
        7240)]
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
            BistroBuilderPreparationDurationAdjustmentPolicy.TryApply(
                3.25f, 0.1f, 30f, 0, out float unchanged, out _) &&
            unchanged == 3.25f,
            "Sin proveedor, la duración permanece exactamente igual.",
            ref passed, ref failed, lines);
        Check(
            BistroBuilderPreparationDurationAdjustmentPolicy.TryApply(
                10f, 0.1f, 30f, 1800, out float pressured, out _) &&
            Mathf.Abs(pressured - 11.8f) < 0.0001f,
            "+18 % convierte 10,0 s en 11,8 s.",
            ref passed, ref failed, lines);
        Check(
            BistroBuilderPreparationDurationAdjustmentPolicy.TryApply(
                29f, 0.1f, 30f, 1800, out float clamped, out _) &&
            Mathf.Abs(clamped - 30f) < 0.0001f,
            "El máximo histórico de cocina sigue limitando la presión.",
            ref passed, ref failed, lines);

        Check(
            !BistroBuilderPreparationDurationAdjustmentPolicy.TryApply(
                10f, 0.1f, 30f, -5001, out _, out _) &&
            !BistroBuilderPreparationDurationAdjustmentPolicy.TryApply(
                10f, 0.1f, 30f, 50001, out _, out _),
            "La política rechaza ajustes fuera del rango seguro.",
            ref passed, ref failed, lines);

        List<BistroBuilderMarketingCampaignDefinition> seed =
            BistroBuilderMarketing7ASeedFactory.CreateSeed();
        RunGlobalGate(seed, ref passed, ref failed, lines);
        RunDishTargetGate(seed, ref passed, ref failed, lines);
        RunMenuTargetGate(seed, ref passed, ref failed, lines);
        RunStackingGate(seed, ref passed, ref failed, lines);
        RunExpiryGate(seed, ref passed, ref failed, lines);

        bool averageTicketOk = BistroBuilderMarketingAverageTicketSelfTest.Run(
            out int averageTicketPassed,
            out int averageTicketFailed,
            out _);
        Check(
            averageTicketOk && averageTicketFailed == 0 &&
            averageTicketPassed >= 10,
            "AverageTicket y sus gates previos permanecen verdes.",
            ref passed, ref failed, lines);

        report =
            "=== BISTRO BUILDER — MARKETING / OPERATIONAL PRESSURE ===\n" +
            string.Join("\n", lines) +
            "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static void RunGlobalGate(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        BistroBuilderMarketingSnapshot snapshot = Activate(
            seed,
            "marketing.local.city",
            3,
            string.Empty,
            "pressure_city");
        bool ok = TryEvaluate(
            snapshot,
            seed,
            3,
            null,
            out int basisPoints,
            out int contributors);
        Check(
            ok && basisPoints == 700 && contributors == 1,
            "Gran campaña de ciudad aporta +7 % de presión global.",
            ref passed, ref failed, lines);
    }

    private static void RunDishTargetGate(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        const string targetDishId = "dish_pressure_target";
        BistroBuilderMarketingSnapshot snapshot = Activate(
            seed,
            "marketing.influencer.food_creator",
            3,
            targetDishId,
            "pressure_dish");

        bool withoutTarget = TryEvaluate(
            snapshot,
            seed,
            3,
            new HashSet<string>(StringComparer.Ordinal),
            out int withoutBasisPoints,
            out _);
        var applicable = new HashSet<string>(StringComparer.Ordinal)
        {
            targetDishId
        };
        bool withTarget = TryEvaluate(
            snapshot, seed, 3, applicable,
            out int withBasisPoints, out int contributors);

        Check(
            withoutTarget && withoutBasisPoints == 0 &&
            withTarget && withBasisPoints == 500 && contributors == 1,
            "La presión de creador gastronómico solo afecta al DishId objetivo.",
            ref passed, ref failed, lines);
    }

    private static void RunMenuTargetGate(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        const string targetMenuId = "menu_pressure_target";
        BistroBuilderMarketingSnapshot snapshot = Activate(
            seed,
            "marketing.event.tasting_menu",
            3,
            targetMenuId,
            "pressure_menu");
        var applicable = new HashSet<string>(StringComparer.Ordinal)
        {
            targetMenuId
        };
        bool ok = TryEvaluate(
            snapshot, seed, 3, applicable,
            out int basisPoints, out int contributors);

        Check(
            ok && basisPoints == 900 && contributors == 1,
            "El menú degustación aplica +9 % solo a su carta objetivo.",
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
            "marketing.local.city",
            3,
            string.Empty,
            "pressure_stack_city");
        snapshot = ActivateFromSnapshot(
            snapshot,
            seed,
            "marketing.event.theme_night",
            3,
            string.Empty,
            "pressure_stack_theme");

        bool ok = TryEvaluate(
            snapshot, seed, 3, null,
            out int basisPoints, out int contributors);

        Check(
            ok && basisPoints == 1900 && contributors == 2,
            "Dos campañas compatibles apilan +19 % de presión.",
            ref passed, ref failed, lines);
    }

    private static void RunExpiryGate(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        BistroBuilderMarketingSnapshot snapshot = Activate(
            seed,
            "marketing.event.theme_night",
            3,
            string.Empty,
            "pressure_expiry");
        bool ok = TryEvaluate(
            snapshot,
            seed,
            4,
            null,
            out int basisPoints,
            out int contributors);

        Check(
            ok && basisPoints == 0 && contributors == 0,
            "La presión desaparece al expirar la campaña de un día.",
            ref passed, ref failed, lines);
    }

    private static bool TryEvaluate(
        BistroBuilderMarketingSnapshot snapshot,
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        int dayIndex,
        ISet<string> applicableTargets,
        out int basisPoints,
        out int contributors)
    {
        return BistroBuilderMarketingEngine.TryEvaluateOperationalPressure(
            snapshot,
            seed,
            dayIndex,
            BistroBuilderMarketingCustomerSegment.Any,
            BistroBuilderMarketingDayPart.Dinner,
            applicableTargets,
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
            lines.Add("[ERROR] " + message);
        }
    }
}
