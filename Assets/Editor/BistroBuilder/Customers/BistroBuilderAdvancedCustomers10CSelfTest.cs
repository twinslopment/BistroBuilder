using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>Autotest puro de historial, habituales y VIP.</summary>
public static class BistroBuilderAdvancedCustomers10CSelfTest
{
    [MenuItem("Tools/Bistro Builder/Customers/10C - Autotest", false, 10022)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) UnityEngine.Debug.Log(report);
        else UnityEngine.Debug.LogError(report);
    }

    public static void RunFromCommandLine()
    {
        if (!Run(out _, out _, out string report))
            throw new InvalidOperationException(report);
        UnityEngine.Debug.Log(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        int ok = 0;
        int fail = 0;
        var lines = new List<string>
        {
            "=== BISTRO BUILDER — 10C / HISTORIAL Y FIDELIDAD ==="
        };
        void Check(bool condition, string message)
        {
            if (condition) { ok++; lines.Add("[OK] " + message); }
            else { fail++; lines.Add("[FAIL] " + message); }
        }

        BistroBuilderAdvancedCustomerHistorySnapshot state =
            BistroBuilderAdvancedCustomerHistoryEngine.CreateEmptySnapshot();
        Check(BistroBuilderAdvancedCustomerHistoryEngine.TryValidateSnapshot(
            state, out _), "El snapshot inicial de historial es válido.");

        var outcome = Outcome(77, "visit.day1.group77", 1, 8500, 6000);
        bool registeredOutcome =
            BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterOutcome(
                state, outcome, out state, out bool changedOutcome, out _);
        Check(registeredOutcome && changedOutcome && state.pendingOutcomes.Count == 1,
            "Un resultado puede llegar antes que GuestRelations sin perderse.");

        var assignment = Assignment(77, "guest_cohort_000077", 1);
        bool assigned = BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterAssignment(
            state, assignment, out state, out bool changedAssignment, out _);
        Check(assigned && changedAssignment && state.pendingOutcomes.Count == 0 &&
            state.pendingAssignments.Count == 0 && state.customers.Count == 1,
            "Resultado y cohorte se reconcilian independientemente del orden.");

        BistroBuilderAdvancedCustomerHistoryRecord first = state.customers[0];
        Check(first.visitCount == 1 && first.loyaltyTier ==
                BistroBuilderCustomerLoyaltyTier.New,
            "La primera visita queda identificada como cliente nuevo.");
        Check(first.recentVisits.Count == 1 && first.AverageSatisfactionBasisPoints == 8500,
            "El historial conserva visita, gasto y satisfacción reales.");

        bool reverseAssignment =
            BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterAssignment(
                state, Assignment(88, "guest_cohort_000088", 2),
                out state, out _, out _);
        bool reverseOutcome =
            BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterOutcome(
                state, Outcome(88, "visit.day2.group88", 2, 7000, 3000),
                out state, out _, out _);
        Check(reverseAssignment && reverseOutcome && state.customers.Count == 2,
            "La reconciliación funciona también cuando la cohorte llega primero.");

        string cohortId = "guest_cohort_000077";
        for (int visit = 2; visit <= 5; visit++)
        {
            int groupId = 100 + visit;
            string experienceId = "visit.day" + visit + ".group" + groupId;
            BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterOutcome(
                state, Outcome(groupId, experienceId, visit, 8500, 6000),
                out state, out _, out _);
            BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterAssignment(
                state, Assignment(groupId, cohortId, visit),
                out state, out _, out _);
        }

        BistroBuilderAdvancedCustomerHistoryEngine.TryGetCustomer(
            state, cohortId, out BistroBuilderAdvancedCustomerHistoryRecord loyal);
        Check(loyal != null && loyal.visitCount == 5,
            "Un habitual conserva contador de visitas acumulado por cohorte.");
        Check(loyal != null && loyal.loyaltyTier == BistroBuilderCustomerLoyaltyTier.Vip,
            "Cinco visitas satisfactorias y gasto suficiente elevan a VIP.");
        Check(loyal != null && loyal.lifetimeSpendCents == 30000L,
            "El gasto histórico se acumula sin autoridad financiera paralela.");

        long revisionBeforeDuplicate = state.revision;
        bool duplicate = BistroBuilderAdvancedCustomerHistoryEngine.TryRegisterOutcome(
            state, Outcome(105, "visit.day5.group105", 5, 8500, 6000),
            out BistroBuilderAdvancedCustomerHistorySnapshot duplicateState,
            out bool duplicateChanged, out _);
        Check(duplicate && !duplicateChanged &&
                duplicateState.revision == revisionBeforeDuplicate,
            "Repetir el mismo resultado pendiente es idempotente.");

        BistroBuilderAdvancedCustomerHistorySnapshot roundTrip = state.DeepClone();
        Check(BistroBuilderAdvancedCustomerHistoryEngine.TryValidateSnapshot(
                roundTrip, out _) && roundTrip.customers.Count == state.customers.Count,
            "El historial completo admite round-trip persistente.");

        passed = ok;
        failed = fail;
        report = string.Join("\n", lines) + "\nResultado: " + passed +
            " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static BistroBuilderAdvancedCustomerVisitOutcome Outcome(
        int groupId,
        string experienceId,
        int day,
        int satisfaction,
        long spend)
    {
        return new BistroBuilderAdvancedCustomerVisitOutcome
        {
            groupId = groupId,
            experienceId = experienceId,
            segmentId = "general",
            dayIndex = day,
            paidAmountCents = spend,
            overallSatisfactionBasisPoints = satisfaction,
            waitingScoreBasisPoints = 7000,
            foodQualityScoreBasisPoints = 8200,
            valueForMoneyScoreBasisPoints = 7600
        };
    }

    private static BistroBuilderAdvancedCustomerCohortAssignment Assignment(
        int groupId,
        string cohortId,
        int day)
    {
        return new BistroBuilderAdvancedCustomerCohortAssignment
        {
            groupId = groupId,
            cohortId = cohortId,
            segmentId = "general",
            dayIndex = day
        };
    }
}
