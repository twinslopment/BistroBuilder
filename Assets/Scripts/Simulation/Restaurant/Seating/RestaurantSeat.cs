using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Convierte un artículo colocable en una silla operativa.
///
/// La raíz colocable permanece inmóvil durante el servicio.
/// Solo OperationalMotionRoot se desplaza para retirar o recoger
/// visualmente la silla.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Seat"
)]
public sealed class RestaurantSeat :
    MonoBehaviour
{
    [Header("Perfil")]

    [SerializeField]
    private RestaurantSeatUseProfileDefinition useProfile;

    [SerializeField]
    private RestaurantSeatFacingAxis facingAxis =
        RestaurantSeatFacingAxis.PositiveZ;

    [Header("Referencias")]

    [SerializeField]
    private RestaurantPlaceableObject placeableObject;

    [SerializeField]
    private Transform associationPoint;

    [SerializeField]
    private Transform operationalMotionRoot;

    [SerializeField]
    private Transform seatPoint;

    [SerializeField]
    private Transform customerApproachPoint;

    [Header("Topología calculada")]

    [SerializeField]
    private RestaurantTableSeatingConfiguration associatedTable;

    [SerializeField]
    private int associatedSlotIndex = -1;

    [SerializeField]
    private RestaurantSeatTopologyStatus topologyStatus =
        RestaurantSeatTopologyStatus.Unassigned;

    [SerializeField]
    [TextArea(2, 4)]
    private string topologyDiagnostic;

    [Header("Estado operativo")]

    [SerializeField]
    private RestaurantSeatOperationalState operationalState =
        RestaurantSeatOperationalState.Parked;

    [SerializeField]
    private string reservationOwnerId;

    private Vector3 parkedMotionLocalPosition;

    private Quaternion parkedMotionLocalRotation;

    private Coroutine motionRoutine;

    public event Action<
        RestaurantSeat,
        RestaurantSeatOperationalState
    > OperationalStateChanged;

    public event Action<RestaurantSeat>
        TopologyChanged;

    public RestaurantSeatUseProfileDefinition UseProfile =>
        useProfile;

    public RestaurantPlaceableObject PlaceableObject =>
        placeableObject;

    public Transform AssociationPoint =>
        associationPoint;

    public Transform OperationalMotionRoot =>
        operationalMotionRoot;

    public Transform SeatPoint =>
        seatPoint;

    public Transform CustomerApproachPoint =>
        customerApproachPoint;

    public RestaurantTableSeatingConfiguration AssociatedTable =>
        associatedTable;

    public int AssociatedSlotIndex =>
        associatedSlotIndex;

    public RestaurantSeatTopologyStatus TopologyStatus =>
        topologyStatus;

    public string TopologyDiagnostic =>
        topologyDiagnostic;

    public RestaurantSeatOperationalState OperationalState =>
        operationalState;

    public string ReservationOwnerId =>
        reservationOwnerId;

    public bool IsAssociated =>
        associatedTable != null &&
        associatedSlotIndex >= 0 &&
        topologyStatus ==
            RestaurantSeatTopologyStatus.Associated;

    public bool IsAvailableForReservation =>
        IsAssociated &&
        string.IsNullOrWhiteSpace(reservationOwnerId) &&
        operationalState ==
            RestaurantSeatOperationalState.Parked;

    private void Awake()
    {
        CacheReferencesIfNeeded();
        CaptureParkedMotionPose();
    }

    private void OnEnable()
    {
        CaptureParkedMotionPose();
    }

    private void OnDisable()
    {
        StopMotionRoutine();
        RestoreParkedMotionPose();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (useProfile == null)
        {
            error = name + " necesita un perfil de uso.";
            return false;
        }

        if (!useProfile.ValidateConfiguration(out error))
        {
            return false;
        }

        if (associationPoint == null)
        {
            error = name + " necesita AssociationPoint.";
            return false;
        }

        if (operationalMotionRoot == null)
        {
            error = name + " necesita OperationalMotionRoot.";
            return false;
        }

        if (seatPoint == null)
        {
            error = name + " necesita SeatPoint.";
            return false;
        }

        if (customerApproachPoint == null)
        {
            error = name + " necesita CustomerApproachPoint.";
            return false;
        }

        if (!operationalMotionRoot.IsChildOf(transform))
        {
            error =
                name +
                ": OperationalMotionRoot debe pertenecer a la silla.";

            return false;
        }

        if (!seatPoint.IsChildOf(operationalMotionRoot))
        {
            error =
                name +
                ": SeatPoint debe moverse con OperationalMotionRoot.";

            return false;
        }

        return true;
    }

    public Vector3 CalculateAssociationPositionAtPose(
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation
    )
    {
        if (associationPoint == null)
        {
            return candidateRootPosition;
        }

        Vector3 localPoint =
            transform.InverseTransformPoint(
                associationPoint.position
            );

        Vector3 scale = transform.lossyScale;

        Vector3 scaledLocalPoint =
            new Vector3(
                localPoint.x * scale.x,
                localPoint.y * scale.y,
                localPoint.z * scale.z
            );

        return candidateRootPosition +
               candidateRootRotation *
               scaledLocalPoint;
    }

    public Vector3 CalculateFacingDirectionAtPose(
        Quaternion candidateRootRotation
    )
    {
        Vector3 direction =
            candidateRootRotation *
            GetLocalFacingDirection();

        direction.y = 0f;

        return direction.sqrMagnitude > 0.000001f
            ? direction.normalized
            : Vector3.forward;
    }

    /// <summary>
    /// Calcula una rotación de raíz que alinea el frente funcional de
    /// la silla con una dirección mundial concreta.
    /// </summary>
    public Quaternion CalculateRootRotationForFacingDirection(
        Vector3 desiredWorldFacingDirection
    )
    {
        desiredWorldFacingDirection.y = 0f;

        if (desiredWorldFacingDirection.sqrMagnitude <=
            0.000001f)
        {
            return transform.rotation;
        }

        desiredWorldFacingDirection.Normalize();

        Quaternion localFacingRotation =
            Quaternion.LookRotation(
                GetLocalFacingDirection(),
                Vector3.up
            );

        Quaternion worldFacingRotation =
            Quaternion.LookRotation(
                desiredWorldFacingDirection,
                Vector3.up
            );

        return worldFacingRotation *
               Quaternion.Inverse(localFacingRotation);
    }

    /// <summary>
    /// Calcula la posición de raíz necesaria para que AssociationPoint
    /// coincida exactamente con una posición mundial.
    /// </summary>
    public Vector3 CalculateRootPositionForAssociationAtPose(
        Vector3 desiredAssociationPosition,
        Quaternion candidateRootRotation
    )
    {
        if (associationPoint == null)
        {
            return desiredAssociationPosition;
        }

        Vector3 localPoint =
            transform.InverseTransformPoint(
                associationPoint.position
            );

        Vector3 scale = transform.lossyScale;

        Vector3 scaledLocalPoint =
            new Vector3(
                localPoint.x * scale.x,
                localPoint.y * scale.y,
                localPoint.z * scale.z
            );

        return desiredAssociationPosition -
               candidateRootRotation *
               scaledLocalPoint;
    }

    public void ApplyTopology(
        RestaurantTableSeatingConfiguration tableConfiguration,
        int slotIndex,
        RestaurantSeatTopologyStatus status,
        string diagnostic
    )
    {
        bool changed =
            !ReferenceEquals(
                associatedTable,
                tableConfiguration
            ) ||
            associatedSlotIndex != slotIndex ||
            topologyStatus != status ||
            !string.Equals(
                topologyDiagnostic,
                diagnostic,
                StringComparison.Ordinal
            );

        associatedTable =
            status == RestaurantSeatTopologyStatus.Associated
                ? tableConfiguration
                : null;

        associatedSlotIndex =
            status == RestaurantSeatTopologyStatus.Associated
                ? slotIndex
                : -1;

        topologyStatus = status;
        topologyDiagnostic = diagnostic ?? string.Empty;

        if (changed)
        {
            TopologyChanged?.Invoke(this);
        }
    }

    public bool TryReserve(string ownerId)
    {
        if (!IsAvailableForReservation ||
            string.IsNullOrWhiteSpace(ownerId))
        {
            return false;
        }

        reservationOwnerId = ownerId.Trim();

        SetOperationalState(
            RestaurantSeatOperationalState.Reserved
        );

        return true;
    }

    public bool ReleaseReservation(
        string ownerId,
        bool force = false
    )
    {
        if (string.IsNullOrWhiteSpace(reservationOwnerId))
        {
            return false;
        }

        if (!force &&
            !string.Equals(
                reservationOwnerId,
                ownerId,
                StringComparison.Ordinal
            ))
        {
            return false;
        }

        reservationOwnerId = string.Empty;

        if (operationalState ==
                RestaurantSeatOperationalState.Reserved ||
            operationalState ==
                RestaurantSeatOperationalState.ReadyForCustomer)
        {
            SetOperationalState(
                RestaurantSeatOperationalState.Parked
            );
        }

        return true;
    }

    public bool TryPullOut()
    {
        if (useProfile == null ||
            operationalMotionRoot == null ||
            (
                operationalState !=
                    RestaurantSeatOperationalState.Reserved &&
                operationalState !=
                    RestaurantSeatOperationalState.Parked
            ))
        {
            return false;
        }

        StartMotion(
            useProfile.PullOutDistance,
            useProfile.PullOutDuration,
            RestaurantSeatOperationalState.PullingOut,
            RestaurantSeatOperationalState.ReadyForCustomer,
            false
        );

        return true;
    }

    public bool TrySetOccupied()
    {
        if (useProfile == null ||
            operationalMotionRoot == null ||
            operationalState !=
                RestaurantSeatOperationalState.ReadyForCustomer)
        {
            return false;
        }

        StartMotion(
            useProfile.OccupiedPullOutDistance,
            useProfile.OccupiedTransitionDuration,
            RestaurantSeatOperationalState.CustomerEntering,
            RestaurantSeatOperationalState.Occupied,
            false
        );

        return true;
    }

    public bool TryBeginLeaving()
    {
        if (useProfile == null ||
            operationalMotionRoot == null ||
            operationalState !=
                RestaurantSeatOperationalState.Occupied)
        {
            return false;
        }

        StartMotion(
            useProfile.PullOutDistance,
            useProfile.OccupiedTransitionDuration,
            RestaurantSeatOperationalState.CustomerLeaving,
            RestaurantSeatOperationalState.ReadyForCustomer,
            false
        );

        return true;
    }

    public bool TryReturnToParked(
        bool releaseReservationWhenFinished = true
    )
    {
        if (useProfile == null ||
            operationalMotionRoot == null ||
            (
                operationalState !=
                    RestaurantSeatOperationalState.ReadyForCustomer &&
                operationalState !=
                    RestaurantSeatOperationalState.Reserved &&
                operationalState !=
                    RestaurantSeatOperationalState.Parked
            ))
        {
            return false;
        }

        StartMotion(
            0f,
            useProfile.ReturnDuration,
            RestaurantSeatOperationalState.Returning,
            RestaurantSeatOperationalState.Parked,
            releaseReservationWhenFinished
        );

        return true;
    }

    private void StartMotion(
        float targetDistance,
        float duration,
        RestaurantSeatOperationalState transitionState,
        RestaurantSeatOperationalState finalState,
        bool releaseReservationWhenFinished
    )
    {
        StopMotionRoutine();

        motionRoutine =
            StartCoroutine(
                AnimateMotionRoutine(
                    targetDistance,
                    duration,
                    transitionState,
                    finalState,
                    releaseReservationWhenFinished
                )
            );
    }

    private IEnumerator AnimateMotionRoutine(
        float targetDistance,
        float duration,
        RestaurantSeatOperationalState transitionState,
        RestaurantSeatOperationalState finalState,
        bool releaseReservationWhenFinished
    )
    {
        SetOperationalState(transitionState);

        Vector3 startPosition =
            operationalMotionRoot.localPosition;

        Vector3 localAwayDirection =
            GetLocalAwayDirection();

        float worldUnitsPerLocalUnit =
            transform.TransformVector(
                localAwayDirection
            ).magnitude;

        float localDistance =
            worldUnitsPerLocalUnit > 0.000001f
                ? Mathf.Max(0f, targetDistance) /
                  worldUnitsPerLocalUnit
                : 0f;

        Vector3 targetPosition =
            parkedMotionLocalPosition +
            localAwayDirection *
            localDistance;

        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / safeDuration);

            float eased =
                progress *
                progress *
                (
                    3f -
                    2f *
                    progress
                );

            operationalMotionRoot.localPosition =
                Vector3.LerpUnclamped(
                    startPosition,
                    targetPosition,
                    eased
                );

            yield return null;
        }

        operationalMotionRoot.localPosition = targetPosition;
        operationalMotionRoot.localRotation =
            parkedMotionLocalRotation;

        motionRoutine = null;

        if (releaseReservationWhenFinished)
        {
            reservationOwnerId = string.Empty;
        }

        SetOperationalState(finalState);
    }

    private void SetOperationalState(
        RestaurantSeatOperationalState newState
    )
    {
        if (operationalState == newState)
        {
            return;
        }

        operationalState = newState;

        OperationalStateChanged?.Invoke(
            this,
            operationalState
        );
    }

    private void StopMotionRoutine()
    {
        if (motionRoutine == null)
        {
            return;
        }

        StopCoroutine(motionRoutine);
        motionRoutine = null;
    }

    private void CaptureParkedMotionPose()
    {
        if (operationalMotionRoot == null)
        {
            return;
        }

        parkedMotionLocalPosition =
            operationalMotionRoot.localPosition;

        parkedMotionLocalRotation =
            operationalMotionRoot.localRotation;
    }

    private void RestoreParkedMotionPose()
    {
        if (operationalMotionRoot == null)
        {
            return;
        }

        operationalMotionRoot.localPosition =
            parkedMotionLocalPosition;

        operationalMotionRoot.localRotation =
            parkedMotionLocalRotation;

        operationalState =
            RestaurantSeatOperationalState.Parked;

        reservationOwnerId = string.Empty;
    }

    private Vector3 GetLocalFacingDirection()
    {
        switch (facingAxis)
        {
            case RestaurantSeatFacingAxis.NegativeZ:
                return Vector3.back;

            case RestaurantSeatFacingAxis.PositiveX:
                return Vector3.right;

            case RestaurantSeatFacingAxis.NegativeX:
                return Vector3.left;

            default:
                return Vector3.forward;
        }
    }

    private Vector3 GetLocalAwayDirection()
    {
        return -GetLocalFacingDirection();
    }

    private void CacheReferencesIfNeeded()
    {
        if (placeableObject == null)
        {
            TryGetComponent(out placeableObject);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheReferencesIfNeeded();
    }

    private void OnValidate()
    {
        CacheReferencesIfNeeded();
    }
#endif
}
