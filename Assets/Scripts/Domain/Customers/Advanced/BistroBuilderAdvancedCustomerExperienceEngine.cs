using System;

/// <summary>
/// Convierte una visita objetiva y perfiles 10A en percepciones individuales.
/// El resultado agregado mantiene la escala de Reputación existente.
/// </summary>
public static class BistroBuilderAdvancedCustomerExperienceEngine
{
    public static bool TryEvaluate(
        BistroBuilderReputationVisitRuntimeRecord visit,
        BistroBuilderCustomerExperienceRecord objectiveExperience,
        BistroBuilderAdvancedCustomerGroupProfile groupProfile,
        out BistroBuilderAdvancedCustomerExperienceResult result,
        out string error)
    {
        result = null;
        if (!BistroBuilderCustomerExperienceEvaluator.TryValidateRuntimeVisit(
                visit, out error) || objectiveExperience == null ||
            !BistroBuilderAdvancedCustomerProfileEngine.TryValidateGroupProfile(
                groupProfile, visit.partySize, out error) ||
            groupProfile.groupId != visit.groupId)
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "10B recibió una visita o un perfil incompatible.";
            return false;
        }

        result = new BistroBuilderAdvancedCustomerExperienceResult
        {
            groupId = visit.groupId
        };

        long serviceSum = 0L;
        long waitSum = 0L;
        long foodSum = 0L;
        long valueSum = 0L;
        long ambienceSum = 0L;
        long overallSum = 0L;

        for (int i = 0; i < groupProfile.members.Count; i++)
        {
            BistroBuilderAdvancedCustomerMemberProfile member = groupProfile.members[i];
            BistroBuilderAdvancedCustomerIndividualExperience individual =
                EvaluateMember(visit, objectiveExperience, member);
            result.individuals.Add(individual);
            serviceSum += individual.serviceScoreBasisPoints;
            waitSum += individual.waitingScoreBasisPoints;
            foodSum += individual.foodQualityScoreBasisPoints;
            valueSum += individual.valueForMoneyScoreBasisPoints;
            ambienceSum += individual.ambienceScoreBasisPoints;
            overallSum += individual.overallSatisfactionBasisPoints;
        }

        int count = result.individuals.Count;
        result.aggregateServiceBasisPoints = Average(serviceSum, count);
        result.aggregateWaitingBasisPoints = Average(waitSum, count);
        result.aggregateFoodQualityBasisPoints = Average(foodSum, count);
        result.aggregateValueBasisPoints = Average(valueSum, count);
        result.aggregateAmbienceBasisPoints = Average(ambienceSum, count);
        result.aggregateSatisfactionBasisPoints = Average(overallSum, count);
        error = string.Empty;
        return true;
    }

    private static BistroBuilderAdvancedCustomerIndividualExperience EvaluateMember(
        BistroBuilderReputationVisitRuntimeRecord visit,
        BistroBuilderCustomerExperienceRecord objectiveExperience,
        BistroBuilderAdvancedCustomerMemberProfile member)
    {
        int waiterScore = ScorePersonalWait(
            visit.waiterWaitSeconds, member.waiterWaitToleranceSeconds);
        int billScore = ScorePersonalWait(
            visit.billWaitSeconds, member.billWaitToleranceSeconds);
        int service = ApplySensitivity(
            Average(waiterScore, billScore), member.serviceSensitivityBasisPoints);

        int tableScore = ScorePersonalWait(
            visit.tableWaitSeconds, member.tableWaitToleranceSeconds);
        float foodTolerance = Math.Max(4f, visit.expectedFoodSeconds) *
            member.foodWaitToleranceBasisPoints / 10000f;
        int foodWaitScore = ScorePersonalWait(visit.foodWaitSeconds, foodTolerance);
        int waiting = Average(tableScore, waiterScore, foodWaitScore, billScore);

        int food = ApplySensitivity(
            objectiveExperience.foodQualityScoreBasisPoints,
            member.qualitySensitivityBasisPoints);
        int value = ApplySensitivity(
            objectiveExperience.valueForMoneyScoreBasisPoints,
            member.priceSensitivityBasisPoints);
        int ambience = ApplySensitivity(
            objectiveExperience.ambienceScoreBasisPoints,
            member.ambienceSensitivityBasisPoints);

        int overall = Clamp((int)Math.Round(
            (food * 35d + service * 25d + waiting * 15d +
             value * 20d + ambience * 5d) / 100d,
            MidpointRounding.AwayFromZero));

        var individual = new BistroBuilderAdvancedCustomerIndividualExperience
        {
            customerId = member.customerId,
            memberIndex = member.memberIndex,
            serviceScoreBasisPoints = service,
            waitingScoreBasisPoints = waiting,
            foodQualityScoreBasisPoints = food,
            valueForMoneyScoreBasisPoints = value,
            ambienceScoreBasisPoints = ambience,
            overallSatisfactionBasisPoints = overall
        };
        AddReasons(individual, visit, member, tableScore, waiterScore,
            foodWaitScore, billScore, food, value, ambience, service);
        return individual;
    }

    private static int ScorePersonalWait(float seconds, float toleranceSeconds)
    {
        float good = Math.Max(0.1f, toleranceSeconds * 0.60f);
        float bad = Math.Max(good + 0.1f, toleranceSeconds * 1.50f);
        return BistroBuilderReputationEngine.ScoreWaitSeconds(seconds, good, bad);
    }

    private static int ApplySensitivity(int score, int sensitivityBasisPoints)
    {
        int safeScore = Clamp(score);
        int safeSensitivity = Math.Max(0, Math.Min(10000, sensitivityBasisPoints));
        double factor = safeSensitivity / 5000d;
        return Clamp((int)Math.Round(
            5000d + (safeScore - 5000d) * factor,
            MidpointRounding.AwayFromZero));
    }

    private static void AddReasons(
        BistroBuilderAdvancedCustomerIndividualExperience individual,
        BistroBuilderReputationVisitRuntimeRecord visit,
        BistroBuilderAdvancedCustomerMemberProfile member,
        int tableScore,
        int waiterScore,
        int foodWaitScore,
        int billScore,
        int food,
        int value,
        int ambience,
        int service)
    {
        if (tableScore < 4500)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.TableWait);
        if (waiterScore < 4500)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.WaiterWait);
        if (foodWaitScore < 4500)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.FoodWait);
        if (billScore < 4500)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.BillWait);
        if (value < 4500)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.PriceTooHigh);
        else if (value >= 8000)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.GoodValue);
        if (food < 4500)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.FoodQualityLow);
        else if (food >= 8000)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.FoodQualityHigh);
        if (service < 4500)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.ServicePoor);
        else if (service >= 8000)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.ServiceExcellent);
        if (ambience < 4500)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.AmbiencePoor);
        else if (ambience >= 8000)
            individual.reasons.Add(BistroBuilderCustomerExperienceReason.AmbienceExcellent);
    }

    private static int Average(params int[] values)
    {
        if (values == null || values.Length == 0) return 5000;
        long sum = 0L;
        for (int i = 0; i < values.Length; i++) sum += values[i];
        return Average(sum, values.Length);
    }

    private static int Average(long sum, int count) => count <= 0 ? 5000 :
        Clamp((int)Math.Round(sum / (double)count, MidpointRounding.AwayFromZero));
    private static int Clamp(int value) => Math.Max(0, Math.Min(10000, value));
}
