using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Motor Comercial Inteligente determinista de 2.3D.
///
/// Decide campañas y promociones temporales exclusivamente a partir de:
/// - perfiles comerciales de supplier.authoring;
/// - estado de mercado 2.3C;
/// - disponibilidad comercial;
/// - historial comercial propio;
/// - semilla determinista.
///
/// No conoce Inventory, previsión 2.2C, recetas, pedidos ni necesidades del jugador.
/// Por diseño no puede "hacer trampas" reaccionando a una rotura de stock del restaurante.
/// </summary>
public static class BistroBuilderSupplierCommercialIntelligenceEngine
{
    private sealed class Candidate
    {
        public BistroBuilderSupplierBaseOfferAuthoringRecord offer;
        public BistroBuilderSupplierMarketOfferState market;
        public double score;
        public bool favorableMarket;
        public bool specialtyMatch;
    }

    public static BistroBuilderSupplierCommercialIntelligenceSnapshot CreateInitialSnapshot(
        BistroBuilderSupplierMarketSnapshot marketSnapshot,
        BistroBuilderSupplierCommercialIntelligenceSettings settings,
        ulong? explicitCommercialSeed = null)
    {
        if (marketSnapshot == null)
        {
            throw new ArgumentNullException(nameof(marketSnapshot));
        }

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        ulong commercialSeed = explicitCommercialSeed.HasValue && explicitCommercialSeed.Value != 0UL
            ? explicitCommercialSeed.Value
            : BistroBuilderSupplierMarketEngine.StableSeedFromText(
                marketSnapshot.marketSeed.ToString(CultureInfo.InvariantCulture),
                settings.DeterministicSalt);

        return new BistroBuilderSupplierCommercialIntelligenceSnapshot
        {
            sourceMarketSeed = marketSnapshot.marketSeed,
            commercialSeed = commercialSeed,
            currentGameDay = Math.Max(1, marketSnapshot.currentGameDay),
            // En una inicialización fresca no inventamos promociones históricas si el
            // motor entra tarde. Save/Load real restaurará el snapshot en 2.3J.
            lastProcessedMarketReviewDay = marketSnapshot.lastReviewGameDay,
            lastProcessedMarketRevision = marketSnapshot.marketRevision,
            commercialRevision = 1,
            nextSequence = 1
        };
    }

    public static bool TryAdvanceToGameDay(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        BistroBuilderSupplierCommercialIntelligenceSettings settings,
        int gameDay,
        out List<BistroBuilderSupplierPromotionRecord> expiredPromotions,
        out string error)
    {
        expiredPromotions = new List<BistroBuilderSupplierPromotionRecord>();
        error = null;

        if (snapshot == null)
        {
            error = "El estado del Motor Comercial Inteligente es nulo.";
            return false;
        }

        if (settings == null)
        {
            error = "Faltan supplier.commercial.settings.";
            return false;
        }

        if (gameDay < 1)
        {
            error = "El día de juego debe ser mayor o igual que 1.";
            return false;
        }

        if (gameDay < snapshot.currentGameDay)
        {
            // Un retroceso real se restaura mediante snapshot en 2.3J.
            return true;
        }

        snapshot.currentGameDay = gameDay;
        if (snapshot.activePromotions == null)
        {
            snapshot.activePromotions = new List<BistroBuilderSupplierPromotionRecord>();
        }
        if (snapshot.promotionHistory == null)
        {
            snapshot.promotionHistory = new List<BistroBuilderSupplierPromotionRecord>();
        }

        for (int index = snapshot.activePromotions.Count - 1; index >= 0; index--)
        {
            BistroBuilderSupplierPromotionRecord promotion = snapshot.activePromotions[index];
            if (promotion == null)
            {
                snapshot.activePromotions.RemoveAt(index);
                continue;
            }

            if (gameDay < promotion.endGameDayExclusive)
            {
                continue;
            }

            snapshot.activePromotions.RemoveAt(index);
            promotion.lifecycle = BistroBuilderSupplierPromotionLifecycle.Finalizada;
            promotion.endedGameDay = promotion.endGameDayExclusive;
            snapshot.promotionHistory.Add(promotion);
            expiredPromotions.Add(promotion.DeepClone());
            snapshot.commercialRevision++;
        }

        TrimHistory(snapshot, settings);
        return true;
    }

    public static bool TryProcessMarketReview(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        BistroBuilderSupplierMarketSnapshot marketSnapshot,
        BistroBuilderSupplierAuthoringDatabase supplierDatabase,
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase,
        BistroBuilderSupplierCommercialIntelligenceSettings settings,
        int reviewDay,
        out BistroBuilderSupplierCommercialReviewOutcome outcome,
        out List<BistroBuilderSupplierPromotionRecord> startedPromotions,
        out List<BistroBuilderSupplierPromotionRecord> expiredPromotions,
        out string error)
    {
        outcome = new BistroBuilderSupplierCommercialReviewOutcome();
        startedPromotions = new List<BistroBuilderSupplierPromotionRecord>();
        expiredPromotions = new List<BistroBuilderSupplierPromotionRecord>();
        error = null;

        if (snapshot == null || marketSnapshot == null || supplierDatabase == null ||
            ingredientDatabase == null || settings == null)
        {
            error = "Faltan estado comercial, mercado, autoría de proveedores/ingredientes o ajustes 2.3D.";
            return false;
        }

        if (snapshot.sourceMarketSeed != marketSnapshot.marketSeed)
        {
            error = "La semilla del Motor Comercial Inteligente no corresponde al mercado 2.3C activo.";
            return false;
        }

        if (reviewDay <= 0 || reviewDay > marketSnapshot.currentGameDay)
        {
            error = "Día de revisión comercial inválido.";
            return false;
        }

        if (reviewDay <= snapshot.lastProcessedMarketReviewDay)
        {
            // Idempotencia: una revisión ya procesada no vuelve a crear campañas.
            outcome.processed = false;
            outcome.reviewDay = reviewDay;
            return true;
        }

        if (marketSnapshot.lastReviewGameDay < reviewDay)
        {
            error = "2.3D no puede adelantarse a una revisión todavía no ejecutada por 2.3C.";
            return false;
        }

        if (!TryAdvanceToGameDay(snapshot, settings, reviewDay, out expiredPromotions, out error))
        {
            return false;
        }

        Dictionary<string, BistroBuilderSupplierMarketOfferState> marketByOffer =
            BuildMarketIndex(marketSnapshot);
        Dictionary<string, BistroBuilderIngredientAuthoringRecord> ingredientsById =
            BuildIngredientIndex(ingredientDatabase);
        Dictionary<string, BistroBuilderSupplierCommercialCampaignState> campaignsBySupplier =
            BuildCampaignIndex(snapshot);

        List<BistroBuilderSupplierAuthoringRecord> suppliers =
            new List<BistroBuilderSupplierAuthoringRecord>();
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> authoredSuppliers = supplierDatabase.Suppliers;
        for (int index = 0; index < authoredSuppliers.Count; index++)
        {
            if (authoredSuppliers[index] != null && authoredSuppliers[index].isActive)
            {
                suppliers.Add(authoredSuppliers[index]);
            }
        }
        suppliers.Sort((a, b) => string.CompareOrdinal(a.SupplierId, b.SupplierId));

        int interval = ResolveMarketReviewInterval(marketSnapshot);
        int suppliersEvaluated = 0;
        int eligibleOffers = 0;
        int campaignsStarted = 0;
        int promotionsStarted = 0;
        int skippedCooldown = 0;
        int noEligible = 0;

        for (int supplierIndex = 0; supplierIndex < suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[supplierIndex];
            suppliersEvaluated++;

            BistroBuilderSupplierPromotionProfileAuthoring profile = supplier.promotionProfile;
            if (profile == null || supplier.baseOffers == null)
            {
                noEligible++;
                continue;
            }

            BistroBuilderSupplierCommercialCampaignState campaignState;
            if (!campaignsBySupplier.TryGetValue(supplier.SupplierId, out campaignState))
            {
                campaignState = new BistroBuilderSupplierCommercialCampaignState
                {
                    supplierId = supplier.SupplierId,
                    lastCampaignGameDay = 0,
                    campaignCount = 0
                };
                snapshot.campaignStates.Add(campaignState);
                campaignsBySupplier[supplier.SupplierId] = campaignState;
            }

            int activeForSupplier = CountActiveForSupplier(snapshot, supplier.SupplierId, reviewDay);
            int capacity = Math.Max(0, settings.MaximumActivePromotionsPerSupplier - activeForSupplier);
            if (capacity <= 0)
            {
                skippedCooldown++;
                continue;
            }

            int cooldownReviews = ResolveCooldownReviews(profile.frequency, settings);
            int cooldownDays = Math.Max(1, interval) * Math.Max(1, cooldownReviews);
            if (campaignState.lastCampaignGameDay > 0 &&
                reviewDay - campaignState.lastCampaignGameDay < cooldownDays)
            {
                skippedCooldown++;
                continue;
            }

            List<Candidate> candidates = BuildCandidates(
                snapshot,
                supplier,
                ingredientDatabase,
                ingredientsById,
                marketByOffer,
                settings,
                reviewDay);
            eligibleOffers += candidates.Count;

            if (candidates.Count == 0)
            {
                noEligible++;
                continue;
            }

            int favorableCount = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].favorableMarket)
                {
                    favorableCount++;
                }
            }

            double favorableRatio = candidates.Count > 0
                ? favorableCount / (double)candidates.Count
                : 0.0;
            double baseChance = ResolveCampaignChance(profile.frequency, settings);
            double contextualChance = Clamp01(baseChance * (0.85 + 0.30 * favorableRatio));
            double campaignRoll = Random01(
                snapshot.commercialSeed,
                reviewDay,
                supplier.SupplierId,
                "campaign-launch");

            if (campaignRoll >= contextualChance)
            {
                continue;
            }

            candidates.Sort((left, right) =>
            {
                int scoreCompare = right.score.CompareTo(left.score);
                return scoreCompare != 0
                    ? scoreCompare
                    : string.CompareOrdinal(left.offer.SupplierOfferId, right.offer.SupplierOfferId);
            });

            int campaignMaximum = Math.Min(
                capacity,
                ResolveMaximumOffersPerCampaign(profile.frequency, settings));
            campaignMaximum = Math.Min(campaignMaximum, candidates.Count);
            int desiredCount = ResolveCampaignOfferCount(
                snapshot.commercialSeed,
                reviewDay,
                supplier.SupplierId,
                campaignMaximum);

            int actuallyStarted = 0;
            for (int candidateIndex = 0;
                 candidateIndex < candidates.Count && actuallyStarted < desiredCount;
                 candidateIndex++)
            {
                Candidate candidate = candidates[candidateIndex];
                BistroBuilderSupplierPromotionRecord promotion = CreatePromotion(
                    snapshot,
                    supplier,
                    profile,
                    candidate,
                    reviewDay,
                    marketSnapshot.marketRevision);

                if (promotion == null)
                {
                    continue;
                }

                snapshot.activePromotions.Add(promotion);
                startedPromotions.Add(promotion.DeepClone());
                actuallyStarted++;
                promotionsStarted++;
                snapshot.commercialRevision++;
            }

            if (actuallyStarted > 0)
            {
                campaignsStarted++;
                campaignState.lastCampaignGameDay = reviewDay;
                campaignState.campaignCount++;
            }
        }

        snapshot.lastProcessedMarketReviewDay = reviewDay;
        snapshot.lastProcessedMarketRevision = marketSnapshot.marketRevision;
        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, reviewDay);

        BistroBuilderSupplierCommercialReviewRecord reviewRecord =
            new BistroBuilderSupplierCommercialReviewRecord
            {
                sequence = snapshot.nextSequence++,
                gameDay = reviewDay,
                sourceMarketRevision = marketSnapshot.marketRevision,
                suppliersEvaluated = suppliersEvaluated,
                eligibleOffers = eligibleOffers,
                campaignsStarted = campaignsStarted,
                promotionsStarted = promotionsStarted,
                promotionsExpired = expiredPromotions.Count,
                suppliersSkippedByCooldown = skippedCooldown,
                suppliersWithoutEligibleOffers = noEligible
            };
        snapshot.reviews.Add(reviewRecord);

        if (campaignsStarted > 0 || expiredPromotions.Count > 0)
        {
            snapshot.commercialRevision++;
        }

        TrimHistory(snapshot, settings);

        outcome = new BistroBuilderSupplierCommercialReviewOutcome
        {
            processed = true,
            reviewDay = reviewDay,
            suppliersEvaluated = suppliersEvaluated,
            eligibleOffers = eligibleOffers,
            campaignsStarted = campaignsStarted,
            promotionsStarted = promotionsStarted,
            promotionsExpired = expiredPromotions.Count
        };
        return true;
    }


    /// <summary>
    /// Reconstruye una vista del mercado exactamente en una revisión pasada usando
    /// los registros de cambio 2.3C. Permite que 2.3D procese correctamente saltos
    /// de varios ciclos (p. ej. día 4 -> 16) sin tomar datos "del futuro".
    /// </summary>
    public static BistroBuilderSupplierMarketSnapshot CreateReviewScopedMarketSnapshot(
        BistroBuilderSupplierMarketSnapshot source,
        int reviewDay)
    {
        if (source == null)
        {
            return null;
        }

        BistroBuilderSupplierMarketSnapshot clone = source.DeepClone();
        if (reviewDay >= source.lastReviewGameDay)
        {
            clone.currentGameDay = Math.Min(source.currentGameDay, Math.Max(1, reviewDay));
            return clone;
        }

        Dictionary<string, BistroBuilderSupplierMarketOfferState> stateByOffer =
            BuildMarketIndex(clone);
        List<BistroBuilderSupplierMarketChangeRecord> changes =
            new List<BistroBuilderSupplierMarketChangeRecord>(clone.changes ??
                new List<BistroBuilderSupplierMarketChangeRecord>());
        changes.Sort((left, right) =>
        {
            int dayCompare = (right != null ? right.gameDay : 0).CompareTo(
                left != null ? left.gameDay : 0);
            if (dayCompare != 0) return dayCompare;
            return (right != null ? right.sequence : 0L).CompareTo(
                left != null ? left.sequence : 0L);
        });

        for (int index = 0; index < changes.Count; index++)
        {
            BistroBuilderSupplierMarketChangeRecord change = changes[index];
            if (change == null || change.gameDay <= reviewDay)
            {
                continue;
            }

            BistroBuilderSupplierMarketOfferState offerState;
            if (stateByOffer.TryGetValue(change.supplierOfferId, out offerState) && offerState != null)
            {
                offerState.currentPriceCents = change.previousPriceCents;
                offerState.availability = change.previousAvailability;
            }
        }

        int removedReviews = 0;
        if (clone.reviews != null)
        {
            for (int index = clone.reviews.Count - 1; index >= 0; index--)
            {
                if (clone.reviews[index] != null && clone.reviews[index].gameDay > reviewDay)
                {
                    clone.reviews.RemoveAt(index);
                    removedReviews++;
                }
            }
        }
        if (clone.changes != null)
        {
            for (int index = clone.changes.Count - 1; index >= 0; index--)
            {
                if (clone.changes[index] != null && clone.changes[index].gameDay > reviewDay)
                {
                    clone.changes.RemoveAt(index);
                }
            }
        }

        int interval = ResolveMarketReviewInterval(source);
        clone.currentGameDay = Math.Max(1, reviewDay);
        clone.lastReviewGameDay = Math.Max(0, reviewDay);
        clone.nextReviewGameDay = reviewDay + Math.Max(1, interval);
        clone.marketRevision = Math.Max(1L, source.marketRevision - removedReviews);

        int reviewCount = clone.reviews != null ? clone.reviews.Count : 0;
        for (int index = 0; index < clone.offerStates.Count; index++)
        {
            BistroBuilderSupplierMarketOfferState offerState = clone.offerStates[index];
            if (offerState == null) continue;
            offerState.reviewCount = reviewCount;
            offerState.lastReviewedGameDay = reviewDay;
            if (offerState.lastPriceChangeGameDay > reviewDay)
            {
                offerState.lastPriceChangeGameDay = FindLastPriceChangeDay(clone, offerState.supplierOfferId);
            }
            if (offerState.lastAvailabilityChangeGameDay > reviewDay)
            {
                offerState.lastAvailabilityChangeGameDay = FindLastAvailabilityChangeDay(clone, offerState.supplierOfferId);
            }
        }

        return clone;
    }

    public static bool ValidateSnapshotAgainstAuthoringAndMarket(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        BistroBuilderSupplierMarketSnapshot marketSnapshot,
        BistroBuilderSupplierAuthoringDatabase supplierDatabase,
        out string error)
    {
        error = null;
        if (snapshot == null)
        {
            error = "Snapshot comercial nulo.";
            return false;
        }
        if (marketSnapshot == null || supplierDatabase == null)
        {
            error = "Faltan mercado o supplier.authoring para validar 2.3D.";
            return false;
        }
        if (snapshot.schemaId != BistroBuilderSupplierCommercialIntelligenceSnapshot.CurrentSchemaId ||
            snapshot.schemaVersion != BistroBuilderSupplierCommercialIntelligenceSnapshot.CurrentSchemaVersion)
        {
            error = "Schema supplier.commercial.runtime incompatible.";
            return false;
        }
        if (snapshot.sourceMarketSeed != marketSnapshot.marketSeed)
        {
            error = "El snapshot comercial pertenece a otra semilla de mercado.";
            return false;
        }

        Dictionary<string, BistroBuilderSupplierMarketOfferState> marketByOffer =
            BuildMarketIndex(marketSnapshot);
        HashSet<string> promotionIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> activeOfferIds = new HashSet<string>(StringComparer.Ordinal);

        if (snapshot.activePromotions == null || snapshot.promotionHistory == null ||
            snapshot.campaignStates == null || snapshot.reviews == null)
        {
            error = "El snapshot comercial contiene colecciones nulas.";
            return false;
        }

        for (int index = 0; index < snapshot.activePromotions.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = snapshot.activePromotions[index];
            if (!ValidatePromotion(promotion, marketByOffer, promotionIds, out error))
            {
                return false;
            }
            if (promotion.lifecycle != BistroBuilderSupplierPromotionLifecycle.Activa)
            {
                error = "La colección de promociones activas contiene una promoción finalizada.";
                return false;
            }
            if (!activeOfferIds.Add(promotion.supplierOfferId))
            {
                error = "Hay más de una promoción activa para " + promotion.supplierOfferId + ".";
                return false;
            }
        }

        for (int index = 0; index < snapshot.promotionHistory.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = snapshot.promotionHistory[index];
            if (!ValidatePromotion(promotion, marketByOffer, promotionIds, out error))
            {
                return false;
            }
            if (promotion.lifecycle != BistroBuilderSupplierPromotionLifecycle.Finalizada)
            {
                error = "El historial contiene una promoción todavía activa.";
                return false;
            }
        }

        return true;
    }

    public static string BuildFingerprint(BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return "null";
        }

        ulong hash = 1469598103934665603UL;
        Mix(ref hash, snapshot.schemaId);
        Mix(ref hash, snapshot.schemaVersion.ToString(CultureInfo.InvariantCulture));
        Mix(ref hash, snapshot.sourceMarketSeed.ToString(CultureInfo.InvariantCulture));
        Mix(ref hash, snapshot.commercialSeed.ToString(CultureInfo.InvariantCulture));
        Mix(ref hash, snapshot.currentGameDay.ToString(CultureInfo.InvariantCulture));
        Mix(ref hash, snapshot.lastProcessedMarketReviewDay.ToString(CultureInfo.InvariantCulture));
        Mix(ref hash, snapshot.commercialRevision.ToString(CultureInfo.InvariantCulture));

        List<BistroBuilderSupplierPromotionRecord> active =
            new List<BistroBuilderSupplierPromotionRecord>(snapshot.activePromotions ??
                new List<BistroBuilderSupplierPromotionRecord>());
        active.Sort((a, b) => string.CompareOrdinal(
            a != null ? a.promotionId : string.Empty,
            b != null ? b.promotionId : string.Empty));
        MixPromotionList(ref hash, active);

        List<BistroBuilderSupplierPromotionRecord> history =
            new List<BistroBuilderSupplierPromotionRecord>(snapshot.promotionHistory ??
                new List<BistroBuilderSupplierPromotionRecord>());
        history.Sort((a, b) => string.CompareOrdinal(
            a != null ? a.promotionId : string.Empty,
            b != null ? b.promotionId : string.Empty));
        MixPromotionList(ref hash, history);
        return hash.ToString("X16");
    }

    private static List<Candidate> BuildCandidates(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase,
        Dictionary<string, BistroBuilderIngredientAuthoringRecord> ingredientsById,
        Dictionary<string, BistroBuilderSupplierMarketOfferState> marketByOffer,
        BistroBuilderSupplierCommercialIntelligenceSettings settings,
        int reviewDay)
    {
        List<Candidate> result = new List<Candidate>();
        if (supplier.baseOffers == null)
        {
            return result;
        }

        for (int index = 0; index < supplier.baseOffers.Count; index++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[index];
            if (offer == null || !offer.isActive || !offer.promotionEligible ||
                string.IsNullOrWhiteSpace(offer.SupplierOfferId))
            {
                continue;
            }

            BistroBuilderSupplierMarketOfferState market;
            if (!marketByOffer.TryGetValue(offer.SupplierOfferId, out market) || market == null)
            {
                continue;
            }

            if (market.availability == BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado)
            {
                continue;
            }
            if (settings.RequireFullyAvailableStock &&
                market.availability != BistroBuilderSupplierOfferAvailability.Disponible)
            {
                continue;
            }
            if (FindActivePromotion(snapshot, offer.SupplierOfferId, reviewDay) != null)
            {
                continue;
            }
            if (WasRecentlyPromoted(snapshot, offer.SupplierOfferId, reviewDay, settings.OfferReuseCooldownDays))
            {
                continue;
            }

            bool specialtyMatch;
            if (!MatchesEligibleCatalog(
                    supplier,
                    offer,
                    ingredientDatabase,
                    ingredientsById,
                    out specialtyMatch))
            {
                continue;
            }

            bool favorable = market.basePriceCents > 0 &&
                market.currentPriceCents <= market.basePriceCents;
            double priceRatio = market.basePriceCents > 0
                ? market.currentPriceCents / (double)market.basePriceCents
                : 1.0;
            double score = Random01(
                snapshot.commercialSeed,
                reviewDay,
                offer.SupplierOfferId,
                "candidate-score") * 1000.0;

            if (favorable)
            {
                score += 220.0;
            }
            else if (priceRatio > 1.05)
            {
                score -= 160.0;
            }
            else
            {
                score += 60.0;
            }

            if (specialtyMatch)
            {
                score += 90.0;
            }
            if ((supplier.commercialModelFlags & BistroBuilderSupplierCommercialModelFlags.Mayorista) != 0)
            {
                score += 45.0;
            }

            result.Add(new Candidate
            {
                offer = offer,
                market = market,
                score = score,
                favorableMarket = favorable,
                specialtyMatch = specialtyMatch
            });
        }

        return result;
    }

    private static BistroBuilderSupplierPromotionRecord CreatePromotion(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierPromotionProfileAuthoring profile,
        Candidate candidate,
        int reviewDay,
        long marketRevision)
    {
        int minBasisPoints = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Max(0f, profile.minimumDiscountPercent) * 100f),
            0,
            9500);
        int maxBasisPoints = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Max(profile.minimumDiscountPercent, profile.maximumDiscountPercent) * 100f),
            minBasisPoints,
            9500);

        if (maxBasisPoints <= 0 || candidate.market.currentPriceCents <= 1)
        {
            return null;
        }

        double discountRoll = Random01(
            snapshot.commercialSeed,
            reviewDay,
            candidate.offer.SupplierOfferId,
            "discount");
        int discountBasisPoints = minBasisPoints +
            (int)Math.Round((maxBasisPoints - minBasisPoints) * discountRoll,
                MidpointRounding.AwayFromZero);
        discountBasisPoints = Math.Max(1, Math.Min(9500, discountBasisPoints));

        long promotionalPrice = (long)Math.Round(
            candidate.market.currentPriceCents * (10000.0 - discountBasisPoints) / 10000.0,
            MidpointRounding.AwayFromZero);
        promotionalPrice = Math.Max(1L, promotionalPrice);
        if (promotionalPrice >= candidate.market.currentPriceCents)
        {
            promotionalPrice = Math.Max(1L, candidate.market.currentPriceCents - 1L);
        }

        if (promotionalPrice >= candidate.market.currentPriceCents)
        {
            return null;
        }

        int minDuration = Math.Max(1, profile.minimumDurationDays);
        int maxDuration = Math.Max(minDuration, profile.maximumDurationDays);
        double durationRoll = Random01(
            snapshot.commercialSeed,
            reviewDay,
            candidate.offer.SupplierOfferId,
            "duration");
        int duration = minDuration +
            (int)Math.Floor(durationRoll * (maxDuration - minDuration + 1));
        duration = Math.Max(minDuration, Math.Min(maxDuration, duration));

        string reasonCode;
        string reasonText;
        ResolveReason(supplier, candidate, out reasonCode, out reasonText);

        return new BistroBuilderSupplierPromotionRecord
        {
            promotionId = "promotion_" + candidate.offer.SupplierOfferId + "_d" + reviewDay,
            supplierOfferId = candidate.offer.SupplierOfferId,
            supplierId = supplier.SupplierId,
            ingredientId = candidate.offer.ingredientId,
            packageFormatId = candidate.offer.packageFormatId,
            startGameDay = reviewDay,
            endGameDayExclusive = reviewDay + duration,
            endedGameDay = 0,
            discountBasisPoints = discountBasisPoints,
            referenceMarketPriceCents = candidate.market.currentPriceCents,
            promotionalPriceCents = promotionalPrice,
            sourceAvailabilityAtStart = candidate.market.availability,
            sourceMarketRevision = marketRevision,
            lifecycle = BistroBuilderSupplierPromotionLifecycle.Activa,
            reasonCode = reasonCode,
            reasonText = reasonText
        };
    }

    private static void ResolveReason(
        BistroBuilderSupplierAuthoringRecord supplier,
        Candidate candidate,
        out string reasonCode,
        out string reasonText)
    {
        if (candidate.favorableMarket)
        {
            reasonCode = "favorable_market_window";
            reasonText = "El proveedor aprovecha una ventana de mercado favorable para lanzar una oferta temporal.";
            return;
        }

        if ((supplier.commercialModelFlags & BistroBuilderSupplierCommercialModelFlags.Mayorista) != 0)
        {
            reasonCode = "wholesale_rotation";
            reasonText = "Campaña de rotación comercial propia del perfil mayorista del proveedor.";
            return;
        }

        if (candidate.specialtyMatch)
        {
            reasonCode = "specialty_campaign";
            reasonText = "Promoción temporal centrada en una familia de producto especializada del proveedor.";
            return;
        }

        reasonCode = "commercial_rotation";
        reasonText = "Campaña temporal generada por la rotación comercial habitual del proveedor.";
    }

    private static bool MatchesEligibleCatalog(
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase,
        Dictionary<string, BistroBuilderIngredientAuthoringRecord> ingredientsById,
        out bool specialtyMatch)
    {
        specialtyMatch = false;
        BistroBuilderSupplierCatalogFlags eligible = supplier.promotionProfile != null
            ? supplier.promotionProfile.eligibleCatalogs
            : BistroBuilderSupplierCatalogFlags.Generalista;

        if ((eligible & BistroBuilderSupplierCatalogFlags.Generalista) != 0)
        {
            return true;
        }

        BistroBuilderIngredientAuthoringRecord ingredient;
        if (ingredientsById.TryGetValue(offer.ingredientId ?? string.Empty, out ingredient) &&
            ingredient != null)
        {
            BistroBuilderSupplierCatalogFlags ingredientFlag =
                ResolveIngredientCatalogFlag(ingredient.categorySnapshot, ingredient.IngredientId);
            if (ingredientFlag != BistroBuilderSupplierCatalogFlags.None &&
                ingredientFlag != BistroBuilderSupplierCatalogFlags.Otros)
            {
                specialtyMatch = (eligible & ingredientFlag) != 0;
                return specialtyMatch;
            }
        }

        // Fallback seguro para proveedores especialistas actuales: si su catálogo de
        // autoría tiene una única familia no generalista, esa familia define sus ofertas.
        BistroBuilderSupplierCatalogFlags nonGeneral =
            supplier.catalogFlags & ~BistroBuilderSupplierCatalogFlags.Generalista;
        if (HasSingleFlag(nonGeneral))
        {
            specialtyMatch = (eligible & nonGeneral) != 0;
            return specialtyMatch;
        }

        return false;
    }

    private static BistroBuilderSupplierCatalogFlags ResolveIngredientCatalogFlag(
        string categorySnapshot,
        string ingredientId)
    {
        string text = NormalizeText((categorySnapshot ?? string.Empty) + " " + (ingredientId ?? string.Empty));
        if (ContainsAny(text, "fruta", "verdura", "hortaliza", "patata", "limon", "cebolla", "ajo"))
            return BistroBuilderSupplierCatalogFlags.FrutasYVerduras;
        if (ContainsAny(text, "carne", "carnes", "chorizo", "panceta", "morcilla"))
            return BistroBuilderSupplierCatalogFlags.Carnes;
        if (ContainsAny(text, "pesc", "marisc", "merluza"))
            return BistroBuilderSupplierCatalogFlags.PescadosYMariscos;
        if (ContainsAny(text, "lact", "queso", "nata", "leche", "mantequilla"))
            return BistroBuilderSupplierCatalogFlags.Lacteos;
        if (ContainsAny(text, "pan", "panader", "harina"))
            return BistroBuilderSupplierCatalogFlags.Panaderia;
        if (ContainsAny(text, "bebida", "refresco", "vino", "agua"))
            return BistroBuilderSupplierCatalogFlags.Bebidas;
        if (ContainsAny(text, "aceite", "condimento", "sal", "especia", "salsa", "aceituna"))
            return BistroBuilderSupplierCatalogFlags.AceitesYCondimentos;
        if (ContainsAny(text, "seco", "despensa", "pasta", "arroz", "azucar", "legumbre", "fabes", "galleta"))
            return BistroBuilderSupplierCatalogFlags.Secos;
        return BistroBuilderSupplierCatalogFlags.Otros;
    }

    private static string NormalizeText(string value)
    {
        string normalized = (value ?? string.Empty).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);
        for (int index = 0; index < normalized.Length; index++)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(normalized[index]);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(normalized[index]);
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        for (int index = 0; index < terms.Length; index++)
        {
            if (text.Contains(terms[index]))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasSingleFlag(BistroBuilderSupplierCatalogFlags flags)
    {
        int value = (int)flags;
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static int ResolveMarketReviewInterval(BistroBuilderSupplierMarketSnapshot marketSnapshot)
    {
        if (marketSnapshot.nextReviewGameDay > marketSnapshot.lastReviewGameDay &&
            marketSnapshot.lastReviewGameDay > 0)
        {
            return Math.Max(1, marketSnapshot.nextReviewGameDay - marketSnapshot.lastReviewGameDay);
        }
        return 5;
    }

    private static double ResolveCampaignChance(
        BistroBuilderSupplierPromotionFrequency frequency,
        BistroBuilderSupplierCommercialIntelligenceSettings settings)
    {
        switch (frequency)
        {
            case BistroBuilderSupplierPromotionFrequency.MuyBaja:
                return settings.VeryLowCampaignChance;
            case BistroBuilderSupplierPromotionFrequency.Baja:
                return settings.LowCampaignChance;
            case BistroBuilderSupplierPromotionFrequency.Alta:
                return settings.HighCampaignChance;
            default:
                return settings.MediumCampaignChance;
        }
    }

    private static int ResolveCooldownReviews(
        BistroBuilderSupplierPromotionFrequency frequency,
        BistroBuilderSupplierCommercialIntelligenceSettings settings)
    {
        switch (frequency)
        {
            case BistroBuilderSupplierPromotionFrequency.MuyBaja:
                return settings.VeryLowCooldownReviews;
            case BistroBuilderSupplierPromotionFrequency.Baja:
                return settings.LowCooldownReviews;
            case BistroBuilderSupplierPromotionFrequency.Alta:
                return settings.HighCooldownReviews;
            default:
                return settings.MediumCooldownReviews;
        }
    }

    private static int ResolveMaximumOffersPerCampaign(
        BistroBuilderSupplierPromotionFrequency frequency,
        BistroBuilderSupplierCommercialIntelligenceSettings settings)
    {
        switch (frequency)
        {
            case BistroBuilderSupplierPromotionFrequency.MuyBaja:
                return settings.VeryLowMaximumOffersPerCampaign;
            case BistroBuilderSupplierPromotionFrequency.Baja:
                return settings.LowMaximumOffersPerCampaign;
            case BistroBuilderSupplierPromotionFrequency.Alta:
                return settings.HighMaximumOffersPerCampaign;
            default:
                return settings.MediumMaximumOffersPerCampaign;
        }
    }

    private static int ResolveCampaignOfferCount(
        ulong seed,
        int reviewDay,
        string supplierId,
        int maximum)
    {
        if (maximum <= 1)
        {
            return Math.Max(0, maximum);
        }

        double roll = Random01(seed, reviewDay, supplierId, "campaign-size");
        if (maximum >= 3 && roll > 0.82)
        {
            return 3;
        }
        if (maximum >= 2 && roll > 0.45)
        {
            return 2;
        }
        return 1;
    }

    private static int CountActiveForSupplier(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        string supplierId,
        int gameDay)
    {
        int count = 0;
        for (int index = 0; index < snapshot.activePromotions.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = snapshot.activePromotions[index];
            if (promotion != null && promotion.IsActiveOnDay(gameDay) &&
                string.Equals(promotion.supplierId, supplierId, StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    private static BistroBuilderSupplierPromotionRecord FindActivePromotion(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        string supplierOfferId,
        int gameDay)
    {
        if (snapshot == null || snapshot.activePromotions == null)
        {
            return null;
        }

        for (int index = 0; index < snapshot.activePromotions.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = snapshot.activePromotions[index];
            if (promotion != null &&
                string.Equals(promotion.supplierOfferId, supplierOfferId, StringComparison.Ordinal) &&
                promotion.IsActiveOnDay(gameDay))
            {
                return promotion;
            }
        }
        return null;
    }

    private static bool WasRecentlyPromoted(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        string supplierOfferId,
        int reviewDay,
        int cooldownDays)
    {
        if (cooldownDays <= 0 || snapshot.promotionHistory == null)
        {
            return false;
        }

        int latestEnd = 0;
        for (int index = 0; index < snapshot.promotionHistory.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = snapshot.promotionHistory[index];
            if (promotion != null &&
                string.Equals(promotion.supplierOfferId, supplierOfferId, StringComparison.Ordinal))
            {
                latestEnd = Math.Max(latestEnd, promotion.endGameDayExclusive);
            }
        }
        return latestEnd > 0 && reviewDay - latestEnd < cooldownDays;
    }

    private static Dictionary<string, BistroBuilderSupplierMarketOfferState> BuildMarketIndex(
        BistroBuilderSupplierMarketSnapshot marketSnapshot)
    {
        Dictionary<string, BistroBuilderSupplierMarketOfferState> result =
            new Dictionary<string, BistroBuilderSupplierMarketOfferState>(StringComparer.Ordinal);
        if (marketSnapshot.offerStates == null)
        {
            return result;
        }
        for (int index = 0; index < marketSnapshot.offerStates.Count; index++)
        {
            BistroBuilderSupplierMarketOfferState state = marketSnapshot.offerStates[index];
            if (state != null && !string.IsNullOrWhiteSpace(state.supplierOfferId))
            {
                result[state.supplierOfferId] = state;
            }
        }
        return result;
    }

    private static Dictionary<string, BistroBuilderIngredientAuthoringRecord> BuildIngredientIndex(
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase)
    {
        Dictionary<string, BistroBuilderIngredientAuthoringRecord> result =
            new Dictionary<string, BistroBuilderIngredientAuthoringRecord>(StringComparer.Ordinal);
        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredients = ingredientDatabase.Ingredients;
        for (int index = 0; index < ingredients.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients[index];
            if (ingredient != null && ingredient.isActive && !string.IsNullOrWhiteSpace(ingredient.IngredientId))
            {
                result[ingredient.IngredientId] = ingredient;
            }
        }
        return result;
    }

    private static Dictionary<string, BistroBuilderSupplierCommercialCampaignState> BuildCampaignIndex(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot)
    {
        Dictionary<string, BistroBuilderSupplierCommercialCampaignState> result =
            new Dictionary<string, BistroBuilderSupplierCommercialCampaignState>(StringComparer.Ordinal);
        if (snapshot.campaignStates == null)
        {
            snapshot.campaignStates = new List<BistroBuilderSupplierCommercialCampaignState>();
        }
        for (int index = 0; index < snapshot.campaignStates.Count; index++)
        {
            BistroBuilderSupplierCommercialCampaignState state = snapshot.campaignStates[index];
            if (state != null && !string.IsNullOrWhiteSpace(state.supplierId))
            {
                result[state.supplierId] = state;
            }
        }
        return result;
    }

    private static bool ValidatePromotion(
        BistroBuilderSupplierPromotionRecord promotion,
        Dictionary<string, BistroBuilderSupplierMarketOfferState> marketByOffer,
        HashSet<string> promotionIds,
        out string error)
    {
        error = null;
        if (promotion == null)
        {
            error = "Existe una promoción nula.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(promotion.promotionId) || !promotionIds.Add(promotion.promotionId))
        {
            error = "PromotionId vacío o duplicado.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(promotion.supplierOfferId) ||
            !marketByOffer.ContainsKey(promotion.supplierOfferId))
        {
            error = "La promoción referencia una oferta que no existe en el mercado: " +
                promotion.supplierOfferId;
            return false;
        }
        if (promotion.discountBasisPoints <= 0 || promotion.discountBasisPoints >= 10000)
        {
            error = "Descuento inválido en " + promotion.promotionId + ".";
            return false;
        }
        if (promotion.referenceMarketPriceCents <= 0 || promotion.promotionalPriceCents <= 0 ||
            promotion.promotionalPriceCents >= promotion.referenceMarketPriceCents)
        {
            error = "Precio promocional inválido en " + promotion.promotionId + ".";
            return false;
        }
        if (promotion.startGameDay < 1 || promotion.endGameDayExclusive <= promotion.startGameDay)
        {
            error = "Duración promocional inválida en " + promotion.promotionId + ".";
            return false;
        }
        return true;
    }


    private static int FindLastPriceChangeDay(
        BistroBuilderSupplierMarketSnapshot snapshot,
        string offerId)
    {
        int day = 0;
        for (int index = 0; index < snapshot.changes.Count; index++)
        {
            BistroBuilderSupplierMarketChangeRecord change = snapshot.changes[index];
            if (change == null || change.supplierOfferId != offerId) continue;
            if (change.changeKind == BistroBuilderSupplierMarketChangeKind.Price ||
                change.changeKind == BistroBuilderSupplierMarketChangeKind.PriceAndAvailability)
            {
                day = Math.Max(day, change.gameDay);
            }
        }
        return day;
    }

    private static int FindLastAvailabilityChangeDay(
        BistroBuilderSupplierMarketSnapshot snapshot,
        string offerId)
    {
        int day = 0;
        for (int index = 0; index < snapshot.changes.Count; index++)
        {
            BistroBuilderSupplierMarketChangeRecord change = snapshot.changes[index];
            if (change == null || change.supplierOfferId != offerId) continue;
            if (change.changeKind == BistroBuilderSupplierMarketChangeKind.Availability ||
                change.changeKind == BistroBuilderSupplierMarketChangeKind.PriceAndAvailability)
            {
                day = Math.Max(day, change.gameDay);
            }
        }
        return day;
    }

    private static void TrimHistory(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        BistroBuilderSupplierCommercialIntelligenceSettings settings)
    {
        int maxPromotions = Math.Max(16, settings.MaximumPromotionHistoryEntries);
        int excessPromotions = snapshot.promotionHistory.Count - maxPromotions;
        if (excessPromotions > 0)
        {
            snapshot.promotionHistory.RemoveRange(0, excessPromotions);
        }

        int maxReviews = Math.Max(8, settings.MaximumReviewHistoryEntries);
        int excessReviews = snapshot.reviews.Count - maxReviews;
        if (excessReviews > 0)
        {
            snapshot.reviews.RemoveRange(0, excessReviews);
        }
    }

    private static void MixPromotionList(
        ref ulong hash,
        List<BistroBuilderSupplierPromotionRecord> promotions)
    {
        for (int index = 0; index < promotions.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = promotions[index];
            if (promotion == null)
            {
                Mix(ref hash, "<null>");
                continue;
            }
            Mix(ref hash, promotion.promotionId);
            Mix(ref hash, promotion.supplierOfferId);
            Mix(ref hash, promotion.startGameDay.ToString(CultureInfo.InvariantCulture));
            Mix(ref hash, promotion.endGameDayExclusive.ToString(CultureInfo.InvariantCulture));
            Mix(ref hash, promotion.discountBasisPoints.ToString(CultureInfo.InvariantCulture));
            Mix(ref hash, promotion.referenceMarketPriceCents.ToString(CultureInfo.InvariantCulture));
            Mix(ref hash, promotion.promotionalPriceCents.ToString(CultureInfo.InvariantCulture));
            Mix(ref hash, ((int)promotion.sourceAvailabilityAtStart).ToString(CultureInfo.InvariantCulture));
            Mix(ref hash, ((int)promotion.lifecycle).ToString(CultureInfo.InvariantCulture));
            Mix(ref hash, promotion.reasonCode);
        }
    }

    private static double Random01(ulong seed, int day, string identity, string channel)
    {
        ulong hash = seed == 0UL ? 1UL : seed;
        Mix(ref hash, day.ToString(CultureInfo.InvariantCulture));
        Mix(ref hash, identity);
        Mix(ref hash, channel);
        hash ^= hash >> 12;
        hash ^= hash << 25;
        hash ^= hash >> 27;
        hash *= 2685821657736338717UL;
        return (hash >> 11) * (1.0 / 9007199254740992.0);
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

    private static double Clamp01(double value)
    {
        if (value < 0.0) return 0.0;
        if (value > 1.0) return 1.0;
        return value;
    }
}
