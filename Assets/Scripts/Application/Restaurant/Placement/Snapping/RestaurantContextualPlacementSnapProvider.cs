using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Contextual Snap Target")]
public sealed class RestaurantPlacementSnapTarget : MonoBehaviour
{
    [SerializeField]
    private bool targetEnabled = true;

    [SerializeField]
    private RestaurantPlacementSnapTargetKind targetKind =
        RestaurantPlacementSnapTargetKind.Surface;

    [SerializeField]
    private string relationshipKey = string.Empty;

    [SerializeField]
    private Vector3 localCenter;

    [SerializeField]
    private Vector3 localNormal = Vector3.up;

    [SerializeField]
    private Vector3 localForward = Vector3.forward;

    [SerializeField]
    private Vector2 size = Vector2.one;

    [SerializeField]
    private bool blocked;

    [SerializeField, Min(0f)]
    private float captureRadiusOverride;

    [SerializeField, Min(0f)]
    private float releaseRadiusOverride;

    [SerializeField]
    private float preferenceBias;

    public bool TargetEnabled => targetEnabled && isActiveAndEnabled;
    public RestaurantPlacementSnapTargetKind TargetKind => targetKind;
    public string RelationshipKey =>
        string.IsNullOrWhiteSpace(relationshipKey)
            ? string.Empty
            : relationshipKey.Trim();
    public Vector2 Size => new Vector2(
        Mathf.Max(0.05f, Mathf.Abs(size.x)),
        Mathf.Max(0.05f, Mathf.Abs(size.y)));
    public bool IsBlocked => blocked;
    public float PreferenceBias => preferenceBias;

    public Vector3 WorldCenter => transform.TransformPoint(localCenter);

    public Vector3 WorldNormal
    {
        get
        {
            Vector3 value = transform.TransformDirection(localNormal);
            return value.sqrMagnitude > 0.000001f
                ? value.normalized
                : Vector3.up;
        }
    }

    public Vector3 WorldForward
    {
        get
        {
            Vector3 normal = WorldNormal;
            Vector3 value = Vector3.ProjectOnPlane(
                transform.TransformDirection(localForward),
                normal);
            if (value.sqrMagnitude <= 0.000001f)
            {
                value = Vector3.ProjectOnPlane(transform.forward, normal);
            }
            if (value.sqrMagnitude <= 0.000001f)
            {
                value = Vector3.ProjectOnPlane(Vector3.forward, normal);
            }
            if (value.sqrMagnitude <= 0.000001f)
            {
                value = Vector3.ProjectOnPlane(Vector3.right, normal);
            }
            return value.normalized;
        }
    }

    public Quaternion WorldRotation =>
        Quaternion.LookRotation(WorldForward, WorldNormal);

    public RestaurantPlacementSnapHintState HintState =>
        blocked
            ? RestaurantPlacementSnapHintState.Blocked
            : RestaurantPlacementSnapHintState.Available;

    public bool Matches(RestaurantPlacementSnapProfile profile)
    {
        if (!TargetEnabled || profile == null ||
            !profile.Accepts(targetKind))
        {
            return false;
        }

        string targetRelation = RelationshipKey;
        string profileRelation = profile.RelationshipKey;

        if (targetRelation.Length == 0)
        {
            /*
             * Un target genérico sigue siendo válido para suelo, pared,
             * superficie o techo. En cambio un socket sin relación no
             * puede capturar silenciosamente un perfil relacional:
             * los enlaces de conjunto deben ser explícitos.
             */
            return targetKind !=
                       RestaurantPlacementSnapTargetKind.Socket ||
                   profileRelation.Length == 0;
        }

        return profileRelation.Length > 0 &&
               string.Equals(
                   targetRelation,
                   profileRelation,
                   StringComparison.Ordinal);
    }

    public bool TryResolveAnchor(
        Vector3 rawAnchorPosition,
        RestaurantPlacementSnapProfile profile,
        out Vector3 anchorPosition,
        out float distance,
        out float captureRadius,
        out float releaseRadius)
    {
        anchorPosition = rawAnchorPosition;
        distance = float.PositiveInfinity;
        captureRadius = 0f;
        releaseRadius = 0f;

        if (!Matches(profile))
            return false;

        Vector3 center = WorldCenter;
        Vector3 normal = WorldNormal;
        Vector3 forward = WorldForward;
        Vector3 right = Vector3.Cross(normal, forward).normalized;
        if (right.sqrMagnitude <= 0.000001f)
            return false;

        Vector3 projected =
            rawAnchorPosition -
            normal * Vector3.Dot(rawAnchorPosition - center, normal);

        Vector3 delta = projected - center;
        Vector2 half = Size * 0.5f;
        float x = Mathf.Clamp(Vector3.Dot(delta, right), -half.x, half.x);
        float y = Mathf.Clamp(Vector3.Dot(delta, forward), -half.y, half.y);

        anchorPosition = center + right * x + forward * y;
        distance = Vector3.Distance(rawAnchorPosition, anchorPosition);

        captureRadius = captureRadiusOverride > 0f
            ? captureRadiusOverride
            : profile.CaptureRadius;

        releaseRadius = releaseRadiusOverride > 0f
            ? Mathf.Max(captureRadius, releaseRadiusOverride)
            : Mathf.Max(captureRadius, profile.ReleaseRadius);

        return float.IsFinite(distance);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        RestaurantPlacementSnapTargetKind kind,
        Vector2 targetSize,
        Vector3 center,
        Vector3 normal,
        Vector3 forward,
        string relation = "",
        bool isBlocked = false,
        float preference = 0f)
    {
        targetKind = kind;
        size = targetSize;
        localCenter = center;
        localNormal = normal;
        localForward = forward;
        relationshipKey = relation ?? string.Empty;
        blocked = isBlocked;
        preferenceBias = preference;
        targetEnabled = true;
    }
#endif

    private void OnValidate()
    {
        size = new Vector2(
            Mathf.Max(0.05f, Mathf.Abs(size.x)),
            Mathf.Max(0.05f, Mathf.Abs(size.y)));
        localNormal = localNormal.sqrMagnitude > 0.000001f
            ? localNormal.normalized
            : Vector3.up;
        relationshipKey = relationshipKey?.Trim() ?? string.Empty;
        captureRadiusOverride = Mathf.Max(0f, captureRadiusOverride);
        releaseRadiusOverride = Mathf.Max(0f, releaseRadiusOverride);
    }
}

[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Contextual Placement Snap Provider")]
public sealed class RestaurantContextualPlacementSnapProvider :
    MonoBehaviour,
    IRestaurantPlacementSnapProvider,
    IRestaurantPlacementSnapSessionAware
{
    [SerializeField]
    private bool snapEnabled = true;

    [SerializeField]
    private int priority = 200;

    private readonly List<RestaurantPlacementSnapTarget> targets =
        new List<RestaurantPlacementSnapTarget>(64);
    private readonly List<int> stableTargetIds =
        new List<int>(64);

    public int Priority => priority;
    public bool IsSnapEnabled => snapEnabled;
    public int CachedTargetCount => targets.Count;

    public void BeginSnapSession(RestaurantAreaMember member)
    {
        RefreshTargets();
    }

    public void EndSnapSession()
    {
        targets.Clear();
        stableTargetIds.Clear();
    }

    public void RefreshTargets()
    {
        targets.Clear();
        stableTargetIds.Clear();
        RestaurantPlacementSnapTarget[] discovered =
            UnityEngine.Object.FindObjectsByType<RestaurantPlacementSnapTarget>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        for (int i = 0; i < discovered.Length; i++)
        {
            RestaurantPlacementSnapTarget target = discovered[i];
            if (target != null && target.TargetEnabled)
                targets.Add(target);
        }

        targets.Sort(CompareTargets);
        for (int i = 0; i < targets.Count; i++)
        {
            stableTargetIds.Add(
                StableHash(
                    BuildHierarchyPath(targets[i].transform) +
                    "|" + (int)targets[i].TargetKind +
                    "|" + targets[i].RelationshipKey));
        }
    }

    public void CollectCandidates(
        RestaurantPlacementSnapContext context,
        List<RestaurantPlacementSnapCandidate> results)
    {
        if (!snapEnabled || context.Member == null || results == null)
            return;

        RestaurantPlacementSnapProfile profile =
            ResolveProfile(context.Member);
        if (profile == null)
            return;

        EnsureTargets();

        Vector3 rawAnchor = ResolveRawAnchor(
            context.Member,
            context.RawRootPosition,
            context.RawRootRotation);

        Vector3 localAnchorOffset =
            ResolveAnchorOffsetInRootSpace(context.Member);

        for (int i = 0; i < targets.Count; i++)
        {
            RestaurantPlacementSnapTarget target = targets[i];
            if (target == null ||
                target.IsBlocked ||
                IsOwnedByMember(target, context.Member))
            {
                continue;
            }

            if (!target.TryResolveAnchor(
                    rawAnchor,
                    profile,
                    out Vector3 targetAnchor,
                    out float distance,
                    out float captureRadius,
                    out float releaseRadius))
            {
                continue;
            }

            if (distance > releaseRadius)
                continue;

            Quaternion snappedRotation =
                profile.AlignRotationToTarget
                    ? target.WorldRotation
                    : context.RawRootRotation;

            Vector3 rootPosition =
                targetAnchor -
                snappedRotation * localAnchorOffset;

            results.Add(
                new RestaurantPlacementSnapCandidate(
                    this,
                    new RestaurantPlacementSnapTargetKey(
                        GetInstanceID(),
                        stableTargetIds[i],
                        (int)target.TargetKind),
                    rootPosition,
                    snappedRotation,
                    distance,
                    captureRadius,
                    releaseRadius,
                    profile.PreferenceBias + target.PreferenceBias,
                    target,
                    RestaurantPlacementSnapHintState.Available));
        }
    }

    public void CollectVisualHints(
        RestaurantPlacementSnapContext context,
        List<RestaurantPlacementSnapHint> results)
    {
        if (!snapEnabled || context.Member == null || results == null)
            return;

        RestaurantPlacementSnapProfile profile =
            ResolveProfile(context.Member);
        if (profile == null)
            return;

        EnsureTargets();

        for (int i = 0; i < targets.Count; i++)
        {
            RestaurantPlacementSnapTarget target = targets[i];
            if (target == null ||
                IsOwnedByMember(target, context.Member) ||
                !target.Matches(profile))
            {
                continue;
            }

            RestaurantPlacementSnapHintGeometry geometry =
                target.TargetKind == RestaurantPlacementSnapTargetKind.Socket
                    ? RestaurantPlacementSnapHintGeometry.CircularAnchor
                    : target.TargetKind == RestaurantPlacementSnapTargetKind.Wall
                        ? RestaurantPlacementSnapHintGeometry.LinearSocket
                        : RestaurantPlacementSnapHintGeometry.RectangularFootprint;

            results.Add(
                new RestaurantPlacementSnapHint(
                    new RestaurantPlacementSnapTargetKey(
                        GetInstanceID(),
                        stableTargetIds[i],
                        (int)target.TargetKind),
                    target.WorldCenter,
                    target.WorldNormal,
                    target.WorldForward,
                    target.Size,
                    geometry,
                    profile.AlignRotationToTarget,
                    target.HintState,
                    target));
        }
    }

    private void EnsureTargets()
    {
        if (targets.Count == 0)
            RefreshTargets();
    }

    private static bool IsOwnedByMember(
        RestaurantPlacementSnapTarget target,
        RestaurantAreaMember member)
    {
        if (target == null || member == null)
            return false;

        RestaurantAreaMember targetOwner =
            target.GetComponentInParent<RestaurantAreaMember>();

        return ReferenceEquals(targetOwner, member);
    }

    private static RestaurantPlacementSnapProfile ResolveProfile(
        RestaurantAreaMember member)
    {
        if (member.TryGetComponent(
                out RestaurantPlacementSnapProfileBinding binding) &&
            binding.Profile != null)
        {
            return binding.Profile;
        }

        if (member.TryGetComponent(
                out RestaurantPlaceableObject placeable) &&
            placeable.ItemDefinition != null)
        {
            return placeable.ItemDefinition.SnapProfile;
        }

        return null;
    }

    private static Vector3 ResolveRawAnchor(
        RestaurantAreaMember member,
        Vector3 rawRootPosition,
        Quaternion rawRootRotation)
    {
        Vector3 localOffset = ResolveAnchorOffsetInRootSpace(member);
        return rawRootPosition + rawRootRotation * localOffset;
    }

    private static Vector3 ResolveAnchorOffsetInRootSpace(
        RestaurantAreaMember member)
    {
        if (member == null)
            return Vector3.zero;

        if (!member.TryGetComponent(
                out RestaurantPlaceableObject placeable))
        {
            return Vector3.zero;
        }

        Transform root = member.transform;
        Transform anchor = placeable.PlacementAnchor;
        if (anchor == null || ReferenceEquals(anchor, root))
            return Vector3.zero;

        return Quaternion.Inverse(root.rotation) *
               (anchor.position - root.position);
    }

    private static int CompareTargets(
        RestaurantPlacementSnapTarget first,
        RestaurantPlacementSnapTarget second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return 1;
        if (second == null) return -1;

        int kind = ((int)first.TargetKind).CompareTo((int)second.TargetKind);
        if (kind != 0) return kind;

        string firstPath = BuildHierarchyPath(first.transform);
        string secondPath = BuildHierarchyPath(second.transform);
        int path = string.Compare(
            firstPath,
            secondPath,
            StringComparison.Ordinal);
        if (path != 0) return path;

        return 0;
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null) return string.Empty;

        string path =
            transform.GetSiblingIndex() + ":" + transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path =
                parent.GetSiblingIndex() + ":" +
                parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
            }
            return (int)hash;
        }
    }
}
