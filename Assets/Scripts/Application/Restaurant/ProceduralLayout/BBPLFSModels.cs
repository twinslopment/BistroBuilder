using System;
using System.Collections.Generic;
using UnityEngine;

public enum BBPLFSSpaceFunction
{
    Unassigned = 0,
    Dining = 1,
    Kitchen = 2,
    Bar = 3,
    Waiting = 4,
    Support = 5
}

public enum BBPLFSGoalProfile
{
    Balanced = 0,
    MaxCapacity = 1,
    MaxComfort = 2,
    ServiceEfficient = 3,
    Premium = 4
}

public enum BBPLFSDesignScopeKind
{
    Selection = 0,
    Zone = 1,
    Room = 2,
    MultipleRooms = 3,
    WholePremises = 4
}
[Serializable]
public sealed class BBPLFSExistingObjectSnapshot
{
    [SerializeField] private string instanceId;
    [SerializeField] private string itemId;
    [SerializeField] private Vector3 worldPosition;
    [SerializeField] private Quaternion worldRotation;
    [SerializeField] private Vector2 footprintSize;
    [SerializeField] private bool editingEnabled;
    [SerializeField] private bool movable;

    public string InstanceId => instanceId;
    public string ItemId => itemId;
    public Vector3 WorldPosition => worldPosition;
    public Quaternion WorldRotation => worldRotation;
    public Vector2 FootprintSize => footprintSize;
    public bool EditingEnabled => editingEnabled;
    public bool Movable => movable;
    public bool Locked => !editingEnabled || !movable;

    public BBPLFSExistingObjectSnapshot(RestaurantPlaceableObject placeable)
    {
        instanceId = placeable != null ? placeable.InstanceId : string.Empty;
        itemId = placeable != null && placeable.ItemDefinition != null ? placeable.ItemDefinition.ItemId : string.Empty;
        worldPosition = placeable != null ? placeable.transform.position : Vector3.zero;
        worldRotation = placeable != null ? placeable.transform.rotation : Quaternion.identity;
        footprintSize = placeable != null && placeable.TryGetComponent(out RestaurantPlacementFootprint fp) ? fp.Size : Vector2.zero;
        if (placeable != null && placeable.TryGetComponent(out RestaurantEditableObject editable))
        {
            editingEnabled = editable.EditingEnabled;
            movable = editable.CanMove;
        }
        else
        {
            editingEnabled = false;
            movable = false;
        }
    }
}

[Serializable]
public sealed class BBPLFSPremisesSpaceSnapshot
{
    [SerializeField] private string spaceId;
    [SerializeField] private RestaurantArea sourceArea;
    [SerializeField] private Bounds worldBounds;
    [SerializeField] private float floorAreaSquareMeters;
    [SerializeField] private List<BBPLFSExistingObjectSnapshot> existingObjects = new();

    public string SpaceId => spaceId;
    public RestaurantArea SourceArea => sourceArea;
    public Bounds WorldBounds => worldBounds;
    public float FloorAreaSquareMeters => floorAreaSquareMeters;
    public IReadOnlyList<BBPLFSExistingObjectSnapshot> ExistingObjects => existingObjects;

    public BBPLFSPremisesSpaceSnapshot(
        RestaurantArea sourceArea,
        Bounds worldBounds,
        List<BBPLFSExistingObjectSnapshot> existingObjects = null)
    {
        this.sourceArea = sourceArea;
        spaceId = sourceArea != null ? sourceArea.AreaId : string.Empty;
        this.worldBounds = worldBounds;
        floorAreaSquareMeters = Mathf.Max(0f, worldBounds.size.x * worldBounds.size.z);
        this.existingObjects = existingObjects ?? new List<BBPLFSExistingObjectSnapshot>();
    }
}

[Serializable]
public sealed class BBPLFSPremisesModel
{
    [SerializeField] private string revision;
    [SerializeField] private List<BBPLFSPremisesSpaceSnapshot> spaces = new();

    public string Revision => revision;
    public IReadOnlyList<BBPLFSPremisesSpaceSnapshot> Spaces => spaces;
    public BBPLFSPremisesModel(
        string revision,
        List<BBPLFSPremisesSpaceSnapshot> spaces)
    {
        this.revision = revision ?? string.Empty;
        this.spaces = spaces ?? new List<BBPLFSPremisesSpaceSnapshot>();
    }

    public bool TryGetSpace(
        RestaurantArea area,
        out BBPLFSPremisesSpaceSnapshot space)
    {
        for (int index = 0; index < spaces.Count; index++)
        {
            BBPLFSPremisesSpaceSnapshot candidate = spaces[index];
            if (candidate != null && candidate.SourceArea == area)
            {
                space = candidate;
                return true;
            }
        }

        space = null;
        return false;
    }
}

[Serializable]
public sealed class BBPLFSDesignScope
{
    [SerializeField] private BBPLFSDesignScopeKind kind = BBPLFSDesignScopeKind.Room;
    [SerializeField] private RestaurantArea[] areas = Array.Empty<RestaurantArea>();
    public BBPLFSDesignScopeKind Kind => kind;
    public IReadOnlyList<RestaurantArea> Areas => areas;
    public BBPLFSDesignScope(
        BBPLFSDesignScopeKind kind,
        params RestaurantArea[] areas)
    {
        this.kind = kind;
        this.areas = areas ?? Array.Empty<RestaurantArea>();
    }

    public bool Contains(RestaurantArea area)
    {
        if (area == null || areas == null)
        {
            return false;
        }

        for (int index = 0; index < areas.Length; index++)
        {
            if (areas[index] == area)
            {
                return true;
            }
        }

        return false;
    }
}

[Serializable]
public sealed class BBPLFSLayoutBrief
{
    [SerializeField] private BBPLFSDesignScope designScope;
    [SerializeField] private BBPLFSSpaceFunction spaceFunction = BBPLFSSpaceFunction.Dining;
    [SerializeField] private BBPLFSGoalProfile goalProfile = BBPLFSGoalProfile.Balanced;
    [SerializeField] private int targetCapacity = 20;
    [SerializeField] private string[] styleTags = Array.Empty<string>();
    [SerializeField] private bool preserveExisting = true;

    public BBPLFSDesignScope DesignScope => designScope;
    public BBPLFSSpaceFunction SpaceFunction => spaceFunction;
    public BBPLFSGoalProfile GoalProfile => goalProfile;
    public int TargetCapacity => Mathf.Max(1, targetCapacity);
    public IReadOnlyList<string> StyleTags => styleTags;
    public bool PreserveExisting => preserveExisting;

    public BBPLFSLayoutBrief(
        BBPLFSDesignScope designScope,
        BBPLFSSpaceFunction spaceFunction,
        BBPLFSGoalProfile goalProfile,
        int targetCapacity,
        string[] styleTags = null,
        bool preserveExisting = true)
    {
        this.designScope = designScope;
        this.spaceFunction = spaceFunction;
        this.goalProfile = goalProfile;
        this.targetCapacity = Mathf.Max(1, targetCapacity);
        this.styleTags = styleTags ?? Array.Empty<string>();
        this.preserveExisting = preserveExisting;
    }
}

[Serializable]
public struct BBPLFSLayoutPlacement
{
    public string ItemId;
    public string Role;
    public Vector3 WorldPosition;
    public Quaternion WorldRotation;

    public BBPLFSLayoutPlacement(
        string itemId,
        string role,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        ItemId = itemId ?? string.Empty;
        Role = role ?? string.Empty;
        WorldPosition = worldPosition;
        WorldRotation = worldRotation;
    }
}
[Serializable]
public sealed class BBPLFSLayoutCandidate
{
    [SerializeField] private string candidateId;
    [SerializeField] private int capacity;
    [SerializeField] private float score;
    [SerializeField] private List<BBPLFSLayoutPlacement> placements = new();

    public string CandidateId => candidateId;
    public int Capacity => capacity;
    public float Score => score;
    public IReadOnlyList<BBPLFSLayoutPlacement> Placements => placements;

    public BBPLFSLayoutCandidate(
        string candidateId,
        int capacity,
        float score,
        List<BBPLFSLayoutPlacement> placements)
    {
        this.candidateId = candidateId ?? string.Empty;
        this.capacity = Mathf.Max(0, capacity);
        this.score = score;
        this.placements = placements ?? new List<BBPLFSLayoutPlacement>();
    }
}

public readonly struct BBPLFSValidationReport
{
    public readonly bool IsValid;
    public readonly string ReasonCode;
    public readonly string Message;

    public BBPLFSValidationReport(bool isValid, string reasonCode, string message)
    {
        IsValid = isValid;
        ReasonCode = reasonCode ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public static BBPLFSValidationReport Valid()
    {
        return new BBPLFSValidationReport(true, string.Empty, string.Empty);
    }
}