using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents granted bar or dining seating. Only this member's visual transform moves;
/// the logical CustomerGroup, Navigation, capacities and spatial leases are never written.
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class BistroBuilderCustomerBarSeatPresenter : MonoBehaviour
{
    public enum VisualState { Baseline, EnteringSeat, Seated, LeavingSeat, Unavailable }
    private CustomerGroup group;
    private CustomerMovementView movement;
    private BistroBuilderCustomerHumanoidProfile profile;
    private BistroBuilderCharacterAnimationServiceV1 animationService;
    private BistroBuilderBarServiceRegistry bars;
    private RestaurantSeatRegistry diningSeats;
    private RestaurantSeat alignedDiningSeat;
    private readonly List<RestaurantSeat> diningSeatBuffer = new List<RestaurantSeat>();
    private BistroBuilderAnimationActorBinding actor;
    private Animator animator;
    private Transform hips;
    private Transform alignedSeat;
    private int memberIndex;
    private Vector3 baselinePosition;
    private Quaternion baselineRotation;
    private Vector3 transitionFromPosition;
    private Quaternion transitionFromRotation;
    private float transitionWeight;
    private BistroBuilderAnimationExecutionHandle active;
    private bool requestPending;
    private readonly List<BistroBuilderBarServiceSpot> occupied = new List<BistroBuilderBarServiceSpot>();
    public VisualState State { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public Transform Hips => hips;
    public Animator Animator => animator;
    public Transform AlignedSeat => alignedSeat;
    public string CurrentMotionId { get; private set; } = string.Empty;

    public bool ConfigureRuntime(CustomerGroup customer, int index, BistroBuilderCustomerHumanoidProfile authoredProfile)
    {
        group = customer; memberIndex = index; profile = authoredProfile;
        movement = group != null ? group.GetComponent<CustomerMovementView>() : null;
        animator = GetComponentInChildren<Animator>(true);
        if (group == null || index < 1 || profile == null || !profile.ValidateConfiguration(out _) ||
            animator == null || !animator.isHuman) { State = VisualState.Unavailable; return false; }
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null) { State = VisualState.Unavailable; return false; }
        var player = GetComponent<BistroBuilderMotionRecipePlayerV1>();
        if (player == null) player = gameObject.AddComponent<BistroBuilderMotionRecipePlayerV1>();
        player.ConfigureRuntime(animator, null);
        actor = GetComponent<BistroBuilderAnimationActorBinding>();
        if (actor == null) actor = gameObject.AddComponent<BistroBuilderAnimationActorBinding>();
        actor.ConfigureRuntime("customer-member:" + group.GroupId + ":" + memberIndex, null, player, null, null);
        baselinePosition = transform.localPosition; baselineRotation = transform.localRotation;
        State = VisualState.Baseline;
        ResolveServices();
        Request(profile.BaselineMotionId, BistroBuilderInteractionOperation.Use);
        return true;
    }

    private void ResolveServices()
    {
        if (bars == null) bars = FindFirstObjectByType<BistroBuilderBarServiceRegistry>();
        if (diningSeats == null) diningSeats = FindFirstObjectByType<RestaurantSeatRegistry>();
        if (animationService != null) return;
        animationService = FindFirstObjectByType<BistroBuilderCharacterAnimationServiceV1>();
        if (animationService != null)
        {
            animationService.ExecutionFinished += HandleFinished;
            animationService.RefreshRegistry();
        }
    }

    private void Update()
    {
        if (group == null || actor == null || State == VisualState.Unavailable) return;
        ResolveServices();
        Transform desiredSeat = ResolveGrantedSeat();
        if (desiredSeat != null && State == VisualState.Baseline)
        {
            alignedSeat = desiredSeat;
            transitionFromPosition = transform.localPosition; transitionFromRotation = transform.localRotation;
            transitionWeight = 0f;
            CancelCurrent();
            State = VisualState.EnteringSeat;
            Request(profile.SitMotionId, BistroBuilderInteractionOperation.Sit);
        }
        else if ((State == VisualState.EnteringSeat || State == VisualState.Seated) && desiredSeat != alignedSeat)
        {
            // A released logical seat never remains represented as occupied indefinitely.
            transitionFromPosition = transform.localPosition; transitionFromRotation = transform.localRotation;
            alignedSeat = null; transitionWeight = 0f;
            CancelCurrent(); State = VisualState.LeavingSeat;
            Request(profile.StandMotionId, BistroBuilderInteractionOperation.Stand);
        }
        if (requestPending && animationService != null) StartPendingRequest();
    }

    private Transform ResolveGrantedSeat()
    {
        if (!group.IsOccupyingBar) return ResolveDiningSeat();
        alignedDiningSeat = null;
        if (bars == null || movement == null || movement.IsMoving ||
            group.CurrentState == CustomerGroupState.WalkingToBar || group.CurrentState == CustomerGroupState.Leaving ||
            group.CurrentState == CustomerGroupState.Finished) return null;
        bars.GetOccupiedSpots(group, occupied);
        int member = 0;
        foreach (var spot in occupied)
        {
            int first = member + 1; member += spot.Capacity;
            if (memberIndex < first || memberIndex > member) continue;
            var seat = spot.AttachedSeat;
            if (spot.Capacity != 1 || seat == null || seat.Occupant != group ||
                !seat.ValidateRuntimeAssociation(out _) ||
                spot.GetComponent<BistroBuilderBarSpatialAdapter>()?.HasCustomerLease != true) return null;
            // Arrival comes from Navigation's actual movement view; after load the native
            // checkpoint can be stationary, but still must be at its floor approach.
            Vector3 delta = group.transform.position - group.AssignedBarSpot.CustomerApproachPoint.position;
            delta.y = 0f;
            if (!movement.HasReachedDestination && (group.CurrentState == CustomerGroupState.Entering ||
                group.CurrentState == CustomerGroupState.WalkingToTable || delta.sqrMagnitude > 0.12f * 0.12f)) return null;
            return seat.SeatFrame;
        }
        return null;
    }

    private Transform ResolveDiningSeat()
    {
        alignedDiningSeat = null;
        RestaurantTable table = group.AssignedTable;
        // Gameplay grants the whole table; topology identifies its actual chairs.
        // This only chooses a visual member position, without reserving or creating capacity.
        if (table == null || table.AssignedCustomerGroup != group || diningSeats == null ||
            movement == null || movement.IsMoving || BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring ||
            !HasCompletedDiningArrival(group.CurrentState)) return null;
        diningSeatBuffer.Clear();
        foreach (RestaurantSeat seat in diningSeats.RegisteredSeats)
        {
            if (seat == null || !seat.isActiveAndEnabled || !seat.IsAssociated ||
                seat.AssociatedTable.Table != table || seat.SeatPoint == null ||
                !string.IsNullOrWhiteSpace(seat.ReservationOwnerId)) continue;
            diningSeatBuffer.Add(seat);
        }
        diningSeatBuffer.Sort((a, b) => a.AssociatedSlotIndex.CompareTo(b.AssociatedSlotIndex));
        if (diningSeatBuffer.Count < group.GroupSize) return null;
        for (int i = 1; i < diningSeatBuffer.Count; i++)
            if (diningSeatBuffer[i - 1].AssociatedSlotIndex == diningSeatBuffer[i].AssociatedSlotIndex) return null;
        if (memberIndex > diningSeatBuffer.Count) return null;
        alignedDiningSeat = diningSeatBuffer[memberIndex - 1];
        return alignedDiningSeat.SeatPoint;
    }
    private static bool HasCompletedDiningArrival(CustomerGroupState state)
    {
        // These authoritative states follow seating completion. service.runtime restores
        // them without serializing Navigation's transient HasReachedDestination flag.
        switch (state)
        {
            case CustomerGroupState.Seated:
            case CustomerGroupState.WaitingForWaiter:
            case CustomerGroupState.Ordering:
            case CustomerGroupState.WaitingForFood:
            case CustomerGroupState.Eating:
            case CustomerGroupState.WaitingForBill:
            case CustomerGroupState.Paying:
                return true;
            default:
                return false;
        }
    }
    private string pendingMotion;
    private BistroBuilderInteractionOperation pendingOperation;
    private void Request(string motion, BistroBuilderInteractionOperation operation)
    { pendingMotion = motion; pendingOperation = operation; requestPending = true; StartPendingRequest(); }
    private void StartPendingRequest()
    {
        if (!requestPending || animationService == null || active.IsValid) return;
        var request = new BistroBuilderAnimationExecutionRequest
        {
            ownerId = "bar-member-presentation:" + group.GroupId + ":" + memberIndex,
            actorId = actor.ActorId, family = BistroBuilderInteractionFamily.Seat,
            operation = pendingOperation, requestedMotionId = pendingMotion,
            requiresAuthoritativeCommit = false, protectedQualityWindow = true,
            watchdogTimeoutSeconds = 10f
        };
        if (!animationService.TryStart(request, out active, out string error))
        {
            LastError = error; requestPending = false; State = VisualState.Unavailable;
            alignedSeat = null; ResetVisualPose(); return;
        }
        CurrentMotionId = pendingMotion; LastError = string.Empty; requestPending = false;
    }

    private void HandleFinished(BistroBuilderMotionExecutionResult result)
    {
        if (!active.IsValid || !active.Equals(result.handle)) return;
        active = default;
        if (result.code != BistroBuilderMotionResultCode.Completed)
        {
            alignedSeat = null; State = VisualState.Unavailable; LastError = result.message; ResetVisualPose(); return;
        }
        if (State == VisualState.EnteringSeat)
        { transitionWeight = 1f; State = VisualState.Seated; Request(profile.IdleMotionId, BistroBuilderInteractionOperation.Use); }
        else if (State == VisualState.LeavingSeat)
        { ResetVisualPose(); State = VisualState.Baseline; Request(profile.BaselineMotionId, BistroBuilderInteractionOperation.Use); }
    }

    private void LateUpdate()
    {
        if (animator == null || hips == null || State == VisualState.Unavailable) return;
        if (active.IsValid && animationService != null && animationService.TryGetProgress(active, out var progress))
            transitionWeight = Mathf.SmoothStep(0f, 1f, progress.normalizedTime);
        if (alignedSeat != null && (State == VisualState.EnteringSeat || State == VisualState.Seated))
        {
            Vector3 hipsLocal = transform.InverseTransformPoint(hips.position);
            Quaternion seatRotation = alignedDiningSeat != null
                ? Quaternion.LookRotation(alignedDiningSeat.CalculateFacingDirectionAtPose(alignedDiningSeat.transform.rotation), alignedSeat.up)
                : alignedSeat.rotation;
            Quaternion desiredRotation = Quaternion.Inverse(transform.parent.rotation) * seatRotation;
            Vector3 pelvisTarget = alignedSeat.position + alignedSeat.up * profile.PelvisAboveSeatMeters;
            Vector3 desiredPosition = transform.parent.InverseTransformPoint(pelvisTarget) -
                desiredRotation * Vector3.Scale(hipsLocal, transform.localScale);
            float weight = State == VisualState.Seated ? 1f : transitionWeight;
            transform.localRotation = Quaternion.Slerp(transitionFromRotation, desiredRotation, weight);
            transform.localPosition = Vector3.Lerp(transitionFromPosition, desiredPosition, weight);
        }
        else if (State == VisualState.LeavingSeat)
        {
            transform.localPosition = Vector3.Lerp(transitionFromPosition, baselinePosition, transitionWeight);
            transform.localRotation = Quaternion.Slerp(transitionFromRotation, baselineRotation, transitionWeight);
        }
    }

    private void CancelCurrent()
    {
        if (animationService != null && actor != null)
        {
            // Clear our handle before cancellation emits its synchronous completion event.
            active = default;
            animationService.TryRehydrate(new BistroBuilderAnimationRehydrationRequest { actorId = actor.ActorId }, out _);
        }
        requestPending = false;
    }
    private void ResetVisualPose() { transform.localPosition = baselinePosition; transform.localRotation = baselineRotation; }
    private void OnEnable()
    {
        if (actor == null || profile == null) return;
        State = VisualState.Baseline; ResolveServices(); Request(profile.BaselineMotionId, BistroBuilderInteractionOperation.Use);
    }
    private void OnDisable()
    {
        CancelCurrent();
        if (animationService != null) animationService.ExecutionFinished -= HandleFinished;
        animationService = null; alignedSeat = null; ResetVisualPose(); State = VisualState.Baseline;
    }
}
