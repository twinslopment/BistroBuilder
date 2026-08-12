#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal static class BistroBuilderSuppliers23ETestData
{
    public static bool TryFindSupplierWithActiveOffer(
        BistroBuilderSupplierAuthoringDatabase suppliers,
        out BistroBuilderSupplierAuthoringRecord supplier,
        out BistroBuilderSupplierBaseOfferAuthoringRecord offer)
    {
        supplier = null;
        offer = null;
        if (suppliers == null) return false;
        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord s = suppliers.Suppliers[supplierIndex];
            if (s == null || !s.isActive || s.baseOffers == null) continue;
            for (int offerIndex = 0; offerIndex < s.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord o = s.baseOffers[offerIndex];
                if (o != null && o.isActive)
                {
                    supplier = s;
                    offer = o;
                    return true;
                }
            }
        }
        return false;
    }

    public static int CountActiveSuppliers(BistroBuilderSupplierAuthoringDatabase suppliers)
    {
        int count = 0;
        if (suppliers == null) return count;
        for (int index = 0; index < suppliers.Suppliers.Count; index++)
        {
            if (suppliers.Suppliers[index] != null && suppliers.Suppliers[index].isActive) count++;
        }
        return count;
    }

    public static int CountActiveOffers(BistroBuilderSupplierAuthoringDatabase suppliers)
    {
        int count = 0;
        if (suppliers == null) return count;
        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null) continue;
            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                if (supplier.baseOffers[offerIndex] != null && supplier.baseOffers[offerIndex].isActive) count++;
            }
        }
        return count;
    }

    public static bool TryBuildValidDraftAndInputs(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderIngredientAuthoringDatabase ingredients,
        BistroBuilderSupplierPurchaseOrderSettings settings,
        int gameDay,
        out BistroBuilderPurchaseOrderRecord order,
        out List<BistroBuilderPurchaseOrderConfirmationLineInput> inputs,
        out string error)
    {
        order = null;
        inputs = new List<BistroBuilderPurchaseOrderConfirmationLineInput>();
        error = null;
        if (snapshot == null || supplier == null || ingredients == null || settings == null ||
            supplier.baseOffers == null)
        {
            error = "Datos insuficientes para construir pedido de prueba.";
            return false;
        }

        BistroBuilderPurchaseOrderRecord stored;
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryCreateDraft(
                snapshot, supplier.SupplierId, gameDay, settings, out stored, out error))
        {
            return false;
        }

        long target = Math.Max(1L, supplier.minimumOrderValueCents);
        long subtotal = 0L;
        for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count && subtotal < target; offerIndex++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
            if (offer == null || !offer.isActive || offer.basePriceCents <= 0L) continue;

            BistroBuilderIngredientAuthoringRecord ingredient;
            BistroBuilderCommercialPackageAuthoringRecord package;
            if (!BistroBuilderSupplierPurchaseOrderEngine.TryFindActivePackage(
                    ingredients, offer.ingredientId, offer.packageFormatId, out ingredient, out package))
            {
                continue;
            }

            int minimum = Math.Max(1, offer.minimumPackageCount);
            int increment = Math.Max(1, offer.orderIncrement);
            long remaining = Math.Max(1L, target - subtotal);
            long rawPackages = (remaining + offer.basePriceCents - 1L) / offer.basePriceCents;
            int packageCount = rawPackages > int.MaxValue ? int.MaxValue : (int)Math.Max(minimum, rawPackages);
            if (packageCount > minimum)
            {
                int offset = packageCount - minimum;
                int remainder = offset % increment;
                if (remainder != 0) packageCount += increment - remainder;
            }

            if (!BistroBuilderSupplierPurchaseOrderEngine.TrySetDraftLine(
                    snapshot, stored, offer, packageCount, gameDay, settings, out error))
            {
                return false;
            }

            long lineSubtotal;
            try
            {
                lineSubtotal = checked(offer.basePriceCents * (long)packageCount);
            }
            catch (OverflowException)
            {
                error = "Overflow al construir pedido de prueba.";
                return false;
            }
            subtotal += lineSubtotal;
            BistroBuilderPurchaseOrderDraftLine draftLine = stored.draftLines[stored.draftLines.Count - 1];
            inputs.Add(BuildInput(
                draftLine,
                supplier,
                offer,
                ingredient,
                package,
                offer.basePriceCents,
                BistroBuilderSupplierOfferAvailability.Disponible,
                true,
                gameDay,
                1L,
                1L));
        }

        if (stored.draftLines.Count == 0)
        {
            error = "No se encontró ninguna oferta apta para construir un pedido de prueba.";
            return false;
        }
        order = stored;
        return true;
    }

    public static BistroBuilderPurchaseOrderConfirmationLineInput BuildInput(
        BistroBuilderPurchaseOrderDraftLine draftLine,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        BistroBuilderIngredientAuthoringRecord ingredient,
        BistroBuilderCommercialPackageAuthoringRecord package,
        long effectivePriceCents,
        BistroBuilderSupplierOfferAvailability availability,
        bool availableForNewOrders,
        int gameDay,
        long marketRevision,
        long commercialRevision)
    {
        return new BistroBuilderPurchaseOrderConfirmationLineInput
        {
            purchaseOrderLineId = draftLine.purchaseOrderLineId,
            supplierOfferId = offer.SupplierOfferId,
            supplierId = supplier.SupplierId,
            ingredientId = offer.ingredientId,
            ingredientDisplayName = ingredient != null ? ingredient.displayNameSnapshot : offer.ingredientId,
            canonicalUnit = ingredient != null ? ingredient.canonicalUnitSnapshot : string.Empty,
            packageFormatId = offer.packageFormatId,
            packageDisplayName = package != null ? package.displayName : offer.packageFormatId,
            packageType = package != null ? package.packageType : string.Empty,
            logisticSize = package != null ? package.logisticSize : BistroBuilderCommercialPackageLogisticSize.Medio,
            packageNetQuantityMicrounits = package != null ? package.netQuantityMicrounits : 1L,
            packageCount = draftLine.packageCount,
            minimumPackageCount = Math.Max(1, offer.minimumPackageCount),
            orderIncrement = Math.Max(1, offer.orderIncrement),
            basePriceCents = offer.basePriceCents,
            marketPriceCents = Math.Max(1L, effectivePriceCents),
            effectiveUnitPriceCents = Math.Max(1L, effectivePriceCents),
            availability = availability,
            availableForNewOrders = availableForNewOrders,
            hasActivePromotion = false,
            promotionStartGameDay = 0,
            quotedLeadTimeGameHours = offer.overrideLeadTime
                ? Math.Max(0.1f, offer.leadTimeOverrideGameHours)
                : Math.Max(0.1f, supplier.defaultLeadTimeGameHours),
            sourceMarketRevision = marketRevision,
            sourceCommercialRevision = commercialRevision
        };
    }
}
#endif
