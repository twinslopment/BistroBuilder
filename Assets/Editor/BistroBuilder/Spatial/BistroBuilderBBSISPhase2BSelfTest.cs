using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderBBSISPhase2BSelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } =
        string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 2B/Autotest")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 2B - AUTOTEST");

        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderOperationalSpatialCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderOperationalSpatialCoordinator>();
        BistroBuilderKitchenSpatialAdapter kitchen =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderKitchenSpatialAdapter>();
        Check(spatial != null,
            "Servicio BBSIS disponible", report);
        Check(coordinator != null,
            "Coordinador operacional disponible", report);
        Check(kitchen != null,
            "Adaptador de cocina disponible", report);
        if (spatial == null || coordinator == null ||
            kitchen == null)
        {
            Finish(report);
            return;
        }

        spatial.ResetTransientRuntimeStateAfterLoad();
        RunKitchenLeaseTests(
            spatial, coordinator, kitchen, report);
        RunPassTests(
            spatial, coordinator, report);
        RunSemanticTests(kitchen, report);
        RunBarTests(report);
        RunStressTest(
            spatial, coordinator, report);

        spatial.ResetTransientRuntimeStateAfterLoad();
        Check(spatial.ActiveLeaseCount == 0 &&
              spatial.ActiveEpisodeCount == 0,
            "Autotest no deja estado espacial transitorio",
            report);
        Finish(report);
    }

    private static void RunKitchenLeaseTests(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderOperationalSpatialCoordinator coordinator,
        BistroBuilderKitchenSpatialAdapter kitchen,
        StringBuilder report)
    {
        bool first = coordinator.TryAcquireKitchenWork(
            "station.cold",
            "phase2b.work.first",
            0,
            out _);
        Check(first,
            "Puesto de cocina libre concede Work Lease",
            report);
        Check(spatial.CountLeases(
                BistroBuilderSpatialClaimKind.Work) == 1,
            "Work Lease queda registrado en BBSIS",
            report);

        bool blocked = coordinator.TryAcquireKitchenWork(
            "station.cold",
            "phase2b.work.second",
            0,
            out _);
        Check(!blocked,
            "Dos tareas incompatibles no comparten puesto",
            report);

        coordinator.ReleaseKitchenWork(
            "phase2b.work.first");
        bool grantedAfterRelease =
            coordinator.TryAcquireKitchenWork(
                "station.cold",
                "phase2b.work.second",
                0,
                out _);
        Check(grantedAfterRelease,
            "Liberar el puesto permite la siguiente tarea",
            report);
        coordinator.ReleaseKitchenWork(
            "phase2b.work.second");

        Check(kitchen.TryGetStationWorkVolume(
                "station.plating",
                2,
                out string platingPort,
                out _,
                out _) &&
              platingPort ==
                "work.station.plating.2",
            "Capacidad de emplatado resuelve slots distintos",
            report);
    }

    private static void RunPassTests(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderOperationalSpatialCoordinator coordinator,
        StringBuilder report)
    {
        bool first = coordinator.TryAcquirePassTransfer(
            "phase2b.pass.first",
            2f,
            out BistroBuilderSpatialLeaseDecision firstDecision);
        Check(first && firstDecision.granted,
            "Pass libre concede Transfer Lease",
            report);

        bool blocked = coordinator.TryAcquirePassTransfer(
            "phase2b.pass.second",
            2f,
            out BistroBuilderSpatialLeaseDecision secondDecision);
        Check(!blocked &&
              secondDecision.failure ==
                BistroBuilderSpatialLeaseFailure.Conflict,
            "Pass impide transferencias incompatibles",
            report);

        coordinator.ReleaseOperationalOwner(
            "phase2b.pass.first");
        bool retry = coordinator.TryAcquirePassTransfer(
            "phase2b.pass.second",
            2f,
            out _);
        Check(retry,
            "Pass liberado acepta la siguiente transferencia",
            report);
        coordinator.ReleaseOperationalOwner(
            "phase2b.pass.second");
        Check(spatial.CountLeases(
                BistroBuilderSpatialClaimKind.Transfer) == 0,
            "Transfer Leases se liberan explícitamente",
            report);
    }

    private static void RunSemanticTests(
        BistroBuilderKitchenSpatialAdapter kitchen,
        StringBuilder report)
    {
        var volumes =
            new List<BistroBuilderSpatialSemanticVolume>();
        int count = kitchen.WriteSemanticVolumes(volumes);
        bool hasWork = false;
        bool hasTransfer = false;
        for (int i = 0; i < volumes.Count; i++)
        {
            BistroBuilderSpatialSemanticVolume volume =
                volumes[i];
            if (volume == null)
                continue;
            hasWork |= volume.role ==
                BistroBuilderSpatialSemanticRole.WorkZone;
            hasTransfer |= volume.role ==
                BistroBuilderSpatialSemanticRole.TransferZone;
        }
        Check(count > 1 && hasWork && hasTransfer,
            "Cocina publica Work Zones y Transfer Zone",
            report);
        Check(kitchen.Subject.Contract.WorkEdges.Count > 0,
            "Cocina publica Work Edges contractuales",
            report);
    }

    private static void RunBarTests(
        StringBuilder report)
    {
        BistroBuilderBarSpatialAdapter[] adapters =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderBarSpatialAdapter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        Check(adapters.Length > 0,
            "Adaptadores reales de barra detectados",
            report);
        bool allSemantic = adapters.Length > 0;
        for (int i = 0; i < adapters.Length; i++)
        {
            var volumes =
                new List<BistroBuilderSpatialSemanticVolume>();
            int count = adapters[i].WriteSemanticVolumes(volumes);
            bool customer = false;
            bool work = false;
            bool transfer = false;
            for (int v = 0; v < volumes.Count; v++)
            {
                if (volumes[v] == null)
                    continue;
                customer |= volumes[v].role ==
                    BistroBuilderSpatialSemanticRole.SeatBay;
                work |= volumes[v].role ==
                    BistroBuilderSpatialSemanticRole.WorkZone;
                transfer |= volumes[v].role ==
                    BistroBuilderSpatialSemanticRole.TransferZone;
            }
            allSemantic &= count == 3 &&
                customer && work && transfer;
        }
        Check(allSemantic,
            "Barra publica cliente, servicio y transferencia",
            report);
    }

    private static void RunStressTest(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderOperationalSpatialCoordinator coordinator,
        StringBuilder report)
    {
        int before = spatial.ActiveLeaseCount;
        bool clean = true;
        for (int i = 0; i < 300; i++)
        {
            string owner = "phase2b.stress." + i;
            if (!coordinator.TryAcquireKitchenWork(
                    "station.range",
                    owner,
                    i % 2,
                    out _))
            {
                clean = false;
                break;
            }
            coordinator.ReleaseKitchenWork(owner);
            if (spatial.ActiveLeaseCount != before)
            {
                clean = false;
                break;
            }
        }
        Check(clean,
            "Stress 300 Work Lease Acquire/Release sin fugas",
            report);
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
                "Autotest BBSIS Fase 2B fallido.");
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
}
