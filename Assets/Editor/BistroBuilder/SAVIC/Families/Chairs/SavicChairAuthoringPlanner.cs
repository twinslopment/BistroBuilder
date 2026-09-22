using System;
using System.Globalization;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicChairAuthoringPlanner
    {
        internal const string Version = "1.0.0";

        internal const string TemplatePrefabAssetPath =
            "Assets/Prefabs/Restaurant/Generated/Seating/" +
            "SillaContemporaneaDeMaderaYMetal.prefab";

        internal const string SeatUseProfileAssetPath =
            "Assets/Data/Restaurant/Seating/SeatUseProfiles/" +
            "SeatUseProfile_StandardDiningChair.asset";

        internal const string EditableDefinitionAssetPath =
            "Assets/Data/Restaurant/EditMode/EditableDefinitions/" +
            "EditableObjectDefinition_SillaContemporaneaDeMaderaYMetal.asset";

        private const float NominalSeatHeightMeters = 0.46f;
        private const float MinimumAcceptedSeatHeightMeters = 0.40f;
        private const float MaximumAcceptedSeatHeightMeters = 0.52f;
        private const float MinimumSourceSeatHeightForCorrection = 0.24f;
        private const float MaximumSourceSeatHeightForCorrection = 0.75f;
        private const float MinimumSafeUniformScale = 0.65f;
        private const float MaximumSafeUniformScale = 1.45f;

        internal static bool TryPlan(
            SavicManifest manifest,
            out SavicChairAuthoringRecord plan,
            out string rejectionReason)
        {
            plan =
                new SavicChairAuthoringRecord();

            rejectionReason =
                string.Empty;

            if (manifest?.classification == null ||
                !string.Equals(
                    manifest.classification.type,
                    "Chair",
                    StringComparison.Ordinal))
            {
                rejectionReason =
                    "The content is not classified as a chair.";
                return false;
            }

            if (manifest.classification.score < 0.74f)
            {
                rejectionReason =
                    "Chair classification confidence is too low for automatic authoring.";
                return false;
            }

            SavicModelAnalysisRecord analysis =
                manifest.model3D;

            if (analysis == null ||
                !analysis.analyzed ||
                !analysis.hasUsableBounds ||
                analysis.chairGeometry == null ||
                !analysis.chairGeometry.analyzed ||
                !analysis.chairGeometry.usable)
            {
                rejectionReason =
                    "The model has no usable chair geometry profile.";
                return false;
            }

            if (analysis.semanticParts == null ||
                !analysis.semanticParts.analyzed ||
                !analysis.semanticParts.automationReady)
            {
                rejectionReason =
                    "Chair semantic parts are not automation-ready.";
                return false;
            }

            if (analysis.hasSkinnedMeshes)
            {
                rejectionReason =
                    "Skinned meshes are not accepted for automatic chair publication.";
                return false;
            }

            if (analysis.hasNegativeScale)
            {
                rejectionReason =
                    "Negative transform scale requires review before chair publication.";
                return false;
            }

            float sourceSeatHeight =
                analysis.chairGeometry.seatHeightMeters;

            if (!IsFinitePositive(sourceSeatHeight))
            {
                rejectionReason =
                    "Chair seat height is unavailable or invalid.";
                return false;
            }

            float uniformScale =
                1f;

            bool scaleCorrectionApplied =
                false;

            if (sourceSeatHeight <
                    MinimumAcceptedSeatHeightMeters ||
                sourceSeatHeight >
                    MaximumAcceptedSeatHeightMeters)
            {
                if (sourceSeatHeight <
                        MinimumSourceSeatHeightForCorrection ||
                    sourceSeatHeight >
                        MaximumSourceSeatHeightForCorrection)
                {
                    rejectionReason =
                        "Detected seat height is outside the safe auto-correction range.";
                    return false;
                }

                uniformScale =
                    NominalSeatHeightMeters /
                    sourceSeatHeight;

                if (uniformScale <
                        MinimumSafeUniformScale ||
                    uniformScale >
                        MaximumSafeUniformScale)
                {
                    rejectionReason =
                        "Required chair scale correction is too large for automatic publication.";
                    return false;
                }

                scaleCorrectionApplied =
                    true;
            }

            float sourceFrontX =
                analysis.chairGeometry.frontDirectionLocalX;

            float sourceFrontZ =
                analysis.chairGeometry.frontDirectionLocalZ;

            if (!TryResolveCanonicalYaw(
                    sourceFrontX,
                    sourceFrontZ,
                    out float visualYawDegrees))
            {
                rejectionReason =
                    "Chair front direction is ambiguous and cannot be normalized safely.";
                return false;
            }

            float scaledWidth =
                analysis.widthMeters *
                uniformScale;

            float scaledHeight =
                analysis.heightMeters *
                uniformScale;

            float scaledDepth =
                analysis.depthMeters *
                uniformScale;

            bool quarterTurn =
                ApproximatelyQuarterTurn(
                    visualYawDegrees);

            float finalWidth =
                quarterTurn
                    ? scaledDepth
                    : scaledWidth;

            float finalDepth =
                quarterTurn
                    ? scaledWidth
                    : scaledDepth;

            float finalSeatHeight =
                sourceSeatHeight *
                uniformScale;

            if (!HasSafePublishedDimensions(
                    finalWidth,
                    scaledHeight,
                    finalDepth,
                    finalSeatHeight))
            {
                rejectionReason =
                    "Normalized dimensions are not safe for an automatic dining-chair profile.";
                return false;
            }

            plan =
                new SavicChairAuthoringRecord
                {
                    planned = true,
                    plannerVersion = Version,
                    uniformScale =
                        uniformScale,
                    visualYawDegrees =
                        visualYawDegrees,
                    finalWidthMeters =
                        finalWidth,
                    finalHeightMeters =
                        scaledHeight,
                    finalDepthMeters =
                        finalDepth,
                    finalSeatHeightMeters =
                        finalSeatHeight,
                    sourceFrontLocalX =
                        sourceFrontX,
                    sourceFrontLocalZ =
                        sourceFrontZ,
                    canonicalFrontLocalX =
                        0f,
                    canonicalFrontLocalZ =
                        1f,
                    suggestedPurchasePriceEuro =
                        ResolveSuggestedPrice(
                            analysis),
                    scaleCorrectionApplied =
                        scaleCorrectionApplied,
                    templatePrefabAssetPath =
                        TemplatePrefabAssetPath,
                    seatUseProfileAssetPath =
                        SeatUseProfileAssetPath,
                    editableDefinitionAssetPath =
                        EditableDefinitionAssetPath,
                    planReason =
                        BuildPlanReason(
                            scaleCorrectionApplied,
                            visualYawDegrees,
                            finalSeatHeight),
                    plannedUtc =
                        DateTime.UtcNow.ToString("O")
                };

            return true;
        }

        private static bool TryResolveCanonicalYaw(
            float frontX,
            float frontZ,
            out float yawDegrees)
        {
            yawDegrees =
                0f;

            Vector2 front =
                new Vector2(
                    frontX,
                    frontZ);

            if (!IsFinite(front.x) ||
                !IsFinite(front.y) ||
                front.sqrMagnitude < 0.80f)
            {
                return false;
            }

            front.Normalize();

            float angleFromPositiveZ =
                Mathf.Atan2(
                    front.x,
                    front.y) *
                Mathf.Rad2Deg;

            float snappedSourceAngle =
                Mathf.Round(
                    angleFromPositiveZ /
                    90f) *
                90f;

            float angularError =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        angleFromPositiveZ,
                        snappedSourceAngle));

            if (angularError > 12f)
                return false;

            yawDegrees =
                NormalizeYaw(
                    -snappedSourceAngle);

            return true;
        }

        private static float NormalizeYaw(
            float yaw)
        {
            float normalized =
                Mathf.Repeat(
                    yaw + 180f,
                    360f) -
                180f;

            if (Mathf.Abs(normalized) < 0.001f)
                return 0f;

            if (Mathf.Abs(
                    Mathf.Abs(normalized) -
                    180f) <
                0.001f)
            {
                return 180f;
            }

            return normalized;
        }

        private static bool ApproximatelyQuarterTurn(
            float yawDegrees)
        {
            float absolute =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        0f,
                        yawDegrees));

            return Math.Abs(
                       absolute -
                       90f) <=
                   0.01f;
        }

        private static bool HasSafePublishedDimensions(
            float width,
            float height,
            float depth,
            float seatHeight)
        {
            return
                width >= 0.32f &&
                width <= 0.95f &&
                height >= 0.62f &&
                height <= 1.25f &&
                depth >= 0.34f &&
                depth <= 0.95f &&
                seatHeight >=
                    MinimumAcceptedSeatHeightMeters &&
                seatHeight <=
                    MaximumAcceptedSeatHeightMeters;
        }

        private static int ResolveSuggestedPrice(
            SavicModelAnalysisRecord analysis)
        {
            bool armchair =
                FindSemanticPart(
                    analysis.semanticParts,
                    "chair.arms") != null;

            return armchair
                ? 85
                : 60;
        }

        private static SavicSemanticPartRecord FindSemanticPart(
            SavicSemanticPartAnalysisRecord semantic,
            string partId)
        {
            if (semantic?.parts == null)
                return null;

            for (int index = 0;
                 index < semantic.parts.Count;
                 index++)
            {
                SavicSemanticPartRecord part =
                    semantic.parts[index];

                if (part != null &&
                    string.Equals(
                        part.partId,
                        partId,
                        StringComparison.Ordinal))
                {
                    return part;
                }
            }

            return null;
        }

        private static string BuildPlanReason(
            bool scaleCorrectionApplied,
            float visualYawDegrees,
            float finalSeatHeight)
        {
            string scaleReason =
                scaleCorrectionApplied
                    ? "uniform seat-height normalization applied"
                    : "source seat height accepted";

            string rotationReason =
                Math.Abs(visualYawDegrees) <=
                    0.001f
                    ? "source front already aligned with canonical +Z"
                    : "visual yaw " +
                      visualYawDegrees.ToString(
                          "0.#",
                          CultureInfo.InvariantCulture) +
                      " degrees aligns detected front with canonical +Z";

            return
                scaleReason +
                "; " +
                rotationReason +
                "; final seat height " +
                finalSeatHeight.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                " m.";
        }

        private static bool IsFinitePositive(
            float value)
        {
            return
                IsFinite(value) &&
                value > 0f;
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
