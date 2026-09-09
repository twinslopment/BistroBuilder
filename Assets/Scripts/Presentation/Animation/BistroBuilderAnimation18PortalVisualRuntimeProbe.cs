#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Gate visual BB18.4: prueba manilla con IK, Commit Frontier, barrido BBSIS,
/// paso por Navigation 17, estados idempotentes y recuperaciÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³n post-commit.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAnimation18PortalVisualRuntimeProbe : MonoBehaviour
{
    [SerializeField] private bool runOnStart;
    [SerializeField] private GameObject humanoidPrefab;
    [SerializeField] private BistroBuilderMotionCatalog motionCatalog;
    [SerializeField] private BistroBuilderPortalAnimationDescriptor portal;
    [SerializeField] private Camera evidenceCamera;

    private BistroBuilderInteractionPresentationService interactions;
    private BistroBuilderNavigationService navigation;
    private BistroBuilderSpatialInteractionService spatial;
    private BistroBuilderNavigableDoor door;
    private BistroBuilderDoorCirculationEnvelope envelope;
    private BistroBuilderDoorSpatialAdapter doorSpatial;
    private GameObject actorA;
    private GameObject actorB;
    private BistroBuilderCharacterAnimationDriver driverA;
    private BistroBuilderCharacterRigAdapter rigA;
    private float minimumHandleError = float.PositiveInfinity;
    private bool observedDynamicSweep;
    private bool observedBbsisMotionLease;
    private bool started;
    private Exception stepException;

    public void ConfigureForEditor(
        bool shouldRun,
        GameObject configuredHumanoid,
        BistroBuilderMotionCatalog configuredCatalog,
        BistroBuilderPortalAnimationDescriptor configuredPortal,
        Camera configuredCamera)
    {
        runOnStart = shouldRun;
        humanoidPrefab = configuredHumanoid;
        motionCatalog = configuredCatalog;
        portal = configuredPortal;
        evidenceCamera = configuredCamera;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        BistroBuilderAnimation18PortalVisualRuntimeProbe[] probes =
            FindObjectsByType<BistroBuilderAnimation18PortalVisualRuntimeProbe>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int i = 0; i < probes.Length; i++)
            probes[i]?.BeginIfRequested();
    }

    private void Start() => BeginIfRequested();

    private void BeginIfRequested()
    {
        if (!runOnStart || started) return;
        started = true;
        Debug.Log("BB18_PORTAL_VISUAL_RUNTIME_START");
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        try
        {
            ResolveAuthorities();
            portal.TryResolveSide(
                BistroBuilderPortalPresentationSide.SideA,
                out Transform approachA,
                out _,
                out _,
                out _);
            actorA = CreateActor("__BB18_PortalActor_A__", approachA.position,
                approachA.rotation, out driverA, out rigA);
        }
        catch (Exception exception)
        {
            Finish(false, exception.Message);
            yield break;
        }

        yield return null;
        yield return ExecuteStep(CalibrateActorGrounding(actorA, driverA));
        if (!ContinueAfterStep()) yield break;
        navigation.RebuildNavigationTopology();
        yield return ExecuteStep(RunBlockedSweepGate());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunPreCommitCancellation());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunNormalOpen());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunAlreadyOpenNoOp());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunConsecutivePassage());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunNormalClose());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunAlreadyClosedNoOp());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunPostCommitCancellation());
        if (!ContinueAfterStep()) yield break;

        if (!observedDynamicSweep || !observedBbsisMotionLease)
        {
            Finish(false, "No se observaron simultÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡neamente Dynamic Sweep y lease BBSIS.");
            yield break;
        }
        if (minimumHandleError > 0.20f)
        {
            Finish(false, "IK de manilla fuera de tolerancia: " +
                minimumHandleError.ToString("0.000") + " m.");
            yield break;
        }
        if (door.IsOpen || door.IsMoving || envelope.IsActive || doorSpatial.HasMotionLease)
        {
            Finish(false, "Portal no terminÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ en estado estable cerrado y sin reservas.");
            yield break;
        }
        if (spatial.ActiveLeaseCount != 0)
        {
            Finish(false, "BBSIS conserva leases residuales: " + spatial.ActiveLeaseCount + ".");
            yield break;
        }

        Finish(true,
            "PASS - manilla IK, Commit Frontier, bloqueo BBSIS, Dynamic Sweep, " +
            "Open/Close, no-op estable, 2 NPC consecutivos y cancelaciones pre/post-commit. " +
            "HandleIK=" + minimumHandleError.ToString("0.000") + "m.");
    }

    private void ResolveAuthorities()
    {
        string portalError = string.Empty;
        if (humanoidPrefab == null || motionCatalog == null || portal == null ||
            !portal.ValidateConfiguration(out portalError))
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(portalError) ? "Fixture BB18.4 incompleto." : portalError);

        interactions = FindFirstObjectByType<BistroBuilderInteractionPresentationService>();
        navigation = FindFirstObjectByType<BistroBuilderNavigationService>();
        spatial = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        if (interactions == null || navigation == null || spatial == null)
            throw new InvalidOperationException("Faltan autoridades BB18, Navigation17 o BBSIS.");

        door = portal.Door;
        envelope = door.GetComponent<BistroBuilderDoorCirculationEnvelope>();
        doorSpatial = door.GetComponent<BistroBuilderDoorSpatialAdapter>();
        if (envelope == null || doorSpatial == null)
            throw new InvalidOperationException("La puerta no estÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ vinculada a Dynamic Sweep BBSIS.");
        interactions.CommitRequested += ConfirmCommit;
    }

    private GameObject CreateActor(
        string actorName,
        Vector3 position,
        Quaternion rotation,
        out BistroBuilderCharacterAnimationDriver driver,
        out BistroBuilderCharacterRigAdapter rig)
    {
        // Root lÃ³gico separado del rig visual: Navigation17 nunca se corrige para arreglar pies.
        GameObject actor = new GameObject(actorName);
        actor.transform.SetPositionAndRotation(position, rotation);
        GameObject visual = Instantiate(humanoidPrefab, actor.transform);
        visual.name = actorName + "_Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.avatar == null ||
            !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new InvalidOperationException(actorName + " no produjo Humanoid vÃ¡lido.");
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        driver = actor.AddComponent<BistroBuilderCharacterAnimationDriver>();
        driver.AssignCatalogIfMissing(motionCatalog);
        actor.AddComponent<BistroBuilderLocomotionAnimationPresenter>();
        rig = actor.AddComponent<BistroBuilderCharacterRigAdapter>();
        if (!rig.ValidateConfiguration(out string rigError))
            throw new InvalidOperationException(rigError);

        BistroBuilderCharacterVisualGroundingAdapter grounding =
            actor.AddComponent<BistroBuilderCharacterVisualGroundingAdapter>();
        grounding.Configure(visual.transform);
        if (!grounding.ValidateConfiguration(out string groundingError))
            throw new InvalidOperationException(groundingError);
        return actor;
    }

    private IEnumerator RunBlockedSweepGate()
    {
        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "bb18.portal.blocker",
            kind = BistroBuilderSpatialClaimKind.DynamicSweep,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Box(
                envelope.WorldCenter,
                door.transform.right,
                door.transform.forward,
                envelope.WorldSize * 0.5f),
            durationSeconds = 4f,
            validateAgainstStaticGeometry = false
        };
        if (!spatial.TryAcquireLease(
                request,
                out BistroBuilderSpatialLease blocker,
                out BistroBuilderSpatialLeaseDecision blockerDecision))
            throw new InvalidOperationException(
                "No pudo prepararse el blocker BBSIS: " + blockerDecision.message);

        BistroBuilderResolvedInteractionPlan plan = CreatePortalPlan(
            actorA,
            BistroBuilderPortalPresentationSide.SideA,
            BistroBuilderInteractionOperation.Open,
            "bb18.portal.blocked");
        if (!interactions.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Portal bloqueado no arrancÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³: " + error);
        yield return WaitForTerminal(handle, 4f);
        spatial.ReleaseLease(blocker.leaseId);

        if (handle.Status != BistroBuilderInteractionStatus.Failed ||
            handle.FailureKind != BistroBuilderInteractionFailureKind.SpatialClaimMissing ||
            door.IsOpen || door.IsMoving)
            throw new InvalidOperationException(
                "El gate BBSIS no bloqueÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ limpiamente el barrido de la puerta.");
    }

    private IEnumerator RunPreCommitCancellation()
    {
        BistroBuilderResolvedInteractionPlan plan = CreatePortalPlan(
            actorA,
            BistroBuilderPortalPresentationSide.SideA,
            BistroBuilderInteractionOperation.Open,
            "bb18.portal.cancel.pre");
        if (!interactions.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Portal cancelable no arrancÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³: " + error);

        float deadline = Time.unscaledTime + 4f;
        bool requested = false;
        while (!handle.IsTerminal && Time.unscaledTime < deadline)
        {
            if (!requested && handle.Phase == BistroBuilderInteractionPhase.Operate &&
                handle.CommitState == BistroBuilderInteractionCommitState.PreCommit)
                requested = handle.RequestCancel();
            yield return null;
        }
        if (!handle.IsTerminal)
            throw new TimeoutException("CancelaciÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³n pre-commit de Portal superÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ watchdog.");
        if (handle.Status != BistroBuilderInteractionStatus.Cancelled ||
            door.IsOpen || door.IsMoving || doorSpatial.HasMotionLease)
            throw new InvalidOperationException(
                "CancelaciÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³n pre-commit no conservÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ la puerta cerrada y sin lease.");
    }

    private IEnumerator RunNormalOpen()
    {
        portal.TryResolveSide(
            BistroBuilderPortalPresentationSide.SideA,
            out Transform approach,
            out _,
            out Transform handleTarget,
            out _);
        actorA.transform.position = approach.position;
        actorA.transform.rotation = approach.rotation;
        SetEvidenceCamera(BistroBuilderPortalPresentationSide.SideA);

        BistroBuilderResolvedInteractionPlan plan = CreatePortalPlan(
            actorA,
            BistroBuilderPortalPresentationSide.SideA,
            BistroBuilderInteractionOperation.Open,
            "bb18.portal.open");
        if (!interactions.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Open no arrancÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³: " + error);

        bool captured = false;
        float deadline = Time.unscaledTime + 6f;
        while (!handle.IsTerminal && Time.unscaledTime < deadline)
        {
            minimumHandleError = Mathf.Min(
                minimumHandleError,
                rigA.GetHandError(true, handleTarget));
            if (door.IsMoving)
            {
                observedDynamicSweep |= envelope.IsActive;
                observedBbsisMotionLease |= doorSpatial.HasMotionLease;
                if (!captured)
                {
                    yield return CaptureEvidence("01_handle_opening.png");
                    captured = true;
                }
            }
            yield return null;
        }
        if (!handle.IsTerminal)
            throw new TimeoutException("Open visual superÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ watchdog.");
        if (handle.Status != BistroBuilderInteractionStatus.Completed ||
            !door.IsOpen || door.IsMoving || envelope.IsActive || doorSpatial.HasMotionLease)
            throw new InvalidOperationException(
                "Open no terminÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ estable, abierto y sin reservas residuales.");
        if (driverA.LastSuccessfullyPlayedMotionId != "portal.open.standard")
            throw new InvalidOperationException("portal.open.standard no se reprodujo realmente.");
        if (!captured)
            throw new InvalidOperationException("No se capturÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ la fase real de apertura.");
        yield return SettleActorForEvidence(driverA);
        AssertActorGrounded(actorA, "Open estable");
        yield return CaptureEvidence("02_open_stable.png");
    }

    private IEnumerator RunAlreadyOpenNoOp()
    {
        BistroBuilderResolvedInteractionPlan plan = CreatePortalPlan(
            actorA,
            BistroBuilderPortalPresentationSide.SideA,
            BistroBuilderInteractionOperation.Open,
            "bb18.portal.open.noop");
        if (!interactions.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Open no-op no arrancÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³: " + error);
        yield return WaitForTerminal(handle, 2f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed ||
            !door.IsOpen || door.IsMoving || doorSpatial.HasMotionLease)
            throw new InvalidOperationException("Open sobre puerta ya abierta no fue idempotente.");
    }

    private IEnumerator RunConsecutivePassage()
    {
        portal.TryResolveSide(
            BistroBuilderPortalPresentationSide.SideA,
            out Transform approach,
            out Transform through,
            out _,
            out _);
        SetEvidenceCamera(BistroBuilderPortalPresentationSide.SideA);
        yield return MoveActor(actorA, driverA, through.position,
            "bb18.portal.pass.a", null);

        actorB = CreateActor("__BB18_PortalActor_B__",
            approach.position + Vector3.left * 0.18f,
            approach.rotation,
            out BistroBuilderCharacterAnimationDriver driverB,
            out _);
        yield return CalibrateActorGrounding(actorB, driverB);
        BistroBuilderResolvedInteractionPlan noOpPlan = CreatePortalPlan(
            actorB,
            BistroBuilderPortalPresentationSide.SideA,
            BistroBuilderInteractionOperation.Open,
            "bb18.portal.open.second.noop");
        if (!interactions.TryStartResolvedInteraction(
                noOpPlan, out BistroBuilderInteractionHandle noOp, out string error))
            throw new InvalidOperationException("Segundo NPC no pudo comprobar Open: " + error);
        yield return WaitForTerminal(noOp, 2f);
        if (noOp.Status != BistroBuilderInteractionStatus.Completed || !door.IsOpen)
            throw new InvalidOperationException("Segundo NPC no reutilizÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ puerta abierta.");

        yield return MoveActor(actorB, driverB, through.position + Vector3.left * 0.18f,
            "bb18.portal.pass.b", "03_two_npc_passage.png");
        if (!door.IsOpen || door.IsMoving)
            throw new InvalidOperationException("La puerta no permaneciÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ estable durante paso consecutivo.");
    }

    private IEnumerator RunNormalClose()
    {
        portal.TryResolveSide(
            BistroBuilderPortalPresentationSide.SideB,
            out Transform approach,
            out _,
            out Transform handleTarget,
            out _);
        actorA.transform.position = approach.position;
        actorA.transform.rotation = approach.rotation;
        SetEvidenceCamera(BistroBuilderPortalPresentationSide.SideB);

        BistroBuilderResolvedInteractionPlan plan = CreatePortalPlan(
            actorA,
            BistroBuilderPortalPresentationSide.SideB,
            BistroBuilderInteractionOperation.Close,
            "bb18.portal.close");
        if (!interactions.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Close no arrancÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³: " + error);

        bool captured = false;
        float deadline = Time.unscaledTime + 6f;
        while (!handle.IsTerminal && Time.unscaledTime < deadline)
        {
            minimumHandleError = Mathf.Min(minimumHandleError,
                rigA.GetHandError(true, handleTarget));
            if (door.IsMoving && !captured)
            {
                yield return CaptureEvidence("04_handle_closing.png");
                captured = true;
            }
            yield return null;
        }
        if (!handle.IsTerminal)
            throw new TimeoutException("Close visual superÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ watchdog.");
        if (handle.Status != BistroBuilderInteractionStatus.Completed ||
            door.IsOpen || door.IsMoving || envelope.IsActive || doorSpatial.HasMotionLease)
            throw new InvalidOperationException("Close no terminÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ estable y cerrado.");
        if (driverA.LastSuccessfullyPlayedMotionId != "portal.close.standard")
            throw new InvalidOperationException("portal.close.standard no se reprodujo realmente.");
        if (!captured)
            throw new InvalidOperationException("No se capturÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ la fase real de cierre.");
        yield return SettleActorForEvidence(driverA);
        AssertActorGrounded(actorA, "Close estable");
        yield return CaptureEvidence("05_closed_stable.png");
    }

    private IEnumerator RunAlreadyClosedNoOp()
    {
        BistroBuilderResolvedInteractionPlan plan = CreatePortalPlan(
            actorA,
            BistroBuilderPortalPresentationSide.SideB,
            BistroBuilderInteractionOperation.Close,
            "bb18.portal.close.noop");
        if (!interactions.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Close no-op no arrancÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³: " + error);
        yield return WaitForTerminal(handle, 2f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed ||
            door.IsOpen || door.IsMoving || doorSpatial.HasMotionLease)
            throw new InvalidOperationException("Close sobre puerta cerrada no fue idempotente.");
    }

    private IEnumerator RunPostCommitCancellation()
    {
        BistroBuilderResolvedInteractionPlan reopen = CreatePortalPlan(
            actorA,
            BistroBuilderPortalPresentationSide.SideB,
            BistroBuilderInteractionOperation.Open,
            "bb18.portal.reopen");
        if (!interactions.TryStartResolvedInteraction(
                reopen, out BistroBuilderInteractionHandle reopenHandle, out string reopenError))
            throw new InvalidOperationException("Reopen no arrancÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³: " + reopenError);
        yield return WaitForTerminal(reopenHandle, 6f);
        if (reopenHandle.Status != BistroBuilderInteractionStatus.Completed || !door.IsOpen)
            throw new InvalidOperationException("Reopen previo a cancelaciÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³n post-commit fallÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³.");
        BistroBuilderResolvedInteractionPlan close = CreatePortalPlan(
            actorA,
            BistroBuilderPortalPresentationSide.SideB,
            BistroBuilderInteractionOperation.Close,
            "bb18.portal.close.cancel.post");
        if (!interactions.TryStartResolvedInteraction(
                close, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Close post-commit no arrancÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³: " + error);

        bool requested = false;
        float deadline = Time.unscaledTime + 6f;
        while (!handle.IsTerminal && Time.unscaledTime < deadline)
        {
            if (!requested && door.IsMoving &&
                handle.CommitState == BistroBuilderInteractionCommitState.Confirmed)
                requested = handle.RequestCancel();
            yield return null;
        }
        if (!handle.IsTerminal)
            throw new TimeoutException("CancelaciÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³n post-commit superÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ watchdog.");
        if (!requested || handle.Status != BistroBuilderInteractionStatus.Recovered ||
            door.IsOpen || door.IsMoving || envelope.IsActive || doorSpatial.HasMotionLease)
            throw new InvalidOperationException(
                "CancelaciÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³n post-commit no terminÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ en estado estable recuperado.");
        SetEvidenceCamera(BistroBuilderPortalPresentationSide.SideB);
        yield return SettleActorForEvidence(driverA);
        AssertActorGrounded(actorA, "Recover post-commit");
        yield return CaptureEvidence("06_postcommit_recovered.png");
    }

    private BistroBuilderResolvedInteractionPlan CreatePortalPlan(
        GameObject actor,
        BistroBuilderPortalPresentationSide side,
        BistroBuilderInteractionOperation operation,
        string id)
    {
        if (!portal.TryResolveSide(side,
                out Transform approach,
                out _,
                out Transform handleTarget,
                out Transform lookTarget))
            throw new InvalidOperationException("No se pudo resolver el lado visual del portal.");

        return new BistroBuilderResolvedInteractionPlan
        {
            interactionId = id,
            ownerId = id,
            family = BistroBuilderInteractionFamily.Portal,
            operation = operation,
            actor = actor,
            target = door.gameObject,
            interactionFrame = approach,
            rightHandTarget = handleTarget,
            lookTarget = lookTarget,
            door = door,
            semanticMotionId = operation == BistroBuilderInteractionOperation.Open
                ? "portal.open.standard"
                : "portal.close.standard",
            fallbackMotionId = "transfer.generic",
            fallbackMotionDuration = 0.9f,
            commitNormalizedTime = 0.42f,
            operationTimeoutSeconds = 3f,
            commitTimeoutSeconds = 1f,
            requiresCommitConfirmation = true
        };
    }

    private IEnumerator MoveActor(
        GameObject actor,
        BistroBuilderCharacterAnimationDriver driver,
        Vector3 destination,
        string ownerId,
        string evidenceFile)
    {
        var points = new List<Vector3>(32);
        if (!navigation.TryBuildRoute(
                ownerId,
                BistroBuilderNavigationAgentMask.Customer,
                actor.transform.position,
                destination,
                points,
                out _,
                out BistroBuilderNavigationRouteKind routeKind) ||
            routeKind == BistroBuilderNavigationRouteKind.None || points.Count == 0)
            throw new InvalidOperationException("Navigation17 no resolviÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ paso por portal: " + ownerId);

        if (!driver.TryPlaySemanticMotion(
                "locomotion.walk", "locomotion.idle", 1f, out _))
            throw new InvalidOperationException("No pudo arrancar locomotion.walk.");
        bool captured = false;
        float deadline = Time.unscaledTime + 10f;
        for (int p = 0; p < points.Count; p++)
        {
            Vector3 waypoint = points[p];
            while (HorizontalDistance(actor.transform.position, waypoint) > 0.04f)
            {
                if (Time.unscaledTime >= deadline)
                    throw new TimeoutException("Paso Navigation17 superÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ watchdog: " + ownerId);
                navigation.UpdateAgentPresence(
                    ownerId,
                    BistroBuilderNavigationAgentMask.Customer,
                    actor.transform.position,
                    0.28f,
                    20);
                Vector3 delta = waypoint - actor.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= 0.000001f) break;
                Vector3 direction = delta.normalized;
                float step = Mathf.Min(1.25f * Time.deltaTime, delta.magnitude);
                Vector3 candidate = actor.transform.position + direction * step;
                candidate.y = actor.transform.position.y;
                if (navigation.CanAdvance(
                        ownerId,
                        BistroBuilderNavigationAgentMask.Customer,
                        candidate,
                        0.28f,
                        20))
                {
                    actor.transform.position = candidate;
                    actor.transform.rotation = Quaternion.Slerp(
                        actor.transform.rotation,
                        Quaternion.LookRotation(direction, Vector3.up),
                        Mathf.Clamp01(Time.deltaTime * 10f));
                }
                if (!captured && !string.IsNullOrWhiteSpace(evidenceFile))
                {
                    yield return CaptureEvidence(evidenceFile);
                    captured = true;
                }
                yield return null;
            }
        }
        actor.transform.position = new Vector3(
            destination.x, actor.transform.position.y, destination.z);
        navigation.RemoveAgentPresence(ownerId);
        BistroBuilderCharacterRigAdapter movementRig =
            actor.GetComponent<BistroBuilderCharacterRigAdapter>();
        movementRig?.ClearInteractionTargetsImmediate();
        if (!driver.ReturnToBaselinePose())
            throw new InvalidOperationException("No pudo restaurarse Idle tras la locomoción.");
        yield return new WaitForSecondsRealtime(0.12f);
        if (driver.LastSuccessfullyPlayedMotionId != "locomotion.walk")
            throw new InvalidOperationException("locomotion.walk no se reprodujo en el paso.");
    }

    private IEnumerator CalibrateActorGrounding(
        GameObject actor,
        BistroBuilderCharacterAnimationDriver driver)
    {
        BistroBuilderCharacterVisualGroundingAdapter grounding = actor != null
            ? actor.GetComponent<BistroBuilderCharacterVisualGroundingAdapter>()
            : null;
        if (grounding == null || driver == null)
            throw new InvalidOperationException("Actor sin grounding visual universal.");
        if (!driver.TryPlaySemanticMotion(
                "locomotion.idle", "locomotion.idle", 1f, out _))
            throw new InvalidOperationException("No pudo reproducirse Idle para calibrar grounding.");
        yield return null;
        yield return new WaitForEndOfFrame();
        if (!grounding.CalibrateFromCurrentPose(out string groundingError))
            throw new InvalidOperationException(groundingError);
        yield return null;
        AssertActorGrounded(actor, "Grounding calibrado");
    }

    private IEnumerator SettleActorForEvidence(BistroBuilderCharacterAnimationDriver driver)
    {
        if (driver == null)
            throw new InvalidOperationException("Driver nulo al asentar evidencia estable.");
        BistroBuilderCharacterRigAdapter rig =
            driver.GetComponent<BistroBuilderCharacterRigAdapter>();
        rig?.ClearInteractionTargetsImmediate();
        if (!driver.ReturnToBaselinePose())
            throw new InvalidOperationException("No pudo restaurarse locomotion.idle para evidencia estable.");
        yield return new WaitForSecondsRealtime(0.18f);
        yield return new WaitForEndOfFrame();
    }

    private void SetEvidenceCamera(BistroBuilderPortalPresentationSide side)
    {
        if (evidenceCamera == null || door == null) return;
        Vector3 sideDirection = side == BistroBuilderPortalPresentationSide.SideA
            ? -door.transform.forward : door.transform.forward;
        Vector3 lateral = side == BistroBuilderPortalPresentationSide.SideA
            ? door.transform.right : -door.transform.right;
        Vector3 target = door.transform.position + Vector3.up * 1.0f + door.transform.right * 0.25f;
        evidenceCamera.transform.position = door.transform.position +
            sideDirection * 5.4f + lateral * 4.4f + Vector3.up * 3.2f;
        Vector3 look = target - evidenceCamera.transform.position;
        evidenceCamera.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
    }

    private static void AssertActorGrounded(GameObject actor, string label)
    {
        if (actor == null) throw new InvalidOperationException(label + ": actor nulo.");
        Renderer[] renderers = actor.GetComponentsInChildren<Renderer>(true);
        float minY = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) minY = Mathf.Min(minY, renderers[i].bounds.min.y);
        float delta = minY - actor.transform.position.y;
        if (float.IsInfinity(minY) || float.IsNaN(minY) || delta < -0.10f || delta > 0.18f)
            throw new InvalidOperationException(label + ": contacto suelo incoherente; delta=" + delta.ToString("0.000") + "m.");
    }

    private static IEnumerator WaitForTerminal(
        BistroBuilderInteractionHandle handle,
        float timeoutSeconds)
    {
        float deadline = Time.unscaledTime + Mathf.Max(0.1f, timeoutSeconds);
        while (handle != null && !handle.IsTerminal && Time.unscaledTime < deadline)
            yield return null;
        if (handle == null)
            throw new InvalidOperationException("Handle Portal nulo.");
        if (!handle.IsTerminal)
            throw new TimeoutException(
                "Portal superÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ watchdog: " + handle.InteractionId +
                "; phase=" + handle.Phase + "; commit=" + handle.CommitState + ".");
    }

    private IEnumerator CaptureEvidence(string fileName)
    {
        if (evidenceCamera == null) yield break;
        yield return new WaitForEndOfFrame();
        const int width = 1280;
        const int height = 720;
        RenderTexture rt = new RenderTexture(width, height, 24);
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previousTarget = evidenceCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            evidenceCamera.targetTexture = rt;
            RenderTexture.active = rt;
            evidenceCamera.Render();
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            string directory = ResolveEvidenceDirectory();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            byte[] png = texture.EncodeToPNG();
            File.WriteAllBytes(path, png);
            Debug.Log("BB18_PORTAL_EVIDENCE|" + fileName + "|SIZE=" + png.Length);
        }
        finally
        {
            evidenceCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Destroy(rt);
            Destroy(texture);
        }
    }

    private static string ResolveEvidenceDirectory()
    {
        string configured = Environment.GetEnvironmentVariable("BB18_PORTAL_EVIDENCE_DIR");
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(Application.persistentDataPath, "BB18PortalEvidence");
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private IEnumerator ExecuteStep(IEnumerator root)
    {
        stepException = null;
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek();
            bool moved;
            object yielded = null;
            try
            {
                moved = current.MoveNext();
                if (moved) yielded = current.Current;
            }
            catch (Exception exception)
            {
                stepException = exception;
                yield break;
            }
            if (!moved)
            {
                stack.Pop();
                continue;
            }
            if (yielded is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return yielded;
        }
    }

    private bool ContinueAfterStep()
    {
        if (stepException == null) return true;
        Debug.LogException(stepException);
        Finish(false, stepException.Message);
        return false;
    }

    private static void ConfirmCommit(
        BistroBuilderInteractionHandle handle,
        BistroBuilderResolvedInteractionPlan plan)
    {
        handle?.ConfirmCommit();
    }

    private static void Finish(bool success, string message)
    {
        string report =
            "=== BISTRO BUILDER - BB18.4 PORTAL VISUAL GATE ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n" +
            "Evidence=" + ResolveEvidenceDirectory();
        string configured = Environment.GetEnvironmentVariable("BB18_PORTAL_REPORT_PATH");
        string reportPath = !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(Application.persistentDataPath, "BB18PortalReport.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? Application.persistentDataPath);
        File.WriteAllText(reportPath, report);
        if (success) Debug.Log(report);
        else Debug.LogError(report);
        Application.Quit(success ? 0 : 1);
    }

    private void OnDestroy()
    {
        if (interactions != null)
            interactions.CommitRequested -= ConfirmCommit;
        if (navigation != null)
        {
            navigation.RemoveAgentPresence("bb18.portal.pass.a");
            navigation.RemoveAgentPresence("bb18.portal.pass.b");
        }
        doorSpatial?.ReleaseMotionReservation();
    }
}
#endif
