using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicMassIngestionRealProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        private const string ChairMaster002Path =
            "Assets/Art/Blender/Placeables/Furniture/Chairs/" +
            "BB_Chair_Master_002/Models/BB_Chair_Master_002.fbx";

        private const string ChairMaster001Path =
            "Assets/Art/Blender/Placeables/Furniture/Chairs/" +
            "BB_Chair_Master_001/Models/BB_Chair_Master_001_Final.fbx";

        private const string BistroChairPath =
            "Assets/Art/Blender/Placeables/Dining/" +
            "chair_bistro_01/Models/chair_bistro_01.fbx";

        private const string CalibrationPath =
            "Assets/Art/Blender/Placeables/Dining/" +
            "calibration_1m/Models/calibration_1m.fbx";

        private const string Prefix =
            "_SAVIC_MASS_PROBE_";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/" +
            "Run Mass Ingestion Real Probe",
            false,
            131)]
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

            string diagnosticRoot =
                Path.Combine(
                    currentLayout.RuntimeRoot,
                    "Diagnostics",
                    "MassIngestion",
                    Guid.NewGuid().ToString("N"));

            SavicStorageLayout queueLayout =
                new SavicStorageLayout(
                    diagnosticRoot);

            queueLayout.EnsureInfrastructure();

            string diagnosticManifestRoot =
                Path.Combine(
                    diagnosticRoot,
                    "IsolatedManifests");

            SavicManifestRepository manifests =
                new SavicManifestRepository(
                    currentLayout,
                    diagnosticManifestRoot);

            SavicJobStore jobs =
                new SavicJobStore(
                    queueLayout);

            SavicIntakeService intake =
                new SavicIntakeService(
                    currentLayout,
                    manifests,
                    jobs);

            List<string> stagedPaths =
                new List<string>();

            List<string> diagnosticArchivePaths =
                new List<string>();

            List<string> diagnosticMirrorAssetPaths =
                new List<string>();

            using SavicAssetMutationScope catalogRollback =
                new SavicAssetMutationScope(
                    currentLayout,
                    "mass_ingestion_real_probe");

            catalogRollback.CaptureAsset(
                MainCatalogPath);

            try
            {
                RequireSourceExists(
                    currentLayout,
                    ChairMaster002Path);

                RequireSourceExists(
                    currentLayout,
                    ChairMaster001Path);

                RequireSourceExists(
                    currentLayout,
                    BistroChairPath);

                RequireSourceExists(
                    currentLayout,
                    CalibrationPath);

                ProbeInput[] inputs =
                {
                    ProbeInput.BrokenGlb(
                        Prefix + "01_broken.glb"),

                    ProbeInput.RealModel(
                        Prefix + "02_chair_master_002.fbx",
                        ChairMaster002Path),

                    ProbeInput.DuplicateRealModel(
                        Prefix + "03_duplicate_chair_master_002.fbx",
                        ChairMaster002Path),

                    ProbeInput.RealModel(
                        Prefix + "04_chair_master_001.fbx",
                        ChairMaster001Path),

                    ProbeInput.RealModel(
                        Prefix + "05_chair_bistro_01.fbx",
                        BistroChairPath),

                    ProbeInput.RealModel(
                        Prefix + "06_calibration_1m.fbx",
                        CalibrationPath),

                    ProbeInput.Structured(
                        Prefix + "07_metadata.json")
                };

                List<SavicIntakeOutcome> outcomes =
                    new List<SavicIntakeOutcome>(
                        inputs.Length);

                for (int index = 0;
                     index < inputs.Length;
                     index++)
                {
                    ProbeInput input =
                        inputs[index];

                    string incomingPath =
                        Path.Combine(
                            currentLayout.DropHereRoot,
                            input.IncomingFileName);

                    DeleteIfExists(
                        incomingPath);

                    StageInput(
                        currentLayout,
                        input,
                        incomingPath);

                    stagedPaths.Add(
                        incomingPath);

                    string sourceHash =
                        SavicHashService.ComputeSha256(
                            incomingPath);

                    string archivePath =
                        currentLayout.GetArchivedSourcePath(
                            sourceHash,
                            input.IncomingFileName);

                    // All probe names are reserved. Remove only residue
                    // belonging to this diagnostic from a previous aborted run.
                    DeleteIfExists(
                        archivePath);

                    SavicIntakeOutcome outcome =
                        intake.IngestSynchronously(
                            incomingPath);

                    Require(
                        outcome.Succeeded,
                        "Mass probe intake failed for " +
                        input.IncomingFileName +
                        ": " +
                        outcome.Message);

                    outcomes.Add(
                        outcome);

                    if (!outcome.DuplicateExact)
                    {
                        diagnosticArchivePaths.Add(
                            archivePath);

                        string mirrorAbsolute =
                            currentLayout.GetUnitySourceMirrorPath(
                                outcome.SourceHash,
                                input.IncomingFileName);

                        diagnosticMirrorAssetPaths.Add(
                            currentLayout.ToProjectRelativePath(
                                mirrorAbsolute));
                    }
                }

                Require(
                    outcomes.Count ==
                    inputs.Length,
                    "Not all mass probe inputs were ingested.");

                Require(
                    outcomes.Count(
                        outcome =>
                            outcome.DuplicateExact) ==
                    1,
                    "Exact duplicate handling did not produce exactly one duplicate.");

                Require(
                    jobs.CountByState(
                        SavicJobState.DuplicateExact) ==
                    1,
                    "Queue did not persist the duplicate as terminal.");

                Require(
                    jobs.PendingProcessCount ==
                    5,
                    "Expected five batch-eligible 3D jobs before processing.");

                SavicJobRecord metadataJob =
                    jobs.Jobs.FirstOrDefault(
                        job =>
                            job != null &&
                            string.Equals(
                                job.originalFileName,
                                Prefix + "07_metadata.json",
                                StringComparison.Ordinal));

                Require(
                    metadataJob != null &&
                    !metadataJob.batchEligible,
                    "Structured metadata incorrectly entered the 3D batch.");

                Require(
                    jobs.TryClaimNextProcessable(
                        out SavicJobRecord interrupted),
                    "Could not claim the first mass-processing job.");

                Require(
                    string.Equals(
                        interrupted.originalFileName,
                        Prefix + "01_broken.glb",
                        StringComparison.Ordinal),
                    "Deterministic queue order changed before recovery test.");

                SavicJobStore recoveredJobs =
                    new SavicJobStore(
                        queueLayout);

                Require(
                    recoveredJobs.RecoverInterruptedJobs() ==
                    1,
                    "Interrupted real batch job was not recovered exactly once.");

                SavicSourceProcessingService processing =
                    new SavicSourceProcessingService(
                        currentLayout,
                        manifests);

                SavicBatchProcessor batch =
                    new SavicBatchProcessor(
                        recoveredJobs,
                        processing);

                Stopwatch drain =
                    Stopwatch.StartNew();

                int processedTicks =
                    0;

                while (recoveredJobs.PendingProcessCount > 0)
                {
                    processedTicks++;

                    Require(
                        processedTicks <= 32,
                        "Mass batch exceeded the safety tick limit.");

                    bool processed =
                        batch.TickOneIgnoringCooldownForDiagnostics();

                    Require(
                        processed,
                        "Mass batch stalled while processable jobs remained.");
                }

                drain.Stop();

                IReadOnlyList<SavicJobRecord> finalJobs =
                    recoveredJobs.Jobs;

                SavicJobRecord brokenJob =
                    finalJobs.FirstOrDefault(
                        job =>
                            job != null &&
                            string.Equals(
                                job.originalFileName,
                                Prefix + "01_broken.glb",
                                StringComparison.Ordinal));

                Require(
                    brokenJob != null &&
                    (string.Equals(
                         brokenJob.state,
                         SavicJobState.FailedProcessing.ToString(),
                         StringComparison.Ordinal) ||
                     string.Equals(
                         brokenJob.state,
                         SavicJobState.NeedsReview.ToString(),
                         StringComparison.Ordinal)),
                    "Malformed GLB did not terminate safely as an isolated failure/review.");

                SavicJobRecord knownGoodJob =
                    finalJobs.FirstOrDefault(
                        job =>
                            job != null &&
                            string.Equals(
                                job.originalFileName,
                                Prefix + "02_chair_master_002.fbx",
                                StringComparison.Ordinal));

                Require(
                    knownGoodJob != null &&
                    string.Equals(
                        knownGoodJob.state,
                        SavicJobState.Done.ToString(),
                        StringComparison.Ordinal),
                    "Known-good chair after the malformed source did not complete.");

                List<SavicJobRecord> eligibleJobs =
                    finalJobs
                        .Where(
                            job =>
                                job != null &&
                                job.batchEligible)
                        .ToList();

                Require(
                    eligibleJobs.Count ==
                    5,
                    "Unexpected batch-eligible job count after processing.");

                Require(
                    eligibleJobs.All(
                        job =>
                            IsTerminalBatchState(
                                job.state)),
                    "At least one eligible mass job remained non-terminal.");

                Require(
                    recoveredJobs.PendingProcessCount ==
                    0,
                    "Mass queue still reports pending 3D work.");

                Require(
                    CountCatalogEntriesWithPrefix() >= 1,
                    "Known-good real model did not reach publication during mass probe.");

                ValidateNoDuplicatePublishedIds(
                    manifests.GetAll());

                int done =
                    eligibleJobs.Count(
                        job =>
                            string.Equals(
                                job.state,
                                SavicJobState.Done.ToString(),
                                StringComparison.Ordinal));

                int review =
                    eligibleJobs.Count(
                        job =>
                            string.Equals(
                                job.state,
                                SavicJobState.NeedsReview.ToString(),
                                StringComparison.Ordinal));

                int failed =
                    eligibleJobs.Count(
                        job =>
                            string.Equals(
                                job.state,
                                SavicJobState.FailedProcessing.ToString(),
                                StringComparison.Ordinal));

                Require(
                    done >= 1,
                    "Mass batch produced no successful publication.");

                Require(
                    failed + review >= 1,
                    "Mass batch did not exercise an isolated bad input.");

                Debug.Log(
                    "[SAVIC] MASS INGESTION REAL PROBE - PASS\n" +
                    "DropHere inputs: " +
                    inputs.Length +
                    "\nBatch-eligible 3D jobs: " +
                    eligibleJobs.Count +
                    "\nExact duplicate: PASS\n" +
                    "Non-3D exclusion: PASS\n" +
                    "Interrupted-job recovery: PASS\n" +
                    "Malformed asset isolation: PASS\n" +
                    "Known-good asset after failure: PASS\n" +
                    "Terminal drain: PASS\n" +
                    "Published DONE: " +
                    done +
                    "\nNeeds review: " +
                    review +
                    "\nFailed safely: " +
                    failed +
                    "\nDrain ticks: " +
                    processedTicks +
                    "\nDrain duration: " +
                    drain.ElapsedMilliseconds +
                    " ms");
            }
            finally
            {
                CleanupGeneratedContent(
                    manifests);

                for (int index = 0;
                     index < diagnosticMirrorAssetPaths.Count;
                     index++)
                {
                    DeleteDiagnosticAsset(
                        diagnosticMirrorAssetPaths[index]);
                }

                for (int index = 0;
                     index < diagnosticArchivePaths.Count;
                     index++)
                {
                    DeleteIfExists(
                        diagnosticArchivePaths[index]);
                }

                for (int index = 0;
                     index < stagedPaths.Count;
                     index++)
                {
                    DeleteIfExists(
                        stagedPaths[index]);
                }

                if (Directory.Exists(
                        diagnosticRoot))
                {
                    try
                    {
                        Directory.Delete(
                            diagnosticRoot,
                            true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[SAVIC] Mass probe temp cleanup warning: " +
                            exception.Message);
                    }
                }

                AssetDatabase.SaveAssets();

                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void StageInput(
            SavicStorageLayout layout,
            ProbeInput input,
            string incomingPath)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    incomingPath) ??
                layout.DropHereRoot);

            switch (input.Kind)
            {
                case ProbeInputKind.RealModel:
                case ProbeInputKind.DuplicateRealModel:
                {
                    string sourceAbsolute =
                        Path.Combine(
                            layout.ProjectRoot,
                            input.SourceAssetPath
                                .Replace(
                                    '/',
                                    Path.DirectorySeparatorChar));

                    File.Copy(
                        sourceAbsolute,
                        incomingPath,
                        true);

                    break;
                }

                case ProbeInputKind.BrokenGlb:
                    File.WriteAllBytes(
                        incomingPath,
                        new byte[]
                        {
                            0x53, 0x41, 0x56, 0x49, 0x43,
                            0x2D, 0x42, 0x52, 0x4F, 0x4B,
                            0x45, 0x4E, 0x2D, 0x47, 0x4C,
                            0x42
                        });
                    break;

                case ProbeInputKind.Structured:
                    File.WriteAllText(
                        incomingPath,
                        "{\"probe\":\"savic-mass-ingestion\",\"version\":1}");
                    break;

                default:
                    throw new InvalidOperationException(
                        "Unknown mass probe input kind.");
            }
        }

        private static void RequireSourceExists(
            SavicStorageLayout layout,
            string assetPath)
        {
            string absolute =
                Path.Combine(
                    layout.ProjectRoot,
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar));

            Require(
                File.Exists(
                    absolute),
                "Required real probe source is missing: " +
                assetPath);
        }

        private static bool IsTerminalBatchState(
            string state)
        {
            return string.Equals(
                       state,
                       SavicJobState.Done.ToString(),
                       StringComparison.Ordinal) ||
                   string.Equals(
                       state,
                       SavicJobState.NeedsReview.ToString(),
                       StringComparison.Ordinal) ||
                   string.Equals(
                       state,
                       SavicJobState.FailedProcessing.ToString(),
                       StringComparison.Ordinal) ||
                   string.Equals(
                       state,
                       SavicJobState.Cancelled.ToString(),
                       StringComparison.Ordinal);
        }

        private static void ValidateNoDuplicatePublishedIds(
            IReadOnlyList<SavicManifest> manifests)
        {
            IEnumerable<IGrouping<string, SavicManifest>> duplicateGroups =
                (manifests ??
                 Array.Empty<SavicManifest>())
                .Where(
                    manifest =>
                        manifest != null &&
                        !string.IsNullOrWhiteSpace(
                            manifest.canonicalContentId))
                .GroupBy(
                    manifest =>
                        manifest.canonicalContentId,
                    StringComparer.Ordinal)
                .Where(
                    group =>
                        group.Count() > 1);

            Require(
                !duplicateGroups.Any(),
                "Mass processing produced duplicate canonical ContentIds.");
        }

        private static int CountCatalogEntriesWithPrefix()
        {
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            if (catalog == null)
                return 0;

            int count =
                0;

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            for (int index = 0;
                 index < items.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition item =
                    items[index];

                if (item == null)
                    continue;

                string assetPath =
                    AssetDatabase.GetAssetPath(
                        item);

                if (!string.IsNullOrWhiteSpace(
                        assetPath) &&
                    assetPath.StartsWith(
                        "Assets/Generated/BistroBuilder/SAVIC/Published/",
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void CleanupGeneratedContent(
            SavicManifestRepository manifests)
        {
            if (manifests == null)
                return;

            IReadOnlyList<SavicManifest> all =
                manifests.GetAll();

            HashSet<string> folders =
                new HashSet<string>(
                    StringComparer.Ordinal);

            for (int index = 0;
                 index < all.Count;
                 index++)
            {
                SavicManifest manifest =
                    all[index];

                if (manifest == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(
                        manifest.canonicalContentId))
                {
                    folders.Add(
                        "Assets/Generated/BistroBuilder/SAVIC/Published/Chairs/" +
                        manifest.canonicalContentId);

                    folders.Add(
                        "Assets/Generated/BistroBuilder/SAVIC/Published/Tables/" +
                        manifest.canonicalContentId);
                }

                if (manifest.artifacts == null)
                    continue;

                for (int artifactIndex = 0;
                     artifactIndex < manifest.artifacts.Count;
                     artifactIndex++)
                {
                    SavicArtifactRecord artifact =
                        manifest.artifacts[artifactIndex];

                    string path =
                        artifact?.projectRelativePath;

                    if (string.IsNullOrWhiteSpace(
                            path) ||
                        !path.StartsWith(
                            "Assets/Generated/BistroBuilder/SAVIC/Published/",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string folder =
                        Path.GetDirectoryName(
                            path)
                        ?.Replace(
                            '\\',
                            '/');

                    if (!string.IsNullOrWhiteSpace(
                            folder))
                    {
                        folders.Add(
                            folder);
                    }
                }
            }

            foreach (string folder in folders.OrderByDescending(
                         value => value.Length))
            {
                if (AssetDatabase.IsValidFolder(
                        folder))
                {
                    AssetDatabase.DeleteAsset(
                        folder);
                }
            }
        }

        private static void DeleteDiagnosticAsset(
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(
                    assetPath) ||
                !assetPath.StartsWith(
                    "Assets/Generated/BistroBuilder/SAVIC/SourceMirror/",
                    StringComparison.Ordinal))
            {
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(
                    assetPath) != null ||
                File.Exists(
                    ToAbsoluteProjectPath(
                        assetPath)))
            {
                AssetDatabase.DeleteAsset(
                    assetPath);
            }
        }

        private static string ToAbsoluteProjectPath(
            string assetPath)
        {
            string projectRoot =
                Directory.GetParent(
                    Application.dataPath)
                ?.FullName ??
                throw new InvalidOperationException(
                    "Project root could not be resolved.");

            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static void DeleteIfExists(
            string path)
        {
            if (string.IsNullOrWhiteSpace(
                    path))
            {
                return;
            }

            try
            {
                if (File.Exists(
                        path))
                {
                    File.Delete(
                        path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[SAVIC] Mass probe file cleanup warning: " +
                    exception.Message);
            }
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    message);
            }
        }

        private enum ProbeInputKind
        {
            RealModel = 0,
            DuplicateRealModel = 1,
            BrokenGlb = 2,
            Structured = 3
        }

        private readonly struct ProbeInput
        {
            private ProbeInput(
                string incomingFileName,
                string sourceAssetPath,
                ProbeInputKind kind)
            {
                IncomingFileName =
                    incomingFileName ?? string.Empty;

                SourceAssetPath =
                    sourceAssetPath ?? string.Empty;

                Kind =
                    kind;
            }

            internal string IncomingFileName { get; }
            internal string SourceAssetPath { get; }
            internal ProbeInputKind Kind { get; }

            internal static ProbeInput RealModel(
                string incomingFileName,
                string sourceAssetPath)
            {
                return new ProbeInput(
                    incomingFileName,
                    sourceAssetPath,
                    ProbeInputKind.RealModel);
            }

            internal static ProbeInput DuplicateRealModel(
                string incomingFileName,
                string sourceAssetPath)
            {
                return new ProbeInput(
                    incomingFileName,
                    sourceAssetPath,
                    ProbeInputKind.DuplicateRealModel);
            }

            internal static ProbeInput BrokenGlb(
                string incomingFileName)
            {
                return new ProbeInput(
                    incomingFileName,
                    string.Empty,
                    ProbeInputKind.BrokenGlb);
            }

            internal static ProbeInput Structured(
                string incomingFileName)
            {
                return new ProbeInput(
                    incomingFileName,
                    string.Empty,
                    ProbeInputKind.Structured);
            }
        }
    }
}
