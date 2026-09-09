using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

/// <summary>
/// Autotest puro de 7B. Demuestra proyección discreta de demanda y que el
/// flujo canónico de clientes no depende directamente de Marketing.
/// </summary>
public static class BistroBuilderMarketing7BSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/7B - Autotest demanda jugable",
        false,
        7210)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) UnityEngine.Debug.Log(report);
        else UnityEngine.Debug.LogError(report);
    }

    public static bool RunFromCommandLine()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) UnityEngine.Debug.Log(report);
        else UnityEngine.Debug.LogError(report);
        if (!ok) throw new InvalidOperationException(report);
        return ok;
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();
        List<BistroBuilderMarketingCampaignDefinition> seed =
            BistroBuilderMarketing7ASeedFactory.CreateSeed();

        var zeroGlobal = new BistroBuilderMarketingEffectSnapshot();
        Dictionary<BistroBuilderMarketingCustomerSegment,
            BistroBuilderMarketingEffectSnapshot> zeroSegments =
                CreateEmptySegmentEffects();

        bool baselineOk = BistroBuilderMarketingDemandEngine.TryBuildProjection(
            3,
            zeroGlobal,
            zeroSegments,
            out BistroBuilderMarketingDemandProjection baseline,
            out _);
        Check(
            baselineOk && baseline.adjustedWalkInGroups == 3 &&
            baseline.reservationLeadCount == 0 &&
            baseline.walkInSegments.Count == 3,
            "Sin Marketing, 3 grupos base siguen siendo 3.",
            ref passed, ref failed, lines);

        BistroBuilderMarketingSnapshot active =
            BistroBuilderMarketingEngine.CreateEmptySnapshot();
        Check(
            TryActivate(
                active,
                Find(seed, "marketing.local.city"),
                "7b_city",
                out active),
            "Campaña global de ciudad se activa en snapshot puro.",
            ref passed, ref failed, lines);
        Check(
            TryActivate(
                active,
                Find(seed, "marketing.local.flyers"),
                "7b_flyers",
                out active),
            "Flyers segmentados se acumulan sin duplicar autoridad.",
            ref passed, ref failed, lines);
        Check(
            TryActivate(
                active,
                Find(seed, "marketing.digital.online_reservations"),
                "7b_reservations",
                out active),
            "Campaña de reservas online se activa en snapshot puro.",
            ref passed, ref failed, lines);

        Check(
            TryEvaluateProjection(
                active,
                seed,
                3,
                BistroBuilderMarketingDayPart.Lunch,
                out BistroBuilderMarketingDemandProjection boosted) &&
            boosted.adjustedWalkInGroups == 4,
            "Demanda real redondea 3 grupos base a 4 con campañas activas.",
            ref passed, ref failed, lines);
        Check(
            boosted != null &&
            boosted.walkInSegments.Count == 4 &&
            boosted.walkInSegments[0] ==
                BistroBuilderMarketingCustomerSegment.LocalResidents,
            "La captación local desplaza visiblemente la mezcla de perfiles.",
            ref passed, ref failed, lines);
        Check(
            boosted != null &&
            boosted.reservationLeadCount == 1 &&
            boosted.reservationSegments[0] ==
                BistroBuilderMarketingCustomerSegment.Planners,
            "+17 % en reservas de Planners produce 1 lead discreto y acotado.",
            ref passed, ref failed, lines);
        Check(
            boosted != null && boosted.reservationLeadCount <= 3,
            "Los leads automáticos de reserva quedan limitados por día.",
            ref passed, ref failed, lines);

        Check(
            BistroBuilderMarketingDemandEngine.ResolveDayPart(600) ==
                BistroBuilderMarketingDayPart.Breakfast &&
            BistroBuilderMarketingDemandEngine.ResolveDayPart(780) ==
                BistroBuilderMarketingDayPart.Lunch &&
            BistroBuilderMarketingDemandEngine.ResolveDayPart(1020) ==
                BistroBuilderMarketingDayPart.Afternoon &&
            BistroBuilderMarketingDemandEngine.ResolveDayPart(1200) ==
                BistroBuilderMarketingDayPart.Dinner,
            "Las franjas horarias se proyectan de forma determinista.",
            ref passed, ref failed, lines);

        var plan = new BistroBuilderCustomerDemandPlan
        {
            planId = "marketing.demand.test",
            walkInGroupCount = 2,
            profiles = new List<BistroBuilderCustomerAcquisitionProfile>
            {
                new BistroBuilderCustomerAcquisitionProfile
                {
                    segmentId = "localresidents",
                    sourceSystemId = "marketing.runtime",
                    sourceReferenceId = "marketing.demand.test",
                    marketingInfluenced = true
                },
                new BistroBuilderCustomerAcquisitionProfile
                {
                    segmentId = "workers",
                    sourceSystemId = "service.baseline",
                    sourceReferenceId = string.Empty,
                    marketingInfluenced = false
                }
            }
        };
        Check(
            plan.TryValidate(out _),
            "El plan genérico de CustomerGroup acepta perfiles de captación.",
            ref passed, ref failed, lines);

        BistroBuilderCustomerDemandPlan broken = plan.DeepClone();
        broken.walkInGroupCount = 3;
        Check(
            !broken.TryValidate(out _),
            "El plan rechaza cardinalidad distinta entre grupos y perfiles.",
            ref passed, ref failed, lines);

        Check(
            CustomerSpawnerHasNoMarketingField(),
            "CustomerGroupSpawner no contiene dependencias directas de Marketing.",
            ref passed, ref failed, lines);
        Check(
            AcquisitionTagIsGeneric(),
            "La etiqueta runtime de captación no referencia tipos de Marketing.",
            ref passed, ref failed, lines);

        bool sevenAOk = BistroBuilderMarketing7ASelfTest.Run(
            out int sevenAPassed,
            out int sevenAFailed,
            out _);
        Check(
            sevenAOk && sevenAFailed == 0 && sevenAPassed >= 19,
            "7B conserva íntegro el gate de Fundación 7A.",
            ref passed, ref failed, lines);

        report = "=== BISTRO BUILDER — 7B / DEMANDA JUGABLE ===\n" +
                 string.Join("\n", lines) +
                 "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static bool TryEvaluateProjection(
        BistroBuilderMarketingSnapshot snapshot,
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        int dayIndex,
        BistroBuilderMarketingDayPart dayPart,
        out BistroBuilderMarketingDemandProjection projection)
    {
        projection = null;
        if (!BistroBuilderMarketingEngine.TryEvaluate(
                snapshot,
                seed,
                new BistroBuilderMarketingEffectQuery
                {
                    dayIndex = dayIndex,
                    segment = BistroBuilderMarketingCustomerSegment.Any,
                    dayPart = dayPart
                },
                out BistroBuilderMarketingEffectSnapshot global,
                out _))
            return false;

        var effects = new Dictionary<
            BistroBuilderMarketingCustomerSegment,
            BistroBuilderMarketingEffectSnapshot>();
        IReadOnlyList<BistroBuilderMarketingCustomerSegment> segments =
            BistroBuilderMarketingDemandEngine.Segments;
        for (int index = 0; index < segments.Count; index++)
        {
            BistroBuilderMarketingCustomerSegment segment = segments[index];
            if (!BistroBuilderMarketingEngine.TryEvaluate(
                    snapshot,
                    seed,
                    new BistroBuilderMarketingEffectQuery
                    {
                        dayIndex = dayIndex,
                        segment = segment,
                        dayPart = dayPart
                    },
                    out BistroBuilderMarketingEffectSnapshot segmentEffect,
                    out _))
                return false;
            effects.Add(segment, segmentEffect);
        }

        return BistroBuilderMarketingDemandEngine.TryBuildProjection(
            3,
            global,
            effects,
            out projection,
            out _);
    }

    private static Dictionary<BistroBuilderMarketingCustomerSegment,
        BistroBuilderMarketingEffectSnapshot> CreateEmptySegmentEffects()
    {
        var result = new Dictionary<BistroBuilderMarketingCustomerSegment,
            BistroBuilderMarketingEffectSnapshot>();
        IReadOnlyList<BistroBuilderMarketingCustomerSegment> segments =
            BistroBuilderMarketingDemandEngine.Segments;
        for (int index = 0; index < segments.Count; index++)
            result.Add(segments[index], new BistroBuilderMarketingEffectSnapshot());
        return result;
    }

    private static bool TryActivate(
        BistroBuilderMarketingSnapshot source,
        BistroBuilderMarketingCampaignDefinition definition,
        string token,
        out BistroBuilderMarketingSnapshot candidate)
    {
        candidate = null;
        return definition != null &&
            BistroBuilderMarketingEngine.TryCreateCampaign(
                source,
                definition,
                3,
                string.Empty,
                "marketing_" + token,
                "marketing.expense." + token,
                out candidate,
                out _);
    }

    private static BistroBuilderMarketingCampaignDefinition Find(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        string id)
    {
        for (int index = 0; index < seed.Count; index++)
            if (BistroBuilderMarketingEngine.NormalizeId(seed[index].campaignId) ==
                BistroBuilderMarketingEngine.NormalizeId(id))
                return seed[index];
        return null;
    }

    private static bool CustomerSpawnerHasNoMarketingField()
    {
        FieldInfo[] fields = typeof(CustomerGroupSpawner).GetFields(
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic);
        for (int index = 0; index < fields.Length; index++)
            if (fields[index].FieldType.Name.Contains("Marketing"))
                return false;
        return true;
    }

    private static bool AcquisitionTagIsGeneric()
    {
        FieldInfo[] fields = typeof(BistroBuilderCustomerAcquisitionTag).GetFields(
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic);
        for (int index = 0; index < fields.Length; index++)
            if (fields[index].FieldType.Name.Contains("Marketing"))
                return false;
        return true;
    }

    private static void Check(
        bool condition,
        string text,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + text);
        }
        else
        {
            failed++;
            lines.Add("[FALLO] " + text);
        }
    }
}
