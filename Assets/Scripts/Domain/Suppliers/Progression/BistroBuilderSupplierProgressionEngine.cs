using System;
using System.Collections.Generic;

/// <summary>
/// Evaluador puro y determinista de desbloqueos 2.3I.
/// Las condiciones de un mismo proveedor se combinan mediante AND.
/// </summary>
public static class BistroBuilderSupplierProgressionEngine
{
    public static BistroBuilderSupplierAccessEvaluation Evaluate(
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierProgressionFacts facts)
    {
        BistroBuilderSupplierAccessEvaluation result = new BistroBuilderSupplierAccessEvaluation
        {
            supplierId = supplier != null ? supplier.SupplierId : string.Empty,
            supplierDisplayName = supplier != null ? supplier.displayName : string.Empty,
            status = BistroBuilderSupplierAccessStatus.Locked,
            isUnlocked = false,
            availableFromStart = supplier != null && supplier.unlockProfile != null && supplier.unlockProfile.availableFromStart
        };

        if (supplier == null || !supplier.isActive)
        {
            result.summary = "Proveedor inexistente o inactivo.";
            return result;
        }

        if (result.availableFromStart)
        {
            result.status = BistroBuilderSupplierAccessStatus.AvailableFromStart;
            result.isUnlocked = true;
            result.conditionsSatisfied = true;
            result.progress01 = 1f;
            result.summary = "Disponible desde el inicio.";
            return result;
        }

        if (supplier.unlockProfile == null || supplier.unlockProfile.conditions == null || supplier.unlockProfile.conditions.Count == 0)
        {
            result.summary = "Bloqueado: no tiene condiciones de desbloqueo configuradas.";
            return result;
        }

        BistroBuilderSupplierProgressionFacts safeFacts = facts ?? new BistroBuilderSupplierProgressionFacts();
        bool allSatisfied = true;
        float progressSum = 0f;
        int evaluatedCount = 0;

        for (int index = 0; index < supplier.unlockProfile.conditions.Count; index++)
        {
            BistroBuilderSupplierUnlockConditionAuthoring condition = supplier.unlockProfile.conditions[index];
            if (condition == null)
            {
                allSatisfied = false;
                continue;
            }

            BistroBuilderSupplierUnlockConditionResult conditionResult = EvaluateCondition(condition, safeFacts);
            result.conditions.Add(conditionResult);
            evaluatedCount++;
            progressSum += Clamp01(conditionResult.progress01);
            if (!conditionResult.satisfied)
            {
                allSatisfied = false;
            }
        }

        result.conditionsSatisfied = evaluatedCount > 0 && allSatisfied;
        result.progress01 = evaluatedCount > 0 ? Clamp01(progressSum / evaluatedCount) : 0f;
        result.isUnlocked = result.conditionsSatisfied;
        result.status = result.isUnlocked
            ? BistroBuilderSupplierAccessStatus.Unlocked
            : BistroBuilderSupplierAccessStatus.Locked;
        result.summary = result.isUnlocked
            ? "Requisitos cumplidos: proveedor desbloqueable."
            : BuildLockedSummary(result.conditions);
        return result;
    }

    public static BistroBuilderSupplierUnlockConditionResult EvaluateCondition(
        BistroBuilderSupplierUnlockConditionAuthoring condition,
        BistroBuilderSupplierProgressionFacts facts)
    {
        BistroBuilderSupplierUnlockConditionResult result = new BistroBuilderSupplierUnlockConditionResult
        {
            kind = condition != null ? condition.kind : BistroBuilderSupplierUnlockRuleKind.Ninguna,
            requiredNumericValue = condition != null ? Math.Max(0L, condition.numericThreshold) : 0L,
            requiredText = condition != null ? NormalizeToken(condition.stringThreshold) : string.Empty,
            sourceAvailable = true
        };

        if (condition == null)
        {
            result.sourceAvailable = false;
            result.reasonCode = "null_condition";
            result.reasonText = "Condición nula.";
            return result;
        }

        BistroBuilderSupplierProgressionFacts safeFacts = facts ?? new BistroBuilderSupplierProgressionFacts();
        switch (condition.kind)
        {
            case BistroBuilderSupplierUnlockRuleKind.DiasAbierto:
                result.currentNumericValue = Math.Max(0, safeFacts.daysOpen);
                result.satisfied = result.currentNumericValue >= result.requiredNumericValue;
                result.progress01 = Ratio(result.currentNumericValue, result.requiredNumericValue);
                result.reasonCode = "days_open";
                result.reasonText = "Días abierto: " + result.currentNumericValue + " / " + result.requiredNumericValue + ".";
                break;

            case BistroBuilderSupplierUnlockRuleKind.VolumenComprasCentimos:
                result.currentNumericValue = Math.Max(0L, safeFacts.qualifiedPurchaseVolumeCents);
                result.satisfied = result.currentNumericValue >= result.requiredNumericValue;
                result.progress01 = Ratio(result.currentNumericValue, result.requiredNumericValue);
                result.reasonCode = "purchase_volume";
                result.reasonText = "Volumen de compras: " + FormatEuros(result.currentNumericValue) + " / " + FormatEuros(result.requiredNumericValue) + ".";
                break;

            case BistroBuilderSupplierUnlockRuleKind.FacturacionCentimos:
                result.sourceAvailable = safeFacts.hasLifetimeRevenue;
                result.currentNumericValue = Math.Max(0L, safeFacts.lifetimeRevenueCents);
                result.satisfied = result.sourceAvailable && result.currentNumericValue >= result.requiredNumericValue;
                result.progress01 = result.sourceAvailable ? Ratio(result.currentNumericValue, result.requiredNumericValue) : 0f;
                result.reasonCode = result.sourceAvailable ? "revenue" : "revenue_source_unavailable";
                result.reasonText = result.sourceAvailable
                    ? "Facturación: " + FormatEuros(result.currentNumericValue) + " / " + FormatEuros(result.requiredNumericValue) + "."
                    : "Facturación: fuente canónica todavía no conectada a 2.3I.";
                break;

            case BistroBuilderSupplierUnlockRuleKind.Reputacion:
                result.sourceAvailable = safeFacts.hasReputation;
                result.currentNumericValue = Math.Max(0, safeFacts.reputationPoints);
                result.satisfied = result.sourceAvailable && result.currentNumericValue >= result.requiredNumericValue;
                result.progress01 = result.sourceAvailable ? Ratio(result.currentNumericValue, result.requiredNumericValue) : 0f;
                result.reasonCode = result.sourceAvailable ? "reputation" : "reputation_source_unavailable";
                result.reasonText = result.sourceAvailable
                    ? "Reputación: " + result.currentNumericValue + " / " + result.requiredNumericValue + "."
                    : "Reputación: fuente canónica todavía no conectada a 2.3I.";
                break;

            case BistroBuilderSupplierUnlockRuleKind.TamanoRestaurante:
                result.sourceAvailable = safeFacts.hasRestaurantCapacity;
                result.currentNumericValue = Math.Max(0, safeFacts.restaurantCapacitySeats);
                result.satisfied = result.sourceAvailable && result.currentNumericValue >= result.requiredNumericValue;
                result.progress01 = result.sourceAvailable ? Ratio(result.currentNumericValue, result.requiredNumericValue) : 0f;
                result.reasonCode = result.sourceAvailable ? "restaurant_capacity" : "restaurant_capacity_source_unavailable";
                result.reasonText = result.sourceAvailable
                    ? "Capacidad del restaurante: " + result.currentNumericValue + " / " + result.requiredNumericValue + "."
                    : "Tamaño del restaurante: fuente canónica todavía no conectada a 2.3I.";
                break;

            case BistroBuilderSupplierUnlockRuleKind.CategoriaCulinaria:
                result.sourceAvailable = safeFacts.hasCuisineCategories;
                result.currentText = JoinNormalized(safeFacts.cuisineCategories);
                result.satisfied = result.sourceAvailable && ContainsNormalized(safeFacts.cuisineCategories, result.requiredText);
                result.progress01 = result.satisfied ? 1f : 0f;
                result.reasonCode = result.sourceAvailable ? "cuisine_category" : "cuisine_category_source_unavailable";
                result.reasonText = result.sourceAvailable
                    ? "Categoría culinaria requerida: " + result.requiredText + "."
                    : "Categoría culinaria: fuente canónica todavía no conectada a 2.3I.";
                break;

            case BistroBuilderSupplierUnlockRuleKind.ConsumoFamiliaIngrediente:
                long familyConsumption;
                result.sourceAvailable = TryGetFamilyConsumption(safeFacts, result.requiredText, out familyConsumption);
                result.currentNumericValue = Math.Max(0L, familyConsumption);
                result.satisfied = result.sourceAvailable && result.currentNumericValue >= result.requiredNumericValue;
                result.progress01 = result.sourceAvailable ? Ratio(result.currentNumericValue, result.requiredNumericValue) : 0f;
                result.reasonCode = result.sourceAvailable ? "ingredient_family_consumption" : "ingredient_family_source_unavailable";
                result.reasonText = result.sourceAvailable
                    ? "Consumo de familia " + result.requiredText + ": " + result.currentNumericValue + " / " + result.requiredNumericValue + " microunidades."
                    : "Consumo de familia de ingrediente: fuente canónica todavía no conectada a 2.3I para " + result.requiredText + ".";
                break;

            default:
                result.sourceAvailable = false;
                result.satisfied = false;
                result.progress01 = 0f;
                result.reasonCode = "unsupported_or_none";
                result.reasonText = "Condición de desbloqueo no configurada.";
                break;
        }

        return result;
    }

    public static BistroBuilderSupplierProgressionSnapshot CreateInitialSnapshot(
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers,
        int currentGameDay,
        ulong marketSeed,
        ulong commercialSeed)
    {
        BistroBuilderSupplierProgressionSnapshot snapshot = new BistroBuilderSupplierProgressionSnapshot
        {
            currentGameDay = Math.Max(1, currentGameDay),
            sourceMarketSeed = marketSeed,
            sourceCommercialSeed = commercialSeed,
            progressionRevision = 1
        };

        if (suppliers == null)
        {
            return snapshot;
        }

        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier == null || !supplier.isActive || string.IsNullOrWhiteSpace(supplier.SupplierId))
            {
                continue;
            }

            bool fromStart = supplier.unlockProfile != null && supplier.unlockProfile.availableFromStart;
            snapshot.suppliers.Add(new BistroBuilderSupplierProgressionStateRecord
            {
                supplierId = supplier.SupplierId,
                unlocked = fromStart,
                unlockedGameDay = fromStart ? Math.Max(1, currentGameDay) : 0,
                unlockReasonCode = fromStart ? "available_from_start" : string.Empty,
                unlockReasonText = fromStart ? "Disponible desde el inicio." : string.Empty,
                stateRevision = 1
            });
        }

        return snapshot;
    }

    public static bool IsPurchaseVolumeQualifiedStatus(
        BistroBuilderPurchaseOrderStatus status,
        BistroBuilderSupplierProgressionSettings settings)
    {
        if (settings == null)
        {
            return status == BistroBuilderPurchaseOrderStatus.InDelivery ||
                   status == BistroBuilderPurchaseOrderStatus.Delivered;
        }

        return (settings.CountInDeliveryOrders && status == BistroBuilderPurchaseOrderStatus.InDelivery) ||
               (settings.CountDeliveredOrders && status == BistroBuilderPurchaseOrderStatus.Delivered);
    }

    public static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }

    private static bool TryGetFamilyConsumption(
        BistroBuilderSupplierProgressionFacts facts,
        string normalizedFamily,
        out long value)
    {
        value = 0L;
        if (facts == null || facts.ingredientFamilyConsumption == null || string.IsNullOrEmpty(normalizedFamily))
        {
            return false;
        }

        for (int index = 0; index < facts.ingredientFamilyConsumption.Count; index++)
        {
            BistroBuilderSupplierIngredientFamilyConsumptionFact fact = facts.ingredientFamilyConsumption[index];
            if (fact != null && string.Equals(NormalizeToken(fact.familyId), normalizedFamily, StringComparison.Ordinal))
            {
                value = Math.Max(0L, fact.consumedMicrounits);
                return true;
            }
        }

        return false;
    }

    private static bool ContainsNormalized(List<string> values, string target)
    {
        if (values == null || string.IsNullOrEmpty(target))
        {
            return false;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(NormalizeToken(values[index]), target, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string JoinNormalized(List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        List<string> normalized = new List<string>();
        for (int index = 0; index < values.Count; index++)
        {
            string value = NormalizeToken(values[index]);
            if (!string.IsNullOrEmpty(value))
            {
                normalized.Add(value);
            }
        }
        return string.Join(", ", normalized.ToArray());
    }

    private static string BuildLockedSummary(List<BistroBuilderSupplierUnlockConditionResult> conditions)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return "Bloqueado.";
        }

        int pending = 0;
        int unavailable = 0;
        for (int index = 0; index < conditions.Count; index++)
        {
            BistroBuilderSupplierUnlockConditionResult condition = conditions[index];
            if (condition == null || condition.satisfied)
            {
                continue;
            }
            pending++;
            if (!condition.sourceAvailable)
            {
                unavailable++;
            }
        }

        if (unavailable > 0)
        {
            return "Bloqueado: faltan " + pending + " requisito(s); " + unavailable + " dependen de una fuente canónica todavía no conectada.";
        }
        return "Bloqueado: faltan " + pending + " requisito(s).";
    }

    private static float Ratio(long current, long required)
    {
        if (required <= 0L)
        {
            return 1f;
        }
        double ratio = (double)Math.Max(0L, current) / required;
        return Clamp01((float)ratio);
    }

    private static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }

    private static string FormatEuros(long cents)
    {
        return (Math.Max(0L, cents) / 100d).ToString("0.00") + " €";
    }
}
