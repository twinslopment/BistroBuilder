using System;
using UnityEngine;

[Flags]
public enum RestaurantPlacementSnapTargetKind
{
    None = 0,
    Floor = 1 << 0,
    Wall = 1 << 1,
    Surface = 1 << 2,
    Ceiling = 1 << 3,
    Socket = 1 << 4,
    All = Floor | Wall | Surface | Ceiling | Socket
}

[CreateAssetMenu(
    fileName = "PlacementSnapProfile_",
    menuName = "Bistro Builder/Restaurant/Edit Mode/Placement Snap Profile")]
public sealed class RestaurantPlacementSnapProfile : ScriptableObject
{
    [SerializeField]
    private RestaurantPlacementSnapTargetKind acceptedTargets =
        RestaurantPlacementSnapTargetKind.Floor;

    [SerializeField, Min(0.05f)]
    private float captureRadius = 0.55f;

    [SerializeField, Min(0.05f)]
    private float releaseRadius = 0.80f;

    [SerializeField]
    private bool alignRotationToTarget;

    [SerializeField]
    private string relationshipKey = string.Empty;

    [SerializeField]
    private float preferenceBias;

    public RestaurantPlacementSnapTargetKind AcceptedTargets => acceptedTargets;
    public float CaptureRadius => Mathf.Max(0.05f, captureRadius);
    public float ReleaseRadius => Mathf.Max(CaptureRadius, releaseRadius);
    public bool AlignRotationToTarget => alignRotationToTarget;
    public string RelationshipKey =>
        string.IsNullOrWhiteSpace(relationshipKey)
            ? string.Empty
            : relationshipKey.Trim();
    public float PreferenceBias => preferenceBias;

    public bool Accepts(RestaurantPlacementSnapTargetKind kind)
    {
        return kind != RestaurantPlacementSnapTargetKind.None &&
               (acceptedTargets & kind) == kind;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        RestaurantPlacementSnapTargetKind targets,
        float capture,
        float release,
        bool alignRotation,
        string relation = "",
        float preference = 0f)
    {
        acceptedTargets = targets;
        captureRadius = Mathf.Max(0.05f, capture);
        releaseRadius = Mathf.Max(captureRadius, release);
        alignRotationToTarget = alignRotation;
        relationshipKey = relation ?? string.Empty;
        preferenceBias = preference;
    }
#endif

    private void OnValidate()
    {
        captureRadius = Mathf.Max(0.05f, captureRadius);
        releaseRadius = Mathf.Max(captureRadius, releaseRadius);
        relationshipKey = relationshipKey?.Trim() ?? string.Empty;
    }
}

[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placement Snap Profile Binding")]
public sealed class RestaurantPlacementSnapProfileBinding : MonoBehaviour
{
    [SerializeField]
    private RestaurantPlacementSnapProfile profile;

    public RestaurantPlacementSnapProfile Profile => profile;

    public void SetProfile(RestaurantPlacementSnapProfile value)
    {
        profile = value;
    }
}

public interface IRestaurantPlacementSnapSessionAware
{
    void BeginSnapSession(RestaurantAreaMember member);
    void EndSnapSession();
}
