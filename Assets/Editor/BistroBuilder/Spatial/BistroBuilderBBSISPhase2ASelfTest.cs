using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderBBSISPhase2ASelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 2A/Autotest")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 2A - AUTOTEST");

        BistroBuilderSpatialInteractionService service =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        BistroBuilderSpatialAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialAssessmentService>();
        Check(service != null, "Servicio BBSIS disponible", report);
        Check(assessment != null, "Spatial Assessment disponible", report);

        int leasesBefore = service != null ? service.ActiveLeaseCount : -1;
        GameObject blocker = null;
        BistroBuilderSpatialContractDefinition syntheticContract = null;
        try
        {
            blocker = new GameObject("BBSIS_2A_SyntheticStatic");
            blocker.transform.position = new Vector3(500f, 0f, 500f);
            BistroBuilderAdaptiveSpatialProxy proxy =
                blocker.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Simple);
            proxy.AddPart(new BistroBuilderSpatialProxyPart
            {
                partId = "body",
                layer = BistroBuilderSpatialProxyLayer.Static,
                shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
                size = new Vector2(2f, 2f)
            });
            syntheticContract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
            syntheticContract.ConfigureForEditor(
                "test.spatial.static", "generic",
                BistroBuilderAdaptiveSpatialProxyMode.Simple,
                new[] { "test.static" });
            BistroBuilderSpatialSubject subject =
                blocker.AddComponent<BistroBuilderSpatialSubject>();
            subject.Configure("synthetic.static", syntheticContract, proxy);
            service?.RegisterSubject(subject);

            RunLeaseTests(service, report);
        }
        finally
        {
            if (blocker != null) UnityEngine.Object.DestroyImmediate(blocker);
            if (syntheticContract != null) UnityEngine.Object.DestroyImmediate(syntheticContract);
            service?.RebuildSubjects();
        }

        Check(service == null || service.ActiveLeaseCount == leasesBefore,
            "Pruebas sintéticas no dejan Spatial Leases", report);

        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        Check(seats.Length > 0, "Fixture real contiene sillas", report);
        if (seats.Length > 0)
            RunRealSeatTests(seats[0], report);

        RestaurantTableSeatingConfiguration[] tables =
            UnityEngine.Object.FindObjectsByType<RestaurantTableSeatingConfiguration>(
                FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        Check(tables.Length > 0, "Fixture real contiene mesas", report);
        if (tables.Length > 0)
            RunRealTableTests(tables[0], report);

        if (assessment != null)
        {
            BistroBuilderSpatialQualityResult first = assessment.EvaluateCurrentLayout();
            BistroBuilderSpatialQualityResult second = assessment.EvaluateCurrentLayout();
            Check(first != null && first.quality >= 0f && first.quality <= 1f,
                "Spatial Quality acotada", report);
            Check(first != null && second != null &&
                  first.diagnostics.Count == second.diagnostics.Count &&
                  Mathf.Approximately(first.quality, second.quality),
                "Evaluación repetida estable", report);
            Check(assessment.LastLedger != null,
                "Bottleneck Ledger generado", report);
        }

        Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialRuntimeBinder>() != null,
            "Runtime Binder instalado", report);
        Check(Enum.GetValues(typeof(BistroBuilderSpatialSemanticRole)).Length >= 8,
            "Roles semánticos espaciales completos", report);

        report.AppendLine("Resultado: " + LastPassed + " OK / " + LastFailed + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (LastFailed > 0)
            throw new InvalidOperationException("Autotest BBSIS Fase 2A fallido.");
    }

    private static void RunLeaseTests(
        BistroBuilderSpatialInteractionService service,
        StringBuilder report)
    {
        if (service == null)
        {
            Check(false, "Preflight estático bloquea sweep imposible", report);
            return;
        }

        BistroBuilderSpatialVolume overlap = BistroBuilderSpatialVolume.Box(
            new Vector3(500f, 0f, 500f),
            Vector3.right,
            Vector3.forward,
            Vector2.one);
        var blockedRequest = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "test.sweep.blocked",
            kind = BistroBuilderSpatialClaimKind.DynamicSweep,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = overlap,
            durationSeconds = 1f,
            validateAgainstStaticGeometry = true
        };

        bool blocked = service.TryAcquireLease(
            blockedRequest,
            out _,
            out BistroBuilderSpatialLeaseDecision blockedDecision);
        Check(!blocked &&
              blockedDecision.failure == BistroBuilderSpatialLeaseFailure.StaticGeometryConflict &&
              blockedDecision.blockingSubjectId == "synthetic.static",
            "Preflight estático bloquea sweep imposible", report);

        blockedRequest.ownerId = "test.sweep.related";
        blockedRequest.relatedSubjectId = "synthetic.static";
        bool relatedAllowed = service.TryAcquireLease(
            blockedRequest,
            out BistroBuilderSpatialLease relatedLease,
            out _);
        Check(relatedAllowed,
            "Relación espacial explícita evita falso positivo estático", report);
        if (relatedAllowed) service.ReleaseLease(relatedLease.leaseId);

        blockedRequest.ownerId = "test.sweep.self";
        blockedRequest.subjectId = "synthetic.static";
        blockedRequest.relatedSubjectId = string.Empty;
        bool selfAllowed = service.TryAcquireLease(
            blockedRequest,
            out BistroBuilderSpatialLease selfLease,
            out _);
        Check(selfAllowed,
            "Subject no colisiona contra su propia geometría", report);
        if (selfAllowed) service.ReleaseLease(selfLease.leaseId);

        BistroBuilderSpatialVolume free = BistroBuilderSpatialVolume.Circle(
            new Vector3(510f, 0f, 510f), 0.4f);
        var firstRequest = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "test.dynamic.first",
            kind = BistroBuilderSpatialClaimKind.DynamicSweep,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = free,
            durationSeconds = 1f,
            validateAgainstStaticGeometry = true
        };
        bool firstGranted = service.TryAcquireLease(
            firstRequest,
            out BistroBuilderSpatialLease firstLease,
            out _);
        Check(firstGranted, "Sweep libre obtiene Spatial Lease", report);

        var secondRequest = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "test.dynamic.second",
            kind = BistroBuilderSpatialClaimKind.DynamicSweep,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = free,
            durationSeconds = 1f,
            validateAgainstStaticGeometry = true
        };
        bool secondGranted = service.TryAcquireLease(
            secondRequest,
            out _,
            out BistroBuilderSpatialLeaseDecision secondDecision);
        Check(!secondGranted && secondDecision.failure == BistroBuilderSpatialLeaseFailure.Conflict,
            "Sweeps incompatibles no coexisten", report);
        if (firstGranted) service.ReleaseLease(firstLease.leaseId);

        int beforeStress = service.ActiveLeaseCount;
        bool allDenied = true;
        for (int i = 0; i < 200; i++)
        {
            blockedRequest.ownerId = "stress.blocked." + i;
            blockedRequest.subjectId = string.Empty;
            blockedRequest.relatedSubjectId = string.Empty;
            if (service.TryAcquireLease(blockedRequest, out _, out _))
            {
                allDenied = false;
                break;
            }
        }
        Check(allDenied && service.ActiveLeaseCount == beforeStress,
            "Stress 200 preflights bloqueados sin fugas", report);
    }

    private static void RunRealSeatTests(RestaurantSeat seat, StringBuilder report)
    {
        BistroBuilderSpatialSubject subject = seat.GetComponent<BistroBuilderSpatialSubject>();
        BistroBuilderSeatSpatialAdapter adapter = seat.GetComponent<BistroBuilderSeatSpatialAdapter>();
        Check(subject != null && adapter != null,
            "Silla real expone Subject y Adapter", report);
        if (subject == null || adapter == null) return;

        Check(subject.TryGetPortWorld(
                "seat", out Vector3 seatPosition, out _, out _, out _) &&
              seat.SeatPoint != null &&
              Vector3.Distance(seatPosition, seat.SeatPoint.position) < 0.01f,
            "Seat Port usa el anclaje real", report);
        Check(subject.TryGetPortWorld(
                "approach", out Vector3 approachPosition, out _, out _, out _) &&
              seat.CustomerApproachPoint != null &&
              Vector3.Distance(approachPosition, seat.CustomerApproachPoint.position) < 0.01f,
            "Approach Port usa el anclaje real", report);

        var semantics = new List<BistroBuilderSpatialSemanticVolume>();
        int count = adapter.WriteSemanticVolumes(semantics);
        bool hasApproach = false;
        bool hasSweep = false;
        for (int i = 0; i < semantics.Count; i++)
        {
            hasApproach |= semantics[i].role == BistroBuilderSpatialSemanticRole.Approach;
            hasSweep |= semantics[i].role == BistroBuilderSpatialSemanticRole.DynamicSweep;
        }
        Check(count >= 2 && hasApproach,
            "Silla real expone clearance de aproximación", report);
        Check(hasSweep,
            "Silla real expone Dynamic Sweep previo al movimiento", report);
        Check(subject.Proxy != null &&
              subject.Proxy.Mode == BistroBuilderAdaptiveSpatialProxyMode.Articulated,
            "Silla real usa Adaptive Spatial Proxy articulado", report);
    }

    private static void RunRealTableTests(
        RestaurantTableSeatingConfiguration table,
        StringBuilder report)
    {
        BistroBuilderSpatialSubject subject = table.GetComponent<BistroBuilderSpatialSubject>();
        BistroBuilderTableSpatialAdapter adapter = table.GetComponent<BistroBuilderTableSpatialAdapter>();
        Check(subject != null && adapter != null,
            "Mesa real expone Subject y Adapter", report);
        if (subject == null || adapter == null) return;

        var semantics = new List<BistroBuilderSpatialSemanticVolume>();
        int count = adapter.WriteSemanticVolumes(semantics);
        Check(count == table.MaximumCustomers,
            "Mesa genera un Seat Bay por plaza real", report);
        bool allSeatBays = true;
        for (int i = 0; i < semantics.Count; i++)
            allSeatBays &= semantics[i].role == BistroBuilderSpatialSemanticRole.SeatBay;
        Check(allSeatBays,
            "Plazas de mesa se publican como Seat Bays", report);
        Check(subject.Contract != null &&
              subject.Contract.Ports.Count == table.MaximumCustomers,
            "Spatial Contract de mesa conserva sus plazas", report);
        Check(subject.Proxy != null &&
              subject.Proxy.Mode == BistroBuilderAdaptiveSpatialProxyMode.Layered,
            "Mesa real usa proxy Static + Operational", report);
    }

    private static void Check(bool condition, string label, StringBuilder report)
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
}
