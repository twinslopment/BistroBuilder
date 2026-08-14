using System;
using System.Collections.Generic;

public enum BistroBuilderLotCostBasisKind
{
    ReferenceEstimate = 0,
    SupplierActual = 1
}

public enum BistroBuilderProductCostQuality
{
    Estimated = 0,
    Mixed = 1,
    Actual = 2
}

[Serializable]
public sealed class BistroBuilderLotCostBasisRecord
{
    public string lotId = string.Empty;
    public string ingredientId = string.Empty;
    public string sourceReferenceId = string.Empty;
    public BistroBuilderLotCostBasisKind basisKind;
    public long basisQuantityCanonicalMilliUnits;
    public long totalCostMicroCents;
    public int receivedDayIndex = 1;

    public BistroBuilderLotCostBasisRecord DeepClone()
    {
        return new BistroBuilderLotCostBasisRecord
        {
            lotId = lotId,
            ingredientId = ingredientId,
            sourceReferenceId = sourceReferenceId,
            basisKind = basisKind,
            basisQuantityCanonicalMilliUnits = basisQuantityCanonicalMilliUnits,
            totalCostMicroCents = totalCostMicroCents,
            receivedDayIndex = receivedDayIndex
        };
    }
}

[Serializable]
public sealed class BistroBuilderConsumedLineCostRecord
{
    public long sequence;
    public string costRecordId = string.Empty;
    public string orderId = string.Empty;
    public string lineId = string.Empty;
    public string dishId = string.Empty;
    public BistroBuilderMealServiceAvailability mealService;
    public BistroBuilderServiceMode serviceMode;
    public int dayIndex = 1;
    public int minuteOfDay;
    public int salePriceCents;
    public long theoreticalCostMicroCents;
    public long theoreticalCostCents;
    public long actualCostMicroCents;
    public long actualCostCents;
    public long theoreticalMarginCents;
    public int theoreticalMarginBasisPoints;
    public BistroBuilderRecipeMarginBand theoreticalMarginBand;
    public long actualMarginCents;
    public int actualMarginBasisPoints;
    public BistroBuilderRecipeMarginBand actualMarginBand;
    public BistroBuilderProductCostQuality costQuality;

    public BistroBuilderConsumedLineCostRecord DeepClone()
    {
        return new BistroBuilderConsumedLineCostRecord
        {
            sequence = sequence,
            costRecordId = costRecordId,
            orderId = orderId,
            lineId = lineId,
            dishId = dishId,
            mealService = mealService,
            serviceMode = serviceMode,
            dayIndex = dayIndex,
            minuteOfDay = minuteOfDay,
            salePriceCents = salePriceCents,
            theoreticalCostMicroCents = theoreticalCostMicroCents,
            theoreticalCostCents = theoreticalCostCents,
            actualCostMicroCents = actualCostMicroCents,
            actualCostCents = actualCostCents,
            theoreticalMarginCents = theoreticalMarginCents,
            theoreticalMarginBasisPoints = theoreticalMarginBasisPoints,
            theoreticalMarginBand = theoreticalMarginBand,
            actualMarginCents = actualMarginCents,
            actualMarginBasisPoints = actualMarginBasisPoints,
            actualMarginBand = actualMarginBand,
            costQuality = costQuality
        };
    }
}

[Serializable]
public sealed class BistroBuilderProductCostSnapshot
{
    public const string CurrentSchemaId = "finance.product_cost.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision = 1L;
    public long nextLineCostSequence = 1L;
    public List<BistroBuilderLotCostBasisRecord> lotCostBases =
        new List<BistroBuilderLotCostBasisRecord>();
    public List<BistroBuilderConsumedLineCostRecord> consumedLineCosts =
        new List<BistroBuilderConsumedLineCostRecord>();

    public BistroBuilderProductCostSnapshot DeepClone()
    {
        var clone = new BistroBuilderProductCostSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            nextLineCostSequence = nextLineCostSequence
        };

        if (lotCostBases != null)
        {
            for (int index = 0; index < lotCostBases.Count; index++)
            {
                if (lotCostBases[index] != null)
                {
                    clone.lotCostBases.Add(lotCostBases[index].DeepClone());
                }
            }
        }

        if (consumedLineCosts != null)
        {
            for (int index = 0; index < consumedLineCosts.Count; index++)
            {
                if (consumedLineCosts[index] != null)
                {
                    clone.consumedLineCosts.Add(
                        consumedLineCosts[index].DeepClone()
                    );
                }
            }
        }

        return clone;
    }
}
