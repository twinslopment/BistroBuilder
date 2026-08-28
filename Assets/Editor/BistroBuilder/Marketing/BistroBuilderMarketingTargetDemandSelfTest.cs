using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate puro del siguiente tramo funcional de Marketing: TargetDemand debe
/// modificar elecciones reales sin alterar el comportamiento histórico 2.1D.
/// </summary>
public static class BistroBuilderMarketingTargetDemandSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/TargetDemand - Autotest",
        false,
        7220)]
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
        BistroBuilderMenuCommercialPolicy policy =
            ScriptableObject.CreateInstance<BistroBuilderMenuCommercialPolicy>();

        try
        {
            ConfigurePolicy(policy);
            List<BistroBuilderMenuOfferItemSnapshot> candidates =
                CreateCandidates();
            RunCompatibilityGate(
                policy,
                candidates,
                ref passed,
                ref failed,
                lines);
            RunWeightingGate(
                policy,
                candidates,
                ref passed,
                ref failed,
                lines);
            RunMarketingTargetGate(
                ref passed,
                ref failed,
                lines);
        }
        catch (Exception exception)
        {
            Check(false, "Excepción no controlada: " + exception.Message,
                ref passed, ref failed, lines);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(policy);
        }

        bool sevenCOk = BistroBuilderMarketing7CSelfTest.Run(
            out int sevenCPassed,
            out int sevenCFailed,
            out _);
        Check(
            sevenCOk && sevenCFailed == 0 && sevenCPassed >= 11,
            "El nuevo tramo conserva íntegro el gate 7C.",
            ref passed, ref failed, lines);

        report =
            "=== BISTRO BUILDER — MARKETING / TARGET DEMAND ===\n" +
            string.Join("\n", lines) +
            "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static void RunCompatibilityGate(
        BistroBuilderMenuCommercialPolicy policy,
        List<BistroBuilderMenuOfferItemSnapshot> candidates,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        BistroBuilderMenuSelectionContext context = CreateContext(17, 0);
        bool oldOk = BistroBuilderMenuSelectionEvaluator.TrySelect(
            candidates,
            policy,
            context,
            null,
            null,
            out BistroBuilderMenuSelectionResult oldResult,
            out _,
            out _);
        bool newOk = BistroBuilderMenuSelectionEvaluator.TrySelectWithExternalWeights(
            candidates,
            policy,
            context,
            null,
            null,
            null,
            out BistroBuilderMenuSelectionResult newResult,
            out _,
            out _);

        Check(
            oldOk && newOk &&
            oldResult.DishId == newResult.DishId &&
            oldResult.DeterministicSeed == newResult.DeterministicSeed &&
            oldResult.EffectiveWeightBasisPoints ==
                newResult.EffectiveWeightBasisPoints,
            "Sin pesos externos, 2.1D conserva selección, semilla y peso.",
            ref passed, ref failed, lines);
    }

    private static void RunWeightingGate(
        BistroBuilderMenuCommercialPolicy policy,
        List<BistroBuilderMenuOfferItemSnapshot> candidates,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        const int Samples = 4096;
        int baselineTarget = 0;
        int boostedTarget = 0;
        var adjustments = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "dish_target", 5000 }
        };

        for (int sample = 0; sample < Samples; sample++)
        {
            BistroBuilderMenuSelectionContext context =
                CreateContext(sample, sample % candidates.Count);
            var baselineRandom =
                new BistroBuilderMenuSelectionDeterministicRandom(
                    (ulong)sample + 1UL);
            var boostedRandom =
                new BistroBuilderMenuSelectionDeterministicRandom(
                    (ulong)sample + 1UL);
            bool baselineOk = BistroBuilderMenuSelectionEvaluator.TrySelect(
                candidates,
                policy,
                context,
                null,
                baselineRandom,
                out BistroBuilderMenuSelectionResult baseline,
                out _,
                out _);
            bool boostedOk = BistroBuilderMenuSelectionEvaluator.TrySelectWithExternalWeights(
                candidates,
                policy,
                context,
                null,
                boostedRandom,
                adjustments,
                out BistroBuilderMenuSelectionResult boosted,
                out _,
                out _);

            if (!baselineOk || !boostedOk)
            {
                Check(false, "La muestra ponderada no pudo resolverse.",
                    ref passed, ref failed, lines);
                return;
            }

            if (baseline.DishId == "dish_target") baselineTarget++;
            if (boosted.DishId == "dish_target") boostedTarget++;
        }

        Check(
            boostedTarget > baselineTarget + 200,
            "El peso +50 % aumenta visiblemente el plato objetivo: " +
            baselineTarget + "→" + boostedTarget + " / " + Samples + ".",
            ref passed, ref failed, lines);

        var invalid = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "dish_target", 50001 }
        };
        bool rejected = !BistroBuilderMenuSelectionEvaluator.TrySelectWithExternalWeights(
            candidates,
            policy,
            CreateContext(1, 0),
            null,
            null,
            invalid,
            out _,
            out BistroBuilderMenuSelectionFailureReason reason,
            out _);
        Check(
            rejected && reason ==
                BistroBuilderMenuSelectionFailureReason.InvalidPolicy,
            "2.1D rechaza ajustes externos fuera del rango seguro.",
            ref passed, ref failed, lines);
    }

    private static void RunMarketingTargetGate(
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        List<BistroBuilderMarketingCampaignDefinition> seed =
            BistroBuilderMarketing7ASeedFactory.CreateSeed();
        BistroBuilderMarketingSnapshot snapshot =
            BistroBuilderMarketingEngine.CreateEmptySnapshot();
        BistroBuilderMarketingCampaignDefinition dishDefinition =
            Find(seed, "marketing.menu.dish_week");

        bool activated = dishDefinition != null &&
            BistroBuilderMarketingEngine.TryCreateCampaign(
                snapshot,
                dishDefinition,
                3,
                "dish_target",
                "marketing_target_selftest_dish",
                "marketing.expense.target_selftest_dish",
                out snapshot,
                out _);
        Check(
            activated,
            "Marketing activa una campaña dirigida a DishId.",
            ref passed, ref failed, lines);

        if (!activated)
            return;
        bool targetOk = BistroBuilderMarketingEngine.TryEvaluate(
            snapshot,
            seed,
            new BistroBuilderMarketingEffectQuery
            {
                dayIndex = 3,
                segment = BistroBuilderMarketingCustomerSegment.Any,
                dayPart = BistroBuilderMarketingDayPart.Lunch,
                targetId = "dish_target"
            },
            out BistroBuilderMarketingEffectSnapshot targetEffects,
            out _);
        bool otherOk = BistroBuilderMarketingEngine.TryEvaluate(
            snapshot,
            seed,
            new BistroBuilderMarketingEffectQuery
            {
                dayIndex = 3,
                segment = BistroBuilderMarketingCustomerSegment.Any,
                dayPart = BistroBuilderMarketingDayPart.Lunch,
                targetId = "dish_other"
            },
            out BistroBuilderMarketingEffectSnapshot otherEffects,
            out _);

        Check(
            targetOk && otherOk &&
            targetEffects.targetDemandBasisPoints == 1800 &&
            otherEffects.targetDemandBasisPoints == 0,
            "TargetDemand se aplica solo al DishId contratado.",
            ref passed, ref failed, lines);

        BistroBuilderMarketingCampaignDefinition menuDefinition =
            Find(seed, "marketing.menu.star_menu");
        bool menuActivated = menuDefinition != null &&
            BistroBuilderMarketingEngine.TryCreateCampaign(
                snapshot,
                menuDefinition,
                3,
                "menu_default",
                "marketing_target_selftest_menu",
                "marketing.expense.target_selftest_menu",
                out snapshot,
                out _);
        BistroBuilderMarketingEffectSnapshot menuEffects = null;
        bool menuTargetOk = false;
        if (menuActivated)
        {
            menuTargetOk = BistroBuilderMarketingEngine.TryEvaluate(
                snapshot,
                seed,
                new BistroBuilderMarketingEffectQuery
                {
                    dayIndex = 3,
                    segment = BistroBuilderMarketingCustomerSegment.Any,
                    dayPart = BistroBuilderMarketingDayPart.Lunch,
                    targetId = "menu_default"
                },
                out menuEffects,
                out _);
        }

        Check(
            menuTargetOk && menuEffects != null &&
            menuEffects.targetDemandBasisPoints == 1800,
            "Una campaña de carta conserva TargetDemand por MenuId.",
            ref passed, ref failed, lines);
    }

    private static List<BistroBuilderMenuOfferItemSnapshot> CreateCandidates()
    {
        return new List<BistroBuilderMenuOfferItemSnapshot>
        {
            CreateCandidate("dish_target", 0, true),
            CreateCandidate("dish_other", 1, false)
        };
    }

    private static BistroBuilderMenuOfferItemSnapshot CreateCandidate(
        string dishId,
        int displayOrder,
        bool signature)
    {
        var availability = new BistroBuilderDishAvailabilitySnapshot(
            dishId,
            BistroBuilderDishAvailabilityState.Available,
            100L,
            string.Empty,
            0L,
            0L,
            1,
            string.Empty);

        return new BistroBuilderMenuOfferItemSnapshot(
            "restaurant_main",
            dishId,
            dishId,
            "category_main_course",
            BistroBuilderDishCourse.Main,
            BistroBuilderKitchenStationType.HotKitchen,
            1000,
            displayOrder,
            signature,
            BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderServiceMode.TableService,
            BistroBuilderDishServiceModeAvailability.All,
            availability,
            BistroBuilderMenuOfferBlockFlags.None,
            BistroBuilderMenuOfferRejectionReason.None,
            string.Empty,
            1);
    }

    private static BistroBuilderMenuSelectionContext CreateContext(
        int ordinal,
        int fallbackOffset)
    {
        return new BistroBuilderMenuSelectionContext(
            BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderServiceMode.TableService,
            "marketing_target_probe_" + ordinal,
            1,
            ordinal,
            fallbackOffset);
    }

    private static void ConfigurePolicy(
        BistroBuilderMenuCommercialPolicy policy)
    {
        SerializedObject serialized = new SerializedObject(policy);
        serialized.FindProperty("minimumPriceCents").intValue = 0;
        serialized.FindProperty("maximumPriceCents").intValue = 1000000;
        serialized.FindProperty("maximumMenuItems").intValue = 32;
        serialized.FindProperty("maximumSignatureDishes").intValue = 3;
        serialized.FindProperty("requireSignatureDishEnabled").boolValue = true;
        serialized.FindProperty("signatureSelectionWeightBasisPoints")
            .intValue = 15000;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static BistroBuilderMarketingCampaignDefinition Find(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        string campaignId)
    {
        string id = BistroBuilderMarketingEngine.NormalizeId(campaignId);
        for (int index = 0; index < seed.Count; index++)
            if (BistroBuilderMarketingEngine.NormalizeId(
                    seed[index].campaignId) == id)
                return seed[index];
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
