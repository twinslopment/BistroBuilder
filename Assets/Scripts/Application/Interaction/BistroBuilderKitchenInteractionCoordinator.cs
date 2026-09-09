using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordina capacidad lógica de procesos de cocina mediante Use Permits.
/// No crea Claims espaciales: la admisión física sigue perteneciendo a BBSIS.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderKitchenInteractionCoordinator : MonoBehaviour
{
    [SerializeField] private BistroBuilderInteractionService interactionService;

    private readonly Dictionary<string, BistroBuilderInteractionGrantHandle> permitsByLine =
        new Dictionary<string, BistroBuilderInteractionGrantHandle>(StringComparer.Ordinal);

    public int ActiveProcessPermitCount => permitsByLine.Count;

    private void Awake()
    {
        ResolveService();
    }

    public void ConfigureForEditor(BistroBuilderInteractionService service)
    {
        interactionService = service;
    }

    public bool TryAcquireProcessSlot(
        string stationId,
        string lineId,
        int stageIndex,
        int slotIndex,
        int priorityClass)
    {
        ResolveService();
        string normalizedLine = BistroBuilderOrderIdUtility.Normalize(lineId);
        string normalizedStation = BistroBuilderStaffStableIdUtility.Normalize(stationId);
        if (interactionService == null || string.IsNullOrWhiteSpace(normalizedLine) ||
            string.IsNullOrWhiteSpace(normalizedStation) || stageIndex < 0 || slotIndex < 0)
            return false;

        if (TryGetValidPermit(normalizedLine, out var existing))
            return interactionService.TryGetGrant(existing, out var grant) &&
                   string.Equals(
                       grant.resourceId,
                       ResourceId(normalizedStation, slotIndex),
                       StringComparison.Ordinal);

        string requestId = RequestId(normalizedLine, stageIndex, slotIndex);
        interactionService.SubmitAcquisition(BuildRequest(
            requestId, normalizedStation, normalizedLine,
            stageIndex, slotIndex, priorityClass));
        interactionService.ResolveArbitrationEpoch();
        if (!interactionService.TryGetDecision(requestId, out var decision) ||
            decision.outcome != BistroBuilderInteractionRequestOutcome.Granted ||
            !decision.handle.IsValid)
            return false;

        if (!interactionService.TryEngageUsePermit(decision.handle))
        {
            interactionService.ReleaseGrant(decision.handle);
            return false;
        }

        permitsByLine[normalizedLine] = decision.handle;
        return true;
    }

    public bool CompleteProcessSlot(string lineId)
    {
        string normalizedLine = BistroBuilderOrderIdUtility.Normalize(lineId);
        if (!TryGetValidPermit(normalizedLine, out var handle))
            return false;

        bool result = interactionService.CompleteGrant(handle);
        permitsByLine.Remove(normalizedLine);
        return result;
    }

    public bool ReleaseProcessSlot(
        string lineId,
        BistroBuilderInteractionReasonCode reason =
            BistroBuilderInteractionReasonCode.TaskCancelled)
    {
        string normalizedLine = BistroBuilderOrderIdUtility.Normalize(lineId);
        if (!TryGetValidPermit(normalizedLine, out var handle))
            return false;

        bool result = interactionService.CancelGrant(handle, reason);
        permitsByLine.Remove(normalizedLine);
        return result;
    }

    public void ReleaseAllProcessSlots()
    {
        ResolveService();
        if (interactionService != null)
        {
            foreach (var pair in permitsByLine)
                interactionService.ReleaseGrant(pair.Value);
        }
        permitsByLine.Clear();
    }

    public bool TryGetProcessPermit(
        string lineId,
        out BistroBuilderInteractionGrantHandle handle)
    {
        return TryGetValidPermit(BistroBuilderOrderIdUtility.Normalize(lineId), out handle);
    }

    public bool ValidateProcessSlot(
        string stationId,
        string lineId,
        int slotIndex)
    {
        ResolveService();
        if (!TryGetValidPermit(
                BistroBuilderOrderIdUtility.Normalize(lineId),
                out var handle) ||
            interactionService == null ||
            !interactionService.TryGetGrant(handle, out var grant))
            return false;

        return string.Equals(
            grant.resourceId,
            ResourceId(
                BistroBuilderStaffStableIdUtility.Normalize(stationId),
                slotIndex),
            StringComparison.Ordinal);
    }

    private bool TryGetValidPermit(
        string normalizedLine,
        out BistroBuilderInteractionGrantHandle handle)
    {
        handle = default;
        if (string.IsNullOrWhiteSpace(normalizedLine) ||
            !permitsByLine.TryGetValue(normalizedLine, out handle))
            return false;

        ResolveService();
        if (interactionService != null &&
            interactionService.ValidateCurrentGrant(handle))
            return true;

        permitsByLine.Remove(normalizedLine);
        handle = default;
        return false;
    }

    private BistroBuilderInteractionAcquisitionRequest BuildRequest(
        string requestId,
        string stationId,
        string lineId,
        int stageIndex,
        int slotIndex,
        int priorityClass)
    {
        return new BistroBuilderInteractionAcquisitionRequest
        {
            requestId = requestId,
            grantKind = BistroBuilderInteractionGrantKind.UsePermit,
            holderKind = BistroBuilderInteractionHolderKind.Process,
            holderId = ProcessHolderId(lineId, stageIndex),
            interactionId = "kitchen.process",
            sessionId = "kitchen-line:" + lineId,
            taskPriorityClass = priorityClass,
            countsCapacity = true,
            capacityUnits = 1,
            persistCanonical = false,
            requiresSpatialAdmission = false,
            engageWithinSeconds = 1f,
            candidates = new List<BistroBuilderInteractionCandidate>
            {
                new BistroBuilderInteractionCandidate
                {
                    resourceId = ResourceId(stationId, slotIndex),
                    channelId = "process",
                    slotId = "slot:" + slotIndex,
                    suitability = 0,
                    travelCostHint = 0
                }
            }
        };
    }

    private void ResolveService()
    {
        if (interactionService == null)
            interactionService = FindFirstObjectByType<BistroBuilderInteractionService>();
    }

    private static string ResourceId(string stationId, int slotIndex)
    {
        return "kitchen-station:" + stationId + ":process-slot:" + slotIndex;
    }

    private static string RequestId(
        string lineId,
        int stageIndex,
        int slotIndex)
    {
        return "kitchen-permit:" + lineId + ":" + stageIndex + ":" + slotIndex;
    }

    private static string ProcessHolderId(string lineId, int stageIndex)
    {
        return "kitchen-process:" + lineId + ":stage:" + stageIndex;
    }
}
