using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class BistroBuilderAssetInteractionDescriptor : MonoBehaviour
{
    [SerializeField] private List<BistroBuilderAnimationInteractionSlotDefinition> slots =
        new List<BistroBuilderAnimationInteractionSlotDefinition>();

    public IReadOnlyList<BistroBuilderAnimationInteractionSlotDefinition> Slots => slots;

    public bool TryResolveSlot(
        BistroBuilderInteractionFamily family,
        string requestedSlotId,
        out BistroBuilderAnimationInteractionSlotDefinition slot)
    {
        slot = null;
        string normalized = BistroBuilderMotionProfile.NormalizeId(requestedSlotId);
        for (int i = 0; i < slots.Count; i++)
        {
            BistroBuilderAnimationInteractionSlotDefinition candidate = slots[i];
            if (candidate == null || candidate.Family != family) continue;
            if (!string.IsNullOrWhiteSpace(normalized) && candidate.SlotId != normalized) continue;
            slot = candidate;
            return true;
        }
        return false;
    }

    public bool TryBuildPlan(
        BistroBuilderInteractionFamily family,
        BistroBuilderInteractionOperation operation,
        string requestedSlotId,
        GameObject actor,
        string ownerId,
        out BistroBuilderResolvedInteractionPlan plan,
        out string error)
    {
        plan = null;
        error = string.Empty;
        if (!TryResolveSlot(family, requestedSlotId, out BistroBuilderAnimationInteractionSlotDefinition slot))
        {
            error = name + " no ofrece un slot compatible.";
            return false;
        }

        plan = new BistroBuilderResolvedInteractionPlan
        {
            interactionId = Guid.NewGuid().ToString("N"),
            ownerId = ownerId,
            family = family,
            operation = operation,
            actor = actor,
            target = gameObject,
            interactionFrame = slot.InteractionFrame,
            seatFrame = slot.SeatFrame,
            exitFrame = slot.ExitFrame,
            transferTarget = slot.TransferTarget,
            seat = slot.Seat,
            door = slot.Door
        };
        return plan.Validate(out error);
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < slots.Count; i++)
        {
            BistroBuilderAnimationInteractionSlotDefinition slot = slots[i];
            if (slot == null || !slot.ValidateConfiguration(out error)) return false;
            string key = ((int)slot.Family) + ":" + slot.SlotId;
            if (!ids.Add(key))
            {
                error = name + " contiene un slot duplicado: " + key;
                return false;
            }
        }
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(List<BistroBuilderAnimationInteractionSlotDefinition> configuredSlots)
    {
        slots = configuredSlots ?? new List<BistroBuilderAnimationInteractionSlotDefinition>();
    }
#endif
}
