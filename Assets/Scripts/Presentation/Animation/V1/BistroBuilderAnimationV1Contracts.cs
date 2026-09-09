using System;
using UnityEngine;

public enum BistroBuilderAnimationQualityTier { Q0 = 0, Q1 = 1, Q2 = 2, Q3 = 3, Q4 = 4 }
public enum BistroBuilderMotionExecutionState { Pending = 0, Running = 1, AwaitingAuthoritativeCommit = 2, Recovering = 3, Completed = 4, Cancelled = 5, Failed = 6, Rehydrated = 7 }
public enum BistroBuilderMotionInterruptPolicy { Immediate = 0, AtSafeMarker = 1, AfterCommit = 2, NonInterruptible = 3 }
public enum BistroBuilderMotionRecoveryMode { Baseline = 0, SemanticSafePose = 1, PreserveCommittedPose = 2 }
public enum BistroBuilderAnimationTargetKind { None = 0, Actor = 1, Seat = 2, Portal = 3, Transferable = 4, Workstation = 5, Appliance = 6, Handover = 7, Social = 8 }
public enum BistroBuilderMotionResultCode { None = 0, Completed = 1, Cancelled = 2, InvalidRequest = 3, ActorUnavailable = 4, TargetUnavailable = 5, MotionUnavailable = 6, StaleExecution = 7, CommitRejected = 8, CommitTimeout = 9, WatchdogTimeout = 10, RuntimeFailure = 11 }

[Serializable]
public struct BistroBuilderAnimationExecutionHandle : IEquatable<BistroBuilderAnimationExecutionHandle>
{
    public string executionId;
    public long generation;
    public BistroBuilderAnimationExecutionHandle(string id, long value) { executionId = id ?? string.Empty; generation = value; }
    public bool IsValid => !string.IsNullOrWhiteSpace(executionId) && generation > 0;
    public bool Equals(BistroBuilderAnimationExecutionHandle other) => generation == other.generation && string.Equals(executionId, other.executionId, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is BistroBuilderAnimationExecutionHandle other && Equals(other);
    public override int GetHashCode() { unchecked { return ((executionId != null ? executionId.GetHashCode() : 0) * 397) ^ generation.GetHashCode(); } }
    public override string ToString() => IsValid ? executionId + "@" + generation : "<invalid>";
}

[Serializable]
public struct BistroBuilderAnimationTargetHandle : IEquatable<BistroBuilderAnimationTargetHandle>
{
    public BistroBuilderAnimationTargetKind kind;
    public string targetId;
    public long generation;
    public BistroBuilderAnimationTargetHandle(BistroBuilderAnimationTargetKind targetKind, string id, long value) { kind = targetKind; targetId = id ?? string.Empty; generation = value; }
    public bool IsValid => kind != BistroBuilderAnimationTargetKind.None && !string.IsNullOrWhiteSpace(targetId) && generation > 0;
    public bool Equals(BistroBuilderAnimationTargetHandle other) => kind == other.kind && generation == other.generation && string.Equals(targetId, other.targetId, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is BistroBuilderAnimationTargetHandle other && Equals(other);
    public override int GetHashCode() { unchecked { return (((int)kind * 397) ^ (targetId != null ? targetId.GetHashCode() : 0)) * 397 ^ generation.GetHashCode(); } }
}

[Serializable]
public sealed class BistroBuilderAnimationExecutionRequest
{
    public string ownerId = string.Empty;
    public string actorId = string.Empty;
    public BistroBuilderInteractionGrantHandle logicalGrant;
    public string spatialLeaseId = string.Empty;
    public BistroBuilderInteractionFamily family;
    public BistroBuilderInteractionOperation operation;
    public BistroBuilderAnimationTargetHandle target;
    public string slotId = string.Empty;
    public string requestedMotionId = string.Empty;
    public string fallbackMotionId = string.Empty;
    [Range(0.5f, 1.5f)] public float tempo = 1f;
    [Range(0f, 1f)] public float commitNormalizedTime = 0.5f;
    [Min(0.1f)] public float commitTimeoutSeconds = 2f;
    [Min(0.1f)] public float watchdogTimeoutSeconds = 8f;
    public bool requiresAuthoritativeCommit = true;
    public bool protectedQualityWindow;
    public BistroBuilderMotionRecoveryMode recoveryMode = BistroBuilderMotionRecoveryMode.SemanticSafePose;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(ownerId)) { error = "Animation request needs ownerId."; return false; }
        if (string.IsNullOrWhiteSpace(actorId)) { error = "Animation request needs actorId."; return false; }
        if (family == BistroBuilderInteractionFamily.None || operation == BistroBuilderInteractionOperation.None) { error = "Animation request needs semantic family/operation."; return false; }
        if (string.IsNullOrWhiteSpace(requestedMotionId) && string.IsNullOrWhiteSpace(fallbackMotionId)) { error = "Animation request needs a semantic motion id or fallback."; return false; }
        if (logicalGrant.IsValid && string.IsNullOrWhiteSpace(logicalGrant.grantId)) { error = "Invalid logical grant handle."; return false; }
        if (commitTimeoutSeconds <= 0f || watchdogTimeoutSeconds <= 0f) { error = "Timeouts must be positive."; return false; }
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderMotionProgress
{
    public BistroBuilderAnimationExecutionHandle handle;
    public BistroBuilderMotionExecutionState state;
    public BistroBuilderInteractionPhase phase;
    [Range(0f, 1f)] public float normalizedTime;
    public string markerId = string.Empty;
    public BistroBuilderAnimationQualityTier qualityTier = BistroBuilderAnimationQualityTier.Q4;
    public bool protectedQualityWindow;
    public float lastProgressUnscaledTime;
}

[Serializable]
public sealed class BistroBuilderMotionExecutionResult
{
    public BistroBuilderAnimationExecutionHandle handle;
    public BistroBuilderMotionResultCode code;
    public string message = string.Empty;
    public bool committed;
}

[Serializable]
public sealed class BistroBuilderAnimationRehydrationRequest
{
    public string actorId = string.Empty;
    public BistroBuilderInteractionFamily semanticFamily;
    public BistroBuilderInteractionOperation semanticOperation;
    public BistroBuilderAnimationTargetHandle target;
    public bool committed;
    public BistroBuilderMotionRecoveryMode recoveryMode = BistroBuilderMotionRecoveryMode.SemanticSafePose;
}

public interface IBBCharacterAnimationService
{
    event Action<BistroBuilderMotionProgress> ProgressChanged;
    event Action<BistroBuilderAnimationExecutionHandle> AuthoritativeCommitRequested;
    event Action<BistroBuilderMotionExecutionResult> ExecutionFinished;
    bool TryStart(BistroBuilderAnimationExecutionRequest request, out BistroBuilderAnimationExecutionHandle handle, out string error);
    bool TryInterrupt(BistroBuilderAnimationExecutionHandle handle, string reason);
    bool TryAcknowledgeAuthoritativeCommit(BistroBuilderAnimationExecutionHandle handle, bool accepted, string reason);
    bool TryGetProgress(BistroBuilderAnimationExecutionHandle handle, out BistroBuilderMotionProgress progress);
    bool TryRehydrate(BistroBuilderAnimationRehydrationRequest request, out string error);
}
