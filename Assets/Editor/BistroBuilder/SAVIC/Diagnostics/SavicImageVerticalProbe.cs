using System;
using System.Collections.Generic;
using System.IO;
using BistroBuilder.Editor.UI.Iconography;
using BistroBuilder.UI.Iconography;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicImageVerticalProbe
    {
        private const string IconCatalogPath =
            "Assets/Resources/BistroBuilder/UI/BBIconCatalog.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Image Vertical Probe",
            false,
            133)]
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
            SavicStorageLayout currentLayout =
                SavicStorageLayout.ForCurrentProject();

            currentLayout.EnsureInfrastructure();

            string runId =
                Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 12);

            string diagnosticRoot =
                Path.Combine(
                    currentLayout.RuntimeRoot,
                    "Diagnostics",
                    "ImageVertical",
                    runId);

            SavicStorageLayout queueLayout =
                new SavicStorageLayout(
                    diagnosticRoot);

            queueLayout.EnsureInfrastructure();

            string manifestRoot =
                Path.Combine(
                    diagnosticRoot,
                    "Manifests");

            SavicManifestRepository manifests =
                new SavicManifestRepository(
                    currentLayout,
                    manifestRoot);

            SavicJobStore jobs =
                new SavicJobStore(
                    queueLayout);

            SavicIntakeService intake =
                new SavicIntakeService(
                    currentLayout,
                    manifests,
                    jobs);

            SavicSourceProcessingService processing =
                new SavicSourceProcessingService(
                    currentLayout,
                    manifests);

            string contentName =
                "content_photo__" +
                runId +
                ".png";

            string normalName =
                "mat_brass_normal__" +
                runId +
                ".png";

            string uiName =
                "ui_StatusCorrect__" +
                runId +
                ".png";

            List<ProbeSource> sources =
                new List<ProbeSource>();

            using SavicAssetMutationScope rollback =
                new SavicAssetMutationScope(
                    currentLayout,
                    "image_vertical_probe");

            rollback.CaptureAsset(
                IconCatalogPath);

            try
            {
                sources.Add(
                    StagePng(
                        currentLayout,
                        contentName,
                        64,
                        40,
                        new Color32(190, 85, 35, 255),
                        new Color32(45, 110, 175, 255)));

                sources.Add(
                    StagePng(
                        currentLayout,
                        normalName,
                        48,
                        48,
                        new Color32(128, 128, 255, 255),
                        new Color32(145, 118, 250, 255)));

                sources.Add(
                    StagePng(
                        currentLayout,
                        uiName,
                        32,
                        32,
                        new Color32(255, 255, 255, 255),
                        new Color32(255, 255, 255, 0)));

                SavicManifest contentManifest =
                    Ingest(
                        intake,
                        manifests,
                        sources[0]);

                SavicManifest normalManifest =
                    Ingest(
                        intake,
                        manifests,
                        sources[1]);

                SavicManifest uiManifest =
                    Ingest(
                        intake,
                        manifests,
                        sources[2]);

                SavicImageAuthoringRecord contentPlan =
                    RequirePlan(
                        contentManifest,
                        SavicImageAuthoringPlanner.ContentImageRole,
                        "GENERIC");

                SavicImageAuthoringRecord normalPlan =
                    RequirePlan(
                        normalManifest,
                        SavicImageAuthoringPlanner.MaterialTextureRole,
                        "NORMAL");

                SavicImageAuthoringRecord uiPlan =
                    RequirePlan(
                        uiManifest,
                        SavicImageAuthoringPlanner.UiIconRole,
                        "UI");

                Require(
                    string.Equals(
                        uiPlan.targetIconId,
                        BBIconId.StatusCorrect.ToString(),
                        StringComparison.Ordinal),
                    "UI image did not resolve StatusCorrect.");

                rollback.CaptureAsset(
                    contentPlan.publishedAssetPath);
                rollback.CaptureAsset(
                    normalPlan.publishedAssetPath);
                rollback.CaptureAsset(
                    uiPlan.publishedAssetPath);

                SavicSourceProcessingOutcome contentOutcome =
                    processing.ProcessBySavicId(
                        contentManifest.savicId);

                SavicSourceProcessingOutcome normalOutcome =
                    processing.ProcessBySavicId(
                        normalManifest.savicId);

                SavicSourceProcessingOutcome uiOutcome =
                    processing.ProcessBySavicId(
                        uiManifest.savicId);

                Require(
                    contentOutcome.Succeeded,
                    "Content image processing failed: " +
                    contentOutcome.Message);

                Require(
                    normalOutcome.Succeeded,
                    "Normal-map processing failed: " +
                    normalOutcome.Message);

                Require(
                    uiOutcome.Succeeded,
                    "UI icon processing failed: " +
                    uiOutcome.Message);

                Require(
                    manifests.TryGetBySavicId(
                        contentManifest.savicId,
                        out contentManifest),
                    "Published content image manifest is missing.");

                Require(
                    manifests.TryGetBySavicId(
                        normalManifest.savicId,
                        out normalManifest),
                    "Published normal-map manifest is missing.");

                Require(
                    manifests.TryGetBySavicId(
                        uiManifest.savicId,
                        out uiManifest),
                    "Published UI icon manifest is missing.");

                ValidateContentImage(
                    contentManifest);

                ValidateNormalMap(
                    normalManifest);

                ValidateUiIcon(
                    uiManifest);

                ValidateRejectedContracts();

                BBIconographyInstaller
                    .RebuildCatalogFromLocalOrThrow();

                BBIconCatalog rebuiltCatalog =
                    AssetDatabase.LoadAssetAtPath<BBIconCatalog>(
                        IconCatalogPath);

                BBIconDefinition rebuiltEntry = default;

                bool rebuildBindingResolved =
                    rebuiltCatalog != null &&
                    rebuiltCatalog.TryGet(
                        BBIconId.StatusCorrect,
                        out rebuiltEntry);

                Require(
                    rebuildBindingResolved &&
                    rebuiltEntry.sprite != null &&
                    string.Equals(
                        AssetDatabase.GetAssetPath(
                            rebuiltEntry.sprite),
                        uiManifest.imageAuthoring.publishedAssetPath,
                        StringComparison.Ordinal),
                    "Iconography rebuild did not preserve the SAVIC-managed UI override.");

                Debug.Log(
                    "[SAVIC] IMAGE VERTICAL PROBE - PASS\n" +
                    "Content image publication: PASS\n" +
                    "Material normal-map profile: PASS\n" +
                    "UI Sprite + BBIconCatalog binding: PASS\n" +
                    "Icon catalog rebuild preservation: PASS\n" +
                    "Unsupported WebP routing: PASS\n" +
                    "Unknown UI icon id routing: PASS");
            }
            finally
            {
                for (int i = 0;
                     i < sources.Count;
                     i++)
                {
                    CleanupProbeSource(
                        sources[i]);
                }

                try
                {
                    if (Directory.Exists(diagnosticRoot))
                        Directory.Delete(
                            diagnosticRoot,
                            true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[SAVIC] Image probe diagnostic cleanup warning: " +
                        exception.Message);
                }
            }
        }

        private static ProbeSource StagePng(
            SavicStorageLayout layout,
            string fileName,
            int width,
            int height,
            Color32 first,
            Color32 second)
        {
            string incomingPath =
                Path.Combine(
                    layout.DropHereRoot,
                    fileName);

            if (File.Exists(incomingPath))
            {
                throw new InvalidOperationException(
                    "Image probe input already exists: " +
                    incomingPath);
            }

            Texture2D texture =
                new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false,
                    false);

            try
            {
                Color32[] pixels =
                    new Color32[width * height];

                for (int y = 0;
                     y < height;
                     y++)
                {
                    for (int x = 0;
                         x < width;
                         x++)
                    {
                        bool alternate =
                            ((x / 4) + (y / 4)) % 2 == 0;

                        pixels[y * width + x] =
                            alternate
                                ? first
                                : second;
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                byte[] bytes =
                    texture.EncodeToPNG();

                File.WriteAllBytes(
                    incomingPath,
                    bytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    texture);
            }

            string hash =
                SavicHashService.ComputeSha256(
                    incomingPath);

            string archivePath =
                layout.GetArchivedSourcePath(
                    hash,
                    fileName);

            return new ProbeSource(
                incomingPath,
                archivePath,
                File.Exists(archivePath));
        }

        private static SavicManifest Ingest(
            SavicIntakeService intake,
            SavicManifestRepository manifests,
            ProbeSource source)
        {
            SavicIntakeOutcome outcome =
                intake.IngestSynchronously(
                    source.IncomingPath);

            Require(
                outcome.Succeeded &&
                !outcome.DuplicateExact,
                "Image probe intake failed or unexpectedly deduplicated.");

            Require(
                manifests.TryGetBySavicId(
                    outcome.ManifestSavicId,
                    out SavicManifest manifest),
                "Image probe manifest was not persisted.");

            return manifest;
        }

        private static SavicImageAuthoringRecord RequirePlan(
            SavicManifest manifest,
            string expectedRole,
            string expectedMapType)
        {
            Require(
                SavicImageAuthoringPlanner.TryPlan(
                    manifest,
                    out SavicImageAuthoringRecord plan,
                    out string reasonCode,
                    out string error),
                "Image plan failed: " +
                reasonCode +
                " — " +
                error);

            Require(
                string.Equals(
                    plan.role,
                    expectedRole,
                    StringComparison.Ordinal) &&
                string.Equals(
                    plan.mapType,
                    expectedMapType,
                    StringComparison.Ordinal),
                "Image plan resolved the wrong role/map type.");

            return plan;
        }

        private static void ValidateContentImage(
            SavicManifest manifest)
        {
            RequirePublished(
                manifest,
                SavicImageAuthoringPlanner.ContentImageRole);

            TextureImporter importer =
                AssetImporter.GetAtPath(
                    manifest.imageAuthoring.publishedAssetPath)
                    as TextureImporter;

            Sprite sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    manifest.imageAuthoring.publishedAssetPath);

            Require(
                importer != null &&
                importer.textureType ==
                    TextureImporterType.Sprite &&
                importer.spriteImportMode ==
                    SpriteImportMode.Single &&
                !importer.mipmapEnabled &&
                importer.sRGBTexture &&
                importer.wrapMode ==
                    TextureWrapMode.Clamp &&
                sprite != null,
                "Content image importer profile is invalid.");
        }

        private static void ValidateNormalMap(
            SavicManifest manifest)
        {
            RequirePublished(
                manifest,
                SavicImageAuthoringPlanner.MaterialTextureRole);

            TextureImporter importer =
                AssetImporter.GetAtPath(
                    manifest.imageAuthoring.publishedAssetPath)
                    as TextureImporter;

            Require(
                importer != null &&
                importer.textureType ==
                    TextureImporterType.NormalMap &&
                importer.mipmapEnabled &&
                !importer.sRGBTexture &&
                importer.wrapMode ==
                    TextureWrapMode.Repeat,
                "Normal-map importer profile is invalid.");
        }

        private static void ValidateUiIcon(
            SavicManifest manifest)
        {
            RequirePublished(
                manifest,
                SavicImageAuthoringPlanner.UiIconRole);

            BBIconCatalog catalog =
                AssetDatabase.LoadAssetAtPath<BBIconCatalog>(
                    IconCatalogPath);

            BBIconDefinition entry = default;

            bool bound =
                catalog != null &&
                catalog.TryGet(
                    BBIconId.StatusCorrect,
                    out entry);

            Require(
                bound &&
                entry.sprite != null &&
                string.Equals(
                    AssetDatabase.GetAssetPath(
                        entry.sprite),
                    manifest.imageAuthoring.publishedAssetPath,
                    StringComparison.Ordinal),
                "BBIconCatalog did not bind the SAVIC UI sprite.");

            string[] labels =
                AssetDatabase.GetLabels(
                    entry.sprite);

            Require(
                Array.IndexOf(
                    labels,
                    "SAVIC.Managed") >= 0 &&
                Array.IndexOf(
                    labels,
                    "SAVIC.UIIcon") >= 0,
                "SAVIC UI icon labels are missing.");
        }

        private static void RequirePublished(
            SavicManifest manifest,
            string role)
        {
            Require(
                manifest != null &&
                string.Equals(
                    manifest.status,
                    "PUBLISHED",
                    StringComparison.Ordinal) &&
                manifest.imageAuthoring != null &&
                manifest.imageAuthoring.planned &&
                string.Equals(
                    manifest.imageAuthoring.role,
                    role,
                    StringComparison.Ordinal) &&
                manifest.imageReadiness != null &&
                manifest.imageReadiness.validated &&
                manifest.imageReadiness.textureResolvable &&
                manifest.imageReadiness.importerProfileValid,
                "Published image readiness is incomplete.");
        }

        private static void ValidateRejectedContracts()
        {
            SavicManifest webp =
                CreateSyntheticImageManifest(
                    "image_probe_webp",
                    "dish.webp",
                    ".webp");

            Require(
                !SavicImageAuthoringPlanner.TryPlan(
                    webp,
                    out _,
                    out string webpReason,
                    out _) &&
                string.Equals(
                    webpReason,
                    "IMAGE_FORMAT_UNSUPPORTED",
                    StringComparison.Ordinal),
                "WebP did not route to explicit unsupported-format review.");

            SavicManifest unknownUi =
                CreateSyntheticImageManifest(
                    "image_probe_unknown_ui",
                    "ui_NotARealIcon.png",
                    ".png");

            Require(
                !SavicImageAuthoringPlanner.TryPlan(
                    unknownUi,
                    out _,
                    out string uiReason,
                    out _) &&
                string.Equals(
                    uiReason,
                    "UI_ICON_ID_UNKNOWN",
                    StringComparison.Ordinal),
                "Unknown explicit UI icon id did not route to review.");
        }

        private static SavicManifest CreateSyntheticImageManifest(
            string savicId,
            string fileName,
            string extension)
        {
            return new SavicManifest
            {
                savicId = savicId,
                source =
                    new SavicSourceRecord
                    {
                        sourceHash =
                            "synthetic-" +
                            savicId,
                        originalFileName =
                            fileName,
                        extension =
                            extension,
                        sourceKind =
                            SavicSourceKind.Image.ToString()
                    }
            };
        }

        private static void CleanupProbeSource(
            ProbeSource source)
        {
            try
            {
                if (File.Exists(
                        source.IncomingPath))
                {
                    File.Delete(
                        source.IncomingPath);
                }

                if (!source.ArchiveExisted &&
                    File.Exists(
                        source.ArchivePath))
                {
                    File.Delete(
                        source.ArchivePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[SAVIC] Image probe source cleanup warning: " +
                    exception.Message);
            }
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct ProbeSource
        {
            internal ProbeSource(
                string incomingPath,
                string archivePath,
                bool archiveExisted)
            {
                IncomingPath = incomingPath;
                ArchivePath = archivePath;
                ArchiveExisted = archiveExisted;
            }

            internal string IncomingPath { get; }
            internal string ArchivePath { get; }
            internal bool ArchiveExisted { get; }
        }
    }
}
