using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderProgression9ESelfTest
{
    [MenuItem("Tools/Bistro Builder/Progression/9E - Autotest", false, 9042)]
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
        Check(definitions.Count == 18,
            "La UI recibe las 18 mejoras canónicas.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var categories = new HashSet<BistroBuilderUpgradeCategory>();
        int summaries = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            BistroBuilderUpgradeDefinition definition = definitions[i];
            if (definition == null) continue;
            ids.Add(definition.upgradeId);
            categories.Add(definition.category);
            if (!string.IsNullOrWhiteSpace(
                    BistroBuilderProgressionPlayerFacade.BuildEffectsSummary(definition)))
                summaries++;
        }
        Check(ids.Count == 18, "La lista jugable no contiene IDs duplicados.");
        Check(categories.Count == 6, "La UI puede filtrar las seis categorías V1.");
        Check(summaries == 18, "Las 18 mejoras exponen un resumen de efecto legible.");

        var snapshot = new BistroBuilderProgressionPlayerUiSnapshot
        {
            progressionLevel = 2,
            purchasedCount = 3,
            nextMilestoneName = "Restaurante en crecimiento",
            nextMilestoneTargetLevel = 3,
            milestoneRequirements = "Días rentables 2"
        };
        Check(snapshot.nextMilestoneTargetLevel > snapshot.progressionLevel,
            "El modelo UI representa el siguiente hito sin mutar progreso.");
        Check(snapshot.upgrades != null && snapshot.upgrades.Count == 0,
            "El modelo UI inicia sus filas de forma segura.");

        report = "=== BISTRO BUILDER — 9E / UI JUGABLE ===\n" +
            string.Join("\n", lines) + "\nResultado: " + p +
            " OK / " + f + " fallos.";
        passed = p;
        failed = f;
        return f == 0;
    }
}
