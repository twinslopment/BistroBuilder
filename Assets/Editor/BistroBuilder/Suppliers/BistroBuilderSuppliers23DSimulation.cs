#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal sealed class BistroBuilderSuppliers23DSimulationResult
{
    public BistroBuilderSupplierMarketSnapshot market;
    public BistroBuilderSupplierCommercialIntelligenceSnapshot commercial;
    public int reviews;
    public int campaigns;
    public int promotionsStarted;
    public int promotionsExpired;
    public int maximumSimultaneousPromotions;
    public int minimumDiscountBasisPoints = int.MaxValue;
    public int maximumDiscountBasisPoints;
    public int minimumDurationDays = int.MaxValue;
    public int maximumDurationDays;
    public Dictionary<string, int> promotionsBySupplier =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public Dictionary<string, int> promotionsByReason =
        new Dictionary<string, int>(StringComparer.Ordinal);
}

internal static class BistroBuilderSuppliers23DSimulation
{
    public static bool TryRun(
        BistroBuilderSupplierAuthoringDatabase suppliers,
        BistroBuilderIngredientAuthoringDatabase ingredients,
        BistroBuilderSupplierMarketSettings marketSettings,
        BistroBuilderSupplierCommercialIntelligenceSettings commercialSettings,
        string seedText,
        int finalDay,
        out BistroBuilderSuppliers23DSimulationResult result,
        out string error)
    {
        result = null;
        error = null;
        if (suppliers == null || ingredients == null || marketSettings == null || commercialSettings == null)
        {
            error = "Faltan datos de autoría o settings 2.3C/2.3D.";
            return false;
        }

        ulong marketSeed = BistroBuilderSupplierMarketEngine.StableSeedFromText(
            seedText ?? "23d-simulation",
            marketSettings.DeterministicSalt);
        BistroBuilderSupplierMarketSnapshot market =
            BistroBuilderSupplierMarketEngine.CreateInitialSnapshot(
                suppliers,
                marketSettings,
                marketSeed,
                1);
        BistroBuilderSupplierCommercialIntelligenceSnapshot commercial =
            BistroBuilderSupplierCommercialIntelligenceEngine.CreateInitialSnapshot(
                market,
                commercialSettings,
                null);

        BistroBuilderSuppliers23DSimulationResult stats =
            new BistroBuilderSuppliers23DSimulationResult();

        int safeFinalDay = Math.Max(1, finalDay);
        for (int day = 1; day <= safeFinalDay; day++)
        {
            List<BistroBuilderSupplierPromotionRecord> expiredDaily;
            if (!BistroBuilderSupplierCommercialIntelligenceEngine.TryAdvanceToGameDay(
                    commercial,
                    commercialSettings,
                    day,
                    out expiredDaily,
                    out error))
            {
                return false;
            }
            stats.promotionsExpired += expiredDaily.Count;

            List<BistroBuilderSupplierMarketReviewOutcome> marketOutcomes;
            if (!BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
                    market,
                    suppliers,
                    marketSettings,
                    day,
                    out marketOutcomes,
                    out error))
            {
                return false;
            }

            for (int outcomeIndex = 0; outcomeIndex < marketOutcomes.Count; outcomeIndex++)
            {
                BistroBuilderSupplierMarketReviewOutcome marketOutcome = marketOutcomes[outcomeIndex];
                BistroBuilderSupplierCommercialReviewOutcome commercialOutcome;
                List<BistroBuilderSupplierPromotionRecord> started;
                List<BistroBuilderSupplierPromotionRecord> expiredAtReview;
                if (!BistroBuilderSupplierCommercialIntelligenceEngine.TryProcessMarketReview(
                        commercial,
                        market,
                        suppliers,
                        ingredients,
                        commercialSettings,
                        marketOutcome.reviewDay,
                        out commercialOutcome,
                        out started,
                        out expiredAtReview,
                        out error))
                {
                    return false;
                }

                if (commercialOutcome.processed)
                {
                    stats.reviews++;
                    stats.campaigns += commercialOutcome.campaignsStarted;
                    stats.promotionsStarted += commercialOutcome.promotionsStarted;
                }
                stats.promotionsExpired += expiredAtReview.Count;

                for (int promotionIndex = 0; promotionIndex < started.Count; promotionIndex++)
                {
                    BistroBuilderSupplierPromotionRecord promotion = started[promotionIndex];
                    if (promotion == null)
                    {
                        continue;
                    }
                    stats.minimumDiscountBasisPoints = Math.Min(
                        stats.minimumDiscountBasisPoints,
                        promotion.discountBasisPoints);
                    stats.maximumDiscountBasisPoints = Math.Max(
                        stats.maximumDiscountBasisPoints,
                        promotion.discountBasisPoints);
                    stats.minimumDurationDays = Math.Min(
                        stats.minimumDurationDays,
                        promotion.DurationDays);
                    stats.maximumDurationDays = Math.Max(
                        stats.maximumDurationDays,
                        promotion.DurationDays);
                    Increment(stats.promotionsBySupplier, promotion.supplierId);
                    Increment(stats.promotionsByReason, promotion.reasonCode);
                }
            }

            stats.maximumSimultaneousPromotions = Math.Max(
                stats.maximumSimultaneousPromotions,
                commercial.activePromotions.Count);
        }

        if (stats.minimumDiscountBasisPoints == int.MaxValue)
        {
            stats.minimumDiscountBasisPoints = 0;
        }
        if (stats.minimumDurationDays == int.MaxValue)
        {
            stats.minimumDurationDays = 0;
        }

        stats.market = market;
        stats.commercial = commercial;
        result = stats;
        return true;
    }

    private static void Increment(Dictionary<string, int> dictionary, string key)
    {
        string safe = string.IsNullOrWhiteSpace(key) ? "<sin-id>" : key;
        int value;
        dictionary.TryGetValue(safe, out value);
        dictionary[safe] = value + 1;
    }
}
#endif
