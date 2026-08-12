using System;
using System.Collections.Generic;

public enum BistroBuilderSmartPurchaseStrategy
{
    Ahorrar = 0,
    Equilibrado = 1,
    Urgente = 2
}

public enum BistroBuilderSmartPurchaseRisk
{
    SinRiesgo = 0,
    Bajo = 1,
    Medio = 2,
    Alto = 3,
    Critico = 4
}

[Serializable]
public sealed class BistroBuilderSmartPurchaseIngredientFact
{
    public string ingredientId;
    public string displayName;
    public string canonicalUnit;
    public long stockMicrounits;
    public long reservedMicrounits;
    public long availableMicrounits;
    public long minimumStockMicrounits;
    public long forecastDailyConsumptionMicrounits;
    public long expiringSoonMicrounits;
    public int earliestExpiryGameDay;
    public float recipeImportance01 = 0.5f;
    public long incomingMicrounits;
    public int earliestIncomingGameDay;
    public bool inventoryResolved;
    public bool forecastResolved;
    public bool policyResolved;
    public bool expiryResolved;

    public BistroBuilderSmartPurchaseIngredientFact DeepClone()
    {
        return (BistroBuilderSmartPurchaseIngredientFact)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderSmartPurchaseOfferFact
{
    public string supplierOfferId;
    public string supplierId;
    public string supplierDisplayName;
    public string ingredientId;
    public string packageFormatId;
    public string packageDisplayName;
    public long packageNetQuantityMicrounits;
    public int minimumPackageCount = 1;
    public int orderIncrement = 1;
    public long effectiveUnitPriceCents;
    public long marketUnitPriceCents;
    public bool hasPromotion;
    public string promotionId;
    public int discountBasisPoints;
    public BistroBuilderSupplierOfferAvailability availability;
    public bool availableForNewOrders;
    public float leadTimeGameHours = 24f;
    public float reliability01 = 0.95f;
    public long supplierMinimumOrderCents;
    public long shippingCostCents;
    public bool freeShippingEnabled;
    public long freeShippingThresholdCents;

    public BistroBuilderSmartPurchaseOfferFact DeepClone()
    {
        return (BistroBuilderSmartPurchaseOfferFact)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderSmartPurchaseCandidate
{
    public string supplierOfferId;
    public string supplierId;
    public string supplierDisplayName;
    public string ingredientId;
    public string packageFormatId;
    public string packageDisplayName;
    public int packageCount;
    public long purchasedMicrounits;
    public long projectedAvailableAtArrivalMicrounits;
    public long targetStockMicrounits;
    public long shortageBeforePurchaseMicrounits;
    public long projectedOverstockMicrounits;
    public long lineSubtotalCents;
    public long estimatedShippingCents;
    public long estimatedTotalCents;
    public long normalizedCostPerMillionMicrounitsCents;
    public float leadTimeGameHours;
    public float reliability01;
    public BistroBuilderSupplierOfferAvailability availability;
    public bool hasPromotion;
    public int discountBasisPoints;
    public BistroBuilderSmartPurchaseRisk stockoutRisk;
    public float wasteRisk01;
    public float score;
    public List<string> reasonCodes = new List<string>();
    public List<string> reasons = new List<string>();

    public BistroBuilderSmartPurchaseCandidate DeepClone()
    {
        BistroBuilderSmartPurchaseCandidate clone = (BistroBuilderSmartPurchaseCandidate)MemberwiseClone();
        clone.reasonCodes = reasonCodes != null ? new List<string>(reasonCodes) : new List<string>();
        clone.reasons = reasons != null ? new List<string>(reasons) : new List<string>();
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderSmartPurchaseIngredientRecommendation
{
    public string ingredientId;
    public string ingredientDisplayName;
    public long currentAvailableMicrounits;
    public long forecastDailyConsumptionMicrounits;
    public long incomingMicrounits;
    public BistroBuilderSmartPurchaseRisk currentRisk;
    public BistroBuilderSmartPurchaseCandidate selected;
    public List<BistroBuilderSmartPurchaseCandidate> alternatives = new List<BistroBuilderSmartPurchaseCandidate>();
    public List<string> reasons = new List<string>();
}

[Serializable]
public sealed class BistroBuilderSmartPurchaseSupplierBasket
{
    public string supplierId;
    public string supplierDisplayName;
    public long subtotalCents;
    public long shippingCents;
    public long totalCents;
    public long minimumOrderCents;
    public bool meetsMinimumOrder;
    public int lineCount;
}

[Serializable]
public sealed class BistroBuilderSmartPurchasePlan
{
    public BistroBuilderSmartPurchaseStrategy strategy;
    public float score;
    public long subtotalCents;
    public long shippingCents;
    public long totalCents;
    public int ingredientsEvaluated;
    public int ingredientsRecommended;
    public int criticalIngredients;
    public int highRiskIngredients;
    public int supplierCount;
    public bool containsMinimumOrderGap;
    public List<BistroBuilderSmartPurchaseIngredientRecommendation> ingredients = new List<BistroBuilderSmartPurchaseIngredientRecommendation>();
    public List<BistroBuilderSmartPurchaseSupplierBasket> suppliers = new List<BistroBuilderSmartPurchaseSupplierBasket>();
    public List<string> summaryReasons = new List<string>();
}

[Serializable]
public sealed class BistroBuilderSmartPurchaseReport
{
    public int gameDay;
    public long generatedSequence;
    public BistroBuilderSmartPurchaseStrategy recommendedStrategy;
    public string recommendedReason;
    public bool inventoryResolved;
    public bool forecastResolved;
    public bool policyResolved;
    public int canonicalIngredientCount;
    public int ingredientFactsResolved;
    public int offersEvaluated;
    public List<BistroBuilderSmartPurchasePlan> plans = new List<BistroBuilderSmartPurchasePlan>();
    public List<string> diagnostics = new List<string>();
}
