using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicCanonicalContinuitySelfTest
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Canonical Continuity Self-Test", false, 129)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "BistroBuilder_SAVIC_Continuity_" + Guid.NewGuid().ToString("N"));
            try
            {
                SavicStorageLayout layout = new SavicStorageLayout(root);
                layout.EnsureInfrastructure();
                byte[] validBytes = { 1, 3, 3, 7, 9 };
                byte[] attachBytes = { 2, 4, 6, 8 };
                string validOriginal = Path.Combine(root, "valid-original.glb");
                string attachOriginal = Path.Combine(root, "attach-original.glb");
                File.WriteAllBytes(validOriginal, validBytes);
                File.WriteAllBytes(attachOriginal, attachBytes);

                string validHash = SavicHashService.ComputeSha256(validOriginal);
                string attachHash = SavicHashService.ComputeSha256(attachOriginal);
                string invalidHash = SavicHashService.ComputeSha256Text("expected-other-bytes");
                SavicJobRecord valid = LegacyJob(layout, "valid.glb", validHash);
                SavicJobRecord missing = LegacyJob(layout, "missing.glb", attachHash);
                SavicJobRecord invalid = LegacyJob(layout, "invalid.glb", invalidHash);
                SavicJobRecord invalidPath = LegacyJob(layout, "outside.glb",
                    SavicHashService.ComputeSha256Text("outside"));
                invalidPath.archivedRelativePath = "../outside.glb";

                string validArchive = layout.FromProjectRelativePath(valid.archivedRelativePath);
                string invalidArchive = layout.FromProjectRelativePath(invalid.archivedRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(validArchive));
                Directory.CreateDirectory(Path.GetDirectoryName(invalidArchive));
                File.Copy(validOriginal, validArchive);
                File.WriteAllBytes(invalidArchive, new byte[] { 9, 9, 9 });

                SavicQueueSnapshot legacy = new SavicQueueSnapshot
                {
                    schemaVersion = 1,
                    jobs = new List<SavicJobRecord> { valid, missing, invalid, invalidPath }
                };
                string json = JsonUtility.ToJson(legacy, true)
                    .Replace("\"batchEligible\": false,", string.Empty);
                Require(!json.Contains("batchEligible"), "Fixture still contains batchEligible.");
                File.WriteAllText(layout.QueueSnapshotPath, json);

                SavicManifestRepository manifests = new SavicManifestRepository(layout);
                SavicJobStore jobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService continuity =
                    new SavicCanonicalReconciliationService(layout, manifests, jobs);

                SavicCanonicalContentInventorySnapshot inventory =
                    SavicCanonicalContentInventoryService.Build(layout,
                        Array.Empty<SavicManifest>(), jobs.Jobs,
                        new SavicProjectInventorySnapshot());
                Require(inventory.Total == 4 && inventory.Orphaned == 4 &&
                        inventory.Rows.Any(row =>
                            row.SourceLocation == "JOB_ONLY" &&
                            row.Reason.Contains("Original is missing")) &&
                        inventory.Rows.Any(row =>
                            row.SourceLocation == "ARCHIVED_JOB_ONLY" &&
                            row.Reason.Contains("manifest is missing")),
                    "Canonical inventory duplicated or misreported orphan jobs.");

                SavicContinuityReport before = continuity.Audit();
                Require(before.readyToReconcile == 1 && before.sourceMissing == 1 &&
                        before.sourceInvalid == 1 && before.identityConflict == 1,
                    "Legacy audit did not distinguish verified, absent and invalid originals.");
                Require(jobs.PendingProcessCount == 0,
                    "Historical queue was activated without explicit reconciliation.");

                Require(continuity.ReconcileVerifiedSources() == 1,
                    "Verified source was not enabled exactly once.");
                Require(continuity.ReconcileVerifiedSources() == 0,
                    "Reconciliation is not idempotent.");
                Require(manifests.TryGetBySavicId(valid.manifestSavicId, out SavicManifest recovered) &&
                        recovered.source.sourceHash == validHash &&
                        recovered.source.originalLastWriteUtcTicks == 0,
                    "Restored manifest lost identity, hash or unknown provenance.");
                Require(jobs.PendingProcessCount == 1,
                    "Only the verified source should be processable.");

                bool rejectedWrongSource = false;
                try
                {
                    continuity.AttachVerifiedOriginal(validOriginal);
                }
                catch (InvalidOperationException)
                {
                    rejectedWrongSource = true;
                }
                Require(rejectedWrongSource, "Unmatched original was accepted.");

                Require(continuity.AttachVerifiedOriginal(attachOriginal) == missing.originalFileName,
                    "Verified original was not attached to the matching legacy job.");
                Require(File.Exists(attachOriginal), "Operator's original was moved or deleted.");
                Require(File.Exists(layout.FromProjectRelativePath(missing.archivedRelativePath)) &&
                        jobs.PendingProcessCount == 2,
                    "Attached original did not become a canonical processable job.");
                Require(!manifests.TryGetBySavicId(invalid.manifestSavicId, out _) &&
                        !jobs.Jobs.First(job => job.jobId == invalid.jobId).batchEligible,
                    "Hash-invalid source was enabled.");

                SavicJobStore reloaded = new SavicJobStore(layout);
                SavicManifestRepository reloadedManifests = new SavicManifestRepository(layout);
                Require(reloaded.PendingProcessCount == 2 &&
                        reloadedManifests.TryGetBySavicId(missing.manifestSavicId, out _),
                    "Canonical reconciliation did not survive reload.");
                Require(reloaded.TryClaimNextProcessable(out SavicJobRecord claimed) &&
                        claimed.jobId == valid.jobId,
                    "Verified jobs did not retain deterministic queue order.");

                SavicQueueSnapshot failedSnapshot =
                    SavicAtomicFile.ReadJson<SavicQueueSnapshot>(layout.QueueSnapshotPath);
                SavicJobRecord mirrorFailure = failedSnapshot.jobs.First(job => job.jobId == valid.jobId);
                mirrorFailure.state = SavicJobState.FailedProcessing.ToString();
                mirrorFailure.reasonCode = "SOURCE_MATERIALIZATION_FAILED";
                mirrorFailure.primaryStage = "MATERIALIZE_SOURCE_MIRROR";
                SavicJobRecord unrelatedFailure = failedSnapshot.jobs.First(job => job.jobId == missing.jobId);
                unrelatedFailure.state = SavicJobState.FailedProcessing.ToString();
                unrelatedFailure.reasonCode = "OTHER_FAILURE";
                unrelatedFailure.primaryStage = "MATERIALIZE_SOURCE_MIRROR";
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath, failedSnapshot);

                SavicJobStore retryJobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService retryContinuity =
                    new SavicCanonicalReconciliationService(layout,
                        new SavicManifestRepository(layout), retryJobs);
                Require(retryContinuity.Audit().processingFailed == 2,
                    "Failed jobs were incorrectly counted as managed.");
                File.WriteAllBytes(validArchive, new byte[] { 0, 0, 0 });
                Require(retryContinuity.RetryVerifiedMirrorFailures() == 0,
                    "A corrupt archived source was retried.");
                File.Copy(validOriginal, validArchive, true);
                Require(retryContinuity.RetryVerifiedMirrorFailures() == 1 &&
                        retryContinuity.RetryVerifiedMirrorFailures() == 0,
                    "Verified mirror retry was not selective and idempotent.");
                Require(retryJobs.PendingProcessCount == 1 &&
                        retryJobs.Jobs.First(job => job.jobId == missing.jobId).state ==
                        SavicJobState.FailedProcessing.ToString(),
                    "Retry changed an unrelated failed job.");

                string batchRoot = Path.Combine(root, "selected-batch");
                SavicStorageLayout batchLayout = new SavicStorageLayout(batchRoot);
                batchLayout.EnsureInfrastructure();
                string selectedOriginal = Path.Combine(batchRoot, "download-renamed.glb");
                string newOriginal = Path.Combine(batchRoot, "new-source.glb");
                File.WriteAllBytes(selectedOriginal, new byte[] { 5, 4, 3, 2, 1 });
                File.WriteAllBytes(newOriginal, new byte[] { 8, 7, 6, 5 });
                SavicJobRecord historical = LegacyJob(batchLayout, "historic-name.glb",
                    SavicHashService.ComputeSha256(selectedOriginal));
                SavicAtomicFile.WriteJson(batchLayout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { historical }
                    });
                SavicManifestRepository batchManifests =
                    new SavicManifestRepository(batchLayout);
                SavicJobStore batchJobs = new SavicJobStore(batchLayout);
                SavicCanonicalReconciliationService batchContinuity =
                    new SavicCanonicalReconciliationService(batchLayout,
                        batchManifests, batchJobs);
                SavicIntakeService batchIntake =
                    new SavicIntakeService(batchLayout, batchManifests, batchJobs);
                SavicSelectedImportResult selected = batchContinuity.ImportSelectedFiles(
                    new[] { selectedOriginal, newOriginal, newOriginal }, batchIntake);
                Require(selected.Restored == 1 && selected.Ingested == 1 &&
                        selected.Duplicates == 0 && selected.Errors.Count == 0 &&
                        batchManifests.Count == 2 &&
                        batchManifests.TryGetBySavicId(historical.manifestSavicId, out _) &&
                        File.Exists(selectedOriginal) && File.Exists(newOriginal),
                    "Selected batch lost historical identity, ingested twice or changed originals.");
                SavicSelectedImportResult repeated = batchContinuity.ImportSelectedFiles(
                    new[] { selectedOriginal, newOriginal }, batchIntake);
                Require(repeated.Restored == 0 && repeated.Ingested == 0 &&
                        repeated.Duplicates == 2 && repeated.Errors.Count == 0 &&
                        batchManifests.Count == 2,
                    "Selected batch is not idempotent.");
                Require(!SavicEquipmentIntegrationPolicy.Resolve("bar_stool.glb")
                            .RequiresGameplayAdapter &&
                        !SavicEquipmentIntegrationPolicy.Resolve("arctic_curve_bar.glb")
                            .RequiresGameplayAdapter &&
                        SavicEquipmentIntegrationPolicy.Resolve("service_counter.glb")
                            .RequiresGameplayAdapter,
                    "A contextual bar token was mistaken for a functional station.");
                SavicQueueSnapshot policySnapshot =
                    SavicAtomicFile.ReadJson<SavicQueueSnapshot>(batchLayout.QueueSnapshotPath);
                SavicJobRecord review = policySnapshot.jobs.First(job =>
                    job.sourceHash == SavicHashService.ComputeSha256(newOriginal) &&
                    job.state == SavicJobState.Ingested.ToString());
                review.state = SavicJobState.NeedsReview.ToString();
                review.reasonCode = "FUNCTIONAL_ADAPTER_REQUIRED";
                review.primaryStage = "PREIMPORT_ROUTE";
                SavicAtomicFile.WriteJson(batchLayout.QueueSnapshotPath, policySnapshot);
                SavicJobStore policyJobs = new SavicJobStore(batchLayout);
                SavicCanonicalReconciliationService policyContinuity =
                    new SavicCanonicalReconciliationService(batchLayout,
                        new SavicManifestRepository(batchLayout), policyJobs);
                Require(policyContinuity.RetryObsoletePreImportReviews() == 1 &&
                        policyContinuity.RetryObsoletePreImportReviews() == 0 &&
                        policyJobs.Jobs.First(job => job.jobId == review.jobId).state ==
                        SavicJobState.Ingested.ToString(),
                    "Obsolete pre-import review was not selectively retried.");
                Debug.Log("[SAVIC] CANONICAL CONTINUITY SELF-TEST - PASS: " +
                          "legacy queue, SHA-256, explicit enablement, attachment, " +
                          "idempotence, reload, claim order, selective mirror retry " +
                          "selected external batch import and obsolete policy retry.");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static SavicJobRecord LegacyJob(
            SavicStorageLayout layout, string name, string hash)
        {
            return new SavicJobRecord
            {
                jobId = Guid.NewGuid().ToString("N"),
                state = SavicJobState.Ingested.ToString(),
                sourceHash = hash,
                originalFileName = name,
                archivedRelativePath = layout.ToProjectRelativePath(
                    layout.GetArchivedSourcePath(hash, name)),
                manifestSavicId = Guid.NewGuid().ToString("N"),
                createdUtc = DateTime.UtcNow.ToString("O"),
                updatedUtc = DateTime.UtcNow.ToString("O"),
                attempts = 1
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
