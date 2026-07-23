using System;
using UnityEngine;

/// <summary>
/// Forma funcional del tablero para distribuir plazas.
/// </summary>
public enum RestaurantTableSeatingShape
{
    Rectangular = 0,
    Round = 1,
    Oval = 2,
    Custom = 3
}

/// <summary>
/// Lado de una mesa rectangular utilizado por una plaza.
/// </summary>
public enum RestaurantTableSeatSide
{
    None = 0,
    PositiveZ = 1,
    NegativeZ = 2,
    PositiveX = 3,
    NegativeX = 4,
    Radial = 5
}

/// <summary>
/// Eje local que representa el frente de una silla.
/// El frente debe mirar hacia el centro de la mesa.
/// </summary>
public enum RestaurantSeatFacingAxis
{
    PositiveZ = 0,
    NegativeZ = 1,
    PositiveX = 2,
    NegativeX = 3
}

/// <summary>
/// Estado de asociación espacial de una silla.
/// </summary>
public enum RestaurantSeatTopologyStatus
{
    Unassigned = 0,
    Associated = 1,
    MissingProfile = 2,
    NoCompatibleTable = 3,
    InvalidSlot = 4,
    TableCapacityReached = 5,
    OperationalClearanceBlocked = 6
}

/// <summary>
/// Estado operativo de una silla durante el servicio.
/// </summary>
public enum RestaurantSeatOperationalState
{
    Parked = 0,
    Reserved = 1,
    PullingOut = 2,
    ReadyForCustomer = 3,
    CustomerEntering = 4,
    Occupied = 5,
    CustomerLeaving = 6,
    Returning = 7,
    Unavailable = 8
}

/// <summary>
/// Plaza calculada de una configuración concreta de mesa.
/// </summary>
public readonly struct RestaurantTableSeatSlot
{
    public int SlotIndex { get; }

    public RestaurantTableSeatSide Side { get; }

    public Vector3 AssociationPosition { get; }

    public Vector3 FacingDirection { get; }

    public RestaurantTableSeatSlot(
        int slotIndex,
        RestaurantTableSeatSide side,
        Vector3 associationPosition,
        Vector3 facingDirection
    )
    {
        SlotIndex = slotIndex;
        Side = side;
        AssociationPosition = associationPosition;

        facingDirection.y = 0f;

        FacingDirection =
            facingDirection.sqrMagnitude > 0.000001f
                ? facingDirection.normalized
                : Vector3.forward;
    }
}

/// <summary>
/// Coincidencia entre una silla y una plaza concreta.
/// </summary>
public readonly struct RestaurantSeatSlotMatch
{
    public bool IsValid { get; }

    public RestaurantTableSeatingConfiguration TableConfiguration { get; }

    public RestaurantTableSeatSlot Slot { get; }

    public float PositionDistance { get; }

    public float FacingAngle { get; }

    public float VerticalDifference { get; }

    public float Score { get; }

    public RestaurantSeatSlotMatch(
        bool isValid,
        RestaurantTableSeatingConfiguration tableConfiguration,
        RestaurantTableSeatSlot slot,
        float positionDistance,
        float facingAngle,
        float verticalDifference,
        float score
    )
    {
        IsValid = isValid;
        TableConfiguration = tableConfiguration;
        Slot = slot;
        PositionDistance = positionDistance;
        FacingAngle = facingAngle;
        VerticalDifference = verticalDifference;
        Score = score;
    }

    public static RestaurantSeatSlotMatch Invalid()
    {
        return new RestaurantSeatSlotMatch(
            false,
            null,
            default,
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity
        );
    }
}
