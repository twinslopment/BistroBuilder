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

/// <summary>
/// Coste analítico de inventario que abandona el almacén sin generar una venta.
/// No representa un movimiento de caja: la compra ya fue pagada anteriormente.
///
/// 2.2 agrega caducidad/merma por ingrediente y no conserva en ese movimiento
/// la asignación exacta de lotes consumidos; por ello V1 congela una estimación
/// de referencia y la marca explícitamente como Estimated.
/// </summary>
[Serializable]
public sealed class BistroBuilderInventoryLossCostRecord
{
    public long sequence;
    public string lossCostRecordId = string.Empty;
    public string inventoryTransactionId = string.Empty;
    public string inventoryOperationId = string.Empty;
    public string ingredientId = string.Empty;
    public BistroBuilderInventoryTransactionType transactionType;
    public int dayIndex = 1;
    public int minuteOfDay;
    public long quantityCanonicalMilliUnits;
    public long costMicroCents;
    public long costCents;
    public BistroBuilderProductCostQuality costQuality =
        BistroBuilderProductCostQuality.Estimated;

    public BistroBuilderInventoryLossCostRecord DeepClone()
    {
        return (BistroBuilderInventoryLossCostRecord)MemberwiseClone();
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
    public long nextInventoryLossCostSequence = 1L;
    public List<BistroBuilderLotCostBasisRecord> lotCostBases =
        new List<BistroBuilderLotCostBasisRecord>();
    public List<BistroBuilderConsumedLineCostRecord> consumedLineCosts =
        new List<BistroBuilderConsumedLineCostRecord>();
    public List<BistroBuilderInventoryLossCostRecord> inventoryLossCosts =
        new List<BistroBuilderInventoryLossCostRecord>();

    public BistroBuilderProductCostSnapshot DeepClone()
    {
        var clone = new BistroBuilderProductCostSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            nextLineCostSequence = nextLineCostSequence,
            nextInventoryLossCostSequence = nextInventoryLossCostSequence
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
                        consumedLineCosts[index].DeepClone());
                }
            }
        }

        if (inventoryLossCosts != null)
        {
            for (int index = 0; index < inventoryLossCosts.Count; index++)
            {
                if (inventoryLossCosts[index] != null)
                {
                    clone.inventoryLossCosts.Add(
                        inventoryLossCosts[index].DeepClone());
                }
            }
        }

        return clone;
    }
}
