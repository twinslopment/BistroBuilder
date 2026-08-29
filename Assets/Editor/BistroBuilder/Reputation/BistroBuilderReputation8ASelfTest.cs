using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro 8A: contrato, agregación por aspectos e idempotencia.
/// </summary>
public static class BistroBuilderReputation8ASelfTest
{
    [MenuItem("Tools/Bistro Builder/Reputation/8A - Autotest", false, 8101)]
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
        BistroBuilderReputationSnapshot empty = BistroBuilderReputationEngine.CreateInitialSnapshot();
        Check(BistroBuilderReputationEngine.TryValidateSnapshot(empty, out _) &&
              empty.aspects.Count == 5 && empty.globalScoreBasisPoints == 5000,
            "Estado inicial neutral y cinco aspectos canónicos.", ref passed, ref failed, lines);

        Check(BistroBuilderReputationEngine.TryApplyExternalReputationPoints(
                empty, "marketing.reputation.test", 1,
                out BistroBuilderReputationSnapshot credited, out bool changed, out _) &&
              changed && credited.externalReputationPoints == 1 &&
              BistroBuilderReputationEngine.ComputePersistentDemandBasisPoints(credited) == 100,
            "+1 punto externo conserva compatibilidad con +1 % de demanda.", ref passed, ref failed, lines);

        Check(BistroBuilderReputationEngine.TryApplyExternalReputationPoints(
                credited, "marketing.reputation.test", 1,
                out BistroBuilderReputationSnapshot replay, out bool replayChanged, out _) &&
              !replayChanged && replay.externalReputationPoints == 1,
            "La misma fuente externa es idempotente.", ref passed, ref failed, lines);

        BistroBuilderCustomerExperienceRecord excellent = Experience(
            "experience.good", 9000, 9000, 9000, 8500, 8000, 9000);
        Check(BistroBuilderReputationEngine.TryApplyExperience(
                credited, excellent, out BistroBuilderReputationSnapshot good,
                out bool experienceChanged, out _) && experienceChanged &&
              good.totalExperiences == 1 && good.positiveExperiences == 1 &&
              good.globalScoreBasisPoints > 5000 && good.wordOfMouthBasisPoints > 0,
            "Una experiencia excelente mejora reputación y boca a boca.", ref passed, ref failed, lines);

        Check(BistroBuilderReputationEngine.TryApplyExperience(
                good, excellent, out BistroBuilderReputationSnapshot duplicate,
                out bool duplicateChanged, out _) && !duplicateChanged &&
              duplicate.totalExperiences == 1,
            "Una experiencia no puede contabilizarse dos veces.", ref passed, ref failed, lines);

        BistroBuilderCustomerExperienceRecord bad = Experience(
            "experience.bad", 1500, 1000, 1800, 2000, 4000, 1500);
        Check(BistroBuilderReputationEngine.TryApplyExperience(
                good, bad, out BistroBuilderReputationSnapshot mixed,
                out bool badChanged, out _) && badChanged &&
              mixed.totalExperiences == 2 && mixed.negativeExperiences == 1 &&
              BistroBuilderReputationEngine.GetAspectScore(
                  mixed, BistroBuilderReputationAspect.WaitingTime) <
              BistroBuilderReputationEngine.GetAspectScore(
                  good, BistroBuilderReputationAspect.WaitingTime),
            "Una mala experiencia deteriora el aspecto afectado.", ref passed, ref failed, lines);

        Check(BistroBuilderReputationEngine.ScoreWaitSeconds(5f, 15f, 120f) == 9000 &&
              BistroBuilderReputationEngine.ScoreWaitSeconds(150f, 15f, 120f) == 1500,
            "Los tiempos de espera tienen extremos deterministas.", ref passed, ref failed, lines);

        Check(BistroBuilderReputationEngine.GetSatisfactionBand(9000) ==
              BistroBuilderCustomerSatisfactionBand.Excellent &&
              BistroBuilderReputationEngine.GetSatisfactionBand(2000) ==
              BistroBuilderCustomerSatisfactionBand.VeryBad,
            "Las bandas de satisfacción cubren experiencias buenas y malas.", ref passed, ref failed, lines);

        BistroBuilderCustomerExperienceRecord invalid = Experience(
            "bad id!", 5000, 5000, 5000, 5000, 5000, 5000);
        Check(!BistroBuilderReputationEngine.TryApplyExperience(
                mixed, invalid, out _, out _, out _),
            "Se rechazan experiencias con identidad no estable.", ref passed, ref failed, lines);

        BistroBuilderReputationSnapshot clone = mixed.DeepClone();
        clone.aspects[0].scoreBasisPoints = 0;
        Check(mixed.aspects[0].scoreBasisPoints != clone.aspects[0].scoreBasisPoints,
            "El snapshot se clona en profundidad.", ref passed, ref failed, lines);

        BistroBuilderReputationSnapshot capped = mixed;
        for (int i = 0; i < 120; i++)
        {
            BistroBuilderReputationEngine.TryApplyExternalReputationPoints(
                capped, "source." + i, 1, out capped, out _, out _);
        }
        Check(capped.externalReputationPoints == 100 &&
              BistroBuilderReputationEngine.ComputePersistentDemandBasisPoints(capped) <= 5000,
            "Reputación externa y demanda quedan acotadas.", ref passed, ref failed, lines);

        report = "=== BISTRO BUILDER — REPUTACIÓN 8A AUTOTEST ===\n" +
                 string.Join("\n", lines) + "\nResultado: " + passed +
                 " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static BistroBuilderCustomerExperienceRecord Experience(
        string id, int service, int waiting, int food, int value, int ambience, int overall)
    {
        return new BistroBuilderCustomerExperienceRecord
        {
            experienceId = id, dayIndex = 1, segmentId = "general", partySize = 2,
            discoverySource = BistroBuilderRestaurantDiscoverySource.Organic,
            tableWaitSeconds = 10f, waiterWaitSeconds = 10f,
            foodWaitSeconds = 20f, billWaitSeconds = 5f,
            serviceScoreBasisPoints = service, waitingScoreBasisPoints = waiting,
            foodQualityScoreBasisPoints = food, valueForMoneyScoreBasisPoints = value,
            ambienceScoreBasisPoints = ambience, overallSatisfactionBasisPoints = overall
        };
    }

    private static void Check(bool condition, string message,
        ref int passed, ref int failed, List<string> lines)
    {
        if (condition) { passed++; lines.Add("[OK] " + message); }
        else { failed++; lines.Add("[FAIL] " + message); }
    }
}
