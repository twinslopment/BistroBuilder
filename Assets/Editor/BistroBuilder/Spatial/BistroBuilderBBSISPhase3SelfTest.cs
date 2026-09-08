using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stress determinista de leases, reset, subjects y diagnósticos BBSIS.
/// </summary>
public static class BistroBuilderBBSISPhase3SelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 3/Autotest")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 3 - AUTOTEST");

        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderSpatialAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialAssessmentService>();
        Check(spatial != null, "Autoridad BBSIS disponible", report);
        Check(assessment != null, "Assessment disponible", report);
        if (spatial == null || assessment == null)
        {
            Finish(report);
            return;
        }

        spatial.ResetTransientRuntimeStateAfterLoad();
        int baselineSubjects = spatial.SubjectCount;
        long grantedBefore = spatial.GrantedLeaseCount;
        long rejectedBefore = spatial.RejectedLeaseCount;
        List<string> released = new List<string>(512);
        Vector3 origin = new Vector3(20000f, 0f, 20000f);
        spatial.LeaseReleased += released.Add;

        BistroBuilderSpatialLease first = Acquire(
            spatial,
            "phase3.blocker.first",
            origin + Vector3.left * 0.35f,
            0.2f,
            report);
        BistroBuilderSpatialLease second = Acquire(
            spatial,
            "phase3.blocker.second",
            origin + Vector3.right * 0.35f,
            0.2f,
            report);
        Check(first != null && second != null,
            "Dos blockers no solapados concedidos", report);

        var probe = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "phase3.probe",
            kind = BistroBuilderSpatialClaimKind.Mobility,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Circle(origin, 0.25f),
            priority = 90,
            durationSeconds = 10f
        };
        bool probeGranted = spatial.TryAcquireLease(
            probe,
            out _,
            out BistroBuilderSpatialLeaseDecision probeDecision);
        Check(!probeGranted && first != null &&
              string.Equals(
                  probeDecision.blockingLeaseId,
                  first.leaseId,
                  StringComparison.Ordinal),
            "Conflicto multiple elige blocker estable por secuencia", report);
        Check(spatial.RejectedLeaseCount > rejectedBefore,
            "Rechazos quedan contabilizados", report);

        BistroBuilderSpatialEpisode episodeA;
        BistroBuilderSpatialEpisode episodeB;
        bool episodeAStarted = spatial.TryBeginEpisode(
            "phase3.episode.owner.a",
            "phase3.reset.a",
            string.Empty,
            out episodeA);
        bool episodeBStarted = spatial.TryBeginEpisode(
            "phase3.episode.owner.b",
            "phase3.reset.b",
            string.Empty,
            out episodeB);
        Check(episodeAStarted && episodeBStarted,
            "Episodios de reset creados", report);        int leasesBeforeReset = spatial.ActiveLeaseCount;
        released.Clear();
        spatial.ResetTransientRuntimeStateAfterLoad();
        Check(spatial.ActiveLeaseCount == 0 &&
              released.Count == leasesBeforeReset,
            "Reset libera leases mediante eventos", report);
        Check(IsSorted(released),
            "Reset libera leases en orden determinista", report);
        Check(spatial.ActiveEpisodeCount == 0 &&
              episodeA.state == BistroBuilderSpatialEpisodeState.Cancelled &&
              episodeB.state == BistroBuilderSpatialEpisodeState.Cancelled,
            "Reset cancela episodios de forma explicita", report);

        BistroBuilderSpatialContractDefinition contract =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderSpatialContractDefinition>(
                "Assets/Resources/BistroBuilder/Spatial/Contracts/" +
                "BB_SpatialContract_Logistics_Cart.asset");
        Check(contract != null,
            "Contrato para prueba de orphan cargado", report);
        GameObject orphanObject = new GameObject("__BBSIS_Phase3_Orphan");
        BistroBuilderAdaptiveSpatialProxy orphanProxy =
            orphanObject.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
        BistroBuilderSpatialSubject orphanSubject =
            orphanObject.AddComponent<BistroBuilderSpatialSubject>();
        orphanSubject.Configure(
            "spatial.phase3.orphan",
            contract,
            orphanProxy);
        spatial.RegisterSubject(orphanSubject);
        var orphanRequest = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "phase3.orphan.owner",
            subjectId = orphanSubject.SubjectId,
            kind = BistroBuilderSpatialClaimKind.Mobility,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Circle(
                new Vector3(22000f, 0f, 22000f), 0.2f),
            durationSeconds = 20f
        };
        Check(spatial.TryAcquireLease(
                orphanRequest,
                out _,
                out _),
            "Lease ligado a subject temporal concedido", report);
        UnityEngine.Object.DestroyImmediate(orphanObject);
        spatial.RebuildSubjects();
        Check(!spatial.TryGetSubject(
                "spatial.phase3.orphan",
                out _) &&
              spatial.ActiveLeaseCount == 0,
            "Rebuild purga leases de subjects desaparecidos", report);

        Acquire(
            spatial,
            "phase3.owner.bulk",
            new Vector3(23000f, 0f, 23000f),
            0.2f,
            report);
        Acquire(
            spatial,
            "phase3.owner.bulk",
            new Vector3(23001f, 0f, 23000f),
            0.2f,
            report);
        released.Clear();
        int bulkReleased = spatial.ReleaseOwnerLeases("phase3.owner.bulk");
        Check(bulkReleased == 2 && spatial.ActiveLeaseCount == 0,
            "Liberacion masiva por owner completa", report);
        Check(IsSorted(released),
            "Liberacion masiva por owner es determinista", report);
        const int gridSide = 16;
        const int denseCount = gridSide * gridSide;
        List<string> denseLeases = new List<string>(denseCount);
        Vector3 denseOrigin = new Vector3(24000f, 0f, 24000f);
        bool denseGranted = true;
        for (int z = 0; z < gridSide && denseGranted; z++)
            for (int x = 0; x < gridSide; x++)
            {
                var request = new BistroBuilderSpatialClaimRequest
                {
                    ownerId = "phase3.dense." + (z * gridSide + x),
                    kind = BistroBuilderSpatialClaimKind.Mobility,
                    conflictMode = BistroBuilderSpatialConflictMode.Block,
                    volume = BistroBuilderSpatialVolume.Circle(
                        denseOrigin + new Vector3(x, 0f, z),
                        0.2f),
                    durationSeconds = 30f
                };
                if (!spatial.TryAcquireLease(
                        request,
                        out BistroBuilderSpatialLease lease,
                        out _))
                {
                    denseGranted = false;
                    break;
                }
                denseLeases.Add(lease.leaseId);
            }
        Check(denseGranted && denseLeases.Count == denseCount &&
              spatial.ActiveLeaseCount == denseCount,
            "Stress denso 256 leases concedidos sin fugas", report);

        bool denseRejected = true;
        for (int i = 0; i < denseCount; i++)
        {
            int x = i % gridSide;
            int z = i / gridSide;
            var request = new BistroBuilderSpatialClaimRequest
            {
                ownerId = "phase3.dense.probe." + i,
                kind = BistroBuilderSpatialClaimKind.Mobility,
                conflictMode = BistroBuilderSpatialConflictMode.Block,
                volume = BistroBuilderSpatialVolume.Circle(
                    denseOrigin + new Vector3(x, 0f, z),
                    0.2f),
                durationSeconds = 1f
            };
            if (spatial.TryAcquireLease(request, out _, out _))
            {
                denseRejected = false;
                break;
            }
        }
        Check(denseRejected,
            "Stress denso 256 conflictos rechazados consistentemente", report);
        for (int i = 0; i < denseLeases.Count; i++)
            spatial.ReleaseLease(denseLeases[i]);
        Check(spatial.ActiveLeaseCount == 0,
            "Stress denso libera el 100% de leases", report);
        Check(spatial.GrantedLeaseCount >= grantedBefore + denseCount + 3,
            "Telemetria registra carga densa", report);
        Check(spatial.RejectedLeaseCount >= rejectedBefore + denseCount + 1,
            "Telemetria registra contention densa", report);

        BistroBuilderSpatialQualityResult qualityA =
            assessment.EvaluateCurrentLayout();
        List<string> ledgerA = SnapshotLedger(assessment.LastLedger);
        BistroBuilderSpatialQualityResult qualityB =
            assessment.EvaluateCurrentLayout();
        List<string> ledgerB = SnapshotLedger(assessment.LastLedger);
        Check(qualityA != null && qualityB != null &&
              Mathf.Approximately(qualityA.quality, qualityB.quality),
            "Spatial Quality repetible", report);
        Check(EqualStrings(ledgerA, ledgerB),
            "Bottleneck Ledger estable entre evaluaciones", report);
        Check(spatial.SubjectCount == baselineSubjects,
            "Stress no altera subjects persistentes", report);

        spatial.LeaseReleased -= released.Add;
        Finish(report);
    }
    private static BistroBuilderSpatialLease Acquire(
        BistroBuilderSpatialInteractionService spatial,
        string ownerId,
        Vector3 point,
        float radius,
        StringBuilder report)
    {
        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = ownerId,
            kind = BistroBuilderSpatialClaimKind.Mobility,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Circle(point, radius),
            durationSeconds = 20f
        };
        bool ok = spatial.TryAcquireLease(
            request,
            out BistroBuilderSpatialLease lease,
            out _);
        Check(ok, "Lease semilla " + ownerId, report);
        return ok ? lease : null;
    }

    private static bool IsSorted(List<string> values)
    {
        for (int i = 1; i < values.Count; i++)
            if (string.CompareOrdinal(values[i - 1], values[i]) > 0)
                return false;
        return true;
    }
    private static List<string> SnapshotLedger(
        BistroBuilderSpatialBottleneckLedger ledger)
    {
        List<string> values = new List<string>();
        if (ledger == null || ledger.records == null)
            return values;
        for (int i = 0; i < ledger.records.Count; i++)
        {
            BistroBuilderSpatialBottleneckRecord record = ledger.records[i];
            if (record != null)
                values.Add(record.bottleneckId);
        }
        return values;
    }

    private static bool EqualStrings(
        List<string> first,
        List<string> second)
    {
        if (first == null || second == null || first.Count != second.Count)
            return false;
        for (int i = 0; i < first.Count; i++)
            if (!string.Equals(
                    first[i], second[i], StringComparison.Ordinal))
                return false;
        return true;
    }
    private static void Check(
        bool condition,
        string label,
        StringBuilder report)
    {
        if (condition)
        {
            LastPassed++;
            report.AppendLine("OK - " + label);
        }
        else
        {
            LastFailed++;
            report.AppendLine("FAIL - " + label);
        }
    }

    private static void Finish(StringBuilder report)
    {
        report.AppendLine(
            "Resultado: " + LastPassed + " OK / " +
            LastFailed + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (LastFailed > 0)
            throw new InvalidOperationException(
                "Autotest BBSIS Fase 3 fallido.");
    }
}
