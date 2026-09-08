using System;
using System.Collections.Generic;

public enum BistroBuilderEndOfDayPhase
{
    Idle = 0,
    ServiceOpen = 1,
    Closing = 2,
    SummaryReady = 3,
    NextDayPrepared = 4
}

[Serializable]
public sealed class BistroBuilderEndOfDaySummary
{
    public const int CurrentSchemaVersion = 1;
    public int schemaVersion = CurrentSchemaVersion;
    public int dayIndex = 1;
    public int calendarYear = 1;
    public int calendarMonth = 1;
    public int calendarDay = 1;
    public int mealService;
    public string closedUtc = string.Empty;

    public long revenueCents;
    public long totalExpensesCents;
    public long operatingResultCents;
    public long grossProfitCents;
    public int paidOrderCount;
    public int consumedLineCount;
    public long inventoryConsumedCanonicalMilliUnits;
    public long inventoryWasteCanonicalMilliUnits;

    public int servedGroupCount;
    public int abandonedGroupCount;
    public int incidentCount;
    public int averageSatisfactionBasisPoints;
    public int reputationBeforeBasisPoints;
    public int reputationAfterBasisPoints;
    public int reputationDeltaBasisPoints;
    public int performanceBasisPoints;

    public bool hasPreviousDayComparison;
    public long revenueDeltaVsPreviousCents;
    public long operatingResultDeltaVsPreviousCents;
    public int satisfactionDeltaVsPreviousBasisPoints;
    public int performanceDeltaVsPreviousBasisPoints;

    public List<string> highlights = new List<string>();
    public List<string> problems = new List<string>();
    public List<string> opportunities = new List<string>();
    public List<string> recommendations = new List<string>();

    public BistroBuilderEndOfDaySummary DeepClone()
    {
        var clone = (BistroBuilderEndOfDaySummary)MemberwiseClone();
        clone.highlights = highlights != null ? new List<string>(highlights) : new List<string>();
        clone.problems = problems != null ? new List<string>(problems) : new List<string>();
        clone.opportunities = opportunities != null ? new List<string>(opportunities) : new List<string>();
        clone.recommendations = recommendations != null ? new List<string>(recommendations) : new List<string>();
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderEndOfDayHistorySnapshot
{
    public const string CurrentSchemaId = "end_of_day.state";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public int lastClosedDayIndex;
    public bool hasActiveSession;
    public int activeDayIndex;
    public int activeCalendarYear;
    public int activeCalendarMonth;
    public int activeCalendarDay;
    public long openingConsumedCanonicalMilliUnits;
    public long openingWasteCanonicalMilliUnits;
    public int openingReputationBasisPoints;
    public int activeSatisfactionTotalBasisPoints;
    public int activeSatisfactionCount;
    public int activeAbandonedCount;
    public List<string> activeIncidentLineIds = new List<string>();
    public List<BistroBuilderEndOfDaySummary> summaries = new List<BistroBuilderEndOfDaySummary>();

    public BistroBuilderEndOfDayHistorySnapshot DeepClone()
    {
        var clone = new BistroBuilderEndOfDayHistorySnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            lastClosedDayIndex = lastClosedDayIndex,
            hasActiveSession = hasActiveSession,
            activeDayIndex = activeDayIndex,
            activeCalendarYear = activeCalendarYear,
            activeCalendarMonth = activeCalendarMonth,
            activeCalendarDay = activeCalendarDay,
            openingConsumedCanonicalMilliUnits = openingConsumedCanonicalMilliUnits,
            openingWasteCanonicalMilliUnits = openingWasteCanonicalMilliUnits,
            openingReputationBasisPoints = openingReputationBasisPoints,
            activeSatisfactionTotalBasisPoints = activeSatisfactionTotalBasisPoints,
            activeSatisfactionCount = activeSatisfactionCount,
            activeAbandonedCount = activeAbandonedCount,
            activeIncidentLineIds = activeIncidentLineIds != null ? new List<string>(activeIncidentLineIds) : new List<string>()
        };
        if (summaries != null)
            for (int i = 0; i < summaries.Count; i++)
                clone.summaries.Add(summaries[i]?.DeepClone());
        return clone;
    }
}

public static class BistroBuilderEndOfDayEngine
{
    public static int ComputePerformanceBasisPoints(
        long operatingResultCents,
        long revenueCents,
        int satisfactionBasisPoints,
        int abandonedGroups,
        int incidentCount)
    {
        int margin = revenueCents > 0
            ? (int)Math.Max(-10000L, Math.Min(10000L,
                operatingResultCents * 10000L / Math.Max(1L, revenueCents)))
            : (operatingResultCents >= 0 ? 0 : -5000);
        int score = 5200 + margin / 5 + (satisfactionBasisPoints - 5000) / 2;
        score -= Math.Min(2500, Math.Max(0, abandonedGroups) * 450);
        score -= Math.Min(1800, Math.Max(0, incidentCount) * 220);
        return Math.Max(0, Math.Min(10000, score));
    }

    public static void BuildGuidance(BistroBuilderEndOfDaySummary summary)
    {
        if (summary == null) return;
        summary.highlights.Clear();
        summary.problems.Clear();
        summary.opportunities.Clear();
        summary.recommendations.Clear();

        if (summary.operatingResultCents > 0)
            summary.highlights.Add("La jornada terminó con resultado operativo positivo.");
        if (summary.averageSatisfactionBasisPoints >= 8000)
            summary.highlights.Add("La satisfacción de los clientes fue especialmente alta.");
        if (summary.reputationDeltaBasisPoints > 0)
            summary.highlights.Add("La reputación mejoró durante el servicio.");
        if (summary.abandonedGroupCount == 0 && summary.servedGroupCount > 0)
            summary.highlights.Add("No se perdieron grupos por espera excesiva.");

        if (summary.operatingResultCents < 0)
            summary.problems.Add("Los gastos del día superaron el margen generado.");
        if (summary.averageSatisfactionBasisPoints > 0 && summary.averageSatisfactionBasisPoints < 6500)
            summary.problems.Add("La satisfacción quedó por debajo del nivel recomendable.");
        if (summary.abandonedGroupCount > 0)
            summary.problems.Add("Hubo clientes que abandonaron por presión de sala o espera.");
        if (summary.incidentCount > 0)
            summary.problems.Add("Se registraron incidencias en comandas durante el servicio.");

        if (summary.revenueCents > 0 && summary.operatingResultCents <= 0)
            summary.opportunities.Add("Revisar costes y gastos puede convertir ventas actuales en beneficio.");
        if (summary.averageSatisfactionBasisPoints >= 8500)
            summary.opportunities.Add("La buena experiencia permite buscar mayor ticket medio sin forzar el servicio.");
        if (summary.hasPreviousDayComparison && summary.revenueDeltaVsPreviousCents > 0)
            summary.opportunities.Add("La tendencia de ventas es favorable frente al día anterior.");

        if (summary.abandonedGroupCount > 0)
            summary.recommendations.Add("Prioriza capacidad de sala y tiempos de espera en el próximo servicio.");
        if (summary.incidentCount > 0)
            summary.recommendations.Add("Revisa las incidencias de cocina/comanda antes de volver a abrir.");
        if (summary.operatingResultCents < 0)
            summary.recommendations.Add("Reduce gasto evitable o ajusta precios/margen antes del siguiente día.");
        if (summary.recommendations.Count == 0)
            summary.recommendations.Add("Mantén el nivel operativo y busca una mejora incremental del rendimiento.");
    }

    public static bool TryValidateSummary(BistroBuilderEndOfDaySummary summary, out string error)
    {
        if (summary == null || summary.schemaVersion != BistroBuilderEndOfDaySummary.CurrentSchemaVersion ||
            summary.dayIndex < 1 || summary.calendarYear < 1 || summary.calendarMonth < 1 ||
            summary.calendarMonth > 12 || summary.calendarDay < 1 || summary.calendarDay > 31 ||
            summary.servedGroupCount < 0 || summary.abandonedGroupCount < 0 ||
            summary.incidentCount < 0 || summary.paidOrderCount < 0 || summary.consumedLineCount < 0 ||
            summary.averageSatisfactionBasisPoints < 0 || summary.averageSatisfactionBasisPoints > 10000 ||
            summary.performanceBasisPoints < 0 || summary.performanceBasisPoints > 10000 ||
            summary.highlights == null || summary.problems == null || summary.opportunities == null ||
            summary.recommendations == null || !DateTime.TryParse(summary.closedUtc, out _))
        {
            error = "El resumen de fin de día contiene datos inválidos.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public static bool TryValidateSnapshot(BistroBuilderEndOfDayHistorySnapshot snapshot, out string error)
    {
        if (snapshot == null || snapshot.schemaId != BistroBuilderEndOfDayHistorySnapshot.CurrentSchemaId ||
            snapshot.schemaVersion != BistroBuilderEndOfDayHistorySnapshot.CurrentSchemaVersion ||
            snapshot.revision < 0 || snapshot.lastClosedDayIndex < 0 || snapshot.summaries == null ||
            snapshot.activeIncidentLineIds == null || snapshot.summaries.Count > 512 ||
            snapshot.activeIncidentLineIds.Count > 512 ||
            (snapshot.hasActiveSession && (snapshot.activeDayIndex < 1 ||
             snapshot.activeCalendarYear < 1 || snapshot.activeCalendarMonth < 1 ||
             snapshot.activeCalendarMonth > 12 || snapshot.activeCalendarDay < 1 ||
             snapshot.activeCalendarDay > 31 || snapshot.activeSatisfactionCount < 0 ||
             snapshot.activeAbandonedCount < 0 || snapshot.openingReputationBasisPoints < 0 ||
             snapshot.openingReputationBasisPoints > 10000)))
        {
            error = "El historial de cierres contiene una cabecera inválida.";
            return false;
        }
        int last = 0;
        var days = new HashSet<int>();
        for (int i = 0; i < snapshot.summaries.Count; i++)
        {
            if (!TryValidateSummary(snapshot.summaries[i], out error) ||
                !days.Add(snapshot.summaries[i].dayIndex)) return false;
            last = Math.Max(last, snapshot.summaries[i].dayIndex);
        }
        if (snapshot.lastClosedDayIndex != last)
        {
            error = "El último día cerrado no coincide con el historial.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}
