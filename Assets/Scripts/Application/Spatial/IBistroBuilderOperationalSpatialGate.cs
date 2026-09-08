using UnityEngine;

/// <summary>
/// Frontera de integración: los sistemas funcionales solicitan espacio,
/// pero BBSIS conserva la autoridad sobre su concesión.
/// </summary>
public interface IBistroBuilderOperationalSpatialGate
{
    bool TryAcquireKitchenWork(
        string stationId,
        string workId,
        int preferredSlot,
        out string rejectionReason);

    void ReleaseKitchenWork(string workId);

    bool TryAcquirePassTransfer(
        string ownerId,
        float durationSeconds,
        out BistroBuilderSpatialLeaseDecision decision);

    void ReleaseOperationalOwner(string ownerId);
}
