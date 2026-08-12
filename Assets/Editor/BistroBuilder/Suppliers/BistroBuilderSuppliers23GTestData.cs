#if UNITY_EDITOR
using System;

internal static class BistroBuilderSuppliers23GTestData
{
    public static BistroBuilderPurchaseOrderRecord BuildConfirmedOrder(
        BistroBuilderSupplierAuthoringRecord supplier,
        string orderId,
        int confirmedDay,
        float leadHours,
        int packageCount,
        BistroBuilderCommercialPackageLogisticSize size)
    {
        BistroBuilderPurchaseOrderRecord order = new BistroBuilderPurchaseOrderRecord
        {
            purchaseOrderId = orderId,
            displayCode = "PO-TEST",
            supplierId = supplier.SupplierId,
            status = BistroBuilderPurchaseOrderStatus.Confirmed,
            createdGameDay = confirmedDay,
            confirmedGameDay = confirmedDay,
            lastModifiedGameDay = confirmedDay,
            quotedLeadTimeGameHours = leadHours,
            stateRevision = 2,
            supplierTerms = new BistroBuilderPurchaseOrderSupplierTermsSnapshot
            {
                supplierId = supplier.SupplierId,
                supplierDisplayName = supplier.displayName,
                reliabilityTier = supplier.reliabilityTier,
                reliabilityValue = supplier.reliabilityValue,
                defaultLeadTimeGameHours = supplier.defaultLeadTimeGameHours,
                preferredVehicle = supplier.logisticsProfile != null ? supplier.logisticsProfile.preferredVehicle : BistroBuilderSupplierVehiclePreference.Automatico,
                vehiclePresentationProfileId = supplier.logisticsProfile != null ? supplier.logisticsProfile.vehiclePresentationProfileId : "vehicle_supplier_default",
                driverPresentationProfileId = supplier.logisticsProfile != null ? supplier.logisticsProfile.driverPresentationProfileId : "driver_supplier_default"
            }
        };
        if (supplier.deliveryWindows != null)
        {
            for (int index = 0; index < supplier.deliveryWindows.Count; index++)
            {
                BistroBuilderSupplierDeliveryWindowAuthoring source = supplier.deliveryWindows[index];
                if (source == null) continue;
                order.supplierTerms.deliveryWindows.Add(new BistroBuilderPurchaseOrderDeliveryWindowSnapshot
                {
                    startMinuteOfDay = source.startMinuteOfDay,
                    endMinuteOfDay = source.endMinuteOfDay,
                    monday = source.monday,
                    tuesday = source.tuesday,
                    wednesday = source.wednesday,
                    thursday = source.thursday,
                    friday = source.friday,
                    saturday = source.saturday,
                    sunday = source.sunday
                });
            }
        }
        order.confirmedLines.Add(new BistroBuilderPurchaseOrderConfirmedLineSnapshot
        {
            purchaseOrderLineId = "line_test_1",
            supplierOfferId = "offer_test_1",
            supplierId = supplier.SupplierId,
            ingredientId = "ingredient_test",
            packageFormatId = "package_test",
            packageDisplayName = "Formato test",
            packageCount = Math.Max(1, packageCount),
            logisticSize = size,
            packageNetQuantityMicrounits = 1000000L,
            totalNetQuantityMicrounits = Math.Max(1, packageCount) * 1000000L,
            effectiveUnitPriceCents = 1000L,
            lineSubtotalCents = Math.Max(1, packageCount) * 1000L,
            quotedLeadTimeGameHours = leadHours
        });
        return order;
    }

    public static BistroBuilderSupplierAuthoringRecord FirstActiveSupplier(BistroBuilderSupplierAuthoringDatabase database)
    {
        if (database == null) return null;
        for (int index = 0; index < database.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = database.Suppliers[index];
            if (supplier != null && supplier.isActive) return supplier;
        }
        return null;
    }
}
#endif
