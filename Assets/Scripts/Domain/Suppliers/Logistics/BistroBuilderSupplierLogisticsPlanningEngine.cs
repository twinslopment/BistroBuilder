using System;
using System.Collections.Generic;
using UnityEngine;

public static class BistroBuilderSupplierLogisticsPlanningEngine
{
    public static ulong BuildLogisticsSeed(string seedText, ulong marketSeed, ulong commercialSeed)
    {
        ulong hash = 1469598103934665603UL;
        Mix(ref hash, seedText ?? string.Empty);
        Mix(ref hash, marketSeed.ToString());
        Mix(ref hash, commercialSeed.ToString());
        return hash == 0UL ? 1UL : hash;
    }

    public static BistroBuilderSupplierLogisticsSnapshot CreateInitialSnapshot(
        int gameDay,
        ulong marketSeed,
        ulong commercialSeed,
        BistroBuilderSupplierLogisticsPlanningSettings settings)
    {
        return new BistroBuilderSupplierLogisticsSnapshot
        {
            currentGameDay = Math.Max(1, gameDay),
            sourceMarketSeed = marketSeed,
            sourceCommercialSeed = commercialSeed,
            logisticsSeed = BuildLogisticsSeed(settings != null ? settings.DeterministicSeedText : "bistro-logistics-v1", marketSeed, commercialSeed),
            logisticsRevision = 1,
            nextPlanSequence = 1
        };
    }

    public static bool TryBuildPlan(
        BistroBuilderSupplierLogisticsSnapshot snapshot,
        BistroBuilderPurchaseOrderRecord order,
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierLogisticsPlanningSettings settings,
        int currentGameDay,
        out BistroBuilderSupplierLogisticsPlanRecord plan,
        out string error)
    {
        plan = null;
        error = null;
        if (snapshot == null || order == null || supplier == null || settings == null)
        {
            error = "Datos insuficientes para construir el plan logístico.";
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Confirmed)
        {
            error = "Solo un PurchaseOrder Confirmed puede recibir un nuevo plan logístico.";
            return false;
        }
        if (order.supplierTerms == null || order.confirmedLines == null || order.confirmedLines.Count == 0)
        {
            error = "El pedido confirmado carece del snapshot comercial/logístico necesario.";
            return false;
        }
        if (!string.Equals(order.supplierId, supplier.SupplierId, StringComparison.Ordinal))
        {
            error = "El proveedor de autoría no coincide con el pedido confirmado.";
            return false;
        }

        int confirmedDay = Math.Max(1, order.confirmedGameDay > 0 ? order.confirmedGameDay : currentGameDay);
        long leadMinutes = (long)Math.Ceiling(Math.Max(0.1f, order.quotedLeadTimeGameHours) * 60d);
        long etaAbsoluteMinute = (long)(confirmedDay - 1) * 1440L + leadMinutes;
        int deliveryDay;
        int windowStart;
        int windowEnd;
        if (!TryFindFirstDeliveryWindow(order.supplierTerms.deliveryWindows, etaAbsoluteMinute, settings, out deliveryDay, out windowStart, out windowEnd))
        {
            error = "No se pudo resolver una ventana de entrega válida.";
            return false;
        }

        float reliability = Mathf.Clamp01(order.supplierTerms.reliabilityValue);
        float tierFactor = TierDelayFactor(order.supplierTerms.reliabilityTier);
        int chance = Mathf.RoundToInt((1f - reliability) * 10000f * tierFactor * settings.DelayChanceMultiplier);
        chance = Mathf.Clamp(chance, settings.MinimumDelayChanceBasisPoints, settings.MaximumDelayChanceBasisPoints);

        ulong planHash = snapshot.logisticsSeed;
        Mix(ref planHash, order.purchaseOrderId ?? string.Empty);
        Mix(ref planHash, order.supplierId ?? string.Empty);
        Mix(ref planHash, order.confirmedGameDay.ToString());
        int roll = (int)(planHash % 10000UL);

        int minDelay = settings.FallbackMinimumDelayMinutes;
        int maxDelay = settings.FallbackMaximumDelayMinutes;
        if (supplier.logisticsProfile != null)
        {
            minDelay = Math.Max(0, supplier.logisticsProfile.minimumDelayMinutes);
            maxDelay = Math.Max(minDelay, supplier.logisticsProfile.maximumDelayMinutes);
        }
        int decidedDelay = 0;
        if (roll < chance && maxDelay > 0)
        {
            ulong delayHash = planHash;
            Mix(ref delayHash, "delay-minutes");
            int span = Math.Max(0, maxDelay - minDelay);
            decidedDelay = minDelay + (span > 0 ? (int)(delayHash % (ulong)(span + 1)) : 0);
        }

        int loadUnits = CalculateLoadUnits(order.confirmedLines, settings);
        int visualUnits = Math.Max(1, DivideCeil(loadUnits, settings.VisualLoadUnitCapacity));
        int trips = Mathf.Clamp(DivideCeil(loadUnits, settings.TripCapacityLoadUnits), 1, settings.MaximumSuggestedTrips);
        BistroBuilderSupplierVehiclePreference vehicle = ResolveVehicle(order.supplierTerms.preferredVehicle, loadUnits, settings);

        string planId = "logistics_plan_" + Math.Max(1L, snapshot.nextPlanSequence).ToString("D8");
        plan = new BistroBuilderSupplierLogisticsPlanRecord
        {
            logisticsPlanId = planId,
            purchaseOrderId = order.purchaseOrderId,
            orderDisplayCode = order.displayCode,
            supplierId = order.supplierId,
            supplierDisplayName = order.supplierTerms.supplierDisplayName,
            createdGameDay = Math.Max(1, currentGameDay),
            sourceOrderStateRevision = order.stateRevision,
            basePlannedDeliveryGameDay = deliveryDay,
            baseWindowStartMinuteOfDay = windowStart,
            baseWindowEndMinuteOfDay = windowEnd,
            plannedDeliveryGameDay = deliveryDay,
            windowStartMinuteOfDay = windowStart,
            windowEndMinuteOfDay = windowEnd,
            reliabilityTier = order.supplierTerms.reliabilityTier,
            reliabilityValue = reliability,
            delayProbabilityBasisPoints = chance,
            deterministicDelayRollBasisPoints = roll,
            decidedDelayGameMinutes = decidedDelay,
            logisticsLoadUnits = loadUnits,
            visualLoadUnits = visualUnits,
            suggestedTripCount = trips,
            resolvedVehicle = vehicle,
            vehiclePresentationProfileId = order.supplierTerms.vehiclePresentationProfileId,
            driverPresentationProfileId = order.supplierTerms.driverPresentationProfileId,
            reasonCode = decidedDelay > 0 ? "delay_risk_precomputed" : "on_time_plan",
            reasonText = decidedDelay > 0
                ? "La fiabilidad del proveedor genera un riesgo de retraso determinista; el pedido nunca desaparece y el retraso solo se aplicará cuando llegue el día previsto."
                : "Planificación compatible con lead time, ventanas de entrega y fiabilidad del proveedor."
        };
        return true;
    }

    public static bool TryApplyDelay(
        BistroBuilderSupplierLogisticsPlanRecord plan,
        int currentGameDay,
        out bool changed,
        out string error)
    {
        changed = false;
        error = null;
        if (plan == null)
        {
            error = "Plan logístico nulo.";
            return false;
        }
        if (plan.decidedDelayGameMinutes <= 0 || plan.delayApplied) return true;
        if (currentGameDay < plan.basePlannedDeliveryGameDay) return true;

        long startAbs = (long)(plan.basePlannedDeliveryGameDay - 1) * 1440L + plan.baseWindowStartMinuteOfDay + plan.decidedDelayGameMinutes;
        long endAbs = (long)(plan.basePlannedDeliveryGameDay - 1) * 1440L + plan.baseWindowEndMinuteOfDay + plan.decidedDelayGameMinutes;
        int startDay = (int)(startAbs / 1440L) + 1;
        int endDay = (int)((Math.Max(startAbs + 1L, endAbs) - 1L) / 1440L) + 1;
        int startMinute = (int)(startAbs % 1440L);
        int endMinute = (int)(endAbs % 1440L);
        if (endMinute == 0 && endDay == startDay) endMinute = 1440;
        if (endDay != startDay || endMinute <= startMinute)
        {
            startDay = endDay;
            endMinute = endMinute <= 0 ? 60 : endMinute;
            startMinute = Math.Max(0, endMinute - Math.Min(120, endMinute));
        }

        plan.plannedDeliveryGameDay = startDay;
        plan.windowStartMinuteOfDay = startMinute;
        plan.windowEndMinuteOfDay = Math.Min(1440, Math.Max(startMinute + 1, endMinute));
        plan.delayApplied = true;
        plan.delayAppliedGameDay = Math.Max(1, currentGameDay);
        plan.status = BistroBuilderSupplierLogisticsPlanStatus.DelayApplied;
        plan.reasonCode = "reliability_delay_applied";
        plan.reasonText = "Se ha aplicado un retraso explicable por la fiabilidad del proveedor. El PurchaseOrder sigue activo y conserva su LogisticsPlanId.";
        plan.stateRevision++;
        changed = true;
        return true;
    }

    public static bool ValidateSnapshot(BistroBuilderSupplierLogisticsSnapshot snapshot, out string error)
    {
        error = null;
        if (snapshot == null)
        {
            error = "Snapshot logístico nulo.";
            return false;
        }
        if (snapshot.schemaId != BistroBuilderSupplierLogisticsSnapshot.CurrentSchemaId ||
            snapshot.schemaVersion != BistroBuilderSupplierLogisticsSnapshot.CurrentSchemaVersion)
        {
            error = "Schema de supplier.logistics.runtime incompatible.";
            return false;
        }
        if (snapshot.currentGameDay < 1 || snapshot.logisticsSeed == 0UL || snapshot.nextPlanSequence < 1)
        {
            error = "Cabecera de snapshot logístico inválida.";
            return false;
        }
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> orderIds = new HashSet<string>(StringComparer.Ordinal);
        if (snapshot.plans == null)
        {
            error = "Colección de planes nula.";
            return false;
        }
        for (int index = 0; index < snapshot.plans.Count; index++)
        {
            BistroBuilderSupplierLogisticsPlanRecord plan = snapshot.plans[index];
            if (plan == null || string.IsNullOrWhiteSpace(plan.logisticsPlanId) || string.IsNullOrWhiteSpace(plan.purchaseOrderId))
            {
                error = "Plan logístico nulo o sin identidad estable.";
                return false;
            }
            if (!ids.Add(plan.logisticsPlanId) || !orderIds.Add(plan.purchaseOrderId))
            {
                error = "LogisticsPlanId o PurchaseOrderId duplicado en supplier.logistics.runtime.";
                return false;
            }
            if (plan.plannedDeliveryGameDay < 1 || plan.windowStartMinuteOfDay < 0 ||
                plan.windowEndMinuteOfDay <= plan.windowStartMinuteOfDay || plan.windowEndMinuteOfDay > 1440)
            {
                error = plan.logisticsPlanId + ": fecha/ventana de entrega inválida.";
                return false;
            }
            if (plan.reliabilityValue < 0f || plan.reliabilityValue > 1f ||
                plan.delayProbabilityBasisPoints < 0 || plan.delayProbabilityBasisPoints > 10000 ||
                plan.deterministicDelayRollBasisPoints < 0 || plan.deterministicDelayRollBasisPoints >= 10000)
            {
                error = plan.logisticsPlanId + ": métricas de fiabilidad/retraso inválidas.";
                return false;
            }
            if (plan.logisticsLoadUnits < 1 || plan.visualLoadUnits < 1 || plan.suggestedTripCount < 1 || plan.suggestedTripCount > 3)
            {
                error = plan.logisticsPlanId + ": carga logística visual inválida.";
                return false;
            }
        }
        return true;
    }

    private static bool TryFindFirstDeliveryWindow(
        List<BistroBuilderPurchaseOrderDeliveryWindowSnapshot> windows,
        long etaAbsoluteMinute,
        BistroBuilderSupplierLogisticsPlanningSettings settings,
        out int gameDay,
        out int startMinute,
        out int endMinute)
    {
        gameDay = 0;
        startMinute = 0;
        endMinute = 0;
        int etaDay = (int)(etaAbsoluteMinute / 1440L) + 1;
        int etaMinute = (int)(etaAbsoluteMinute % 1440L);
        bool hasWindows = windows != null && windows.Count > 0;
        for (int offset = 0; offset <= settings.MaximumWindowSearchDays; offset++)
        {
            int day = etaDay + offset;
            if (!hasWindows)
            {
                int start = settings.FallbackWindowStartMinuteOfDay;
                int end = settings.FallbackWindowEndMinuteOfDay;
                if (offset == 0 && etaMinute >= end) continue;
                gameDay = day;
                startMinute = offset == 0 ? Math.Max(start, etaMinute) : start;
                endMinute = end;
                if (startMinute < endMinute) return true;
                continue;
            }

            for (int index = 0; index < windows.Count; index++)
            {
                BistroBuilderPurchaseOrderDeliveryWindowSnapshot window = windows[index];
                if (window == null || !IsWindowEnabledForDay(window, day, settings.FirstGameDayWeekday)) continue;
                int start = Mathf.Clamp(window.startMinuteOfDay, 0, 1439);
                int end = Mathf.Clamp(window.endMinuteOfDay, start + 1, 1440);
                if (offset == 0 && etaMinute >= end) continue;
                int candidateStart = offset == 0 ? Math.Max(start, etaMinute) : start;
                if (candidateStart >= end) continue;
                gameDay = day;
                startMinute = candidateStart;
                endMinute = end;
                return true;
            }
        }
        return false;
    }

    private static bool IsWindowEnabledForDay(BistroBuilderPurchaseOrderDeliveryWindowSnapshot window, int gameDay, int firstWeekday)
    {
        int weekday = ((Math.Max(1, gameDay) - 1 + firstWeekday) % 7 + 7) % 7;
        switch (weekday)
        {
            case 0: return window.monday;
            case 1: return window.tuesday;
            case 2: return window.wednesday;
            case 3: return window.thursday;
            case 4: return window.friday;
            case 5: return window.saturday;
            default: return window.sunday;
        }
    }

    private static int CalculateLoadUnits(List<BistroBuilderPurchaseOrderConfirmedLineSnapshot> lines, BistroBuilderSupplierLogisticsPlanningSettings settings)
    {
        long total = 0L;
        for (int index = 0; index < lines.Count; index++)
        {
            BistroBuilderPurchaseOrderConfirmedLineSnapshot line = lines[index];
            if (line == null) continue;
            int perPackage;
            switch (line.logisticSize)
            {
                case BistroBuilderCommercialPackageLogisticSize.Pequeno: perPackage = settings.SmallPackageLoadUnits; break;
                case BistroBuilderCommercialPackageLogisticSize.Grande: perPackage = settings.LargePackageLoadUnits; break;
                default: perPackage = settings.MediumPackageLoadUnits; break;
            }
            total += (long)Math.Max(1, line.packageCount) * Math.Max(1, perPackage);
            if (total >= int.MaxValue) return int.MaxValue;
        }
        return Math.Max(1, (int)total);
    }

    private static BistroBuilderSupplierVehiclePreference ResolveVehicle(BistroBuilderSupplierVehiclePreference preferred, int loadUnits, BistroBuilderSupplierLogisticsPlanningSettings settings)
    {
        if (preferred == BistroBuilderSupplierVehiclePreference.Furgoneta || preferred == BistroBuilderSupplierVehiclePreference.CamionLigero)
            return preferred;
        return loadUnits >= settings.LightTruckThresholdLoadUnits
            ? BistroBuilderSupplierVehiclePreference.CamionLigero
            : BistroBuilderSupplierVehiclePreference.Furgoneta;
    }

    private static float TierDelayFactor(BistroBuilderSupplierReliabilityTier tier)
    {
        switch (tier)
        {
            case BistroBuilderSupplierReliabilityTier.Excelente: return 0.75f;
            case BistroBuilderSupplierReliabilityTier.Alta: return 1f;
            case BistroBuilderSupplierReliabilityTier.Normal: return 1.20f;
            default: return 1.50f;
        }
    }

    private static int DivideCeil(int value, int divisor)
    {
        value = Math.Max(1, value);
        divisor = Math.Max(1, divisor);
        return (value + divisor - 1) / divisor;
    }

    private static void Mix(ref ulong hash, string value)
    {
        string safe = value ?? string.Empty;
        for (int index = 0; index < safe.Length; index++)
        {
            hash ^= safe[index];
            hash *= 1099511628211UL;
        }
        hash ^= 0xFFUL;
        hash *= 1099511628211UL;
    }
}
