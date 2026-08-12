using System;
using System.Collections.Generic;

public enum BistroBuilderSupplierLogisticsPlanStatus
{
    Planned = 0,
    DelayApplied = 1,
    ReadyForDispatch = 2,
    Dispatched = 3,
    Delivered = 4,
    Cancelled = 5
}

[Serializable]
public sealed class BistroBuilderSupplierLogisticsPlanRecord
{
    public string logisticsPlanId;
    public string purchaseOrderId;
    public string orderDisplayCode;
    public string supplierId;
    public string supplierDisplayName;
    public BistroBuilderSupplierLogisticsPlanStatus status = BistroBuilderSupplierLogisticsPlanStatus.Planned;

    public int createdGameDay = 1;
    public long stateRevision = 1;
    public long sourceOrderStateRevision;

    public int basePlannedDeliveryGameDay;
    public int baseWindowStartMinuteOfDay;
    public int baseWindowEndMinuteOfDay;
    public int plannedDeliveryGameDay;
    public int windowStartMinuteOfDay;
    public int windowEndMinuteOfDay;

    public BistroBuilderSupplierReliabilityTier reliabilityTier;
    public float reliabilityValue;
    public int delayProbabilityBasisPoints;
    public int deterministicDelayRollBasisPoints;
    public int decidedDelayGameMinutes;
    public bool delayApplied;
    public int delayAppliedGameDay;

    public int logisticsLoadUnits;
    public int visualLoadUnits;
    public int suggestedTripCount;
    public BistroBuilderSupplierVehiclePreference resolvedVehicle;
    public string vehiclePresentationProfileId;
    public string driverPresentationProfileId;

    public string reasonCode;
    public string reasonText;

    public bool HasDelayDecision => decidedDelayGameMinutes > 0;
    public bool IsTerminal => status == BistroBuilderSupplierLogisticsPlanStatus.Delivered ||
                              status == BistroBuilderSupplierLogisticsPlanStatus.Cancelled;

    public BistroBuilderSupplierLogisticsPlanRecord DeepClone()
    {
        return (BistroBuilderSupplierLogisticsPlanRecord)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderSupplierLogisticsSnapshot
{
    public const string CurrentSchemaId = "supplier.logistics.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public int currentGameDay = 1;
    public ulong logisticsSeed;
    public ulong sourceMarketSeed;
    public ulong sourceCommercialSeed;
    public long logisticsRevision = 1;
    public long nextPlanSequence = 1;
    public List<BistroBuilderSupplierLogisticsPlanRecord> plans =
        new List<BistroBuilderSupplierLogisticsPlanRecord>();

    public BistroBuilderSupplierLogisticsSnapshot DeepClone()
    {
        BistroBuilderSupplierLogisticsSnapshot clone = new BistroBuilderSupplierLogisticsSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            currentGameDay = currentGameDay,
            logisticsSeed = logisticsSeed,
            sourceMarketSeed = sourceMarketSeed,
            sourceCommercialSeed = sourceCommercialSeed,
            logisticsRevision = logisticsRevision,
            nextPlanSequence = nextPlanSequence
        };
        if (plans != null)
        {
            for (int index = 0; index < plans.Count; index++)
            {
                if (plans[index] != null) clone.plans.Add(plans[index].DeepClone());
            }
        }
        return clone;
    }
}

public sealed class BistroBuilderSupplierDispatchTicket
{
    public string logisticsPlanId;
    public string purchaseOrderId;
    public string orderDisplayCode;
    public string supplierId;
    public int plannedDeliveryGameDay;
    public int windowStartMinuteOfDay;
    public int windowEndMinuteOfDay;
    public int appliedDelayGameMinutes;
    public int logisticsLoadUnits;
    public int visualLoadUnits;
    public int suggestedTripCount;
    public BistroBuilderSupplierVehiclePreference vehicle;
    public string vehiclePresentationProfileId;
    public string driverPresentationProfileId;

    public BistroBuilderSupplierDispatchTicket DeepClone()
    {
        return (BistroBuilderSupplierDispatchTicket)MemberwiseClone();
    }
}
