using System;
using UnityEngine;

/// <summary>
/// Familias semÃ¡nticas universales de interacciÃ³n visual.
/// Gameplay decide la intenciÃ³n y el sistema espacial decide si puede ocurrir;
/// estas familias sÃ³lo describen cÃ³mo se representa.
/// </summary>
public enum BistroBuilderInteractionFamily
{
    None = 0,
    Seat = 1,
    Portal = 2,
    Transfer = 3,
    Carry = 4,
    Workstation = 5,
    Appliance = 6,
    Handover = 7,
    Social = 8
}

public enum BistroBuilderInteractionOperation
{
    None = 0,
    Sit = 1,
    Stand = 2,
    Open = 3,
    Close = 4,
    Pickup = 5,
    Place = 6,
    Use = 7,
    Give = 8,
    Receive = 9,
    Converse = 10
}

public enum BistroBuilderInteractionPhase
{
    None = 0,
    Acquire = 1,
    Approach = 2,
    Align = 3,
    Engage = 4,
    Operate = 5,
    Disengage = 6,
    Settle = 7,
    Recover = 8,
    Completed = 9,
    Failed = 10,
    Cancelled = 11
}

public enum BistroBuilderInteractionStatus
{
    Pending = 0,
    Running = 1,
    AwaitingCommit = 2,
    Completed = 3,
    Cancelled = 4,
    Failed = 5,
    Recovered = 6
}

public enum BistroBuilderInteractionCommitState
{
    PreCommit = 0,
    AwaitingConfirmation = 1,
    Confirmed = 2,
    Rejected = 3
}

public enum BistroBuilderAnimationBodyMode
{
    FullBody = 0,
    UpperBody = 1,
    Additive = 2
}

public enum BistroBuilderMotionSyncPointKind
{
    VisualMarker = 0,
    SyncMarker = 1,
    Gate = 2,
    ProgressMarker = 3
}

public enum BistroBuilderInteractionFailureKind
{
    None = 0,
    InvalidPlan = 1,
    ActorBusy = 2,
    SpatialClaimMissing = 3,
    AlignmentOutOfBudget = 4,
    TargetUnavailable = 5,
    MotionUnavailable = 6,
    CommitRejected = 7,
    CommitTimeout = 8,
    OperationTimeout = 9,
    CancelledBeforeCommit = 10,
    RuntimeException = 11
}

[Serializable]
public sealed class BistroBuilderMotionSyncPoint
{
    [SerializeField] private string markerId = "contact";
    [SerializeField, Range(0f, 1f)] private float normalizedTime = 0.5f;
    [SerializeField] private BistroBuilderMotionSyncPointKind kind = BistroBuilderMotionSyncPointKind.SyncMarker;

    public string MarkerId => string.IsNullOrWhiteSpace(markerId) ? string.Empty : markerId.Trim().ToLowerInvariant();
    public float NormalizedTime => Mathf.Clamp01(normalizedTime);
    public BistroBuilderMotionSyncPointKind Kind => kind;
}

[Serializable]
public sealed class BistroBuilderInteractionAdaptationBudget
{
    [SerializeField, Range(0.01f, 0.30f)] private float comfortableRootDistanceRatio = 0.08f;
    [SerializeField, Range(0.01f, 0.40f)] private float assistedRootDistanceRatio = 0.15f;
    [SerializeField, Range(1f, 90f)] private float comfortableYawDegrees = 20f;
    [SerializeField, Range(1f, 120f)] private float assistedYawDegrees = 35f;
    [SerializeField, Range(0.5f, 1.2f)] private float comfortableReachRatio = 0.90f;
    [SerializeField, Range(0.5f, 1.4f)] private float assistedReachRatio = 1.05f;

    public float ComfortableRootDistanceRatio => Mathf.Max(0.01f, comfortableRootDistanceRatio);
    public float AssistedRootDistanceRatio => Mathf.Max(ComfortableRootDistanceRatio, assistedRootDistanceRatio);
    public float ComfortableYawDegrees => Mathf.Max(1f, comfortableYawDegrees);
    public float AssistedYawDegrees => Mathf.Max(ComfortableYawDegrees, assistedYawDegrees);
    public float ComfortableReachRatio => Mathf.Max(0.5f, comfortableReachRatio);
    public float AssistedReachRatio => Mathf.Max(ComfortableReachRatio, assistedReachRatio);

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (AssistedRootDistanceRatio < ComfortableRootDistanceRatio)
        {
            error = "El margen root asistido no puede ser menor que el cÃ³modo.";
            return false;
        }
        if (AssistedYawDegrees < ComfortableYawDegrees)
        {
            error = "El margen angular asistido no puede ser menor que el cÃ³modo.";
            return false;
        }
        if (AssistedReachRatio < ComfortableReachRatio)
        {
            error = "El alcance asistido no puede ser menor que el cÃ³modo.";
            return false;
        }
        return true;
    }
}

/// <summary>
/// Plan de presentaciÃ³n ya resuelto. No decide si la acciÃ³n es legal.
/// Debe construirse Ãºnicamente despuÃ©s de que gameplay y espacio hayan
/// elegido actor, objetivo y slot.
/// </summary>
[Serializable]
public sealed class BistroBuilderResolvedInteractionPlan
{
    public string interactionId = string.Empty;
    public string ownerId = string.Empty;
    public BistroBuilderInteractionFamily family;
    public BistroBuilderInteractionOperation operation;
    public GameObject actor;
    public GameObject target;
    public Transform interactionFrame;
    public Transform seatFrame;
    public Transform exitFrame;
    public Transform transferTarget;
    public Transform sourceSocket;
    public Transform destinationSocket;
    public Transform rightHandTarget;
    public Transform leftHandTarget;
    public Transform lookTarget;
    public RestaurantSeat seat;
    public BistroBuilderNavigableDoor door;
    public BistroBuilderTransferableVisual transferable;
    public string semanticMotionId = string.Empty;
    public string fallbackMotionId = string.Empty;
    [Range(0.5f, 1.5f)] public float tempo = 1f;
    [Range(0f, 1f)] public float commitNormalizedTime = 0.5f;
    [Min(0.05f)] public float fallbackMotionDuration = 0.6f;
    [Min(0.1f)] public float operationTimeoutSeconds = 6f;
    [Min(0.1f)] public float commitTimeoutSeconds = 2f;
    public bool requiresCommitConfirmation = true;
    public BistroBuilderInteractionAdaptationBudget adaptationBudget = new BistroBuilderInteractionAdaptationBudget();

    public string EffectiveInteractionId => string.IsNullOrWhiteSpace(interactionId)
        ? Guid.NewGuid().ToString("N")
        : interactionId.Trim();

    public string EffectiveOwnerId => string.IsNullOrWhiteSpace(ownerId)
        ? string.Empty
        : ownerId.Trim();

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (actor == null)
        {
            error = "El plan necesita actor.";
            return false;
        }
        if (family == BistroBuilderInteractionFamily.None || operation == BistroBuilderInteractionOperation.None)
        {
            error = "El plan necesita familia y operaciÃ³n semÃ¡nticas.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(EffectiveOwnerId))
        {
            error = "El plan necesita OwnerId estable.";
            return false;
        }
        if (adaptationBudget == null || !adaptationBudget.Validate(out error))
            return false;

        switch (family)
        {
            case BistroBuilderInteractionFamily.Seat:
                if (seat == null)
                {
                    error = "La interacciÃ³n Seat necesita RestaurantSeat.";
                    return false;
                }
                break;
            case BistroBuilderInteractionFamily.Portal:
                if (door == null)
                {
                    error = "La interacciÃ³n Portal necesita BistroBuilderNavigableDoor.";
                    return false;
                }
                break;
            case BistroBuilderInteractionFamily.Transfer:
                if (transferable == null || destinationSocket == null)
                {
                    error = "Transfer necesita objeto visual y socket de destino.";
                    return false;
                }
                break;
        }
        return true;
    }
}

/// <summary>
/// Handle observable y cancelable. Nunca contiene lÃ³gica de gameplay.
/// </summary>
public sealed class BistroBuilderInteractionHandle
{
    public string HandleId { get; }
    public string InteractionId { get; }
    public string OwnerId { get; }
    public BistroBuilderInteractionStatus Status { get; private set; }
    public BistroBuilderInteractionPhase Phase { get; private set; }
    public BistroBuilderInteractionCommitState CommitState { get; private set; }
    public BistroBuilderInteractionFailureKind FailureKind { get; private set; }
    public string FailureReason { get; private set; }
    public float StartedAtUnscaledTime { get; }
    public bool CancellationRequested { get; private set; }
    public bool IsTerminal => Status == BistroBuilderInteractionStatus.Completed ||
                              Status == BistroBuilderInteractionStatus.Cancelled ||
                              Status == BistroBuilderInteractionStatus.Failed ||
                              Status == BistroBuilderInteractionStatus.Recovered;

    public event Action<BistroBuilderInteractionHandle> Changed;

    internal BistroBuilderInteractionHandle(string interactionId, string ownerId)
    {
        HandleId = Guid.NewGuid().ToString("N");
        InteractionId = interactionId ?? string.Empty;
        OwnerId = ownerId ?? string.Empty;
        Status = BistroBuilderInteractionStatus.Pending;
        Phase = BistroBuilderInteractionPhase.None;
        CommitState = BistroBuilderInteractionCommitState.PreCommit;
        FailureKind = BistroBuilderInteractionFailureKind.None;
        FailureReason = string.Empty;
        StartedAtUnscaledTime = Time.unscaledTime;
    }

    public bool RequestCancel()
    {
        if (IsTerminal) return false;
        CancellationRequested = true;
        Changed?.Invoke(this);
        return true;
    }

    public bool ConfirmCommit()
    {
        if (CommitState != BistroBuilderInteractionCommitState.AwaitingConfirmation)
            return false;
        CommitState = BistroBuilderInteractionCommitState.Confirmed;
        Changed?.Invoke(this);
        return true;
    }

    public bool RejectCommit(string reason)
    {
        if (CommitState != BistroBuilderInteractionCommitState.AwaitingConfirmation)
            return false;
        CommitState = BistroBuilderInteractionCommitState.Rejected;
        FailureReason = reason ?? string.Empty;
        Changed?.Invoke(this);
        return true;
    }

    internal void SetRunning(BistroBuilderInteractionPhase phase)
    {
        Status = BistroBuilderInteractionStatus.Running;
        Phase = phase;
        Changed?.Invoke(this);
    }

    internal void SetAwaitingCommit()
    {
        Status = BistroBuilderInteractionStatus.AwaitingCommit;
        CommitState = BistroBuilderInteractionCommitState.AwaitingConfirmation;
        Changed?.Invoke(this);
    }

    internal void MarkCommitted()
    {
        CommitState = BistroBuilderInteractionCommitState.Confirmed;
        Status = BistroBuilderInteractionStatus.Running;
        Changed?.Invoke(this);
    }

    internal void Complete(bool recovered = false)
    {
        Phase = BistroBuilderInteractionPhase.Completed;
        Status = recovered ? BistroBuilderInteractionStatus.Recovered : BistroBuilderInteractionStatus.Completed;
        Changed?.Invoke(this);
    }

    internal void Cancel(BistroBuilderInteractionFailureKind failureKind, string reason)
    {
        Phase = BistroBuilderInteractionPhase.Cancelled;
        Status = BistroBuilderInteractionStatus.Cancelled;
        FailureKind = failureKind;
        FailureReason = reason ?? string.Empty;
        Changed?.Invoke(this);
    }

    internal void Fail(BistroBuilderInteractionFailureKind failureKind, string reason)
    {
        Phase = BistroBuilderInteractionPhase.Failed;
        Status = BistroBuilderInteractionStatus.Failed;
        FailureKind = failureKind;
        FailureReason = reason ?? string.Empty;
        Changed?.Invoke(this);
    }
}
