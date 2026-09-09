using System;

/// <summary>
/// Reglas puras de recomendación e intención de retorno. No modifica Reputación,
/// Marketing ni GuestRelations; únicamente deriva intención desde hechos guardados.
/// </summary>
public static class BistroBuilderAdvancedCustomerAdvocacyEngine
{
    public static BistroBuilderAdvancedCustomerIndividualAdvocacy EvaluateIndividual(
        BistroBuilderAdvancedCustomerIndividualExperience experience,
        BistroBuilderCustomerLoyaltyTier loyaltyTier)
    {
        if (experience == null) return null;
        int recommendation = Weighted(
            experience.overallSatisfactionBasisPoints, 50,
            experience.foodQualityScoreBasisPoints, 20,
            experience.serviceScoreBasisPoints, 15,
            experience.valueForMoneyScoreBasisPoints, 10,
            experience.ambienceScoreBasisPoints, 5);
        int returnIntent = Weighted(
            experience.overallSatisfactionBasisPoints, 45,
            experience.valueForMoneyScoreBasisPoints, 20,
            experience.serviceScoreBasisPoints, 15,
            experience.waitingScoreBasisPoints, 15,
            experience.foodQualityScoreBasisPoints, 5);
        returnIntent = Clamp(returnIntent + LoyaltyBonus(loyaltyTier));
        return new BistroBuilderAdvancedCustomerIndividualAdvocacy
        {
            customerId = experience.customerId,
            recommendationBasisPoints = recommendation,
            returnIntentBasisPoints = returnIntent
        };
    }

    public static BistroBuilderAdvancedCustomerAdvocacyResult EvaluateHistory(
        BistroBuilderAdvancedCustomerHistoryRecord history)
    {
        if (history == null || history.visitCount < 1)
            return null;

        int satisfaction = history.AverageSatisfactionBasisPoints;
        int waiting = AverageHistory(history, 0);
        int food = AverageHistory(history, 1);
        int value = AverageHistory(history, 2);
        int recommendation = Weighted(
            satisfaction, 55, food, 20, value, 15, waiting, 10, satisfaction, 0);
        int returnIntent = Clamp(Weighted(
            satisfaction, 55, value, 20, waiting, 15, food, 10, satisfaction, 0) +
            LoyaltyBonus(history.loyaltyTier));
        int spendBonus = (int)Math.Min(2500L, history.lifetimeSpendCents / 25L);
        int frequencyBonus = Math.Min(2500, history.visitCount * 300);

        return new BistroBuilderAdvancedCustomerAdvocacyResult
        {
            cohortId = history.cohortId,
            recommendationBasisPoints = recommendation,
            returnIntentBasisPoints = returnIntent,
            returnPriority = checked(returnIntent * 2 + recommendation +
                spendBonus + frequencyBonus)
        };
    }

    private static int AverageHistory(
        BistroBuilderAdvancedCustomerHistoryRecord history,
        int dimension)
    {
        if (history?.recentVisits == null || history.recentVisits.Count == 0)
            return history != null ? history.AverageSatisfactionBasisPoints : 5000;
        long sum = 0L; int count = 0;
        for (int i = 0; i < history.recentVisits.Count; i++)
        {
            var visit = history.recentVisits[i];
            if (visit == null) continue;
            switch (dimension)
            {
                case 0: sum += visit.waitingBasisPoints; break;
                case 1: sum += visit.foodQualityBasisPoints; break;
                case 2: sum += visit.valueBasisPoints; break;
                default: sum += visit.satisfactionBasisPoints; break;
            }
            count++;
        }
        return count == 0 ? history.AverageSatisfactionBasisPoints :
            Clamp((int)Math.Round(sum / (double)count,
                MidpointRounding.AwayFromZero));
    }

    private static int LoyaltyBonus(BistroBuilderCustomerLoyaltyTier tier)
    {
        switch (tier)
        {
            case BistroBuilderCustomerLoyaltyTier.Returning: return 300;
            case BistroBuilderCustomerLoyaltyTier.Regular: return 900;
            case BistroBuilderCustomerLoyaltyTier.Vip: return 1500;
            default: return 0;
        }
    }

    private static int Weighted(
        int a, int aw,
        int b, int bw,
        int c, int cw,
        int d, int dw,
        int e, int ew)
    {
        int weight = aw + bw + cw + dw + ew;
        if (weight <= 0) return 5000;
        long sum = (long)Clamp(a) * aw + (long)Clamp(b) * bw +
            (long)Clamp(c) * cw + (long)Clamp(d) * dw +
            (long)Clamp(e) * ew;
        return Clamp((int)Math.Round(
            sum / (double)weight, MidpointRounding.AwayFromZero));
    }

    private static int Clamp(int value) => Math.Max(0, Math.Min(10000, value));
}
