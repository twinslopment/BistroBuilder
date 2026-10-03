using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicChairNavigationReadinessValidator
    {
        internal const string Version = "1.0.0";

        private const float SeatHeightToleranceMeters = 0.03f;

        internal static SavicChairNavigationReadinessRecord Validate(
            SavicManifest manifest,
            SavicChairAuthoringRecord plan,
            string prefabAssetPath)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (plan == null || !plan.planned)
            {
                throw new InvalidOperationException(
                    "Navigation readiness requires a valid chair authoring plan.");
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabAssetPath);

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Published chair prefab cannot be loaded for navigation validation.");
            }

            RestaurantSeat seat =
                prefab.GetComponent<RestaurantSeat>();

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent<RestaurantPlacementFootprint>();

            BistroBuilderSeatCirculationEnvelope envelope =
                prefab.GetComponent<BistroBuilderSeatCirculationEnvelope>();

            if (seat == null ||
                footprint == null ||
                envelope == null)
            {
                throw new InvalidOperationException(
                    "Published chair is missing seat, footprint or dynamic circulation integration.");
            }

            if (!seat.ValidateConfiguration(out string seatError))
            {
                throw new InvalidOperationException(
                    "Published chair seating configuration is invalid: " +
                    seatError);
            }

            if (!footprint.BlocksOtherPlacements)
            {
                throw new InvalidOperationException(
                    "Published chair footprint does not block static navigation topology.");
            }

            if (!Approximately(
                    footprint.Size.x,
                    plan.finalWidthMeters,
                    0.002f) ||
                !Approximately(
                    footprint.Size.y,
                    plan.finalDepthMeters,
                    0.002f))
            {
                throw new InvalidOperationException(
                    "Published chair footprint dimensions differ from normalized dimensions.");
            }

            RestaurantPlacementShape shape =
                footprint.BuildCurrentShape();

            ValidateShape(shape);

            Vector3 facing =
                seat.CalculateFacingDirectionAtPose(
                    prefab.transform.rotation);

            facing.y = 0f;

            if (facing.sqrMagnitude < 0.9f)
            {
                throw new InvalidOperationException(
                    "Published chair produced an invalid functional facing direction.");
            }

            facing.Normalize();

            if (Vector3.Dot(
                    facing,
                    prefab.transform.forward) <
                0.999f)
            {
                throw new InvalidOperationException(
                    "Published chair functional front is not canonical +Z.");
            }

            if (seat.SeatPoint == null ||
                seat.CustomerApproachPoint == null)
            {
                throw new InvalidOperationException(
                    "Published chair is missing navigation interaction anchors.");
            }

            float seatHeight =
                prefab.transform
                    .InverseTransformPoint(
                        seat.SeatPoint.position)
                    .y;

            if (Math.Abs(
                    seatHeight -
                    plan.finalSeatHeightMeters) >
                SeatHeightToleranceMeters)
            {
                throw new InvalidOperationException(
                    "Chair SeatPoint height differs from detected normalized seat height.");
            }

            RestaurantSeatUseProfileDefinition profile =
                seat.UseProfile;

            if (profile == null)
            {
                throw new InvalidOperationException(
                    "Published chair has no seat-use profile.");
            }

            float approachClearance =
                DistanceOutsideShapeXZ(
                    shape,
                    seat.CustomerApproachPoint.position);

            float minimumApproachClearance =
                Math.Max(
                    profile.CustomerApproachDistance,
                    profile.CustomerApproachRadius);

            if (approachClearance <
                minimumApproachClearance)
            {
                throw new InvalidOperationException(
                    "Chair customer approach point is too close to the static footprint.");
            }

            Vector3 approachDelta =
                seat.CustomerApproachPoint.position -
                prefab.transform.position;

            approachDelta.y = 0f;

            if (Vector3.Dot(
                    approachDelta,
                    facing) >=
                -0.05f)
            {
                throw new InvalidOperationException(
                    "Chair customer approach point is not behind the canonical front.");
            }

            if (manifest.chairColliders == null ||
                !manifest.chairColliders.generated ||
                !manifest.chairColliders.semanticBacked ||
                manifest.chairColliders.colliderCount < 3)
            {
                throw new InvalidOperationException(
                    "Navigation readiness requires semantic compound chair colliders first.");
            }

            Collider[] colliders =
                prefab.GetComponentsInChildren<Collider>(true);

            int solidColliderCount = 0;

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Collider collider = colliders[index];

                if (collider != null &&
                    !collider.isTrigger)
                {
                    solidColliderCount++;
                }
            }

            if (solidColliderCount !=
                manifest.chairColliders.colliderCount)
            {
                throw new InvalidOperationException(
                    "Published chair solid collider count differs from SAVIC collider metadata.");
            }

            return new SavicChairNavigationReadinessRecord
            {
                validated = true,
                validatorVersion = Version,
                footprintBlocksNavigation = true,
                footprintWidthMeters = footprint.Size.x,
                footprintDepthMeters = footprint.Size.y,
                seatHeightMeters = seatHeight,
                approachDistanceMeters = approachClearance,
                approachRadiusMeters = profile.CustomerApproachRadius,
                solidColliderCount = solidColliderCount,
                canonicalFrontPositiveZ = true,
                evidence =
                    "Navigation consumes the canonical chair footprint; SeatPoint " +
                    "matches detected seat height; approach remains behind +Z-facing " +
                    "chair and outside the static obstacle; BistroBuilderSeatCirculationEnvelope " +
                    "is installed for pull-out/return dynamic space; semantic compound " +
                    "colliders match the published manifest.",
                validatedUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static void ValidateShape(
            RestaurantPlacementShape shape)
        {
            if (!IsFinite(shape.Center) ||
                !IsFinite(shape.RightAxis) ||
                !IsFinite(shape.ForwardAxis) ||
                !IsFinite(shape.HalfWidth) ||
                !IsFinite(shape.HalfDepth) ||
                shape.HalfWidth <= 0f ||
                shape.HalfDepth <= 0f)
            {
                throw new InvalidOperationException(
                    "Published chair produced an invalid navigation footprint.");
            }

            float orthogonality =
                Math.Abs(
                    Vector3.Dot(
                        shape.RightAxis,
                        shape.ForwardAxis));

            if (orthogonality > 0.01f)
            {
                throw new InvalidOperationException(
                    "Published chair footprint axes are not orthogonal enough for navigation.");
            }
        }

        private static float DistanceOutsideShapeXZ(
            RestaurantPlacementShape shape,
            Vector3 point)
        {
            Vector3 delta =
                point -
                shape.Center;

            delta.y = 0f;

            float localX =
                Math.Abs(
                    Vector3.Dot(
                        delta,
                        shape.RightAxis));

            float localZ =
                Math.Abs(
                    Vector3.Dot(
                        delta,
                        shape.ForwardAxis));

            float outsideX =
                Math.Max(
                    0f,
                    localX -
                    shape.HalfWidth);

            float outsideZ =
                Math.Max(
                    0f,
                    localZ -
                    shape.HalfDepth);

            return (float)Math.Sqrt(
                outsideX * outsideX +
                outsideZ * outsideZ);
        }

        private static bool Approximately(
            float left,
            float right,
            float tolerance)
        {
            return Math.Abs(left - right) <=
                   Math.Max(0f, tolerance);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
