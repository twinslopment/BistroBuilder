using System;
using System.Collections.Generic;

/// <summary>Motor puro de hitos de negocio del Bloque 9.</summary>
public static class BistroBuilderProgressionMilestoneEngine
{
    public static bool TryValidateCatalog(
        IReadOnlyList<BistroBuilderProgressionMilestoneDefinition> definitions,
        out string error)
    {
        if (definitions == null || definitions.Count == 0)
        {
            error = "El catálogo de hitos está vacío.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        int previousLevel = 1;
        long previousRevenue = -1L;
        int previousReputation = -1;
        int previousUpgrades = -1;
        int previousProfitableDays = -1;
        for (int i = 0; i < definitions.Count; i++)
        {
            BistroBuilderProgressionMilestoneDefinition item = definitions[i];
            if (item == null ||
                !BistroBuilderProgressionEngine.IsSafeStableId(item.milestoneId) ||
                !BistroBuilderProgressionEngine.IsSafeStableId(item.stageId) ||
                string.IsNullOrWhiteSpace(item.displayName) ||
                !ids.Add(BistroBuilderProgressionEngine.NormalizeId(item.milestoneId)))
            {
                error = "Existe un hito nulo, duplicado o con identidad inválida.";
                return false;
            }
            if (item.targetLevel != previousLevel + 1 ||
                item.requiredReputationBasisPoints < 0 ||
                item.requiredReputationBasisPoints > 10000 ||
                item.requiredPurchasedUpgradeCount < 0 ||
                item.requiredCumulativeRevenueCents < 0L ||
                item.requiredProfitableDays < 0)
            {
                error = item.milestoneId + ": requisitos o nivel objetivo inválidos.";
                return false;
            }
            if (item.requiredCumulativeRevenueCents < previousRevenue ||
                item.requiredReputationBasisPoints < previousReputation ||
                item.requiredPurchasedUpgradeCount < previousUpgrades ||
                item.requiredProfitableDays < previousProfitableDays)
            {
                error = item.milestoneId + ": los requisitos no pueden retroceder.";
                return false;
            }
            previousLevel = item.targetLevel;
            previousRevenue = item.requiredCumulativeRevenueCents;
            previousReputation = item.requiredReputationBasisPoints;
            previousUpgrades = item.requiredPurchasedUpgradeCount;
            previousProfitableDays = item.requiredProfitableDays;
        }
        error = string.Empty;
        return true;
    }

    public static BistroBuilderProgressionMilestoneEvaluation EvaluateNext(
        IReadOnlyList<BistroBuilderProgressionMilestoneDefinition> definitions,
        BistroBuilderProgressionMilestoneContext context)
    {
        var result = new BistroBuilderProgressionMilestoneEvaluation();
        if (definitions == null || context == null) return result;
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null && definitions[i].targetLevel > context.currentLevel)
            {
                result.milestone = definitions[i].DeepClone();
                break;
            }
        }
        if (result.milestone == null) return result;

        BistroBuilderProgressionMilestoneDefinition target = result.milestone;
        if (context.reputationBasisPoints < target.requiredReputationBasisPoints)
            result.unmetRequirements.Add("Reputación " +
                (target.requiredReputationBasisPoints / 100d).ToString("0.##") + "%");
        if (context.purchasedUpgradeCount < target.requiredPurchasedUpgradeCount)
            result.unmetRequirements.Add("Mejoras " + target.requiredPurchasedUpgradeCount);
        if (context.cumulativeRevenueCents < target.requiredCumulativeRevenueCents)
            result.unmetRequirements.Add("Ingresos acumulados " +
                (target.requiredCumulativeRevenueCents / 100m).ToString("0.00") + " €");
        if (context.profitableDays < target.requiredProfitableDays)
            result.unmetRequirements.Add("Días rentables " + target.requiredProfitableDays);
        result.completed = result.unmetRequirements.Count == 0;
        return result;
    }
}
