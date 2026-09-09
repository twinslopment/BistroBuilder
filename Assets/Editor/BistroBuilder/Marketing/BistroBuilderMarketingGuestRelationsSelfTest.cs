using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate puro de reputación persistente y visitas recurrentes.
/// </summary>
public static class BistroBuilderMarketingGuestRelationsSelfTest
{
    [MenuItem("Tools/Bistro Builder/Marketing/Guest Relations - Autotest", false, 7250)]
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
        passed = 0;
        failed = 0;
        var lines = new List<string>();
        BistroBuilderGuestRelationsSnapshot empty =
            BistroBuilderGuestRelationsEngine.CreateEmptySnapshot();
        Check(
            BistroBuilderGuestRelationsEngine.TryValidateSnapshot(empty, out _),
            "El estado vacío de GuestRelations es válido.",
            ref passed, ref failed, lines);

        Check(
            BistroBuilderGuestRelationsEngine.TryApplyReputationCredit(
                empty, "marketing.reputation.test", 2,
                out BistroBuilderGuestRelationsSnapshot credited,
                out bool changed, out _) &&
            changed && credited.reputationPoints == 2 && credited.revision == 1L,
            "Una campaña acredita reputación persistente una sola vez.",
            ref passed, ref failed, lines);

        Check(
            BistroBuilderGuestRelationsEngine.TryApplyReputationCredit(
                credited, "marketing.reputation.test", 2,
                out BistroBuilderGuestRelationsSnapshot replayed,
                out bool replayChanged, out _) &&
            !replayChanged && replayed.reputationPoints == 2 &&
            replayed.revision == credited.revision,
            "Repetir la misma fuente no duplica reputación.",
            ref passed, ref failed, lines);

        Check(
            BistroBuilderGuestRelationsEngine.ComputeReputationDemandBasisPoints(2) == 200,
            "Dos puntos de reputación aportan +2 % de demanda duradera.",
            ref passed, ref failed, lines);
        Check(
            BistroBuilderGuestRelationsEngine.ComputeReputationDemandBasisPoints(100) == 5000,
            "La reputación queda limitada a +50 % de demanda.",
            ref passed, ref failed, lines);

        Check(
            BistroBuilderGuestRelationsEngine.TryRecordCompletedVisit(
                credited, "workers", 2, 3, string.Empty,
                out BistroBuilderGuestRelationsSnapshot firstVisit,
                out string cohortId, out _) &&
            firstVisit.cohorts.Count == 1 &&
            firstVisit.cohorts[0].visitCount == 1,
            "Una visita completada crea una cohorte persistente.",
            ref passed, ref failed, lines);

        Check(
            BistroBuilderGuestRelationsEngine.TryRecordCompletedVisit(
                firstVisit, "workers", 2, 4, cohortId,
                out BistroBuilderGuestRelationsSnapshot returnVisit,
                out string sameCohortId, out _) &&
            sameCohortId == cohortId && returnVisit.cohorts.Count == 1 &&
            returnVisit.cohorts[0].visitCount == 2 &&
            returnVisit.cohorts[0].lastVisitDay == 4,
            "Una visita recurrente actualiza la cohorte sin duplicarla.",
            ref passed, ref failed, lines);

        var eligible = new List<BistroBuilderGuestVisitCohortRecord>();
        BistroBuilderGuestRelationsEngine.CopyEligibleCohorts(
            returnVisit, 5, eligible);
        Check(
            eligible.Count == 1 && eligible[0].cohortId == cohortId,
            "Solo cohortes de días anteriores pueden volver.",
            ref passed, ref failed, lines);
        Check(
            BistroBuilderGuestRelationsEngine.ConvertRepeatVisitBasisPointsToCount(
                800, 4, 4) == 1 &&
            BistroBuilderGuestRelationsEngine.ConvertRepeatVisitBasisPointsToCount(
                0, 4, 4) == 0,
            "+8 % de repetición produce una vuelta discreta; 0 % no produce ninguna.",
            ref passed, ref failed, lines);

        var returningProfile = new BistroBuilderCustomerAcquisitionProfile
        {
            segmentId = "workers",
            sourceSystemId = BistroBuilderMarketingService.FinanceSourceSystemId,
            sourceReferenceId = "marketing.demand.test",
            marketingInfluenced = true,
            returningVisit = true,
            guestRelationsReferenceId = cohortId,
            preferredGroupSize = 2
        };
        Check(
            returningProfile.TryValidate(out _),
            "El perfil genérico admite una cohorte recurrente y su tamaño previo.",
            ref passed, ref failed, lines);

        returningProfile.guestRelationsReferenceId = string.Empty;
        Check(
            !returningProfile.TryValidate(out _),
            "Una llegada recurrente sin identidad de cohorte se rechaza.",
            ref passed, ref failed, lines);
        bool demandOk = BistroBuilderMarketing7BSelfTest.Run(
            out int demandPassed, out int demandFailed, out _);
        Check(
            demandOk && demandFailed == 0 && demandPassed > 0,
            "ReservationDemand y walk-ins de 7B permanecen verdes.",
            ref passed, ref failed, lines);

        bool pressureOk = BistroBuilderMarketingOperationalPressureSelfTest.Run(
            out int pressurePassed, out int pressureFailed, out _);
        Check(
            pressureOk && pressureFailed == 0 && pressurePassed >= 10,
            "OperationalPressure permanece verde.",
            ref passed, ref failed, lines);

        bool ticketOk = BistroBuilderMarketingAverageTicketSelfTest.Run(
            out int ticketPassed, out int ticketFailed, out _);
        Check(
            ticketOk && ticketFailed == 0 && ticketPassed >= 10,
            "AverageTicket permanece verde.",
            ref passed, ref failed, lines);

        report =
            "=== BISTRO BUILDER — MARKETING / GUEST RELATIONS ===\n" +
            string.Join("\n", lines) +
            "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static void Check(
        bool condition,
        string message,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + message);
        }
        else
        {
            failed++;
            lines.Add("[FAIL] " + message);
        }
    }
}
