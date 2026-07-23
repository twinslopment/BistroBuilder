using System;
using UnityEngine;

/// <summary>
/// Caja operativa local que debe permanecer libre para que un
/// artículo pueda utilizarse correctamente.
///
/// Ejemplos futuros: retirada de sillas, apertura de puertas,
/// extracción de cajones y acceso de mantenimiento.
/// </summary>
[Serializable]
public struct RestaurantOperationalClearanceBox
{
    [SerializeField]
    private string clearanceId;

    [SerializeField]
    private Vector3 localCenter;

    [SerializeField]
    private Vector2 size;

    [SerializeField]
    [Range(-180f, 180f)]
    private float localYawDegrees;

    [SerializeField]
    private string blockedUserMessage;

    public string ClearanceId =>
        string.IsNullOrWhiteSpace(clearanceId)
            ? "operational_clearance"
            : clearanceId.Trim();

    public Vector3 LocalCenter =>
        localCenter;

    public Vector2 Size =>
        new Vector2(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y)
        );

    public float LocalYawDegrees =>
        localYawDegrees;

    public string BlockedUserMessage =>
        string.IsNullOrWhiteSpace(blockedUserMessage)
            ? "El objeto necesita más espacio para poder utilizarse."
            : blockedUserMessage.Trim();
}

/// <summary>
/// Colección reusable de espacios operativos de un artículo.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Operational Clearance Set"
)]
public sealed class RestaurantOperationalClearanceSet :
    MonoBehaviour
{
    [SerializeField]
    private RestaurantOperationalClearanceBox[] clearances =
        Array.Empty<RestaurantOperationalClearanceBox>();

    [SerializeField]
    private bool blocksOtherPlacements = true;

    [SerializeField]
    private bool requiresClearanceForOwner = true;

    public int ClearanceCount =>
        clearances != null
            ? clearances.Length
            : 0;

    public bool BlocksOtherPlacements =>
        blocksOtherPlacements;

    public bool RequiresClearanceForOwner =>
        requiresClearanceForOwner;

    public bool TryBuildShapeAtPose(
        int index,
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation,
        out RestaurantPlacementShape shape,
        out RestaurantOperationalClearanceBox definition
    )
    {
        shape = default;
        definition = default;

        if (clearances == null ||
            index < 0 ||
            index >= clearances.Length)
        {
            return false;
        }

        definition = clearances[index];

        Vector3 scale =
            transform.lossyScale;

        Vector3 scaledLocalCenter =
            new Vector3(
                definition.LocalCenter.x * scale.x,
                definition.LocalCenter.y * scale.y,
                definition.LocalCenter.z * scale.z
            );

        Quaternion localRotation =
            Quaternion.Euler(
                0f,
                definition.LocalYawDegrees,
                0f
            );

        Quaternion worldRotation =
            candidateRootRotation *
            localRotation;

        Vector3 worldCenter =
            candidateRootPosition +
            candidateRootRotation *
            scaledLocalCenter;

        Vector2 size =
            definition.Size;

        Vector2 halfExtents =
            new Vector2(
                Mathf.Max(
                    0.005f,
                    Mathf.Abs(size.x * scale.x) * 0.5f
                ),
                Mathf.Max(
                    0.005f,
                    Mathf.Abs(size.y * scale.z) * 0.5f
                )
            );

        shape =
            new RestaurantPlacementShape(
                worldCenter,
                worldRotation * Vector3.right,
                worldRotation * Vector3.forward,
                halfExtents,
                0f
            );

        return true;
    }

    public bool TryBuildCurrentShape(
        int index,
        out RestaurantPlacementShape shape,
        out RestaurantOperationalClearanceBox definition
    )
    {
        return TryBuildShapeAtPose(
            index,
            transform.position,
            transform.rotation,
            out shape,
            out definition
        );
    }
}
