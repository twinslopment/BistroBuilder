using System;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicTableAuthoringPlanner
    {
        internal const string Version = "1.0.0";

        internal const string Table2TemplatePath =
            "Assets/Prefabs/Restaurant/Furniture/Table_Basic.prefab";

        internal const string Table4TemplatePath =
            "Assets/Prefabs/Restaurant/Furniture/Table_Basic_4.prefab";

        internal const string Table2SeatingPath =
            "Assets/Data/Restaurant/Seating/TableConfigurations/" +
            "TableSeatingConfiguration_TableBasic2.asset";

        internal const string Table4SeatingPath =
            "Assets/Data/Restaurant/Seating/TableConfigurations/" +
            "TableSeatingConfiguration_TableBasic4.asset";

        private const float NominalTableHeightMeters = 0.75f;
        private const float MinimumAcceptedHeightMeters = 0.60f;
        private const float MaximumAcceptedHeightMeters = 0.90f;
        private const float MinimumSourceHeightForCorrection = 0.30f;
        private const float MaximumSourceHeightForCorrection = 2.50f;
        private const float MinimumSafeUniformScale = 0.35f;
        private const float MaximumSafeUniformScale = 2.85f;

        internal static bool TryPlan(
            SavicManifest manifest,
            out SavicTableAuthoringRecord plan,
            out string rejectionReason)
        {
            plan = new SavicTableAuthoringRecord();
            rejectionReason = string.Empty;

            if (manifest?.classification == null ||
                !string.Equals(
                    manifest.classification.type,
                    "Table",
                    StringComparison.Ordinal))
            {
                rejectionReason =
                    "The content is not classified as a table.";
                return false;
            }

            if (manifest.classification.score < 0.78f)
            {
                rejectionReason =
                    "Table classification confidence is too low for automatic authoring.";
                return false;
            }

            SavicModelAnalysisRecord analysis =
                manifest.model3D;

            if (analysis == null ||
                !analysis.analyzed ||
                !analysis.hasUsableBounds)
            {
                rejectionReason =
                    "The model has no usable analyzed bounds.";
                return false;
            }

            if (analysis.hasSkinnedMeshes)
            {
                rejectionReason =
                    "Skinned meshes are not accepted for automatic table publication.";
                return false;
            }

            if (analysis.hasNegativeScale)
            {
                rejectionReason =
                    "Negative transform scale requires review before table publication.";
                return false;
            }

            float uniformScale = 1f;
            bool correctionApplied = false;

            if (analysis.heightMeters < MinimumAcceptedHeightMeters ||
                analysis.heightMeters > MaximumAcceptedHeightMeters)
            {
                if (analysis.heightMeters <
                        MinimumSourceHeightForCorrection ||
                    analysis.heightMeters >
                        MaximumSourceHeightForCorrection)
                {
                    rejectionReason =
                        "Source height is outside the safe auto-correction range.";
                    return false;
                }

                uniformScale =
                    NominalTableHeightMeters /
                    analysis.heightMeters;

                if (uniformScale < MinimumSafeUniformScale ||
                    uniformScale > MaximumSafeUniformScale)
                {
                    rejectionReason =
                        "Required scale correction is too large for automatic publication.";
                    return false;
                }

                correctionApplied = true;
            }

            float scaledWidth =
                analysis.widthMeters * uniformScale;

            float scaledHeight =
                analysis.heightMeters * uniformScale;

            float scaledDepth =
                analysis.depthMeters * uniformScale;

            bool rotateQuarterTurn =
                scaledDepth > scaledWidth;

            float finalWidth =
                rotateQuarterTurn
                    ? scaledDepth
                    : scaledWidth;

            float finalDepth =
                rotateQuarterTurn
                    ? scaledWidth
                    : scaledDepth;

            if (!HasSafePublishedDimensions(
                    finalWidth,
                    scaledHeight,
                    finalDepth))
            {
                rejectionReason =
                    "Normalized dimensions are not safe for an automatic table profile.";
                return false;
            }

            int capacity =
                ResolveCapacity(
                    finalWidth,
                    finalDepth);

            if (capacity == 0)
            {
                rejectionReason =
                    "The normalized tabletop is too small for the canonical seating rules.";
                return false;
            }

            plan = new SavicTableAuthoringRecord
            {
                planned = true,
                plannerVersion = Version,
                uniformScale = uniformScale,
                visualYawDegrees =
                    rotateQuarterTurn ? 90f : 0f,
                finalWidthMeters = finalWidth,
                finalHeightMeters = scaledHeight,
                finalDepthMeters = finalDepth,
                capacity = capacity,
                suggestedPurchasePriceEuro =
                    ResolveSuggestedPrice(capacity),
                scaleCorrectionApplied =
                    correctionApplied,
                templatePrefabAssetPath =
                    capacity >= 4
                        ? Table4TemplatePath
                        : Table2TemplatePath,
                seatingDefinitionAssetPath =
                    capacity >= 4
                        ? Table4SeatingPath
                        : Table2SeatingPath,
                planReason =
                    BuildPlanReason(
                        correctionApplied,
                        rotateQuarterTurn,
                        capacity),
                plannedUtc =
                    DateTime.UtcNow.ToString("O")
            };

            return true;
        }

        private static bool HasSafePublishedDimensions(
            float width,
            float height,
            float depth)
        {
            return width >= 0.75f &&
                   width <= 2.40f &&
                   height >= MinimumAcceptedHeightMeters &&
                   height <= MaximumAcceptedHeightMeters &&
                   depth >= 0.50f &&
                   depth <= 1.40f;
        }

        private static int ResolveCapacity(
            float width,
            float depth)
        {
            if (width >= 1.30f &&
                depth >= 0.55f)
            {
                return 4;
            }

            if (width >= 0.75f &&
                depth >= 0.50f)
            {
                return 2;
            }

            return 0;
        }

        private static int ResolveSuggestedPrice(
            int capacity)
        {
            return capacity >= 4
                ? 180
                : 120;
        }

        private static string BuildPlanReason(
            bool scaleCorrectionApplied,
            bool rotateQuarterTurn,
            int capacity)
        {
            string scaleReason =
                scaleCorrectionApplied
                    ? "uniform height normalization applied"
                    : "source scale accepted";

            string rotationReason =
                rotateQuarterTurn
                    ? "visual rotated 90 degrees to align the longest side with local X"
                    : "source horizontal orientation accepted";

            return scaleReason +
                   "; " +
                   rotationReason +
                   "; canonical capacity " +
                   capacity +
                   ".";
        }
    }
}
