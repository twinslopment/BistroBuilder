using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BistroBuilderBBSISPhase1SelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 1/Ejecutar autotests")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
        int ok = 0;
        int fail = 0;
        var report = new StringBuilder("BBSIS FASE 1 - AUTOTEST\n");
        void Check(bool condition, string label)
        {
            if (condition) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        BistroBuilderSpatialInteractionService service =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        Check(service != null, "Servicio BBSIS instalado");
        Check(service != null && service.ValidateConfiguration(out _),
            "Servicio BBSIS configurado");
        if (service == null)
            throw new InvalidOperationException("No existe BBSIS para ejecutar autotests.");

        service.ResetTransientRuntimeStateAfterLoad();
        TestVolumes(Check);
        TestLeases(service, Check);
        TestEpisodes(service, Check);
        TestDeterminism(service, Check);
        TestProxies(Check);
        TestStress(service, Check);

        BistroBuilderSpatialRuntimeSnapshot snapshot = service.CaptureRuntimeSnapshot();
        Check(snapshot != null && snapshot.schemaId == "spatial.runtime" &&
              snapshot.schemaVersion == 1,
            "Snapshot runtime conserva solo esquema/revisión reconstruible");
        service.ResetTransientRuntimeStateAfterLoad();
        Check(service.ActiveLeaseCount == 0 && service.ActiveEpisodeCount == 0,
            "Load descarta estado espacial transitorio");

        LastPassed = ok;
        LastFailed = fail;
        report.AppendLine("Resultado: " + ok + " OK / " + fail + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (fail > 0) throw new InvalidOperationException(LastReport);
    }

    private static void TestVolumes(Action<bool, string> check)
    {
        BistroBuilderSpatialVolume box = BistroBuilderSpatialVolume.Box(
            Vector3.zero, Vector3.right, Vector3.forward, new Vector2(1f, 0.5f));
        BistroBuilderSpatialVolume circleNear =
            BistroBuilderSpatialVolume.Circle(new Vector3(0.9f, 0f, 0f), 0.25f);
        BistroBuilderSpatialVolume circleFar =
            BistroBuilderSpatialVolume.Circle(new Vector3(2f, 0f, 0f), 0.25f);
        check(box.ContainsPoint(new Vector3(0.5f, 0f, 0.2f)),
            "OBB contiene puntos reales en ejes orientados");
        check(box.Overlaps(circleNear) && circleNear.Overlaps(box),
            "Solape Circle/OBB es simétrico");
        check(!box.Overlaps(circleFar),
            "Volúmenes separados no producen falso conflicto");
    }

    private static void TestLeases(
        BistroBuilderSpatialInteractionService service,
        Action<bool, string> check)
    {
        var block = Request("lease:a", new Vector3(100f, 0f, 100f),
            BistroBuilderSpatialConflictMode.Block);
        bool first = service.TryAcquireLease(block, out BistroBuilderSpatialLease leaseA, out _);
        bool second = service.TryAcquireLease(
            Request("lease:b", new Vector3(100f, 0f, 100f),
                BistroBuilderSpatialConflictMode.Reservable),
            out _, out BistroBuilderSpatialLeaseDecision denied);

        check(first && leaseA != null, "Primer Spatial Lease incompatible se concede");
        check(!second && denied.failure == BistroBuilderSpatialLeaseFailure.Conflict,
            "Leases incompatibles nunca coexisten");
        bool compatible = service.TryAcquireLease(
            Request("lease:c", new Vector3(100f, 0f, 100f),
                BistroBuilderSpatialConflictMode.Compatible),
            out BistroBuilderSpatialLease leaseC, out _);
        check(compatible && leaseC != null,
            "Claims COMPATIBLE pueden compartir espacio por contrato");
        check(service.ReleaseLease(leaseA.leaseId) && service.ReleaseLease(leaseC.leaseId),
            "Spatial Leases se liberan explícitamente");
    }

    private static void TestEpisodes(
        BistroBuilderSpatialInteractionService service,
        Action<bool, string> check)
    {
        bool began = service.TryBeginEpisode(
            "episode:owner", "service.test", string.Empty,
            out BistroBuilderSpatialEpisode episode);
        check(began && episode != null &&
              episode.state == BistroBuilderSpatialEpisodeState.Active,
            "Spatial Episode inicia con identidad estable");

        BistroBuilderSpatialClaimRequest request = Request(
            "episode:owner", new Vector3(110f, 0f, 110f),
            BistroBuilderSpatialConflictMode.Reservable);
        request.episodeId = episode != null ? episode.episodeId : string.Empty;
        bool leased = service.TryAcquireLease(request, out _, out _);
        check(leased, "Spatial Episode puede poseer Claims/Leases");
        bool ended = episode != null && service.EndEpisode(
            episode.episodeId, BistroBuilderSpatialEpisodeState.Completed);
        check(ended && service.ActiveEpisodeCount == 0,
            "Cerrar Spatial Episode termina su ciclo de vida");
        check(service.ActiveLeaseCount == 0,
            "Cerrar Spatial Episode libera sus Leases");
    }

    private static void TestDeterminism(
        BistroBuilderSpatialInteractionService service,
        Action<bool, string> check)
    {
        Vector3 preferred = new Vector3(120f, 0f, 120f);
        bool a = service.TryReservePointWithAlternates(
            "det:a", BistroBuilderSpatialClaimKind.Destination,
            preferred, 0.3f, 40, 10f, null,
            out Vector3 reservedA, out _);
        bool b = service.TryReservePointWithAlternates(
            "det:b", BistroBuilderSpatialClaimKind.Destination,
            preferred, 0.3f, 40, 10f, null,
            out Vector3 firstAlternative, out _);

        service.ReleaseOwnerLeases("det:b", BistroBuilderSpatialClaimKind.Destination);
        bool bAgain = service.TryReservePointWithAlternates(
            "det:b", BistroBuilderSpatialClaimKind.Destination,
            preferred, 0.3f, 40, 10f, null,
            out Vector3 secondAlternative, out _);
        check(a && b && bAgain && reservedA == preferred,
            "Reserva preferida y alternativa se resuelven de forma estable");
        check((firstAlternative - secondAlternative).sqrMagnitude < 0.000001f,
            "Selección de alternativa es determinista");
        check((reservedA - firstAlternative).sqrMagnitude > 0.01f,
            "Dos Claims reservables no ocupan el mismo destino");
        service.ReleaseOwnerLeases("det:a", BistroBuilderSpatialClaimKind.Destination);
        service.ReleaseOwnerLeases("det:b", BistroBuilderSpatialClaimKind.Destination);
    }

    private static void TestProxies(Action<bool, string> check)
    {
        GameObject root = new GameObject("__BBSIS_Proxy_Test__");
        GameObject articulation = new GameObject("Joint");
        articulation.transform.SetParent(root.transform, false);
        try
        {
            BistroBuilderAdaptiveSpatialProxy proxy =
                root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.ConfigureForEditor(BistroBuilderAdaptiveSpatialProxyMode.Simple);
            proxy.ClearPartsForEditor();
            proxy.AddPartForEditor(new BistroBuilderSpatialProxyPart
            {
                partId = "simple",
                layer = BistroBuilderSpatialProxyLayer.Static,
                shapeKind = BistroBuilderSpatialShapeKind.ConvexHull,
                convexHull = new System.Collections.Generic.List<Vector2>
                {
                    new Vector2(-0.6f, -0.3f),
                    new Vector2(0.5f, -0.4f),
                    new Vector2(0.7f, 0.2f),
                    new Vector2(-0.4f, 0.5f)
                }
            });
            check(proxy.ValidateProxy(out _) &&
                  proxy.ContainsPoint(Vector3.zero, BistroBuilderSpatialProxyLayer.Static),
                "Proxy Simple admite contorno convexo realista");

            proxy.ConfigureForEditor(BistroBuilderAdaptiveSpatialProxyMode.Compound);
            proxy.AddPartForEditor(new BistroBuilderSpatialProxyPart
            {
                partId = "compound.2",
                layer = BistroBuilderSpatialProxyLayer.Static,
                shapeKind = BistroBuilderSpatialShapeKind.Circle,
                localCenter = Vector3.right,
                radius = 0.25f
            });
            check(proxy.ValidateProxy(out _),
                "Proxy Compound combina varias geometrías espaciales");

            proxy.ConfigureForEditor(BistroBuilderAdaptiveSpatialProxyMode.Layered);
            proxy.AddPartForEditor(new BistroBuilderSpatialProxyPart
            {
                partId = "layer.dynamic",
                layer = BistroBuilderSpatialProxyLayer.Dynamic,
                shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
                size = new Vector2(0.5f, 1f)
            });
            check(proxy.ValidateProxy(out _),
                "Proxy Layered separa geometría Static/Operational/Dynamic");

            proxy.ConfigureForEditor(BistroBuilderAdaptiveSpatialProxyMode.Articulated);
            proxy.AddPartForEditor(new BistroBuilderSpatialProxyPart
            {
                partId = "joint.part",
                layer = BistroBuilderSpatialProxyLayer.Dynamic,
                shapeKind = BistroBuilderSpatialShapeKind.Capsule,
                anchor = articulation.transform,
                radius = 0.15f,
                capsuleLength = 0.8f
            });
            check(proxy.ValidateProxy(out _),
                "Proxy Articulated sigue articulaciones reales sin Rigidbody obligatorio");
            check(root.GetComponent<Rigidbody>() == null &&
                  articulation.GetComponent<Rigidbody>() == null,
                "Adaptive Spatial Proxy no requiere física dinámica");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestStress(
        BistroBuilderSpatialInteractionService service,
        Action<bool, string> check)
    {
        bool stressOk = true;
        const int iterations = 1000;
        for (int i = 0; i < iterations; i++)
        {
            string owner = "stress:" + i.ToString("D4");
            Vector3 point = new Vector3(200f + (i % 25) * 2f, 0f, 200f + (i / 25) * 2f);
            if (!service.TryAcquirePointLease(
                    owner, BistroBuilderSpatialClaimKind.Interaction,
                    point, 0.2f, 10, 30f, out string leaseId) ||
                string.IsNullOrEmpty(leaseId) || !service.ReleaseLease(leaseId))
            {
                stressOk = false;
                break;
            }
        }
        check(stressOk && service.ActiveLeaseCount == 0,
            "Stress 1000 Acquire/Release no deja fugas de leases");
    }

    private static BistroBuilderSpatialClaimRequest Request(
        string owner,
        Vector3 center,
        BistroBuilderSpatialConflictMode mode)
    {
        return new BistroBuilderSpatialClaimRequest
        {
            ownerId = owner,
            kind = BistroBuilderSpatialClaimKind.Interaction,
            conflictMode = mode,
            volume = BistroBuilderSpatialVolume.Circle(center, 0.45f),
            priority = 10,
            durationSeconds = 30f
        };
    }

    public static void RunFromCommandLine()
    {
        try
        {
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
