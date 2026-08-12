using System;
using System.Collections.Generic;

public enum BistroBuilderSupplierAccessStatus
{
    Locked = 0,
    AvailableFromStart = 1,
    Unlocked = 2
}

[Serializable]
public sealed class BistroBuilderSupplierProgressionFacts
{
    public int currentGameDay = 1;
    public int daysOpen;
    public long qualifiedPurchaseVolumeCents;

    public bool hasLifetimeRevenue;
    public long lifetimeRevenueCents;

    public bool hasReputation;
    public int reputationPoints;

    public bool hasRestaurantCapacity;
    public int restaurantCapacitySeats;

    public bool hasCuisineCategories;
    public List<string> cuisineCategories = new List<string>();

    public List<BistroBuilderSupplierIngredientFamilyConsumptionFact> ingredientFamilyConsumption =
        new List<BistroBuilderSupplierIngredientFamilyConsumptionFact>();

    public BistroBuilderSupplierProgressionFacts DeepClone()
    {
        BistroBuilderSupplierProgressionFacts clone = new BistroBuilderSupplierProgressionFacts
        {
            currentGameDay = currentGameDay,
            daysOpen = daysOpen,
            qualifiedPurchaseVolumeCents = qualifiedPurchaseVolumeCents,
            hasLifetimeRevenue = hasLifetimeRevenue,
            lifetimeRevenueCents = lifetimeRevenueCents,
            hasReputation = hasReputation,
            reputationPoints = reputationPoints,
            hasRestaurantCapacity = hasRestaurantCapacity,
            restaurantCapacitySeats = restaurantCapacitySeats,
            hasCuisineCategories = hasCuisineCategories
        };

        if (cuisineCategories != null)
        {
            clone.cuisineCategories.AddRange(cuisineCategories);
        }

        if (ingredientFamilyConsumption != null)
        {
            for (int index = 0; index < ingredientFamilyConsumption.Count; index++)
            {
                BistroBuilderSupplierIngredientFamilyConsumptionFact fact = ingredientFamilyConsumption[index];
                if (fact != null)
                {
                    clone.ingredientFamilyConsumption.Add(fact.DeepClone());
                }
            }
        }

        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderSupplierIngredientFamilyConsumptionFact
{
    public string familyId;
    public long consumedMicrounits;

    public BistroBuilderSupplierIngredientFamilyConsumptionFact DeepClone()
    {
        return new BistroBuilderSupplierIngredientFamilyConsumptionFact
        {
            familyId = familyId,
            consumedMicrounits = consumedMicrounits
        };
    }
}

/// <summary>
/// Constructor explícito de hechos externos. Los sistemas ajenos a Proveedores pueden
/// contribuir hechos de solo lectura sin que 2.3I dependa de sus implementaciones.
/// </summary>
public sealed class BistroBuilderSupplierProgressionFactBuilder
{
    private readonly BistroBuilderSupplierProgressionFacts facts;

    public BistroBuilderSupplierProgressionFactBuilder(BistroBuilderSupplierProgressionFacts baseFacts)
    {
        facts = baseFacts != null ? baseFacts.DeepClone() : new BistroBuilderSupplierProgressionFacts();
    }

    public void SetLifetimeRevenueCents(long cents)
    {
        facts.hasLifetimeRevenue = true;
        facts.lifetimeRevenueCents = Math.Max(0L, cents);
    }

    public void SetReputationPoints(int points)
    {
        facts.hasReputation = true;
        facts.reputationPoints = Math.Max(0, Math.Min(100, points));
    }

    public void SetRestaurantCapacitySeats(int seats)
    {
        facts.hasRestaurantCapacity = true;
        facts.restaurantCapacitySeats = Math.Max(0, seats);
    }

    public void AddCuisineCategory(string categoryId)
    {
        string normalized = BistroBuilderSupplierProgressionEngine.NormalizeToken(categoryId);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        facts.hasCuisineCategories = true;
        if (!facts.cuisineCategories.Contains(normalized))
        {
            facts.cuisineCategories.Add(normalized);
        }
    }

    public void SetIngredientFamilyConsumptionMicrounits(string familyId, long consumedMicrounits)
    {
        string normalized = BistroBuilderSupplierProgressionEngine.NormalizeToken(familyId);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        for (int index = 0; index < facts.ingredientFamilyConsumption.Count; index++)
        {
            BistroBuilderSupplierIngredientFamilyConsumptionFact current = facts.ingredientFamilyConsumption[index];
            if (current != null && string.Equals(current.familyId, normalized, StringComparison.Ordinal))
            {
                current.consumedMicrounits = Math.Max(0L, consumedMicrounits);
                return;
            }
        }

        facts.ingredientFamilyConsumption.Add(new BistroBuilderSupplierIngredientFamilyConsumptionFact
        {
            familyId = normalized,
            consumedMicrounits = Math.Max(0L, consumedMicrounits)
        });
    }

    public BistroBuilderSupplierProgressionFacts Build()
    {
        return facts.DeepClone();
    }
}

/// <summary>
/// Contrato opcional para Finanzas/Reputación/Restaurante/Cocina/Consumo.
/// 2.3I nunca hace reflexión sobre esos sistemas ni se convierte en su autoridad.
/// </summary>
public interface IBistroBuilderSupplierProgressionFactSource
{
    void ContributeSupplierProgressionFacts(BistroBuilderSupplierProgressionFactBuilder builder);
}

[Serializable]
public sealed class BistroBuilderSupplierUnlockConditionResult
{
    public BistroBuilderSupplierUnlockRuleKind kind;
    public bool sourceAvailable;
    public bool satisfied;
    public float progress01;
    public long currentNumericValue;
    public long requiredNumericValue;
    public string currentText;
    public string requiredText;
    public string reasonCode;
    public string reasonText;

    public BistroBuilderSupplierUnlockConditionResult DeepClone()
    {
        return (BistroBuilderSupplierUnlockConditionResult)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderSupplierAccessEvaluation
{
    public string supplierId;
    public string supplierDisplayName;
    public BistroBuilderSupplierAccessStatus status;
    public bool isUnlocked;
    public bool availableFromStart;
    public bool conditionsSatisfied;
    public float progress01;
    public string summary;
    public List<BistroBuilderSupplierUnlockConditionResult> conditions =
        new List<BistroBuilderSupplierUnlockConditionResult>();

    public BistroBuilderSupplierAccessEvaluation DeepClone()
    {
        BistroBuilderSupplierAccessEvaluation clone = (BistroBuilderSupplierAccessEvaluation)MemberwiseClone();
        clone.conditions = new List<BistroBuilderSupplierUnlockConditionResult>();
        if (conditions != null)
        {
            for (int index = 0; index < conditions.Count; index++)
            {
                if (conditions[index] != null)
                {
                    clone.conditions.Add(conditions[index].DeepClone());
                }
            }
        }
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderSupplierProgressionStateRecord
{
    public string supplierId;
    public bool unlocked;
    public int unlockedGameDay;
    public string unlockReasonCode;
    public string unlockReasonText;
    public long stateRevision = 1;

    public BistroBuilderSupplierProgressionStateRecord DeepClone()
    {
        return (BistroBuilderSupplierProgressionStateRecord)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderSupplierProgressionSnapshot
{
    public const string CurrentSchemaId = "supplier.progression.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public int currentGameDay = 1;
    public ulong sourceMarketSeed;
    public ulong sourceCommercialSeed;
    public long progressionRevision = 1;
    public long qualifiedPurchaseVolumeCents;
    public List<string> countedQualifiedPurchaseOrderIds = new List<string>();
    public List<BistroBuilderSupplierProgressionStateRecord> suppliers =
        new List<BistroBuilderSupplierProgressionStateRecord>();

    public BistroBuilderSupplierProgressionSnapshot DeepClone()
    {
        BistroBuilderSupplierProgressionSnapshot clone = new BistroBuilderSupplierProgressionSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            currentGameDay = currentGameDay,
            sourceMarketSeed = sourceMarketSeed,
            sourceCommercialSeed = sourceCommercialSeed,
            progressionRevision = progressionRevision,
            qualifiedPurchaseVolumeCents = qualifiedPurchaseVolumeCents
        };

        if (countedQualifiedPurchaseOrderIds != null)
        {
            clone.countedQualifiedPurchaseOrderIds.AddRange(countedQualifiedPurchaseOrderIds);
        }

        if (suppliers != null)
        {
            for (int index = 0; index < suppliers.Count; index++)
            {
                if (suppliers[index] != null)
                {
                    clone.suppliers.Add(suppliers[index].DeepClone());
                }
            }
        }

        return clone;
    }
}
