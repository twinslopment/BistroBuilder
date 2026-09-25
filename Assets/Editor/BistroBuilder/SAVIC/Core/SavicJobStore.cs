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
                job.lastDurationMilliseconds =
                    Math.Max(0L, durationMilliseconds);
                snapshot.schedulerGeneration++;
                Save();
            }

            NotifyChanged();
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

            snapshot.jobs.RemoveRange(
                0,
                snapshot.jobs.Count - maximumRecords);
        }
    }
}
