using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evalúa viabilidad y calidad espacial sin decidir gameplay ni rutas.
/// Consume Spatial Subjects y proveedores semánticos de BBSIS.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSpatialAssessmentService : MonoBehaviour
{
    [SerializeField, Range(0.01f, 0.5f)] private float blockingPenalty = 0.22f;
    [SerializeField, Range(0.005f, 0.25f)] private float degradationPenalty = 0.04f;

    private BistroBuilderSpatialInteractionService spatialService;
    private readonly List<BistroBuilderSpatialSemanticVolume> semanticVolumes =
        new List<BistroBuilderSpatialSemanticVolume>(128);

    public BistroBuilderSpatialQualityResult LastResult { get; private set; } =
        new BistroBuilderSpatialQualityResult();
    public BistroBuilderSpatialBottleneckLedger LastLedger { get; private set; } =
        new BistroBuilderSpatialBottleneckLedger();

    private void Awake()
    {
        spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
    }

    public bool ValidateConfiguration(out string error)
    {
        if (blockingPenalty <= 0f || degradationPenalty <= 0f)
        {
            error = "Penalizaciones de Spatial Quality inválidas.";
            return false;
        }
        if (spatialService == null)
            spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        if (spatialService == null)
        {
            error = "Spatial Assessment necesita la autoridad BBSIS.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public BistroBuilderSpatialQualityResult EvaluateCurrentLayout()
    {
        if (spatialService == null)
            spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        semanticVolumes.Clear();
        CollectSemanticVolumes();
        semanticVolumes.Sort(CompareSemanticVolumes);

        var result = new BistroBuilderSpatialQualityResult();
        var ledger = new BistroBuilderSpatialBottleneckLedger
        {
            topologyRevision = spatialService != null ? spatialService.Revision : 0
        };

        EvaluateAgainstStaticGeometry(result, ledger);
        EvaluateSemanticPairs(result, ledger);
        result.viable = !HasBlocking(result);
        float penalty = CalculatePenalty(result);
        result.quality = Mathf.Clamp01(1f - penalty);
        ledger.flowQuality = result.quality;
        ledger.records.Sort(CompareBottlenecks);
        LastResult = result;
        LastLedger = ledger;
        return result;
    }

    private void CollectSemanticVolumes()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IBistroBuilderSpatialSemanticProvider provider)
                provider.WriteSemanticVolumes(semanticVolumes);
        }
    }

    private void EvaluateAgainstStaticGeometry(
        BistroBuilderSpatialQualityResult result,
        BistroBuilderSpatialBottleneckLedger ledger)
    {
        if (spatialService == null) return;
        for (int i = 0; i < semanticVolumes.Count; i++)
        {
            BistroBuilderSpatialSemanticVolume semantic = semanticVolumes[i];
            if (semantic == null ||
                !spatialService.TryFindStaticGeometryConflict(
                    semantic.volume,
                    semantic.subjectId,
                    semantic.relatedSubjectId,
                    out string blocker))
                continue;

            bool blocking = semantic.critical &&
                            semantic.role != BistroBuilderSpatialSemanticRole.Approach;
            string diagnosticId = "static:" + semantic.subjectId + ":" +
                                  semantic.semanticId + ":" + blocker;
            AddFinding(
                result,
                ledger,
                diagnosticId,
                semantic.subjectId,
                blocker,
                semantic.role,
                blocking,
                blocking ? 1f : 0.35f,
                semantic.semanticId + " intersecta geometría estática de " + blocker + ".");
        }
    }

    private void EvaluateSemanticPairs(
        BistroBuilderSpatialQualityResult result,
        BistroBuilderSpatialBottleneckLedger ledger)
    {
        for (int firstIndex = 0; firstIndex < semanticVolumes.Count; firstIndex++)
        {
            BistroBuilderSpatialSemanticVolume first = semanticVolumes[firstIndex];
            if (first == null) continue;
            for (int secondIndex = firstIndex + 1;
                 secondIndex < semanticVolumes.Count;
                 secondIndex++)
            {
                BistroBuilderSpatialSemanticVolume second = semanticVolumes[secondIndex];
                if (second == null ||
                    string.Equals(first.subjectId, second.subjectId, StringComparison.Ordinal) ||
                    AreRelated(first, second) ||
                    !first.volume.Overlaps(second.volume))
                    continue;

                if (!TryClassifyPair(first, second, out bool blocking, out float severity))
                    continue;
                string id = "pair:" + first.subjectId + ":" + first.semanticId + ":" +
                            second.subjectId + ":" + second.semanticId;
                AddFinding(
                    result,
                    ledger,
                    id,
                    first.subjectId,
                    second.subjectId,
                    first.role,
                    blocking,
                    severity,
                    first.semanticId + " solapa " + second.semanticId + ".");
            }
        }
    }

    private static bool AreRelated(
        BistroBuilderSpatialSemanticVolume first,
        BistroBuilderSpatialSemanticVolume second)
    {
        return (!string.IsNullOrWhiteSpace(first.relatedSubjectId) &&
                string.Equals(first.relatedSubjectId, second.subjectId, StringComparison.Ordinal)) ||
               (!string.IsNullOrWhiteSpace(second.relatedSubjectId) &&
                string.Equals(second.relatedSubjectId, first.subjectId, StringComparison.Ordinal));
    }

    public static bool TryClassifyPair(
        BistroBuilderSpatialSemanticVolume first,
        BistroBuilderSpatialSemanticVolume second,
        out bool blocking,
        out float severity)
    {
        blocking = false;
        severity = 0f;
        if (first.role == BistroBuilderSpatialSemanticRole.SeatBay &&
            second.role == BistroBuilderSpatialSemanticRole.SeatBay)
        {
            blocking = true;
            severity = 0.9f;
            return true;
        }

        bool firstMobile =
            first.role == BistroBuilderSpatialSemanticRole.MobilityEnvelope ||
            first.role == BistroBuilderSpatialSemanticRole.CarryEnvelope;
        bool secondMobile =
            second.role == BistroBuilderSpatialSemanticRole.MobilityEnvelope ||
            second.role == BistroBuilderSpatialSemanticRole.CarryEnvelope;
        if (firstMobile || secondMobile)
        {
            BistroBuilderSpatialSemanticVolume other =
                firstMobile ? second : first;
            blocking = other.role !=
                BistroBuilderSpatialSemanticRole.Approach;
            severity = blocking ? 0.88f : 0.45f;
            return true;
        }

        if (first.role == BistroBuilderSpatialSemanticRole.DynamicSweep ||
            second.role == BistroBuilderSpatialSemanticRole.DynamicSweep)
        {
            blocking = false;
            severity = 0.45f;
            return true;
        }

        if (first.role == BistroBuilderSpatialSemanticRole.Approach ||
            second.role == BistroBuilderSpatialSemanticRole.Approach)
        {
            blocking = false;
            severity = 0.25f;
            return true;
        }

        bool firstWork =
            first.role == BistroBuilderSpatialSemanticRole.WorkZone;
        bool secondWork =
            second.role == BistroBuilderSpatialSemanticRole.WorkZone;
        bool firstTransfer =
            first.role == BistroBuilderSpatialSemanticRole.TransferZone;
        bool secondTransfer =
            second.role == BistroBuilderSpatialSemanticRole.TransferZone;
        bool firstSeat =
            first.role == BistroBuilderSpatialSemanticRole.SeatBay;
        bool secondSeat =
            second.role == BistroBuilderSpatialSemanticRole.SeatBay;

        if (firstWork && secondWork)
        {
            blocking = true;
            severity = 0.85f;
            return true;
        }
        if ((firstWork && secondTransfer) ||
            (secondWork && firstTransfer))
        {
            blocking = true;
            severity = 0.78f;
            return true;
        }
        if ((firstTransfer && secondSeat) ||
            (secondTransfer && firstSeat))
        {
            blocking = true;
            severity = 0.72f;
            return true;
        }
        if (firstTransfer && secondTransfer)
        {
            blocking = false;
            severity = 0.4f;
            return true;
        }

        return false;
    }

    private static void AddFinding(
        BistroBuilderSpatialQualityResult result,
        BistroBuilderSpatialBottleneckLedger ledger,
        string diagnosticId,
        string subjectId,
        string otherSubjectId,
        BistroBuilderSpatialSemanticRole role,
        bool blocking,
        float severity,
        string evidence)
    {
        result.diagnostics.Add(new BistroBuilderSpatialDiagnostic
        {
            diagnosticId = diagnosticId,
            subjectId = subjectId,
            evidence = evidence,
            blocking = blocking
        });
        ledger.records.Add(new BistroBuilderSpatialBottleneckRecord
        {
            bottleneckId = diagnosticId,
            subjectId = subjectId,
            otherSubjectId = otherSubjectId,
            role = role,
            severity = Mathf.Clamp01(severity),
            blocking = blocking,
            evidence = evidence
        });
    }

    private bool HasBlocking(BistroBuilderSpatialQualityResult result)
    {
        for (int i = 0; i < result.diagnostics.Count; i++)
            if (result.diagnostics[i] != null && result.diagnostics[i].blocking)
                return true;
        return false;
    }

    private float CalculatePenalty(BistroBuilderSpatialQualityResult result)
    {
        float penalty = 0f;
        for (int i = 0; i < result.diagnostics.Count; i++)
        {
            BistroBuilderSpatialDiagnostic diagnostic = result.diagnostics[i];
            if (diagnostic == null) continue;
            penalty += diagnostic.blocking ? blockingPenalty : degradationPenalty;
        }
        return penalty;
    }

    private static int CompareSemanticVolumes(
        BistroBuilderSpatialSemanticVolume first,
        BistroBuilderSpatialSemanticVolume second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return 1;
        if (second == null) return -1;
        int bySubject = string.CompareOrdinal(first.subjectId, second.subjectId);
        if (bySubject != 0) return bySubject;
        int bySemantic = string.CompareOrdinal(first.semanticId, second.semanticId);
        if (bySemantic != 0) return bySemantic;
        return first.role.CompareTo(second.role);
    }

    private static int CompareBottlenecks(
        BistroBuilderSpatialBottleneckRecord first,
        BistroBuilderSpatialBottleneckRecord second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return 1;
        if (second == null) return -1;
        int byBlocking = second.blocking.CompareTo(first.blocking);
        if (byBlocking != 0) return byBlocking;
        int bySeverity = second.severity.CompareTo(first.severity);
        if (bySeverity != 0) return bySeverity;
        return string.CompareOrdinal(first.bottleneckId, second.bottleneckId);
    }
}
