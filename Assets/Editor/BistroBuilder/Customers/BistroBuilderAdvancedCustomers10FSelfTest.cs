using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderAdvancedCustomers10FSelfTest
{
    [MenuItem("Tools/Bistro Builder/Customers/10F - Autotest", false, 10052)]
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
        int p = 0; int f = 0;
        var lines = new List<string>();
        void Check(bool condition, string text)
        {
            if (condition) { p++; lines.Add("[OK] " + text); }
            else { f++; lines.Add("[FAIL] " + text); }
        }
        var visit = new BistroBuilderReputationVisitRuntimeRecord
        {
            groupId = 700,
            segmentId = "general",
            partySize = 2,
            expectedFoodSeconds = 20f,
            foodWaitSeconds = 30f,
            foodQualityPotentialBasisPoints = 7000,
            ambienceScoreBasisPoints = 5000
        };
        var profile = new BistroBuilderAdvancedCustomerGroupProfile
        {
            groupId = 700,
            dayIndex = 1,
            segmentId = "general"
        };
        profile.members.Add(Member("customer.fast", 1, 10000));
        profile.members.Add(Member("customer.patient", 2, 22000));

        bool ok = BistroBuilderAdvancedCustomerBehaviorEngine.TryEvaluate(
            visit, profile, CustomerGroupState.WaitingForFood,
            out BistroBuilderAdvancedCustomerGroupBehavior behavior,
            out string error);
        Check(ok && string.IsNullOrEmpty(error),
            "10F evalúa una reacción desde visita + perfiles válidos.");
        Check(behavior != null && behavior.individuals.Count == 2,
            "La reacción conserva individualidad dentro del CustomerGroup.");
        Check(behavior.individuals[0].patiencePressureBasisPoints >
            behavior.individuals[1].patiencePressureBasisPoints,
            "Dos clientes reaccionan distinto ante la misma espera.");
        Check(behavior.dominantReason == BistroBuilderCustomerBehaviorReason.FoodWait,
            "El motivo conductual refleja el estado de servicio actual.");
        Check(behavior.maximumPressureBasisPoints ==
            behavior.individuals[0].patiencePressureBasisPoints,
            "El grupo expone la reacción más crítica sin borrar la media.");

        visit.foodWaitSeconds = 2f;
        BistroBuilderAdvancedCustomerBehaviorEngine.TryEvaluate(
            visit, profile, CustomerGroupState.WaitingForFood,
            out BistroBuilderAdvancedCustomerGroupBehavior early, out _);
        Check(early.dominantMood == BistroBuilderCustomerBehaviorMood.Calm,
            "Una espera corta mantiene al grupo calmado.");

        BistroBuilderAdvancedCustomerBehaviorEngine.TryEvaluate(
            visit, profile, CustomerGroupState.Eating,
            out BistroBuilderAdvancedCustomerGroupBehavior eating, out _);
        Check(eating.dominantReason == BistroBuilderCustomerBehaviorReason.None &&
            eating.maximumPressureBasisPoints == 0,
            "Estados sin espera no inventan impaciencia.");

        Check(BistroBuilderAdvancedCustomerBehaviorEngine.ResolveMood(9500) ==
            BistroBuilderCustomerBehaviorMood.Critical,
            "La presión extrema produce una reacción crítica explícita.");

        passed = p; failed = f;
        report = "=== BISTRO BUILDER — 10F / COMPORTAMIENTO EN SERVICIO ===\n" +
            string.Join("\n", lines) + "\nResultado: " + p +
            " OK / " + f + " fallos.";
        return f == 0;
    }

    private static BistroBuilderAdvancedCustomerMemberProfile Member(
        string id, int index, int foodToleranceBasisPoints)
    {
        return new BistroBuilderAdvancedCustomerMemberProfile
        {
            customerId = id,
            memberIndex = index,
            archetypeId = "balanced",
            tableWaitToleranceSeconds = 60f,
            waiterWaitToleranceSeconds = 40f,
            foodWaitToleranceBasisPoints = foodToleranceBasisPoints,
            billWaitToleranceSeconds = 35f,
            serviceSensitivityBasisPoints = 5000,
            qualitySensitivityBasisPoints = 5000,
            priceSensitivityBasisPoints = 5000,
            ambienceSensitivityBasisPoints = 5000,
            preferredDishCategoryIds = new List<string>(),
            avoidedDishCategoryIds = new List<string>()
        };
    }
}
