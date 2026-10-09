using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum BistroBuilderEditorV2DiagnosisLayer
{
    None = 0,
    Circulation = 1,
    Accessibility = 2,
    Capacity = 4,
    Interaction = 8,
    Layout = 16,
    All = Circulation | Accessibility | Capacity | Interaction | Layout
}

public enum BistroBuilderEditorV2DiagnosisSeverity
{
    Information = 0,
    Warning = 1,
    Blocking = 2
}

/// <summary>Read-only findings. Coordinates refer to live scene objects
/// only when hasWorldPosition is true.</summary>
[Serializable]
public sealed class BistroBuilderEditorV2DiagnosticFinding
{
    public string id;
    public BistroBuilderEditorV2DiagnosisLayer layer;
    public BistroBuilderEditorV2DiagnosisSeverity severity;
    public string targetId;
    public string title;
    public string explanation;
    public string recommendation;
    public bool hasWorldPosition;
    public Vector3 worldPosition;
}

public sealed class BistroBuilderEditorV2DiagnosisReport
{
    public readonly List<BistroBuilderEditorV2DiagnosticFinding> findings =
        new List<BistroBuilderEditorV2DiagnosticFinding>(64);
    public BistroBuilderEditorV2DiagnosisLayer scannedLayers;
    public int scannedObjects;
    public int scannedRoutes;
    public int warnings;
    public int blocking;
    public bool complete = true;
    public int spatialRevision;
    public int navigationRevision;
    public int evaluations;
    public long spatialMilliseconds;
    public long navigationMilliseconds;
    public long placementMilliseconds;
    public long capacityMilliseconds;
    public long architectureMilliseconds;
    public bool navigationPending;
}

/// <summary>Deterministic, duplicate-free projection; no Unity scene writes.</summary>
public static class BistroBuilderEditorV2DiagnosisComposer
{
    public static bool Add(
        BistroBuilderEditorV2DiagnosisReport report,
        BistroBuilderEditorV2DiagnosticFinding finding)
    {
        if (report == null || finding == null ||
            string.IsNullOrWhiteSpace(finding.id) ||
            string.IsNullOrWhiteSpace(finding.title) ||
            (finding.layer & report.scannedLayers) == 0)
            return false;
        for (int i = 0; i < report.findings.Count; i++)
            if (string.Equals(report.findings[i].id, finding.id,
                StringComparison.Ordinal))
                return false;
        report.findings.Add(finding);
        return true;
    }

    public static void Finish(BistroBuilderEditorV2DiagnosisReport report)
    {
        report.findings.Sort((left, right) =>
        {
            int severity = right.severity.CompareTo(left.severity);
            if (severity != 0) return severity;
            int layer = left.layer.CompareTo(right.layer);
            return layer != 0 ? layer :
                string.CompareOrdinal(left.id, right.id);
        });
        report.warnings = 0;
        report.blocking = 0;
        for (int i = 0; i < report.findings.Count; i++)
        {
            if (report.findings[i].severity ==
                BistroBuilderEditorV2DiagnosisSeverity.Blocking)
                report.blocking++;
            else if (report.findings[i].severity ==
                     BistroBuilderEditorV2DiagnosisSeverity.Warning)
                report.warnings++;
        }
    }

    public static BistroBuilderEditorV2DiagnosisLayer ForSpatialRole(
        BistroBuilderSpatialSemanticRole role)
    {
        switch (role)
        {
            case BistroBuilderSpatialSemanticRole.MobilityEnvelope:
            case BistroBuilderSpatialSemanticRole.CarryEnvelope:
            case BistroBuilderSpatialSemanticRole.TraversalGate:
                return BistroBuilderEditorV2DiagnosisLayer.Circulation;
            case BistroBuilderSpatialSemanticRole.SeatBay:
            case BistroBuilderSpatialSemanticRole.Approach:
                return BistroBuilderEditorV2DiagnosisLayer.Accessibility;
            case BistroBuilderSpatialSemanticRole.WorkZone:
            case BistroBuilderSpatialSemanticRole.TransferZone:
            case BistroBuilderSpatialSemanticRole.OperationalClearance:
                return BistroBuilderEditorV2DiagnosisLayer.Interaction;
            default:
                return BistroBuilderEditorV2DiagnosisLayer.Layout;
        }
    }

    public static bool AddSpatialBottleneck(
        BistroBuilderEditorV2DiagnosisReport report,
        BistroBuilderSpatialBottleneckRecord record,
        bool hasPosition, Vector3 position)
    {
        if (record == null) return false;
        var layer = ForSpatialRole(record.role);
        return Add(report, new BistroBuilderEditorV2DiagnosticFinding
        {
            id = "bbsis:" + record.bottleneckId,
            targetId = record.subjectId,
            layer = layer,
            severity = record.blocking
                ? BistroBuilderEditorV2DiagnosisSeverity.Blocking
                : BistroBuilderEditorV2DiagnosisSeverity.Warning,
            title = record.blocking ? "Espacio funcional bloqueado" :
                "Posible estrechamiento",
            explanation = record.evidence,
            recommendation = RecommendationForSpatialRole(record.role),
            hasWorldPosition = hasPosition,
            worldPosition = position
        });
    }

    public static bool AddNavigationIssue(
        BistroBuilderEditorV2DiagnosisReport report,
        BistroBuilderCirculationIssue issue)
    {
        if (issue == null) return false;
        var severity = issue.severity == BistroBuilderCirculationIssueSeverity.Blocking
            ? BistroBuilderEditorV2DiagnosisSeverity.Blocking :
            issue.severity == BistroBuilderCirculationIssueSeverity.Warning
                ? BistroBuilderEditorV2DiagnosisSeverity.Warning :
                  BistroBuilderEditorV2DiagnosisSeverity.Information;
        return Add(report, new BistroBuilderEditorV2DiagnosticFinding
        {
            id = "nav:" + issue.issueId,
            layer = BistroBuilderEditorV2DiagnosisLayer.Circulation,
            targetId = issue.sourceName,
            severity = severity,
            title = "Problema de circulación: " + issue.sourceName,
            explanation = issue.message,
            recommendation =
                "Revisa el pasillo y deja un recorrido transitable entre estos puntos."
        });
    }

    public static void AddSeatingIssues(
        BistroBuilderEditorV2DiagnosisReport report,
        int seatCount, int unassociatedCount)
    {
        if (unassociatedCount > 0)
            Add(report, new BistroBuilderEditorV2DiagnosticFinding
            {
                id = "capacity:unassociated-seats",
                layer = BistroBuilderEditorV2DiagnosisLayer.Capacity,
                severity = BistroBuilderEditorV2DiagnosisSeverity.Warning,
                targetId = "SeatingTopology",
                title = unassociatedCount + " silla(s) sin mesa válida",
                explanation =
                    "Estas sillas no contribuyen a las plazas asociadas del comedor.",
                recommendation =
                    "Acerca y orienta cada silla hacia una plaza compatible de una mesa."
            });
        if (seatCount == 0)
            Add(report, new BistroBuilderEditorV2DiagnosticFinding
            {
                id = "capacity:no-seats",
                layer = BistroBuilderEditorV2DiagnosisLayer.Capacity,
                severity = BistroBuilderEditorV2DiagnosisSeverity.Warning,
                title = "El comedor no tiene sillas operativas.",
                explanation = "No se detectaron asientos registrados.",
                recommendation = "Coloca sillas y asígnalas a mesas."
            });
    }

    public static string RecommendationForSpatialRole(
        BistroBuilderSpatialSemanticRole role)
    {
        switch (ForSpatialRole(role))
        {
            case BistroBuilderEditorV2DiagnosisLayer.Circulation:
                return "Despeja este paso o separa los muebles que lo invaden.";
            case BistroBuilderEditorV2DiagnosisLayer.Accessibility:
                return "Reubica la silla o la mesa para dejar libre su zona de acceso.";
            case BistroBuilderEditorV2DiagnosisLayer.Interaction:
                return "Deja espacio de trabajo y acceso alrededor de este equipo.";
            default:
                return "Revisa el tamaño, el solapamiento y la orientación de los elementos.";
        }
    }
}
