using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BistroBuilderAnimationTargetBinding : MonoBehaviour
{
    [SerializeField] private string targetId = string.Empty;
    [SerializeField] private long generation = 1;
    [SerializeField] private BistroBuilderAnimationTargetKind targetKind = BistroBuilderAnimationTargetKind.Workstation;
    [SerializeField] private List<BistroBuilderAnimationTargetSlot> slots = new List<BistroBuilderAnimationTargetSlot>();

    public string TargetId => string.IsNullOrWhiteSpace(targetId) ? gameObject.name : targetId.Trim();
    public long Generation => Math.Max(1L, generation);
    public BistroBuilderAnimationTargetKind TargetKind => targetKind;
    public IReadOnlyList<BistroBuilderAnimationTargetSlot> Slots => slots;
    public BistroBuilderAnimationTargetHandle Handle => new BistroBuilderAnimationTargetHandle(targetKind, TargetId, Generation);

    public bool TryResolveSlot(string requestedSlotId, BistroBuilderInteractionFamily family, out BistroBuilderAnimationTargetSlot slot)
    {
        slot = null;
        string normalized = BistroBuilderMotionProfile.NormalizeId(requestedSlotId);
        for (int i = 0; i < slots.Count; i++)
        {
            BistroBuilderAnimationTargetSlot candidate = slots[i];
            if (candidate == null || candidate.Family != family) continue;
            if (!string.IsNullOrWhiteSpace(normalized) && candidate.SlotId != normalized) continue;
            slot = candidate;
            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string configuredTargetId,
        long configuredGeneration,
        BistroBuilderAnimationTargetKind configuredTargetKind,
        List<BistroBuilderAnimationTargetSlot> configuredSlots)
    {
        targetId = configuredTargetId ?? string.Empty;
        generation = Math.Max(1L, configuredGeneration);
        targetKind = configuredTargetKind;
        slots = configuredSlots ?? new List<BistroBuilderAnimationTargetSlot>();
    }
#endif

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(TargetId)) { error = name + " needs stable TargetId."; return false; }
        if (targetKind == BistroBuilderAnimationTargetKind.None) { error = name + " needs target kind."; return false; }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < slots.Count; i++)
        {
            BistroBuilderAnimationTargetSlot slot = slots[i];
            if (slot == null || !slot.ValidateConfiguration(out error)) return false;
            string key = slot.Family + ":" + slot.SlotId;
            if (!keys.Add(key)) { error = name + " contains duplicate animation slot " + key; return false; }
        }
        return true;
    }
}
