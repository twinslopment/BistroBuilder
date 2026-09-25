using System;

/// <summary>
/// Convierte métricas verificables de una visita en satisfacción percibida.
/// No conoce escena ni servicios Unity.
/// </summary>
public static class BistroBuilderCustomerExperienceEvaluator
{
    public static bool TryEvaluate(
        BistroBuilderReputationVisitRuntimeRecord visit,
        int dayIndex,
        out BistroBuilderCustomerExperienceRecord experience,
        out string error)
    {
        experience = null;
        if (!TryValidateRuntimeVisit(visit, out error) || dayIndex < 1)
            return false;

        int waiterScore = ApplyWaitRecoveryMitigations(
            BistroBuilderReputationEngine.ScoreWaitSeconds(
                visit.waiterWaitSeconds, 8f, 60f),
            visit.waiterDelayExplanationMitigationBasisPoints,
            visit.waiterIncidentApologyMitigationBasisPoints);
        int billScore = ApplyWaitRecoveryMitigations(
            BistroBuilderReputationEngine.ScoreWaitSeconds(
                visit.billWaitSeconds, 8f, 45f),
            visit.billDelayExplanationMitigationBasisPoints,
            visit.billIncidentApologyMitigationBasisPoints);
        int service = Average(waiterScore, billScore);

        float expected = Math.Max(4f, visit.expectedFoodSeconds);
        int foodWaitScore = ApplyWaitRecoveryMitigations(
            BistroBuilderReputationEngine.ScoreWaitSeconds(
                visit.foodWaitSeconds, expected * 1.35f + 4f,
                expected * 3f + 30f),
            visit.foodDelayExplanationMitigationBasisPoints,
            visit.foodIncidentApologyMitigationBasisPoints);
        int waiting = Average(
            BistroBuilderReputationEngine.ScoreWaitSeconds(
                visit.tableWaitSeconds, 20f, 120f),
            waiterScore,
            foodWaitScore,
            billScore);

        int food = ComputeFoodQuality(visit, expected);
        int value = ComputeValueForMoney(
            visit.paidAmountCents,
            visit.referenceAmountCents);
        int ambience = ClampScore(visit.ambienceScoreBasisPoints);
        int overall = ApplyServiceIncidentImpact(
            (int)Math.Round(
                (food * 35d + service * 25d + waiting * 15d +
                 value * 20d + ambience * 5d) / 100d,
                MidpointRounding.AwayFromZero),
            visit.serviceIncidentPenaltyBasisPoints,
            visit.serviceIncidentApologyRecoveryBasisPoints);

        experience = new BistroBuilderCustomerExperienceRecord
        {
            experienceId = "visit.day" + dayIndex + ".group" + visit.groupId,
            dayIndex = dayIndex,
            segmentId = BistroBuilderReputationEngine.NormalizeId(visit.segmentId),
            partySize = visit.partySize,
            discoverySource = visit.discoverySource,
            tableWaitSeconds = visit.tableWaitSeconds,
            waiterWaitSeconds = visit.waiterWaitSeconds,
            foodWaitSeconds = visit.foodWaitSeconds,
            billWaitSeconds = visit.billWaitSeconds,
            serviceScoreBasisPoints = service,
            waitingScoreBasisPoints = waiting,
            foodQualityScoreBasisPoints = food,
            valueForMoneyScoreBasisPoints = value,
            ambienceScoreBasisPoints = ambience,
            overallSatisfactionBasisPoints = ClampScore(overall)
        };
        error = string.Empty;
        return true;
    }

    public static bool TryValidateRuntimeVisit(
        BistroBuilderReputationVisitRuntimeRecord visit,
        out string error)
    {
        if (visit == null || visit.groupId < 1 || visit.partySize < 1 ||
            visit.partySize > 32 ||
            !BistroBuilderReputationEngine.IsSafeId(
                BistroBuilderReputationEngine.NormalizeId(visit.segmentId)) ||
            !Enum.IsDefined(typeof(BistroBuilderRestaurantDiscoverySource),
                visit.discoverySource) ||
            !Finite(visit.tableWaitSeconds) || !Finite(visit.waiterWaitSeconds) ||
            !Finite(visit.foodWaitSeconds) || !Finite(visit.billWaitSeconds) ||
            !Finite(visit.waiterCareCreditSeconds) ||
            !Finite(visit.foodCareCreditSeconds) ||
            !Finite(visit.billCareCreditSeconds) ||
            visit.waiterDelayExplanationMitigationBasisPoints < 0 ||
            visit.waiterDelayExplanationMitigationBasisPoints > 10000 ||
            visit.waiterIncidentApologyMitigationBasisPoints < 0 ||
            visit.waiterIncidentApologyMitigationBasisPoints > 10000 ||
            visit.foodDelayExplanationMitigationBasisPoints < 0 ||
            visit.foodDelayExplanationMitigationBasisPoints > 10000 ||
            visit.foodIncidentApologyMitigationBasisPoints < 0 ||
            visit.foodIncidentApologyMitigationBasisPoints > 10000 ||
            visit.billDelayExplanationMitigationBasisPoints < 0 ||
            visit.billDelayExplanationMitigationBasisPoints > 10000 ||
            visit.billIncidentApologyMitigationBasisPoints < 0 ||
            visit.billIncidentApologyMitigationBasisPoints > 10000 ||
            visit.recoverableServiceIncidentCount < 0 ||
            visit.apologizedServiceIncidentCount < 0 ||
            visit.apologizedServiceIncidentCount >
                visit.recoverableServiceIncidentCount ||
            visit.serviceIncidentPenaltyBasisPoints < 0 ||
            visit.serviceIncidentPenaltyBasisPoints > 10000 ||
            visit.serviceIncidentApologyRecoveryBasisPoints < 0 ||
            visit.serviceIncidentApologyRecoveryBasisPoints >
                visit.serviceIncidentPenaltyBasisPoints ||
            !Finite(visit.expectedFoodSeconds) || visit.paidAmountCents < 0L ||
            visit.referenceAmountCents < 0L ||
            visit.foodQualityPotentialBasisPoints < 0 ||
            visit.foodQualityPotentialBasisPoints > 10000 ||
            visit.ambienceScoreBasisPoints < 0 ||
            visit.ambienceScoreBasisPoints > 10000)
        {
            error = "El runtime de experiencia contiene datos inválidos.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public static int ApplyBillDelayExplanationMitigation(
        int rawBillScoreBasisPoints,
        int mitigationBasisPoints)
    {
        return ApplyPenaltyMitigation(
            rawBillScoreBasisPoints,
            mitigationBasisPoints
        );
    }

    public static int ApplyBillRecoveryMitigations(
        int rawBillScoreBasisPoints,
        int explanationMitigationBasisPoints,
        int apologyMitigationBasisPoints)
    {
        return ApplyWaitRecoveryMitigations(
            rawBillScoreBasisPoints,
            explanationMitigationBasisPoints,
            apologyMitigationBasisPoints
        );
    }

    public static int ApplyWaitRecoveryMitigations(
        int rawScoreBasisPoints,
        int explanationMitigationBasisPoints,
        int apologyMitigationBasisPoints)
    {
        int afterExplanation = ApplyPenaltyMitigation(
            rawScoreBasisPoints,
            explanationMitigationBasisPoints
        );
        return ApplyPenaltyMitigation(
            afterExplanation,
            apologyMitigationBasisPoints
        );
    }

    public static int ApplyServiceIncidentImpact(
        int rawOverallBasisPoints,
        int incidentPenaltyBasisPoints,
        int apologyRecoveryBasisPoints)
    {
        int raw = ClampScore(rawOverallBasisPoints);
        int penalty = Math.Max(0, Math.Min(10000, incidentPenaltyBasisPoints));
        int recovery = Math.Max(
            0,
            Math.Min(penalty, apologyRecoveryBasisPoints)
        );
        return ClampScore(raw - penalty + recovery);
    }

    private static int ApplyPenaltyMitigation(
        int rawScoreBasisPoints,
        int mitigationBasisPoints)
    {
        int raw = ClampScore(rawScoreBasisPoints);
        int mitigation = Math.Max(0, Math.Min(10000, mitigationBasisPoints));
        if (mitigation == 0 || raw >= 10000)
            return raw;

        int recovered = (int)Math.Round(
            (10000 - raw) * (mitigation / 10000d),
            MidpointRounding.AwayFromZero);
        return ClampScore(raw + recovered);
    }

    private static int ComputeFoodQuality(
        BistroBuilderReputationVisitRuntimeRecord visit,
        float expected)
    {
        int quality = visit.foodQualityPotentialBasisPoints;
        if (visit.foodWaitSeconds > expected * 2f)
        {
            float excess = Math.Min(2f,
                (visit.foodWaitSeconds - expected * 2f) / expected);
            quality -= (int)Math.Round(excess * 1200f,
                MidpointRounding.AwayFromZero);
        }
        return ClampScore(quality);
    }

    public static int ComputeValueForMoney(long paid, long reference)
    {
        if (paid < 0L || reference < 0L) return 5000;
        if (reference == 0L) return paid == 0L ? 7500 : 3000;
        double ratio = paid / (double)reference;
        if (ratio <= 0.80d) return 9000;
        if (ratio <= 1.00d)
            return Lerp(9000, 7500, (ratio - 0.80d) / 0.20d);
        if (ratio <= 1.20d)
            return Lerp(7500, 5000, (ratio - 1.00d) / 0.20d);
        if (ratio <= 1.60d)
            return Lerp(5000, 2500, (ratio - 1.20d) / 0.40d);
        return 2000;
    }

    private static int Average(params int[] values)
    {
        long sum = 0L;
        for (int i = 0; i < values.Length; i++) sum += values[i];
        return values.Length == 0 ? 5000 :
            (int)Math.Round(sum / (double)values.Length,
                MidpointRounding.AwayFromZero);
    }

    private static int Lerp(int a, int b, double t) => ClampScore(
        (int)Math.Round(a + (b - a) * Math.Max(0d, Math.Min(1d, t)),
            MidpointRounding.AwayFromZero));
    private static int ClampScore(int value) => Math.Max(0, Math.Min(10000, value));
    private static bool Finite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}
