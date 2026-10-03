using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicTableNavigationReadinessValidator
    {
        internal const string Version = "1.0.0";

        private const float DefaultAgentRadius = 0.28f;
        private const float NavigationStaticClearance = 0.08f;
        private const float MinimumEndpointClearance =
            DefaultAgentRadius + NavigationStaticClearance;

        internal static SavicTableNavigationReadinessRecord Validate(
            SavicManifest manifest,
            SavicTableAuthoringRecord plan,
            string prefabAssetPath)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (plan == null || !plan.planned)
            {
                throw new InvalidOperationException(
                    "Navigation readiness requires a valid table authoring plan.");
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabAssetPath);

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Published table prefab cannot be loaded for navigation validation.");
            }

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent<RestaurantPlacementFootprint>();

            RestaurantTable table =
                prefab.GetComponent<RestaurantTable>();

            if (footprint == null ||
                table == null)
            {
                throw new InvalidOperationException(
                    "Published table is missing the footprint or table component required by navigation.");
            }

            if (!footprint.BlocksOtherPlacements)
            {
                throw new InvalidOperationException(
                    "Published table footprint does not block static navigation topology.");
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
                    "Published table footprint dimensions differ from the normalized table dimensions.");
            }

            RestaurantPlacementShape shape =
                footprint.BuildCurrentShape();

            ValidateShape(shape);

            Transform customer =
                table.CustomerApproachPoint;

            Transform waiter =
                table.WaiterServicePoint;

            if (customer == null ||
                waiter == null)
            {
                throw new InvalidOperationException(
                    "Published table is missing navigation interaction endpoints.");
            }

            float customerClearance =
                DistanceOutsideShapeXZ(
                    shape,
                    customer.position);

            float waiterClearance =
                DistanceOutsideShapeXZ(
                    shape,
                    waiter.position);

            if (customerClearance <
                MinimumEndpointClearance)
            {
                throw new InvalidOperationException(
                    "Customer approach point is too close to the table obstacle for the default navigation radius.");
            }

            if (waiterClearance <
                MinimumEndpointClearance)
            {
                throw new InvalidOperationException(
                    "Waiter service point is too close to the table obstacle for the default navigation radius.");
            }

            if (manifest.tableColliders == null ||
                !manifest.tableColliders.generated ||
                !manifest.tableColliders.semanticBacked ||
                manifest.tableColliders.colliderCount <= 1)
            {
                throw new InvalidOperationException(
                    "Navigation readiness requires the semantic compound collider publication to be valid first.");
            }

            Collider[] colliders =
                prefab.GetComponentsInChildren<Collider>(true);

            int solidColliderCount = 0;

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Collider collider =
                    colliders[index];

                if (collider != null &&
                    !collider.isTrigger)
                {
                    solidColliderCount++;
                }
            }

            if (solidColliderCount !=
                manifest.tableColliders.colliderCount)
            {
                throw new InvalidOperationException(
                    "Published solid collider count differs from SAVIC collider metadata.");
            }

            return new SavicTableNavigationReadinessRecord
            {
                validated = true,
                validatorVersion = Version,
                footprintBlocksNavigation = true,
                footprintWidthMeters =
                    footprint.Size.x,
                footprintDepthMeters =
                    footprint.Size.y,
                defaultAgentRadiusMeters =
                    DefaultAgentRadius,
                requiredEndpointClearanceMeters =
                    MinimumEndpointClearance,
                customerEndpointClearanceMeters =
                    customerClearance,
                waiterEndpointClearanceMeters =
                    waiterClearance,
                solidColliderCount =
                    solidColliderCount,
                usesCanonicalFootprintTopology =
                    true,
                evidence =
                    "Navigation consumes the canonical RestaurantPlacementFootprint; " +
                    "customer/waiter endpoints remain outside the obstacle with default-agent clearance; " +
                    "semantic compound colliders are coherent with the published manifest.",
                validatedUtc =
                    DateTime.UtcNow.ToString("O")
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
                    "Published table produced an invalid navigation footprint.");
            }

            float orthogonality =
                Math.Abs(
                    Vector3.Dot(
                        shape.RightAxis,
                        shape.ForwardAxis));

            if (orthogonality > 0.01f)
            {
                throw new InvalidOperationException(
                    "Published table footprint axes are not orthogonal enough for navigation.");
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
            return Math.Abs(
                       left -
                       right) <=
                   Math.Max(
                       0f,
                       tolerance);
        }

        private static bool IsFinite(
            Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(
            float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
