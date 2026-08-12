using System;
using System.Collections.Generic;

public enum BistroBuilderPurchaseOrderStatus
{
    Draft = 0,
    Confirmed = 1,
    PendingDelivery = 2,
    InDelivery = 3,
    Delivered = 4,
    Cancelled = 5
}

[Serializable]
public sealed class BistroBuilderPurchaseOrderDeliveryWindowSnapshot
{
    public int startMinuteOfDay;
    public int endMinuteOfDay;
    public bool monday;
    public bool tuesday;
    public bool wednesday;
    public bool thursday;
    public bool friday;
    public bool saturday;
    public bool sunday;

    public BistroBuilderPurchaseOrderDeliveryWindowSnapshot DeepClone()
    {
        return new BistroBuilderPurchaseOrderDeliveryWindowSnapshot
        {
            startMinuteOfDay = startMinuteOfDay,
            endMinuteOfDay = endMinuteOfDay,
            monday = monday,
            tuesday = tuesday,
            wednesday = wednesday,
            thursday = thursday,
            friday = friday,
            saturday = saturday,
            sunday = sunday
        };
    }
}

[Serializable]
public sealed class BistroBuilderPurchaseOrderSupplierTermsSnapshot
{
    public string supplierId;
    public string supplierDisplayName;
    public long minimumOrderValueCents;
    public long configuredShippingCostCents;
    public bool freeShippingEnabled;
    public long freeShippingThresholdCents;
    public long appliedShippingCostCents;
    public float defaultLeadTimeGameHours;
    public BistroBuilderSupplierReliabilityTier reliabilityTier;
    public float reliabilityValue;
    public BistroBuilderSupplierVehiclePreference preferredVehicle;
    public string vehiclePresentationProfileId;
    public string driverPresentationProfileId;
    public List<BistroBuilderPurchaseOrderDeliveryWindowSnapshot> deliveryWindows =
        new List<BistroBuilderPurchaseOrderDeliveryWindowSnapshot>();

    public BistroBuilderPurchaseOrderSupplierTermsSnapshot DeepClone()
    {
        BistroBuilderPurchaseOrderSupplierTermsSnapshot clone =
            new BistroBuilderPurchaseOrderSupplierTermsSnapshot
            {
                supplierId = supplierId,
                supplierDisplayName = supplierDisplayName,
                minimumOrderValueCents = minimumOrderValueCents,
                configuredShippingCostCents = configuredShippingCostCents,
                freeShippingEnabled = freeShippingEnabled,
                freeShippingThresholdCents = freeShippingThresholdCents,
                appliedShippingCostCents = appliedShippingCostCents,
                defaultLeadTimeGameHours = defaultLeadTimeGameHours,
                reliabilityTier = reliabilityTier,
                reliabilityValue = reliabilityValue,
                preferredVehicle = preferredVehicle,
                vehiclePresentationProfileId = vehiclePresentationProfileId,
                driverPresentationProfileId = driverPresentationProfileId
            };

        if (deliveryWindows != null)
        {
            for (int index = 0; index < deliveryWindows.Count; index++)
            {
                if (deliveryWindows[index] != null)
                {
                    clone.deliveryWindows.Add(deliveryWindows[index].DeepClone());
                }
            }
        }
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderPurchaseOrderDraftLine
{
    public string purchaseOrderLineId;
    public string supplierOfferId;
    public int packageCount;
    public int sortOrder;

    public BistroBuilderPurchaseOrderDraftLine DeepClone()
    {
        return new BistroBuilderPurchaseOrderDraftLine
        {
            purchaseOrderLineId = purchaseOrderLineId,
            supplierOfferId = supplierOfferId,
            packageCount = packageCount,
            sortOrder = sortOrder
        };
    }
}

[Serializable]
public sealed class BistroBuilderPurchaseOrderConfirmedLineSnapshot
{
    public string purchaseOrderLineId;
    public string supplierOfferId;
    public string supplierId;
    public string ingredientId;
    public string ingredientDisplayName;
    public string canonicalUnit;
    public string packageFormatId;
    public string packageDisplayName;
    public string packageType;
    public BistroBuilderCommercialPackageLogisticSize logisticSize;
    public long packageNetQuantityMicrounits;
    public int packageCount;
    public long totalNetQuantityMicrounits;
    public int minimumPackageCount;
    public int orderIncrement;
    public long basePriceCents;
    public long marketPriceCents;
    public long effectiveUnitPriceCents;
    public long lineSubtotalCents;
    public BistroBuilderSupplierOfferAvailability availabilityAtConfirmation;
    public bool hadActivePromotion;
    public string promotionId;
    public int promotionStartGameDay;
    public int promotionEndGameDayExclusive;
    public int discountBasisPoints;
    public string promotionReasonCode;
    public string promotionReasonText;
    public float quotedLeadTimeGameHours;
    public long sourceMarketRevision;
    public long sourceCommercialRevision;

    public BistroBuilderPurchaseOrderConfirmedLineSnapshot DeepClone()
    {
        return new BistroBuilderPurchaseOrderConfirmedLineSnapshot
        {
            purchaseOrderLineId = purchaseOrderLineId,
            supplierOfferId = supplierOfferId,
            supplierId = supplierId,
            ingredientId = ingredientId,
            ingredientDisplayName = ingredientDisplayName,
            canonicalUnit = canonicalUnit,
            packageFormatId = packageFormatId,
            packageDisplayName = packageDisplayName,
            packageType = packageType,
            logisticSize = logisticSize,
            packageNetQuantityMicrounits = packageNetQuantityMicrounits,
            packageCount = packageCount,
            totalNetQuantityMicrounits = totalNetQuantityMicrounits,
            minimumPackageCount = minimumPackageCount,
            orderIncrement = orderIncrement,
            basePriceCents = basePriceCents,
            marketPriceCents = marketPriceCents,
            effectiveUnitPriceCents = effectiveUnitPriceCents,
            lineSubtotalCents = lineSubtotalCents,
            availabilityAtConfirmation = availabilityAtConfirmation,
            hadActivePromotion = hadActivePromotion,
            promotionId = promotionId,
            promotionStartGameDay = promotionStartGameDay,
            promotionEndGameDayExclusive = promotionEndGameDayExclusive,
            discountBasisPoints = discountBasisPoints,
            promotionReasonCode = promotionReasonCode,
            promotionReasonText = promotionReasonText,
            quotedLeadTimeGameHours = quotedLeadTimeGameHours,
            sourceMarketRevision = sourceMarketRevision,
            sourceCommercialRevision = sourceCommercialRevision
        };
    }
}

[Serializable]
public sealed class BistroBuilderPurchaseOrderRecord
{
    public string purchaseOrderId;
    public string displayCode;
    public string supplierId;
    public BistroBuilderPurchaseOrderStatus status = BistroBuilderPurchaseOrderStatus.Draft;
    public int createdGameDay = 1;
    public int lastModifiedGameDay = 1;
    public int confirmedGameDay;
    public int pendingDeliveryGameDay;
    public int inDeliveryGameDay;
    public int deliveredGameDay;
    public int cancelledGameDay;
    public long stateRevision = 1;
    public long nextLineSequence = 1;
    public string currencyCode = "EUR";

    public List<BistroBuilderPurchaseOrderDraftLine> draftLines =
        new List<BistroBuilderPurchaseOrderDraftLine>();
    public List<BistroBuilderPurchaseOrderConfirmedLineSnapshot> confirmedLines =
        new List<BistroBuilderPurchaseOrderConfirmedLineSnapshot>();
    public BistroBuilderPurchaseOrderSupplierTermsSnapshot supplierTerms;

    public long subtotalCents;
    public long shippingCostCents;
    public long totalCents;
    public float quotedLeadTimeGameHours;
    public long sourceMarketRevision;
    public long sourceCommercialRevision;

    // Campos reservados para 2.3G/2.3H/2.2B. 2.3E define el contrato pero no planifica ni recibe.
    public string logisticsPlanId;
    public int plannedDeliveryGameDay;
    public int plannedDeliveryWindowStartMinuteOfDay = -1;
    public int plannedDeliveryWindowEndMinuteOfDay = -1;
    public int actualDeliveryStartGameDay;
    public int plannedDelayGameMinutes;
    public int appliedDelayGameMinutes;
    public string deliveryReceiptId;
    public string cancellationReason;

    public bool IsEditable => status == BistroBuilderPurchaseOrderStatus.Draft;
    public bool IsTerminal => status == BistroBuilderPurchaseOrderStatus.Delivered ||
                              status == BistroBuilderPurchaseOrderStatus.Cancelled;

    public BistroBuilderPurchaseOrderRecord DeepClone()
    {
        BistroBuilderPurchaseOrderRecord clone = new BistroBuilderPurchaseOrderRecord
        {
            purchaseOrderId = purchaseOrderId,
            displayCode = displayCode,
            supplierId = supplierId,
            status = status,
            createdGameDay = createdGameDay,
            lastModifiedGameDay = lastModifiedGameDay,
            confirmedGameDay = confirmedGameDay,
            pendingDeliveryGameDay = pendingDeliveryGameDay,
            inDeliveryGameDay = inDeliveryGameDay,
            deliveredGameDay = deliveredGameDay,
            cancelledGameDay = cancelledGameDay,
            stateRevision = stateRevision,
            nextLineSequence = nextLineSequence,
            currencyCode = currencyCode,
            supplierTerms = supplierTerms != null ? supplierTerms.DeepClone() : null,
            subtotalCents = subtotalCents,
            shippingCostCents = shippingCostCents,
            totalCents = totalCents,
            quotedLeadTimeGameHours = quotedLeadTimeGameHours,
            sourceMarketRevision = sourceMarketRevision,
            sourceCommercialRevision = sourceCommercialRevision,
            logisticsPlanId = logisticsPlanId,
            plannedDeliveryGameDay = plannedDeliveryGameDay,
            plannedDeliveryWindowStartMinuteOfDay = plannedDeliveryWindowStartMinuteOfDay,
            plannedDeliveryWindowEndMinuteOfDay = plannedDeliveryWindowEndMinuteOfDay,
            actualDeliveryStartGameDay = actualDeliveryStartGameDay,
            plannedDelayGameMinutes = plannedDelayGameMinutes,
            appliedDelayGameMinutes = appliedDelayGameMinutes,
            deliveryReceiptId = deliveryReceiptId,
            cancellationReason = cancellationReason
        };

        if (draftLines != null)
        {
            for (int index = 0; index < draftLines.Count; index++)
            {
                if (draftLines[index] != null)
                {
                    clone.draftLines.Add(draftLines[index].DeepClone());
                }
            }
        }
        if (confirmedLines != null)
        {
            for (int index = 0; index < confirmedLines.Count; index++)
            {
                if (confirmedLines[index] != null)
                {
                    clone.confirmedLines.Add(confirmedLines[index].DeepClone());
                }
            }
        }
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderSupplierPurchaseOrdersSnapshot
{
    public const string CurrentSchemaId = "supplier.orders.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public int currentGameDay = 1;
    public ulong sourceMarketSeed;
    public ulong sourceCommercialSeed;
    public long ordersRevision = 1;
    public long nextOrderSequence = 1;
    public List<BistroBuilderPurchaseOrderRecord> orders =
        new List<BistroBuilderPurchaseOrderRecord>();

    public BistroBuilderSupplierPurchaseOrdersSnapshot DeepClone()
    {
        BistroBuilderSupplierPurchaseOrdersSnapshot clone =
            new BistroBuilderSupplierPurchaseOrdersSnapshot
            {
                schemaId = schemaId,
                schemaVersion = schemaVersion,
                currentGameDay = currentGameDay,
                sourceMarketSeed = sourceMarketSeed,
                sourceCommercialSeed = sourceCommercialSeed,
                ordersRevision = ordersRevision,
                nextOrderSequence = nextOrderSequence
            };

        if (orders != null)
        {
            for (int index = 0; index < orders.Count; index++)
            {
                if (orders[index] != null)
                {
                    clone.orders.Add(orders[index].DeepClone());
                }
            }
        }
        return clone;
    }
}

/// <summary>
/// Entrada cerrada que el servicio construye a partir de autoría + cotización 2.3D.
/// El engine de pedidos no consulta mercado ni promociones directamente.
/// </summary>
public sealed class BistroBuilderPurchaseOrderConfirmationLineInput
{
    public string purchaseOrderLineId;
    public string supplierOfferId;
    public string supplierId;
    public string ingredientId;
    public string ingredientDisplayName;
    public string canonicalUnit;
    public string packageFormatId;
    public string packageDisplayName;
    public string packageType;
    public BistroBuilderCommercialPackageLogisticSize logisticSize;
    public long packageNetQuantityMicrounits;
    public int packageCount;
    public int minimumPackageCount;
    public int orderIncrement;
    public long basePriceCents;
    public long marketPriceCents;
    public long effectiveUnitPriceCents;
    public BistroBuilderSupplierOfferAvailability availability;
    public bool availableForNewOrders;
    public bool hasActivePromotion;
    public string promotionId;
    public int promotionStartGameDay;
    public int promotionEndGameDayExclusive;
    public int discountBasisPoints;
    public string promotionReasonCode;
    public string promotionReasonText;
    public float quotedLeadTimeGameHours;
    public long sourceMarketRevision;
    public long sourceCommercialRevision;
}

public sealed class BistroBuilderPurchaseOrderConfirmationPreview
{
    public string purchaseOrderId;
    public string displayCode;
    public string supplierId;
    public string supplierDisplayName;
    public int lineCount;
    public long subtotalCents;
    public long shippingCostCents;
    public long totalCents;
    public long minimumOrderValueCents;
    public bool minimumOrderSatisfied;
    public bool canConfirm;
    public List<string> blockers = new List<string>();
    public float quotedLeadTimeGameHours;
    public string currencyCode;
    public List<BistroBuilderPurchaseOrderConfirmedLineSnapshot> lines =
        new List<BistroBuilderPurchaseOrderConfirmedLineSnapshot>();

    public BistroBuilderPurchaseOrderConfirmationPreview DeepClone()
    {
        BistroBuilderPurchaseOrderConfirmationPreview clone =
            new BistroBuilderPurchaseOrderConfirmationPreview
            {
                purchaseOrderId = purchaseOrderId,
                displayCode = displayCode,
                supplierId = supplierId,
                supplierDisplayName = supplierDisplayName,
                lineCount = lineCount,
                subtotalCents = subtotalCents,
                shippingCostCents = shippingCostCents,
                totalCents = totalCents,
                minimumOrderValueCents = minimumOrderValueCents,
                minimumOrderSatisfied = minimumOrderSatisfied,
                canConfirm = canConfirm,
                quotedLeadTimeGameHours = quotedLeadTimeGameHours,
                currencyCode = currencyCode
            };
        if (blockers != null)
        {
            clone.blockers.AddRange(blockers);
        }
        if (lines != null)
        {
            for (int index = 0; index < lines.Count; index++)
            {
                if (lines[index] != null)
                {
                    clone.lines.Add(lines[index].DeepClone());
                }
            }
        }
        return clone;
    }
}

public sealed class BistroBuilderPurchaseOrderConfirmationReceipt
{
    public string purchaseOrderId;
    public string displayCode;
    public string supplierId;
    public string supplierDisplayName;
    public int lineCount;
    public long subtotalCents;
    public long shippingCostCents;
    public long totalCents;
    public string currencyCode;
    public int confirmedGameDay;
    public float quotedLeadTimeGameHours;
    public BistroBuilderPurchaseOrderStatus status;

    public BistroBuilderPurchaseOrderConfirmationReceipt DeepClone()
    {
        return new BistroBuilderPurchaseOrderConfirmationReceipt
        {
            purchaseOrderId = purchaseOrderId,
            displayCode = displayCode,
            supplierId = supplierId,
            supplierDisplayName = supplierDisplayName,
            lineCount = lineCount,
            subtotalCents = subtotalCents,
            shippingCostCents = shippingCostCents,
            totalCents = totalCents,
            currencyCode = currencyCode,
            confirmedGameDay = confirmedGameDay,
            quotedLeadTimeGameHours = quotedLeadTimeGameHours,
            status = status
        };
    }
}
