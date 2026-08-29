using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Autotest puro de la fundación 9A.</summary>
public static class BistroBuilderProgression9ASelfTest
{
    [MenuItem("Tools/Bistro Builder/Progression/9A - Autotest", false, 9002)]
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
        var lines = new List<string> { "=== BISTRO BUILDER — 9A / MEJORAS Y PROGRESIÓN ===" };
        void Check(bool condition, string message)
        {
            if (condition) { okCount++; lines.Add("[OK] " + message); }
            else { failCount++; lines.Add("[FAIL] " + message); }
        }

        List<BistroBuilderUpgradeDefinition> seed = BistroBuilderProgression9ASeed.Build();
        Check(seed.Count == 18, "El seed contiene 18 mejoras data-driven.");
        Check(BistroBuilderProgressionEngine.TryValidateCatalog(seed, out _),
            "El catálogo semilla valida IDs, requisitos y grafo sin ciclos.");

        var counts = new int[6];
        for (int i = 0; i < seed.Count; i++) counts[(int)seed[i].category]++;
        bool balanced = true;
        for (int i = 0; i < counts.Length; i++) balanced &= counts[i] == 3;
        Check(balanced, "Las seis categorías V1 tienen tres mejoras semilla.");

        var richBase = Context(1, 5000, 1000000,
            "restaurant.base", "facility.dining_room", "facility.kitchen");
        var dining1 = Find(seed, "dining.comfort_seating");
        var dining2 = Find(seed, "dining.service_station");
        var terrace = Find(seed, "terrace.basic_comfort");
        var bar = Find(seed, "bar.storage_upgrade");
        var acoustic = Find(seed, "dining.acoustic_treatment");

        Check(BistroBuilderProgressionEngine.EvaluateAvailability(dining1, richBase).state ==
            BistroBuilderUpgradeAvailabilityState.Available,
            "Una mejora básica compatible queda disponible en nivel 1.");
        Check(BistroBuilderProgressionEngine.EvaluateAvailability(terrace, richBase).state ==
            BistroBuilderUpgradeAvailabilityState.Locked,
            "Una terraza inexistente bloquea sus mejoras por capacidad del local.");
        Check(BistroBuilderProgressionEngine.EvaluateAvailability(bar, richBase).state ==
            BistroBuilderUpgradeAvailabilityState.Locked,
            "Una barra inexistente bloquea sus mejoras por capacidad del local.");
        Check(BistroBuilderProgressionEngine.EvaluateAvailability(dining2, richBase).state ==
            BistroBuilderUpgradeAvailabilityState.Locked,
            "El nivel de progresión bloquea mejoras futuras.");

        var level2 = Context(2, 5000, 1000000,
            "restaurant.base", "facility.dining_room", "facility.kitchen");
        Check(BistroBuilderProgressionEngine.EvaluateAvailability(dining2, level2).state ==
            BistroBuilderUpgradeAvailabilityState.Locked,
            "Los prerrequisitos son obligatorios aunque el nivel sea suficiente.");
        level2.purchasedUpgradeIds.Add("dining.comfort_seating");
        Check(BistroBuilderProgressionEngine.EvaluateAvailability(dining2, level2).state ==
            BistroBuilderUpgradeAvailabilityState.Available,
            "Comprar el prerrequisito abre la siguiente mejora compatible.");

        var level3 = Context(3, 5300, 1000000,
            "restaurant.base", "facility.dining_room", "facility.kitchen");
        level3.purchasedUpgradeIds.Add("dining.comfort_seating");
        level3.purchasedUpgradeIds.Add("dining.service_station");
        Check(BistroBuilderProgressionEngine.EvaluateAvailability(acoustic, level3).state ==
            BistroBuilderUpgradeAvailabilityState.Locked,
            "La reputación real puede condicionar mejoras avanzadas.");
        level3.reputationBasisPoints = 5400;
        Check(BistroBuilderProgressionEngine.EvaluateAvailability(acoustic, level3).state ==
            BistroBuilderUpgradeAvailabilityState.Available,
            "Al alcanzar reputación suficiente la mejora se desbloquea.");

        var poor = Context(1, 5000, 1,
            "restaurant.base", "facility.dining_room", "facility.kitchen");
        var poorAvailability = BistroBuilderProgressionEngine.EvaluateAvailability(dining1, poor);
        Check(poorAvailability.state == BistroBuilderUpgradeAvailabilityState.Available &&
            !poorAvailability.affordable,
            "Desbloqueo y capacidad económica son conceptos separados.");

        var snapshot = BistroBuilderProgressionEngine.CreateInitialSnapshot();
        bool firstPurchase = BistroBuilderProgressionEngine.TryCreatePurchaseCandidate(
            snapshot, dining1, 1, out var candidate, out _);
        Check(firstPurchase && candidate.revision == 1 && candidate.purchased.Count == 1,
            "El motor crea una adquisición persistible sin mutar el snapshot origen.");
        Check(!BistroBuilderProgressionEngine.TryCreatePurchaseCandidate(
                candidate, dining1, 1, out _, out _),
            "Una mejora no se puede adquirir dos veces.");

        var cyclic = BistroBuilderProgression9ASeed.Build();
        cyclic[0].prerequisiteUpgradeIds.Add(cyclic[1].upgradeId);
        Check(!BistroBuilderProgressionEngine.TryValidateCatalog(cyclic, out _),
            "El catálogo rechaza ciclos de prerrequisitos.");

        passed = okCount;
        failed = failCount;
        report = string.Join("\n", lines) + "\nResultado: " + passed +
            " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static BistroBuilderUpgradeAvailabilityContext Context(
        int level, int reputation, long cash, params string[] capabilities)
    {
        var context = new BistroBuilderUpgradeAvailabilityContext
        {
            progressionLevel = level,
            reputationBasisPoints = reputation,
            availableCashCents = cash
        };
        for (int i = 0; i < capabilities.Length; i++) context.capabilityIds.Add(capabilities[i]);
        return context;
    }

    private static BistroBuilderUpgradeDefinition Find(
        IReadOnlyList<BistroBuilderUpgradeDefinition> seed, string id)
    {
        for (int i = 0; i < seed.Count; i++)
            if (seed[i].upgradeId == id) return seed[i];
        return null;
    }
}
