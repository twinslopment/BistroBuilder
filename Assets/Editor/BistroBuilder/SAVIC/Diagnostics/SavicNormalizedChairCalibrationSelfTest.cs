using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicNormalizedChairCalibrationSelfTest
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Normalized Chair Calibration Self-Test", false, 130)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            SavicManifest normalized = Fixture("dining_chair.glb", 1.90f, 0.98f);
            Require(SavicChairAuthoringPlanner.TryPlan(normalized,
                    out SavicChairAuthoringRecord plan, out string rejection),
                "High-confidence normalized chair was rejected: " + rejection);
            Require(plan.uniformScale > 0.40f && plan.uniformScale < 0.52f &&
                    plan.finalSeatHeightMeters >= 0.40f &&
                    plan.finalSeatHeightMeters <= 0.52f &&
                    plan.finalWidthMeters >= 0.32f &&
                    plan.finalDepthMeters >= 0.34f,
                "Normalized chair escaped canonical physical bounds.");

            normalized.source.originalFileName = "bar_stool.glb";
            Require(!SavicChairAuthoringPlanner.TryPlan(normalized, out _, out _),
                "Bar stool was silently converted into a dining chair.");
            normalized.source.originalFileName = "dining_chair.glb";
            normalized.model3D.heightMeters = 1.70f;
            Require(!SavicChairAuthoringPlanner.TryPlan(normalized, out _, out _),
                "Arbitrary oversize chair passed normalized-export calibration.");
            normalized.model3D.heightMeters = 1.90f;
            normalized.model3D.chairGeometry.confidenceScore = 0.60f;
            Require(!SavicChairAuthoringPlanner.TryPlan(normalized, out _, out _),
                "Weak geometry passed normalized-export calibration.");

            SavicManifest standard = Fixture("standard_chair.glb", 0.90f, 0.45f);
            Require(SavicChairAuthoringPlanner.TryPlan(standard,
                    out SavicChairAuthoringRecord standardPlan, out _) &&
                    Math.Abs(standardPlan.uniformScale - 1f) < 0.001f,
                "Canonical chair calibration regressed.");
            TestSelectiveRetry();
            Debug.Log("[SAVIC] NORMALIZED CHAIR CALIBRATION SELF-TEST - PASS: " +
                      "normalized chair, physical bounds, ambiguous stool, weak evidence, standard chair and version-bounded verified retry.");
        }

        private static void TestSelectiveRetry()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "BistroBuilder_SAVIC_ChairCalibration_" + Guid.NewGuid().ToString("N"));
            try
            {
                SavicStorageLayout layout = new SavicStorageLayout(root);
                layout.EnsureInfrastructure();
                const string name = "normalized_chair.glb";
                byte[] bytes = { 1, 2, 3, 4, 5, 6 };
                string original = Path.Combine(root, name);
                File.WriteAllBytes(original, bytes);
                string hash = SavicHashService.ComputeSha256(original);
                string archived = layout.GetArchivedSourcePath(hash, name);
                Directory.CreateDirectory(Path.GetDirectoryName(archived));
                File.Copy(original, archived);

                SavicManifest manifest = Fixture(name, 1.90f, 0.98f);
                manifest.savicId = Guid.NewGuid().ToString("N");
                manifest.status = "NEEDS_REVIEW";
                manifest.source.sourceHash = hash;
                manifest.source.sourceKind = SavicSourceKind.Model3D.ToString();
                manifest.source.archivedRelativePath = layout.ToProjectRelativePath(archived);
                new SavicManifestRepository(layout).Save(manifest);

                SavicJobRecord review = new SavicJobRecord
                {
                    jobId = Guid.NewGuid().ToString("N"),
                    manifestSavicId = manifest.savicId,
                    sourceHash = hash,
                    originalFileName = name,
                    archivedRelativePath = manifest.source.archivedRelativePath,
                    state = SavicJobState.NeedsReview.ToString(),
                    reasonCode = "CHAIR_AUTHORING_REVIEW",
                    primaryStage = "FAMILY_PUBLICATION",
                    batchEligible = true
                };
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { review }
                    });
                SavicJobStore jobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService continuity =
                    new SavicCanonicalReconciliationService(layout,
                        new SavicManifestRepository(layout), jobs);

                File.WriteAllBytes(archived, new byte[] { 9, 9 });
                Require(continuity.RetryBestVerifiedChairAuthoringReview() == string.Empty,
                    "Corrupt original was queued for chair publication.");
                File.Copy(original, archived, true);
                Require(continuity.RetryBestVerifiedChairAuthoringReview() == name &&
                        continuity.RetryBestVerifiedChairAuthoringReview() == string.Empty &&
                        jobs.Jobs[0].state == SavicJobState.Ingested.ToString(),
                    "Verified chair retry was not selective and idempotent.");

                // A planner revision may retry a review once; a later domain
                // reload must not loop if publication still needs review.
                review.state = SavicJobState.NeedsReview.ToString();
                review.lastAutomaticChairPlannerRetryVersion = string.Empty;
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { review }
                    });
                SavicJobStore automaticJobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService automatic =
                    new SavicCanonicalReconciliationService(layout,
                        new SavicManifestRepository(layout), automaticJobs);
                Require(automatic.RetryVerifiedChairReviewsForCurrentPlanner(8) == 1 &&
                        automaticJobs.Jobs[0].state == SavicJobState.Ingested.ToString() &&
                        automaticJobs.Jobs[0].lastAutomaticChairPlannerRetryVersion ==
                        SavicChairAuthoringPlanner.Version,
                    "Automatic planner revision did not queue the verified chair once.");

                SavicJobRecord retried = automaticJobs.Jobs[0];
                retried.state = SavicJobState.NeedsReview.ToString();
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { retried }
                    });
                SavicJobStore reloadedJobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService reloaded =
                    new SavicCanonicalReconciliationService(layout,
                        new SavicManifestRepository(layout), reloadedJobs);
                Require(reloaded.RetryVerifiedChairReviewsForCurrentPlanner(8) == 0 &&
                        reloadedJobs.Jobs[0].state == SavicJobState.NeedsReview.ToString(),
                    "Domain reload repeated the same planner revision indefinitely.");

                retried.reasonCode = "CHAIR_PUBLICATION_FAILED";
                retried.message = "Chair preview generation failed: Rendered preview appears blank or visually degenerate.";
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { retried }
                    });
                SavicJobStore previewJobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService previewRetry =
                    new SavicCanonicalReconciliationService(layout,
                        new SavicManifestRepository(layout), previewJobs);
                Require(previewRetry.RetryVerifiedChairPreviewFailures(8) == 1 &&
                        previewJobs.Jobs[0].state == SavicJobState.Ingested.ToString() &&
                        previewJobs.Jobs[0].lastAutomaticChairPreviewRetryVersion ==
                        SavicPreviewRenderer.Version,
                    "Verified graphics recovery did not queue the preview failure.");

                SavicJobRecord previewFailedAgain = previewJobs.Jobs[0];
                previewFailedAgain.state = SavicJobState.NeedsReview.ToString();
                previewFailedAgain.reasonCode = "CHAIR_PUBLICATION_FAILED";
                previewFailedAgain.message = retried.message;
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { previewFailedAgain }
                    });
                SavicJobStore secondPreviewJobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService secondPreviewRetry =
                    new SavicCanonicalReconciliationService(layout,
                        new SavicManifestRepository(layout), secondPreviewJobs);
                Require(secondPreviewRetry.RetryVerifiedChairPreviewFailures(8) == 0,
                    "The same preview renderer revision retried indefinitely.");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static SavicManifest Fixture(string fileName, float height, float seat)
        {
            float ratio = height > 0f ? seat / height : 0f;
            return new SavicManifest
            {
                source = new SavicSourceRecord { originalFileName = fileName },
                classification = new SavicClassificationRecord
                {
                    type = "Chair", score = 0.85f, geometryBacked = true
                },
                model3D = new SavicModelAnalysisRecord
                {
                    analyzed = true, hasUsableBounds = true,
                    widthMeters = height > 1.5f ? 1.0f : 0.48f,
                    heightMeters = height,
                    depthMeters = height > 1.5f ? 1.15f : 0.55f,
                    chairGeometry = new SavicChairGeometryProfileRecord
                    {
                        analyzed = true, usable = true,
                        seatHeightMeters = seat, seatHeight01 = ratio,
                        confidenceScore = 0.95f, frontDirectionLocalZ = 1f
                    },
                    semanticParts = new SavicSemanticPartAnalysisRecord
                    {
                        analyzed = true, automationReady = true
                    }
                }
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
