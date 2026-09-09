using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Publica el espacio funcional de cocina y pass sin decidir producción,
/// asignación de cocineros ni rutas.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderKitchenSpatialAdapter :
    MonoBehaviour,
    IBistroBuilderSpatialSemanticProvider
{
    public const string PassPortId = "pass.transfer";

    [SerializeField] private KitchenSystem kitchenSystem;
    [SerializeField] private BistroBuilderSpatialSubject subject;

    public string SpatialSubjectId =>
        subject != null ? subject.SubjectId : string.Empty;

    public KitchenSystem KitchenSystem => kitchenSystem;
    public BistroBuilderSpatialSubject Subject => subject;

    public void Configure(
        KitchenSystem kitchen,
        BistroBuilderSpatialSubject spatialSubject)
    {
        kitchenSystem = kitchen;
        subject = spatialSubject;
        EnsurePassAnchor();
    }

    public bool ValidateConfiguration(out string error)
    {
        if (kitchenSystem == null || subject == null ||
            subject.Contract == null || subject.Proxy == null)
        {
            error = "El adaptador espacial de cocina está incompleto.";
            return false;
        }

        if (!subject.Contract.HasTrait("kitchen.operational") ||
            !subject.TryGetPortWorld(
                PassPortId, out _, out _, out _, out _))
        {
            error = "La cocina no publica su pass contractual.";
            return false;
        }

        int workPorts = 0;
        for (int i = 0; i < subject.Contract.Ports.Count; i++)
            if (subject.Contract.Ports[i] != null &&
                subject.Contract.Ports[i].kind ==
                    BistroBuilderSpatialPortKind.Work)
                workPorts++;

        if (workPorts == 0)
        {
            error = "La cocina no publica puestos de trabajo.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetStationWorkVolume(
        string stationId,
        int preferredSlot,
        out string portId,
        out BistroBuilderSpatialVolume volume,
        out BistroBuilderSpatialConflictMode conflictMode)
    {
        portId = string.Empty;
        volume = default;
        conflictMode = BistroBuilderSpatialConflictMode.Block;
        if (subject == null || subject.Contract == null)
            return false;

        string normalized =
            BistroBuilderStaffStableIdUtility.Normalize(stationId);
        string prefix = "work." + normalized + ".";
        int requested = Mathf.Max(0, preferredSlot);
        string exact = prefix + requested;

        if (TryBuildPortVolume(exact, out volume, out conflictMode))
        {
            portId = exact;
            return true;
        }

        for (int i = 0; i < subject.Contract.Ports.Count; i++)
        {
            BistroBuilderSpatialPortDefinition port =
                subject.Contract.Ports[i];
            if (port == null || port.kind !=
                    BistroBuilderSpatialPortKind.Work ||
                !port.portId.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            if (!TryBuildPortVolume(
                    port.portId, out volume, out conflictMode))
                continue;
            portId = port.portId;
            return true;
        }

        return false;
    }

    public bool TryGetPassVolume(
        out BistroBuilderSpatialVolume volume,
        out BistroBuilderSpatialConflictMode conflictMode)
    {
        return TryBuildPortVolume(
            PassPortId, out volume, out conflictMode);
    }

    public int WriteSemanticVolumes(
        List<BistroBuilderSpatialSemanticVolume> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        if (subject == null || subject.Contract == null)
            return 0;

        int before = results.Count;
        for (int i = 0; i < subject.Contract.Ports.Count; i++)
        {
            BistroBuilderSpatialPortDefinition port =
                subject.Contract.Ports[i];
            if (port == null ||
                (port.kind != BistroBuilderSpatialPortKind.Work &&
                 port.kind != BistroBuilderSpatialPortKind.Transfer))
                continue;
            if (!TryBuildPortVolume(
                    port.portId,
                    out BistroBuilderSpatialVolume volume,
                    out BistroBuilderSpatialConflictMode mode))
                continue;

            results.Add(new BistroBuilderSpatialSemanticVolume
            {
                subjectId = subject.SubjectId,
                semanticId = port.portId,
                role = port.kind == BistroBuilderSpatialPortKind.Work
                    ? BistroBuilderSpatialSemanticRole.WorkZone
                    : BistroBuilderSpatialSemanticRole.TransferZone,
                layer = BistroBuilderSpatialProxyLayer.Operational,
                conflictMode = mode,
                volume = volume,
                critical = true
            });
        }

        return results.Count - before;
    }

    private bool TryBuildPortVolume(
        string portId,
        out BistroBuilderSpatialVolume volume,
        out BistroBuilderSpatialConflictMode conflictMode)
    {
        volume = default;
        conflictMode = BistroBuilderSpatialConflictMode.Block;
        if (subject == null ||
            !subject.TryGetPortWorld(
                portId,
                out Vector3 position,
                out _,
                out float radius,
                out conflictMode))
            return false;
        volume = BistroBuilderSpatialVolume.Circle(
            position, Mathf.Max(0.2f, radius));
        return true;
    }

    private void EnsurePassAnchor()
    {
        if (kitchenSystem == null || subject == null ||
            kitchenSystem.PickupPoint == null)
            return;
        BistroBuilderSpatialPortAnchors anchors =
            subject.GetComponent<BistroBuilderSpatialPortAnchors>();
        if (anchors == null)
            anchors = subject.gameObject.AddComponent<
                BistroBuilderSpatialPortAnchors>();
        anchors.ClearBindings();
        anchors.AddBinding(PassPortId, kitchenSystem.PickupPoint);
    }
}
