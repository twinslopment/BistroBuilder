using System;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderAnimationTargetSlot
{
    [SerializeField] private string slotId = "default";
    [SerializeField] private BistroBuilderInteractionFamily family;
    [SerializeField] private Transform interactionFrame;
    [SerializeField] private Transform seatFrame;
    [SerializeField] private Transform exitFrame;
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform lookTarget;

    public string SlotId => BistroBuilderMotionProfile.NormalizeId(slotId);
    public BistroBuilderInteractionFamily Family => family;
    public Transform InteractionFrame => interactionFrame;
    public Transform SeatFrame => seatFrame;
    public Transform ExitFrame => exitFrame;
    public Transform RightHandTarget => rightHandTarget;
    public Transform LeftHandTarget => leftHandTarget;
    public Transform LookTarget => lookTarget;

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string configuredSlotId,
        BistroBuilderInteractionFamily configuredFamily,
        Transform configuredInteractionFrame,
        Transform configuredSeatFrame,
        Transform configuredExitFrame,
        Transform configuredRightHandTarget,
        Transform configuredLeftHandTarget,
        Transform configuredLookTarget)
    {
        slotId = configuredSlotId ?? string.Empty;
        family = configuredFamily;
        interactionFrame = configuredInteractionFrame;
        seatFrame = configuredSeatFrame;
        exitFrame = configuredExitFrame;
        rightHandTarget = configuredRightHandTarget;
        leftHandTarget = configuredLeftHandTarget;
        lookTarget = configuredLookTarget;
    }
#endif

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(SlotId)) { error = "Animation target slot needs SlotId."; return false; }
        if (family == BistroBuilderInteractionFamily.None) { error = "Animation target slot needs family."; return false; }
        if (interactionFrame == null) { error = "Animation target slot " + SlotId + " needs InteractionFrame."; return false; }
        return true;
    }
}
