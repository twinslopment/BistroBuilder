using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BistroBuilder.Editor.Savic
{
    internal enum SavicIncrementalAction
    {
        FullRebuild = 0,
        AppearanceOnly = 1,
        ReusePublished = 2
    }

    internal readonly struct SavicIncrementalPlan
    {
        internal SavicIncrementalPlan(
            SavicIncrementalAction action,
            string geometryFingerprint,
            string appearanceFingerprint,
            string reason)
        {
            Action = action;
            GeometryFingerprint = geometryFingerprint ?? string.Empty;
            AppearanceFingerprint = appearanceFingerprint ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        internal SavicIncrementalAction Action { get; }
        internal string GeometryFingerprint { get; }
        internal string AppearanceFingerprint { get; }
        internal string Reason { get; }

        internal bool GeometryReusable =>
            Action != SavicIncrementalAction.FullRebuild;

        internal bool IsAppearanceOnly =>
            Action == SavicIncrementalAction.AppearanceOnly;

        internal bool IsExactReuse =>
            Action == SavicIncrementalAction.ReusePublished;
    }

    internal static class SavicIncrementalInvalidationService
    {
        internal const string Version = "1.0.0";

        internal static SavicIncrementalPlan Evaluate(
            SavicManifest previousPublished,
            SavicManifest current,
            SavicModelAnalysisRecord currentAnalysis)
        {
            if (current?.source == null)
                throw new ArgumentNullException(nameof(current));

            if (currentAnalysis == null ||
                !currentAnalysis.analyzed)
            {
                throw new ArgumentException(
                    "Current analyzed model is required.",
                    nameof(currentAnalysis));
            }

            string currentGeometry =
                BuildGeometryFingerprint(
                    currentAnalysis,
                    current.source.originalFileName);

            string currentMaterialCore =
                BuildMaterialCoreFingerprint(
                    currentAnalysis);

            string currentAppearance =
                BuildAppearanceFingerprint(
                    currentMaterialCore,
                    current.source.sourceHash);

            if (previousPublished?.source == null ||
                previousPublished.model3D == null ||
                !previousPublished.model3D.analyzed ||
                !string.Equals(
                    previousPublished.status,
                    "PUBLISHED",
                    StringComparison.Ordinal))
            {
                return new SavicIncrementalPlan(
                    SavicIncrementalAction.FullRebuild,
                    currentGeometry,
                    currentAppearance,
                    "No compatible published baseline exists.");
            }

            if (!string.Equals(
                    previousPublished.source.originalFileName,
                    current.source.originalFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new SavicIncrementalPlan(
                    SavicIncrementalAction.FullRebuild,
                    currentGeometry,
                    currentAppearance,
                    "Source identity metadata changed; full rebuild is safer.");
            }

            string previousGeometry =
                BuildGeometryFingerprint(
                    previousPublished.model3D,
                    previousPublished.source.originalFileName);

            if (!string.Equals(
                    previousGeometry,
                    currentGeometry,
                    StringComparison.Ordinal))
            {
                return new SavicIncrementalPlan(
                    SavicIncrementalAction.FullRebuild,
                    currentGeometry,
                    currentAppearance,
                    "Structural geometry fingerprint changed.");
            }

            string previousMaterialCore =
                BuildMaterialCoreFingerprint(
                    previousPublished.model3D);

            string previousAppearance =
                BuildAppearanceFingerprint(
                    previousMaterialCore,
                    previousPublished.source.sourceHash);

            bool materialChanged =
                !string.Equals(
                    previousMaterialCore,
                    currentMaterialCore,
                    StringComparison.Ordinal);

            bool sourceChanged =
                !string.Equals(
                    previousPublished.source.sourceHash,
                    current.source.sourceHash,
                    StringComparison.OrdinalIgnoreCase);

            if (materialChanged)
            {
                return new SavicIncrementalPlan(
                    SavicIncrementalAction.AppearanceOnly,
                    currentGeometry,
                    currentAppearance,
                    "Structural geometry is unchanged while material/appearance data changed.");
            }

            if (sourceChanged)
            {
                // The source bytes changed but neither the structural nor
                // material fingerprints explain the change. Do not guess:
                // fall back to the full path to protect colliders/semantics.
                return new SavicIncrementalPlan(
                    SavicIncrementalAction.FullRebuild,
                    currentGeometry,
                    currentAppearance,
                    "Source bytes changed without a proven appearance-only delta.");
            }

            if (!string.Equals(
                    previousAppearance,
                    currentAppearance,
                    StringComparison.Ordinal))
            {
                return new SavicIncrementalPlan(
                    SavicIncrementalAction.FullRebuild,
                    currentGeometry,
                    currentAppearance,
                    "Appearance baseline is incompatible with the current evaluator.");
            }

            return new SavicIncrementalPlan(
                SavicIncrementalAction.ReusePublished,
                currentGeometry,
                currentAppearance,
                "Source, structural geometry and appearance fingerprints are unchanged.");
        }

        internal static SavicIncrementalStateRecord Stamp(
            SavicIncrementalStateRecord previous,
            SavicIncrementalPlan plan)
        {
            SavicIncrementalStateRecord state =
                new SavicIncrementalStateRecord
                {
                    evaluatorVersion = Version,
                    geometryFingerprint =
                        plan.GeometryFingerprint,
                    appearanceFingerprint =
                        plan.AppearanceFingerprint,
                    lastAction =
                        plan.Action.ToString(),
                    reason = plan.Reason,
                    reusedGeometry =
                        plan.GeometryReusable,
                    reusedSemanticParts =
                        plan.GeometryReusable,
                    reusedClassification =
                        plan.GeometryReusable,
                    reusedColliders =
                        plan.GeometryReusable,
                    appearanceOnlyRefresh =
                        plan.IsAppearanceOnly,
                    reuseCount =
                        previous?.reuseCount ?? 0,
                    fullRebuildCount =
                        previous?.fullRebuildCount ?? 0,
                    appearanceRefreshCount =
                        previous?.appearanceRefreshCount ?? 0,
                    evaluatedUtc =
                        DateTime.UtcNow.ToString("O")
                };

            switch (plan.Action)
            {
                case SavicIncrementalAction.ReusePublished:
                    state.reuseCount++;
                    break;

                case SavicIncrementalAction.AppearanceOnly:
                    state.appearanceRefreshCount++;
                    break;

                default:
                    state.fullRebuildCount++;
                    break;
            }

            return state;
        }

        internal static string BuildGeometryFingerprint(
            SavicModelAnalysisRecord analysis,
            string sourceName)
        {
            if (analysis == null)
                return string.Empty;

            StringBuilder builder =
                new StringBuilder(1024);

            Append(builder, "savic.incremental.geometry.v1");
            Append(builder, SavicModelAnalyzer.Version);
            Append(builder, SavicGeometryProfileAnalyzer.Version);
            Append(builder, SavicChairGeometryAnalyzer.Version);
            Append(builder, sourceName ?? string.Empty);
            Append(builder, analysis.hasUsableBounds);
            Append(builder, analysis.boundsCenterX);
            Append(builder, analysis.boundsCenterY);
            Append(builder, analysis.boundsCenterZ);
            Append(builder, analysis.widthMeters);
            Append(builder, analysis.heightMeters);
            Append(builder, analysis.depthMeters);
            Append(builder, analysis.rendererCount);
            Append(builder, analysis.meshInstanceCount);
            Append(builder, analysis.uniqueMeshCount);
            Append(builder, analysis.vertexCount);
            Append(builder, analysis.triangleCount);
            Append(builder, analysis.hasSkinnedMeshes);
            Append(builder, analysis.hasNegativeScale);

            SavicGeometryProfileRecord geometry =
                analysis.geometry;

            if (geometry != null)
            {
                Append(builder, geometry.analyzerVersion);
                Append(builder, geometry.usable);
                Append(builder, geometry.meshInstanceCount);
                Append(builder, geometry.sourceTriangleCount);
                Append(builder, geometry.sampledTriangleCount);
                Append(builder, geometry.invalidTriangleCount);
                Append(builder, geometry.degenerateTriangleCount);
                Append(builder, geometry.maximumSamplingStride);
                Append(builder, geometry.estimatedSurfaceAreaSquareMeters);
                Append(builder, geometry.upwardFacingAreaRatio);
                Append(builder, geometry.horizontalAreaRatio);
                Append(builder, geometry.verticalAreaRatio);
                Append(builder, geometry.upperBandAreaRatio);
                Append(builder, geometry.lowerBandAreaRatio);
                Append(builder, geometry.surfaceAreaCentroidHeight01);
                Append(builder, geometry.upperUpwardProjectedCoverage);
                Append(builder, geometry.lowerHorizontalProjectedCoverage);
            }

            SavicChairGeometryProfileRecord chair =
                analysis.chairGeometry;

            if (chair != null)
            {
                Append(builder, chair.analyzerVersion);
                Append(builder, chair.usable);
                Append(builder, chair.sourceTriangleCount);
                Append(builder, chair.sampledTriangleCount);
                Append(builder, chair.invalidTriangleCount);
                Append(builder, chair.degenerateTriangleCount);
                Append(builder, chair.maximumSamplingStride);
                Append(builder, chair.seatHeight01);
                Append(builder, chair.seatHeightMeters);
                Append(builder, chair.seatUpwardAreaRatio);
                Append(builder, chair.seatProjectedCoverage);
                Append(builder, chair.upperVerticalAreaRatio);
                Append(builder, chair.upperVerticalCentroidX01);
                Append(builder, chair.upperVerticalCentroidZ01);
                Append(builder, chair.backAxis);
                Append(builder, chair.backSide);
                Append(builder, chair.backEdgeBias);
                Append(builder, chair.frontDirectionLocalX);
                Append(builder, chair.frontDirectionLocalZ);
                Append(builder, chair.lowerSupportAreaRatio);
                Append(builder, chair.confidenceScore);
            }

            return SavicHashService.ComputeSha256Text(
                builder.ToString());
        }

        internal static string BuildMaterialCoreFingerprint(
            SavicModelAnalysisRecord analysis)
        {
            if (analysis == null)
                return string.Empty;

            StringBuilder builder =
                new StringBuilder(1024);

            Append(builder, "savic.incremental.appearance-core.v1");
            Append(builder, SavicMaterialSemanticAnalyzer.Version);
            Append(builder, SavicMaterialSemanticResolver.Version);
            Append(builder, analysis.materialSlotCount);
            Append(builder, analysis.uniqueMaterialCount);
            Append(builder, analysis.missingMaterialSlots);
            Append(builder, analysis.texturedMaterialCount);
            Append(builder, analysis.hasVertexColors);
            Append(builder, analysis.hasUv0);
            Append(builder, analysis.hasNonDefaultBaseColor);
            Append(builder, analysis.appearanceDataCompleteness);
            Append(builder, analysis.dominantMaterialSemantic);
            Append(builder, analysis.dominantMaterialConfidence);

            IEnumerable<string> materialRows =
                (analysis.materials ??
                 new List<SavicMaterialAnalysisRecord>())
                .Where(material => material != null)
                .Select(BuildMaterialRow)
                .OrderBy(
                    row => row,
                    StringComparer.Ordinal);

            foreach (string row in materialRows)
                Append(builder, row);

            return SavicHashService.ComputeSha256Text(
                builder.ToString());
        }

        private static string BuildAppearanceFingerprint(
            string materialCoreFingerprint,
            string sourceHash)
        {
            return SavicHashService.ComputeSha256Text(
                string.Join(
                    "|",
                    "savic.incremental.appearance.v1",
                    materialCoreFingerprint ?? string.Empty,
                    sourceHash ?? string.Empty));
        }

        private static string BuildMaterialRow(
            SavicMaterialAnalysisRecord material)
        {
            return string.Join(
                "~",
                material.materialName ?? string.Empty,
                material.shaderName ?? string.Empty,
                material.semantic ?? string.Empty,
                material.semanticConfidence ?? string.Empty,
                F(material.semanticScore),
                F(material.metallic),
                F(material.smoothness),
                F(material.baseColorR),
                F(material.baseColorG),
                F(material.baseColorB),
                F(material.baseColorA),
                material.transparent ? "1" : "0",
                material.textureCount.ToString(
                    CultureInfo.InvariantCulture),
                material.textureNames ?? string.Empty);
        }

        private static void Append(
            StringBuilder builder,
            object value)
        {
            builder.Append(
                value switch
                {
                    null => string.Empty,
                    float number => F(number),
                    double number => number.ToString(
                        "R",
                        CultureInfo.InvariantCulture),
                    IFormattable formattable =>
                        formattable.ToString(
                            null,
                            CultureInfo.InvariantCulture),
                    _ => value.ToString()
                });

            builder.Append('|');
        }

        private static string F(float value)
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture);
        }
    }
}
