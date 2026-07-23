using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regla genérica que mantiene libres los volúmenes operativos.
///
/// Valida en ambos sentidos:
/// - Los espacios operativos del candidato no pueden contener
///   otros muebles u obstáculos.
/// - La huella del candidato no puede invadir espacios operativos
///   de artículos ya colocados.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Operational Clearance Rule"
)]
public sealed class RestaurantOperationalClearanceConstraintRule :
    MonoBehaviour,
    IRestaurantPlacementConstraintRule
{
    [SerializeField]
    private bool constraintEnabled = true;

    [SerializeField]
    private int priority = 100;

    private readonly List<RestaurantPlacementFootprint>
        footprintBuffer =
            new List<RestaurantPlacementFootprint>(32);

    private readonly List<RestaurantPlacementObstacle>
        obstacleBuffer =
            new List<RestaurantPlacementObstacle>(16);

    public int Priority =>
        priority;

    public bool IsConstraintEnabled =>
        constraintEnabled;

    public RestaurantPlacementConstraintEvaluation Evaluate(
        RestaurantPlacementConstraintContext context
    )
    {
        if (context.Member == null ||
            context.CandidateFootprint == null)
        {
            return RestaurantPlacementConstraintEvaluation.Valid();
        }

        CopyRelevantFootprints(context);

        RestaurantOperationalClearanceSet candidateSet =
            context.Member.GetComponent<
                RestaurantOperationalClearanceSet
            >();

        if (candidateSet != null &&
            candidateSet.RequiresClearanceForOwner)
        {
            RestaurantPlacementConstraintEvaluation candidateResult =
                EvaluateCandidateClearances(
                    context,
                    candidateSet
                );

            if (!candidateResult.IsValid)
            {
                return candidateResult;
            }
        }

        if (context.PlacementRegistry == null)
        {
            return RestaurantPlacementConstraintEvaluation.Valid();
        }

        RestaurantPlacementShape candidateShape =
            context.CandidateFootprint.BuildShapeAtPose(
                context.CandidateRootPosition,
                context.CandidateRootRotation
            );

        for (int footprintIndex = 0;
             footprintIndex < footprintBuffer.Count;
             footprintIndex++)
        {
            RestaurantPlacementFootprint footprint =
                footprintBuffer[footprintIndex];

            if (footprint == null ||
                ReferenceEquals(
                    footprint,
                    context.CandidateFootprint
                ))
            {
                continue;
            }

            RestaurantOperationalClearanceSet otherSet =
                footprint.GetComponent<
                    RestaurantOperationalClearanceSet
                >();

            if (otherSet == null ||
                !otherSet.BlocksOtherPlacements)
            {
                continue;
            }

            for (int index = 0;
                 index < otherSet.ClearanceCount;
                 index++)
            {
                if (!otherSet.TryBuildCurrentShape(
                        index,
                        out RestaurantPlacementShape clearanceShape,
                        out RestaurantOperationalClearanceBox definition
                    ))
                {
                    continue;
                }

                if (!RestaurantPlacementCollisionUtility
                        .HasPhysicalOverlap(
                            candidateShape,
                            clearanceShape
                        ))
                {
                    continue;
                }

                return RestaurantPlacementConstraintEvaluation.Invalid(
                    "operational_clearance_existing",
                    definition.BlockedUserMessage,
                    context.Member.name +
                    " invade el espacio operativo '" +
                    definition.ClearanceId +
                    "' de " +
                    footprint.name +
                    ".",
                    footprint
                );
            }
        }

        return RestaurantPlacementConstraintEvaluation.Valid();
    }

    private void CopyRelevantFootprints(
        RestaurantPlacementConstraintContext context
    )
    {
        footprintBuffer.Clear();

        if (context.PlacementRegistry == null)
        {
            return;
        }

        if (context.CandidateArea != null)
        {
            context.PlacementRegistry.CopyFootprintsInArea(
                context.CandidateArea,
                footprintBuffer,
                context.CandidateFootprint,
                true
            );

            return;
        }

        foreach (RestaurantPlacementFootprint footprint
                 in context.PlacementRegistry.RegisteredFootprints)
        {
            if (footprint == null ||
                ReferenceEquals(
                    footprint,
                    context.CandidateFootprint
                ) ||
                !footprint.BlocksOtherPlacements)
            {
                continue;
            }

            footprintBuffer.Add(footprint);
        }
    }


    private static bool IsShapeInsideArea(
        RestaurantPlacementShape shape,
        RestaurantArea area
    )
    {
        if (area == null)
        {
            return true;
        }

        if (!area.ContainsPosition(shape.Center))
        {
            return false;
        }

        return
            area.ContainsPosition(shape.GetCorner(-1f, -1f)) &&
            area.ContainsPosition(shape.GetCorner(-1f, 1f)) &&
            area.ContainsPosition(shape.GetCorner(1f, -1f)) &&
            area.ContainsPosition(shape.GetCorner(1f, 1f));
    }

    private RestaurantPlacementConstraintEvaluation
        EvaluateCandidateClearances(
            RestaurantPlacementConstraintContext context,
            RestaurantOperationalClearanceSet candidateSet
        )
    {
        for (int clearanceIndex = 0;
             clearanceIndex < candidateSet.ClearanceCount;
             clearanceIndex++)
        {
            if (!candidateSet.TryBuildShapeAtPose(
                    clearanceIndex,
                    context.CandidateRootPosition,
                    context.CandidateRootRotation,
                    out RestaurantPlacementShape clearanceShape,
                    out RestaurantOperationalClearanceBox definition
                ))
            {
                continue;
            }

            if (context.CandidateArea != null &&
                !IsShapeInsideArea(
                    clearanceShape,
                    context.CandidateArea
                ))
            {
                return RestaurantPlacementConstraintEvaluation.Invalid(
                    "operational_clearance_outside_area",
                    definition.BlockedUserMessage,
                    "El espacio operativo '" +
                    definition.ClearanceId +
                    "' de " +
                    context.Member.name +
                    " sale del área " +
                    context.CandidateArea.name +
                    ".",
                    context.CandidateArea
                );
            }

            for (int footprintIndex = 0;
                 footprintIndex < footprintBuffer.Count;
                 footprintIndex++)
            {
                RestaurantPlacementFootprint footprint =
                    footprintBuffer[footprintIndex];

                if (footprint == null ||
                    ReferenceEquals(
                        footprint,
                        context.CandidateFootprint
                    ))
                {
                    continue;
                }

                RestaurantPlacementShape otherShape =
                    footprint.BuildCurrentShape();

                if (!RestaurantPlacementCollisionUtility
                        .HasPhysicalOverlap(
                            clearanceShape,
                            otherShape
                        ))
                {
                    continue;
                }

                return RestaurantPlacementConstraintEvaluation.Invalid(
                    "operational_clearance_candidate",
                    definition.BlockedUserMessage,
                    "El espacio operativo '" +
                    definition.ClearanceId +
                    "' de " +
                    context.Member.name +
                    " está ocupado por " +
                    footprint.name +
                    ".",
                    footprint
                );
            }

            if (context.ObstacleRegistry == null)
            {
                continue;
            }

            context.ObstacleRegistry.CopyBlockingObstacles(
                obstacleBuffer
            );

            for (int obstacleIndex = 0;
                 obstacleIndex < obstacleBuffer.Count;
                 obstacleIndex++)
            {
                RestaurantPlacementObstacle obstacle =
                    obstacleBuffer[obstacleIndex];

                if (obstacle == null ||
                    !obstacle.IsBlocking)
                {
                    continue;
                }

                RestaurantPlacementShape obstacleShape =
                    new RestaurantPlacementShape(
                        obstacle.WorldCenter,
                        obstacle.WorldRightAxis,
                        obstacle.WorldForwardAxis,
                        obstacle.WorldSize * 0.5f,
                        obstacle.MinimumClearance
                    );

                if (!RestaurantPlacementCollisionUtility
                        .HasPhysicalOverlap(
                            clearanceShape,
                            obstacleShape
                        ))
                {
                    continue;
                }

                return RestaurantPlacementConstraintEvaluation.Invalid(
                    "operational_clearance_obstacle",
                    definition.BlockedUserMessage,
                    "El espacio operativo '" +
                    definition.ClearanceId +
                    "' de " +
                    context.Member.name +
                    " está ocupado por " +
                    obstacle.name +
                    ".",
                    obstacle
                );
            }
        }

        return RestaurantPlacementConstraintEvaluation.Valid();
    }
}
