using System;
using System.Collections.Generic;
using System.Linq;

public static class BistroBuilderSupplierSmartPurchaseEngine
{
    private sealed class Weights
    {
        public float cost;
        public float speed;
        public float reliability;
        public float waste;
        public float stockout;
        public float targetDays;
    }

    public static BistroBuilderSmartPurchaseReport BuildReport(
        int gameDay,
        long sequence,
        IReadOnlyList<BistroBuilderSmartPurchaseIngredientFact> ingredientFacts,
        IReadOnlyList<BistroBuilderSmartPurchaseOfferFact> offers,
        BistroBuilderSupplierSmartPurchaseSettings settings,
        IList<string> diagnostics = null)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (ingredientFacts == null) throw new ArgumentNullException(nameof(ingredientFacts));
        if (offers == null) throw new ArgumentNullException(nameof(offers));

        BistroBuilderSmartPurchaseReport report = new BistroBuilderSmartPurchaseReport
        {
            gameDay = Math.Max(1, gameDay),
            generatedSequence = Math.Max(1L, sequence),
            canonicalIngredientCount = ingredientFacts.Count,
            ingredientFactsResolved = ingredientFacts.Count(x => x != null && x.inventoryResolved),
            inventoryResolved = ingredientFacts.Count > 0 && ingredientFacts.All(x => x != null && x.inventoryResolved),
            forecastResolved = ingredientFacts.Count > 0 && ingredientFacts.All(x => x != null && x.forecastResolved),
            policyResolved = ingredientFacts.Count > 0 && ingredientFacts.All(x => x != null && x.policyResolved),
            offersEvaluated = offers.Count
        };
        if (diagnostics != null) report.diagnostics.AddRange(diagnostics);

        report.plans.Add(BuildPlan(BistroBuilderSmartPurchaseStrategy.Ahorrar, report.gameDay, ingredientFacts, offers, settings));
        report.plans.Add(BuildPlan(BistroBuilderSmartPurchaseStrategy.Equilibrado, report.gameDay, ingredientFacts, offers, settings));
        report.plans.Add(BuildPlan(BistroBuilderSmartPurchaseStrategy.Urgente, report.gameDay, ingredientFacts, offers, settings));

        ChooseRecommendedStrategy(report);
        return report;
    }

    private static BistroBuilderSmartPurchasePlan BuildPlan(
        BistroBuilderSmartPurchaseStrategy strategy,
        int gameDay,
        IReadOnlyList<BistroBuilderSmartPurchaseIngredientFact> facts,
        IReadOnlyList<BistroBuilderSmartPurchaseOfferFact> offers,
        BistroBuilderSupplierSmartPurchaseSettings settings)
    {
        Weights weights = ResolveWeights(strategy, settings);
        BistroBuilderSmartPurchasePlan plan = new BistroBuilderSmartPurchasePlan { strategy = strategy };

        for (int i = 0; i < facts.Count; i++)
        {
            BistroBuilderSmartPurchaseIngredientFact fact = facts[i];
            if (fact == null || string.IsNullOrWhiteSpace(fact.ingredientId) || !fact.inventoryResolved) continue;
            plan.ingredientsEvaluated++;

            BistroBuilderSmartPurchaseRisk risk = EvaluateRisk(fact, settings);
            if (risk == BistroBuilderSmartPurchaseRisk.Critico) plan.criticalIngredients++;
            if (risk == BistroBuilderSmartPurchaseRisk.Alto) plan.highRiskIngredients++;

            List<BistroBuilderSmartPurchaseCandidate> candidates = new List<BistroBuilderSmartPurchaseCandidate>();
            for (int j = 0; j < offers.Count; j++)
            {
                BistroBuilderSmartPurchaseOfferFact offer = offers[j];
                if (offer == null || !offer.availableForNewOrders || offer.availability == BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado) continue;
                if (!string.Equals(offer.ingredientId, fact.ingredientId, StringComparison.Ordinal)) continue;
                BistroBuilderSmartPurchaseCandidate candidate = EvaluateCandidate(gameDay, fact, offer, risk, strategy, weights, settings);
                if (candidate != null) candidates.Add(candidate);
            }

            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            if (candidates.Count == 0) continue;

            BistroBuilderSmartPurchaseCandidate selected = candidates[0];
            if (selected.shortageBeforePurchaseMicrounits <= 0 && risk <= BistroBuilderSmartPurchaseRisk.Bajo) continue;

            BistroBuilderSmartPurchaseIngredientRecommendation rec = new BistroBuilderSmartPurchaseIngredientRecommendation
            {
                ingredientId = fact.ingredientId,
                ingredientDisplayName = fact.displayName,
                currentAvailableMicrounits = fact.availableMicrounits,
                forecastDailyConsumptionMicrounits = fact.forecastDailyConsumptionMicrounits,
                incomingMicrounits = fact.incomingMicrounits,
                currentRisk = risk,
                selected = selected.DeepClone()
            };
            int altCount = Math.Min(3, candidates.Count);
            for (int a = 0; a < altCount; a++) rec.alternatives.Add(candidates[a].DeepClone());
            rec.reasons.AddRange(selected.reasons);
            plan.ingredients.Add(rec);
        }

        int deferredByMinimum = RepairMinimumOrderGaps(plan, gameDay, facts, offers, settings, strategy);
        RebuildPlanTotals(plan, offers, settings, strategy);
        if (deferredByMinimum > 0)
            plan.summaryReasons.Add(deferredByMinimum + " necesidad(es) de riesgo bajo se han aplazado porque forzar el pedido mínimo provocaría una compra desproporcionada; se reevaluarán en la siguiente recomendación.");
        AddPlanReasons(plan, weights.targetDays);
        return plan;
    }

    private static BistroBuilderSmartPurchaseCandidate EvaluateCandidate(
        int gameDay,
        BistroBuilderSmartPurchaseIngredientFact fact,
        BistroBuilderSmartPurchaseOfferFact offer,
        BistroBuilderSmartPurchaseRisk risk,
        BistroBuilderSmartPurchaseStrategy strategy,
        Weights w,
        BistroBuilderSupplierSmartPurchaseSettings settings)
    {
        if (offer.packageNetQuantityMicrounits <= 0L || offer.effectiveUnitPriceCents <= 0L) return null;
        double arrivalDays = Math.Max(0.0, offer.leadTimeGameHours / 24.0);
        long consumptionUntilArrival = SafeRound(fact.forecastDailyConsumptionMicrounits * arrivalDays);
        long incomingBeforeArrival = fact.incomingMicrounits > 0L && fact.earliestIncomingGameDay > 0 && fact.earliestIncomingGameDay <= gameDay + Math.Ceiling(arrivalDays)
            ? fact.incomingMicrounits : 0L;
        long projected = Math.Max(0L, fact.availableMicrounits + incomingBeforeArrival - consumptionUntilArrival);
        long targetByForecast = SafeRound(fact.forecastDailyConsumptionMicrounits * Math.Max(0.5f, w.targetDays));
        long target = Math.Max(Math.Max(0L, fact.minimumStockMicrounits), targetByForecast);
        if (target <= 0L && fact.forecastDailyConsumptionMicrounits <= 0L) return null;
        long shortage = Math.Max(0L, target - projected);
        if (shortage <= 0L && risk <= BistroBuilderSmartPurchaseRisk.Bajo) return null;

        int packages = RoundPackages(shortage, offer.packageNetQuantityMicrounits, offer.minimumPackageCount, offer.orderIncrement);
        if (packages <= 0) packages = Math.Max(1, offer.minimumPackageCount);
        long purchased = SafeMultiply(offer.packageNetQuantityMicrounits, packages);
        long post = projected + purchased;
        long overstock = Math.Max(0L, post - target);
        float wasteRisk = ComputeWasteRisk(fact, purchased, target, overstock, gameDay, settings);
        long subtotal = SafeMultiply(offer.effectiveUnitPriceCents, packages);
        long shipping = EstimateShipping(subtotal, offer);
        long total = SafeAdd(subtotal, shipping);
        // El coste normalizado representa el producto comprado, sin portes de una línea aislada.
        // Los portes reales se consolidan posteriormente por proveedor a nivel de plan.
        long norm = purchased > 0L ? SafeRound((double)subtotal * 1000000.0 / purchased) : long.MaxValue;

        float costQuality = 1f / (1f + (float)Math.Max(0L, norm) / 1000f);
        float speedQuality = 1f / (1f + Math.Max(0f, offer.leadTimeGameHours) / 24f);
        float reliabilityQuality = Clamp01(offer.reliability01);
        float wasteQuality = 1f - Clamp01(wasteRisk);
        float stockoutQuality = 1f - Risk01(risk);
        float availabilityPenalty = offer.availability == BistroBuilderSupplierOfferAvailability.StockLimitado ? settings.limitedAvailabilityPenalty : 0f;

        float score = 100f * WeightedAverage(
            costQuality, w.cost,
            speedQuality, w.speed,
            reliabilityQuality, w.reliability,
            wasteQuality, w.waste,
            stockoutQuality, w.stockout);
        // El pedido mínimo no se penaliza por línea: varias líneas del mismo proveedor pueden
        // alcanzar conjuntamente el mínimo. La penalización correcta se aplica a la cesta consolidada.
        score -= availabilityPenalty + settings.overstockPenaltyScale * wasteRisk;

        BistroBuilderSmartPurchaseCandidate c = new BistroBuilderSmartPurchaseCandidate
        {
            supplierOfferId = offer.supplierOfferId,
            supplierId = offer.supplierId,
            supplierDisplayName = offer.supplierDisplayName,
            ingredientId = offer.ingredientId,
            packageFormatId = offer.packageFormatId,
            packageDisplayName = offer.packageDisplayName,
            packageCount = packages,
            purchasedMicrounits = purchased,
            projectedAvailableAtArrivalMicrounits = projected,
            targetStockMicrounits = target,
            shortageBeforePurchaseMicrounits = shortage,
            projectedOverstockMicrounits = overstock,
            lineSubtotalCents = subtotal,
            estimatedShippingCents = shipping,
            estimatedTotalCents = total,
            normalizedCostPerMillionMicrounitsCents = norm,
            leadTimeGameHours = offer.leadTimeGameHours,
            reliability01 = offer.reliability01,
            availability = offer.availability,
            hasPromotion = offer.hasPromotion,
            discountBasisPoints = offer.discountBasisPoints,
            stockoutRisk = risk,
            wasteRisk01 = wasteRisk,
            score = score
        };
        BuildReasons(c, fact, offer, strategy);
        return c;
    }


    private static int RepairMinimumOrderGaps(
        BistroBuilderSmartPurchasePlan plan,
        int gameDay,
        IReadOnlyList<BistroBuilderSmartPurchaseIngredientFact> facts,
        IReadOnlyList<BistroBuilderSmartPurchaseOfferFact> offers,
        BistroBuilderSupplierSmartPurchaseSettings settings,
        BistroBuilderSmartPurchaseStrategy strategy)
    {
        int deferred = 0;
        int guard = Math.Max(4, plan.ingredients.Count * 4);
        for (int iteration = 0; iteration < guard; iteration++)
        {
            Dictionary<string, BistroBuilderSmartPurchaseSupplierBasket> current = BuildBasketMap(plan.ingredients, offers);
            BistroBuilderSmartPurchaseSupplierBasket gapBasket = current.Values
                .Where(x => x != null && !x.meetsMinimumOrder)
                .OrderByDescending(x => Math.Max(0L, x.minimumOrderCents - x.subtotalCents))
                .FirstOrDefault();
            if (gapBasket == null) break;

            if (TryMoveLineToReduceMinimumGap(plan, gapBasket.supplierId, offers))
                continue;
            if (TryTopUpBasketToMinimum(plan, gapBasket, gameDay, facts, offers, settings, strategy))
                continue;
            if (TryDeferLowRiskLine(plan, gapBasket.supplierId))
            {
                deferred++;
                continue;
            }
            break;
        }
        return deferred;
    }

    private static bool TryMoveLineToReduceMinimumGap(
        BistroBuilderSmartPurchasePlan plan,
        string sourceSupplierId,
        IReadOnlyList<BistroBuilderSmartPurchaseOfferFact> offers)
    {
        Dictionary<string, BistroBuilderSmartPurchaseSupplierBasket> before = BuildBasketMap(plan.ingredients, offers);
        int beforeInvalid; long beforeGap; MeasureMinimumGaps(before, out beforeInvalid, out beforeGap);
        BistroBuilderSmartPurchaseIngredientRecommendation bestRec = null;
        BistroBuilderSmartPurchaseCandidate bestCandidate = null;
        int bestInvalid = beforeInvalid;
        long bestGap = beforeGap;
        float bestScore = float.MinValue;

        for (int i = 0; i < plan.ingredients.Count; i++)
        {
            BistroBuilderSmartPurchaseIngredientRecommendation rec = plan.ingredients[i];
            if (rec == null || rec.selected == null || rec.alternatives == null ||
                !string.Equals(rec.selected.supplierId, sourceSupplierId, StringComparison.Ordinal)) continue;
            BistroBuilderSmartPurchaseCandidate original = rec.selected;
            for (int a = 0; a < rec.alternatives.Count; a++)
            {
                BistroBuilderSmartPurchaseCandidate alt = rec.alternatives[a];
                if (alt == null || string.Equals(alt.supplierId, sourceSupplierId, StringComparison.Ordinal) || alt.packageCount <= 0) continue;
                rec.selected = alt;
                Dictionary<string, BistroBuilderSmartPurchaseSupplierBasket> after = BuildBasketMap(plan.ingredients, offers);
                int invalid; long gap; MeasureMinimumGaps(after, out invalid, out gap);
                bool improves = invalid < bestInvalid || (invalid == bestInvalid && gap < bestGap) ||
                    (invalid == bestInvalid && gap == bestGap && alt.score > bestScore);
                if (improves && (invalid < beforeInvalid || gap < beforeGap))
                {
                    bestRec = rec;
                    bestCandidate = alt.DeepClone();
                    bestInvalid = invalid;
                    bestGap = gap;
                    bestScore = alt.score;
                }
                rec.selected = original;
            }
        }

        if (bestRec == null || bestCandidate == null) return false;
        bestCandidate.reasonCodes.Add("minimum_order_consolidation");
        bestCandidate.reasons.Add("Se elige esta alternativa para consolidar la cesta y respetar el pedido mínimo del proveedor.");
        bestRec.selected = bestCandidate;
        bestRec.reasons = new List<string>(bestCandidate.reasons);
        return true;
    }

    private static bool TryTopUpBasketToMinimum(
        BistroBuilderSmartPurchasePlan plan,
        BistroBuilderSmartPurchaseSupplierBasket basket,
        int gameDay,
        IReadOnlyList<BistroBuilderSmartPurchaseIngredientFact> facts,
        IReadOnlyList<BistroBuilderSmartPurchaseOfferFact> offers,
        BistroBuilderSupplierSmartPurchaseSettings settings,
        BistroBuilderSmartPurchaseStrategy strategy)
    {
        long gap = Math.Max(0L, basket.minimumOrderCents - basket.subtotalCents);
        if (gap <= 0L) return false;
        BistroBuilderSmartPurchaseIngredientRecommendation bestRec = null;
        BistroBuilderSmartPurchaseCandidate best = null;
        long bestExtra = long.MaxValue;

        for (int i = 0; i < plan.ingredients.Count; i++)
        {
            BistroBuilderSmartPurchaseIngredientRecommendation rec = plan.ingredients[i];
            BistroBuilderSmartPurchaseCandidate c = rec != null ? rec.selected : null;
            if (c == null || !string.Equals(c.supplierId, basket.supplierId, StringComparison.Ordinal)) continue;
            BistroBuilderSmartPurchaseOfferFact offer = FindOffer(offers, c.supplierOfferId);
            BistroBuilderSmartPurchaseIngredientFact fact = facts.FirstOrDefault(x => x != null && x.ingredientId == c.ingredientId);
            if (offer == null || fact == null || offer.effectiveUnitPriceCents <= 0L || offer.packageNetQuantityMicrounits <= 0L) continue;

            int step = Math.Max(1, offer.orderIncrement);
            long neededPackages = (gap + offer.effectiveUnitPriceCents - 1L) / offer.effectiveUnitPriceCents;
            long add = Math.Max(step, neededPackages);
            long rem = add % step;
            if (rem != 0L) add += step - rem;
            if (add > int.MaxValue - c.packageCount) continue;
            int newCount = c.packageCount + (int)add;
            float multiplier = c.packageCount > 0 ? (float)newCount / c.packageCount : float.MaxValue;
            float maxMultiplier = strategy == BistroBuilderSmartPurchaseStrategy.Ahorrar ? 2.5f :
                (strategy == BistroBuilderSmartPurchaseStrategy.Equilibrado ? 2.0f : 1.5f);
            if (rec.currentRisk >= BistroBuilderSmartPurchaseRisk.Alto) maxMultiplier += 1.5f;
            if (multiplier > maxMultiplier) continue;

            long purchased = SafeMultiply(offer.packageNetQuantityMicrounits, newCount);
            long subtotal = SafeMultiply(offer.effectiveUnitPriceCents, newCount);
            long overstock = Math.Max(0L, SafeAdd(c.projectedAvailableAtArrivalMicrounits, purchased) - c.targetStockMicrounits);
            float waste = ComputeWasteRisk(fact, purchased, c.targetStockMicrounits, overstock, gameDay, settings);
            float wasteLimit = strategy == BistroBuilderSmartPurchaseStrategy.Ahorrar ? 0.55f :
                (strategy == BistroBuilderSmartPurchaseStrategy.Equilibrado ? 0.50f : 0.35f);
            if (rec.currentRisk >= BistroBuilderSmartPurchaseRisk.Alto) wasteLimit = Math.Max(wasteLimit, 0.80f);
            if (waste > wasteLimit) continue;

            long extra = subtotal - c.lineSubtotalCents;
            if (extra < 0L || extra >= bestExtra) continue;
            BistroBuilderSmartPurchaseCandidate adjusted = c.DeepClone();
            adjusted.packageCount = newCount;
            adjusted.purchasedMicrounits = purchased;
            adjusted.projectedOverstockMicrounits = overstock;
            adjusted.wasteRisk01 = waste;
            adjusted.lineSubtotalCents = subtotal;
            adjusted.estimatedShippingCents = EstimateShipping(subtotal, offer);
            adjusted.estimatedTotalCents = SafeAdd(subtotal, adjusted.estimatedShippingCents);
            adjusted.normalizedCostPerMillionMicrounitsCents = purchased > 0L ? SafeRound((double)subtotal * 1000000.0 / purchased) : long.MaxValue;
            adjusted.reasonCodes.Add("minimum_order_topup");
            adjusted.reasons.Add("Se ajusta el número de paquetes para alcanzar el pedido mínimo sin introducir un sobrestock desproporcionado.");
            bestRec = rec; best = adjusted; bestExtra = extra;
        }

        if (bestRec == null || best == null) return false;
        bestRec.selected = best;
        bestRec.reasons = new List<string>(best.reasons);
        return true;
    }

    private static bool TryDeferLowRiskLine(BistroBuilderSmartPurchasePlan plan, string supplierId)
    {
        int bestIndex = -1;
        float bestWaste = float.MinValue;
        for (int i = 0; i < plan.ingredients.Count; i++)
        {
            BistroBuilderSmartPurchaseIngredientRecommendation rec = plan.ingredients[i];
            if (rec == null || rec.selected == null || !string.Equals(rec.selected.supplierId, supplierId, StringComparison.Ordinal)) continue;
            if (rec.currentRisk > BistroBuilderSmartPurchaseRisk.Bajo) continue;
            if (rec.selected.wasteRisk01 >= bestWaste)
            {
                bestWaste = rec.selected.wasteRisk01;
                bestIndex = i;
            }
        }
        if (bestIndex < 0) return false;
        plan.ingredients.RemoveAt(bestIndex);
        return true;
    }

    private static Dictionary<string, BistroBuilderSmartPurchaseSupplierBasket> BuildBasketMap(
        IReadOnlyList<BistroBuilderSmartPurchaseIngredientRecommendation> recommendations,
        IReadOnlyList<BistroBuilderSmartPurchaseOfferFact> offers)
    {
        Dictionary<string, BistroBuilderSmartPurchaseSupplierBasket> baskets = new Dictionary<string, BistroBuilderSmartPurchaseSupplierBasket>(StringComparer.Ordinal);
        for (int i = 0; i < recommendations.Count; i++)
        {
            BistroBuilderSmartPurchaseCandidate selected = recommendations[i] != null ? recommendations[i].selected : null;
            if (selected == null || string.IsNullOrWhiteSpace(selected.supplierId)) continue;
            BistroBuilderSmartPurchaseSupplierBasket basket;
            if (!baskets.TryGetValue(selected.supplierId, out basket))
            {
                basket = new BistroBuilderSmartPurchaseSupplierBasket { supplierId = selected.supplierId, supplierDisplayName = selected.supplierDisplayName };
                baskets.Add(selected.supplierId, basket);
            }
            basket.subtotalCents = SafeAdd(basket.subtotalCents, Math.Max(0L, selected.lineSubtotalCents));
            BistroBuilderSmartPurchaseOfferFact offer = FindOffer(offers, selected.supplierOfferId);
            basket.minimumOrderCents = Math.Max(basket.minimumOrderCents, offer != null ? Math.Max(0L, offer.supplierMinimumOrderCents) : 0L);
            basket.lineCount++;
        }
        foreach (BistroBuilderSmartPurchaseSupplierBasket basket in baskets.Values)
        {
            BistroBuilderSmartPurchaseOfferFact reference = offers.FirstOrDefault(x => x != null && x.supplierId == basket.supplierId);
            long shipping = 0L;
            if (reference != null)
            {
                bool free = reference.freeShippingEnabled && reference.freeShippingThresholdCents > 0L && basket.subtotalCents >= reference.freeShippingThresholdCents;
                shipping = free ? 0L : Math.Max(0L, reference.shippingCostCents);
            }
            basket.shippingCents = shipping;
            basket.totalCents = SafeAdd(basket.subtotalCents, shipping);
            basket.meetsMinimumOrder = basket.subtotalCents >= basket.minimumOrderCents;
        }
        return baskets;
    }

    private static void MeasureMinimumGaps(Dictionary<string, BistroBuilderSmartPurchaseSupplierBasket> baskets, out int invalidCount, out long totalGap)
    {
        invalidCount = 0; totalGap = 0L;
        foreach (BistroBuilderSmartPurchaseSupplierBasket basket in baskets.Values)
        {
            if (basket == null || basket.meetsMinimumOrder) continue;
            invalidCount++;
            totalGap = SafeAdd(totalGap, Math.Max(0L, basket.minimumOrderCents - basket.subtotalCents));
        }
    }

    private static void RebuildPlanTotals(
        BistroBuilderSmartPurchasePlan plan,
        IReadOnlyList<BistroBuilderSmartPurchaseOfferFact> offers,
        BistroBuilderSupplierSmartPurchaseSettings settings,
        BistroBuilderSmartPurchaseStrategy strategy)
    {
        plan.suppliers.Clear(); plan.subtotalCents = 0L; plan.shippingCents = 0L; plan.totalCents = 0L;
        plan.containsMinimumOrderGap = false;
        plan.ingredientsRecommended = plan.ingredients.Count;
        Dictionary<string, BistroBuilderSmartPurchaseSupplierBasket> baskets = BuildBasketMap(plan.ingredients, offers);
        foreach (BistroBuilderSmartPurchaseSupplierBasket basket in baskets.Values.OrderBy(x => x.supplierId, StringComparer.Ordinal))
        {
            if (!basket.meetsMinimumOrder) plan.containsMinimumOrderGap = true;
            plan.subtotalCents = SafeAdd(plan.subtotalCents, basket.subtotalCents);
            plan.shippingCents = SafeAdd(plan.shippingCents, basket.shippingCents);
            plan.totalCents = SafeAdd(plan.totalCents, basket.totalCents);
            plan.suppliers.Add(basket);
        }
        plan.supplierCount = plan.suppliers.Count;

        if (plan.ingredientsRecommended <= 0) { plan.score = 0f; return; }
        double scoreSum = 0.0;
        for (int i = 0; i < plan.ingredients.Count; i++)
            if (plan.ingredients[i] != null && plan.ingredients[i].selected != null) scoreSum += plan.ingredients[i].selected.score;
        float baseScore = (float)(scoreSum / plan.ingredientsRecommended);
        float minimumPenalty = plan.containsMinimumOrderGap ? settings.supplierMinimumGapPenalty : 0f;
        float shippingRatio = plan.subtotalCents > 0L ? (float)plan.shippingCents / plan.subtotalCents : 0f;
        float shippingScale = strategy == BistroBuilderSmartPurchaseStrategy.Ahorrar ? 10f :
            (strategy == BistroBuilderSmartPurchaseStrategy.Equilibrado ? 6f : 2f);
        plan.score = baseScore - minimumPenalty - shippingRatio * shippingScale;
    }

    private static void BuildReasons(BistroBuilderSmartPurchaseCandidate c, BistroBuilderSmartPurchaseIngredientFact fact, BistroBuilderSmartPurchaseOfferFact offer, BistroBuilderSmartPurchaseStrategy strategy)
    {
        if (c.stockoutRisk >= BistroBuilderSmartPurchaseRisk.Alto)
        {
            c.reasonCodes.Add("stockout_risk");
            c.reasons.Add("Riesgo de quedarse sin stock antes de la reposición.");
        }
        if (offer.hasPromotion)
        {
            c.reasonCodes.Add("active_promotion");
            c.reasons.Add("La oferta tiene una promoción activa que reduce el coste efectivo.");
        }
        if (offer.reliability01 >= 0.97f)
        {
            c.reasonCodes.Add("high_reliability");
            c.reasons.Add("Proveedor con fiabilidad alta para esta necesidad.");
        }
        if (offer.leadTimeGameHours <= 12f)
        {
            c.reasonCodes.Add("fast_arrival");
            c.reasons.Add("Plazo de llegada corto frente al riesgo actual.");
        }
        if (c.wasteRisk01 >= 0.35f)
        {
            c.reasonCodes.Add("overstock_warning");
            c.reasons.Add("El formato puede generar sobrestock; se penaliza para limitar caducidad.");
        }
        if (strategy == BistroBuilderSmartPurchaseStrategy.Ahorrar)
        {
            c.reasonCodes.Add("saving_strategy");
            c.reasons.Add("Ahorrar prioriza coste normalizado, portes y eficiencia de formato.");
        }
        else if (strategy == BistroBuilderSmartPurchaseStrategy.Urgente)
        {
            c.reasonCodes.Add("urgent_strategy");
            c.reasons.Add("Urgente prioriza velocidad, fiabilidad y prevención de rotura.");
        }
        else
        {
            c.reasonCodes.Add("balanced_strategy");
            c.reasons.Add("Equilibrado pondera coste, cobertura, desperdicio, plazo y fiabilidad.");
        }
    }

    private static void AddPlanReasons(BistroBuilderSmartPurchasePlan plan, float targetCoverageDays)
    {
        if (plan.criticalIngredients > 0)
            plan.summaryReasons.Add(plan.criticalIngredients + " ingrediente(s) presentan riesgo crítico de rotura.");
        if (plan.containsMinimumOrderGap)
            plan.summaryReasons.Add("Algún proveedor no alcanza todavía su pedido mínimo; la UI debe permitir consolidar o cambiar alternativa.");
        plan.summaryReasons.Add("Cobertura objetivo de la estrategia: " + targetCoverageDays.ToString("0.#") + " día(s) de consumo previsto.");
        if (plan.strategy == BistroBuilderSmartPurchaseStrategy.Ahorrar)
            plan.summaryReasons.Add("Ahorrar prioriza coste normalizado, eficiencia de formato y portes; puede requerir más desembolso inicial al comprar mayor cobertura.");
        else if (plan.strategy == BistroBuilderSmartPurchaseStrategy.Urgente)
            plan.summaryReasons.Add("Urgente prioriza continuidad, velocidad y fiabilidad; puede comprar una cobertura inmediata menor y no implica necesariamente mayor desembolso inicial.");
        else
            plan.summaryReasons.Add("Equilibrado busca un compromiso entre coste, cobertura, plazo, fiabilidad y desperdicio.");
    }

    private static void ChooseRecommendedStrategy(BistroBuilderSmartPurchaseReport report)
    {
        BistroBuilderSmartPurchasePlan saving = report.plans.First(x => x.strategy == BistroBuilderSmartPurchaseStrategy.Ahorrar);
        BistroBuilderSmartPurchasePlan balanced = report.plans.First(x => x.strategy == BistroBuilderSmartPurchaseStrategy.Equilibrado);
        BistroBuilderSmartPurchasePlan urgent = report.plans.First(x => x.strategy == BistroBuilderSmartPurchaseStrategy.Urgente);
        int critical = Math.Max(saving.criticalIngredients, Math.Max(balanced.criticalIngredients, urgent.criticalIngredients));
        int high = Math.Max(saving.highRiskIngredients, Math.Max(balanced.highRiskIngredients, urgent.highRiskIngredients));
        if (critical > 0 || high >= 2)
        {
            report.recommendedStrategy = BistroBuilderSmartPurchaseStrategy.Urgente;
            report.recommendedReason = "Hay riesgo de rotura relevante: se priorizan plazo y fiabilidad.";
            return;
        }
        if (!saving.containsMinimumOrderGap && HasClearNormalizedSavingAdvantage(saving, balanced, 0.90f))
        {
            report.recommendedStrategy = BistroBuilderSmartPurchaseStrategy.Ahorrar;
            report.recommendedReason = "El riesgo es controlado y Ahorrar ofrece una ventaja clara de coste normalizado, aunque su mayor cobertura pueda exigir más desembolso inicial.";
            return;
        }
        report.recommendedStrategy = BistroBuilderSmartPurchaseStrategy.Equilibrado;
        report.recommendedReason = "No hay una emergencia dominante ni un ahorro suficientemente grande para sacrificar equilibrio.";
    }

    private static bool HasClearNormalizedSavingAdvantage(
        BistroBuilderSmartPurchasePlan saving,
        BistroBuilderSmartPurchasePlan balanced,
        float thresholdRatio)
    {
        if (saving == null || balanced == null || saving.ingredients == null || balanced.ingredients == null) return false;
        double ratioSum = 0.0;
        int comparable = 0;
        for (int i = 0; i < saving.ingredients.Count; i++)
        {
            BistroBuilderSmartPurchaseIngredientRecommendation s = saving.ingredients[i];
            if (s == null || s.selected == null || s.selected.normalizedCostPerMillionMicrounitsCents <= 0L) continue;
            BistroBuilderSmartPurchaseIngredientRecommendation b = balanced.ingredients.FirstOrDefault(x => x != null && x.ingredientId == s.ingredientId);
            if (b == null || b.selected == null || b.selected.normalizedCostPerMillionMicrounitsCents <= 0L) continue;
            ratioSum += (double)s.selected.normalizedCostPerMillionMicrounitsCents / b.selected.normalizedCostPerMillionMicrounitsCents;
            comparable++;
        }
        return comparable > 0 && (ratioSum / comparable) <= Math.Max(0.01f, thresholdRatio);
    }

    public static BistroBuilderSmartPurchaseRisk EvaluateRisk(BistroBuilderSmartPurchaseIngredientFact fact, BistroBuilderSupplierSmartPurchaseSettings settings)
    {
        if (fact == null || !fact.inventoryResolved) return BistroBuilderSmartPurchaseRisk.SinRiesgo;
        if (fact.availableMicrounits <= 0L) return BistroBuilderSmartPurchaseRisk.Critico;
        if (fact.forecastDailyConsumptionMicrounits <= 0L)
            return fact.availableMicrounits < fact.minimumStockMicrounits ? BistroBuilderSmartPurchaseRisk.Medio : BistroBuilderSmartPurchaseRisk.SinRiesgo;
        double hours = (double)fact.availableMicrounits / fact.forecastDailyConsumptionMicrounits * 24.0;
        if (hours <= settings.criticalStockoutHours) return BistroBuilderSmartPurchaseRisk.Critico;
        if (hours <= settings.highStockoutHours) return BistroBuilderSmartPurchaseRisk.Alto;
        if (fact.availableMicrounits < fact.minimumStockMicrounits) return BistroBuilderSmartPurchaseRisk.Medio;
        if (hours <= 96.0) return BistroBuilderSmartPurchaseRisk.Bajo;
        return BistroBuilderSmartPurchaseRisk.SinRiesgo;
    }

    private static float ComputeWasteRisk(BistroBuilderSmartPurchaseIngredientFact fact, long purchased, long target, long overstock, int gameDay, BistroBuilderSupplierSmartPurchaseSettings settings)
    {
        double overstockRatio = target > 0L ? (double)overstock / target : (purchased > 0L ? 1.0 : 0.0);
        double expiringRatio = fact.availableMicrounits > 0L ? (double)Math.Max(0L, fact.expiringSoonMicrounits) / fact.availableMicrounits : 0.0;
        double expiryUrgency = fact.earliestExpiryGameDay > 0 && fact.earliestExpiryGameDay - gameDay <= settings.expiryRiskWindowDays ? 0.35 : 0.0;
        return Clamp01((float)(0.65 * Math.Min(1.0, overstockRatio) + 0.25 * Math.Min(1.0, expiringRatio) + expiryUrgency));
    }

    private static int RoundPackages(long need, long packageQty, int minimum, int increment)
    {
        if (packageQty <= 0L) return 0;
        long raw = need <= 0L ? 0L : (need + packageQty - 1L) / packageQty;
        raw = Math.Max(raw, Math.Max(1, minimum));
        int step = Math.Max(1, increment);
        long remainder = raw % step;
        if (remainder != 0L) raw += step - remainder;
        return raw > int.MaxValue ? int.MaxValue : (int)raw;
    }

    private static long EstimateShipping(long subtotal, BistroBuilderSmartPurchaseOfferFact offer)
    {
        if (offer.freeShippingEnabled && offer.freeShippingThresholdCents > 0L && subtotal >= offer.freeShippingThresholdCents) return 0L;
        return Math.Max(0L, offer.shippingCostCents);
    }

    private static BistroBuilderSmartPurchaseOfferFact FindOffer(IReadOnlyList<BistroBuilderSmartPurchaseOfferFact> offers, string id)
    {
        for (int i = 0; i < offers.Count; i++) if (offers[i] != null && offers[i].supplierOfferId == id) return offers[i];
        return null;
    }

    private static Weights ResolveWeights(BistroBuilderSmartPurchaseStrategy strategy, BistroBuilderSupplierSmartPurchaseSettings s)
    {
        if (strategy == BistroBuilderSmartPurchaseStrategy.Ahorrar)
            return new Weights { cost=s.savingCostWeight, speed=s.savingSpeedWeight, reliability=s.savingReliabilityWeight, waste=s.savingWasteWeight, stockout=s.savingStockoutWeight, targetDays=s.savingTargetCoverageDays };
        if (strategy == BistroBuilderSmartPurchaseStrategy.Urgente)
            return new Weights { cost=s.urgentCostWeight, speed=s.urgentSpeedWeight, reliability=s.urgentReliabilityWeight, waste=s.urgentWasteWeight, stockout=s.urgentStockoutWeight, targetDays=s.urgentTargetCoverageDays };
        return new Weights { cost=s.balancedCostWeight, speed=s.balancedSpeedWeight, reliability=s.balancedReliabilityWeight, waste=s.balancedWasteWeight, stockout=s.balancedStockoutWeight, targetDays=s.balancedTargetCoverageDays };
    }

    private static float WeightedAverage(float a,float wa,float b,float wb,float c,float wc,float d,float wd,float e,float we)
    {
        float sum = wa+wb+wc+wd+we;
        return sum <= 0.0001f ? 0f : (a*wa+b*wb+c*wc+d*wd+e*we)/sum;
    }
    private static float Risk01(BistroBuilderSmartPurchaseRisk r) { return ((int)r)/4f; }
    private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
    private static long SafeRound(double value) { if (double.IsNaN(value) || value <= 0.0) return 0L; if (value >= long.MaxValue) return long.MaxValue; return (long)Math.Round(value, MidpointRounding.AwayFromZero); }
    private static long SafeMultiply(long a,long b) { if (a <= 0L || b <= 0L) return 0L; if (a > long.MaxValue/b) return long.MaxValue; return a*b; }
    private static long SafeAdd(long a,long b) { if (b > 0L && a > long.MaxValue-b) return long.MaxValue; return a+b; }
}
