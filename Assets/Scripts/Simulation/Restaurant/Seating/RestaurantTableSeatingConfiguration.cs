using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aplica una configuración fija de plazas a una mesa concreta.
///
/// Genera plazas matemáticas y no necesita hijos manuales por asiento.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Table Seating Configuration"
)]
public sealed class RestaurantTableSeatingConfiguration :
    MonoBehaviour
{
    [SerializeField]
    private RestaurantTable table;

    [SerializeField]
    private RestaurantPlacementFootprint placementFootprint;

    [SerializeField]
    private RestaurantTableSeatingConfigurationDefinition definition;

    [SerializeField]
    private Transform seatingCenter;

    public RestaurantTable Table => table;

    public RestaurantPlacementFootprint PlacementFootprint =>
        placementFootprint;

    public RestaurantTableSeatingConfigurationDefinition Definition =>
        definition;

    public int MaximumCustomers =>
        definition != null
            ? definition.MaximumCustomers
            : 0;

    private void Awake()
    {
        CacheReferencesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (table == null)
        {
            error = name + " necesita RestaurantTable.";
            return false;
        }

        if (placementFootprint == null)
        {
            error =
                name +
                " necesita RestaurantPlacementFootprint.";

            return false;
        }

        if (definition == null)
        {
            error =
                name +
                " necesita una definición de plazas.";

            return false;
        }

        ResolveWorldDimensionsAtPose(
            transform.position,
            transform.rotation,
            out float width,
            out float depth
        );

        if (!definition.ValidateConfiguration(
                width,
                depth,
                out error
            ))
        {
            return false;
        }

        if (table.Capacity !=
            definition.MaximumCustomers)
        {
            error =
                name +
                " tiene Capacity=" +
                table.Capacity +
                ", pero su configuración fija admite " +
                definition.MaximumCustomers +
                ".";

            return false;
        }

        return true;
    }

    /// <summary>
    /// Escribe las plazas de la pose actual en una lista reutilizable.
    /// </summary>
    public int WriteCurrentSlots(
        List<RestaurantTableSeatSlot> results
    )
    {
        return WriteSlotsAtPose(
            transform.position,
            transform.rotation,
            results
        );
    }

    /// <summary>
    /// Escribe plazas para una pose candidata de la mesa.
    /// </summary>
    public int WriteSlotsAtPose(
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation,
        List<RestaurantTableSeatSlot> results
    )
    {
        if (results == null)
        {
            return 0;
        }

        results.Clear();

        if (definition == null ||
            placementFootprint == null)
        {
            return 0;
        }

        RestaurantPlacementShape shape =
            BuildSeatingShapeAtPose(
                candidateRootPosition,
                candidateRootRotation
            );

        Vector3 center =
            ResolveCenterAtPose(
                candidateRootPosition,
                candidateRootRotation,
                shape.Center
            );

        if (definition.Shape ==
            RestaurantTableSeatingShape.Round)
        {
            WriteRoundSlots(
                shape,
                center,
                results
            );

            return results.Count;
        }

        if (definition.Shape !=
            RestaurantTableSeatingShape.Rectangular)
        {
            return 0;
        }

        int nextSlotIndex = 0;

        nextSlotIndex =
            WriteRectangularSideSlots(
                shape,
                center,
                RestaurantTableSeatSide.PositiveZ,
                definition.PositiveZSeats,
                nextSlotIndex,
                results
            );

        nextSlotIndex =
            WriteRectangularSideSlots(
                shape,
                center,
                RestaurantTableSeatSide.NegativeZ,
                definition.NegativeZSeats,
                nextSlotIndex,
                results
            );

        nextSlotIndex =
            WriteRectangularSideSlots(
                shape,
                center,
                RestaurantTableSeatSide.PositiveX,
                definition.PositiveXSeats,
                nextSlotIndex,
                results
            );

        WriteRectangularSideSlots(
            shape,
            center,
            RestaurantTableSeatSide.NegativeX,
            definition.NegativeXSeats,
            nextSlotIndex,
            results
        );

        return results.Count;
    }

    /// <summary>
    /// Evalúa una silla candidata contra una plaza concreta.
    /// </summary>
    public bool TryEvaluateSeatAgainstSlot(
        RestaurantSeat seat,
        Vector3 candidateSeatRootPosition,
        Quaternion candidateSeatRootRotation,
        RestaurantTableSeatSlot slot,
        out RestaurantSeatSlotMatch match
    )
    {
        match = RestaurantSeatSlotMatch.Invalid();

        if (seat == null ||
            seat.UseProfile == null)
        {
            return false;
        }

        Vector3 associationPosition =
            seat.CalculateAssociationPositionAtPose(
                candidateSeatRootPosition,
                candidateSeatRootRotation
            );

        Vector3 difference =
            associationPosition -
            slot.AssociationPosition;

        float verticalDifference =
            Mathf.Abs(difference.y);

        difference.y = 0f;

        float positionDistance =
            difference.magnitude;

        Vector3 facingDirection =
            seat.CalculateFacingDirectionAtPose(
                candidateSeatRootRotation
            );

        float facingAngle =
            Vector3.Angle(
                facingDirection,
                slot.FacingDirection
            );

        RestaurantSeatUseProfileDefinition profile =
            seat.UseProfile;

        bool isValid =
            positionDistance <=
                profile.SlotPositionTolerance &&
            facingAngle <=
                profile.MaximumFacingAngle &&
            verticalDifference <=
                profile.MaximumVerticalDifference;

        float score =
            positionDistance * 10f +
            facingAngle /
            Mathf.Max(1f, profile.MaximumFacingAngle) +
            verticalDifference * 10f;

        match =
            new RestaurantSeatSlotMatch(
                isValid,
                this,
                slot,
                positionDistance,
                facingAngle,
                verticalDifference,
                score
            );

        return isValid;
    }

    /// <summary>
    /// Calcula la distancia horizontal entre un punto y el perímetro
    /// funcional de la mesa. Un valor negativo indica que el punto
    /// está dentro del tablero.
    /// </summary>
    public bool TryCalculatePerimeterGapAtPose(
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation,
        Vector3 worldPoint,
        out float gap
    )
    {
        gap = float.PositiveInfinity;

        if (definition == null ||
            placementFootprint == null)
        {
            return false;
        }

        RestaurantPlacementShape shape =
            BuildSeatingShapeAtPose(
                candidateRootPosition,
                candidateRootRotation
            );

        Vector3 center =
            ResolveCenterAtPose(
                candidateRootPosition,
                candidateRootRotation,
                shape.Center
            );

        Vector3 offset = worldPoint - center;
        offset.y = 0f;

        if (definition.Shape ==
            RestaurantTableSeatingShape.Round)
        {
            float radius =
                Mathf.Max(
                    shape.HalfWidth,
                    shape.HalfDepth
                );

            gap = offset.magnitude - radius;
            return true;
        }

        if (definition.Shape !=
            RestaurantTableSeatingShape.Rectangular)
        {
            return false;
        }

        float localX =
            Vector3.Dot(
                offset,
                shape.RightAxis
            );

        float localZ =
            Vector3.Dot(
                offset,
                shape.ForwardAxis
            );

        float outsideX =
            Mathf.Abs(localX) -
            shape.HalfWidth;

        float outsideZ =
            Mathf.Abs(localZ) -
            shape.HalfDepth;

        if (outsideX <= 0f &&
            outsideZ <= 0f)
        {
            gap =
                -Mathf.Min(
                    -outsideX,
                    -outsideZ
                );

            return true;
        }

        gap =
            Mathf.Sqrt(
                Mathf.Max(0f, outsideX) *
                Mathf.Max(0f, outsideX) +
                Mathf.Max(0f, outsideZ) *
                Mathf.Max(0f, outsideZ)
            );

        return true;
    }

    public void ResolveWorldDimensionsAtPose(
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation,
        out float width,
        out float depth
    )
    {
        if (definition == null ||
            placementFootprint == null)
        {
            width = 0f;
            depth = 0f;
            return;
        }

        RestaurantPlacementShape shape =
            BuildSeatingShapeAtPose(
                candidateRootPosition,
                candidateRootRotation
            );

        width = shape.HalfWidth * 2f;
        depth = shape.HalfDepth * 2f;
    }

    private RestaurantPlacementShape BuildSeatingShapeAtPose(
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation
    )
    {
        if (definition.UsePlacementFootprintDimensions)
        {
            return placementFootprint.BuildShapeAtPose(
                candidateRootPosition,
                candidateRootRotation
            );
        }

        Vector3 scale = transform.lossyScale;

        float width =
            definition.Shape ==
                RestaurantTableSeatingShape.Round
                ? definition.ManualRoundDiameter *
                  Mathf.Max(
                      Mathf.Abs(scale.x),
                      Mathf.Abs(scale.z)
                  )
                : definition.ManualWidth *
                  Mathf.Abs(scale.x);

        float depth =
            definition.Shape ==
                RestaurantTableSeatingShape.Round
                ? width
                : definition.ManualDepth *
                  Mathf.Abs(scale.z);

        return new RestaurantPlacementShape(
            candidateRootPosition,
            candidateRootRotation * Vector3.right,
            candidateRootRotation * Vector3.forward,
            new Vector2(
                Mathf.Max(0.05f, width * 0.5f),
                Mathf.Max(0.05f, depth * 0.5f)
            ),
            0f
        );
    }

    private void WriteRoundSlots(
        RestaurantPlacementShape shape,
        Vector3 center,
        List<RestaurantTableSeatSlot> results
    )
    {
        int capacity =
            definition.MaximumCustomers;

        float radius =
            Mathf.Max(
                shape.HalfWidth,
                shape.HalfDepth
            );

        float slotRadius =
            radius +
            definition.ParkedGapFromTableEdge;

        float angleStep =
            360f /
            capacity;

        for (int index = 0;
             index < capacity;
             index++)
        {
            float angle =
                definition.FirstRoundSeatAngleDegrees +
                angleStep *
                index;

            Vector3 radialDirection =
                Quaternion.AngleAxis(
                    angle,
                    Vector3.up
                ) *
                shape.ForwardAxis;

            radialDirection.y = 0f;
            radialDirection.Normalize();

            results.Add(
                new RestaurantTableSeatSlot(
                    index,
                    RestaurantTableSeatSide.Radial,
                    center +
                    radialDirection *
                    slotRadius,
                    -radialDirection
                )
            );
        }
    }

    private int WriteRectangularSideSlots(
        RestaurantPlacementShape shape,
        Vector3 center,
        RestaurantTableSeatSide side,
        int seatCount,
        int firstSlotIndex,
        List<RestaurantTableSeatSlot> results
    )
    {
        if (seatCount <= 0)
        {
            return firstSlotIndex;
        }

        Vector3 sideNormal;
        Vector3 distributionAxis;
        float halfSideDepth;
        float sideLength;

        switch (side)
        {
            case RestaurantTableSeatSide.PositiveZ:
                sideNormal = shape.ForwardAxis;
                distributionAxis = shape.RightAxis;
                halfSideDepth = shape.HalfDepth;
                sideLength = shape.HalfWidth * 2f;
                break;

            case RestaurantTableSeatSide.NegativeZ:
                sideNormal = -shape.ForwardAxis;
                distributionAxis = shape.RightAxis;
                halfSideDepth = shape.HalfDepth;
                sideLength = shape.HalfWidth * 2f;
                break;

            case RestaurantTableSeatSide.PositiveX:
                sideNormal = shape.RightAxis;
                distributionAxis = shape.ForwardAxis;
                halfSideDepth = shape.HalfWidth;
                sideLength = shape.HalfDepth * 2f;
                break;

            default:
                sideNormal = -shape.RightAxis;
                distributionAxis = shape.ForwardAxis;
                halfSideDepth = shape.HalfWidth;
                sideLength = shape.HalfDepth * 2f;
                break;
        }

        float usableLength =
            Mathf.Max(
                0f,
                sideLength -
                definition.SideEndInset * 2f
            );

        float segmentLength =
            usableLength /
            seatCount;

        Vector3 sideCenter =
            center +
            sideNormal *
            (
                halfSideDepth +
                definition.ParkedGapFromTableEdge
            );

        for (int index = 0;
             index < seatCount;
             index++)
        {
            float offset =
                -usableLength * 0.5f +
                segmentLength *
                (
                    index +
                    0.5f
                );

            results.Add(
                new RestaurantTableSeatSlot(
                    firstSlotIndex + index,
                    side,
                    sideCenter +
                    distributionAxis *
                    offset,
                    -sideNormal
                )
            );
        }

        return firstSlotIndex + seatCount;
    }

    private Vector3 ResolveCenterAtPose(
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation,
        Vector3 fallbackCenter
    )
    {
        if (seatingCenter == null)
        {
            return fallbackCenter;
        }

        Vector3 localCenter =
            transform.InverseTransformPoint(
                seatingCenter.position
            );

        Vector3 scale = transform.lossyScale;

        Vector3 scaledLocalCenter =
            new Vector3(
                localCenter.x * scale.x,
                localCenter.y * scale.y,
                localCenter.z * scale.z
            );

        return candidateRootPosition +
               candidateRootRotation *
               scaledLocalCenter;
    }

    private void CacheReferencesIfNeeded()
    {
        if (table == null)
        {
            TryGetComponent(out table);
        }

        if (placementFootprint == null)
        {
            TryGetComponent(out placementFootprint);
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

    private void OnDrawGizmosSelected()
    {
        List<RestaurantTableSeatSlot> slots =
            new List<RestaurantTableSeatSlot>(16);

        WriteCurrentSlots(slots);

        for (int index = 0;
             index < slots.Count;
             index++)
        {
            RestaurantTableSeatSlot slot = slots[index];

            Gizmos.DrawWireSphere(
                slot.AssociationPosition,
                0.05f
            );

            Gizmos.DrawLine(
                slot.AssociationPosition,
                slot.AssociationPosition +
                slot.FacingDirection *
                0.30f
            );
        }
    }
#endif
}
