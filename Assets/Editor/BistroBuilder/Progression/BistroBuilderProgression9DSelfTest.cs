using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderProgression9DSelfTest
{
    [MenuItem("Tools/Bistro Builder/Progression/9D - Autotest", false, 9032)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static void RunFromCommandLine()
    {
        if (!Run(out _, out _, out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        int p = 0, f = 0;
        var lines = new List<string>();
        void Check(bool condition, string text)
        {
            if (condition) { p++; lines.Add("[OK] " + text); }
            else { f++; lines.Add("[FAIL] " + text); }
        }
        List<BistroBuilderUpgradeDefinition> definitions =
            BistroBuilderProgression9ASeed.Build();
        Check(BistroBuilderProgressionEngine.TryValidateCatalog(definitions, out _),
            "El catálogo 9A con efectos sigue siendo válido.");
        Check(definitions.Count == 18,
            "Las 18 mejoras semilla conservan identidad y cardinalidad.");

        int withEffects = 0;
        var categories = new HashSet<BistroBuilderUpgradeCategory>();
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i].effects != null && definitions[i].effects.Count > 0)
                withEffects++;
            categories.Add(definitions[i].category);
        }
        Check(withEffects == 18,
            "Todas las mejoras V1 producen al menos un efecto jugable.");
        Check(categories.Count == 6,
            "Los seis tipos de mejora están cubiertos por efectos funcionales.");

        var snapshot = BistroBuilderProgressionEngine.CreateInitialSnapshot();
        snapshot.purchased.Add(P("kitchen.prep_organization"));
        snapshot.purchased.Add(P("bar.storage_upgrade"));
        snapshot.purchased.Add(P("ambience.lighting_plan"));
        snapshot.purchased.Add(P("infrastructure.storage_efficiency"));
        int tablePrep = BistroBuilderUpgradeEffectsEngine.SumPurchasedEffect(
            definitions, snapshot, BistroBuilderUpgradeEffectKind.PreparationDuration, false);
        int barPrep = BistroBuilderUpgradeEffectsEngine.SumPurchasedEffect(
            definitions, snapshot, BistroBuilderUpgradeEffectKind.PreparationDuration, true);
        int ambience = BistroBuilderUpgradeEffectsEngine.SumPurchasedEffect(
            definitions, snapshot, BistroBuilderUpgradeEffectKind.AmbienceScore, false);
        int food = BistroBuilderUpgradeEffectsEngine.SumPurchasedEffect(
            definitions, snapshot, BistroBuilderUpgradeEffectKind.FoodQualityPotential, false);

        Check(tablePrep < 0,
            "Una mejora de cocina reduce duración de preparación en mesa.");
        Check(barPrep < tablePrep,
            "Las mejoras específicas de barra solo se suman en contexto de barra.");
        Check(ambience > 0,
            "Las mejoras de ambiente elevan la percepción ambiental.");
        Check(food > 0,
            "Infraestructura mejora el potencial de calidad de forma medible.");

        var empty = BistroBuilderProgressionEngine.CreateInitialSnapshot();
        Check(BistroBuilderUpgradeEffectsEngine.SumPurchasedEffect(
                definitions, empty, BistroBuilderUpgradeEffectKind.AmbienceScore, false) == 0,
            "Las mejoras no compradas no producen efectos fantasma.");
        BistroBuilderUpgradeDefinition broken = definitions[0].DeepClone();
        broken.effects[0].basisPoints = 9000;
        Check(!BistroBuilderProgressionEngine.TryValidateDefinition(broken, out _),
            "El catálogo rechaza efectos fuera del rango seguro.");

        report = "=== BISTRO BUILDER — 9D / EFECTOS JUGABLES ===\n" +
            string.Join("\n", lines) + "\nResultado: " + p +
            " OK / " + f + " fallos.";
        passed = p;
        failed = f;
        return f == 0;
    }

    private static BistroBuilderPurchasedUpgradeRecord P(string id)
    {
        return new BistroBuilderPurchasedUpgradeRecord
        {
            upgradeId = id,
            purchasedDayIndex = 1,
            paidCents = 1
        };
    }
}
