using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime orchestrator V1. Gameplay/Interaction/BBSIS/Nav decide what may happen;
/// this service only resolves and presents semantic motion.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderCharacterAnimationServiceV1 : MonoBehaviour, IBBCharacterAnimationService
{
    [SerializeField] private BistroBuilderMotionRecipeCatalog recipeCatalog;
    [SerializeField] private BistroBuilderPerceptualAnimationBudgeter budgeter;
    [SerializeField, Range(0f, 1f)] private float defaultPerceptualImportance = 0.65f;
    [SerializeField, Min(1)] private int variantHistoryDepth = 3;

    private readonly Dictionary<string, BistroBuilderAnimationActorBinding> actors = new Dictionary<string, BistroBuilderAnimationActorBinding>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderAnimationTargetBinding> targets = new Dictionary<string, BistroBuilderAnimationTargetBinding>(StringComparer.Ordinal);
    private readonly Dictionary<string, Session> sessions = new Dictionary<string, Session>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderAnimationExecutionHandle> activeByActor = new Dictionary<string, BistroBuilderAnimationExecutionHandle>(StringComparer.Ordinal);
    private BistroBuilderMotionVariantScheduler variantScheduler;
    private long nextGeneration = 1;

    public event Action<BistroBuilderMotionProgress> ProgressChanged;
    public event Action<BistroBuilderAnimationExecutionHandle> AuthoritativeCommitRequested;
    public event Action<BistroBuilderMotionExecutionResult> ExecutionFinished;

    public int ActiveExecutionCount => sessions.Count;

#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderMotionRecipeCatalog configuredCatalog, BistroBuilderPerceptualAnimationBudgeter configuredBudgeter)
    {
        recipeCatalog = configuredCatalog;
        budgeter = configuredBudgeter;
    }
#endif
    private void Awake()
    {
        variantScheduler = new BistroBuilderMotionVariantScheduler(variantHistoryDepth);
        RefreshRegistry();
    }

    public void RefreshRegistry()
    {
        actors.Clear();
        targets.Clear();
        BistroBuilderAnimationActorBinding[] actorBindings = FindObjectsByType<BistroBuilderAnimationActorBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < actorBindings.Length; i++)
        {
            BistroBuilderAnimationActorBinding binding = actorBindings[i];
            if (binding != null && !string.IsNullOrWhiteSpace(binding.ActorId)) actors[binding.ActorId] = binding;
        }
        BistroBuilderAnimationTargetBinding[] targetBindings = FindObjectsByType<BistroBuilderAnimationTargetBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < targetBindings.Length; i++)
        {
            BistroBuilderAnimationTargetBinding binding = targetBindings[i];
            if (binding != null && !string.IsNullOrWhiteSpace(binding.TargetId)) targets[binding.TargetId] = binding;
        }
    }

    public bool TryStart(BistroBuilderAnimationExecutionRequest request, out BistroBuilderAnimationExecutionHandle handle, out string error)
    {
        handle = default;
        error = string.Empty;
        if (request == null || !request.Validate(out error)) return false;
        if (!actors.TryGetValue(request.actorId, out BistroBuilderAnimationActorBinding actor) || actor == null)
        {
            RefreshRegistry();
            if (!actors.TryGetValue(request.actorId, out actor) || actor == null) { error = "Animation actor is not registered: " + request.actorId; return false; }
        }
        if (activeByActor.TryGetValue(request.actorId, out BistroBuilderAnimationExecutionHandle existing) && existing.IsValid)
        {
            error = "Animation actor already has an active execution: " + existing;
            return false;
        }

        BistroBuilderAnimationTargetBinding target = null;
        if (request.target.IsValid)
        {
            if (!IsMatchingTarget(request.target, out target))
            {
                RefreshRegistry();
                if (!IsMatchingTarget(request.target, out target))
                {
                    error = "Animation target handle is stale or unavailable: " + request.target.targetId;
                    return false;
                }
            }
        }

        BistroBuilderMotionRecipe recipe = null;
        if (recipeCatalog == null || (!recipeCatalog.TryResolve(request.requestedMotionId, out recipe) && !recipeCatalog.TryResolve(request.fallbackMotionId, out recipe)))
        {
            error = "No certified Motion Recipe resolves " + request.requestedMotionId + " / " + request.fallbackMotionId;
            return false;
        }

        long generation = NextGeneration();
        handle = new BistroBuilderAnimationExecutionHandle(Guid.NewGuid().ToString("N"), generation);
        BistroBuilderAnimationQualityTier quality = budgeter != null
            ? budgeter.Evaluate(handle, defaultPerceptualImportance, request.protectedQualityWindow)
            : BistroBuilderAnimationQualityTier.Q4;
        if (variantScheduler == null) variantScheduler = new BistroBuilderMotionVariantScheduler(variantHistoryDepth);
        if (!variantScheduler.TrySelect(recipe, request.actorId, quality, handle.executionId, out BistroBuilderMotionRecipeVariant variant))
        {
            error = "Motion Recipe has no runtime variant compatible with current quality tier: " + recipe.MotionId;
            return false;
        }
        if (actor.RecipePlayer == null)
        {
            error = "Animation actor has no BistroBuilderMotionRecipePlayerV1: " + request.actorId;
            return false;
        }

        var session = new Session
        {
            handle = handle,
            request = request,
            actor = actor,
            target = target,
            recipe = recipe,
            variant = variant,
            progress = new BistroBuilderMotionProgress
            {
                handle = handle,
                state = BistroBuilderMotionExecutionState.Pending,
                phase = BistroBuilderInteractionPhase.Acquire,
                normalizedTime = 0f,
                qualityTier = quality,
                protectedQualityWindow = request.protectedQualityWindow,
                lastProgressUnscaledTime = Time.unscaledTime
            },
            startedAt = Time.unscaledTime,
            watchdogDeadline = Time.unscaledTime + Mathf.Max(0.1f, request.watchdogTimeoutSeconds)
        };
        sessions.Add(handle.ToString(), session);
        activeByActor[request.actorId] = handle;
        budgeter?.Assign(handle, quality);
        if (request.protectedQualityWindow && budgeter != null) budgeter.Protect(handle, Mathf.Max(0.5f, request.watchdogTimeoutSeconds));
        session.coroutine = StartCoroutine(RunSession(session));
        return true;
    }

    public bool TryInterrupt(BistroBuilderAnimationExecutionHandle handle, string reason)
    {
        if (!TryGetSession(handle, out Session session)) return false;
        if (session.progress.state == BistroBuilderMotionExecutionState.Completed || session.progress.state == BistroBuilderMotionExecutionState.Cancelled || session.progress.state == BistroBuilderMotionExecutionState.Failed) return false;
        session.interruptRequested = true;
        session.interruptReason = reason ?? string.Empty;
        return true;
    }

    public bool TryAcknowledgeAuthoritativeCommit(BistroBuilderAnimationExecutionHandle handle, bool accepted, string reason)
    {
        if (!TryGetSession(handle, out Session session) || !session.commitRequested || session.commitAcknowledged) return false;
        session.commitAcknowledged = true;
        session.commitAccepted = accepted;
        session.commitReason = reason ?? string.Empty;
        return true;
    }

    public bool TryGetProgress(BistroBuilderAnimationExecutionHandle handle, out BistroBuilderMotionProgress progress)
    {
        progress = null;
        if (!TryGetSession(handle, out Session session)) return false;
        progress = CloneProgress(session.progress);
        return true;
    }

    public bool TryRehydrate(BistroBuilderAnimationRehydrationRequest request, out string error)
    {
        error = string.Empty;
        if (request == null || string.IsNullOrWhiteSpace(request.actorId)) { error = "Rehydration needs actorId."; return false; }
        if (!actors.TryGetValue(request.actorId, out BistroBuilderAnimationActorBinding actor) || actor == null)
        {
            RefreshRegistry();
            if (!actors.TryGetValue(request.actorId, out actor) || actor == null) { error = "Rehydration actor unavailable: " + request.actorId; return false; }
        }
        if (activeByActor.TryGetValue(request.actorId, out BistroBuilderAnimationExecutionHandle active) && active.IsValid && TryGetSession(active, out Session activeSession))
        {
            if (activeSession.coroutine != null) StopCoroutine(activeSession.coroutine);
            bool committed = activeSession.commitAcknowledged && activeSession.commitAccepted;
            Finish(activeSession, BistroBuilderMotionResultCode.Cancelled, "Load rehydration.", committed);
        }
        actor.RigAdapter?.ClearInteractionTargetsImmediate();
        if (actor.RecipePlayer != null) actor.RecipePlayer.ReturnToBaselinePose();
        return true;
    }

    private IEnumerator RunSession(Session session)
    {
        SetProgress(session, BistroBuilderMotionExecutionState.Running, BistroBuilderInteractionPhase.Acquire, 0f, string.Empty);
        yield return null;
        SetProgress(session, BistroBuilderMotionExecutionState.Running, BistroBuilderInteractionPhase.Approach, 0f, string.Empty);
        yield return null;
        SetProgress(session, BistroBuilderMotionExecutionState.Running, BistroBuilderInteractionPhase.Align, 0f, string.Empty);
        ApplyPresentationTargets(session);
        yield return null;

        if (session.actor.RecipePlayer == null || !session.actor.RecipePlayer.TryPlay(session.recipe, session.variant, session.request.tempo, out session.duration))
        {
            Finish(session, BistroBuilderMotionResultCode.MotionUnavailable, "Motion Recipe player rejected runtime variant.", false);
            yield break;
        }

        session.duration = Mathf.Max(0.05f, session.duration);
        session.motionStartedAt = Time.unscaledTime;
        session.watchdogDeadline = Mathf.Max(session.watchdogDeadline, session.motionStartedAt + session.duration + session.request.commitTimeoutSeconds + 1f);
        float commitAt = Mathf.Clamp01(session.request.commitNormalizedTime);
        bool committed = !session.request.requiresAuthoritativeCommit;

        while (true)
        {
            float elapsed = Time.unscaledTime - session.motionStartedAt;
            float normalized = Mathf.Clamp01(elapsed / session.duration);
            string marker = ResolveCrossedMarker(session.recipe, session.progress.normalizedTime, normalized);
            SetProgress(session, session.commitRequested && !session.commitAcknowledged ? BistroBuilderMotionExecutionState.AwaitingAuthoritativeCommit : BistroBuilderMotionExecutionState.Running, BistroBuilderInteractionPhase.Operate, normalized, marker);

            if (session.interruptRequested && CanInterruptNow(session, normalized, committed))
            {
                yield return RecoverAndFinish(session, BistroBuilderMotionResultCode.Cancelled, string.IsNullOrWhiteSpace(session.interruptReason) ? "Animation interrupted." : session.interruptReason, committed);
                yield break;
            }

            if (!committed && normalized >= commitAt)
            {
                if (!session.commitRequested)
                {
                    session.commitRequested = true;
                    session.commitRequestedAt = Time.unscaledTime;
                    SetProgress(session, BistroBuilderMotionExecutionState.AwaitingAuthoritativeCommit, BistroBuilderInteractionPhase.Operate, normalized, "commit.frontier");
                    AuthoritativeCommitRequested?.Invoke(session.handle);
                }
                if (session.commitAcknowledged)
                {
                    if (!session.commitAccepted)
                    {
                        yield return RecoverAndFinish(session, BistroBuilderMotionResultCode.CommitRejected, string.IsNullOrWhiteSpace(session.commitReason) ? "Authoritative commit rejected." : session.commitReason, false);
                        yield break;
                    }
                    committed = true;
                }
                else if (Time.unscaledTime - session.commitRequestedAt >= session.request.commitTimeoutSeconds)
                {
                    yield return RecoverAndFinish(session, BistroBuilderMotionResultCode.CommitTimeout, "Authoritative commit timed out.", false);
                    yield break;
                }
            }

            if (Time.unscaledTime >= session.watchdogDeadline)
            {
                yield return RecoverAndFinish(session, BistroBuilderMotionResultCode.WatchdogTimeout, "Animation watchdog expired.", committed);
                yield break;
            }

            if (normalized >= 1f && (committed || !session.request.requiresAuthoritativeCommit)) break;
            yield return null;
        }

        SetProgress(session, BistroBuilderMotionExecutionState.Running, BistroBuilderInteractionPhase.Disengage, 1f, string.Empty);
        ClearPresentationTargets(session);
        SetProgress(session, BistroBuilderMotionExecutionState.Running, BistroBuilderInteractionPhase.Settle, 1f, string.Empty);
        Finish(session, BistroBuilderMotionResultCode.Completed, "Completed.", committed);
    }

    private IEnumerator RecoverAndFinish(Session session, BistroBuilderMotionResultCode code, string message, bool committed)
    {
        SetProgress(session, BistroBuilderMotionExecutionState.Recovering, BistroBuilderInteractionPhase.Recover, session.progress.normalizedTime, "recovery");
        ClearPresentationTargets(session);
        if (session.actor.RecipePlayer != null)
        {
            bool preserveCommitted = committed && session.request.recoveryMode == BistroBuilderMotionRecoveryMode.PreserveCommittedPose;
            if (!preserveCommitted) session.actor.RecipePlayer.ReturnToBaselinePose();
        }
        yield return null;
        Finish(session, code, message, committed);
    }

    private void ApplyPresentationTargets(Session session)
    {
        if (session.actor == null || session.actor.RigAdapter == null || session.target == null) return;
        if (!session.target.TryResolveSlot(session.request.slotId, session.request.family, out BistroBuilderAnimationTargetSlot slot) || slot == null) return;
        if (slot.RightHandTarget != null) session.actor.RigAdapter.SetRightHandTarget(slot.RightHandTarget, 1f);
        if (slot.LeftHandTarget != null) session.actor.RigAdapter.SetLeftHandTarget(slot.LeftHandTarget, 1f);
        if (slot.LookTarget != null) session.actor.RigAdapter.SetLookTarget(slot.LookTarget, 0.35f);
    }

    private void ClearPresentationTargets(Session session)
    {
        session.actor?.RigAdapter?.ClearInteractionTargets();
    }

    private bool CanInterruptNow(Session session, float normalized, bool committed)
    {
        switch (session.recipe.InterruptPolicy)
        {
            case BistroBuilderMotionInterruptPolicy.Immediate: return true;
            case BistroBuilderMotionInterruptPolicy.AfterCommit: return committed;
            case BistroBuilderMotionInterruptPolicy.NonInterruptible: return false;
            default:
                float safe = session.recipe.FindMarkerTime01("interrupt.safe", 0.80f);
                return normalized >= safe || (!committed && normalized < Mathf.Clamp01(session.request.commitNormalizedTime) * 0.25f);
        }
    }

    private static string ResolveCrossedMarker(BistroBuilderMotionRecipe recipe, float previous, float current)
    {
        if (recipe == null || recipe.SyncPoints == null) return string.Empty;
        for (int i = 0; i < recipe.SyncPoints.Count; i++)
        {
            BistroBuilderMotionSyncPoint marker = recipe.SyncPoints[i];
            if (marker != null && marker.NormalizedTime > previous && marker.NormalizedTime <= current) return marker.MarkerId;
        }
        return string.Empty;
    }

    private void SetProgress(Session session, BistroBuilderMotionExecutionState state, BistroBuilderInteractionPhase phase, float normalized, string marker)
    {
        session.progress.state = state;
        session.progress.phase = phase;
        session.progress.normalizedTime = Mathf.Clamp01(normalized);
        session.progress.markerId = marker ?? string.Empty;
        session.progress.lastProgressUnscaledTime = Time.unscaledTime;
        ProgressChanged?.Invoke(CloneProgress(session.progress));
    }

    private void Finish(Session session, BistroBuilderMotionResultCode code, string message, bool committed)
    {
        if (!sessions.ContainsKey(session.handle.ToString())) return;
        ClearPresentationTargets(session);
        if (code == BistroBuilderMotionResultCode.Completed)
        {
            session.progress.state = BistroBuilderMotionExecutionState.Completed;
            session.progress.phase = BistroBuilderInteractionPhase.Completed;
        }
        else if (code == BistroBuilderMotionResultCode.Cancelled)
        {
            session.progress.state = BistroBuilderMotionExecutionState.Cancelled;
            session.progress.phase = BistroBuilderInteractionPhase.Cancelled;
        }
        else
        {
            session.progress.state = BistroBuilderMotionExecutionState.Failed;
            session.progress.phase = BistroBuilderInteractionPhase.Failed;
        }
        session.progress.lastProgressUnscaledTime = Time.unscaledTime;
        ProgressChanged?.Invoke(CloneProgress(session.progress));
        sessions.Remove(session.handle.ToString());
        activeByActor.Remove(session.request.actorId);
        budgeter?.Release(session.handle);
        ExecutionFinished?.Invoke(new BistroBuilderMotionExecutionResult { handle = session.handle, code = code, message = message ?? string.Empty, committed = committed });
    }

    private bool IsMatchingTarget(BistroBuilderAnimationTargetHandle handle, out BistroBuilderAnimationTargetBinding target)
    {
        target = null;
        return handle.IsValid &&
               targets.TryGetValue(handle.targetId, out target) &&
               target != null &&
               target.Generation == handle.generation &&
               target.TargetKind == handle.kind;
    }
    private bool TryGetSession(BistroBuilderAnimationExecutionHandle handle, out Session session)
    {
        session = null;
        return handle.IsValid && sessions.TryGetValue(handle.ToString(), out session) && session != null && session.handle.Equals(handle);
    }

    private long NextGeneration()
    {
        if (nextGeneration == long.MaxValue) throw new InvalidOperationException("Animation execution generation space exhausted.");
        return nextGeneration++;
    }

    private static BistroBuilderMotionProgress CloneProgress(BistroBuilderMotionProgress source)
    {
        return new BistroBuilderMotionProgress
        {
            handle = source.handle,
            state = source.state,
            phase = source.phase,
            normalizedTime = source.normalizedTime,
            markerId = source.markerId,
            qualityTier = source.qualityTier,
            protectedQualityWindow = source.protectedQualityWindow,
            lastProgressUnscaledTime = source.lastProgressUnscaledTime
        };
    }

    private sealed class Session
    {
        public BistroBuilderAnimationExecutionHandle handle;
        public BistroBuilderAnimationExecutionRequest request;
        public BistroBuilderAnimationActorBinding actor;
        public BistroBuilderAnimationTargetBinding target;
        public BistroBuilderMotionRecipe recipe;
        public BistroBuilderMotionRecipeVariant variant;
        public BistroBuilderMotionProgress progress;
        public float startedAt;
        public float motionStartedAt;
        public float duration;
        public float watchdogDeadline;
        public bool interruptRequested;
        public string interruptReason;
        public bool commitRequested;
        public float commitRequestedAt;
        public bool commitAcknowledged;
        public bool commitAccepted;
        public string commitReason;
        public Coroutine coroutine;
    }
}
