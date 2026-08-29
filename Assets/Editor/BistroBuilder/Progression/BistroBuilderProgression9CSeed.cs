using System.Collections.Generic;

/// <summary>Hitos V1 de evolución del negocio. Valores balanceables sin cambiar lógica.</summary>
public static class BistroBuilderProgression9CSeed
{
    public static List<BistroBuilderProgressionMilestoneDefinition> Build()
    {
        return new List<BistroBuilderProgressionMilestoneDefinition>
        {
            M("progression.established", "restaurant.established", "Restaurante establecido",
                2, 5000, 2, 50000, 0),
            M("progression.growing", "restaurant.growing", "Restaurante en crecimiento",
                3, 5250, 4, 150000, 2),
            M("progression.consolidated", "restaurant.consolidated", "Restaurante consolidado",
                4, 5500, 7, 400000, 4),
            M("progression.signature", "restaurant.signature", "Restaurante de referencia",
                5, 5800, 10, 800000, 7)
        };
    }

    private static BistroBuilderProgressionMilestoneDefinition M(
        string id, string stage, string name, int level, int reputation,
        int upgrades, long revenueCents, int profitableDays)
    {
        return new BistroBuilderProgressionMilestoneDefinition
        {
            milestoneId = id,
            stageId = stage,
            displayName = name,
            targetLevel = level,
            requiredReputationBasisPoints = reputation,
            requiredPurchasedUpgradeCount = upgrades,
            requiredCumulativeRevenueCents = revenueCents,
            requiredProfitableDays = profitableDays
        };
    }
}
