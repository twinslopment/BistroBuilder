using System;
using System.Collections.Generic;
using UnityEngine;

public enum BBPLFSLayoutRole
{
    Other = 0,
    DiningTable = 1,
    DiningSeat = 2,
    Counter = 3,
    KitchenEquipment = 4,
    Storage = 5,
    Decoration = 6
}

[CreateAssetMenu(
    fileName = "BBPLFSAssetProfile_",
    menuName = "Bistro Builder/BBPLFS/Asset Layout Profile")]
public sealed class BBPLFSAssetLayoutProfile : ScriptableObject
{
    [SerializeField] private RestaurantPlaceableItemDefinition itemDefinition;
    [SerializeField] private BBPLFSLayoutRole layoutRole;
    [SerializeField] private string layoutFamily = "generic";
    [SerializeField] private Vector2 fastFootprint = Vector2.one;
    [SerializeField] private int functionalCapacity = 1;
    [SerializeField] private string[] styleTags = Array.Empty<string>();

    public RestaurantPlaceableItemDefinition ItemDefinition => itemDefinition;
    public BBPLFSLayoutRole LayoutRole => layoutRole;
    public string LayoutFamily => layoutFamily;
    public Vector2 FastFootprint => new(Mathf.Max(0.05f, fastFootprint.x), Mathf.Max(0.05f, fastFootprint.y));
    public int FunctionalCapacity => Mathf.Max(1, functionalCapacity);
    public IReadOnlyList<string> StyleTags => styleTags;
    public bool IsUsable => itemDefinition != null && itemDefinition.HasValidPrefab;

#if UNITY_EDITOR
    public void EditorConfigure(
        RestaurantPlaceableItemDefinition definition,
        BBPLFSLayoutRole role,
        string family,
        Vector2 footprint,
        int capacity,
        string[] tags)
    {
        itemDefinition = definition;
        layoutRole = role;
        layoutFamily = string.IsNullOrWhiteSpace(family) ? "generic" : family.Trim();
        fastFootprint = new Vector2(Mathf.Max(0.05f, footprint.x), Mathf.Max(0.05f, footprint.y));
        functionalCapacity = Mathf.Max(1, capacity);
        styleTags = tags ?? Array.Empty<string>();
    }
#endif
}
