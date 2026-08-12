using System;
using System.Collections.Generic;

public enum BistroBuilderSupplierPromotionLifecycle
{
    Activa = 0,
    Finalizada = 1
}

[Serializable]
public sealed class BistroBuilderSupplierPromotionRecord
{
    public string promotionId;
    public string supplierOfferId;
    public string supplierId;
    public string ingredientId;
    public string packageFormatId;
    public int startGameDay;
    public int endGameDayExclusive;
    public int endedGameDay;
    public int discountBasisPoints;
    public long referenceMarketPriceCents;
    public long promotionalPriceCents;
    public BistroBuilderSupplierOfferAvailability sourceAvailabilityAtStart;
    public long sourceMarketRevision;
    public BistroBuilderSupplierPromotionLifecycle lifecycle;
    public string reasonCode;
    public string reasonText;

    public int DurationDays => Math.Max(0, endGameDayExclusive - startGameDay);

    public bool IsActiveOnDay(int gameDay)
    {
        return lifecycle == BistroBuilderSupplierPromotionLifecycle.Activa &&
               gameDay >= startGameDay &&
               gameDay < endGameDayExclusive;
    }

    public BistroBuilderSupplierPromotionRecord DeepClone()
    {
        return new BistroBuilderSupplierPromotionRecord
        {
            promotionId = promotionId,
            supplierOfferId = supplierOfferId,
            supplierId = supplierId,
            ingredientId = ingredientId,
            packageFormatId = packageFormatId,
            startGameDay = startGameDay,
            endGameDayExclusive = endGameDayExclusive,
            endedGameDay = endedGameDay,
            discountBasisPoints = discountBasisPoints,
            referenceMarketPriceCents = referenceMarketPriceCents,
            promotionalPriceCents = promotionalPriceCents,
            sourceAvailabilityAtStart = sourceAvailabilityAtStart,
            sourceMarketRevision = sourceMarketRevision,
            lifecycle = lifecycle,
            reasonCode = reasonCode,
            reasonText = reasonText
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierCommercialCampaignState
{
    public string supplierId;
    public int lastCampaignGameDay;
    public int campaignCount;

    public BistroBuilderSupplierCommercialCampaignState DeepClone()
    {
        return new BistroBuilderSupplierCommercialCampaignState
        {
            supplierId = supplierId,
            lastCampaignGameDay = lastCampaignGameDay,
            campaignCount = campaignCount
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierCommercialReviewRecord
{
    public long sequence;
    public int gameDay;
    public long sourceMarketRevision;
    public int suppliersEvaluated;
    public int eligibleOffers;
    public int campaignsStarted;
    public int promotionsStarted;
    public int promotionsExpired;
    public int suppliersSkippedByCooldown;
    public int suppliersWithoutEligibleOffers;

    public BistroBuilderSupplierCommercialReviewRecord DeepClone()
    {
        return new BistroBuilderSupplierCommercialReviewRecord
        {
            sequence = sequence,
            gameDay = gameDay,
            sourceMarketRevision = sourceMarketRevision,
            suppliersEvaluated = suppliersEvaluated,
            eligibleOffers = eligibleOffers,
            campaignsStarted = campaignsStarted,
            promotionsStarted = promotionsStarted,
            promotionsExpired = promotionsExpired,
            suppliersSkippedByCooldown = suppliersSkippedByCooldown,
            suppliersWithoutEligibleOffers = suppliersWithoutEligibleOffers
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierCommercialIntelligenceSnapshot
{
    public const string CurrentSchemaId = "supplier.commercial.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public ulong sourceMarketSeed;
    public ulong commercialSeed;
    public int currentGameDay = 1;
    public int lastProcessedMarketReviewDay;
    public long lastProcessedMarketRevision;
    public long commercialRevision = 1;
    public long nextSequence = 1;

    public List<BistroBuilderSupplierPromotionRecord> activePromotions =
        new List<BistroBuilderSupplierPromotionRecord>();
    public List<BistroBuilderSupplierPromotionRecord> promotionHistory =
        new List<BistroBuilderSupplierPromotionRecord>();
    public List<BistroBuilderSupplierCommercialCampaignState> campaignStates =
        new List<BistroBuilderSupplierCommercialCampaignState>();
    public List<BistroBuilderSupplierCommercialReviewRecord> reviews =
        new List<BistroBuilderSupplierCommercialReviewRecord>();

    public BistroBuilderSupplierCommercialIntelligenceSnapshot DeepClone()
    {
        BistroBuilderSupplierCommercialIntelligenceSnapshot clone =
            new BistroBuilderSupplierCommercialIntelligenceSnapshot
            {
                schemaId = schemaId,
                schemaVersion = schemaVersion,
                sourceMarketSeed = sourceMarketSeed,
                commercialSeed = commercialSeed,
                currentGameDay = currentGameDay,
                lastProcessedMarketReviewDay = lastProcessedMarketReviewDay,
                lastProcessedMarketRevision = lastProcessedMarketRevision,
                commercialRevision = commercialRevision,
                nextSequence = nextSequence
            };

        CopyPromotions(activePromotions, clone.activePromotions);
        CopyPromotions(promotionHistory, clone.promotionHistory);

        if (campaignStates != null)
        {
            for (int index = 0; index < campaignStates.Count; index++)
            {
                if (campaignStates[index] != null)
                {
                    clone.campaignStates.Add(campaignStates[index].DeepClone());
                }
            }
        }

        if (reviews != null)
        {
            for (int index = 0; index < reviews.Count; index++)
            {
                if (reviews[index] != null)
                {
                    clone.reviews.Add(reviews[index].DeepClone());
                }
            }
        }

        return clone;
    }

    private static void CopyPromotions(
        List<BistroBuilderSupplierPromotionRecord> source,
        List<BistroBuilderSupplierPromotionRecord> destination)
    {
        if (source == null)
        {
            return;
        }

        for (int index = 0; index < source.Count; index++)
        {
            if (source[index] != null)
            {
                destination.Add(source[index].DeepClone());
            }
        }
    }
}

public struct BistroBuilderSupplierCommercialReviewOutcome
{
    public bool processed;
    public int reviewDay;
    public int suppliersEvaluated;
    public int eligibleOffers;
    public int campaignsStarted;
    public int promotionsStarted;
    public int promotionsExpired;
}

public sealed class BistroBuilderSupplierCommercialQuote
{
    public string supplierOfferId;
    public string supplierId;
    public string ingredientId;
    public string packageFormatId;
    public long marketPriceCents;
    public long effectivePriceCents;
    public BistroBuilderSupplierOfferAvailability availability;
    public bool availableForNewOrders;
    public bool hasActivePromotion;
    public string promotionId;
    public int promotionEndGameDayExclusive;
    public int discountBasisPoints;
    public string reasonCode;
    public string reasonText;

    public BistroBuilderSupplierCommercialQuote DeepClone()
    {
        return new BistroBuilderSupplierCommercialQuote
        {
            supplierOfferId = supplierOfferId,
            supplierId = supplierId,
            ingredientId = ingredientId,
            packageFormatId = packageFormatId,
            marketPriceCents = marketPriceCents,
            effectivePriceCents = effectivePriceCents,
            availability = availability,
            availableForNewOrders = availableForNewOrders,
            hasActivePromotion = hasActivePromotion,
            promotionId = promotionId,
            promotionEndGameDayExclusive = promotionEndGameDayExclusive,
            discountBasisPoints = discountBasisPoints,
            reasonCode = reasonCode,
            reasonText = reasonText
        };
    }
}
