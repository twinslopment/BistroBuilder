using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Autotest puro de satisfacción individual 10B.</summary>
public static class BistroBuilderAdvancedCustomers10BSelfTest
{
    [MenuItem("Tools/Bistro Builder/Customers/10B - Autotest", false, 10012)]
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

    public static bool Run(out int passed, out int failed, out string report)
    {
        int ok = 0;
        int fail = 0;
        var lines = new List<string>
            { "=== BISTRO BUILDER — 10B / SATISFACCIÓN INDIVIDUAL ===" };
        void Check(bool condition, string text)
        {
            if (condition) { ok++; lines.Add("[OK] " + text); }
            else { fail++; lines.Add("[FAIL] " + text); }
        }

        var visit = new BistroBuilderReputationVisitRuntimeRecord
        {
            groupId = 77,
            partySize = 2,
            segmentId = "workers",
            tableWaitSeconds = 55f,
            waiterWaitSeconds = 32f,
            foodWaitSeconds = 80f,
            billWaitSeconds = 28f,
            expectedFoodSeconds = 45f,
            paidAmountCents = 1400,
            referenceAmountCents = 1000,
            foodQualityPotentialBasisPoints = 8200,
            ambienceScoreBasisPoints = 7200
        };
        Check(BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
                visit, 3, out var objective, out _),
            "La visita objetiva sigue siendo evaluable por Reputación existente.");

        BistroBuilderAdvancedCustomerGroupProfile profile = BuildProfile();
        Check(BistroBuilderAdvancedCustomerExperienceEngine.TryEvaluate(
                visit, objective, profile, out var result, out _),
            "10B convierte la misma visita en percepciones individuales.");

        Check(result != null && result.individuals.Count == 2,
            "Existe una satisfacción separada para cada comensal.");
        Check(result.individuals[0].waitingScoreBasisPoints <
              result.individuals[1].waitingScoreBasisPoints,
            "Dos niveles de paciencia producen reacción distinta a la misma espera.");
        Check(result.individuals[0].reasons.Contains(
                  BistroBuilderCustomerExperienceReason.TableWait) &&
              !result.individuals[1].reasons.Contains(
                  BistroBuilderCustomerExperienceReason.TableWait),
            "Los motivos explican quién percibe negativamente una espera.");
        Check(result.individuals[0].valueForMoneyScoreBasisPoints <
              result.individuals[1].valueForMoneyScoreBasisPoints,
            "La sensibilidad al precio amplifica una mala relación calidad/precio.");
        Check(result.individuals[0].reasons.Contains(
                BistroBuilderCustomerExperienceReason.PriceTooHigh),
            "El precio alto queda registrado como motivo explícito de insatisfacción.");
        Check(result.individuals[1].foodQualityScoreBasisPoints >=
              objective.foodQualityScoreBasisPoints,
            "Una alta sensibilidad a calidad amplifica una experiencia culinaria positiva.");

        int expectedAggregate = (int)Math.Round(
            (result.individuals[0].overallSatisfactionBasisPoints +
             result.individuals[1].overallSatisfactionBasisPoints) / 2d,
            MidpointRounding.AwayFromZero);
        Check(result.aggregateSatisfactionBasisPoints == expectedAggregate,
            "La satisfacción que llega a Reputación es el agregado real de individuos.");

        var malformed = profile.DeepClone();
        malformed.members.RemoveAt(1);
        Check(!BistroBuilderAdvancedCustomerExperienceEngine.TryEvaluate(
                visit, objective, malformed, out _, out _),
            "10B rechaza perfiles cuya cardinalidad no coincide con el grupo.");

        var cheapVisit = visit.DeepClone();
        cheapVisit.paidAmountCents = 700;
        BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
            cheapVisit, 3, out var cheapObjective, out _);
        BistroBuilderAdvancedCustomerExperienceEngine.TryEvaluate(
            cheapVisit, cheapObjective, profile, out var cheapResult, out _);
        Check(cheapResult.individuals[0].valueForMoneyScoreBasisPoints >
              result.individuals[0].valueForMoneyScoreBasisPoints,
            "El mismo cliente reacciona mejor cuando el precio mejora objetivamente.");

        passed = ok;
        failed = fail;
        report = string.Join("\n", lines) + "\nResultado: " + passed +
            " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static BistroBuilderAdvancedCustomerGroupProfile BuildProfile()
    {
        var profile = new BistroBuilderAdvancedCustomerGroupProfile
        {
            groupId = 77,
            dayIndex = 3,
            segmentId = "workers"
        };
        profile.members.Add(Member(
            "visit.day0003.group000077.member01", 1,
            35f, 22f, 12000, 20f,
            8500, 5000, 10000, 4000));
        profile.members.Add(Member(
            "visit.day0003.group000077.member02", 2,
            100f, 65f, 22000, 60f,
            4000, 9000, 2500, 7000));
        return profile;
    }

    private static BistroBuilderAdvancedCustomerMemberProfile Member(
        string id, int index,
        float tableWait, float waiterWait, int foodWaitBp, float billWait,
        int serviceSensitivity, int qualitySensitivity,
        int priceSensitivity, int ambienceSensitivity)
    {
        return new BistroBuilderAdvancedCustomerMemberProfile
        {
            customerId = id,
            memberIndex = index,
            archetypeId = index == 1 ? "worker_quick" : "foodie_quality",
            tableWaitToleranceSeconds = tableWait,
            waiterWaitToleranceSeconds = waiterWait,
            foodWaitToleranceBasisPoints = foodWaitBp,
            billWaitToleranceSeconds = billWait,
            serviceSensitivityBasisPoints = serviceSensitivity,
            qualitySensitivityBasisPoints = qualitySensitivity,
            priceSensitivityBasisPoints = priceSensitivity,
            ambienceSensitivityBasisPoints = ambienceSensitivity
        };
    }
}
