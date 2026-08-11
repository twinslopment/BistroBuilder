using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Núcleo determinista de 2.3C. No conoce escenas, GameClock, pedidos ni
/// inventario. A igual semilla, día, autoría y estado anterior produce el
/// mismo mercado.
/// </summary>
public static class BistroBuilderSupplierMarketEngine
{
    public static BistroBuilderSupplierMarketSnapshot CreateInitialSnapshot(
        BistroBuilderSupplierAuthoringDatabase supplierDatabase,
        BistroBuilderSupplierMarketSettings settings,
        ulong marketSeed,
        int startGameDay = 1)
    {
        if (supplierDatabase == null)
        {
            throw new ArgumentNullException(nameof(supplierDatabase));
        }

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        int safeStartDay = Mathf.Max(1, startGameDay);
        BistroBuilderSupplierMarketSnapshot snapshot =
            new BistroBuilderSupplierMarketSnapshot
            {
                marketSeed = marketSeed,
                currentGameDay = safeStartDay,
                lastReviewGameDay = 0,
                nextReviewGameDay = FirstReviewAtOrAfter(
                    safeStartDay,
                    settings.ReviewEveryGameDays),
                marketRevision = 1,
                nextSequence = 1
            };

        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers =
            supplierDatabase.Suppliers;

        for (int supplierIndex = 0; supplierIndex < suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null)
            {
                continue;
            }

            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                    supplier.baseOffers[offerIndex];

                if (offer == null || !offer.isActive)
                {
                    continue;
                }

                snapshot.offerStates.Add(
                    new BistroBuilderSupplierMarketOfferState
                    {
                        supplierOfferId = offer.SupplierOfferId,
                        supplierId = supplier.SupplierId,
                        ingredientId = offer.ingredientId,
                        packageFormatId = offer.packageFormatId,
                        basePriceCents = Math.Max(1L, offer.basePriceCents),
                        currentPriceCents = Math.Max(1L, offer.basePriceCents),
                        availability = offer.initialAvailability,
                        lastReviewedGameDay = 0,
                        lastPriceChangeGameDay = 0,
                        lastAvailabilityChangeGameDay = 0,
                        reviewCount = 0
                    });
            }
        }

        snapshot.offerStates.Sort(
            (left, right) => string.CompareOrdinal(
                left != null ? left.supplierOfferId : string.Empty,
                right != null ? right.supplierOfferId : string.Empty));

        return snapshot;
    }

    public static bool TryAdvanceToGameDay(
        BistroBuilderSupplierMarketSnapshot snapshot,
        BistroBuilderSupplierAuthoringDatabase supplierDatabase,
        BistroBuilderSupplierMarketSettings settings,
        int gameDay,
        out List<BistroBuilderSupplierMarketReviewOutcome> outcomes,
        out string error)
    {
        outcomes = new List<BistroBuilderSupplierMarketReviewOutcome>();
        error = null;

        if (snapshot == null)
        {
            error = "El estado de mercado es nulo.";
            return false;
        }

        if (supplierDatabase == null)
        {
            error = "Falta supplier.authoring.";
            return false;
        }

        if (settings == null)
        {
            error = "Faltan los ajustes supplier.market.settings.";
            return false;
        }

        if (gameDay < 1)
        {
            error = "El día de juego debe ser mayor o igual que 1.";
            return false;
        }

        if (!ValidateSnapshotAgainstAuthoring(snapshot, supplierDatabase, out error))
        {
            return false;
        }

        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, gameDay);

        int interval = Math.Max(1, settings.ReviewEveryGameDays);
        if (snapshot.nextReviewGameDay < 1)
        {
            snapshot.nextReviewGameDay = FirstReviewAtOrAfter(
                snapshot.currentGameDay,
                interval);
        }

        while (snapshot.nextReviewGameDay <= gameDay)
        {
            int reviewDay = snapshot.nextReviewGameDay;
            BistroBuilderSupplierMarketReviewOutcome outcome;

            if (!TryReview(
                    snapshot,
                    supplierDatabase,
                    settings,
                    reviewDay,
                    out outcome,
                    out error))
            {
                return false;
            }

            outcomes.Add(outcome);
            snapshot.lastReviewGameDay = reviewDay;
            snapshot.nextReviewGameDay = reviewDay + interval;
        }

        snapshot.currentGameDay = gameDay;
        return true;
    }

    public static bool TryReview(
        BistroBuilderSupplierMarketSnapshot snapshot,
        BistroBuilderSupplierAuthoringDatabase supplierDatabase,
        BistroBuilderSupplierMarketSettings settings,
        int reviewDay,
        out BistroBuilderSupplierMarketReviewOutcome outcome,
        out string error)
    {
        outcome = new BistroBuilderSupplierMarketReviewOutcome();
        error = null;

        if (snapshot == null || supplierDatabase == null || settings == null)
        {
            error = "Mercado, autoría o ajustes no disponibles.";
            return false;
        }

        Dictionary<string, BistroBuilderSupplierAuthoringRecord> suppliersById =
            BuildSupplierIndex(supplierDatabase);
        Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord> offersById =
            BuildOfferIndex(supplierDatabase);

        int priceChanges = 0;
        int availabilityChanges = 0;
        int unchanged = 0;
        int reviewed = 0;

        for (int index = 0; index < snapshot.offerStates.Count; index++)
        {
            BistroBuilderSupplierMarketOfferState state = snapshot.offerStates[index];
            if (state == null)
            {
                continue;
            }

            BistroBuilderSupplierAuthoringRecord supplier;
            BistroBuilderSupplierBaseOfferAuthoringRecord offer;

            if (!suppliersById.TryGetValue(state.supplierId, out supplier) ||
                !offersById.TryGetValue(state.supplierOfferId, out offer))
            {
                error = "El estado " + state.supplierOfferId +
                    " ya no tiene una oferta/proveedor de autoría válido.";
                return false;
            }

            long previousPrice = state.currentPriceCents;
            BistroBuilderSupplierOfferAvailability previousAvailability =
                state.availability;

            long nextPrice = ResolveNextPrice(
                snapshot.marketSeed,
                reviewDay,
                supplier,
                offer,
                state,
                settings);

            BistroBuilderSupplierOfferAvailability nextAvailability =
                ResolveNextAvailability(
                    snapshot.marketSeed,
                    reviewDay,
                    supplier,
                    offer,
                    state,
                    settings);

            bool priceChanged = nextPrice != previousPrice;
            bool availabilityChanged = nextAvailability != previousAvailability;

            state.currentPriceCents = nextPrice;
            state.availability = nextAvailability;
            state.lastReviewedGameDay = reviewDay;
            state.reviewCount = Math.Max(0, state.reviewCount) + 1;

            if (priceChanged)
            {
                state.lastPriceChangeGameDay = reviewDay;
                priceChanges++;
            }

            if (availabilityChanged)
            {
                state.lastAvailabilityChangeGameDay = reviewDay;
                availabilityChanges++;
            }

            if (!priceChanged && !availabilityChanged)
            {
                unchanged++;
            }
            else
            {
                BistroBuilderSupplierMarketChangeKind kind =
                    priceChanged && availabilityChanged
                        ? BistroBuilderSupplierMarketChangeKind.PriceAndAvailability
                        : priceChanged
                            ? BistroBuilderSupplierMarketChangeKind.Price
                            : BistroBuilderSupplierMarketChangeKind.Availability;

                snapshot.changes.Add(
                    new BistroBuilderSupplierMarketChangeRecord
                    {
                        sequence = snapshot.nextSequence++,
                        gameDay = reviewDay,
                        supplierOfferId = state.supplierOfferId,
                        supplierId = state.supplierId,
                        ingredientId = state.ingredientId,
                        changeKind = kind,
                        previousPriceCents = previousPrice,
                        currentPriceCents = nextPrice,
                        previousAvailability = previousAvailability,
                        currentAvailability = nextAvailability,
                        reasonCode = "market_review_5d"
                    });
            }

            reviewed++;
        }

        snapshot.reviews.Add(
            new BistroBuilderSupplierMarketReviewRecord
            {
                sequence = snapshot.nextSequence++,
                gameDay = reviewDay,
                offersReviewed = reviewed,
                priceChanges = priceChanges,
                availabilityChanges = availabilityChanges,
                unchangedOffers = unchanged
            });

        TrimHistory(snapshot, settings);
        snapshot.marketRevision = Math.Max(1L, snapshot.marketRevision + 1L);

        outcome = new BistroBuilderSupplierMarketReviewOutcome
        {
            reviewed = true,
            reviewDay = reviewDay,
            offersReviewed = reviewed,
            priceChanges = priceChanges,
            availabilityChanges = availabilityChanges,
            unchangedOffers = unchanged
        };

        return true;
    }

    public static bool ValidateSnapshotAgainstAuthoring(
        BistroBuilderSupplierMarketSnapshot snapshot,
        BistroBuilderSupplierAuthoringDatabase supplierDatabase,
        out string error)
    {
        error = null;

        if (snapshot == null)
        {
            error = "Snapshot nulo.";
            return false;
        }

        if (snapshot.schemaId != BistroBuilderSupplierMarketSnapshot.CurrentSchemaId ||
            snapshot.schemaVersion != BistroBuilderSupplierMarketSnapshot.CurrentSchemaVersion)
        {
            error = "Schema supplier.market.runtime incompatible.";
            return false;
        }

        Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord> offers =
            BuildOfferIndex(supplierDatabase);
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        int activeOfferCount = offers.Count;
        int stateCount = 0;

        for (int index = 0; index < snapshot.offerStates.Count; index++)
        {
            BistroBuilderSupplierMarketOfferState state = snapshot.offerStates[index];
            if (state == null)
            {
                error = "Existe un estado de mercado nulo.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(state.supplierOfferId) ||
                !ids.Add(state.supplierOfferId))
            {
                error = "SupplierOfferId de mercado vacío o duplicado.";
                return false;
            }

            BistroBuilderSupplierBaseOfferAuthoringRecord offer;
            if (!offers.TryGetValue(state.supplierOfferId, out offer))
            {
                error = "El mercado contiene una oferta que no existe en autoría: " +
                    state.supplierOfferId;
                return false;
            }

            if (state.basePriceCents <= 0 || state.currentPriceCents <= 0)
            {
                error = "Precio no positivo en " + state.supplierOfferId + ".";
                return false;
            }

            stateCount++;
        }

        if (stateCount != activeOfferCount)
        {
            error = "Cardinalidad mercado/autoría distinta: mercado " + stateCount +
                ", ofertas activas " + activeOfferCount + ".";
            return false;
        }

        return true;
    }

    public static ulong StableSeedFromText(string value, int salt)
    {
        ulong hash = 1469598103934665603UL;
        string safe = (value ?? string.Empty) + "|" + salt;
        for (int index = 0; index < safe.Length; index++)
        {
            hash ^= safe[index];
            hash *= 1099511628211UL;
        }

        return hash == 0UL ? 1UL : hash;
    }

    public static string BuildFingerprint(BistroBuilderSupplierMarketSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return "null";
        }

        ulong hash = 1469598103934665603UL;
        Mix(ref hash, snapshot.schemaId);
        Mix(ref hash, snapshot.schemaVersion.ToString());
        Mix(ref hash, snapshot.marketSeed.ToString());
        Mix(ref hash, snapshot.currentGameDay.ToString());
        Mix(ref hash, snapshot.lastReviewGameDay.ToString());
        Mix(ref hash, snapshot.nextReviewGameDay.ToString());
        Mix(ref hash, snapshot.marketRevision.ToString());

        if (snapshot.offerStates != null)
        {
            List<BistroBuilderSupplierMarketOfferState> sorted =
                new List<BistroBuilderSupplierMarketOfferState>(snapshot.offerStates);
            sorted.Sort((a, b) => string.CompareOrdinal(
                a != null ? a.supplierOfferId : string.Empty,
                b != null ? b.supplierOfferId : string.Empty));

            for (int index = 0; index < sorted.Count; index++)
            {
                BistroBuilderSupplierMarketOfferState state = sorted[index];
                if (state == null)
                {
                    Mix(ref hash, "<null>");
                    continue;
                }

                Mix(ref hash, state.supplierOfferId);
                Mix(ref hash, state.currentPriceCents.ToString());
                Mix(ref hash, ((int)state.availability).ToString());
                Mix(ref hash, state.lastReviewedGameDay.ToString());
                Mix(ref hash, state.reviewCount.ToString());
            }
        }

        return hash.ToString("X16");
    }

    private static long ResolveNextPrice(
        ulong marketSeed,
        int reviewDay,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        BistroBuilderSupplierMarketOfferState state,
        BistroBuilderSupplierMarketSettings settings)
    {
        BistroBuilderSupplierPriceProfile profile =
            supplier.priceEvolutionProfile != null
                ? supplier.priceEvolutionProfile.profile
                : BistroBuilderSupplierPriceProfile.Moderado;

        double changeChance = profile == BistroBuilderSupplierPriceProfile.Estable
            ? settings.StablePriceChangeChance
            : profile == BistroBuilderSupplierPriceProfile.Variable
                ? settings.VariablePriceChangeChance
                : settings.ModeratePriceChangeChance;

        double trigger = Random01(marketSeed, reviewDay, state.supplierOfferId, "price-trigger");
        if (trigger >= changeChance)
        {
            return ClampPriceToBounds(state.currentPriceCents, supplier, offer, state.basePriceCents);
        }

        double maxStep = profile == BistroBuilderSupplierPriceProfile.Estable
            ? settings.StableMaximumStepPercent
            : profile == BistroBuilderSupplierPriceProfile.Variable
                ? settings.VariableMaximumStepPercent
                : settings.ModerateMaximumStepPercent;

        double supplierPulse = RandomSigned(marketSeed, reviewDay, supplier.SupplierId, "supplier-pulse");
        double offerNoise = RandomSigned(marketSeed, reviewDay, state.supplierOfferId, "offer-noise");

        double deviationPercent = state.basePriceCents > 0
            ? ((double)state.currentPriceCents / state.basePriceCents - 1.0) * 100.0
            : 0.0;

        double meanReversion = -Math.Sign(deviationPercent) *
            Math.Min(0.35, Math.Abs(deviationPercent) / 30.0);
        double directionScore = supplierPulse * 0.65 + offerNoise * 0.35 + meanReversion;
        int direction = directionScore >= 0.0 ? 1 : -1;

        double magnitudeRandom = Random01(marketSeed, reviewDay, state.supplierOfferId, "price-step");
        double magnitude = 0.45 + magnitudeRandom * Math.Max(0.1, maxStep - 0.45);
        double stepPercent = magnitude * direction;

        long candidate = (long)Math.Round(
            state.currentPriceCents * (1.0 + stepPercent / 100.0),
            MidpointRounding.AwayFromZero);

        long clamped = ClampPriceToBounds(candidate, supplier, offer, state.basePriceCents);
        if (clamped == state.currentPriceCents)
        {
            long oneCent = ClampPriceToBounds(
                state.currentPriceCents + direction,
                supplier,
                offer,
                state.basePriceCents);
            return Math.Max(1L, oneCent);
        }

        return Math.Max(1L, clamped);
    }

    private static long ClampPriceToBounds(
        long candidate,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        long basePriceCents)
    {
        float supplierMin = supplier.priceEvolutionProfile != null
            ? supplier.priceEvolutionProfile.minimumVariationPercent
            : -8f;
        float supplierMax = supplier.priceEvolutionProfile != null
            ? supplier.priceEvolutionProfile.maximumVariationPercent
            : 12f;

        double minimumPercent = Math.Max(supplierMin, offer.minimumMarketVariationPercent);
        double maximumPercent = Math.Min(supplierMax, offer.maximumMarketVariationPercent);

        if (minimumPercent > maximumPercent)
        {
            double middle = (minimumPercent + maximumPercent) * 0.5;
            minimumPercent = middle;
            maximumPercent = middle;
        }

        long minimum = Math.Max(
            1L,
            (long)Math.Round(basePriceCents * (1.0 + minimumPercent / 100.0),
                MidpointRounding.AwayFromZero));
        long maximum = Math.Max(
            minimum,
            (long)Math.Round(basePriceCents * (1.0 + maximumPercent / 100.0),
                MidpointRounding.AwayFromZero));

        if (candidate < minimum)
        {
            return minimum;
        }

        if (candidate > maximum)
        {
            return maximum;
        }

        return candidate;
    }

    private static BistroBuilderSupplierOfferAvailability ResolveNextAvailability(
        ulong marketSeed,
        int reviewDay,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        BistroBuilderSupplierMarketOfferState state,
        BistroBuilderSupplierMarketSettings settings)
    {
        if (supplier.availabilityProfile == null)
        {
            return state.availability;
        }

        double multiplier = ResolveAvailabilityMultiplier(
            supplier.availabilityProfile.profile,
            settings);
        double limitedWeight = Math.Max(
            0.0,
            Math.Min(0.60, supplier.availabilityProfile.limitedStockWeight * multiplier));
        double outWeight = Math.Max(
            0.0,
            Math.Min(0.25, supplier.availabilityProfile.temporaryOutOfStockWeight * multiplier));
        double roll = Random01(marketSeed, reviewDay, state.supplierOfferId, "availability");

        if (state.availability == BistroBuilderSupplierOfferAvailability.Disponible)
        {
            if (roll < outWeight)
            {
                return BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado;
            }

            if (roll < outWeight + limitedWeight)
            {
                return BistroBuilderSupplierOfferAvailability.StockLimitado;
            }

            return BistroBuilderSupplierOfferAvailability.Disponible;
        }

        if (state.availability == BistroBuilderSupplierOfferAvailability.StockLimitado)
        {
            double outChance = Math.Min(0.20, outWeight * 1.5 + 0.015);
            double remainLimitedChance = Math.Min(0.55, 0.28 + limitedWeight * 0.65);

            if (roll < outChance)
            {
                return BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado;
            }

            if (roll < outChance + remainLimitedChance)
            {
                return BistroBuilderSupplierOfferAvailability.StockLimitado;
            }

            return BistroBuilderSupplierOfferAvailability.Disponible;
        }

        double remainOutChance = Math.Min(0.22, 0.04 + outWeight * 1.5);
        double recoverLimitedChance = Math.Min(0.62, 0.34 + limitedWeight * 0.75);

        if (roll < remainOutChance)
        {
            return BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado;
        }

        if (roll < remainOutChance + recoverLimitedChance)
        {
            return BistroBuilderSupplierOfferAvailability.StockLimitado;
        }

        return BistroBuilderSupplierOfferAvailability.Disponible;
    }

    private static double ResolveAvailabilityMultiplier(
        BistroBuilderSupplierAvailabilityProfile profile,
        BistroBuilderSupplierMarketSettings settings)
    {
        switch (profile)
        {
            case BistroBuilderSupplierAvailabilityProfile.MuyEstable:
                return settings.VeryStableAvailabilityMultiplier;
            case BistroBuilderSupplierAvailabilityProfile.Variable:
                return settings.VariableAvailabilityMultiplier;
            case BistroBuilderSupplierAvailabilityProfile.Estacional:
                return settings.SeasonalAvailabilityMultiplier;
            default:
                return settings.StableAvailabilityMultiplier;
        }
    }

    private static int FirstReviewAtOrAfter(int day, int interval)
    {
        int safeDay = Math.Max(1, day);
        int safeInterval = Math.Max(1, interval);
        int multiple = ((safeDay + safeInterval - 1) / safeInterval) * safeInterval;
        return Math.Max(safeInterval, multiple);
    }

    private static Dictionary<string, BistroBuilderSupplierAuthoringRecord> BuildSupplierIndex(
        BistroBuilderSupplierAuthoringDatabase database)
    {
        Dictionary<string, BistroBuilderSupplierAuthoringRecord> result =
            new Dictionary<string, BistroBuilderSupplierAuthoringRecord>(StringComparer.Ordinal);

        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = database.Suppliers;
        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier != null && supplier.isActive &&
                !string.IsNullOrWhiteSpace(supplier.SupplierId))
            {
                result[supplier.SupplierId] = supplier;
            }
        }

        return result;
    }

    private static Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord> BuildOfferIndex(
        BistroBuilderSupplierAuthoringDatabase database)
    {
        Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord> result =
            new Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord>(StringComparer.Ordinal);

        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = database.Suppliers;
        for (int supplierIndex = 0; supplierIndex < suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null)
            {
                continue;
            }

            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                if (offer != null && offer.isActive &&
                    !string.IsNullOrWhiteSpace(offer.SupplierOfferId))
                {
                    result[offer.SupplierOfferId] = offer;
                }
            }
        }

        return result;
    }

    private static void TrimHistory(
        BistroBuilderSupplierMarketSnapshot snapshot,
        BistroBuilderSupplierMarketSettings settings)
    {
        int maxChanges = Math.Max(16, settings.MaximumChangeHistoryEntries);
        int excessChanges = snapshot.changes.Count - maxChanges;
        if (excessChanges > 0)
        {
            snapshot.changes.RemoveRange(0, excessChanges);
        }

        int maxReviews = Math.Max(4, settings.MaximumReviewHistoryEntries);
        int excessReviews = snapshot.reviews.Count - maxReviews;
        if (excessReviews > 0)
        {
            snapshot.reviews.RemoveRange(0, excessReviews);
        }
    }

    private static double Random01(ulong seed, int day, string identity, string channel)
    {
        ulong hash = seed == 0UL ? 1UL : seed;
        Mix(ref hash, day.ToString());
        Mix(ref hash, identity);
        Mix(ref hash, channel);
        hash ^= hash >> 12;
        hash ^= hash << 25;
        hash ^= hash >> 27;
        hash *= 2685821657736338717UL;
        return (hash >> 11) * (1.0 / 9007199254740992.0);
    }

    private static double RandomSigned(ulong seed, int day, string identity, string channel)
    {
        return Random01(seed, day, identity, channel) * 2.0 - 1.0;
    }

    private static void Mix(ref ulong hash, string value)
    {
        string safe = value ?? string.Empty;
        for (int index = 0; index < safe.Length; index++)
        {
            hash ^= safe[index];
            hash *= 1099511628211UL;
        }
    }
}
