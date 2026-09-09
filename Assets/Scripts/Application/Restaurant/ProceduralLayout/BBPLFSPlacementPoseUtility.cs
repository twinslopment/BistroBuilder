using UnityEngine;

/// <summary>
/// Converts between BBPLFS root poses and the surface-anchor poses expected
/// by the classic Edit Mode creation pipeline.
/// </summary>
public static class BBPLFSPlacementPoseUtility
{
    public static Vector3 RootPositionForSurfaceAnchor(
        RestaurantPlaceableObject prefab,
        Vector3 surfaceAnchorWorldPosition,
        Quaternion rootWorldRotation)
    {
        if (prefab == null)
        {
            return surfaceAnchorWorldPosition;
        }

        Vector3 localAnchor = ResolveLocalAnchor(prefab);
        Vector3 scaledLocalAnchor = Vector3.Scale(
            localAnchor,
            prefab.transform.lossyScale);

        return surfaceAnchorWorldPosition -
               rootWorldRotation * scaledLocalAnchor;
    }

    public static Vector3 SurfaceAnchorForRootPose(
        RestaurantPlaceableObject prefab,
        Vector3 rootWorldPosition,
        Quaternion rootWorldRotation)
    {
        if (prefab == null)
        {
            return rootWorldPosition;
        }

        Vector3 localAnchor = ResolveLocalAnchor(prefab);
        Vector3 scaledLocalAnchor = Vector3.Scale(
            localAnchor,
            prefab.transform.lossyScale);

        return rootWorldPosition +
               rootWorldRotation * scaledLocalAnchor;
    }

    private static Vector3 ResolveLocalAnchor(
        RestaurantPlaceableObject prefab)
    {
        Transform root = prefab.transform;
        Transform anchor = prefab.PlacementAnchor;
        return anchor == null || ReferenceEquals(anchor, root)
            ? Vector3.zero
            : root.InverseTransformPoint(anchor.position);
    }
}
