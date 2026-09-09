using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adapta las tareas de camarero existentes al Task Claim canónico de
/// BB Interaction & Reservation. WaiterTask conserva estado operativo,
/// pero la exclusividad lógica pertenece exclusivamente al kernel.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderWaiterTaskClaimCoordinator : MonoBehaviour
{
    [SerializeField] private BistroBuilderInteractionService interactionService;

    private readonly Dictionary<int, BistroBuilderInteractionGrantHandle> claimsByTaskId =
        new Dictionary<int, BistroBuilderInteractionGrantHandle>();

    public int ActiveClaimCount => claimsByTaskId.Count;

    private void Awake()
    {
        ResolveService();
    }

    public void ConfigureForEditor(BistroBuilderInteractionService service)
    {
        interactionService = service;
    }

    public bool TryClaimTask(WaiterTask task, Waiter waiter)
    {
        if (task == null || waiter == null || !waiter.IsAvailable)
            return false;

        ResolveService();
        if (interactionService == null)
            return false;

        if (TryGetValidClaim(task, out BistroBuilderInteractionGrantHandle existing))
        {
            return interactionService.TryGetGrant(existing, out var grant) &&
                   string.Equals(grant.holderId, HolderId(waiter), StringComparison.Ordinal);
        }

        string requestId = RequestId(task, waiter);
        interactionService.SubmitAcquisition(BuildRequest(task, waiter, requestId));
        interactionService.ResolveArbitrationEpoch();

        if (!interactionService.TryGetDecision(requestId, out var decision) ||
            decision.outcome != BistroBuilderInteractionRequestOutcome.Granted ||
            !decision.handle.IsValid)
            return false;

        claimsByTaskId[task.TaskId] = decision.handle;
        return true;
    }

    public bool TryClaimBundle(
        IReadOnlyList<WaiterTask> tasks,
        Waiter waiter
    )
    {
        if (tasks == null || tasks.Count == 0 || waiter == null || !waiter.IsAvailable)
            return false;

        ResolveService();
        if (interactionService == null)
            return false;

        var bundle = new BistroBuilderInteractionBundleRequest
        {
            bundleId = BundleId(tasks, waiter)
        };

        for (int i = 0; i < tasks.Count; i++)
        {
            WaiterTask task = tasks[i];
            if (task == null || TryGetValidClaim(task, out _))
                return false;
            bundle.requests.Add(BuildRequest(task, waiter, RequestId(task, waiter)));
        }

        string bundleId = interactionService.SubmitBundle(bundle);
        if (string.IsNullOrWhiteSpace(bundleId))
            return false;

        interactionService.ResolveArbitrationEpoch();
        if (!interactionService.TryGetBundleDecision(bundleId, out var decision) ||
            decision.outcome != BistroBuilderInteractionRequestOutcome.Granted ||
            decision.decisions == null || decision.decisions.Count != tasks.Count)
            return false;

        for (int i = 0; i < decision.decisions.Count; i++)
        {
            BistroBuilderInteractionDecision item = decision.decisions[i];
            WaiterTask task = FindTaskByRequest(tasks, item.requestId, waiter);
            if (task == null || !item.handle.IsValid)
            {
                ReleaseClaims(tasks);
                return false;
            }
            claimsByTaskId[task.TaskId] = item.handle;
        }
        return true;
    }

    public bool TryCommitTask(WaiterTask task)
    {
        ResolveService();
        return task != null && interactionService != null &&
               TryGetValidClaim(task, out var handle) &&
               interactionService.TryCommitTaskClaim(handle);
    }

    public bool ValidateTaskOwnership(WaiterTask task, Waiter waiter)
    {
        ResolveService();
        if (task == null || waiter == null || interactionService == null ||
            !TryGetValidClaim(task, out var handle))
            return false;

        return interactionService.TryGetGrant(handle, out var grant) &&
               string.Equals(grant.holderId, HolderId(waiter), StringComparison.Ordinal) &&
               (grant.state == BistroBuilderInteractionGrantState.Claimed ||
                grant.state == BistroBuilderInteractionGrantState.Committed);
    }

    public void ReleaseTaskClaim(WaiterTask task)
    {
        if (task == null || !claimsByTaskId.TryGetValue(task.TaskId, out var handle))
            return;
        ResolveService();
        interactionService?.ReleaseGrant(handle);
        claimsByTaskId.Remove(task.TaskId);
    }

    public void EndTaskClaim(WaiterTask task)
    {
        if (task == null || !claimsByTaskId.TryGetValue(task.TaskId, out var handle))
            return;
        ResolveService();
        if (interactionService != null)
        {
            if (task.State == WaiterTaskState.Completed)
                interactionService.CompleteGrant(handle);
            else
                interactionService.CancelGrant(handle);
        }
        claimsByTaskId.Remove(task.TaskId);
    }

    public void ReleaseClaims(IReadOnlyList<WaiterTask> tasks)
    {
        if (tasks == null)
            return;
        for (int i = 0; i < tasks.Count; i++)
            ReleaseTaskClaim(tasks[i]);
    }

    public int InvalidateWaiter(Waiter waiter)
    {
        ResolveService();
        if (waiter == null || interactionService == null)
            return 0;

        int affected = interactionService.InvalidateHolder(
            HolderId(waiter),
            BistroBuilderInteractionReasonCode.ShiftEnded);

        var staleTaskIds = new List<int>();
        foreach (KeyValuePair<int, BistroBuilderInteractionGrantHandle> pair in claimsByTaskId)
        {
            if (!interactionService.ValidateCurrentGrant(pair.Value))
                staleTaskIds.Add(pair.Key);
        }
        for (int i = 0; i < staleTaskIds.Count; i++)
            claimsByTaskId.Remove(staleTaskIds[i]);
        return affected;
    }

    public bool TryGetClaim(WaiterTask task, out BistroBuilderInteractionGrantHandle handle)
    {
        return TryGetValidClaim(task, out handle);
    }

    private BistroBuilderInteractionAcquisitionRequest BuildRequest(
        WaiterTask task,
        Waiter waiter,
        string requestId
    )
    {
        return new BistroBuilderInteractionAcquisitionRequest
        {
            requestId = requestId,
            grantKind = BistroBuilderInteractionGrantKind.TaskClaim,
            holderKind = BistroBuilderInteractionHolderKind.Actor,
            holderId = HolderId(waiter),
            interactionId = "waiter.task." + task.Type.ToString().ToLowerInvariant(),
            sessionId = "waiter-task-session:" + task.TaskId,
            taskPriorityClass = (int)task.Priority,
            countsCapacity = true,
            capacityUnits = 1,
            persistCanonical = false,
            candidates = new List<BistroBuilderInteractionCandidate>
            {
                new BistroBuilderInteractionCandidate
                {
                    resourceId = ResourceId(task),
                    suitability = 0,
                    travelCostHint = 0
                }
            }
        };
    }

    private bool TryGetValidClaim(
        WaiterTask task,
        out BistroBuilderInteractionGrantHandle handle
    )
    {
        handle = default;
        if (task == null || !claimsByTaskId.TryGetValue(task.TaskId, out handle))
            return false;
        ResolveService();
        if (interactionService != null && interactionService.ValidateCurrentGrant(handle))
            return true;
        claimsByTaskId.Remove(task.TaskId);
        handle = default;
        return false;
    }

    private void ResolveService()
    {
        if (interactionService == null)
            interactionService = FindFirstObjectByType<BistroBuilderInteractionService>();
    }

    private static string HolderId(Waiter waiter)
    {
        return waiter != null ? "waiter:" + waiter.WaiterId : string.Empty;
    }

    private static string ResourceId(WaiterTask task)
    {
        return task != null ? "waiter-task:" + task.TaskId : string.Empty;
    }

    private static string RequestId(WaiterTask task, Waiter waiter)
    {
        return "waiter-claim:" + task.TaskId + ":" +
               (waiter != null ? waiter.WaiterId : string.Empty);
    }

    private static string BundleId(IReadOnlyList<WaiterTask> tasks, Waiter waiter)
    {
        var ids = new List<int>(tasks.Count);
        for (int i = 0; i < tasks.Count; i++)
            if (tasks[i] != null) ids.Add(tasks[i].TaskId);
        ids.Sort();
        return "waiter-claim-bundle:" +
               (waiter != null ? waiter.WaiterId : string.Empty) + ":" +
               string.Join("-", ids);
    }

    private static WaiterTask FindTaskByRequest(
        IReadOnlyList<WaiterTask> tasks,
        string requestId,
        Waiter waiter
    )
    {
        for (int i = 0; i < tasks.Count; i++)
        {
            WaiterTask task = tasks[i];
            if (task != null && string.Equals(
                    RequestId(task, waiter), requestId, StringComparison.Ordinal))
                return task;
        }
        return null;
    }
}
