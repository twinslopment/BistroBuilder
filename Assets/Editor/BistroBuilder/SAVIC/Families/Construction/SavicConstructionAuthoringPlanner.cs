using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicConstructionAuthoringPlanner
    {
        internal const string Version = "1.0.0";

        internal const string WallRole = "WALL_VISUAL";
        internal const string DoorRole = "DOOR_OPENING_FILL";
        internal const string WindowRole = "WINDOW_OPENING_FILL";

        internal static bool TryPlan(
            SavicManifest manifest,
            out SavicConstructionAuthoringRecord plan,
            out string reasonCode,
            out string error)
        {
            plan = new SavicConstructionAuthoringRecord
            {
                plannerVersion = Version
            };
            reasonCode = string.Empty;
            error = string.Empty;

            if (manifest?.source == null ||
                manifest.model3D == null ||
                manifest.classification == null)
            {
                reasonCode = "CONSTRUCTION_INVALID_MANIFEST";
                error = "Construction planning requires source, analysis and classification.";
                return false;
            }

            SavicModelAnalysisRecord analysis = manifest.model3D;
            if (!analysis.analyzed || !analysis.hasUsableBounds)
            {
                reasonCode = "CONSTRUCTION_GEOMETRY_UNUSABLE";
                error = "Construction asset has no usable metric bounds.";
                return false;
            }

            if (analysis.hasSkinnedMeshes)
            {
                reasonCode = "CONSTRUCTION_SKINNED_UNSUPPORTED";
                error = "Construction assets must be static geometry.";
                return false;
            }

            string type = manifest.classification.type ?? string.Empty;
            string role;
            string openingType;

            if (string.Equals(type, "Wall", StringComparison.Ordinal))
            {
                role = WallRole;
                openingType = string.Empty;
            }
            else if (string.Equals(type, "Door", StringComparison.Ordinal))
            {
                role = DoorRole;
                openingType = "door";
            }
            else if (string.Equals(type, "Window", StringComparison.Ordinal))
            {
                role = WindowRole;
                openingType = "window";
            }
            else
            {
                reasonCode = "CONSTRUCTION_TYPE_UNSUPPORTED";
                error = "Classification is not a supported construction type.";
                return false;
            }

            float horizontalA = analysis.widthMeters;
            float horizontalB = analysis.depthMeters;
            float nominalWidth = Mathf.Max(horizontalA, horizontalB);
            float nominalDepth = Mathf.Min(horizontalA, horizontalB);
            float nominalHeight = analysis.heightMeters;
            float yaw = horizontalB > horizontalA ? 90f : 0f;

            if (!HasSafeDimensions(
                    type,
                    nominalWidth,
                    nominalHeight,
                    nominalDepth))
            {
                reasonCode = "CONSTRUCTION_DIMENSIONS_OUT_OF_RANGE";
                error =
                    type +
                    " dimensions are outside the safe automatic construction range: " +
                    nominalWidth.ToString("0.###", CultureInfo.InvariantCulture) +
                    " x " +
                    nominalHeight.ToString("0.###", CultureInfo.InvariantCulture) +
                    " x " +
                    nominalDepth.ToString("0.###", CultureInfo.InvariantCulture) +
                    " m.";
                return false;
            }

            EnsureCanonicalContentId(manifest);

            string contentFolder =
                "Assets/Generated/BistroBuilder/SAVIC/Published/Construction/" +
                manifest.canonicalContentId;

            plan.planned = true;
            plan.role = role;
            plan.definitionId = manifest.canonicalContentId;
            plan.openingType = openingType;
            plan.nominalWidthMeters = nominalWidth;
            plan.nominalHeightMeters = nominalHeight;
            plan.nominalDepthMeters = nominalDepth;
            plan.visualYawDegrees = yaw;
            plan.prefabAssetPath =
                contentFolder +
                "/Construction_" +
                manifest.canonicalContentId +
                ".prefab";
            plan.planReason =
                "Explicit " +
                type +
                " identity mapped to the canonical Bistro Builder construction contract.";
            plan.plannedUtc = DateTime.UtcNow.ToString("O");
            return true;
        }

        private static bool HasSafeDimensions(
            string type,
            float width,
            float height,
            float depth)
        {
            if (!IsFinitePositive(width) ||
                !IsFinitePositive(height) ||
                !IsFinitePositive(depth))
            {
                return false;
            }

            if (string.Equals(type, "Door", StringComparison.Ordinal))
            {
                return width >= 0.40f && width <= 2.50f &&
                       height >= 1.50f && height <= 3.20f &&
                       depth >= 0.005f && depth <= 0.80f;
            }

            if (string.Equals(type, "Window", StringComparison.Ordinal))
            {
                return width >= 0.20f && width <= 5.00f &&
                       height >= 0.20f && height <= 4.00f &&
                       depth >= 0.003f && depth <= 0.80f;
            }

            return width >= 0.10f && width <= 8.00f &&
                   height >= 0.30f && height <= 5.00f &&
                   depth >= 0.003f && depth <= 0.80f;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   value > 0f;
        }

        private static void EnsureCanonicalContentId(
            SavicManifest manifest)
        {
            if (!string.IsNullOrWhiteSpace(manifest.canonicalContentId))
                return;

            if (string.IsNullOrWhiteSpace(manifest.savicId))
            {
                throw new InvalidOperationException(
                    "Construction content requires a SAVIC identity.");
            }

            string normalizedType =
                NormalizeToken(
                    manifest.classification?.type ?? "construction");

            manifest.canonicalContentId =
                "bb_" +
                normalizedType +
                "_" +
                manifest.savicId.ToLowerInvariant();
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "construction";

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < value.Length; index++)
            {
                char character = char.ToLowerInvariant(value[index]);
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
                else if (builder.Length > 0 &&
                         builder[builder.Length - 1] != '_')
                {
                    builder.Append('_');
                }
            }

            string result = builder.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(result)
                ? "construction"
                : result;
        }
    }
}
