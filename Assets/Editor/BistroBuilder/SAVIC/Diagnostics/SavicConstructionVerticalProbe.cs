using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicConstructionVerticalProbe
    {
        private const string ConstructionKitPath =
            "Assets/Resources/BistroBuilder/Construction/ConstructionAssetKit.asset";

        private const string WallSourcePath =
            "Assets/Art/Architecture/Walls/Source/BB_Wall_Module_Master_001.glb";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Construction Vertical Probe",
            false,
            132)]
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
            SavicStorageLayout layout =
                SavicStorageLayout.ForCurrentProject();
            layout.EnsureInfrastructure();

            BistroBuilderConstructionAssetKit kit =
                AssetDatabase.LoadAssetAtPath<BistroBuilderConstructionAssetKit>(
                    ConstructionKitPath);

            Require(kit != null, "Canonical ConstructionAssetKit is missing.");
            Require(kit.doorPrefab != null, "Default construction door prefab is missing.");
            Require(kit.windowPrefab != null, "Default construction window prefab is missing.");

            GameObject wallSource =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    WallSourcePath);

            Require(wallSource != null, "Canonical wall source GLB is missing.");

            string runId =
                Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 12);

            string diagnosticManifestRoot =
                Path.Combine(
                    layout.RuntimeRoot,
                    "Diagnostics",
                    "ConstructionVertical",
                    runId,
                    "Manifests");

            SavicManifestRepository manifests =
                new SavicManifestRepository(
                    layout,
                    diagnosticManifestRoot);

            SavicManifest wallManifest =
                CreateManifest(
                    "construction_probe_wall_" + runId,
                    "BB_Wall_Module_Master_001.glb",
                    0.49141f,
                    1.89958f,
                    0.03436f);

            SavicManifest doorManifest =
                CreateManifest(
                    "construction_probe_door_" + runId,
                    "restaurant_door.fbx",
                    0.90f,
                    2.10f,
                    0.12f);

            SavicManifest windowManifest =
                CreateManifest(
                    "construction_probe_window_" + runId,
                    "restaurant_window.fbx",
                    1.20f,
                    1.20f,
                    0.12f);

            ValidateClassification(
                wallManifest,
                "Wall");
            ValidateClassification(
                doorManifest,
                "Door");
            ValidateClassification(
                windowManifest,
                "Window");

            ValidateNegativeClassificationContracts();

            Require(
                SavicConstructionAuthoringPlanner.TryPlan(
                    wallManifest,
                    out SavicConstructionAuthoringRecord wallPlan,
                    out _,
                    out string wallPlanError),
                wallPlanError);

            Require(
                SavicConstructionAuthoringPlanner.TryPlan(
                    doorManifest,
                    out SavicConstructionAuthoringRecord doorPlan,
                    out _,
                    out string doorPlanError),
                doorPlanError);

            Require(
                SavicConstructionAuthoringPlanner.TryPlan(
                    windowManifest,
                    out SavicConstructionAuthoringRecord windowPlan,
                    out _,
                    out string windowPlanError),
                windowPlanError);

            wallManifest.construction = wallPlan;
            doorManifest.construction = doorPlan;
            windowManifest.construction = windowPlan;

            using SavicAssetMutationScope rollback =
                new SavicAssetMutationScope(
                    layout,
                    "construction_vertical_probe");

            rollback.CaptureAsset(ConstructionKitPath);
            rollback.CaptureAsset(wallPlan.prefabAssetPath);
            rollback.CaptureAsset(doorPlan.prefabAssetPath);
            rollback.CaptureAsset(windowPlan.prefabAssetPath);

            GameObject host = null;
            GameObject openingHost = null;

            try
            {
                SavicConstructionFamilyModule wallModule =
                    new SavicConstructionFamilyModule(
                        "Wall",
                        layout,
                        manifests);

                SavicConstructionFamilyModule doorModule =
                    new SavicConstructionFamilyModule(
                        "Door",
                        layout,
                        manifests);

                SavicConstructionFamilyModule windowModule =
                    new SavicConstructionFamilyModule(
                        "Window",
                        layout,
                        manifests);

                Require(
                    wallModule.Process(
                        wallManifest,
                        wallSource).Succeeded,
                    "Wall construction publication failed.");

                Require(
                    doorModule.Process(
                        doorManifest,
                        kit.doorPrefab).Succeeded,
                    "Door construction publication failed.");

                Require(
                    windowModule.Process(
                        windowManifest,
                        kit.windowPrefab).Succeeded,
                    "Window construction publication failed.");

                ValidatePublishedReadiness(
                    wallManifest,
                    SavicConstructionAuthoringPlanner.WallRole);
                ValidatePublishedReadiness(
                    doorManifest,
                    SavicConstructionAuthoringPlanner.DoorRole);
                ValidatePublishedReadiness(
                    windowManifest,
                    SavicConstructionAuthoringPlanner.WindowRole);

                ValidateRegistry(
                    kit,
                    wallManifest,
                    doorManifest,
                    windowManifest);

                host =
                    new GameObject(
                        "SAVIC_ConstructionVertical_WallHost");

                BistroBuilderArchitectureRuntimeMaterializer materializer =
                    host.AddComponent<BistroBuilderArchitectureRuntimeMaterializer>();

                BistroBuilderEditDocument document =
                    new BistroBuilderEditDocument();

                BistroBuilderWallRecord wall =
                    new BistroBuilderWallRecord
                    {
                        wallId = BistroBuilderEditId.NewId(),
                        buildPlaneId = "default",
                        axisStart = Vector2.zero,
                        axisEnd = new Vector2(2.0f, 0f),
                        baseElevation = 0f,
                        height = 2.5f,
                        thickness = 0.12f,
                        wallDefinitionId =
                            wallManifest.canonicalContentId
                    };

                document.walls.Add(wall);

                BistroBuilderArchitectureMaterializationSummary summary =
                    materializer.Rebuild(document);

                Require(
                    summary.wallObjects == 1,
                    "Construction materializer did not create the SAVIC wall.");
                Require(
                    materializer.HasWallVisualModule,
                    "Construction materializer did not resolve the SAVIC wall definition.");

                Transform generated =
                    host.transform.Find(
                        "BB18_GeneratedArchitecture");

                Require(
                    generated != null &&
                    generated.childCount == 1,
                    "Generated SAVIC wall hierarchy is invalid.");

                Renderer[] wallRenderers =
                    generated.GetChild(0)
                        .GetComponentsInChildren<Renderer>(true);

                Require(
                    Array.Exists(
                        wallRenderers,
                        renderer =>
                            renderer != null &&
                            renderer.gameObject.name.StartsWith(
                                "WallVisualModule_",
                                StringComparison.Ordinal)),
                    "SAVIC wall visual module was not tiled by the canonical materializer.");

                openingHost =
                    new GameObject(
                        "SAVIC_ConstructionVertical_OpeningHost");

                ValidateOpeningRuntime(
                    openingHost.transform,
                    wall,
                    doorManifest,
                    "door",
                    0f,
                    2.10f);

                ValidateOpeningRuntime(
                    openingHost.transform,
                    wall,
                    windowManifest,
                    "window",
                    0.85f,
                    1.20f);

                Debug.Log(
                    "[SAVIC] CONSTRUCTION VERTICAL PROBE - PASS\n" +
                    "Wall classification/publication/registry/materialization: PASS\n" +
                    "Door classification/publication/registry/opening fill: PASS\n" +
                    "Window classification/publication/registry/opening fill: PASS\n" +
                    "Construction definition isolation: PASS\n" +
                    "Opening collider authority: PASS");
            }
            finally
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);

                if (openingHost != null)
                    UnityEngine.Object.DestroyImmediate(openingHost);

                CleanupGeneratedFolder(wallPlan.prefabAssetPath);
                CleanupGeneratedFolder(doorPlan.prefabAssetPath);
                CleanupGeneratedFolder(windowPlan.prefabAssetPath);

                try
                {
                    string diagnosticRoot =
                        Directory.GetParent(
                            diagnosticManifestRoot)
                        ?.FullName;

                    if (!string.IsNullOrWhiteSpace(diagnosticRoot) &&
                        Directory.Exists(diagnosticRoot))
                    {
                        Directory.Delete(
                            diagnosticRoot,
                            true);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[SAVIC] Construction probe diagnostic cleanup warning: " +
                        exception.Message);
                }
            }
        }

        private static SavicManifest CreateManifest(
            string savicId,
            string sourceName,
            float width,
            float height,
            float depth)
        {
            SavicManifest manifest =
                new SavicManifest
                {
                    savicId = savicId,
                    source =
                        new SavicSourceRecord
                        {
                            sourceHash =
                                "construction-probe-" +
                                savicId,
                            originalFileName = sourceName,
                            sourceKind =
                                SavicSourceKind.Model3D.ToString()
                        },
                    model3D =
                        new SavicModelAnalysisRecord
                        {
                            analyzed = true,
                            analyzerVersion = "construction-probe",
                            hasUsableBounds = true,
                            widthMeters = width,
                            heightMeters = height,
                            depthMeters = depth,
                            rendererCount = 1,
                            meshInstanceCount = 1,
                            uniqueMeshCount = 1,
                            vertexCount = 4,
                            triangleCount = 2,
                            appearanceDataCompleteness = "PARTIAL"
                        }
                };

            manifest.classification =
                SavicContentClassifier.Classify(
                    manifest);

            manifest.family =
                manifest.classification.family;
            manifest.type =
                manifest.classification.type;
            manifest.category =
                manifest.classification.category;

            return manifest;
        }

        private static void ValidateClassification(
            SavicManifest manifest,
            string expectedType)
        {
            Require(
                manifest.classification != null &&
                string.Equals(
                    manifest.classification.family,
                    "Architecture",
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.classification.type,
                    expectedType,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.classification.category,
                    "Construction",
                    StringComparison.Ordinal),
                "Construction classifier did not resolve " +
                expectedType +
                " correctly.");
        }

        private static void ValidateNegativeClassificationContracts()
        {
            SavicManifest cabinetDoor =
                CreateManifest(
                    "construction_probe_negative_cabinet",
                    "kitchen_cabinet_door.fbx",
                    0.6f,
                    0.8f,
                    0.04f);

            Require(
                !string.Equals(
                    cabinetDoor.classification?.type,
                    "Door",
                    StringComparison.Ordinal),
                "Cabinet door was incorrectly classified as an architectural Door.");

            SavicManifest wallMirror =
                CreateManifest(
                    "construction_probe_negative_mirror",
                    "wall_mirror.glb",
                    0.8f,
                    1.8f,
                    0.05f);

            Require(
                !string.Equals(
                    wallMirror.classification?.type,
                    "Wall",
                    StringComparison.Ordinal),
                "Wall mirror was incorrectly classified as an architectural Wall.");
        }

        private static void ValidatePublishedReadiness(
            SavicManifest manifest,
            string expectedRole)
        {
            Require(
                string.Equals(
                    manifest.status,
                    "PUBLISHED",
                    StringComparison.Ordinal),
                "Construction asset was not published.");

            Require(
                manifest.construction != null &&
                manifest.construction.planned &&
                string.Equals(
                    manifest.construction.role,
                    expectedRole,
                    StringComparison.Ordinal),
                "Construction authoring plan is incomplete.");

            Require(
                manifest.constructionReadiness != null &&
                manifest.constructionReadiness.validated &&
                manifest.constructionReadiness.prefabResolvable &&
                manifest.constructionReadiness.constructionKitResolvable &&
                manifest.constructionReadiness.definitionResolvable,
                "Construction readiness did not validate.");
        }

        private static void ValidateRegistry(
            BistroBuilderConstructionAssetKit kit,
            SavicManifest wall,
            SavicManifest door,
            SavicManifest window)
        {
            Require(
                kit.TryResolveWallVisual(
                    wall.canonicalContentId,
                    out GameObject wallPrefab,
                    out _) &&
                wallPrefab != null,
                "SAVIC wall definition is not resolvable from ConstructionAssetKit.");

            Require(
                kit.TryResolveOpening(
                    "door",
                    door.canonicalContentId,
                    out GameObject doorPrefab,
                    out _) &&
                doorPrefab != null,
                "SAVIC door definition is not resolvable from ConstructionAssetKit.");

            Require(
                kit.TryResolveOpening(
                    "window",
                    window.canonicalContentId,
                    out GameObject windowPrefab,
                    out _) &&
                windowPrefab != null,
                "SAVIC window definition is not resolvable from ConstructionAssetKit.");
        }

        private static void ValidateOpeningRuntime(
            Transform parent,
            BistroBuilderWallRecord wall,
            SavicManifest manifest,
            string openingType,
            float bottomElevation,
            float height)
        {
            int childCountBefore =
                parent.childCount;

            BistroBuilderOpeningRecord opening =
                new BistroBuilderOpeningRecord
                {
                    openingId = BistroBuilderEditId.NewId(),
                    hostWallId = wall.wallId,
                    axisPosition01 = 0.5f,
                    width =
                        manifest.construction.nominalWidthMeters,
                    bottomElevation = bottomElevation,
                    height = height,
                    openingType = openingType,
                    fillDefinitionId =
                        manifest.canonicalContentId
                };

            BistroBuilderOpeningVisuals.Build(
                parent,
                wall,
                new List<BistroBuilderOpeningRecord>
                {
                    opening
                },
                null);

            Require(
                parent.childCount == childCountBefore + 1,
                "Construction opening did not instantiate exactly one registered fill.");

            Transform fill =
                parent.GetChild(
                    parent.childCount - 1);

            Require(
                fill != null &&
                string.Equals(
                    fill.name.Replace("(Clone)", string.Empty).Trim(),
                    manifest.canonicalContentId,
                    StringComparison.Ordinal),
                "Construction opening resolved the wrong definition prefab.");

            Require(
                fill.GetComponentsInChildren<Collider>(true).Length == 0,
                "Construction opening prefab introduced collider authority into the passage.");
        }

        private static void CleanupGeneratedFolder(
            string prefabAssetPath)
        {
            string folder =
                Path.GetDirectoryName(
                    prefabAssetPath)
                    ?.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(folder))
                return;

            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);
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
