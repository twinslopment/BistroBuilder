#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// DemostraciÃ³n visual reproducible de BB18 con un Humanoid real:
/// navegaciÃ³n 17 -> locomociÃ³n -> Seat/Stand -> Transfer/Commit.
/// SÃ³lo forma parte de Editor y Development Builds.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAnimation18VisualRuntimeProbe : MonoBehaviour
{
    [SerializeField] private bool runOnStart;
    [SerializeField] private GameObject humanoidPrefab;
    [SerializeField] private BistroBuilderMotionCatalog motionCatalog;
    [SerializeField] private RestaurantSeat seat;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform transferApproach;
    [SerializeField] private Transform sourceSocket;
    [SerializeField] private BistroBuilderTransferableVisual transferable;
    [SerializeField] private Transform placeApproach;
    [SerializeField] private Transform placeSocket;
    [SerializeField] private Transform boxApproach;
    [SerializeField] private Transform boxSourceSocket;
    [SerializeField] private BistroBuilderTransferableVisual boxTransferable;
    [SerializeField] private Camera evidenceCamera;

    private BistroBuilderInteractionPresentationService interactionService;
    private BistroBuilderNavigationService navigation;
    private GameObject actor;
    private Animator animator;
    private BistroBuilderCharacterAnimationDriver animationDriver;
    private BistroBuilderLocomotionAnimationPresenter locomotionPresenter;
    private Transform rightHand;
    private Transform leftHand;
    private BistroBuilderCharacterRigAdapter rigAdapter;
    private BistroBuilderCarryPresenter carryPresenter;
    private BistroBuilderCarrySocketSet carrySockets;
    private BistroBuilderCarryableDescriptor plateDescriptor;
    private BistroBuilderCarryableDescriptor boxDescriptor;
    private float plateContactError;
    private float plateCarryError;
    private float boxRightError;
    private float boxLeftError;
    private float scaleMatrixMaxError;
    private float matrixMaxError;
    private bool started;
    private bool walkEvidenceCaptured;
    private float maxObservedSpeed;
    private bool presenterObservedLocomotion;
    private BistroBuilderNavigationRouteKind firstRouteKind;
    private BistroBuilderNavigationRouteKind secondRouteKind;
    private Exception stepException;

    public void ConfigureForEditor(
        bool shouldRun,
        GameObject configuredHumanoidPrefab,
        BistroBuilderMotionCatalog configuredCatalog,
        RestaurantSeat configuredSeat,
        Transform configuredStartPoint,
        Transform configuredTransferApproach,
        Transform configuredSourceSocket,
        BistroBuilderTransferableVisual configuredTransferable,
        Transform configuredPlaceApproach,
        Transform configuredPlaceSocket,
        Transform configuredBoxApproach,
        Transform configuredBoxSourceSocket,
        BistroBuilderTransferableVisual configuredBoxTransferable,
        Camera configuredCamera)
    {
        runOnStart = shouldRun;
        humanoidPrefab = configuredHumanoidPrefab;
        motionCatalog = configuredCatalog;
        seat = configuredSeat;
        startPoint = configuredStartPoint;
        transferApproach = configuredTransferApproach;
        sourceSocket = configuredSourceSocket;
        transferable = configuredTransferable;
        placeApproach = configuredPlaceApproach;
        placeSocket = configuredPlaceSocket;
        boxApproach = configuredBoxApproach;
        boxSourceSocket = configuredBoxSourceSocket;
        boxTransferable = configuredBoxTransferable;
        evidenceCamera = configuredCamera;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        BistroBuilderAnimation18VisualRuntimeProbe[] probes =
            FindObjectsByType<BistroBuilderAnimation18VisualRuntimeProbe>(
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
        Debug.Log("BB18_VISUAL_RUNTIME_START");
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        try
        {
            SetupActor();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(false, exception.Message);
            yield break;
        }

        yield return null;
        navigation.RebuildNavigationTopology();
        yield return ExecuteStep(MoveTo(
            seat.CustomerApproachPoint.position,
            "bb18.visual.walk.seat",
            true,
            kind => firstRouteKind = kind));
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunSit());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunSeatedEvidence());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunStand());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(MoveTo(
            transferApproach.position,
            "bb18.visual.walk.transfer",
            false,
            kind => secondRouteKind = kind));
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(AlignFacing(transferApproach));
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunPickup());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunCarryReconcile());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(MoveTo(
            placeApproach.position,
            "bb18.visual.walk.carry",
            false,
            _ => { },
            "05_plate_carry_walk.png"));
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(AlignFacing(placeApproach));
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunPlace());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(MoveTo(
            boxApproach.position,
            "bb18.visual.walk.box",
            false,
            _ => { }));
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(AlignFacing(boxApproach));
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunBoxPickup());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunAvatarScaleMatrix());
        if (!ContinueAfterStep()) yield break;

        if (maxObservedSpeed < 0.90f || maxObservedSpeed > 1.60f)
        {
            Finish(false, "Velocidad locomotora fuera de rango: " +
                maxObservedSpeed.ToString("0.00") + " m/s.");
            yield break;
        }
        if (!presenterObservedLocomotion)
        {
            Finish(false, "Locomotion Presenter no observó movimiento real estable.");
            yield break;
        }
        if (firstRouteKind == BistroBuilderNavigationRouteKind.None ||
            secondRouteKind == BistroBuilderNavigationRouteKind.None)
        {
            Finish(false, "Navigation 17 no produjo rutas válidas.");
            yield break;
        }

        Finish(true,
            "PASS - Humanoid real, locomoción Navigation17, Sit/Stand animados, " +
            "SeatFrame, IK/Carry, Pickup/Place, Box2H y Scale Matrix funcionan. " +
            "IK PlateContact=" + plateContactError.ToString("0.000") + "m; " +
            "PlateCarry=" + plateCarryError.ToString("0.000") + "m; " +
            "BoxR/L=" + boxRightError.ToString("0.000") + "/" +
                boxLeftError.ToString("0.000") + "m; ScaleMax=" +
                scaleMatrixMaxError.ToString("0.000") + "m. " +
            "MaxSpeed=" + maxObservedSpeed.ToString("0.00") +
            " m/s; Routes=" + firstRouteKind + "/" + secondRouteKind + ".");
    }
    private void SetupActor()
    {
        if (humanoidPrefab == null || motionCatalog == null || seat == null ||
            startPoint == null || transferApproach == null || sourceSocket == null ||
            transferable == null || placeApproach == null || placeSocket == null ||
            boxApproach == null || boxSourceSocket == null || boxTransferable == null)
            throw new InvalidOperationException("Fixture visual BB18.3 incompleto.");
        interactionService = FindFirstObjectByType<BistroBuilderInteractionPresentationService>();
        navigation = FindFirstObjectByType<BistroBuilderNavigationService>();
        if (interactionService == null || navigation == null)
            throw new InvalidOperationException("Faltan autoridades BB18/Navigation17.");

        actor = Instantiate(humanoidPrefab, startPoint.position, startPoint.rotation);
        actor.name = "__BB18_VisualHumanoid__";
        animator = actor.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new InvalidOperationException("El fixture no produjo Animator Humanoid válido.");

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animationDriver = actor.AddComponent<BistroBuilderCharacterAnimationDriver>();
        animationDriver.AssignCatalogIfMissing(motionCatalog);
        locomotionPresenter = actor.AddComponent<BistroBuilderLocomotionAnimationPresenter>();
        rigAdapter = actor.AddComponent<BistroBuilderCharacterRigAdapter>();
        carrySockets = actor.AddComponent<BistroBuilderCarrySocketSet>();
        carryPresenter = actor.AddComponent<BistroBuilderCarryPresenter>();
        if (!rigAdapter.ValidateConfiguration(out string rigError))
            throw new InvalidOperationException("Animation Rigging inválido: " + rigError);
        if (!carryPresenter.ValidateConfiguration(out string carryError))
            throw new InvalidOperationException("Carry Presenter inválido: " + carryError);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (rightHand == null || leftHand == null)
            throw new InvalidOperationException("Humanoid sin manos resolubles.");

        plateDescriptor = transferable.GetComponent<BistroBuilderCarryableDescriptor>();
        boxDescriptor = boxTransferable.GetComponent<BistroBuilderCarryableDescriptor>();
        if (plateDescriptor == null || boxDescriptor == null)
            throw new InvalidOperationException("Faltan Carryable Descriptors del fixture.");
        interactionService.CommitRequested += ConfirmCommit;
    }
    private IEnumerator MoveTo(
        Vector3 destination,
        string ownerId,
        bool captureWalk,
        Action<BistroBuilderNavigationRouteKind> routeSink,
        string carryEvidenceFile = null)
    {
        bool carryEvidenceCaptured = false;
        var points = new List<Vector3>(32);
        if (!navigation.TryBuildRoute(
                ownerId,
                BistroBuilderNavigationAgentMask.Customer,
                actor.transform.position,
                destination,
                points,
                out _,
                out BistroBuilderNavigationRouteKind kind))
            throw new InvalidOperationException("Navigation17 no pudo resolver la ruta " + ownerId + ".");

        routeSink?.Invoke(kind);
        if (points.Count == 0)
            throw new InvalidOperationException("Navigation17 devolviÃ³ una ruta vacÃ­a.");

        if (!animationDriver.TryPlaySemanticMotion(
                "locomotion.walk", "locomotion.idle", 1f, out _))
            throw new InvalidOperationException("No pudo arrancar locomotion.walk.");
        float deadline = Time.unscaledTime + 12f;
        Vector3 routeStart = actor.transform.position;
        for (int p = 0; p < points.Count; p++)
        {
            Vector3 waypoint = points[p];
            while (HorizontalDistance(actor.transform.position, waypoint) > 0.035f)
            {
                if (Time.unscaledTime >= deadline)
                    throw new TimeoutException("LocomociÃ³n visual superÃ³ watchdog: " + ownerId);

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

                float actualStepSpeed = step / Mathf.Max(0.0001f, Time.deltaTime);
                maxObservedSpeed = Mathf.Max(maxObservedSpeed, actualStepSpeed);
                float presenterSpeed = locomotionPresenter.ActualSpeedMetersPerSecond;
                if (presenterSpeed >= 0.30f && presenterSpeed <= 2.50f)
                    presenterObservedLocomotion = true;

                if (captureWalk && !walkEvidenceCaptured &&
                    HorizontalDistance(routeStart, actor.transform.position) > 0.8f)
                {
                    yield return CaptureEvidence("01_walk.png");
                    walkEvidenceCaptured = true;
                }
                if (!carryEvidenceCaptured && !string.IsNullOrWhiteSpace(carryEvidenceFile) &&
                    HorizontalDistance(routeStart, actor.transform.position) > 0.55f)
                {
                    yield return CaptureEvidence(carryEvidenceFile);
                    carryEvidenceCaptured = true;
                }
                yield return null;
            }
        }
        actor.transform.position = new Vector3(
            destination.x, actor.transform.position.y, destination.z);
        navigation.RemoveAgentPresence(ownerId);
        animationDriver.StopTransientMotion();
        if (animationDriver.LastSuccessfullyPlayedMotionId != "locomotion.walk")
            throw new InvalidOperationException("El clip locomotion.walk no llegÃ³ a reproducirse.");
    }

    private IEnumerator RunSit()
    {
        const string owner = "bb18.visual.seat";
        if (!seat.TryReserve(owner))
            throw new InvalidOperationException("No pudo reservarse la silla visual.");

        BistroBuilderResolvedInteractionPlan plan = CreateSeatPlan(
            "bb18.visual.sit",
            owner,
            BistroBuilderInteractionOperation.Sit,
            "seat.sit.standard",
            0.58f);
        if (!interactionService.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Sit no arrancÃ³: " + error);

        yield return WaitForTerminal(handle, 5f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed ||
            seat.OperationalState != RestaurantSeatOperationalState.Occupied)
            throw new InvalidOperationException("Sit no alcanzÃ³ estado estable Occupied.");
        if (animationDriver.LastSuccessfullyPlayedMotionId != "seat.sit.standard")
            throw new InvalidOperationException("Sitting_Enter no se reprodujo realmente.");
    }

    private IEnumerator RunSeatedEvidence()
    {
        if (!animationDriver.TryPlaySemanticMotion(
                "seat.idle.standard", "seat.sit.standard", 1f, out _))
            throw new InvalidOperationException("No pudo arrancar seat.idle.standard.");
        yield return new WaitForSecondsRealtime(0.45f);
        yield return CaptureEvidence("02_seated.png");
        animationDriver.StopTransientMotion();
    }

    private IEnumerator RunStand()
    {
        const string owner = "bb18.visual.seat";
        BistroBuilderResolvedInteractionPlan plan = CreateSeatPlan(
            "bb18.visual.stand",
            owner,
            BistroBuilderInteractionOperation.Stand,
            "seat.stand.standard",
            0.55f);
        if (!interactionService.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Stand no arrancÃ³: " + error);

        yield return WaitForTerminal(handle, 5f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed ||
            seat.OperationalState != RestaurantSeatOperationalState.Parked)
            throw new InvalidOperationException("Stand no regresÃ³ la silla a Parked.");
        if (animationDriver.LastSuccessfullyPlayedMotionId != "seat.stand.standard")
            throw new InvalidOperationException("Sitting_Exit no se reprodujo realmente.");
        if (!seat.ReleaseReservation(owner))
            throw new InvalidOperationException("No pudo liberarse la silla tras Stand.");
    }

    private IEnumerator AlignFacing(Transform frame)
    {
        if (frame == null) yield break;
        Vector3 forward = frame.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.000001f) yield break;
        Quaternion start = actor.transform.rotation;
        Quaternion target = Quaternion.LookRotation(forward.normalized, Vector3.up);
        float elapsed = 0f;
        const float duration = 0.20f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            actor.transform.rotation = Quaternion.Slerp(
                start, target, t * t * (3f - 2f * t));
            yield return null;
        }
        actor.transform.rotation = target;
    }
    private IEnumerator RunPickup()
    {
        BistroBuilderResolvedInteractionPlan plan = new BistroBuilderResolvedInteractionPlan
        {
            interactionId = "bb18.visual.pickup",
            ownerId = "bb18.visual.pickup",
            family = BistroBuilderInteractionFamily.Transfer,
            operation = BistroBuilderInteractionOperation.Pickup,
            actor = actor,
            target = transferable.gameObject,
            interactionFrame = transferApproach,
            transferTarget = plateDescriptor.RightGrip,
            transferable = transferable,
            sourceSocket = sourceSocket,
            destinationSocket = rightHand,
            semanticMotionId = "transfer.table.1h",
            fallbackMotionId = "transfer.generic",
            commitNormalizedTime = 0.60f,
            operationTimeoutSeconds = 3f,
            commitTimeoutSeconds = 1.5f,
            requiresCommitConfirmation = true
        };
        if (!interactionService.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Pickup no arrancÃ³: " + error);

        yield return WaitForCommitFrontier(handle, 2.5f);
        yield return new WaitForSecondsRealtime(0.12f);
        plateContactError = rigAdapter.GetHandError(true, plateDescriptor.RightGrip);
        if (plateContactError > 0.14f)
            throw new InvalidOperationException(
                "IK mano-plato fuera de tolerancia en contacto: " +
                plateContactError.ToString("0.000") + " m.");
        yield return CaptureEvidence("03_pickup_contact.png");
        if (!handle.ConfirmCommit())
            throw new InvalidOperationException("No pudo confirmarse el Commit de Pickup.");

        yield return WaitForTerminal(handle, 5f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed)
            throw new InvalidOperationException(
                "Pickup terminÃ³ como " + handle.Status + ": " + handle.FailureReason);
        if (!carryPresenter.IsCarrying ||
            carryPresenter.CurrentCarryMode != BistroBuilderCarryMode.Plate)
            throw new InvalidOperationException("Carry Plate no quedÃ³ activo tras Pickup.");
        if (!carryPresenter.TryResolveSocket(plateDescriptor, out Transform plateCarrySocket) ||
            transferable.transform.parent != plateCarrySocket)
            throw new InvalidOperationException("Plato no quedÃ³ en el Carry Socket semÃ¡ntico.");

        yield return new WaitForSecondsRealtime(0.18f);
        plateCarryError = carryPresenter.CurrentRightHandError;
        if (plateCarryError > 0.14f)
            throw new InvalidOperationException(
                "Carry Plate IK fuera de tolerancia: " +
                plateCarryError.ToString("0.000") + " m.");
        yield return CaptureEvidence("04_pickup_attached.png");
    }

    private IEnumerator RunCarryReconcile()
    {
        carryPresenter.EndCarryPose();
        yield return null;
        if (!carryPresenter.ReconcileCarriedVisual(transferable, plateDescriptor))
            throw new InvalidOperationException(
                "No pudo reconstruirse Carry desde estado lÃ³gico estable.");
        yield return new WaitForSecondsRealtime(0.15f);
        plateCarryError = Mathf.Max(plateCarryError, carryPresenter.CurrentRightHandError);
        if (!carryPresenter.IsCarrying || plateCarryError > 0.14f)
            throw new InvalidOperationException(
                "ReconciliaciÃ³n Save/Load visual de Carry no es estable.");
    }
    private IEnumerator RunPlace()
    {
        Transform currentCarrySocket = transferable.transform.parent;
        BistroBuilderResolvedInteractionPlan plan = new BistroBuilderResolvedInteractionPlan
        {
            interactionId = "bb18.visual.place",
            ownerId = "bb18.visual.place",
            family = BistroBuilderInteractionFamily.Transfer,
            operation = BistroBuilderInteractionOperation.Place,
            actor = actor,
            target = transferable.gameObject,
            interactionFrame = placeApproach,
            transferTarget = placeSocket,
            transferable = transferable,
            sourceSocket = currentCarrySocket,
            destinationSocket = placeSocket,
            semanticMotionId = "transfer.table.1h",
            fallbackMotionId = "transfer.generic",
            commitNormalizedTime = 0.58f,
            operationTimeoutSeconds = 3f,
            commitTimeoutSeconds = 1.5f,
            requiresCommitConfirmation = true
        };
        if (!interactionService.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Place no arrancÃ³: " + error);
        yield return WaitForCommitFrontier(handle, 2.5f);
        yield return new WaitForSecondsRealtime(0.12f);
        float placeError = rigAdapter.GetHandError(true, placeSocket);
        if (placeError > 0.16f)
            throw new InvalidOperationException(
                "IK Place fuera de tolerancia: " + placeError.ToString("0.000") + " m.");
        yield return CaptureEvidence("06_place_contact.png");
        if (!handle.ConfirmCommit())
            throw new InvalidOperationException("No pudo confirmarse el Commit de Place.");
        yield return WaitForTerminal(handle, 5f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed)
            throw new InvalidOperationException("Place terminÃ³ como " + handle.Status + ".");
        if (transferable.transform.parent != placeSocket || carryPresenter.IsCarrying)
            throw new InvalidOperationException(
                "Place no dejÃ³ el plato en estado visual estable.");
        yield return new WaitForSecondsRealtime(0.12f);
        yield return CaptureEvidence("07_plate_placed.png");
    }

    private IEnumerator RunBoxPickup()
    {
        BistroBuilderResolvedInteractionPlan plan = new BistroBuilderResolvedInteractionPlan
        {
            interactionId = "bb18.visual.box.pickup",
            ownerId = "bb18.visual.box.pickup",
            family = BistroBuilderInteractionFamily.Transfer,
            operation = BistroBuilderInteractionOperation.Pickup,
            actor = actor,
            target = boxTransferable.gameObject,
            interactionFrame = boxApproach,
            transferTarget = boxDescriptor.RightGrip,
            transferable = boxTransferable,
            sourceSocket = boxSourceSocket,
            destinationSocket = rightHand,
            semanticMotionId = "transfer.table.1h",
            fallbackMotionId = "transfer.generic",
            commitNormalizedTime = 0.60f,
            operationTimeoutSeconds = 3f,
            commitTimeoutSeconds = 1.5f,
            requiresCommitConfirmation = true
        };
        if (!interactionService.TryStartResolvedInteraction(
                plan, out BistroBuilderInteractionHandle handle, out string error))
            throw new InvalidOperationException("Box Pickup no arrancÃ³: " + error);
        yield return WaitForCommitFrontier(handle, 2.5f);
        yield return new WaitForSecondsRealtime(0.14f);
        boxRightError = rigAdapter.GetHandError(true, boxDescriptor.RightGrip);
        boxLeftError = rigAdapter.GetHandError(false, boxDescriptor.LeftGrip);
        if (boxRightError > 0.17f || boxLeftError > 0.17f)
            throw new InvalidOperationException(
                "IK Box 2H fuera de tolerancia. R=" + boxRightError.ToString("0.000") +
                " m; L=" + boxLeftError.ToString("0.000") + " m.");
        yield return CaptureEvidence("08_box_contact.png");
        if (!handle.ConfirmCommit())
            throw new InvalidOperationException("No pudo confirmarse el Commit de Box Pickup.");
        yield return WaitForTerminal(handle, 5f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed)
            throw new InvalidOperationException(
                "Box Pickup terminÃ³ como " + handle.Status + ": " + handle.FailureReason);
        if (!carryPresenter.IsCarrying ||
            carryPresenter.CurrentCarryMode != BistroBuilderCarryMode.BoxTwoHand)
            throw new InvalidOperationException("Carry BoxTwoHand no quedÃ³ activo.");
        if (!carryPresenter.TryResolveSocket(boxDescriptor, out Transform boxCarrySocket) ||
            boxTransferable.transform.parent != boxCarrySocket)
            throw new InvalidOperationException("Caja no quedÃ³ en TwoHandCenter.");
        yield return new WaitForSecondsRealtime(0.18f);
        boxRightError = Mathf.Max(boxRightError, carryPresenter.CurrentRightHandError);
        boxLeftError = Mathf.Max(boxLeftError, carryPresenter.CurrentLeftHandError);
        if (boxRightError > 0.17f || boxLeftError > 0.17f)
            throw new InvalidOperationException(
                "Carry Box 2H fuera de tolerancia. R=" + boxRightError.ToString("0.000") +
                " m; L=" + boxLeftError.ToString("0.000") + " m.");
        yield return CaptureEvidence("09_box_carry.png");
    }

    private IEnumerator RunAvatarScaleMatrix()
    {
        float[] scales = { 0.88f, 1.00f, 1.12f };
        Vector3 basePosition = new Vector3(14f, 0f, 14f);
        scaleMatrixMaxError = 0f;
        for (int i = 0; i < scales.Length; i++)
        {
            GameObject clone = Instantiate(
                humanoidPrefab,
                basePosition + Vector3.right * (i * 2.5f),
                Quaternion.identity);
            clone.name = "__BB18_ScaleProbe_" + scales[i].ToString("0.00") + "__";
            clone.transform.localScale *= scales[i];
            Animator cloneAnimator = clone.GetComponentInChildren<Animator>(true);
            if (cloneAnimator == null || cloneAnimator.avatar == null ||
                !cloneAnimator.avatar.isValid || !cloneAnimator.avatar.isHuman)
                throw new InvalidOperationException("Scale Matrix produjo Humanoid inválido.");
            cloneAnimator.applyRootMotion = false;
            cloneAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            BistroBuilderCharacterAnimationDriver cloneDriver =
                clone.AddComponent<BistroBuilderCharacterAnimationDriver>();
            cloneDriver.AssignCatalogIfMissing(motionCatalog);
            if (!cloneDriver.TryPlaySemanticMotion(
                    "locomotion.idle", "locomotion.idle", 1f, out _))
                throw new InvalidOperationException("Scale Matrix no pudo estabilizar locomotion.idle.");
            yield return null;
            yield return new WaitForSecondsRealtime(0.10f);

            BistroBuilderCharacterRigAdapter adapter =
                clone.AddComponent<BistroBuilderCharacterRigAdapter>();
            if (!adapter.ValidateConfiguration(out string error))
                throw new InvalidOperationException(
                    "Scale Matrix no configuró IK en " + scales[i].ToString("0.00") + ": " + error);
            Transform hand = cloneAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null)
                throw new InvalidOperationException("Scale Matrix no resolvió RightHand.");

            GameObject targetObject = new GameObject("ScaleProbeTarget");
            targetObject.transform.position =
                hand.position + clone.transform.forward * (0.10f * scales[i]) +
                clone.transform.right * (0.06f * scales[i]);
            targetObject.transform.rotation = hand.rotation;
            adapter.SetRightHandTarget(targetObject.transform, 1f);
            yield return new WaitForSecondsRealtime(0.30f);
            float errorDistance = adapter.GetHandError(true, targetObject.transform);
            scaleMatrixMaxError = Mathf.Max(scaleMatrixMaxError, errorDistance);
            cloneDriver.StopTransientMotion();
            Destroy(targetObject);
            Destroy(clone);
            yield return null;
        }
        if (scaleMatrixMaxError > 0.10f)
            throw new InvalidOperationException(
                "Avatar Scale Matrix superó tolerancia IK: " +
                scaleMatrixMaxError.ToString("0.000") + " m.");
    }
    private static IEnumerator WaitForCommitFrontier(
        BistroBuilderInteractionHandle handle,
        float timeoutSeconds)
    {
        float deadline = Time.unscaledTime + Mathf.Max(0.1f, timeoutSeconds);
        while (handle != null && !handle.IsTerminal &&
               handle.CommitState != BistroBuilderInteractionCommitState.AwaitingConfirmation &&
               Time.unscaledTime < deadline)
            yield return null;
        if (handle == null)
            throw new InvalidOperationException("Handle nulo esperando Commit Frontier.");
        if (handle.IsTerminal)
            throw new InvalidOperationException(
                "InteracciÃ³n terminÃ³ antes del Commit Frontier: " + handle.FailureReason);
        if (handle.CommitState != BistroBuilderInteractionCommitState.AwaitingConfirmation)
            throw new TimeoutException("No se alcanzÃ³ Commit Frontier dentro del watchdog.");
    }
    private BistroBuilderResolvedInteractionPlan CreateSeatPlan(
        string id,
        string owner,
        BistroBuilderInteractionOperation operation,
        string motionId,
        float commitTime)
    {
        return new BistroBuilderResolvedInteractionPlan
        {
            interactionId = id,
            ownerId = owner,
            family = BistroBuilderInteractionFamily.Seat,
            operation = operation,
            actor = actor,
            target = seat.gameObject,            interactionFrame = seat.CustomerApproachPoint,
            seatFrame = seat.SeatPoint,
            exitFrame = seat.CustomerApproachPoint,
            seat = seat,
            semanticMotionId = motionId,
            fallbackMotionId = "seat.generic",
            fallbackMotionDuration = 0.7f,
            commitNormalizedTime = commitTime,
            operationTimeoutSeconds = 4f,
            commitTimeoutSeconds = 1.2f,
            requiresCommitConfirmation = true
        };
    }

    private static IEnumerator WaitForTerminal(
        BistroBuilderInteractionHandle handle,
        float timeoutSeconds)
    {
        float deadline = Time.unscaledTime + Mathf.Max(0.1f, timeoutSeconds);
        while (handle != null && !handle.IsTerminal && Time.unscaledTime < deadline)
            yield return null;

        if (handle == null)
            throw new InvalidOperationException("Handle nulo durante gate visual BB18.");
        if (!handle.IsTerminal)
            throw new TimeoutException("InteracciÃ³n visual superÃ³ watchdog: " + handle.InteractionId);
    }

    private IEnumerator CaptureEvidence(string fileName)
    {
        if (evidenceCamera == null) yield break;
        yield return new WaitForEndOfFrame();        const int width = 1280;
        const int height = 720;
        RenderTexture renderTexture = new RenderTexture(width, height, 24);
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previousTarget = evidenceCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            evidenceCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            evidenceCamera.Render();
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            string directory = ResolveEvidenceDirectory();
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
        }
        finally
        {
            evidenceCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Destroy(renderTexture);
            Destroy(texture);
        }
    }

    private static string ResolveEvidenceDirectory()
    {
        string configured = Environment.GetEnvironmentVariable("BB18_VISUAL_EVIDENCE_DIR");
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(Application.persistentDataPath, "BB18VisualEvidence");
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
    private void ConfirmCommit(
        BistroBuilderInteractionHandle requestedHandle,
        BistroBuilderResolvedInteractionPlan plan)
    {
        if (requestedHandle == null) return;
        if (plan != null && (
            string.Equals(plan.interactionId, "bb18.visual.pickup", StringComparison.Ordinal) ||
            string.Equals(plan.interactionId, "bb18.visual.place", StringComparison.Ordinal) ||
            string.Equals(plan.interactionId, "bb18.visual.box.pickup", StringComparison.Ordinal)))
            return;
        requestedHandle.ConfirmCommit();
    }

    private static void Finish(bool success, string message)
    {
        string report =
            "=== BISTRO BUILDER - BB18 VISUAL HUMANOID GATE ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n" +
            "Evidence=" + ResolveEvidenceDirectory();
        string configured = Environment.GetEnvironmentVariable("BB18_VISUAL_REPORT_PATH");
        string reportPath = !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(Application.persistentDataPath, "BB18VisualReport.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? Application.persistentDataPath);
        File.WriteAllText(reportPath, report);
        if (success) Debug.Log(report);
        else Debug.LogError(report);
        Application.Quit(success ? 0 : 1);
    }

    private void OnDestroy()
    {
        if (interactionService != null)
            interactionService.CommitRequested -= ConfirmCommit;
        if (navigation != null && actor != null)
        {
            navigation.RemoveAgentPresence("bb18.visual.walk.seat");
            navigation.RemoveAgentPresence("bb18.visual.walk.transfer");
        }
        if (seat != null && !string.IsNullOrWhiteSpace(seat.ReservationOwnerId))
            seat.ReleaseReservation("bb18.visual.seat", true);
    }
}
#endif



