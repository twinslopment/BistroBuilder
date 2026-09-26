using System;
using System.Globalization;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicChairAuthoringPlanner
    {
        internal const string Version = "3.0.0";

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

        private const float CanonicalChairWidthMeters = 0.50f;
        private const float CanonicalChairHeightMeters = 0.86f;
        private const float CanonicalChairDepthMeters = 0.55f;

        private static readonly float[] CommonUnitScales =
        {
            0.001f,
            0.01f,
            0.1f,
            1f,
            10f,
            100f,
            1000f
        };

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

            SavicModelAnalysisRecord analysis =
                manifest.model3D;

            if (analysis == null ||
                !analysis.analyzed ||
                !analysis.hasUsableBounds)
            {
                rejectionReason =
                    "Chair authoring requires analyzed model bounds.";
                return false;
            }

            SavicChairGeometryProfileRecord geometry =
                analysis.chairGeometry;

            if (geometry == null ||
                !geometry.analyzed)
            {
                rejectionReason =
                    "Chair functional geometry analysis did not complete.";
                return false;
            }

            if (!geometry.orientationResolved)
            {
                rejectionReason =
                    "Chair front/back orientation is unresolved. " +
                    geometry.evidence;
                return false;
            }

            if (!geometry.supportResolved)
            {
                rejectionReason =
                    "Chair lower-support geometry is unresolved. " +
                    geometry.evidence;
                return false;
            }

            if (!geometry.usable)
            {
                rejectionReason =
                    "Chair functional geometry gates are inconsistent. " +
                    geometry.evidence;
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

            float sourceFrontX =
                geometry.frontDirectionLocalX;

            float sourceFrontZ =
                geometry.frontDirectionLocalZ;

            if (!TryResolveCanonicalYaw(
                    sourceFrontX,
                    sourceFrontZ,
                    out float visualYawDegrees))
            {
                rejectionReason =
                    "Chair front direction is ambiguous and cannot be normalized safely.";
                return false;
            }

            bool quarterTurn =
                ApproximatelyQuarterTurn(
                    visualYawDegrees);

            float orientedSourceWidth =
                quarterTurn
                    ? analysis.depthMeters
                    : analysis.widthMeters;

            float orientedSourceDepth =
                quarterTurn
                    ? analysis.widthMeters
                    : analysis.depthMeters;

            if (!TryResolveAuthoringScale(
                    orientedSourceWidth,
                    analysis.heightMeters,
                    orientedSourceDepth,
                    geometry,
                    out float uniformScale,
                    out bool scaleCorrectionApplied,
                    out bool unitNormalizationApplied,
                    out string unitNormalizationMode,
                    out bool canonicalSeatFallback,
                    out string scaleReason))
            {
                rejectionReason =
                    scaleReason;
                return false;
            }

            float finalWidth =
                orientedSourceWidth *
                uniformScale;

            float finalHeight =
                analysis.heightMeters *
                uniformScale;

            float finalDepth =
                orientedSourceDepth *
                uniformScale;

            float finalSeatHeight =
                canonicalSeatFallback
                    ? NominalSeatHeightMeters
                    : geometry.seatHeightMeters *
                      uniformScale;

            if (!HasSafePublishedDimensions(
                    finalWidth,
                    finalHeight,
                    finalDepth,
                    finalSeatHeight))
            {
                rejectionReason =
                    "Chair unit/scale normalization produced unsafe dining-chair dimensions: " +
                    "source=" +
                    FormatDimensions(
                        orientedSourceWidth,
                        analysis.heightMeters,
                        orientedSourceDepth) +
                    ", scale=" +
                    uniformScale.ToString(
                        "0.######",
                        CultureInfo.InvariantCulture) +
                    ", final=" +
                    FormatDimensions(
                        finalWidth,
                        finalHeight,
                        finalDepth) +
                    ", seat=" +
                    finalSeatHeight.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    " m.";
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
                        finalHeight,
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
                    unitNormalizationApplied =
                        unitNormalizationApplied,
                    unitNormalizationMode =
                        unitNormalizationMode,
                    sourceWidthMeters =
                        orientedSourceWidth,
                    sourceHeightMeters =
                        analysis.heightMeters,
                    sourceDepthMeters =
                        orientedSourceDepth,
                    templatePrefabAssetPath =
                        TemplatePrefabAssetPath,
                    seatUseProfileAssetPath =
                        SeatUseProfileAssetPath,
                    editableDefinitionAssetPath =
                        EditableDefinitionAssetPath,
                    planReason =
                        BuildPlanReason(
                            scaleCorrectionApplied,
                            unitNormalizationApplied,
                            unitNormalizationMode,
                            canonicalSeatFallback,
                            visualYawDegrees,
                            uniformScale,
                            finalWidth,
                            finalHeight,
                            finalDepth,
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
            bool unitNormalizationApplied,
            string unitNormalizationMode,
            bool canonicalSeatFallback,
            float visualYawDegrees,
            float uniformScale,
            float finalWidth,
            float finalHeight,
            float finalDepth,
            float finalSeatHeight)
        {
            string scaleReason =
                unitNormalizationApplied
                    ? "source units normalized with " +
                      unitNormalizationMode +
                      " scale " +
                      uniformScale.ToString(
                          "0.######",
                          CultureInfo.InvariantCulture)
                    : canonicalSeatFallback
                        ? "canonical dining-chair seat height used because source seat surface was not reliable"
                        : scaleCorrectionApplied
                            ? "moderate seat-height normalization applied"
                            : "source physical scale accepted";

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
                "; final dimensions " +
                FormatDimensions(
                    finalWidth,
                    finalHeight,
                    finalDepth) +
                "; final seat height " +
                finalSeatHeight.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                " m.";
        }

        private static bool TryResolveAuthoringScale(
            float sourceWidth,
            float sourceHeight,
            float sourceDepth,
            SavicChairGeometryProfileRecord geometry,
            out float scale,
            out bool scaleCorrectionApplied,
            out bool unitNormalizationApplied,
            out string unitNormalizationMode,
            out bool canonicalSeatFallback,
            out string rejectionReason)
        {
            scale = 1f;
            scaleCorrectionApplied = false;
            unitNormalizationApplied = false;
            unitNormalizationMode = "NONE";
            canonicalSeatFallback =
                geometry == null ||
                !geometry.seatResolved ||
                !IsFinitePositive(
                    geometry.seatHeightMeters);
            rejectionReason = string.Empty;

            if (!IsFinitePositive(sourceWidth) ||
                !IsFinitePositive(sourceHeight) ||
                !IsFinitePositive(sourceDepth))
            {
                rejectionReason =
                    "Chair source dimensions are missing or invalid.";
                return false;
            }

            float minimumScale =
                Math.Max(
                    0.000001f,
                    Math.Max(
                        0.32f / sourceWidth,
                        Math.Max(
                            0.62f / sourceHeight,
                            0.34f / sourceDepth)));

            float maximumScale =
                Math.Min(
                    1000000f,
                    Math.Min(
                        0.95f / sourceWidth,
                        Math.Min(
                            1.25f / sourceHeight,
                            0.95f / sourceDepth)));

            if (minimumScale >
                maximumScale)
            {
                rejectionReason =
                    "Chair proportions cannot fit the safe dining-chair envelope with any uniform scale. Source dimensions " +
                    FormatDimensions(
                        sourceWidth,
                        sourceHeight,
                        sourceDepth) +
                    ".";
                return false;
            }

            bool sourceScaleAlreadySafe =
                1f >= minimumScale &&
                1f <= maximumScale;

            if (sourceScaleAlreadySafe)
            {
                if (!canonicalSeatFallback)
                {
                    float sourceSeatHeight =
                        geometry.seatHeightMeters;

                    if (sourceSeatHeight <
                            MinimumAcceptedSeatHeightMeters ||
                        sourceSeatHeight >
                            MaximumAcceptedSeatHeightMeters)
                    {
                        float seatScale =
                            NominalSeatHeightMeters /
                            sourceSeatHeight;

                        if (seatScale >=
                                MinimumSafeUniformScale &&
                            seatScale <=
                                MaximumSafeUniformScale &&
                            seatScale >=
                                minimumScale &&
                            seatScale <=
                                maximumScale)
                        {
                            scale =
                                seatScale;

                            scaleCorrectionApplied =
                                true;
                        }
                        else
                        {
                            canonicalSeatFallback =
                                true;
                        }
                    }
                }

                return true;
            }

            float preferredScale =
                Median3(
                    CanonicalChairWidthMeters /
                    sourceWidth,
                    CanonicalChairHeightMeters /
                    sourceHeight,
                    CanonicalChairDepthMeters /
                    sourceDepth);

            float bestUnitScale =
                0f;

            float bestError =
                float.PositiveInfinity;

            for (int index = 0;
                 index < CommonUnitScales.Length;
                 index++)
            {
                float candidate =
                    CommonUnitScales[index];

                if (candidate <
                        minimumScale ||
                    candidate >
                        maximumScale)
                {
                    continue;
                }

                float error =
                    Math.Abs(
                        Mathf.Log10(
                            candidate /
                            Math.Max(
                                0.000001f,
                                preferredScale)));

                if (error <
                    bestError)
                {
                    bestError =
                        error;

                    bestUnitScale =
                        candidate;
                }
            }

            if (bestUnitScale > 0f)
            {
                scale =
                    bestUnitScale;

                unitNormalizationApplied =
                    true;

                unitNormalizationMode =
                    "COMMON_UNIT_FACTOR";

                canonicalSeatFallback =
                    true;

                return true;
            }

            float clampedPreferred =
                Mathf.Clamp(
                    preferredScale,
                    minimumScale,
                    maximumScale);

            if (clampedPreferred >=
                    MinimumSafeUniformScale &&
                clampedPreferred <=
                    MaximumSafeUniformScale)
            {
                scale =
                    clampedPreferred;

                scaleCorrectionApplied =
                    true;

                canonicalSeatFallback =
                    true;

                return true;
            }

            rejectionReason =
                "Chair source scale is outside the safe authoring range and does not match a supported common unit conversion. Source dimensions " +
                FormatDimensions(
                    sourceWidth,
                    sourceHeight,
                    sourceDepth) +
                "; feasible uniform scale " +
                minimumScale.ToString(
                    "0.######",
                    CultureInfo.InvariantCulture) +
                "–" +
                maximumScale.ToString(
                    "0.######",
                    CultureInfo.InvariantCulture) +
                ".";

            return false;
        }

        private static float Median3(
            float a,
            float b,
            float c)
        {
            return
                a > b
                    ? b > c
                        ? b
                        : a > c
                            ? c
                            : a
                    : a > c
                        ? a
                        : b > c
                            ? c
                            : b;
        }

        private static string FormatDimensions(
            float width,
            float height,
            float depth)
        {
            return
                width.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                " x " +
                height.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                " x " +
                depth.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                " m";
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
