using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proveedor de snapping para la relación silla-mesa.
///
/// Convierte las plazas matemáticas de cada mesa en destinos cómodos
/// para el cursor. No valida la colocación: la regla especializada de
/// asientos y el validador general conservan la autoridad final.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Seating Snap Provider"
)]
public sealed class RestaurantSeatingSnapProvider :
    MonoBehaviour,
    IRestaurantPlacementSnapProvider
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantTableRegistry tableRegistry;

    [SerializeField]
    private RestaurantSeatRegistry seatRegistry;

    [Header("Captura")]

    [SerializeField]
    private bool snapEnabled = true;

    [SerializeField]
    private int priority = 100;

    [Tooltip(
        "Distancia máxima para capturar inicialmente una plaza."
    )]
    [SerializeField]
    [Min(0.10f)]
    private float captureRadius = 0.65f;

    [Tooltip(
        "Distancia para liberar una plaza ya capturada. Debe ser " +
        "mayor que el radio de captura para evitar parpadeos."
    )]
    [SerializeField]
    [Min(0.10f)]
    private float releaseRadius = 0.85f;

    [SerializeField]
    [Min(0f)]
    private float maximumVerticalCaptureDifference = 0.30f;

    [Header("Visualización")]

    [SerializeField]
    [Min(0.25f)]
    private float visualizationRadius = 2.50f;

    [SerializeField]
    [Min(0.05f)]
    private float slotIndicatorRadius = 0.18f;

    private const int MaximumSupportedSlotsPerTable = 128;

    private readonly List<RestaurantTableSeatSlot>
        slotBuffer =
            new List<RestaurantTableSeatSlot>(16);

    private readonly bool[] occupiedSlotBuffer =
        new bool[MaximumSupportedSlotsPerTable];

    public int Priority => priority;

    public bool IsSnapEnabled => snapEnabled;

    public float CaptureRadius =>
        Mathf.Max(0.10f, captureRadius);

    public float ReleaseRadius =>
        Mathf.Max(CaptureRadius, releaseRadius);

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (tableRegistry == null)
        {
            error =
                name +
                " necesita RestaurantTableRegistry.";

            return false;
        }

        if (seatRegistry == null)
        {
            error =
                name +
                " necesita RestaurantSeatRegistry.";

            return false;
        }

        if (ReleaseRadius <= CaptureRadius)
        {
            error =
                name +
                " necesita un radio de liberación mayor que el " +
                "radio de captura.";

            return false;
        }

        return true;
    }

    public void CollectCandidates(
        RestaurantPlacementSnapContext context,
        List<RestaurantPlacementSnapCandidate> results
    )
    {
        if (!snapEnabled ||
            results == null ||
            context.Member == null ||
            tableRegistry == null ||
            !context.Member.TryGetComponent(
                out RestaurantSeat candidateSeat
            ) ||
            candidateSeat.UseProfile == null)
        {
            return;
        }

        Vector3 rawAssociationPosition =
            candidateSeat.CalculateAssociationPositionAtPose(
                context.RawRootPosition,
                context.RawRootRotation
            );

        foreach (RestaurantTable table
                 in tableRegistry.RegisteredTables)
        {
            if (table == null ||
                !table.TryGetComponent(
                    out RestaurantTableSeatingConfiguration
                        configuration
                ) ||
                configuration.Definition == null)
            {
                continue;
            }

            int slotCount =
                configuration.WriteCurrentSlots(slotBuffer);

            if (slotCount <= 0 ||
                slotCount > occupiedSlotBuffer.Length)
            {
                continue;
            }

            MarkOccupiedSlots(
                configuration,
                candidateSeat,
                slotCount
            );

            int providerInstanceId = GetInstanceID();
            int tableInstanceId = table.GetInstanceID();

            for (int slotArrayIndex = 0;
                 slotArrayIndex < slotCount;
                 slotArrayIndex++)
            {
                RestaurantTableSeatSlot slot =
                    slotBuffer[slotArrayIndex];

                Vector3 difference =
                    rawAssociationPosition -
                    slot.AssociationPosition;

                float verticalDifference =
                    Mathf.Abs(difference.y);

                difference.y = 0f;

                float horizontalDistance =
                    difference.magnitude;

                if (verticalDifference >
                        maximumVerticalCaptureDifference ||
                    horizontalDistance > ReleaseRadius)
                {
                    continue;
                }

                Quaternion snappedRotation =
                    candidateSeat
                        .CalculateRootRotationForFacingDirection(
                            slot.FacingDirection
                        );

                Vector3 snappedPosition =
                    candidateSeat
                        .CalculateRootPositionForAssociationAtPose(
                            slot.AssociationPosition,
                            snappedRotation
                        );

                bool occupied =
                    occupiedSlotBuffer[slotArrayIndex];

                results.Add(
                    new RestaurantPlacementSnapCandidate(
                        this,
                        new RestaurantPlacementSnapTargetKey(
                            providerInstanceId,
                            tableInstanceId,
                            slot.SlotIndex
                        ),
                        snappedPosition,
                        snappedRotation,
                        horizontalDistance,
                        CaptureRadius,
                        ReleaseRadius,
                        occupied ? 0.02f : 0f,
                        configuration,
                        occupied
                            ? RestaurantPlacementSnapHintState
                                .Occupied
                            : RestaurantPlacementSnapHintState
                                .Available
                    )
                );
            }
        }
    }

    public void CollectVisualHints(
        RestaurantPlacementSnapContext context,
        List<RestaurantPlacementSnapHint> results
    )
    {
        if (!snapEnabled ||
            results == null ||
            context.Member == null ||
            tableRegistry == null ||
            !context.Member.TryGetComponent(
                out RestaurantSeat candidateSeat
            ) ||
            candidateSeat.UseProfile == null)
        {
            return;
        }

        Vector3 rawAssociationPosition =
            candidateSeat.CalculateAssociationPositionAtPose(
                context.RawRootPosition,
                context.RawRootRotation
            );

        foreach (RestaurantTable table
                 in tableRegistry.RegisteredTables)
        {
            if (table == null ||
                !table.TryGetComponent(
                    out RestaurantTableSeatingConfiguration
                        configuration
                ) ||
                configuration.Definition == null)
            {
                continue;
            }

            int slotCount =
                configuration.WriteCurrentSlots(slotBuffer);

            if (slotCount <= 0 ||
                slotCount > occupiedSlotBuffer.Length)
            {
                continue;
            }

            bool tableIsNear = false;

            for (int slotIndex = 0;
                 slotIndex < slotCount;
                 slotIndex++)
            {
                Vector3 difference =
                    rawAssociationPosition -
                    slotBuffer[slotIndex].AssociationPosition;

                difference.y = 0f;

                if (difference.sqrMagnitude <=
                    visualizationRadius * visualizationRadius)
                {
                    tableIsNear = true;
                    break;
                }
            }

            if (!tableIsNear)
            {
                continue;
            }

            MarkOccupiedSlots(
                configuration,
                candidateSeat,
                slotCount
            );

            int providerInstanceId = GetInstanceID();
            int tableInstanceId = table.GetInstanceID();

            for (int slotArrayIndex = 0;
                 slotArrayIndex < slotCount;
                 slotArrayIndex++)
            {
                RestaurantTableSeatSlot slot =
                    slotBuffer[slotArrayIndex];

                bool occupied =
                    occupiedSlotBuffer[slotArrayIndex];

                results.Add(
                    new RestaurantPlacementSnapHint(
                        new RestaurantPlacementSnapTargetKey(
                            providerInstanceId,
                            tableInstanceId,
                            slot.SlotIndex
                        ),
                        slot.AssociationPosition,
                        Vector3.up,
                        slot.FacingDirection,
                        Vector2.one *
                        Mathf.Max(
                            0.10f,
                            slotIndicatorRadius * 2f
                        ),
                        RestaurantPlacementSnapHintGeometry
                            .CircularAnchor,
                        true,
                        occupied
                            ? RestaurantPlacementSnapHintState
                                .Occupied
                            : RestaurantPlacementSnapHintState
                                .Available,
                        configuration
                    )
                );
            }
        }
    }

    private void MarkOccupiedSlots(
        RestaurantTableSeatingConfiguration configuration,
        RestaurantSeat candidateSeat,
        int slotCount
    )
    {
        for (int index = 0;
             index < slotCount;
             index++)
        {
            occupiedSlotBuffer[index] = false;
        }

        if (seatRegistry == null)
        {
            return;
        }

        foreach (RestaurantSeat registeredSeat
                 in seatRegistry.RegisteredSeats)
        {
            if (registeredSeat == null ||
                ReferenceEquals(
                    registeredSeat,
                    candidateSeat
                ))
            {
                continue;
            }

            if (registeredSeat.IsAssociated)
            {
                if (ReferenceEquals(
                        registeredSeat.AssociatedTable,
                        configuration
                    ))
                {
                    int associatedArrayIndex =
                        FindSlotArrayIndex(
                            registeredSeat.AssociatedSlotIndex,
                            slotCount
                        );

                    if (associatedArrayIndex >= 0)
                    {
                        occupiedSlotBuffer[
                            associatedArrayIndex
                        ] = true;
                    }
                }

                /*
                 * Una silla ya asociada a otra mesa no necesita
                 * comparaciones geométricas contra esta configuración.
                 */
                continue;
            }

            /*
             * Respaldo para el breve intervalo entre confirmar una
             * silla y reconstruir la topología en el frame siguiente.
             */
            for (int slotIndex = 0;
                 slotIndex < slotCount;
                 slotIndex++)
            {
                if (occupiedSlotBuffer[slotIndex])
                {
                    continue;
                }

                if (configuration.TryEvaluateSeatAgainstSlot(
                        registeredSeat,
                        registeredSeat.transform.position,
                        registeredSeat.transform.rotation,
                        slotBuffer[slotIndex],
                        out _
                    ))
                {
                    occupiedSlotBuffer[slotIndex] = true;
                    break;
                }
            }
        }
    }

    private int FindSlotArrayIndex(
        int logicalSlotIndex,
        int slotCount
    )
    {
        for (int index = 0;
             index < slotCount;
             index++)
        {
            if (slotBuffer[index].SlotIndex ==
                logicalSlotIndex)
            {
                return index;
            }
        }

        return -1;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (tableRegistry == null)
        {
            TryGetComponent(out tableRegistry);
        }

        if (seatRegistry == null)
        {
            TryGetComponent(out seatRegistry);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();

        captureRadius = Mathf.Max(0.10f, captureRadius);
        releaseRadius =
            Mathf.Max(
                captureRadius + 0.05f,
                releaseRadius
            );

        maximumVerticalCaptureDifference =
            Mathf.Max(0f, maximumVerticalCaptureDifference);

        visualizationRadius =
            Mathf.Max(0.25f, visualizationRadius);

        slotIndicatorRadius =
            Mathf.Max(0.05f, slotIndicatorRadius);
    }
#endif
}
