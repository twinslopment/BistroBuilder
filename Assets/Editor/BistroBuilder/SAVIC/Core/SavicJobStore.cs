using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicJobStore
    {
        private readonly SavicStorageLayout layout;
        private readonly object sync = new object();
        private SavicQueueSnapshot snapshot;

        internal SavicJobStore(SavicStorageLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        internal event Action Changed;

        internal bool IsPaused
        {
            get
            {
                lock (sync)
                {
                    EnsureLoaded();
                    return snapshot.paused;
                }
            }
        }

        internal IReadOnlyList<SavicJobRecord> Jobs
        {
            get
            {
                lock (sync)
                {
                    EnsureLoaded();
                    return snapshot.jobs.ToArray();
                }
            }
        }

        internal SavicJobRecord RecordIngested(
            SavicManifest manifest,
            bool duplicateExact,
            string message)
        {
            if (manifest?.source == null)
                throw new ArgumentNullException(nameof(manifest));

            SavicJobRecord record;

            lock (sync)
            {
                EnsureLoaded();
                string now = DateTime.UtcNow.ToString("O");

                record = new SavicJobRecord
                {
                    jobId = Guid.NewGuid().ToString("N"),
                    state = (duplicateExact
                        ? SavicJobState.DuplicateExact
                        : SavicJobState.Ingested).ToString(),
                    sourceHash = manifest.source.sourceHash,
                    originalFileName = manifest.source.originalFileName,
                    archivedRelativePath = manifest.source.archivedRelativePath,
                    manifestSavicId = manifest.savicId,
                    createdUtc = now,
                    updatedUtc = now,
                    attempts = 1,
                    message = message ?? string.Empty,
                    batchEligible =
                        !duplicateExact &&
                        string.Equals(
                            manifest.source.sourceKind,
                            SavicSourceKind.Model3D.ToString(),
                            StringComparison.Ordinal),
                    checkpoint = duplicateExact
                        ? "DUPLICATE_EXACT"
                        : "INGESTED",
                    completedUtc = duplicateExact
                        ? now
                        : string.Empty
                };

                snapshot.jobs.Add(record);
                TrimHistory();
                Save();
            }

            NotifyChanged();
            return record;
        }

        internal SavicJobRecord RecordFailure(
            string originalFileName,
            string sourceHash,
            string archivedRelativePath,
            SavicJobState state,
            int attempts,
            string message)
        {
            SavicJobRecord record;

            lock (sync)
            {
                EnsureLoaded();
                string now = DateTime.UtcNow.ToString("O");

                record = new SavicJobRecord
                {
                    jobId = Guid.NewGuid().ToString("N"),
                    state = state.ToString(),
                    sourceHash = sourceHash ?? string.Empty,
                    originalFileName = originalFileName ?? string.Empty,
                    archivedRelativePath = archivedRelativePath ?? string.Empty,
                    createdUtc = now,
                    updatedUtc = now,
                    attempts = Math.Max(1, attempts),
                    message = message ?? string.Empty,
                    checkpoint = state.ToString().ToUpperInvariant(),
                    completedUtc = now
                };

                snapshot.jobs.Add(record);
                TrimHistory();
                Save();
            }

            NotifyChanged();
            return record;
        }

        internal void SetPaused(bool paused)
        {
            bool changed = false;

            lock (sync)
            {
                EnsureLoaded();

                if (snapshot.paused == paused)
                    return;

                snapshot.paused = paused;
                snapshot.schedulerGeneration++;
                Save();
                changed = true;
            }

            if (changed)
                NotifyChanged();
        }

        internal int RecoverInterruptedJobs()
        {
            int recovered = 0;
            string now = DateTime.UtcNow.ToString("O");

            lock (sync)
            {
                EnsureLoaded();

                for (int index = 0;
                     index < snapshot.jobs.Count;
                     index++)
                {
                    SavicJobRecord job = snapshot.jobs[index];

                    if (job == null ||
                        !string.Equals(
                            job.state,
                            SavicJobState.Processing.ToString(),
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (job.cancelRequested)
                    {
                        job.state =
                            SavicJobState.Cancelled.ToString();
                        job.checkpoint = "CANCELLED_AFTER_RECOVERY";
                        job.completedUtc = now;
                    }
                    else
                    {
                        job.state =
                            SavicJobState.Ingested.ToString();
                        job.checkpoint = "RECOVERED_AFTER_RELOAD";
                        job.message =
                            "Interrupted processing recovered and re-queued safely.";
                    }

                    job.processingStartedUtc = string.Empty;
                    job.updatedUtc = now;
                    recovered++;
                }

                if (recovered == 0)
                    return 0;

                snapshot.recoveredJobs += recovered;
                snapshot.schedulerGeneration++;
                snapshot.lastRecoveryUtc = now;
                Save();
            }

            NotifyChanged();
            return recovered;
        }

        internal bool RequestCancel(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                return false;

            bool changed = false;
            string now = DateTime.UtcNow.ToString("O");

            lock (sync)
            {
                EnsureLoaded();

                SavicJobRecord job =
                    snapshot.jobs.FirstOrDefault(
                        candidate =>
                            candidate != null &&
                            string.Equals(
                                candidate.jobId,
                                jobId,
                                StringComparison.Ordinal));

                if (job == null ||
                    IsTerminal(job.state))
                {
                    return false;
                }

                if (string.Equals(
                        job.state,
                        SavicJobState.Processing.ToString(),
                        StringComparison.Ordinal))
                {
                    job.cancelRequested = true;
                    job.checkpoint = "CANCEL_REQUESTED";
                    job.message =
                        "Cancellation requested; current atomic asset operation will finish safely.";
                }
                else
                {
                    job.cancelRequested = true;
                    job.state =
                        SavicJobState.Cancelled.ToString();
                    job.checkpoint = "CANCELLED";
                    job.completedUtc = now;
                    job.message = "Cancelled before processing.";
                }

                job.updatedUtc = now;
                snapshot.schedulerGeneration++;
                Save();
                changed = true;
            }

            if (changed)
                NotifyChanged();

            return changed;
        }

        // Historical queue snapshots deliberately keep batchEligible=false.
        // Canonical reconciliation is the only path that opts a verified job in.
        internal bool EnableVerifiedLegacyJob(
            string jobId,
            string savicId,
            string sourceHash)
        {
            if (string.IsNullOrWhiteSpace(jobId) ||
                string.IsNullOrWhiteSpace(savicId) ||
                string.IsNullOrWhiteSpace(sourceHash))
            {
                throw new ArgumentException("A complete verified job identity is required.");
            }

            bool changed = false;
            lock (sync)
            {
                EnsureLoaded();
                SavicJobRecord job = snapshot.jobs.FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.jobId, jobId, StringComparison.Ordinal));

                if (job == null ||
                    !string.Equals(job.manifestSavicId, savicId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.sourceHash, sourceHash, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.state, SavicJobState.Ingested.ToString(), StringComparison.Ordinal) ||
                    job.cancelRequested)
                {
                    throw new InvalidOperationException(
                        "The queued job changed or is not eligible for explicit reconciliation.");
                }

                if (!job.batchEligible)
                {
                    job.batchEligible = true;
                    job.checkpoint = "CANONICAL_RECONCILED";
                    job.message = "Canonical source and manifest verified; queued for normal SAVIC processing.";
                    job.updatedUtc = DateTime.UtcNow.ToString("O");
                    snapshot.schedulerGeneration++;
                    Save();
                    changed = true;
                }
            }

            if (changed)
                NotifyChanged();
            return changed;
        }

        // The caller must verify the archived source and manifest immediately
        // before this narrowly scoped retry. Other failures stay terminal.
        internal bool RetryVerifiedMirrorFailure(
            string jobId,
            string savicId,
            string sourceHash)
        {
            bool changed = false;
            lock (sync)
            {
                EnsureLoaded();
                SavicJobRecord job = snapshot.jobs.FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.jobId, jobId, StringComparison.Ordinal));

                if (job == null ||
                    !string.Equals(job.manifestSavicId, savicId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.sourceHash, sourceHash, StringComparison.OrdinalIgnoreCase) ||
                    !job.batchEligible || job.cancelRequested ||
                    !string.Equals(job.state, SavicJobState.FailedProcessing.ToString(), StringComparison.Ordinal) ||
                    !string.Equals(job.reasonCode, "SOURCE_MATERIALIZATION_FAILED", StringComparison.Ordinal) ||
                    !string.Equals(job.primaryStage, "MATERIALIZE_SOURCE_MIRROR", StringComparison.Ordinal))
                {
                    return false;
                }

                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_MIRROR_RETRY";
                job.message = "Archived source and manifest verified; retrying the corrected source-mirror materialization.";
                job.processingStartedUtc = string.Empty;
                job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++;
                Save();
                changed = true;
            }

            if (changed)
                NotifyChanged();
            return changed;
        }

        // A pre-import policy decision may be retried only after the caller
        // verifies the original and proves the current policy changed.
        internal bool RetryVerifiedObsoletePreImportReview(
            string jobId, string savicId, string sourceHash)
        {
            bool changed = false;
            lock (sync)
            {
                EnsureLoaded();
                SavicJobRecord job = snapshot.jobs.FirstOrDefault(candidate =>
                    candidate != null && candidate.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    !string.Equals(job.manifestSavicId, savicId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.sourceHash, sourceHash, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.state, SavicJobState.NeedsReview.ToString(), StringComparison.Ordinal) ||
                    !string.Equals(job.reasonCode, "FUNCTIONAL_ADAPTER_REQUIRED", StringComparison.Ordinal) ||
                    !string.Equals(job.primaryStage, "PREIMPORT_ROUTE", StringComparison.Ordinal))
                    return false;

                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_POLICY_RETRY";
                job.message = "Verified source queued for evaluation under the corrected pre-import policy.";
                job.processingStartedUtc = string.Empty;
                job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++;
                Save();
                changed = true;
            }

            if (changed)
                NotifyChanged();
            return changed;
        }

        // Caller has verified the canonical archive and a current classifier
        // result that now resolves to a registered publication family.
        internal bool RetryVerifiedClassificationReview(
            string jobId, string savicId, string sourceHash,
            string classifierVersion)
        {
            if (string.IsNullOrWhiteSpace(classifierVersion))
                throw new ArgumentException("Classifier version is required.",
                    nameof(classifierVersion));

            bool changed = false;
            lock (sync)
            {
                EnsureLoaded();
                SavicJobRecord job = snapshot.jobs.FirstOrDefault(candidate =>
                    candidate != null && candidate.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    !string.Equals(job.manifestSavicId, savicId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.sourceHash, sourceHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    job.state != SavicJobState.NeedsReview.ToString() ||
                    job.reasonCode != "UNSUPPORTED_PUBLICATION_FAMILY" ||
                    job.primaryStage != "CLASSIFICATION" ||
                    string.Equals(job.lastAutomaticClassifierRetryVersion,
                        classifierVersion, StringComparison.Ordinal))
                    return false;

                job.lastAutomaticClassifierRetryVersion = classifierVersion;
                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_CLASSIFIER_RETRY";
                job.message = "Verified source queued once under the current registered content family.";
                job.processingStartedUtc = string.Empty;
                job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++;
                Save();
                changed = true;
            }

            if (changed)
                NotifyChanged();
            return changed;
        }

        // Reconciliation verifies the source and complete runtime proof first.
        // This queues publication; it never promotes a job to Done.
        internal bool RetryVerifiedBarRuntimeAcceptance(string jobId, string savicId,
            string sourceHash, string acceptanceFingerprint)
        {
            if (string.IsNullOrWhiteSpace(acceptanceFingerprint)) return false;
            lock (sync)
            {
                EnsureLoaded();
                SavicJobRecord job = snapshot.jobs.FirstOrDefault(candidate => candidate?.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    job.manifestSavicId != savicId || job.sourceHash != sourceHash ||
                    job.state != SavicJobState.NeedsReview.ToString() ||
                    job.reasonCode != "BAR_COUNTER_RUNTIME_ACCEPTANCE_PENDING" || job.primaryStage != "FAMILY_PUBLICATION" ||
                    job.lastAutomaticBarAcceptanceFingerprint == acceptanceFingerprint) return false;
                job.lastAutomaticBarAcceptanceFingerprint = acceptanceFingerprint;
                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_BAR_RUNTIME_ACCEPTANCE";
                job.message = "Current source and runtime acceptance verified; queued for canonical publication.";
                job.processingStartedUtc = job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++;
                Save();
            }
            NotifyChanged();
            return true;
        }

        internal bool RetryVerifiedBarStoolRuntimeAcceptance(string jobId, string savicId,
            string sourceHash, string acceptanceFingerprint)
        {
            if (string.IsNullOrWhiteSpace(acceptanceFingerprint)) return false;
            lock (sync)
            {
                EnsureLoaded();
                var job = snapshot.jobs.FirstOrDefault(candidate => candidate?.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    job.manifestSavicId != savicId || job.sourceHash != sourceHash ||
                    job.state != SavicJobState.NeedsReview.ToString() ||
                    job.reasonCode != "BAR_STOOL_RUNTIME_ACCEPTANCE_PENDING" || job.primaryStage != "FAMILY_PUBLICATION" ||
                    job.lastAutomaticBarStoolAcceptanceFingerprint == acceptanceFingerprint) return false;
                job.lastAutomaticBarStoolAcceptanceFingerprint = acceptanceFingerprint;
                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_BAR_STOOL_RUNTIME_ACCEPTANCE";
                job.message = "Current source and seated runtime acceptance verified; queued for canonical publication.";
                job.processingStartedUtc = job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++;
                Save();
            }
            NotifyChanged();
            return true;
        }

        internal bool RetryVerifiedOverheadReview(string jobId, string savicId, string sourceHash,
            string fingerprint, bool acceptedRuntime)
        {
            if (string.IsNullOrWhiteSpace(fingerprint)) return false;
            lock (sync)
            {
                EnsureLoaded();
                var job = snapshot.jobs.FirstOrDefault(candidate => candidate?.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested || job.manifestSavicId != savicId ||
                    job.sourceHash != sourceHash || job.state != SavicJobState.NeedsReview.ToString() || job.primaryStage != "FAMILY_PUBLICATION") return false;
                if (acceptedRuntime)
                {
                    if (job.reasonCode != "OVERHEAD_RUNTIME_ACCEPTANCE_PENDING" || job.lastAutomaticOverheadAcceptanceFingerprint == fingerprint) return false;
                    job.lastAutomaticOverheadAcceptanceFingerprint = fingerprint;
                }
                else
                {
                    if ((job.reasonCode != "EQUIPMENT_FUNCTION_AMBIGUOUS" && job.reasonCode != "PLACEMENT_OVERHEAD_REQUIRES_ADAPTER" &&
                        job.reasonCode != "OVERHEAD_AUTHORING_REVIEW" && job.reasonCode != "OVERHEAD_COMMON_PLAN_REVIEW") ||
                        job.lastAutomaticOverheadPlannerFingerprint == fingerprint) return false;
                    job.lastAutomaticOverheadPlannerFingerprint = fingerprint;
                }
                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = acceptedRuntime ? "VERIFIED_OVERHEAD_RUNTIME_ACCEPTANCE" : "VERIFIED_OVERHEAD_PLAN_RETRY";
                job.message = "Verified passive overhead source queued for the canonical family publication stage.";
                job.processingStartedUtc = job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString(); job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O"); snapshot.schedulerGeneration++; Save();
            }
            NotifyChanged(); return true;
        }

        internal bool RetryVerifiedPublishedBarAuthoring(string jobId, string savicId,
            string sourceHash, string authoringFingerprint)
        {
            if (string.IsNullOrWhiteSpace(authoringFingerprint)) return false;
            lock (sync)
            {
                EnsureLoaded();
                var job = snapshot.jobs.FirstOrDefault(candidate => candidate?.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    job.manifestSavicId != savicId || job.sourceHash != sourceHash ||
                    job.state != SavicJobState.Done.ToString() || job.reasonCode != "PUBLISHED" ||
                    job.lastAutomaticBarAuthoringFingerprint == authoringFingerprint) return false;
                job.lastAutomaticBarAuthoringFingerprint = authoringFingerprint;
                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_BAR_AUTHORING_UPDATE";
                job.message = "Verified published bar queued for current canonical function authoring and renewed runtime acceptance.";
                job.processingStartedUtc = job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++; Save();
            }
            NotifyChanged(); return true;
        }

        internal bool RetryVerifiedChairAuthoringReview(
            string jobId, string savicId, string sourceHash,
            string automaticPlannerVersion = "")
        {
            bool changed = false;
            lock (sync)
            {
                EnsureLoaded();
                SavicJobRecord job = snapshot.jobs.FirstOrDefault(candidate =>
                    candidate != null && candidate.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    !string.Equals(job.manifestSavicId, savicId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.sourceHash, sourceHash, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.state, SavicJobState.NeedsReview.ToString(), StringComparison.Ordinal) ||
                    !string.Equals(job.reasonCode, "CHAIR_AUTHORING_REVIEW", StringComparison.Ordinal) ||
                    !string.Equals(job.primaryStage, "FAMILY_PUBLICATION", StringComparison.Ordinal) ||
                    (!string.IsNullOrEmpty(automaticPlannerVersion) &&
                     string.Equals(job.lastAutomaticChairPlannerRetryVersion,
                         automaticPlannerVersion, StringComparison.Ordinal)))
                    return false;

                if (!string.IsNullOrEmpty(automaticPlannerVersion))
                    job.lastAutomaticChairPlannerRetryVersion = automaticPlannerVersion;
                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_CHAIR_PLAN_RETRY";
                job.message = "Verified source queued after the current chair planner accepted a safe calibration.";
                job.processingStartedUtc = string.Empty;
                job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++;
                Save();
                changed = true;
            }

            if (changed)
                NotifyChanged();
            return changed;
        }

        internal bool RetryVerifiedTableAuthoringReview(
            string jobId, string savicId, string sourceHash,
            string plannerVersion)
        {
            if (string.IsNullOrWhiteSpace(plannerVersion))
                throw new ArgumentException("Planner version is required.", nameof(plannerVersion));

            bool changed = false;
            lock (sync)
            {
                EnsureLoaded();
                SavicJobRecord job = snapshot.jobs.FirstOrDefault(candidate =>
                    candidate != null && candidate.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    !string.Equals(job.manifestSavicId, savicId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.sourceHash, sourceHash, StringComparison.OrdinalIgnoreCase) ||
                    job.state != SavicJobState.NeedsReview.ToString() ||
                    job.reasonCode != "FAMILY_PUBLICATION_FAILED" ||
                    job.primaryStage != "FAMILY_PUBLICATION" ||
                    job.message != "Normalized dimensions are not safe for an automatic table profile." ||
                    string.Equals(job.lastAutomaticTablePlannerRetryVersion,
                        plannerVersion, StringComparison.Ordinal))
                    return false;

                job.lastAutomaticTablePlannerRetryVersion = plannerVersion;
                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_TABLE_PLAN_RETRY";
                job.message = "Verified compact square table queued under the current planner revision.";
                job.processingStartedUtc = string.Empty;
                job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++;
                Save();
                changed = true;
            }

            if (changed)
                NotifyChanged();
            return changed;
        }

        // A changed seating profile may require republishing a verified table.
        // The publisher preserves catalog identity and manual item values, and
        // the planner version prevents a reload from repeating the operation.
        internal bool RetryVerifiedPublishedTableProfile(
            string jobId, string savicId, string sourceHash,
            string plannerVersion)
        {
            if (string.IsNullOrWhiteSpace(plannerVersion))
                throw new ArgumentException("Planner version is required.", nameof(plannerVersion));

            bool changed = false;
            lock (sync)
            {
                EnsureLoaded();
                SavicJobRecord job = snapshot.jobs.FirstOrDefault(candidate =>
                    candidate != null && candidate.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    !string.Equals(job.manifestSavicId, savicId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.sourceHash, sourceHash, StringComparison.OrdinalIgnoreCase) ||
                    job.state != SavicJobState.Done.ToString() ||
                    job.reasonCode != "PUBLISHED" ||
                    string.Equals(job.lastAutomaticTablePlannerRetryVersion,
                        plannerVersion, StringComparison.Ordinal))
                    return false;

                job.lastAutomaticTablePlannerRetryVersion = plannerVersion;
                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_TABLE_PROFILE_UPDATE";
                job.message = "Verified published table queued once for a corrected seating profile.";
                job.processingStartedUtc = string.Empty;
                job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++;
                Save();
                changed = true;
            }

            if (changed)
                NotifyChanged();
            return changed;
        }

        // A null graphics device can make an otherwise valid chair preview
        // fail. Retry only that exact failure once for this renderer revision.
        internal bool RetryVerifiedChairPreviewFailure(
            string jobId, string savicId, string sourceHash,
            string rendererVersion)
        {
            if (string.IsNullOrWhiteSpace(rendererVersion))
                throw new ArgumentException("Renderer version is required.", nameof(rendererVersion));

            bool changed = false;
            lock (sync)
            {
                EnsureLoaded();
                SavicJobRecord job = snapshot.jobs.FirstOrDefault(candidate =>
                    candidate != null && candidate.jobId == jobId);
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    !string.Equals(job.manifestSavicId, savicId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(job.sourceHash, sourceHash, StringComparison.OrdinalIgnoreCase) ||
                    job.state != SavicJobState.NeedsReview.ToString() ||
                    job.reasonCode != "CHAIR_PUBLICATION_FAILED" ||
                    job.primaryStage != "FAMILY_PUBLICATION" ||
                    (job.message ?? string.Empty).IndexOf(
                        "Rendered preview appears blank or visually degenerate.",
                        StringComparison.Ordinal) < 0 ||
                    string.Equals(job.lastAutomaticChairPreviewRetryVersion,
                        rendererVersion, StringComparison.Ordinal))
                    return false;

                job.lastAutomaticChairPreviewRetryVersion = rendererVersion;
                job.state = SavicJobState.Ingested.ToString();
                job.checkpoint = "VERIFIED_CHAIR_PREVIEW_RETRY";
                job.message = "Verified chair queued once after a graphics device became available.";
                job.processingStartedUtc = string.Empty;
                job.completedUtc = string.Empty;
                job.preparationStage = SavicSourcePreparationStage.None.ToString();
                job.sourcePrepared = false;
                job.updatedUtc = DateTime.UtcNow.ToString("O");
                snapshot.schedulerGeneration++;
                Save();
                changed = true;
            }

            if (changed)
                NotifyChanged();
            return changed;
        }

        internal bool TryClaimNextProcessable(
            out SavicJobRecord claimed)
        {
            claimed = null;
            bool changed = false;
            string now = DateTime.UtcNow.ToString("O");

            lock (sync)
            {
                EnsureLoaded();

                if (snapshot.paused)
                    return false;

                for (int index = 0;
                     index < snapshot.jobs.Count;
                     index++)
                {
                    SavicJobRecord job = snapshot.jobs[index];

                    if (job == null ||
                        !job.batchEligible ||
                        !string.Equals(
                            job.state,
                            SavicJobState.Ingested.ToString(),
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (job.cancelRequested)
                    {
                        job.state =
                            SavicJobState.Cancelled.ToString();
                        job.checkpoint = "CANCELLED";
                        job.completedUtc = now;
                        job.updatedUtc = now;
                        changed = true;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(
                            job.manifestSavicId))
                    {
                        job.state =
                            SavicJobState.FailedProcessing.ToString();
                        job.checkpoint = "FAILED_NO_MANIFEST";
                        job.completedUtc = now;
                        job.updatedUtc = now;
                        job.message =
                            "Queue record has no SAVIC manifest identity.";
                        changed = true;
                        continue;
                    }

                    job.state =
                        SavicJobState.Processing.ToString();
                    job.checkpoint = "PROCESSING";
                    job.processingStartedUtc = now;
                    job.updatedUtc = now;
                    job.completedUtc = string.Empty;
                    claimed = job;
                    changed = true;
                    break;
                }

                if (changed)
                {
                    snapshot.schedulerGeneration++;
                    Save();
                }
            }

            if (changed)
                NotifyChanged();

            return claimed != null;
        }

        internal void YieldPreparationStage(
            string jobId,
            SavicSourceProcessingOutcome outcome,
            SavicSourcePreparationStage preparationStage,
            long durationMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                throw new ArgumentException(
                    "JobId is required.",
                    nameof(jobId));

            string now =
                DateTime.UtcNow.ToString("O");

            lock (sync)
            {
                EnsureLoaded();

                SavicJobRecord job =
                    snapshot.jobs.FirstOrDefault(
                        candidate =>
                            candidate != null &&
                            string.Equals(
                                candidate.jobId,
                                jobId,
                                StringComparison.Ordinal));

                if (job == null)
                {
                    throw new InvalidOperationException(
                        "Queue job no longer exists: " +
                        jobId);
                }

                if (job.cancelRequested)
                {
                    job.state =
                        SavicJobState.Cancelled.ToString();

                    job.checkpoint =
                        "CANCELLED_AFTER_ATOMIC_OPERATION";

                    job.completedUtc =
                        now;

                    job.message =
                        "Cancellation completed after the current source-preparation stage.";
                }
                else
                {
                    job.state =
                        SavicJobState.Ingested.ToString();

                    job.preparationStage =
                        preparationStage.ToString();

                    job.sourcePrepared =
                        preparationStage ==
                        SavicSourcePreparationStage.SourceImported;

                    job.checkpoint =
                        preparationStage ==
                        SavicSourcePreparationStage.MirrorMaterialized
                            ? "MIRROR_MATERIALIZED"
                            : "SOURCE_PREPARED";

                    job.completedUtc =
                        string.Empty;

                    job.message =
                        string.IsNullOrWhiteSpace(
                            outcome.Message)
                            ? "Source preparation stage completed."
                            : outcome.Message;
                }

                job.processingStartedUtc =
                    string.Empty;

                job.updatedUtc =
                    now;

                long safeDuration =
                    Math.Max(
                        0L,
                        durationMilliseconds);

                job.lastDurationMilliseconds =
                    Math.Max(
                        0L,
                        job.lastDurationMilliseconds) +
                    safeDuration;

                job.maximumAtomicDurationMilliseconds =
                    Math.Max(
                        job.maximumAtomicDurationMilliseconds,
                        safeDuration);

                ApplyOutcomeDiagnostics(
                    job,
                    outcome,
                    true);

                snapshot.schedulerGeneration++;
                Save();
            }

            NotifyChanged();
        }

        internal void CompleteProcessing(
            string jobId,
            SavicSourceProcessingOutcome outcome,
            long durationMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                throw new ArgumentException(
                    "JobId is required.",
                    nameof(jobId));

            string now = DateTime.UtcNow.ToString("O");

            lock (sync)
            {
                EnsureLoaded();

                SavicJobRecord job =
                    snapshot.jobs.FirstOrDefault(
                        candidate =>
                            candidate != null &&
                            string.Equals(
                                candidate.jobId,
                                jobId,
                                StringComparison.Ordinal));

                if (job == null)
                {
                    throw new InvalidOperationException(
                        "Queue job no longer exists: " + jobId);
                }

                if (job.cancelRequested)
                {
                    job.state =
                        SavicJobState.Cancelled.ToString();
                    job.checkpoint = "CANCELLED_AFTER_ATOMIC_OPERATION";
                    job.message =
                        "Cancellation completed after the current atomic asset operation.";
                }
                else if (outcome.Succeeded)
                {
                    job.state =
                        SavicJobState.Done.ToString();
                    job.checkpoint = "DONE";
                    job.message = outcome.Message;
                }
                else if (string.Equals(
                             outcome.Status,
                             "NEEDS_REVIEW",
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(
                             outcome.Status,
                             "UNSUPPORTED_SOURCE_KIND",
                             StringComparison.OrdinalIgnoreCase))
                {
                    job.state =
                        SavicJobState.NeedsReview.ToString();
                    job.checkpoint = "NEEDS_REVIEW";
                    job.message = outcome.Message;
                }
                else
                {
                    job.state =
                        SavicJobState.FailedProcessing.ToString();
                    job.checkpoint = "FAILED_PROCESSING";
                    job.message = outcome.Message;
                }

                job.processingStartedUtc = string.Empty;
                job.completedUtc = now;
                job.updatedUtc = now;
                long safeDuration =
                    Math.Max(
                        0L,
                        durationMilliseconds);

                job.lastDurationMilliseconds =
                    Math.Max(
                        0L,
                        job.lastDurationMilliseconds) +
                    safeDuration;

                job.maximumAtomicDurationMilliseconds =
                    Math.Max(
                        job.maximumAtomicDurationMilliseconds,
                        safeDuration);

                ApplyOutcomeDiagnostics(
                    job,
                    outcome,
                    !string.IsNullOrWhiteSpace(
                        job.preparationStage) &&
                    !string.Equals(
                        job.preparationStage,
                        SavicSourcePreparationStage.None.ToString(),
                        StringComparison.Ordinal));

                snapshot.schedulerGeneration++;
                Save();
            }

            NotifyChanged();
        }

        private static void ApplyOutcomeDiagnostics(
            SavicJobRecord job,
            SavicSourceProcessingOutcome outcome,
            bool appendStages)
        {
            if (job == null)
                return;

            job.outcomeStatus =
                outcome.Status ?? string.Empty;

            SavicProcessingDiagnostics diagnostics =
                outcome.Diagnostics;

            if (diagnostics != null)
            {
                job.reasonCode =
                    string.IsNullOrWhiteSpace(
                        diagnostics.reasonCode)
                        ? outcome.Succeeded
                            ? "PUBLISHED"
                            : string.IsNullOrWhiteSpace(
                                  outcome.Status)
                                ? "UNCLASSIFIED_OUTCOME"
                                : outcome.Status
                                      .Trim()
                                      .ToUpperInvariant()
                        : diagnostics.reasonCode;

                job.primaryStage =
                    string.IsNullOrWhiteSpace(
                        diagnostics.primaryStage)
                        ? "BATCH"
                        : diagnostics.primaryStage;

                List<SavicProcessingStageRecord> incoming =
                    diagnostics.stages == null
                        ? new List<SavicProcessingStageRecord>()
                        : diagnostics.stages
                            .Where(stage => stage != null)
                            .Select(
                                stage =>
                                    new SavicProcessingStageRecord
                                    {
                                        stageId =
                                            stage.stageId ?? string.Empty,
                                        result =
                                            stage.result ?? string.Empty,
                                        durationMilliseconds =
                                            Math.Max(
                                                0L,
                                                stage.durationMilliseconds),
                                        detail =
                                            stage.detail ?? string.Empty
                                    })
                            .ToList();

                if (!appendStages ||
                    job.stageTimings == null)
                {
                    job.stageTimings =
                        incoming;
                }
                else
                {
                    job.stageTimings.AddRange(
                        incoming);
                }

                return;
            }

            job.reasonCode =
                outcome.Succeeded
                    ? "PUBLISHED"
                    : "UNCLASSIFIED_OUTCOME";

            job.primaryStage =
                "BATCH";

            if (!appendStages)
            {
                job.stageTimings =
                    new List<SavicProcessingStageRecord>();
            }
        }

        internal int CountByState(SavicJobState state)
        {
            lock (sync)
            {
                EnsureLoaded();
                string value = state.ToString();
                return snapshot.jobs.Count(job =>
                    string.Equals(job.state, value, StringComparison.Ordinal));
            }
        }

        internal int PendingProcessCount
        {
            get
            {
                lock (sync)
                {
                    EnsureLoaded();

                    return snapshot.jobs.Count(
                        job =>
                            job != null &&
                            job.batchEligible &&
                            (string.Equals(
                                 job.state,
                                 SavicJobState.Ingested.ToString(),
                                 StringComparison.Ordinal) ||
                             string.Equals(
                                 job.state,
                                 SavicJobState.Processing.ToString(),
                                 StringComparison.Ordinal)));
                }
            }
        }

        internal void Reload()
        {
            lock (sync)
            {
                snapshot = null;
                EnsureLoaded();
            }

            NotifyChanged();
        }

        private void NotifyChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SAVIC] Job change listener failed safely: " +
                    exception);
            }
        }

        private void EnsureLoaded()
        {
            if (snapshot != null)
                return;

            layout.EnsureInfrastructure();
            snapshot = SavicAtomicFile.ReadJson<SavicQueueSnapshot>(layout.QueueSnapshotPath)
                ?? new SavicQueueSnapshot();

            snapshot.jobs ??= new List<SavicJobRecord>();

            for (int index = 0;
                 index < snapshot.jobs.Count;
                 index++)
            {
                SavicJobRecord job =
                    snapshot.jobs[index];

                if (job == null)
                    continue;

                if (string.IsNullOrWhiteSpace(
                        job.preparationStage))
                {
                    job.preparationStage =
                        job.sourcePrepared
                            ? SavicSourcePreparationStage
                                .SourceImported
                                .ToString()
                            : SavicSourcePreparationStage
                                .None
                                .ToString();
                }

                job.sourcePrepared =
                    string.Equals(
                        job.preparationStage,
                        SavicSourcePreparationStage
                            .SourceImported
                            .ToString(),
                        StringComparison.Ordinal);
            }
        }

        private void Save()
        {
            snapshot.schemaVersion = SavicVersion.QueueSchemaVersion;
            snapshot.savedUtc = DateTime.UtcNow.ToString("O");
            SavicAtomicFile.WriteJson(layout.QueueSnapshotPath, snapshot);
        }

        private static bool IsTerminal(string state)
        {
            return string.Equals(
                       state,
                       SavicJobState.Done.ToString(),
                       StringComparison.Ordinal) ||
                   string.Equals(
                       state,
                       SavicJobState.DuplicateExact.ToString(),
                       StringComparison.Ordinal) ||
                   string.Equals(
                       state,
                       SavicJobState.FailedSource.ToString(),
                       StringComparison.Ordinal) ||
                   string.Equals(
                       state,
                       SavicJobState.Quarantined.ToString(),
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

        private void TrimHistory()
        {
            const int maximumRecords = 5000;

            if (snapshot.jobs.Count <= maximumRecords)
                return;

            int toRemove =
                snapshot.jobs.Count -
                maximumRecords;

            int index = 0;

            while (toRemove > 0 &&
                   index < snapshot.jobs.Count)
            {
                SavicJobRecord job =
                    snapshot.jobs[index];

                if (job == null ||
                    IsTerminal(job.state))
                {
                    snapshot.jobs.RemoveAt(index);
                    toRemove--;
                    continue;
                }

                index++;
            }

            // Never discard active work just to satisfy the history cap.
            // If more than 5,000 records are simultaneously non-terminal,
            // the queue is allowed to exceed the nominal history limit.
        }
    }
}
