using System;
using UnityEngine;

public static class BistroBuilderEndOfDay15SelfTest
{
    public static bool Run(out int passed, out int failed, out string report)
    {
        int localPassed = 0; int localFailed = 0;
        var lines = new System.Collections.Generic.List<string>();
        void Check(bool ok, string label)
        {
            if (ok) { localPassed++; lines.Add("OK - " + label); }
            else { localFailed++; lines.Add("FAIL - " + label); }
        }

        int strong = BistroBuilderEndOfDayEngine.ComputePerformanceBasisPoints(
            50000, 100000, 9000, 0, 0);
        int weak = BistroBuilderEndOfDayEngine.ComputePerformanceBasisPoints(
            -10000, 50000, 4500, 3, 4);
        Check(strong > weak, "El rendimiento premia beneficio y satisfacción");
        Check(strong >= 0 && strong <= 10000 && weak >= 0 && weak <= 10000,
            "Rendimiento siempre acotado");

        var summary = new BistroBuilderEndOfDaySummary
        {
            dayIndex = 3, calendarYear = 1, calendarMonth = 1, calendarDay = 3,
            closedUtc = DateTime.UtcNow.ToString("O"), revenueCents = 120000,
            totalExpensesCents = 70000, operatingResultCents = 50000,
            grossProfitCents = 80000, paidOrderCount = 12, consumedLineCount = 22,
            servedGroupCount = 9, averageSatisfactionBasisPoints = 8800,
            reputationBeforeBasisPoints = 6100, reputationAfterBasisPoints = 6300,
            reputationDeltaBasisPoints = 200, performanceBasisPoints = strong
        };
        BistroBuilderEndOfDayEngine.BuildGuidance(summary);
        Check(BistroBuilderEndOfDayEngine.TryValidateSummary(summary, out _),
            "Resumen válido aceptado");
        Check(summary.highlights.Count > 0, "Genera momentos destacados");
        Check(summary.opportunities.Count > 0, "Genera oportunidades");
        Check(summary.recommendations.Count > 0, "Genera recomendación siguiente día");

        var problem = summary.DeepClone();
        problem.dayIndex = 4; problem.calendarDay = 4;
        problem.operatingResultCents = -20000; problem.averageSatisfactionBasisPoints = 5200;
        problem.abandonedGroupCount = 2; problem.incidentCount = 3;
        problem.performanceBasisPoints = BistroBuilderEndOfDayEngine.ComputePerformanceBasisPoints(
            problem.operatingResultCents, problem.revenueCents,
            problem.averageSatisfactionBasisPoints, problem.abandonedGroupCount, problem.incidentCount);
        BistroBuilderEndOfDayEngine.BuildGuidance(problem);
        Check(problem.problems.Count >= 3, "Detecta problemas operativos relevantes");
        Check(problem.recommendations.Count >= 2, "Recomienda acciones ante problemas");

        var snapshot = new BistroBuilderEndOfDayHistorySnapshot();
        snapshot.summaries.Add(summary.DeepClone());
        snapshot.summaries.Add(problem.DeepClone());
        snapshot.lastClosedDayIndex = 4; snapshot.revision = 2;
        Check(BistroBuilderEndOfDayEngine.TryValidateSnapshot(snapshot, out _),
            "Historial válido aceptado");
        var clone = snapshot.DeepClone();
        clone.summaries[0].revenueCents++;
        Check(snapshot.summaries[0].revenueCents != clone.summaries[0].revenueCents,
            "Historial clona profundamente");

        var duplicate = snapshot.DeepClone();
        duplicate.summaries[1].dayIndex = 3;
        Check(!BistroBuilderEndOfDayEngine.TryValidateSnapshot(duplicate, out _),
            "Historial rechaza días duplicados");
        var badLast = snapshot.DeepClone(); badLast.lastClosedDayIndex = 99;
        Check(!BistroBuilderEndOfDayEngine.TryValidateSnapshot(badLast, out _),
            "Historial rechaza último cierre incoherente");
        var badScore = summary.DeepClone(); badScore.performanceBasisPoints = 10001;
        Check(!BistroBuilderEndOfDayEngine.TryValidateSummary(badScore, out _),
            "Resumen rechaza score fuera de rango");
        var badDate = summary.DeepClone(); badDate.closedUtc = "invalid";
        Check(!BistroBuilderEndOfDayEngine.TryValidateSummary(badDate, out _),
            "Resumen exige timestamp persistible");
        Check(problem.performanceBasisPoints < summary.performanceBasisPoints,
            "Incidencias, abandonos y baja satisfacción penalizan");
        Check(summary.DeepClone().recommendations.Count == summary.recommendations.Count,
            "Resumen conserva recomendaciones al clonar");
        Check(new BistroBuilderEndOfDayHistorySnapshot().lastClosedDayIndex == 0,
            "Historial vacío representa partida sin cierres");

        passed = localPassed; failed = localFailed;
        report = "BLOQUE 15 - AUTOTEST\n" + string.Join("\n", lines) +
                 "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }
}
