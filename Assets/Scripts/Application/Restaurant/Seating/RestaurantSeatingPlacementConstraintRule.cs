using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regla especializada que valida la relación silla-mesa.
///
/// Bloquea:
/// - Sillas fuera de una plaza definida.
/// - Sillas orientadas incorrectamente.
/// - Plazas ya ocupadas.
/// - Sillas que exceden la capacidad fija de una mesa.
/// - Movimientos de mesa que dejarían sillas fuera de sus plazas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Seating Placement Rule"
)]
public sealed class RestaurantSeatingPlacementConstraintRule :
    MonoBehaviour,
    IRestaurantPlacementConstraintRule
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantSeatRegistry seatRegistry;

    [SerializeField]
    private RestaurantTableRegistry tableRegistry;

    [Header("Regla")]

    [SerializeField]
    private bool constraintEnabled = true;

    [SerializeField]
    private int priority = 10;

    [Tooltip(
        "Multiplicador de tolerancia usado únicamente para detectar " +
        "que una silla extra pretende pertenecer a una mesa llena."
    )]
    [SerializeField]
    [Min(1f)]
    private float fullTableDetectionToleranceMultiplier = 2.5f;

    private readonly List<RestaurantTableSeatSlot>
        slotBuffer =
            new List<RestaurantTableSeatSlot>(16);

    private readonly bool[] occupiedSlotBuffer =
        new bool[32];

    public int Priority => priority;

    public bool IsConstraintEnabled => constraintEnabled;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public RestaurantPlacementConstraintEvaluation Evaluate(
        RestaurantPlacementConstraintContext context
    )
    {
        if (context.Member == null)
        {
            return RestaurantPlacementConstraintEvaluation.Valid();
        }

        if (context.Member.TryGetComponent(
                out RestaurantSeat candidateSeat
            ))
        {
            return EvaluateSeatCandidate(
                candidateSeat,
                context.CandidateRootPosition,
                context.CandidateRootRotation
            );
        }

        if (context.Member.TryGetComponent(
                out RestaurantTableSeatingConfiguration
                    candidateTable
            ))
        {
            return EvaluateTableCandidate(
                candidateTable,
                context.CandidateRootPosition,
                context.CandidateRootRotation
            );
        }

        return RestaurantPlacementConstraintEvaluation.Valid();
    }

    private RestaurantPlacementConstraintEvaluation
        EvaluateSeatCandidate(
            RestaurantSeat candidateSeat,
            Vector3 candidateRootPosition,
            Quaternion candidateRootRotation
        )
    {
        if (candidateSeat.UseProfile == null)
        {
            return RestaurantPlacementConstraintEvaluation.Invalid(
                "seating_missing_profile",
                "La silla no tiene una configuración de uso válida.",
                candidateSeat.name +
                " no tiene RestaurantSeatUseProfileDefinition.",
                candidateSeat
            );
        }

        if (tableRegistry == null ||
            seatRegistry == null)
        {
            return RestaurantPlacementConstraintEvaluation.Invalid(
                "seating_system_unavailable",
                "El sistema de asientos no está disponible.",
                "Falta RestaurantTableRegistry o RestaurantSeatRegistry.",
                this
            );
        }

        RestaurantSeatSlotMatch bestFreeMatch =
            RestaurantSeatSlotMatch.Invalid();

        RestaurantSeatSlotMatch bestOccupiedMatch =
            RestaurantSeatSlotMatch.Invalid();

        RestaurantSeatSlotMatch nearestMatch =
            RestaurantSeatSlotMatch.Invalid();

        RestaurantTableSeatingConfiguration nearestFullTable =
            null;

        float nearestFullTableDistance =
            float.PositiveInfinity;

        Vector3 candidateAssociationPosition =
            candidateSeat.CalculateAssociationPositionAtPose(
                candidateRootPosition,
                candidateRootRotation
            );

        foreach (RestaurantTable table
                 in tableRegistry.RegisteredTables)
        {
            if (table == null ||
                !table.TryGetComponent(
                    out RestaurantTableSeatingConfiguration
                        tableConfiguration
                ) ||
                tableConfiguration.Definition == null)
            {
                continue;
            }

            int slotCount =
                tableConfiguration.WriteCurrentSlots(
                    slotBuffer
                );

            if (slotCount <= 0 ||
                slotCount > occupiedSlotBuffer.Length)
            {
                continue;
            }

            ClearOccupiedSlots(slotCount);

            int occupiedCount =
                MarkOccupiedSlots(
                    tableConfiguration,
                    candidateSeat,
                    slotCount
                );

            float nearestDistanceForTable =
                float.PositiveInfinity;

            for (int slotIndex = 0;
                 slotIndex < slotCount;
                 slotIndex++)
            {
                RestaurantTableSeatSlot slot =
                    slotBuffer[slotIndex];

                tableConfiguration.TryEvaluateSeatAgainstSlot(
                    candidateSeat,
                    candidateRootPosition,
                    candidateRootRotation,
                    slot,
                    out RestaurantSeatSlotMatch match
                );

                if (match.PositionDistance <
                    nearestDistanceForTable)
                {
                    nearestDistanceForTable =
                        match.PositionDistance;
                }

                if (match.PositionDistance <
                    nearestMatch.PositionDistance)
                {
                    nearestMatch = match;
                }

                if (!match.IsValid)
                {
                    continue;
                }

                if (occupiedSlotBuffer[slotIndex])
                {
                    if (match.Score <
                        bestOccupiedMatch.Score)
                    {
                        bestOccupiedMatch = match;
                    }

                    continue;
                }

                if (match.Score <
                    bestFreeMatch.Score)
                {
                    bestFreeMatch = match;
                }
            }

            bool tableIsFull =
                occupiedCount >=
                tableConfiguration.MaximumCustomers;

            float broadTolerance =
                candidateSeat.UseProfile
                    .SlotPositionTolerance *
                fullTableDetectionToleranceMultiplier;

            bool isAroundTablePerimeter =
                tableConfiguration
                    .TryCalculatePerimeterGapAtPose(
                        tableConfiguration.transform.position,
                        tableConfiguration.transform.rotation,
                        candidateAssociationPosition,
                        out float perimeterGap
                    ) &&
                perimeterGap >= -broadTolerance &&
                Mathf.Abs(
                    perimeterGap -
                    tableConfiguration.Definition
                        .ParkedGapFromTableEdge
                ) <= broadTolerance;

            float fullTableDistance =
                isAroundTablePerimeter
                    ? Mathf.Abs(
                        perimeterGap -
                        tableConfiguration.Definition
                            .ParkedGapFromTableEdge
                    )
                    : nearestDistanceForTable;

            if (tableIsFull &&
                isAroundTablePerimeter &&
                fullTableDistance <
                    nearestFullTableDistance)
            {
                nearestFullTable = tableConfiguration;
                nearestFullTableDistance =
                    fullTableDistance;
            }
        }

        if (bestFreeMatch.IsValid)
        {
            return RestaurantPlacementConstraintEvaluation.Valid();
        }

        if (nearestFullTable != null)
        {
            int maximumCustomers =
                nearestFullTable.MaximumCustomers;

            return RestaurantPlacementConstraintEvaluation.Invalid(
                "seating_table_capacity_exceeded",
                "Esta mesa admite un máximo de " +
                maximumCustomers +
                " clientes.",
                candidateSeat.name +
                " intenta añadir una plaza a " +
                nearestFullTable.name +
                ", que ya tiene ocupadas sus " +
                maximumCustomers +
                " plazas.",
                nearestFullTable,
                true
            );
        }

        if (bestOccupiedMatch.IsValid)
        {
            return RestaurantPlacementConstraintEvaluation.Invalid(
                "seating_slot_occupied",
                "Esta plaza de la mesa ya está ocupada.",
                candidateSeat.name +
                " coincide con la plaza " +
                bestOccupiedMatch.Slot.SlotIndex +
                " de " +
                bestOccupiedMatch.TableConfiguration.name +
                ".",
                bestOccupiedMatch.TableConfiguration
            );
        }

        if (nearestMatch.TableConfiguration != null &&
            nearestMatch.PositionDistance <=
                candidateSeat.UseProfile.SlotPositionTolerance)
        {
            if (nearestMatch.FacingAngle >
                candidateSeat.UseProfile.MaximumFacingAngle)
            {
                return RestaurantPlacementConstraintEvaluation.Invalid(
                    "seating_facing_invalid",
                    "La silla debe mirar hacia la mesa.",
                    candidateSeat.name +
                    " forma un ángulo de " +
                    nearestMatch.FacingAngle.ToString("0.##") +
                    "° con la plaza más próxima.",
                    nearestMatch.TableConfiguration
                );
            }

            if (nearestMatch.VerticalDifference >
                candidateSeat.UseProfile.MaximumVerticalDifference)
            {
                return RestaurantPlacementConstraintEvaluation.Invalid(
                    "seating_height_invalid",
                    "La silla y la mesa no están a la misma altura.",
                    candidateSeat.name +
                    " tiene una diferencia vertical de " +
                    nearestMatch.VerticalDifference.ToString("0.###") +
                    " m.",
                    nearestMatch.TableConfiguration
                );
            }
        }

        return RestaurantPlacementConstraintEvaluation.Invalid(
            "seating_slot_required",
            "Coloca la silla en una plaza válida de una mesa.",
            candidateSeat.name +
            " no coincide con ninguna plaza libre configurada.",
            nearestMatch.TableConfiguration
        );
    }

    private RestaurantPlacementConstraintEvaluation
        EvaluateTableCandidate(
            RestaurantTableSeatingConfiguration candidateTable,
            Vector3 candidateRootPosition,
            Quaternion candidateRootRotation
        )
    {
        if (seatRegistry == null ||
            candidateTable == null)
        {
            return RestaurantPlacementConstraintEvaluation.Valid();
        }

        int slotCount =
            candidateTable.WriteSlotsAtPose(
                candidateRootPosition,
                candidateRootRotation,
                slotBuffer
            );

        if (slotCount <= 0 ||
            slotCount > occupiedSlotBuffer.Length)
        {
            return RestaurantPlacementConstraintEvaluation.Invalid(
                "seating_table_configuration_invalid",
                "La mesa no tiene una configuración de plazas válida.",
                candidateTable.name +
                " no ha generado plazas utilizables.",
                candidateTable
            );
        }

        ClearOccupiedSlots(slotCount);

        foreach (RestaurantSeat seat
                 in seatRegistry.RegisteredSeats)
        {
            if (seat == null ||
                !ReferenceEquals(
                    seat.AssociatedTable,
                    candidateTable
                ))
            {
                continue;
            }

            int bestSlotIndex = -1;
            float bestScore = float.PositiveInfinity;

            for (int slotIndex = 0;
                 slotIndex < slotCount;
                 slotIndex++)
            {
                if (occupiedSlotBuffer[slotIndex])
                {
                    continue;
                }

                RestaurantTableSeatSlot slot =
                    slotBuffer[slotIndex];

                bool matched =
                    candidateTable.TryEvaluateSeatAgainstSlot(
                        seat,
                        seat.transform.position,
                        seat.transform.rotation,
                        slot,
                        out RestaurantSeatSlotMatch match
                    );

                if (!matched ||
                    match.Score >= bestScore)
                {
                    continue;
                }

                bestScore = match.Score;
                bestSlotIndex = slotIndex;
            }

            if (bestSlotIndex < 0)
            {
                return RestaurantPlacementConstraintEvaluation.Invalid(
                    "seating_table_move_orphans_seat",
                    "La mesa dejaría una silla fuera de una plaza válida.",
                    "Mover " +
                    candidateTable.name +
                    " dejaría sin plaza a " +
                    seat.name +
                    ".",
                    seat
                );
            }

            occupiedSlotBuffer[bestSlotIndex] = true;
        }

        return RestaurantPlacementConstraintEvaluation.Valid();
    }

    private int MarkOccupiedSlots(
        RestaurantTableSeatingConfiguration tableConfiguration,
        RestaurantSeat ignoredSeat,
        int slotCount
    )
    {
        int occupiedCount = 0;

        foreach (RestaurantSeat seat
                 in seatRegistry.RegisteredSeats)
        {
            if (seat == null ||
                ReferenceEquals(seat, ignoredSeat))
            {
                continue;
            }

            if (ReferenceEquals(
                    seat.AssociatedTable,
                    tableConfiguration
                ) &&
                seat.AssociatedSlotIndex >= 0 &&
                seat.AssociatedSlotIndex < slotCount &&
                !occupiedSlotBuffer[
                    seat.AssociatedSlotIndex
                ])
            {
                occupiedSlotBuffer[
                    seat.AssociatedSlotIndex
                ] = true;

                occupiedCount++;
                continue;
            }

            int bestSlotIndex = -1;
            float bestScore = float.PositiveInfinity;

            for (int slotIndex = 0;
                 slotIndex < slotCount;
                 slotIndex++)
            {
                if (occupiedSlotBuffer[slotIndex])
                {
                    continue;
                }

                bool matched =
                    tableConfiguration.TryEvaluateSeatAgainstSlot(
                        seat,
                        seat.transform.position,
                        seat.transform.rotation,
                        slotBuffer[slotIndex],
                        out RestaurantSeatSlotMatch match
                    );

                if (!matched ||
                    match.Score >= bestScore)
                {
                    continue;
                }

                bestScore = match.Score;
                bestSlotIndex = slotIndex;
            }

            if (bestSlotIndex < 0)
            {
                continue;
            }

            occupiedSlotBuffer[bestSlotIndex] = true;
            occupiedCount++;
        }

        return occupiedCount;
    }

    private void ClearOccupiedSlots(int count)
    {
        for (int index = 0;
             index < count;
             index++)
        {
            occupiedSlotBuffer[index] = false;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (seatRegistry == null)
        {
            TryGetComponent(out seatRegistry);
        }

        if (tableRegistry == null)
        {
            TryGetComponent(out tableRegistry);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        fullTableDetectionToleranceMultiplier =
            Mathf.Max(
                1f,
                fullTableDetectionToleranceMultiplier
            );

        CacheDependenciesIfNeeded();
    }
#endif
}
