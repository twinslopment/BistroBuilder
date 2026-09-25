using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicIncrementalInvalidationSelfTest
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Incremental Invalidation Self-Test",
            false,
            129)]
        public static void RunFromMenu()
        {
            RunOrThrow();
        }

        public static void RunFromCommandLine()
        {
            RunOrThrow();
        }

        private static void RunOrThrow()
        {
            SavicManifest previous =
                CreatePublishedBaseline(
                    new string('a', 64));

            SavicManifest exact =
                Clone(previous);

            SavicIncrementalPlan exactPlan =
                SavicIncrementalInvalidationService.Evaluate(
                    previous,
                    exact,
                    exact.model3D);

            Require(
                exactPlan.Action ==
                SavicIncrementalAction.ReusePublished,
                "Exact input should reuse the published baseline.");

            SavicManifest materialOnly =
                Clone(previous);

            materialOnly.source.sourceHash =
                new string('b', 64);

            materialOnly.model3D.materials[0].baseColorR =
                0.42f;

            materialOnly.model3D.hasNonDefaultBaseColor =
                true;

            materialOnly.model3D.appearanceDataCompleteness =
                "FLAT_COLOR";

            SavicIncrementalPlan materialPlan =
                SavicIncrementalInvalidationService.Evaluate(
                    previous,
                    materialOnly,
                    materialOnly.model3D);

            Require(
                materialPlan.Action ==
                SavicIncrementalAction.AppearanceOnly,
                "Material-only delta should not rebuild geometry/colliders.");

            SavicManifest geometryChange =
                Clone(previous);

            geometryChange.source.sourceHash =
                new string('c', 64);

            geometryChange.model3D.widthMeters +=
                0.08f;

            SavicIncrementalPlan geometryPlan =
                SavicIncrementalInvalidationService.Evaluate(
                    previous,
                    geometryChange,
                    geometryChange.model3D);

            Require(
                geometryPlan.Action ==
                SavicIncrementalAction.FullRebuild,
                "Structural geometry delta must force a full rebuild.");

            SavicManifest ambiguousChange =
                Clone(previous);

            ambiguousChange.source.sourceHash =
                new string('d', 64);

            SavicIncrementalPlan ambiguousPlan =
                SavicIncrementalInvalidationService.Evaluate(
                    previous,
                    ambiguousChange,
                    ambiguousChange.model3D);

            Require(
                ambiguousPlan.Action ==
                SavicIncrementalAction.FullRebuild,
                "Unexplained source-byte delta must fail safe to full rebuild.");

            SavicIncrementalStateRecord stamped =
                SavicIncrementalInvalidationService.Stamp(
                    previous.incremental,
                    materialPlan);

            Require(
                stamped.appearanceOnlyRefresh &&
                stamped.reusedGeometry &&
                stamped.reusedSemanticParts &&
                stamped.reusedColliders,
                "Appearance-only stamp did not preserve structural reuse.");

            Require(
                SavicBatchProcessor.ComputeCooldownSeconds(16) == 0d,
                "Within-budget operation should not throttle the queue.");

            double cooldown =
                SavicBatchProcessor.ComputeCooldownSeconds(2500);

            Require(
                cooldown > 0d &&
                cooldown <= 0.25d,
                "Slow operation cooldown is outside the safety budget.");

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            string expected =
                SavicIncrementalInvalidationService
                    .BuildGeometryFingerprint(
                        previous.model3D,
                        previous.source.originalFileName);

            for (int index = 0;
                 index < 2000;
                 index++)
            {
                string fingerprint =
                    SavicIncrementalInvalidationService
                        .BuildGeometryFingerprint(
                            previous.model3D,
                            previous.source.originalFileName);

                Require(
                    string.Equals(
                        expected,
                        fingerprint,
                        StringComparison.Ordinal),
                    "Geometry fingerprint is not deterministic.");
            }

            stopwatch.Stop();

            Require(
                stopwatch.ElapsedMilliseconds < 5000,
                "2,000 incremental fingerprint evaluations exceeded 5 seconds.");

            Debug.Log(
                "[SAVIC] INCREMENTAL INVALIDATION SELF-TEST - PASS\n" +
                "Exact reuse: PASS\n" +
                "Material-only refresh: PASS\n" +
                "Geometry invalidation: PASS\n" +
                "Ambiguous delta fail-safe: PASS\n" +
                "Collider/semantic reuse flags: PASS\n" +
                "Editor tick budget throttle: PASS\n" +
                "2000 deterministic fingerprints: " +
                stopwatch.ElapsedMilliseconds +
                " ms");
        }

        private static SavicManifest CreatePublishedBaseline(
            string sourceHash)
        {
            SavicModelAnalysisRecord analysis =
                new SavicModelAnalysisRecord
                {
                    analyzed = true,
                    analyzerVersion =
                        SavicModelAnalyzer.Version,
                    hasUsableBounds = true,
                    boundsCenterX = 0f,
                    boundsCenterY = 0.4f,
                    boundsCenterZ = 0f,
                    widthMeters = 0.8f,
                    heightMeters = 0.8f,
                    depthMeters = 0.8f,
                    rendererCount = 1,
                    meshInstanceCount = 1,
                    uniqueMeshCount = 1,
                    vertexCount = 240,
                    triangleCount = 400,
                    materialSlotCount = 1,
                    uniqueMaterialCount = 1,
                    missingMaterialSlots = 0,
                    texturedMaterialCount = 0,
                    hasVertexColors = false,
                    hasUv0 = true,
                    hasNonDefaultBaseColor = false,
                    appearanceDataCompleteness = "NONE",
                    hasSkinnedMeshes = false,
                    hasNegativeScale = false,
                    dominantMaterialSemantic = "Wood",
                    dominantMaterialConfidence = "HIGH",
                    geometry =
                        new SavicGeometryProfileRecord
                        {
                            analyzed = true,
                            analyzerVersion =
                                SavicGeometryProfileAnalyzer.Version,
                            usable = true,
                            meshInstanceCount = 1,
                            sourceTriangleCount = 400,
                            sampledTriangleCount = 400,
                            maximumSamplingStride = 1,
                            estimatedSurfaceAreaSquareMeters = 2.4f,
                            upwardFacingAreaRatio = 0.28f,
                            horizontalAreaRatio = 0.42f,
                            verticalAreaRatio = 0.58f,
                            upperBandAreaRatio = 0.31f,
                            lowerBandAreaRatio = 0.29f,
                            surfaceAreaCentroidHeight01 = 0.5f,
                            upperUpwardProjectedCoverage = 0.72f,
                            lowerHorizontalProjectedCoverage = 0.22f
                        },
                    chairGeometry =
                        new SavicChairGeometryProfileRecord
                        {
                            analyzed = true,
                            analyzerVersion =
                                SavicChairGeometryAnalyzer.Version,
                            usable = false,
                            sourceTriangleCount = 400,
                            sampledTriangleCount = 400,
                            maximumSamplingStride = 1
                        },
                    materials =
                        new List<SavicMaterialAnalysisRecord>
                        {
                            new SavicMaterialAnalysisRecord
                            {
                                materialName = "Wood",
                                shaderName = "Universal Render Pipeline/Lit",
                                semantic = "Wood",
                                semanticConfidence = "HIGH",
                                semanticScore = 0.94f,
                                metallic = 0f,
                                smoothness = 0.25f,
                                baseColorR = 1f,
                                baseColorG = 1f,
                                baseColorB = 1f,
                                baseColorA = 1f,
                                transparent = false,
                                textureCount = 0,
                                textureNames = string.Empty
                            }
                        }
                };

            SavicManifest manifest =
                new SavicManifest
                {
                    savicId = "incremental-self-test",
                    canonicalContentId =
                        "incremental_self_test",
                    status = "PUBLISHED",
                    source =
                        new SavicSourceRecord
                        {
                            sourceHash = sourceHash,
                            originalFileName =
                                "table_incremental.glb",
                            extension = ".glb",
                            sourceKind =
                                SavicSourceKind.Model3D
                                    .ToString()
                        },
                    model3D = analysis,
                    classification =
                        new SavicClassificationRecord
                        {
                            classified = true,
                            classifierVersion =
                                SavicContentClassifier.Version,
                            family = "Furniture",
                            type = "Table",
                            category = "Furniture",
                            confidence = "HIGH"
                        },
                    incremental =
                        new SavicIncrementalStateRecord()
                };

            return manifest;
        }

        private static SavicManifest Clone(
            SavicManifest manifest)
        {
            string json =
                JsonUtility.ToJson(
                    manifest,
                    false);

            return JsonUtility.FromJson
                <SavicManifest>(
                    json);
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
