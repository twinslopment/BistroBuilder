using System;
using System.Collections.Generic;
using UnityEngine;

public enum BBPLFSPlacementPattern
{
    InteriorGrid = 0,
    Staggered = 1,
    CenterAxis = 2,
    WallBand = 3,
    Perimeter = 4
}

[Serializable]
public sealed class BBPLFSFurnishingSlot
{
    [SerializeField] private BBPLFSLayoutRole role = BBPLFSLayoutRole.Other;
    [SerializeField, Min(1)] private int count = 1;
    [SerializeField] private bool required = true;

    public BBPLFSLayoutRole Role => role;
    public int Count => Mathf.Max(1, count);
    public bool Required => required;

    public BBPLFSFurnishingSlot(BBPLFSLayoutRole role, int count, bool required = true)
    {
        this.role = role;
        this.count = Mathf.Max(1, count);
        this.required = required;
    }
}

[CreateAssetMenu(fileName = "BBPLFSFurnishingSet_", menuName = "Bistro Builder/BBPLFS/Furnishing Set")]
public sealed class BBPLFSFurnishingSet : ScriptableObject
{
    [SerializeField] private string setId = "furnishing_set";
    [SerializeField] private BBPLFSSpaceFunction spaceFunction = BBPLFSSpaceFunction.Dining;
    [SerializeField, Min(0)] private int functionalCapacity;
    [SerializeField] private BBPLFSPlacementPattern[] allowedPatterns = Array.Empty<BBPLFSPlacementPattern>();
    [SerializeField] private BBPLFSFurnishingSlot[] slots = Array.Empty<BBPLFSFurnishingSlot>();
    [SerializeField] private string[] styleTags = Array.Empty<string>();

    public string SetId => string.IsNullOrWhiteSpace(setId) ? name : setId.Trim().ToLowerInvariant();
    public BBPLFSSpaceFunction SpaceFunction => spaceFunction;
    public int FunctionalCapacity => Mathf.Max(0, functionalCapacity);
    public IReadOnlyList<BBPLFSPlacementPattern> AllowedPatterns => allowedPatterns;
    public IReadOnlyList<BBPLFSFurnishingSlot> Slots => slots;
    public IReadOnlyList<string> StyleTags => styleTags;

#if UNITY_EDITOR
    public void EditorConfigure(string id, BBPLFSSpaceFunction function, int capacity,
        BBPLFSPlacementPattern[] patterns, BBPLFSFurnishingSlot[] configuredSlots, string[] tags)
    {
        setId = string.IsNullOrWhiteSpace(id) ? "furnishing_set" : id.Trim().ToLowerInvariant();
        spaceFunction = function;
        functionalCapacity = Mathf.Max(0, capacity);
        allowedPatterns = patterns ?? Array.Empty<BBPLFSPlacementPattern>();
        slots = configuredSlots ?? Array.Empty<BBPLFSFurnishingSlot>();
        styleTags = tags ?? Array.Empty<string>();
    }
#endif
}
