using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

            lock (sync)
            {
                EnsureLoaded();
                string now = DateTime.UtcNow.ToString("O");

                SavicJobRecord record = new SavicJobRecord
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
                    message = message ?? string.Empty
                };

                snapshot.jobs.Add(record);
                TrimHistory();
                Save();
                return record;
            }
        }

        internal SavicJobRecord RecordFailure(
            string originalFileName,
            string sourceHash,
            string archivedRelativePath,
            SavicJobState state,
            int attempts,
            string message)
        {
            lock (sync)
            {
                EnsureLoaded();
                string now = DateTime.UtcNow.ToString("O");

                SavicJobRecord record = new SavicJobRecord
                {
                    jobId = Guid.NewGuid().ToString("N"),
                    state = state.ToString(),
                    sourceHash = sourceHash ?? string.Empty,
                    originalFileName = originalFileName ?? string.Empty,
                    archivedRelativePath = archivedRelativePath ?? string.Empty,
                    createdUtc = now,
                    updatedUtc = now,
                    attempts = Math.Max(1, attempts),
                    message = message ?? string.Empty
                };

                snapshot.jobs.Add(record);
                TrimHistory();
                Save();
                return record;
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

        internal void Reload()
        {
            lock (sync)
            {
                snapshot = null;
                EnsureLoaded();
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
