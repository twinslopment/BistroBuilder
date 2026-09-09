using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest acumulativo 8A-8G de reputación, experiencia, reseñas,
/// descubrimiento, habituales y persistencia runtime.
/// </summary>
public static class BistroBuilderReputationBlock8SelfTest
{
    [MenuItem("Tools/Bistro Builder/Reputation/8G - Autotest completo", false, 8191)]
    private static void RunFromMenu()
    {
        if (!Run(out _, out _, out string report)) Debug.LogError(report);
        else Debug.Log(report);
    }

    public static void RunFromCommandLine()
    {
        if (!Run(out _, out _, out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0; failed = 0;
        var lines = new List<string>();

        bool foundation = BistroBuilderReputation8ASelfTest.Run(
            out int foundationPassed, out int foundationFailed, out _);
        Check(foundation && foundationPassed >= 11 && foundationFailed == 0,
            "La fundación 8A permanece verde.", ref passed, ref failed, lines);

        var goodVisit = Visit(1, 800, 1000, 5f, 5f, 14f, 4f, 10f, 8200);
        Check(BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
                goodVisit, 2, out BistroBuilderCustomerExperienceRecord good,
                out _) && good != null &&
              good.valueForMoneyScoreBasisPoints == 9000 &&
              good.overallSatisfactionBasisPoints > 7000,
            "Una visita rápida y con precio favorable produce alta satisfacción.",
            ref passed, ref failed, lines);

        var expensiveVisit = Visit(2, 1600, 1000, 80f, 55f, 100f, 40f, 10f, 6500);
        Check(BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
                expensiveVisit, 2, out BistroBuilderCustomerExperienceRecord bad,
                out _) && bad.valueForMoneyScoreBasisPoints <= 2500 &&
              bad.waitingScoreBasisPoints < 4000 &&
              bad.overallSatisfactionBasisPoints < good.overallSatisfactionBasisPoints,
            "Esperas largas y sobreprecio deterioran valor y satisfacción.",
            ref passed, ref failed, lines);

        Check(BistroBuilderCustomerExperienceEvaluator.ComputeValueForMoney(800, 1000) == 9000 &&
              BistroBuilderCustomerExperienceEvaluator.ComputeValueForMoney(1000, 1000) == 7500 &&
              BistroBuilderCustomerExperienceEvaluator.ComputeValueForMoney(1600, 1000) == 2500,
            "La relación calidad/precio usa el cobro real frente al precio de referencia.",
            ref passed, ref failed, lines);

        BistroBuilderReputationSnapshot initial =
            BistroBuilderReputationEngine.CreateInitialSnapshot();
        good.discoverySource = BistroBuilderRestaurantDiscoverySource.Marketing;
        Check(BistroBuilderReputationEngine.TryApplyExperience(
                initial, good, out BistroBuilderReputationSnapshot afterGood,
                out bool changed, out _) && changed &&
              afterGood.totalExperiences == 1 && afterGood.positiveExperiences == 1 &&
              afterGood.marketingDiscoveries == 1 && afterGood.reviews.Count == 1 &&
              afterGood.reviews[0].stars >= 4 &&
              !string.IsNullOrWhiteSpace(afterGood.reviews[0].summaryKey),
            "Una experiencia real genera reputación, descubrimiento y reseña.",
            ref passed, ref failed, lines);

        Check(BistroBuilderReputationEngine.TryApplyExperience(
                afterGood, good, out BistroBuilderReputationSnapshot replay,
                out bool replayChanged, out _) && !replayChanged &&
              replay.totalExperiences == 1 && replay.reviews.Count == 1,
            "La misma visita no duplica reputación ni reseñas.",
            ref passed, ref failed, lines);

        bad.experienceId = "visit.day2.group2";
        bad.discoverySource = BistroBuilderRestaurantDiscoverySource.WordOfMouth;
        Check(BistroBuilderReputationEngine.TryApplyExperience(
                afterGood, bad, out BistroBuilderReputationSnapshot mixed,
                out bool badChanged, out _) && badChanged &&
              mixed.totalExperiences == 2 && mixed.wordOfMouthDiscoveries == 1 &&
              mixed.reviews.Count == 2 && mixed.reviews[1].stars <= mixed.reviews[0].stars,
            "Una mala experiencia deja una reseña peor y conserva su canal de descubrimiento.",
            ref passed, ref failed, lines);

        Check(BistroBuilderReputationEngine.ComputeOrganicRepeatVisitBasisPoints(afterGood) > 0 &&
              BistroBuilderReputationEngine.ComputeOrganicRepeatVisitBasisPoints(initial) == 0,
            "La satisfacción alta crea retorno orgánico sin inventar clientes.",
            ref passed, ref failed, lines);

        Check(BistroBuilderReputationEngine.ComputeWordOfMouthBasisPoints(9000) > 0 &&
              BistroBuilderReputationEngine.ComputeWordOfMouthBasisPoints(1000) < 0,
            "El boca a boca reacciona en ambos sentidos a experiencias buenas y malas.",
            ref passed, ref failed, lines);

        var runtime = new BistroBuilderReputationRuntimeSnapshot();
        runtime.visits.Add(goodVisit.DeepClone());
        Check(BistroBuilderCustomerExperienceTrackingService.TryValidateRuntimeSnapshot(
                runtime, out _) && runtime.DeepClone().visits.Count == 1,
            "reputation.runtime valida y clona visitas activas.",
            ref passed, ref failed, lines);

        var duplicateRuntime = runtime.DeepClone();
        duplicateRuntime.visits.Add(goodVisit.DeepClone());
        Check(!BistroBuilderCustomerExperienceTrackingService.TryValidateRuntimeSnapshot(
                duplicateRuntime, out _),
            "reputation.runtime rechaza GroupId duplicados.",
            ref passed, ref failed, lines);

        var returning = BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        returning.sourceSystemId = "reputation.returning";
        returning.sourceReferenceId = "plan.test";
        returning.discoverySourceId = "returning_guest";
        returning.returningVisit = true;
        returning.guestRelationsReferenceId = "guest_cohort_000001";
        returning.preferredGroupSize = 2;
        Check(returning.TryValidate(out _) && returning.DeepClone().returningVisit &&
              returning.DeepClone().discoverySourceId == "returning_guest",
            "El contrato de captación conserva habitual, cohorte, tamaño y descubrimiento.",
            ref passed, ref failed, lines);

        var invalidDiscovery = BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        invalidDiscovery.discoverySourceId = "bad source!";
        Check(!invalidDiscovery.TryValidate(out _),
            "Los canales de descubrimiento mantienen identidad estable.",
            ref passed, ref failed, lines);

        BistroBuilderReputationSnapshot discovery = initial.DeepClone();
        BistroBuilderRestaurantDiscoverySource[] sources =
        {
            BistroBuilderRestaurantDiscoverySource.Organic,
            BistroBuilderRestaurantDiscoverySource.Marketing,
            BistroBuilderRestaurantDiscoverySource.WordOfMouth,
            BistroBuilderRestaurantDiscoverySource.ReturningGuest,
            BistroBuilderRestaurantDiscoverySource.Reservation
        };
        for (int i = 0; i < sources.Length; i++)
        {
            BistroBuilderCustomerExperienceRecord item = Experience(
                "discovery." + i, 7000, 7000, 7000, 7000, 7000, 7000);
            item.discoverySource = sources[i];
            BistroBuilderReputationEngine.TryApplyExperience(
                discovery, item, out discovery, out _, out _);
        }
        Check(discovery.organicDiscoveries == 1 && discovery.marketingDiscoveries == 1 &&
              discovery.wordOfMouthDiscoveries == 1 &&
              discovery.returningGuestDiscoveries == 1 &&
              discovery.reservationDiscoveries == 1,
            "Los cinco canales de descubrimiento se contabilizan por separado.",
            ref passed, ref failed, lines);

        BistroBuilderReputationSnapshot clone = discovery.DeepClone();
        clone.reviews[0].summaryKey = "mutated";
        Check(discovery.reviews[0].summaryKey != clone.reviews[0].summaryKey,
            "Reseñas y experiencias se clonan en profundidad.",
            ref passed, ref failed, lines);

        Check(BistroBuilderReputationRuntimeSaveSectionProvider.StableSectionId ==
                  "reputation.runtime" &&
              BistroBuilderReputationSaveSectionProvider.StableSectionId ==
                  "reputation.state",
            "Persistencia separa estado histórico y visitas en curso.",
            ref passed, ref failed, lines);

        report = "=== BISTRO BUILDER — REPUTACIÓN BLOQUE 8 AUTOTEST ===\n" +
                 string.Join("\n", lines) + "\nResultado: " + passed +
                 " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static BistroBuilderReputationVisitRuntimeRecord Visit(
        int groupId, long paid, long reference, float table, float waiter,
        float food, float bill, float expected, int quality)
    {
        return new BistroBuilderReputationVisitRuntimeRecord
        {
            groupId = groupId,
            segmentId = "general",
            partySize = 2,
            discoverySource = BistroBuilderRestaurantDiscoverySource.Organic,
            tableWaitSeconds = table,
            waiterWaitSeconds = waiter,
            foodWaitSeconds = food,
            billWaitSeconds = bill,
            paidAmountCents = paid,
            referenceAmountCents = reference,
            expectedFoodSeconds = expected,
            foodQualityPotentialBasisPoints = quality,
            ambienceScoreBasisPoints = 6000,
            orderCompleted = true,
            financeCaptured = true
        };
    }

    private static BistroBuilderCustomerExperienceRecord Experience(
        string id, int service, int waiting, int food, int value,
        int ambience, int overall)
    {
        return new BistroBuilderCustomerExperienceRecord
        {
            experienceId = id,
            dayIndex = 1,
            segmentId = "general",
            partySize = 2,
            discoverySource = BistroBuilderRestaurantDiscoverySource.Organic,
            tableWaitSeconds = 10f,
            waiterWaitSeconds = 10f,
            foodWaitSeconds = 20f,
            billWaitSeconds = 5f,
            serviceScoreBasisPoints = service,
            waitingScoreBasisPoints = waiting,
            foodQualityScoreBasisPoints = food,
            valueForMoneyScoreBasisPoints = value,
            ambienceScoreBasisPoints = ambience,
            overallSatisfactionBasisPoints = overall
        };
    }

    private static void Check(bool condition, string message,
        ref int passed, ref int failed, List<string> lines)
    {
        if (condition) { passed++; lines.Add("[OK] " + message); }
        else { failed++; lines.Add("[FAIL] " + message); }
    }
}
