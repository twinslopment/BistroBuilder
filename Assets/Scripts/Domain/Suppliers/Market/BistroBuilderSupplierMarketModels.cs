using System;
using System.Collections.Generic;

public enum BistroBuilderSupplierMarketChangeKind
{
    None = 0,
    Price = 1,
    Availability = 2,
    PriceAndAvailability = 3
}

[Serializable]
public sealed class BistroBuilderSupplierMarketOfferState
{
    public string supplierOfferId;
    public string supplierId;
    public string ingredientId;
    public string packageFormatId;
    public long basePriceCents;
    public long currentPriceCents;
    public BistroBuilderSupplierOfferAvailability availability;
    public int lastReviewedGameDay;
    public int lastPriceChangeGameDay;
    public int lastAvailabilityChangeGameDay;
    public int reviewCount;

    public BistroBuilderSupplierMarketOfferState DeepClone()
    {
        return new BistroBuilderSupplierMarketOfferState
        {
            supplierOfferId = supplierOfferId,
            supplierId = supplierId,
            ingredientId = ingredientId,
            packageFormatId = packageFormatId,
            basePriceCents = basePriceCents,
            currentPriceCents = currentPriceCents,
            availability = availability,
            lastReviewedGameDay = lastReviewedGameDay,
            lastPriceChangeGameDay = lastPriceChangeGameDay,
            lastAvailabilityChangeGameDay = lastAvailabilityChangeGameDay,
            reviewCount = reviewCount
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierMarketChangeRecord
{
    public long sequence;
    public int gameDay;
    public string supplierOfferId;
    public string supplierId;
    public string ingredientId;
    public BistroBuilderSupplierMarketChangeKind changeKind;
    public long previousPriceCents;
    public long currentPriceCents;
    public BistroBuilderSupplierOfferAvailability previousAvailability;
    public BistroBuilderSupplierOfferAvailability currentAvailability;
    public string reasonCode;

    public BistroBuilderSupplierMarketChangeRecord DeepClone()
    {
        return new BistroBuilderSupplierMarketChangeRecord
        {
            sequence = sequence,
            gameDay = gameDay,
            supplierOfferId = supplierOfferId,
            supplierId = supplierId,
            ingredientId = ingredientId,
            changeKind = changeKind,
            previousPriceCents = previousPriceCents,
            currentPriceCents = currentPriceCents,
            previousAvailability = previousAvailability,
            currentAvailability = currentAvailability,
            reasonCode = reasonCode
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierMarketReviewRecord
{
    public long sequence;
    public int gameDay;
    public int offersReviewed;
    public int priceChanges;
    public int availabilityChanges;
    public int unchangedOffers;

    public BistroBuilderSupplierMarketReviewRecord DeepClone()
    {
        return new BistroBuilderSupplierMarketReviewRecord
        {
            sequence = sequence,
            gameDay = gameDay,
            offersReviewed = offersReviewed,
            priceChanges = priceChanges,
            availabilityChanges = availabilityChanges,
            unchangedOffers = unchangedOffers
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierMarketSnapshot
{
    public const string CurrentSchemaId = "supplier.market.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public ulong marketSeed;
    public int currentGameDay = 1;
    public int lastReviewGameDay;
    public int nextReviewGameDay = 5;
    public long marketRevision = 1;
    public long nextSequence = 1;
    public List<BistroBuilderSupplierMarketOfferState> offerStates =
        new List<BistroBuilderSupplierMarketOfferState>();
    public List<BistroBuilderSupplierMarketChangeRecord> changes =
        new List<BistroBuilderSupplierMarketChangeRecord>();
    public List<BistroBuilderSupplierMarketReviewRecord> reviews =
        new List<BistroBuilderSupplierMarketReviewRecord>();

    public BistroBuilderSupplierMarketSnapshot DeepClone()
    {
        BistroBuilderSupplierMarketSnapshot clone =
            new BistroBuilderSupplierMarketSnapshot
            {
                schemaId = schemaId,
                schemaVersion = schemaVersion,
                marketSeed = marketSeed,
                currentGameDay = currentGameDay,
                lastReviewGameDay = lastReviewGameDay,
                nextReviewGameDay = nextReviewGameDay,
                marketRevision = marketRevision,
                nextSequence = nextSequence
            };

        if (offerStates != null)
        {
            for (int index = 0; index < offerStates.Count; index++)
            {
                if (offerStates[index] != null)
                {
                    clone.offerStates.Add(offerStates[index].DeepClone());
                }
            }
        }

        if (changes != null)
        {
            for (int index = 0; index < changes.Count; index++)
            {
                if (changes[index] != null)
                {
                    clone.changes.Add(changes[index].DeepClone());
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
}

public struct BistroBuilderSupplierMarketReviewOutcome
{
    public bool reviewed;
    public int reviewDay;
    public int offersReviewed;
    public int priceChanges;
    public int availabilityChanges;
    public int unchangedOffers;
}
