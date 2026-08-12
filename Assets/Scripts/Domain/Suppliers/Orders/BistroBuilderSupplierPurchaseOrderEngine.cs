using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Reglas puras de 2.3E. No consulta servicios runtime ni escribe Inventario/Recepciones.
/// El servicio de aplicación resuelve autoría + cotización 2.3D y entrega entradas cerradas.
/// </summary>
public static class BistroBuilderSupplierPurchaseOrderEngine
{
    public static BistroBuilderSupplierPurchaseOrdersSnapshot CreateInitialSnapshot(
        int gameDay,
        ulong sourceMarketSeed = 0UL,
        ulong sourceCommercialSeed = 0UL)
    {
        return new BistroBuilderSupplierPurchaseOrdersSnapshot
        {
            currentGameDay = Math.Max(1, gameDay),
            sourceMarketSeed = sourceMarketSeed,
            sourceCommercialSeed = sourceCommercialSeed,
            ordersRevision = 1,
            nextOrderSequence = 1
        };
    }

    public static bool TryCreateDraft(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        string supplierId,
        int gameDay,
        BistroBuilderSupplierPurchaseOrderSettings settings,
        out BistroBuilderPurchaseOrderRecord created,
        out string error)
    {
        created = null;
        error = null;
        if (snapshot == null)
        {
            error = "Snapshot de pedidos nulo.";
            return false;
        }
        if (settings == null)
        {
            error = "supplier.orders.settings no está disponible.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(supplierId))
        {
            error = "SupplierId vacío.";
            return false;
        }
        if (snapshot.orders == null)
        {
            snapshot.orders = new List<BistroBuilderPurchaseOrderRecord>();
        }
        if (snapshot.orders.Count >= settings.MaximumOrdersInSnapshot)
        {
            error = "Se alcanzó el límite defensivo de pedidos del snapshot.";
            return false;
        }

        long sequence = Math.Max(1L, snapshot.nextOrderSequence);
        string technicalId = "purchase_order_" + sequence.ToString("D8");
        string displayCode = settings.DisplayCodePrefix + "-" + sequence.ToString("D6");
        int safeDay = Math.Max(1, gameDay);

        created = new BistroBuilderPurchaseOrderRecord
        {
            purchaseOrderId = technicalId,
            displayCode = displayCode,
            supplierId = supplierId,
            status = BistroBuilderPurchaseOrderStatus.Draft,
            createdGameDay = safeDay,
            lastModifiedGameDay = safeDay,
            stateRevision = 1,
            nextLineSequence = 1,
            currencyCode = settings.CurrencyCode
        };

        snapshot.orders.Add(created);
        snapshot.nextOrderSequence = sequence + 1;
        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, safeDay);
        snapshot.ordersRevision++;
        return true;
    }

    public static bool TrySetDraftLine(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        int packageCount,
        int gameDay,
        BistroBuilderSupplierPurchaseOrderSettings settings,
        out string error)
    {
        error = null;
        if (snapshot == null || order == null || offer == null || settings == null)
        {
            error = "Datos insuficientes para editar la línea del pedido.";
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Draft)
        {
            error = "Solo los pedidos Draft se pueden editar.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(offer.SupplierOfferId))
        {
            error = "La oferta no tiene SupplierOfferId estable.";
            return false;
        }
        if (!IsPackageCountValid(offer, packageCount))
        {
            error = "La cantidad debe respetar mínimo " + Math.Max(1, offer.minimumPackageCount) +
                    " e incremento " + Math.Max(1, offer.orderIncrement) + ".";
            return false;
        }
        if (order.draftLines == null)
        {
            order.draftLines = new List<BistroBuilderPurchaseOrderDraftLine>();
        }

        BistroBuilderPurchaseOrderDraftLine existing = null;
        for (int index = 0; index < order.draftLines.Count; index++)
        {
            BistroBuilderPurchaseOrderDraftLine candidate = order.draftLines[index];
            if (candidate != null &&
                string.Equals(candidate.supplierOfferId, offer.SupplierOfferId, StringComparison.Ordinal))
            {
                existing = candidate;
                break;
            }
        }

        if (existing == null)
        {
            if (order.draftLines.Count >= settings.MaximumLinesPerOrder)
            {
                error = "Se alcanzó el máximo de líneas permitido por pedido.";
                return false;
            }

            long lineSequence = Math.Max(1L, order.nextLineSequence);
            existing = new BistroBuilderPurchaseOrderDraftLine
            {
                purchaseOrderLineId = order.purchaseOrderId + "_line_" + lineSequence.ToString("D4"),
                supplierOfferId = offer.SupplierOfferId,
                packageCount = packageCount,
                sortOrder = order.draftLines.Count
            };
            order.draftLines.Add(existing);
            order.nextLineSequence = lineSequence + 1;
        }
        else
        {
            existing.packageCount = packageCount;
        }

        Touch(snapshot, order, gameDay);
        return true;
    }

    public static bool TryRemoveDraftLine(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        string supplierOfferId,
        int gameDay,
        out string error)
    {
        error = null;
        if (snapshot == null || order == null || string.IsNullOrWhiteSpace(supplierOfferId))
        {
            error = "Datos insuficientes para eliminar la línea.";
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Draft)
        {
            error = "Solo los pedidos Draft se pueden editar.";
            return false;
        }
        if (order.draftLines == null)
        {
            error = "La línea no existe.";
            return false;
        }

        for (int index = 0; index < order.draftLines.Count; index++)
        {
            BistroBuilderPurchaseOrderDraftLine line = order.draftLines[index];
            if (line != null && string.Equals(line.supplierOfferId, supplierOfferId, StringComparison.Ordinal))
            {
                order.draftLines.RemoveAt(index);
                Touch(snapshot, order, gameDay);
                return true;
            }
        }

        error = "La oferta indicada no forma parte del borrador.";
        return false;
    }

    public static bool TryBuildConfirmationPreview(
        BistroBuilderPurchaseOrderRecord order,
        BistroBuilderSupplierAuthoringRecord supplier,
        IList<BistroBuilderPurchaseOrderConfirmationLineInput> inputs,
        BistroBuilderSupplierPurchaseOrderSettings settings,
        out BistroBuilderPurchaseOrderConfirmationPreview preview,
        out string error)
    {
        preview = null;
        error = null;
        if (order == null || supplier == null || inputs == null || settings == null)
        {
            error = "Datos insuficientes para construir la cotización del pedido.";
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Draft)
        {
            error = "Solo un pedido Draft puede cotizarse para confirmación.";
            return false;
        }
        if (!string.Equals(order.supplierId, supplier.SupplierId, StringComparison.Ordinal))
        {
            error = "El proveedor del pedido no coincide con el proveedor cotizado.";
            return false;
        }

        int draftLineCount = order.draftLines != null ? order.draftLines.Count : 0;
        if (inputs.Count != draftLineCount)
        {
            error = "La cotización no contiene exactamente las mismas líneas que el Draft.";
            return false;
        }
        HashSet<string> matchedDraftLineIds = new HashSet<string>(StringComparer.Ordinal);
        for (int inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
        {
            BistroBuilderPurchaseOrderConfirmationLineInput input = inputs[inputIndex];
            if (input == null || string.IsNullOrWhiteSpace(input.purchaseOrderLineId) ||
                !matchedDraftLineIds.Add(input.purchaseOrderLineId))
            {
                error = "La cotización contiene líneas nulas o duplicadas.";
                return false;
            }

            bool matched = false;
            for (int draftIndex = 0; draftIndex < draftLineCount; draftIndex++)
            {
                BistroBuilderPurchaseOrderDraftLine draft = order.draftLines[draftIndex];
                if (draft != null &&
                    string.Equals(draft.purchaseOrderLineId, input.purchaseOrderLineId, StringComparison.Ordinal) &&
                    string.Equals(draft.supplierOfferId, input.supplierOfferId, StringComparison.Ordinal) &&
                    draft.packageCount == input.packageCount)
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                error = input.purchaseOrderLineId + ": la cotización no converge con la línea Draft original.";
                return false;
            }
        }

        preview = new BistroBuilderPurchaseOrderConfirmationPreview
        {
            purchaseOrderId = order.purchaseOrderId,
            displayCode = order.displayCode,
            supplierId = supplier.SupplierId,
            supplierDisplayName = supplier.displayName,
            currencyCode = settings.CurrencyCode,
            minimumOrderValueCents = Math.Max(0L, supplier.minimumOrderValueCents)
        };

        if (inputs.Count == 0)
        {
            preview.blockers.Add("El pedido no contiene líneas.");
        }

        long subtotal = 0L;
        float maxLeadTime = Math.Max(0.1f, supplier.defaultLeadTimeGameHours);
        for (int index = 0; index < inputs.Count; index++)
        {
            BistroBuilderPurchaseOrderConfirmationLineInput input = inputs[index];
            if (input == null)
            {
                preview.blockers.Add("Existe una línea sin datos comerciales.");
                continue;
            }
            if (!string.Equals(input.supplierId, supplier.SupplierId, StringComparison.Ordinal))
            {
                preview.blockers.Add(input.supplierOfferId + ": pertenece a otro proveedor.");
                continue;
            }
            if (!input.availableForNewOrders ||
                input.availability == BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado)
            {
                preview.blockers.Add(input.supplierOfferId + ": temporalmente agotado para nuevos pedidos.");
            }
            if (input.packageCount < Math.Max(1, input.minimumPackageCount))
            {
                preview.blockers.Add(input.supplierOfferId + ": no alcanza el mínimo de paquetes.");
            }
            int increment = Math.Max(1, input.orderIncrement);
            int minimum = Math.Max(1, input.minimumPackageCount);
            if ((input.packageCount - minimum) % increment != 0)
            {
                preview.blockers.Add(input.supplierOfferId + ": cantidad incompatible con el incremento comercial.");
            }
            if (input.effectiveUnitPriceCents <= 0L || input.marketPriceCents <= 0L || input.basePriceCents <= 0L)
            {
                preview.blockers.Add(input.supplierOfferId + ": contiene un precio no positivo.");
            }
            if (input.packageNetQuantityMicrounits <= 0L)
            {
                preview.blockers.Add(input.supplierOfferId + ": formato comercial sin cantidad válida.");
            }

            long lineSubtotal;
            long totalQuantity;
            try
            {
                lineSubtotal = checked(input.effectiveUnitPriceCents * (long)input.packageCount);
                totalQuantity = checked(input.packageNetQuantityMicrounits * (long)input.packageCount);
                subtotal = checked(subtotal + lineSubtotal);
            }
            catch (OverflowException)
            {
                error = "Desbordamiento al calcular importes o cantidades del pedido.";
                return false;
            }

            preview.lines.Add(new BistroBuilderPurchaseOrderConfirmedLineSnapshot
            {
                purchaseOrderLineId = input.purchaseOrderLineId,
                supplierOfferId = input.supplierOfferId,
                supplierId = input.supplierId,
                ingredientId = input.ingredientId,
                ingredientDisplayName = input.ingredientDisplayName,
                canonicalUnit = input.canonicalUnit,
                packageFormatId = input.packageFormatId,
                packageDisplayName = input.packageDisplayName,
                packageType = input.packageType,
                logisticSize = input.logisticSize,
                packageNetQuantityMicrounits = input.packageNetQuantityMicrounits,
                packageCount = input.packageCount,
                totalNetQuantityMicrounits = totalQuantity,
                minimumPackageCount = input.minimumPackageCount,
                orderIncrement = input.orderIncrement,
                basePriceCents = input.basePriceCents,
                marketPriceCents = input.marketPriceCents,
                effectiveUnitPriceCents = input.effectiveUnitPriceCents,
                lineSubtotalCents = lineSubtotal,
                availabilityAtConfirmation = input.availability,
                hadActivePromotion = input.hasActivePromotion,
                promotionId = input.promotionId,
                promotionStartGameDay = input.promotionStartGameDay,
                promotionEndGameDayExclusive = input.promotionEndGameDayExclusive,
                discountBasisPoints = input.discountBasisPoints,
                promotionReasonCode = input.promotionReasonCode,
                promotionReasonText = input.promotionReasonText,
                quotedLeadTimeGameHours = Math.Max(0.1f, input.quotedLeadTimeGameHours),
                sourceMarketRevision = input.sourceMarketRevision,
                sourceCommercialRevision = input.sourceCommercialRevision
            });

            maxLeadTime = Math.Max(maxLeadTime, Math.Max(0.1f, input.quotedLeadTimeGameHours));
        }

        preview.lineCount = preview.lines.Count;
        preview.subtotalCents = subtotal;
        preview.minimumOrderSatisfied = subtotal >= Math.Max(0L, supplier.minimumOrderValueCents);
        if (!preview.minimumOrderSatisfied)
        {
            preview.blockers.Add(
                "El subtotal no alcanza el pedido mínimo del proveedor (" +
                Math.Max(0L, supplier.minimumOrderValueCents) + " céntimos)." );
        }

        long shipping = Math.Max(0L, supplier.shippingCostCents);
        if (supplier.freeShippingEnabled &&
            subtotal >= Math.Max(0L, supplier.freeShippingThresholdCents))
        {
            shipping = 0L;
        }

        preview.shippingCostCents = shipping;
        try
        {
            preview.totalCents = checked(subtotal + shipping);
        }
        catch (OverflowException)
        {
            error = "Desbordamiento al calcular el total del pedido.";
            return false;
        }
        preview.quotedLeadTimeGameHours = maxLeadTime;
        preview.canConfirm = preview.blockers.Count == 0;
        return true;
    }

    public static bool TryConfirm(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderPurchaseOrderConfirmationPreview preview,
        int gameDay,
        long marketRevision,
        long commercialRevision,
        out BistroBuilderPurchaseOrderConfirmationReceipt receipt,
        out string error)
    {
        receipt = null;
        error = null;
        if (snapshot == null || order == null || supplier == null || preview == null)
        {
            error = "Datos insuficientes para confirmar el pedido.";
            return false;
        }
        if (order.status == BistroBuilderPurchaseOrderStatus.Confirmed)
        {
            receipt = BuildReceipt(order);
            return true;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Draft)
        {
            error = "Solo un pedido Draft puede confirmarse.";
            return false;
        }
        if (snapshot.sourceMarketSeed == 0UL || snapshot.sourceCommercialSeed == 0UL)
        {
            error = "supplier.orders.runtime debe estar vinculado a una sesión concreta de 2.3C/2.3D antes de confirmar.";
            return false;
        }
        if (!preview.canConfirm)
        {
            error = preview.blockers != null && preview.blockers.Count > 0
                ? string.Join(" | ", preview.blockers.ToArray())
                : "La cotización actual no permite confirmar el pedido.";
            return false;
        }
        if (!string.Equals(order.purchaseOrderId, preview.purchaseOrderId, StringComparison.Ordinal) ||
            !string.Equals(order.supplierId, preview.supplierId, StringComparison.Ordinal))
        {
            error = "La cotización no pertenece al pedido que se intenta confirmar.";
            return false;
        }
        if (!ValidateConfirmationPreviewForCommit(order, supplier, preview, out error))
        {
            return false;
        }

        int safeDay = Math.Max(1, gameDay);
        order.confirmedLines.Clear();
        for (int index = 0; index < preview.lines.Count; index++)
        {
            if (preview.lines[index] != null)
            {
                order.confirmedLines.Add(preview.lines[index].DeepClone());
            }
        }

        order.draftLines.Clear();
        order.supplierTerms = BuildSupplierTermsSnapshot(supplier, preview.shippingCostCents);
        order.subtotalCents = preview.subtotalCents;
        order.shippingCostCents = preview.shippingCostCents;
        order.totalCents = preview.totalCents;
        order.quotedLeadTimeGameHours = preview.quotedLeadTimeGameHours;
        order.sourceMarketRevision = marketRevision;
        order.sourceCommercialRevision = commercialRevision;
        order.currencyCode = preview.currencyCode;
        order.status = BistroBuilderPurchaseOrderStatus.Confirmed;
        order.confirmedGameDay = safeDay;
        order.lastModifiedGameDay = safeDay;
        order.stateRevision++;
        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, safeDay);
        snapshot.ordersRevision++;
        receipt = BuildReceipt(order);
        return true;
    }

    public static bool TryCancel(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        string reason,
        int gameDay,
        BistroBuilderSupplierPurchaseOrderSettings settings,
        out string error)
    {
        error = null;
        if (snapshot == null || order == null || settings == null)
        {
            error = "Datos insuficientes para cancelar el pedido.";
            return false;
        }
        if (order.status == BistroBuilderPurchaseOrderStatus.Cancelled)
        {
            return true;
        }
        if (order.status == BistroBuilderPurchaseOrderStatus.InDelivery)
        {
            error = "Un pedido InDelivery ya no puede cancelarse.";
            return false;
        }
        if (order.status == BistroBuilderPurchaseOrderStatus.Delivered)
        {
            error = "Un pedido Delivered no puede cancelarse.";
            return false;
        }
        if (order.status == BistroBuilderPurchaseOrderStatus.Confirmed && !settings.AllowCancelConfirmed)
        {
            error = "La configuración no permite cancelar pedidos Confirmed.";
            return false;
        }
        if (order.status == BistroBuilderPurchaseOrderStatus.PendingDelivery && !settings.AllowCancelPendingDelivery)
        {
            error = "La configuración no permite cancelar pedidos PendingDelivery.";
            return false;
        }

        int safeDay = Math.Max(1, gameDay);
        if (safeDay < Math.Max(1, order.createdGameDay))
        {
            error = "La cancelación no puede preceder a la creación del pedido.";
            return false;
        }
        order.status = BistroBuilderPurchaseOrderStatus.Cancelled;
        order.cancelledGameDay = safeDay;
        order.lastModifiedGameDay = safeDay;
        order.cancellationReason = string.IsNullOrWhiteSpace(reason)
            ? "Cancelado por el jugador."
            : reason.Trim();
        order.stateRevision++;
        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, safeDay);
        snapshot.ordersRevision++;
        return true;
    }

    public static bool TryMarkPendingDelivery(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        string logisticsPlanId,
        int plannedDeliveryGameDay,
        int windowStartMinuteOfDay,
        int windowEndMinuteOfDay,
        int gameDay,
        out string error)
    {
        error = null;
        if (snapshot == null || order == null)
        {
            error = "Datos insuficientes para planificar la entrega.";
            return false;
        }
        string normalizedPlanId = string.IsNullOrWhiteSpace(logisticsPlanId)
            ? null
            : logisticsPlanId.Trim();
        if (order.status == BistroBuilderPurchaseOrderStatus.PendingDelivery)
        {
            if (!string.IsNullOrEmpty(normalizedPlanId) &&
                string.Equals(order.logisticsPlanId, normalizedPlanId, StringComparison.Ordinal) &&
                order.plannedDeliveryGameDay == plannedDeliveryGameDay &&
                order.plannedDeliveryWindowStartMinuteOfDay == windowStartMinuteOfDay &&
                order.plannedDeliveryWindowEndMinuteOfDay == windowEndMinuteOfDay)
            {
                return true;
            }
            error = "El pedido ya está PendingDelivery con otro plan. Usa TryUpdatePendingDeliveryPlan para replanificar.";
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Confirmed)
        {
            error = "Solo un pedido Confirmed puede pasar a PendingDelivery.";
            return false;
        }
        if (string.IsNullOrEmpty(normalizedPlanId))
        {
            error = "2.3G debe aportar un LogisticsPlanId estable.";
            return false;
        }
        int safeDay = Math.Max(1, gameDay);
        if (safeDay < Math.Max(1, order.confirmedGameDay))
        {
            error = "PendingDelivery no puede registrarse antes de la confirmación.";
            return false;
        }
        if (plannedDeliveryGameDay < safeDay)
        {
            error = "La entrega planificada no puede quedar en el pasado respecto al reloj actual.";
            return false;
        }
        if (windowStartMinuteOfDay < 0 || windowEndMinuteOfDay <= windowStartMinuteOfDay ||
            windowEndMinuteOfDay > 24 * 60)
        {
            error = "La ventana de entrega planificada no es válida.";
            return false;
        }

        order.logisticsPlanId = normalizedPlanId;
        order.plannedDeliveryGameDay = plannedDeliveryGameDay;
        order.plannedDeliveryWindowStartMinuteOfDay = windowStartMinuteOfDay;
        order.plannedDeliveryWindowEndMinuteOfDay = windowEndMinuteOfDay;
        order.plannedDelayGameMinutes = 0;
        order.status = BistroBuilderPurchaseOrderStatus.PendingDelivery;
        order.pendingDeliveryGameDay = safeDay;
        order.lastModifiedGameDay = safeDay;
        order.stateRevision++;
        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, safeDay);
        snapshot.ordersRevision++;
        return true;
    }


    public static bool TryUpdatePendingDeliveryPlan(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        string logisticsPlanId,
        int plannedDeliveryGameDay,
        int windowStartMinuteOfDay,
        int windowEndMinuteOfDay,
        int plannedDelayGameMinutes,
        int gameDay,
        out string error)
    {
        error = null;
        if (snapshot == null || order == null)
        {
            error = "Datos insuficientes para actualizar el plan de entrega.";
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.PendingDelivery)
        {
            error = "Solo un pedido PendingDelivery puede replanificarse.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(logisticsPlanId) ||
            !string.Equals(order.logisticsPlanId, logisticsPlanId.Trim(), StringComparison.Ordinal))
        {
            error = "La replanificación debe conservar el LogisticsPlanId del pedido.";
            return false;
        }
        int safeDelay = Math.Max(0, plannedDelayGameMinutes);
        if (order.plannedDeliveryGameDay == plannedDeliveryGameDay &&
            order.plannedDeliveryWindowStartMinuteOfDay == windowStartMinuteOfDay &&
            order.plannedDeliveryWindowEndMinuteOfDay == windowEndMinuteOfDay &&
            order.plannedDelayGameMinutes == safeDelay)
        {
            return true;
        }

        int safeDay = Math.Max(1, gameDay);
        if (safeDay < Math.Max(1, order.pendingDeliveryGameDay))
        {
            error = "La replanificación no puede preceder al estado PendingDelivery.";
            return false;
        }
        if (plannedDeliveryGameDay < safeDay)
        {
            error = "La nueva entrega planificada no puede quedar en el pasado respecto al reloj actual.";
            return false;
        }
        if (windowStartMinuteOfDay < 0 || windowEndMinuteOfDay <= windowStartMinuteOfDay ||
            windowEndMinuteOfDay > 24 * 60)
        {
            error = "La nueva ventana de entrega no es válida.";
            return false;
        }

        order.plannedDeliveryGameDay = plannedDeliveryGameDay;
        order.plannedDeliveryWindowStartMinuteOfDay = windowStartMinuteOfDay;
        order.plannedDeliveryWindowEndMinuteOfDay = windowEndMinuteOfDay;
        order.plannedDelayGameMinutes = safeDelay;
        order.lastModifiedGameDay = safeDay;
        order.stateRevision++;
        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, safeDay);
        snapshot.ordersRevision++;
        return true;
    }

    public static bool TryMarkInDelivery(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        int actualDeliveryStartGameDay,
        int appliedDelayGameMinutes,
        int gameDay,
        out string error)
    {
        error = null;
        if (snapshot == null || order == null)
        {
            error = "Datos insuficientes para iniciar el reparto.";
            return false;
        }
        int safeDay = Math.Max(1, gameDay);
        int safeActualStart = Math.Max(1, actualDeliveryStartGameDay);
        int safeAppliedDelay = Math.Max(0, appliedDelayGameMinutes);
        if (order.status == BistroBuilderPurchaseOrderStatus.InDelivery)
        {
            if (order.actualDeliveryStartGameDay == safeActualStart &&
                order.appliedDelayGameMinutes == safeAppliedDelay)
            {
                return true;
            }
            error = "El pedido ya está InDelivery; no se puede reescribir el inicio o retraso aplicado.";
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.PendingDelivery)
        {
            error = "Solo un pedido PendingDelivery puede pasar a InDelivery.";
            return false;
        }

        if (safeActualStart < Math.Max(1, order.pendingDeliveryGameDay) || safeDay < safeActualStart)
        {
            error = "InDelivery no puede comenzar antes de PendingDelivery ni en un día futuro respecto al reloj actual.";
            return false;
        }
        order.status = BistroBuilderPurchaseOrderStatus.InDelivery;
        order.inDeliveryGameDay = safeDay;
        order.actualDeliveryStartGameDay = safeActualStart;
        order.appliedDelayGameMinutes = safeAppliedDelay;
        order.lastModifiedGameDay = safeDay;
        order.stateRevision++;
        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, safeDay);
        snapshot.ordersRevision++;
        return true;
    }

    public static bool TryMarkDelivered(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        string deliveryReceiptId,
        int deliveredGameDay,
        out string error)
    {
        error = null;
        if (snapshot == null || order == null)
        {
            error = "Datos insuficientes para cerrar la entrega.";
            return false;
        }
        string normalizedReceiptId = string.IsNullOrWhiteSpace(deliveryReceiptId)
            ? null
            : deliveryReceiptId.Trim();
        int safeDay = Math.Max(1, deliveredGameDay);
        if (order.status == BistroBuilderPurchaseOrderStatus.Delivered)
        {
            if (!string.IsNullOrEmpty(normalizedReceiptId) &&
                string.Equals(order.deliveryReceiptId, normalizedReceiptId, StringComparison.Ordinal) &&
                order.deliveredGameDay == safeDay)
            {
                return true;
            }
            error = "El pedido ya está Delivered con otro ReceiptId o fecha; el cierre es inmutable.";
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.InDelivery)
        {
            error = "Solo un pedido InDelivery puede pasar a Delivered.";
            return false;
        }
        if (string.IsNullOrEmpty(normalizedReceiptId))
        {
            error = "2.2B debe aportar un ReceiptId estable para cerrar el pedido.";
            return false;
        }

        if (safeDay < Math.Max(1, order.inDeliveryGameDay))
        {
            error = "Delivered no puede preceder al inicio de InDelivery.";
            return false;
        }
        order.status = BistroBuilderPurchaseOrderStatus.Delivered;
        order.deliveredGameDay = safeDay;
        order.deliveryReceiptId = normalizedReceiptId;
        order.lastModifiedGameDay = safeDay;
        order.stateRevision++;
        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, safeDay);
        snapshot.ordersRevision++;
        return true;
    }

    public static bool ValidateSnapshotAgainstAuthoring(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderSupplierAuthoringDatabase suppliers,
        BistroBuilderIngredientAuthoringDatabase ingredients,
        BistroBuilderSupplierPurchaseOrderSettings settings,
        out string error)
    {
        error = null;
        if (snapshot == null || suppliers == null || ingredients == null || settings == null)
        {
            error = "Faltan datos para validar supplier.orders.runtime.";
            return false;
        }
        if (snapshot.schemaId != BistroBuilderSupplierPurchaseOrdersSnapshot.CurrentSchemaId ||
            snapshot.schemaVersion != BistroBuilderSupplierPurchaseOrdersSnapshot.CurrentSchemaVersion)
        {
            error = "supplier.orders.runtime usa un schema incompatible.";
            return false;
        }
        if (snapshot.orders == null || snapshot.orders.Count > settings.MaximumOrdersInSnapshot)
        {
            error = "Colección de pedidos inválida o superior al límite defensivo.";
            return false;
        }
        if (snapshot.currentGameDay < 1 || snapshot.ordersRevision < 1L || snapshot.nextOrderSequence < 1L)
        {
            error = "Metadatos de supplier.orders.runtime inválidos.";
            return false;
        }

        HashSet<string> orderIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> displayCodes = new HashSet<string>(StringComparer.Ordinal);
        long maxOrderSequence = 0L;
        for (int index = 0; index < snapshot.orders.Count; index++)
        {
            BistroBuilderPurchaseOrderRecord order = snapshot.orders[index];
            if (order == null || string.IsNullOrWhiteSpace(order.purchaseOrderId) ||
                string.IsNullOrWhiteSpace(order.displayCode) ||
                !orderIds.Add(order.purchaseOrderId) || !displayCodes.Add(order.displayCode))
            {
                error = "Existe un pedido nulo o con identidad duplicada/no válida.";
                return false;
            }
            long orderSequence;
            if (!TryParseCanonicalOrderSequence(order.purchaseOrderId, out orderSequence) ||
                !string.Equals(
                    order.displayCode,
                    settings.DisplayCodePrefix + "-" + orderSequence.ToString("D6"),
                    StringComparison.Ordinal))
            {
                error = order.purchaseOrderId + ": identidad PurchaseOrder/código visible no canónica.";
                return false;
            }
            maxOrderSequence = Math.Max(maxOrderSequence, orderSequence);

            if (order.stateRevision < 1L || order.nextLineSequence < 1L ||
                order.lastModifiedGameDay > snapshot.currentGameDay ||
                !Enum.IsDefined(typeof(BistroBuilderPurchaseOrderStatus), order.status) ||
                !string.Equals(order.currencyCode, settings.CurrencyCode, StringComparison.Ordinal))
            {
                error = order.purchaseOrderId + ": metadatos de revisión/estado/moneda no válidos.";
                return false;
            }

            BistroBuilderSupplierAuthoringRecord supplier;
            if (!suppliers.TryGetSupplier(order.supplierId, out supplier) || supplier == null)
            {
                error = order.purchaseOrderId + ": SupplierId no existe en supplier.authoring.";
                return false;
            }

            bool hasFrozenCommercialLines = order.confirmedLines != null && order.confirmedLines.Count > 0;
            if (hasFrozenCommercialLines &&
                (snapshot.sourceMarketSeed == 0UL || snapshot.sourceCommercialSeed == 0UL))
            {
                error = order.purchaseOrderId + ": pedido confirmado sin identidad de sesión 2.3C/2.3D.";
                return false;
            }

            long maxLineSequence = 0L;
            HashSet<string> allLineIds = new HashSet<string>(StringComparer.Ordinal);
            if (order.draftLines != null)
            {
                for (int lineIndex = 0; lineIndex < order.draftLines.Count; lineIndex++)
                {
                    BistroBuilderPurchaseOrderDraftLine line = order.draftLines[lineIndex];
                    long lineSequence;
                    if (line == null || !allLineIds.Add(line.purchaseOrderLineId) ||
                        !TryParseCanonicalLineSequence(order.purchaseOrderId, line.purchaseOrderLineId, out lineSequence))
                    {
                        error = order.purchaseOrderId + ": identidad de línea Draft no canónica o duplicada.";
                        return false;
                    }
                    maxLineSequence = Math.Max(maxLineSequence, lineSequence);
                }
            }
            if (order.confirmedLines != null)
            {
                for (int lineIndex = 0; lineIndex < order.confirmedLines.Count; lineIndex++)
                {
                    BistroBuilderPurchaseOrderConfirmedLineSnapshot line = order.confirmedLines[lineIndex];
                    long lineSequence;
                    if (line == null || !allLineIds.Add(line.purchaseOrderLineId) ||
                        !TryParseCanonicalLineSequence(order.purchaseOrderId, line.purchaseOrderLineId, out lineSequence))
                    {
                        error = order.purchaseOrderId + ": identidad de línea confirmada no canónica o duplicada.";
                        return false;
                    }
                    maxLineSequence = Math.Max(maxLineSequence, lineSequence);
                }
            }
            if (order.nextLineSequence <= maxLineSequence)
            {
                error = order.purchaseOrderId + ": NextLineSequence podría reutilizar una identidad ya emitida.";
                return false;
            }

            bool hasEditableDraftLines = order.draftLines != null && order.draftLines.Count > 0;
            bool isActivePostConfirmation =
                order.status == BistroBuilderPurchaseOrderStatus.Confirmed ||
                order.status == BistroBuilderPurchaseOrderStatus.PendingDelivery ||
                order.status == BistroBuilderPurchaseOrderStatus.InDelivery ||
                order.status == BistroBuilderPurchaseOrderStatus.Delivered;
            if (order.status == BistroBuilderPurchaseOrderStatus.Draft &&
                (hasFrozenCommercialLines || order.supplierTerms != null ||
                 order.subtotalCents != 0L || order.shippingCostCents != 0L || order.totalCents != 0L ||
                 order.confirmedGameDay != 0 || order.pendingDeliveryGameDay != 0 ||
                 order.inDeliveryGameDay != 0 || order.deliveredGameDay != 0 || order.cancelledGameDay != 0))
            {
                error = order.purchaseOrderId + ": Draft contiene estado congelado de fases posteriores.";
                return false;
            }
            if ((isActivePostConfirmation ||
                 (order.status == BistroBuilderPurchaseOrderStatus.Cancelled && hasFrozenCommercialLines)) &&
                hasEditableDraftLines)
            {
                error = order.purchaseOrderId + ": un pedido confirmado no puede conservar líneas Draft editables.";
                return false;
            }
            if (order.status == BistroBuilderPurchaseOrderStatus.Cancelled &&
                (order.inDeliveryGameDay > 0 || order.deliveredGameDay > 0 ||
                 order.actualDeliveryStartGameDay > 0 || !string.IsNullOrWhiteSpace(order.deliveryReceiptId)))
            {
                error = order.purchaseOrderId + ": Cancelled conserva hitos incompatibles con una expedición ya iniciada.";
                return false;
            }

            if (order.status == BistroBuilderPurchaseOrderStatus.Draft)
            {
                if (order.draftLines == null || order.draftLines.Count > settings.MaximumLinesPerOrder)
                {
                    error = order.purchaseOrderId + ": líneas Draft inválidas.";
                    return false;
                }
                HashSet<string> lineIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> offerIds = new HashSet<string>(StringComparer.Ordinal);
                for (int lineIndex = 0; lineIndex < order.draftLines.Count; lineIndex++)
                {
                    BistroBuilderPurchaseOrderDraftLine line = order.draftLines[lineIndex];
                    if (line == null || string.IsNullOrWhiteSpace(line.purchaseOrderLineId) ||
                        !lineIds.Add(line.purchaseOrderLineId) ||
                        string.IsNullOrWhiteSpace(line.supplierOfferId) || !offerIds.Add(line.supplierOfferId))
                    {
                        error = order.purchaseOrderId + ": línea Draft duplicada o inválida.";
                        return false;
                    }
                    BistroBuilderSupplierBaseOfferAuthoringRecord offer;
                    if (!TryFindActiveOffer(supplier, line.supplierOfferId, out offer) || offer == null ||
                        !IsPackageCountValid(offer, line.packageCount))
                    {
                        error = order.purchaseOrderId + ": línea Draft no converge con la oferta activa.";
                        return false;
                    }
                }
            }
            else if (order.status != BistroBuilderPurchaseOrderStatus.Cancelled ||
                     (order.confirmedLines != null && order.confirmedLines.Count > 0))
            {
                if (order.status != BistroBuilderPurchaseOrderStatus.Cancelled &&
                    (order.confirmedLines == null || order.confirmedLines.Count == 0))
                {
                    error = order.purchaseOrderId + ": pedido no Draft sin snapshot comercial de líneas.";
                    return false;
                }
                if (order.confirmedLines != null)
                {
                    if (order.confirmedLines.Count > settings.MaximumLinesPerOrder)
                    {
                        error = order.purchaseOrderId + ": demasiadas líneas confirmadas.";
                        return false;
                    }
                    HashSet<string> confirmedLineIds = new HashSet<string>(StringComparer.Ordinal);
                    HashSet<string> confirmedOfferIds = new HashSet<string>(StringComparer.Ordinal);
                    long computedSubtotal = 0L;
                    for (int lineIndex = 0; lineIndex < order.confirmedLines.Count; lineIndex++)
                    {
                        BistroBuilderPurchaseOrderConfirmedLineSnapshot line = order.confirmedLines[lineIndex];
                        if (line == null || string.IsNullOrWhiteSpace(line.purchaseOrderLineId) ||
                            !confirmedLineIds.Add(line.purchaseOrderLineId) ||
                            string.IsNullOrWhiteSpace(line.supplierOfferId) || !confirmedOfferIds.Add(line.supplierOfferId) ||
                            !string.Equals(line.supplierId, order.supplierId, StringComparison.Ordinal) ||
                            string.IsNullOrWhiteSpace(line.ingredientId) || string.IsNullOrWhiteSpace(line.packageFormatId) ||
                            line.effectiveUnitPriceCents <= 0L || line.marketPriceCents <= 0L || line.basePriceCents <= 0L ||
                            line.lineSubtotalCents <= 0L || line.packageCount <= 0 ||
                            line.minimumPackageCount < 1 || line.orderIncrement < 1 ||
                            line.packageCount < line.minimumPackageCount ||
                            (line.packageCount - line.minimumPackageCount) % line.orderIncrement != 0 ||
                            line.packageNetQuantityMicrounits <= 0L || line.totalNetQuantityMicrounits <= 0L ||
                            line.quotedLeadTimeGameHours <= 0f ||
                            !Enum.IsDefined(typeof(BistroBuilderSupplierOfferAvailability), line.availabilityAtConfirmation) ||
                            !Enum.IsDefined(typeof(BistroBuilderCommercialPackageLogisticSize), line.logisticSize) ||
                            line.availabilityAtConfirmation == BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado)
                        {
                            error = order.purchaseOrderId + ": snapshot comercial confirmado inválido.";
                            return false;
                        }
                        if (line.hadActivePromotion)
                        {
                            if (string.IsNullOrWhiteSpace(line.promotionId) ||
                                line.discountBasisPoints <= 0 ||
                                line.promotionStartGameDay < 1 ||
                                line.promotionEndGameDayExclusive <= line.promotionStartGameDay ||
                                line.effectiveUnitPriceCents > line.marketPriceCents)
                            {
                                error = order.purchaseOrderId + ": snapshot promocional congelado inválido.";
                                return false;
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(line.promotionId) ||
                                 line.effectiveUnitPriceCents != line.marketPriceCents)
                        {
                            error = order.purchaseOrderId + ": precio efectivo no promocional no coincide con mercado.";
                            return false;
                        }
                        try
                        {
                            long expectedLineSubtotal = checked(line.effectiveUnitPriceCents * (long)line.packageCount);
                            long expectedQuantity = checked(line.packageNetQuantityMicrounits * (long)line.packageCount);
                            computedSubtotal = checked(computedSubtotal + line.lineSubtotalCents);
                            if (line.lineSubtotalCents != expectedLineSubtotal || line.totalNetQuantityMicrounits != expectedQuantity)
                            {
                                error = order.purchaseOrderId + ": aritmética congelada de línea inconsistente.";
                                return false;
                            }
                        }
                        catch (OverflowException)
                        {
                            error = order.purchaseOrderId + ": overflow al validar aritmética confirmada.";
                            return false;
                        }
                    }
                    if (order.confirmedLines.Count > 0)
                    {
                        if (order.subtotalCents <= 0L || order.shippingCostCents < 0L || order.totalCents <= 0L)
                        {
                            error = order.purchaseOrderId + ": importes confirmados no válidos.";
                            return false;
                        }
                        long expectedTotal;
                        try
                        {
                            expectedTotal = checked(order.subtotalCents + order.shippingCostCents);
                        }
                        catch (OverflowException)
                        {
                            error = order.purchaseOrderId + ": overflow al validar total confirmado.";
                            return false;
                        }
                        if (computedSubtotal != order.subtotalCents || expectedTotal != order.totalCents)
                        {
                            error = order.purchaseOrderId + ": subtotal/porte/total no convergen con las líneas congeladas.";
                            return false;
                        }
                        if (order.supplierTerms != null)
                        {
                            BistroBuilderPurchaseOrderSupplierTermsSnapshot terms = order.supplierTerms;
                            if (terms.supplierId != order.supplierId ||
                                string.IsNullOrWhiteSpace(terms.supplierDisplayName) ||
                                terms.minimumOrderValueCents < 0L || terms.configuredShippingCostCents < 0L ||
                                terms.freeShippingThresholdCents < 0L || terms.appliedShippingCostCents < 0L ||
                                terms.appliedShippingCostCents != order.shippingCostCents ||
                                terms.defaultLeadTimeGameHours <= 0f ||
                                terms.reliabilityValue < 0f || terms.reliabilityValue > 1f)
                            {
                                error = order.purchaseOrderId + ": condiciones congeladas de proveedor no convergen con el pedido.";
                                return false;
                            }
                            if (terms.deliveryWindows != null)
                            {
                                for (int windowIndex = 0; windowIndex < terms.deliveryWindows.Count; windowIndex++)
                                {
                                    BistroBuilderPurchaseOrderDeliveryWindowSnapshot window = terms.deliveryWindows[windowIndex];
                                    if (window == null || window.startMinuteOfDay < 0 ||
                                        window.endMinuteOfDay <= window.startMinuteOfDay ||
                                        window.endMinuteOfDay > 24 * 60)
                                    {
                                        error = order.purchaseOrderId + ": ventana logística congelada inválida.";
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if ((isActivePostConfirmation ||
                 (order.status == BistroBuilderPurchaseOrderStatus.Cancelled && hasFrozenCommercialLines)) &&
                (order.supplierTerms == null || order.totalCents <= 0L || order.subtotalCents <= 0L))
            {
                error = order.purchaseOrderId + ": condiciones confirmadas incompletas.";
                return false;
            }
            bool hasLogisticsLifecycle =
                order.pendingDeliveryGameDay > 0 || !string.IsNullOrWhiteSpace(order.logisticsPlanId) ||
                order.plannedDeliveryGameDay > 0 || order.plannedDeliveryWindowStartMinuteOfDay >= 0 ||
                order.plannedDeliveryWindowEndMinuteOfDay >= 0 || order.plannedDelayGameMinutes > 0 ||
                order.status == BistroBuilderPurchaseOrderStatus.PendingDelivery ||
                order.status == BistroBuilderPurchaseOrderStatus.InDelivery ||
                order.status == BistroBuilderPurchaseOrderStatus.Delivered;
            if (hasLogisticsLifecycle)
            {
                if (string.IsNullOrWhiteSpace(order.logisticsPlanId) ||
                    order.plannedDeliveryGameDay < Math.Max(order.confirmedGameDay, order.pendingDeliveryGameDay) ||
                    order.plannedDeliveryWindowStartMinuteOfDay < 0 ||
                    order.plannedDeliveryWindowEndMinuteOfDay <= order.plannedDeliveryWindowStartMinuteOfDay ||
                    order.plannedDeliveryWindowEndMinuteOfDay > 24 * 60 ||
                    order.plannedDelayGameMinutes < 0)
                {
                    error = order.purchaseOrderId + ": plan logístico congelado inválido.";
                    return false;
                }
            }
            bool hasStartedDelivery =
                order.inDeliveryGameDay > 0 || order.actualDeliveryStartGameDay > 0 ||
                order.appliedDelayGameMinutes > 0 ||
                order.status == BistroBuilderPurchaseOrderStatus.InDelivery ||
                order.status == BistroBuilderPurchaseOrderStatus.Delivered;
            if (hasStartedDelivery &&
                (order.actualDeliveryStartGameDay < order.pendingDeliveryGameDay ||
                 order.actualDeliveryStartGameDay > order.inDeliveryGameDay ||
                 order.appliedDelayGameMinutes < 0))
            {
                error = order.purchaseOrderId + ": inicio/retraso real de entrega inválido.";
                return false;
            }
            if (order.status == BistroBuilderPurchaseOrderStatus.Delivered &&
                string.IsNullOrWhiteSpace(order.deliveryReceiptId))
            {
                error = order.purchaseOrderId + ": Delivered sin ReceiptId.";
                return false;
            }
            if (order.status != BistroBuilderPurchaseOrderStatus.Delivered &&
                !string.IsNullOrWhiteSpace(order.deliveryReceiptId))
            {
                error = order.purchaseOrderId + ": ReceiptId presente antes de Delivered.";
                return false;
            }
            if (order.status == BistroBuilderPurchaseOrderStatus.Cancelled)
            {
                if (string.IsNullOrWhiteSpace(order.cancellationReason))
                {
                    error = order.purchaseOrderId + ": Cancelled sin motivo trazable.";
                    return false;
                }
            }
            else if (order.cancelledGameDay != 0 || !string.IsNullOrWhiteSpace(order.cancellationReason))
            {
                error = order.purchaseOrderId + ": metadatos de cancelación presentes en un estado no Cancelled.";
                return false;
            }

            if (order.createdGameDay < 1 || order.lastModifiedGameDay < order.createdGameDay)
            {
                error = order.purchaseOrderId + ": cronología base inválida.";
                return false;
            }
            if (hasFrozenCommercialLines && order.confirmedGameDay < order.createdGameDay)
            {
                error = order.purchaseOrderId + ": Confirmed precede a Created.";
                return false;
            }
            if (hasLogisticsLifecycle &&
                (order.confirmedGameDay < order.createdGameDay ||
                 order.pendingDeliveryGameDay < order.confirmedGameDay))
            {
                error = order.purchaseOrderId + ": PendingDelivery precede a Confirmed o carece de hito previo.";
                return false;
            }
            if (hasStartedDelivery && order.inDeliveryGameDay < order.pendingDeliveryGameDay)
            {
                error = order.purchaseOrderId + ": InDelivery precede a PendingDelivery.";
                return false;
            }
            if (order.status == BistroBuilderPurchaseOrderStatus.Delivered &&
                order.deliveredGameDay < order.inDeliveryGameDay)
            {
                error = order.purchaseOrderId + ": Delivered precede a InDelivery.";
                return false;
            }
            if (order.status == BistroBuilderPurchaseOrderStatus.Cancelled &&
                (order.cancelledGameDay < order.createdGameDay ||
                 (hasFrozenCommercialLines && order.cancelledGameDay < order.confirmedGameDay) ||
                 (hasLogisticsLifecycle && order.cancelledGameDay < order.pendingDeliveryGameDay)))
            {
                error = order.purchaseOrderId + ": Cancelled precede a un hito previo del pedido.";
                return false;
            }
            int terminalOrCurrentStateDay = order.createdGameDay;
            switch (order.status)
            {
                case BistroBuilderPurchaseOrderStatus.Confirmed:
                    terminalOrCurrentStateDay = order.confirmedGameDay;
                    break;
                case BistroBuilderPurchaseOrderStatus.PendingDelivery:
                    terminalOrCurrentStateDay = order.pendingDeliveryGameDay;
                    break;
                case BistroBuilderPurchaseOrderStatus.InDelivery:
                    terminalOrCurrentStateDay = order.inDeliveryGameDay;
                    break;
                case BistroBuilderPurchaseOrderStatus.Delivered:
                    terminalOrCurrentStateDay = order.deliveredGameDay;
                    break;
                case BistroBuilderPurchaseOrderStatus.Cancelled:
                    terminalOrCurrentStateDay = order.cancelledGameDay;
                    break;
            }
            if (order.lastModifiedGameDay < terminalOrCurrentStateDay)
            {
                error = order.purchaseOrderId + ": LastModified precede al estado actual.";
                return false;
            }
        }
        if (snapshot.nextOrderSequence <= maxOrderSequence)
        {
            error = "NextOrderSequence podría reutilizar un PurchaseOrderId ya emitido.";
            return false;
        }
        return true;
    }

    public static bool TryFindActiveOffer(
        BistroBuilderSupplierAuthoringRecord supplier,
        string supplierOfferId,
        out BistroBuilderSupplierBaseOfferAuthoringRecord offer)
    {
        offer = null;
        if (supplier == null || supplier.baseOffers == null || string.IsNullOrWhiteSpace(supplierOfferId))
        {
            return false;
        }
        for (int index = 0; index < supplier.baseOffers.Count; index++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord candidate = supplier.baseOffers[index];
            if (candidate != null && candidate.isActive &&
                string.Equals(candidate.SupplierOfferId, supplierOfferId, StringComparison.Ordinal))
            {
                offer = candidate;
                return true;
            }
        }
        return false;
    }

    public static bool TryFindActivePackage(
        BistroBuilderIngredientAuthoringDatabase ingredients,
        string ingredientId,
        string packageFormatId,
        out BistroBuilderIngredientAuthoringRecord ingredient,
        out BistroBuilderCommercialPackageAuthoringRecord package)
    {
        ingredient = null;
        package = null;
        if (ingredients == null ||
            !ingredients.TryGetIngredient(ingredientId, out ingredient) || ingredient == null ||
            !ingredient.isActive || ingredient.commercialPackages == null)
        {
            return false;
        }
        for (int index = 0; index < ingredient.commercialPackages.Count; index++)
        {
            BistroBuilderCommercialPackageAuthoringRecord candidate = ingredient.commercialPackages[index];
            if (candidate != null && candidate.isActive &&
                string.Equals(candidate.PackageFormatId, packageFormatId, StringComparison.Ordinal))
            {
                package = candidate;
                return true;
            }
        }
        return false;
    }

    public static bool IsPackageCountValid(
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        int packageCount)
    {
        if (offer == null || packageCount <= 0)
        {
            return false;
        }
        int minimum = Math.Max(1, offer.minimumPackageCount);
        int increment = Math.Max(1, offer.orderIncrement);
        return packageCount >= minimum && (packageCount - minimum) % increment == 0;
    }

    public static string BuildFingerprint(BistroBuilderSupplierPurchaseOrdersSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return "NULL";
        }

        StringBuilder builder = new StringBuilder(4096);
        AppendFingerprintToken(builder, snapshot.schemaId);
        AppendFingerprintToken(builder, snapshot.schemaVersion);
        AppendFingerprintToken(builder, snapshot.currentGameDay);
        AppendFingerprintToken(builder, snapshot.sourceMarketSeed);
        AppendFingerprintToken(builder, snapshot.sourceCommercialSeed);
        AppendFingerprintToken(builder, snapshot.ordersRevision);
        AppendFingerprintToken(builder, snapshot.nextOrderSequence);

        int orderCount = snapshot.orders != null ? snapshot.orders.Count : -1;
        AppendFingerprintToken(builder, orderCount);
        if (snapshot.orders != null)
        {
            for (int index = 0; index < snapshot.orders.Count; index++)
            {
                BistroBuilderPurchaseOrderRecord order = snapshot.orders[index];
                AppendFingerprintToken(builder, order != null);
                if (order == null) continue;

                AppendFingerprintToken(builder, order.purchaseOrderId);
                AppendFingerprintToken(builder, order.displayCode);
                AppendFingerprintToken(builder, order.supplierId);
                AppendFingerprintToken(builder, (int)order.status);
                AppendFingerprintToken(builder, order.createdGameDay);
                AppendFingerprintToken(builder, order.lastModifiedGameDay);
                AppendFingerprintToken(builder, order.confirmedGameDay);
                AppendFingerprintToken(builder, order.pendingDeliveryGameDay);
                AppendFingerprintToken(builder, order.inDeliveryGameDay);
                AppendFingerprintToken(builder, order.deliveredGameDay);
                AppendFingerprintToken(builder, order.cancelledGameDay);
                AppendFingerprintToken(builder, order.stateRevision);
                AppendFingerprintToken(builder, order.nextLineSequence);
                AppendFingerprintToken(builder, order.currencyCode);
                AppendFingerprintToken(builder, order.subtotalCents);
                AppendFingerprintToken(builder, order.shippingCostCents);
                AppendFingerprintToken(builder, order.totalCents);
                AppendFingerprintToken(builder, order.quotedLeadTimeGameHours);
                AppendFingerprintToken(builder, order.sourceMarketRevision);
                AppendFingerprintToken(builder, order.sourceCommercialRevision);
                AppendFingerprintToken(builder, order.logisticsPlanId);
                AppendFingerprintToken(builder, order.plannedDeliveryGameDay);
                AppendFingerprintToken(builder, order.plannedDeliveryWindowStartMinuteOfDay);
                AppendFingerprintToken(builder, order.plannedDeliveryWindowEndMinuteOfDay);
                AppendFingerprintToken(builder, order.actualDeliveryStartGameDay);
                AppendFingerprintToken(builder, order.plannedDelayGameMinutes);
                AppendFingerprintToken(builder, order.appliedDelayGameMinutes);
                AppendFingerprintToken(builder, order.deliveryReceiptId);
                AppendFingerprintToken(builder, order.cancellationReason);

                AppendFingerprintToken(builder, order.supplierTerms != null);
                if (order.supplierTerms != null)
                {
                    BistroBuilderPurchaseOrderSupplierTermsSnapshot terms = order.supplierTerms;
                    AppendFingerprintToken(builder, terms.supplierId);
                    AppendFingerprintToken(builder, terms.supplierDisplayName);
                    AppendFingerprintToken(builder, terms.minimumOrderValueCents);
                    AppendFingerprintToken(builder, terms.configuredShippingCostCents);
                    AppendFingerprintToken(builder, terms.freeShippingEnabled);
                    AppendFingerprintToken(builder, terms.freeShippingThresholdCents);
                    AppendFingerprintToken(builder, terms.appliedShippingCostCents);
                    AppendFingerprintToken(builder, terms.defaultLeadTimeGameHours);
                    AppendFingerprintToken(builder, (int)terms.reliabilityTier);
                    AppendFingerprintToken(builder, terms.reliabilityValue);
                    AppendFingerprintToken(builder, (int)terms.preferredVehicle);
                    AppendFingerprintToken(builder, terms.vehiclePresentationProfileId);
                    AppendFingerprintToken(builder, terms.driverPresentationProfileId);
                    int windowCount = terms.deliveryWindows != null ? terms.deliveryWindows.Count : -1;
                    AppendFingerprintToken(builder, windowCount);
                    if (terms.deliveryWindows != null)
                    {
                        for (int windowIndex = 0; windowIndex < terms.deliveryWindows.Count; windowIndex++)
                        {
                            BistroBuilderPurchaseOrderDeliveryWindowSnapshot window = terms.deliveryWindows[windowIndex];
                            AppendFingerprintToken(builder, window != null);
                            if (window == null) continue;
                            AppendFingerprintToken(builder, window.startMinuteOfDay);
                            AppendFingerprintToken(builder, window.endMinuteOfDay);
                            AppendFingerprintToken(builder, window.monday);
                            AppendFingerprintToken(builder, window.tuesday);
                            AppendFingerprintToken(builder, window.wednesday);
                            AppendFingerprintToken(builder, window.thursday);
                            AppendFingerprintToken(builder, window.friday);
                            AppendFingerprintToken(builder, window.saturday);
                            AppendFingerprintToken(builder, window.sunday);
                        }
                    }
                }

                int draftCount = order.draftLines != null ? order.draftLines.Count : -1;
                AppendFingerprintToken(builder, draftCount);
                if (order.draftLines != null)
                {
                    for (int lineIndex = 0; lineIndex < order.draftLines.Count; lineIndex++)
                    {
                        BistroBuilderPurchaseOrderDraftLine line = order.draftLines[lineIndex];
                        AppendFingerprintToken(builder, line != null);
                        if (line == null) continue;
                        AppendFingerprintToken(builder, line.purchaseOrderLineId);
                        AppendFingerprintToken(builder, line.supplierOfferId);
                        AppendFingerprintToken(builder, line.packageCount);
                        AppendFingerprintToken(builder, line.sortOrder);
                    }
                }

                int confirmedCount = order.confirmedLines != null ? order.confirmedLines.Count : -1;
                AppendFingerprintToken(builder, confirmedCount);
                if (order.confirmedLines != null)
                {
                    for (int lineIndex = 0; lineIndex < order.confirmedLines.Count; lineIndex++)
                    {
                        BistroBuilderPurchaseOrderConfirmedLineSnapshot line = order.confirmedLines[lineIndex];
                        AppendFingerprintToken(builder, line != null);
                        if (line == null) continue;
                        AppendFingerprintToken(builder, line.purchaseOrderLineId);
                        AppendFingerprintToken(builder, line.supplierOfferId);
                        AppendFingerprintToken(builder, line.supplierId);
                        AppendFingerprintToken(builder, line.ingredientId);
                        AppendFingerprintToken(builder, line.ingredientDisplayName);
                        AppendFingerprintToken(builder, line.canonicalUnit);
                        AppendFingerprintToken(builder, line.packageFormatId);
                        AppendFingerprintToken(builder, line.packageDisplayName);
                        AppendFingerprintToken(builder, line.packageType);
                        AppendFingerprintToken(builder, (int)line.logisticSize);
                        AppendFingerprintToken(builder, line.packageNetQuantityMicrounits);
                        AppendFingerprintToken(builder, line.packageCount);
                        AppendFingerprintToken(builder, line.totalNetQuantityMicrounits);
                        AppendFingerprintToken(builder, line.minimumPackageCount);
                        AppendFingerprintToken(builder, line.orderIncrement);
                        AppendFingerprintToken(builder, line.basePriceCents);
                        AppendFingerprintToken(builder, line.marketPriceCents);
                        AppendFingerprintToken(builder, line.effectiveUnitPriceCents);
                        AppendFingerprintToken(builder, line.lineSubtotalCents);
                        AppendFingerprintToken(builder, (int)line.availabilityAtConfirmation);
                        AppendFingerprintToken(builder, line.hadActivePromotion);
                        AppendFingerprintToken(builder, line.promotionId);
                        AppendFingerprintToken(builder, line.promotionStartGameDay);
                        AppendFingerprintToken(builder, line.promotionEndGameDayExclusive);
                        AppendFingerprintToken(builder, line.discountBasisPoints);
                        AppendFingerprintToken(builder, line.promotionReasonCode);
                        AppendFingerprintToken(builder, line.promotionReasonText);
                        AppendFingerprintToken(builder, line.quotedLeadTimeGameHours);
                        AppendFingerprintToken(builder, line.sourceMarketRevision);
                        AppendFingerprintToken(builder, line.sourceCommercialRevision);
                    }
                }
            }
        }
        return StableHash64(builder.ToString(), 2305001).ToString("X16");
    }

    private static void AppendFingerprintToken(StringBuilder builder, string value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length).Append('#').Append(safe).Append(';');
    }

    private static void AppendFingerprintToken(StringBuilder builder, bool value)
    {
        AppendFingerprintToken(builder, value ? "1" : "0");
    }

    private static void AppendFingerprintToken(StringBuilder builder, int value)
    {
        AppendFingerprintToken(builder, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendFingerprintToken(StringBuilder builder, long value)
    {
        AppendFingerprintToken(builder, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendFingerprintToken(StringBuilder builder, ulong value)
    {
        AppendFingerprintToken(builder, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendFingerprintToken(StringBuilder builder, float value)
    {
        AppendFingerprintToken(builder, value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static bool ValidateConfirmationPreviewForCommit(
        BistroBuilderPurchaseOrderRecord order,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderPurchaseOrderConfirmationPreview preview,
        out string error)
    {
        error = null;
        if (order == null || supplier == null || preview == null || preview.lines == null)
        {
            error = "Cotización de confirmación incompleta.";
            return false;
        }
        int draftCount = order.draftLines != null ? order.draftLines.Count : 0;
        if (!preview.canConfirm || (preview.blockers != null && preview.blockers.Count > 0) ||
            preview.lineCount != preview.lines.Count || preview.lines.Count != draftCount || draftCount == 0)
        {
            error = "La cotización no está en un estado confirmable coherente.";
            return false;
        }
        long expectedMinimum = Math.Max(0L, supplier.minimumOrderValueCents);
        if (preview.minimumOrderValueCents != expectedMinimum ||
            !preview.minimumOrderSatisfied || preview.subtotalCents < expectedMinimum ||
            preview.quotedLeadTimeGameHours <= 0f)
        {
            error = "La cotización no converge con el mínimo/lead time del proveedor.";
            return false;
        }

        long expectedShipping = Math.Max(0L, supplier.shippingCostCents);
        if (supplier.freeShippingEnabled &&
            preview.subtotalCents >= Math.Max(0L, supplier.freeShippingThresholdCents))
        {
            expectedShipping = 0L;
        }
        if (preview.shippingCostCents != expectedShipping)
        {
            error = "La cotización no converge con la regla de portes del proveedor.";
            return false;
        }

        long expectedTotal;
        long computedSubtotal = 0L;
        try
        {
            expectedTotal = checked(preview.subtotalCents + preview.shippingCostCents);
        }
        catch (OverflowException)
        {
            error = "Overflow al verificar el total de confirmación.";
            return false;
        }
        if (preview.subtotalCents <= 0L || preview.totalCents != expectedTotal)
        {
            error = "Subtotal/portes/total de la cotización no son coherentes.";
            return false;
        }

        HashSet<string> lineIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> offerIds = new HashSet<string>(StringComparer.Ordinal);
        for (int lineIndex = 0; lineIndex < preview.lines.Count; lineIndex++)
        {
            BistroBuilderPurchaseOrderConfirmedLineSnapshot line = preview.lines[lineIndex];
            if (line == null || string.IsNullOrWhiteSpace(line.purchaseOrderLineId) ||
                !lineIds.Add(line.purchaseOrderLineId) || string.IsNullOrWhiteSpace(line.supplierOfferId) ||
                !offerIds.Add(line.supplierOfferId) ||
                !string.Equals(line.supplierId, supplier.SupplierId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(line.ingredientId) || string.IsNullOrWhiteSpace(line.packageFormatId) ||
                line.packageCount < Math.Max(1, line.minimumPackageCount) || line.orderIncrement < 1 ||
                (line.packageCount - Math.Max(1, line.minimumPackageCount)) % Math.Max(1, line.orderIncrement) != 0 ||
                line.basePriceCents <= 0L || line.marketPriceCents <= 0L || line.effectiveUnitPriceCents <= 0L ||
                line.packageNetQuantityMicrounits <= 0L || line.quotedLeadTimeGameHours <= 0f ||
                line.availabilityAtConfirmation == BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado)
            {
                error = "La cotización contiene una línea comercial inválida.";
                return false;
            }

            if (line.hadActivePromotion)
            {
                if (string.IsNullOrWhiteSpace(line.promotionId) || line.discountBasisPoints <= 0 ||
                    line.promotionStartGameDay < 1 || line.promotionEndGameDayExclusive <= line.promotionStartGameDay ||
                    line.effectiveUnitPriceCents > line.marketPriceCents)
                {
                    error = line.purchaseOrderLineId + ": metadatos promocionales inválidos antes de confirmar.";
                    return false;
                }
            }
            else if (!string.IsNullOrWhiteSpace(line.promotionId) ||
                     line.effectiveUnitPriceCents != line.marketPriceCents)
            {
                error = line.purchaseOrderLineId + ": precio efectivo no promocional inconsistente.";
                return false;
            }

            bool matchedDraft = false;
            for (int draftIndex = 0; draftIndex < draftCount; draftIndex++)
            {
                BistroBuilderPurchaseOrderDraftLine draft = order.draftLines[draftIndex];
                if (draft != null &&
                    string.Equals(draft.purchaseOrderLineId, line.purchaseOrderLineId, StringComparison.Ordinal) &&
                    string.Equals(draft.supplierOfferId, line.supplierOfferId, StringComparison.Ordinal) &&
                    draft.packageCount == line.packageCount)
                {
                    matchedDraft = true;
                    break;
                }
            }
            if (!matchedDraft)
            {
                error = line.purchaseOrderLineId + ": la línea congelada ya no coincide con el Draft.";
                return false;
            }

            try
            {
                long lineSubtotal = checked(line.effectiveUnitPriceCents * (long)line.packageCount);
                long totalQuantity = checked(line.packageNetQuantityMicrounits * (long)line.packageCount);
                computedSubtotal = checked(computedSubtotal + lineSubtotal);
                if (line.lineSubtotalCents != lineSubtotal || line.totalNetQuantityMicrounits != totalQuantity)
                {
                    error = line.purchaseOrderLineId + ": aritmética de línea alterada antes de confirmar.";
                    return false;
                }
            }
            catch (OverflowException)
            {
                error = "Overflow al verificar la aritmética de la cotización.";
                return false;
            }
        }
        if (computedSubtotal != preview.subtotalCents)
        {
            error = "El subtotal de la cotización no coincide con sus líneas.";
            return false;
        }
        return true;
    }

    private static bool TryParseCanonicalOrderSequence(string purchaseOrderId, out long sequence)
    {
        sequence = 0L;
        const string prefix = "purchase_order_";
        if (string.IsNullOrWhiteSpace(purchaseOrderId) ||
            !purchaseOrderId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        string suffix = purchaseOrderId.Substring(prefix.Length);
        return suffix.Length == 8 && long.TryParse(suffix, out sequence) && sequence > 0L;
    }

    private static bool TryParseCanonicalLineSequence(
        string purchaseOrderId,
        string purchaseOrderLineId,
        out long sequence)
    {
        sequence = 0L;
        string prefix = (purchaseOrderId ?? string.Empty) + "_line_";
        if (string.IsNullOrWhiteSpace(purchaseOrderLineId) ||
            !purchaseOrderLineId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        string suffix = purchaseOrderLineId.Substring(prefix.Length);
        return suffix.Length == 4 && long.TryParse(suffix, out sequence) && sequence > 0L;
    }

    public static BistroBuilderPurchaseOrderConfirmationReceipt BuildReceipt(BistroBuilderPurchaseOrderRecord order)
    {
        if (order == null)
        {
            return null;
        }
        return new BistroBuilderPurchaseOrderConfirmationReceipt
        {
            purchaseOrderId = order.purchaseOrderId,
            displayCode = order.displayCode,
            supplierId = order.supplierId,
            supplierDisplayName = order.supplierTerms != null ? order.supplierTerms.supplierDisplayName : order.supplierId,
            lineCount = order.confirmedLines != null ? order.confirmedLines.Count : 0,
            subtotalCents = order.subtotalCents,
            shippingCostCents = order.shippingCostCents,
            totalCents = order.totalCents,
            currencyCode = order.currencyCode,
            confirmedGameDay = order.confirmedGameDay,
            quotedLeadTimeGameHours = order.quotedLeadTimeGameHours,
            status = order.status
        };
    }

    private static BistroBuilderPurchaseOrderSupplierTermsSnapshot BuildSupplierTermsSnapshot(
        BistroBuilderSupplierAuthoringRecord supplier,
        long appliedShippingCostCents)
    {
        BistroBuilderPurchaseOrderSupplierTermsSnapshot terms =
            new BistroBuilderPurchaseOrderSupplierTermsSnapshot
            {
                supplierId = supplier.SupplierId,
                supplierDisplayName = supplier.displayName,
                minimumOrderValueCents = Math.Max(0L, supplier.minimumOrderValueCents),
                configuredShippingCostCents = Math.Max(0L, supplier.shippingCostCents),
                freeShippingEnabled = supplier.freeShippingEnabled,
                freeShippingThresholdCents = Math.Max(0L, supplier.freeShippingThresholdCents),
                appliedShippingCostCents = Math.Max(0L, appliedShippingCostCents),
                defaultLeadTimeGameHours = Math.Max(0.1f, supplier.defaultLeadTimeGameHours),
                reliabilityTier = supplier.reliabilityTier,
                reliabilityValue = supplier.reliabilityValue,
                preferredVehicle = supplier.logisticsProfile != null
                    ? supplier.logisticsProfile.preferredVehicle
                    : BistroBuilderSupplierVehiclePreference.Automatico,
                vehiclePresentationProfileId = supplier.logisticsProfile != null
                    ? supplier.logisticsProfile.vehiclePresentationProfileId
                    : null,
                driverPresentationProfileId = supplier.logisticsProfile != null
                    ? supplier.logisticsProfile.driverPresentationProfileId
                    : null
            };

        if (supplier.deliveryWindows != null)
        {
            for (int index = 0; index < supplier.deliveryWindows.Count; index++)
            {
                BistroBuilderSupplierDeliveryWindowAuthoring source = supplier.deliveryWindows[index];
                if (source == null) continue;
                terms.deliveryWindows.Add(new BistroBuilderPurchaseOrderDeliveryWindowSnapshot
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
        return terms;
    }

    private static ulong StableHash64(string value, int salt)
    {
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset ^ unchecked((uint)salt);
        string text = value ?? string.Empty;
        for (int index = 0; index < text.Length; index++)
        {
            ushort c = text[index];
            hash ^= (byte)(c & 0xFF);
            hash *= prime;
            hash ^= (byte)(c >> 8);
            hash *= prime;
        }
        return hash == 0UL ? 1UL : hash;
    }

    private static void Touch(
        BistroBuilderSupplierPurchaseOrdersSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        int gameDay)
    {
        int safeDay = Math.Max(1, gameDay);
        order.lastModifiedGameDay = safeDay;
        order.stateRevision++;
        snapshot.currentGameDay = Math.Max(snapshot.currentGameDay, safeDay);
        snapshot.ordersRevision++;
    }
}
