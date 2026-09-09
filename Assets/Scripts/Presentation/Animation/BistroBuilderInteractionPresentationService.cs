using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad ÃƒÂºnica de presentaciÃƒÂ³n de interacciones coordinadas.
/// Recibe planes ya resueltos y nunca decide intenciÃƒÂ³n de gameplay ni
/// accesibilidad espacial.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderInteractionPresentationService : MonoBehaviour
{
    [SerializeField] private BistroBuilderMotionCatalog defaultMotionCatalog;
    [SerializeField, Min(0.01f)] private float defaultAlignDuration = 0.12f;
    [SerializeField, Min(0.1f)] private float watchdogGraceSeconds = 1.5f;

    private readonly Dictionary<int, BistroBuilderInteractionHandle> activeByActor =
        new Dictionary<int, BistroBuilderInteractionHandle>();
    private readonly Dictionary<string, BistroBuilderInteractionHandle> activeByHandle =
        new Dictionary<string, BistroBuilderInteractionHandle>(StringComparer.Ordinal);

    public event Action<BistroBuilderInteractionHandle, BistroBuilderResolvedInteractionPlan> CommitRequested;
    public event Action<BistroBuilderInteractionHandle, BistroBuilderResolvedInteractionPlan> InteractionFinished;

    public int ActiveInteractionCount => activeByHandle.Count;

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (defaultMotionCatalog != null && !defaultMotionCatalog.ValidateConfiguration(out error))
            return false;
        return true;
    }

    public bool TryStartResolvedInteraction(
        BistroBuilderResolvedInteractionPlan plan,
        out BistroBuilderInteractionHandle handle,
        out string error)
    {
        handle = null;
        error = string.Empty;
        if (plan == null || !plan.Validate(out error)) return false;

        int actorKey = plan.actor.GetInstanceID();
        if (activeByActor.TryGetValue(actorKey, out BistroBuilderInteractionHandle existing) &&
            existing != null && !existing.IsTerminal)
        {
            error = "El actor ya tiene una interacciÃƒÂ³n corporal activa.";
            return false;
        }

        handle = new BistroBuilderInteractionHandle(plan.EffectiveInteractionId, plan.EffectiveOwnerId);
        activeByActor[actorKey] = handle;
        activeByHandle[handle.HandleId] = handle;
        StartCoroutine(RunSafe(plan, handle, actorKey));
        return true;
    }

    public bool TryCancel(string handleId)
    {
        if (string.IsNullOrWhiteSpace(handleId) ||
            !activeByHandle.TryGetValue(handleId, out BistroBuilderInteractionHandle handle) ||
            handle == null)
            return false;
        return handle.RequestCancel();
    }

    public bool TryGetHandle(string handleId, out BistroBuilderInteractionHandle handle) =>
        activeByHandle.TryGetValue(handleId ?? string.Empty, out handle);

    private IEnumerator RunSafe(
        BistroBuilderResolvedInteractionPlan plan,
        BistroBuilderInteractionHandle handle,
        int actorKey)
    {
        IEnumerator routine = RunCore(plan, handle);
        bool moveNext = true;
        while (moveNext)
        {
            object current = null;
            try
            {
                moveNext = routine.MoveNext();
                if (moveNext) current = routine.Current;
            }
            catch (Exception exception)
            {
                handle.Fail(
                    BistroBuilderInteractionFailureKind.RuntimeException,
                    exception.GetType().Name + ": " + exception.Message);
                Debug.LogException(exception, this);
                moveNext = false;
            }

            if (moveNext) yield return current;
        }

        if (!handle.IsTerminal)
            handle.Fail(BistroBuilderInteractionFailureKind.RuntimeException,
                "La interacciÃƒÂ³n terminÃƒÂ³ sin estado terminal.");

        BistroBuilderCharacterAnimationDriver driver =
            plan.actor != null ? plan.actor.GetComponent<BistroBuilderCharacterAnimationDriver>() : null;
        BistroBuilderCharacterRigAdapter finalRig =
            plan.actor != null ? plan.actor.GetComponent<BistroBuilderCharacterRigAdapter>() : null;
        finalRig?.ClearInteractionTargetsImmediate();
        if (driver != null && !driver.ReturnToBaselinePose())
            driver.StopTransientMotion();
        activeByActor.Remove(actorKey);
        activeByHandle.Remove(handle.HandleId);
        InteractionFinished?.Invoke(handle, plan);
    }

    private IEnumerator RunCore(
        BistroBuilderResolvedInteractionPlan plan,
        BistroBuilderInteractionHandle handle)
    {
        handle.SetRunning(BistroBuilderInteractionPhase.Acquire);
        yield return null;

        if (handle.CancellationRequested)
        {
            handle.Cancel(BistroBuilderInteractionFailureKind.CancelledBeforeCommit,
                "Cancelada antes de comenzar.");
            yield break;
        }

        handle.SetRunning(BistroBuilderInteractionPhase.Approach);
        yield return null;

        bool standingUpFromSeat =
            plan.family == BistroBuilderInteractionFamily.Seat &&
            plan.operation == BistroBuilderInteractionOperation.Stand;

        // Al levantarse, el actor parte del SeatFrame estable; no debe ser
        // teletransportado al Approach/Exit antes de ejecutar Stand.
        if (!standingUpFromSeat && plan.interactionFrame != null)
        {
            handle.SetRunning(BistroBuilderInteractionPhase.Align);
            bool alignAccepted = IsAlignmentWithinBudget(plan, out string alignmentError);
            if (!alignAccepted)
            {
                handle.Fail(BistroBuilderInteractionFailureKind.AlignmentOutOfBudget, alignmentError);
                yield break;
            }
            yield return AlignActor(plan);
        }

        switch (plan.family)
        {
            case BistroBuilderInteractionFamily.Seat:
                yield return RunSeat(plan, handle);
                break;
            case BistroBuilderInteractionFamily.Portal:
                yield return RunPortal(plan, handle);
                break;
            case BistroBuilderInteractionFamily.Transfer:
                yield return RunTransfer(plan, handle);
                break;
            default:
                handle.Fail(BistroBuilderInteractionFailureKind.InvalidPlan,
                    "La familia todavÃƒÂ­a no tiene presenter runtime: " + plan.family);
                break;
        }
    }

    private IEnumerator RunSeat(
        BistroBuilderResolvedInteractionPlan plan,
        BistroBuilderInteractionHandle handle)
    {
        RestaurantSeat seat = plan.seat;
        if (seat == null)
        {
            handle.Fail(BistroBuilderInteractionFailureKind.TargetUnavailable, "Falta RestaurantSeat.");
            yield break;
        }

        if (!string.Equals(seat.ReservationOwnerId, plan.EffectiveOwnerId, StringComparison.Ordinal))
        {
            handle.Fail(BistroBuilderInteractionFailureKind.SpatialClaimMissing,
                "Animation recibiÃƒÂ³ Seat sin reserva espacial/gameplay del mismo owner.");
            yield break;
        }

        if (plan.operation == BistroBuilderInteractionOperation.Sit)
        {
            yield return RunSit(plan, handle, seat);
            yield break;
        }
        if (plan.operation == BistroBuilderInteractionOperation.Stand)
        {
            yield return RunStand(plan, handle, seat);
            yield break;
        }

        handle.Fail(BistroBuilderInteractionFailureKind.InvalidPlan,
            "Seat sÃƒÂ³lo acepta Sit/Stand en el vertical slice.");
    }

    private IEnumerator RunSit(
        BistroBuilderResolvedInteractionPlan plan,
        BistroBuilderInteractionHandle handle,
        RestaurantSeat seat)
    {
        handle.SetRunning(BistroBuilderInteractionPhase.Engage);
        if (!seat.TryPullOut())
        {
            handle.Fail(BistroBuilderInteractionFailureKind.TargetUnavailable,
                "La silla no pudo iniciar PullOut.");
            yield break;
        }

        yield return WaitForSeatState(
            seat,
            RestaurantSeatOperationalState.ReadyForCustomer,
            plan.operationTimeoutSeconds,
            handle);
        if (handle.IsTerminal) yield break;

        float duration = BeginCharacterMotion(plan);
        float commitAt = Mathf.Clamp01(plan.commitNormalizedTime) * duration;
        handle.SetRunning(BistroBuilderInteractionPhase.Operate);
        yield return WaitCancelableSeconds(commitAt, handle, false);
        if (handle.CancellationRequested)
        {
            handle.SetRunning(BistroBuilderInteractionPhase.Recover);
            seat.TryReturnToParked(false);
            yield return WaitForSeatStateNoHandle(
                seat,
                RestaurantSeatOperationalState.Parked,
                plan.operationTimeoutSeconds);
            handle.Cancel(BistroBuilderInteractionFailureKind.CancelledBeforeCommit,
                "Sit cancelado antes del Commit Frontier; silla reconciliada a Parked.");
            yield break;
        }

        yield return AwaitCommit(plan, handle);
        if (handle.IsTerminal)
        {
            seat.TryReturnToParked(false);
            yield return WaitForSeatStateNoHandle(
                seat,
                RestaurantSeatOperationalState.Parked,
                plan.operationTimeoutSeconds);
            yield break;
        }
        if (!seat.TrySetOccupied())
        {
            handle.Fail(BistroBuilderInteractionFailureKind.TargetUnavailable,
                "La silla no pudo estabilizarse como Occupied tras commit.");
            yield break;
        }
        handle.MarkCommitted();

        yield return WaitForSeatState(
            seat,
            RestaurantSeatOperationalState.Occupied,
            plan.operationTimeoutSeconds,
            handle);
        if (handle.IsTerminal) yield break;

        float remaining = Mathf.Max(0f, duration - commitAt);
        yield return WaitCancelableSeconds(remaining, handle, true);
        if (handle.CancellationRequested)
        {
            handle.SetRunning(BistroBuilderInteractionPhase.Recover);
            SnapActorHorizontalToFrame(plan.actor, plan.seatFrame);
            handle.Complete(true);
            yield break;
        }

        handle.SetRunning(BistroBuilderInteractionPhase.Settle);
        SnapActorHorizontalToFrame(plan.actor, plan.seatFrame);
        handle.Complete();
    }

    private IEnumerator RunStand(
        BistroBuilderResolvedInteractionPlan plan,
        BistroBuilderInteractionHandle handle,
        RestaurantSeat seat)
    {
        handle.SetRunning(BistroBuilderInteractionPhase.Engage);
        if (!seat.TryBeginLeaving())
        {
            handle.Fail(BistroBuilderInteractionFailureKind.TargetUnavailable,
                "La silla no pudo iniciar CustomerLeaving.");
            yield break;
        }

        yield return WaitForSeatState(
            seat,
            RestaurantSeatOperationalState.ReadyForCustomer,
            plan.operationTimeoutSeconds,
            handle);
        if (handle.IsTerminal) yield break;

        float duration = BeginCharacterMotion(plan);
        float commitAt = Mathf.Clamp01(plan.commitNormalizedTime) * duration;
        handle.SetRunning(BistroBuilderInteractionPhase.Operate);
        yield return WaitCancelableSeconds(commitAt, handle, false);
        if (handle.CancellationRequested)
        {
            handle.SetRunning(BistroBuilderInteractionPhase.Recover);
            seat.TrySetOccupied();
            yield return WaitForSeatStateNoHandle(
                seat,
                RestaurantSeatOperationalState.Occupied,
                plan.operationTimeoutSeconds);
            SnapActorHorizontalToFrame(plan.actor, plan.seatFrame);
            handle.Cancel(BistroBuilderInteractionFailureKind.CancelledBeforeCommit,
                "Stand cancelado antes del Commit Frontier; silla reconciliada a Occupied.");
            yield break;
        }

        yield return AwaitCommit(plan, handle);
        if (handle.IsTerminal)
        {
            seat.TrySetOccupied();
            yield return WaitForSeatStateNoHandle(
                seat,
                RestaurantSeatOperationalState.Occupied,
                plan.operationTimeoutSeconds);
            SnapActorHorizontalToFrame(plan.actor, plan.seatFrame);
            yield break;
        }
        handle.MarkCommitted();

        float remaining = Mathf.Max(0f, duration - commitAt);
        yield return WaitCancelableSeconds(remaining, handle, true);
        seat.TryReturnToParked(false);
        yield return WaitForSeatState(
            seat,
            RestaurantSeatOperationalState.Parked,
            plan.operationTimeoutSeconds,
            handle);
        if (handle.IsTerminal) yield break;

        Transform exitFrame = plan.exitFrame != null ? plan.exitFrame : plan.interactionFrame;
        if (handle.CancellationRequested)
        {
            handle.SetRunning(BistroBuilderInteractionPhase.Recover);
            SnapActorToFrame(plan.actor, exitFrame);
            handle.Complete(true);
            yield break;
        }
        handle.SetRunning(BistroBuilderInteractionPhase.Settle);
        SnapActorToFrame(plan.actor, exitFrame);
        handle.Complete();
    }

    private IEnumerator RunPortal(
        BistroBuilderResolvedInteractionPlan plan,
        BistroBuilderInteractionHandle handle)
    {
        BistroBuilderNavigableDoor door = plan.door;
        bool targetOpen = plan.operation == BistroBuilderInteractionOperation.Open;
        if (!targetOpen && plan.operation != BistroBuilderInteractionOperation.Close)
        {
            handle.Fail(BistroBuilderInteractionFailureKind.InvalidPlan,
                "Portal solo acepta Open/Close.");
            yield break;
        }

        BistroBuilderCharacterRigAdapter rigAdapter = plan.actor != null
            ? plan.actor.GetComponent<BistroBuilderCharacterRigAdapter>()
            : null;
        Transform rightTarget = plan.rightHandTarget != null
            ? plan.rightHandTarget
            : plan.transferTarget;
        Transform leftTarget = plan.leftHandTarget;
        Transform lookTarget = plan.lookTarget != null
            ? plan.lookTarget
            : rightTarget;

        if (door.IsOpen == targetOpen && !door.IsMoving)
        {
            rigAdapter?.ClearInteractionTargets();
            handle.SetRunning(BistroBuilderInteractionPhase.Settle);
            handle.Complete();
            yield break;
        }

        if (rigAdapter != null)
        {
            if (rightTarget != null) rigAdapter.SetRightHandTarget(rightTarget, 1f);
            if (leftTarget != null) rigAdapter.SetLeftHandTarget(leftTarget, 1f);
            if (lookTarget != null) rigAdapter.SetLookTarget(lookTarget, 0.35f);
        }

        float duration = BeginCharacterMotion(plan);
        float commitAt = Mathf.Clamp01(plan.commitNormalizedTime) * duration;
        handle.SetRunning(BistroBuilderInteractionPhase.Operate);
        yield return WaitCancelableSeconds(commitAt, handle, false);
        if (handle.CancellationRequested)
        {
            rigAdapter?.ClearInteractionTargets();
            handle.Cancel(BistroBuilderInteractionFailureKind.CancelledBeforeCommit,
                "Portal cancelado antes del Commit Frontier.");
            yield break;
        }

        BistroBuilderDoorSpatialAdapter spatialAdapter =
            door.GetComponent<BistroBuilderDoorSpatialAdapter>();
        bool spatialReservationHeld = false;
        if (spatialAdapter != null)
        {
            float reservationDuration = Mathf.Max(
                plan.operationTimeoutSeconds,
                duration - commitAt + 0.75f);
            if (!spatialAdapter.TryReserveMotion(
                    plan.EffectiveOwnerId,
                    reservationDuration,
                    out BistroBuilderSpatialLeaseDecision decision))
            {
                rigAdapter?.ClearInteractionTargets();
                handle.Fail(BistroBuilderInteractionFailureKind.SpatialClaimMissing,
                    string.IsNullOrWhiteSpace(decision.message)
                        ? "BBSIS rechazo el barrido dinamico de la puerta."
                        : decision.message);
                yield break;
            }
            spatialReservationHeld = true;
        }

        yield return AwaitCommit(plan, handle);
        if (handle.IsTerminal)
        {
            if (spatialReservationHeld) spatialAdapter.ReleaseMotionReservation();
            rigAdapter?.ClearInteractionTargets();
            yield break;
        }

        if (door.IsOpen != targetOpen && !door.TrySetOpen(targetOpen))
        {
            if (spatialReservationHeld) spatialAdapter.ReleaseMotionReservation();
            rigAdapter?.ClearInteractionTargets();
            handle.Fail(BistroBuilderInteractionFailureKind.TargetUnavailable,
                "La puerta no pudo iniciar el movimiento solicitado.");
            yield break;
        }
        handle.MarkCommitted();

        float deadline = Time.unscaledTime +
            plan.operationTimeoutSeconds + watchdogGraceSeconds;
        while (door.IsMoving || door.IsOpen != targetOpen)
        {
            if (Time.unscaledTime >= deadline)
            {
                if (spatialReservationHeld) spatialAdapter.ReleaseMotionReservation();
                rigAdapter?.ClearInteractionTargets();
                handle.Fail(BistroBuilderInteractionFailureKind.OperationTimeout,
                    "Timeout esperando el estado estable de la puerta.");
                yield break;
            }
            yield return null;
        }

        handle.SetRunning(BistroBuilderInteractionPhase.Disengage);
        // Liberamos IK al comenzar el disengage para que el peso pueda caer a cero
        // mientras termina el clip, evitando conservar la pose de contacto en reposo.
        rigAdapter?.ClearInteractionTargets();
        float remaining = Mathf.Max(0f, duration - commitAt);
        yield return WaitCancelableSeconds(remaining, handle, true);
        if (spatialReservationHeld) spatialAdapter.ReleaseMotionReservation();

        if (handle.CancellationRequested)
        {
            handle.SetRunning(BistroBuilderInteractionPhase.Recover);
            handle.Complete(true);
            yield break;
        }

        handle.SetRunning(BistroBuilderInteractionPhase.Settle);
        handle.Complete();
    }
    private IEnumerator RunTransfer(
        BistroBuilderResolvedInteractionPlan plan,
        BistroBuilderInteractionHandle handle)
    {
        BistroBuilderCharacterRigAdapter rigAdapter = plan.actor != null
            ? plan.actor.GetComponent<BistroBuilderCharacterRigAdapter>()
            : null;
        BistroBuilderCarryPresenter carryPresenter = plan.actor != null
            ? plan.actor.GetComponent<BistroBuilderCarryPresenter>()
            : null;
        BistroBuilderCarryableDescriptor carryable = plan.transferable != null
            ? plan.transferable.GetComponent<BistroBuilderCarryableDescriptor>()
            : null;
        Transform primaryReach = plan.operation == BistroBuilderInteractionOperation.Place && plan.transferTarget != null
            ? plan.transferTarget
            : (carryable != null
                ? carryable.RightGrip
                : (plan.transferTarget != null ? plan.transferTarget : plan.transferable.transform));
        if (rigAdapter != null)
        {
            bool useRight = carryable == null || carryable.UsesRightHandIK;
            bool useLeft = carryable != null && carryable.UsesLeftHandIK;
            if (useRight) rigAdapter.SetRightHandTarget(primaryReach, 1f);
            if (useLeft) rigAdapter.SetLeftHandTarget(carryable.LeftGrip, 1f);
            rigAdapter.SetLookTarget(
                carryable != null ? carryable.LookTarget : primaryReach,
                0.25f);
        }

        float duration = BeginCharacterMotion(plan);
        float commitAt = Mathf.Clamp01(plan.commitNormalizedTime) * duration;
        handle.SetRunning(BistroBuilderInteractionPhase.Operate);
        yield return WaitCancelableSeconds(commitAt, handle, false);

        if (handle.CancellationRequested)
        {
            rigAdapter?.ClearInteractionTargets();
            if (plan.sourceSocket != null)
                plan.transferable.ReconcileVisualTo(plan.sourceSocket);
            handle.Cancel(BistroBuilderInteractionFailureKind.CancelledBeforeCommit,
                "Transfer cancelado antes del Commit Frontier.");
            yield break;
        }
        yield return AwaitCommit(plan, handle);
        if (handle.IsTerminal)
        {
            rigAdapter?.ClearInteractionTargets();
            yield break;
        }

        Transform destination = plan.destinationSocket;
        if (plan.operation == BistroBuilderInteractionOperation.Pickup &&
            carryPresenter != null && carryable != null &&
            carryPresenter.TryResolveSocket(carryable, out Transform carrySocket))
        {
            destination = carrySocket;
        }

        if (destination == null || !plan.transferable.AttachVisual(destination))
        {
            rigAdapter?.ClearInteractionTargets();
            handle.Fail(BistroBuilderInteractionFailureKind.TargetUnavailable,
                "No se pudo reconciliar el objeto con el socket de destino.");
            yield break;
        }
        if (carryable != null)
        {
            plan.transferable.transform.localPosition = carryable.CarriedLocalPosition;
            plan.transferable.transform.localRotation = carryable.CarriedLocalRotation;
        }
        handle.MarkCommitted();
        if (plan.operation == BistroBuilderInteractionOperation.Pickup)
        {
            if (carryPresenter != null)
                carryPresenter.BeginCarryPose(plan.transferable, carryable);
            else
                rigAdapter?.ClearInteractionTargets(false);
        }
        else if (plan.operation == BistroBuilderInteractionOperation.Place)
        {
            carryPresenter?.EndCarryPose();
            rigAdapter?.ClearInteractionTargets();
        }

        float remaining = Mathf.Max(0f, duration - commitAt);
        yield return WaitCancelableSeconds(remaining, handle, true);
        if (handle.CancellationRequested)
        {
            plan.transferable.ReconcileVisualTo(destination);
            if (plan.operation == BistroBuilderInteractionOperation.Pickup)
                carryPresenter?.BeginCarryPose(plan.transferable, carryable);
            else
                carryPresenter?.EndCarryPose();
            handle.SetRunning(BistroBuilderInteractionPhase.Recover);
            handle.Complete(true);
            yield break;
        }
        handle.Complete();
    }
    private IEnumerator AwaitCommit(
        BistroBuilderResolvedInteractionPlan plan,
        BistroBuilderInteractionHandle handle)
    {
        if (!plan.requiresCommitConfirmation)
        {
            handle.MarkCommitted();
            yield break;
        }

        handle.SetAwaitingCommit();
        CommitRequested?.Invoke(handle, plan);
        float deadline = Time.unscaledTime + plan.commitTimeoutSeconds;
        while (handle.CommitState == BistroBuilderInteractionCommitState.AwaitingConfirmation)
        {
            if (handle.CancellationRequested)
            {
                handle.Cancel(BistroBuilderInteractionFailureKind.CancelledBeforeCommit,
                    "Cancelada mientras esperaba confirmaciÃƒÂ³n lÃƒÂ³gica.");
                yield break;
            }
            if (Time.unscaledTime >= deadline)
            {
                handle.Fail(BistroBuilderInteractionFailureKind.CommitTimeout,
                    "Gameplay no confirmÃƒÂ³ el Commit Frontier dentro del timeout.");
                yield break;
            }
            yield return null;
        }

        if (handle.CommitState == BistroBuilderInteractionCommitState.Rejected)
        {
            handle.Fail(BistroBuilderInteractionFailureKind.CommitRejected,
                string.IsNullOrWhiteSpace(handle.FailureReason)
                    ? "Gameplay rechazÃƒÂ³ el commit."
                    : handle.FailureReason);
        }
    }

    private float BeginCharacterMotion(BistroBuilderResolvedInteractionPlan plan)
    {
        BistroBuilderCharacterAnimationDriver driver =
            plan.actor != null ? plan.actor.GetComponent<BistroBuilderCharacterAnimationDriver>() : null;
        if (driver == null) return Mathf.Max(0.05f, plan.fallbackMotionDuration);
        driver.AssignCatalogIfMissing(defaultMotionCatalog);
        bool played = driver.TryPlaySemanticMotion(
            plan.semanticMotionId,
            plan.fallbackMotionId,
            plan.tempo,
            out float duration);
        return played ? Mathf.Max(0.05f, duration) : Mathf.Max(0.05f, duration > 0f ? duration : plan.fallbackMotionDuration);
    }

    private IEnumerator WaitCancelableSeconds(
        float seconds,
        BistroBuilderInteractionHandle handle,
        bool postCommit)
    {
        float deadline = Time.unscaledTime + Mathf.Max(0f, seconds);
        while (Time.unscaledTime < deadline)
        {
            if (handle.CancellationRequested)
            {
                if (!postCommit) yield break;
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator WaitForSeatState(
        RestaurantSeat seat,
        RestaurantSeatOperationalState expected,
        float timeout,
        BistroBuilderInteractionHandle handle)
    {
        float deadline = Time.unscaledTime + Mathf.Max(0.1f, timeout) + watchdogGraceSeconds;
        while (seat != null && seat.OperationalState != expected)
        {
            if (Time.unscaledTime >= deadline)
            {
                handle.Fail(BistroBuilderInteractionFailureKind.OperationTimeout,
                    "Timeout esperando estado de silla " + expected + ".");
                yield break;
            }
            yield return null;
        }
        if (seat == null)
            handle.Fail(BistroBuilderInteractionFailureKind.TargetUnavailable,
                "La silla desapareciÃƒÂ³ durante la interacciÃƒÂ³n.");
    }

    /// <summary>
    /// RecuperaciÃƒÂ³n best-effort posterior a un fallo/cancelaciÃƒÂ³n. No cambia
    /// el resultado del handle ya terminal; sÃƒÂ³lo reconcilia la presentaciÃƒÂ³n.
    /// </summary>
    private IEnumerator WaitForSeatStateNoHandle(
        RestaurantSeat seat,
        RestaurantSeatOperationalState expected,
        float timeout)
    {
        float deadline = Time.unscaledTime + Mathf.Max(0.1f, timeout) + watchdogGraceSeconds;
        while (seat != null && seat.OperationalState != expected && Time.unscaledTime < deadline)
            yield return null;
    }

    private bool IsAlignmentWithinBudget(
        BistroBuilderResolvedInteractionPlan plan,
        out string error)
    {
        error = string.Empty;
        if (plan.actor == null || plan.interactionFrame == null) return true;
        float height = EstimateActorHeight(plan.actor);
        Vector3 delta = plan.interactionFrame.position - plan.actor.transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        float maxDistance = height * plan.adaptationBudget.AssistedRootDistanceRatio;
        float yaw = Mathf.Abs(Vector3.SignedAngle(
            plan.actor.transform.forward,
            plan.interactionFrame.forward,
            Vector3.up));

        if (distance > maxDistance)
        {
            error = "Residual root " + distance.ToString("0.000") +
                    " m supera presupuesto " + maxDistance.ToString("0.000") + " m.";
            return false;
        }
        if (yaw > plan.adaptationBudget.AssistedYawDegrees)
        {
            error = "Residual yaw " + yaw.ToString("0.0") +
                    "Ã‚Â° supera presupuesto " + plan.adaptationBudget.AssistedYawDegrees.ToString("0.0") + "Ã‚Â°.";
            return false;
        }
        return true;
    }

    private IEnumerator AlignActor(BistroBuilderResolvedInteractionPlan plan)
    {
        Transform actor = plan.actor.transform;
        Transform frame = plan.interactionFrame;
        Vector3 startPosition = actor.position;
        Quaternion startRotation = actor.rotation;
        Vector3 targetPosition = new Vector3(frame.position.x, actor.position.y, frame.position.z);
        Vector3 flatForward = frame.forward;
        flatForward.y = 0f;
        Quaternion targetRotation = flatForward.sqrMagnitude > 0.000001f
            ? Quaternion.LookRotation(flatForward.normalized, Vector3.up)
            : startRotation;
        float duration = Mathf.Max(0.01f, defaultAlignDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            actor.position = Vector3.Lerp(startPosition, targetPosition, eased);
            actor.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
            yield return null;
        }
        actor.position = targetPosition;
        actor.rotation = targetRotation;
    }

    private static void SnapActorHorizontalToFrame(GameObject actor, Transform frame)
    {
        if (actor == null || frame == null) return;
        Vector3 position = actor.transform.position;
        position.x = frame.position.x;
        position.z = frame.position.z;
        actor.transform.position = position;
        Vector3 forward = frame.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.000001f)
            actor.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
    private static void SnapActorToFrame(GameObject actor, Transform frame)
    {
        if (actor == null || frame == null) return;
        Vector3 position = frame.position;
        actor.transform.position = position;
        Vector3 forward = frame.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.000001f)
            actor.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private static float EstimateActorHeight(GameObject actor)
    {
        if (actor == null) return 1.75f;
        CharacterController controller = actor.GetComponent<CharacterController>();
        if (controller != null) return Mathf.Max(0.5f, controller.height * actor.transform.lossyScale.y);
        CapsuleCollider capsule = actor.GetComponent<CapsuleCollider>();
        if (capsule != null) return Mathf.Max(0.5f, capsule.height * actor.transform.lossyScale.y);
        Renderer[] renderers = actor.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return Mathf.Max(0.5f, bounds.size.y);
        }
        return 1.75f;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderMotionCatalog catalog)
    {
        defaultMotionCatalog = catalog;
    }
#endif
}

