using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Autotest de compra y persistencia de mejoras.</summary>
public static class BistroBuilderProgression9BSelfTest
{
    [MenuItem("Tools/Bistro Builder/Progression/9B - Autotest", false, 9012)]
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
        int okCount = 0; int failCount = 0;
        var lines = new List<string> { "=== BISTRO BUILDER — 9B / COMPRA Y PERSISTENCIA ===" };
        void Check(bool condition, string message)
        {
            if (condition) { okCount++; lines.Add("[OK] " + message); }
            else { failCount++; lines.Add("[FAIL] " + message); }
        }

        List<BistroBuilderUpgradeDefinition> seed = BistroBuilderProgression9ASeed.Build();
        var first = seed[0];
        var initial = BistroBuilderProgressionEngine.CreateInitialSnapshot();
        Check(BistroBuilderProgressionEngine.TryValidateSnapshot(initial, out _),
            "El estado inicial de mejoras es persistible y válido.");

        bool purchased = BistroBuilderProgressionEngine.TryCreatePurchaseCandidate(
            initial, first, 3, out var candidate, out _);
        Check(purchased && candidate.purchased.Count == 1 &&
              candidate.purchased[0].purchasedDayIndex == 3 &&
              candidate.purchased[0].paidCents == first.costCents,
            "La compra conserva ID, día e importe realmente pagado.");
        Check(initial.purchased.Count == 0 && initial.revision == 0,
            "La transacción pura no muta el estado de origen.");

        string json = JsonUtility.ToJson(candidate);
        var roundTrip = JsonUtility.FromJson<BistroBuilderUpgradeSnapshot>(json);
        Check(BistroBuilderProgressionEngine.TryValidateSnapshot(roundTrip, out _) &&
              roundTrip.purchased.Count == 1 && roundTrip.revision == 1,
            "El snapshot sobrevive a una serialización JSON real.");
        Check(roundTrip.purchased[0].upgradeId == first.upgradeId &&
              roundTrip.purchased[0].paidCents == first.costCents,
            "Save/Load conserva identidad e importe de la mejora.");

        var duplicate = candidate.DeepClone();
        duplicate.purchased.Add(candidate.purchased[0].DeepClone());
        Check(!BistroBuilderProgressionEngine.TryValidateSnapshot(duplicate, out _),
            "Persistencia rechaza adquisiciones duplicadas.");

        Check(BistroBuilderUpgradeSaveSectionProvider.StableSectionId == "progression.upgrades" &&
              BistroBuilderUpgradeSaveSectionProvider.StableSectionVersion == 1,
            "La sección de guardado usa ID y versión estables.");
        Check(BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.dining_room") &&
              BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.kitchen") &&
              BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.infrastructure"),
            "Las compras usan el contrato financiero 3F existente.");

        var context = new BistroBuilderUpgradeAvailabilityContext
        {
            progressionLevel = 2,
            reputationBasisPoints = 5000,
            availableCashCents = 1000000
        };
        context.capabilityIds.Add("facility.dining_room");
        context.purchasedUpgradeIds.Add(first.upgradeId);
        var second = seed[1];
        Check(BistroBuilderProgressionEngine.EvaluateAvailability(second, context).state ==
            BistroBuilderUpgradeAvailabilityState.Available,
            "Una compra restaurada satisface prerrequisitos posteriores.");

        var malformed = candidate.DeepClone();
        malformed.schemaVersion = 999;
        Check(!BistroBuilderProgressionEngine.TryValidateSnapshot(malformed, out _),
            "Una versión futura/desconocida no se acepta silenciosamente.");

        passed = okCount;
        failed = failCount;
        report = string.Join("\n", lines) + "\nResultado: " + passed +
            " OK / " + failed + " fallos.";
        return failed == 0;
    }
}