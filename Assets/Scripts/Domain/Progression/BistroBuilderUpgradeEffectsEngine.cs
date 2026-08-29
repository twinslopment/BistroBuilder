using System;
using System.Collections.Generic;

/// <summary>Agregador puro de efectos de mejoras compradas.</summary>
public static class BistroBuilderUpgradeEffectsEngine
{
    public static int SumPurchasedEffect(
        IReadOnlyList<BistroBuilderUpgradeDefinition> definitions,
        BistroBuilderUpgradeSnapshot snapshot,
        BistroBuilderUpgradeEffectKind kind,
        bool barContext)
    {
        if (definitions == null || snapshot?.purchased == null) return 0;

        var purchased = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.purchased.Count; i++)
        {
            string id = BistroBuilderProgressionEngine.NormalizeId(
                snapshot.purchased[i]?.upgradeId);
            if (id.Length > 0) purchased.Add(id);
        }

        int total = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            BistroBuilderUpgradeDefinition definition = definitions[i];
            if (definition == null ||
                !purchased.Contains(BistroBuilderProgressionEngine.NormalizeId(definition.upgradeId)) ||
                definition.effects == null)
                continue;
            for (int e = 0; e < definition.effects.Count; e++)
            {
                BistroBuilderUpgradeEffectDefinition effect = definition.effects[e];
                if (effect == null || effect.kind != kind) continue;
                if (effect.barServiceOnly && !barContext) continue;
                total = Math.Max(-5000, Math.Min(5000, total + effect.basisPoints));
            }
        }
        return total;
    }
}
