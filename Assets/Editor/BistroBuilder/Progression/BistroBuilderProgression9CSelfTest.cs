using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderProgression9CSelfTest
{
    [MenuItem("Tools/Bistro Builder/Progression/9C - Autotest", false, 9022)]
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
        int p = 0; int fCount = 0;
        var lines = new List<string>();
        void Check(bool condition, string text)
        {
            if (condition) { p++; lines.Add("[OK] " + text); }
            else { fCount++; lines.Add("[FAIL] " + text); }
        }

        List<BistroBuilderProgressionMilestoneDefinition> seed =
            BistroBuilderProgression9CSeed.Build();
        Check(BistroBuilderProgressionMilestoneEngine.TryValidateCatalog(seed, out _),
            "El catálogo de hitos V1 es válido y monotónico.");
        Check(seed.Count == 4 && seed[0].targetLevel == 2 && seed[3].targetLevel == 5,
            "La progresión V1 cubre de nivel 2 a nivel 5 sin saltos.");

        var context = new BistroBuilderProgressionMilestoneContext
        {
            currentLevel = 1,
            reputationBasisPoints = 5000,
            purchasedUpgradeCount = 2,
            cumulativeRevenueCents = 50000,
            profitableDays = 0
        };
        var evaluation = BistroBuilderProgressionMilestoneEngine.EvaluateNext(seed, context);
        Check(evaluation.completed && evaluation.milestone.targetLevel == 2,
            "El primer hito se completa al alcanzar todos sus requisitos.");

        context.reputationBasisPoints = 4999;
        evaluation = BistroBuilderProgressionMilestoneEngine.EvaluateNext(seed, context);
        Check(!evaluation.completed && evaluation.unmetRequirements.Count == 1,
            "La reputación real puede bloquear el avance.");
        context.reputationBasisPoints = 5000;
        context.purchasedUpgradeCount = 1;
        evaluation = BistroBuilderProgressionMilestoneEngine.EvaluateNext(seed, context);
        Check(!evaluation.completed,
            "Las mejoras adquiridas forman parte del progreso real.");
        context.purchasedUpgradeCount = 2;
        context.cumulativeRevenueCents = 49999;
        evaluation = BistroBuilderProgressionMilestoneEngine.EvaluateNext(seed, context);
        Check(!evaluation.completed,
            "El rendimiento económico acumulado puede bloquear el avance.");

        context.currentLevel = 2;
        context.reputationBasisPoints = 5250;
        context.purchasedUpgradeCount = 4;
        context.cumulativeRevenueCents = 150000;
        context.profitableDays = 1;
        evaluation = BistroBuilderProgressionMilestoneEngine.EvaluateNext(seed, context);
        Check(!evaluation.completed,
            "Los días rentables introducen rendimiento sostenido, no solo caja puntual.");
        context.profitableDays = 2;
        evaluation = BistroBuilderProgressionMilestoneEngine.EvaluateNext(seed, context);
        Check(evaluation.completed && evaluation.milestone.targetLevel == 3,
            "El segundo hito avanza solo con rendimiento sostenido completo.");

        context.currentLevel = 5;
        evaluation = BistroBuilderProgressionMilestoneEngine.EvaluateNext(seed, context);
        Check(evaluation.milestone == null && !evaluation.completed,
            "Al alcanzar el techo V1 no se inventan niveles adicionales.");

        List<BistroBuilderProgressionMilestoneDefinition> broken =
            BistroBuilderProgression9CSeed.Build();
        broken[2].targetLevel = 5;
        Check(!BistroBuilderProgressionMilestoneEngine.TryValidateCatalog(broken, out _),
            "El catálogo rechaza saltos de nivel.");

        broken = BistroBuilderProgression9CSeed.Build();
        broken[2].requiredCumulativeRevenueCents = 1000;
        Check(!BistroBuilderProgressionMilestoneEngine.TryValidateCatalog(broken, out _),
            "El catálogo rechaza requisitos que retroceden.");

        report = "=== BISTRO BUILDER — 9C / HITOS Y EVOLUCIÓN ===\n" +
            string.Join("\n", lines) + "\nResultado: " + p +
            " OK / " + fCount + " fallos.";
        passed = p; failed = fCount;
        return fCount == 0;
    }
}
